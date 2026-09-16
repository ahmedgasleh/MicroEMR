using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using MicroEMR.Application.Scheduling;

namespace MicroEMR.Api.Controllers;

internal static class SchedulingExceptionResponses
{
    public static bool IsExpected(Exception exception) => exception switch
    {
        SchedulingConflictException or SchedulingBlockedTimeConflictException or
            AppointmentAlreadyCancelledException or AppointmentStatusConcurrencyException or
            AppointmentStatusTransitionException or KeyNotFoundException or ArgumentException => true,
        InvalidOperationException { InnerException: SqlException sql }
            when sql.Number is 51060 or 51061 or 51062 or 51064 or 51068 => true,
        _ => false
    };

    public static ActionResult ToResult(Exception exception)
    {
        var (status, message) = exception switch
        {
            SchedulingConflictException or SchedulingBlockedTimeConflictException =>
                (StatusCodes.Status409Conflict, "The requested time is no longer available."),
            AppointmentAlreadyCancelledException or AppointmentStatusConcurrencyException or
                AppointmentStatusTransitionException =>
                (StatusCodes.Status409Conflict, "The appointment state changed. Reload and try again."),
            KeyNotFoundException => (StatusCodes.Status404NotFound, "The requested scheduling record was not found."),
            InvalidOperationException { InnerException: SqlException { Number: 51061 or 51062 } } =>
                (StatusCodes.Status404NotFound, "The requested scheduling record was not found."),
            _ => (StatusCodes.Status400BadRequest, "The scheduling request is invalid.")
        };
        return new ObjectResult(new { message }) { StatusCode = status };
    }
}
