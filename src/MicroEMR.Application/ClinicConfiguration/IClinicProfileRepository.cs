namespace MicroEMR.Application.ClinicConfiguration;

public interface IClinicProfileRepository
{
    Task<ClinicProfileData?> GetAsync(CancellationToken cancellationToken = default);
    Task<ClinicProfileData> SaveAsync(SaveClinicConfigurationRequest request, long actorUserId,
        CancellationToken cancellationToken = default);
}
