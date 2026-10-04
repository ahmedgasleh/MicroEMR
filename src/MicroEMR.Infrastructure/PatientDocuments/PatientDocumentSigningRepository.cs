using System.Data;
using Microsoft.Data.SqlClient;
using MicroEMR.Application.ClinicalOutput;
using MicroEMR.Application.PatientDocuments;
using MicroEMR.Application.PatientDocuments.Contracts;

namespace MicroEMR.Infrastructure.PatientDocuments;

public sealed partial class PatientDocumentRepository : IConsultationSigningRepository
{
    public async Task<PatientDocumentDetailsResponse> SignAsync(Guid patientUid, Guid documentUid,
        string rowVersion, long actorUserId,
        Func<ConsultationSigningContext, CancellationToken, Task<CreateClinicalOutputArtifact>> createArtifact,
        CancellationToken token = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(token);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(token);
        try
        {
            ConsultationSigningContext context;
            await using (var command = Command("dbo.PatientDocument_PrepareSignConsultation"))
            {
                await using var reader = await command.ExecuteReaderAsync(token);
                if (!await reader.ReadAsync(token)) throw new KeyNotFoundException("Consultation not found.");
                var signerUid = reader.GetGuid(0);
                var signerName = reader.GetString(1);
                var signedAt = DateTime.SpecifyKind(reader.GetDateTime(2), DateTimeKind.Utc);
                await reader.NextResultAsync(token);
                await reader.ReadAsync(token);
                var document = MapDetails(reader);
                await reader.NextResultAsync(token);
                context = new(document, await ReadRecipientsAsync(reader, token), signerUid, signerName, signedAt);
            }
            var artifact = await createArtifact(context, token);
            PatientDocumentDetailsResponse result;
            await using (var command = Command("dbo.PatientDocument_CommitSignConsultation"))
            {
                command.Parameters.Add("@SignerProviderUid", SqlDbType.UniqueIdentifier).Value = context.SignerProviderUid;
                command.Parameters.Add("@SignerDisplayName", SqlDbType.NVarChar, 200).Value = context.SignerDisplayName;
                command.Parameters.Add("@SignedAt", SqlDbType.DateTime2).Value = context.SignedAtUtc;
                command.Parameters.Add("@ArtifactUid", SqlDbType.UniqueIdentifier).Value = artifact.ArtifactUid;
                command.Parameters.Add("@StorageKey", SqlDbType.NVarChar, 700).Value = artifact.StorageKey;
                command.Parameters.Add("@FileSizeBytes", SqlDbType.BigInt).Value = artifact.FileSizeBytes;
                command.Parameters.Add("@Sha256", SqlDbType.Char, 64).Value = artifact.Sha256;
                await using var reader = await command.ExecuteReaderAsync(token);
                await reader.ReadAsync(token);
                result = MapDetails(reader);
            }
            await transaction.CommitAsync(token);
            result.IsConsultationReport = true;
            result.FinalRecipients = context.Recipients;
            return result;

            SqlCommand Command(string name)
            {
                var command = new SqlCommand(name, connection, transaction) { CommandType = CommandType.StoredProcedure };
                command.Parameters.Add("@PatientUid", SqlDbType.UniqueIdentifier).Value = patientUid;
                command.Parameters.Add("@DocumentUid", SqlDbType.UniqueIdentifier).Value = documentUid;
                command.Parameters.Add("@ExpectedRowVersion", SqlDbType.Binary, 8).Value = Convert.FromBase64String(rowVersion);
                command.Parameters.Add("@ActorUserId", SqlDbType.BigInt).Value = actorUserId;
                return command;
            }
        }
        catch (SqlException exception) when (exception.Number == 51910)
        { throw new KeyNotFoundException("Consultation not found.", exception); }
        catch (SqlException exception) when (exception.Number == 51911)
        { throw new PatientDocumentNotDraftException("Only Draft consultations can be signed.", exception); }
        catch (SqlException exception) when (exception.Number == 51912)
        { throw new PatientDocumentConcurrencyException("The document changed. Reload before signing.", exception); }
        catch (SqlException exception) when (exception.Number == 51914)
        { throw new UnauthorizedAccessException("Signing requires an active linked Provider.", exception); }
        catch (SqlException exception) when (exception.Number is 51913 or 51915)
        { throw new ArgumentException(exception.Message, exception); }
        // Disposing an uncommitted transaction rolls back both status and artifact metadata.
    }
}
