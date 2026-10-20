using MicroEMR.Application.PatientPrescriptions;

namespace MicroEMR.Web.Models;

public sealed record PrescriptionDetailViewModel(PatientPrescriptionResponse Prescription,
    PrescriptionPrivateNoteDetail? PrivateNote, IReadOnlyList<PrescriptionPrivateNoteRevision> History);
