-- Chart Alert safeguards only. Retain existing rows, lifecycle and historical audit.
SET XACT_ABORT ON;
GO
CREATE OR ALTER PROCEDURE dbo.PatientChartAlert_GetByUid
 @PatientUid UNIQUEIDENTIFIER,@PatientChartAlertUid UNIQUEIDENTIFIER AS
BEGIN
 SET NOCOUNT ON;
 SELECT a.*,cu.DisplayName CreatedByDisplayName,uu.DisplayName UpdatedByDisplayName,ru.DisplayName ResolvedByDisplayName
 FROM dbo.PatientChartAlert a JOIN dbo.Patient p ON p.PatientUid=a.PatientUid AND p.IsDeleted=0
 LEFT JOIN dbo.ApplicationUser cu ON cu.UserId=a.CreatedBy LEFT JOIN dbo.ApplicationUser uu ON uu.UserId=a.UpdatedBy LEFT JOIN dbo.ApplicationUser ru ON ru.UserId=a.ResolvedBy
 WHERE a.PatientUid=@PatientUid AND a.PatientChartAlertUid=@PatientChartAlertUid;
END;
GO
CREATE OR ALTER PROCEDURE dbo.PatientChartAlert_GetByPatientUid
 @PatientUid UNIQUEIDENTIFIER,@StatusFilter NVARCHAR(50)=N'Active' AS
BEGIN
 SET NOCOUNT ON;
 IF NOT EXISTS(SELECT 1 FROM dbo.Patient WHERE PatientUid=@PatientUid AND IsDeleted=0) THROW 52601,'Patient not found.',1;
 IF @StatusFilter NOT IN(N'Active',N'Resolved',N'All') OR @StatusFilter IS NULL SET @StatusFilter=N'Active';
 SELECT a.*,cu.DisplayName CreatedByDisplayName,uu.DisplayName UpdatedByDisplayName,ru.DisplayName ResolvedByDisplayName
 FROM dbo.PatientChartAlert a JOIN dbo.Patient p ON p.PatientUid=a.PatientUid AND p.IsDeleted=0
 LEFT JOIN dbo.ApplicationUser cu ON cu.UserId=a.CreatedBy LEFT JOIN dbo.ApplicationUser uu ON uu.UserId=a.UpdatedBy LEFT JOIN dbo.ApplicationUser ru ON ru.UserId=a.ResolvedBy
 WHERE a.PatientUid=@PatientUid AND (@StatusFilter=N'All' OR a.AlertStatus=@StatusFilter)
 ORDER BY CASE a.AlertPriority WHEN N'Critical' THEN 1 WHEN N'High' THEN 2 WHEN N'Normal' THEN 3 ELSE 4 END,a.CreatedAt DESC,a.PatientChartAlertUid;
END;
GO
CREATE OR ALTER PROCEDURE dbo.PatientChartAlert_Create
 @PatientUid UNIQUEIDENTIFIER,
 @AlertTitle NVARCHAR(200),@AlertMessage NVARCHAR(1000)=NULL,@AlertType NVARCHAR(50)=N'General',@AlertPriority NVARCHAR(50)=N'Normal',
 @CreatedBy BIGINT=NULL,@RequestCorrelationId NVARCHAR(100)=NULL AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @PatientId BIGINT=(SELECT PatientId FROM dbo.Patient WITH(UPDLOCK,HOLDLOCK) WHERE PatientUid=@PatientUid AND IsDeleted=0);
 IF @PatientId IS NULL THROW 52601,'Patient not found.',1;
 IF @CreatedBy IS NULL OR NOT EXISTS(SELECT 1 FROM dbo.ApplicationUser WHERE UserId=@CreatedBy AND IsActive=1) THROW 52603,'An active clinical actor is required.',1;
 DECLARE @TenantUid UNIQUEIDENTIFIER=(SELECT TenantUid FROM dbo.TenantDatabaseIdentity);
 IF @TenantUid IS NULL THROW 52603,'Trusted tenant identity is required.',1;
 SET @RequestCorrelationId=COALESCE(NULLIF(LTRIM(RTRIM(@RequestCorrelationId)),N''),CONVERT(NVARCHAR(100),NEWID()));
 DECLARE @OldValue NVARCHAR(MAX)=NULL,@NewValue NVARCHAR(MAX);
 DECLARE @PatientChartAlertUid UNIQUEIDENTIFIER=NEWID();
 IF NULLIF(LTRIM(RTRIM(@AlertTitle)),N'') IS NULL THROW 52600,'Alert title is required.',1;
 IF @AlertType IS NULL OR @AlertType NOT IN(N'General',N'Clinical',N'Administrative',N'Safety',N'Scheduling') SET @AlertType=N'General';
 IF @AlertPriority IS NULL OR @AlertPriority NOT IN(N'Low',N'Normal',N'High',N'Critical') SET @AlertPriority=N'Normal';
 INSERT dbo.PatientChartAlert(PatientChartAlertUid,PatientUid,AlertTitle,AlertMessage,AlertType,AlertPriority,CreatedBy)
 VALUES(@PatientChartAlertUid,@PatientUid,LTRIM(RTRIM(@AlertTitle)),NULLIF(LTRIM(RTRIM(@AlertMessage)),N''),@AlertType,@AlertPriority,@CreatedBy);
 SET @NewValue=(SELECT @TenantUid TenantUid,@RequestCorrelationId RequestCorrelationId,a.* FROM dbo.PatientChartAlert a WHERE a.PatientUid=@PatientUid AND a.PatientChartAlertUid=@PatientChartAlertUid FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
 INSERT dbo.AuditLog(UserId,PatientId,PatientUid,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt,
 AuditEventUid,EventCategory,ResourceType,ResourceUid,Outcome,RequestCorrelationId,SourceApplication)
 VALUES(@CreatedBy,@PatientId,@PatientUid,N'ChartAlertCreated',N'PatientChartAlert',CONVERT(NVARCHAR(100),@PatientChartAlertUid),@OldValue,@NewValue,SYSUTCDATETIME(),
 NEWID(),N'ClinicalMutation',N'PatientChartAlert',@PatientChartAlertUid,N'Succeeded',@RequestCorrelationId,N'MicroEMR.Api');
 COMMIT;
 EXEC dbo.PatientChartAlert_GetByUid @PatientUid,@PatientChartAlertUid;
 END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK; THROW; END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.PatientChartAlert_Update
 @PatientUid UNIQUEIDENTIFIER,@PatientChartAlertUid UNIQUEIDENTIFIER,
 @AlertTitle NVARCHAR(200),@AlertMessage NVARCHAR(1000)=NULL,@AlertType NVARCHAR(50)=N'General',@AlertPriority NVARCHAR(50)=N'Normal',
 @UpdatedBy BIGINT=NULL,@ExpectedRowVersion VARBINARY(MAX)=NULL,@RequestCorrelationId NVARCHAR(100)=NULL AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @PatientId BIGINT=(SELECT PatientId FROM dbo.Patient WITH(UPDLOCK,HOLDLOCK) WHERE PatientUid=@PatientUid AND IsDeleted=0);
 IF @PatientId IS NULL THROW 52601,'Patient not found.',1;
 IF @UpdatedBy IS NULL OR NOT EXISTS(SELECT 1 FROM dbo.ApplicationUser WHERE UserId=@UpdatedBy AND IsActive=1) THROW 52603,'An active clinical actor is required.',1;
 DECLARE @TenantUid UNIQUEIDENTIFIER=(SELECT TenantUid FROM dbo.TenantDatabaseIdentity);
 IF @TenantUid IS NULL THROW 52603,'Trusted tenant identity is required.',1;
 SET @RequestCorrelationId=COALESCE(NULLIF(LTRIM(RTRIM(@RequestCorrelationId)),N''),CONVERT(NVARCHAR(100),NEWID()));
 DECLARE @OldValue NVARCHAR(MAX)=NULL,@NewValue NVARCHAR(MAX);
 IF @ExpectedRowVersion IS NULL OR DATALENGTH(@ExpectedRowVersion)<>8 THROW 52600,'An exact eight-byte reviewed RowVersion is required.',1;
 DECLARE @CurrentVersion BINARY(8),@Status NVARCHAR(50);
 SELECT @CurrentVersion=RowVersion,@Status=AlertStatus FROM dbo.PatientChartAlert WITH(UPDLOCK,HOLDLOCK)
 WHERE PatientUid=@PatientUid AND PatientChartAlertUid=@PatientChartAlertUid;
 IF @CurrentVersion IS NULL THROW 52601,'Alert not found.',1;
 IF @CurrentVersion<>@ExpectedRowVersion THROW 52609,'The alert changed. Reload and review before saving.',1;
 IF @Status=N'Resolved' THROW 52609,'Resolved alerts cannot be edited.',1;
 SET @OldValue=(SELECT @TenantUid TenantUid,@RequestCorrelationId RequestCorrelationId,a.* FROM dbo.PatientChartAlert a WHERE a.PatientUid=@PatientUid AND a.PatientChartAlertUid=@PatientChartAlertUid FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
 IF NULLIF(LTRIM(RTRIM(@AlertTitle)),N'') IS NULL THROW 52600,'Alert title is required.',1;
 IF @AlertType IS NULL OR @AlertType NOT IN(N'General',N'Clinical',N'Administrative',N'Safety',N'Scheduling') SET @AlertType=N'General';
 IF @AlertPriority IS NULL OR @AlertPriority NOT IN(N'Low',N'Normal',N'High',N'Critical') SET @AlertPriority=N'Normal';
 UPDATE dbo.PatientChartAlert SET AlertTitle=LTRIM(RTRIM(@AlertTitle)),AlertMessage=NULLIF(LTRIM(RTRIM(@AlertMessage)),N''),AlertType=@AlertType,AlertPriority=@AlertPriority,UpdatedAt=SYSUTCDATETIME(),UpdatedBy=@UpdatedBy
 WHERE PatientUid=@PatientUid AND PatientChartAlertUid=@PatientChartAlertUid AND RowVersion=@ExpectedRowVersion AND AlertStatus=N'Active';
 IF @@ROWCOUNT<>1 THROW 52609,'The alert changed. Reload and review.',1;
 SET @NewValue=(SELECT @TenantUid TenantUid,@RequestCorrelationId RequestCorrelationId,a.* FROM dbo.PatientChartAlert a WHERE a.PatientUid=@PatientUid AND a.PatientChartAlertUid=@PatientChartAlertUid FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
 INSERT dbo.AuditLog(UserId,PatientId,PatientUid,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt,
 AuditEventUid,EventCategory,ResourceType,ResourceUid,Outcome,RequestCorrelationId,SourceApplication)
 VALUES(@UpdatedBy,@PatientId,@PatientUid,N'ChartAlertUpdated',N'PatientChartAlert',CONVERT(NVARCHAR(100),@PatientChartAlertUid),@OldValue,@NewValue,SYSUTCDATETIME(),
 NEWID(),N'ClinicalMutation',N'PatientChartAlert',@PatientChartAlertUid,N'Succeeded',@RequestCorrelationId,N'MicroEMR.Api');
 COMMIT;
 EXEC dbo.PatientChartAlert_GetByUid @PatientUid,@PatientChartAlertUid;
 END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK; THROW; END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.PatientChartAlert_Resolve
 @PatientUid UNIQUEIDENTIFIER,@PatientChartAlertUid UNIQUEIDENTIFIER,@ResolutionReason NVARCHAR(500)=NULL,
 @ResolvedBy BIGINT=NULL,@ExpectedRowVersion VARBINARY(MAX)=NULL,@RequestCorrelationId NVARCHAR(100)=NULL AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @PatientId BIGINT=(SELECT PatientId FROM dbo.Patient WITH(UPDLOCK,HOLDLOCK) WHERE PatientUid=@PatientUid AND IsDeleted=0);
 IF @PatientId IS NULL THROW 52601,'Patient not found.',1;
 IF @ResolvedBy IS NULL OR NOT EXISTS(SELECT 1 FROM dbo.ApplicationUser WHERE UserId=@ResolvedBy AND IsActive=1) THROW 52603,'An active clinical actor is required.',1;
 DECLARE @TenantUid UNIQUEIDENTIFIER=(SELECT TenantUid FROM dbo.TenantDatabaseIdentity);
 IF @TenantUid IS NULL THROW 52603,'Trusted tenant identity is required.',1;
 SET @RequestCorrelationId=COALESCE(NULLIF(LTRIM(RTRIM(@RequestCorrelationId)),N''),CONVERT(NVARCHAR(100),NEWID()));
 DECLARE @OldValue NVARCHAR(MAX)=NULL,@NewValue NVARCHAR(MAX);
 IF @ExpectedRowVersion IS NULL OR DATALENGTH(@ExpectedRowVersion)<>8 THROW 52600,'An exact eight-byte reviewed RowVersion is required.',1;
 DECLARE @CurrentVersion BINARY(8),@Status NVARCHAR(50);
 SELECT @CurrentVersion=RowVersion,@Status=AlertStatus FROM dbo.PatientChartAlert WITH(UPDLOCK,HOLDLOCK)
 WHERE PatientUid=@PatientUid AND PatientChartAlertUid=@PatientChartAlertUid;
 IF @CurrentVersion IS NULL THROW 52601,'Alert not found.',1;
 IF @CurrentVersion<>@ExpectedRowVersion THROW 52609,'The alert changed. Reload and review before saving.',1;
 IF @Status=N'Resolved' BEGIN COMMIT; EXEC dbo.PatientChartAlert_GetByUid @PatientUid,@PatientChartAlertUid; RETURN; END;
 SET @OldValue=(SELECT @TenantUid TenantUid,@RequestCorrelationId RequestCorrelationId,a.* FROM dbo.PatientChartAlert a WHERE a.PatientUid=@PatientUid AND a.PatientChartAlertUid=@PatientChartAlertUid FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
 UPDATE dbo.PatientChartAlert SET AlertStatus=N'Resolved',ResolvedAt=COALESCE(ResolvedAt,SYSUTCDATETIME()),ResolvedBy=COALESCE(ResolvedBy,@ResolvedBy),ResolutionReason=COALESCE(ResolutionReason,NULLIF(LTRIM(RTRIM(@ResolutionReason)),N'')),UpdatedAt=SYSUTCDATETIME(),UpdatedBy=@ResolvedBy
 WHERE PatientUid=@PatientUid AND PatientChartAlertUid=@PatientChartAlertUid AND RowVersion=@ExpectedRowVersion AND AlertStatus=N'Active';
 IF @@ROWCOUNT<>1 THROW 52609,'The alert changed. Reload and review.',1;
 SET @NewValue=(SELECT @TenantUid TenantUid,@RequestCorrelationId RequestCorrelationId,a.* FROM dbo.PatientChartAlert a WHERE a.PatientUid=@PatientUid AND a.PatientChartAlertUid=@PatientChartAlertUid FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
 INSERT dbo.AuditLog(UserId,PatientId,PatientUid,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt,
 AuditEventUid,EventCategory,ResourceType,ResourceUid,Outcome,RequestCorrelationId,SourceApplication)
 VALUES(@ResolvedBy,@PatientId,@PatientUid,N'ChartAlertResolved',N'PatientChartAlert',CONVERT(NVARCHAR(100),@PatientChartAlertUid),@OldValue,@NewValue,SYSUTCDATETIME(),
 NEWID(),N'ClinicalMutation',N'PatientChartAlert',@PatientChartAlertUid,N'Succeeded',@RequestCorrelationId,N'MicroEMR.Api');
 COMMIT;
 EXEC dbo.PatientChartAlert_GetByUid @PatientUid,@PatientChartAlertUid;
 END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK; THROW; END CATCH;
END;
GO
