using Microsoft.Data.SqlClient;

namespace MicroEMR.Infrastructure.Tenancy;

internal static class AdministrativeConnectionConfiguration
{
    // Administrative credentials may differ, but the selected database/server must not.
    public static string Validate(string? administrative, string? runtime, string setting)
    {
        if (string.IsNullOrWhiteSpace(administrative))
            throw new InvalidOperationException($"Required administrative SQL setting '{setting}' is missing.");
        if (string.IsNullOrWhiteSpace(runtime))
            throw new InvalidOperationException($"Runtime SQL target required to validate '{setting}' is missing.");

        SqlConnectionStringBuilder admin;
        SqlConnectionStringBuilder normal;
        try
        {
            admin = new(administrative);
            normal = new(runtime);
        }
        catch (ArgumentException)
        {
            // Do not attach parser exceptions that may contain configuration values.
            throw new InvalidOperationException($"SQL configuration for '{setting}' is invalid.");
        }

        if (string.IsNullOrWhiteSpace(admin.DataSource) || string.IsNullOrWhiteSpace(admin.InitialCatalog)
            || !string.IsNullOrWhiteSpace(admin.AttachDBFilename)
            || !string.Equals(admin.DataSource, normal.DataSource, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(admin.InitialCatalog, normal.InitialCatalog, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Administrative SQL target for '{setting}' must match the runtime assignment.");

        return administrative;
    }
}
