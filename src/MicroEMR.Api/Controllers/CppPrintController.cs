using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Api.Authorization;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.ClinicalUsers;
using MicroEMR.Application.PatientCpp;
using MicroEMR.Application.SecurityAudit;

namespace MicroEMR.Api.Controllers;

[ApiController, Authorize, RequirePermission(PermissionKeys.PatientsView)]
[Route("api/patients/{patientUid:guid}/cpp/print")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CppPrintController(ICppPrintService service, ILogger<CppPrintController> logger) : ControllerBase
{
    [HttpGet("options")]
    public async Task<IActionResult> Options(CancellationToken token)
    {
        try { return Ok(await service.GetOptionsAsync(token)); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (ClinicalUserResolutionException) { return Forbid(); }
        catch (Exception error) { logger.LogWarning(error,"CPP print options unavailable."); return Problem(statusCode:503,detail:"CPP print options are unavailable."); }
    }

    [HttpPost, SensitiveCapability(SecurityAuditCapabilities.PatientChartView)]
    public async Task<IActionResult> Print(Guid patientUid, [FromBody] CppPrintRequest request, CancellationToken token)
    {
        try
        {
            var bytes = await service.PrintAsync(patientUid,request,HttpContext.TraceIdentifier,token);
            return bytes is null ? NotFound() : File(bytes,"application/pdf");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (ArgumentException error) { return Problem(statusCode:400,detail:error.Message); }
        catch (UnauthorizedAccessException error) { return Problem(statusCode:403,detail:error.Message); }
        catch (ClinicalUserResolutionException) { return Problem(statusCode:403,detail:"A clinical user is required to print CPP."); }
        catch (Exception error) when (error is CppPrintUnavailableException or MicroEMR.Application.ClinicalOutput.PdfRenderingException) { logger.LogWarning(error,"CPP print could not complete."); return Problem(statusCode:503,detail:error.Message); }
        catch (Exception error) { logger.LogError(error,"CPP print failed."); return Problem(statusCode:503,detail:"CPP printing is unavailable. Retry before printing."); }
    }
}
