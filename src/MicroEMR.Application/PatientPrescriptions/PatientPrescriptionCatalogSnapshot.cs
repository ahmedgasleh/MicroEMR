using MicroEMR.Application.DrugCatalog;

namespace MicroEMR.Application.PatientPrescriptions;

public sealed partial class PatientPrescriptionResponse
{
    public CatalogDrug? CatalogDrug { get; set; }
}
