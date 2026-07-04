namespace PlaylistMiner.Core.DTOs;

public record OrganizePlanDto(
    int VideosExamined,
    int TotalActions,
    int TotalEstimatedQuotaCost,
    List<OrganizePlanItemDto> Items);
