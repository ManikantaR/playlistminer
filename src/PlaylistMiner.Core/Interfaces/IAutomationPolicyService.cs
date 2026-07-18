using PlaylistMiner.Core.DTOs;

namespace PlaylistMiner.Core.Interfaces;

public interface IAutomationPolicyService
{
    Task<AutomationPolicyDto> GetPolicyAsync(CancellationToken ct = default);
    Task<AutomationPolicyDto> UpdatePolicyAsync(UpdateAutomationPolicyRequest request, CancellationToken ct = default);
}
