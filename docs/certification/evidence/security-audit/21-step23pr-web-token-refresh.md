# Step 23P-R — Web token refresh

## Session problem and scope

MicroEMR Web saved OpenID Connect access and refresh tokens in its protected authentication
ticket, but every API client forwarded the saved access token without renewing it. Shortening
access-token lifetime would therefore have interrupted active sessions. This step adds only
server-side Web session renewal; access-token lifetime and platform authorization are unchanged.

## Central refresh pipeline

`WebApiBearerTokenHandler` is attached to every typed Web-to-API `HttpClient`. It obtains a
valid token through `WebSessionTokenService` and replaces any legacy bearer header immediately
before sending the API request. Refresh logic is not duplicated in controllers or API clients.

The service reads the protected cookie ticket's `expires_at`. The configurable
`Authentication:TokenRefresh:RefreshThreshold` defaults to one minute. A token outside that
window is forwarded without a token-endpoint call or cookie rewrite.

`OpenIdConnectRefreshTokenClient` resolves the token endpoint from the trusted OpenID Connect
metadata for the configured authority and uses the existing confidential Web client credentials.
It uses a dedicated 15-second `HttpClient` with no API refresh handler, preventing recursion.
The request uses only the refresh grant, refresh token, client ID, and client secret.

## Rotation, persistence, and failures

On success, the service replaces `access_token`, replaces `refresh_token` when rotated (or
retains it when omitted), updates `expires_at` and ticket expiry, and signs the same principal
back into the existing cookie scheme. The refreshed access token is used for the current API
request. Claims and unrelated authentication properties are preserved.

`invalid_grant` invalidates the local cookie session, stops the API request, and requires an
interactive sign-in. A dedicated middleware challenges normal page requests and returns 401 for
JSON/AJAX requests so browser code never receives protocol details. Network, timeout, 5xx,
malformed, and other temporary failures preserve
the existing ticket and propagate a generic temporary-authentication failure. No refresh is
retried in a single request.

Token values, authorization headers, response bodies, and client secrets are never logged or
returned to browser code. Logs contain only status-class operational events.

## Concurrency model

`SessionTokenRefreshCoordinator` serializes refresh by a SHA-256-derived key for the current
refresh token. Inside the serialized callback, the service re-authenticates and re-evaluates the
ticket before redemption. Waiting requests re-check the completed coordinator entry after
acquiring the lock and reuse its refreshed token set or terminal outcome, preventing duplicate
redemption, repeated invalid grants, and rotation races. Entries expire opportunistically after
two minutes and gates are released in `finally`.

This coordinator is process-local. It is appropriate to the current single Web-instance model,
but it does not serialize refresh across horizontally scaled instances. Before multi-instance
Web hosting, replace it with distributed/session-coordinated refresh protection or use a shared
server-side ticket/token store that guarantees single redemption.

The Auth token endpoint remains the future extension point for reloading and validating
`platform_authorization_version`; no entitlement/version behavior is included here.

## Verification

Focused tests cover: no-refresh forwarding, near-expiry rotation and cookie persistence,
concurrent single redemption and reuse, stale-header replacement, `invalid_grant` session
invalidation, and temporary-failure ticket preservation. Existing Auth/API tests and the Release
solution build are regression gates.

No database files or migrations changed. Platform migrations remain through `018`; tenant
migrations remain through `0046`. Access-token lifetime remains unchanged.

## Manual runtime checklist

1. Sign in normally and make representative patient chart, scheduling, report, and file calls.
2. In a controlled development environment, move the protected ticket expiry inside the refresh
   window without displaying or logging token values.
3. Trigger an API request and confirm it succeeds without redirecting to sign-in.
4. Confirm a subsequent request succeeds and server-side diagnostics show cookie renewal without
   token values.
5. Trigger simultaneous API requests in the refresh window and confirm one token redemption.
6. Invalidate the refresh grant in a controlled test and confirm the API request is stopped and
   interactive reauthentication is required.
7. Review logs and browser responses to confirm that no access token, refresh token, bearer value,
   or client secret is present.

After automated and manual verification, Step 23P-B can resume with the documented single-instance
constraint addressed for any future horizontally scaled deployment.
