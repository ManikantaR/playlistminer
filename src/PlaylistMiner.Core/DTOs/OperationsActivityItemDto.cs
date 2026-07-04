using System;

namespace PlaylistMiner.Core.DTOs;

public record OperationsActivityItemDto(
    int Id,
    string RunId,
    string PipelineType,
    string PipelineLabel,
    string Status,
    string Level,
    string Phase,
    string Message,
    DateTime OccurredAt
);
