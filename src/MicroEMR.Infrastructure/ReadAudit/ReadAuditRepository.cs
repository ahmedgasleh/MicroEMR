using System.Data;
using Microsoft.Data.SqlClient;
using MicroEMR.Application.ReadAudit;
using MicroEMR.Infrastructure.Tenancy;

namespace MicroEMR.Infrastructure.ReadAudit;

public sealed class ReadAuditRepository(ITenantSqlConnectionFactory connectionFactory)
    : IReadAuditRepository
{
    public async Task<Guid> RecordPatientChartOpenedAsync(
        Guid patientUid,
        long clinicalUserId,
        string requestCorrelationId,
        string sourceApplication,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand("dbo.AuditLog_RecordPatientChartOpened", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.Add("@PatientUid", SqlDbType.UniqueIdentifier).Value = patientUid;
        command.Parameters.Add("@ClinicalUserId", SqlDbType.BigInt).Value = clinicalUserId;
        command.Parameters.Add("@RequestCorrelationId", SqlDbType.NVarChar, 100).Value = requestCorrelationId;
        command.Parameters.Add("@SourceApplication", SqlDbType.NVarChar, 50).Value = sourceApplication;

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is Guid auditEventUid
            ? auditEventUid
            : throw new InvalidOperationException("The chart-open audit procedure returned no event identifier.");
    }

    public async Task<Guid> RecordStructuredReadAsync(
        string eventType,
        string resourceType,
        Guid resourceUid,
        Guid patientUid,
        long clinicalUserId,
        string requestCorrelationId,
        string sourceApplication,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand("dbo.AuditLog_RecordStructuredRead", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.Add("@EventType", SqlDbType.NVarChar, 100).Value = eventType;
        command.Parameters.Add("@ResourceType", SqlDbType.NVarChar, 100).Value = resourceType;
        command.Parameters.Add("@ResourceUid", SqlDbType.UniqueIdentifier).Value = resourceUid;
        command.Parameters.Add("@PatientUid", SqlDbType.UniqueIdentifier).Value = patientUid;
        command.Parameters.Add("@ClinicalUserId", SqlDbType.BigInt).Value = clinicalUserId;
        command.Parameters.Add("@RequestCorrelationId", SqlDbType.NVarChar, 100).Value = requestCorrelationId;
        command.Parameters.Add("@SourceApplication", SqlDbType.NVarChar, 50).Value = sourceApplication;

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is Guid auditEventUid
            ? auditEventUid
            : throw new InvalidOperationException("The structured read audit procedure returned no event identifier.");
    }

    public async Task<Guid> RecordAggregateReportAsync(
        string eventType,
        string reportKey,
        long clinicalUserId,
        string requestCorrelationId,
        string sourceApplication,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand("dbo.AuditLog_RecordStructuredRead", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.Add("@EventType", SqlDbType.NVarChar, 100).Value = eventType;
        command.Parameters.Add("@ResourceType", SqlDbType.NVarChar, 100).Value = ReadAuditResourceTypes.Report;
        command.Parameters.Add("@ResourceUid", SqlDbType.UniqueIdentifier).Value = DBNull.Value;
        command.Parameters.Add("@PatientUid", SqlDbType.UniqueIdentifier).Value = DBNull.Value;
        command.Parameters.Add("@ClinicalUserId", SqlDbType.BigInt).Value = clinicalUserId;
        command.Parameters.Add("@RequestCorrelationId", SqlDbType.NVarChar, 100).Value = requestCorrelationId;
        command.Parameters.Add("@SourceApplication", SqlDbType.NVarChar, 50).Value = sourceApplication;
        command.Parameters.Add("@ReportKey", SqlDbType.NVarChar, 100).Value = reportKey;

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is Guid auditEventUid
            ? auditEventUid
            : throw new InvalidOperationException("The aggregate report audit procedure returned no event identifier.");
    }
}
