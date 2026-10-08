-- Extend existing clinical-selection persistence for uploaded external reports. No clinical file copies.
SET XACT_ABORT ON;
IF COL_LENGTH(N'dbo.PatientReferralClinicalSelection',N'FileUid') IS NULL
    ALTER TABLE dbo.PatientReferralClinicalSelection ADD FileUid UNIQUEIDENTIFIER NULL;
GO
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_PatientReferralClinicalSelection_File')
    ALTER TABLE dbo.PatientReferralClinicalSelection ADD CONSTRAINT FK_PatientReferralClinicalSelection_File
        FOREIGN KEY(FileUid) REFERENCES dbo.PatientFile(FileUid);
GO
ALTER TABLE dbo.PatientReferralClinicalSelection DROP CONSTRAINT CK_PatientReferralClinicalSelection_Reference;
ALTER TABLE dbo.PatientReferralClinicalSelection ADD CONSTRAINT CK_PatientReferralClinicalSelection_Reference CHECK
(
    (SelectionKind=N'CPP' AND CppCategoryCode IS NOT NULL AND CppCategoryCode IN(N'PROBLEMS',N'ALLERGIES',N'MEDICATIONS') AND EncounterUid IS NULL AND ResultUid IS NULL AND FileUid IS NULL)
    OR (SelectionKind=N'ENCOUNTER' AND CppCategoryCode IS NULL AND EncounterUid IS NOT NULL AND EncounterUid<>'00000000-0000-0000-0000-000000000000' AND ResultUid IS NULL AND FileUid IS NULL)
    OR (SelectionKind=N'RESULT' AND CppCategoryCode IS NULL AND EncounterUid IS NULL AND ResultUid IS NOT NULL AND ResultUid<>'00000000-0000-0000-0000-000000000000' AND FileUid IS NULL)
    OR (SelectionKind=N'FILE' AND CppCategoryCode IS NULL AND EncounterUid IS NULL AND ResultUid IS NULL AND FileUid IS NOT NULL AND FileUid<>'00000000-0000-0000-0000-000000000000')
);
GO
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.PatientReferralClinicalSelection') AND name=N'UX_PatientReferralClinicalSelection_File')
    CREATE UNIQUE INDEX UX_PatientReferralClinicalSelection_File ON dbo.PatientReferralClinicalSelection(ReferralUid,FileUid) WHERE IsDeleted=0 AND FileUid IS NOT NULL;
GO
CREATE OR ALTER PROCEDURE dbo.PatientReferralClinicalSelection_Get
    @PatientUid UNIQUEIDENTIFIER,@ReferralUid UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    -- One transaction gives the aggregate version and selection list a consistent view.
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;
    SELECT r.RowVersion FROM dbo.PatientReferral r WITH(HOLDLOCK)
    JOIN dbo.Patient p ON p.PatientUid=r.PatientUid AND p.IsDeleted=0
    WHERE r.PatientUid=@PatientUid AND r.ReferralUid=@ReferralUid;
    SELECT s.SelectionUid,s.SelectionKind,s.CppCategoryCode,s.EncounterUid,s.ResultUid,s.FileUid,s.CreatedAt,s.CreatedBy
    FROM dbo.PatientReferralClinicalSelection s
    JOIN dbo.PatientReferral r ON r.ReferralUid=s.ReferralUid AND r.PatientUid=@PatientUid
    JOIN dbo.Patient p ON p.PatientUid=r.PatientUid AND p.IsDeleted=0
    WHERE s.ReferralUid=@ReferralUid AND s.IsDeleted=0
    ORDER BY s.SelectionKind,s.CppCategoryCode,s.EncounterUid,s.ResultUid,s.FileUid;
    COMMIT;
END;
GO
CREATE OR ALTER PROCEDURE dbo.PatientReferralClinicalSelection_ReplaceDraft
    @PatientUid UNIQUEIDENTIFIER,@ReferralUid UNIQUEIDENTIFIER,@ExpectedRowVersion BINARY(8),
    @SelectionsJson NVARCHAR(MAX),@UpdatedBy BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF @SelectionsJson IS NULL OR ISJSON(@SelectionsJson)<>1 OR LEFT(LTRIM(@SelectionsJson),1)<>N'['
        THROW 51703,'Selections must be a JSON array.',1;
    IF (SELECT COUNT(*) FROM OPENJSON(@SelectionsJson))>500
        THROW 51703,'Too many selections.',1;
    IF EXISTS(SELECT 1 FROM OPENJSON(@SelectionsJson) WHERE [type]<>5)
        THROW 51703,'Each selection must be an object.',1;
    IF EXISTS(SELECT 1 FROM OPENJSON(@SelectionsJson) j CROSS APPLY OPENJSON(j.value) p
        WHERE p.[key] COLLATE Latin1_General_100_BIN2 NOT IN(N'SelectionKind',N'CppCategoryCode',N'EncounterUid',N'ResultUid',N'FileUid') OR p.[type] NOT IN(0,1))
        THROW 51703,'Only selection references are accepted.',1;
    IF EXISTS(SELECT 1 FROM OPENJSON(@SelectionsJson) j CROSS APPLY OPENJSON(j.value) p GROUP BY j.[key],p.[key] HAVING COUNT(*)>1)
        THROW 51703,'Duplicate selection properties are not allowed.',1;
    DECLARE @Desired TABLE(SelectionKind NVARCHAR(MAX) COLLATE Latin1_General_100_BIN2,CppCategoryCode NVARCHAR(MAX) COLLATE Latin1_General_100_BIN2,EncounterText NVARCHAR(MAX),ResultText NVARCHAR(MAX),FileText NVARCHAR(MAX));
    INSERT @Desired
    SELECT SelectionKind,CppCategoryCode,EncounterUid,ResultUid,FileUid FROM OPENJSON(@SelectionsJson)
    WITH(SelectionKind NVARCHAR(MAX),CppCategoryCode NVARCHAR(MAX),EncounterUid NVARCHAR(MAX),ResultUid NVARCHAR(MAX),FileUid NVARCHAR(MAX));
    IF EXISTS(SELECT 1 FROM @Desired WHERE
        SelectionKind IS NULL OR SelectionKind NOT IN(N'CPP',N'ENCOUNTER',N'RESULT',N'FILE') OR DATALENGTH(SelectionKind)<>DATALENGTH(RTRIM(SelectionKind))
        OR (CppCategoryCode IS NOT NULL AND DATALENGTH(CppCategoryCode)<>DATALENGTH(RTRIM(CppCategoryCode)))
        OR (SelectionKind=N'CPP' AND (FileText IS NOT NULL OR CppCategoryCode IS NULL OR CppCategoryCode NOT IN(N'PROBLEMS',N'ALLERGIES',N'MEDICATIONS') OR EncounterText IS NOT NULL OR ResultText IS NOT NULL))
        OR (SelectionKind=N'ENCOUNTER' AND (FileText IS NOT NULL OR CppCategoryCode IS NOT NULL OR ResultText IS NOT NULL OR EncounterText IS NULL OR DATALENGTH(EncounterText)<>72 OR TRY_CONVERT(UNIQUEIDENTIFIER,EncounterText) IS NULL OR TRY_CONVERT(UNIQUEIDENTIFIER,EncounterText)='00000000-0000-0000-0000-000000000000'))
        OR (SelectionKind=N'RESULT' AND (FileText IS NOT NULL OR CppCategoryCode IS NOT NULL OR EncounterText IS NOT NULL OR ResultText IS NULL OR DATALENGTH(ResultText)<>72 OR TRY_CONVERT(UNIQUEIDENTIFIER,ResultText) IS NULL OR TRY_CONVERT(UNIQUEIDENTIFIER,ResultText)='00000000-0000-0000-0000-000000000000'))
        OR (SelectionKind=N'FILE' AND (CppCategoryCode IS NOT NULL OR EncounterText IS NOT NULL OR ResultText IS NOT NULL OR FileText IS NULL OR DATALENGTH(FileText)<>72 OR TRY_CONVERT(UNIQUEIDENTIFIER,FileText) IS NULL OR TRY_CONVERT(UNIQUEIDENTIFIER,FileText)='00000000-0000-0000-0000-000000000000')))
        THROW 51703,'Invalid selection kind or reference.',1;
    IF EXISTS(SELECT 1 FROM @Desired GROUP BY SelectionKind,CppCategoryCode,TRY_CONVERT(UNIQUEIDENTIFIER,EncounterText),TRY_CONVERT(UNIQUEIDENTIFIER,ResultText),TRY_CONVERT(UNIQUEIDENTIFIER,FileText) HAVING COUNT(*)>1)
        THROW 51703,'Duplicate selections are not allowed.',1;
    BEGIN TRY
        BEGIN TRANSACTION;
        DECLARE @Status NVARCHAR(30),@Version BINARY(8),@PatientId BIGINT,@ChangedAt DATETIME2(0)=SYSUTCDATETIME();
        SELECT @Status=r.Status,@Version=r.RowVersion,@PatientId=p.PatientId
        FROM dbo.PatientReferral r WITH(UPDLOCK,HOLDLOCK)
        JOIN dbo.Patient p ON p.PatientUid=r.PatientUid AND p.IsDeleted=0
        WHERE r.PatientUid=@PatientUid AND r.ReferralUid=@ReferralUid;
        IF @Status IS NULL THROW 51700,'Referral not found.',1;
        IF @Status<>N'Draft' THROW 51701,'Clinical selections can only change while Draft.',1;
        IF @ExpectedRowVersion IS NULL OR @Version<>@ExpectedRowVersion THROW 51702,'Referral changed. Refresh and try again.',1;
        IF NOT EXISTS(SELECT 1 FROM dbo.ApplicationUser WHERE UserId=@UpdatedBy AND IsActive=1)
            THROW 51705,'Active clinical user not found.',1;
        IF EXISTS(SELECT 1 FROM @Desired d WHERE d.SelectionKind=N'ENCOUNTER' AND NOT EXISTS
            (SELECT 1 FROM dbo.PatientEncounter e WITH(HOLDLOCK) WHERE e.EncounterUid=TRY_CONVERT(UNIQUEIDENTIFIER,d.EncounterText) AND e.PatientUid=@PatientUid))
            THROW 51704,'Encounter not found for this patient.',1;
        IF EXISTS(SELECT 1 FROM @Desired d WHERE d.SelectionKind=N'RESULT' AND NOT EXISTS
            (SELECT 1 FROM dbo.PatientResult r WITH(HOLDLOCK) WHERE r.PatientResultUid=TRY_CONVERT(UNIQUEIDENTIFIER,d.ResultText) AND r.PatientUid=@PatientUid AND r.LifecycleStatus=N'Current'))
            THROW 51704,'Current result not found for this patient.',1;
        IF EXISTS(SELECT 1 FROM @Desired d WHERE d.SelectionKind=N'FILE' AND NOT EXISTS
            (SELECT 1 FROM dbo.PatientFile f WITH(HOLDLOCK) WHERE f.FileUid=TRY_CONVERT(UNIQUEIDENTIFIER,d.FileText) AND f.PatientUid=@PatientUid AND f.Status=N'Active'))
            THROW 51704,'Active report file not found for this patient.',1;
        DECLARE @Before NVARCHAR(MAX)=(SELECT SelectionKind,CppCategoryCode,EncounterUid,ResultUid,FileUid FROM dbo.PatientReferralClinicalSelection WHERE ReferralUid=@ReferralUid AND IsDeleted=0 ORDER BY SelectionKind,CppCategoryCode,EncounterUid,ResultUid,FileUid FOR JSON PATH);
        -- Retain unchanged rows/UIDs; end removed rows and append newly selected references.
        UPDATE s SET IsDeleted=1,UpdatedBy=@UpdatedBy,UpdatedAt=@ChangedAt
        FROM dbo.PatientReferralClinicalSelection s WHERE s.ReferralUid=@ReferralUid AND s.IsDeleted=0 AND NOT EXISTS
        (SELECT 1 FROM @Desired d WHERE d.SelectionKind=s.SelectionKind AND
            ((d.SelectionKind=N'CPP' AND d.CppCategoryCode=s.CppCategoryCode)
            OR (d.SelectionKind=N'ENCOUNTER' AND TRY_CONVERT(UNIQUEIDENTIFIER,d.EncounterText)=s.EncounterUid)
            OR (d.SelectionKind=N'RESULT' AND TRY_CONVERT(UNIQUEIDENTIFIER,d.ResultText)=s.ResultUid)
            OR (d.SelectionKind=N'FILE' AND TRY_CONVERT(UNIQUEIDENTIFIER,d.FileText)=s.FileUid)));
        INSERT dbo.PatientReferralClinicalSelection(ReferralUid,SelectionKind,CppCategoryCode,EncounterUid,ResultUid,FileUid,CreatedBy,CreatedAt)
        SELECT @ReferralUid,d.SelectionKind,d.CppCategoryCode,TRY_CONVERT(UNIQUEIDENTIFIER,d.EncounterText),TRY_CONVERT(UNIQUEIDENTIFIER,d.ResultText),TRY_CONVERT(UNIQUEIDENTIFIER,d.FileText),@UpdatedBy,@ChangedAt
        FROM @Desired d WHERE NOT EXISTS(SELECT 1 FROM dbo.PatientReferralClinicalSelection s WHERE s.ReferralUid=@ReferralUid AND s.IsDeleted=0 AND d.SelectionKind=s.SelectionKind AND
            ((d.SelectionKind=N'CPP' AND d.CppCategoryCode=s.CppCategoryCode)
            OR (d.SelectionKind=N'ENCOUNTER' AND TRY_CONVERT(UNIQUEIDENTIFIER,d.EncounterText)=s.EncounterUid)
            OR (d.SelectionKind=N'RESULT' AND TRY_CONVERT(UNIQUEIDENTIFIER,d.ResultText)=s.ResultUid)
            OR (d.SelectionKind=N'FILE' AND TRY_CONVERT(UNIQUEIDENTIFIER,d.FileText)=s.FileUid)));
        UPDATE dbo.PatientReferral SET UpdatedBy=@UpdatedBy,UpdatedAt=@ChangedAt WHERE ReferralUid=@ReferralUid AND PatientUid=@PatientUid;
        DECLARE @After NVARCHAR(MAX)=(SELECT SelectionKind,CppCategoryCode,EncounterUid,ResultUid,FileUid FROM dbo.PatientReferralClinicalSelection WHERE ReferralUid=@ReferralUid AND IsDeleted=0 ORDER BY SelectionKind,CppCategoryCode,EncounterUid,ResultUid,FileUid FOR JSON PATH);
        INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt)
        VALUES(@UpdatedBy,@PatientId,N'ReferralClinicalSelectionsReplaced',N'PatientReferral',CONVERT(NVARCHAR(100),@ReferralUid),COALESCE(@Before,N'[]'),COALESCE(@After,N'[]'),@ChangedAt);
        -- Return under the aggregate lock, before another mutation can advance the version.
        EXEC dbo.PatientReferralClinicalSelection_Get @PatientUid,@ReferralUid;
        COMMIT;
    END TRY
    BEGIN CATCH
        IF XACT_STATE()<>0 ROLLBACK;
        THROW;
    END CATCH;
END;
GO
