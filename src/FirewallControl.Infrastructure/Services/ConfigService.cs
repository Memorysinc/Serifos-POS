using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using FirewallControl.Core.Business;
using FirewallControl.Core.Entities;
using FirewallControl.Core.Interfaces;

namespace FirewallControl.Infrastructure.Services;

/// <summary>
/// Replicates configuracionfirewalini.prg, inicio.prg, and SeleServidor.scx.
/// Reads FireWall.Ini and decrypts Firewall.sql using VfpCompat.Encriptar("OFF", line, "###$$$$").
/// Also manages persistent user preferences and customizable keyboard shortcuts in preferencias_atajos.json.
/// </summary>
public class ConfigService : IConfigService
{
    private const string PreferencesFileName = "preferencias_atajos.json";

    public FirewallConfig Config { get; private set; } = new();
    public AppPreferencesEntity Preferences { get; private set; } = new();

    public ConfigService()
    {
        LoadConfiguration();
    }

    public ConfigService(string baseDirectory)
    {
        Config.BaseDirectory = baseDirectory;
        LoadConfiguration();
    }

    public void LoadConfiguration()
    {
        string baseDir = string.IsNullOrWhiteSpace(Config.BaseDirectory)
            ? AppDomain.CurrentDomain.BaseDirectory
            : Config.BaseDirectory;

        string iniPath = Path.Combine(baseDir, "FireWall.Ini");
        // Prefer the workspace/deploy root that contains FireWall.Ini + genes_productos.dbf + Styles
        string? current = baseDir;
        for (int i = 0; i < 10 && current != null; i++)
        {
            foreach (string probe in new[] { current, Path.Combine(current, "ParaOtraPC_FirewallControl_NET10") })
            {
                string candidateIni = Path.Combine(probe, "FireWall.Ini");
                string candidateGenes = Path.Combine(probe, "genes_productos.dbf");
                if (File.Exists(candidateIni) && File.Exists(candidateGenes))
                {
                    iniPath = candidateIni;
                    baseDir = probe;
                    i = 10;
                    break;
                }
            }
            current = Directory.GetParent(current)?.FullName;
        }

        if (!File.Exists(iniPath))
        {
            current = baseDir;
            for (int i = 0; i < 10 && current != null; i++)
            {
                foreach (string probe in new[] { current, Path.Combine(current, "ParaOtraPC_FirewallControl_NET10") })
                {
                    string candidate = Path.Combine(probe, "FireWall.Ini");
                    if (File.Exists(candidate))
                    {
                        iniPath = candidate;
                        baseDir = probe;
                        i = 10;
                        break;
                    }
                }
                current = Directory.GetParent(current)?.FullName;
            }
        }

        Config.BaseDirectory = baseDir;

        if (File.Exists(iniPath))
        {
            var ini = ParseIniFile(iniPath);
            if (ini.TryGetValue("Preferencias.NameMenu", out string? nm) || ini.TryGetValue("Config.lcNameMenu", out nm)) Config.NameMenu = nm!;
            if (ini.TryGetValue("Preferencias.Skin", out string? sk) || ini.TryGetValue("Config.lcSkin", out sk)) Config.Skin = sk!;
            if (ini.TryGetValue("Preferencias.Agente", out string? ag) || ini.TryGetValue("Config.lcAgente", out ag)) Config.Agente = ag!;
            if ((ini.TryGetValue("Preferencias.GuardaSesion", out string? gs) || ini.TryGetValue("Config.lnGuardaSesion", out gs)) && int.TryParse(gs, out int gsi)) Config.GuardaSesion = gsi;
            if ((ini.TryGetValue("Preferencias.RutaServer", out string? rs) || ini.TryGetValue("Config.lcRutaServer", out rs)) && !string.IsNullOrWhiteSpace(rs)) Config.RutaServer = rs!;
            if ((ini.TryGetValue("Preferencias.Config", out string? cf) || ini.TryGetValue("Config.lcConfigFile", out cf)) && !string.IsNullOrWhiteSpace(cf)) Config.ConfigFile = cf!;
            if ((ini.TryGetValue("Preferencias.BaseDatos", out string? bd) || ini.TryGetValue("Config.lcBaseDatos", out bd)) && !string.IsNullOrWhiteSpace(bd)) Config.BaseDatos = bd!;
            if ((ini.TryGetValue("Preferencias.Server", out string? st) || ini.TryGetValue("Config.lnServer", out st)) && int.TryParse(st, out int sti)) Config.ServerType = sti;
            if ((ini.TryGetValue("Preferencias.Port", out string? pt) || ini.TryGetValue("Config.lnPort", out pt)) && int.TryParse(pt, out int pti)) Config.Port = pti;

            if (ini.TryGetValue("Local.Timbrado", out string? tim) || ini.TryGetValue("Config.lcTimbrado", out tim)) Config.Timbrado = tim!;
            if ((ini.TryGetValue("Local.DescuentoMaximo", out string? dm) || ini.TryGetValue("Config.lnDescuento", out dm)) && decimal.TryParse(dm, out decimal dmd)) Config.DescuentoMaximo = dmd;
            if ((ini.TryGetValue("Local.AnulacionVenta", out string? av) || ini.TryGetValue("Config.lnAnulacionVenta", out av)) && int.TryParse(av, out int avi)) Config.AnulacionVenta = avi;
            if (ini.TryGetValue("Local.MonedaFactura", out string? mf) || ini.TryGetValue("Config.lcMonedaFactura", out mf)) Config.MonedaFactura = mf!;
            if ((ini.TryGetValue("Local.IdMonedaFactura", out string? imf) || ini.TryGetValue("Config.lcIDMonedaFactura", out imf)) && int.TryParse(imf, out int imfi)) Config.IdMonedaFactura = imfi;
            if (ini.TryGetValue("Local.ListaPrecios", out string? lp) && int.TryParse(lp, out int lpi)) Config.ListaPrecios = lpi;
            if (ini.TryGetValue("Local.IdSucursal", out string? ids) && int.TryParse(ids, out int idsi)) Config.IdSucursal = idsi;
            if (ini.TryGetValue("Local.IdCaja", out string? idc) && int.TryParse(idc, out int idci)) Config.IdCaja = idci;
            if (ini.TryGetValue("Local.IdDeposito", out string? idd) && int.TryParse(idd, out int iddi)) Config.IdDeposito = iddi;
            if (ini.TryGetValue("Local.IdVendedor", out string? idv) && int.TryParse(idv, out int idvi)) Config.IdVendedor = idvi;
            if (ini.TryGetValue("Local.IdCliente", out string? idcl) && int.TryParse(idcl, out int idcli)) Config.IdClienteDefault = idcli;
            if ((ini.TryGetValue("Local.Balanza", out string? bal) || ini.TryGetValue("Local.lnBalanza", out bal)) && int.TryParse(bal, out int bali)) Config.Balanza = bali;
            if ((ini.TryGetValue("Local.ImpresionBalanza", out string? ib) || ini.TryGetValue("Local.lnImpresionBalanza", out ib)) && int.TryParse(ib, out int ibi)) Config.ImpresionBalanza = ibi;
            if ((ini.TryGetValue("Local.ImpresionNormal", out string? ino) || ini.TryGetValue("Local.lnImpresionNormal", out ino)) && int.TryParse(ino, out int inoi)) Config.ImpresionNormal = inoi;
            if ((ini.TryGetValue("Local.EntregaMercaderia", out string? em) || ini.TryGetValue("Local.lnEntrega_Mercaderia", out em)) && int.TryParse(em, out int emi)) Config.EntregaMercaderia = emi;
            if ((ini.TryGetValue("Local.MostrarVencimiento", out string? mv) || ini.TryGetValue("Local.lnMostrarVencimiento", out mv)) && int.TryParse(mv, out int mvi)) Config.MostrarVencimiento = mvi;

            if (ini.TryGetValue("FastCarga.ApiKey", out string? fcKey)) Config.FastCargaApiKey = fcKey.Trim();
            if (ini.TryGetValue("FastCarga.ApiModel", out string? fcModel) && !string.IsNullOrWhiteSpace(fcModel)) Config.FastCargaApiModel = fcModel.Trim();
            if (ini.TryGetValue("FastCarga.ApiModelsCatalog", out string? fcCat)) Config.FastCargaApiModelsCatalog = fcCat.Trim();
        }

        string sqlFile = Path.Combine(baseDir, string.IsNullOrWhiteSpace(Config.ConfigFile) ? "Firewall.sql" : Config.ConfigFile);
        if (File.Exists(sqlFile))
        {
            try
            {
                string[] lines = File.ReadAllLines(sqlFile, Encoding.Latin1);
                if (lines.Length >= 5)
                {
                    // Exact structure of Firewall.sql in VFP inicio.prg / SeleServidor.scx:
                    // Line 0: empty header line
                    // Line 1: Server Host (e.g. "localhost")
                    // Line 2: User (e.g. "root")
                    // Line 3: Password (encrypted)
                    // Line 4: Port (e.g. "3306")
                    string srv = VfpCompat.Encriptar("OFF", lines[1], "###$$$$").Trim();
                    string usr = VfpCompat.Encriptar("OFF", lines[2], "###$$$$").Trim();
                    string pwd = VfpCompat.Encriptar("OFF", lines[3], "###$$$$").Trim();
                    string portStr = VfpCompat.Encriptar("OFF", lines[4], "###$$$$").Trim();

                    if (!string.IsNullOrWhiteSpace(srv)) Config.SqlServerHost = srv;
                    if (!string.IsNullOrWhiteSpace(usr)) Config.SqlUser = usr;
                    if (!string.IsNullOrWhiteSpace(pwd)) Config.SqlPassword = pwd;
                    if (int.TryParse(portStr, out int p) && p > 0) Config.Port = p;
                }
            }
            catch
            {
                // Keep defaults
            }
        }

        LoadPreferencesFromDisk(baseDir);
    }

    private void LoadPreferencesFromDisk(string baseDir)
    {
        try
        {
            string prefPath = Path.Combine(baseDir, PreferencesFileName);
            if (File.Exists(prefPath))
            {
                string json = File.ReadAllText(prefPath, Encoding.UTF8);
                var loaded = JsonSerializer.Deserialize<AppPreferencesEntity>(json);
                if (loaded != null)
                {
                    loaded.CustomShortcuts = new Dictionary<string, string>(
                        loaded.CustomShortcuts ?? new Dictionary<string, string>(),
                        StringComparer.OrdinalIgnoreCase);
                    if (string.IsNullOrWhiteSpace(loaded.EstiloPos))
                        loaded.EstiloPos = "Renovado";
                    if (string.IsNullOrWhiteSpace(loaded.TemaAtelier))
                        loaded.TemaAtelier = "sefiros";
                    if (string.IsNullOrWhiteSpace(loaded.MonedaBaseIso))
                        loaded.MonedaBaseIso = "PYG";
                    if (string.IsNullOrWhiteSpace(loaded.Idioma))
                        loaded.Idioma = "es";
                    Preferences = loaded;
                }
            }
        }
        catch
        {
            // Keep default preferences
        }
    }

    public void SavePreferences()
    {
        try
        {
            string baseDir = string.IsNullOrWhiteSpace(Config.BaseDirectory)
                ? AppDomain.CurrentDomain.BaseDirectory
                : Config.BaseDirectory;
            string prefPath = Path.Combine(baseDir, PreferencesFileName);
            string json = JsonSerializer.Serialize(Preferences, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(prefPath, json, Encoding.UTF8);
        }
        catch
        {
            // Ignore write errors on read-only paths
        }
    }

    public void ResetToFactoryDefaults()
    {
        Preferences = new AppPreferencesEntity();
        Config.MonedaFactura = "GUARANI";
        Config.IdMonedaFactura = 1;
        Config.ListaPrecios = 1;
        Config.DescuentoMaximo = 100m;
        SavePreferences();
    }

    public void SaveLocalSettings()
    {
        try
        {
            string baseDir = string.IsNullOrWhiteSpace(Config.BaseDirectory)
                ? AppDomain.CurrentDomain.BaseDirectory
                : Config.BaseDirectory;
            string iniPath = Path.Combine(baseDir, "FireWall.Ini");
            var updates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Config.lcBaseDatos"] = Config.BaseDatos,
                ["Config.lnPort"] = Config.Port.ToString(),
                ["Config.lcRutaServer"] = Config.SqlServerHost,
                ["Config.lcTimbrado"] = Config.Timbrado,
                ["Config.lnDescuento"] = Config.DescuentoMaximo.ToString("0"),
                ["Config.lnAnulacionVenta"] = Config.AnulacionVenta.ToString(),
                ["Config.lcMonedaFactura"] = Config.MonedaFactura,
                ["Config.lcIDMonedaFactura"] = Config.IdMonedaFactura.ToString(),
                ["Local.Timbrado"] = Config.Timbrado,
                ["Local.DescuentoMaximo"] = Config.DescuentoMaximo.ToString("0"),
                ["Local.AnulacionVenta"] = Config.AnulacionVenta.ToString(),
                ["Local.ListaPrecios"] = Config.ListaPrecios.ToString(),
                ["Local.IdSucursal"] = Config.IdSucursal.ToString(),
                ["Local.IdCaja"] = Config.IdCaja.ToString(),
                ["Local.IdDeposito"] = Config.IdDeposito.ToString(),
                ["Local.IdVendedor"] = Config.IdVendedor.ToString(),
                ["Local.IdCliente"] = Config.IdClienteDefault.ToString(),
                ["Local.lnBalanza"] = Config.Balanza.ToString(),
                ["Local.lnImpresionBalanza"] = Config.ImpresionBalanza.ToString(),
                ["Local.lnImpresionNormal"] = Config.ImpresionNormal.ToString(),
                ["Local.lnEntrega_Mercaderia"] = Config.EntregaMercaderia.ToString(),
                ["Local.lnMostrarVencimiento"] = Config.MostrarVencimiento.ToString()
            };
            UpdateIniFile(iniPath, updates);
        }
        catch
        {
            // Ignore write errors on read-only paths
        }
    }

    public void SaveFastCargaSettings(string apiKey, string apiModel, string modelsCatalog)
    {
        Config.FastCargaApiKey = (apiKey ?? string.Empty).Trim();
        Config.FastCargaApiModel = string.IsNullOrWhiteSpace(apiModel)
            ? FastCargaParser.DefaultApiModel
            : apiModel.Trim();
        Config.FastCargaApiModelsCatalog = (modelsCatalog ?? string.Empty).Trim();

        try
        {
            string baseDir = string.IsNullOrWhiteSpace(Config.BaseDirectory)
                ? AppDomain.CurrentDomain.BaseDirectory
                : Config.BaseDirectory;
            string iniPath = Path.Combine(baseDir, "FireWall.Ini");
            var updates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["FastCarga.ApiKey"] = Config.FastCargaApiKey,
                ["FastCarga.ApiModel"] = Config.FastCargaApiModel,
                ["FastCarga.ApiModelsCatalog"] = Config.FastCargaApiModelsCatalog
            };
            UpdateIniFile(iniPath, updates);
        }
        catch
        {
            // Ignore write errors on read-only paths
        }
    }

    public void SaveServerCredentials(string host, string user, string password, int port, string database)
    {
        Config.SqlServerHost = string.IsNullOrWhiteSpace(host) ? "localhost" : host.Trim();
        Config.RutaServer = Config.SqlServerHost;
        Config.SqlUser = string.IsNullOrWhiteSpace(user) ? "root" : user.Trim();
        Config.SqlPassword = password ?? string.Empty;
        Config.Port = port > 0 ? port : 3306;
        Config.BaseDatos = string.IsNullOrWhiteSpace(database) ? "firewallcontrol" : database.Trim();

        string baseDir = string.IsNullOrWhiteSpace(Config.BaseDirectory)
            ? AppDomain.CurrentDomain.BaseDirectory
            : Config.BaseDirectory;
        string sqlFile = Path.Combine(baseDir, string.IsNullOrWhiteSpace(Config.ConfigFile) ? "Firewall.sql" : Config.ConfigFile);

        var lines = new[]
        {
            string.Empty,
            VfpCompat.Encriptar("ONN", Config.SqlServerHost, "###$$$$"),
            VfpCompat.Encriptar("ONN", Config.SqlUser, "###$$$$"),
            VfpCompat.Encriptar("ONN", Config.SqlPassword, "###$$$$"),
            VfpCompat.Encriptar("ONN", Config.Port.ToString(), "###$$$$")
        };

        try
        {
            File.WriteAllLines(sqlFile, lines, Encoding.Latin1);
            SaveLocalSettings();
        }
        catch
        {
            // Ignore write errors
        }
    }

    public string GetMySqlConnectionString()
    {
        string host = string.IsNullOrWhiteSpace(Config.SqlServerHost) ? "localhost" : Config.SqlServerHost.Trim();
        return $"Server={host};Port={Config.Port};Database={Config.BaseDatos};User ID={Config.SqlUser};Password={Config.SqlPassword};AllowZeroDateTime=False;ConvertZeroDateTime=True;ConnectionTimeout=4;DefaultCommandTimeout=30;CharacterSet=latin1;";
    }

    private static void UpdateIniFile(string path, Dictionary<string, string> updates)
    {
        var lines = File.Exists(path)
            ? new List<string>(File.ReadAllLines(path, Encoding.Latin1))
            : new List<string> { "[Config]", "[Local]" };

        var writtenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string currentSection = "";

        for (int i = 0; i < lines.Count; i++)
        {
            string line = lines[i].Trim();
            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                currentSection = line.Substring(1, line.Length - 2).Trim();
                continue;
            }

            int eqIdx = line.IndexOf('=');
            if (eqIdx > 0 && !line.StartsWith(";") && !line.StartsWith("#"))
            {
                string key = line.Substring(0, eqIdx).Trim();
                string fullKey = string.IsNullOrEmpty(currentSection) ? key : $"{currentSection}.{key}";
                if (updates.TryGetValue(fullKey, out string? newVal))
                {
                    lines[i] = $"{key}={newVal}";
                    writtenKeys.Add(fullKey);
                }
            }
        }

        // Group missing keys by their section and insert under each section (creating missing sections cleanly)
        foreach (var kvp in updates)
        {
            if (writtenKeys.Contains(kvp.Key))
                continue;

            int dotIdx = kvp.Key.IndexOf('.');
            string targetSection = dotIdx > 0 ? kvp.Key[..dotIdx] : "Local";
            string shortKey = dotIdx > 0 ? kvp.Key[(dotIdx + 1)..] : kvp.Key;

            int secIdx = lines.FindIndex(l => l.Trim().Equals($"[{targetSection}]", StringComparison.OrdinalIgnoreCase));
            if (secIdx < 0)
            {
                if (lines.Count > 0 && !string.IsNullOrWhiteSpace(lines[^1]))
                    lines.Add("");
                lines.Add($"[{targetSection}]");
                lines.Add($"{shortKey}={kvp.Value}");
            }
            else
            {
                lines.Insert(secIdx + 1, $"{shortKey}={kvp.Value}");
            }
        }

        File.WriteAllLines(path, lines, Encoding.Latin1);
    }

    private static Dictionary<string, string> ParseIniFile(string path)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string currentSection = "";

        foreach (string rawLine in File.ReadAllLines(path, Encoding.Latin1))
        {
            string line = rawLine.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith(";") || line.StartsWith("#"))
                continue;

            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                currentSection = line.Substring(1, line.Length - 2).Trim();
                continue;
            }

            int eqIdx = line.IndexOf('=');
            if (eqIdx > 0)
            {
                string key = line.Substring(0, eqIdx).Trim();
                string val = line.Substring(eqIdx + 1).Trim();
                string fullKey = string.IsNullOrEmpty(currentSection) ? key : $"{currentSection}.{key}";
                result[fullKey] = val;
            }
        }

        return result;
    }
}
