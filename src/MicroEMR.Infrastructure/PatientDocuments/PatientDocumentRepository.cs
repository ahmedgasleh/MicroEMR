using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MicroEMR.Application.PatientDocuments.Contracts;
using MicroEMR.Application.PatientDocuments.Repositories;


namespace MicroEMR.Infrastructure.PatientDocuments;

public sealed class PatientDocumentRepository
    : IPatientDocumentRepository
{
    private readonly string _connectionString;
    private readonly ILogger<PatientDocumentRepository> _logger;

    public PatientDocumentRepository(
        IConfiguration configuration,
        ILogger<PatientDocumentRepository> logger)
    {
        _connectionString =
            configuration.GetConnectionString("MicroEmrDatabase")
            ?? throw new InvalidOperationException(
                "Connection string 'MicroEmrDatabase' was not found.");

        _logger = logger;
    }

    public async Task<
        IReadOnlyList<PatientDocumentListItemResponse>>
        GetByPatientUidAsync(
            Guid patientUid,
            CancellationToken cancellationToken = default)
    {
        var documents =
            new List<PatientDocumentListItemResponse>();

        await using var connection =
            new SqlConnection(_connectionString);

        await using var command =
            new SqlCommand(
                "dbo.PatientDocument_GetByPatientUid",
                connection)
            {
                CommandType = CommandType.StoredProcedure
            };

        command.Parameters.Add(
            new SqlParameter(
                "@PatientUid",
                SqlDbType.UniqueIdentifier)
            {
                Value = patientUid
            });

        await connection.OpenAsync(cancellationToken);

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            documents.Add(MapListItem(reader));
        }

        return documents;
    }

    public async Task<PatientDocumentDetailsResponse?> GetByUidAsync(
        Guid documentUid,
        CancellationToken cancellationToken = default)
    {
        await using var connection =
            new SqlConnection(_connectionString);

        await using var command =
            new SqlCommand(
                "dbo.PatientDocument_GetByUid",
                connection)
            {
                CommandType = CommandType.StoredProcedure
            };

        command.Parameters.Add(
            new SqlParameter(
                "@DocumentUid",
                SqlDbType.UniqueIdentifier)
            {
                Value = documentUid
            });

        await connection.OpenAsync(cancellationToken);

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return MapDetails(reader);
    }

    public async Task<
        IReadOnlyList<DocumentTemplateListItemResponse>>
        GetActiveTemplatesAsync(
            CancellationToken cancellationToken = default)
    {
        var templates =
            new List<DocumentTemplateListItemResponse>();

        await using var connection =
            new SqlConnection(_connectionString);

        await using var command =
            new SqlCommand(
                "dbo.DocumentTemplate_GetActive",
                connection)
            {
                CommandType = CommandType.StoredProcedure
            };

        await connection.OpenAsync(cancellationToken);

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            templates.Add(MapTemplateListItem(reader));
        }

        return templates;
    }

    public async Task<DocumentTemplateDetailsResponse?>
        GetTemplateByUidAsync(
            Guid templateUid,
            CancellationToken cancellationToken = default)
    {
        await using var connection =
            new SqlConnection(_connectionString);

        await using var command =
            new SqlCommand(
                "dbo.DocumentTemplate_GetByUid",
                connection)
            {
                CommandType = CommandType.StoredProcedure
            };

        command.Parameters.Add(
            new SqlParameter(
                "@TemplateUid",
                SqlDbType.UniqueIdentifier)
            {
                Value = templateUid
            });

        await connection.OpenAsync(cancellationToken);

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return MapTemplateDetails(reader);
    }

    public async Task<PatientDocumentDetailsResponse> CreateAsync(
        Guid patientUid,
        CreatePatientDocumentRequest request,
        long? createdBy,
        CancellationToken cancellationToken = default)
    {
        await using var connection =
            new SqlConnection(_connectionString);

        await using var command =
            new SqlCommand(
                "dbo.PatientDocument_Create",
                connection)
            {
                CommandType = CommandType.StoredProcedure
            };

        command.Parameters.Add(
            new SqlParameter(
                "@PatientUid",
                SqlDbType.UniqueIdentifier)
            {
                Value = patientUid
            });

        command.Parameters.Add(
            new SqlParameter(
                "@TemplateUid",
                SqlDbType.UniqueIdentifier)
            {
                Value = request.TemplateUid.HasValue
                    ? request.TemplateUid.Value
                    : DBNull.Value
            });

        AddRequiredString(
            command,
            "@DocumentType",
            SqlDbType.NVarChar,
            100,
            request.DocumentType);

        AddRequiredString(
            command,
            "@Title",
            SqlDbType.NVarChar,
            250,
            request.Title);

        AddNullableString(
            command,
            "@DocumentContent",
            SqlDbType.NVarChar,
            -1,
            request.Content);

        command.Parameters.Add(
            new SqlParameter(
                "@CreatedBy",
                SqlDbType.BigInt)
            {
                Value = createdBy.HasValue
                    ? createdBy.Value
                    : DBNull.Value
            });

        await connection.OpenAsync(cancellationToken);

        try
        {
            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException(
                    "PatientDocument_Create returned no document record.");
            }

            return MapDetails(reader);
        }
        catch (SqlException exception)
        {
            _logger.LogError(
                exception,
                "Failed to create document '{DocumentTitle}' " +
                "for patient {PatientUid}.",
                request.Title,
                patientUid);

            throw;
        }
    }

    private static PatientDocumentListItemResponse MapListItem(
        SqlDataReader reader)
    {
        return new PatientDocumentListItemResponse
        {
            DocumentUid =
                reader.GetGuid(
                    reader.GetOrdinal("DocumentUid")),

            PatientUid =
                reader.GetGuid(
                    reader.GetOrdinal("PatientUid")),

            TemplateUid =
                GetNullableGuid(
                    reader,
                    "TemplateUid"),

            DocumentType =
                reader.GetString(
                    reader.GetOrdinal("DocumentType")),

            Title =
                reader.GetString(
                    reader.GetOrdinal("Title")),

            Status =
                reader.GetString(
                    reader.GetOrdinal("DocumentStatus")),

            CreatedAt =
                reader.GetDateTime(
                    reader.GetOrdinal("CreatedAt")),

            UpdatedAt =
                GetNullableDateTime(
                    reader,
                    "UpdatedAt"),

            CreatedBy =
                GetNullableInt64(
                    reader,
                    "CreatedBy"),

            CreatedByDisplayName =
                GetNullableString(
                    reader,
                    "CreatedByDisplayName")
        };
    }

    private static PatientDocumentDetailsResponse MapDetails(
        SqlDataReader reader)
    {
        return new PatientDocumentDetailsResponse
        {
            DocumentUid =
                reader.GetGuid(
                    reader.GetOrdinal("DocumentUid")),

            PatientUid =
                reader.GetGuid(
                    reader.GetOrdinal("PatientUid")),

            TemplateUid =
                GetNullableGuid(
                    reader,
                    "TemplateUid"),

            DocumentType =
                reader.GetString(
                    reader.GetOrdinal("DocumentType")),

            Title =
                reader.GetString(
                    reader.GetOrdinal("Title")),

            Status =
                reader.GetString(
                    reader.GetOrdinal("DocumentStatus")),

            Content =
                GetNullableString(
                    reader,
                    "DocumentContent")
                ?? string.Empty,

            CreatedBy =
                GetNullableInt64(
                    reader,
                    "CreatedBy"),

            CreatedByDisplayName =
                GetNullableString(
                    reader,
                    "CreatedByDisplayName"),

            CreatedAt =
                reader.GetDateTime(
                    reader.GetOrdinal("CreatedAt")),

            UpdatedAt =
                GetNullableDateTime(
                    reader,
                    "UpdatedAt"),

            RowVersion =
                GetNullableRowVersion(reader, "RowVersion")
        };
    }

    private static DocumentTemplateListItemResponse
        MapTemplateListItem(
            SqlDataReader reader)
    {
        return new DocumentTemplateListItemResponse
        {
            TemplateUid =
                reader.GetGuid(
                    reader.GetOrdinal("TemplateUid")),

            TemplateName =
                reader.GetString(
                    reader.GetOrdinal("TemplateName")),

            DocumentType =
                reader.GetString(
                    reader.GetOrdinal("DocumentType")),

            Description =
                GetNullableString(
                    reader,
                    "Description"),

            IsActive =
                reader.GetBoolean(
                    reader.GetOrdinal("IsActive"))
        };
    }

    private static DocumentTemplateDetailsResponse
        MapTemplateDetails(
            SqlDataReader reader)
    {
        return new DocumentTemplateDetailsResponse
        {
            TemplateUid =
                reader.GetGuid(
                    reader.GetOrdinal("TemplateUid")),

            TemplateName =
                reader.GetString(
                    reader.GetOrdinal("TemplateName")),

            DocumentType =
                reader.GetString(
                    reader.GetOrdinal("DocumentType")),

            Description =
                GetNullableString(
                    reader,
                    "Description"),

            TemplateContent =
                reader.GetString(
                    reader.GetOrdinal("TemplateContent")),

            IsActive =
                reader.GetBoolean(
                    reader.GetOrdinal("IsActive"))
        };
    }

    private static void AddRequiredString(
        SqlCommand command,
        string parameterName,
        SqlDbType sqlDbType,
        int size,
        string value)
    {
        command.Parameters.Add(
            new SqlParameter(
                parameterName,
                sqlDbType,
                size)
            {
                Value = value.Trim()
            });
    }

    private static void AddNullableString(
        SqlCommand command,
        string parameterName,
        SqlDbType sqlDbType,
        int size,
        string? value)
    {
        command.Parameters.Add(
            new SqlParameter(
                parameterName,
                sqlDbType,
                size)
            {
                Value = string.IsNullOrWhiteSpace(value)
                    ? DBNull.Value
                    : value.Trim()
            });
    }

    private static string? GetNullableString(
        SqlDataReader reader,
        string columnName)
    {
        var ordinal =
            reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? null
            : reader.GetString(ordinal);
    }

    private static Guid? GetNullableGuid(
        SqlDataReader reader,
        string columnName)
    {
        var ordinal =
            reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? null
            : reader.GetGuid(ordinal);
    }

    private static long? GetNullableInt64(
        SqlDataReader reader,
        string columnName)
    {
        var ordinal =
            reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? null
            : reader.GetInt64(ordinal);
    }

    private static DateTime? GetNullableDateTime(
        SqlDataReader reader,
        string columnName)
    {
        var ordinal =
            reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? null
            : reader.GetDateTime(ordinal);
    }

    private static string GetNullableRowVersion(
        SqlDataReader reader,
        string columnName)
    {
        try
        {
            var ordinal =
                reader.GetOrdinal(columnName);

            return reader.IsDBNull(ordinal)
                ? string.Empty
                : Convert.ToBase64String(
                    (byte[])reader.GetValue(ordinal));
        }
        catch (IndexOutOfRangeException)
        {
            return string.Empty;
        }
    }
}
