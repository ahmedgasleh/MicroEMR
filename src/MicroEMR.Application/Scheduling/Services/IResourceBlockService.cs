using MicroEMR.Application.Scheduling.DTOs;

namespace MicroEMR.Application.Scheduling.Services;

public interface IResourceBlockService
{
    Task<ResourceBlockDto> CreateBlockAsync(CreateResourceBlockRequest request, Guid currentUserId, CancellationToken cancellationToken = default);
    Task<ResourceBlockDto?> GetBlockAsync(Guid blockId, CancellationToken cancellationToken = default);
    Task<List<ResourceBlockDto>> GetProviderBlocksAsync(Guid providerId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<List<ResourceBlockDto>> GetResourceBlocksAsync(Guid resourceId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<bool> DeleteBlockAsync(Guid blockId, Guid currentUserId, CancellationToken cancellationToken = default);
}
