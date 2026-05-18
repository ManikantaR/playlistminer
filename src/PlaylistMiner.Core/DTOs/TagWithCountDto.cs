namespace PlaylistMiner.Core.DTOs;

public record TagWithCountDto(int Id, string Name, string Slug, string? Category, int VideoCount);
