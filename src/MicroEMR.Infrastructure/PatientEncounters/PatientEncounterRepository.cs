using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MicroEMR.Application.PatientEncounters.Contracts;
using MicroEMR.Application.PatientEncounters.Repositories;

namespace MicroEMR.Infrastructure.PatientEncounters;

public sealed class PatientEncounterRepository
    : IPatientEncounterRepository
{
    private readonly string _connectionString;
    private readonly ILogger<PatientEncounterRepository> _logger;

    public PatientEncounterRepository(
        IConfiguration configuration,
        ILogger<PatientEncounterRepository> logger)
    {
        _connectionString =
            configuration.GetConnectionString("MicroEmrDatabase")
            ?? throw new InvalidOperationException(
                "Connection string 'MicroEmrDatabase' was not found.");

        _logger = logger;
    }

    public async Task<IReadOnlyList<PatientEncounterListItemResponse>>
        GetByPatientUidAsync(
            Guid patientUid,
            CancellationToken cancellationToken = default)
    {
        var encounters =
            new List<PatientEncounterListItemResponse>();

        await using var connection =
            new SqlConnection(_connectionString);

        await using var command =
            new SqlCommand(
                "dbo.PatientEncounter_GetByPatientUid",
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
            encounters.Add(MapListItem(reader));
        }

        return encounters;
    }

    public async Task<PatientEncounterDetailsResponse?> GetByUidAsync(
        Guid encounterUid,
        CancellationToken cancellationToken = default)
    {
        await using var connection =
            new SqlConnection(_connectionString);

        await using var command =
            new SqlCommand(
                "dbo.PatientEncounter_GetByUid",
                connection)
            {
                CommandType = CommandType.StoredProcedure
            };

        command.Parameters.Add(
            new SqlParameter(
                "@EncounterUid",
                SqlDbType.UniqueIdentifier)
            {
                Value = encounterUid
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

    public async Task<PatientEncounterDetailsResponse> CreateAsync(
        Guid patientUid,
        CreatePatientEncounterRequest request,
        long? createdBy,
        string? createdByDisplayName,
        CancellationToken cancellationToken = default)
    {
        await using var connection =
            new SqlConnection(_connectionString);

        await using var command =
            new SqlCommand(
                "dbo.PatientEncounter_Create",
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
                "@EncounterDateUtc",
                SqlDbType.DateTime2)
            {
                Value = request.EncounterDateUtc
            });

        AddRequiredString(
            command,
            "@EncounterType",
            SqlDbType.NVarChar,
            100,
            request.EncounterType);

        AddNullableString(
            command,
            "@ReasonForVisit",
            SqlDbType.NVarChar,
            500,
            request.ReasonForVisit);

        AddNullableString(
            command,
            "@LocationName",
            SqlDbType.NVarChar,
            200,
            request.LocationName);

        AddNullableString(
            command,
            "@ProviderName",
            SqlDbType.NVarChar,
            200,
            request.ProviderName);

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
                    "PatientEncounter_Create returned no encounter record.");
            }

            return MapDetails(reader);
        }
        catch (SqlException exception)
        {
            _logger.LogError(
                exception,
                "Failed to create encounter for patient {PatientUid}.",
                patientUid);

            throw;
        }
    }

    private static PatientEncounterListItemResponse MapListItem(
        SqlDataReader reader)
    {
        return new PatientEncounterListItemResponse
        {
            EncounterUid =
                reader.GetGuid(reader.GetOrdinal("EncounterUid")),

            PatientUid =
                reader.GetGuid(reader.GetOrdinal("PatientUid")),

            EncounterDateUtc =
                reader.GetDateTime(reader.GetOrdinal("EncounterDateUtc")),

            EncounterType =
                reader.GetString(reader.GetOrdinal("EncounterType")),

            ReasonForVisit =
                GetNullableString(reader, "ReasonForVisit"),

            LocationName =
                GetNullableString(reader, "LocationName"),

            ProviderName =
                GetNullableString(reader, "ProviderName"),

            Status =
                reader.GetString(reader.GetOrdinal("EncounterStatus")),

            CreatedBy =
                GetNullableInt64(reader, "CreatedBy"),

            CreatedByDisplayName =
                GetNullableString(reader, "CreatedByDisplayName"),

            CreatedAt =
                reader.GetDateTime(reader.GetOrdinal("CreatedAt")),

            UpdatedAt =
                GetNullableDateTime(reader, "UpdatedAt")
        };
    }

    private static PatientEncounterDetailsResponse MapDetails(
        SqlDataReader reader)
    {
        return new PatientEncounterDetailsResponse
        {
            EncounterUid =
                reader.GetGuid(reader.GetOrdinal("EncounterUid")),

            PatientUid =
                reader.GetGuid(reader.GetOrdinal("PatientUid")),

            EncounterDateUtc =
                reader.GetDateTime(reader.GetOrdinal("EncounterDateUtc")),

            EncounterType =
                reader.GetString(reader.GetOrdinal("EncounterType")),

            ReasonForVisit =
                GetNullableString(reader, "ReasonForVisit"),

            LocationName =
                GetNullableString(reader, "LocationName"),

            ProviderName =
                GetNullableString(reader, "ProviderName"),

            Status =
                reader.GetString(reader.GetOrdinal("EncounterStatus")),

            CreatedBy =
                GetNullableInt64(reader, "CreatedBy"),

            CreatedByDisplayName =
                GetNullableString(reader, "CreatedByDisplayName"),

            CreatedAt =
                reader.GetDateTime(reader.GetOrdinal("CreatedAt")),

            UpdatedAt =
                GetNullableDateTime(reader, "UpdatedAt"),

            RowVersion =
                GetRowVersion(reader, "RowVersion")
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

    private static string GetRowVersion(
        SqlDataReader reader,
        string columnName)
    {
        var ordinal =
            reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? string.Empty
            : Convert.ToBase64String(
                (byte[])reader.GetValue(ordinal));
    }
}
