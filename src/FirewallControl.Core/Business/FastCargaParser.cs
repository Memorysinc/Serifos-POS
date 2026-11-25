using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using FirewallControl.Core.Entities;

namespace FirewallControl.Core.Business;

/// <summary>
/// Logica pura compartida para Carga Rapida de Productos + Gemini Files API
/// y calculo de cuotas, replicando PROGS\carga_rapida_productos.prg y abm_cuotaventa.scx.
/// </summary>
public static class FastCargaParser
{
    public const string DefaultApiModel = "gemini-3.6-flash";
    public const long MaxPayloadBytes = 52_428_800L; // 50 MB

    public static readonly IReadOnlyList<string> PredefinedModels = new[]
    {
        "gemini-3.6-flash",
        "gemini-3.0-flash",
        "gemini-2.5-flash",
        "gemini-2.5-pro",
        "gemini-2.5-flash-lite",
        "gemini-2.0-flash",
        "gemini-2.0-flash-lite",
        "gemini-1.5-flash",
        "gemini-1.5-pro"
    };

    private static readonly Regex RegexLetters = new("[A-Za-z]", RegexOptions.Compiled);
    private static readonly Regex RegexLeadingDash = new(@"^\s*-\s*", RegexOptions.Compiled);
    private static readonly Regex RegexPet = new(@"\bpet\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex Regex1500 = new(@"\b1500\b", RegexOptions.Compiled);
    private static readonly Regex Regex2000 = new(@"\b2000\b", RegexOptions.Compiled);
    private static readonly Regex Regex3000 = new(@"\b3000\b", RegexOptions.Compiled);
    private static readonly Regex Regex500 = new(@"\b500\b", RegexOptions.Compiled);
    private static readonly Regex RegexPackSuffix = new(@"\s*x\s*\d+\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex RegexMultiSpace = new(@"\s+", RegexOptions.Compiled);

    public static string BuildInvoiceExtractionPrompt() =>
        "Analiza la factura de las imagenes. Extrae los productos. REGLAS ESTRICTAS: " +
        "1. Descarta cualquier producto cuyo codigo de barras contenga letras. " +
        "2. EL NOMBRE DEBE SER LIMPIADO: Borra guiones al inicio. Borra la palabra 'Pet' o 'PET'. " +
        "Borra la cantidad del paquete al final (ej 'x 4'). Convierte los volumenes: '1500' a '1.5L', '2000' a '2L', '3000' a '3L', y '500' a '500ml'. " +
        "3. EL COSTO UNITARIO es el PRECIO BRUTO total dividido por la cantidad del paquete. Ejemplo: Si el precio es 51.900 y es 'x 6', el costo es 8650. " +
        "4. El Costo Unitario debe ser numero entero SIN PUNTOS (ej. 8650). " +
        "5. Responde UNICAMENTE en formato de tabla separada por tabulaciones: CodigoBarra\tNombre\tCostoUnitario. Nada de markdown, saludos ni cabeceras.";

    public static bool ContainsLetters(string? text)
        => !string.IsNullOrEmpty(text) && RegexLetters.IsMatch(text);

    public static string CleanNombreIa(string? rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
            return string.Empty;

        string s = rawName.Trim();
        s = RegexLeadingDash.Replace(s, "");
        s = RegexPet.Replace(s, "");
        s = Regex1500.Replace(s, "1.5L");
        s = Regex2000.Replace(s, "2L");
        s = Regex3000.Replace(s, "3L");
        s = Regex500.Replace(s, "500ml");
        s = RegexPackSuffix.Replace(s, "");
        s = RegexMultiSpace.Replace(s, " ").Trim();
        return s;
    }

    public static decimal CleanCostoIa(string? rawCost)
    {
        if (string.IsNullOrWhiteSpace(rawCost))
            return 0m;

        string s = rawCost.Trim();
        if (s.EndsWith(".00", StringComparison.Ordinal) || s.EndsWith(",00", StringComparison.Ordinal))
        {
            s = s[..^3];
        }

        s = s.Replace(".", "").Replace(",", "").Replace(" ", "");
        if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal val))
            return Math.Round(val, 0, MidpointRounding.AwayFromZero);

        return 0m;
    }

    public static List<FastCargaQueueItem> ParseTsvToQueue(string? rawTsv)
    {
        var result = new List<FastCargaQueueItem>();
        if (string.IsNullOrWhiteSpace(rawTsv))
            return result;

        string normalized = rawTsv.Replace("\r", "");
        string[] lines = normalized.Split('\n');
        int nextId = 1;

        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith("```", StringComparison.Ordinal))
                continue;

            string[] fields = line.Split('\t');
            if (fields.Length < 3)
                continue;

            string cod = fields[0].Trim();
            string nombreRaw = fields[1].Trim();
            string costoRaw = fields[2].Trim();

            if (string.IsNullOrEmpty(cod) || ContainsLetters(cod))
                continue;

            if (cod.Length > 25) cod = cod[..25];

            string nombreClean = CleanNombreIa(nombreRaw);
            if (string.IsNullOrEmpty(nombreClean))
                nombreClean = $"PRODUCTO {cod}";
            if (nombreClean.Length > 250) nombreClean = nombreClean[..250];

            decimal costo = CleanCostoIa(costoRaw);

            result.Add(new FastCargaQueueItem
            {
                ItemId = nextId++,
                Codigo = cod,
                Nombre = nombreClean,
                Costo = costo,
                Estado = "PENDIENTE"
            });
        }

        return result;
    }

    public static string ExtractGeminiText(string? jsonResponse)
    {
        if (string.IsNullOrWhiteSpace(jsonResponse))
            return string.Empty;

        try
        {
            using var doc = JsonDocument.Parse(jsonResponse);
            if (!doc.RootElement.TryGetProperty("candidates", out var candidates) ||
                candidates.ValueKind != JsonValueKind.Array ||
                candidates.GetArrayLength() == 0)
            {
                return string.Empty;
            }

            string fallback = string.Empty;
            string? lastNonThought = null;

            foreach (var cand in candidates.EnumerateArray())
            {
                if (!cand.TryGetProperty("content", out var content) ||
                    !content.TryGetProperty("parts", out var parts) ||
                    parts.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var part in parts.EnumerateArray())
                {
                    if (!part.TryGetProperty("text", out var textElem) ||
                        textElem.ValueKind != JsonValueKind.String)
                    {
                        continue;
                    }

                    string? textVal = textElem.GetString();
                    if (string.IsNullOrWhiteSpace(textVal))
                        continue;

                    fallback = textVal;
                    bool isThought = part.TryGetProperty("thought", out var thoughtElem) &&
                                     thoughtElem.ValueKind == JsonValueKind.True;
                    if (!isThought)
                    {
                        lastNonThought = textVal;
                    }
                }
            }

            return lastNonThought ?? fallback;
        }
        catch
        {
            return string.Empty;
        }
    }

    public static List<string> ParseModelsJson(string? jsonResponse)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(jsonResponse))
            return result;

        try
        {
            using var doc = JsonDocument.Parse(jsonResponse);
            if (!doc.RootElement.TryGetProperty("models", out var models) ||
                models.ValueKind != JsonValueKind.Array)
            {
                return result;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var modelElem in models.EnumerateArray())
            {
                if (!modelElem.TryGetProperty("name", out var nameElem) ||
                    nameElem.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                string fullName = nameElem.GetString()?.Trim() ?? string.Empty;
                const string prefix = "models/";
                if (!fullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                string modelName = fullName[prefix.Length..].Trim();
                if (!modelName.StartsWith("gemini-", StringComparison.OrdinalIgnoreCase))
                    continue;

                string lower = modelName.ToLowerInvariant();
                if (lower.Contains("embedding") || lower.Contains("imagen") ||
                    lower.Contains("veo") || lower.Contains("-tts") ||
                    lower.Contains("aqa") || lower.Contains("robotics") ||
                    lower.Contains("computer-use") || lower.Contains("deep-research"))
                {
                    continue;
                }

                bool supportsGenerate = false;
                if (modelElem.TryGetProperty("supportedGenerationMethods", out var methodsElem) &&
                    methodsElem.ValueKind == JsonValueKind.Array)
                {
                    foreach (var m in methodsElem.EnumerateArray())
                    {
                        if (string.Equals(m.GetString(), "generateContent", StringComparison.OrdinalIgnoreCase))
                        {
                            supportsGenerate = true;
                            break;
                        }
                    }
                }

                if (!supportsGenerate)
                    continue;

                if (seen.Add(modelName))
                {
                    result.Add(modelName);
                }
            }
        }
        catch
        {
            return result;
        }

        return result;
    }

    public static List<SaleInstallmentEntity> CalculateInstallments(
        decimal totalVenta,
        decimal entrega,
        int numCuotas,
        int diasIntervalo,
        DateTime fechaBase,
        decimal interesPorcentaje = 0m)
    {
        var list = new List<SaleInstallmentEntity>();
        int cuotas = Math.Clamp(numCuotas, 1, 60);
        int dias = Math.Clamp(diasIntervalo, 1, 365);

        decimal entregaAplicada = Math.Clamp(entrega, 0m, totalVenta);
        decimal capital = Math.Max(0m, totalVenta - entregaAplicada);
        decimal saldoFinanciado = interesPorcentaje > 0m
            ? Math.Round(capital * (1m + interesPorcentaje / 100m), 0, MidpointRounding.AwayFromZero)
            : Math.Round(capital, 0, MidpointRounding.AwayFromZero);

        if (saldoFinanciado <= 0m)
            return list;

        decimal baseCuota = Math.Floor(saldoFinanciado / cuotas);
        decimal acumulado = 0m;

        for (int i = 1; i <= cuotas; i++)
        {
            decimal montoCuota = i < cuotas ? baseCuota : (saldoFinanciado - acumulado);
            acumulado += montoCuota;

            list.Add(new SaleInstallmentEntity
            {
                NumeroCuota = i,
                CuotaTexto = $"{i}/{cuotas}",
                Vencimiento = fechaBase.Date.AddDays(dias * i),
                Dias = dias,
                Monto = montoCuota,
                Entrega = 0m,
                Saldo = montoCuota
            });
        }

        return list;
    }
}
