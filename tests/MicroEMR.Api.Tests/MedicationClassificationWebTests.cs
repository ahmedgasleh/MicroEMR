using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Web.Models.PatientMedications;
using MicroEMR.Web.Services.PatientMedications;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class MedicationClassificationWebTests
{
    [Theory]
    [InlineData("Ongoing")]
    [InlineData("Acute")]
    [InlineData(null)]
    public async Task WebCreateAndEditCarryClassificationWithoutChangingOtherClinicalFields(string? classification)
    {
        var patient=Guid.NewGuid();var uid=Guid.NewGuid();
        var stored=new PatientMedicationDetailsResponse{PatientUid=patient,MedicationUid=uid,MedicationName="TEST ONLY - web mapping",Classification=classification,Notes="Retained note",RowVersion=Convert.ToBase64String(new byte[8])};
        UpdatePatientMedicationRequest? captured=null;
        CreatePatientMedicationRequest? created=null;
        var client=DispatchProxy.Create<IPatientMedicationApiClient,CppSelectivePrintTests.Proxy>();
        ((CppSelectivePrintTests.Proxy)(object)client).Call=(method,args)=> method switch
        {
            "CreateAsync"=>(object)CaptureCreate((CreatePatientMedicationRequest)args[1]!),
            "GetByUidAsync"=>Task.FromResult<PatientMedicationDetailsResponse?>(stored),
            "UpdateAsync"=>Capture((UpdatePatientMedicationRequest)args[2]!),
            _=>throw new InvalidOperationException(method)
        };
        Task<PatientMedicationDetailsResponse> CaptureCreate(CreatePatientMedicationRequest value){created=value;return Task.FromResult(stored);}
        Task<PatientMedicationDetailsResponse?> Capture(UpdatePatientMedicationRequest value){captured=value;return Task.FromResult<PatientMedicationDetailsResponse?>(stored);}
        var web=new MicroEMR.Web.Controllers.PatientMedicationsController(client,NullLogger<MicroEMR.Web.Controllers.PatientMedicationsController>.Instance)
        {ControllerContext=new(){HttpContext=new DefaultHttpContext()}};
        web.TempData=new TempDataDictionary(web.HttpContext,new TempData());
        web.Url=DispatchProxy.Create<IUrlHelper,CppSelectivePrintTests.Proxy>();
        ((CppSelectivePrintTests.Proxy)(object)web.Url).Call=(_,_)=>"/test-chart";
        web.HttpContext.Request.Headers["X-Requested-With"]="XMLHttpRequest";
        Assert.IsType<RedirectToActionResult>(await web.Create(new(){PatientUid=patient,MedicationName=stored.MedicationName,Classification=classification,Notes=stored.Notes},default));
        Assert.NotNull(created);Assert.Equal(classification,created.Classification);Assert.Equal(stored.Notes,created.Notes);
        var loaded=Assert.IsType<EditPatientMedicationViewModel>(Assert.IsType<JsonResult>(await web.Edit(uid,default)).Value);
        Assert.Equal(classification,loaded.Classification);
        loaded.Classification=classification=="Ongoing"?null:"Ongoing";
        Assert.IsType<JsonResult>(await web.Edit(loaded,default));
        Assert.NotNull(captured);Assert.Equal(loaded.Classification,captured.Classification);
        Assert.Equal(stored.Notes,captured.Notes);Assert.Equal(stored.RowVersion,captured.RowVersion);
    }
    private sealed class TempData : ITempDataProvider
    {
        public IDictionary<string,object> LoadTempData(HttpContext context)=>new Dictionary<string,object>();
        public void SaveTempData(HttpContext context,IDictionary<string,object> values) { }
    }
}
