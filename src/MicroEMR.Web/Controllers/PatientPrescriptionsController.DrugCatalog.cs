using MicroEMR.Application.DrugCatalog;
using MicroEMR.Application.PatientPrescriptions;

namespace MicroEMR.Web.Controllers;

public sealed partial class PatientPrescriptionsController
{
    private void BindDrugCatalog(PrescriptionDraftRequest request)
    {
        request.DrugCatalogSelection = null;
        if (!Request.HasFormContentType) return;
        var uid = Request.Form["DrugCatalogSelectionUid"].ToString();
        var version = Request.Form["DrugCatalogSelectionVersion"].ToString();
        if (uid.Length == 0 && version.Length == 0) return;
        try
        {
            if (!Guid.TryParse(uid, out var parsed) || parsed == Guid.Empty) throw new ArgumentException();
            DrugCatalogService.Version(version);
            request.DrugCatalogSelection = new(parsed, version);
        }
        catch (ArgumentException) { ModelState.AddModelError("DrugCatalogSelection", "Search and select the drug again, or use manual entry."); }
    }
}
