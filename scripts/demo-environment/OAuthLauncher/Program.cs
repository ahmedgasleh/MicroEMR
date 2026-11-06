using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace MicroEMR.DemoOAuth;

public static class OAuthFlow
{
    public const string Authority = "https://localhost:7179";
    public const string ClientId = "microemr_demo_setup";
    public const string Redirect = "http://127.0.0.1:8407/callback/";
    public const string Scope = "microemr_api";
    public const string Subject = "56c3a21b-3207-4954-a970-f32f1feb2ac2";
    public const string TenantUid = "f94c6ecd-4417-483e-8a94-0507cf8384c2";
    public static string RandomValue() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
    public static string Challenge(string verifier) => Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    public static string Callback(string target, string expectedState)
    {
        if (!target.StartsWith("/callback/?", StringComparison.Ordinal) || target.Contains('#') || target.Length > 8192)
            throw new InvalidOperationException("Invalid callback.");
        var query = QueryHelpers.ParseQuery(target[target.IndexOf('?')..]);
        if ((query.Count != 2 && query.Count != 3) ||
            query.Keys.Any(key => key is not ("code" or "state" or "iss")) ||
            (query.Count == 3 && (!query.TryGetValue("iss", out var issuer) || issuer.Count != 1 || issuer.ToString() != Authority + "/")) ||
            !query.TryGetValue("state", out var state) || state.Count != 1 ||
            !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(state.ToString()), Encoding.UTF8.GetBytes(expectedState)) ||
            !query.TryGetValue("code", out var code) || code.Count != 1 || string.IsNullOrWhiteSpace(code) || code.ToString().Length > 4096)
            throw new InvalidOperationException("Authorization response rejected.");
        return code.ToString();
    }

    public static Uri Endpoint(string value)
    {
        var uri = new Uri(value, UriKind.Absolute);
        if (uri.Scheme != "https" || uri.GetLeftPart(UriPartial.Authority) != Authority ||
            uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0)
            throw new InvalidOperationException("Untrusted discovery endpoint.");
        return uri;
    }

    public static string TokenResponse(JsonElement response)
    {
        if (response.TryGetProperty("error", out _) || response.TryGetProperty("refresh_token", out _) ||
            !response.TryGetProperty("token_type", out var type) || !string.Equals(type.GetString(), "Bearer", StringComparison.OrdinalIgnoreCase) ||
            !response.TryGetProperty("access_token", out var token) || token.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(token.GetString()) || token.GetString()!.Length > 32768 ||
            !response.TryGetProperty("expires_in", out var expiry) || !expiry.TryGetInt32(out var seconds) || seconds <= 0 ||
            (response.TryGetProperty("scope", out var scope) && (scope.ValueKind != JsonValueKind.String || scope.GetString() != Scope)))
            throw new InvalidOperationException("Token response rejected.");
        return token.GetString()!;
    }

    public static async Task ValidateToken(string token, IEnumerable<SecurityKey> keys)
    {
        var result = await new JsonWebTokenHandler { MapInboundClaims = false }.ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = Authority + "/",
            ValidateAudience = true, ValidAudience = Scope,
            ValidateLifetime = true, RequireExpirationTime = true, ClockSkew = TimeSpan.FromSeconds(15),
            RequireSignedTokens = true, ValidateIssuerSigningKey = true, IssuerSigningKeys = keys,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256]
        });
        if (!result.IsValid || result.ClaimsIdentity.FindFirst("scope")?.Value != Scope ||
            result.ClaimsIdentity.FindFirst("sub")?.Value != Subject ||
            ((JsonWebToken)result.SecurityToken).ValidTo <= DateTime.UtcNow.AddSeconds(30))
            throw new InvalidOperationException("Access token validation failed.");
    }

    public static async Task Authorize(HttpClient http, Func<Uri, Task<string>> receive, Func<string, Task> handoff,
        CancellationToken cancellationToken)
    {
        using var discovery = await GetJson(http, new Uri(Authority + "/.well-known/openid-configuration"), cancellationToken);
        var metadata = discovery.RootElement;
        // OpenIddict's issuer includes a trailing slash. Do not normalize arbitrary issuers.
        if (metadata.GetProperty("issuer").GetString() != Authority + "/" ||
            !metadata.GetProperty("code_challenge_methods_supported").EnumerateArray().Any(x => x.GetString() == "S256"))
            throw new InvalidOperationException("Discovery rejected.");
        var authorize = Endpoint(metadata.GetProperty("authorization_endpoint").GetString()!);
        var tokenEndpoint = Endpoint(metadata.GetProperty("token_endpoint").GetString()!);
        var jwks = Endpoint(metadata.GetProperty("jwks_uri").GetString()!);
        string? verifier = RandomValue(), state = RandomValue(), code = null, token = null;
        try
        {
            var url = QueryHelpers.AddQueryString(authorize.ToString(), new Dictionary<string, string?>
            {
                ["client_id"] = ClientId, ["redirect_uri"] = Redirect, ["response_type"] = "code",
                ["scope"] = Scope, ["state"] = state, ["code_challenge"] = Challenge(verifier), ["code_challenge_method"] = "S256"
            });
            // OAuth only: no openid scope or ID token, so no OIDC nonce is applicable.
            code = Callback(await receive(new Uri(url)), state);
            using var body = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code", ["client_id"] = ClientId,
                ["redirect_uri"] = Redirect, ["code"] = code, ["code_verifier"] = verifier
            });
            using var response = await http.PostAsync(tokenEndpoint, body, cancellationToken);
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Token exchange failed.");
            using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            token = TokenResponse(payload.RootElement);
            using var keyDocument = await GetJson(http, jwks, cancellationToken);
            await ValidateToken(token, new JsonWebKeySet(keyDocument.RootElement.GetRawText()).GetSigningKeys());
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://localhost:7003/api/context/tenant");
            request.Headers.Authorization = new("Bearer", token);
            using var tenantResponse = await http.SendAsync(request, cancellationToken);
            if (!tenantResponse.IsSuccessStatusCode) throw new InvalidOperationException("API session rejected.");
            using var tenant = JsonDocument.Parse(await tenantResponse.Content.ReadAsStringAsync(cancellationToken));
            if (tenant.RootElement.GetProperty("tenantUid").GetString() != TenantUid ||
                tenant.RootElement.GetProperty("tenantKey").GetString() != "local-dev-fresh")
                throw new InvalidOperationException("Wrong tenant.");
            await handoff(token);
        }
        finally { verifier = state = code = token = null; }
    }

    private static async Task<JsonDocument> GetJson(HttpClient http, Uri uri, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(uri, cancellationToken);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Metadata request failed.");
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
    }
}

public sealed class LoopbackCallback : IDisposable
{
    private readonly TcpListener listener = new(IPAddress.Loopback, 8407);
    public async Task<string> Receive(Uri authorization, Action<Uri> openBrowser, TimeSpan timeout)
    {
        using var deadline = new CancellationTokenSource(timeout);
        listener.Start(1); // Exclusive bind; never reuse a busy port or choose an unregistered port.
        try
        {
            openBrowser(authorization);
            using var connection = await listener.AcceptTcpClientAsync(deadline.Token);
            listener.Stop(); // Single-use, including an invalid response.
            using var stream = connection.GetStream();
            var bytes = new List<byte>();
            var next = new byte[1];
            while (bytes.Count < 8192)
            {
                if (await stream.ReadAsync(next, deadline.Token) == 0) break;
                bytes.Add(next[0]);
                if (bytes.Count >= 4 && bytes.TakeLast(4).SequenceEqual(new byte[] { 13, 10, 13, 10 })) break;
            }
            var headers = Encoding.ASCII.GetString(bytes.ToArray()).Split("\r\n");
            var request = headers[0].Split(' ');
            if (bytes.Count >= 8192 || headers.Length < 4 || headers[^1] != "" || headers[^2] != "" ||
                request.Length != 3 || request[0] != "GET" || request[2] != "HTTP/1.1" ||
                headers.Count(x => x.StartsWith("Host:", StringComparison.OrdinalIgnoreCase)) != 1 ||
                !headers.Any(x => x.Equals("Host: 127.0.0.1:8407", StringComparison.OrdinalIgnoreCase)) ||
                headers.Any(x => x.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase) || x.StartsWith("Transfer-Encoding:", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Invalid loopback request.");
            // No reflection of query values in the browser response.
            const string message = "Authorization received. Return to the launcher for the result.\r\n";
            var reply = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nCache-Control: no-store\r\nConnection: close\r\nContent-Length: {Encoding.ASCII.GetByteCount(message)}\r\n\r\n{message}");
            await stream.WriteAsync(reply, deadline.Token);
            return request[1];
        }
        finally { listener.Stop(); }
    }
    public void Dispose() => listener.Stop();
}

public static class Preparation
{
    public static async Task WaitForPreparationExit(Process process, CancellationToken cancellationToken = default)
    {
        // API restart descendants can retain redirected pipe handles after preparation exits.
        // Wait for the preparation process itself, rather than waiting for pipe EOF.
        while (!process.HasExited) await Task.Delay(100, cancellationToken);
    }

    public static ProcessStartInfo StartInfo(string script, string phase, string token)
    {
        if (phase is not ("Stage" or "Activate" or "Seed" or "Verify" or "Cutback")) throw new InvalidOperationException("Invalid phase.");
        var start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"))
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-File", script, "-Phase", phase }) start.ArgumentList.Add(argument);
        start.Environment["MICROEMR_DEMO_BEARER"] = token;
        return start;
    }

    public static async Task Run(string script, string phase, string token)
    {
        var start = StartInfo(script, phase, token);
        using var process = new Process { StartInfo = start };
        // Suppress raw subprocess diagnostics: authentication material must not reach console logs.
        process.OutputDataReceived += (_, _) => { };
        process.ErrorDataReceived += (_, _) => { };
        try
        {
            process.Start();
            start.Environment.Remove("MICROEMR_DEMO_BEARER");
            process.BeginOutputReadLine(); process.BeginErrorReadLine();
            await WaitForPreparationExit(process);
            process.CancelOutputRead(); process.CancelErrorRead();
            if (process.ExitCode != 0) throw new InvalidOperationException("Preparation stopped; inspect sanitized phase receipts.");
        }
        finally { start.Environment.Remove("MICROEMR_DEMO_BEARER"); }
    }
}

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var preparationStarted = false;
        try
        {
            if (args.Length != 2 || args[0] is not ("Authenticate" or "Stage" or "Activate" or "Seed" or "Verify" or "Cutback"))
                throw new InvalidOperationException("Invalid launcher arguments.");
            var phase = args[0];
            var script = Path.GetFullPath(args[1]);
            if (!File.Exists(script) || Path.GetFileName(script) != "Prepare-DemoEnvironment.ps1") throw new InvalidOperationException("Preparation script missing.");
            if (phase != "Authenticate" && Environment.GetEnvironmentVariable("MICROEMR_DEMO_EXECUTION_CONSENT") != "OWNER_APPROVED_DEMO_V1_" + phase.ToUpperInvariant())
                throw new InvalidOperationException("Phase consent missing.");
            using var handler = new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false };
            using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30), MaxResponseContentBufferSize = 1024 * 1024 };
            using var callback = new LoopbackCallback();
            await OAuthFlow.Authorize(http, uri => callback.Receive(uri,
                url => Process.Start(new ProcessStartInfo(url.AbsoluteUri) { UseShellExecute = true }), TimeSpan.FromMinutes(3)),
                token =>
                {
                    if (phase == "Authenticate") return Task.CompletedTask;
                    preparationStarted = true;
                    return Preparation.Run(script, phase, token);
                }, CancellationToken.None);
            Console.WriteLine(phase == "Authenticate" ? "Authentication and tenant validated. No preparation phase executed; token discarded." : "Preparation phase completed.");
            return 0;
        }
        catch
        {
            // Never emit exception objects/messages from OAuth, HTTP, browser or JWT libraries.
            Console.Error.WriteLine(preparationStarted
                ? "Authorization and tenant validated; preparation failed. Inspect .tmp/demo-environment-v1/phase-failure.json and phase receipts. No credential details logged."
                : "Launcher stopped safely. Check local Auth activation, approved actor/tenant, TLS trust, callback port, timeout and phase consent. No credential details logged.");
            return 1;
        }
    }
}
