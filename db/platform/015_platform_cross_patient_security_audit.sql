USE MicroEMR_Platform;
GO

IF COL_LENGTH(N'dbo.PlatformSecurityAuditEvent', N'RequestedPatientUid') IS NULL
    ALTER TABLE dbo.PlatformSecurityAuditEvent ADD RequestedPatientUid UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.PlatformSecurityAuditEvent', N'AuthoritativePatientUid') IS NULL
    ALTER TABLE dbo.PlatformSecurityAuditEvent ADD AuthoritativePatientUid UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.PlatformSecurityAuditEvent', N'ResourceType') IS NULL
    ALTER TABLE dbo.PlatformSecurityAuditEvent ADD ResourceType NVARCHAR(50) NULL;
IF COL_LENGTH(N'dbo.PlatformSecurityAuditEvent', N'ResourceUid') IS NULL
    ALTER TABLE dbo.PlatformSecurityAuditEvent ADD ResourceUid UNIQUEIDENTIFIER NULL;
GO

IF OBJECT_ID(N'dbo.CK_PlatformSecurityAuditEvent_DenialReason', N'C') IS NOT NULL
    ALTER TABLE dbo.PlatformSecurityAuditEvent
        DROP CONSTRAINT CK_PlatformSecurityAuditEvent_DenialReason;

ALTER TABLE dbo.PlatformSecurityAuditEvent WITH CHECK
    ADD CONSTRAINT CK_PlatformSecurityAuditEvent_DenialReason
        CHECK (DenialReason IN (N'MissingPermission', N'CrossPatientOwnership'));
GO

IF OBJECT_ID(N'dbo.CK_PlatformSecurityAuditEvent_OwnershipShape', N'C') IS NOT NULL
    ALTER TABLE dbo.PlatformSecurityAuditEvent
        DROP CONSTRAINT CK_PlatformSecurityAuditEvent_OwnershipShape;

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
        );
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.PlatformSecurityAuditEvent')
      AND name = N'IX_PlatformSecurityAuditEvent_OwnershipResourceTime'
)
    CREATE INDEX IX_PlatformSecurityAuditEvent_OwnershipResourceTime
        ON dbo.PlatformSecurityAuditEvent
        (
            TargetTenantUid,
            ResourceType,
            ResourceUid,
            OccurredAtUtc DESC
        )
        WHERE DenialReason = N'CrossPatientOwnership';
GO

-- Deployment must grant EXECUTE on dbo.PlatformSecurityAudit_RecordCrossPatientOwnership
-- to the configured API database principal. No direct table permission is required.
CREATE OR ALTER PROCEDURE dbo.PlatformSecurityAudit_RecordCrossPatientOwnership
    @ActorSubject NVARCHAR(451),
    @ClinicalUserId BIGINT = NULL,
    @TargetTenantUid UNIQUEIDENTIFIER,
    @Capability NVARCHAR(101),
    @RequestedPatientUid UNIQUEIDENTIFIER,
    @AuthoritativePatientUid UNIQUEIDENTIFIER,
    @ResourceType NVARCHAR(51),
    @ResourceUid UNIQUEIDENTIFIER,
    @SourceApplication NVARCHAR(51),
    @RequestCorrelationId NVARCHAR(129) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @ActorSubject IS NULL OR LEN(LTRIM(RTRIM(@ActorSubject))) = 0
        THROW 51700, 'Authenticated actor subject is required.', 1;
    IF LEN(@ActorSubject) > 450
        THROW 51701, 'Authenticated actor subject is too long.', 1;
    IF @ClinicalUserId IS NOT NULL AND @ClinicalUserId <= 0
        THROW 51702, 'Clinical user identifier is invalid.', 1;
    IF @TargetTenantUid IS NULL OR @TargetTenantUid = '00000000-0000-0000-0000-000000000000'
        THROW 51703, 'Trusted tenant identifier is required.', 1;
    IF @RequestedPatientUid IS NULL OR @RequestedPatientUid = '00000000-0000-0000-0000-000000000000'
        THROW 51704, 'Requested patient identifier is required.', 1;
    IF @AuthoritativePatientUid IS NULL OR @AuthoritativePatientUid = '00000000-0000-0000-0000-000000000000'
        THROW 51705, 'Authoritative patient identifier is required.', 1;
    IF @RequestedPatientUid = @AuthoritativePatientUid
        THROW 51706, 'Requested and authoritative patients must differ.', 1;
    IF @ResourceUid IS NULL OR @ResourceUid = '00000000-0000-0000-0000-000000000000'
        THROW 51707, 'Resolved resource identifier is required.', 1;
    IF @Capability IS NULL OR LEN(@Capability) > 100
        THROW 51708, 'Capability is invalid.', 1;
    IF @ResourceType IS NULL OR LEN(@ResourceType) > 50
        THROW 51709, 'Resource type is invalid.', 1;
    IF @SourceApplication IS NULL OR LEN(@SourceApplication) > 50
        THROW 51710, 'Source application is invalid.', 1;
    IF @RequestCorrelationId IS NOT NULL AND LEN(@RequestCorrelationId) > 128
        THROW 51711, 'Request correlation identifier is too long.', 1;

    SET @ActorSubject = LTRIM(RTRIM(@ActorSubject));
    SET @Capability = LTRIM(RTRIM(@Capability));
    SET @ResourceType = LTRIM(RTRIM(@ResourceType));
    SET @SourceApplication = LTRIM(RTRIM(@SourceApplication));
    SET @RequestCorrelationId = NULLIF(LTRIM(RTRIM(@RequestCorrelationId)), N'');

    IF @Capability <> N'EncounterView' OR @ResourceType <> N'Encounter'
        THROW 51712, 'Capability and resource type combination is not approved.', 1;
    IF @SourceApplication <> N'MicroEMR.Api'
        THROW 51713, 'Source application is not approved.', 1;

    INSERT dbo.PlatformSecurityAuditEvent
    (
        SecurityAuditEventUid, EventType, Outcome, DenialReason, ActorSubject,
        ClinicalUserId, TargetTenantUid, Capability, RequiredPermission,
        SourceApplication, RequestCorrelationId, OccurredAtUtc,
        RequestedPatientUid, AuthoritativePatientUid, ResourceType, ResourceUid
    )
    VALUES
    (
        NEWID(), N'SecurityAccessDenied', 'Denied', N'CrossPatientOwnership', @ActorSubject,
        @ClinicalUserId, @TargetTenantUid, N'EncounterView', N'Encounters.View',
        @SourceApplication, @RequestCorrelationId, SYSUTCDATETIME(),
        @RequestedPatientUid, @AuthoritativePatientUid, N'Encounter', @ResourceUid
    );
END;
GO
