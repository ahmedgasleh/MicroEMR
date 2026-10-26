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

// Closes specific Step 202 assertion gaps. Existing fixtures own all disposable SQL setup/cleanup.
public sealed class TargetFacilityAddressAcceptanceSqlTests(TargetFacilityAddressDatabase fixture)
    : IClassFixture<TargetFacilityAddressDatabase>
{
    [Fact]
    public async Task MultilineUnicodeReplacementAndClearReopenThroughApiAndFullDetail()
    {
        var db=fixture.Name.Pickup.Base.Db;
        var patient=await db.Stores.SeedPatient();
        await using var host=await PrescriptionDateHost.Start(db,false);
        using var client=host.Client();
        var route=$"api/patients/{patient}/prescriptions";
        const string first="Maple Community Pharmacy\n123 Example Street\nToronto, ON M5V 2T6";
        const string replacement="Pharmacie Étoile 薬局\n45 Rue de l'Exemple\nMontréal, QC H2X 1Y4";
        var request=PrescriptionDatePrecisionTests.Draft();request.TargetDispensingFacilityAddress=first;
        var created=await client.PostAsJsonAsync(route,request);
        Assert.Equal(HttpStatusCode.Created,created.StatusCode);
        var row=(await created.Content.ReadFromJsonAsync<PatientPrescriptionResponse>())!;
        var reopened=(await client.GetFromJsonAsync<PatientPrescriptionResponse>($"{route}/{row.PrescriptionUid}"))!;
        Assert.Equal(first,reopened.TargetDispensingFacilityAddress);

        var edit=PrescriptionDatePrecisionTests.Draft();edit.RowVersion=reopened.RowVersion;edit.TargetDispensingFacilityAddress=replacement;
        var response=await client.PutAsJsonAsync($"{route}/{row.PrescriptionUid}",edit);
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        reopened=(await client.GetFromJsonAsync<PatientPrescriptionResponse>($"{route}/{row.PrescriptionUid}"))!;
        Assert.Equal(replacement,reopened.TargetDispensingFacilityAddress);
        Assert.NotEqual(row.RowVersion,reopened.RowVersion);
        var detail=await TargetFacilityAddressAcceptanceViewTests.Render(
            "/Views/PatientPrescriptions/Detail.cshtml",new PrescriptionDetailViewModel(reopened,null,[]),"PatientPrescriptions","Detail");
        Assert.Contains(HtmlEncoder.Default.Encode(replacement),detail);
        Assert.Contains("white-space: pre-wrap; overflow-wrap: anywhere;",detail);

        edit.RowVersion=reopened.RowVersion;edit.TargetDispensingFacilityAddress=null;
        response=await client.PutAsJsonAsync($"{route}/{row.PrescriptionUid}",edit);
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var cleared=(await client.GetFromJsonAsync<PatientPrescriptionResponse>($"{route}/{row.PrescriptionUid}"))!;
        Assert.Null(cleared.TargetDispensingFacilityAddress);Assert.NotEqual(reopened.RowVersion,cleared.RowVersion);
        Assert.Equal(row.ProductDisplayText,cleared.ProductDisplayText);Assert.Equal(row.Directions,cleared.Directions);
        detail=await TargetFacilityAddressAcceptanceViewTests.Render(
            "/Views/PatientPrescriptions/Detail.cshtml",new PrescriptionDetailViewModel(cleared,null,[]),"PatientPrescriptions","Detail");
        var address=Regex.Match(detail,@"Target Dispensing Facility Address:</strong><span[^>]*>(.*?)</span>",RegexOptions.Singleline);
        Assert.True(address.Success);Assert.Equal("Not recorded",address.Groups[1].Value);
        Assert.DoesNotContain(HtmlEncoder.Default.Encode(replacement),detail);
    }
}

public sealed class TargetFacilityAddressAcceptanceViewTests
{
    [Theory]
    [InlineData("Draft")]
    [InlineData("Finalized")]
    public async Task ProductionPrescriptionListActionsDoNotChangeWithAddressOrAddressReview(string status)
    {
        var patient=Guid.NewGuid();
        var row=new PatientPrescriptionResponse{PatientUid=patient,PrescriptionUid=Guid.NewGuid(),Status=status,ArtifactUid=status=="Finalized"?Guid.NewGuid():null,
            ProductDisplayText="TEST ONLY",RowVersion=Convert.ToBase64String(new byte[8])};
        var model=new PatientChartViewModel{Patient=new(){PatientUid=patient,DateOfBirth=new(1990,1,1)},
            ActiveTab="medications",CanPrescribe=true,Prescriptions=[row],Cpp=EmptyCpp(patient)};
        var before=Actions(await Render("/Views/Patients/Details.cshtml",model,"Patients","Details"));
        row.TargetDispensingFacilityAddress="Pharmacie Étoile 薬局\n45 Rue de l'Exemple";
        row.FacilityAddressReviewRequired=true;row.FacilityAddressReviewed=false;
        var unreviewed=Actions(await Render("/Views/Patients/Details.cshtml",model,"Patients","Details"));
        row.FacilityAddressReviewed=true;
        var reviewed=Actions(await Render("/Views/Patients/Details.cshtml",model,"Patients","Details"));
        Assert.Equal(before,unreviewed);Assert.Equal(before,reviewed);
        Assert.Contains("Details",before);
        if(status=="Draft"){Assert.Contains("Edit",before);Assert.Contains("Sign and finalize prescription",before);}
        else{Assert.Contains("View/Print",before);Assert.Contains("Create Correction",before);Assert.Contains("Cancel Prescription",before);}
        Assert.DoesNotContain("TargetDispensingFacilityAddress",reviewed);
        Assert.DoesNotContain("FacilityAddressReviewed",reviewed);
        Assert.DoesNotContain("Pharmacie",reviewed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LegacyOutputAndSignedPdfLayoutAreUnchangedByAddressSnapshot(bool signed)
    {
        var snapshot=new JsonObject{["PrescriptionOutputVersion"]=2,["PrescriptionUid"]="11111111-2222-3333-4444-555555555555",
            ["PatientName"]="TEST ONLY",["ProductDisplayText"]="TEST ONLY medicine",["Directions"]="TEST ONLY directions",
            ["PrescribedDate"]="2026-10-26",["Quantity"]=1,["QuantityUnit"]="tablet",["AuthorizedRepeats"]=0};
        if(signed){snapshot["SignatureVersion"]=1;snapshot["PrescriberDisplayName"]="TEST ONLY prescriber";}
        var occurrence=new PrescriptionOutputOccurrence(Guid.Parse("22222222-3333-4444-5555-666666666666"),
            DateTimeOffset.Parse("2026-10-26T16:00:00Z"),new("test-subject","TEST ONLY","step202"));
        PrescriptionPdfLayout Layout(){using var document=JsonDocument.Parse(snapshot.ToJsonString());return PrescriptionOutputService.Compose(document.RootElement,occurrence);}
        var baseline=Layout();
        snapshot["TargetDispensingFacilityAddressVersion"]=1;
        snapshot["TargetDispensingFacilityAddress"]="Pharmacie Étoile 薬局\n45 Rue de l'Exemple";
        snapshot["FacilityAddressReviewed"]=true;
        Assert.Equal(baseline,Layout()); // Entire body, header and footer; dynamic occurrence held constant.
        snapshot["TargetDispensingFacilityAddress"]=null;
        Assert.Equal(baseline,Layout());

        var capture=new PrescriptionDatePrecisionTests.Capture();
        var artifactJson=snapshot.ToJsonString();
        var repository=CppSelectivePrintTests.Stub<IPatientPrescriptionRepository>((method,_)=>method=="GetArtifactAsync"
            ?Task.FromResult<PrescriptionArtifact?>(new(Guid.Empty,artifactJson)):throw new InvalidOperationException(method));
        var legacy=new PatientPrescriptionService(repository,capture);
        var legacyAbsent=(await legacy.RenderArtifactPdfAsync(Guid.Empty,Guid.Empty))!;
        snapshot["TargetDispensingFacilityAddress"]="Pharmacie Étoile 薬局\n45 Rue de l'Exemple";artifactJson=snapshot.ToJsonString();
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

    internal static async Task<string> Render(string path,object model,string controller,string actionName)
    {
        var builder=WebApplication.CreateBuilder(new WebApplicationOptions{ApplicationName=typeof(PrescriptionDetailViewModel).Assembly.GetName().Name,ContentRootPath=AppContext.BaseDirectory});
        builder.Services.AddControllersWithViews();builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddSingleton(CppSelectivePrintTests.Stub<IWebPermissionService>((method,_)=>method=="HasAsync"?(object)Task.FromResult(false):Task.FromResult<IReadOnlySet<string>>(new HashSet<string>())));
        builder.Services.AddSingleton(CppSelectivePrintTests.Stub<IWebPlatformEntitlementAccessor>((_,_)=>Task.FromResult(false)));
        await using var app=builder.Build();using var scope=app.Services.CreateScope();var services=scope.ServiceProvider;
        var http=new DefaultHttpContext{RequestServices=services};http.SetEndpoint(new Endpoint(_=>Task.CompletedTask,new EndpointMetadataCollection(),"Step 202 synthetic render"));
        var route=new RouteData();route.Values["controller"]=controller;route.Values["action"]=actionName;
        var action=new ActionContext(http,route,new ActionDescriptor{RouteValues=new Dictionary<string,string?>{{"controller",controller},{"action",actionName}}});
        var view=services.GetRequiredService<ICompositeViewEngine>().GetView(null,path,false);Assert.True(view.Success);
        var data=new ViewDataDictionary(new EmptyModelMetadataProvider(),new ModelStateDictionary()){Model=model};
        using var writer=new StringWriter();await view.View.RenderAsync(new ViewContext(action,view.View,data,new TempDataDictionary(http,services.GetRequiredService<ITempDataProvider>()),writer,new HtmlHelperOptions()));
        return writer.ToString();
    }
}
