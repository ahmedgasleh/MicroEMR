using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Application.PatientPrescriptions;
using MicroEMR.Web.Authorization;
using MicroEMR.Web.Models;
using MicroEMR.Web.Services.PatientPrescriptions;
using Xunit;
using Controller = MicroEMR.Web.Controllers.PatientPrescriptionsController;

namespace MicroEMR.Api.Tests;

public sealed class PrescriptionPrivateNoteWebTests
{
    private static (Controller controller, Func<int> reads) Fixture(bool entitled = true, bool author = true, string token = "reviewed")
    {
        var reads = 0;
        var api = CppSelectivePrintTests.Stub<IPatientPrescriptionApiClient>((method, _) =>
        {
            if (method == "GetAsync") return Task.FromResult<PatientPrescriptionResponse?>(new() { PrescriptionUid = Guid.NewGuid(), PatientUid = Guid.NewGuid(), Status = "Draft", RowVersion = "reviewed" });
            if (method == "PrivateNoteAsync") { reads++; return Task.FromResult<PrescriptionPrivateNoteDetail?>(new(Guid.NewGuid(), Guid.NewGuid(), "TEST ONLY internal", "Draft", token, author)); }
            if (method == "PrivateNoteHistoryAsync") { reads++; return Task.FromResult<IReadOnlyList<PrescriptionPrivateNoteRevision>>([]); }
            throw new InvalidOperationException("Unexpected call: " + method);
        });
        var permissions = CppSelectivePrintTests.Stub<IWebPermissionService>((method, _) => method == "HasAsync" ? Task.FromResult(entitled) : throw new InvalidOperationException());
        return (new Controller(api, permissions) { ControllerContext = new() { HttpContext = new DefaultHttpContext() } }, () => reads);
    }
    [Fact]
    public async Task GeneralModalEditRemainsPrivateFreeEvenForAuthor()
    {
        var (controller, reads) = Fixture(); controller.Request.Headers["X-Requested-With"] = "XMLHttpRequest";
        var result = Assert.IsType<JsonResult>(await controller.Edit(Guid.NewGuid(), Guid.NewGuid(), default));
        Assert.False(Assert.IsType<PrescriptionDraftRequest>(result.Value).PrivateNoteSpecified);
        Assert.DoesNotContain("privateNote", JsonSerializer.Serialize(result.Value)); Assert.Equal(0, reads());
    }
    [Theory]
    [InlineData(true, true)] [InlineData(true, false)] [InlineData(false, false)]
    public async Task StandaloneEditorLoadsOnlyAuthorizedOriginalAuthorText(bool entitled, bool author)
    {
        var (controller, reads) = Fixture(entitled, author);
        var result = Assert.IsType<ViewResult>(await controller.Edit(Guid.NewGuid(), Guid.NewGuid(), default));
        var model = Assert.IsType<PrescriptionDraftRequest>(result.Model);
        Assert.Equal(entitled && author, model.PrivateNoteSpecified);
        Assert.Equal(entitled ? 1 : 0, reads());
    }
    [Fact]
    public async Task FallbackEditorRejectsDifferentPublicAndProtectedVersions()
    {
        var (controller, _) = Fixture(token: "changed");
        Assert.IsType<ConflictObjectResult>(await controller.Edit(Guid.NewGuid(), Guid.NewGuid(), default));
    }
    [Fact]
    public async Task OrdinaryDetailDoesNotFetchProtectedContentForChartReader()
    {
        var (controller, reads) = Fixture(false);
        var result = Assert.IsType<ViewResult>(await controller.Detail(Guid.NewGuid(), Guid.NewGuid(), default));
        Assert.Null(Assert.IsType<PrescriptionDetailViewModel>(result.Model).PrivateNote); Assert.Equal(0, reads());
    }
    [Fact]
    public async Task PrivateEditorResponseDoesNotSupplyOtherAuthorsText()
    {
        var (controller, _) = Fixture(author: false);
        var result = Assert.IsType<JsonResult>(await controller.PrivateNoteEditor(Guid.NewGuid(), Guid.NewGuid(), default));
        var model = Assert.IsType<PrescriptionPrivateNoteDetail>(result.Value); Assert.False(model.CanEdit); Assert.Null(model.NoteText);
    }
    [Fact]
    public async Task InvalidNonAuthorPostDoesNotRenderPrivateEditor()
    {
        var (controller, _) = Fixture(author: false); controller.ModelState.AddModelError("ProductName", "Required");
        var result = Assert.IsType<ViewResult>(await controller.Edit(Guid.NewGuid(), Guid.NewGuid(), new PrescriptionDraftRequest(), default));
        Assert.Equal(false, result.ViewData["CanEditPrivateNote"]);
    }
}
