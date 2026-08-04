using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Web.Controllers;
using MicroEMR.Web.Models.PatientFiles;
using MicroEMR.Web.Services.PatientFiles;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class PatientFileWebTests
{
    [Fact]
    public async Task WebContentActionStreamsBytesAndPreservesHeadersBeforeDisposal()
    {
        var patientUid = Guid.NewGuid(); var fileUid = Guid.NewGuid();
        var source = new TrackingStream("%PDF-streamed"u8.ToArray());
        var sourceLength = source.Length;
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(source) };
        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        response.Content.Headers.ContentLength = sourceLength;
        response.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment") { FileNameStar = "clinical report.pdf" };
        var controller = Controller(new StubClient(response));

        var result = await controller.Content(patientUid, fileUid, CancellationToken.None);

        Assert.IsType<EmptyResult>(result);
        Assert.Equal("%PDF-streamed"u8.ToArray(), ((MemoryStream)controller.Response.Body).ToArray());
        Assert.Equal("application/pdf", controller.Response.ContentType);
        Assert.Equal(sourceLength, controller.Response.ContentLength);
        Assert.Contains("filename*=utf-8''clinical%20report.pdf", controller.Response.Headers.ContentDisposition.ToString());
        Assert.True(source.Disposed);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, 401)]
    [InlineData(HttpStatusCode.Forbidden, 403)]
    [InlineData(HttpStatusCode.NotFound, 404)]
    public async Task WebContentActionMapsApiFailuresSafely(HttpStatusCode apiStatus, int expectedStatus)
    {
        var controller = Controller(new StubClient(apiStatus == HttpStatusCode.NotFound
            ? new HttpResponseMessage(HttpStatusCode.NotFound)
            : new HttpRequestException("internal", null, apiStatus)));
        var result = await controller.Content(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);
        Assert.Equal(expectedStatus, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task ClientUsesPatientScopedRoutesAndExactMultipartFields()
    {
        var patientUid = Guid.NewGuid(); var fileUid = Guid.NewGuid(); var handler = new Handler(fileUid, patientUid);
        var client = new PatientFileApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test/") },
            new HttpContextAccessor { HttpContext = Context() });

        await client.GetByPatientUidAsync(patientUid);
        await client.GetByUidAsync(patientUid, fileUid);
        await using var bytes = new MemoryStream("%PDF-test"u8.ToArray());
        var formFile = new FormFile(bytes, 0, bytes.Length, "ignored-browser-field", "report.pdf") { Headers = new HeaderDictionary(), ContentType = "application/pdf" };
        await client.UploadAsync(patientUid, formFile, "Clinical report", "Reports");
        using var content = await client.GetContentAsync(patientUid, fileUid);

        Assert.Equal($"api/patients/{patientUid}/files", handler.Requests[0].Path);
        Assert.Equal($"api/patients/{patientUid}/files/{fileUid}", handler.Requests[1].Path);
        Assert.Equal(HttpMethod.Post, handler.Requests[2].Method);
        Assert.Equal($"api/patients/{patientUid}/files/{fileUid}/content", handler.Requests[3].Path);
        var body = handler.Requests[2].Body!;
        Assert.Contains("name=file", body); Assert.Contains("filename=report.pdf", body);
        Assert.Contains("name=description", body); Assert.Contains("Clinical report", body);
        Assert.Contains("name=category", body); Assert.Contains("Reports", body);
        Assert.DoesNotContain("PatientUid", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Tenant", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Actor", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("StorageKey", body, StringComparison.OrdinalIgnoreCase);
        Assert.All(handler.Requests, request => Assert.Equal("Bearer test-token", request.Authorization));
    }

    [Fact]
    public void PatientFilesUiDoesNotExposeStorageOrLifecycleActions()
    {
        var root = FindRepositoryRoot();
        var view = File.ReadAllText(Path.Combine(root, "src", "MicroEMR.Web", "Views", "Patients", "Details.cshtml"));
        var script = File.ReadAllText(Path.Combine(root, "src", "MicroEMR.Web", "ClientApp", "patients", "patient-files.ts"));
        Assert.Contains("No files uploaded for this patient.", script);
        Assert.Contains("Upload File", view); Assert.Contains("Maximum 25 MB", view);
        Assert.Contains("data-content-url-root", view);
        Assert.DoesNotContain("data-content-url-template", view);
        Assert.Contains("/${encodeURIComponent(uid)}/content", script);
        Assert.DoesNotContain("StorageKey", view); Assert.DoesNotContain("StorageKey", script);
        Assert.DoesNotContain(">Archive<", view); Assert.DoesNotContain(">Delete<", view);
        Assert.DoesNotContain("PatientUid\"", view[view.IndexOf("patientFileUploadForm", StringComparison.Ordinal)..view.IndexOf("</form>", view.IndexOf("patientFileUploadForm", StringComparison.Ordinal), StringComparison.Ordinal)]);
    }

    private static string FindRepositoryRoot([System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
    {
        var sourceDirectory = Path.GetDirectoryName(sourceFile);
        if (!string.IsNullOrWhiteSpace(sourceDirectory))
        {
            var sourceRoot = Path.GetFullPath(Path.Combine(sourceDirectory, "..", ".."));
            if (File.Exists(Path.Combine(sourceRoot, "MicroEMR.slnx"))) return sourceRoot;
        }
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MicroEMR.slnx"))) directory = directory.Parent;
        if (directory is not null) return directory.FullName;
        directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MicroEMR.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
    }

    private static PatientFilesController Controller(IPatientFileApiClient client)
    {
        var controller = new PatientFilesController(client, NullLogger<PatientFilesController>.Instance);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        controller.Response.Body = new MemoryStream();
        return controller;
    }

    private sealed class StubClient(object contentResult) : IPatientFileApiClient
    {
        public Task<HttpResponseMessage> GetContentAsync(Guid patientUid, Guid fileUid, CancellationToken cancellationToken = default) =>
            contentResult is Exception exception ? Task.FromException<HttpResponseMessage>(exception) : Task.FromResult((HttpResponseMessage)contentResult);
        public Task<IReadOnlyList<PatientFileViewModel>> GetByPatientUidAsync(Guid patientUid, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PatientFileViewModel?> GetByUidAsync(Guid patientUid, Guid fileUid, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PatientFileViewModel?> UploadAsync(Guid patientUid, IFormFile file, string? description, string? category, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class TrackingStream(byte[] bytes) : MemoryStream(bytes)
    {
        public bool Disposed { get; private set; }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }

    private static DefaultHttpContext Context()
    {
        var properties = new AuthenticationProperties(); properties.StoreTokens([new AuthenticationToken { Name = "access_token", Value = "test-token" }]);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "user")], "test")), properties, "test");
        return new DefaultHttpContext { RequestServices = new ServiceCollection().AddSingleton<IAuthenticationService>(new Auth(ticket)).BuildServiceProvider() };
    }

    private sealed class Handler(Guid fileUid, Guid patientUid) : HttpMessageHandler
    {
        public List<(HttpMethod Method, string Path, string? Body, string? Authorization)> Requests { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request.Method, request.RequestUri!.PathAndQuery.TrimStart('/'), request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken), request.Headers.Authorization?.ToString()));
            var isList = request.Method == HttpMethod.Get && request.RequestUri.AbsolutePath.EndsWith("/files", StringComparison.Ordinal);
            var json = $"{{\"fileUid\":\"{fileUid}\",\"patientUid\":\"{patientUid}\",\"originalFileName\":\"report.pdf\",\"contentType\":\"application/pdf\",\"fileSizeBytes\":9,\"status\":\"Active\",\"uploadedAtUtc\":\"2026-08-04T12:00:00Z\",\"uploadedBy\":1,\"rowVersion\":\"v\"}}";
            if (request.RequestUri.AbsolutePath.EndsWith("/content", StringComparison.Ordinal)) return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("content") };
            return new HttpResponseMessage(request.Method == HttpMethod.Post ? HttpStatusCode.Created : HttpStatusCode.OK) { Content = new StringContent(isList ? $"[{json}]" : json, System.Text.Encoding.UTF8, "application/json") };
        }
    }

    private sealed class Auth(AuthenticationTicket ticket) : IAuthenticationService
    {
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) => Task.FromResult(AuthenticateResult.Success(ticket));
        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
    }
}
