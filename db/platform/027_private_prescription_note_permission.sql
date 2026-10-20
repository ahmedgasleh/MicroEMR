-- Step 121: opt-in private-note permission. No automatic grants; apply after 026.
SET XACT_ABORT ON; SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON;
GO
ALTER TABLE dbo.UserPermissionOverride DROP CONSTRAINT CK_UserPermissionOverride_Key;
GO
ALTER TABLE dbo.UserPermissionOverride WITH CHECK ADD CONSTRAINT CK_UserPermissionOverride_Key CHECK (PermissionKey IN
(N'Patients.View',N'Patients.Edit',N'Scheduling.View',N'Scheduling.Manage',N'Encounters.View',N'Encounters.Edit',N'Encounters.Sign',N'Documents.View',N'Documents.Manage',N'Documents.Sign',N'Templates.Use',N'Templates.Manage',N'ClinicalData.Manage',N'Prescriptions.Prescribe',N'Prescriptions.ViewPrivateNotes',N'Referrals.View',N'Referrals.Manage',N'Results.View',N'Results.Review',N'Tasks.View',N'Tasks.Manage',N'Reports.View',N'Reports.Export',N'ClinicSettings.Manage',N'Providers.View',N'Providers.Manage',N'Users.View',N'Users.Manage',N'Users.ManageAccess'));
GO
ALTER TABLE dbo.UserPermissionOverride CHECK CONSTRAINT CK_UserPermissionOverride_Key;
GO
CREATE OR ALTER PROCEDURE dbo.AccessProfile_ReplacePermissions @TenantUid UNIQUEIDENTIFIER,@AccessProfileUid UNIQUEIDENTIFIER,@PermissionKeys NVARCHAR(MAX),@ExpectedRowVersion BINARY(8),@ActorUserId NVARCHAR(450) AS
BEGIN SET NOCOUNT ON;SET XACT_ABORT ON;BEGIN TRANSACTION;DECLARE @LockResult INT,@Current BINARY(8),@LockResource NVARCHAR(255)=CONCAT(N'MicroEMR:AccessAdmin:',@TenantUid);EXEC @LockResult=sys.sp_getapplock @Resource=@LockResource,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;IF @LockResult<0 THROW 51601,'Access administration is busy. Try again.',1;SELECT @Current=RowVersion FROM dbo.AccessProfile WITH(UPDLOCK,HOLDLOCK) WHERE TenantUid=@TenantUid AND AccessProfileUid=@AccessProfileUid AND IsActive=1;IF @Current IS NULL THROW 51401,'Profile not found.',1;IF @Current<>@ExpectedRowVersion THROW 51402,'Profile changed.',1;DECLARE @Submitted TABLE(PermissionKey NVARCHAR(100) PRIMARY KEY);INSERT @Submitted SELECT DISTINCT LTRIM(RTRIM(value)) FROM STRING_SPLIT(@PermissionKeys,N',') WHERE LTRIM(RTRIM(value))<>N'';IF NOT EXISTS(SELECT 1 FROM @Submitted)THROW 51403,'At least one permission is required.',1;IF EXISTS(SELECT 1 FROM @Submitted WHERE PermissionKey NOT IN(N'Patients.View',N'Patients.Edit',N'Scheduling.View',N'Scheduling.Manage',N'Encounters.View',N'Encounters.Edit',N'Encounters.Sign',N'Documents.View',N'Documents.Manage',N'Documents.Sign',N'Templates.Use',N'Templates.Manage',N'ClinicalData.Manage',N'Prescriptions.Prescribe',N'Prescriptions.ViewPrivateNotes',N'Referrals.View',N'Referrals.Manage',N'Results.View',N'Results.Review',N'Tasks.View',N'Tasks.Manage',N'Reports.View',N'Reports.Export',N'ClinicSettings.Manage',N'Providers.View',N'Providers.Manage',N'Users.View',N'Users.Manage',N'Users.ManageAccess'))THROW 51403,'Unknown permission.',1;DECLARE @Old NVARCHAR(MAX)=(SELECT PermissionKey FROM dbo.AccessProfilePermission WHERE AccessProfileUid=@AccessProfileUid ORDER BY PermissionKey FOR JSON PATH);DELETE dbo.AccessProfilePermission WHERE AccessProfileUid=@AccessProfileUid;INSERT dbo.AccessProfilePermission SELECT @AccessProfileUid,PermissionKey FROM @Submitted;UPDATE dbo.AccessProfile SET UpdatedBy=@ActorUserId,UpdatedAt=SYSUTCDATETIME() WHERE AccessProfileUid=@AccessProfileUid;IF NOT EXISTS(SELECT 1 FROM dbo.AccessManagementAdministrator(@TenantUid))THROW 51602,'This profile change would remove the final access administrator.',1;DECLARE @New NVARCHAR(MAX)=(SELECT PermissionKey FROM dbo.AccessProfilePermission WHERE AccessProfileUid=@AccessProfileUid ORDER BY PermissionKey FOR JSON PATH);INSERT dbo.PlatformAuditEvent VALUES(NEWID(),@ActorUserId,N'TenantAdmin',N'AccessProfilePermissionsChanged',@TenantUid,NULL,N'Succeeded',SYSUTCDATETIME(),NEWID(),CONCAT(N'{"profileUid":"',@AccessProfileUid,N'","old":',@Old,N',"new":',@New,N'}'));COMMIT;END;
GO
CREATE OR ALTER PROCEDURE dbo.UserPermissionOverride_Set @TenantUid UNIQUEIDENTIFIER,@UserId NVARCHAR(450),@PermissionKey NVARCHAR(100),@OverrideState VARCHAR(7),@ExpectedMembershipRowVersion BINARY(8),@ActorUserId NVARCHAR(450) AS
BEGIN SET NOCOUNT ON;SET XACT_ABORT ON;BEGIN TRANSACTION;DECLARE @LockResult INT,@Current BINARY(8),@Old VARCHAR(7),@LockResource NVARCHAR(255)=CONCAT(N'MicroEMR:AccessAdmin:',@TenantUid);EXEC @LockResult=sys.sp_getapplock @Resource=@LockResource,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;IF @LockResult<0 THROW 51601,'Access administration is busy. Try again.',1;SELECT @Current=RowVersion FROM dbo.UserTenantMembership WITH(UPDLOCK,HOLDLOCK) WHERE TenantUid=@TenantUid AND UserId=@UserId;IF @Current IS NULL THROW 51501,'Membership not found.',1;IF @Current<>@ExpectedMembershipRowVersion THROW 51502,'Access configuration changed.',1;IF @OverrideState NOT IN('Inherit','Allow','Deny')THROW 51503,'Invalid override state.',1;IF @PermissionKey NOT IN(N'Patients.View',N'Patients.Edit',N'Scheduling.View',N'Scheduling.Manage',N'Encounters.View',N'Encounters.Edit',N'Encounters.Sign',N'Documents.View',N'Documents.Manage',N'Documents.Sign',N'Templates.Use',N'Templates.Manage',N'ClinicalData.Manage',N'Prescriptions.Prescribe',N'Prescriptions.ViewPrivateNotes',N'Referrals.View',N'Referrals.Manage',N'Results.View',N'Results.Review',N'Tasks.View',N'Tasks.Manage',N'Reports.View',N'Reports.Export',N'ClinicSettings.Manage',N'Providers.View',N'Providers.Manage',N'Users.View',N'Users.Manage',N'Users.ManageAccess')THROW 51503,'Unknown permission.',1;SELECT @Old=OverrideState FROM dbo.UserPermissionOverride WHERE TenantUid=@TenantUid AND UserId=@UserId AND PermissionKey=@PermissionKey;IF @OverrideState='Inherit'DELETE dbo.UserPermissionOverride WHERE TenantUid=@TenantUid AND UserId=@UserId AND PermissionKey=@PermissionKey;ELSE MERGE dbo.UserPermissionOverride AS t USING(SELECT @TenantUid TenantUid,@UserId UserId,@PermissionKey PermissionKey)s ON t.TenantUid=s.TenantUid AND t.UserId=s.UserId AND t.PermissionKey=s.PermissionKey WHEN MATCHED THEN UPDATE SET OverrideState=@OverrideState,UpdatedBy=@ActorUserId,UpdatedAt=SYSUTCDATETIME() WHEN NOT MATCHED THEN INSERT(TenantUid,UserId,PermissionKey,OverrideState,CreatedBy,UpdatedBy)VALUES(@TenantUid,@UserId,@PermissionKey,@OverrideState,@ActorUserId,@ActorUserId);UPDATE dbo.UserTenantMembership SET UpdatedAt=SYSUTCDATETIME() WHERE TenantUid=@TenantUid AND UserId=@UserId;IF NOT EXISTS(SELECT 1 FROM dbo.AccessManagementAdministrator(@TenantUid))THROW 51602,'This override would remove the final access administrator.',1;INSERT dbo.PlatformAuditEvent VALUES(NEWID(),@ActorUserId,N'TenantAdmin',N'UserPermissionOverrideChanged',@TenantUid,@UserId,N'Succeeded',SYSUTCDATETIME(),NEWID(),CONCAT(N'{"permission":"',STRING_ESCAPE(@PermissionKey,'json'),N'","old":"',COALESCE(@Old,'Inherit'),N'","new":"',@OverrideState,N'"}'));COMMIT;END;
GO

-- Add only private-note capability pairs; preserve tenant-selection and ownership shapes.
ALTER TABLE dbo.PlatformSecurityAuditEvent DROP CONSTRAINT CK_PlatformSecurityAuditEvent_CapabilityPermission;
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
                    OR (Capability = N'PrescriptionPrivateNoteView' AND RequiredPermission = N'Prescriptions.ViewPrivateNotes')
                    OR (Capability = N'PrescriptionPrivateNoteWrite' AND RequiredPermission = N'Prescriptions.Prescribe')
                )
            )
            OR
            (Capability = N'TenantSelection' AND RequiredPermission IS NULL)
        );
GO
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
        OR (@Capability = N'PrescriptionPrivateNoteView' AND @RequiredPermission = N'Prescriptions.ViewPrivateNotes')
        OR (@Capability = N'PrescriptionPrivateNoteWrite' AND @RequiredPermission = N'Prescriptions.Prescribe')
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
