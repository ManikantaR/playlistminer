namespace PlaylistMiner.Core.DTOs;

public record OperationsActivityFeedDto(
    List<OperationsActivityItemDto> Items,
    int Limit,
    int Offset,
    int TotalCount,
    bool HasMore
);
