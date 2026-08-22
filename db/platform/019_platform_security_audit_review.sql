USE MicroEMR_Platform;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.PlatformSecurityAuditEvent')
      AND name = N'IX_PlatformSecurityAuditEvent_ReviewKeyset'
)
BEGIN
    CREATE INDEX IX_PlatformSecurityAuditEvent_ReviewKeyset
        ON dbo.PlatformSecurityAuditEvent(OccurredAtUtc DESC, SecurityAuditEventUid DESC)
        INCLUDE (DenialReason, Capability, RequiredPermission, SourceApplication,
                 TargetTenantUid, RequestedTenantUid, RequestCorrelationId, ActorSubject);
END;
GO

CREATE OR ALTER PROCEDURE dbo.PlatformSecurityAudit_Search
    @FromUtc DATETIME2(7),
    @ToUtc DATETIME2(7),
    @PageSize INT,
    @CursorOccurredAtUtc DATETIME2(7) = NULL,
    @CursorSecurityAuditEventUid UNIQUEIDENTIFIER = NULL,
    @DenialReason NVARCHAR(50) = NULL,
    @Capability NVARCHAR(100) = NULL,
    @SourceApplication NVARCHAR(50) = NULL,
    @TargetTenantUid UNIQUEIDENTIFIER = NULL,
    @RequestCorrelationId NVARCHAR(128) = NULL,
    @ActorSubject NVARCHAR(450) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @FromUtc IS NULL OR @ToUtc IS NULL OR @FromUtc >= @ToUtc
        THROW 52020, 'The UTC date range is invalid.', 1;
    IF DATEDIFF_BIG(SECOND, @FromUtc, @ToUtc) > 2678400
        THROW 52021, 'The UTC date range exceeds 31 days.', 1;
    IF @PageSize < 1 OR @PageSize > 100
        THROW 52022, 'Page size must be between 1 and 100.', 1;
    IF (@CursorOccurredAtUtc IS NULL AND @CursorSecurityAuditEventUid IS NOT NULL)
       OR (@CursorOccurredAtUtc IS NOT NULL AND @CursorSecurityAuditEventUid IS NULL)
        THROW 52023, 'The continuation key is invalid.', 1;
    IF @CursorSecurityAuditEventUid = '00000000-0000-0000-0000-000000000000'
        THROW 52023, 'The continuation key is invalid.', 1;
    IF @TargetTenantUid = '00000000-0000-0000-0000-000000000000'
        THROW 52024, 'The target tenant identifier is invalid.', 1;
    IF @DenialReason IS NOT NULL AND @DenialReason NOT IN
       (N'MissingPermission', N'CrossPatientOwnership', N'UnresolvedClinicalActor', N'InvalidTenantMembership')
        THROW 52025, 'The denial reason is invalid.', 1;
    IF @Capability IS NOT NULL AND @Capability NOT IN
       (N'PatientChartView', N'EncounterView', N'EncounterEdit', N'TenantSelection',
        N'PatientDocumentView', N'PatientFileDownload', N'AppointmentReportRun', N'AppointmentReportExport')
        THROW 52035, 'The capability is invalid.', 1;
    IF @SourceApplication IS NOT NULL AND @SourceApplication NOT IN
       (N'MicroEMR.Api', N'MicroEMR.Web', N'MicroEMR.Auth')
        THROW 52026, 'The source application is invalid.', 1;
    IF @RequestCorrelationId IS NOT NULL AND
       (LEN(LTRIM(RTRIM(@RequestCorrelationId))) = 0 OR LEN(@RequestCorrelationId) > 128)
        THROW 52027, 'The correlation identifier is invalid.', 1;
    IF @ActorSubject IS NOT NULL AND
       (LEN(LTRIM(RTRIM(@ActorSubject))) = 0 OR LEN(@ActorSubject) > 450)
        THROW 52028, 'The actor subject is invalid.', 1;

    SELECT TOP (@PageSize + 1)
        SecurityAuditEventUid, OccurredAtUtc, DenialReason, Capability,
        RequiredPermission, SourceApplication, TargetTenantUid, RequestCorrelationId,
        CONVERT(NVARCHAR(11), CASE WHEN LEN(ActorSubject) <= 8 THEN REPLICATE(N'*', LEN(ActorSubject))
             ELSE CONCAT(LEFT(ActorSubject, 4), N'...', RIGHT(ActorSubject, 4)) END)
             AS MaskedActorSubject
    FROM dbo.PlatformSecurityAuditEvent
    WHERE OccurredAtUtc >= @FromUtc
      AND OccurredAtUtc < @ToUtc
      AND (@CursorOccurredAtUtc IS NULL
           OR OccurredAtUtc < @CursorOccurredAtUtc
           OR (OccurredAtUtc = @CursorOccurredAtUtc
               AND SecurityAuditEventUid < @CursorSecurityAuditEventUid))
      AND (@DenialReason IS NULL OR DenialReason = @DenialReason)
      AND (@Capability IS NULL OR Capability = @Capability)
      AND (@SourceApplication IS NULL OR SourceApplication = @SourceApplication)
      AND (@TargetTenantUid IS NULL OR TargetTenantUid = @TargetTenantUid)
      AND (@RequestCorrelationId IS NULL OR RequestCorrelationId = @RequestCorrelationId)
      AND (@ActorSubject IS NULL OR ActorSubject = @ActorSubject)
    ORDER BY OccurredAtUtc DESC, SecurityAuditEventUid DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PlatformSecurityAudit_GetByUid
    @SecurityAuditEventUid UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    IF @SecurityAuditEventUid IS NULL OR
       @SecurityAuditEventUid = '00000000-0000-0000-0000-000000000000'
        THROW 52029, 'The security audit event identifier is invalid.', 1;

    SELECT SecurityAuditEventUid, EventType, Outcome, DenialReason, ActorSubject,
           ClinicalUserId, TargetTenantUid, RequestedTenantUid, Capability,
           RequiredPermission, SourceApplication, RequestCorrelationId,
           RequestedPatientUid, AuthoritativePatientUid, ResourceType, ResourceUid,
           OccurredAtUtc
    FROM dbo.PlatformSecurityAuditEvent
    WHERE SecurityAuditEventUid = @SecurityAuditEventUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PlatformAudit_RecordSecurityAuditReview
    @ActorSubject NVARCHAR(450),
    @Action NVARCHAR(100),
    @CorrelationId UNIQUEIDENTIFIER,
    @SecurityAuditEventUid UNIQUEIDENTIFIER = NULL,
    @ResultCount INT = NULL,
    @FilterSummary NVARCHAR(1000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @ActorSubject = LTRIM(RTRIM(@ActorSubject));
    IF NULLIF(@ActorSubject, N'') IS NULL OR LEN(@ActorSubject) > 450
        THROW 52030, 'The reviewer identity is invalid.', 1;
    IF @Action NOT IN (N'SecurityAuditSearched', N'SecurityAuditViewed')
        THROW 52031, 'The review action is invalid.', 1;
    IF @CorrelationId IS NULL OR @CorrelationId = '00000000-0000-0000-0000-000000000000'
        THROW 52032, 'The review correlation is invalid.', 1;
    IF (@Action = N'SecurityAuditSearched' AND
        (@SecurityAuditEventUid IS NOT NULL OR @ResultCount IS NULL OR @ResultCount < 0 OR @ResultCount > 100))
       OR (@Action = N'SecurityAuditViewed' AND
        (@SecurityAuditEventUid IS NULL OR @SecurityAuditEventUid = '00000000-0000-0000-0000-000000000000'
         OR @ResultCount IS NOT NULL OR @FilterSummary IS NOT NULL))
        THROW 52033, 'The review evidence shape is invalid.', 1;
    IF LEN(@FilterSummary) > 1000
        THROW 52034, 'The review filter summary is too long.', 1;

    DECLARE @DetailsJson NVARCHAR(2000) = CASE WHEN @Action = N'SecurityAuditSearched'
        THEN CONCAT(N'{"resultCount":', CONVERT(NVARCHAR(3), @ResultCount),
             N',"query":', COALESCE(@FilterSummary, N'null'), N'}')
        ELSE CONCAT(N'{"securityAuditEventUid":"', CONVERT(NVARCHAR(36), @SecurityAuditEventUid), N'"}') END;

    INSERT dbo.PlatformAuditEvent
    (
        PlatformAuditEventUid, ActorUserId, ActorType, Action, TargetTenantUid,
        TargetUserId, Outcome, OccurredAtUtc, CorrelationId, DetailsJson
    )
    VALUES
    (
        NEWID(), @ActorSubject, 'PlatformReviewer', @Action, NULL,
        NULL, 'Succeeded', SYSUTCDATETIME(), @CorrelationId, @DetailsJson
    );
END;
GO

-- Deployment grants should permit the platform review API principal to EXECUTE only
-- dbo.PlatformSecurityAudit_Search, dbo.PlatformSecurityAudit_GetByUid, and
-- dbo.PlatformAudit_RecordSecurityAuditReview. Direct table SELECT/DML is not required.
