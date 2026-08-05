using System.Globalization;
using System.Text;
using MicroEMR.Application.Scheduling;
using MicroEMR.Application.Tenancy;

namespace MicroEMR.Application.Reporting;

public sealed record AppointmentStatusReportRow(Guid AppointmentUid, DateTime StartAtUtc, DateTime EndAtUtc,
    Guid PatientUid, string PatientName, string? ChartNumber, string ProviderName, string Status);
public sealed record AppointmentStatusCount(string Status, int Count);
public sealed record AppointmentStatusReport(DateOnly StartDate, DateOnly EndDate, string TimeZoneId,
    int TotalAppointments, IReadOnlyList<AppointmentStatusCount> StatusCounts,
    IReadOnlyList<AppointmentStatusReportRow> Appointments);
public sealed record AppointmentStatusReportData(IReadOnlyList<AppointmentStatusCount> StatusCounts,
    IReadOnlyList<AppointmentStatusReportRow> Appointments);

public interface IAppointmentStatusReportRepository
{
    Task<AppointmentStatusReportData> GetAsync(DateTime startUtc, DateTime endUtc,
        CancellationToken cancellationToken = default);
}

public interface IAppointmentStatusReportService
{
    Task<AppointmentStatusReport> GetAsync(DateOnly startDate, DateOnly endDate,
        CancellationToken cancellationToken = default);
    byte[] CreateCsv(AppointmentStatusReport report);
}

public sealed class AppointmentStatusReportService(
    ITenantContext tenantContext, ITenantCatalog tenantCatalog, IAppointmentStatusReportRepository repository)
    : IAppointmentStatusReportService
{
    public static readonly IReadOnlyList<string> CanonicalStatuses = Enum.GetValues<AppointmentStatus>()
        .Select(AppointmentStatusMapper.ToStorageValue).ToArray();

    public async Task<AppointmentStatusReport> GetAsync(DateOnly startDate, DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        if (endDate < startDate) throw new ArgumentException("End date must be on or after start date.");
        if (endDate.DayNumber - startDate.DayNumber > 365) throw new ArgumentException("The report range cannot exceed 366 days.");
        var tenant = await tenantCatalog.GetByUidAsync(tenantContext.TenantUid, cancellationToken)
            ?? throw new InvalidOperationException("The active tenant could not be resolved.");
        var zone = TimeZoneInfo.FindSystemTimeZoneById(tenant.DefaultTimeZoneId);
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(startDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified), zone);
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(endDate.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified), zone);
        var data = await repository.GetAsync(startUtc, endUtc, cancellationToken);
        var counts = data.StatusCounts.GroupBy(x => x.Status, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Sum(y => y.Count), StringComparer.OrdinalIgnoreCase);
        var normalized = CanonicalStatuses.Select(x => new AppointmentStatusCount(x, counts.GetValueOrDefault(x))).ToArray();
        return new(startDate, endDate, tenant.DefaultTimeZoneId, data.Appointments.Count, normalized,
            data.Appointments.OrderBy(x => x.StartAtUtc).ThenBy(x => x.AppointmentUid).ToArray());
    }

    public byte[] CreateCsv(AppointmentStatusReport report)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(report.TimeZoneId);
        var output = new StringBuilder("Appointment Date,Start Time,End Time,Patient Name,Chart Number,Provider/Resource,Status\r\n");
        foreach (var row in report.Appointments)
        {
            var start = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(row.StartAtUtc, DateTimeKind.Utc), zone);
            var end = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(row.EndAtUtc, DateTimeKind.Utc), zone);
            output.AppendJoin(',', Csv(start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                Csv(start.ToString("HH:mm", CultureInfo.InvariantCulture)), Csv(end.ToString("HH:mm", CultureInfo.InvariantCulture)),
                Csv(row.PatientName), Csv(row.ChartNumber), Csv(row.ProviderName), Csv(row.Status)).Append("\r\n");
        }
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        return [.. encoding.GetPreamble(), .. encoding.GetBytes(output.ToString())];
    }

    private static string Csv(string? value)
    {
        var safe = value ?? string.Empty;
        if (safe.Length > 0 && safe[0] is '=' or '+' or '-' or '@') safe = "'" + safe;
        return "\"" + safe.Replace("\"", "\"\"") + "\"";
    }
}
