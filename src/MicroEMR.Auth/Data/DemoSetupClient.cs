using OpenIddict.Abstractions;

namespace MicroEMR.Auth.Data;

// Separate from the existing Web registration: never updates another client.
public sealed class DemoSetupClient(IServiceProvider services, IHostEnvironment environment,
    IConfiguration configuration) : IHostedService
{
    public const string ClientId = "microemr_demo_setup";
    public const string RedirectUri = "http://127.0.0.1:8407/callback/";

    public static bool Enabled(IHostEnvironment environment, IConfiguration configuration)
    {
        // Fail closed when the host has unspecified/wildcard/shared bindings.
        var bindings = configuration["urls"]?.Split(';', StringSplitOptions.RemoveEmptyEntries) ?? [];
        return environment.IsDevelopment() && configuration.GetValue<bool>("DemoSetupClient:Enabled") &&
            bindings.Length > 0 && bindings.All(value => Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
                uri.IsLoopback && uri.UserInfo.Length == 0) &&
            bindings.Contains("https://localhost:7179", StringComparer.OrdinalIgnoreCase);
    }

    public static OpenIddictApplicationDescriptor Descriptor()
    {
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = ClientId,
            ClientType = OpenIddictConstants.ClientTypes.Public,
            ConsentType = OpenIddictConstants.ConsentTypes.Explicit,
            DisplayName = "Local development demo setup"
        };
        descriptor.RedirectUris.Add(new Uri(RedirectUri));
        descriptor.Permissions.UnionWith([
            OpenIddictConstants.Permissions.Endpoints.Authorization,
            OpenIddictConstants.Permissions.Endpoints.Token,
            OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
            OpenIddictConstants.Permissions.ResponseTypes.Code,
            OpenIddictConstants.Permissions.Prefixes.Scope + "microemr_api"
        ]);
        descriptor.Requirements.Add(OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange);
        return descriptor;
    }

    // Also enforce the exact port, even where native-client redirect validation relaxes ports.
    public static bool AcceptAuthorization(bool enabled, string? redirect, string? method, string? scope) =>
        enabled && redirect == RedirectUri && method == "S256" && scope == "microemr_api";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!Enabled(environment, configuration)) return;
        using var scope = services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var existing = await manager.FindByClientIdAsync(ClientId, cancellationToken);
        if (existing is null) await manager.CreateAsync(Descriptor(), cancellationToken);
        else await manager.UpdateAsync(existing, Descriptor(), cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
