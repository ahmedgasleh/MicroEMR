using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using MicroEMR.Application.SecurityAudit;
using MicroEMR.Infrastructure.Tenancy;

namespace MicroEMR.Infrastructure.SecurityAudit;

public sealed class SqlPlatformSecurityAuditRepository(IConfiguration configuration)
    : IPlatformSecurityAuditRepository
{
    private readonly string _connectionString =
        PlatformDatabaseConnection.GetConnectionString(configuration);

    public async Task RecordMissingPermissionAsync(
        MissingPermissionSecurityEvent securityEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(securityEvent);

        await using var connection = new SqlConnection(_connectionString);
        await using var command = new SqlCommand(
            "dbo.PlatformSecurityAudit_RecordMissingPermission",
            connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        Add(command, "@ActorSubject", SqlDbType.NVarChar, 451, securityEvent.ActorSubject);
        Add(command, "@ClinicalUserId", SqlDbType.BigInt, 0, securityEvent.ClinicalUserId);
        Add(command, "@TargetTenantUid", SqlDbType.UniqueIdentifier, 0, securityEvent.TrustedTenantUid);
        Add(command, "@Capability", SqlDbType.NVarChar, 101, securityEvent.Capability);
        Add(command, "@RequiredPermission", SqlDbType.NVarChar, 101, securityEvent.RequiredPermission);
        Add(command, "@SourceApplication", SqlDbType.NVarChar, 51, securityEvent.SourceApplication);
        Add(command, "@RequestCorrelationId", SqlDbType.NVarChar, 129, securityEvent.RequestCorrelationId);

        await connection.OpenAsync(cancellationToken);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task RecordCrossPatientOwnershipAsync(
        CrossPatientOwnershipSecurityEvent securityEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(securityEvent);

        await using var connection = new SqlConnection(_connectionString);
        await using var command = new SqlCommand(
            "dbo.PlatformSecurityAudit_RecordCrossPatientOwnership",
            connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        Add(command, "@ActorSubject", SqlDbType.NVarChar, 451, securityEvent.ActorSubject);
        Add(command, "@ClinicalUserId", SqlDbType.BigInt, 0, securityEvent.ClinicalUserId);
        Add(command, "@TargetTenantUid", SqlDbType.UniqueIdentifier, 0, securityEvent.TrustedTenantUid);
        Add(command, "@Capability", SqlDbType.NVarChar, 101, securityEvent.Capability);
        Add(command, "@RequestedPatientUid", SqlDbType.UniqueIdentifier, 0, securityEvent.RequestedPatientUid);
        Add(command, "@AuthoritativePatientUid", SqlDbType.UniqueIdentifier, 0, securityEvent.AuthoritativePatientUid);
        Add(command, "@ResourceType", SqlDbType.NVarChar, 51, securityEvent.ResourceType);
        Add(command, "@ResourceUid", SqlDbType.UniqueIdentifier, 0, securityEvent.ResourceUid);
        Add(command, "@SourceApplication", SqlDbType.NVarChar, 51, securityEvent.SourceApplication);
        Add(command, "@RequestCorrelationId", SqlDbType.NVarChar, 129, securityEvent.RequestCorrelationId);

        await connection.OpenAsync(cancellationToken);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void Add(
        SqlCommand command,
        string name,
        SqlDbType type,
        int size,
        object? value)
    {
        var parameter = size > 0
            ? command.Parameters.Add(name, type, size)
            : command.Parameters.Add(name, type);
        parameter.Value = value ?? DBNull.Value;
    }
}
