USE MicroEMR_Platform;
GO

IF OBJECT_ID(N'dbo.PlatformSecurityAuditEvent', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PlatformSecurityAuditEvent
    (
        SecurityAuditEventUid UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT PK_PlatformSecurityAuditEvent PRIMARY KEY
            CONSTRAINT DF_PlatformSecurityAuditEvent_Uid DEFAULT NEWID(),
        EventType NVARCHAR(50) NOT NULL,
        Outcome VARCHAR(30) NOT NULL,
        DenialReason NVARCHAR(50) NOT NULL,
        ActorSubject NVARCHAR(450) NOT NULL,
        ClinicalUserId BIGINT NULL,
        TargetTenantUid UNIQUEIDENTIFIER NULL,
        Capability NVARCHAR(100) NOT NULL,
        RequiredPermission NVARCHAR(100) NOT NULL,
        SourceApplication NVARCHAR(50) NOT NULL,
        RequestCorrelationId NVARCHAR(128) NULL,
        OccurredAtUtc DATETIME2(7) NOT NULL
            CONSTRAINT DF_PlatformSecurityAuditEvent_OccurredAtUtc DEFAULT SYSUTCDATETIME(),

        CONSTRAINT CK_PlatformSecurityAuditEvent_EventType
            CHECK (EventType = N'SecurityAccessDenied'),
        CONSTRAINT CK_PlatformSecurityAuditEvent_Outcome
            CHECK (Outcome = 'Denied'),
        CONSTRAINT CK_PlatformSecurityAuditEvent_DenialReason
            CHECK (DenialReason = N'MissingPermission'),
        CONSTRAINT CK_PlatformSecurityAuditEvent_ActorSubject
            CHECK (LEN(LTRIM(RTRIM(ActorSubject))) > 0),
        CONSTRAINT CK_PlatformSecurityAuditEvent_ClinicalUserId
            CHECK (ClinicalUserId IS NULL OR ClinicalUserId > 0),
        CONSTRAINT CK_PlatformSecurityAuditEvent_SourceApplication
            CHECK (SourceApplication IN (N'MicroEMR.Api', N'MicroEMR.Web')),
        CONSTRAINT CK_PlatformSecurityAuditEvent_RequestCorrelationId
            CHECK (RequestCorrelationId IS NULL OR LEN(LTRIM(RTRIM(RequestCorrelationId))) > 0),
        CONSTRAINT CK_PlatformSecurityAuditEvent_CapabilityPermission
            CHECK
            (
                (Capability = N'PatientChartView' AND RequiredPermission = N'Patients.View')
                OR (Capability = N'EncounterView' AND RequiredPermission = N'Encounters.View')
                OR (Capability = N'PatientDocumentView' AND RequiredPermission = N'Documents.View')
                OR (Capability = N'PatientFileDownload' AND RequiredPermission = N'Documents.View')
                OR (Capability = N'AppointmentReportRun' AND RequiredPermission = N'Reports.View')
                OR (Capability = N'AppointmentReportExport' AND RequiredPermission = N'Reports.Export')
            )
    );

    CREATE INDEX IX_PlatformSecurityAuditEvent_OccurredAtUtc
        ON dbo.PlatformSecurityAuditEvent(OccurredAtUtc DESC);
    CREATE INDEX IX_PlatformSecurityAuditEvent_TenantTime
        ON dbo.PlatformSecurityAuditEvent(TargetTenantUid, OccurredAtUtc DESC)
        WHERE TargetTenantUid IS NOT NULL;
    CREATE INDEX IX_PlatformSecurityAuditEvent_ActorTime
        ON dbo.PlatformSecurityAuditEvent(ActorSubject, OccurredAtUtc DESC);
    CREATE INDEX IX_PlatformSecurityAuditEvent_RequestCorrelation
        ON dbo.PlatformSecurityAuditEvent(RequestCorrelationId)
        WHERE RequestCorrelationId IS NOT NULL;
END;
GO

-- Deployment must grant EXECUTE on dbo.PlatformSecurityAudit_RecordMissingPermission
-- to the configured application database principal. No direct table permission is required.
CREATE OR ALTER PROCEDURE dbo.PlatformSecurityAudit_RecordMissingPermission
    @ActorSubject NVARCHAR(451),
    @ClinicalUserId BIGINT = NULL,
    @TargetTenantUid UNIQUEIDENTIFIER = NULL,
    @Capability NVARCHAR(101),
    @RequiredPermission NVARCHAR(101),
    @SourceApplication NVARCHAR(51),
    @RequestCorrelationId NVARCHAR(129) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @ActorSubject IS NULL OR LEN(LTRIM(RTRIM(@ActorSubject))) = 0
        THROW 51600, 'Authenticated actor subject is required.', 1;
    IF LEN(@ActorSubject) > 450
        THROW 51601, 'Authenticated actor subject is too long.', 1;
    IF @ClinicalUserId IS NOT NULL AND @ClinicalUserId <= 0
        THROW 51602, 'Clinical user identifier is invalid.', 1;
    IF @Capability IS NULL OR LEN(@Capability) > 100
        THROW 51603, 'Capability is invalid.', 1;
    IF @RequiredPermission IS NULL OR LEN(@RequiredPermission) > 100
        THROW 51604, 'Required permission is invalid.', 1;
    IF @SourceApplication IS NULL OR LEN(@SourceApplication) > 50
        THROW 51605, 'Source application is invalid.', 1;
    IF @RequestCorrelationId IS NOT NULL AND LEN(@RequestCorrelationId) > 128
        THROW 51606, 'Request correlation identifier is too long.', 1;

    SET @ActorSubject = LTRIM(RTRIM(@ActorSubject));
    SET @Capability = LTRIM(RTRIM(@Capability));
    SET @RequiredPermission = LTRIM(RTRIM(@RequiredPermission));
    SET @SourceApplication = LTRIM(RTRIM(@SourceApplication));
    SET @RequestCorrelationId = NULLIF(LTRIM(RTRIM(@RequestCorrelationId)), N'');

    IF @SourceApplication NOT IN (N'MicroEMR.Api', N'MicroEMR.Web')
        THROW 51607, 'Source application is not approved.', 1;
    IF NOT
    (
        (@Capability = N'PatientChartView' AND @RequiredPermission = N'Patients.View')
        OR (@Capability = N'EncounterView' AND @RequiredPermission = N'Encounters.View')
        OR (@Capability = N'PatientDocumentView' AND @RequiredPermission = N'Documents.View')
        OR (@Capability = N'PatientFileDownload' AND @RequiredPermission = N'Documents.View')
        OR (@Capability = N'AppointmentReportRun' AND @RequiredPermission = N'Reports.View')
        OR (@Capability = N'AppointmentReportExport' AND @RequiredPermission = N'Reports.Export')
    )
        THROW 51608, 'Capability and permission combination is not approved.', 1;

    INSERT dbo.PlatformSecurityAuditEvent
    (
        SecurityAuditEventUid, EventType, Outcome, DenialReason, ActorSubject,
        ClinicalUserId, TargetTenantUid, Capability, RequiredPermission,
        SourceApplication, RequestCorrelationId, OccurredAtUtc
    )
    VALUES
    (
        NEWID(), N'SecurityAccessDenied', 'Denied', N'MissingPermission', @ActorSubject,
        @ClinicalUserId, @TargetTenantUid, @Capability, @RequiredPermission,
        @SourceApplication, @RequestCorrelationId, SYSUTCDATETIME()
    );
END;
GO
