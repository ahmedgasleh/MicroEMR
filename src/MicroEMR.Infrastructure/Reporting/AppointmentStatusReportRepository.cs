using System.Data;
using Microsoft.Data.SqlClient;
using MicroEMR.Application.Reporting;
using MicroEMR.Application.Scheduling;
using MicroEMR.Infrastructure.Tenancy;

namespace MicroEMR.Infrastructure.Reporting;

public sealed class AppointmentStatusReportRepository(ITenantSqlConnectionFactory connectionFactory)
    : IAppointmentStatusReportRepository
{
    public async Task<AppointmentStatusReportData> GetAsync(DateTime startUtc, DateTime endUtc,
        CancellationToken cancellationToken = default)
    {
        var counts = new List<AppointmentStatusCount>();
        var rows = new List<AppointmentStatusReportRow>();
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand("dbo.Appointment_ReportByStatus", connection)
            { CommandType = CommandType.StoredProcedure };
        command.Parameters.Add("@StartDateTimeUtc", SqlDbType.DateTime2).Value = startUtc;
        command.Parameters.Add("@EndDateTimeUtc", SqlDbType.DateTime2).Value = endUtc;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            counts.Add(new(CanonicalStatus(reader.GetString(reader.GetOrdinal("Status"))), reader.GetInt32(reader.GetOrdinal("AppointmentCount"))));
        await reader.NextResultAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            rows.Add(new(reader.GetGuid(reader.GetOrdinal("AppointmentUid")),
                DateTime.SpecifyKind(reader.GetDateTime(reader.GetOrdinal("StartDateTimeUtc")), DateTimeKind.Utc),
                DateTime.SpecifyKind(reader.GetDateTime(reader.GetOrdinal("EndDateTimeUtc")), DateTimeKind.Utc),
                reader.GetGuid(reader.GetOrdinal("PatientUid")), reader.GetString(reader.GetOrdinal("PatientName")),
                reader.IsDBNull(reader.GetOrdinal("ChartNumber")) ? null : reader.GetString(reader.GetOrdinal("ChartNumber")),
                reader.GetString(reader.GetOrdinal("ProviderName")), CanonicalStatus(reader.GetString(reader.GetOrdinal("Status")))));
        return new(counts, rows);
    }

    private static string CanonicalStatus(string value) =>
        AppointmentStatusMapper.ToStorageValue(AppointmentStatusMapper.Parse(value));
}
