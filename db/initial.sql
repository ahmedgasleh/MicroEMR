<!-- Core tables --->

CREATE TABLE dbo.Patient
(
    PatientId BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    PatientUid UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),

    ChartNumber NVARCHAR(50) NOT NULL,
    HealthCardNumber NVARCHAR(50) NULL,
    HealthCardVersion NVARCHAR(10) NULL,

    FirstName NVARCHAR(100) NOT NULL,
    LastName NVARCHAR(100) NOT NULL,
    DateOfBirth DATE NOT NULL,
    Sex NVARCHAR(20) NULL,

    PhoneNumber NVARCHAR(30) NULL,
    Email NVARCHAR(255) NULL,
    AddressLine1 NVARCHAR(255) NULL,
    AddressLine2 NVARCHAR(255) NULL,
    City NVARCHAR(100) NULL,
    Province NVARCHAR(50) NULL,
    PostalCode NVARCHAR(20) NULL,

    IsActive BIT NOT NULL DEFAULT 1,
    IsDeleted BIT NOT NULL DEFAULT 0,

    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy BIGINT NULL,
    UpdatedAt DATETIME2 NULL,
    UpdatedBy BIGINT NULL,

    CONSTRAINT UQ_Patient_ChartNumber UNIQUE (ChartNumber)
);

CREATE TABLE dbo.Provider
(
    ProviderId BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    ProviderUid UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),

    FirstName NVARCHAR(100) NOT NULL,
    LastName NVARCHAR(100) NOT NULL,
    DisplayName NVARCHAR(200) NOT NULL,

    ProviderType NVARCHAR(50) NOT NULL, -- Physician, Nurse, Technician
    BillingNumber NVARCHAR(50) NULL,
    Specialty NVARCHAR(100) NULL,

    IsActive BIT NOT NULL DEFAULT 1,

    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy BIGINT NULL
);

CREATE TABLE dbo.ClinicLocation
(
    LocationId BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    LocationName NVARCHAR(150) NOT NULL,
    AddressLine1 NVARCHAR(255) NULL,
    City NVARCHAR(100) NULL,
    Province NVARCHAR(50) NULL,
    IsActive BIT NOT NULL DEFAULT 1
);

CREATE TABLE dbo.ClinicResource
(
    ResourceId BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    LocationId BIGINT NULL,

    ResourceName NVARCHAR(150) NOT NULL,
    ResourceType NVARCHAR(50) NOT NULL, -- Room, Machine, Staff, Equipment

    IsBookable BIT NOT NULL DEFAULT 1,
    IsActive BIT NOT NULL DEFAULT 1,

    CONSTRAINT FK_ClinicResource_Location
        FOREIGN KEY (LocationId) REFERENCES dbo.ClinicLocation(LocationId)
);

CREATE TABLE dbo.ClinicResource
(
    ResourceId BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    LocationId BIGINT NULL,

    ResourceName NVARCHAR(150) NOT NULL,
    ResourceType NVARCHAR(50) NOT NULL, -- Room, Machine, Staff, Equipment

    IsBookable BIT NOT NULL DEFAULT 1,
    IsActive BIT NOT NULL DEFAULT 1,

    CONSTRAINT FK_ClinicResource_Location
        FOREIGN KEY (LocationId) REFERENCES dbo.ClinicLocation(LocationId)
);

<!-- Scheduling tables -->

CREATE TABLE dbo.AppointmentStatus
(
    AppointmentStatusId INT NOT NULL PRIMARY KEY,
    StatusName NVARCHAR(50) NOT NULL
);

INSERT INTO dbo.AppointmentStatus
(
    AppointmentStatusId,
    StatusName
)
VALUES
(1, 'Booked'),
(2, 'Confirmed'),
(3, 'Arrived'),
(4, 'InRoom'),
(5, 'Completed'),
(6, 'Cancelled'),
(7, 'NoShow'),
(8, 'Rescheduled');

CREATE TABLE dbo.AppointmentType
(
    AppointmentTypeId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    TypeName NVARCHAR(100) NOT NULL,
    DefaultDurationMinutes INT NOT NULL DEFAULT 15,
    ColorCode NVARCHAR(20) NULL,
    IsActive BIT NOT NULL DEFAULT 1
);

CREATE TABLE dbo.Appointment
(
    AppointmentId BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    AppointmentUid UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),

    PatientId BIGINT NOT NULL,
    ProviderId BIGINT NOT NULL,
    LocationId BIGINT NULL,

    AppointmentTypeId INT NULL,
    AppointmentStatusId INT NOT NULL DEFAULT 1,

    StartTime DATETIME2 NOT NULL,
    EndTime DATETIME2 NOT NULL,

    ReasonForVisit NVARCHAR(500) NULL,
    Notes NVARCHAR(MAX) NULL,

    IsDeleted BIT NOT NULL DEFAULT 0,

    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy BIGINT NULL,
    UpdatedAt DATETIME2 NULL,
    UpdatedBy BIGINT NULL,

    CONSTRAINT FK_Appointment_Patient
        FOREIGN KEY (PatientId) REFERENCES dbo.Patient(PatientId),

    CONSTRAINT FK_Appointment_Provider
        FOREIGN KEY (ProviderId) REFERENCES dbo.Provider(ProviderId),

    CONSTRAINT FK_Appointment_Location
        FOREIGN KEY (LocationId) REFERENCES dbo.ClinicLocation(LocationId),

    CONSTRAINT FK_Appointment_Type
        FOREIGN KEY (AppointmentTypeId) REFERENCES dbo.AppointmentType(AppointmentTypeId),

    CONSTRAINT FK_Appointment_Status
        FOREIGN KEY (AppointmentStatusId) REFERENCES dbo.AppointmentStatus(AppointmentStatusId),

    CONSTRAINT CK_Appointment_Time
        CHECK (EndTime > StartTime)
);

CREATE TABLE dbo.AppointmentResource
(
    AppointmentResourceId BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,

    AppointmentId BIGINT NOT NULL,
    ResourceId BIGINT NOT NULL,

    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),

    CONSTRAINT FK_AppointmentResource_Appointment
        FOREIGN KEY (AppointmentId) REFERENCES dbo.Appointment(AppointmentId),

    CONSTRAINT FK_AppointmentResource_Resource
        FOREIGN KEY (ResourceId) REFERENCES dbo.ClinicResource(ResourceId)
);

CREATE TABLE dbo.ProviderAvailability
(
    ProviderAvailabilityId BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,

    ProviderId BIGINT NOT NULL,
    DayOfWeek INT NOT NULL, -- 1 Sunday, 2 Monday, etc.
    StartTime TIME NOT NULL,
    EndTime TIME NOT NULL,

    SlotMinutes INT NOT NULL DEFAULT 15,
    IsActive BIT NOT NULL DEFAULT 1,

    CONSTRAINT FK_ProviderAvailability_Provider
        FOREIGN KEY (ProviderId) REFERENCES dbo.Provider(ProviderId)
);

CREATE TABLE dbo.ScheduleBlock
(
    ScheduleBlockId BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,

    ProviderId BIGINT NULL,
    ResourceId BIGINT NULL,
    LocationId BIGINT NULL,

    StartTime DATETIME2 NOT NULL,
    EndTime DATETIME2 NOT NULL,

    BlockReason NVARCHAR(255) NOT NULL, -- Lunch, Vacation, Meeting, Maintenance
    IsDeleted BIT NOT NULL DEFAULT 0,

    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy BIGINT NULL,

    CONSTRAINT FK_ScheduleBlock_Provider
        FOREIGN KEY (ProviderId) REFERENCES dbo.Provider(ProviderId),

    CONSTRAINT FK_ScheduleBlock_Resource
        FOREIGN KEY (ResourceId) REFERENCES dbo.ClinicResource(ResourceId),

    CONSTRAINT FK_ScheduleBlock_Location
        FOREIGN KEY (LocationId) REFERENCES dbo.ClinicLocation(LocationId),

    CONSTRAINT CK_ScheduleBlock_Time
        CHECK (EndTime > StartTime)
);

<!-- Patient Chart tables --->

CREATE TABLE dbo.PatientEncounter
(
    EncounterId BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    EncounterUid UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),

    PatientId BIGINT NOT NULL,
    AppointmentId BIGINT NULL,
    ProviderId BIGINT NULL,

    EncounterDate DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    EncounterType NVARCHAR(100) NULL, -- Office Visit, Phone, Virtual, Procedure
    ChiefComplaint NVARCHAR(500) NULL,

    Status NVARCHAR(50) NOT NULL DEFAULT 'Open', -- Open, Signed, Amended, Cancelled

    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy BIGINT NULL,
    SignedAt DATETIME2 NULL,
    SignedBy BIGINT NULL,

    CONSTRAINT FK_Encounter_Patient
        FOREIGN KEY (PatientId) REFERENCES dbo.Patient(PatientId),

    CONSTRAINT FK_Encounter_Appointment
        FOREIGN KEY (AppointmentId) REFERENCES dbo.Appointment(AppointmentId),

    CONSTRAINT FK_Encounter_Provider
        FOREIGN KEY (ProviderId) REFERENCES dbo.Provider(ProviderId)
);

CREATE TABLE dbo.DocumentTemplate
(
    TemplateId BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,

    TemplateName NVARCHAR(150) NOT NULL,
    TemplateType NVARCHAR(100) NOT NULL, -- Consult Note, Follow-up, Letter, Procedure
    TemplateHtml NVARCHAR(MAX) NOT NULL,

    IsActive BIT NOT NULL DEFAULT 1,

    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy BIGINT NULL
);

CREATE TABLE dbo.ClinicalNote
(
    ClinicalNoteId BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    ClinicalNoteUid UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),

    EncounterId BIGINT NOT NULL,
    PatientId BIGINT NOT NULL,
    ProviderId BIGINT NULL,
    TemplateId BIGINT NULL,

    NoteTitle NVARCHAR(255) NOT NULL,
    NoteHtml NVARCHAR(MAX) NOT NULL,
    PlainTextSummary NVARCHAR(MAX) NULL,

    Status NVARCHAR(50) NOT NULL DEFAULT 'Draft', -- Draft, Signed, Amended

    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy BIGINT NULL,
    UpdatedAt DATETIME2 NULL,
    UpdatedBy BIGINT NULL,
    SignedAt DATETIME2 NULL,
    SignedBy BIGINT NULL,

    CONSTRAINT FK_ClinicalNote_Encounter
        FOREIGN KEY (EncounterId) REFERENCES dbo.PatientEncounter(EncounterId),

    CONSTRAINT FK_ClinicalNote_Patient
        FOREIGN KEY (PatientId) REFERENCES dbo.Patient(PatientId),

    CONSTRAINT FK_ClinicalNote_Provider
        FOREIGN KEY (ProviderId) REFERENCES dbo.Provider(ProviderId),

    CONSTRAINT FK_ClinicalNote_Template
        FOREIGN KEY (TemplateId) REFERENCES dbo.DocumentTemplate(TemplateId)
);

CREATE TABLE dbo.PatientDocument
(
    PatientDocumentId BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    PatientDocumentUid UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),

    PatientId BIGINT NOT NULL,
    EncounterId BIGINT NULL,

    DocumentTitle NVARCHAR(255) NOT NULL,
    DocumentType NVARCHAR(100) NOT NULL, -- PDF, Lab, ECG, Echo, Referral, Scanned
    DocumentStatus NVARCHAR(50) NOT NULL DEFAULT 'Active',

    DocumentDate DATETIME2 NULL,
    SourceSystem NVARCHAR(100) NULL,

    FileName NVARCHAR(255) NULL,
    MimeType NVARCHAR(100) NULL,
    StorageProvider NVARCHAR(50) NULL, -- Local, S3, AzureBlob
    StoragePath NVARCHAR(1000) NULL,

    IsDeleted BIT NOT NULL DEFAULT 0,

    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy BIGINT NULL,

    CONSTRAINT FK_PatientDocument_Patient
        FOREIGN KEY (PatientId) REFERENCES dbo.Patient(PatientId),

    CONSTRAINT FK_PatientDocument_Encounter
        FOREIGN KEY (EncounterId) REFERENCES dbo.PatientEncounter(EncounterId)
);

CREATE TABLE dbo.DocumentAttachment
(
    AttachmentId BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,

    PatientDocumentId BIGINT NOT NULL,

    FileName NVARCHAR(255) NOT NULL,
    MimeType NVARCHAR(100) NOT NULL,
    FileSizeBytes BIGINT NULL,

    StorageProvider NVARCHAR(50) NOT NULL,
    StoragePath NVARCHAR(1000) NOT NULL,

    PageCount INT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),

    CONSTRAINT FK_DocumentAttachment_PatientDocument
        FOREIGN KEY (PatientDocumentId) REFERENCES dbo.PatientDocument(PatientDocumentId)
);

<!-- Login / access control tables --->

CREATE TABLE dbo.ApplicationUser
(
    UserId BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    UserUid UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),

    Username NVARCHAR(100) NOT NULL,
    DisplayName NVARCHAR(200) NOT NULL,
    Email NVARCHAR(255) NULL,

    ProviderId BIGINT NULL,

    IsActive BIT NOT NULL DEFAULT 1,

    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),

    CONSTRAINT UQ_ApplicationUser_Username UNIQUE (Username),

    CONSTRAINT FK_ApplicationUser_Provider
        FOREIGN KEY (ProviderId) REFERENCES dbo.Provider(ProviderId)
);

CREATE TABLE dbo.UserRole
(
    UserRoleId BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,

    UserId BIGINT NOT NULL,
    RoleName NVARCHAR(100) NOT NULL, -- Admin, Physician, MOA, Billing, Viewer

    CONSTRAINT FK_UserRole_User
        FOREIGN KEY (UserId) REFERENCES dbo.ApplicationUser(UserId)
);

CREATE TABLE dbo.UserPermission
(
    UserPermissionId BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,

    UserId BIGINT NOT NULL,
    PermissionCode NVARCHAR(100) NOT NULL,
    IsAllowed BIT NOT NULL DEFAULT 1,

    CONSTRAINT FK_UserPermission_User
        FOREIGN KEY (UserId) REFERENCES dbo.ApplicationUser(UserId)
);

<!-- Example permissions -->

INSERT INTO dbo.UserPermission
(
    UserId,
    PermissionCode,
    IsAllowed
)
VALUES
(1, 'SCHEDULING_VIEW', 1),
(1, 'SCHEDULING_EDIT', 1),
(1, 'PATIENT_CHART_VIEW', 1),
(1, 'PATIENT_CHART_EDIT', 1),
(1, 'DOCUMENT_SIGN', 1),
(1, 'ADMIN_USERS', 1);

<!-- Audit table -->
<!-- For healthcare software, this is very important. -->

CREATE TABLE dbo.AuditLog
(
    AuditLogId BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,

    UserId BIGINT NULL,
    PatientId BIGINT NULL,

    ActionName NVARCHAR(100) NOT NULL,
    EntityName NVARCHAR(100) NOT NULL,
    EntityId NVARCHAR(100) NULL,

    OldValue NVARCHAR(MAX) NULL,
    NewValue NVARCHAR(MAX) NULL,

    IpAddress NVARCHAR(50) NULL,
    BrowserInfo NVARCHAR(500) NULL,

    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),

    CONSTRAINT FK_AuditLog_User
        FOREIGN KEY (UserId) REFERENCES dbo.ApplicationUser(UserId),

    CONSTRAINT FK_AuditLog_Patient
        FOREIGN KEY (PatientId) REFERENCES dbo.Patient(PatientId)
);

<!-- Add indexes for performance -->

CREATE INDEX IX_Patient_Name
ON dbo.Patient (LastName, FirstName);

CREATE INDEX IX_Patient_DOB
ON dbo.Patient (DateOfBirth);

CREATE INDEX IX_Appointment_Provider_StartTime
ON dbo.Appointment (ProviderId, StartTime);

CREATE INDEX IX_Appointment_Patient
ON dbo.Appointment (PatientId);

CREATE INDEX IX_Appointment_Start_End
ON dbo.Appointment (StartTime, EndTime);

CREATE INDEX IX_Encounter_Patient_Date
ON dbo.PatientEncounter (PatientId, EncounterDate DESC);

CREATE INDEX IX_ClinicalNote_Patient
ON dbo.ClinicalNote (PatientId);

CREATE INDEX IX_PatientDocument_Patient
ON dbo.PatientDocument (PatientId, DocumentDate DESC);

CREATE INDEX IX_AuditLog_Patient_Date
ON dbo.AuditLog (PatientId, CreatedAt DESC);




