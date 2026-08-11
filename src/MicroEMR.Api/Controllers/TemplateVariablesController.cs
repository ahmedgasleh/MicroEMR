using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Application.Templates.Variables;

namespace MicroEMR.Api.Controllers;

[ApiController, Authorize, Route("api/document-templates/variables")]
public sealed class TemplateVariablesController(ITemplateVariableResolver resolver) : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(resolver.Registry);
}
