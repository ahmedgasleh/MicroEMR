using System.Text.Encodings.Web;
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
using MicroEMR.Application.PatientPrescriptions;
using MicroEMR.Web.Authorization;
using MicroEMR.Web.Models;
using MicroEMR.Web.Services.PatientPrescriptions;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class TargetFacilityNameWebTests
{
    [Theory]
    [InlineData("Create")] [InlineData("Edit")] [InlineData("Detail")]
    public async Task ProductionViewsRenderNameAsEncodedTextWithoutChangingOutputScope(string viewName)
    {
        const string name="薬局 <branch> & O'Brien";
        var builder=WebApplication.CreateBuilder(new WebApplicationOptions{ApplicationName=typeof(PrescriptionDetailViewModel).Assembly.GetName().Name,ContentRootPath=AppContext.BaseDirectory});
        builder.Services.AddControllersWithViews();builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddSingleton(CppSelectivePrintTests.Stub<IWebPermissionService>((_,_)=>Task.FromResult(false)));
        builder.Services.AddSingleton(CppSelectivePrintTests.Stub<IWebPlatformEntitlementAccessor>((_,_)=>throw new InvalidOperationException("Anonymous renderer must not request entitlements")));
        await using var app=builder.Build();using var scope=app.Services.CreateScope();var services=scope.ServiceProvider;
        var http=new DefaultHttpContext{RequestServices=services};http.SetEndpoint(new Endpoint(_=>Task.CompletedTask,new EndpointMetadataCollection(),"Facility view fixture"));
        var route=new RouteData();route.Values["controller"]="PatientPrescriptions";route.Values["action"]=viewName;
        var action=new ActionContext(http,route,new ActionDescriptor{RouteValues=new Dictionary<string,string?>{{"controller","PatientPrescriptions"},{"action",viewName}}});
        var view=services.GetRequiredService<ICompositeViewEngine>().GetView(null,$"/Views/PatientPrescriptions/{viewName}.cshtml",false);Assert.True(view.Success);
        var draft=new CreatePrescriptionDraftRequest{TargetDispensingFacilityName=name,IsCorrectionDraft=true,FacilityNameReviewRequired=true};
        object model=viewName=="Detail"?new PrescriptionDetailViewModel(new PatientPrescriptionResponse{TargetDispensingFacilityName=name,Status="Draft"},null,[]):draft;
        var data=new ViewDataDictionary(new EmptyModelMetadataProvider(),new ModelStateDictionary()){Model=model};
        using var writer=new StringWriter();await view.View.RenderAsync(new ViewContext(action,view.View,data,new TempDataDictionary(http,services.GetRequiredService<ITempDataProvider>()),writer,new HtmlHelperOptions()));
        var html=writer.ToString();Assert.Contains("Target Dispensing Facility Name",html);Assert.Contains(HtmlEncoder.Default.Encode(name),html);Assert.DoesNotContain("<branch>",html);
        if(viewName!="Detail")
        {
            Assert.Contains("name=\"facilityNameOperation\"",html);Assert.Contains("name=\"TargetDispensingFacilityName\"",html);
            Assert.Contains("name=\"FacilityNameReviewed\"",html);Assert.Contains("data-facility-name-review",html);
            var input=System.Text.RegularExpressions.Regex.Match(html,"<input\\b[^>]*name=\"TargetDispensingFacilityName\"[^>]*>").Value;
            Assert.NotEmpty(input);Assert.DoesNotContain("maxlength",input); // No silent browser paste truncation of this field.
        }
    }

    [Theory]
    [InlineData("preserve",false,null)] [InlineData("clear",true,null)] [InlineData("set",true,"薬局 École")]
    public async Task ExistingWebPostMapsPresenceBeforeValidation(string operation,bool specified,string? expected)
    {
        var api=CppSelectivePrintTests.Stub<IPatientPrescriptionApiClient>((_,_)=>throw new InvalidOperationException("No API write expected in validation fixture"));
        var http=new DefaultHttpContext();http.Request.ContentType="application/x-www-form-urlencoded";http.Request.Headers["X-Requested-With"]="XMLHttpRequest";
        http.Request.Form=new FormCollection(new Dictionary<string,Microsoft.Extensions.Primitives.StringValues>{["facilityNameOperation"]=operation,["TargetDispensingFacilityName"]="薬局 École"});
        var controller=new MicroEMR.Web.Controllers.PatientPrescriptionsController(api){ControllerContext=new(){HttpContext=http}};
        controller.ModelState.AddModelError("fixture","Stop before persistence.");var model=new PrescriptionDraftRequest();
        Assert.IsType<BadRequestObjectResult>(await controller.Edit(Guid.NewGuid(),Guid.NewGuid(),model,default));
        Assert.Equal(specified,model.TargetDispensingFacilityNameSpecified);Assert.Equal(expected,model.TargetDispensingFacilityName);
        Assert.Equal(operation,controller.ViewData["FacilityNameOperation"]);
    }

    [Fact]
    public async Task ExistingModalEditCarriesExactNameAndReviewState()
    {
        var api=CppSelectivePrintTests.Stub<IPatientPrescriptionApiClient>((method,_)=>method=="GetAsync"?Task.FromResult<PatientPrescriptionResponse?>(new(){Status="Draft",TargetDispensingFacilityName="Pharmacie 薬局",SupersedesPrescriptionUid=Guid.NewGuid(),FacilityNameReviewRequired=true,FacilityNameReviewed=false}):throw new InvalidOperationException(method));
        var http=new DefaultHttpContext();http.Request.Headers["X-Requested-With"]="XMLHttpRequest";
        var controller=new MicroEMR.Web.Controllers.PatientPrescriptionsController(api){ControllerContext=new(){HttpContext=http}};
        var result=Assert.IsType<JsonResult>(await controller.Edit(Guid.NewGuid(),Guid.NewGuid(),default));var model=Assert.IsType<PrescriptionDraftRequest>(result.Value);
        Assert.Equal("Pharmacie 薬局",model.TargetDispensingFacilityName);Assert.True(model.TargetDispensingFacilityNameSpecified);Assert.True(model.IsCorrectionDraft);Assert.True(model.FacilityNameReviewRequired);Assert.False(model.FacilityNameReviewed);
    }
}
