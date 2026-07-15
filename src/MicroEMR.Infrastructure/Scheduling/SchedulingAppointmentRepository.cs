using System.Data;
using MicroEMR.Application.Scheduling.Contracts;
using MicroEMR.Application.Scheduling;
using MicroEMR.Application.Scheduling.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MicroEMR.Infrastructure.Scheduling;

public sealed class SchedulingAppointmentRepository : ISchedulingAppointmentRepository
{
    private readonly string _connectionString;
    private readonly ILogger<SchedulingAppointmentRepository> _logger;

    public SchedulingAppointmentRepository(
        IConfiguration configuration,
        ILogger<SchedulingAppointmentRepository> logger)
    {
        _connectionString = configuration.GetConnectionString("MicroEmrDatabase")
            ?? throw new InvalidOperationException("Connection string 'MicroEmrDatabase' was not found.");
        _logger = logger;
    }

    public async Task<ScheduleAppointmentListItemResponse> CreateAsync(
        CreateScheduleAppointmentRequest request,
        long? createdBy,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await using var command = new SqlCommand("dbo.ScheduleAppointment_Create", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.AddWithValue("@PatientUid", request.PatientUid);
        command.Parameters.AddWithValue("@PrimaryResourceUid", request.PrimaryResourceUid);
        command.Parameters.AddWithValue("@RoomResourceUid", (object?)request.RoomResourceUid ?? DBNull.Value);
        command.Parameters.AddWithValue("@StartDateTimeUtc", request.StartDateTimeUtc);
        command.Parameters.AddWithValue("@EndDateTimeUtc", request.EndDateTimeUtc);
        AddNullableString(command, "@AppointmentType", 100, request.AppointmentType);
        AddNullableString(command, "@Reason", 500, request.Reason);
        AddNullableString(command, "@Notes", 1000, request.Notes);
        command.Parameters.AddWithValue("@CreatedBy", (object?)createdBy ?? DBNull.Value);

        await connection.OpenAsync(cancellationToken);

        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException("ScheduleAppointment_Create returned no appointment record.");
            }

            return new ScheduleAppointmentListItemResponse
            {
                AppointmentUid = reader.GetGuid(reader.GetOrdinal("AppointmentUid")),
                PatientUid = request.PatientUid,
                PatientDisplayName = GetNullableString(reader, "PatientDisplayName"),
                Reason = GetNullableString(reader, "Reason"),
                AppointmentType = GetNullableString(reader, "AppointmentType"),
                StartDateTimeUtc = DateTime.SpecifyKind(reader.GetDateTime(reader.GetOrdinal("StartDateTimeUtc")), DateTimeKind.Utc),
                EndDateTimeUtc = DateTime.SpecifyKind(reader.GetDateTime(reader.GetOrdinal("EndDateTimeUtc")), DateTimeKind.Utc),
                PrimaryResourceUid = reader.GetGuid(reader.GetOrdinal("PrimaryResourceUid"))
            };
        }
        catch (SqlException exception) when (exception.Number == 51063)
        {
            throw new SchedulingConflictException(
                "The selected time conflicts with another appointment for this resource.",
                exception);
        }
        catch (SqlException exception) when (exception.Number is 51060 or 51061 or 51062 or 51064)
        {
            throw new InvalidOperationException(exception.Message, exception);
        }
        catch (SqlException exception)
        {
            _logger.LogError(exception, "Failed to create a scheduling appointment.");
            throw;
        }
    }

    public async Task<CancelScheduleAppointmentResponse?> CancelAsync(
        Guid appointmentUid,
        CancelScheduleAppointmentRequest request,
        long? cancelledBy,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await using var command = new SqlCommand("dbo.ScheduleAppointment_Cancel", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.Add(new SqlParameter("@AppointmentUid", SqlDbType.UniqueIdentifier) { Value = appointmentUid });
        AddNullableString(command, "@CancelReason", 500, request.CancelReason);
        command.Parameters.Add(new SqlParameter("@CancelledBy", SqlDbType.BigInt)
        {
            Value = (object?)cancelledBy ?? DBNull.Value
        });

        await connection.OpenAsync(cancellationToken);
        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                return null;

            return new CancelScheduleAppointmentResponse
            {
                AppointmentUid = reader.GetGuid(reader.GetOrdinal("AppointmentUid")),
                Status = reader.GetString(reader.GetOrdinal("AppointmentStatus")),
                CancelledAt = reader.IsDBNull(reader.GetOrdinal("CancelledAt"))
                    ? null
                    : DateTime.SpecifyKind(reader.GetDateTime(reader.GetOrdinal("CancelledAt")), DateTimeKind.Utc),
                CancelReason = GetNullableString(reader, "CancelReason")
            };
        }
        catch (SqlException exception) when (exception.Number == 51066)
        {
            throw new AppointmentAlreadyCancelledException("The appointment is already cancelled.", exception);
        }
        catch (SqlException exception)
        {
            _logger.LogError(exception, "Failed to cancel a scheduling appointment.");
            throw;
        }
    }

    public async Task<ScheduleAppointmentDetailsResponse?> UpdateAsync(
        Guid appointmentUid,
        UpdateScheduleAppointmentRequest request,
        long? modifiedBy,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await using var command = new SqlCommand("dbo.ScheduleAppointment_Update", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.Add(new SqlParameter("@AppointmentUid", SqlDbType.UniqueIdentifier) { Value = appointmentUid });
        command.Parameters.Add(new SqlParameter("@PrimaryResourceUid", SqlDbType.UniqueIdentifier) { Value = request.PrimaryResourceUid });
        command.Parameters.Add(new SqlParameter("@RoomResourceUid", SqlDbType.UniqueIdentifier)
        {
            Value = (object?)request.RoomResourceUid ?? DBNull.Value
        });
        command.Parameters.Add(new SqlParameter("@StartDateTimeUtc", SqlDbType.DateTime2) { Value = request.StartDateTimeUtc });
        command.Parameters.Add(new SqlParameter("@EndDateTimeUtc", SqlDbType.DateTime2) { Value = request.EndDateTimeUtc });
        AddNullableString(command, "@AppointmentType", 100, request.AppointmentType);
        AddNullableString(command, "@Reason", 500, request.Reason);
        AddNullableString(command, "@Notes", 1000, request.Notes);
        command.Parameters.Add(new SqlParameter("@ModifiedBy", SqlDbType.BigInt)
        {
            Value = (object?)modifiedBy ?? DBNull.Value
        });

        await connection.OpenAsync(cancellationToken);
        try
        {
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
                RowVersion = null
            };
        }
        catch (SqlException exception) when (exception.Number == 51063)
        {
            throw new SchedulingConflictException(
                "The selected time conflicts with another appointment for this resource.", exception);
        }
        catch (SqlException exception) when (exception.Number == 51067)
        {
            throw new AppointmentAlreadyCancelledException("Cancelled appointments cannot be edited.", exception);
        }
        catch (SqlException exception) when (exception.Number is 51060 or 51062 or 51064)
        {
            throw new InvalidOperationException("The appointment update request is invalid.", exception);
        }
        catch (SqlException exception)
        {
            _logger.LogError(exception, "Failed to update a scheduling appointment.");
            throw;
        }
    }

    public async Task<ScheduleAppointmentDetailsResponse?> RescheduleAsync(
        Guid appointmentUid,
        RescheduleAppointmentRequest request,
        long? modifiedBy,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await using var command = new SqlCommand("dbo.ScheduleAppointment_Reschedule", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.Add(new SqlParameter("@AppointmentUid", SqlDbType.UniqueIdentifier) { Value = appointmentUid });
        command.Parameters.Add(new SqlParameter("@PrimaryResourceUid", SqlDbType.UniqueIdentifier) { Value = request.PrimaryResourceUid });
        command.Parameters.Add(new SqlParameter("@RoomResourceUid", SqlDbType.UniqueIdentifier)
        {
            Value = (object?)request.RoomResourceUid ?? DBNull.Value
        });
        command.Parameters.Add(new SqlParameter("@StartDateTimeUtc", SqlDbType.DateTime2) { Value = request.StartDateTimeUtc });
        command.Parameters.Add(new SqlParameter("@EndDateTimeUtc", SqlDbType.DateTime2) { Value = request.EndDateTimeUtc });
        command.Parameters.Add(new SqlParameter("@ModifiedBy", SqlDbType.BigInt)
        {
            Value = (object?)modifiedBy ?? DBNull.Value
        });

        await connection.OpenAsync(cancellationToken);
        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                return null;

            return ReadAppointmentDetails(reader);
        }
        catch (SqlException exception) when (exception.Number == 51063)
        {
            throw new SchedulingConflictException(
                "The selected time conflicts with another appointment for this resource.", exception);
        }
        catch (SqlException exception) when (exception.Number == 51067)
        {
            throw new AppointmentAlreadyCancelledException("Cancelled appointments cannot be rescheduled.", exception);
        }
        catch (SqlException exception) when (exception.Number is 51060 or 51062 or 51064)
        {
            throw new InvalidOperationException("The appointment reschedule request is invalid.", exception);
        }
        catch (SqlException exception)
        {
            _logger.LogError(exception, "Failed to reschedule a scheduling appointment.");
            throw;
        }
    }

    public async Task<UpdateAppointmentStatusResponse?> UpdateStatusAsync(
        Guid appointmentUid,
        UpdateAppointmentStatusRequest request,
        long? updatedBy,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await using var command = new SqlCommand("dbo.ScheduleAppointment_UpdateStatus", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.Add(new SqlParameter("@AppointmentUid", SqlDbType.UniqueIdentifier) { Value = appointmentUid });
        command.Parameters.Add(new SqlParameter("@AppointmentStatus", SqlDbType.NVarChar, 30) { Value = request.Status });
        command.Parameters.Add(new SqlParameter("@UpdatedBy", SqlDbType.BigInt)
        {
            Value = (object?)updatedBy ?? DBNull.Value
        });

        await connection.OpenAsync(cancellationToken);
        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                return null;

            return new UpdateAppointmentStatusResponse
            {
                AppointmentUid = reader.GetGuid(reader.GetOrdinal("AppointmentUid")),
                Status = reader.GetString(reader.GetOrdinal("AppointmentStatus")),
                UpdatedAt = GetNullableUtcDateTime(reader, "UpdatedAt")
            };
        }
        catch (SqlException exception) when (exception.Number == 51067)
        {
            throw new AppointmentAlreadyCancelledException(
                "Cancelled appointments cannot be updated.", exception);
        }
        catch (SqlException exception) when (exception.Number == 51068)
        {
            throw new InvalidOperationException("Invalid appointment status.", exception);
        }
        catch (SqlException exception)
        {
            _logger.LogError(exception, "Failed to update a scheduling appointment status.");
            throw;
        }
    }

    private static void AddNullableString(SqlCommand command, string name, int size, string? value)
    {
        command.Parameters.Add(new SqlParameter(name, SqlDbType.NVarChar, size)
        {
            Value = string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim()
        });
    }

    private static string? GetNullableString(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static Guid? GetNullableGuid(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetGuid(ordinal);
    }

    private static long? GetNullableInt64(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt64(ordinal);
    }

    private static DateTime SpecifyUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static DateTime? GetNullableUtcDateTime(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : SpecifyUtc(reader.GetDateTime(ordinal));
    }

    private static ScheduleAppointmentDetailsResponse ReadAppointmentDetails(SqlDataReader reader) => new()
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
        RowVersion = null
    };
}
