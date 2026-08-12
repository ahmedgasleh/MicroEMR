using MicroEMR.Application.PatientEncounters.Contracts;
using MicroEMR.Application.PatientEncounters.Repositories;
using MicroEMR.Application.Scheduling;
using MicroEMR.Application.Scheduling.Repositories;
using MicroEMR.Application.Scheduling.Services;
using MicroEMR.Application.PatientDocuments.Repositories;
using MicroEMR.Application.Templates.Runtime;
using MicroEMR.Application.Templates.Serialization;
using MicroEMR.Application.Templates.Definitions;
using MicroEMR.Application.Templates.Variables;
using MicroEMR.Application.Patients.Services;
using MicroEMR.Application.ClinicalOutput;

namespace MicroEMR.Application.PatientEncounters.Services;

public sealed class PatientEncounterService : IPatientEncounterService
{
    private readonly IPatientEncounterRepository _repository;
    private readonly ISchedulingAppointmentRepository _schedulingAppointmentRepository;
    private readonly IAppointmentStatusTransitionService _appointmentStatusTransitionService;
    private readonly IPatientDocumentRepository? _templates;
    private readonly IDocumentTemplateVersionRepository? _versions;
    private readonly ITemplateDefinitionSerializer? _definitions;
    private readonly ITemplateInstanceRuntime? _runtime;
    private readonly IPatientService? _patients;
    private readonly ITemplateVariableResolver? _variables;
    private readonly bool _structuredRuntimeAvailable;
    private readonly IClinicalArtifactService? _artifacts;

    public PatientEncounterService(
        IPatientEncounterRepository repository,
        ISchedulingAppointmentRepository schedulingAppointmentRepository,
        IAppointmentStatusTransitionService appointmentStatusTransitionService)
    {
        _repository = repository;
        _schedulingAppointmentRepository = schedulingAppointmentRepository;
        _appointmentStatusTransitionService = appointmentStatusTransitionService;
    }

    public PatientEncounterService(
        IPatientEncounterRepository repository,
        ISchedulingAppointmentRepository schedulingAppointmentRepository,
        IAppointmentStatusTransitionService appointmentStatusTransitionService,
        IPatientDocumentRepository templates,
        IDocumentTemplateVersionRepository versions,
        ITemplateDefinitionSerializer definitions,
        ITemplateInstanceRuntime runtime,
        IPatientService patients,
        ITemplateVariableResolver variables,
        IClinicalArtifactService? artifacts = null)
    {
        _repository = repository;
        _schedulingAppointmentRepository = schedulingAppointmentRepository;
        _appointmentStatusTransitionService = appointmentStatusTransitionService;
        _templates = templates;
        _versions = versions;
        _definitions = definitions;
        _runtime = runtime;
        _patients = patients;
        _variables = variables;
        _artifacts = artifacts;
        _structuredRuntimeAvailable = true;
    }

    public Task<IReadOnlyList<PatientEncounterListItemResponse>>
        GetByPatientUidAsync(
            Guid patientUid,
            CancellationToken cancellationToken = default)
    {
        return _repository.GetByPatientUidAsync(
            patientUid,
            cancellationToken);
    }

    public async Task<PatientEncounterDetailsResponse?> GetByUidAsync(
        Guid encounterUid,
        CancellationToken cancellationToken = default)
    {
        var encounter = await _repository.GetByUidAsync(encounterUid, cancellationToken);
        return encounter is null ? null : await EnrichAsync(encounter, cancellationToken);
    }

    public Task<IReadOnlyList<PatientEncounterHistoryResponse>> GetHistoryAsync(
        Guid patientUid,
        Guid encounterUid,
        CancellationToken cancellationToken = default)
    {
        if (patientUid == Guid.Empty)
            throw new ArgumentException("Patient identifier is required.", nameof(patientUid));
        if (encounterUid == Guid.Empty)
            throw new ArgumentException("Encounter identifier is required.", nameof(encounterUid));

        return _repository.GetHistoryAsync(patientUid, encounterUid, cancellationToken);
    }

    public Task<IReadOnlyList<PatientEncounterAddendumResponse>> GetAddendumsAsync(
        Guid patientUid,
        Guid encounterUid,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentifiers(patientUid, encounterUid);
        return _repository.GetAddendumsAsync(patientUid, encounterUid, cancellationToken);
    }

    public Task<PatientEncounterAddendumResponse?> CreateAddendumAsync(
        Guid patientUid,
        Guid encounterUid,
        CreateEncounterAddendumRequest request,
        long? createdBy,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentifiers(patientUid, encounterUid);
        if (string.IsNullOrWhiteSpace(request.AddendumText))
            throw new ArgumentException("Addendum text is required.", nameof(request));

        request.AddendumText = request.AddendumText.Trim();
        return _repository.CreateAddendumAsync(
            patientUid, encounterUid, request, createdBy, cancellationToken);
    }

    public async Task<PatientEncounterDetailsResponse> CreateAsync(
        Guid patientUid,
        CreatePatientEncounterRequest request,
        long? createdBy,
        string? createdByDisplayName,
        CancellationToken cancellationToken = default)
    {
        if (request.TemplateUid.HasValue)
        {
            var template = await _templates!.GetTemplateByUidAsync(request.TemplateUid.Value, cancellationToken)
                ?? throw new UnauthorizedAccessException("The selected encounter template is unavailable.");
            if (!template.IsActive || template.TemplateKind != "Encounter"
                || template.TemplateScope == "Personal" && template.OwnerUserId != createdBy)
                throw new UnauthorizedAccessException("The selected encounter template cannot be used.");
            var version = (await _versions!.GetByTemplateUidAsync(template.TemplateUid, cancellationToken))
                .SingleOrDefault(x => x.IsCurrent && x.Status == "Published")
                ?? throw new InvalidOperationException("The selected encounter template has no active published version.");
            EnsureVersionProvenance(template.TemplateUid, version.TemplateUid);
            var definition = RequireDefinition(version.DefinitionJson);
            var patient = await _patients!.GetByUidAsync(patientUid, cancellationToken)
                ?? throw new InvalidOperationException("The patient was not found.");
            ResolveDefinitionText(definition, new(patient.FullName, patient.DateOfBirth,
                createdByDisplayName, request.EncounterDateUtc, DateOnly.FromDateTime(DateTime.UtcNow)));
            var initial = _runtime!.CreateInitial(definition);
            if (!initial.IsValid) throw new TemplateInstanceValidationException(initial.Errors);
            request.ResolvedTemplateVersionUid = version.TemplateVersionUid;
            request.StructuredDataJson = initial.Json;
            var soap = CreateSoapSnapshots(definition, initial.Data!);
            request.SubjectiveSnapshot = soap.Subjective;
            request.ObjectiveSnapshot = soap.Objective;
            request.AssessmentSnapshot = soap.Assessment;
            request.PlanSnapshot = soap.Plan;
        }
        var created = await _repository.CreateAsync(patientUid, request, createdBy,
            createdByDisplayName, cancellationToken);
        return await EnrichAsync(created, cancellationToken);
    }

    public Task<PatientEncounterDetailsResponse?> UpdateNoteAsync(
        Guid patientUid,
        Guid encounterUid,
        UpdateEncounterNoteRequest request,
        long? updatedBy,
        CancellationToken cancellationToken = default)
    {
        if (patientUid == Guid.Empty)
            throw new ArgumentException("Patient identifier is required.", nameof(patientUid));

        if (encounterUid == Guid.Empty)
            throw new ArgumentException("Encounter identifier is required.", nameof(encounterUid));

        return _repository.UpdateNoteAsync(
            patientUid,
            encounterUid,
            request,
            updatedBy,
            cancellationToken);
    }

    public Task<PatientEncounterDetailsResponse?> UpdateSoapNoteAsync(
        Guid patientUid,
        Guid encounterUid,
        UpdateEncounterSoapNoteRequest request,
        long? updatedBy,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentifiers(patientUid, encounterUid);
        return _repository.UpdateSoapNoteAsync(
            patientUid, encounterUid, request, updatedBy, cancellationToken);
    }

    public async Task<PatientEncounterDetailsResponse?> UpdateStructuredDataAsync(
        Guid patientUid, Guid encounterUid, UpdateEncounterStructuredDataRequest request,
        long? updatedBy, CancellationToken cancellationToken = default)
    {
        ValidateIdentifiers(patientUid, encounterUid);
        var current = await _repository.GetByUidAsync(encounterUid, cancellationToken);
        if (current is null || current.PatientUid != patientUid) return null;
        if (!current.TemplateVersionUid.HasValue)
            throw new InvalidOperationException("The encounter is not schema-driven.");
        var version = await _versions!.GetByUidAsync(current.TemplateVersionUid.Value, cancellationToken)
            ?? throw new InvalidOperationException("The encounter's historical template version is unavailable.");
        EnsureVersionProvenance(current.TemplateUid, version.TemplateUid);
        var definition = RequireDefinition(version.DefinitionJson);
        var processed = _runtime!.Process(definition, request.StructuredDataJson);
        if (!processed.IsValid) throw new TemplateInstanceValidationException(processed.Errors);
        request.StructuredDataJson = processed.Json!;
        var soap = CreateSoapSnapshots(definition, processed.Data!);
        var saved = await _repository.UpdateStructuredDataAsync(patientUid, encounterUid, request,
            soap.Subjective, soap.Objective, soap.Assessment, soap.Plan, updatedBy, cancellationToken);
        return saved is null ? null : await EnrichAsync(saved, cancellationToken);
    }

    public async Task<PatientEncounterDetailsResponse?> SignAsync(
        Guid patientUid,
        Guid encounterUid,
        long? signedBy,
        CancellationToken cancellationToken = default)
    {
        if (patientUid == Guid.Empty)
            throw new ArgumentException("Patient identifier is required.", nameof(patientUid));

        if (encounterUid == Guid.Empty)
            throw new ArgumentException("Encounter identifier is required.", nameof(encounterUid));

        var current = _structuredRuntimeAvailable
            ? await _repository.GetByUidAsync(encounterUid, cancellationToken)
            : null;
        if (current is not null && current.PatientUid != patientUid) return null;
        if (current is not null && string.Equals(current.Status, "Signed", StringComparison.OrdinalIgnoreCase))
        {
            if (current.TemplateVersionUid.HasValue && _artifacts is not null)
                await _artifacts.EnsureEncounterFinalPdfAsync(encounterUid, signedBy, cancellationToken);
            return await EnrichAsync(current, cancellationToken);
        }
        if (current?.TemplateVersionUid is Guid versionUid)
        {
            var version = await _versions!.GetByUidAsync(versionUid, cancellationToken)
                ?? throw new InvalidOperationException("The encounter's historical template version is unavailable.");
            EnsureVersionProvenance(current.TemplateUid, version.TemplateUid);
            var validation = _runtime!.Process(RequireDefinition(version.DefinitionJson), current.StructuredDataJson);
            if (!validation.IsValid) throw new TemplateInstanceValidationException(validation.Errors);
        }
        _appointmentStatusTransitionService.EnsureCanTransition(
            AppointmentStatus.Seen,
            AppointmentStatus.Completed);

        var signed = await _repository.SignAsync(
            patientUid,
            encounterUid,
            signedBy,
            AppointmentStatus.Seen,
            AppointmentStatus.Completed,
            cancellationToken);
        if (signed?.TemplateVersionUid.HasValue == true && _artifacts is not null)
            await _artifacts.EnsureEncounterFinalPdfAsync(encounterUid, signedBy, cancellationToken);
        return signed is null ? null : await EnrichAsync(signed, cancellationToken);
    }

    private async Task<PatientEncounterDetailsResponse> EnrichAsync(PatientEncounterDetailsResponse encounter, CancellationToken token)
    {
        if (!encounter.TemplateVersionUid.HasValue) return encounter;
        var version = await _versions!.GetByUidAsync(encounter.TemplateVersionUid.Value, token)
            ?? throw new InvalidOperationException("The encounter's historical template version is unavailable.");
        EnsureVersionProvenance(encounter.TemplateUid, version.TemplateUid);
        encounter.TemplateDefinition = RequireDefinition(version.DefinitionJson);
        var patient = await _patients!.GetByUidAsync(encounter.PatientUid, token)
            ?? throw new InvalidOperationException("The encounter patient is unavailable.");
        ResolveDefinitionText(encounter.TemplateDefinition, new(patient.FullName, patient.DateOfBirth,
            encounter.ProviderName ?? encounter.CreatedByDisplayName, encounter.EncounterDateUtc,
            DateOnly.FromDateTime(DateTime.UtcNow)));
        encounter.TemplateVersionNumber = version.VersionNumber;
        if (encounter.TemplateUid.HasValue)
            encounter.TemplateName = (await _templates!.GetTemplateByUidAsync(encounter.TemplateUid.Value, token))?.TemplateName;
        return encounter;
    }

    private TemplateDefinition RequireDefinition(string json)
    {
        var result = _definitions!.Process(json);
        if (!result.IsValid) throw new TemplateInstanceValidationException(result.Errors
            .Select(x => new TemplateInstanceValidationError(x.Path, x.Code, x.Message)).ToArray());
        return result.Definition!;
    }

    private void ResolveDefinitionText(TemplateDefinition definition, TemplateVariableContext context)
    {
        foreach (var field in definition.Sections!.SelectMany(x => x.Fields!))
        {
            if (field.Type == TemplateFieldTypes.StaticText && field.Content is not null)
                field.Content = _variables!.Resolve(field.Content, context);
            if (field.Type is TemplateFieldTypes.Text or TemplateFieldTypes.TextArea && field.DefaultValue is not null)
                field.DefaultValue = _variables!.Resolve(field.DefaultValue, context);
        }
    }

    private (string? Subjective, string? Objective, string? Assessment, string? Plan) CreateSoapSnapshots(
        TemplateDefinition definition, TemplateInstanceData data)
    {
        string? Section(string key)
        {
            var section = definition.Sections!.SingleOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
            if (section is null) return null;
            var subset = new TemplateDefinition { SchemaVersion = definition.SchemaVersion, Sections = [section] };
            var value = _runtime!.RenderSnapshot(subset, data);
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        return (Section("subjective"), Section("objective"), Section("assessment"), Section("plan"));
    }

    public async Task<StartEncounterFromAppointmentResponse?> StartFromAppointmentAsync(
        Guid appointmentUid,
        long? createdBy,
        CancellationToken cancellationToken = default)
    {
        if (appointmentUid == Guid.Empty)
            throw new ArgumentException("Appointment identifier is required.", nameof(appointmentUid));

        var appointmentStatus = await _schedulingAppointmentRepository.GetStatusAsync(
            appointmentUid,
            cancellationToken);
        if (appointmentStatus is null)
            return null;

        switch (appointmentStatus.Value)
        {
            case AppointmentStatus.Cancelled:
                throw new AppointmentCancelledException(
                    "Cancelled appointments cannot start encounters.");
            case AppointmentStatus.NoShow:
                throw new AppointmentNoShowException(
                    "No-show appointments cannot start encounters.");
            case AppointmentStatus.Completed:
                throw new AppointmentCompletedException(
                    "Completed appointments cannot start new encounters.");
            case AppointmentStatus.Seen:
                break;
            default:
                _appointmentStatusTransitionService.EnsureCanTransition(
                    appointmentStatus.Value,
                    AppointmentStatus.Seen);
                break;
        }

        return await _repository.StartFromAppointmentAsync(
            appointmentUid, createdBy, cancellationToken);
    }

    private static void ValidateIdentifiers(Guid patientUid, Guid encounterUid)
    {
        if (patientUid == Guid.Empty)
            throw new ArgumentException("Patient identifier is required.", nameof(patientUid));
        if (encounterUid == Guid.Empty)
            throw new ArgumentException("Encounter identifier is required.", nameof(encounterUid));
    }

    private static void EnsureVersionProvenance(Guid? templateUid, Guid versionTemplateUid)
    {
        if (templateUid.HasValue && versionTemplateUid != templateUid.Value)
            throw new InvalidOperationException("The template version does not belong to the encounter template.");
    }
}
