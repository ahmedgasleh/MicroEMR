using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Application.EncounterSoapTemplates;

namespace MicroEMR.Api.Controllers;

[ApiController, Authorize, Route("api/encounter-soap-templates")]
public sealed class EncounterSoapTemplatesController : ControllerBase
{
    private readonly IEncounterSoapTemplateRepository _repository;
    private readonly ILogger<EncounterSoapTemplatesController> _logger;
    public EncounterSoapTemplatesController(IEncounterSoapTemplateRepository repository,ILogger<EncounterSoapTemplatesController> logger){_repository=repository;_logger=logger;}
    [HttpGet] public async Task<IActionResult> GetAll(string status="Active",CancellationToken token=default)=>Ok(await _repository.GetAllAsync(status,token));
    [HttpGet("{uid:guid}")] public async Task<IActionResult> Get(Guid uid,CancellationToken token)=>uid==Guid.Empty?BadRequest():await _repository.GetByUidAsync(uid,token) is { } x?Ok(x):NotFound();
    [HttpPost] public async Task<IActionResult> Create(CreateEncounterSoapTemplateRequest request,CancellationToken token)=>await Mutate(()=>_repository.CreateAsync(request,UserId(),token),true);
    [HttpPut("{uid:guid}")] public async Task<IActionResult> Update(Guid uid,UpdateEncounterSoapTemplateRequest request,CancellationToken token)=>uid==Guid.Empty?BadRequest():await Mutate(()=>_repository.UpdateAsync(uid,request,UserId(),token));
    [HttpPost("{uid:guid}/set-active")] public async Task<IActionResult> SetActive(Guid uid,SetEncounterSoapTemplateActiveRequest request,CancellationToken token)=>uid==Guid.Empty?BadRequest():await Mutate(()=>_repository.SetActiveAsync(uid,request.IsActive,UserId(),token));
    private async Task<IActionResult> Mutate(Func<Task<EncounterSoapTemplateResponse?>> action,bool created=false){try{var x=await action();return x is null?NotFound():created?CreatedAtAction(nameof(Get),new{uid=x.EncounterSoapTemplateUid},x):Ok(x);}catch(Exception ex){_logger.LogError(ex,"Encounter SOAP template operation failed.");return StatusCode(500,new{message="The template operation could not be completed."});}}
    private long? UserId(){var v=User.FindFirstValue("user_id")??User.FindFirstValue(ClaimTypes.NameIdentifier)??User.FindFirstValue("sub");return long.TryParse(v,out var id)?id:null;}
}
