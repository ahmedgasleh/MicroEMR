using System.Text.Json.Nodes;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class TrackedConfigurationSecretGuardrailTests
{
    [Fact]
    public void SourceAppsettingsContainNoCredentialBearingDatabaseConnections()
    {
        foreach (var file in SourceAppsettings())
        {
            var root = JsonNode.Parse(File.ReadAllText(file)) as JsonObject;
            var connections = root?["ConnectionStrings"] as JsonObject;
            if (connections is null) continue;

            foreach (var connection in connections)
            {
                var value = connection.Value?.GetValue<string>() ?? string.Empty;
                var containsCredential = value.Contains(
                    "Password=", StringComparison.OrdinalIgnoreCase) ||
                    value.Contains("Pwd=", StringComparison.OrdinalIgnoreCase);
                Assert.True(!containsCredential,
                    $"Credential-bearing connection string found in {Path.GetRelativePath(Root(), file)}.");
            }
        }
    }

    [Fact]
    public void SharedOidcClientSecretIsAbsentFromTrackedAppsettings()
    {
        var auth = Read("src", "MicroEMR.Auth", "appsettings.Development.json");
        var web = Read("src", "MicroEMR.Web", "appsettings.json");

        Assert.True(string.IsNullOrWhiteSpace(
            auth["OpenIddict"]?["WebClientSecret"]?.GetValue<string>()));
        Assert.True(string.IsNullOrWhiteSpace(
            web["Authentication"]?["ClientSecret"]?.GetValue<string>()));
    }

    [Fact]
    public void AuthAndWebRequireExternalSecretsWithoutLoggingValues()
    {
        var authProgram = Source("src", "MicroEMR.Auth", "Program.cs");
        var authSeed = Source("src", "MicroEMR.Auth", "Data", "SeedData.cs");
        var webProgram = Source("src", "MicroEMR.Web", "Program.cs");
        var webProject = Source("src", "MicroEMR.Web", "MicroEMR.Web.csproj");

        Assert.Contains("Required connection string 'ConnectionStrings:AuthServerConnection' is not configured.", authProgram);
        Assert.Contains("Required OpenIddict Web client secret is not configured.", authSeed);
        Assert.Contains("Required OpenID Connect Web client secret is not configured.", webProgram);
        Assert.Contains("<UserSecretsId>MicroEMR.Web-local-development</UserSecretsId>", webProject);
    }

    private static IEnumerable<string> SourceAppsettings() =>
        Directory.EnumerateDirectories(Path.Combine(Root(), "src"))
            .SelectMany(project => Directory.EnumerateFiles(
                project, "appsettings*.json", SearchOption.TopDirectoryOnly));

    private static JsonObject Read(params string[] parts) =>
        JsonNode.Parse(Source(parts))!.AsObject();

    private static string Source(params string[] parts) =>
        File.ReadAllText(Path.Combine([Root(), .. parts]));

    private static string Root() => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
}
