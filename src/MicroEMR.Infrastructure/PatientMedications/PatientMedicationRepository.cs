using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MicroEMR.Application.PatientMedications.Contracts;
using MicroEMR.Application.PatientMedications.Repositories;

namespace MicroEMR.Infrastructure.PatientMedications;

public sealed class PatientMedicationRepository : IPatientMedicationRepository
{
    private readonly string _connectionString;
    private readonly ILogger<PatientMedicationRepository> _logger;

    public PatientMedicationRepository(
        IConfiguration configuration,
        ILogger<PatientMedicationRepository> logger)
    {
        _connectionString =
            configuration.GetConnectionString("MicroEmrDatabase")
            ?? throw new InvalidOperationException(
                "Connection string 'MicroEmrDatabase' was not found.");

        _logger = logger;
    }

    public async Task<IReadOnlyList<PatientMedicationListItemResponse>>
        GetByPatientUidAsync(
            Guid patientUid,
            CancellationToken cancellationToken = default)
    {
        var medications =
            new List<PatientMedicationListItemResponse>();

        await using var connection =
            new SqlConnection(_connectionString);

        await using var command =
            new SqlCommand(
                "dbo.PatientMedication_GetByPatientUid",
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
            medications.Add(MapListItem(reader));
        }

        return medications;
    }

    public async Task<PatientMedicationDetailsResponse?> GetByUidAsync(
        Guid medicationUid,
        CancellationToken cancellationToken = default)
    {
        await using var connection =
            new SqlConnection(_connectionString);

        await using var command =
            new SqlCommand(
                "dbo.PatientMedication_GetByUid",
                connection)
            {
                CommandType = CommandType.StoredProcedure
            };

        command.Parameters.Add(
            new SqlParameter(
                "@MedicationUid",
                SqlDbType.UniqueIdentifier)
            {
                Value = medicationUid
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

    public async Task<PatientMedicationDetailsResponse> CreateAsync(
        Guid patientUid,
        CreatePatientMedicationRequest request,
        long? createdBy,
        string? createdByDisplayName,
        CancellationToken cancellationToken = default)
    {
        await using var connection =
            new SqlConnection(_connectionString);

        await using var command =
            new SqlCommand(
                "dbo.PatientMedication_Create",
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
            "@MedicationName",
            SqlDbType.NVarChar,
            200,
            request.MedicationName);

        AddNullableString(
            command,
            "@Strength",
            SqlDbType.NVarChar,
            100,
            request.Strength);

        AddNullableString(
            command,
            "@DosageForm",
            SqlDbType.NVarChar,
            100,
            request.DosageForm);

        AddNullableString(
            command,
            "@Route",
            SqlDbType.NVarChar,
            100,
            request.Route);

        AddNullableString(
            command,
            "@Directions",
            SqlDbType.NVarChar,
            500,
            request.Directions);

        AddNullableString(
            command,
            "@Frequency",
            SqlDbType.NVarChar,
            100,
            request.Frequency);

        AddNullableDate(
            command,
            "@StartDate",
            request.StartDate);

        AddNullableDate(
            command,
            "@EndDate",
            request.EndDate);

        AddNullableString(
            command,
            "@Indication",
            SqlDbType.NVarChar,
            300,
            request.Indication);

        AddNullableString(
            command,
            "@PrescriberName",
            SqlDbType.NVarChar,
            200,
            request.PrescriberName);

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
                    "PatientMedication_Create returned no medication record.");
            }

            return MapDetails(reader);
        }
        catch (SqlException exception)
        {
            _logger.LogError(
                exception,
                "Failed to create medication for patient {PatientUid}.",
                patientUid);

            throw;
        }
    }

    private static PatientMedicationListItemResponse MapListItem(
        SqlDataReader reader)
    {
        return new PatientMedicationListItemResponse
        {
            MedicationUid = reader.GetGuid(reader.GetOrdinal("MedicationUid")),
            PatientUid = reader.GetGuid(reader.GetOrdinal("PatientUid")),
            MedicationName = reader.GetString(reader.GetOrdinal("MedicationName")),
            Strength = GetNullableString(reader, "Strength"),
            DosageForm = GetNullableString(reader, "DosageForm"),
            Route = GetNullableString(reader, "Route"),
            Directions = GetNullableString(reader, "Directions"),
            Frequency = GetNullableString(reader, "Frequency"),
            StartDate = GetNullableDateTime(reader, "StartDate"),
            EndDate = GetNullableDateTime(reader, "EndDate"),
            Indication = GetNullableString(reader, "Indication"),
            PrescriberName = GetNullableString(reader, "PrescriberName"),
            Notes = GetNullableString(reader, "Notes"),
            Status = reader.GetString(reader.GetOrdinal("MedicationStatus")),
            CreatedBy = GetNullableInt64(reader, "CreatedBy"),
            CreatedByDisplayName = GetNullableString(reader, "CreatedByDisplayName"),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
            UpdatedAt = GetNullableDateTime(reader, "UpdatedAt")
        };
    }

    private static PatientMedicationDetailsResponse MapDetails(
        SqlDataReader reader)
    {
        return new PatientMedicationDetailsResponse
        {
            MedicationUid = reader.GetGuid(reader.GetOrdinal("MedicationUid")),
            PatientUid = reader.GetGuid(reader.GetOrdinal("PatientUid")),
            MedicationName = reader.GetString(reader.GetOrdinal("MedicationName")),
            Strength = GetNullableString(reader, "Strength"),
            DosageForm = GetNullableString(reader, "DosageForm"),
            Route = GetNullableString(reader, "Route"),
            Directions = GetNullableString(reader, "Directions"),
            Frequency = GetNullableString(reader, "Frequency"),
            StartDate = GetNullableDateTime(reader, "StartDate"),
            EndDate = GetNullableDateTime(reader, "EndDate"),
            Indication = GetNullableString(reader, "Indication"),
            PrescriberName = GetNullableString(reader, "PrescriberName"),
            Notes = GetNullableString(reader, "Notes"),
            Status = reader.GetString(reader.GetOrdinal("MedicationStatus")),
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

    private static void AddNullableDate(
        SqlCommand command,
        string parameterName,
        DateTime? value)
    {
        command.Parameters.Add(
            new SqlParameter(
                parameterName,
                SqlDbType.Date)
            {
                Value = value.HasValue
                    ? value.Value.Date
                    : DBNull.Value
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
