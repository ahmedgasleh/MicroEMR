namespace MicroEMR.Infrastructure.Tenancy;

public sealed class TenantDatabaseConnectionException : Exception
{
    public TenantDatabaseConnectionException(string message)
        : base(message)
    {
    }

    public TenantDatabaseConnectionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
