using Microsoft.Data.SqlClient;
using MicroEMR.Infrastructure.Tenancy;
using System.Text.RegularExpressions;

public sealed record SanitizedSqlConnectionProperties(
    string Server,
    string Database,
    string AuthenticationMode,
    bool IntegratedSecurity,
    bool UserIdConfigured,
    string Encrypt,
    bool TrustServerCertificate,
    int ConnectionTimeout,
    string? HostNameInCertificate,
    string SecretSource);

public sealed record TenantConnectionDiagnosticResult(
    SanitizedSqlConnectionProperties Properties,
    bool ConnectionOpened,
    bool TlsSucceeded,
    bool AuthenticationSucceeded,
    bool SchemaMigrationAccessible,
    string FailureStage,
    string? ExceptionType,
    int? SqlErrorNumber,
    string? SafeErrorMessage,
    string? SqlServerVersion,
    string? AuthenticationScheme,
    string RuntimeVersion,
    string SqlClientVersion,
    string WindowsIdentity);

public static class TenantConnectionDiagnostics
{
    public static SanitizedSqlConnectionProperties Describe(
        string connectionString,
        string expectedDatabase,
        string secretSource)
    {
        var builder = TenantSqlConnectionFactory.ValidateConnectionString(connectionString, expectedDatabase);
        var authentication = builder.IntegratedSecurity
            ? "Windows Integrated Security"
            : string.IsNullOrWhiteSpace(builder.UserID) ? "Other/unspecified" : "SQL authentication";
        return new(
            builder.DataSource,
            builder.InitialCatalog,
            authentication,
            builder.IntegratedSecurity,
            !string.IsNullOrWhiteSpace(builder.UserID),
            builder.Encrypt.ToString(),
            builder.TrustServerCertificate,
            builder.ConnectTimeout,
            NullIfEmpty(builder.HostNameInCertificate),
            secretSource);
    }

    public static async Task<TenantConnectionDiagnosticResult> DiagnoseAsync(
        string connectionString,
        string expectedDatabase,
        string secretSource,
        CancellationToken cancellationToken = default)
    {
        var builder = TenantSqlConnectionFactory.ValidateConnectionString(connectionString, expectedDatabase);
        var properties = Describe(connectionString, expectedDatabase, secretSource);
        try
        {
            await using var connection = new SqlConnection(builder.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            const string sql = """
                SELECT DB_NAME(),
                       CONVERT(nvarchar(128), SERVERPROPERTY('ProductVersion')),
                       (SELECT auth_scheme FROM sys.dm_exec_connections WHERE session_id = @@SPID),
                       CASE WHEN OBJECT_ID(N'dbo.SchemaMigration', N'U') IS NULL THEN 0 ELSE 1 END;
                """;
            await using var command = new SqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);
            var databaseMatches = string.Equals(reader.GetString(0), expectedDatabase, StringComparison.OrdinalIgnoreCase);
            return Result(properties, databaseMatches, databaseMatches, databaseMatches,
                databaseMatches && reader.GetInt32(3) == 1,
                databaseMatches ? "None" : "Database identity", null, null,
                databaseMatches ? null : "Connected database does not match the assigned database.",
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2));
        }
        catch (Exception exception)
        {
            var sqlException = FindSqlException(exception);
            var message = SafeMessage(sqlException?.Message ?? exception.Message);
            var stage = ClassifyFailure(message);
            var reachedServer = stage is "TLS negotiation" or "Windows authentication" or "SQL authentication";
            var tlsSucceeded = stage is "Windows authentication" or "SQL authentication";
            return Result(properties, false, tlsSucceeded, false, false, stage,
                (sqlException ?? exception).GetType().FullName, sqlException?.Number, message, null, null);
        }
    }

    public static string SafeMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return "No error detail available.";
        var redacted = Regex.Replace(
            message.ReplaceLineEndings(" ").Trim(),
            @"(?i)\b(password|pwd|user\s*id|uid)\s*=\s*[^;\s]+",
            "$1=<redacted>");
        return Regex.Replace(
            redacted,
            @"(?i)login failed for user\s+'[^']+'",
            "Login failed for user '<redacted>'");
    }

    private static string ClassifyFailure(string message)
    {
        if (message.Contains("SSPI", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("target principal name", StringComparison.OrdinalIgnoreCase)) return "Windows authentication";
        if (message.Contains("certificate", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("encryption", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("SSL", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("TLS", StringComparison.OrdinalIgnoreCase)) return "TLS negotiation";
        if (message.Contains("login failed", StringComparison.OrdinalIgnoreCase)) return "SQL authentication";
        return "Connection open";
    }

    private static SqlException? FindSqlException(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
            if (current is SqlException sqlException) return sqlException;
        return null;
    }

    private static TenantConnectionDiagnosticResult Result(
        SanitizedSqlConnectionProperties properties,
        bool opened,
        bool tls,
        bool authenticated,
        bool schema,
        string stage,
        string? type,
        int? number,
        string? message,
        string? serverVersion,
        string? authenticationScheme) =>
        new(properties, opened, tls, authenticated, schema, stage, type, number, message,
            serverVersion, authenticationScheme,
            System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            typeof(SqlConnection).Assembly.GetName().Version?.ToString() ?? "unknown",
            $"{Environment.UserDomainName}\\{Environment.UserName}");

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
