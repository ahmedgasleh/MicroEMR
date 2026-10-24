using System.Text.RegularExpressions;
using MicroEMR.Application.PatientEncounters;
using MicroEMR.Infrastructure.PatientEncounters;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class EncounterMedicationPrescriptionTests
{
    [DiagnosisSqlFact]
    public async Task AllEncounterMedicationActionsPreserveRealFinalizedPrescriptionAndSignedArtifact()
    {
        var db = new PrescriptionRefillSqlDatabase();
        try
        {
            await db.InitializeAsync();
            var patient = await db.SeedPatient(); var encounter = Guid.NewGuid();
            var draft = await db.Repository.CreateAsync(patient, PrescriptionRefillInstructionsTests.Draft(), 7);
            var finalized = (await db.Repository.SignAndFinalizeAsync(patient, draft.PrescriptionUid, draft.RowVersion, 7))!;
            var artifact = (await db.Repository.GetArtifactAsync(patient, finalized.PrescriptionUid))!;
            var original = await db.Read<string>("SELECT (SELECT * FROM dbo.PatientPrescription WHERE PrescriptionUid=@Uid FOR JSON PATH,INCLUDE_NULL_VALUES)", finalized.PrescriptionUid);
            await db.Execute("""
                CREATE TABLE dbo.PatientEncounter(EncounterUid uniqueidentifier PRIMARY KEY,PatientUid uniqueidentifier,EncounterStatus nvarchar(30),RowVersion rowversion);
                CREATE TABLE dbo.PatientEncounterHistory(EncounterHistoryUid uniqueidentifier,EncounterUid uniqueidentifier,PatientUid uniqueidentifier,ActionType nvarchar(50),ActionDescription nvarchar(500),OldStatus nvarchar(50),NewStatus nvarchar(50),Reason nvarchar(500),CreatedBy bigint,CreatedAt datetime2 DEFAULT SYSUTCDATETIME());
                """);
            await db.Execute($"INSERT dbo.PatientEncounter(EncounterUid,PatientUid,EncounterStatus) VALUES('{encounter}','{patient}','Open')");
            var files = new[] { "patient_medication_stored_procedures.sql", "tenant-clinical/migrations/0059-medication-discontinuation-concurrency.sql", "tenant-clinical/migrations/0077-medication-classification.sql", "tenant-clinical/migrations/0087-medication-plan-intent-safeguards.sql", "tenant-clinical/migrations/0089-medication-compliance-assessments.sql" };
            foreach (var file in files)
                foreach (var batch in Regex.Split(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "database", file)), @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase).Where(x => !string.IsNullOrWhiteSpace(x))) await db.Execute(batch);
            var history = Regex.Split(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "database", "patient_encounter_stored_procedures.sql")), @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)
                .Single(x => x.TrimStart().StartsWith("CREATE OR ALTER PROCEDURE dbo.PatientEncounterHistory_Create", StringComparison.Ordinal));
            await db.Execute(history);
            var input = EncounterMedicationTests.Draft(); input.EncounterRowVersion = Convert.ToBase64String(await db.Read<byte[]>("SELECT RowVersion FROM dbo.PatientEncounter WHERE EncounterUid=@Uid", encounter));
            var repository = new EncounterMedicationRepository(db.MedicationFactory);
            var row = (await repository.SaveAsync(patient, encounter, input, 7))!;
            input.Action = "Update"; input.RequestUid = Guid.NewGuid(); input.MedicationUid = row.MedicationUid; input.MedicationRowVersion = row.RowVersion; input.Medication!.Directions = "TEST ONLY reviewed";
            row = (await repository.SaveAsync(patient, encounter, input, 7))!;
            input.Action = "Discontinue"; input.RequestUid = Guid.NewGuid(); input.MedicationRowVersion = row.RowVersion;
            Assert.Equal("Discontinued", (await repository.SaveAsync(patient, encounter, input, 7))!.Status);
            Assert.Equal(original, await db.Read<string>("SELECT (SELECT * FROM dbo.PatientPrescription WHERE PrescriptionUid=@Uid FOR JSON PATH,INCLUDE_NULL_VALUES)", finalized.PrescriptionUid));
            Assert.Equal(artifact, await db.Repository.GetArtifactAsync(patient, finalized.PrescriptionUid));
        }
        finally { await db.DisposeAsync(); }
    }
}
