using Microsoft.AspNetCore.Mvc;
using MicroEMR.Application.Scheduling.DTOs;
using MicroEMR.Application.Scheduling.Services;
using MicroEMR.Web.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace MicroEMR.Web.Controllers.Scheduling;
[Authorize(Roles = AppRoles.SchedulingStaff)]
public class SchedulingController : Controller
{
    private readonly ICalendarService _calendarService;
    private readonly IAppointmentService _appointmentService;
    private readonly IScheduleSlotService _scheduleSlotService;
    private readonly IResourceBlockService _resourceBlockService;
    private readonly ILogger<SchedulingController> _logger;

    public SchedulingController(
        ICalendarService calendarService,
        IAppointmentService appointmentService,
        IScheduleSlotService scheduleSlotService,
        IResourceBlockService resourceBlockService,
        ILogger<SchedulingController> logger)
    {
        _calendarService = calendarService;
        _appointmentService = appointmentService;
        _scheduleSlotService = scheduleSlotService;
        _resourceBlockService = resourceBlockService;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        return View();
    }

    public async Task<IActionResult> Calendar(Guid providerId, Guid? clinicResourceId, DateTime? viewDate)
    {
        try
        {
            var date = viewDate ?? DateTime.Today;
            var calendar = await _calendarService.GetCalendarViewAsync(providerId, clinicResourceId, date);
            return View(calendar);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading calendar");
            TempData["Error"] = "Error loading calendar";
            return RedirectToAction(nameof(Index));
        }
    }

    public async Task<IActionResult> CreateAppointment(Guid providerId, Guid? slotId)
    {
        var model = new CreateAppointmentViewModel
        {
            ProviderId = providerId,
            SlotId = slotId
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAppointment(CreateAppointmentViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        try
        {
            var request = new CreateAppointmentRequest
            {
                PatientId = model.PatientId,
                ProviderId = model.ProviderId,
                ClinicResourceId = model.ClinicResourceId,
                StartAt = model.StartAt,
                EndAt = model.EndAt,
                AppointmentType = model.AppointmentType,
                Notes = model.Notes
            };

            var userId = GetCurrentUserId();
            var appointment = await _appointmentService.CreateAppointmentAsync(request, userId);
            
            _logger.LogInformation("Appointment created successfully for patient {PatientId}", model.PatientId);
            TempData["Success"] = "Appointment created successfully";
            return RedirectToAction(nameof(Calendar), new { providerId = model.ProviderId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating appointment");
            ModelState.AddModelError(string.Empty, "Error creating appointment");
            return View(model);
        }
    }

    public async Task<IActionResult> RescheduleAppointment(Guid appointmentId)
    {
        try
        {
            var appointment = await _appointmentService.GetAppointmentAsync(appointmentId);
            if (appointment == null)
                return NotFound();

            var model = new RescheduleAppointmentViewModel
            {
                AppointmentId = appointmentId,
                PatientId = appointment.PatientId,
                PatientName = appointment.PatientName,
                ProviderId = appointment.ProviderId,
                ProviderName = appointment.ProviderName,
                CurrentStartAt = appointment.StartAt,
                CurrentEndAt = appointment.EndAt
            };

            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading reschedule form");
            TempData["Error"] = "Error loading reschedule form";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RescheduleAppointment(RescheduleAppointmentViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        try
        {
            var request = new RescheduleAppointmentRequest
            {
                AppointmentId = model.AppointmentId,
                NewStartAt = model.NewStartAt,
                NewEndAt = model.NewEndAt,
                Reason = model.Reason
            };

            var userId = GetCurrentUserId();
            var appointment = await _appointmentService.RescheduleAppointmentAsync(request, userId);
            
            _logger.LogInformation("Appointment {AppointmentId} rescheduled", model.AppointmentId);
            TempData["Success"] = "Appointment rescheduled successfully";
            return RedirectToAction(nameof(Calendar), new { providerId = model.ProviderId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rescheduling appointment");
            ModelState.AddModelError(string.Empty, "Error rescheduling appointment");
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelAppointment(Guid appointmentId, string reason)
    {
        try
        {
            var request = new CancelAppointmentRequest
            {
                AppointmentId = appointmentId,
                Reason = reason
            };

            var userId = GetCurrentUserId();
            await _appointmentService.CancelAppointmentAsync(request, userId);
            
            _logger.LogInformation("Appointment {AppointmentId} cancelled", appointmentId);
            TempData["Success"] = "Appointment cancelled successfully";
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling appointment");
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    public async Task<IActionResult> AppointmentHistory(Guid appointmentId)
    {
        try
        {
            var history = await _appointmentService.GetAppointmentHistoryAsync(appointmentId);
            return View(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading appointment history");
            TempData["Error"] = "Error loading appointment history";
            return RedirectToAction(nameof(Index));
        }
    }

    public async Task<IActionResult> ManageResourceBlocks(Guid providerId, Guid resourceId)
    {
        try
        {
            var startDate = DateTime.Today;
            var endDate = DateTime.Today.AddDays(30);
            var blocks = await _resourceBlockService.GetResourceBlocksAsync(resourceId, startDate, endDate);
            
            var model = new ManageResourceBlocksViewModel
            {
                ProviderId = providerId,
                ResourceId = resourceId,
                ResourceBlocks = blocks
            };

            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading resource blocks");
            TempData["Error"] = "Error loading resource blocks";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateResourceBlock(CreateResourceBlockViewModel model)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var request = new CreateResourceBlockRequest
            {
                ResourceId = model.ResourceId,
                ProviderId = model.ProviderId,
                BlockStartTime = model.BlockStartTime,
                BlockEndTime = model.BlockEndTime,
                Reason = model.Reason,
                BlockType = model.BlockType
            };

            var userId = GetCurrentUserId();
            await _resourceBlockService.CreateBlockAsync(request, userId);
            
            _logger.LogInformation("Resource block created for resource {ResourceId}", model.ResourceId);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating resource block");
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst("sub") ?? User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");
        return Guid.TryParse(userIdClaim?.Value, out var userId) ? userId : Guid.Empty;
    }
}

#region ViewModels

public class CreateAppointmentViewModel
{
    public Guid ProviderId { get; set; }
    public Guid? SlotId { get; set; }
    public Guid PatientId { get; set; }
    public Guid? ClinicResourceId { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public string? AppointmentType { get; set; }
    public string? Notes { get; set; }
}

public class RescheduleAppointmentViewModel
{
    public Guid AppointmentId { get; set; }
    public Guid PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public Guid ProviderId { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public DateTime CurrentStartAt { get; set; }
    public DateTime CurrentEndAt { get; set; }
    public DateTime NewStartAt { get; set; }
    public DateTime NewEndAt { get; set; }
    public string? Reason { get; set; }
}

public class ManageResourceBlocksViewModel
{
    public Guid ProviderId { get; set; }
    public Guid ResourceId { get; set; }
    public List<ResourceBlockDto> ResourceBlocks { get; set; } = new();
}

public class CreateResourceBlockViewModel
{
    public Guid ProviderId { get; set; }
    public Guid ResourceId { get; set; }
    public DateTime BlockStartTime { get; set; }
    public DateTime BlockEndTime { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? BlockType { get; set; }
}

#endregion
