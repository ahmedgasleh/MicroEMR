using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Web.Services.PatientDocuments;

namespace MicroEMR.Web.Controllers;

[Authorize]
public sealed class PatientDocumentsController : Controller
{
    private readonly IPatientDocumentApiClient _documentApiClient;

    public PatientDocumentsController (
        IPatientDocumentApiClient documentApiClient )
    {
        _documentApiClient = documentApiClient;
    }

    [HttpGet]
    public async Task<IActionResult> Details (
        Guid documentUid,
        CancellationToken cancellationToken )
    {
        if (documentUid == Guid.Empty)
        {
            return BadRequest();
        }

        var document =
            await _documentApiClient.GetByUidAsync(
                documentUid,
                cancellationToken);

        if (document is null)
        {
            return NotFound();
        }

        return View(document);
    }
}
