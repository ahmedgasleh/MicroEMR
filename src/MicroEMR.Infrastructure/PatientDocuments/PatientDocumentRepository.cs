using System.Data;
using Microsoft.Data.SqlClient;
using MicroEMR.Infrastructure.Tenancy;
using Microsoft.Extensions.Logging;
using MicroEMR.Application.PatientDocuments.Contracts;
using MicroEMR.Application.PatientDocuments.Repositories;
using MicroEMR.Application.PatientDocuments;


namespace MicroEMR.Infrastructure.PatientDocuments;

public sealed class PatientDocumentRepository
    : IPatientDocumentRepository
{
    private readonly ITenantSqlConnectionFactory _connectionFactory;
    private readonly ILogger<PatientDocumentRepository> _logger;

    public PatientDocumentRepository(
        ITenantSqlConnectionFactory connectionFactory,
        ILogger<PatientDocumentRepository> logger)
    {
        _connectionFactory = connectionFactory;

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
            await _connectionFactory.OpenConnectionAsync(cancellationToken);

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
            await _connectionFactory.OpenConnectionAsync(cancellationToken);

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
            await _connectionFactory.OpenConnectionAsync(cancellationToken);

        await using var command =
            new SqlCommand(
                "dbo.DocumentTemplate_GetActive",
                connection)
            {
                CommandType = CommandType.StoredProcedure
            };



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
            await _connectionFactory.OpenConnectionAsync(cancellationToken);

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



        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return MapTemplateDetails(reader);
    }

    public async Task<IReadOnlyList<DocumentTemplateDetailsResponse>> GetTemplatesAsync(
        string statusFilter, CancellationToken cancellationToken = default)
    {
        var templates = new List<DocumentTemplateDetailsResponse>();
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand("dbo.DocumentTemplate_GetAll", connection)
        { CommandType = CommandType.StoredProcedure };
        command.Parameters.Add(new SqlParameter("@StatusFilter", SqlDbType.NVarChar, 50) { Value = statusFilter });

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) templates.Add(MapTemplateDetails(reader));
        return templates;
    }

    public Task<DocumentTemplateDetailsResponse?> CreateTemplateAsync(
        CreateDocumentTemplateRequest request, long? createdBy, CancellationToken cancellationToken = default) =>
        ExecuteTemplateMutationAsync("dbo.DocumentTemplate_Create", null, request.TemplateName,
            request.DocumentType, request.TemplateContent, null, createdBy, cancellationToken);

    public Task<DocumentTemplateDetailsResponse?> UpdateTemplateAsync(
        Guid templateUid, UpdateDocumentTemplateRequest request, long? updatedBy, CancellationToken cancellationToken = default) =>
        ExecuteTemplateMutationAsync("dbo.DocumentTemplate_Update", templateUid, request.TemplateName,
            request.DocumentType, request.TemplateContent, null, updatedBy, cancellationToken);

    public Task<DocumentTemplateDetailsResponse?> SetTemplateActiveAsync(
        Guid templateUid, bool isActive, long? updatedBy, CancellationToken cancellationToken = default) =>
        ExecuteTemplateMutationAsync("dbo.DocumentTemplate_SetActive", templateUid, null, null, null,
            isActive, updatedBy, cancellationToken);

    private async Task<DocumentTemplateDetailsResponse?> ExecuteTemplateMutationAsync(
        string procedure, Guid? templateUid, string? name, string? type, string? content,
        bool? isActive, long? userId, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand(procedure, connection) { CommandType = CommandType.StoredProcedure };
        if (templateUid.HasValue) command.Parameters.Add(new SqlParameter("@TemplateUid", SqlDbType.UniqueIdentifier) { Value = templateUid.Value });
        if (name is not null) AddRequiredString(command, "@TemplateName", SqlDbType.NVarChar, 200, name);
        if (type is not null) AddRequiredString(command, "@DocumentType", SqlDbType.NVarChar, 100, type);
        if (content is not null) command.Parameters.Add(new SqlParameter("@TemplateContent", SqlDbType.NVarChar, -1) { Value = content });
        if (isActive.HasValue) command.Parameters.Add(new SqlParameter("@IsActive", SqlDbType.Bit) { Value = isActive.Value });
        command.Parameters.Add(new SqlParameter(procedure.EndsWith("Create", StringComparison.Ordinal) ? "@CreatedBy" : "@UpdatedBy", SqlDbType.BigInt)
        { Value = (object?)userId ?? DBNull.Value });

        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await reader.ReadAsync(cancellationToken) ? MapTemplateDetails(reader) : null;
        }
        catch (SqlException exception) when (exception.Number == 51031)
        {
            throw new DocumentTemplateVersionConflictException(
                "Published template content cannot be edited in place. Create a new draft version instead.", exception);
        }
    }

    public async Task<PatientDocumentDetailsResponse> CreateAsync(
        Guid patientUid,
        CreatePatientDocumentRequest request,
        long? createdBy,
        CancellationToken cancellationToken = default)
    {
        await using var connection =
            await _connectionFactory.OpenConnectionAsync(cancellationToken);

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

            TemplateVersionUid =
                GetOptionalGuid(reader, "TemplateVersionUid"),

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

            TemplateVersionUid =
                GetOptionalGuid(reader, "TemplateVersionUid"),

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
                    reader.GetOrdinal("IsActive")),

            TemplateVersionUid = GetOptionalGuid(reader, "TemplateVersionUid"),
            CurrentVersion = GetOptionalInt32(reader, "CurrentVersion")
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
                    reader.GetOrdinal("IsActive")),

            CreatedAt = GetOptionalDateTime(reader, "CreatedAt") ?? default,
            CreatedBy = GetOptionalInt64(reader, "CreatedBy"),
            CreatedByDisplayName = GetOptionalString(reader, "CreatedByDisplayName"),
            UpdatedAt = GetOptionalDateTime(reader, "UpdatedAt"),
            UpdatedBy = GetOptionalInt64(reader, "UpdatedBy"),
            UpdatedByDisplayName = GetOptionalString(reader, "UpdatedByDisplayName"),
            RowVersion = GetOptionalRowVersion(reader, "RowVersion"),
            TemplateVersionUid = GetOptionalGuid(reader, "TemplateVersionUid"),
            CurrentVersion = GetOptionalInt32(reader, "CurrentVersion")
        };
    }

    private static int FindOrdinal(SqlDataReader reader, string columnName)
    {
        for (var ordinal = 0; ordinal < reader.FieldCount; ordinal++)
            if (string.Equals(reader.GetName(ordinal), columnName, StringComparison.OrdinalIgnoreCase)) return ordinal;
        return -1;
    }

    private static string? GetOptionalString(SqlDataReader reader, string columnName)
    {
        var ordinal = FindOrdinal(reader, columnName);
        return ordinal < 0 || reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static long? GetOptionalInt64(SqlDataReader reader, string columnName)
    {
        var ordinal = FindOrdinal(reader, columnName);
        return ordinal < 0 || reader.IsDBNull(ordinal) ? null : reader.GetInt64(ordinal);
    }

    private static Guid? GetOptionalGuid(SqlDataReader reader, string columnName)
    {
        var ordinal = FindOrdinal(reader, columnName);
        return ordinal < 0 || reader.IsDBNull(ordinal) ? null : reader.GetGuid(ordinal);
    }

    private static int? GetOptionalInt32(SqlDataReader reader, string columnName)
    {
        var ordinal = FindOrdinal(reader, columnName);
        return ordinal < 0 || reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }

    private static DateTime? GetOptionalDateTime(SqlDataReader reader, string columnName)
    {
        var ordinal = FindOrdinal(reader, columnName);
        return ordinal < 0 || reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
    }

    private static string? GetOptionalRowVersion(SqlDataReader reader, string columnName)
    {
        var ordinal = FindOrdinal(reader, columnName);
        return ordinal < 0 || reader.IsDBNull(ordinal) ? null : Convert.ToBase64String((byte[])reader.GetValue(ordinal));
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
