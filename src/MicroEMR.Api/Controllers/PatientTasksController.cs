using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using MicroEMR.Application.PatientTasks;

namespace MicroEMR.Api.Controllers;

[ApiController, Authorize, Route("api/patients/{patientUid:guid}/tasks")]
public sealed class PatientTasksController : ControllerBase
{
    private static readonly HashSet<string> Statuses = new(["Open", "Completed", "All"], StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> Types = new(["General", "Follow-up", "Call Patient", "Review Result", "Form", "Referral", "Booking"], StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> Priorities = new(["Low", "Normal", "High", "Urgent"], StringComparer.OrdinalIgnoreCase);
    private readonly IPatientTaskRepository _repository;
    private readonly ILogger<PatientTasksController> _logger;
    public PatientTasksController(IPatientTaskRepository repository, ILogger<PatientTasksController> logger) { _repository = repository; _logger = logger; }

    [HttpGet]
    public async Task<IActionResult> List(Guid patientUid, string status = "Open", CancellationToken cancellationToken = default)
    {
        if (patientUid == Guid.Empty || !Statuses.Contains(status)) return BadRequest(new { message = "Invalid patient or status filter." });
        return Ok(await _repository.GetByPatientUidAsync(patientUid, status, cancellationToken));
    }
    [HttpGet("{patientTaskUid:guid}")]
    public async Task<IActionResult> Get(Guid patientUid, Guid patientTaskUid, CancellationToken cancellationToken) =>
        patientUid == Guid.Empty || patientTaskUid == Guid.Empty ? BadRequest() :
        await _repository.GetByUidAsync(patientUid, patientTaskUid, cancellationToken) is { } item ? Ok(item) : NotFound();
    [HttpPost]
    public Task<IActionResult> Create(Guid patientUid, CreatePatientTaskRequest request, CancellationToken cancellationToken) =>
        Mutate(patientUid, request, () => _repository.CreateAsync(patientUid, request, UserId(), cancellationToken), true);
    [HttpPut("{patientTaskUid:guid}")]
    public Task<IActionResult> Update(Guid patientUid, Guid patientTaskUid, UpdatePatientTaskRequest request, CancellationToken cancellationToken) =>
        Mutate(patientUid, request, () => _repository.UpdateAsync(patientUid, patientTaskUid, request, UserId(), cancellationToken));
    [HttpPost("{patientTaskUid:guid}/complete")]
    public Task<IActionResult> Complete(Guid patientUid, Guid patientTaskUid, CompletePatientTaskRequest request, CancellationToken cancellationToken) =>
        Mutate(patientUid, request, () => _repository.CompleteAsync(patientUid, patientTaskUid, request, UserId(), cancellationToken));
    [HttpPost("{patientTaskUid:guid}/reopen")]
    public Task<IActionResult> Reopen(Guid patientUid, Guid patientTaskUid, CancellationToken cancellationToken) =>
        Mutate(patientUid, null, () => _repository.ReopenAsync(patientUid, patientTaskUid, UserId(), cancellationToken));

    private async Task<IActionResult> Mutate(Guid patientUid, object? request, Func<Task<PatientTaskResponse?>> action, bool created = false)
    {
        if (patientUid == Guid.Empty) return BadRequest(new { message = "Patient identifier is required." });
        if (request is SavePatientTaskRequest save && (string.IsNullOrWhiteSpace(save.TaskTitle) || !Types.Contains(save.TaskType ?? "General") || !Priorities.Contains(save.TaskPriority ?? "Normal")))
            return BadRequest(new { message = "Task title, type, or priority is invalid." });
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try
        {
            var item = await action();
            return item is null ? NotFound() : created ? CreatedAtAction(nameof(Get), new { patientUid=item.PatientUid, patientTaskUid=item.PatientTaskUid }, item) : Ok(item);
        }
        catch (SqlException exception) when (exception.Number == 51302) { return Conflict(new { message = "Completed tasks cannot be edited." }); }
        catch (SqlException exception) when (exception.Number is 51300 or 51301) { return BadRequest(new { message = exception.Message }); }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Patient task operation failed for patient {PatientUid}.", patientUid);
            return StatusCode(500, new { message = "The patient task operation could not be completed." });
        }
    }
    private long? UserId() { var value=User.FindFirstValue("user_id")??User.FindFirstValue(ClaimTypes.NameIdentifier)??User.FindFirstValue("sub"); return long.TryParse(value,out var id)?id:null; }
}

[ApiController, Authorize, Route("api/patient-tasks")]
public sealed class PatientTaskDashboardController : ControllerBase
{
    private readonly IPatientTaskRepository _repository;
    public PatientTaskDashboardController(IPatientTaskRepository repository) => _repository = repository;
    [HttpGet("open")]
    public async Task<IActionResult> Open(int maxRows = 10, CancellationToken cancellationToken = default)
    {
        if (maxRows is < 1 or > 50) return BadRequest(new { message = "maxRows must be between 1 and 50." });
        var value=User.FindFirstValue("user_id")??User.FindFirstValue(ClaimTypes.NameIdentifier)??User.FindFirstValue("sub");
        long? userId=long.TryParse(value,out var id)?id:null;
        return Ok(await _repository.GetOpenForDashboardAsync(userId, maxRows, cancellationToken));
    }
}
