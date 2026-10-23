using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MicroEMR.Api;

public static class ApiSwaggerConfiguration
{
    public static void Configure(SwaggerGenOptions options)
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "MicroEMR API",
            Version = "v1",
            Description = "API for MicroEMR patients, scheduling and charts."
        });
        var defaultSchemaId = new SchemaGeneratorOptions().SchemaIdSelector;
        options.CustomSchemaIds(type =>
            type == typeof(Application.Scheduling.Contracts.RescheduleAppointmentRequest)
                ? "SchedulingContractRescheduleAppointmentRequest"
                : defaultSchemaId(type));
    }
}
