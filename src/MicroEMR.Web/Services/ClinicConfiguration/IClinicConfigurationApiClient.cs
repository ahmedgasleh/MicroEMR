using MicroEMR.Web.Models.ClinicConfiguration;

namespace MicroEMR.Web.Services.ClinicConfiguration;

public interface IClinicConfigurationApiClient
{
    Task<ClinicConfigurationViewModel> GetAsync(CancellationToken cancellationToken = default);
    Task<ClinicConfigurationViewModel> SaveAsync(SaveClinicConfigurationRequest request,
        CancellationToken cancellationToken = default);
}
