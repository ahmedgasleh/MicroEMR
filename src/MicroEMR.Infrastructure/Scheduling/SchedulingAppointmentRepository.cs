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
}
