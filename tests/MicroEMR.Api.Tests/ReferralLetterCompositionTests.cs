using System.Reflection;
using System.Text;
using MicroEMR.Application.ClinicalOutput;
using MicroEMR.Application.ClinicalUsers;
using MicroEMR.Application.ClinicConfiguration;
using MicroEMR.Application.PatientReferrals;
using MicroEMR.Application.Patients.Contracts;
using MicroEMR.Application.Patients.Repositories;
using MicroEMR.Application.Patients.Services;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class ReferralLetterCompositionTests
{
    [Fact]
    public async Task SelectedPreviewContentFollowsNarrativeAndPrecedesSupportingDocumentsWithoutChangingSentComposition()
    {
        var f = new Fixture(selectedClinicalHtml: "<section><h2>Selected Clinical Information</h2><p>Selected source content</p></section>");
        var html = await f.Preview();
        Assert.True(html.IndexOf("Clinical summary",StringComparison.Ordinal) < html.IndexOf("Selected Clinical Information",StringComparison.Ordinal));
        Assert.True(html.IndexOf("Selected Clinical Information",StringComparison.Ordinal) < html.IndexOf("Supporting documents",StringComparison.Ordinal));
        Assert.DoesNotContain("<h2>Patient demographics</h2>",html);
        await f.Service.MarkSentAsync(f.Patient.PatientUid,f.Referral.ReferralUid,new() {RowVersion=f.Referral.RowVersion});
        Assert.DoesNotContain("Selected source content",Encoding.UTF8.GetString(f.SentArtifact!.PdfContent));
    }
    [Fact]
    public async Task PreviewIncludesAuthoritativePatientProviderClinicRecipientAndNarrative()
    {
        var f = new Fixture();
        var html = await f.Preview();
        foreach (var value in new[] { "Ada Lee", "June 15, 1980", "1234567890 AB", "Woman", "Female",
            "Dr Selected", "Physician", "Family Medicine", "998877", "Clinic Legal Name", "10 Clinic Street",
            "Toronto", "ON", "M1M 1M1", "416-111-1111", "416-222-2222", "clinic@example.test",
            "Dr Recipient", "Recipient Office", "905-111-1111", "905-222-2222", "Reason text", "Clinical narrative",
            "Supporting report" }) Assert.Contains(value, html);
        Assert.Contains("<dt>Age:</dt><dd>45</dd>", html);
        Assert.Contains("<dt>Phone:</dt><dd>905-111-1111</dd>", html);
        Assert.Contains("<dt>Fax:</dt><dd>905-222-2222</dd>", html);
        Assert.DoesNotContain("Old provider snapshot", html);
        var header = html[..html.IndexOf("</header>", StringComparison.Ordinal)];
        var body = html[html.IndexOf("<section class=\"clinical-print-body\">", StringComparison.Ordinal)..];
        foreach (var label in new[] { "Patient", "DOB", "Age", "Gender", "Sex at birth", "Health Card", "Patient alternative contact" })
        {
            var marker = $"<dt>{label}:</dt>";
            Assert.Contains(marker, header);
            Assert.DoesNotContain(marker, body);
            Assert.Equal(1, html.Split(marker, StringSplitOptions.None).Length - 1);
        }
        Assert.DoesNotContain("<h2>Patient demographics</h2>", html);
    }

    [Theory]
    [InlineData(1980, 6, 14, 46)]
    [InlineData(1980, 6, 15, 45)]
    [InlineData(2000, 2, 29, 26)]
    public async Task AgeUsesBirthdayAndClinicLocalLetterDate(int year, int month, int day, int expected)
    {
        var f = new Fixture();
        f.Patient.DateOfBirth = new DateOnly(year, month, day);
        // Clock is June 15 UTC, but still June 14 in the clinic's timezone.
        Assert.Contains($"<dt>Age:</dt><dd>{expected}</dd>", await f.Preview());
    }

    [Fact]
    public async Task DraftDateIsExplicitAndPreviewPerformsOnlyReads()
    {
        var f = new Fixture();
        var html = await f.Preview();
        Assert.Contains("Draft preview date:</strong> June 14, 2026", html);
        Assert.Contains("Draft preview — not sent", html);
        Assert.DoesNotContain("Referral Letter Date:</strong>", html);
        Assert.DoesNotContain("January 1, 2001", html);
        Assert.DoesNotContain("January 1, 2030", html);
        Assert.DoesNotContain("January 1, 2031", html);
        Assert.Null(f.SentArtifact);
        Assert.Equal(0, f.ActorCalls);
        Assert.Equal(new[] { "GetByUidAsync", "GetProviderAsync" }, f.ReferralCalls);
        // All unconfigured repository mutations throw in this fixture.
    }

    [Fact]
    public async Task FinalCompositionUsesSendDateWithoutDraftLabelOrResponseAndClosureDates()
    {
        var f = new Fixture();
        await f.Service.MarkSentAsync(f.Patient.PatientUid, f.Referral.ReferralUid,
            new ReferralStatusTransitionRequest { RowVersion = f.Referral.RowVersion });
        var artifact = Assert.IsType<ReferralArtifactWrite>(f.SentArtifact);
        var html = Encoding.UTF8.GetString(artifact.PdfContent);
        Assert.Equal(f.Clock.GetUtcNow().UtcDateTime, artifact.SentAtUtc);
        Assert.Contains("Referral Letter Date:</strong> June 14, 2026", html);
        Assert.DoesNotContain("Draft preview", html);
        Assert.DoesNotContain("January 1, 2030", html);
        Assert.DoesNotContain("January 1, 2031", html);
    }

    [Fact]
    public async Task MissingDemographicsDoNotSubstituteSexOrPatientPhoneForGenderOrAlternativeContact()
    {
        var f = new Fixture();
        f.Patient.GenderIdentity = " ";
        f.Patient.HealthCardNumber = null;
        f.Patient.AlternatePhoneNumber = "416-999-9999";
        var html = await f.Preview();
        Assert.Contains("<dt>Gender:</dt><dd>Not recorded</dd>", html);
        Assert.Contains("<dt>Sex at birth:</dt><dd>Female</dd>", html);
        Assert.Contains("<dt>Health Card:</dt><dd>Not recorded</dd>", html);
        Assert.Contains("<dt>Patient alternative contact:</dt><dd>Not recorded</dd>", html);
        Assert.DoesNotContain("416-999-9999", html);
        Assert.DoesNotContain("<dt>HCN:</dt><dd>AB</dd>", html);
        Assert.DoesNotContain("<dt>Health Card:</dt><dd>AB</dd>", html);
    }

    [Fact]
    public void SharedHeaderOmitsExtraDemographicsWhenOtherCallersDoNotSupplyThem()
    {
        var context = new ClinicalPrintContext(
            new("Clinic", null, null, null, null, null, null, null, null),
            new("Patient Name", new(1980, 6, 15), "12345", "AB", "Chart1"),
            new("Document", "Title", "Type", new(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc), null),
            new(null, null, null, null, null), "UTC");
        var html = new ClinicalPrintLayoutRenderer().Render(context, "<p>Content</p>");
        Assert.Contains("<dt>Patient:</dt><dd>Patient Name</dd>", html);
        Assert.Contains("<dt>Health Card:</dt><dd>12345 AB</dd>", html);
        foreach (var label in new[] { "Age", "Gender", "Sex at birth", "Patient alternative contact" })
            Assert.DoesNotContain($"<dt>{label}:</dt>", html);
    }

    [Fact]
    public async Task MissingRecipientContactsAndSummaryAreExplicit()
    {
        var f = new Fixture(missingOptional: true);
        var html = await f.Preview();
        Assert.Contains("<dt>Phone:</dt><dd>Not recorded</dd>", html);
        Assert.Contains("<dt>Fax:</dt><dd>Not recorded</dd>", html);
        Assert.Contains("<dt>Organization:</dt><dd>Not recorded</dd>", html);
        Assert.Contains("<h2>Clinical summary</h2><p style=\"white-space: pre-wrap\">Not recorded</p>", html);
    }

    [Fact]
    public async Task NarrativeAndDemographicsAreEncodedAndNarrativeRetainsLineBreaks()
    {
        var f = new Fixture();
        f.Patient.FirstName = "<script>Ada</script>";
        var html = await f.Preview();
        Assert.Contains("&lt;script&gt;Ada&lt;/script&gt;", html);
        Assert.DoesNotContain("<script>", html);
        Assert.Contains("Reason text &lt;b&gt;literal&lt;/b&gt;\nSecond line", html);
        Assert.Contains("Clinical narrative &amp; context\nNext line", html);
    }

    [Fact]
    public async Task PreviewDoesNotReadAnotherPatientsReferral()
    {
        var f = new Fixture();
        Assert.Null(await f.Service.PreviewLetterAsync(Guid.NewGuid(), f.Referral.ReferralUid));
        Assert.Equal(new[] { "GetByUidAsync" }, f.ReferralCalls);
    }

    [Theory]
    [InlineData(ReferralStatus.Sent)]
    [InlineData(ReferralStatus.ResponseReceived)]
    [InlineData(ReferralStatus.Closed)]
    public async Task NonDraftStillCannotBeRegeneratedThroughPreview(ReferralStatus status)
    {
        var f = new Fixture(status: status);
        await Assert.ThrowsAsync<PatientReferralTransitionException>(() => f.Preview());
        Assert.Equal(new[] { "GetByUidAsync" }, f.ReferralCalls);
    }

    private sealed class Fixture
    {
        public PatientDetailsResponse Patient { get; } = new()
        {
            PatientUid = Guid.NewGuid(), FirstName = "Ada", LastName = "Lee", DateOfBirth = new(1980, 6, 15),
            GenderIdentity = "Woman", SexAtBirth = "Female", HealthCardNumber = "1234567890", HealthCardVersion = "AB"
        };
        public PatientReferral Referral { get; }
        public PatientReferralService Service { get; }
        public TimeProvider Clock { get; } = new FixedClock();
        public ReferralArtifactWrite? SentArtifact { get; private set; }
        public int ActorCalls { get; private set; }
        public List<string> ReferralCalls { get; } = [];

        public Fixture(bool missingOptional = false, ReferralStatus status = ReferralStatus.Draft, string selectedClinicalHtml = "")
        {
            var providerUid = Guid.NewGuid();
            Referral = new()
            {
                PatientUid = Patient.PatientUid, ReferralUid = Guid.NewGuid(), ReferringProviderUid = providerUid,
                ReferringProviderDisplayNameSnapshot = "Old provider snapshot", Status = status, RowVersion = "AAAA",
                RecipientName = "Dr Recipient", RecipientOrganization = missingOptional ? null : "Recipient Office",
                RecipientPhone = missingOptional ? null : "905-111-1111", RecipientFax = missingOptional ? null : "905-222-2222",
                Reason = "Reason text <b>literal</b>\nSecond line", ClinicalSummary = missingOptional ? null : "Clinical narrative & context\nNext line",
                CreatedAt = new(2001, 1, 1), ResponseReceivedAt = new(2030, 1, 1), ClosedAt = new(2031, 1, 1)
            };
            var referrals = Stub<IPatientReferralRepository>((method, args) =>
            {
                ReferralCalls.Add(method);
                switch (method)
                {
                    case "GetByUidAsync":
                        return Task.FromResult((Guid)args[0]! == Patient.PatientUid && (Guid)args[1]! == Referral.ReferralUid ? Referral : null);
                    case "GetProviderAsync":
                        Assert.Equal(providerUid, args[0]);
                        return Task.FromResult<ReferralProvider?>(new(providerUid, "Dr Selected", "Physician", "998877", "Family Medicine"));
                    case "SendWithArtifactAsync":
                        Assert.Equal(Patient.PatientUid, args[0]); Assert.Equal(Referral.ReferralUid, args[1]);
                        SentArtifact = (ReferralArtifactWrite)args[4]!;
                        return Task.FromResult<PatientReferral?>(Referral);
                    default: throw new InvalidOperationException($"Unexpected referral call: {method}");
                }
            });
            object PatientRead(string method, object?[] args)
            {
                Assert.Equal("GetByUidAsync", method); Assert.Equal(Patient.PatientUid, args[0]);
                return Task.FromResult<PatientDetailsResponse?>(Patient);
            }
            Service = new(referrals, Stub<IPatientRepository>(PatientRead),
                Stub<IAuthenticatedClinicalUserAccessor>((method, _) =>
                {
                    Assert.Equal("GetRequiredUserIdAsync", method); Assert.Contains("GetByUidAsync", ReferralCalls);
                    ActorCalls++;
                    return Task.FromResult(7L);
                }), new ReferralStatusTransitionService(), Stub<IPatientService>(PatientRead),
                Stub<IClinicConfigurationService>((method, _) =>
                {
                    Assert.Equal("GetAsync", method);
                    return Task.FromResult(new ClinicConfigurationResponse("Tenant name", "America/Toronto", "Clinic Legal Name",
                        "416-111-1111", "416-222-2222", "clinic@example.test", "10 Clinic Street", null,
                        "Toronto", "ON", "M1M 1M1", "CA", null, null, null, null));
                }), Stub<IReferralDocumentRepository>((method, args) =>
                {
                    Assert.Equal("GetByReferralUidAsync", method);
                    Assert.Equal(Patient.PatientUid, args[0]); Assert.Equal(Referral.ReferralUid, args[1]);
                    return Task.FromResult<IReadOnlyList<ReferralDocumentLinkResponse>>([new()
                    { DocumentUid = Guid.NewGuid(), Title = "Supporting report", DocumentType = "Report", DocumentStatus = "Final" }]);
                }), new ClinicalPrintLayoutRenderer(), Stub<IPdfRenderer>((method, args) =>
                {
                    Assert.Equal("RenderAsync", method);
                    return Task.FromResult(Encoding.UTF8.GetBytes((string)args[0]!));
                }), Clock,
                permissions: Stub<MicroEMR.Application.AccessProfiles.ICurrentUserPermissionService>((method, _) =>
                    Task.FromResult<IReadOnlySet<string>>(new HashSet<string> { MicroEMR.Application.AccessProfiles.PermissionKeys.DocumentsView })),
                clinicalContent: Stub<IReferralClinicalContentService>((method, _) => Task.FromResult(selectedClinicalHtml)));
        }

        public async Task<string> Preview() => Encoding.UTF8.GetString(
            (await Service.PreviewLetterAsync(Patient.PatientUid, Referral.ReferralUid))!);
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 6, 15, 2, 0, 0, TimeSpan.Zero);
    }

    private static T Stub<T>(Func<string, object?[], object> call) where T : class
    {
        var result = DispatchProxy.Create<T, StrictProxy>();
        ((StrictProxy)(object)result).Call = call;
        return result;
    }

    public class StrictProxy : DispatchProxy
    {
        public Func<string, object?[], object> Call { get; set; } = null!;
        protected override object Invoke(MethodInfo? targetMethod, object?[]? args) => Call(targetMethod!.Name, args!);
    }
}
