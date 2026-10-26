using System.Net;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using MicroEMR.Application.PatientCpp;
using MicroEMR.Application.PatientPrescriptions;
using MicroEMR.Web.Authorization;
using MicroEMR.Web.Models;
using MicroEMR.Web.Models.Patients;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class ReasonCodingScopeTests
{
    [Theory]
    [InlineData("Draft")]
    [InlineData("Finalized")]
    public async Task ProductionPrescriptionListActionsDoNotChangeWithReasonOrReview(string status)
    {
        var patient=Guid.NewGuid();
        var row=new PatientPrescriptionResponse{PatientUid=patient,PrescriptionUid=Guid.NewGuid(),Status=status,ArtifactUid=status=="Finalized"?Guid.NewGuid():null,
            ProductDisplayText="TEST ONLY",RowVersion=Convert.ToBase64String(new byte[8])};
        var model=new PatientChartViewModel{Patient=new(){PatientUid=patient,DateOfBirth=new(1990,1,1)},
            ActiveTab="medications",CanPrescribe=true,Prescriptions=[row],Cpp=EmptyCpp(patient)};
        var before=Actions(await TargetFacilityAddressAcceptanceViewTests.Render("/Views/Patients/Details.cshtml",model,"Patients","Details"));
        row.ReasonCoding=new("urn:TEST","Ab01","2026","Pharmacie Étoile 薬局");
        row.ReasonCodingReviewRequired=true;row.ReasonCodingReviewed=false;
        var unreviewed=Actions(await TargetFacilityAddressAcceptanceViewTests.Render("/Views/Patients/Details.cshtml",model,"Patients","Details"));
        row.ReasonCodingReviewed=true;
        var reviewed=Actions(await TargetFacilityAddressAcceptanceViewTests.Render("/Views/Patients/Details.cshtml",model,"Patients","Details"));
        Assert.Equal(before,unreviewed);Assert.Equal(before,reviewed);
        Assert.Contains("Details",before);
        if(status=="Draft"){Assert.Contains("Edit",before);Assert.Contains("Sign and finalize prescription",before);}
        else{Assert.Contains("View/Print",before);Assert.Contains("Create Correction",before);Assert.Contains("Cancel Prescription",before);}
        Assert.DoesNotContain("ReasonCoding",reviewed);
        Assert.DoesNotContain("ReasonCodingReviewed",reviewed);
        Assert.DoesNotContain("Pharmacie",reviewed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LegacyOutputAndSignedPdfLayoutAreUnchangedByReasonSnapshot(bool signed)
    {
        var snapshot=new JsonObject{["PrescriptionOutputVersion"]=2,["PrescriptionUid"]="11111111-2222-3333-4444-555555555555",
            ["PatientName"]="TEST ONLY",["ProductDisplayText"]="TEST ONLY medicine",["Directions"]="TEST ONLY directions",
            ["PrescribedDate"]="2026-10-26",["Quantity"]=1,["QuantityUnit"]="tablet",["AuthorizedRepeats"]=0};
        if(signed){snapshot["SignatureVersion"]=1;snapshot["PrescriberDisplayName"]="TEST ONLY prescriber";}
        var occurrence=new PrescriptionOutputOccurrence(Guid.Parse("22222222-3333-4444-5555-666666666666"),
            DateTimeOffset.Parse("2026-10-26T16:00:00Z"),new("test-subject","TEST ONLY","step202"));
        PrescriptionPdfLayout Layout(){using var document=JsonDocument.Parse(snapshot.ToJsonString());return PrescriptionOutputService.Compose(document.RootElement,occurrence);}
        var baseline=Layout();
        snapshot["ReasonCodingVersion"]=1;
        snapshot["ReasonCoding"]=new JsonObject{["system"]="urn:TEST",["code"]="Ab01",["version"]="2026",["display"]="Pharmacie Étoile 薬局"};
        snapshot["ReasonCodingReviewed"]=true;
        Assert.Equal(baseline,Layout()); // Entire body, header and footer; dynamic occurrence held constant.
        snapshot["ReasonCoding"]=null;
        Assert.Equal(baseline,Layout());

        var capture=new PrescriptionDatePrecisionTests.Capture();
        var artifactJson=snapshot.ToJsonString();
        var repository=CppSelectivePrintTests.Stub<IPatientPrescriptionRepository>((method,_)=>method=="GetArtifactAsync"
            ?Task.FromResult<PrescriptionArtifact?>(new(Guid.Empty,artifactJson)):throw new InvalidOperationException(method));
        var legacy=new PatientPrescriptionService(repository,capture);
        var legacyAbsent=(await legacy.RenderArtifactPdfAsync(Guid.Empty,Guid.Empty))!;
        snapshot["ReasonCoding"]=new JsonObject{["system"]="urn:TEST",["code"]="Ab01",["version"]="2026",["display"]="Pharmacie Étoile 薬局"};artifactJson=snapshot.ToJsonString();
        Assert.Equal(legacyAbsent,(await legacy.RenderArtifactPdfAsync(Guid.Empty,Guid.Empty))!);
    }

    private static string Actions(string html)
    {
        var table=Regex.Match(html,@"<th[^>]*>Actions</th>.*?<tbody>(.*?)</tbody>",RegexOptions.Singleline);
        Assert.True(table.Success);
        var cells=Regex.Matches(table.Groups[1].Value,@"<td class=""text-end text-nowrap"">(.*?)</td>",RegexOptions.Singleline);
        Assert.Single(cells.Cast<Match>());
        // Antiforgery values legitimately change per render; retain all other controls, routes and text.
        return Regex.Replace(cells[0].Groups[1].Value,@"<input\b[^>]*name=""__RequestVerificationToken""[^>]*>","");
    }

    private static PatientCppSummaryResponse EmptyCpp(Guid patient)=>new(patient,new("TEST ONLY",null,new(1990,1,1),36,null,null,null),
        PatientCppSection<PatientCppProblem>.From([],0),PatientCppSection<PatientCppAllergy>.From([],0),
        PatientCppSection<PatientCppMedication>.From([],0),PatientCppSection<PatientCppPrescription>.From([],0),
        PatientCppSection<PatientCppImmunization>.From([],0),PatientCppSection<PatientCppResult>.From([],0),
        PatientCppSection<PatientCppVitals>.From([],0),PatientCppSection<PatientCppEncounter>.From([],0),
        PatientCppSection<PatientCppReferral>.From([],0),PatientCppSection<PatientCppDocument>.From([],0));

}
