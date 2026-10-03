using System.Reflection;
using MicroEMR.Application.PatientCareTeam;
using MicroEMR.Application.PatientDocuments.Contracts;
using MicroEMR.Application.PatientDocuments.Repositories;
using MicroEMR.Application.PatientDocuments.Services;
using MicroEMR.Application.Providers;
using Xunit;
using Microsoft.Extensions.Logging.Abstractions;

namespace MicroEMR.Api.Tests;

public sealed class ConsultationRecipientServiceTests
{
    private static readonly Guid PatientUid = Guid.Parse("1146334d-6d73-44f1-9d17-40fa54f14986");
    private static readonly Guid DocumentUid = Guid.Parse("ac725455-f06c-4e1a-bfd4-5064d92c84df");
    private static readonly Guid TemplateUid = Guid.Parse("829b3d2d-1429-4dc3-b127-9e6b60813ba4");
    private static readonly Guid ReferringUid = Guid.Parse("2a863030-4310-43e1-88bc-3379675d82ec");
    private static readonly Guid PcpUid = Guid.Parse("eb545fff-d15d-42cf-9d8d-256409af23f8");
    private const string Version = "AAAAAAAAAAE=";

    [Fact]
    public async Task ActivePrimaryReferringAndPcpAreSuggestedInToCcOrder()
    {
        var service = Service([Relation(ReferringUid, "REFERRING"), Relation(PcpUid, "PCP")]);
        var state = await service.GetAsync(PatientUid, DocumentUid, true);
        Assert.NotNull(state);
        Assert.Equal(["TO", "CC"], state.Suggestions.Select(x => x.RecipientType));
        Assert.Equal([ReferringUid, PcpUid], state.Suggestions.Select(x => x.ProviderUid));
        Assert.Equal([1, 2], state.Suggestions.Select(x => x.RecipientOrder));
        Assert.Empty(state.Recipients);
    }

    [Theory]
    [InlineData(true, false, "TO")]
    [InlineData(false, true, "CC")]
    [InlineData(false, false, null)]
    public async Task MissingRolesDoNotCreateFallbackSuggestions(bool referring, bool pcp, string? expected)
    {
        var relationships = new List<PatientCareTeamRelationship>();
        if (referring) relationships.Add(Relation(ReferringUid, "REFERRING"));
        if (pcp) relationships.Add(Relation(PcpUid, "PCP"));
        relationships.Add(Relation(Guid.NewGuid(), "ATTENDING"));
        var state = await Service(relationships).GetAsync(PatientUid, DocumentUid, true);
        Assert.Equal(expected is null ? [] : [expected], state!.Suggestions.Select(x => x.RecipientType));
    }

    [Fact]
    public async Task HistoricalNonPrimaryFutureAndInactiveProviderRelationshipsAreExcluded()
    {
        var relationships = new[]
        {
            Relation(ReferringUid, "REFERRING") with { IsPrimary = false },
            Relation(PcpUid, "PCP") with { IsActive = false, EndDate = DateOnly.FromDateTime(DateTime.UtcNow) },
            Relation(Guid.NewGuid(), "REFERRING") with { StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)) },
            Relation(Guid.NewGuid(), "PCP")
        };
        var state = await Service(relationships).GetAsync(PatientUid, DocumentUid, true);
        Assert.Empty(state!.Suggestions);
    }

    [Fact]
    public async Task SavedRecipientsAndConfirmedEmptySetNeverReapplyCareTeamSuggestions()
    {
        var snapshot = new PatientDocumentRecipientResponse(Guid.NewGuid(), 1, "TO", ReferringUid,
            "Original referring provider", "Original clinic", "416-555-0100");
        var saved = await Service([Relation(PcpUid, "PCP")], [snapshot]).GetAsync(PatientUid, DocumentUid, true);
        Assert.Equal([snapshot], saved!.Recipients);
        Assert.Empty(saved.Suggestions);

        var confirmedEmpty = await Service([Relation(ReferringUid, "REFERRING")], [], true)
            .GetAsync(PatientUid, DocumentUid, true);
        Assert.Empty(confirmedEmpty!.Recipients);
        Assert.Empty(confirmedEmpty.Suggestions);
    }

    [Fact]
    public async Task MissingCareTeamReadPermissionOmitsSuggestionsWithoutBlockingRecipientState()
    {
        var state = await Service([Relation(ReferringUid, "REFERRING")])
            .GetAsync(PatientUid, DocumentUid, includeCareTeamSuggestions: false);
        Assert.NotNull(state);
        Assert.Empty(state.Suggestions);
        Assert.Equal(Version, state.RowVersion);
    }

    [Fact]
    public async Task UnavailableSuggestionsDoNotBlockManualRecipientEditing()
    {
        var state = await Service([], suggestionFailure: new InvalidOperationException("Unavailable"))
            .GetAsync(PatientUid, DocumentUid, true);
        Assert.NotNull(state);
        Assert.Empty(state.Suggestions);
        Assert.Equal(Version, state.RowVersion);
    }

    [Fact]
    public async Task SuggestionCancellationIsNotSwallowed()
    {
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Service([], suggestionFailure: new OperationCanceledException())
                .GetAsync(PatientUid, DocumentUid, true));
    }

    [Fact]
    public async Task SamePrimaryProviderForBothRolesIsSuggestedOnlyAsTo()
    {
        var state = await Service([Relation(ReferringUid, "REFERRING"), Relation(ReferringUid, "PCP")])
            .GetAsync(PatientUid, DocumentUid, true);
        Assert.Equal("TO", Assert.Single(state!.Suggestions).RecipientType);
    }

    [Fact]
    public async Task OtherPatientAndEndedRelationshipsAreNotSuggested()
    {
        var state = await Service([
            Relation(ReferringUid, "REFERRING") with { PatientUid = Guid.NewGuid() },
            Relation(PcpUid, "PCP") with { EndDate = DateOnly.FromDateTime(DateTime.UtcNow) }
        ]).GetAsync(PatientUid, DocumentUid, true);
        Assert.Empty(state!.Suggestions);
    }

    [Fact]
    public async Task ReplacementUsesExistingPatientDocumentRecipientWritePath()
    {
        var request = new ReplacePatientDocumentRecipientsRequest
        {
            RowVersion = Version,
            Recipients = [new(1, "TO", ReferringUid), new(2, "CC", PcpUid)]
        };
        var expected = new PatientDocumentRecipientsResponse(DocumentUid, "AAAAAAAAAAI=", []);
        var invoked = false;
        var service = Service([], replacement: (patient, document, received, actor) =>
        {
            invoked = true;
            Assert.Equal(PatientUid, patient);
            Assert.Equal(DocumentUid, document);
            Assert.Same(request, received);
            Assert.Equal(42, actor);
            return expected;
        });
        Assert.Same(expected, await service.ReplaceAsync(PatientUid, DocumentUid, request, 42));
        Assert.True(invoked);
        Assert.Null(await service.ReplaceAsync(Guid.NewGuid(), DocumentUid, request, 42));
    }

    [Fact]
    public async Task UnrelatedDocumentTypeHasNoConsultationRecipientState()
    {
        var service = Service([], templateType: "GENERAL_NOTE");
        Assert.Null(await service.GetAsync(PatientUid, DocumentUid, true));
        Assert.Null(await service.ReplaceAsync(PatientUid, DocumentUid,
            new ReplacePatientDocumentRecipientsRequest { RowVersion = Version }, 42));
    }

    [Fact]
    public async Task SameProviderCannotBeSavedTwiceEvenAcrossToAndCc()
    {
        var documents = new PatientDocumentService(
            Proxy<IPatientDocumentRepository>((method, _) => throw new InvalidOperationException(method.Name)),
            null!, null!, null!, null!);
        var request = new ReplacePatientDocumentRecipientsRequest
        {
            RowVersion = Version,
            Recipients = [new(1, "TO", ReferringUid), new(2, "CC", ReferringUid)]
        };
        await Assert.ThrowsAsync<ArgumentException>(() =>
            documents.ReplaceDraftRecipientsAsync(PatientUid, DocumentUid, request, 42));
    }

    private static ConsultationRecipientService Service(
        IReadOnlyList<PatientCareTeamRelationship> relationships,
        IReadOnlyList<PatientDocumentRecipientResponse>? recipients = null,
        bool confirmed = false,
        Func<Guid, Guid, ReplacePatientDocumentRecipientsRequest, long, PatientDocumentRecipientsResponse>? replacement = null,
        string templateType = "CONSULTATION_REPORT",
        Exception? suggestionFailure = null)
    {
        var document = new PatientDocumentDetailsResponse
        { PatientUid = PatientUid, DocumentUid = DocumentUid, TemplateUid = TemplateUid,
            Status = "Draft", RowVersion = Version };
        var template = new DocumentTemplateDetailsResponse
        { TemplateUid = TemplateUid, DocumentType = templateType };
        var documents = Proxy<IPatientDocumentService>((method, args) => method.Name switch
        {
            nameof(IPatientDocumentService.GetByUidAsync) => Task.FromResult<PatientDocumentDetailsResponse?>(document),
            nameof(IPatientDocumentService.GetTemplateByUidAsync) => Task.FromResult<DocumentTemplateDetailsResponse?>(template),
            nameof(IPatientDocumentService.GetRecipientsAsync) => Task.FromResult<IReadOnlyList<PatientDocumentRecipientResponse>?>(recipients ?? []),
            nameof(IPatientDocumentService.ReplaceDraftRecipientsAsync) => Task.FromResult<PatientDocumentRecipientsResponse?>(
                replacement!( (Guid)args![0]!, (Guid)args[1]!, (ReplacePatientDocumentRecipientsRequest)args[2]!, (long)args[3]!)),
            _ => throw new NotSupportedException(method.Name)
        });
        var repository = Proxy<IPatientDocumentRepository>((method, _) => method.Name switch
        {
            nameof(IPatientDocumentRepository.HasRecipientReplacementAsync) => Task.FromResult(confirmed),
            _ => throw new NotSupportedException(method.Name)
        });
        var careTeam = Proxy<IPatientCareTeamService>((method, _) => method.Name switch
        {
            nameof(IPatientCareTeamService.ListAsync) => suggestionFailure is null
                ? Task.FromResult(relationships)
                : Task.FromException<IReadOnlyList<PatientCareTeamRelationship>>(suggestionFailure),
            _ => throw new NotSupportedException(method.Name)
        });
        var providers = Proxy<IProviderAdministrationService>((method, _) => method.Name switch
        {
            nameof(IProviderAdministrationService.ListAsync) => Task.FromResult<IReadOnlyList<ProviderAdministrationItem>>(
                [Provider(ReferringUid), Provider(PcpUid)]),
            _ => throw new NotSupportedException(method.Name)
        });
        return new(documents, repository, careTeam, providers, NullLogger<ConsultationRecipientService>.Instance);
    }

    private static PatientCareTeamRelationship Relation(Guid providerUid, string role) =>
        new(Guid.NewGuid(), PatientUid, providerUid, "Provider", role, role, true,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), null, true,
            DateTime.UtcNow, 1, null, null, Version);

    private static ProviderAdministrationItem Provider(Guid uid) =>
        new(uid, "Test", "Provider", "Dr. Test Provider", "Physician", null, null,
            "Clinic", null, null, true, DateTime.UtcNow, null, null, null,
            null, null, null, Version);

    private static T Proxy<T>(Func<MethodInfo, object?[]?, object?> handler) where T : class
    {
        var proxy = DispatchProxy.Create<T, TestProxy>();
        ((TestProxy)(object)proxy).Handler = handler;
        return proxy;
    }

    public class TestProxy : DispatchProxy
    {
        public Func<MethodInfo, object?[]?, object?> Handler { get; set; } = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args);
    }
}
