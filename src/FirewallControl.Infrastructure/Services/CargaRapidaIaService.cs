using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FirewallControl.Core.Business;
using FirewallControl.Core.Entities;
using FirewallControl.Core.Interfaces;

namespace FirewallControl.Infrastructure.Services;

/// <summary>
/// Motor singleton en segundo plano para Carga Rapida de Productos + Gemini Files API (hasta 50 MB).
/// Sobrevive al ocultar la ventana con [F2] y permite seguir facturando mientras sube/analiza facturas.
/// </summary>
public class CargaRapidaIaService : ICargaRapidaIaService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly List<FastCargaQueueItem> _queueItems = new();
    private readonly object _syncLock = new();
    private CancellationTokenSource? _pipelineCts;
    private string _activeModelName = FastCargaParser.DefaultApiModel;

    public bool IsProcessingIa { get; private set; }
    public string CurrentStage { get; private set; } = "IDLE";
    public int UploadIndex { get; private set; }
    public int TotalFiles { get; private set; }
    public bool IsQueueActive { get; private set; }
    public int QueueIndex { get; private set; }
    public int QueueTotal { get; private set; }
    public IReadOnlyList<FastCargaQueueItem> QueueItems => _queueItems;
    public bool PendingLoadCurrent { get; set; }
    public string PendingStatusMessage { get; set; } = string.Empty;
    public bool IsHidden { get; set; }
    public FastCargaSnapshot? Snapshot { get; private set; }

    public event Action? StateChanged;
    public event Action<int>? QueueReady;
    public event Action<string, string>? ErrorOccurred;

    public CargaRapidaIaService()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(5)
        };
    }

    public async Task<List<string>> FetchAvailableModelsAsync(string apiKey)
    {
        string cleanKey = CleanToken(apiKey);
        if (string.IsNullOrWhiteSpace(cleanKey))
            return new List<string>();

        string url = $"https://generativelanguage.googleapis.com/v1beta/models?pageSize=100&key={Uri.EscapeDataString(cleanKey)}";
        using var response = await _httpClient.GetAsync(url);
        string body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"HTTP {(int)response.StatusCode}: {Truncate(body, 500)}");
        }

        return FastCargaParser.ParseModelsJson(body);
    }

    public Task<bool> StartAnalyzeInvoicesAsync(IReadOnlyList<string> filePaths, string apiKey, string modelOrUrl)
    {
        if (IsProcessingIa)
            return Task.FromResult(false);

        string cleanKey = CleanToken(apiKey);
        string cleanModel = CleanToken(modelOrUrl);
        if (string.IsNullOrWhiteSpace(cleanModel))
            cleanModel = FastCargaParser.DefaultApiModel;

        if (string.IsNullOrWhiteSpace(cleanKey) &&
            !cleanModel.Contains("key=", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Ingresa una API Key válida antes de analizar facturas.");
        }

        var validFiles = new List<(string Path, string FileName, string MimeType, long Size)>();
        long totalBytes = 0;

        foreach (string rawPath in filePaths)
        {
            if (string.IsNullOrWhiteSpace(rawPath) || !File.Exists(rawPath))
                continue;

            var fi = new FileInfo(rawPath);
            if (fi.Length <= 0)
                continue;

            validFiles.Add((fi.FullName, fi.Name, GetMimeType(fi.FullName), fi.Length));
            totalBytes += fi.Length;
        }

        if (validFiles.Count == 0)
            throw new InvalidOperationException("No se encontraron archivos válidos para subir a Files API.");

        if (totalBytes > FastCargaParser.MaxPayloadBytes)
        {
            double mb = Math.Round(totalBytes / 1048576.0, 1);
            throw new InvalidOperationException(
                $"Las imágenes seleccionadas suman {mb:0.0} MB y superan el límite de 50 MB de Files API.");
        }

        _pipelineCts?.Cancel();
        _pipelineCts?.Dispose();
        _pipelineCts = new CancellationTokenSource();
        var ct = _pipelineCts.Token;

        _activeModelName = cleanModel;
        TotalFiles = validFiles.Count;
        UploadIndex = 1;
        IsProcessingIa = true;
        CurrentStage = "UPLOAD_START";
        PendingLoadCurrent = false;
        PendingStatusMessage = string.Empty;
        NotifyStateChanged();

        _ = RunPipelineAsync(validFiles, cleanKey, cleanModel, ct);
        return Task.FromResult(true);
    }

    private async Task RunPipelineAsync(
        List<(string Path, string FileName, string MimeType, long Size)> files,
        string apiKey,
        string modelOrUrl,
        CancellationToken ct)
    {
        var uploadedUris = new List<(string FileUri, string MimeType)>();

        try
        {
            for (int i = 0; i < files.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var f = files[i];
                UploadIndex = i + 1;

                // 1. UPLOAD_START (Resumable session)
                CurrentStage = "UPLOAD_START";
                NotifyStateChanged();

                string startUrl = $"https://generativelanguage.googleapis.com/upload/v1beta/files?key={Uri.EscapeDataString(apiKey)}";
                using var startReq = new HttpRequestMessage(HttpMethod.Post, startUrl);
                startReq.Headers.TryAddWithoutValidation("X-Goog-Upload-Protocol", "resumable");
                startReq.Headers.TryAddWithoutValidation("X-Goog-Upload-Command", "start");
                startReq.Headers.TryAddWithoutValidation("X-Goog-Upload-Header-Content-Length", f.Size.ToString());
                startReq.Headers.TryAddWithoutValidation("X-Goog-Upload-Header-Content-Type", f.MimeType);
                string startPayload = JsonSerializer.Serialize(new { file = new { display_name = f.FileName } });
                startReq.Content = new StringContent(startPayload, Encoding.UTF8, "application/json");

                using var startResp = await _httpClient.SendAsync(startReq, ct);
                string startRespText = await startResp.Content.ReadAsStringAsync(ct);
                if (!startResp.IsSuccessStatusCode)
                {
                    ReportError($"Error al iniciar subida Files API (HTTP {(int)startResp.StatusCode}).", startRespText);
                    return;
                }

                string? uploadUrl = null;
                if (startResp.Headers.TryGetValues("X-Goog-Upload-URL", out var hdrVals))
                {
                    uploadUrl = hdrVals.FirstOrDefault()?.Trim();
                }

                if (string.IsNullOrWhiteSpace(uploadUrl))
                {
                    ReportError("Files API no devolvió encabezado X-Goog-Upload-URL.", startRespText);
                    return;
                }

                // 2. UPLOAD_BYTES (Stream binary payload without Base64 bloat)
                CurrentStage = "UPLOAD_BYTES";
                NotifyStateChanged();

                await using var fileStream = new FileStream(f.Path, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var uploadReq = new HttpRequestMessage(HttpMethod.Post, uploadUrl);
                using var streamContent = new StreamContent(fileStream);
                streamContent.Headers.ContentLength = f.Size;
                streamContent.Headers.ContentType = new MediaTypeHeaderValue(f.MimeType);
                uploadReq.Headers.TryAddWithoutValidation("X-Goog-Upload-Offset", "0");
                uploadReq.Headers.TryAddWithoutValidation("X-Goog-Upload-Command", "upload, finalize");
                uploadReq.Content = streamContent;

                using var uploadResp = await _httpClient.SendAsync(uploadReq, ct);
                string uploadRespText = await uploadResp.Content.ReadAsStringAsync(ct);
                if (!uploadResp.IsSuccessStatusCode)
                {
                    ReportError($"Error al subir archivo {i + 1} a Files API (HTTP {(int)uploadResp.StatusCode}).", uploadRespText);
                    return;
                }

                string fileUri = ExtractFileUriFromJson(uploadRespText);
                if (string.IsNullOrWhiteSpace(fileUri))
                {
                    ReportError("Files API no devolvió la URI del archivo subido.", uploadRespText);
                    return;
                }

                uploadedUris.Add((fileUri, f.MimeType));
            }

            // 3. GENERATE CONTENT WITH FILE URIS
            ct.ThrowIfCancellationRequested();
            CurrentStage = "GENERATE";
            NotifyStateChanged();

            string generateUrl = BuildGeminiUrl(apiKey, modelOrUrl);
            var partsList = new List<object>
            {
                new { text = FastCargaParser.BuildInvoiceExtractionPrompt() }
            };
            foreach (var up in uploadedUris)
            {
                partsList.Add(new
                {
                    file_data = new
                    {
                        mime_type = up.MimeType,
                        file_uri = up.FileUri
                    }
                });
            }

            string genPayload = JsonSerializer.Serialize(new
            {
                contents = new[] { new { parts = partsList } }
            });

            using var genReq = new HttpRequestMessage(HttpMethod.Post, generateUrl);
            genReq.Content = new StringContent(genPayload, Encoding.UTF8, "application/json");
            using var genResp = await _httpClient.SendAsync(genReq, ct);
            string genRespText = await genResp.Content.ReadAsStringAsync(ct);

            IsProcessingIa = false;
            CurrentStage = "IDLE";

            if (!genResp.IsSuccessStatusCode)
            {
                ReportError($"Error Gemini HTTP {(int)genResp.StatusCode}. Revisa API Key / Modelo.", genRespText);
                return;
            }

            string rawText = FastCargaParser.ExtractGeminiText(genRespText);
            if (string.IsNullOrWhiteSpace(rawText))
            {
                ReportError("La API respondió sin formato tabular esperado.", genRespText);
                return;
            }

            var parsedItems = FastCargaParser.ParseTsvToQueue(rawText);
            PopulateQueue(parsedItems);

            if (parsedItems.Count == 0)
            {
                ReportError("Gemini descartó todos los productos o no devolvió filas válidas.", rawText);
                return;
            }

            if (IsHidden)
            {
                PendingLoadCurrent = true;
                ClearSnapshot();
                PendingStatusMessage = $"Factura analizada en segundo plano ({parsedItems.Count} producto/s).";
            }

            NotifyStateChanged();
            QueueReady?.Invoke(parsedItems.Count);
        }
        catch (OperationCanceledException)
        {
            IsProcessingIa = false;
            CurrentStage = "IDLE";
            NotifyStateChanged();
        }
        catch (Exception ex)
        {
            IsProcessingIa = false;
            CurrentStage = "IDLE";
            ReportError($"Error en consulta Files API / Gemini: {Truncate(ex.Message, 140)}", ex.ToString());
        }
    }

    public void PopulateQueue(IEnumerable<FastCargaQueueItem> items)
    {
        lock (_syncLock)
        {
            _queueItems.Clear();
            _queueItems.AddRange(items);
            QueueTotal = _queueItems.Count;
            IsQueueActive = QueueTotal > 0;
            QueueIndex = IsQueueActive ? 1 : 0;
        }
        NotifyStateChanged();
    }

    public FastCargaQueueItem? GetCurrentQueueItem()
    {
        lock (_syncLock)
        {
            if (!IsQueueActive || QueueIndex < 1 || QueueIndex > _queueItems.Count)
                return null;
            return _queueItems[QueueIndex - 1];
        }
    }

    public FastCargaQueueItem? AdvanceQueue(bool skipped)
    {
        lock (_syncLock)
        {
            if (!IsQueueActive || _queueItems.Count == 0)
                return null;

            if (QueueIndex >= 1 && QueueIndex <= _queueItems.Count)
            {
                var current = _queueItems[QueueIndex - 1];
                if (string.Equals(current.Estado, "PENDIENTE", StringComparison.OrdinalIgnoreCase))
                {
                    current.Estado = skipped ? "SALTADO" : "GUARDADO";
                }
            }

            QueueIndex++;
            if (QueueIndex > _queueItems.Count)
            {
                IsQueueActive = false;
                QueueIndex = _queueItems.Count;
                NotifyStateChanged();
                return null;
            }

            var next = _queueItems[QueueIndex - 1];
            NotifyStateChanged();
            return next;
        }
    }

    public void CancelAndResetAll()
    {
        try
        {
            _pipelineCts?.Cancel();
        }
        catch
        {
            // Ignore cancellation race
        }

        lock (_syncLock)
        {
            IsProcessingIa = false;
            CurrentStage = "IDLE";
            UploadIndex = 0;
            TotalFiles = 0;
            _queueItems.Clear();
            IsQueueActive = false;
            QueueIndex = 0;
            QueueTotal = 0;
            PendingLoadCurrent = false;
            PendingStatusMessage = string.Empty;
            IsHidden = false;
            Snapshot = null;
        }

        NotifyStateChanged();
    }

    public void SaveSnapshot(FastCargaSnapshot snapshot)
    {
        Snapshot = snapshot;
        IsHidden = true;
        NotifyStateChanged();
    }

    public void ClearSnapshot()
    {
        Snapshot = null;
    }

    public string GetStageBadge() => CurrentStage switch
    {
        "UPLOAD_START" or "UPLOAD_BYTES" =>
            $"COLA IA · Subiendo foto {Math.Max(1, UploadIndex)}/{Math.Max(1, TotalFiles)} (Files API)",
        "GENERATE" =>
            $"COLA IA · Analizando {Math.Max(1, TotalFiles)} archivo/s en Gemini...",
        _ when IsQueueActive && QueueTotal > 0 && QueueIndex >= 1 && QueueIndex <= QueueTotal =>
            $"COLA IA [{QueueIndex}/{QueueTotal}] · Ingresa Venta y [Enter/F8]",
        _ => "COLA IA · Sin productos en espera"
    };

    public string GetStageStatusMessage(string modelName) => CurrentStage switch
    {
        "UPLOAD_START" or "UPLOAD_BYTES" =>
            $"Subiendo archivo {Math.Max(1, UploadIndex)}/{Math.Max(1, TotalFiles)} a Gemini Files API...",
        "GENERATE" =>
            $"Archivos subidos. Esperando análisis de Gemini ({(string.IsNullOrWhiteSpace(modelName) ? _activeModelName : modelName)})...",
        _ => "Listo."
    };

    private void ReportError(string message, string detail)
    {
        IsProcessingIa = false;
        CurrentStage = "IDLE";
        PendingStatusMessage = message;
        NotifyStateChanged();
        ErrorOccurred?.Invoke(message, detail);
    }

    private void NotifyStateChanged()
    {
        StateChanged?.Invoke();
    }

    private static string BuildGeminiUrl(string apiKey, string modelOrUrl)
    {
        if (modelOrUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            modelOrUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(apiKey) &&
                !modelOrUrl.Contains("key=", StringComparison.OrdinalIgnoreCase))
            {
                char sep = modelOrUrl.Contains('?') ? '&' : '?';
                return $"{modelOrUrl}{sep}key={Uri.EscapeDataString(apiKey)}";
            }
            return modelOrUrl;
        }

        return $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(modelOrUrl)}:generateContent?key={Uri.EscapeDataString(apiKey)}";
    }

    private static string ExtractFileUriFromJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("file", out var fileObj) &&
                fileObj.TryGetProperty("uri", out var uriProp))
            {
                return uriProp.GetString()?.Trim() ?? string.Empty;
            }
            if (doc.RootElement.TryGetProperty("uri", out var directUri))
            {
                return directUri.GetString()?.Trim() ?? string.Empty;
            }
        }
        catch
        {
            // Ignore malformed JSON
        }
        return string.Empty;
    }

    private static string GetMimeType(string filePath)
    {
        return Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            ".pdf" => "application/pdf",
            _ => "image/jpeg"
        };
    }

    private static string CleanToken(string? input)
        => string.IsNullOrEmpty(input)
            ? string.Empty
            : input.Replace("\r", "").Replace("\n", "").Replace("\t", "").Trim();

    private static string Truncate(string? text, int maxLen)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return text.Length <= maxLen ? text : text[..maxLen];
    }

    public void Dispose()
    {
        _pipelineCts?.Cancel();
        _pipelineCts?.Dispose();
        _httpClient.Dispose();
    }
}
