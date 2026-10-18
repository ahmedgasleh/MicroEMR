-- Additive Immunization fields, precise dates, explicit refusal and required safeguards. No clinical backfill.
SET XACT_ABORT ON;
GO
CREATE TABLE dbo.ImmunizationTypeReference
(
 Code NVARCHAR(50) COLLATE Latin1_General_100_BIN2 NOT NULL PRIMARY KEY,
 Name NVARCHAR(250) NOT NULL, SourceStatus NCHAR(1) NOT NULL,
 Comments NVARCHAR(1000) NOT NULL, SourceRow INT NOT NULL,
 SourceVersion NVARCHAR(100) NOT NULL
);
INSERT dbo.ImmunizationTypeReference(Code,Name,SourceStatus,Comments,SourceRow,SourceVersion) VALUES
(N'BCG',N'Bacillus Calmette-Guérin',N'R',N'Not found in the February 2016 update to the "Contents of Immunizing Agents Available for Use in Canada" in the "Canadian Immunization Guide".',231,N'CDS-S 2.1 Final / 2026-06-05 / CT-020'),
(N'Chol-O',N'Cholera - Oral',N'R',N'Not found in the February 2016 update to the "Contents of Immunizing Agents Available for Use in Canada" in the "Canadian Immunization Guide"',232,N'CDS-S 2.1 Final / 2026-06-05 / CT-020'),
(N'CholEcol',N'Cholera, Enterotoxigenic Escherichia coli',N'P',N' ',233,N'CDS-S 2.1 Final / 2026-06-05 / CT-020'),
(N'Chol-Ecol-O',N'Cholera - E.coli - Oral',N'P',N' ',234,N'CDS-S 2.1 Final / 2026-06-05 / CT-020'),
(N'DTaP',N'Diphtheria, Tetanus, Acellular Pertussis - pediatric',N'R',N'Not found in the February 2016 update to the "Contents of Immunizing Agents Available for Use in Canada" in the "Canadian Immunization Guide"',235,N'CDS-S 2.1 Final / 2026-06-05 / CT-020'),
(N'DTaP-IPV',N'Diphtheria, Tetanus, Acellular Pertussis, Inactivated Polio - pediatric',N'P',N' ',236,N'CDS-S 2.1 Final / 2026-06-05 / CT-020'),
(N'DTaP-IPV-Hib',N'Diphtheria, Tetanus, Acellular Pertussis, Inactivated Polio, Haemophilus influenzae type b - pediatric',N'P',N' ',237,N'CDS-S 2.1 Final / 2026-06-05 / CT-020'),
(N'DTaP-IPV-Hib-HB',N'Diphtheria, Tetanus, Acellular Pertussis, Inactivated Polio, Haemophilus influenzae type b, Hepatitis B - pediatric',N'P',N' ',238,N'CDS-S 2.1 Final / 2026-06-05 / CT-020'),
(N'DTaP-IPV-HB',N'Diphtheria, Tetanus, Acellular Pertussis, Inactivated Polio, Hepatitis B - pediatric',N'R',N'Not found in the February 2016 update to the "Contents of Immunizing Agents Available for Use in Canada" in the "Canadian Immunization Guide"',239,N'CDS-S 2.1 Final / 2026-06-05 / CT-020'),
(N'DTaP-Hib',N'Diphtheria, Tetanus, Acellular Pertussis, Haemophilus influenzae type b - pediatric',N'R',N'Not found in the February 2016 update to the "Contents of Immunizing Agents Available for Use in Canada" in the "Canadian Immunization Guide"',240,N'CDS-S 2.1 Final / 2026-06-05 / CT-020'),
(N'DT-IPV',N'Diphtheria, Tetanus, Polio - pediatric',N'R',N'Not found in the February 2016 update to the "Contents of Immunizing Agents Available for Use in Canada" in the "Canadian Immunization Guide"',241,N'CDS-S 2.1 Final / 2026-06-05 / CT-020'),
(N'HA',N'Hepatitis A',N'P',N' ',242,N'CDS-S 2.1 Final / 2026-06-05 / CT-020'),
(N'HAHB',N'Hepatitis A and B',N'P',N' ',243,N'CDS-S 2.1 Final / 2026-06-05 / CT-020'),
(N'HA-Typh-I',N'Hepatitis A and Typhoid - Injection',N'P',N' ',244,N'CDS-S 2.1 Final / 2026-06-05 / CT-020'),
(N'HB',N'Hepatitis B',N'P',N' ',245,N'CDS-S 2.1 Final / 2026-06-05 / CT-020'),
(N'HBTmf',N'Hepatitis B - Thimerosal free',N'R',N'Not found in the February 2016 update to the "Contents of Immunizing Agents Available for Use in Canada" in the "Canadian Immunization Guide"',246,N'CDS-S 2.1 Final / 2026-06-05 / CT-020'),
(N'Hib',N'Haemophilus influenzae type b',N'P',N' ',247,N'CDS-S 2.1 Final / 2026-06-05 / CT-020'),
(N'HPV',N'Human Papilloma Virus',N'P',N'Previously categorized under "Other"',248,N'CDS-S 2.1 Final / 2026-06-05 / CT-020'),
(N'Inf',N'Influenza',N'P',N' ',249,N'CDS-S 2.1 Final / 2026-06-05 / CT-020'),
(N'IPV',N'Inactivated Polio',N'P',N' ',250,N'CDS-S 2.1 Final / 2026-06-05 / CT-020'),
(N'JE',N'Japanese Encephalitis',N'P',N' ',251,N'CDS-S 2.1 Final / 2026-06-05 / CT-020'),
(N'Men',N'Meningococcus',N'P',N' ',252,N'CDS-S 2.1 Final / 2026-06-05 / CT-020'),
(N'Men B',N'Neisseria Meningitidis Serogroup B Protein',N'P',N' ',253,N'CDS-S 2.1 Final / 2026-06-05 / CT-020'),
(N'Men-C',N'Meningococcal - Conjugate',N'P',N' ',254,N'CDS-S 2.1 Final / 2026-06-05 / CT-020'),
(N'Men-P-AC',N'Meningococcal - Polysaccharide',N'P',N' ',255,N'CDS-S 2.1 Final / 2026-06-05 / CT-020'),
(N'Men-P-ACWY',N'Meningococcal - Polysaccharide',N'P',N' ',256,N'CDS-S 2.1 Final / 2026-06-05 / CT-020'),
(N'MMR',N'Measles, Mumps, Rubella',N'P',N' ',257,N'CDS-S 2.1 Final / 2026-06-05 / CT-020'),
(N'MMR-Var',N'Measles, Mumps, Rubella, Varicella',N'P',N'Previously categorized under "Other"',258,N'CDS-S 2.1 Final / 2026-06-05 / CT-020'),
(N'MR',N'Measles, Rubella',N'R',N'Not found in the February 2016 update to the "Contents of Immunizing Agents Available for Use in Canada" in the "Canadian Immunization Guide"',259,N'CDS-S 2.1 Final / 2026-06-05 / CT-020'),
(N'Pneu',N'Pneumococcus',N'P',N' ',260,N'CDS-S 2.1 Final / 2026-06-05 / CT-020'),
(N'Pneu-C-7',N'Pneumococcal-Conjugate - valent',N'P',N' ',261,N'CDS-S 2.1 Final / 2026-06-05 / CT-020'),
(N'Pneu-P-23',N'Pneumococcal-Polysaccharide - valent',N'P',N' ',262,N'CDS-S 2.1 Final / 2026-06-05 / CT-020'),
(N'Rab',N'Rabies',N'P',N' ',263,N'CDS-S 2.1 Final / 2026-06-05 / CT-020'),
(N'ROT',N'Rotavirus - Oral',N'P',N'Previously categorized under "Other"',264,N'CDS-S 2.1 Final / 2026-06-05 / CT-020'),
(N'T',N'Tetanus',N'P',N' ',265,N'CDS-S 2.1 Final / 2026-06-05 / CT-020'),
(N'Td',N'Tetanus, Diphtheria - adult',N'P',N' ',266,N'CDS-S 2.1 Final / 2026-06-05 / CT-020'),
(N'Tdap',N'Tetanus, Diphtheria, Acellular Pertussis - adult',N'P',N' ',267,N'CDS-S 2.1 Final / 2026-06-05 / CT-020'),
(N'TdapIPV',N'Tetanus, Diphtheria, Acellular Pertussis, Inactivated Poliomyelitis',N'P',N' ',268,N'CDS-S 2.1 Final / 2026-06-05 / CT-020'),
(N'Td-IPV',N'Tetanus, Diphtheria, Inactivated Polio - adult',N'P',N' ',269,N'CDS-S 2.1 Final / 2026-06-05 / CT-020'),
(N'TBE',N'Tickborne Encephalitis',N'R',N'Not found in the February 2016 update to the "Contents of Immunizing Agents Available for Use in Canada" in the "Canadian Immunization Guide"',270,N'CDS-S 2.1 Final / 2026-06-05 / CT-020'),
(N'Typh',N'Typhoid',N'P',N' ',271,N'CDS-S 2.1 Final / 2026-06-05 / CT-020'),
(N'Typh-I',N'Typhoid - Injection',N'P',N' ',272,N'CDS-S 2.1 Final / 2026-06-05 / CT-020'),
(N'Typh-O',N'Typhoid - Oral',N'P',N' ',273,N'CDS-S 2.1 Final / 2026-06-05 / CT-020'),
(N'Typh (HA)',N'Typhoid, Hepatitis A',N'P',N' ',274,N'CDS-S 2.1 Final / 2026-06-05 / CT-020'),
(N'Var',N'Varicella',N'P',N' ',275,N'CDS-S 2.1 Final / 2026-06-05 / CT-020'),
(N'YF',N'Yellow Fever',N'P',N' ',276,N'CDS-S 2.1 Final / 2026-06-05 / CT-020'),
(N'Zos',N'Herpes Zoster',N'P',N' ',277,N'CDS-S 2.1 Final / 2026-06-05 / CT-020'),
(N'BAtx',N'Botulism Antitoxin',N'P',N'Not found in the February 2016 update to the "Contents of Immunizing Agents Available for Use in Canada" in the "Canadian Immunization Guide"',279,N'CDS-S 2.1 Final / 2026-06-05 / CT-020'),
(N'CMVIg',N'Cytomegalovirus Immune Globulin',N'P',N' ',280,N'CDS-S 2.1 Final / 2026-06-05 / CT-020'),
(N'DAtx',N'Diphtheria Antitoxin',N'P',N' ',281,N'CDS-S 2.1 Final / 2026-06-05 / CT-020'),
(N'HBIg',N'Hepatitis B Immunoglobulin',N'P',N' ',282,N'CDS-S 2.1 Final / 2026-06-05 / CT-020'),
(N'Ig',N'Immune Globulin',N'P',N' ',283,N'CDS-S 2.1 Final / 2026-06-05 / CT-020'),
(N'RabIg',N'Rabies Immunoglobulin',N'P',N' ',284,N'CDS-S 2.1 Final / 2026-06-05 / CT-020'),
(N'RSVAb',N'Respiratory Syncytial Virus Antibody',N'P',N'New',285,N'CDS-S 2.1 Final / 2026-06-05 / CT-020'),
(N'RSVIg',N'Respiratory Syncytial Virus Immunoglobulin',N'P',N'Not found in the February 2016 update to the "Contents of Immunizing Agents Available for Use in Canada" in the "Canadian Immunization Guide"',286,N'CDS-S 2.1 Final / 2026-06-05 / CT-020'),
(N'TIg',N'Tetanus Immunoglobulin',N'P',N' ',287,N'CDS-S 2.1 Final / 2026-06-05 / CT-020'),
(N'VarIg',N'Varicella Immunoglobulin',N'P',N' ',288,N'CDS-S 2.1 Final / 2026-06-05 / CT-020'),
(N'VIG',N'Vaccinia Immune Globulin',N'P',N'New',289,N'CDS-S 2.1 Final / 2026-06-05 / CT-020');
GO
ALTER TABLE dbo.PatientImmunization DROP CONSTRAINT CK_PatientImmunization_Status, CK_PatientImmunization_ErrorMetadata;
ALTER TABLE dbo.PatientImmunization ALTER COLUMN AdministrationDate DATE NULL;
ALTER TABLE dbo.PatientImmunization ALTER COLUMN SourceType NVARCHAR(30) NULL;
ALTER TABLE dbo.PatientImmunization ADD DIN NVARCHAR(100) NULL, Manufacturer NVARCHAR(200) NULL,
 ImmunizationTypeCode NVARCHAR(50) COLLATE Latin1_General_100_BIN2 NULL, Refused BIT NULL,
 AdministrationYear SMALLINT NULL, AdministrationMonth TINYINT NULL, AdministrationDay TINYINT NULL,
 RefusalYear SMALLINT NULL, RefusalMonth TINYINT NULL, RefusalDay TINYINT NULL;
ALTER TABLE dbo.PatientImmunization ADD
 CONSTRAINT FK_Immunization_Type FOREIGN KEY(ImmunizationTypeCode) REFERENCES dbo.ImmunizationTypeReference(Code),
 CONSTRAINT CK_Immunization_AdminParts CHECK(dbo.HistoryPartialDateValid(AdministrationYear,AdministrationMonth,AdministrationDay)=1),
 CONSTRAINT CK_Immunization_RefusalParts CHECK(dbo.HistoryPartialDateValid(RefusalYear,RefusalMonth,RefusalDay)=1),
 CONSTRAINT CK_Immunization_DateCompatibility CHECK(AdministrationYear IS NULL OR
  (AdministrationDay IS NULL AND AdministrationDate IS NULL) OR (AdministrationDay IS NOT NULL AND AdministrationDate IS NOT NULL AND
   YEAR(AdministrationDate)=AdministrationYear AND MONTH(AdministrationDate)=AdministrationMonth AND DAY(AdministrationDate)=AdministrationDay)),
 CONSTRAINT CK_PatientImmunization_Status CHECK(Status IN(N'Completed',N'Refused',N'EnteredInError')),
 CONSTRAINT CK_Immunization_Outcome CHECK((Status<>N'Refused' OR (Refused IS NOT NULL AND Refused=1)) AND (Status<>N'Completed' OR Refused IS NULL OR Refused=0)),
 CONSTRAINT CK_Immunization_RefusalContent CHECK((Refused IS NULL OR Refused=0 OR
  (AdministrationDate IS NULL AND AdministrationYear IS NULL AND AdministrationMonth IS NULL AND AdministrationDay IS NULL AND
   DIN IS NULL AND Manufacturer IS NULL AND SourceType IS NULL AND AdministeredByName IS NULL AND DoseNumber IS NULL AND Route IS NULL AND Site IS NULL AND LotNumber IS NULL)) AND
  ((Refused IS NOT NULL AND Refused=1) OR (RefusalYear IS NULL AND RefusalMonth IS NULL AND RefusalDay IS NULL))),
 CONSTRAINT CK_Immunization_AdminSource CHECK(COALESCE(Refused,0)=1 OR SourceType IS NOT NULL),
 CONSTRAINT CK_PatientImmunization_ErrorMetadata CHECK
  ((Status IN(N'Completed',N'Refused') AND EnteredInErrorAtUtc IS NULL AND EnteredInErrorBy IS NULL AND EnteredInErrorReason IS NULL) OR
   (Status=N'EnteredInError' AND EnteredInErrorAtUtc IS NOT NULL AND EnteredInErrorBy IS NOT NULL AND NULLIF(LTRIM(RTRIM(EnteredInErrorReason)),N'') IS NOT NULL));
GO
CREATE OR ALTER PROCEDURE dbo.PatientImmunization_ListByPatient
    @PatientUid UNIQUEIDENTIFIER,
    @Status NVARCHAR(30) = N'All'
AS
BEGIN
    SET NOCOUNT ON;
    IF @Status NOT IN (N'Completed', N'Refused', N'EnteredInError', N'All') SET @Status = N'All';
    SELECT i.ImmunizationUid, i.PatientUid, i.VaccineName, i.AdministrationDate, i.DoseNumber,
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
    SELECT i.ImmunizationUid, i.PatientUid, i.VaccineName, i.AdministrationDate, i.DoseNumber,
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
 @ClinicalFields NVARCHAR(MAX)=NULL,@AdministrationDateSpecified BIT=0,@RequestCorrelationId NVARCHAR(100)=NULL
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
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
 IF @EncounterUid IS NOT NULL AND NOT EXISTS(SELECT 1 FROM dbo.PatientEncounter WITH(UPDLOCK,HOLDLOCK) WHERE EncounterUid=@EncounterUid AND PatientUid=@PatientUid) THROW 52305,'Encounter was not found for patient.',1;
 DECLARE @ImmunizationUid UNIQUEIDENTIFIER=NEWID();
 INSERT dbo.PatientImmunization(ImmunizationUid,PatientUid,VaccineName,AdministrationDate,DoseNumber,Route,Site,LotNumber,SourceType,SourceDescription,AdministeredByName,EncounterUid,Notes,Status,CreatedBy,
 DIN,Manufacturer,ImmunizationTypeCode,Refused,AdministrationYear,AdministrationMonth,AdministrationDay,RefusalYear,RefusalMonth,RefusalDay)
 VALUES(@ImmunizationUid,@PatientUid,LTRIM(RTRIM(@VaccineName)),@AdministrationDate,@DoseNumber,NULLIF(LTRIM(RTRIM(@Route)),N''),NULLIF(LTRIM(RTRIM(@Site)),N''),NULLIF(LTRIM(RTRIM(@LotNumber)),N''),@SourceType,
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
 @ClinicalFields NVARCHAR(MAX)=NULL,@AdministrationDateSpecified BIT=0,@RequestCorrelationId NVARCHAR(100)=NULL
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
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
 IF @EncounterUid IS NOT NULL AND NOT EXISTS(SELECT 1 FROM dbo.PatientEncounter WITH(UPDLOCK,HOLDLOCK) WHERE EncounterUid=@EncounterUid AND PatientUid=@PatientUid) THROW 52305,'Encounter was not found for patient.',1;
 UPDATE dbo.PatientImmunization SET VaccineName=LTRIM(RTRIM(@VaccineName)),AdministrationDate=@AdministrationDate,DoseNumber=@DoseNumber,
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
CREATE OR ALTER PROCEDURE dbo.PatientImmunization_MarkEnteredInError
 @PatientUid UNIQUEIDENTIFIER,@ImmunizationUid UNIQUEIDENTIFIER,@Reason NVARCHAR(500),@ExpectedRowVersion BINARY(8)=NULL,@Actor BIGINT,
 @RequestCorrelationId NVARCHAR(100)=NULL
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 IF @ExpectedRowVersion IS NULL THROW 52300,'A current RowVersion is required.',1;
 IF NULLIF(LTRIM(RTRIM(@Reason)),N'') IS NULL THROW 52300,'An entered-in-error reason is required.',1;
 BEGIN TRY
 IF @Actor IS NULL OR NOT EXISTS(SELECT 1 FROM dbo.ApplicationUser WHERE UserId=@Actor AND IsActive=1) THROW 52306,'Active clinical actor was not found.',1;
 DECLARE @TenantUid UNIQUEIDENTIFIER=(SELECT TenantUid FROM dbo.TenantDatabaseIdentity);
 IF @TenantUid IS NULL THROW 52306,'Trusted tenant identity is required.',1;
 SET @RequestCorrelationId=COALESCE(NULLIF(LTRIM(RTRIM(@RequestCorrelationId)),N''),CONVERT(NVARCHAR(100),NEWID()));
 BEGIN TRANSACTION;
 DECLARE @PatientId BIGINT=(SELECT PatientId FROM dbo.Patient WITH(UPDLOCK,HOLDLOCK) WHERE PatientUid=@PatientUid AND IsDeleted=0);
 IF @PatientId IS NULL BEGIN ROLLBACK; RETURN; END;
 DECLARE @Current BINARY(8),@Status NVARCHAR(30),@OldValue NVARCHAR(MAX),@NewValue NVARCHAR(MAX);
 SELECT @Current=RowVersion,@Status=Status FROM dbo.PatientImmunization WITH(UPDLOCK,HOLDLOCK) WHERE PatientUid=@PatientUid AND ImmunizationUid=@ImmunizationUid;
 IF @Current IS NULL BEGIN ROLLBACK; RETURN; END;
 IF @Current<>@ExpectedRowVersion THROW 52308,'Immunization changed. Reload and review.',1;
 IF @Status=N'EnteredInError' THROW 52309,'Immunization is already entered in error.',1;
 SET @OldValue=(SELECT @TenantUid AS TenantUid,@RequestCorrelationId AS RequestCorrelationId,i.* FROM dbo.PatientImmunization i WHERE i.PatientUid=@PatientUid AND i.ImmunizationUid=@ImmunizationUid FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
 UPDATE dbo.PatientImmunization SET Status=N'EnteredInError',UpdatedAtUtc=SYSUTCDATETIME(),UpdatedBy=@Actor,
 EnteredInErrorAtUtc=SYSUTCDATETIME(),EnteredInErrorBy=@Actor,EnteredInErrorReason=LTRIM(RTRIM(@Reason))
 WHERE PatientUid=@PatientUid AND ImmunizationUid=@ImmunizationUid AND RowVersion=@ExpectedRowVersion;
 IF @@ROWCOUNT<>1 THROW 52308,'Immunization changed.',1;
 SET @NewValue=(SELECT @TenantUid AS TenantUid,@RequestCorrelationId AS RequestCorrelationId,i.* FROM dbo.PatientImmunization i WHERE i.PatientUid=@PatientUid AND i.ImmunizationUid=@ImmunizationUid FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
 INSERT dbo.AuditLog(UserId,PatientId,PatientUid,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt,
 AuditEventUid,EventCategory,ResourceType,ResourceUid,Outcome,RequestCorrelationId,SourceApplication)
 VALUES(@Actor,@PatientId,@PatientUid,N'ImmunizationEnteredInError',N'PatientImmunization',CONVERT(NVARCHAR(100),@ImmunizationUid),@OldValue,@NewValue,SYSUTCDATETIME(),
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
