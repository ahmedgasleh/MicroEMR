using MicroEMR.Application.Scheduling.Contracts;
using MicroEMR.Application.Scheduling.Repositories;

namespace MicroEMR.Application.Scheduling.Services;

public sealed class SchedulingReadService : ISchedulingReadService
{
    private readonly ISchedulingReadRepository _repository;

    public SchedulingReadService(ISchedulingReadRepository repository)
    {
        _repository = repository;
    }

    public async Task<SchedulingDaySheetResponse> GetDaySheetAsync(
        SchedulingDaySheetRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Date == default || request.Date == DateOnly.MaxValue
            || DateOnly.FromDateTime(request.Start.Date) != request.Date
            || DateOnly.FromDateTime(request.End.Date) != request.Date.AddDays(1)
            || request.Start.TimeOfDay != TimeSpan.Zero || request.End.TimeOfDay != TimeSpan.Zero
            || request.End - request.Start < TimeSpan.FromHours(23)
            || request.End - request.Start > TimeSpan.FromHours(25))
            throw new ArgumentException("A single scheduling day with local midnight boundaries is required.");
        if (request.ClinicianUids is { Length: 0 } || request.ClinicianUids?.Contains(Guid.Empty) == true)
            throw new ArgumentException("Select at least one valid clinician.");

        var resources = await _repository.GetActiveResourcesAsync(cancellationToken);
        var clinicians = resources.Where(resource => resource.IsActive
            && string.Equals(resource.ResourceType, "Provider", StringComparison.OrdinalIgnoreCase)).ToArray();
        var permittedUids = clinicians.Select(resource => resource.ResourceUid).ToHashSet();
        var clinicianNames = clinicians.ToDictionary(resource => resource.ResourceUid, resource => resource.DisplayName);
        if (request.ClinicianUids?.Any(uid => !permittedUids.Contains(uid)) == true)
            throw new ArgumentException("The selected clinician is unavailable in this schedule.");
        var selectedUids = request.ClinicianUids?.ToHashSet() ?? permittedUids;
        var startUtc = request.Start.UtcDateTime;
        var endUtc = request.End.UtcDateTime;
        // Reuse the operational calendar query and its overlap/cancellation policy.
        var appointments = await _repository.GetAppointmentsAsync(startUtc, endUtc, null, cancellationToken);
        return new SchedulingDaySheetResponse
        {
            Date = request.Date,
            ClinicianScope = request.ClinicianUids is null ? "All Clinicians"
                : string.Join(", ", clinicians.Where(resource => selectedUids.Contains(resource.ResourceUid))
                    .OrderBy(resource => resource.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .Select(resource => resource.DisplayName)),
            GeneratedAtUtc = DateTime.UtcNow,
            Appointments = appointments.Where(appointment => selectedUids.Contains(appointment.PrimaryResourceUid)
                    && appointment.StartDateTimeUtc < endUtc && appointment.EndDateTimeUtc > startUtc
                    && !string.Equals(appointment.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                .OrderBy(appointment => string.IsNullOrWhiteSpace(appointment.PatientDisplayName)
                    ? "Unknown patient" : appointment.PatientDisplayName.Trim(), StringComparer.OrdinalIgnoreCase)
                .ThenBy(appointment => appointment.StartDateTimeUtc)
                .ThenBy(appointment => appointment.AppointmentUid)
                .Select(appointment => new SchedulingDaySheetRow
                {
                    PatientName = string.IsNullOrWhiteSpace(appointment.PatientDisplayName)
                        ? "Unknown patient" : appointment.PatientDisplayName.Trim(),
                    StartDateTimeUtc = appointment.StartDateTimeUtc,
                    EndDateTimeUtc = appointment.EndDateTimeUtc,
                    ClinicianName = clinicianNames[appointment.PrimaryResourceUid],
                    Status = appointment.Status
                }).ToArray()
        };
    }

    public async Task<IReadOnlyList<PatientAppointmentResponse>?> GetPatientAppointmentsAsync(
        Guid patientUid, CancellationToken cancellationToken = default)
    {
        if (patientUid == Guid.Empty) throw new ArgumentException("Patient UID is required.", nameof(patientUid));
        var items = await _repository.GetPatientAppointmentsAsync(patientUid, cancellationToken);
        if (items?.Any(item => item.PatientUid != patientUid) == true)
            throw new InvalidOperationException("Appointment history does not belong to the requested patient.");
        return items;
    }

    public Task<IReadOnlyList<ScheduleResourceResponse>> GetActiveResourcesAsync(
        CancellationToken cancellationToken = default)
    {
        return _repository.GetActiveResourcesAsync(cancellationToken);
    }

    public Task<IReadOnlyList<ScheduleAppointmentListItemResponse>> GetAppointmentsAsync(
        DateTime startUtc,
        DateTime endUtc,
        Guid? resourceUid,
        CancellationToken cancellationToken = default)
    {
        if (endUtc <= startUtc)
        {
            throw new ArgumentException(
                "The appointment end range must be after the start range.");
        }

        return _repository.GetAppointmentsAsync(
            NormalizeUtc(startUtc),
            NormalizeUtc(endUtc),
            resourceUid,
            cancellationToken);
    }

    public Task<ScheduleAppointmentDetailsResponse?> GetAppointmentByUidAsync(
        Guid appointmentUid,
        CancellationToken cancellationToken = default)
    {
        if (appointmentUid == Guid.Empty)
            throw new ArgumentException("Appointment UID is required.", nameof(appointmentUid));

        return _repository.GetAppointmentByUidAsync(appointmentUid, cancellationToken);
    }

    public Task<IReadOnlyList<AppointmentHistoryResponse>> GetHistoryAsync(
        Guid appointmentUid,
        CancellationToken cancellationToken = default)
    {
        if (appointmentUid == Guid.Empty)
            throw new ArgumentException("Appointment UID is required.", nameof(appointmentUid));

        return _repository.GetHistoryAsync(appointmentUid, cancellationToken);
    }

    public Task<IReadOnlyList<ScheduleMonthSummaryItemResponse>> GetMonthSummaryAsync(
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken cancellationToken = default)
    {
        if (endUtc <= startUtc)
            throw new ArgumentException("The month summary end range must be after the start range.");
        if (endUtc - startUtc > TimeSpan.FromDays(45))
            throw new ArgumentException("The month summary range cannot exceed 45 days.");

        return _repository.GetMonthSummaryAsync(
            NormalizeUtc(startUtc), NormalizeUtc(endUtc), cancellationToken);
    }

    public Task<IReadOnlyList<SchedulingBlockedTimeResponse>> GetBlockedTimesAsync(
        DateTime startDateTimeUtc,
        DateTime endDateTimeUtc,
        CancellationToken cancellationToken = default)
    {
        if (endDateTimeUtc <= startDateTimeUtc)
            throw new ArgumentException("Blocked-time range is invalid.");
        return _repository.GetBlockedTimesAsync(
            NormalizeUtc(startDateTimeUtc), NormalizeUtc(endDateTimeUtc), cancellationToken);
    }

    private static DateTime NormalizeUtc(DateTime value)
    {
        if (value.Kind == DateTimeKind.Utc)
        {
            return value;
        }

        if (value.Kind == DateTimeKind.Unspecified)
        {
            return DateTime.SpecifyKind(value, DateTimeKind.Local)
                .ToUniversalTime();
        }

        return value.ToUniversalTime();
    }
}
