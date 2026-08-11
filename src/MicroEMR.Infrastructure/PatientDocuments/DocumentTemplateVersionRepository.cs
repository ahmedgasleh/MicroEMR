using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using MicroEMR.Application.PatientDocuments;
using MicroEMR.Application.PatientDocuments.Contracts;
using MicroEMR.Application.PatientDocuments.Repositories;
using MicroEMR.Infrastructure.Tenancy;

namespace MicroEMR.Infrastructure.PatientDocuments;

public sealed class DocumentTemplateVersionRepository(
    ITenantSqlConnectionFactory connectionFactory,
    ILogger<DocumentTemplateVersionRepository> logger) : IDocumentTemplateVersionRepository
{
    public async Task<DocumentTemplateVersionResponse?> GetByUidAsync(Guid templateVersionUid, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = Command("dbo.DocumentTemplateVersion_GetByUid", connection);
        AddUid(command, "@TemplateVersionUid", templateVersionUid);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Map(reader) : null;
    }

    public async Task<IReadOnlyList<DocumentTemplateVersionResponse>> GetByTemplateUidAsync(
        Guid templateUid,
        CancellationToken cancellationToken = default)
    {
        var versions = new List<DocumentTemplateVersionResponse>();
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = Command("dbo.DocumentTemplateVersion_GetByTemplateUid", connection);
        AddUid(command, "@TemplateUid", templateUid);
        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) versions.Add(Map(reader));
            return versions;
        }
        catch (SqlException exception) when (exception.Number == 2812)
        {
            logger.LogInformation(
                "Document template versioning is not installed in the current tenant database.");
            return versions;
        }
    }

    public Task<DocumentTemplateVersionResponse?> CreateDraftAsync(
        Guid templateUid,
        long? createdBy,
        CancellationToken cancellationToken = default) =>
        MutateAsync("dbo.DocumentTemplateVersion_CreateDraft", templateUid, null, null, null,
            null, null, createdBy, cancellationToken);

    public Task<DocumentTemplateVersionResponse?> UpdateDraftAsync(
        Guid templateUid,
        Guid templateVersionUid,
        UpdateDocumentTemplateVersionRequest request,
        long? updatedBy,
        CancellationToken cancellationToken = default) =>
        MutateAsync("dbo.DocumentTemplateVersion_UpdateDraft", templateUid, templateVersionUid,
            request.TemplateContent, request.RowVersion, request.SchemaVersion, request.DefinitionJson,
            updatedBy, cancellationToken);

    public Task<DocumentTemplateVersionResponse?> PublishAsync(
        Guid templateUid,
        Guid templateVersionUid,
        string rowVersion,
        long? publishedBy,
        CancellationToken cancellationToken = default) =>
        MutateAsync("dbo.DocumentTemplateVersion_Publish", templateUid, templateVersionUid,
            null, rowVersion, null, null, publishedBy, cancellationToken);

    public Task<DocumentTemplateVersionResponse?> RetireAsync(
        Guid templateUid,
        Guid templateVersionUid,
        string rowVersion,
        long? retiredBy,
        CancellationToken cancellationToken = default) =>
        MutateAsync("dbo.DocumentTemplateVersion_Retire", templateUid, templateVersionUid,
            null, rowVersion, null, null, retiredBy, cancellationToken);

    private async Task<DocumentTemplateVersionResponse?> MutateAsync(
        string procedure,
        Guid templateUid,
        Guid? versionUid,
        string? content,
        string? rowVersion,
        int? schemaVersion,
        string? definitionJson,
        long? userId,
        CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = Command(procedure, connection);
        AddUid(command, "@TemplateUid", templateUid);
        if (versionUid.HasValue) AddUid(command, "@TemplateVersionUid", versionUid.Value);
        if (content is not null)
            command.Parameters.Add(new SqlParameter("@TemplateContent", SqlDbType.NVarChar, -1) { Value = content });
        if (schemaVersion.HasValue)
            command.Parameters.Add(new SqlParameter("@SchemaVersion", SqlDbType.Int) { Value = schemaVersion.Value });
        if (definitionJson is not null)
            command.Parameters.Add(new SqlParameter("@DefinitionJson", SqlDbType.NVarChar, -1) { Value = definitionJson });
        if (rowVersion is not null)
            command.Parameters.Add(new SqlParameter("@ExpectedRowVersion", SqlDbType.Timestamp) { Value = Convert.FromBase64String(rowVersion) });

        var userParameter = procedure.EndsWith("CreateDraft", StringComparison.Ordinal)
            ? "@CreatedBy"
            : procedure.EndsWith("Publish", StringComparison.Ordinal)
                ? "@PublishedBy"
                : procedure.EndsWith("Retire", StringComparison.Ordinal)
                    ? "@RetiredBy"
                    : "@UpdatedBy";
        command.Parameters.Add(new SqlParameter(userParameter, SqlDbType.BigInt) { Value = (object?)userId ?? DBNull.Value });

        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await reader.ReadAsync(cancellationToken) ? Map(reader) : null;
        }
        catch (SqlException exception) when (exception.Number is 2812 or 51031 or 51032 or 51033 or 51034 or 51035)
        {
            throw new DocumentTemplateVersionConflictException(
                exception.Number == 2812
                    ? "Document template versioning is not available for this tenant yet."
                    : "The template version was changed or is no longer editable. Refresh and try again.",
                exception);
        }
        catch (SqlException exception)
        {
            logger.LogError(exception, "Document template version operation failed for {TemplateUid}.", templateUid);
            throw;
        }
    }

    private static SqlCommand Command(string procedure, SqlConnection connection) =>
        new(procedure, connection) { CommandType = CommandType.StoredProcedure };

    private static void AddUid(SqlCommand command, string name, Guid value) =>
        command.Parameters.Add(new SqlParameter(name, SqlDbType.UniqueIdentifier) { Value = value });

    private static DocumentTemplateVersionResponse Map(SqlDataReader reader) => new()
    {
        TemplateVersionUid = reader.GetGuid(reader.GetOrdinal("TemplateVersionUid")),
        TemplateUid = reader.GetGuid(reader.GetOrdinal("TemplateUid")),
        VersionNumber = reader.GetInt32(reader.GetOrdinal("VersionNumber")),
        TemplateContent = reader.GetString(reader.GetOrdinal("TemplateContent")),
        SchemaVersion = reader.GetInt32(reader.GetOrdinal("SchemaVersion")),
        DefinitionJson = reader.GetString(reader.GetOrdinal("DefinitionJson")),
        Status = reader.GetString(reader.GetOrdinal("VersionStatus")),
        IsCurrent = reader.GetBoolean(reader.GetOrdinal("IsCurrent")),
        PublishedAt = OptionalDate(reader, "PublishedAt"),
        PublishedBy = OptionalLong(reader, "PublishedBy"),
        CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
        CreatedBy = OptionalLong(reader, "CreatedBy"),
        UpdatedAt = OptionalDate(reader, "UpdatedAt"),
        UpdatedBy = OptionalLong(reader, "UpdatedBy"),
        RowVersion = Convert.ToBase64String((byte[])reader["RowVersion"])
    };

    private static DateTime? OptionalDate(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
    }

    private static long? OptionalLong(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt64(ordinal);
    }
}
