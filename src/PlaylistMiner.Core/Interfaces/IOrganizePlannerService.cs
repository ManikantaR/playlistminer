using PlaylistMiner.Core.DTOs;

namespace PlaylistMiner.Core.Interfaces;

public interface IOrganizePlannerService
{
    Task<OrganizePlanDto> BuildPlanAsync(CancellationToken ct = default);
}
