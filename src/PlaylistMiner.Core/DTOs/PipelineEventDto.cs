using System;

namespace PlaylistMiner.Core.DTOs;

public record PipelineEventDto(
    int Id,
    string RunId,
    DateTime OccurredAt,
    string Level,
    string Phase,
    string Message,
    string? PayloadJson
);
