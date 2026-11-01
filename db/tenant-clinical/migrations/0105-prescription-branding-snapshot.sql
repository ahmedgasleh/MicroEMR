-- Only future finalizations use version 3; existing immutable artifacts are never updated.
SET XACT_ABORT ON;
GO
CREATE OR ALTER PROCEDURE dbo.PatientPrescription_SignAndFinalize @PatientUid UNIQUEIDENTIFIER,@PrescriptionUid UNIQUEIDENTIFIER,@ActorUserId BIGINT,@RowVersion VARBINARY(MAX),@ConfirmSignature BIT,@PickupReviewed bit=0 AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;
 IF @RowVersion IS NULL OR DATALENGTH(@RowVersion)<>8 THROW 51504,'A reviewed eight-byte row version is required.',1;
 IF ISNULL(@ConfirmSignature,0)<>1 THROW 51506,'Explicit signature confirmation is required.',1;
 DECLARE @SignedAt DATETIME2(0)=SYSUTCDATETIME();DECLARE @FirstName nvarchar(100),@LastName nvarchar(100),@Ohip nvarchar(50); DECLARE @PatientId BIGINT,@ProviderUid UNIQUEIDENTIFIER,@ProviderName NVARCHAR(200),@Credential NVARCHAR(200),@ArtifactUid UNIQUEIDENTIFIER=NEWID(),@Original UNIQUEIDENTIFIER,@Html NVARCHAR(MAX),@OldValue nvarchar(max),@NewValue nvarchar(max),@OriginalOld nvarchar(max),@OriginalNew nvarchar(max);
 BEGIN TRANSACTION;
 SELECT @FirstName=p.FirstName,@LastName=p.LastName,@Ohip=p.BillingNumber,@ProviderUid=p.ProviderUid,@ProviderName=p.DisplayName,@Credential=CONCAT(p.ProviderType,CASE WHEN p.BillingNumber IS NULL THEN N'' ELSE N' | '+p.BillingNumber END) FROM dbo.ApplicationUser u JOIN dbo.Provider p ON p.ProviderId=u.ProviderId AND p.IsActive=1 WHERE u.UserId=@ActorUserId AND u.IsActive=1;
 SELECT @PatientId=p.PatientId,@Original=x.SupersedesPrescriptionUid FROM dbo.PatientPrescription x WITH(UPDLOCK,HOLDLOCK) JOIN dbo.Patient p ON p.PatientUid=x.PatientUid AND p.IsDeleted=0 WHERE x.PatientUid=@PatientUid AND x.PrescriptionUid=@PrescriptionUid AND x.Status=N'Draft' AND x.PrescriberUserId=@ActorUserId AND x.RowVersion=@RowVersion;
 IF @ProviderUid IS NULL BEGIN ROLLBACK;THROW 51502,'An active mapped provider is required to prescribe.',1;END;
 IF @PatientId IS NULL BEGIN ROLLBACK;THROW 51504,'Draft is stale, immutable, missing, or belongs to another prescriber.',1;END;
 IF @Original IS NOT NULL AND EXISTS(SELECT 1 FROM dbo.PatientPrescription WHERE PatientUid=@PatientUid AND PrescriptionUid IN(@PrescriptionUid,@Original) AND DispensingLocationSystem IS NOT NULL)
 AND NOT EXISTS(SELECT 1 FROM dbo.PatientPrescription WHERE PatientUid=@PatientUid AND PrescriptionUid=@PrescriptionUid AND DispensingLocationReferenceReviewed=1)
 BEGIN ROLLBACK;THROW 53029,'Review dispensing location reference in Edit and save before finalizing this correction.',1;END;
 IF @Original IS NOT NULL AND EXISTS(SELECT 1 FROM dbo.PatientPrescription WHERE PatientUid=@PatientUid AND PrescriptionUid IN(@PrescriptionUid,@Original) AND ProtocolSystem IS NOT NULL)
 AND NOT EXISTS(SELECT 1 FROM dbo.PatientPrescription WHERE PatientUid=@PatientUid AND PrescriptionUid=@PrescriptionUid AND ProtocolReferenceReviewed=1)
 BEGIN ROLLBACK;THROW 53027,'Review protocol reference in Edit and save before finalizing this correction.',1;END;
 IF @Original IS NOT NULL AND EXISTS(SELECT 1 FROM dbo.PatientPrescription WHERE PatientUid=@PatientUid AND PrescriptionUid IN(@PrescriptionUid,@Original) AND ReasonCodeSystem IS NOT NULL)
 AND NOT EXISTS(SELECT 1 FROM dbo.PatientPrescription WHERE PatientUid=@PatientUid AND PrescriptionUid=@PrescriptionUid AND ReasonCodingReviewed=1)
 BEGIN ROLLBACK;THROW 53025,'Review coded reason in Edit and save before finalizing this correction.',1;END;
 IF @Original IS NOT NULL AND EXISTS(SELECT 1 FROM dbo.PatientPrescription WHERE PatientUid=@PatientUid AND PrescriptionUid IN(@PrescriptionUid,@Original) AND TargetDispensingFacilityAddress IS NOT NULL)
 AND NOT EXISTS(SELECT 1 FROM dbo.PatientPrescription WHERE PatientUid=@PatientUid AND PrescriptionUid=@PrescriptionUid AND FacilityAddressReviewed=1)
 BEGIN ROLLBACK;THROW 53023,'Review target dispensing facility address in Edit and save before finalizing this correction.',1;END;
 IF @Original IS NOT NULL AND EXISTS(SELECT 1 FROM dbo.PatientPrescription WHERE PatientUid=@PatientUid AND PrescriptionUid IN(@PrescriptionUid,@Original) AND TargetDispensingFacilityName IS NOT NULL)
 AND NOT EXISTS(SELECT 1 FROM dbo.PatientPrescription WHERE PatientUid=@PatientUid AND PrescriptionUid=@PrescriptionUid AND FacilityNameReviewed=1)
 BEGIN ROLLBACK;THROW 53021,'Review target dispensing facility name in Edit and save before finalizing this correction.',1;END;
 IF @Original IS NOT NULL AND ISNULL(@PickupReviewed,0)<>1 AND EXISTS(
  SELECT 1 FROM dbo.PatientPrescription WHERE PatientUid=@PatientUid AND PrescriptionUid IN(@PrescriptionUid,@Original) AND EarliestPickup IS NOT NULL)
 BEGIN ROLLBACK;THROW 51504,'Review earliest pickup before finalizing this correction.',1;END;
 -- Freeze the canonical tenant-local header while the existing finalization transaction is active.
 DECLARE @Branding nvarchar(max);
 IF EXISTS(SELECT 1 FROM dbo.ClinicBrandingLogo WITH(HOLDLOCK) WHERE PngBytes IS NOT NULL AND
   (DATALENGTH(PngBytes)<45 OR DATALENGTH(PngBytes)>1048576 OR SUBSTRING(PngBytes,1,8)<>0x89504E470D0A1A0A OR
    SUBSTRING(PngBytes,13,4)<>0x49484452 OR SUBSTRING(PngBytes,DATALENGTH(PngBytes)-7,4)<>0x49454E44 OR
    CONVERT(int,SUBSTRING(PngBytes,17,4))<>Width OR CONVERT(int,SUBSTRING(PngBytes,21,4))<>Height))
 BEGIN ROLLBACK;THROW 51813,'Stored clinic branding is not a bounded canonical PNG.',1;END;
 SELECT @Branding=(SELECT 1 Version,COALESCE(NULLIF(LTRIM(RTRIM(c.LegalName)),N''),N'Clinic') Name,
   c.AddressLine1,c.AddressLine2,c.City,c.ProvinceState Province,c.PostalCode,c.Phone,c.Fax,c.Email,c.Country,
   l.PngBytes PngBase64,l.Width,l.Height
   FROM (VALUES(1)) AS singleton(Id)
   LEFT JOIN dbo.ClinicProfile c WITH(HOLDLOCK) ON c.ClinicProfileId=singleton.Id
   LEFT JOIN dbo.ClinicBrandingLogo l WITH(HOLDLOCK) ON l.ClinicBrandingLogoId=singleton.Id
   FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
 SELECT @OldValue=(SELECT * FROM dbo.PatientPrescription WHERE PatientUid=@PatientUid AND PrescriptionUid=@PrescriptionUid FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
 SELECT @Html=(SELECT 2 PrescriptionOutputVersion,1 SignatureVersion,1 RefillInstructionsVersion,
 CONCAT(p.FirstName,N' ',p.LastName) PatientName,p.DateOfBirth,p.HealthCardNumber,p.PhoneNumber PatientPhone,
 p.AddressLine1 PatientAddressLine1,p.AddressLine2 PatientAddressLine2,p.City PatientCity,p.Province PatientProvince,p.PostalCode PatientPostalCode,
 c.LegalName ClinicName,c.Phone ClinicPhone,c.AddressLine1 ClinicAddressLine1,c.AddressLine2 ClinicAddressLine2,c.City ClinicCity,c.ProvinceState ClinicProvince,c.PostalCode ClinicPostalCode,
 COALESCE(dbo.PrescriptionDateText(JSON_QUERY(x.DateFields,'$.WrittenDate')),CONVERT(varchar(10),x.PrescribedDate,23)) PrescribedDate,COALESCE(NULLIF(dbo.PrescriptionDateText(JSON_QUERY(x.DateFields,'$.StartDate')),''),CONVERT(varchar(10),x.StartDate,23)) StartDate,1 DatePrecisionVersion,JSON_QUERY(x.DateFields) DateFields,1 GenericNameVersion,JSON_QUERY(CASE WHEN x.DispensingLocationSystem IS NULL THEN NULL ELSE (SELECT x.DispensingLocationSystem [system],x.DispensingLocationIdentifier identifier FOR JSON PATH,WITHOUT_ARRAY_WRAPPER) END) DispensingLocationReference,x.DispensingLocationReferenceReviewed,1 ProtocolReferenceVersion,JSON_QUERY(CASE WHEN x.ProtocolSystem IS NULL THEN NULL ELSE (SELECT x.ProtocolSystem [system],x.ProtocolIdentifier identifier FOR JSON PATH,WITHOUT_ARRAY_WRAPPER) END) ProtocolReference,x.ProtocolReferenceReviewed,1 ReasonCodingVersion,JSON_QUERY(CASE WHEN x.ReasonCodeSystem IS NULL THEN NULL ELSE (SELECT x.ReasonCodeSystem [system],x.ReasonCode code,x.ReasonCodeVersion version,x.ReasonCodeDisplay display FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER) END) ReasonCoding,x.ReasonCodingReviewed,1 TargetDispensingFacilityAddressVersion,x.TargetDispensingFacilityAddress,x.FacilityAddressReviewed,1 TargetDispensingFacilityNameVersion,x.TargetDispensingFacilityName,x.FacilityNameReviewed,1 EarliestPickupVersion,JSON_QUERY(x.EarliestPickup) EarliestPickup,@PickupReviewed EarliestPickupReviewed,x.GenericName,x.ProductDisplayText,x.PaperCopyRequiredBeforeDispense,x.MinimumDispenseIntervalDays,x.SubstitutionProhibited,x.TreatmentType,@FirstName PrescriberFirstName,@LastName PrescriberLastName,@Ohip PrescriberOhip,x.MedicationForm,x.NotesToPharmacist,x.StrengthValue,x.StrengthUnit,x.DoseAmount,x.DoseUnit,x.Route,x.FrequencyDisplay,x.Prn,x.Directions,
 x.Quantity,x.QuantityUnit,x.AuthorizedRepeats,x.InitialDaysSupply,x.RefillQuantity,x.RefillQuantityUnit,x.RefillDaysSupply,x.Indication,
 x.CreatedBy,x.CreatedAtUtc,creator.DisplayName CreatedByDisplayName,
 @ProviderName PrescriberDisplayName,@Credential PrescriberCredential,@ActorUserId SignerUserId,@ProviderUid SignerProviderUid,@SignedAt SignedAtUtc,x.PrescriptionUid
 FROM dbo.PatientPrescription x JOIN dbo.Patient p ON p.PatientUid=x.PatientUid
 LEFT JOIN dbo.ApplicationUser creator ON creator.UserId=x.CreatedBy
 LEFT JOIN dbo.ClinicProfile c ON c.ClinicProfileId=1
 WHERE x.PatientUid=@PatientUid AND x.PrescriptionUid=@PrescriptionUid FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
 SET @Html=JSON_MODIFY(@Html,'$.PrescriptionOutputVersion',3);
 SET @Html=JSON_MODIFY(@Html,'$.PrescriptionContentVersion',2);
 SET @Html=JSON_MODIFY(@Html,'$.Branding',JSON_QUERY(@Branding));
 UPDATE dbo.PatientPrescription SET PrescriberFirstNameSnapshot=@FirstName,PrescriberLastNameSnapshot=@LastName,PrescriberOhipSnapshot=@Ohip,Status=N'Finalized',PrescriberProviderUid=@ProviderUid,PrescriberDisplayNameSnapshot=@ProviderName,PrescriberCredentialSnapshot=@Credential,ProductDisplaySnapshot=ProductDisplayText,FinalizedBy=@ActorUserId,FinalizedAtUtc=@SignedAt,ArtifactUid=@ArtifactUid WHERE PrescriptionUid=@PrescriptionUid;
 INSERT dbo.PatientPrescriptionArtifact(ArtifactUid,PrescriptionUid,PatientUid,ArtifactJson,CreatedBy) VALUES(@ArtifactUid,@PrescriptionUid,@PatientUid,@Html,@ActorUserId);
 SELECT @NewValue=(SELECT * FROM dbo.PatientPrescription WHERE PatientUid=@PatientUid AND PrescriptionUid=@PrescriptionUid FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
 SET @NewValue=JSON_MODIFY(@NewValue,'$.EarliestPickupReviewed',ISNULL(@PickupReviewed,0));
 INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt) VALUES(@ActorUserId,@PatientId,N'PrescriptionFinalized',N'PatientPrescription',CONVERT(nvarchar(100),@PrescriptionUid),@OldValue,@NewValue,SYSUTCDATETIME());
 IF @Original IS NOT NULL BEGIN
   SELECT @OriginalOld=(SELECT * FROM dbo.PatientPrescription WITH(UPDLOCK,HOLDLOCK) WHERE PatientUid=@PatientUid AND PrescriptionUid=@Original FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
   UPDATE dbo.PatientPrescription SET Status=N'Superseded',SupersededByPrescriptionUid=@PrescriptionUid WHERE PatientUid=@PatientUid AND PrescriptionUid=@Original AND Status=N'Finalized';
   IF @@ROWCOUNT<>1 BEGIN ROLLBACK;THROW 51505,'The correction source is no longer eligible for supersession.',1;END;
   SELECT @OriginalNew=(SELECT * FROM dbo.PatientPrescription WHERE PatientUid=@PatientUid AND PrescriptionUid=@Original FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
   INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt) VALUES(@ActorUserId,@PatientId,N'PrescriptionSuperseded',N'PatientPrescription',CONVERT(nvarchar(100),@Original),@OriginalOld,@OriginalNew,SYSUTCDATETIME());
 END;
 COMMIT;EXEC dbo.PatientPrescription_GetByUid @PatientUid,@PrescriptionUid;
END;
GO
CREATE OR ALTER PROCEDURE dbo.PatientPrescription_Finalize @PatientUid UNIQUEIDENTIFIER,@PrescriptionUid UNIQUEIDENTIFIER,@ActorUserId BIGINT,@RowVersion VARBINARY(MAX),@PickupReviewed bit=0 AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;IF @RowVersion IS NULL OR DATALENGTH(@RowVersion)<>8 THROW 51504,'A reviewed eight-byte row version is required.',1; DECLARE @FirstName nvarchar(100),@LastName nvarchar(100),@Ohip nvarchar(50); DECLARE @PatientId BIGINT,@ProviderUid UNIQUEIDENTIFIER,@ProviderName NVARCHAR(200),@Credential NVARCHAR(200),@ArtifactUid UNIQUEIDENTIFIER=NEWID(),@Original UNIQUEIDENTIFIER,@Html NVARCHAR(MAX),@OldValue nvarchar(max),@NewValue nvarchar(max),@OriginalOld nvarchar(max),@OriginalNew nvarchar(max);
 BEGIN TRANSACTION;
 SELECT @FirstName=p.FirstName,@LastName=p.LastName,@Ohip=p.BillingNumber,@ProviderUid=p.ProviderUid,@ProviderName=p.DisplayName,@Credential=CONCAT(p.ProviderType,CASE WHEN p.BillingNumber IS NULL THEN N'' ELSE N' | '+p.BillingNumber END) FROM dbo.ApplicationUser u JOIN dbo.Provider p ON p.ProviderId=u.ProviderId AND p.IsActive=1 WHERE u.UserId=@ActorUserId AND u.IsActive=1;
 SELECT @PatientId=p.PatientId,@Original=x.SupersedesPrescriptionUid FROM dbo.PatientPrescription x WITH(UPDLOCK,HOLDLOCK) JOIN dbo.Patient p ON p.PatientUid=x.PatientUid AND p.IsDeleted=0 WHERE x.PatientUid=@PatientUid AND x.PrescriptionUid=@PrescriptionUid AND x.Status=N'Draft' AND x.PrescriberUserId=@ActorUserId AND x.RowVersion=@RowVersion;
 IF @ProviderUid IS NULL BEGIN ROLLBACK;THROW 51502,'An active mapped provider is required to prescribe.',1;END;
 IF @PatientId IS NULL BEGIN ROLLBACK;THROW 51504,'Draft is stale, immutable, missing, or belongs to another prescriber.',1;END;
 IF @Original IS NOT NULL AND EXISTS(SELECT 1 FROM dbo.PatientPrescription WHERE PatientUid=@PatientUid AND PrescriptionUid IN(@PrescriptionUid,@Original) AND DispensingLocationSystem IS NOT NULL)
 AND NOT EXISTS(SELECT 1 FROM dbo.PatientPrescription WHERE PatientUid=@PatientUid AND PrescriptionUid=@PrescriptionUid AND DispensingLocationReferenceReviewed=1)
 BEGIN ROLLBACK;THROW 53029,'Review dispensing location reference in Edit and save before finalizing this correction.',1;END;
 IF @Original IS NOT NULL AND EXISTS(SELECT 1 FROM dbo.PatientPrescription WHERE PatientUid=@PatientUid AND PrescriptionUid IN(@PrescriptionUid,@Original) AND ProtocolSystem IS NOT NULL)
 AND NOT EXISTS(SELECT 1 FROM dbo.PatientPrescription WHERE PatientUid=@PatientUid AND PrescriptionUid=@PrescriptionUid AND ProtocolReferenceReviewed=1)
 BEGIN ROLLBACK;THROW 53027,'Review protocol reference in Edit and save before finalizing this correction.',1;END;
 IF @Original IS NOT NULL AND EXISTS(SELECT 1 FROM dbo.PatientPrescription WHERE PatientUid=@PatientUid AND PrescriptionUid IN(@PrescriptionUid,@Original) AND ReasonCodeSystem IS NOT NULL)
 AND NOT EXISTS(SELECT 1 FROM dbo.PatientPrescription WHERE PatientUid=@PatientUid AND PrescriptionUid=@PrescriptionUid AND ReasonCodingReviewed=1)
 BEGIN ROLLBACK;THROW 53025,'Review coded reason in Edit and save before finalizing this correction.',1;END;
 IF @Original IS NOT NULL AND EXISTS(SELECT 1 FROM dbo.PatientPrescription WHERE PatientUid=@PatientUid AND PrescriptionUid IN(@PrescriptionUid,@Original) AND TargetDispensingFacilityAddress IS NOT NULL)
 AND NOT EXISTS(SELECT 1 FROM dbo.PatientPrescription WHERE PatientUid=@PatientUid AND PrescriptionUid=@PrescriptionUid AND FacilityAddressReviewed=1)
 BEGIN ROLLBACK;THROW 53023,'Review target dispensing facility address in Edit and save before finalizing this correction.',1;END;
 IF @Original IS NOT NULL AND EXISTS(SELECT 1 FROM dbo.PatientPrescription WHERE PatientUid=@PatientUid AND PrescriptionUid IN(@PrescriptionUid,@Original) AND TargetDispensingFacilityName IS NOT NULL)
 AND NOT EXISTS(SELECT 1 FROM dbo.PatientPrescription WHERE PatientUid=@PatientUid AND PrescriptionUid=@PrescriptionUid AND FacilityNameReviewed=1)
 BEGIN ROLLBACK;THROW 53021,'Review target dispensing facility name in Edit and save before finalizing this correction.',1;END;
 IF @Original IS NOT NULL AND ISNULL(@PickupReviewed,0)<>1 AND EXISTS(
  SELECT 1 FROM dbo.PatientPrescription WHERE PatientUid=@PatientUid AND PrescriptionUid IN(@PrescriptionUid,@Original) AND EarliestPickup IS NOT NULL)
 BEGIN ROLLBACK;THROW 51504,'Review earliest pickup before finalizing this correction.',1;END;
 -- Freeze the canonical tenant-local header while the existing finalization transaction is active.
 DECLARE @Branding nvarchar(max);
 IF EXISTS(SELECT 1 FROM dbo.ClinicBrandingLogo WITH(HOLDLOCK) WHERE PngBytes IS NOT NULL AND
   (DATALENGTH(PngBytes)<45 OR DATALENGTH(PngBytes)>1048576 OR SUBSTRING(PngBytes,1,8)<>0x89504E470D0A1A0A OR
    SUBSTRING(PngBytes,13,4)<>0x49484452 OR SUBSTRING(PngBytes,DATALENGTH(PngBytes)-7,4)<>0x49454E44 OR
    CONVERT(int,SUBSTRING(PngBytes,17,4))<>Width OR CONVERT(int,SUBSTRING(PngBytes,21,4))<>Height))
 BEGIN ROLLBACK;THROW 51813,'Stored clinic branding is not a bounded canonical PNG.',1;END;
 SELECT @Branding=(SELECT 1 Version,COALESCE(NULLIF(LTRIM(RTRIM(c.LegalName)),N''),N'Clinic') Name,
   c.AddressLine1,c.AddressLine2,c.City,c.ProvinceState Province,c.PostalCode,c.Phone,c.Fax,c.Email,c.Country,
   l.PngBytes PngBase64,l.Width,l.Height
   FROM (VALUES(1)) AS singleton(Id)
   LEFT JOIN dbo.ClinicProfile c WITH(HOLDLOCK) ON c.ClinicProfileId=singleton.Id
   LEFT JOIN dbo.ClinicBrandingLogo l WITH(HOLDLOCK) ON l.ClinicBrandingLogoId=singleton.Id
   FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
 SELECT @OldValue=(SELECT * FROM dbo.PatientPrescription WHERE PatientUid=@PatientUid AND PrescriptionUid=@PrescriptionUid FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
 SELECT @Html=(SELECT CONCAT(p.FirstName,N' ',p.LastName) PatientName,p.DateOfBirth,COALESCE(dbo.PrescriptionDateText(JSON_QUERY(x.DateFields,'$.WrittenDate')),CONVERT(varchar(10),x.PrescribedDate,23)) PrescribedDate,COALESCE(NULLIF(dbo.PrescriptionDateText(JSON_QUERY(x.DateFields,'$.StartDate')),''),CONVERT(varchar(10),x.StartDate,23)) StartDate,1 DatePrecisionVersion,JSON_QUERY(x.DateFields) DateFields,1 GenericNameVersion,JSON_QUERY(CASE WHEN x.DispensingLocationSystem IS NULL THEN NULL ELSE (SELECT x.DispensingLocationSystem [system],x.DispensingLocationIdentifier identifier FOR JSON PATH,WITHOUT_ARRAY_WRAPPER) END) DispensingLocationReference,x.DispensingLocationReferenceReviewed,1 ProtocolReferenceVersion,JSON_QUERY(CASE WHEN x.ProtocolSystem IS NULL THEN NULL ELSE (SELECT x.ProtocolSystem [system],x.ProtocolIdentifier identifier FOR JSON PATH,WITHOUT_ARRAY_WRAPPER) END) ProtocolReference,x.ProtocolReferenceReviewed,1 ReasonCodingVersion,JSON_QUERY(CASE WHEN x.ReasonCodeSystem IS NULL THEN NULL ELSE (SELECT x.ReasonCodeSystem [system],x.ReasonCode code,x.ReasonCodeVersion version,x.ReasonCodeDisplay display FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER) END) ReasonCoding,x.ReasonCodingReviewed,1 TargetDispensingFacilityAddressVersion,x.TargetDispensingFacilityAddress,x.FacilityAddressReviewed,1 TargetDispensingFacilityNameVersion,x.TargetDispensingFacilityName,x.FacilityNameReviewed,1 EarliestPickupVersion,JSON_QUERY(x.EarliestPickup) EarliestPickup,@PickupReviewed EarliestPickupReviewed,x.GenericName,x.ProductDisplayText,x.PaperCopyRequiredBeforeDispense,x.MinimumDispenseIntervalDays,x.SubstitutionProhibited,x.TreatmentType,@FirstName PrescriberFirstName,@LastName PrescriberLastName,@Ohip PrescriberOhip,x.StrengthValue,x.StrengthUnit,x.DoseAmount,x.DoseUnit,x.Route,x.FrequencyDisplay,x.Prn,x.Directions,x.Quantity,x.QuantityUnit,x.AuthorizedRepeats,x.InitialDaysSupply,x.RefillQuantity,x.RefillQuantityUnit,x.RefillDaysSupply,1 RefillInstructionsVersion,x.Indication,@ProviderName PrescriberDisplayName,@Credential PrescriberCredential,x.PrescriptionUid FOR JSON PATH,WITHOUT_ARRAY_WRAPPER) FROM dbo.PatientPrescription x JOIN dbo.Patient p ON p.PatientUid=x.PatientUid WHERE x.PrescriptionUid=@PrescriptionUid;
 SET @Html=JSON_MODIFY(@Html,'$.PrescriptionOutputVersion',3);
 SET @Html=JSON_MODIFY(@Html,'$.PrescriptionContentVersion',1);
 SET @Html=JSON_MODIFY(@Html,'$.Branding',JSON_QUERY(@Branding));
 UPDATE dbo.PatientPrescription SET PrescriberFirstNameSnapshot=@FirstName,PrescriberLastNameSnapshot=@LastName,PrescriberOhipSnapshot=@Ohip,Status=N'Finalized',PrescriberProviderUid=@ProviderUid,PrescriberDisplayNameSnapshot=@ProviderName,PrescriberCredentialSnapshot=@Credential,ProductDisplaySnapshot=ProductDisplayText,FinalizedBy=@ActorUserId,FinalizedAtUtc=SYSUTCDATETIME(),ArtifactUid=@ArtifactUid WHERE PrescriptionUid=@PrescriptionUid;
 INSERT dbo.PatientPrescriptionArtifact(ArtifactUid,PrescriptionUid,PatientUid,ArtifactJson,CreatedBy) VALUES(@ArtifactUid,@PrescriptionUid,@PatientUid,@Html,@ActorUserId);
 SELECT @NewValue=(SELECT * FROM dbo.PatientPrescription WHERE PatientUid=@PatientUid AND PrescriptionUid=@PrescriptionUid FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
 SET @NewValue=JSON_MODIFY(@NewValue,'$.EarliestPickupReviewed',ISNULL(@PickupReviewed,0));
 INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt) VALUES(@ActorUserId,@PatientId,N'PrescriptionFinalized',N'PatientPrescription',CONVERT(nvarchar(100),@PrescriptionUid),@OldValue,@NewValue,SYSUTCDATETIME());
 IF @Original IS NOT NULL BEGIN
   SELECT @OriginalOld=(SELECT * FROM dbo.PatientPrescription WITH(UPDLOCK,HOLDLOCK) WHERE PatientUid=@PatientUid AND PrescriptionUid=@Original FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
   UPDATE dbo.PatientPrescription SET Status=N'Superseded',SupersededByPrescriptionUid=@PrescriptionUid WHERE PatientUid=@PatientUid AND PrescriptionUid=@Original AND Status=N'Finalized';
   IF @@ROWCOUNT<>1 BEGIN ROLLBACK;THROW 51505,'The correction source is no longer eligible for supersession.',1;END;
   SELECT @OriginalNew=(SELECT * FROM dbo.PatientPrescription WHERE PatientUid=@PatientUid AND PrescriptionUid=@Original FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
   INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt) VALUES(@ActorUserId,@PatientId,N'PrescriptionSuperseded',N'PatientPrescription',CONVERT(nvarchar(100),@Original),@OriginalOld,@OriginalNew,SYSUTCDATETIME());
 END;
 COMMIT;EXEC dbo.PatientPrescription_GetByUid @PatientUid,@PrescriptionUid;
END;
GO

