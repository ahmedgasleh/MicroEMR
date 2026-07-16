using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MicroEMR.Application.PatientAllergies.Contracts;
using MicroEMR.Application.PatientAllergies.Repositories;
using MicroEMR.Application.PatientAllergies;

namespace MicroEMR.Infrastructure.PatientAllergies;

public sealed class PatientAllergyRepository : IPatientAllergyRepository
{
    private readonly string _connectionString;
    private readonly ILogger<PatientAllergyRepository> _logger;

    public PatientAllergyRepository(
        IConfiguration configuration,
        ILogger<PatientAllergyRepository> logger)
    {
        _connectionString =
            configuration.GetConnectionString("MicroEmrDatabase")
            ?? throw new InvalidOperationException(
                "Connection string 'MicroEmrDatabase' was not found.");

        _logger = logger;
    }

    public async Task<IReadOnlyList<PatientAllergyListItemResponse>>
        GetByPatientUidAsync(
            Guid patientUid,
            CancellationToken cancellationToken = default)
    {
        var allergies =
            new List<PatientAllergyListItemResponse>();

        await using var connection =
            new SqlConnection(_connectionString);

        await using var command =
            new SqlCommand(
                "dbo.PatientAllergy_GetByPatientUid",
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
            allergies.Add(MapListItem(reader));
        }

        return allergies;
    }

    public async Task<PatientAllergyDetailsResponse?> GetByUidAsync(
        Guid allergyUid,
        CancellationToken cancellationToken = default)
    {
        await using var connection =
            new SqlConnection(_connectionString);

        await using var command =
            new SqlCommand(
                "dbo.PatientAllergy_GetByUid",
                connection)
            {
                CommandType = CommandType.StoredProcedure
            };

        command.Parameters.Add(
            new SqlParameter(
                "@AllergyUid",
                SqlDbType.UniqueIdentifier)
            {
                Value = allergyUid
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

    public async Task<PatientAllergyDetailsResponse> CreateAsync(
        Guid patientUid,
        CreatePatientAllergyRequest request,
        long? createdBy,
        string? createdByDisplayName,
        CancellationToken cancellationToken = default)
    {
        await using var connection =
            new SqlConnection(_connectionString);

        await using var command =
            new SqlCommand(
                "dbo.PatientAllergy_Create",
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

        AddRequiredString(
            command,
            "@AllergenName",
            SqlDbType.NVarChar,
            200,
            request.AllergenName);

        AddNullableString(
            command,
            "@AllergenType",
            SqlDbType.NVarChar,
            100,
            request.AllergenType);

        AddNullableString(
            command,
            "@Reaction",
            SqlDbType.NVarChar,
            500,
            request.Reaction);

        AddNullableString(
            command,
            "@Severity",
            SqlDbType.NVarChar,
            30,
            request.Severity);

        command.Parameters.Add(
            new SqlParameter(
                "@OnsetDate",
                SqlDbType.Date)
            {
                Value = request.OnsetDate.HasValue
                    ? request.OnsetDate.Value.Date
                    : DBNull.Value
            });

        AddNullableString(
            command,
            "@Notes",
            SqlDbType.NVarChar,
            1000,
            request.Notes);

        command.Parameters.Add(
            new SqlParameter(
                "@CreatedBy",
                SqlDbType.BigInt)
            {
                Value = createdBy.HasValue
                    ? createdBy.Value
                    : DBNull.Value
            });

        AddNullableString(
            command,
            "@CreatedByDisplayName",
            SqlDbType.NVarChar,
            200,
            createdByDisplayName);

        await connection.OpenAsync(cancellationToken);

        try
        {
            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException(
                    "PatientAllergy_Create returned no allergy record.");
            }

            return MapDetails(reader);
        }
        catch (SqlException exception)
        {
            _logger.LogError(
                exception,
                "Failed to create allergy for patient {PatientUid}.",
                patientUid);

            throw;
        }
    }

    public async Task<PatientAllergyDetailsResponse?> UpdateAsync(
        Guid patientUid,
        Guid allergyUid,
        UpdatePatientAllergyRequest request,
        long? updatedBy,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await using var command = new SqlCommand("dbo.PatientAllergy_Update", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.Add(new SqlParameter("@PatientUid", SqlDbType.UniqueIdentifier) { Value = patientUid });
        command.Parameters.Add(new SqlParameter("@AllergyUid", SqlDbType.UniqueIdentifier) { Value = allergyUid });
        AddRequiredString(command, "@AllergenName", SqlDbType.NVarChar, 200, request.AllergenName);
        AddNullableString(command, "@AllergenType", SqlDbType.NVarChar, 100, request.AllergenType);
        AddNullableString(command, "@Reaction", SqlDbType.NVarChar, 500, request.Reaction);
        AddNullableString(command, "@Severity", SqlDbType.NVarChar, 30, request.Severity);
        command.Parameters.Add(new SqlParameter("@OnsetDate", SqlDbType.Date)
        {
            Value = request.OnsetDate.HasValue ? request.OnsetDate.Value.Date : DBNull.Value
        });
        AddRequiredString(command, "@AllergyStatus", SqlDbType.NVarChar, 30, request.Status);
        AddNullableString(command, "@Notes", SqlDbType.NVarChar, 1000, request.Notes);
        command.Parameters.Add(new SqlParameter("@UpdatedBy", SqlDbType.BigInt)
        {
            Value = (object?)updatedBy ?? DBNull.Value
        });
        command.Parameters.Add(new SqlParameter("@RowVersion", SqlDbType.Timestamp)
        {
            Value = Convert.FromBase64String(request.RowVersion)
        });

        await connection.OpenAsync(cancellationToken);
        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await reader.ReadAsync(cancellationToken) ? MapDetails(reader) : null;
        }
        catch (SqlException exception) when (exception.Number == 51052)
        {
            throw new PatientAllergyConcurrencyException(
                "The allergy was changed by another user.", exception);
        }
        catch (SqlException exception)
        {
            _logger.LogError(exception, "Failed to update a patient allergy.");
            throw;
        }
    }

    private static PatientAllergyListItemResponse MapListItem(
        SqlDataReader reader)
    {
        return new PatientAllergyListItemResponse
        {
            AllergyUid = reader.GetGuid(reader.GetOrdinal("AllergyUid")),
            PatientUid = reader.GetGuid(reader.GetOrdinal("PatientUid")),
            AllergenName = reader.GetString(reader.GetOrdinal("AllergenName")),
            AllergenType = GetNullableString(reader, "AllergenType"),
            Reaction = GetNullableString(reader, "Reaction"),
            Severity = GetNullableString(reader, "Severity"),
            OnsetDate = GetNullableDateTime(reader, "OnsetDate"),
            Notes = GetNullableString(reader, "Notes"),
            Status = reader.GetString(reader.GetOrdinal("AllergyStatus")),
            CreatedBy = GetNullableInt64(reader, "CreatedBy"),
            CreatedByDisplayName = GetNullableString(reader, "CreatedByDisplayName"),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
            UpdatedAt = GetNullableDateTime(reader, "UpdatedAt")
        };
    }

    private static PatientAllergyDetailsResponse MapDetails(
        SqlDataReader reader)
    {
        return new PatientAllergyDetailsResponse
        {
            AllergyUid = reader.GetGuid(reader.GetOrdinal("AllergyUid")),
            PatientUid = reader.GetGuid(reader.GetOrdinal("PatientUid")),
            AllergenName = reader.GetString(reader.GetOrdinal("AllergenName")),
            AllergenType = GetNullableString(reader, "AllergenType"),
            Reaction = GetNullableString(reader, "Reaction"),
            Severity = GetNullableString(reader, "Severity"),
            OnsetDate = GetNullableDateTime(reader, "OnsetDate"),
            Notes = GetNullableString(reader, "Notes"),
            Status = reader.GetString(reader.GetOrdinal("AllergyStatus")),
            CreatedBy = GetNullableInt64(reader, "CreatedBy"),
            CreatedByDisplayName = GetNullableString(reader, "CreatedByDisplayName"),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
            UpdatedAt = GetNullableDateTime(reader, "UpdatedAt"),
            RowVersion = GetRowVersion(reader, "RowVersion")
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
        var ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? null
            : reader.GetString(ordinal);
    }

    private static long? GetNullableInt64(
        SqlDataReader reader,
        string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? null
            : reader.GetInt64(ordinal);
    }

    private static DateTime? GetNullableDateTime(
        SqlDataReader reader,
        string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? null
            : reader.GetDateTime(ordinal);
    }

    private static string GetRowVersion(
        SqlDataReader reader,
        string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? string.Empty
            : Convert.ToBase64String(
                (byte[])reader.GetValue(ordinal));
    }
}
