using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class TenantConnectionDiagnosticsTests
{
    private const string SqlLoginConnection =
        "Server=DEVSQL01;Database=MicroEMR_Test;User ID=private-user;Password=private-password;Encrypt=True;TrustServerCertificate=False;Connect Timeout=9";

    [Fact]
    public void Description_reports_safe_connection_properties()
    {
        var result = TenantConnectionDiagnostics.Describe(
            SqlLoginConnection, "MicroEMR_Test", "test source");
        Assert.Equal("DEVSQL01", result.Server);
        Assert.Equal("MicroEMR_Test", result.Database);
        Assert.False(result.IntegratedSecurity);
        Assert.True(result.UserIdConfigured);
        Assert.Equal("True", result.Encrypt);
        Assert.False(result.TrustServerCertificate);
        Assert.Equal(9, result.ConnectionTimeout);
    }

    [Fact]
    public void Description_never_contains_password_or_user_id()
    {
        var output = TenantConnectionDiagnostics.Describe(
            SqlLoginConnection, "MicroEMR_Test", "test source").ToString();
        Assert.DoesNotContain("private-password", output, StringComparison.Ordinal);
        Assert.DoesNotContain("private-user", output, StringComparison.Ordinal);
        Assert.DoesNotContain(SqlLoginConnection, output, StringComparison.Ordinal);
    }

    [Fact]
    public void Integrated_security_is_reported()
    {
        var result = TenantConnectionDiagnostics.Describe(
            "Server=localhost;Database=MicroEMR_Test;Integrated Security=True;Encrypt=False",
            "MicroEMR_Test", "test source");
        Assert.True(result.IntegratedSecurity);
        Assert.Equal("Windows Integrated Security", result.AuthenticationMode);
    }

    [Fact]
    public void Failure_message_redacts_credentials()
    {
        var output = TenantConnectionDiagnostics.SafeMessage(
            "Failure User ID=private-user;Password=private-password; server unavailable");
        Assert.DoesNotContain("private-user", output, StringComparison.Ordinal);
        Assert.DoesNotContain("private-password", output, StringComparison.Ordinal);
        Assert.Contains("<redacted>", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Login_failure_redacts_user_name()
    {
        var output = TenantConnectionDiagnostics.SafeMessage("Login failed for user 'private-user'.");
        Assert.DoesNotContain("private-user", output, StringComparison.Ordinal);
        Assert.Equal("Login failed for user '<redacted>'.", output);
    }
}
