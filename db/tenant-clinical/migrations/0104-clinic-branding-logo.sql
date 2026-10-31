SET XACT_ABORT ON;
IF OBJECT_ID(N'dbo.ClinicBrandingLogo', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ClinicBrandingLogo
    (
        ClinicBrandingLogoId tinyint NOT NULL CONSTRAINT PK_ClinicBrandingLogo PRIMARY KEY
            CONSTRAINT CK_ClinicBrandingLogo_Singleton CHECK (ClinicBrandingLogoId=1),
        PngBytes varbinary(max) NULL,
        Width int NULL,
        Height int NULL,
        UpdatedAtUtc datetime2(7) NOT NULL,
        UpdatedBy bigint NOT NULL CONSTRAINT FK_ClinicBrandingLogo_User REFERENCES dbo.ApplicationUser(UserId),
        RowVersion rowversion NOT NULL,
        CONSTRAINT CK_ClinicBrandingLogo_Image CHECK
        ((PngBytes IS NULL AND Width IS NULL AND Height IS NULL) OR
         (PngBytes IS NOT NULL AND DATALENGTH(PngBytes) BETWEEN 1 AND 1048576
          AND Width IS NOT NULL AND Width BETWEEN 1 AND 2048
          AND Height IS NOT NULL AND Height BETWEEN 1 AND 2048))
    );
END;
GO
CREATE OR ALTER PROCEDURE dbo.ClinicBrandingLogo_Get
AS
BEGIN
    SET NOCOUNT ON;
    SELECT PngBytes, Width, Height, RowVersion FROM dbo.ClinicBrandingLogo WHERE ClinicBrandingLogoId=1;
END;
GO
CREATE OR ALTER PROCEDURE dbo.ClinicBrandingLogo_Save
    @PngBytes varbinary(max)=NULL,
    @Width int=NULL,
    @Height int=NULL,
    @UpdatedBy bigint,
    @ExpectedRowVersion binary(8)=NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF NOT EXISTS(SELECT 1 FROM dbo.ApplicationUser WHERE UserId=@UpdatedBy AND IsActive=1)
        THROW 51812, 'An active clinical user is required.', 1;
    BEGIN TRY
        BEGIN TRANSACTION;
        DECLARE @Exists bit=0, @Version binary(8), @OldValue nvarchar(max), @NewValue nvarchar(max);
        SELECT @Exists=1, @Version=RowVersion FROM dbo.ClinicBrandingLogo WITH(UPDLOCK,HOLDLOCK) WHERE ClinicBrandingLogoId=1;
        IF (@Exists=0 AND @ExpectedRowVersion IS NOT NULL) OR
           (@Exists=1 AND (@ExpectedRowVersion IS NULL OR @Version<>@ExpectedRowVersion))
            THROW 51811, 'Clinic branding has changed.', 1;
        SELECT @OldValue=(SELECT Width, Height, CONVERT(varchar(64),HASHBYTES('SHA2_256',PngBytes),2) AS LogoSha256
            FROM dbo.ClinicBrandingLogo WHERE ClinicBrandingLogoId=1 FOR JSON PATH,WITHOUT_ARRAY_WRAPPER,INCLUDE_NULL_VALUES);
        IF @Exists=1
            UPDATE dbo.ClinicBrandingLogo SET PngBytes=@PngBytes, Width=@Width, Height=@Height,
                UpdatedAtUtc=SYSUTCDATETIME(), UpdatedBy=@UpdatedBy WHERE ClinicBrandingLogoId=1;
        ELSE
            INSERT dbo.ClinicBrandingLogo(ClinicBrandingLogoId,PngBytes,Width,Height,UpdatedAtUtc,UpdatedBy)
            VALUES(1,@PngBytes,@Width,@Height,SYSUTCDATETIME(),@UpdatedBy);
        SELECT @NewValue=(SELECT Width, Height, CONVERT(varchar(64),HASHBYTES('SHA2_256',PngBytes),2) AS LogoSha256
            FROM dbo.ClinicBrandingLogo WHERE ClinicBrandingLogoId=1 FOR JSON PATH,WITHOUT_ARRAY_WRAPPER,INCLUDE_NULL_VALUES);
        INSERT dbo.AuditLog(UserId,ActionName,EntityName,EntityId,OldValue,NewValue)
        VALUES(@UpdatedBy,CASE WHEN @PngBytes IS NULL THEN N'Remove' WHEN @Exists=0 THEN N'Create' ELSE N'Update' END,
            N'ClinicBrandingLogo',N'1',@OldValue,@NewValue);
        COMMIT TRANSACTION;
        EXEC dbo.ClinicBrandingLogo_Get;
    END TRY
    BEGIN CATCH
        IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO
