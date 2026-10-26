using System.Data;
using Microsoft.Data.SqlClient;
using MicroEMR.Application.PatientPrescriptions;

namespace MicroEMR.Infrastructure.PatientPrescriptions;

public sealed partial class PatientPrescriptionRepository : IPrescriptionOutputRepository
{
    public Task<PatientPrescriptionResponse?> SignAndFinalizeReviewedAsync(Guid p,Guid u,string v,long a,bool reviewed,CancellationToken t=default) =>
        One("dbo.PatientPrescription_SignAndFinalize",p,u,v,q=>{Actor(q,a);q.Parameters.Add("@ConfirmSignature",SqlDbType.Bit).Value=true;if(reviewed)q.Parameters.Add("@PickupReviewed",SqlDbType.Bit).Value=true;},t);
    public Task<PatientPrescriptionResponse?> SignAndFinalizeAsync(Guid patient, Guid prescription,
        string rowVersion, long actor, CancellationToken token = default) =>
        One("dbo.PatientPrescription_SignAndFinalize", patient, prescription, rowVersion, command =>
        {
            Actor(command, actor);
            command.Parameters.Add("@ConfirmSignature", SqlDbType.Bit).Value = true;
        }, token);

    public async Task RecordGenerationAsync(Guid patient, Guid prescription, Guid artifact,
        PrescriptionOutputOccurrence occurrence, CancellationToken token = default)
    {
        await using var connection = await connections.OpenConnectionAsync(token);
        await using var command = Cmd(connection, "dbo.PatientPrescription_RecordOutputGeneration");
        Uid(command, "@PatientUid", patient);
        Uid(command, "@PrescriptionUid", prescription);
        Uid(command, "@ArtifactUid", artifact);
        Uid(command, "@OccurrenceUid", occurrence.Uid);
        Str(command, "@AuthSubjectId", occurrence.Requester.Subject, 450, false);
        Str(command, "@RequesterName", occurrence.Requester.Name, 200, false);
        Str(command, "@RequestCorrelationId", occurrence.Requester.CorrelationId, 100, false);
        command.Parameters.Add("@GeneratedAtUtc", SqlDbType.DateTime2).Value = occurrence.GeneratedAtUtc.UtcDateTime;
        await command.ExecuteNonQueryAsync(token);
    }
}
