namespace MicroEMR.Application.Tenancy;

public sealed record TenantDatabaseInfo(
    Guid TenantUid,
    string DatabaseServerKey,
    string DatabaseName,
    string SecretReference,
    string DatabaseStatus);
