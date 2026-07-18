namespace PlaylistMiner.Core.DTOs;

public record CreateOperationRequestDto(
    string Type,
    string? Source,
    string? Target,
    int? MaxItems,
    int? QuotaEstimate,
    DateTime? NotBefore,
    string? AllowedWindowStart,
    string? AllowedWindowEnd);

public record OperationRequestDto(
    int Id,
    string Type,
    string Status,
    string CreatedBy,
    string? Source,
    string? Target,
    int? MaxItems,
    int? QuotaEstimate,
    DateTime? NotBefore,
    string? AllowedWindowStart,
    string? AllowedWindowEnd,
    string? RunId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    string? Error);
