using FirewallControl.Core.Entities;

namespace FirewallControl.Core.Interfaces;

public interface ICargaRapidaIaService
{
    bool IsProcessingIa { get; }
    string CurrentStage { get; }
    int UploadIndex { get; }
    int TotalFiles { get; }
    bool IsQueueActive { get; }
    int QueueIndex { get; }
    int QueueTotal { get; }
    IReadOnlyList<FastCargaQueueItem> QueueItems { get; }
    bool PendingLoadCurrent { get; set; }
    string PendingStatusMessage { get; set; }
    bool IsHidden { get; set; }
    FastCargaSnapshot? Snapshot { get; }

    event Action? StateChanged;
    event Action<int>? QueueReady;
    event Action<string, string>? ErrorOccurred;

    Task<List<string>> FetchAvailableModelsAsync(string apiKey);
    Task<bool> StartAnalyzeInvoicesAsync(IReadOnlyList<string> filePaths, string apiKey, string modelOrUrl);
    void PopulateQueue(IEnumerable<FastCargaQueueItem> items);
    FastCargaQueueItem? GetCurrentQueueItem();
    FastCargaQueueItem? AdvanceQueue(bool skipped);
    void CancelAndResetAll();
    void SaveSnapshot(FastCargaSnapshot snapshot);
    void ClearSnapshot();
    string GetStageBadge();
    string GetStageStatusMessage(string modelName);
}
