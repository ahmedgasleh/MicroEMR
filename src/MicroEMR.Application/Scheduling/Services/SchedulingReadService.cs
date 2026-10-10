using MicroEMR.Application.Scheduling.Contracts;
using MicroEMR.Application.Scheduling.Repositories;
using MicroEMR.Application.Scheduling.Utilities;
using Microsoft.Extensions.Options;

namespace MicroEMR.Application.Scheduling.Services;

public sealed class SchedulingReadService : ISchedulingReadService
{
    private readonly ISchedulingReadRepository _repository;
    private readonly TimeProvider _clock;
    private readonly NextAvailableSearchOptions _searchOptions;

    public SchedulingReadService(ISchedulingReadRepository repository, TimeProvider? clock = null,
        IOptions<NextAvailableSearchOptions>? searchOptions = null)
    {
        _repository = repository;
        _clock = clock ?? TimeProvider.System;
        _searchOptions = searchOptions?.Value ?? new();
    }

    public async Task<NextAvailableAppointmentsResponse> GetNextAvailableAsync(
        NextAvailableAppointmentsRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var maximum = Math.Clamp(_searchOptions.MaxHorizonDays, 1, 90);
        if (request.ClinicianUid == Guid.Empty || request.RoomUid == Guid.Empty
            || request.StartDate == default || request.HorizonDays < 1 || request.HorizonDays > maximum
            || request.StartDate.DayNumber > DateOnly.MaxValue.DayNumber - request.HorizonDays
            || request.DurationMinutes < 15 || request.DurationMinutes > 240 || request.DurationMinutes % 15 != 0
            || request.Weekdays?.Any(day => day < 0 || day > 6) == true)
            throw new ArgumentException($"Select a clinician, a valid date, 1–{maximum} search days and a duration of 15–240 minutes in 15-minute increments.");
        string[] types = ["Office Visit", "Phone Visit", "Virtual Visit", "Follow-up", "Consultation", "Procedure", "Other"];
        var type = string.IsNullOrWhiteSpace(request.AppointmentType) ? null : request.AppointmentType.Trim();
        if (type is not null && !types.Contains(type)) throw new ArgumentException("Select an available appointment type.");
        var from = request.PreferredStart ?? new TimeOnly(8, 0);
        var to = request.PreferredEnd ?? new TimeOnly(18, 0);
        if (to <= from || from.Ticks % TimeSpan.TicksPerMinute != 0 || to.Ticks % TimeSpan.TicksPerMinute != 0)
            throw new ArgumentException("Use whole-minute times with the time-window end after its start.");
        TimeZoneInfo zone;
        try { zone = request.TimeZoneId is null ? TimeZoneInfo.Local : TimeZoneInfo.FindSystemTimeZoneById(request.TimeZoneId); }
        catch (Exception error) when (error is TimeZoneNotFoundException or InvalidTimeZoneException)
        { throw new ArgumentException("The scheduling time zone is unavailable."); }
        var now = _clock.GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(now, zone));
        if (request.StartDate < today || request.StartDate > today.AddDays(maximum))
            throw new ArgumentException("Search must start today or within the configured search horizon.");
        var resources = await _repository.GetActiveResourcesAsync(cancellationToken);
        if (!resources.Any(r => r.ResourceUid == request.ClinicianUid && r.IsActive && r.ResourceType == "Provider")
            || request.RoomUid is Guid room && !resources.Any(r => r.ResourceUid == room && r.IsActive && r.ResourceType == "Room"))
            throw new ArgumentException("The selected clinician or room is unavailable in this schedule.");
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(request.StartDate.ToDateTime(TimeOnly.MinValue), zone);
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(request.StartDate.AddDays(request.HorizonDays).ToDateTime(TimeOnly.MinValue), zone);
        // Fail closed if the authoritative occupancy query cannot be read; never infer an empty schedule.
        var busy = await _repository.GetAvailabilityBusyPeriodsAsync(request.ClinicianUid, request.RoomUid, startUtc, endUtc, cancellationToken);
        var periods = busy.Select(p => (p.StartDateTimeUtc, p.EndDateTimeUtc)).ToList();
        var slots = new List<AvailableAppointmentSlot>();
        for (var offset = 0; offset < request.HorizonDays && slots.Count < 20; offset++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var date = request.StartDate.AddDays(offset);
            if (request.Weekdays is { Length: > 0 } && !request.Weekdays.Contains((int)date.DayOfWeek)) continue;
            // These are the existing calendar's operational hours, not provider-specific rosters.
            var localStart = date.ToDateTime(from < new TimeOnly(8, 0) ? new TimeOnly(8, 0) : from);
            var localEnd = date.ToDateTime(to > new TimeOnly(18, 0) ? new TimeOnly(18, 0) : to);
            if (localEnd <= localStart) continue;
            localStart = localStart.AddMinutes((15 - localStart.Minute % 15) % 15);
            if (zone.IsInvalidTime(localStart) || zone.IsAmbiguousTime(localStart)
                || zone.IsInvalidTime(localEnd) || zone.IsAmbiguousTime(localEnd)) continue;
            var workStart = TimeZoneInfo.ConvertTimeToUtc(localStart, zone);
            var workEnd = TimeZoneInfo.ConvertTimeToUtc(localEnd, zone);
            foreach (var (start, end) in SchedulingHelper.CalculateAvailableSlots([(workStart, workEnd)], periods, [], request.DurationMinutes))
            {
                if (start < now) continue;
                slots.Add(new(request.ClinicianUid, request.RoomUid, type, DateTime.SpecifyKind(start, DateTimeKind.Utc),
                    DateTime.SpecifyKind(end, DateTimeKind.Utc), TimeZoneInfo.ConvertTimeFromUtc(start, zone), TimeZoneInfo.ConvertTimeFromUtc(end, zone)));
                if (slots.Count == 20) break;
            }
        }
        return new() { TimeZoneId = zone.Id, Slots = slots };
    }

    public async Task<SchedulingDaySheetResponse> GetDaySheetAsync(
        SchedulingDaySheetRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var chronological = string.Equals(request.Order, "Chronological", StringComparison.OrdinalIgnoreCase);
        if (!chronological && !string.Equals(request.Order, "Alphabetic", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Day sheet order must be Alphabetic or Chronological.");
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
        var included = appointments.Where(appointment => selectedUids.Contains(appointment.PrimaryResourceUid)
            && appointment.StartDateTimeUtc < endUtc && appointment.EndDateTimeUtc > startUtc
            && !string.Equals(appointment.Status, "Cancelled", StringComparison.OrdinalIgnoreCase));
        var ordered = chronological
            ? included.OrderBy(appointment => appointment.StartDateTimeUtc)
                .ThenBy(appointment => clinicianNames[appointment.PrimaryResourceUid], StringComparer.OrdinalIgnoreCase)
                .ThenBy(appointment => string.IsNullOrWhiteSpace(appointment.PatientDisplayName)
                    ? "Unknown patient" : appointment.PatientDisplayName.Trim(), StringComparer.OrdinalIgnoreCase)
                .ThenBy(appointment => appointment.AppointmentUid)
            : included.OrderBy(appointment => string.IsNullOrWhiteSpace(appointment.PatientDisplayName)
                    ? "Unknown patient" : appointment.PatientDisplayName.Trim(), StringComparer.OrdinalIgnoreCase)
                .ThenBy(appointment => appointment.StartDateTimeUtc)
                .ThenBy(appointment => appointment.AppointmentUid);
        return new SchedulingDaySheetResponse
        {
            Order = chronological ? "Chronological" : "Alphabetic",
            Date = request.Date,
            ClinicianScope = request.ClinicianUids is null ? "All Clinicians"
                : string.Join(", ", clinicians.Where(resource => selectedUids.Contains(resource.ResourceUid))
                    .OrderBy(resource => resource.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .Select(resource => resource.DisplayName)),
            GeneratedAtUtc = DateTime.UtcNow,
            Appointments = ordered
                .Select(appointment => new SchedulingDaySheetRow
                {
                    IsAdHoc = appointment.IsAdHoc,
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
