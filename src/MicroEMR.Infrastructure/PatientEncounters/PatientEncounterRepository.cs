using System.Data;
using Microsoft.Data.SqlClient;
using MicroEMR.Infrastructure.Tenancy;
using Microsoft.Extensions.Logging;
using MicroEMR.Application.PatientEncounters.Contracts;
using MicroEMR.Application.PatientEncounters.Repositories;
using MicroEMR.Application.PatientEncounters;
using MicroEMR.Application.Scheduling;

namespace MicroEMR.Infrastructure.PatientEncounters;

public sealed class PatientEncounterRepository
    : IPatientEncounterRepository
{
    private readonly ITenantSqlConnectionFactory _connectionFactory;
    private readonly ILogger<PatientEncounterRepository> _logger;

    public PatientEncounterRepository(
        ITenantSqlConnectionFactory connectionFactory,
        ILogger<PatientEncounterRepository> logger)
    {
        _connectionFactory = connectionFactory;

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
            await _connectionFactory.OpenConnectionAsync(cancellationToken);

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
            await _connectionFactory.OpenConnectionAsync(cancellationToken);

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



        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return MapDetails(reader);
    }

    public async Task<IReadOnlyList<PatientEncounterHistoryResponse>> GetHistoryAsync(
        Guid patientUid,
        Guid encounterUid,
        CancellationToken cancellationToken = default)
    {
        var history = new List<PatientEncounterHistoryResponse>();
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand(
            "dbo.PatientEncounterHistory_GetByEncounterUid", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.Add("@PatientUid", SqlDbType.UniqueIdentifier).Value = patientUid;
        command.Parameters.Add("@EncounterUid", SqlDbType.UniqueIdentifier).Value = encounterUid;


        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            history.Add(new PatientEncounterHistoryResponse
            {
                EncounterHistoryUid = reader.GetGuid(reader.GetOrdinal("EncounterHistoryUid")),
                EncounterUid = reader.GetGuid(reader.GetOrdinal("EncounterUid")),
                PatientUid = reader.GetGuid(reader.GetOrdinal("PatientUid")),
                ActionType = reader.GetString(reader.GetOrdinal("ActionType")),
                ActionDescription = GetNullableString(reader, "ActionDescription"),
                OldStatus = GetNullableString(reader, "OldStatus"),
                NewStatus = GetNullableString(reader, "NewStatus"),
                Reason = GetNullableString(reader, "Reason"),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                CreatedBy = GetNullableInt64(reader, "CreatedBy"),
                CreatedByDisplayName = GetNullableString(reader, "CreatedByDisplayName")
            });
        }

        return history;
    }

    public async Task<IReadOnlyList<PatientEncounterAddendumResponse>> GetAddendumsAsync(
        Guid patientUid,
        Guid encounterUid,
        CancellationToken cancellationToken = default)
    {
        var addendums = new List<PatientEncounterAddendumResponse>();
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand(
            "dbo.PatientEncounterAddendum_GetByEncounterUid", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.Add("@PatientUid", SqlDbType.UniqueIdentifier).Value = patientUid;
        command.Parameters.Add("@EncounterUid", SqlDbType.UniqueIdentifier).Value = encounterUid;


        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            addendums.Add(MapAddendum(reader));

        return addendums;
    }

    public async Task<PatientEncounterAddendumResponse?> CreateAddendumAsync(
        Guid patientUid,
        Guid encounterUid,
        CreateEncounterAddendumRequest request,
        long? createdBy,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand(
            "dbo.PatientEncounterAddendum_Create", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.Add("@PatientUid", SqlDbType.UniqueIdentifier).Value = patientUid;
        command.Parameters.Add("@EncounterUid", SqlDbType.UniqueIdentifier).Value = encounterUid;
        command.Parameters.Add("@AddendumText", SqlDbType.NVarChar, -1).Value = request.AddendumText;
        command.Parameters.Add("@CreatedBy", SqlDbType.BigInt).Value = (object?)createdBy ?? DBNull.Value;


        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await reader.ReadAsync(cancellationToken) ? MapAddendum(reader) : null;
        }
        catch (SqlException exception) when (exception.Number == 51075)
        {
            throw new EncounterAddendumNotAllowedException(
                "Addendums can only be added to signed encounters.", exception);
        }
        catch (SqlException exception)
        {
            _logger.LogError(exception, "Failed to create an encounter addendum.");
            throw;
        }
    }

    public async Task<PatientEncounterDetailsResponse> CreateAsync(
        Guid patientUid,
        CreatePatientEncounterRequest request,
        long? createdBy,
        string? createdByDisplayName,
        CancellationToken cancellationToken = default)
    {
        await using var connection =
            await _connectionFactory.OpenConnectionAsync(cancellationToken);

        await using var command =
            new SqlCommand(
                request.TemplateUid.HasValue ? "dbo.PatientEncounter_CreateStructured" : "dbo.PatientEncounter_Create",
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

        if (request.TemplateUid.HasValue)
        {
            command.Parameters.Add("@TemplateUid", SqlDbType.UniqueIdentifier).Value = request.TemplateUid.Value;
            command.Parameters.Add("@TemplateVersionUid", SqlDbType.UniqueIdentifier).Value = request.ResolvedTemplateVersionUid!.Value;
            AddNullableString(command, "@StructuredDataJson", SqlDbType.NVarChar, -1, request.StructuredDataJson);
            AddNullableString(command, "@SubjectiveNote", SqlDbType.NVarChar, -1, request.SubjectiveSnapshot);
            AddNullableString(command, "@ObjectiveNote", SqlDbType.NVarChar, -1, request.ObjectiveSnapshot);
            AddNullableString(command, "@AssessmentNote", SqlDbType.NVarChar, -1, request.AssessmentSnapshot);
            AddNullableString(command, "@PlanNote", SqlDbType.NVarChar, -1, request.PlanSnapshot);
        }
        else

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
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
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

    public async Task<PatientEncounterDetailsResponse?> UpdateSoapNoteAsync(
        Guid patientUid,
        Guid encounterUid,
        UpdateEncounterSoapNoteRequest request,
        long? updatedBy,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand("dbo.PatientEncounter_UpdateSoapNote", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.Add("@PatientUid", SqlDbType.UniqueIdentifier).Value = patientUid;
        command.Parameters.Add("@EncounterUid", SqlDbType.UniqueIdentifier).Value = encounterUid;
        AddNullableString(command, "@SubjectiveNote", SqlDbType.NVarChar, -1, request.SubjectiveNote);
        AddNullableString(command, "@ObjectiveNote", SqlDbType.NVarChar, -1, request.ObjectiveNote);
        AddNullableString(command, "@AssessmentNote", SqlDbType.NVarChar, -1, request.AssessmentNote);
        AddNullableString(command, "@PlanNote", SqlDbType.NVarChar, -1, request.PlanNote);
        command.Parameters.Add("@UpdatedBy", SqlDbType.BigInt).Value = (object?)updatedBy ?? DBNull.Value;


        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await reader.ReadAsync(cancellationToken) ? MapDetails(reader) : null;
        }
        catch (SqlException exception) when (exception.Number == 51071)
        {
            throw new EncounterNoteNotEditableException(
                "The encounter note cannot be edited.", exception);
        }
        catch (SqlException exception)
        {
            _logger.LogError(exception, "Failed to update an encounter SOAP note.");
            throw;
        }
    }

    public async Task<PatientEncounterDetailsResponse?> UpdateStructuredDataAsync(
        Guid patientUid, Guid encounterUid, UpdateEncounterStructuredDataRequest request,
        string? subjective, string? objective, string? assessment, string? plan,
        long? updatedBy, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand("dbo.PatientEncounter_UpdateStructuredData", connection)
        { CommandType = CommandType.StoredProcedure };
        command.Parameters.Add("@PatientUid", SqlDbType.UniqueIdentifier).Value = patientUid;
        command.Parameters.Add("@EncounterUid", SqlDbType.UniqueIdentifier).Value = encounterUid;
        command.Parameters.Add("@StructuredDataJson", SqlDbType.NVarChar, -1).Value = request.StructuredDataJson;
        AddNullableString(command, "@SubjectiveNote", SqlDbType.NVarChar, -1, subjective);
        AddNullableString(command, "@ObjectiveNote", SqlDbType.NVarChar, -1, objective);
        AddNullableString(command, "@AssessmentNote", SqlDbType.NVarChar, -1, assessment);
        AddNullableString(command, "@PlanNote", SqlDbType.NVarChar, -1, plan);
        command.Parameters.Add("@ExpectedRowVersion", SqlDbType.Timestamp).Value = Convert.FromBase64String(request.RowVersion);
        command.Parameters.Add("@UpdatedBy", SqlDbType.BigInt).Value = (object?)updatedBy ?? DBNull.Value;
        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await reader.ReadAsync(cancellationToken) ? MapDetails(reader) : null;
        }
        catch (SqlException exception) when (exception.Number is 51071 or 51073)
        {
            throw new EncounterNoteNotEditableException("The encounter changed or cannot be edited.", exception);
        }
    }

    public async Task<PatientEncounterDetailsResponse?> SignAsync(
        Guid patientUid,
        Guid encounterUid,
        long? signedBy,
        AppointmentStatus expectedAppointmentStatus,
        AppointmentStatus completedAppointmentStatus,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
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
        command.Parameters.Add(new SqlParameter(
            "@ExpectedAppointmentStatus", SqlDbType.NVarChar, 30)
        {
            Value = AppointmentStatusMapper.ToStorageValue(expectedAppointmentStatus)
        });
        command.Parameters.Add(new SqlParameter(
            "@CompletedAppointmentStatus", SqlDbType.NVarChar, 30)
        {
            Value = AppointmentStatusMapper.ToStorageValue(completedAppointmentStatus)
        });

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
        catch (SqlException exception) when (exception.Number == 51085)
        {
            throw new LinkedAppointmentCannotBeCompletedException(
                "The linked appointment cannot be completed in its current status.",
                exception);
        }
        catch (SqlException exception) when (exception.Number == 51086)
        {
            throw new LinkedAppointmentNotFoundException(
                "The linked appointment was not found.",
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
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
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
        catch (SqlException exception) when (exception.Number == 51083)
        {
            throw new AppointmentNoShowException(
                "No-show appointments cannot start encounters.", exception);
        }
        catch (SqlException exception) when (exception.Number == 51084)
        {
            throw new AppointmentCannotStartEncounterException(
                "The appointment cannot start an encounter from its current status.", exception);
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

    private static PatientEncounterAddendumResponse MapAddendum(SqlDataReader reader) => new()
    {
        EncounterAddendumUid = reader.GetGuid(reader.GetOrdinal("EncounterAddendumUid")),
        EncounterUid = reader.GetGuid(reader.GetOrdinal("EncounterUid")),
        PatientUid = reader.GetGuid(reader.GetOrdinal("PatientUid")),
        AddendumText = reader.GetString(reader.GetOrdinal("AddendumText")),
        CreatedAt = DateTime.SpecifyKind(
            reader.GetDateTime(reader.GetOrdinal("CreatedAt")), DateTimeKind.Utc),
        CreatedBy = GetNullableInt64(reader, "CreatedBy"),
        CreatedByDisplayName = GetNullableString(reader, "CreatedByDisplayName")
    };

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

            SubjectiveNote = GetOptionalString(reader, "SubjectiveNote"),
            ObjectiveNote = GetOptionalString(reader, "ObjectiveNote"),
            AssessmentNote = GetOptionalString(reader, "AssessmentNote"),
            PlanNote = GetOptionalString(reader, "PlanNote"),
            TemplateUid = GetOptionalGuid(reader, "TemplateUid"),
            TemplateVersionUid = GetOptionalGuid(reader, "TemplateVersionUid"),
            StructuredDataJson = GetOptionalString(reader, "StructuredDataJson"),

            SignedAt =
                GetOptionalDateTime(reader, "SignedAt"),

            SignedBy =
                GetOptionalInt64(reader, "SignedBy"),

            SignedByDisplayName =
                GetOptionalString(reader, "SignedByDisplayName"),

            RowVersion =
                GetRowVersion(reader, "RowVersion"),

            AppointmentUid = GetOptionalGuid(reader, "AppointmentUid"),
            AppointmentStartDateTime = GetOptionalUtcDateTime(reader, "AppointmentStartDateTime"),
            AppointmentEndDateTime = GetOptionalUtcDateTime(reader, "AppointmentEndDateTime"),
            AppointmentReason = GetOptionalString(reader, "AppointmentReason"),
            AppointmentProviderDisplayName = GetOptionalString(reader, "AppointmentProviderDisplayName"),
            AppointmentStatus = GetOptionalString(reader, "AppointmentStatus")
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

    private static Guid? GetOptionalGuid(SqlDataReader reader, string columnName)
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
                    : reader.GetGuid(ordinal);
            }
        }

        return null;
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

    private static DateTime? GetOptionalUtcDateTime(
        SqlDataReader reader,
        string columnName)
    {
        var value = GetOptionalDateTime(reader, columnName);
        return value.HasValue
            ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
            : null;
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
