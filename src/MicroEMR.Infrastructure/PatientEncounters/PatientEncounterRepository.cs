using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MicroEMR.Application.PatientEncounters.Contracts;
using MicroEMR.Application.PatientEncounters.Repositories;
using MicroEMR.Application.PatientEncounters;

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

    public async Task<PatientEncounterDetailsResponse?> UpdateNoteAsync(
        Guid patientUid,
        Guid encounterUid,
        UpdateEncounterNoteRequest request,
        long? updatedBy,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await using var command = new SqlCommand(
            "dbo.PatientEncounter_UpdateNote",
            connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.Add(new SqlParameter(
            "@PatientUid", SqlDbType.UniqueIdentifier)
        {
            Value = patientUid
        });
        command.Parameters.Add(new SqlParameter(
            "@EncounterUid", SqlDbType.UniqueIdentifier)
        {
            Value = encounterUid
        });
        command.Parameters.Add(new SqlParameter(
            "@EncounterNotes", SqlDbType.NVarChar, -1)
        {
            Value = string.IsNullOrEmpty(request.Notes)
                ? DBNull.Value
                : request.Notes
        });
        command.Parameters.Add(new SqlParameter(
            "@UpdatedBy", SqlDbType.BigInt)
        {
            Value = (object?)updatedBy ?? DBNull.Value
        });

        await connection.OpenAsync(cancellationToken);

        try
        {
            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);

            return await reader.ReadAsync(cancellationToken)
                ? MapDetails(reader)
                : null;
        }
        catch (SqlException exception) when (exception.Number == 51071)
        {
            throw new EncounterNoteNotEditableException(
                "The encounter note cannot be edited in its current status.",
                exception);
        }
        catch (SqlException exception)
        {
            _logger.LogError(
                exception,
                "Failed to update the note for encounter {EncounterUid}.",
                encounterUid);
            throw;
        }
    }

    public async Task<PatientEncounterDetailsResponse?> SignAsync(
        Guid patientUid,
        Guid encounterUid,
        long? signedBy,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await using var command = new SqlCommand(
            "dbo.PatientEncounter_Sign",
            connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.Add(new SqlParameter(
            "@PatientUid", SqlDbType.UniqueIdentifier)
        {
            Value = patientUid
        });
        command.Parameters.Add(new SqlParameter(
            "@EncounterUid", SqlDbType.UniqueIdentifier)
        {
            Value = encounterUid
        });
        command.Parameters.Add(new SqlParameter(
            "@SignedBy", SqlDbType.BigInt)
        {
            Value = (object?)signedBy ?? DBNull.Value
        });

        await connection.OpenAsync(cancellationToken);

        try
        {
            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);

            return await reader.ReadAsync(cancellationToken)
                ? MapDetails(reader)
                : null;
        }
        catch (SqlException exception) when (exception.Number == 51072)
        {
            throw new EncounterCannotBeSignedException(
                "The encounter cannot be signed in its current status.",
                exception);
        }
        catch (SqlException exception)
        {
            _logger.LogError(
                exception,
                "Failed to sign encounter {EncounterUid}.",
                encounterUid);
            throw;
        }
    }

    public async Task<StartEncounterFromAppointmentResponse?> StartFromAppointmentAsync(
        Guid appointmentUid,
        long? createdBy,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await using var command = new SqlCommand(
            "dbo.PatientEncounter_StartFromAppointment", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.Add(new SqlParameter(
            "@AppointmentUid", SqlDbType.UniqueIdentifier)
        {
            Value = appointmentUid
        });
        command.Parameters.Add(new SqlParameter("@CreatedBy", SqlDbType.BigInt)
        {
            Value = (object?)createdBy ?? DBNull.Value
        });

        await connection.OpenAsync(cancellationToken);
        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                return null;

            return new StartEncounterFromAppointmentResponse
            {
                EncounterUid = reader.GetGuid(reader.GetOrdinal("EncounterUid")),
                PatientUid = reader.GetGuid(reader.GetOrdinal("PatientUid")),
                AppointmentUid = reader.GetGuid(reader.GetOrdinal("AppointmentUid")),
                EncounterDate = reader.GetDateTime(reader.GetOrdinal("EncounterDate")),
                EncounterType = GetOptionalString(reader, "EncounterType"),
                ReasonForVisit = GetOptionalString(reader, "ReasonForVisit"),
                Status = reader.GetString(reader.GetOrdinal("Status")),
                WasCreated = reader.GetBoolean(reader.GetOrdinal("WasCreated"))
            };
        }
        catch (SqlException exception) when (exception.Number == 51069)
        {
            throw new AppointmentCancelledException(
                "Cancelled appointments cannot start encounters.", exception);
        }
        catch (SqlException exception) when (exception.Number == 51070)
        {
            throw new AppointmentCompletedException(
                "Completed appointments cannot start new encounters.", exception);
        }
        catch (SqlException exception)
        {
            _logger.LogError(exception, "Failed to start an encounter from an appointment.");
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

            Notes =
                GetOptionalString(reader, "EncounterNotes"),

            SignedAt =
                GetOptionalDateTime(reader, "SignedAt"),

            SignedBy =
                GetOptionalInt64(reader, "SignedBy"),

            SignedByDisplayName =
                GetOptionalString(reader, "SignedByDisplayName"),

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

    private static string? GetOptionalString(
        SqlDataReader reader,
        string columnName)
    {
        for (var ordinal = 0; ordinal < reader.FieldCount; ordinal++)
        {
            if (!string.Equals(
                    reader.GetName(ordinal),
                    columnName,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return reader.IsDBNull(ordinal)
                ? null
                : reader.GetString(ordinal);
        }

        return null;
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

    private static DateTime? GetOptionalDateTime(
        SqlDataReader reader,
        string columnName)
    {
        for (var ordinal = 0; ordinal < reader.FieldCount; ordinal++)
        {
            if (string.Equals(
                    reader.GetName(ordinal),
                    columnName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return reader.IsDBNull(ordinal)
                    ? null
                    : reader.GetDateTime(ordinal);
            }
        }

        return null;
    }

    private static long? GetOptionalInt64(
        SqlDataReader reader,
        string columnName)
    {
        for (var ordinal = 0; ordinal < reader.FieldCount; ordinal++)
        {
            if (string.Equals(
                    reader.GetName(ordinal),
                    columnName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return reader.IsDBNull(ordinal)
                    ? null
                    : reader.GetInt64(ordinal);
            }
        }

        return null;
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
