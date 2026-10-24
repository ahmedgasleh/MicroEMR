-- DE08.008: independent presence-aware amount/unit. No clinical backfill or legacy value changes.
SET XACT_ABORT ON;
GO
CREATE TABLE dbo.ImmunizationDoseUnitReference
(
 Code NVARCHAR(10) COLLATE Latin1_General_100_BIN2 NOT NULL PRIMARY KEY
);
-- Owner-approved local codes, not a claim of an external clinical vocabulary mapping.
INSERT dbo.ImmunizationDoseUnitReference(Code) VALUES(N'mL'),(N'mg'),(N'mcg'),(N'units');
ALTER TABLE dbo.PatientImmunization ADD DoseAmount DECIMAL(7,3) NULL,
 DoseUnitCode NVARCHAR(10) COLLATE Latin1_General_100_BIN2 NULL;
ALTER TABLE dbo.PatientImmunization ADD
 CONSTRAINT FK_Immunization_DoseUnit FOREIGN KEY(DoseUnitCode) REFERENCES dbo.ImmunizationDoseUnitReference(Code),
 CONSTRAINT CK_Immunization_DosePair CHECK((DoseAmount IS NULL AND DoseUnitCode IS NULL) OR (DoseAmount IS NOT NULL AND DoseUnitCode IS NOT NULL)),
 CONSTRAINT CK_Immunization_DoseAmount CHECK(DoseAmount IS NULL OR DoseAmount>0),
 CONSTRAINT CK_Immunization_RefusalDose CHECK(Refused IS NULL OR Refused=0 OR DoseAmount IS NULL);
GO
CREATE OR ALTER PROCEDURE dbo.PatientImmunization_ListByPatient
    @PatientUid UNIQUEIDENTIFIER,
    @Status NVARCHAR(30) = N'All'
AS
BEGIN
    SET NOCOUNT ON;
    IF @Status NOT IN (N'Completed', N'Refused', N'EnteredInError', N'All') SET @Status = N'All';
    SELECT i.ImmunizationUid, i.PatientUid, i.VaccineName, i.AdministrationDate, i.DoseNumber, i.DoseAmount, i.DoseUnitCode,
        i.Route, i.Site, i.LotNumber, i.SourceType, i.SourceDescription, i.AdministeredByName,
        i.EncounterUid, i.Notes, i.Status, i.CreatedAtUtc, i.CreatedBy,
        cu.DisplayName AS CreatedByDisplayName, i.UpdatedAtUtc, i.UpdatedBy,
        uu.DisplayName AS UpdatedByDisplayName, i.EnteredInErrorAtUtc, i.EnteredInErrorBy,
        eu.DisplayName AS EnteredInErrorByDisplayName, i.EnteredInErrorReason, i.RowVersion, i.DIN,i.Manufacturer,i.ImmunizationTypeCode,i.Refused,
        i.AdministrationYear,i.AdministrationMonth,i.AdministrationDay,i.RefusalYear,i.RefusalMonth,i.RefusalDay
    FROM dbo.PatientImmunization AS i
    JOIN dbo.Patient p ON p.PatientUid=i.PatientUid AND p.IsDeleted=0
    LEFT JOIN dbo.ApplicationUser AS cu ON cu.UserId = i.CreatedBy
    LEFT JOIN dbo.ApplicationUser AS uu ON uu.UserId = i.UpdatedBy
    LEFT JOIN dbo.ApplicationUser AS eu ON eu.UserId = i.EnteredInErrorBy
    WHERE i.PatientUid = @PatientUid AND (@Status = N'All' OR i.Status = @Status)
    ORDER BY COALESCE(i.AdministrationYear,YEAR(i.AdministrationDate),i.RefusalYear) DESC,
        COALESCE(i.AdministrationMonth,MONTH(i.AdministrationDate),i.RefusalMonth) DESC,
        COALESCE(i.AdministrationDay,DAY(i.AdministrationDate),i.RefusalDay) DESC, i.CreatedAtUtc DESC,i.ImmunizationUid;
END;
GO
CREATE OR ALTER PROCEDURE dbo.PatientImmunization_GetByUid
    @PatientUid UNIQUEIDENTIFIER,
    @ImmunizationUid UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT i.ImmunizationUid, i.PatientUid, i.VaccineName, i.AdministrationDate, i.DoseNumber, i.DoseAmount, i.DoseUnitCode,
        i.Route, i.Site, i.LotNumber, i.SourceType, i.SourceDescription, i.AdministeredByName,
        i.EncounterUid, i.Notes, i.Status, i.CreatedAtUtc, i.CreatedBy,
        cu.DisplayName AS CreatedByDisplayName, i.UpdatedAtUtc, i.UpdatedBy,
        uu.DisplayName AS UpdatedByDisplayName, i.EnteredInErrorAtUtc, i.EnteredInErrorBy,
        eu.DisplayName AS EnteredInErrorByDisplayName, i.EnteredInErrorReason, i.RowVersion, i.DIN,i.Manufacturer,i.ImmunizationTypeCode,i.Refused,
        i.AdministrationYear,i.AdministrationMonth,i.AdministrationDay,i.RefusalYear,i.RefusalMonth,i.RefusalDay
    FROM dbo.PatientImmunization AS i
    JOIN dbo.Patient p ON p.PatientUid=i.PatientUid AND p.IsDeleted=0
    LEFT JOIN dbo.ApplicationUser AS cu ON cu.UserId = i.CreatedBy
    LEFT JOIN dbo.ApplicationUser AS uu ON uu.UserId = i.UpdatedBy
    LEFT JOIN dbo.ApplicationUser AS eu ON eu.UserId = i.EnteredInErrorBy
    WHERE i.PatientUid = @PatientUid AND i.ImmunizationUid = @ImmunizationUid;
END;
GO
CREATE OR ALTER PROCEDURE dbo.PatientImmunization_Create
 @PatientUid UNIQUEIDENTIFIER, @VaccineName NVARCHAR(200),@AdministrationDate DATE=NULL,@DoseNumber INT=NULL,
 @Route NVARCHAR(100)=NULL,@Site NVARCHAR(100)=NULL,@LotNumber NVARCHAR(100)=NULL,
 @SourceType NVARCHAR(30)=NULL,@SourceDescription NVARCHAR(500)=NULL,@AdministeredByName NVARCHAR(200)=NULL,
 @EncounterUid UNIQUEIDENTIFIER=NULL,@Notes NVARCHAR(1000)=NULL,@Actor BIGINT,
 @ClinicalFields NVARCHAR(MAX)=NULL,@AdministrationDateSpecified BIT=0,@RequestCorrelationId NVARCHAR(100)=NULL,@Dose NVARCHAR(MAX)=NULL
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 DECLARE @DoseAmount DECIMAL(7,3),@DoseUnitCode NVARCHAR(MAX);
 DECLARE @Replace BIT=CASE WHEN @ClinicalFields IS NULL THEN 0 ELSE 1 END,@DIN NVARCHAR(MAX),@Manufacturer NVARCHAR(MAX),@Type NVARCHAR(MAX),@Refused BIT,
 @AY INT,@AM INT,@AD INT,@RY INT,@RM INT,@RD INT,@OldValue NVARCHAR(MAX)=NULL,@NewValue NVARCHAR(MAX);
 IF @Replace=1 AND (@AdministrationDateSpecified=1 OR @AdministrationDate IS NOT NULL) THROW 52300,'Supply clinical fields or legacy administration date.',1;
 BEGIN TRY
 IF @Actor IS NULL OR NOT EXISTS(SELECT 1 FROM dbo.ApplicationUser WHERE UserId=@Actor AND IsActive=1) THROW 52306,'Active clinical actor was not found.',1;
 DECLARE @TenantUid UNIQUEIDENTIFIER=(SELECT TenantUid FROM dbo.TenantDatabaseIdentity);
 IF @TenantUid IS NULL THROW 52306,'Trusted tenant identity is required.',1;
 SET @RequestCorrelationId=COALESCE(NULLIF(LTRIM(RTRIM(@RequestCorrelationId)),N''),CONVERT(NVARCHAR(100),NEWID()));
 BEGIN TRANSACTION;
 DECLARE @PatientId BIGINT=(SELECT PatientId FROM dbo.Patient WITH(UPDLOCK,HOLDLOCK) WHERE PatientUid=@PatientUid AND IsDeleted=0);
 IF @PatientId IS NULL THROW 52305,'Patient was not found.',1;
 IF @ClinicalFields IS NOT NULL AND (ISJSON(@ClinicalFields)<>1 OR
 LEFT(LTRIM(REPLACE(REPLACE(REPLACE(@ClinicalFields,NCHAR(9),N' '),NCHAR(10),N' '),NCHAR(13),N' ')),1)<>N'{') THROW 52300,'Clinical fields must be an object.',1;
 SELECT @DIN=DIN,@Manufacturer=Manufacturer,@Type=TypeCode,@Refused=Refused,
 @AY=AY,@AM=AM,@AD=AD,@RY=RY,@RM=RM,@RD=RD
 FROM OPENJSON(COALESCE(@ClinicalFields,N'{}')) WITH
 (DIN NVARCHAR(MAX) '$.DIN',Manufacturer NVARCHAR(MAX) '$.Manufacturer',TypeCode NVARCHAR(MAX) '$.ImmunizationTypeCode',Refused BIT '$.Refused',
 AY INT '$.AdministrationDate.Year',AM INT '$.AdministrationDate.Month',AD INT '$.AdministrationDate.Day',
 RY INT '$.RefusalDate.Year',RM INT '$.RefusalDate.Month',RD INT '$.RefusalDate.Day');
 SET @DIN=NULLIF(LTRIM(RTRIM(@DIN)),N''); SET @Manufacturer=NULLIF(LTRIM(RTRIM(@Manufacturer)),N''); SET @Type=NULLIF(LTRIM(RTRIM(@Type)),N'');
 IF DATALENGTH(@DIN)>200 OR DATALENGTH(@Manufacturer)>400 THROW 52300,'Product fields exceed supported lengths.',1;
 IF @Type IS NOT NULL AND NOT EXISTS(SELECT 1 FROM dbo.ImmunizationTypeReference WHERE Code=@Type COLLATE Latin1_General_100_BIN2 AND DATALENGTH(Code)=DATALENGTH(@Type)) THROW 52300,'Invalid CT-020 type.',1;
 IF dbo.HistoryPartialDateValid(@AY,@AM,@AD)=0 OR dbo.HistoryPartialDateValid(@RY,@RM,@RD)=0 THROW 52300,'Invalid date precision.',1;
 IF @Replace=1 SET @AdministrationDate=CASE WHEN @AD IS NOT NULL THEN DATEFROMPARTS(@AY,@AM,@AD) END;
 IF @Replace=0 AND @AdministrationDate IS NULL THROW 52300,'Legacy administration date is required.',1;
 IF NULLIF(LTRIM(RTRIM(@VaccineName)),N'') IS NULL THROW 52300,'Vaccine name is required.',1;
 IF @AdministrationDate>CONVERT(DATE,SYSUTCDATETIME()) OR (@AY IS NOT NULL AND DATEFROMPARTS(@AY,COALESCE(@AM,1),COALESCE(@AD,1))>CONVERT(DATE,SYSUTCDATETIME())) THROW 52300,'Administration date cannot be in the future.',1;
 IF @DoseNumber<=0 THROW 52300,'Dose number must be positive.',1;
 IF @Refused=1
 BEGIN
  IF @AdministrationDate IS NOT NULL OR @AY IS NOT NULL OR @AM IS NOT NULL OR @AD IS NOT NULL OR @DIN IS NOT NULL OR @Manufacturer IS NOT NULL OR
   @SourceType IS NOT NULL OR @AdministeredByName IS NOT NULL OR @DoseNumber IS NOT NULL OR @Route IS NOT NULL OR @Site IS NOT NULL OR @LotNumber IS NOT NULL
   THROW 52300,'A refusal cannot assert administration content.',1;
 END
 ELSE
 BEGIN
  IF @RY IS NOT NULL OR @RM IS NOT NULL OR @RD IS NOT NULL THROW 52300,'Refusal date requires explicit refusal.',1;
  IF @SourceType IS NULL OR @SourceType NOT IN(N'ClinicAdministered',N'HistoricalExternal') THROW 52300,'Source type is invalid.',1;
  IF @SourceType=N'ClinicAdministered' AND NULLIF(LTRIM(RTRIM(@AdministeredByName)),N'') IS NULL THROW 52300,'Administered by is required.',1;
 END;
 -- Validate raw JSON before decimal conversion: SQL decimal parameters can silently round.
 IF @Dose IS NOT NULL
 BEGIN
  IF ISJSON(@Dose)<>1 OR LEFT(LTRIM(REPLACE(REPLACE(REPLACE(@Dose,NCHAR(9),N' '),NCHAR(10),N' '),NCHAR(13),N' ')),1)<>N'{' THROW 52300,'Dose must be an object.',1;
  IF EXISTS(SELECT 1 FROM OPENJSON(@Dose) WHERE [key] COLLATE Latin1_General_100_BIN2 NOT IN(N'Amount',N'UnitCode'))
   OR EXISTS(SELECT 1 FROM OPENJSON(@Dose) GROUP BY [key] COLLATE Latin1_General_100_BIN2 HAVING COUNT(*)>1)
   THROW 52300,'Dose accepts only one Amount and UnitCode.',1;
  SET @DoseAmount=NULL; SET @DoseUnitCode=NULL;
  DECLARE @RawAmount NVARCHAR(MAX)=JSON_VALUE(@Dose,'$.Amount');
  SET @DoseUnitCode=JSON_VALUE(@Dose,'$.UnitCode');
  IF EXISTS(SELECT 1 FROM OPENJSON(@Dose) WHERE ([key]=N'Amount' AND [type] NOT IN(0,2)) OR ([key]=N'UnitCode' AND [type] NOT IN(0,1)))
   THROW 52300,'Dose amount must be a JSON number and unit must be a canonical code.',1;
  IF (@RawAmount IS NULL AND @DoseUnitCode IS NOT NULL) OR (@RawAmount IS NOT NULL AND @DoseUnitCode IS NULL)
   THROW 52300,'Dose amount and unit must both be supplied or both left blank.',1;
  IF @RawAmount IS NOT NULL
  BEGIN
   IF LEN(@RawAmount)>8 OR @RawAmount COLLATE Latin1_General_100_BIN2 LIKE N'%[^0-9.]%'
    OR (CHARINDEX(N'.',@RawAmount)>0 AND LEN(@RawAmount)-CHARINDEX(N'.',@RawAmount)>3)
    OR TRY_CONVERT(DECIMAL(7,3),@RawAmount) IS NULL OR TRY_CONVERT(DECIMAL(7,3),@RawAmount)<=0
    THROW 52300,'Dose amount must be greater than 0, at most 9999.999, with at most 3 fractional digits.',1;
   SET @DoseAmount=CONVERT(DECIMAL(7,3),@RawAmount);
  END;
  IF @DoseUnitCode IS NOT NULL AND NOT EXISTS(SELECT 1 FROM dbo.ImmunizationDoseUnitReference WHERE Code=@DoseUnitCode COLLATE Latin1_General_100_BIN2 AND DATALENGTH(Code)=DATALENGTH(@DoseUnitCode))
   THROW 52300,'Unsupported immunization dose unit.',1;
 END;
 IF @Refused=1 AND @DoseAmount IS NOT NULL THROW 52300,'A refusal cannot assert an administered dose.',1;
 IF @EncounterUid IS NOT NULL AND NOT EXISTS(SELECT 1 FROM dbo.PatientEncounter WITH(UPDLOCK,HOLDLOCK) WHERE EncounterUid=@EncounterUid AND PatientUid=@PatientUid) THROW 52305,'Encounter was not found for patient.',1;
 DECLARE @ImmunizationUid UNIQUEIDENTIFIER=NEWID();
 INSERT dbo.PatientImmunization(DoseAmount,DoseUnitCode,ImmunizationUid,PatientUid,VaccineName,AdministrationDate,DoseNumber,Route,Site,LotNumber,SourceType,SourceDescription,AdministeredByName,EncounterUid,Notes,Status,CreatedBy,
 DIN,Manufacturer,ImmunizationTypeCode,Refused,AdministrationYear,AdministrationMonth,AdministrationDay,RefusalYear,RefusalMonth,RefusalDay)
 VALUES(@DoseAmount,@DoseUnitCode,@ImmunizationUid,@PatientUid,LTRIM(RTRIM(@VaccineName)),@AdministrationDate,@DoseNumber,NULLIF(LTRIM(RTRIM(@Route)),N''),NULLIF(LTRIM(RTRIM(@Site)),N''),NULLIF(LTRIM(RTRIM(@LotNumber)),N''),@SourceType,
 NULLIF(LTRIM(RTRIM(@SourceDescription)),N''),NULLIF(LTRIM(RTRIM(@AdministeredByName)),N''),@EncounterUid,NULLIF(LTRIM(RTRIM(@Notes)),N''),CASE WHEN @Refused=1 THEN N'Refused' ELSE N'Completed' END,@Actor,
 @DIN,@Manufacturer,@Type,@Refused,@AY,@AM,@AD,@RY,@RM,@RD);
 SET @NewValue=(SELECT @TenantUid AS TenantUid,@RequestCorrelationId AS RequestCorrelationId,i.* FROM dbo.PatientImmunization i WHERE i.PatientUid=@PatientUid AND i.ImmunizationUid=@ImmunizationUid FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
 INSERT dbo.AuditLog(UserId,PatientId,PatientUid,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt,
 AuditEventUid,EventCategory,ResourceType,ResourceUid,Outcome,RequestCorrelationId,SourceApplication)
 VALUES(@Actor,@PatientId,@PatientUid,N'ImmunizationCreated',N'PatientImmunization',CONVERT(NVARCHAR(100),@ImmunizationUid),@OldValue,@NewValue,SYSUTCDATETIME(),
 NEWID(),N'ClinicalMutation',N'PatientImmunization',@ImmunizationUid,N'Succeeded',@RequestCorrelationId,N'MicroEMR.Api');
 COMMIT;
 EXEC dbo.PatientImmunization_GetByUid @PatientUid,@ImmunizationUid;
 END TRY
 BEGIN CATCH
  IF @@TRANCOUNT>0 ROLLBACK;
  THROW;
 END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.PatientImmunization_Update
 @PatientUid UNIQUEIDENTIFIER,@ImmunizationUid UNIQUEIDENTIFIER,@ExpectedRowVersion BINARY(8)=NULL,
 @VaccineName NVARCHAR(200),@AdministrationDate DATE=NULL,@DoseNumber INT=NULL,
 @Route NVARCHAR(100)=NULL,@Site NVARCHAR(100)=NULL,@LotNumber NVARCHAR(100)=NULL,
 @SourceType NVARCHAR(30)=NULL,@SourceDescription NVARCHAR(500)=NULL,@AdministeredByName NVARCHAR(200)=NULL,
 @EncounterUid UNIQUEIDENTIFIER=NULL,@Notes NVARCHAR(1000)=NULL,@Actor BIGINT,
 @ClinicalFields NVARCHAR(MAX)=NULL,@AdministrationDateSpecified BIT=0,@RequestCorrelationId NVARCHAR(100)=NULL,@Dose NVARCHAR(MAX)=NULL
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 DECLARE @DoseAmount DECIMAL(7,3),@DoseUnitCode NVARCHAR(MAX);
 DECLARE @Replace BIT=CASE WHEN @ClinicalFields IS NULL THEN 0 ELSE 1 END,@DIN NVARCHAR(MAX),@Manufacturer NVARCHAR(MAX),@Type NVARCHAR(MAX),@Refused BIT,
 @AY INT,@AM INT,@AD INT,@RY INT,@RM INT,@RD INT,@OldValue NVARCHAR(MAX)=NULL,@NewValue NVARCHAR(MAX);
 IF @Replace=1 AND (@AdministrationDateSpecified=1 OR @AdministrationDate IS NOT NULL) THROW 52300,'Supply clinical fields or legacy administration date.',1;
 BEGIN TRY
 IF @ExpectedRowVersion IS NULL THROW 52300,'A current RowVersion is required.',1;
 IF @Actor IS NULL OR NOT EXISTS(SELECT 1 FROM dbo.ApplicationUser WHERE UserId=@Actor AND IsActive=1) THROW 52306,'Active clinical actor was not found.',1;
 DECLARE @TenantUid UNIQUEIDENTIFIER=(SELECT TenantUid FROM dbo.TenantDatabaseIdentity);
 IF @TenantUid IS NULL THROW 52306,'Trusted tenant identity is required.',1;
 SET @RequestCorrelationId=COALESCE(NULLIF(LTRIM(RTRIM(@RequestCorrelationId)),N''),CONVERT(NVARCHAR(100),NEWID()));
 BEGIN TRANSACTION;
 DECLARE @PatientId BIGINT=(SELECT PatientId FROM dbo.Patient WITH(UPDLOCK,HOLDLOCK) WHERE PatientUid=@PatientUid AND IsDeleted=0);
 IF @PatientId IS NULL BEGIN ROLLBACK; RETURN; END;
 DECLARE @Current BINARY(8),@Status NVARCHAR(30),@StoredDate DATE,@StoredYear INT,@StoredDay INT;
 SELECT @Current=i.RowVersion,@Status=i.Status,@StoredDate=i.AdministrationDate,@StoredYear=i.AdministrationYear,@StoredDay=i.AdministrationDay,
 @DoseAmount=i.DoseAmount,@DoseUnitCode=i.DoseUnitCode,
 @ClinicalFields=CASE WHEN @Replace=1 THEN @ClinicalFields ELSE
 (SELECT i.DIN,i.Manufacturer,i.ImmunizationTypeCode,i.Refused,i.AdministrationYear AS [AdministrationDate.Year],i.AdministrationMonth AS [AdministrationDate.Month],i.AdministrationDay AS [AdministrationDate.Day],
 i.RefusalYear AS [RefusalDate.Year],i.RefusalMonth AS [RefusalDate.Month],i.RefusalDay AS [RefusalDate.Day] FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER) END
 FROM dbo.PatientImmunization i WITH(UPDLOCK,HOLDLOCK) WHERE i.PatientUid=@PatientUid AND i.ImmunizationUid=@ImmunizationUid;
 IF @Current IS NULL BEGIN ROLLBACK; RETURN; END;
 IF @Current<>@ExpectedRowVersion THROW 52308,'Immunization changed. Reload and review.',1;
 IF @Status=N'EnteredInError' THROW 52309,'Entered-in-error records cannot be edited.',1;
 SET @OldValue=(SELECT @TenantUid AS TenantUid,@RequestCorrelationId AS RequestCorrelationId,i.* FROM dbo.PatientImmunization i WHERE i.PatientUid=@PatientUid AND i.ImmunizationUid=@ImmunizationUid FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
 IF @ClinicalFields IS NOT NULL AND (ISJSON(@ClinicalFields)<>1 OR
 LEFT(LTRIM(REPLACE(REPLACE(REPLACE(@ClinicalFields,NCHAR(9),N' '),NCHAR(10),N' '),NCHAR(13),N' ')),1)<>N'{') THROW 52300,'Clinical fields must be an object.',1;
 SELECT @DIN=DIN,@Manufacturer=Manufacturer,@Type=TypeCode,@Refused=Refused,
 @AY=AY,@AM=AM,@AD=AD,@RY=RY,@RM=RM,@RD=RD
 FROM OPENJSON(COALESCE(@ClinicalFields,N'{}')) WITH
 (DIN NVARCHAR(MAX) '$.DIN',Manufacturer NVARCHAR(MAX) '$.Manufacturer',TypeCode NVARCHAR(MAX) '$.ImmunizationTypeCode',Refused BIT '$.Refused',
 AY INT '$.AdministrationDate.Year',AM INT '$.AdministrationDate.Month',AD INT '$.AdministrationDate.Day',
 RY INT '$.RefusalDate.Year',RM INT '$.RefusalDate.Month',RD INT '$.RefusalDate.Day');
 SET @DIN=NULLIF(LTRIM(RTRIM(@DIN)),N''); SET @Manufacturer=NULLIF(LTRIM(RTRIM(@Manufacturer)),N''); SET @Type=NULLIF(LTRIM(RTRIM(@Type)),N'');
 IF DATALENGTH(@DIN)>200 OR DATALENGTH(@Manufacturer)>400 THROW 52300,'Product fields exceed supported lengths.',1;
 IF @Type IS NOT NULL AND NOT EXISTS(SELECT 1 FROM dbo.ImmunizationTypeReference WHERE Code=@Type COLLATE Latin1_General_100_BIN2 AND DATALENGTH(Code)=DATALENGTH(@Type)) THROW 52300,'Invalid CT-020 type.',1;
 IF dbo.HistoryPartialDateValid(@AY,@AM,@AD)=0 OR dbo.HistoryPartialDateValid(@RY,@RM,@RD)=0 THROW 52300,'Invalid date precision.',1;
 IF @Replace=1 SET @AdministrationDate=CASE WHEN @AD IS NOT NULL THEN DATEFROMPARTS(@AY,@AM,@AD) END;
 ELSE IF @AdministrationDateSpecified=1 OR @AdministrationDate IS NOT NULL
 BEGIN
  IF @Status=N'Refused' THROW 52300,'Use the updated editor for a refused record.',1;
  IF @AdministrationDate IS NULL AND @StoredYear IS NOT NULL AND @StoredDay IS NULL THROW 52300,'Use the updated editor to clear a partial date.',1;
  SET @AY=NULL;SET @AM=NULL;SET @AD=NULL;
 END
 ELSE SET @AdministrationDate=@StoredDate;
 IF (@Status=N'Refused' AND (@Refused IS NULL OR @Refused=0)) OR (@Status=N'Completed' AND @Refused=1) THROW 52300,'Outcome conversion is not supported.',1;
 IF NULLIF(LTRIM(RTRIM(@VaccineName)),N'') IS NULL THROW 52300,'Vaccine name is required.',1;
 IF @AdministrationDate>CONVERT(DATE,SYSUTCDATETIME()) OR (@AY IS NOT NULL AND DATEFROMPARTS(@AY,COALESCE(@AM,1),COALESCE(@AD,1))>CONVERT(DATE,SYSUTCDATETIME())) THROW 52300,'Administration date cannot be in the future.',1;
 IF @DoseNumber<=0 THROW 52300,'Dose number must be positive.',1;
 IF @Refused=1
 BEGIN
  IF @AdministrationDate IS NOT NULL OR @AY IS NOT NULL OR @AM IS NOT NULL OR @AD IS NOT NULL OR @DIN IS NOT NULL OR @Manufacturer IS NOT NULL OR
   @SourceType IS NOT NULL OR @AdministeredByName IS NOT NULL OR @DoseNumber IS NOT NULL OR @Route IS NOT NULL OR @Site IS NOT NULL OR @LotNumber IS NOT NULL
   THROW 52300,'A refusal cannot assert administration content.',1;
 END
 ELSE
 BEGIN
  IF @RY IS NOT NULL OR @RM IS NOT NULL OR @RD IS NOT NULL THROW 52300,'Refusal date requires explicit refusal.',1;
  IF @SourceType IS NULL OR @SourceType NOT IN(N'ClinicAdministered',N'HistoricalExternal') THROW 52300,'Source type is invalid.',1;
  IF @SourceType=N'ClinicAdministered' AND NULLIF(LTRIM(RTRIM(@AdministeredByName)),N'') IS NULL THROW 52300,'Administered by is required.',1;
 END;
 -- Validate raw JSON before decimal conversion: SQL decimal parameters can silently round.
 IF @Dose IS NOT NULL
 BEGIN
  IF ISJSON(@Dose)<>1 OR LEFT(LTRIM(REPLACE(REPLACE(REPLACE(@Dose,NCHAR(9),N' '),NCHAR(10),N' '),NCHAR(13),N' ')),1)<>N'{' THROW 52300,'Dose must be an object.',1;
  IF EXISTS(SELECT 1 FROM OPENJSON(@Dose) WHERE [key] COLLATE Latin1_General_100_BIN2 NOT IN(N'Amount',N'UnitCode'))
   OR EXISTS(SELECT 1 FROM OPENJSON(@Dose) GROUP BY [key] COLLATE Latin1_General_100_BIN2 HAVING COUNT(*)>1)
   THROW 52300,'Dose accepts only one Amount and UnitCode.',1;
  SET @DoseAmount=NULL; SET @DoseUnitCode=NULL;
  DECLARE @RawAmount NVARCHAR(MAX)=JSON_VALUE(@Dose,'$.Amount');
  SET @DoseUnitCode=JSON_VALUE(@Dose,'$.UnitCode');
  IF EXISTS(SELECT 1 FROM OPENJSON(@Dose) WHERE ([key]=N'Amount' AND [type] NOT IN(0,2)) OR ([key]=N'UnitCode' AND [type] NOT IN(0,1)))
   THROW 52300,'Dose amount must be a JSON number and unit must be a canonical code.',1;
  IF (@RawAmount IS NULL AND @DoseUnitCode IS NOT NULL) OR (@RawAmount IS NOT NULL AND @DoseUnitCode IS NULL)
   THROW 52300,'Dose amount and unit must both be supplied or both left blank.',1;
  IF @RawAmount IS NOT NULL
  BEGIN
   IF LEN(@RawAmount)>8 OR @RawAmount COLLATE Latin1_General_100_BIN2 LIKE N'%[^0-9.]%'
    OR (CHARINDEX(N'.',@RawAmount)>0 AND LEN(@RawAmount)-CHARINDEX(N'.',@RawAmount)>3)
    OR TRY_CONVERT(DECIMAL(7,3),@RawAmount) IS NULL OR TRY_CONVERT(DECIMAL(7,3),@RawAmount)<=0
    THROW 52300,'Dose amount must be greater than 0, at most 9999.999, with at most 3 fractional digits.',1;
   SET @DoseAmount=CONVERT(DECIMAL(7,3),@RawAmount);
  END;
  IF @DoseUnitCode IS NOT NULL AND NOT EXISTS(SELECT 1 FROM dbo.ImmunizationDoseUnitReference WHERE Code=@DoseUnitCode COLLATE Latin1_General_100_BIN2 AND DATALENGTH(Code)=DATALENGTH(@DoseUnitCode))
   THROW 52300,'Unsupported immunization dose unit.',1;
 END;
 IF @Refused=1 AND @DoseAmount IS NOT NULL THROW 52300,'A refusal cannot assert an administered dose.',1;
 IF @EncounterUid IS NOT NULL AND NOT EXISTS(SELECT 1 FROM dbo.PatientEncounter WITH(UPDLOCK,HOLDLOCK) WHERE EncounterUid=@EncounterUid AND PatientUid=@PatientUid) THROW 52305,'Encounter was not found for patient.',1;
 UPDATE dbo.PatientImmunization SET DoseAmount=@DoseAmount,DoseUnitCode=@DoseUnitCode,VaccineName=LTRIM(RTRIM(@VaccineName)),AdministrationDate=@AdministrationDate,DoseNumber=@DoseNumber,
 Route=NULLIF(LTRIM(RTRIM(@Route)),N''),Site=NULLIF(LTRIM(RTRIM(@Site)),N''),LotNumber=NULLIF(LTRIM(RTRIM(@LotNumber)),N''),SourceType=@SourceType,
 SourceDescription=NULLIF(LTRIM(RTRIM(@SourceDescription)),N''),AdministeredByName=NULLIF(LTRIM(RTRIM(@AdministeredByName)),N''),EncounterUid=@EncounterUid,Notes=NULLIF(LTRIM(RTRIM(@Notes)),N''),
 DIN=@DIN,Manufacturer=@Manufacturer,ImmunizationTypeCode=@Type,Refused=@Refused,AdministrationYear=@AY,AdministrationMonth=@AM,AdministrationDay=@AD,RefusalYear=@RY,RefusalMonth=@RM,RefusalDay=@RD,
 UpdatedAtUtc=SYSUTCDATETIME(),UpdatedBy=@Actor
 WHERE PatientUid=@PatientUid AND ImmunizationUid=@ImmunizationUid AND RowVersion=@ExpectedRowVersion;
 IF @@ROWCOUNT<>1 THROW 52308,'Immunization changed.',1;
 SET @NewValue=(SELECT @TenantUid AS TenantUid,@RequestCorrelationId AS RequestCorrelationId,i.* FROM dbo.PatientImmunization i WHERE i.PatientUid=@PatientUid AND i.ImmunizationUid=@ImmunizationUid FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
 INSERT dbo.AuditLog(UserId,PatientId,PatientUid,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt,
 AuditEventUid,EventCategory,ResourceType,ResourceUid,Outcome,RequestCorrelationId,SourceApplication)
 VALUES(@Actor,@PatientId,@PatientUid,N'ImmunizationUpdated',N'PatientImmunization',CONVERT(NVARCHAR(100),@ImmunizationUid),@OldValue,@NewValue,SYSUTCDATETIME(),
 NEWID(),N'ClinicalMutation',N'PatientImmunization',@ImmunizationUid,N'Succeeded',@RequestCorrelationId,N'MicroEMR.Api');
 COMMIT;
 EXEC dbo.PatientImmunization_GetByUid @PatientUid,@ImmunizationUid;
 END TRY
 BEGIN CATCH
  IF @@TRANCOUNT>0 ROLLBACK;
  THROW;
 END CATCH;
END;
GO
