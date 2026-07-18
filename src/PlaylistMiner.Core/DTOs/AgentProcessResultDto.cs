namespace PlaylistMiner.Core.DTOs;

public record AgentProcessResultDto(
    string Status,
    string Message,
    SyncResult? Sync,
    OrganizeExecutionResultDto? Execution);
