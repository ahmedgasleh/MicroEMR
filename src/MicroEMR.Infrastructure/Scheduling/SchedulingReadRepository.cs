using System.Data;
using MicroEMR.Application.Scheduling.Contracts;
using MicroEMR.Application.Scheduling.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MicroEMR.Infrastructure.Scheduling;

public sealed class SchedulingReadRepository : ISchedulingReadRepository
{
    private readonly string _connectionString;
    private readonly ILogger<SchedulingReadRepository> _logger;

    public SchedulingReadRepository(
        IConfiguration configuration,
        ILogger<SchedulingReadRepository> logger)
    {
        _connectionString =
            configuration.GetConnectionString("MicroEmrDatabase")
            ?? throw new InvalidOperationException(
                "Connection string 'MicroEmrDatabase' was not found.");

        _logger = logger;
    }

    public async Task<IReadOnlyList<ScheduleResourceResponse>> GetActiveResourcesAsync(
        CancellationToken cancellationToken = default)
    {
        var resources = new List<ScheduleResourceResponse>();

        await using var connection = new SqlConnection(_connectionString);
        await using var command = new SqlCommand(
            """
            IF OBJECT_ID(N'dbo.ScheduleResource_GetActive', N'P') IS NOT NULL
            BEGIN
                EXEC dbo.ScheduleResource_GetActive;
            END
            ELSE IF OBJECT_ID(N'dbo.ScheduleResource', N'U') IS NOT NULL
            BEGIN
                SELECT
                    ResourceUid,
                    ResourceType,
                    DisplayName,
                    ColorCode,
                    IsActive,
                    SortOrder
                FROM dbo.ScheduleResource
                WHERE IsActive = 1
                ORDER BY
                    ResourceType,
                    SortOrder,
                    DisplayName;
            END
            ELSE
            BEGIN
                SELECT
                    CAST(NULL AS UNIQUEIDENTIFIER) AS ResourceUid,
                    CAST(NULL AS NVARCHAR(50)) AS ResourceType,
                    CAST(NULL AS NVARCHAR(200)) AS DisplayName,
                    CAST(NULL AS NVARCHAR(20)) AS ColorCode,
                    CAST(NULL AS BIT) AS IsActive,
                    CAST(NULL AS INT) AS SortOrder
                WHERE 1 = 0;
            END;
            """,
            connection)
        {
            CommandType = CommandType.Text
        };

        await connection.OpenAsync(cancellationToken);

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            resources.Add(new ScheduleResourceResponse
            {
                ResourceUid = reader.GetGuid(reader.GetOrdinal("ResourceUid")),
                ResourceType = reader.GetString(reader.GetOrdinal("ResourceType")),
                DisplayName = reader.GetString(reader.GetOrdinal("DisplayName")),
                ColorCode = GetNullableString(reader, "ColorCode"),
                IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
                SortOrder = reader.GetInt32(reader.GetOrdinal("SortOrder"))
            });
        }

        return resources;
    }

    public async Task<IReadOnlyList<ScheduleAppointmentListItemResponse>>
        GetAppointmentsAsync(
            DateTime startUtc,
            DateTime endUtc,
            Guid? resourceUid,
            CancellationToken cancellationToken = default)
    {
        var appointments = new List<ScheduleAppointmentListItemResponse>();

        const string requiredObjectsSql = """
            SELECT
                CASE
                    WHEN OBJECT_ID(N'dbo.ScheduleAppointment', N'U') IS NOT NULL
                        AND OBJECT_ID(N'dbo.Patient', N'U') IS NOT NULL
                        AND OBJECT_ID(N'dbo.ScheduleResource', N'U') IS NOT NULL
                    THEN CAST(1 AS BIT)
                    ELSE CAST(0 AS BIT)
                END;
            """;

        const string sql = """
            SELECT
                a.AppointmentUid,
                p.PatientUid,
                NULLIF(
                    LTRIM(RTRIM(CONCAT(p.LastName, ', ', p.FirstName))),
                    ',') AS PatientDisplayName,
                p.ChartNumber,
                a.Reason,
                a.AppointmentType,
                a.StartDateTimeUtc,
                a.EndDateTimeUtc,
                sr.ResourceUid AS PrimaryResourceUid,
                sr.DisplayName AS PrimaryResourceName,
                a.AppointmentStatus AS Status
            FROM dbo.ScheduleAppointment a
            INNER JOIN dbo.Patient p
                ON p.PatientUid = a.PatientUid
            INNER JOIN dbo.ScheduleResource sr
                ON sr.ResourceId = a.PrimaryResourceId
            WHERE a.IsDeleted = 0
                AND a.AppointmentStatus <> N'Cancelled'
                AND a.StartDateTimeUtc < @EndUtc
                AND a.EndDateTimeUtc > @StartUtc
                AND
                (
                    @ResourceUid IS NULL
                    OR sr.ResourceUid = @ResourceUid
                    OR EXISTS
                    (
                        SELECT 1
                        FROM dbo.ScheduleResource AS room
                        WHERE room.ResourceId = a.RoomResourceId
                            AND room.ResourceUid = @ResourceUid
                    )
                )
            ORDER BY a.StartDateTimeUtc, a.ScheduleAppointmentId;
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using (var requiredObjectsCommand =
            new SqlCommand(requiredObjectsSql, connection))
        {
            var requiredObjectsExist =
                (bool)(await requiredObjectsCommand.ExecuteScalarAsync(
                    cancellationToken)
                    ?? false);

            if (!requiredObjectsExist)
            {
                _logger.LogInformation(
                    "Scheduling appointment events were not loaded because the read-only scheduling event schema is incomplete.");

                return appointments;
            }
        }

        await using var command = new SqlCommand(sql, connection)
        {
            CommandType = CommandType.Text
        };

        command.Parameters.Add(
            new SqlParameter("@StartUtc", SqlDbType.DateTime2)
            {
                Value = NormalizeUtc(startUtc)
            });

        command.Parameters.Add(
            new SqlParameter("@EndUtc", SqlDbType.DateTime2)
            {
                Value = NormalizeUtc(endUtc)
            });

        command.Parameters.Add(
            new SqlParameter("@ResourceUid", SqlDbType.UniqueIdentifier)
            {
                Value = resourceUid.HasValue
                    ? resourceUid.Value
                    : DBNull.Value
            });

        try
        {
            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                appointments.Add(new ScheduleAppointmentListItemResponse
                {
                    AppointmentUid =
                        reader.GetGuid(reader.GetOrdinal("AppointmentUid")),
                    PatientUid =
                        reader.GetGuid(reader.GetOrdinal("PatientUid")),
                    PatientDisplayName =
                        GetNullableString(reader, "PatientDisplayName"),
                    ChartNumber = GetNullableString(reader, "ChartNumber"),
                    Reason = GetNullableString(reader, "Reason"),
                    AppointmentType =
                        GetNullableString(reader, "AppointmentType"),
                    StartDateTimeUtc = SpecifyUtc(
                        reader.GetDateTime(
                            reader.GetOrdinal("StartDateTimeUtc"))),
                    EndDateTimeUtc = SpecifyUtc(
                        reader.GetDateTime(
                            reader.GetOrdinal("EndDateTimeUtc"))),
                    PrimaryResourceUid =
                        reader.GetGuid(reader.GetOrdinal("PrimaryResourceUid")),
                    PrimaryResourceName =
                        GetNullableString(reader, "PrimaryResourceName"),
                    Status = reader.GetString(reader.GetOrdinal("Status"))
                });
            }
        }
        catch (SqlException exception)
            when (IsMissingSchedulingReadObject(exception))
        {
            _logger.LogWarning(
                exception,
                "Scheduling appointment events were not loaded because the read-only scheduling event schema is incomplete.");

            return appointments;
        }
        catch (SqlException exception)
        {
            _logger.LogError(
                exception,
                "Failed to load scheduling appointments from {StartUtc} to {EndUtc}.",
                startUtc,
                endUtc);

            throw;
        }

        return appointments;
    }

    public async Task<ScheduleAppointmentDetailsResponse?> GetAppointmentByUidAsync(
        Guid appointmentUid,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await using var command = new SqlCommand(
            "dbo.ScheduleAppointment_GetByUid",
            connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.Add(new SqlParameter("@AppointmentUid", SqlDbType.UniqueIdentifier)
        {
            Value = appointmentUid
        });

        await connection.OpenAsync(cancellationToken);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new ScheduleAppointmentDetailsResponse
        {
            AppointmentUid = reader.GetGuid(reader.GetOrdinal("AppointmentUid")),
            PatientUid = reader.GetGuid(reader.GetOrdinal("PatientUid")),
            PrimaryResourceUid = reader.GetGuid(reader.GetOrdinal("PrimaryResourceUid")),
            RoomResourceUid = GetNullableGuid(reader, "RoomResourceUid"),
            StartDateTimeUtc = SpecifyUtc(reader.GetDateTime(reader.GetOrdinal("StartDateTimeUtc"))),
            EndDateTimeUtc = SpecifyUtc(reader.GetDateTime(reader.GetOrdinal("EndDateTimeUtc"))),
            AppointmentType = GetNullableString(reader, "AppointmentType"),
            Reason = GetNullableString(reader, "Reason"),
            Notes = GetNullableString(reader, "Notes"),
            Status = reader.GetString(reader.GetOrdinal("Status")),
            PatientDisplayName = reader.GetString(reader.GetOrdinal("PatientDisplayName")),
            ChartNumber = reader.GetString(reader.GetOrdinal("ChartNumber")),
            PrimaryResourceName = reader.GetString(reader.GetOrdinal("PrimaryResourceName")),
            RoomResourceName = GetNullableString(reader, "RoomResourceName"),
            CreatedBy = GetNullableInt64(reader, "CreatedBy"),
            CreatedByDisplayName = GetNullableString(reader, "CreatedByDisplayName"),
            CreatedAt = SpecifyUtc(reader.GetDateTime(reader.GetOrdinal("CreatedAt"))),
            UpdatedAt = GetNullableUtcDateTime(reader, "UpdatedAt"),
            RowVersion = GetNullableBase64(reader, "RowVersion")
        };
    }

    public async Task<IReadOnlyList<ScheduleMonthSummaryItemResponse>> GetMonthSummaryAsync(
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken cancellationToken = default)
    {
        var summary = new List<ScheduleMonthSummaryItemResponse>();
        await using var connection = new SqlConnection(_connectionString);
        await using var command = new SqlCommand(
            "dbo.ScheduleAppointment_GetMonthSummary", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.Add(new SqlParameter("@StartDateTimeUtc", SqlDbType.DateTime2)
        {
            Value = NormalizeUtc(startUtc)
        });
        command.Parameters.Add(new SqlParameter("@EndDateTimeUtc", SqlDbType.DateTime2)
        {
            Value = NormalizeUtc(endUtc)
        });

        await connection.OpenAsync(cancellationToken);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            summary.Add(new ScheduleMonthSummaryItemResponse
            {
                Date = reader.GetDateTime(reader.GetOrdinal("AppointmentDate")),
                AppointmentCount = reader.GetInt32(reader.GetOrdinal("AppointmentCount")),
                ProviderCount = reader.GetInt32(reader.GetOrdinal("ProviderCount")),
                Status = reader.GetString(reader.GetOrdinal("Status"))
            });
        }

        return summary;
    }

    public async Task<IReadOnlyList<AppointmentHistoryResponse>> GetHistoryAsync(
        Guid appointmentUid,
        CancellationToken cancellationToken = default)
    {
        var history = new List<AppointmentHistoryResponse>();
        await using var connection = new SqlConnection(_connectionString);
        await using var command = new SqlCommand(
            "dbo.AppointmentHistory_GetByAppointmentUid",
            connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.Add(new SqlParameter(
            "@AppointmentUid", SqlDbType.UniqueIdentifier)
        {
            Value = appointmentUid
        });

        await connection.OpenAsync(cancellationToken);
        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            history.Add(new AppointmentHistoryResponse
            {
                AppointmentHistoryUid = reader.GetGuid(reader.GetOrdinal("AppointmentHistoryUid")),
                AppointmentUid = reader.GetGuid(reader.GetOrdinal("AppointmentUid")),
                ActionType = reader.GetString(reader.GetOrdinal("ActionType")),
                ActionDescription = GetNullableString(reader, "ActionDescription"),
                OldStartDateTimeUtc = GetNullableUtcDateTime(reader, "OldStartDateTimeUtc"),
                NewStartDateTimeUtc = GetNullableUtcDateTime(reader, "NewStartDateTimeUtc"),
                OldEndDateTimeUtc = GetNullableUtcDateTime(reader, "OldEndDateTimeUtc"),
                NewEndDateTimeUtc = GetNullableUtcDateTime(reader, "NewEndDateTimeUtc"),
                OldStatus = GetNullableString(reader, "OldStatus"),
                NewStatus = GetNullableString(reader, "NewStatus"),
                OldResourceUid = GetNullableGuid(reader, "OldResourceUid"),
                NewResourceUid = GetNullableGuid(reader, "NewResourceUid"),
                Reason = GetNullableString(reader, "Reason"),
                CreatedAt = SpecifyUtc(reader.GetDateTime(reader.GetOrdinal("CreatedAt"))),
                CreatedBy = GetNullableInt64(reader, "CreatedBy"),
                CreatedByDisplayName = GetNullableString(reader, "CreatedByDisplayName")
            });
        }

        return history;
    }

    private static bool IsMissingSchedulingReadObject(SqlException exception)
    {
        foreach (SqlError error in exception.Errors)
        {
            if (error.Number is 207 or 208 or 2812)
            {
                return true;
            }
        }

        return false;
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

    private static Guid? GetNullableGuid(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetGuid(ordinal);
    }

    private static long? GetNullableInt64(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt64(ordinal);
    }

    private static DateTime? GetNullableUtcDateTime(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : SpecifyUtc(reader.GetDateTime(ordinal));
    }

    private static string? GetNullableBase64(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal)
            ? null
            : Convert.ToBase64String((byte[])reader.GetValue(ordinal));
    }

    private static DateTime NormalizeUtc(DateTime value)
    {
        if (value.Kind == DateTimeKind.Utc)
        {
            return value;
        }

        if (value.Kind == DateTimeKind.Unspecified)
        {
            return DateTime.SpecifyKind(value, DateTimeKind.Local)
                .ToUniversalTime();
        }

        return value.ToUniversalTime();
    }

    private static DateTime SpecifyUtc(DateTime value)
    {
        return value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }
}
