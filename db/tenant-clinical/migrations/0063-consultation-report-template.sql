-- Built-in, tenant-local Consultation Report template. The fixed UIDs and type code
-- identify this document independently of its display name or generated filename.
SET XACT_ABORT ON;

DECLARE @TemplateUid UNIQUEIDENTIFIER = '829b3d2d-1429-4dc3-b127-9e6b60813ba4';
DECLARE @VersionUid UNIQUEIDENTIFIER = 'aa409bd4-96f4-4275-a458-cba8aa61a729';
DECLARE @DefinitionJson NVARCHAR(MAX) = N'{
  "schemaVersion": 1,
  "sections": [
    {"id":"reason-for-consultation","key":"reasonForConsultation","title":"Reason for Consultation","order":1,"fields":[{"id":"reason-text","key":"reasonText","type":"TextArea","label":"Reason for Consultation","order":1}]},
    {"id":"relevant-history","key":"relevantHistory","title":"Relevant History","order":2,"fields":[{"id":"history-text","key":"historyText","type":"TextArea","label":"Relevant History","order":1}]},
    {"id":"examination-findings","key":"examinationFindings","title":"Examination / Findings","order":3,"fields":[{"id":"findings-text","key":"findingsText","type":"TextArea","label":"Examination / Findings","order":1}]},
    {"id":"investigations","key":"investigations","title":"Investigations","order":4,"fields":[{"id":"investigations-text","key":"investigationsText","type":"TextArea","label":"Investigations","order":1}]},
    {"id":"impression-assessment","key":"impressionAssessment","title":"Impression / Assessment","order":5,"fields":[{"id":"impression-text","key":"impressionText","type":"TextArea","label":"Impression / Assessment","order":1}]},
    {"id":"recommendations-plan","key":"recommendationsPlan","title":"Recommendations / Plan","order":6,"fields":[{"id":"recommendations-text","key":"recommendationsText","type":"TextArea","label":"Recommendations / Plan","order":1}]},
    {"id":"medication-changes","key":"medicationChanges","title":"Medication Changes","order":7,"fields":[{"id":"medication-text","key":"medicationText","type":"TextArea","label":"Medication Changes","order":1}]},
    {"id":"follow-up","key":"followUp","title":"Follow-up","order":8,"fields":[{"id":"follow-up-text","key":"followUpText","type":"TextArea","label":"Follow-up","order":1}]}
  ]
}';

BEGIN TRANSACTION;

IF NOT EXISTS (SELECT 1 FROM dbo.DocumentTemplate WHERE TemplateUid = @TemplateUid)
BEGIN
    INSERT dbo.DocumentTemplate
        (TemplateUid, TemplateName, TemplateType, Category, TemplateKind,
         TemplateScope, TemplateHtml, IsActive, CreatedAt)
    VALUES
        (@TemplateUid, N'Consultation Report', N'CONSULTATION_REPORT',
         N'Consultation Report', N'Document', N'System', N'', 1, SYSUTCDATETIME());
END;

IF NOT EXISTS (SELECT 1 FROM dbo.DocumentTemplateVersion WHERE TemplateVersionUid = @VersionUid)
BEGIN
    INSERT dbo.DocumentTemplateVersion
        (TemplateVersionUid, TemplateUid, VersionNumber, TemplateContent,
         SchemaVersion, DefinitionJson, VersionStatus, IsCurrent,
         PublishedAt, CreatedAt)
    VALUES
        (@VersionUid, @TemplateUid, 1, N'', 1, @DefinitionJson,
         N'Published', 1, SYSUTCDATETIME(), SYSUTCDATETIME());
END;

COMMIT TRANSACTION;
