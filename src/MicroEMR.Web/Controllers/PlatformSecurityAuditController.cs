using Microsoft.AspNetCore.Mvc;
using MicroEMR.Application.PlatformEntitlements;
using MicroEMR.Application.SecurityAudit;
using MicroEMR.Web.Authorization;
using MicroEMR.Web.Models.SecurityAudit;
using MicroEMR.Web.Services.SecurityAudit;

namespace MicroEMR.Web.Controllers;

[RequirePlatformEntitlement(PlatformEntitlementKeys.SecurityAuditView)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PlatformSecurityAuditController(
    ISecurityAuditApiClient client,
    ISecurityAuditPagingStateProtector pagingStateProtector,
    ILogger<PlatformSecurityAuditController> logger) : Controller
{
    [HttpGet]
    public Task<IActionResult> Index(CancellationToken cancellationToken) =>
        RunSearchAsync(new SecurityAuditSearchForm(), cancellationToken);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Search(
        SecurityAuditSearchForm filters, CancellationToken cancellationToken)
    {
        filters.ContinuationToken = null;
        return RunSearchAsync(filters, cancellationToken);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Older(
        string pagingStateToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(pagingStateToken) || pagingStateToken.Length > 5000 ||
            !pagingStateProtector.TryUnprotect(pagingStateToken, out var filters))
            return Task.FromResult<IActionResult>(View("Index", new SecurityAuditIndexViewModel
            {
                Filters = new(), ErrorMessage = "Paging state is invalid. Apply the filters again.",
                IsValidationError = true
            }));
        return RunSearchAsync(filters, cancellationToken);
    }

    [HttpGet]
    public IActionResult Reset() => RedirectToAction(nameof(Index));

    [HttpGet]
    public async Task<IActionResult> Details(
        Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return NotFound();
        try
        {
            var detail = await client.GetAsync(id, cancellationToken);
            return detail is null ? NotFound() : View(new SecurityAuditDetailViewModel { Detail = detail });
        }
        catch (SecurityAuditApiException exception) when (
            exception.Failure == SecurityAuditApiFailure.Unauthorized)
        {
            return Forbid();
        }
        catch (Exception exception) when (exception is SecurityAuditApiException or HttpRequestException)
        {
            logger.LogError(exception,
                "Security Audit detail could not be loaded. EventUid: {SecurityAuditEventUid}.", id);
            return View(new SecurityAuditDetailViewModel
            {
                ErrorMessage = "Security Audit detail is temporarily unavailable. Please try again."
            });
        }
    }

    private async Task<IActionResult> RunSearchAsync(
        SecurityAuditSearchForm filters, CancellationToken cancellationToken)
    {
        var validation = Validate(filters);
        if (validation is not null)
            return View("Index", new SecurityAuditIndexViewModel
            {
                Filters = SafeForDisplay(filters), ErrorMessage = validation, IsValidationError = true
            });

        try
        {
            var result = await client.SearchAsync(ToRequest(filters), cancellationToken);
            filters.FromUtc = result.FromUtc.UtcDateTime;
            filters.ToUtc = result.ToUtc.UtcDateTime;
            filters.ContinuationToken = result.ContinuationToken;
            var actorApplied = !string.IsNullOrWhiteSpace(filters.ActorSubject);
            var pagingState = result.ContinuationToken is null ? null : pagingStateProtector.Protect(filters);
            return View("Index", new SecurityAuditIndexViewModel
            {
                Filters = SafeForDisplay(filters), Results = result,
                PagingStateToken = pagingState, ActorSubjectFilterApplied = actorApplied
            });
        }
        catch (SecurityAuditApiException exception) when (
            exception.Failure == SecurityAuditApiFailure.Unauthorized)
        {
            return Forbid();
        }
        catch (SecurityAuditApiException exception) when (
            exception.Failure == SecurityAuditApiFailure.Validation)
        {
            return View("Index", new SecurityAuditIndexViewModel
            {
                Filters = SafeForDisplay(filters),
                ErrorMessage = "The selected filters are invalid. Check the UTC date range and exact values.",
                IsValidationError = true
            });
        }
        catch (Exception exception) when (exception is SecurityAuditApiException or HttpRequestException)
        {
            logger.LogError(exception, "Security Audit search could not be loaded.");
            return View("Index", new SecurityAuditIndexViewModel
            {
                Filters = SafeForDisplay(filters),
                ErrorMessage = "Security Audit is temporarily unavailable. Please try again."
            });
        }
    }

    private static SecurityAuditSearchRequest ToRequest(SecurityAuditSearchForm value) => new(
        Utc(value.FromUtc), Utc(value.ToUtc), PlatformSecurityAuditReviewService.DefaultPageSize,
        value.ContinuationToken, value.DenialReason, value.Capability, value.SourceApplication,
        value.TargetTenantUid, value.RequestCorrelationId, value.ActorSubject);

    private static SecurityAuditSearchForm SafeForDisplay(SecurityAuditSearchForm value) => new()
    {
        FromUtc = value.FromUtc, ToUtc = value.ToUtc, DenialReason = value.DenialReason,
        Capability = value.Capability, SourceApplication = value.SourceApplication,
        TargetTenantUid = value.TargetTenantUid, RequestCorrelationId = value.RequestCorrelationId
    };

    private static DateTimeOffset? Utc(DateTime? value) => value is null
        ? null
        : new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc));

    private static string? Validate(SecurityAuditSearchForm value)
    {
        if (value.FromUtc.HasValue != value.ToUtc.HasValue)
            return "Provide both From and To UTC values, or leave both blank for the last 24 hours.";
        if (value.FromUtc is not null && value.ToUtc is not null)
        {
            if (value.FromUtc >= value.ToUtc) return "From UTC must be earlier than To UTC.";
            if (value.ToUtc - value.FromUtc > TimeSpan.FromDays(31))
                return "The Security Audit range cannot exceed 31 days.";
        }
        if (value.TargetTenantUid == Guid.Empty) return "Trusted Tenant UID must be a non-empty GUID.";
        if (value.RequestCorrelationId?.Trim().Length > 128) return "Correlation ID cannot exceed 128 characters.";
        if (value.ActorSubject?.Trim().Length > 450) return "Actor Subject cannot exceed 450 characters.";
        return null;
    }
}
