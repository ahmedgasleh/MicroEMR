SET XACT_ABORT ON;
GO
CREATE OR ALTER PROCEDURE dbo.PatientFile_Archive
 @PatientUid UNIQUEIDENTIFIER,@FileUid UNIQUEIDENTIFIER,@ExpectedRowVersion BINARY(8),@Actor BIGINT
AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;BEGIN TRAN;
 DECLARE @Status NVARCHAR(20),@Version BINARY(8),@PatientId BIGINT;
 SELECT @Status=f.Status,@Version=f.RowVersion,@PatientId=p.PatientId FROM dbo.PatientFile f WITH(UPDLOCK,HOLDLOCK)
 JOIN dbo.Patient p ON p.PatientUid=f.PatientUid WHERE f.PatientUid=@PatientUid AND f.FileUid=@FileUid;
 IF @Status IS NULL BEGIN ROLLBACK;THROW 51710,'Patient file not found.',1;END;
 IF @Version<>@ExpectedRowVersion BEGIN ROLLBACK;THROW 51711,'Patient file concurrency conflict.',1;END;
 IF @Status<>N'Active' BEGIN ROLLBACK;THROW 51712,'Patient file transition is not allowed.',1;END;
 UPDATE dbo.PatientFile SET Status=N'Archived',UpdatedAt=SYSUTCDATETIME(),UpdatedBy=@Actor WHERE PatientUid=@PatientUid AND FileUid=@FileUid;
 INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt)
 VALUES(@Actor,@PatientId,N'Archive',N'PatientFile',CONVERT(NVARCHAR(100),@FileUid),N'Status=Active',N'Status=Archived',SYSUTCDATETIME());
 COMMIT;EXEC dbo.PatientFile_GetByUid @PatientUid,@FileUid;
END;
GO
CREATE OR ALTER PROCEDURE dbo.PatientFile_Restore
 @PatientUid UNIQUEIDENTIFIER,@FileUid UNIQUEIDENTIFIER,@ExpectedRowVersion BINARY(8),@Actor BIGINT
AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;BEGIN TRAN;
 DECLARE @Status NVARCHAR(20),@Version BINARY(8),@PatientId BIGINT;
 SELECT @Status=f.Status,@Version=f.RowVersion,@PatientId=p.PatientId FROM dbo.PatientFile f WITH(UPDLOCK,HOLDLOCK)
 JOIN dbo.Patient p ON p.PatientUid=f.PatientUid WHERE f.PatientUid=@PatientUid AND f.FileUid=@FileUid;
 IF @Status IS NULL BEGIN ROLLBACK;THROW 51710,'Patient file not found.',1;END;
 IF @Version<>@ExpectedRowVersion BEGIN ROLLBACK;THROW 51711,'Patient file concurrency conflict.',1;END;
 IF @Status<>N'Archived' BEGIN ROLLBACK;THROW 51712,'Patient file transition is not allowed.',1;END;
 UPDATE dbo.PatientFile SET Status=N'Active',UpdatedAt=SYSUTCDATETIME(),UpdatedBy=@Actor WHERE PatientUid=@PatientUid AND FileUid=@FileUid;
 INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt)
 VALUES(@Actor,@PatientId,N'Restore',N'PatientFile',CONVERT(NVARCHAR(100),@FileUid),N'Status=Archived',N'Status=Active',SYSUTCDATETIME());
 COMMIT;EXEC dbo.PatientFile_GetByUid @PatientUid,@FileUid;
END;
GO
