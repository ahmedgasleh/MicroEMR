using Microsoft.AspNetCore.Authorization;
using MicroEMR.Api.Authorization;
using MicroEMR.Application.AccessProfiles;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Application.Templates.Variables;

namespace MicroEMR.Api.Controllers;

[ApiController, Authorize, Route("api/document-templates/variables")]
[RequirePermission(PermissionKeys.TemplatesUse)]
public sealed class TemplateVariablesController(ITemplateVariableResolver resolver) : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(resolver.Registry);
}
