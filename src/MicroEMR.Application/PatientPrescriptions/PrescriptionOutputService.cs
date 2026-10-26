using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.Json;

namespace MicroEMR.Application.PatientPrescriptions;

public sealed class PrescriptionSigningRequest
{
    [Required] public string RowVersion { get; set; } = "";
    public bool ConfirmSignature { get; set; }
    public bool PickupReviewed { get; set; }
}

public sealed record PrescriptionOutputRequester(string Subject, string Name, string CorrelationId);
public sealed record PrescriptionOutputOccurrence(Guid Uid, DateTimeOffset GeneratedAtUtc,
    PrescriptionOutputRequester Requester);
public sealed record PrescriptionPdfLayout(string Body, string Header, string Footer);

public interface IPrescriptionPdfRenderer
{
    Task<byte[]> RenderPrescriptionAsync(PrescriptionPdfLayout layout, CancellationToken token = default);
}

public interface IPrescriptionOutputRepository
{
    Task<PatientPrescriptionResponse?> SignAndFinalizeReviewedAsync(Guid p,Guid u,string v,long a,bool reviewed,CancellationToken t=default)=>SignAndFinalizeAsync(p,u,v,a,t);
    Task<PatientPrescriptionResponse?> SignAndFinalizeAsync(Guid patient, Guid prescription,
        string rowVersion, long actor, CancellationToken token = default);
    Task RecordGenerationAsync(Guid patient, Guid prescription, Guid artifact,
        PrescriptionOutputOccurrence occurrence, CancellationToken token = default);
}

public sealed class PrescriptionOutputService(IPatientPrescriptionRepository prescriptions,
    IPrescriptionOutputRepository output, IPatientPrescriptionService legacy,
    IPrescriptionPdfRenderer pdf, TimeProvider clock)
{
    public Task<PatientPrescriptionResponse?> SignAndFinalizeAsync(Guid patient, Guid prescription,
        PrescriptionSigningRequest request, long actor, CancellationToken token = default)
    {
        if (!request.ConfirmSignature || actor <= 0)
            throw new ArgumentException("Explicit authenticated signature confirmation is required.");
        if (string.IsNullOrWhiteSpace(request.RowVersion) ||
            !Convert.TryFromBase64String(request.RowVersion, new byte[8], out var length) || length != 8)
            throw new ArgumentException("A valid prescription row version is required.");
        return request.PickupReviewed ? output.SignAndFinalizeReviewedAsync(patient, prescription, request.RowVersion, actor, true, token) : output.SignAndFinalizeAsync(patient, prescription, request.RowVersion, actor, token);
    }

    public async Task<byte[]?> GenerateAsync(Guid patient, Guid prescription,
        PrescriptionOutputRequester requester, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(requester.Subject) || requester.Subject.Length > 450 ||
            string.IsNullOrWhiteSpace(requester.Name) || requester.Name.Length > 200 ||
            string.IsNullOrWhiteSpace(requester.CorrelationId) || requester.CorrelationId.Length > 100)
            throw new ArgumentException("A valid authenticated output requester is required.");
        var artifact = await prescriptions.GetArtifactAsync(patient, prescription, token);
        if (artifact is null) return null;
        using var snapshot = JsonDocument.Parse(artifact.Json);
        var occurrence = new PrescriptionOutputOccurrence(Guid.NewGuid(), clock.GetUtcNow(), requester);
        var bytes = snapshot.RootElement.TryGetProperty("PrescriptionOutputVersion", out var version) && version.GetInt32() == 2
            ? await pdf.RenderPrescriptionAsync(Compose(snapshot.RootElement, occurrence), token)
            : await legacy.RenderArtifactPdfAsync(patient, prescription, token);
        if (bytes is null || bytes.Length == 0)
            throw new InvalidOperationException("Prescription output generation returned no content.");
        // This records generation only. Delivery and physical printing cannot be asserted here.
        // Fail closed if the audit cannot commit; never rewrite the frozen clinical snapshot.
        await output.RecordGenerationAsync(patient, prescription, artifact.ArtifactUid, occurrence, token);
        return bytes;
    }

    public static PrescriptionPdfLayout Compose(JsonElement x, PrescriptionOutputOccurrence occurrence)
    {
        static string E(string? value) => WebUtility.HtmlEncode(value) ?? "";
        string G(string name) => x.TryGetProperty(name, out var v) && v.ValueKind != JsonValueKind.Null
            ? E(v.ToString()) : "Not recorded";
        string Pair(string label, string name) => $"<tr><th scope='row'>{label}</th><td>{G(name)}</td></tr>";
        var signature = x.TryGetProperty("SignatureVersion", out var marker) && marker.GetInt32() == 1
            ? $"Electronically signed by {G("PrescriberDisplayName")} ({G("PrescriberCredential")})<br>Signed: {G("SignedAtUtc")} UTC"
            : "No electronic signature recorded";
        var demographics = $"<b>{G("ClinicName")}</b> — {G("ClinicAddressLine1")} {G("ClinicAddressLine2")}, {G("ClinicCity")} {G("ClinicProvince")} {G("ClinicPostalCode")} · Phone: {G("ClinicPhone")}<br>" +
            $"<b>Patient: {G("PatientName")}</b> · DOB: {G("DateOfBirth")} · HCN: {G("HealthCardNumber")}<br>" +
            $"{G("PatientAddressLine1")} {G("PatientAddressLine2")}, {G("PatientCity")} {G("PatientProvince")} {G("PatientPostalCode")} · Phone: {G("PatientPhone")}";
        var header = $"<div style='font:9px Arial;flex:1;min-width:0;margin:0 0.65in;line-height:1.4;overflow-wrap:anywhere'>{demographics}</div>";
        var footer = $"<div style='font:9px Arial;flex:1;min-width:0;margin:0 0.65in;line-height:1.4;overflow-wrap:anywhere'>{signature}<br>" +
            $"Prepared by: {G("CreatedByDisplayName")} · Created: {G("CreatedAtUtc")} UTC<br>" +
            $"Print/reprint output requested by: {E(occurrence.Requester.Name)} ({E(occurrence.Requester.Subject)})<br>" +
            $"Output generated at: {occurrence.GeneratedAtUtc:yyyy-MM-dd HH:mm:ss.fffffff} UTC · Reference: {occurrence.Uid}<br>" +
            "Local structured prescription. Not electronically transmitted.<br>" +
            "Generated output; physical printing not confirmed. · Page <span class='pageNumber'></span> of <span class='totalPages'></span></div>";
        var body = "<!doctype html><html><head><meta charset='utf-8'><style>body{margin:0;font:11px/1.4 Arial;overflow-wrap:anywhere}table{width:100%;border-collapse:collapse;table-layout:fixed}th,td{text-align:left;vertical-align:top;padding:3px 0}th{width:30%;padding-right:12px}td{white-space:pre-wrap}h1{font-size:18px;margin:0 0 12px}</style></head><body><h1>Prescription</h1><table>" +
            Pair("Prescription ID", "PrescriptionUid") + Pair("Prescribed date", "PrescribedDate") +
            Pair("Medication", "ProductDisplayText") + (x.TryGetProperty("GenericNameVersion",out _) ? Pair("Generic name", "GenericName") : "") + Pair("Strength", "StrengthValue") + Pair("Strength unit", "StrengthUnit") +
            Pair("Medication form", "MedicationForm") + Pair("Dose", "DoseAmount") + Pair("Dose unit", "DoseUnit") +
            Pair("Route", "Route") + Pair("Frequency", "FrequencyDisplay") + Pair("As needed (PRN)", "Prn") +
            Pair("Directions", "Directions") + Pair("Start date", "StartDate") +
            Pair("Initial quantity", "Quantity") + Pair("Initial quantity unit", "QuantityUnit") + Pair("Initial days' supply", "InitialDaysSupply") +
            Pair("Authorized refills", "AuthorizedRepeats") + Pair("Refill quantity", "RefillQuantity") +
            Pair("Refill quantity unit", "RefillQuantityUnit") + Pair("Refill days' supply", "RefillDaysSupply") +
            Pair("Notes to pharmacist", "NotesToPharmacist") + Pair("Indication", "Indication") +
            "</table></body></html>";
        return new(body, header, footer);
    }
}
