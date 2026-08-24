using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Api.Authorization;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.ClinicalDataMigration;

namespace MicroEMR.Api.Controllers;

[ApiController]
[Authorize]
[RequirePermission(PermissionKeys.UsersManageAccess)]
[Route("api/data-migration")]
public sealed class ClinicalDataMigrationController(IClinicalDataMigrationValidationService service,IClinicalDataMigrationImportService importService,ILogger<ClinicalDataMigrationController> logger):ControllerBase
{
    [HttpPost("validate")]
    [RequestSizeLimit(5*1024*1024)]
    public async Task<ActionResult<ClinicalMigrationValidationReport>> Validate(ClinicalMigrationPackageV1 package,CancellationToken token)
    {
        try{return Ok(await service.ValidateAsync(package,ClinicalUserActorContext.GetRequired(HttpContext),token));}
        catch(ClinicalMigrationPackageException exception){return BadRequest(new{code=exception.Code,message="The canonical migration package failed validation."});}
        catch(InvalidOperationException exception){logger.LogWarning(exception,"Clinical migration validation could not be completed.");return Conflict(new{message="The migration package could not be validated."});}
    }

    [HttpGet("batches/{batchUid:guid}")]
    public async Task<ActionResult<ClinicalMigrationValidationReport>> Get(Guid batchUid,CancellationToken token)=>
        await service.GetReportAsync(batchUid,token) is{} report?Ok(report):NotFound();

    [HttpGet("batches/{batchUid:guid}/issues")]
    public async Task<ActionResult<IReadOnlyList<ClinicalMigrationIssue>>> Issues(Guid batchUid,int page=1,int pageSize=50,CancellationToken token=default)=>
        Ok(await service.ListIssuesAsync(batchUid,page,pageSize,token));

    [HttpPost("batches/{batchUid:guid}/import")]
    public async Task<ActionResult<ClinicalMigrationImportResult>> Import(Guid batchUid,CancellationToken token)
    {
        try{return await importService.ImportAsync(batchUid,ClinicalUserActorContext.GetRequired(HttpContext),token) is{} result?Ok(result):NotFound();}
        catch(Microsoft.Data.SqlClient.SqlException exception) when(exception.Number is >=52500 and <=52508)
        {logger.LogWarning("Controlled clinical import rejected for batch {MigrationBatchUid} with code {ErrorCode}.",batchUid,exception.Number);return Conflict(new{code=exception.Number,message="The validated migration batch is not eligible for import."});}
    }
}
