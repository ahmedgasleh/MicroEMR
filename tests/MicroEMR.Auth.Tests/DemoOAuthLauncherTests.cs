using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using MicroEMR.Auth.Data;
using MicroEMR.DemoOAuth;
using OpenIddict.Abstractions;
using Xunit;

namespace MicroEMR.Auth.Tests;

public sealed class DemoOAuthLauncherTests
{
    [Fact]
    public async Task PreparationExitDoesNotWaitForRestartedDescendantToCloseOutput()
    {
        if (!OperatingSystem.IsWindows()) return;
        var powershell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");
        var start = new System.Diagnostics.ProcessStartInfo(powershell)
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-NonInteractive");
        start.ArgumentList.Add("-Command");
        start.ArgumentList.Add("$s=[Diagnostics.ProcessStartInfo]::new((Join-Path $PSHOME 'powershell.exe'));$s.UseShellExecute=$false;$s.Arguments='-NoProfile -NonInteractive -Command Start-Sleep -Seconds 30';$p=[Diagnostics.Process]::Start($s);[Console]::Out.WriteLine($p.Id);exit 0");
        using var process = System.Diagnostics.Process.Start(start)!;
        System.Diagnostics.Process? descendant = null;
        try
        {
            var id = await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(15));
            descendant = System.Diagnostics.Process.GetProcessById(int.Parse(id!));
            var outputEnd = process.StandardOutput.ReadToEndAsync();
            await Preparation.WaitForPreparationExit(process).WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(0, process.ExitCode);
            Assert.False(descendant.HasExited);
            Assert.False(outputEnd.IsCompleted);
        }
        finally
        {
            if (descendant is not null)
            {
                if (!descendant.HasExited) descendant.Kill();
                descendant.Dispose();
            }
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
    }

    [Fact]
    public void ClientIsSeparatePublicCodeOnlyWithExactRedirectAndDevelopmentGate()
    {
        var descriptor = DemoSetupClient.Descriptor();
        Assert.Equal(OAuthFlow.ClientId, descriptor.ClientId);
        Assert.Equal(OpenIddictConstants.ClientTypes.Public, descriptor.ClientType);
        Assert.Null(descriptor.ClientSecret);
        Assert.Equal([new Uri(OAuthFlow.Redirect)], descriptor.RedirectUris);
        Assert.Contains(OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange, descriptor.Requirements);
        Assert.Equal(5, descriptor.Permissions.Count);
        Assert.Contains(OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode, descriptor.Permissions);
        Assert.DoesNotContain(OpenIddictConstants.Permissions.GrantTypes.RefreshToken, descriptor.Permissions);
        var enabled = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["DemoSetupClient:Enabled"] = "true", ["urls"] = "https://localhost:7179;http://localhost:5080" }).Build();
        Assert.True(DemoSetupClient.Enabled(new Host { EnvironmentName = "Development" }, enabled));
        Assert.False(DemoSetupClient.Enabled(new Host { EnvironmentName = "Production" }, enabled));
        Assert.False(DemoSetupClient.Enabled(new Host { EnvironmentName = "Development" }, new ConfigurationBuilder().Build()));
        enabled["urls"] = "https://0.0.0.0:7179";
        Assert.False(DemoSetupClient.Enabled(new Host { EnvironmentName = "Development" }, enabled));
        Assert.True(DemoSetupClient.AcceptAuthorization(true, OAuthFlow.Redirect, "S256", OAuthFlow.Scope));
        Assert.False(DemoSetupClient.AcceptAuthorization(true, OAuthFlow.Redirect, "plain", OAuthFlow.Scope));
        Assert.False(DemoSetupClient.AcceptAuthorization(true, "http://127.0.0.1:8408/callback/", "S256", OAuthFlow.Scope));
        Assert.False(DemoSetupClient.AcceptAuthorization(true, OAuthFlow.Redirect, "S256", "microemr_api offline_access"));
        Assert.False(DemoSetupClient.AcceptAuthorization(false, OAuthFlow.Redirect, "S256", OAuthFlow.Scope));
    }

    [Fact]
    public void PkceMatchesRfc7636VectorAndRandomValuesAreIndependent()
    {
        Assert.Equal("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM", OAuthFlow.Challenge("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk"));
        var first = OAuthFlow.RandomValue();
        Assert.Equal(43, first.Length);
        Assert.NotEqual(first, OAuthFlow.RandomValue());
    }

    [Theory]
    [InlineData("/callback/?code=synthetic&state=wrong")]
    [InlineData("/callback/?code=synthetic&state=expected&state=expected")]
    [InlineData("/callback/?code=synthetic&code=other&state=expected")]
    [InlineData("/callback/?error=access_denied&state=expected")]
    [InlineData("/callback/?code=&state=expected")]
    [InlineData("/wrong/?code=synthetic&state=expected")]
    [InlineData("/callback/?code=synthetic&state=expected#fragment")]
    [InlineData("http://evil.invalid/callback/?code=synthetic&state=expected")]
    [InlineData("/callback/?code=synthetic&state=expected&iss=https%3A%2F%2Fevil.invalid%2F")]
    [InlineData("/callback/?code=synthetic&STATE=expected")]
    public void InvalidCallbacksAreRejected(string target) =>
        Assert.Throws<InvalidOperationException>(() => OAuthFlow.Callback(target, "expected"));

    [Fact]
    public void CorrectCallbackAccepted() => Assert.Equal("synthetic", OAuthFlow.Callback("/callback/?code=synthetic&state=expected", "expected"));

    [Fact]
    public void AuthorizationResponseIssuerIsValidatedWhenPresent() => Assert.Equal("synthetic",
        OAuthFlow.Callback("/callback/?code=synthetic&state=expected&iss=https%3A%2F%2Flocalhost%3A7179%2F", "expected"));

    [Fact]
    public async Task ListenerTimesOutAndReleasesPort()
    {
        using var listener = new LoopbackCallback();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => listener.Receive(new Uri("https://localhost:7179/"), _ => { }, TimeSpan.FromMilliseconds(100)));
        var probe = new TcpListener(IPAddress.Loopback, 8407);
        try { probe.Start(); } finally { probe.Stop(); }
    }

    [Fact]
    public async Task ListenerRejectsWrongHostAndCloses()
    {
        using var listener = new LoopbackCallback();
        Task send = Task.CompletedTask;
        await Assert.ThrowsAsync<InvalidOperationException>(() => listener.Receive(new Uri("https://localhost:7179/"), _ =>
        {
            send = Send();
        }, TimeSpan.FromSeconds(3)));
        await send;
        static async Task Send()
        {
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, 8407);
            await client.GetStream().WriteAsync(Encoding.ASCII.GetBytes("GET /callback/?state=x&code=y HTTP/1.1\r\nHost: evil.invalid\r\n\r\n"));
        }
    }

    [Fact]
    public async Task ListenerReceivesOneCallbackWithoutReflectingItsSecrets()
    {
        using var listener = new LoopbackCallback();
        Task<string> browser = Task.FromResult("");
        var target = await listener.Receive(new Uri("https://localhost:7179/"), _ => browser = Send(), TimeSpan.FromSeconds(3));
        Assert.Equal("synthetic", OAuthFlow.Callback(target, "expected"));
        var response = await browser;
        Assert.DoesNotContain("synthetic", response);
        Assert.DoesNotContain("expected", response);
        Assert.Contains("Cache-Control: no-store", response);
        static async Task<string> Send()
        {
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, 8407);
            var stream = client.GetStream();
            await stream.WriteAsync(Encoding.ASCII.GetBytes("GET /callback/?state=expected&code=synthetic HTTP/1.1\r\nHost: 127.0.0.1:8407\r\n\r\n"));
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"access_token\":\"synthetic\",\"token_type\":\"MAC\",\"expires_in\":300,\"scope\":\"microemr_api\"}")]
    [InlineData("{\"access_token\":\"synthetic\",\"token_type\":\"Bearer\",\"expires_in\":0,\"scope\":\"microemr_api\"}")]
    [InlineData("{\"access_token\":\"synthetic\",\"token_type\":\"Bearer\",\"expires_in\":300,\"scope\":\"wrong\"}")]
    [InlineData("{\"access_token\":\"synthetic\",\"token_type\":\"Bearer\",\"expires_in\":300,\"scope\":\"microemr_api\",\"refresh_token\":\"forbidden\"}")]
    public void InvalidTokenResponsesRejected(string json)
    {
        using var document = JsonDocument.Parse(json);
        Assert.Throws<InvalidOperationException>(() => OAuthFlow.TokenResponse(document.RootElement));
    }

    [Fact]
    public void OAuthResponseMayOmitUnchangedScopeButSignedTokenScopeIsStillRequired()
    {
        using var document = JsonDocument.Parse("{\"access_token\":\"synthetic\",\"token_type\":\"Bearer\",\"expires_in\":300}");
        Assert.Equal("synthetic", OAuthFlow.TokenResponse(document.RootElement));
    }

    [Theory]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("scope")]
    [InlineData("signature")]
    [InlineData("subject")]
    [InlineData("expired")]
    public async Task SignedTokenValidationRejectsWrongClaimsOrSignature(string defect)
    {
        using var fixture = new OAuthFixture();
        await Assert.ThrowsAsync<InvalidOperationException>(() => OAuthFlow.ValidateToken(fixture.Token(defect), [fixture.Key]));
    }

    [Fact]
    public async Task MockOAuthExchangeValidatesBeforeSingleHandoffWithoutDisclosingMaterial()
    {
        using var fixture = new OAuthFixture();
        using var http = new HttpClient(fixture);
        var count = 0;
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var output = new StringWriter();
        Console.SetOut(output); Console.SetError(output);
        try
        {
            await OAuthFlow.Authorize(http, uri =>
            {
                var query = QueryHelpers.ParseQuery(uri.Query);
                Assert.Equal("S256", query["code_challenge_method"]);
                Assert.Equal("code", query["response_type"]);
                Assert.Equal(OAuthFlow.Redirect, query["redirect_uri"]);
                Assert.Equal(OAuthFlow.Scope, query["scope"]);
                fixture.Challenge = query["code_challenge"].ToString();
                return Task.FromResult("/callback/?code=synthetic-code&state=" + query["state"]);
            }, token =>
            {
                count++;
                var start = Preparation.StartInfo("C:\\test\\Prepare-DemoEnvironment.ps1", "Stage", token);
                Assert.Equal(token, start.Environment["MICROEMR_DEMO_BEARER"]);
                Assert.DoesNotContain(token, string.Join(" ", start.ArgumentList));
                Assert.Contains("-File", start.ArgumentList);
                Assert.Contains("C:\\test\\Prepare-DemoEnvironment.ps1", start.ArgumentList);
                Assert.False(start.UseShellExecute);
                return Task.CompletedTask; // Never start the actual preparation process.
            }, CancellationToken.None);
            Assert.Equal(1, count);
            Assert.True(fixture.Exchanged);
            Assert.Equal("", output.ToString());
        }
        finally { Console.SetOut(originalOut); Console.SetError(originalError); }
    }

    [Fact]
    public async Task WrongStateNeverExchangesCodeOrInvokesPreparation()
    {
        using var fixture = new OAuthFixture();
        using var http = new HttpClient(fixture);
        await Assert.ThrowsAsync<InvalidOperationException>(() => OAuthFlow.Authorize(http,
            _ => Task.FromResult("/callback/?code=synthetic-code&state=wrong"),
            _ => throw new Exception("Handoff must not run"), CancellationToken.None));
        Assert.False(fixture.Exchanged);
    }

    [Theory]
    [InlineData("issuer")]
    [InlineData("tenant")]
    [InlineData("audience")]
    public async Task InvalidOAuthSessionNeverReachesHandoff(string defect)
    {
        using var fixture = new OAuthFixture { Defect = defect };
        using var http = new HttpClient(fixture);
        var invoked = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => OAuthFlow.Authorize(http, uri =>
        {
            var query = QueryHelpers.ParseQuery(uri.Query);
            fixture.Challenge = query["code_challenge"].ToString();
            return Task.FromResult("/callback/?code=synthetic-code&state=" + query["state"]);
        }, _ => { invoked = true; return Task.CompletedTask; }, CancellationToken.None));
        Assert.False(invoked);
    }

    [Theory]
    [InlineData("http://localhost:7179/connect/token")]
    [InlineData("https://evil.invalid/connect/token")]
    [InlineData("https://localhost:7179/connect/token?extra=1")]
    [InlineData("https://user@localhost:7179/connect/token")]
    public void DiscoveryCannotRedirectTokenExchangeToAnotherOrigin(string endpoint) =>
        Assert.Throws<InvalidOperationException>(() => OAuthFlow.Endpoint(endpoint));

    [Fact]
    public async Task MainSanitizesFailures()
    {
        var previous = Console.Error;
        using var output = new StringWriter();
        Console.SetError(output);
        try
        {
            Assert.Equal(1, await MicroEMR.DemoOAuth.Program.Main(["synthetic-sensitive-value"]));
            Assert.DoesNotContain("synthetic-sensitive-value", output.ToString());
            Assert.Contains("stopped safely", output.ToString());
        }
        finally { Console.SetError(previous); }
    }

    private sealed class Host : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "";
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class OAuthFixture : HttpMessageHandler
    {
        private readonly RSA rsa = RSA.Create(2048);
        public RsaSecurityKey Key => new(rsa) { KeyId = "test-key" };
        public string? Challenge { get; set; }
        public bool Exchanged { get; private set; }
        public string? Defect { get; init; }
        public string Token(string? defect = null)
        {
            var claims = new Dictionary<string, object> { ["sub"] = defect == "subject" ? "wrong" : OAuthFlow.Subject, ["scope"] = defect == "scope" ? "wrong" : OAuthFlow.Scope };
            using var other = RSA.Create(2048);
            return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
            {
                Issuer = defect == "issuer" ? "https://evil.invalid/" : OAuthFlow.Authority + "/",
                Audience = defect == "audience" ? "wrong" : OAuthFlow.Scope,
                Claims = claims, IssuedAt = DateTime.UtcNow.AddMinutes(-10), NotBefore = DateTime.UtcNow.AddMinutes(-10),
                Expires = defect == "expired" ? DateTime.UtcNow.AddMinutes(-1) : DateTime.UtcNow.AddMinutes(5),
                SigningCredentials = new(defect == "signature" ? new RsaSecurityKey(other) : Key, SecurityAlgorithms.RsaSha256)
            });
        }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            object payload;
            switch (request.RequestUri!.AbsolutePath)
            {
                case "/.well-known/openid-configuration":
                    payload = new { issuer = OAuthFlow.Authority + "/", authorization_endpoint = OAuthFlow.Authority + "/connect/authorize", token_endpoint = OAuthFlow.Authority + "/connect/token", jwks_uri = OAuthFlow.Authority + "/keys", code_challenge_methods_supported = new[] { "S256" } }; break;
                case "/connect/token":
                    var form = QueryHelpers.ParseQuery(await request.Content!.ReadAsStringAsync(cancellationToken));
                    Assert.False(form.ContainsKey("client_secret"));
                    Assert.Equal(OAuthFlow.ClientId, form["client_id"]);
                    Assert.Equal(OAuthFlow.Redirect, form["redirect_uri"]);
                    Assert.Equal("synthetic-code", form["code"]);
                    Assert.Equal(Challenge, OAuthFlow.Challenge(form["code_verifier"]!));
                    Exchanged = true;
                    payload = new { access_token = Token(Defect), token_type = "Bearer", expires_in = 300, scope = OAuthFlow.Scope }; break;
                case "/keys":
                    var key = JsonWebKeyConverter.ConvertFromRSASecurityKey(new RsaSecurityKey(rsa.ExportParameters(false)) { KeyId = "test-key" });
                    payload = new { keys = new[] { key } }; break;
                case "/api/context/tenant":
                    Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
                    payload = new { tenantUid = Defect == "tenant" ? "wrong" : OAuthFlow.TenantUid, tenantKey = "local-dev-fresh" }; break;
                default: throw new InvalidOperationException("Unexpected mock request");
            }
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json") };
        }
        protected override void Dispose(bool disposing) { if (disposing) rsa.Dispose(); base.Dispose(disposing); }
    }
}
