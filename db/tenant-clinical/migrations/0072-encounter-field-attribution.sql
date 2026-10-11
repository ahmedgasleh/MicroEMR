-- Step 79B: preserve baseline narrative; field-linked provenance, no backfill.
SET XACT_ABORT ON;
GO
IF COL_LENGTH('dbo.PatientEncounterContribution','FieldKey') IS NULL
    ALTER TABLE dbo.PatientEncounterContribution ADD FieldKey nvarchar(100) COLLATE Latin1_General_100_BIN2 NULL,
        FieldLabel nvarchar(200) NULL,
        ValueKind nvarchar(20) NOT NULL CONSTRAINT DF_EncounterContribution_Kind DEFAULT N'Narrative',
        PriorValueJson nvarchar(max) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.PatientEncounterContribution') AND name='UX_EncounterContribution_FieldValue')
    CREATE UNIQUE INDEX UX_EncounterContribution_FieldValue ON dbo.PatientEncounterContribution(EncounterUid,FieldKey)
        WHERE ValueKind=N'Value';
GO
CREATE OR ALTER PROCEDURE dbo.PatientEncounterContribution_Get
    @PatientUid UNIQUEIDENTIFIER, @EncounterUid UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT c.ContributionUid, c.SectionKey, c.SectionTitle, c.OriginalContributorId,
        c.OriginalContributorDisplayName, c.CreatedAtUtc,
        r.RevisionNumber, r.Content, r.EditorId, r.EditorDisplayName, r.RevisedAtUtc, c.FieldKey, c.ValueKind, c.PriorValueJson, c.FieldLabel
    FROM dbo.PatientEncounterContribution c
    INNER JOIN dbo.PatientEncounter e ON e.EncounterUid=c.EncounterUid AND e.PatientUid=c.PatientUid
    INNER JOIN dbo.Patient p ON p.PatientUid=e.PatientUid AND p.IsDeleted=0
    INNER JOIN dbo.PatientEncounterContributionRevision r ON r.ContributionUid=c.ContributionUid
    WHERE c.PatientUid=@PatientUid AND c.EncounterUid=@EncounterUid
    ORDER BY c.ContributionId, r.RevisionNumber;
END;
GO
CREATE OR ALTER PROCEDURE dbo.PatientEncounterContribution_Save
    @PatientUid UNIQUEIDENTIFIER, @EncounterUid UNIQUEIDENTIFIER,
    @SectionKey NVARCHAR(100), @Content NVARCHAR(MAX), @ExpectedRowVersion BINARY(8),
    @ActorId BIGINT, @ContributionUid UNIQUEIDENTIFIER = NULL, @FieldKey NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF OBJECT_ID(N'dbo.AuditLog', N'U') IS NULL THROW 51171, 'Clinical audit storage is required.', 1;
    IF NULLIF(LTRIM(RTRIM(@Content)),N'') IS NULL OR DATALENGTH(@Content)>64000
        THROW 51171, 'Contribution content is required and must not exceed 32000 characters.', 1;
    BEGIN TRY
        BEGIN TRANSACTION;
        DECLARE @Status NVARCHAR(30), @Version BINARY(8), @TemplateVersionUid UNIQUEIDENTIFIER,
            @PatientId BIGINT, @SectionTitle NVARCHAR(200), @ActorName NVARCHAR(200),
            @Revision INT=1, @Now DATETIME2(7)=SYSUTCDATETIME(), @OldContent NVARCHAR(MAX),
            @Action NVARCHAR(30)=N'ContributionCreated';
        -- Same parent lock as note saves/signing; every block write advances the parent RowVersion.
        SELECT @Status=e.EncounterStatus, @Version=e.RowVersion, @TemplateVersionUid=e.TemplateVersionUid,
            @PatientId=p.PatientId
        FROM dbo.PatientEncounter e WITH (UPDLOCK,HOLDLOCK)
        INNER JOIN dbo.Patient p ON p.PatientUid=e.PatientUid AND p.IsDeleted=0
        WHERE e.PatientUid=@PatientUid AND e.EncounterUid=@EncounterUid;
        IF @PatientId IS NULL BEGIN ROLLBACK; RETURN; END;
        IF @Status<>N'Open' OR @Status IS NULL THROW 51071, 'Only open encounters can be edited.', 1;
        IF @ExpectedRowVersion IS NULL OR @Version<>@ExpectedRowVersion
            THROW 51073, 'The encounter was changed by another user.', 1;
        SELECT @ActorName=COALESCE(NULLIF(DisplayName,N''),CONCAT(N'Clinical user ',UserId))
            FROM dbo.ApplicationUser WHERE UserId=@ActorId;
        IF @ActorName IS NULL THROW 51171, 'A tenant clinical actor is required.', 1;
        IF @TemplateVersionUid IS NULL
            SET @SectionTitle=CASE @SectionKey COLLATE Latin1_General_100_BIN2
                WHEN N'subjective' THEN N'Subjective' WHEN N'objective' THEN N'Objective'
                WHEN N'assessment' THEN N'Assessment' WHEN N'plan' THEN N'Plan' WHEN N'notes' THEN N'Note' END;
        ELSE
            SELECT @SectionTitle=s.Title FROM dbo.DocumentTemplateVersion v
            CROSS APPLY OPENJSON(v.DefinitionJson,'$.sections') WITH ([Key] NVARCHAR(100) '$.key', Title NVARCHAR(200) '$.title') s
            WHERE v.TemplateVersionUid=@TemplateVersionUid AND s.[Key] COLLATE Latin1_General_100_BIN2=@SectionKey COLLATE Latin1_General_100_BIN2;
        IF @SectionTitle IS NULL THROW 51171, 'The section does not belong to the encounter.', 1;
        DECLARE @FieldLabel nvarchar(200);
        IF @FieldKey IS NOT NULL
        BEGIN
            SELECT @FieldLabel=f.Label FROM dbo.DocumentTemplateVersion v
            CROSS APPLY OPENJSON(v.DefinitionJson,'$.sections') WITH ([Key] nvarchar(100) '$.key', Fields nvarchar(max) '$.fields' AS JSON) s
            CROSS APPLY OPENJSON(s.Fields) WITH ([Key] nvarchar(100) '$.key', Label nvarchar(200) '$.label', [Type] nvarchar(30) '$.type') f
            WHERE v.TemplateVersionUid=@TemplateVersionUid
                AND s.[Key] COLLATE Latin1_General_100_BIN2=@SectionKey COLLATE Latin1_General_100_BIN2
                AND f.[Key] COLLATE Latin1_General_100_BIN2=@FieldKey COLLATE Latin1_General_100_BIN2
                AND f.[Type] IN (N'Text',N'TextArea');
            IF @FieldLabel IS NULL THROW 51171,'The narrative field does not belong to this section.',1;
        END;
        IF @ContributionUid IS NULL
        BEGIN
            SET @ContributionUid=NEWID();
            INSERT dbo.PatientEncounterContribution(ContributionUid,EncounterUid,PatientUid,SectionKey,SectionTitle,
                OriginalContributorId,OriginalContributorDisplayName,CreatedAtUtc,FieldKey,FieldLabel)
            VALUES(@ContributionUid,@EncounterUid,@PatientUid,@SectionKey,@SectionTitle,@ActorId,@ActorName,@Now,@FieldKey,@FieldLabel);
        END
        ELSE
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM dbo.PatientEncounterContribution
                WHERE ContributionUid=@ContributionUid AND EncounterUid=@EncounterUid AND PatientUid=@PatientUid
                    AND SectionKey COLLATE Latin1_General_100_BIN2=@SectionKey COLLATE Latin1_General_100_BIN2
                    AND ValueKind=N'Narrative' AND ((FieldKey IS NULL AND @FieldKey IS NULL)
                    OR FieldKey COLLATE Latin1_General_100_BIN2=@FieldKey COLLATE Latin1_General_100_BIN2))
                BEGIN ROLLBACK; RETURN; END;
            SELECT TOP(1) @Revision=RevisionNumber+1, @OldContent=Content
                FROM dbo.PatientEncounterContributionRevision WHERE ContributionUid=@ContributionUid ORDER BY RevisionNumber DESC;
            SET @Action=N'ContributionRevised';
        END;
        -- Revisions and original identity are immutable: no update/delete path.
        INSERT dbo.PatientEncounterContributionRevision(ContributionUid,RevisionNumber,Content,EditorId,EditorDisplayName,RevisedAtUtc)
            VALUES(@ContributionUid,@Revision,@Content,@ActorId,@ActorName,@Now);
        UPDATE dbo.PatientEncounter SET UpdatedBy=@ActorId, UpdatedAt=@Now
            WHERE EncounterUid=@EncounterUid AND PatientUid=@PatientUid;
        INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt)
            VALUES(@ActorId,@PatientId,@Action,N'PatientEncounterContribution',CONVERT(NVARCHAR(100),@ContributionUid),@OldContent,@Content,@Now);
        EXEC dbo.PatientEncounterHistory_Create @EncounterUid=@EncounterUid,@PatientUid=@PatientUid,
            @ActionType=@Action,@ActionDescription=N'Encounter contribution saved; original attribution preserved.',
            @CreatedBy=@ActorId,@ReturnResult=0;
        COMMIT;
        SELECT @ContributionUid;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT>0 ROLLBACK;
        THROW;
    END CATCH;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientEncounter_UpdateNote
    @PatientUid uniqueidentifier,@EncounterUid uniqueidentifier,@EncounterNotes nvarchar(max)=NULL,@UpdatedBy bigint=NULL AS
BEGIN
    SET NOCOUNT ON;
    THROW 51071,'Existing narrative is read-only. Use attributed contributions for new text.',1;
END;
GO
CREATE OR ALTER PROCEDURE dbo.PatientEncounter_UpdateSoapNote
    @PatientUid uniqueidentifier,@EncounterUid uniqueidentifier,@SubjectiveNote nvarchar(max)=NULL,
    @ObjectiveNote nvarchar(max)=NULL,@AssessmentNote nvarchar(max)=NULL,@PlanNote nvarchar(max)=NULL,@UpdatedBy bigint=NULL AS
BEGIN
    SET NOCOUNT ON;
    THROW 51071,'Existing SOAP text is read-only. Use attributed contributions for new text.',1;
END;
GO
-- Compare decimal JSON values exactly, without rounded numeric casts losing tiny changes.
CREATE OR ALTER FUNCTION dbo.PatientEncounter_NormalizeNumber(@Value nvarchar(max)) RETURNS nvarchar(max) AS
BEGIN
    IF @Value IS NULL RETURN NULL;
    DECLARE @Sign nvarchar(1)=N'',@Power int=0,@Point int,@Exponent int;
    IF LEFT(@Value,1)=N'-' BEGIN SET @Sign=N'-'; SET @Value=SUBSTRING(@Value,2,LEN(@Value)); END;
    SET @Exponent=CHARINDEX(N'e',LOWER(@Value));
    IF @Exponent>0
    BEGIN
        SET @Power=TRY_CONVERT(int,SUBSTRING(@Value,@Exponent+1,LEN(@Value)));
        SET @Value=LEFT(@Value,@Exponent-1);
    END;
    SET @Point=CHARINDEX(N'.',@Value);
    IF @Point>0 SET @Power=@Power-(LEN(@Value)-@Point);
    SET @Value=REPLACE(@Value,N'.',N'');
    WHILE LEFT(@Value,1)=N'0' AND LEN(@Value)>0 SET @Value=SUBSTRING(@Value,2,LEN(@Value));
    IF LEN(@Value)=0 RETURN N'0';
    WHILE RIGHT(@Value,1)=N'0' BEGIN SET @Value=LEFT(@Value,LEN(@Value)-1); SET @Power=@Power+1; END;
    RETURN @Sign+@Value+N'e'+CONVERT(nvarchar(20),@Power);
END;
GO
CREATE OR ALTER PROCEDURE dbo.PatientEncounter_UpdateStructuredData
    @PatientUid uniqueidentifier,@EncounterUid uniqueidentifier,@StructuredDataJson nvarchar(max),
    @SubjectiveNote nvarchar(max)=NULL,@ObjectiveNote nvarchar(max)=NULL,@AssessmentNote nvarchar(max)=NULL,@PlanNote nvarchar(max)=NULL,
    @ExpectedRowVersion binary(8),@UpdatedBy bigint=NULL AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRY
        BEGIN TRANSACTION;
        DECLARE @PatientId bigint,@Status nvarchar(30),@Version binary(8),@TemplateVersionUid uniqueidentifier,
            @OldJson nvarchar(max),@Definition nvarchar(max),@ActorName nvarchar(200),@Now datetime2(7)=SYSUTCDATETIME();
        SELECT @PatientId=p.PatientId,@Status=e.EncounterStatus,@Version=e.RowVersion,
            @TemplateVersionUid=e.TemplateVersionUid,@OldJson=e.StructuredDataJson
        FROM dbo.PatientEncounter e WITH(UPDLOCK,HOLDLOCK) JOIN dbo.Patient p ON p.PatientUid=e.PatientUid AND p.IsDeleted=0
        WHERE e.PatientUid=@PatientUid AND e.EncounterUid=@EncounterUid;
        IF @PatientId IS NULL BEGIN ROLLBACK; RETURN; END;
        IF @Status IS NULL OR @Status<>N'Open' THROW 51071,'Only open encounters can be edited.',1;
        IF @ExpectedRowVersion IS NULL OR @Version<>@ExpectedRowVersion THROW 51073,'The encounter changed. Reopen it before saving.',1;
        SELECT @ActorName=COALESCE(NULLIF(DisplayName,N''),CONCAT(N'Clinical user ',UserId)) FROM dbo.ApplicationUser WHERE UserId=@UpdatedBy;
        IF @ActorName IS NULL THROW 51171,'A tenant clinical actor is required.',1;
        SELECT @Definition=DefinitionJson FROM dbo.DocumentTemplateVersion WHERE TemplateVersionUid=@TemplateVersionUid;
        IF @Definition IS NULL OR ISJSON(@StructuredDataJson)<>1 THROW 51171,'The historical encounter template is unavailable or data is invalid.',1;
        IF JSON_VALUE(@StructuredDataJson,'$.schemaVersion')<>JSON_VALUE(@Definition,'$.schemaVersion')
            OR JSON_VALUE(@StructuredDataJson,'$.schemaVersion') IS NULL OR JSON_QUERY(@StructuredDataJson,'$.values') IS NULL
            THROW 51171,'The data schema version does not match the encounter.',1;
        DECLARE @Fields TABLE (FieldKey nvarchar(100) COLLATE Latin1_General_100_BIN2 PRIMARY KEY,
            SectionKey nvarchar(100),Title nvarchar(200),Label nvarchar(200),[Type] nvarchar(30),Options nvarchar(max));
        INSERT @Fields
        SELECT f.[Key],s.[Key],s.Title,f.Label,f.[Type],f.Options FROM OPENJSON(@Definition,'$.sections')
            WITH ([Key] nvarchar(100) '$.key',Title nvarchar(200) '$.title',Fields nvarchar(max) '$.fields' AS JSON) s
        CROSS APPLY OPENJSON(s.Fields) WITH ([Key] nvarchar(100) '$.key',Label nvarchar(200) '$.label',
            [Type] nvarchar(30) '$.type',Options nvarchar(max) '$.options' AS JSON) f;
        DECLARE @Before TABLE ([Key] nvarchar(100) COLLATE Latin1_General_100_BIN2 PRIMARY KEY,Value nvarchar(max),[Type] int);
        DECLARE @After TABLE ([Key] nvarchar(100) COLLATE Latin1_General_100_BIN2 PRIMARY KEY,Value nvarchar(max),[Type] int);
        INSERT @Before SELECT [key],value,[type] FROM OPENJSON(@OldJson,'$.values');
        INSERT @After SELECT [key],value,[type] FROM OPENJSON(@StructuredDataJson,'$.values');
        IF EXISTS(SELECT 1 FROM @After a LEFT JOIN @Fields f ON f.FieldKey=a.[Key] WHERE f.FieldKey IS NULL)
            THROW 51171,'Unknown template field.',1;
        IF EXISTS(SELECT 1 FROM @Fields f LEFT JOIN @Before b ON b.[Key]=f.FieldKey LEFT JOIN @After a ON a.[Key]=f.FieldKey
            WHERE f.[Type] IN (N'Text',N'TextArea') AND
                (ISNULL(b.[Type],0)<>ISNULL(a.[Type],0) OR ISNULL(b.Value,N'') COLLATE Latin1_General_100_BIN2<>ISNULL(a.Value,N'') COLLATE Latin1_General_100_BIN2
                OR ISNULL(DATALENGTH(b.Value),-1)<>ISNULL(DATALENGTH(a.Value),-1)))
            THROW 51071,'Existing narrative is read-only. Use field-linked contributions for new text.',1;
        IF EXISTS(SELECT 1 FROM @After a JOIN @Fields f ON f.FieldKey=a.[Key] WHERE a.[Type]<>0 AND
            (f.[Type]=N'StaticText' OR f.[Type] IN (N'Text',N'TextArea') AND a.[Type]<>1
            OR f.[Type]=N'Number' AND (a.[Type]<>2 OR TRY_CONVERT(decimal(38,0),a.Value) IS NULL)
            OR f.[Type]=N'Date' AND (a.[Type]<>1 OR TRY_CONVERT(date,a.Value,23) IS NULL OR LEN(a.Value)<>10)
            OR f.[Type] IN (N'Boolean',N'Checkbox') AND a.[Type]<>3
            OR f.[Type] IN (N'Select',N'Radio') AND (a.[Type]<>1 OR NOT EXISTS
                (SELECT 1 FROM OPENJSON(f.Options) WITH (Value nvarchar(100) '$.value') o WHERE o.Value COLLATE Latin1_General_100_BIN2=a.Value COLLATE Latin1_General_100_BIN2))))
            THROW 51171,'Invalid template field value.',1;
        DECLARE @Key nvarchar(100),@Section nvarchar(100),@Title nvarchar(200),@Label nvarchar(200),@OldValue nvarchar(max),@NewValue nvarchar(max),
            @OldType int,@NewType int,@Uid uniqueidentifier,@Revision int;
        DECLARE changes CURSOR LOCAL FAST_FORWARD FOR
            SELECT f.FieldKey,f.SectionKey,f.Title,f.Label,b.Value,a.Value,ISNULL(b.[Type],0),ISNULL(a.[Type],0)
            FROM @Fields f LEFT JOIN @Before b ON b.[Key]=f.FieldKey LEFT JOIN @After a ON a.[Key]=f.FieldKey
            WHERE f.[Type] NOT IN (N'Text',N'TextArea',N'StaticText') AND
                (ISNULL(b.[Type],0)<>ISNULL(a.[Type],0) OR
                CASE WHEN f.[Type]=N'Number' AND dbo.PatientEncounter_NormalizeNumber(b.Value)=dbo.PatientEncounter_NormalizeNumber(a.Value) THEN 0
                    WHEN ISNULL(b.Value,N'') COLLATE Latin1_General_100_BIN2=ISNULL(a.Value,N'') COLLATE Latin1_General_100_BIN2
                        AND ISNULL(DATALENGTH(b.Value),-1)=ISNULL(DATALENGTH(a.Value),-1) THEN 0 ELSE 1 END=1);
        OPEN changes;
        FETCH NEXT FROM changes INTO @Key,@Section,@Title,@Label,@OldValue,@NewValue,@OldType,@NewType;
        WHILE @@FETCH_STATUS=0
        BEGIN
            SET @OldValue=CASE WHEN @OldType=0 THEN N'null' WHEN @OldType=1 THEN N'"'+STRING_ESCAPE(@OldValue,'json')+N'"' ELSE @OldValue END;
            SET @NewValue=CASE WHEN @NewType=0 THEN N'null' WHEN @NewType=1 THEN N'"'+STRING_ESCAPE(@NewValue,'json')+N'"' ELSE @NewValue END;
            SET @Uid=NULL; SET @Revision=1;
            SELECT @Uid=ContributionUid FROM dbo.PatientEncounterContribution WHERE EncounterUid=@EncounterUid AND FieldKey=@Key AND ValueKind=N'Value';
            IF @Uid IS NULL
            BEGIN
                SET @Uid=NEWID();
                INSERT dbo.PatientEncounterContribution(ContributionUid,EncounterUid,PatientUid,SectionKey,SectionTitle,FieldKey,FieldLabel,ValueKind,
                    PriorValueJson,OriginalContributorId,OriginalContributorDisplayName,CreatedAtUtc)
                VALUES(@Uid,@EncounterUid,@PatientUid,@Section,@Title,@Key,@Label,N'Value',@OldValue,@UpdatedBy,@ActorName,@Now);
            END
            ELSE SELECT @Revision=MAX(RevisionNumber)+1 FROM dbo.PatientEncounterContributionRevision WHERE ContributionUid=@Uid;
            INSERT dbo.PatientEncounterContributionRevision VALUES(@Uid,@Revision,@NewValue,@UpdatedBy,@ActorName,@Now);
            INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt)
                VALUES(@UpdatedBy,@PatientId,N'FieldValueSaved',N'PatientEncounterContribution',CONVERT(nvarchar(100),@Uid),@OldValue,@NewValue,@Now);
            FETCH NEXT FROM changes INTO @Key,@Section,@Title,@Label,@OldValue,@NewValue,@OldType,@NewType;
        END;
        CLOSE changes; DEALLOCATE changes;
        -- Stored SOAP projections remain preserved; Application derives current projections from trusted data.
        UPDATE dbo.PatientEncounter SET StructuredDataJson=@StructuredDataJson,UpdatedBy=@UpdatedBy,UpdatedAt=@Now
            WHERE EncounterUid=@EncounterUid AND PatientUid=@PatientUid;
        INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt)
            VALUES(@UpdatedBy,@PatientId,N'UpdateNote',N'PatientEncounter',CONVERT(nvarchar(100),@EncounterUid),@OldJson,@StructuredDataJson,@Now);
        EXEC dbo.PatientEncounterHistory_Create @EncounterUid,@PatientUid,N'NoteUpdated',N'Template values and field provenance saved.',NULL,@Status,NULL,@UpdatedBy,0;
        COMMIT;
        EXEC dbo.PatientEncounter_GetByUid @EncounterUid;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT>0 ROLLBACK;
        THROW;
    END CATCH;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientEncounter_Sign
    @PatientUid UNIQUEIDENTIFIER,
    @EncounterUid UNIQUEIDENTIFIER,
    @SignedBy BIGINT = NULL,
    @ExpectedAppointmentStatus NVARCHAR(30),
    @CompletedAppointmentStatus NVARCHAR(30),
    @ExpectedRowVersion BINARY(8) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @PatientId BIGINT;
    DECLARE @EncounterStatus NVARCHAR(30);
    DECLARE @AppointmentUid UNIQUEIDENTIFIER;
    DECLARE @AppointmentStatus NVARCHAR(30);
    DECLARE @AppointmentFound BIT;
    DECLARE @EncounterFound BIT;
    -- Avoid scalar literals in procedure definitions because clients using
    -- Always Encrypted parameterization rewrite them during metadata discovery.
    SET @EncounterFound = CONVERT(BIT, @@ROWCOUNT - @@ROWCOUNT);
    SET @AppointmentFound = CONVERT(BIT, @@ROWCOUNT - @@ROWCOUNT);

    BEGIN TRANSACTION;

    SELECT
        @PatientId = pe.PatientId,
        @EncounterStatus = pe.EncounterStatus,
        @AppointmentUid = pe.AppointmentUid,
        @EncounterFound = 1
    FROM dbo.PatientEncounter AS pe WITH (UPDLOCK, HOLDLOCK)
    WHERE pe.PatientUid = @PatientUid
        AND pe.EncounterUid = @EncounterUid;

    IF @EncounterFound = 0
    BEGIN
        ROLLBACK TRANSACTION;
        RETURN;
    END;

    IF @EncounterStatus = N'Signed'
    BEGIN
        COMMIT TRANSACTION;
        EXEC dbo.PatientEncounter_GetByUid @EncounterUid = @EncounterUid;
        RETURN;
    END;

    IF ISNULL(@EncounterStatus, N'') <> N'Open'
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 51072, 'The encounter cannot be signed in its current status.', 1;
    END;

    IF @ExpectedRowVersion IS NOT NULL AND NOT EXISTS
        (SELECT 1 FROM dbo.PatientEncounter WHERE EncounterUid=@EncounterUid AND PatientUid=@PatientUid AND RowVersion=@ExpectedRowVersion)
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 51073,'The encounter changed after validation. Reopen before signing.',1;
    END;
    IF @ExpectedRowVersion IS NULL AND EXISTS
        (SELECT 1 FROM dbo.PatientEncounter WHERE EncounterUid=@EncounterUid AND TemplateVersionUid IS NOT NULL)
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 51073,'A validated encounter version is required for signing.',1;
    END;

    IF @AppointmentUid IS NOT NULL
    BEGIN
        SELECT
            @AppointmentStatus = appointment.AppointmentStatus,
            @AppointmentFound = 1
        FROM dbo.ScheduleAppointment AS appointment WITH (UPDLOCK, HOLDLOCK)
        WHERE appointment.AppointmentUid = @AppointmentUid
            AND appointment.IsDeleted = 0;

        IF @AppointmentFound = 0
        BEGIN
            ROLLBACK TRANSACTION;
            THROW 51086, 'The linked appointment was not found.', 1;
        END;

        IF @AppointmentStatus IS NULL OR @AppointmentStatus NOT IN
            (@ExpectedAppointmentStatus, @CompletedAppointmentStatus)
        BEGIN
            ROLLBACK TRANSACTION;
            THROW 51085, 'The linked appointment cannot be completed from its current status.', 1;
        END;
    END;

    UPDATE dbo.PatientEncounter
    SET EncounterStatus = N'Signed',
        Status = N'Signed',
        SignedAt = SYSUTCDATETIME(),
        SignedBy = @SignedBy,
        UpdatedAt = SYSUTCDATETIME(),
        UpdatedBy = @SignedBy
    WHERE PatientUid = @PatientUid
        AND EncounterUid = @EncounterUid;

    IF OBJECT_ID(N'dbo.AuditLog', N'U') IS NOT NULL
    BEGIN
        INSERT INTO dbo.AuditLog
        (
            UserId, PatientId, ActionName, EntityName, EntityId,
            OldValue, NewValue, CreatedAt
        )
        VALUES
        (
            @SignedBy, @PatientId, N'Sign', N'PatientEncounter',
            CONVERT(NVARCHAR(100), @EncounterUid), N'Open', N'Signed',
            SYSUTCDATETIME()
        );
    END;

    EXEC dbo.PatientEncounterHistory_Create
        @EncounterUid, @PatientUid, N'Signed', N'Encounter signed.',
        @EncounterStatus, N'Signed', NULL, @SignedBy, 0;

    IF @AppointmentUid IS NOT NULL
        AND @AppointmentStatus = @ExpectedAppointmentStatus
    BEGIN
        UPDATE dbo.ScheduleAppointment
        SET AppointmentStatus = @CompletedAppointmentStatus,
            UpdatedAt = SYSUTCDATETIME(),
            UpdatedBy = @SignedBy
        WHERE AppointmentUid = @AppointmentUid
            AND IsDeleted = 0
            AND AppointmentStatus = @ExpectedAppointmentStatus;

        IF @@ROWCOUNT <> 1
        BEGIN
            ROLLBACK TRANSACTION;
            THROW 51085, 'The linked appointment was updated by another user.', 1;
        END;

        IF OBJECT_ID(N'dbo.AuditLog', N'U') IS NOT NULL
        BEGIN
            INSERT INTO dbo.AuditLog
            (
                UserId, PatientId, ActionName, EntityName, EntityId,
                OldValue, NewValue, CreatedAt
            )
            VALUES
            (
                @SignedBy, @PatientId, N'UpdateStatus', N'ScheduleAppointment',
                CONVERT(NVARCHAR(100), @AppointmentUid),
                @AppointmentStatus, @CompletedAppointmentStatus,
                SYSUTCDATETIME()
            );
        END;

        EXEC dbo.AppointmentHistory_Create
            @AppointmentUid = @AppointmentUid,
            @ActionType = N'StatusChanged',
            @ActionDescription = N'Appointment completed when encounter was signed.',
            @OldStatus = @AppointmentStatus,
            @NewStatus = @CompletedAppointmentStatus,
            @CreatedBy = @SignedBy,
            @ReturnResult = 0;
    END;

    COMMIT TRANSACTION;

    EXEC dbo.PatientEncounter_GetByUid
        @EncounterUid = @EncounterUid;
END;

GO
