using System;

namespace PlaylistMiner.Core.DTOs;

public record OperationsQuotaDto(
    int MovesUsedToday,
    int MoveBudget,
    DateTime ResetsAt,
    int UnitsRemaining,
    bool IsBlocked,
    string Message
);
