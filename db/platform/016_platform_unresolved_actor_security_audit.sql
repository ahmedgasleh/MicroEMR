USE MicroEMR_Platform;
GO

IF OBJECT_ID(N'dbo.CK_PlatformSecurityAuditEvent_DenialReason', N'C') IS NOT NULL
    ALTER TABLE dbo.PlatformSecurityAuditEvent DROP CONSTRAINT CK_PlatformSecurityAuditEvent_DenialReason;

ALTER TABLE dbo.PlatformSecurityAuditEvent WITH CHECK
    ADD CONSTRAINT CK_PlatformSecurityAuditEvent_DenialReason
        CHECK (DenialReason IN
        (
            N'MissingPermission',
            N'CrossPatientOwnership',
            N'UnresolvedClinicalActor'
        ));
GO

IF OBJECT_ID(N'dbo.CK_PlatformSecurityAuditEvent_CapabilityPermission', N'C') IS NOT NULL
    ALTER TABLE dbo.PlatformSecurityAuditEvent DROP CONSTRAINT CK_PlatformSecurityAuditEvent_CapabilityPermission;

ALTER TABLE dbo.PlatformSecurityAuditEvent WITH CHECK
    ADD CONSTRAINT CK_PlatformSecurityAuditEvent_CapabilityPermission
        CHECK
        (
            (Capability = N'PatientChartView' AND RequiredPermission = N'Patients.View')
            OR (Capability = N'EncounterView' AND RequiredPermission = N'Encounters.View')
            OR (Capability = N'PatientDocumentView' AND RequiredPermission = N'Documents.View')
            OR (Capability = N'PatientFileDownload' AND RequiredPermission = N'Documents.View')
            OR (Capability = N'AppointmentReportRun' AND RequiredPermission = N'Reports.View')
            OR (Capability = N'AppointmentReportExport' AND RequiredPermission = N'Reports.Export')
            OR (Capability = N'EncounterEdit' AND RequiredPermission = N'Encounters.Edit')
        );
GO

IF OBJECT_ID(N'dbo.CK_PlatformSecurityAuditEvent_OwnershipShape', N'C') IS NOT NULL
    ALTER TABLE dbo.PlatformSecurityAuditEvent DROP CONSTRAINT CK_PlatformSecurityAuditEvent_OwnershipShape;

ALTER TABLE dbo.PlatformSecurityAuditEvent WITH CHECK
    ADD CONSTRAINT CK_PlatformSecurityAuditEvent_OwnershipShape
        CHECK
        (
            (
                DenialReason = N'MissingPermission'
                AND RequestedPatientUid IS NULL
                AND AuthoritativePatientUid IS NULL
                AND ResourceType IS NULL
                AND ResourceUid IS NULL
            )
            OR
            (
                DenialReason = N'CrossPatientOwnership'
                AND TargetTenantUid IS NOT NULL
                AND TargetTenantUid <> '00000000-0000-0000-0000-000000000000'
                AND RequestedPatientUid IS NOT NULL
                AND RequestedPatientUid <> '00000000-0000-0000-0000-000000000000'
                AND AuthoritativePatientUid IS NOT NULL
                AND AuthoritativePatientUid <> '00000000-0000-0000-0000-000000000000'
                AND RequestedPatientUid <> AuthoritativePatientUid
                AND ResourceType = N'Encounter'
                AND ResourceUid IS NOT NULL
                AND ResourceUid <> '00000000-0000-0000-0000-000000000000'
                AND Capability = N'EncounterView'
                AND RequiredPermission = N'Encounters.View'
                AND SourceApplication = N'MicroEMR.Api'
            )
            OR
            (
                DenialReason = N'UnresolvedClinicalActor'
                AND ClinicalUserId IS NULL
                AND TargetTenantUid IS NOT NULL
                AND TargetTenantUid <> '00000000-0000-0000-0000-000000000000'
                AND Capability = N'EncounterEdit'
                AND RequiredPermission = N'Encounters.Edit'
                AND SourceApplication = N'MicroEMR.Api'
                AND RequestedPatientUid IS NULL
                AND AuthoritativePatientUid IS NULL
                AND ResourceType IS NULL
                AND ResourceUid IS NULL
            )
        );
GO

-- Deployment must grant EXECUTE on dbo.PlatformSecurityAudit_RecordUnresolvedClinicalActor
-- to the configured API database principal. No direct table permission is required.
CREATE OR ALTER PROCEDURE dbo.PlatformSecurityAudit_RecordUnresolvedClinicalActor
    @ActorSubject NVARCHAR(451),
    @TargetTenantUid UNIQUEIDENTIFIER,
    @Capability NVARCHAR(101),
    @RequiredPermission NVARCHAR(101),
    @SourceApplication NVARCHAR(51),
    @RequestCorrelationId NVARCHAR(129) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @ActorSubject IS NULL OR LEN(LTRIM(RTRIM(@ActorSubject))) = 0
        THROW 51800, 'Authenticated actor subject is required.', 1;
    IF LEN(@ActorSubject) > 450
        THROW 51801, 'Authenticated actor subject is too long.', 1;
    IF @TargetTenantUid IS NULL OR @TargetTenantUid = '00000000-0000-0000-0000-000000000000'
        THROW 51802, 'Trusted tenant identifier is required.', 1;
    IF @Capability IS NULL OR LEN(@Capability) > 100
        THROW 51803, 'Capability is invalid.', 1;
    IF @RequiredPermission IS NULL OR LEN(@RequiredPermission) > 100
        THROW 51804, 'Required permission is invalid.', 1;
    IF @SourceApplication IS NULL OR LEN(@SourceApplication) > 50
        THROW 51805, 'Source application is invalid.', 1;
    IF @RequestCorrelationId IS NOT NULL AND LEN(@RequestCorrelationId) > 128
        THROW 51806, 'Request correlation identifier is too long.', 1;

    SET @ActorSubject = LTRIM(RTRIM(@ActorSubject));
    SET @Capability = LTRIM(RTRIM(@Capability));
    SET @RequiredPermission = LTRIM(RTRIM(@RequiredPermission));
    SET @SourceApplication = LTRIM(RTRIM(@SourceApplication));
    SET @RequestCorrelationId = NULLIF(LTRIM(RTRIM(@RequestCorrelationId)), N'');

    IF @Capability <> N'EncounterEdit' OR @RequiredPermission <> N'Encounters.Edit'
        THROW 51807, 'Capability and permission combination is not approved.', 1;
    IF @SourceApplication <> N'MicroEMR.Api'
        THROW 51808, 'Source application is not approved.', 1;

    INSERT dbo.PlatformSecurityAuditEvent
    (
        SecurityAuditEventUid, EventType, Outcome, DenialReason, ActorSubject,
        ClinicalUserId, TargetTenantUid, Capability, RequiredPermission,
        SourceApplication, RequestCorrelationId, OccurredAtUtc,
        RequestedPatientUid, AuthoritativePatientUid, ResourceType, ResourceUid
    )
    VALUES
    (
        NEWID(), N'SecurityAccessDenied', N'Denied', N'UnresolvedClinicalActor', @ActorSubject,
        NULL, @TargetTenantUid, N'EncounterEdit', N'Encounters.Edit',
        @SourceApplication, @RequestCorrelationId, SYSUTCDATETIME(),
        NULL, NULL, NULL, NULL
    );
END;
GO
