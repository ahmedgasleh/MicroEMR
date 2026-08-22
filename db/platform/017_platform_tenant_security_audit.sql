USE MicroEMR_Platform;
GO

IF OBJECT_ID(N'dbo.CK_PlatformSecurityAuditEvent_OwnershipShape', N'C') IS NOT NULL
    ALTER TABLE dbo.PlatformSecurityAuditEvent DROP CONSTRAINT CK_PlatformSecurityAuditEvent_OwnershipShape;

IF OBJECT_ID(N'dbo.CK_PlatformSecurityAuditEvent_CapabilityPermission', N'C') IS NOT NULL
    ALTER TABLE dbo.PlatformSecurityAuditEvent DROP CONSTRAINT CK_PlatformSecurityAuditEvent_CapabilityPermission;
GO

IF COL_LENGTH(N'dbo.PlatformSecurityAuditEvent', N'RequestedTenantUid') IS NULL
    ALTER TABLE dbo.PlatformSecurityAuditEvent
        ADD RequestedTenantUid UNIQUEIDENTIFIER NULL;

ALTER TABLE dbo.PlatformSecurityAuditEvent
    ALTER COLUMN RequiredPermission NVARCHAR(100) NULL;
GO

IF OBJECT_ID(N'dbo.CK_PlatformSecurityAuditEvent_DenialReason', N'C') IS NOT NULL
    ALTER TABLE dbo.PlatformSecurityAuditEvent DROP CONSTRAINT CK_PlatformSecurityAuditEvent_DenialReason;

ALTER TABLE dbo.PlatformSecurityAuditEvent WITH CHECK
    ADD CONSTRAINT CK_PlatformSecurityAuditEvent_DenialReason
        CHECK (DenialReason IN
        (
            N'MissingPermission',
            N'CrossPatientOwnership',
            N'UnresolvedClinicalActor',
            N'InvalidTenantMembership'
        ));
GO

IF OBJECT_ID(N'dbo.CK_PlatformSecurityAuditEvent_SourceApplication', N'C') IS NOT NULL
    ALTER TABLE dbo.PlatformSecurityAuditEvent DROP CONSTRAINT CK_PlatformSecurityAuditEvent_SourceApplication;

ALTER TABLE dbo.PlatformSecurityAuditEvent WITH CHECK
    ADD CONSTRAINT CK_PlatformSecurityAuditEvent_SourceApplication
        CHECK
        (
            (DenialReason <> N'InvalidTenantMembership'
                AND SourceApplication IN (N'MicroEMR.Api', N'MicroEMR.Web'))
            OR
            (DenialReason = N'InvalidTenantMembership'
                AND SourceApplication = N'MicroEMR.Auth')
        );
GO

ALTER TABLE dbo.PlatformSecurityAuditEvent WITH CHECK
    ADD CONSTRAINT CK_PlatformSecurityAuditEvent_CapabilityPermission
        CHECK
        (
            (
                RequiredPermission IS NOT NULL
                AND
                (
                    (Capability = N'PatientChartView' AND RequiredPermission = N'Patients.View')
                    OR (Capability = N'EncounterView' AND RequiredPermission = N'Encounters.View')
                    OR (Capability = N'PatientDocumentView' AND RequiredPermission = N'Documents.View')
                    OR (Capability = N'PatientFileDownload' AND RequiredPermission = N'Documents.View')
                    OR (Capability = N'AppointmentReportRun' AND RequiredPermission = N'Reports.View')
                    OR (Capability = N'AppointmentReportExport' AND RequiredPermission = N'Reports.Export')
                    OR (Capability = N'EncounterEdit' AND RequiredPermission = N'Encounters.Edit')
                )
            )
            OR
            (Capability = N'TenantSelection' AND RequiredPermission IS NULL)
        );
GO

ALTER TABLE dbo.PlatformSecurityAuditEvent WITH CHECK
    ADD CONSTRAINT CK_PlatformSecurityAuditEvent_OwnershipShape
        CHECK
        (
            (
                DenialReason = N'MissingPermission'
                AND RequiredPermission IS NOT NULL
                AND RequestedTenantUid IS NULL
                AND RequestedPatientUid IS NULL
                AND AuthoritativePatientUid IS NULL
                AND ResourceType IS NULL
                AND ResourceUid IS NULL
            )
            OR
            (
                DenialReason = N'CrossPatientOwnership'
                AND RequiredPermission = N'Encounters.View'
                AND RequestedTenantUid IS NULL
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
                AND SourceApplication = N'MicroEMR.Api'
            )
            OR
            (
                DenialReason = N'UnresolvedClinicalActor'
                AND ClinicalUserId IS NULL
                AND RequestedTenantUid IS NULL
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
            OR
            (
                DenialReason = N'InvalidTenantMembership'
                AND ClinicalUserId IS NULL
                AND TargetTenantUid IS NULL
                AND RequestedTenantUid IS NOT NULL
                AND RequestedTenantUid <> '00000000-0000-0000-0000-000000000000'
                AND Capability = N'TenantSelection'
                AND RequiredPermission IS NULL
                AND SourceApplication = N'MicroEMR.Auth'
                AND RequestedPatientUid IS NULL
                AND AuthoritativePatientUid IS NULL
                AND ResourceType IS NULL
                AND ResourceUid IS NULL
            )
        );
GO

-- Deployment must grant EXECUTE on dbo.PlatformSecurityAudit_RecordInvalidTenantMembership
-- to the configured Auth database principal. No direct table permission is required.
CREATE OR ALTER PROCEDURE dbo.PlatformSecurityAudit_RecordInvalidTenantMembership
    @ActorSubject NVARCHAR(451),
    @RequestedTenantUid UNIQUEIDENTIFIER,
    @SourceApplication NVARCHAR(51),
    @RequestCorrelationId NVARCHAR(129) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @ActorSubject IS NULL OR LEN(LTRIM(RTRIM(@ActorSubject))) = 0
        THROW 51900, 'Authenticated actor subject is required.', 1;
    IF LEN(@ActorSubject) > 450
        THROW 51901, 'Authenticated actor subject is too long.', 1;
    IF @RequestedTenantUid IS NULL OR @RequestedTenantUid = '00000000-0000-0000-0000-000000000000'
        THROW 51902, 'Requested tenant identifier is required.', 1;
    IF @SourceApplication IS NULL OR LEN(@SourceApplication) > 50
        THROW 51903, 'Source application is invalid.', 1;
    IF @RequestCorrelationId IS NOT NULL AND LEN(@RequestCorrelationId) > 128
        THROW 51904, 'Request correlation identifier is too long.', 1;

    SET @ActorSubject = LTRIM(RTRIM(@ActorSubject));
    SET @SourceApplication = LTRIM(RTRIM(@SourceApplication));
    SET @RequestCorrelationId = NULLIF(LTRIM(RTRIM(@RequestCorrelationId)), N'');

    IF @SourceApplication <> N'MicroEMR.Auth'
        THROW 51905, 'Source application is not approved.', 1;

    INSERT dbo.PlatformSecurityAuditEvent
    (
        SecurityAuditEventUid, EventType, Outcome, DenialReason, ActorSubject,
        ClinicalUserId, TargetTenantUid, RequestedTenantUid, Capability,
        RequiredPermission, SourceApplication, RequestCorrelationId, OccurredAtUtc,
        RequestedPatientUid, AuthoritativePatientUid, ResourceType, ResourceUid
    )
    VALUES
    (
        NEWID(), N'SecurityAccessDenied', N'Denied', N'InvalidTenantMembership', @ActorSubject,
        NULL, NULL, @RequestedTenantUid, N'TenantSelection',
        NULL, @SourceApplication, @RequestCorrelationId, SYSUTCDATETIME(),
        NULL, NULL, NULL, NULL
    );
END;
GO
