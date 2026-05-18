using PlaylistMiner.Core.DTOs;
using PlaylistMiner.Core.Models;

namespace PlaylistMiner.Core.Interfaces;

public interface IImportService
{
    Task<ImportResult> ImportTakeoutAsync(Stream csvStream, CancellationToken ct = default);
    Task<List<ImportBatch>> GetHistoryAsync(CancellationToken ct = default);
}
