/* Step 05: current Provider directory contact details. */
SET XACT_ABORT ON;
GO
ALTER TABLE dbo.Provider ADD
    OrganizationName NVARCHAR(200) NULL,
    Phone NVARCHAR(30) NULL,
    Fax NVARCHAR(30) NULL;
GO

CREATE OR ALTER PROCEDURE dbo.Provider_List @Status NVARCHAR(10)=N'Active' AS
BEGIN
    SET NOCOUNT ON;
    IF @Status NOT IN(N'Active',N'Inactive',N'All') THROW 51703,'Provider status filter is invalid.',1;
    SELECT p.ProviderUid,p.FirstName,p.LastName,p.DisplayName,p.ProviderType,p.BillingNumber,p.Specialty,
           p.OrganizationName,p.Phone,p.Fax,p.IsActive,p.CreatedAt,p.CreatedBy,p.UpdatedAt,p.UpdatedBy,
           u.UserUid LinkedApplicationUserUid,u.DisplayName LinkedApplicationUserDisplayName,
           u.Email LinkedApplicationUserEmail,p.RowVersion
    FROM dbo.Provider p LEFT JOIN dbo.ApplicationUser u ON u.ProviderId=p.ProviderId
    WHERE @Status=N'All' OR (@Status=N'Active' AND p.IsActive=1) OR (@Status=N'Inactive' AND p.IsActive=0)
    ORDER BY p.DisplayName,p.ProviderUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.Provider_Get @ProviderUid UNIQUEIDENTIFIER AS
BEGIN
    SET NOCOUNT ON;
    SELECT p.ProviderUid,p.FirstName,p.LastName,p.DisplayName,p.ProviderType,p.BillingNumber,p.Specialty,
           p.OrganizationName,p.Phone,p.Fax,p.IsActive,p.CreatedAt,p.CreatedBy,p.UpdatedAt,p.UpdatedBy,
           u.UserUid LinkedApplicationUserUid,u.DisplayName LinkedApplicationUserDisplayName,
           u.Email LinkedApplicationUserEmail,p.RowVersion
    FROM dbo.Provider p LEFT JOIN dbo.ApplicationUser u ON u.ProviderId=p.ProviderId
    WHERE p.ProviderUid=@ProviderUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.Provider_Create
    @FirstName NVARCHAR(100),@LastName NVARCHAR(100),@DisplayName NVARCHAR(200),
    @ProviderType NVARCHAR(50),@BillingNumber NVARCHAR(50)=NULL,@Specialty NVARCHAR(100)=NULL,
    @OrganizationName NVARCHAR(200)=NULL,@Phone NVARCHAR(30)=NULL,@Fax NVARCHAR(30)=NULL,
    @ActorUserId BIGINT AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    IF NOT EXISTS(SELECT 1 FROM dbo.ApplicationUser WHERE UserId=@ActorUserId AND IsActive=1)
        THROW 51704,'Active actor is required.',1;
    IF NULLIF(LTRIM(RTRIM(@FirstName)),N'') IS NULL OR NULLIF(LTRIM(RTRIM(@LastName)),N'') IS NULL
       OR NULLIF(LTRIM(RTRIM(@DisplayName)),N'') IS NULL OR NULLIF(LTRIM(RTRIM(@ProviderType)),N'') IS NULL
        THROW 51705,'Provider name and type are required.',1;
    DECLARE @Uid UNIQUEIDENTIFIER=NEWID();
    BEGIN TRANSACTION;
    INSERT dbo.Provider(ProviderUid,FirstName,LastName,DisplayName,ProviderType,BillingNumber,Specialty,
                        OrganizationName,Phone,Fax,IsActive,CreatedAt,CreatedBy)
    VALUES(@Uid,LTRIM(RTRIM(@FirstName)),LTRIM(RTRIM(@LastName)),LTRIM(RTRIM(@DisplayName)),
           LTRIM(RTRIM(@ProviderType)),NULLIF(LTRIM(RTRIM(@BillingNumber)),N''),
           NULLIF(LTRIM(RTRIM(@Specialty)),N''),NULLIF(LTRIM(RTRIM(@OrganizationName)),N''),
           NULLIF(LTRIM(RTRIM(@Phone)),N''),NULLIF(LTRIM(RTRIM(@Fax)),N''),1,SYSUTCDATETIME(),@ActorUserId);
    INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,NewValue,CreatedAt)
    VALUES(@ActorUserId,NULL,N'ProviderCreated',N'Provider',CONVERT(NVARCHAR(36),@Uid),N'Status=Active',SYSUTCDATETIME());
    COMMIT;
    EXEC dbo.Provider_Get @Uid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.Provider_Update
    @ProviderUid UNIQUEIDENTIFIER,@FirstName NVARCHAR(100),@LastName NVARCHAR(100),
    @DisplayName NVARCHAR(200),@ProviderType NVARCHAR(50),@BillingNumber NVARCHAR(50)=NULL,
    @Specialty NVARCHAR(100)=NULL,@OrganizationName NVARCHAR(200)=NULL,@Phone NVARCHAR(30)=NULL,
    @Fax NVARCHAR(30)=NULL,@ExpectedRowVersion BINARY(8),@ActorUserId BIGINT AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    IF NULLIF(LTRIM(RTRIM(@FirstName)),N'') IS NULL OR NULLIF(LTRIM(RTRIM(@LastName)),N'') IS NULL
       OR NULLIF(LTRIM(RTRIM(@DisplayName)),N'') IS NULL OR NULLIF(LTRIM(RTRIM(@ProviderType)),N'') IS NULL
        THROW 51705,'Provider name and type are required.',1;
    BEGIN TRANSACTION;
    IF NOT EXISTS(SELECT 1 FROM dbo.ApplicationUser WHERE UserId=@ActorUserId AND IsActive=1)
        THROW 51704,'Active actor is required.',1;
    DECLARE @Current BINARY(8);
    SELECT @Current=RowVersion FROM dbo.Provider WITH(UPDLOCK,HOLDLOCK) WHERE ProviderUid=@ProviderUid;
    IF @Current IS NULL BEGIN ROLLBACK; RETURN; END;
    IF @Current<>@ExpectedRowVersion THROW 51706,'Provider changed before it could be updated.',1;
    UPDATE dbo.Provider SET
        FirstName=LTRIM(RTRIM(@FirstName)),LastName=LTRIM(RTRIM(@LastName)),
        DisplayName=LTRIM(RTRIM(@DisplayName)),ProviderType=LTRIM(RTRIM(@ProviderType)),
        BillingNumber=NULLIF(LTRIM(RTRIM(@BillingNumber)),N''),
        Specialty=NULLIF(LTRIM(RTRIM(@Specialty)),N''),
        OrganizationName=NULLIF(LTRIM(RTRIM(@OrganizationName)),N''),
        Phone=NULLIF(LTRIM(RTRIM(@Phone)),N''),Fax=NULLIF(LTRIM(RTRIM(@Fax)),N''),
        UpdatedAt=SYSUTCDATETIME(),UpdatedBy=@ActorUserId
    WHERE ProviderUid=@ProviderUid AND RowVersion=@ExpectedRowVersion;
    INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,NewValue,CreatedAt)
    VALUES(@ActorUserId,NULL,N'ProviderUpdated',N'Provider',CONVERT(NVARCHAR(36),@ProviderUid),
           N'Provider administrative fields updated',SYSUTCDATETIME());
    COMMIT;
    EXEC dbo.Provider_Get @ProviderUid;
END;
GO
