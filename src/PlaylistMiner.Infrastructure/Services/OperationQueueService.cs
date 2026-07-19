using Microsoft.EntityFrameworkCore;
using PlaylistMiner.Core.DTOs;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Core.Models;
using PlaylistMiner.Infrastructure.Data;

namespace PlaylistMiner.Infrastructure.Services;

public class OperationQueueService(PlaylistMinerDbContext db) : IOperationQueueService
{
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "full_sync",
        "inbox_sync",
        "process_now",
        "categorize",
        "organize_execute",
        "playlist_restore"
    };

    public async Task<OperationRequestDto> QueueAsync(
        CreateOperationRequestDto request,
        string createdBy = "user",
        CancellationToken ct = default)
    {
        Validate(request);

        var now = DateTime.UtcNow;
        var operation = new OperationRequest
        {
            Type = NormalizeType(request.Type),
            Status = request.NotBefore.HasValue && request.NotBefore.Value > now ? "scheduled" : "queued",
            CreatedBy = string.IsNullOrWhiteSpace(createdBy) ? "user" : createdBy,
            Source = string.IsNullOrWhiteSpace(request.Source) ? null : request.Source.Trim(),
            Target = string.IsNullOrWhiteSpace(request.Target) ? null : request.Target.Trim(),
            MaxItems = request.MaxItems,
            QuotaEstimate = request.QuotaEstimate,
            NotBefore = request.NotBefore,
            AllowedWindowStart = NormalizeWindow(request.AllowedWindowStart),
            AllowedWindowEnd = NormalizeWindow(request.AllowedWindowEnd),
            CreatedAt = now,
            UpdatedAt = now
        };

        db.OperationRequests.Add(operation);
        await db.SaveChangesAsync(ct);
        return Map(operation);
    }

    public async Task<IReadOnlyList<OperationRequestDto>> ListAsync(CancellationToken ct = default)
        => await db.OperationRequests
            .AsNoTracking()
            .OrderByDescending(o => o.CreatedAt)
            .Take(100)
            .Select(o => Map(o))
            .ToListAsync(ct);

    public async Task<OperationRequestDto?> GetAsync(int id, CancellationToken ct = default)
    {
        var operation = await db.OperationRequests.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id, ct);
        return operation is null ? null : Map(operation);
    }

    public async Task<OperationRequestDto?> CancelAsync(int id, CancellationToken ct = default)
    {
        var operation = await db.OperationRequests.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (operation is null) return null;

        if (operation.Status is not ("queued" or "scheduled" or "deferred"))
        {
            throw new InvalidOperationException("Only queued, scheduled, or deferred operations can be canceled.");
        }

        var now = DateTime.UtcNow;
        operation.Status = "canceled";
        operation.UpdatedAt = now;
        operation.CompletedAt = now;
        await db.SaveChangesAsync(ct);
        return Map(operation);
    }

    public async Task<OperationRequest?> GetNextRunnableAsync(DateTime now, CancellationToken ct = default)
    {
        var operation = await db.OperationRequests
            .Where(o => o.Status == "queued" || o.Status == "scheduled" || o.Status == "deferred")
            .Where(o => o.NotBefore == null || o.NotBefore <= now)
            .OrderBy(o => o.NotBefore ?? o.CreatedAt)
            .ThenBy(o => o.Id)
            .FirstOrDefaultAsync(ct);

        if (operation is null) return null;

        if (!IsInsideAllowedWindow(operation, now))
        {
            operation.Status = "deferred";
            operation.Error = "Operation is outside allowed execution window.";
            operation.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return null;
        }

        operation.Status = "running";
        operation.Error = null;
        operation.StartedAt ??= DateTime.UtcNow;
        operation.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return operation;
    }

    public async Task MarkCompletedAsync(int id, string? runId = null, CancellationToken ct = default)
    {
        var operation = await db.OperationRequests.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (operation is null) return;

        var now = DateTime.UtcNow;
        operation.Status = "completed";
        operation.RunId = runId ?? operation.RunId;
        operation.UpdatedAt = now;
        operation.CompletedAt = now;
        await db.SaveChangesAsync(ct);
    }

    public async Task MarkFailedAsync(int id, string error, CancellationToken ct = default)
    {
        var operation = await db.OperationRequests.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (operation is null) return;

        var now = DateTime.UtcNow;
        operation.Status = "failed";
        operation.Error = error;
        operation.UpdatedAt = now;
        operation.CompletedAt = now;
        await db.SaveChangesAsync(ct);
    }

    private static void Validate(CreateOperationRequestDto request)
    {
        if (!AllowedTypes.Contains(NormalizeType(request.Type)))
        {
            throw new ArgumentException("Unsupported operation type.", nameof(request));
        }

        if (request.MaxItems is <= 0)
        {
            throw new ArgumentException("Max items must be greater than zero.", nameof(request));
        }

        if (request.QuotaEstimate is < 0)
        {
            throw new ArgumentException("Quota estimate cannot be negative.", nameof(request));
        }

        if (NormalizeType(request.Type) == "playlist_restore")
        {
            if (!int.TryParse(request.Source, out var sourcePlaylistId) || sourcePlaylistId <= 0)
            {
                throw new ArgumentException("Playlist restore requires a positive source playlist id.", nameof(request));
            }

            if (!int.TryParse(request.Target, out var targetPlaylistId) || targetPlaylistId <= 0)
            {
                throw new ArgumentException("Playlist restore requires a positive target playlist id.", nameof(request));
            }

            if (request.MaxItems is > 500)
            {
                throw new ArgumentException("Playlist restore max items must be 500 or less.", nameof(request));
            }
        }

        _ = NormalizeWindow(request.AllowedWindowStart);
        _ = NormalizeWindow(request.AllowedWindowEnd);
    }

    private static bool IsInsideAllowedWindow(OperationRequest operation, DateTime now)
    {
        if (operation.AllowedWindowStart is null || operation.AllowedWindowEnd is null)
        {
            return true;
        }

        var start = TimeOnly.Parse(operation.AllowedWindowStart);
        var end = TimeOnly.Parse(operation.AllowedWindowEnd);
        var current = TimeOnly.FromDateTime(now);

        return start <= end
            ? current >= start && current <= end
            : current >= start || current <= end;
    }

    private static string NormalizeType(string type) => type.Trim().ToLowerInvariant();

    private static string? NormalizeWindow(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!TimeOnly.TryParse(value, out var parsed))
        {
            throw new ArgumentException("Allowed execution windows must use HH:mm format.");
        }

        return parsed.ToString("HH:mm");
    }

    private static OperationRequestDto Map(OperationRequest operation) => new(
        operation.Id,
        operation.Type,
        operation.Status,
        operation.CreatedBy,
        operation.Source,
        operation.Target,
        operation.MaxItems,
        operation.QuotaEstimate,
        operation.NotBefore,
        operation.AllowedWindowStart,
        operation.AllowedWindowEnd,
        operation.RunId,
        operation.CreatedAt,
        operation.UpdatedAt,
        operation.StartedAt,
        operation.CompletedAt,
        operation.Error);
}
