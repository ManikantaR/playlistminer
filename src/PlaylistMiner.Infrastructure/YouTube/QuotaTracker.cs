using Microsoft.EntityFrameworkCore;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Core.Models;
using PlaylistMiner.Infrastructure.Data;

namespace PlaylistMiner.Infrastructure.YouTube;

public sealed class QuotaTracker(PlaylistMinerDbContext db) : IQuotaTracker
{
    private const string QuotaExhaustedKey = "youtube.quota_exhausted_at";
    private static readonly TimeZoneInfo PacificTz = TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles");

    public async Task<bool> IsQuotaExhaustedAsync(CancellationToken ct = default)
    {
        var setting = await db.Settings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == QuotaExhaustedKey, ct);

        if (setting is null)
            return false;

        if (!DateTime.TryParse(setting.Value, out var exhaustedAtUtc))
            return false;

        return !HasResetSince(exhaustedAtUtc);
    }

    public async Task RecordQuotaExhaustedAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var existing = await db.Settings.FirstOrDefaultAsync(s => s.Key == QuotaExhaustedKey, ct);

        if (existing is not null)
        {
            existing.Value = now.ToString("O");
            existing.UpdatedAt = now;
        }
        else
        {
            db.Settings.Add(new Setting
            {
                Key = QuotaExhaustedKey,
                Value = now.ToString("O"),
                UpdatedAt = now
            });
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<QuotaStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var setting = await db.Settings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == QuotaExhaustedKey, ct);

        var resetsAt = GetNextResetTimeUtc();

        if (setting is null || !DateTime.TryParse(setting.Value, out var exhaustedAtUtc))
            return new QuotaStatus(false, null, resetsAt, "Quota available.");

        if (HasResetSince(exhaustedAtUtc))
            return new QuotaStatus(false, null, resetsAt, "Quota available.");

        return new QuotaStatus(
            true,
            exhaustedAtUtc,
            resetsAt,
            $"YouTube API quota exhausted. Resets at {resetsAt:g} UTC (midnight Pacific).");
    }

    private static bool HasResetSince(DateTime exhaustedAtUtc)
    {
        var exhaustedPacific = TimeZoneInfo.ConvertTimeFromUtc(exhaustedAtUtc, PacificTz);
        var nowPacific = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, PacificTz);
        return nowPacific.Date > exhaustedPacific.Date;
    }

    private static DateTime GetNextResetTimeUtc()
    {
        var nowPacific = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, PacificTz);
        var tomorrowMidnightPacific = nowPacific.Date.AddDays(1);
        return TimeZoneInfo.ConvertTimeToUtc(tomorrowMidnightPacific, PacificTz);
    }
}
