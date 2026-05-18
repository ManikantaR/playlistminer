namespace PlaylistMiner.Api.Models;

public record PatchTagsRequest(int[] TagIdsToAdd, int[] TagIdsToRemove);
