using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Web.Authorization;
using MicroEMR.Web.Controllers;
using MicroEMR.Web.Models.PatientDocuments;
using MicroEMR.Web.Services.PatientDocuments;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class ConsultationRecipientWebTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ApiPassesCareTeamPermissionAndReturnsCurrentRecipientVersion(bool permitted)
    {
        var patientUid = Guid.NewGuid();
        var documentUid = Guid.NewGuid();
        var state = new MicroEMR.Application.PatientDocuments.Contracts.ConsultationRecipientState(
            documentUid, patientUid, "AAAAAAAAAAI=", [], []);
        var authorization = Proxy<Microsoft.AspNetCore.Authorization.IAuthorizationService>((_, args) =>
        {
            Assert.Equal(MicroEMR.Api.Authorization.PermissionPolicyProvider.Prefix +
                MicroEMR.Application.AccessProfiles.PermissionKeys.PatientsView, args![2]);
            return Task.FromResult(permitted
                ? Microsoft.AspNetCore.Authorization.AuthorizationResult.Success()
                : Microsoft.AspNetCore.Authorization.AuthorizationResult.Failed());
        });
        var service = Proxy<MicroEMR.Application.PatientDocuments.Services.IConsultationRecipientService>((_, args) =>
        {
            Assert.Equal(patientUid, args![0]);
            Assert.Equal(documentUid, args[1]);
            Assert.Equal(permitted, args[2]);
            return Task.FromResult<MicroEMR.Application.PatientDocuments.Contracts.ConsultationRecipientState?>(state);
        });
        var controller = new MicroEMR.Api.Controllers.PatientDocumentsController(null!, null!,
            authorization, null!, null!, service)
        {
            ControllerContext = new ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() }
        };
        var result = await controller.GetConsultationRecipients(patientUid, documentUid, default);
        Assert.Same(state, Assert.IsType<OkObjectResult>(result.Result).Value);
    }

    [Fact]
    public async Task InvalidRecipientBindingNeverReachesPersistence()
    {
        var controller = new PatientDocumentsController(null!,
            NullLogger<PatientDocumentsController>.Instance, null!, null!);
        controller.ModelState.AddModelError("Recipients[0].ProviderUid", "Invalid provider identity");
        var result = await controller.SaveConsultationRecipients(Guid.NewGuid(),
            new() { PatientUid = Guid.NewGuid(), RowVersion = "AAAAAAAAAAE=" }, default);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Theory]
    [InlineData(true, "Draft", true)]
    [InlineData(false, "Draft", false)]
    [InlineData(true, "Signed", false)]
    public async Task DetailsLoadsRecipientsOnlyForDraftConsultation(bool consultation, string status, bool expected)
    {
        var uid = Guid.NewGuid();
        var document = new PatientDocumentDetailsResponse
        {
            PatientUid = Guid.NewGuid(), DocumentUid = uid, IsConsultationReport = consultation,
            Status = status, RowVersion = "AAAAAAAAAAI="
        };
        var loaded = false;
        var client = Proxy<IPatientDocumentApiClient>((method, _) => method.Name switch
        {
            nameof(IPatientDocumentApiClient.GetByUidAsync) => Task.FromResult<PatientDocumentDetailsResponse?>(document),
            nameof(IPatientDocumentApiClient.GetConsultationRecipientsAsync) => Load(),
            _ => throw new InvalidOperationException(method.Name)
        });
        Task<MicroEMR.Application.PatientDocuments.Contracts.ConsultationRecipientState?> Load()
        {
            loaded = true;
            return Task.FromResult<MicroEMR.Application.PatientDocuments.Contracts.ConsultationRecipientState?>(null);
        }
        var permissions = Proxy<IWebPermissionService>((_, _) => Task.FromResult(true));
        var controller = new PatientDocumentsController(client,
            NullLogger<PatientDocumentsController>.Instance, null!, permissions);
        var view = Assert.IsType<ViewResult>(await controller.Details(uid, default));
        Assert.Same(document, view.Model);
        Assert.Equal(expected, loaded);
        Assert.Equal("AAAAAAAAAAI=", ((PatientDocumentDetailsResponse)view.Model!).RowVersion);
    }

    private static T Proxy<T>(Func<MethodInfo, object?[]?, object?> handler) where T : class
    {
        var proxy = DispatchProxy.Create<T, ConsultationRecipientServiceTests.TestProxy>();
        ((ConsultationRecipientServiceTests.TestProxy)(object)proxy).Handler = handler;
        return proxy;
    }
}
