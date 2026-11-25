using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using FirewallControl.Core.Entities;
using FirewallControl.Core.Interfaces;

namespace FirewallControl.Infrastructure.Services;

/// <summary>
/// Pure 64-bit C# binary reader/writer for Visual FoxPro 9 DBF + FPT files:
/// - Styles\_MenuPrompts.dbf
/// - Styles\_MenuBars.dbf
/// - genes_productos.dbf (GEN_ID, BOTON_ID, CODIGO, NOMBRE, PRECIO, IMAGEN)
/// Eliminates any 32-bit VFPOLEDB runtime dependency on .NET 10 x64.
/// </summary>
public class VfpDbfService : IVfpDbfService
{
    private record DbfField(string Name, char Type, int Offset, int Length, int Decimals);

    static VfpDbfService()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public List<MenuPromptItem> LoadMenuStructure(string stylesDirectory, bool includeVeterinaria = false)
    {
        (string promptsPath, string barsPath) = ResolveMenuDbfPaths(stylesDirectory);

        if (string.IsNullOrEmpty(promptsPath) || string.IsNullOrEmpty(barsPath) ||
            !File.Exists(promptsPath) || !File.Exists(barsPath))
        {
            return BuildFallbackMenuStructure(includeVeterinaria);
        }

        var rawPrompts = ReadDbf(promptsPath);
        var rawBars = ReadDbf(barsPath);

        var prompts = new List<MenuPromptItem>();
        int recNo = 1;
        foreach (var row in rawPrompts)
        {
            int referencia = ParseInt(row.GetValueOrDefault("referencia") ?? row.GetValueOrDefault("id"));
            string promptText = (row.GetValueOrDefault("promptmenu") ?? row.GetValueOrDefault("caption") ?? "").Trim();
            string opcNumero = (row.GetValueOrDefault("opcnumero") ?? row.GetValueOrDefault("position") ?? "").Trim();
            bool activo = !row.ContainsKey("valor") || ParseBool(row["valor"]);

            bool isVet = opcNumero == "107" || promptText.Contains("Veterinaria", StringComparison.OrdinalIgnoreCase);
            if (isVet && !includeVeterinaria)
                continue;

            if (isVet && includeVeterinaria)
                activo = true;

            prompts.Add(new MenuPromptItem
            {
                RecNo = recNo++,
                Referencia = referencia > 0 ? referencia : recNo,
                PromptMenu = promptText,
                OpcNumero = string.IsNullOrEmpty(opcNumero) ? referencia.ToString(CultureInfo.InvariantCulture) : opcNumero,
                Activo = activo
            });
        }

        prompts = prompts
            .OrderBy(p => p.Referencia)
            .ThenBy(p => p.RecNo)
            .ToList();

        var allBars = new List<MenuBarItem>();
        int barRecNo = 1;
        foreach (var row in rawBars)
        {
            string opcMenu = (row.GetValueOrDefault("opcmenu") ?? row.GetValueOrDefault("prompt") ?? "").Trim();
            string opcNumero = (row.GetValueOrDefault("opcnumero") ?? row.GetValueOrDefault("number") ?? row.GetValueOrDefault("id") ?? "").Trim();
            int parentNu = ParseInt(row.GetValueOrDefault("parentnu"));
            string action = (row.GetValueOrDefault("comando") ?? row.GetValueOrDefault("action") ?? "").Trim();
            bool isSepAfter = ParseBool(row.GetValueOrDefault("separador"));
            bool isSubMenu = ParseBool(row.GetValueOrDefault("submenu")) || int.TryParse(action, out _);
            string parentMenu = (row.GetValueOrDefault("parentmenu") ?? "").Trim();
            if (string.IsNullOrEmpty(parentMenu) && int.TryParse(action, out int altSubId) && altSubId > 0)
                parentMenu = altSubId.ToString(CultureInfo.InvariantCulture);

            bool activo = !row.ContainsKey("valor") || ParseBool(row["valor"]);
            string imgRaw = (row.GetValueOrDefault("img") ?? row.GetValueOrDefault("picture") ?? "").Trim();
            if (imgRaw.Contains('\r') || imgRaw.Contains('\n'))
                imgRaw = imgRaw.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? string.Empty;

            if (opcMenu == "\\-")
                continue;

            allBars.Add(new MenuBarItem
            {
                RecNo = barRecNo++,
                ParentNu = parentNu,
                OpcMenu = opcMenu,
                OpcNumero = opcNumero,
                Separador = isSepAfter,
                Img = imgRaw,
                Comando = action,
                SubMenu = isSubMenu,
                ParentMenu = parentMenu,
                Activo = activo
            });
        }

        var barsByOwner = allBars
            .Where(b => !string.IsNullOrWhiteSpace(b.OpcNumero))
            .GroupBy(b => b.OpcNumero, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(b => b.ParentNu).ThenBy(b => b.RecNo).ToList(),
                StringComparer.OrdinalIgnoreCase);

        foreach (var bar in allBars)
        {
            if (bar.SubMenu && !string.IsNullOrWhiteSpace(bar.ParentMenu) &&
                barsByOwner.TryGetValue(bar.ParentMenu, out var subItems))
            {
                bar.SubItems = subItems;
            }
        }

        foreach (var prompt in prompts)
        {
            if (barsByOwner.TryGetValue(prompt.OpcNumero, out var children))
            {
                prompt.Children = children;
            }
        }

        return prompts.Count > 0 ? prompts : BuildFallbackMenuStructure(includeVeterinaria);
    }

    private static (string PromptsPath, string BarsPath) ResolveMenuDbfPaths(string stylesDirectory)
    {
        static bool TryInFolder(string folder, out string pPath, out string bPath)
        {
            string pLower = Path.Combine(folder, "_menuprompts.dbf");
            string bLower = Path.Combine(folder, "_menubars.dbf");
            if (File.Exists(pLower) && File.Exists(bLower))
            {
                pPath = pLower;
                bPath = bLower;
                return true;
            }

            string pCamel = Path.Combine(folder, "_MenuPrompts.dbf");
            string bCamel = Path.Combine(folder, "_MenuBars.dbf");
            if (File.Exists(pCamel) && File.Exists(bCamel))
            {
                pPath = pCamel;
                bPath = bCamel;
                return true;
            }

            pPath = string.Empty;
            bPath = string.Empty;
            return false;
        }

        if (!string.IsNullOrWhiteSpace(stylesDirectory) &&
            TryInFolder(stylesDirectory, out string directP, out string directB))
        {
            return (directP, directB);
        }

        string? current = string.IsNullOrWhiteSpace(stylesDirectory)
            ? AppDomain.CurrentDomain.BaseDirectory
            : Path.GetFullPath(stylesDirectory);

        for (int i = 0; i < 12 && !string.IsNullOrEmpty(current); i++)
        {
            foreach (string cand in new[]
            {
                Path.Combine(current, "Styles"),
                Path.Combine(current, "ParaOtraPC_FirewallControl_NET10", "Styles"),
                current
            })
            {
                if (TryInFolder(cand, out string p, out string b))
                    return (p, b);
            }
            current = Directory.GetParent(current)?.FullName;
        }

        return (string.Empty, string.Empty);
    }

    private static List<MenuPromptItem> BuildFallbackMenuStructure(bool includeVeterinaria)
    {
        var list = new List<MenuPromptItem>
        {
            new()
            {
                Referencia = 1, OpcNumero = "100", PromptMenu = "\\<Archivo", Activo = true,
                Children = new()
                {
                    new() { ParentNu = 1, OpcMenu = "Datos de la Empresa", Comando = "Do Form abm_datosempresa", Activo = true },
                    new() { ParentNu = 2, OpcMenu = "Usuarios y Grupos", Comando = "Do Form creausu", Activo = true },
                    new() { ParentNu = 3, OpcMenu = "Configurar Sistema", Comando = "Do Form configsys", Activo = true },
                    new() { ParentNu = 4, OpcMenu = "Parámetros de Impresora", Comando = "SYS(1037)", Activo = true, Separador = true },
                    new() { ParentNu = 5, OpcMenu = "Salir", Comando = "QUIT", Activo = true }
                }
            },
            new()
            {
                Referencia = 2, OpcNumero = "106", PromptMenu = "\\<Personales", Activo = true,
                Children = new()
                {
                    new() { ParentNu = 1, OpcMenu = "Legajos de Personales", Comando = "Do Form Crear_legajos", Activo = true },
                    new() { ParentNu = 2, OpcMenu = "Descuentos al Personal", Comando = "Do Form Crear_descuentopersonal", Activo = true },
                    new() { ParentNu = 3, OpcMenu = "Salarios y Liquidación", Comando = "Do Form Crear_salarios", Activo = true },
                    new() { ParentNu = 4, OpcMenu = "Vendedores", Comando = "Do Form Crear_vendedores", Activo = true },
                    new() { ParentNu = 5, OpcMenu = "Supervisores y Cobradores", Comando = "Do Form Crear_supervisores", Activo = true }
                }
            },
            new()
            {
                Referencia = 3, OpcNumero = "101", PromptMenu = "\\<Catalogos", Activo = true,
                Children = new()
                {
                    new() { ParentNu = 1, OpcMenu = "Productos y Stock", Comando = "Do Form Crear_productos", Activo = true },
                    new() { ParentNu = 2, OpcMenu = "Clientes", Comando = "Do Form Crear_clientes", Activo = true },
                    new() { ParentNu = 3, OpcMenu = "Proveedores", Comando = "Do Form Crear_proveedores", Activo = true },
                    new() { ParentNu = 4, OpcMenu = "Depósitos", Comando = "Do Form Crear_depositos", Activo = true },
                    new() { ParentNu = 5, OpcMenu = "Monedas y Sucursales", Comando = "Do Form Crear_monedas", Activo = true }
                }
            },
            new()
            {
                Referencia = 4, OpcNumero = "102", PromptMenu = "\\<Procesos", Activo = true,
                Children = new()
                {
                    new() { ParentNu = 1, OpcMenu = "Facturación POS (Ventas)", Comando = "Do Form Abm_ventas_autoservice", Activo = true },
                    new() { ParentNu = 2, OpcMenu = "Registro de Compras", Comando = "Do Form Crear_compras", Activo = true },
                    new() { ParentNu = 3, OpcMenu = "Pedidos de Compra", Comando = "Do Form Crear_pedidos", Activo = true },
                    new() { ParentNu = 4, OpcMenu = "Presupuestos", Comando = "Do Form Crear_presupuestos", Activo = true, Separador = true },
                    new() { ParentNu = 5, OpcMenu = "Movimientos de Caja", Comando = "Do Form Crear_movimientocaja", Activo = true },
                    new() { ParentNu = 6, OpcMenu = "Cobros a Clientes", Comando = "Do Form Crear_cobroclientes", Activo = true },
                    new() { ParentNu = 7, OpcMenu = "Pagos a Proveedores", Comando = "Do Form Crear_pagoproveedores", Activo = true },
                    new() { ParentNu = 8, OpcMenu = "Ajuste de Stock / Traslados", Comando = "Do Form Crear_ajustes", Activo = true }
                }
            },
            new()
            {
                Referencia = 6, OpcNumero = "105", PromptMenu = "\\<Informes", Activo = true,
                Children = new()
                {
                    new() { ParentNu = 1, OpcMenu = "Informe de Ventas", Comando = "Do Form Informe_ventas", Activo = true },
                    new() { ParentNu = 2, OpcMenu = "Informe de Lucro / Rentabilidad", Comando = "Do Form Informe_lucro", Activo = true },
                    new() { ParentNu = 3, OpcMenu = "Lista de Precios / Stock Depósito", Comando = "Do Form Informe_listaprecios", Activo = true },
                    new() { ParentNu = 4, OpcMenu = "Vencimiento de Productos", Comando = "Do Form Vencimiento_productos", Activo = true }
                }
            },
            new()
            {
                Referencia = 7, OpcNumero = "103", PromptMenu = "\\<Utilierias", Activo = true,
                Children = new()
                {
                    new() { ParentNu = 1, OpcMenu = "Configurar Servidor MySQL", Comando = "Do Form SeleServidor", Activo = true },
                    new() { ParentNu = 2, OpcMenu = "Ajuste Masivo de Precios", Comando = "Do Form Crear_productos_ajuste", Activo = true },
                    new() { ParentNu = 3, OpcMenu = "Agenda y Recordatorios", Comando = "Do Form Agenda", Activo = true }
                }
            },
            new()
            {
                Referencia = 8, OpcNumero = "104", PromptMenu = "A\\<yuda", Activo = true,
                Children = new()
                {
                    new() { ParentNu = 1, OpcMenu = "Acerca de Sefiros POS", Comando = "Do Form acercade", Activo = true }
                }
            }
        };

        if (includeVeterinaria)
        {
            list.Insert(4, new MenuPromptItem
            {
                Referencia = 5, OpcNumero = "107", PromptMenu = "\\<Veterinaria", Activo = true,
                Children = new()
                {
                    new() { ParentNu = 1, OpcMenu = "Pacientes", Comando = "Do Form Pacientes", Activo = true },
                    new() { ParentNu = 2, OpcMenu = "Peluquería", Comando = "Do Form Peluqueria", Activo = true },
                    new() { ParentNu = 3, OpcMenu = "Vacunas y Avisos", Comando = "Do Form Avisos_vacunas", Activo = true }
                }
            });
        }

        return list;
    }

    public List<GenesButtonItem> LoadAllGenesProducts(string dbfPath)
    {
        var result = new List<GenesButtonItem>();
        if (!File.Exists(dbfPath))
        {
            string? dir = Path.GetDirectoryName(Path.GetFullPath(dbfPath));
            for (int i = 0; i < 6 && dir != null; i++)
            {
                string cand1 = Path.Combine(dir, "genes_productos.dbf");
                if (File.Exists(cand1)) { dbfPath = cand1; break; }
                string cand2 = Path.Combine(dir, "styles", "genes_productos.dbf");
                if (File.Exists(cand2)) { dbfPath = cand2; break; }
                dir = Directory.GetParent(dir)?.FullName;
            }
            if (!File.Exists(dbfPath))
                return result;
        }

        string baseDir = Path.GetDirectoryName(Path.GetFullPath(dbfPath)) ?? AppDomain.CurrentDomain.BaseDirectory;
        if (Path.GetFileName(baseDir).Equals("styles", StringComparison.OrdinalIgnoreCase))
        {
            baseDir = Directory.GetParent(baseDir)?.FullName ?? baseDir;
        }

        var rows = ReadDbf(dbfPath);
        int autoId = 1;
        foreach (var row in rows)
        {
            int id = ParseInt(row.GetValueOrDefault("id"));
            if (id <= 0) id = autoId;
            autoId = Math.Max(autoId + 1, id + 1);

            int genId = ParseInt(row.GetValueOrDefault("nro_gen") ?? row.GetValueOrDefault("gen_id"));
            int orden = ParseInt(row.GetValueOrDefault("orden") ?? row.GetValueOrDefault("boton_id"));
            if (orden <= 0) orden = id;

            string codigo = (row.GetValueOrDefault("codigo") ?? "").Trim();
            string nombre = (row.GetValueOrDefault("nombre") ?? "").Trim();
            string tipo = (row.GetValueOrDefault("tipo") ?? "KG").Trim().ToUpperInvariant();
            if (tipo != "KG" && tipo != "UNIDAD" && tipo != "DOCENA")
                tipo = "KG";

            decimal precio = ParseDecimal(row.GetValueOrDefault("precio"));
            decimal costo = ParseDecimal(row.GetValueOrDefault("costo"));
            if (costo <= 0m && precio > 1m)
                costo = Math.Round(precio * 0.80m, 0);

            string iconoRaw = (row.GetValueOrDefault("icono") ?? row.GetValueOrDefault("imagen") ?? "").Trim();
            bool actualizar = row.ContainsKey("actualizar") ? ParseBool(row["actualizar"]) : true;
            bool syncSql = row.ContainsKey("sync_sql") ? ParseBool(row["sync_sql"]) : true;

            result.Add(new GenesButtonItem
            {
                Id = id,
                GenId = genId,
                BotonId = orden,
                Codigo = codigo,
                Nombre = nombre,
                Tipo = tipo,
                Precio = precio,
                Costo = costo,
                Icono = iconoRaw,
                Actualizar = actualizar,
                SyncSql = syncSql,
                IconoResolved = ResolveIconPath(baseDir, iconoRaw, genId)
            });
        }

        return result
            .OrderBy(x => x.GenId)
            .ThenBy(x => x.Orden <= 0 ? 999999 : x.Orden)
            .ThenBy(x => x.Id)
            .ToList();
    }

    public List<GenesButtonItem> LoadGenesButtons(string dbfPath, int genId)
    {
        var all = LoadAllGenesProducts(dbfPath)
            .Where(x => x.GenId == genId)
            .OrderBy(x => x.Orden <= 0 ? 999999 : x.Orden)
            .ThenBy(x => x.Id)
            .ToList();

        if (all.Count > 0)
            return all;

        // Fallback if empty DBF or uninitialized department
        var fallback = new List<GenesButtonItem>(25);
        for (int i = 1; i <= 25; i++)
        {
            fallback.Add(new GenesButtonItem
            {
                Id = i,
                GenId = genId,
                BotonId = i,
                Codigo = "",
                Nombre = $"Botón {i}",
                Tipo = "KG",
                Precio = 0m,
                Costo = 0m,
                Icono = ""
            });
        }
        return fallback;
    }

    public void SaveGenesButton(string dbfPath, GenesButtonItem item)
    {
        var allProducts = LoadAllGenesProducts(dbfPath);

        GenesButtonItem? match = null;
        if (item.Id > 0)
        {
            match = allProducts.FirstOrDefault(x => x.Id == item.Id);
        }
        if (match == null && !string.IsNullOrWhiteSpace(item.Codigo))
        {
            match = allProducts.FirstOrDefault(x =>
                x.GenId == item.GenId &&
                string.Equals(x.Codigo, item.Codigo, StringComparison.OrdinalIgnoreCase));
        }
        if (match == null && item.BotonId > 0)
        {
            match = allProducts.FirstOrDefault(x => x.GenId == item.GenId && x.BotonId == item.BotonId);
        }

        if (match != null)
        {
            match.GenId = item.GenId;
            if (item.BotonId > 0) match.BotonId = item.BotonId;
            match.Codigo = item.Codigo ?? "";
            match.Nombre = item.Nombre ?? "";
            match.Tipo = string.IsNullOrWhiteSpace(item.Tipo) ? "KG" : item.Tipo.ToUpperInvariant();
            match.Precio = item.Precio;
            match.Costo = item.Costo > 0 ? item.Costo : (item.Precio > 1 ? Math.Round(item.Precio * 0.80m, 0) : 8000m);
            match.Icono = item.Icono ?? "";
            match.Actualizar = item.Actualizar;
            match.SyncSql = item.SyncSql;
        }
        else
        {
            int nextId = allProducts.Count > 0 ? allProducts.Max(x => x.Id) + 1 : 1;
            int nextOrden = item.BotonId > 0
                ? item.BotonId
                : allProducts.Where(x => x.GenId == item.GenId).Select(x => x.Orden).DefaultIfEmpty(0).Max() + 1;

            item.Id = nextId;
            item.BotonId = nextOrden;
            if (string.IsNullOrWhiteSpace(item.Tipo)) item.Tipo = "KG";
            if (item.Costo <= 0 && item.Precio > 1) item.Costo = Math.Round(item.Precio * 0.80m, 0);
            allProducts.Add(item);
        }

        WriteGenesDbfAndMirror(dbfPath, allProducts);
    }

    public void SaveAllGenesProducts(string dbfPath, List<GenesButtonItem> items)
    {
        if (items == null || items.Count == 0)
            return;
        WriteGenesDbfAndMirror(dbfPath, items);
    }

    public void DeleteGenesProduct(string dbfPath, int id)
    {
        var allProducts = LoadAllGenesProducts(dbfPath);
        int removed = allProducts.RemoveAll(x => x.Id == id);
        if (removed > 0)
        {
            WriteGenesDbfAndMirror(dbfPath, allProducts);
        }
    }

    public void ReorderGenesProducts(string dbfPath, int genId, IReadOnlyList<int> orderedIds)
    {
        var allProducts = LoadAllGenesProducts(dbfPath);
        var genItems = allProducts.Where(x => x.GenId == genId).ToList();
        int order = 1;
        foreach (int id in orderedIds)
        {
            var prod = genItems.FirstOrDefault(x => x.Id == id);
            if (prod != null)
            {
                prod.Orden = order++;
            }
        }
        foreach (var prod in genItems.Where(x => !orderedIds.Contains(x.Id)))
        {
            prod.Orden = order++;
        }

        WriteGenesDbfAndMirror(dbfPath, allProducts);
    }

    public string GetNextFreeGeneCode(string dbfPath)
    {
        var allProducts = LoadAllGenesProducts(dbfPath);
        long maxCode = 8000000000000L;
        foreach (var p in allProducts)
        {
            if (long.TryParse(p.Codigo.Trim(), out long parsed) && parsed >= 8000000000000L && parsed < 9999999999999L)
            {
                if (parsed > maxCode) maxCode = parsed;
            }
        }
        return (maxCode + 1).ToString(CultureInfo.InvariantCulture);
    }

    private static string ResolveIconPath(string baseDir, string iconoRaw, int genId)
    {
        if (string.IsNullOrWhiteSpace(iconoRaw))
            return string.Empty;

        string clean = iconoRaw.Trim().Replace('/', '\\');
        if (Path.IsPathRooted(clean) && File.Exists(clean))
            return clean;

        string candidate1 = Path.Combine(baseDir, clean);
        if (File.Exists(candidate1))
            return candidate1;

        string candidatePng = Path.ChangeExtension(candidate1, ".png");
        if (File.Exists(candidatePng))
            return candidatePng;

        string fileOnly = Path.GetFileNameWithoutExtension(clean);
        for (int g = 1; g <= 4; g++)
        {
            int checkGen = g == 1 ? Math.Clamp(genId, 1, 4) : (g <= genId ? g - 1 : g);
            string dir = Path.Combine(baseDir, "images", "genes", $"gen{checkGen}");
            foreach (string ext in new[] { ".png", ".jpg", ".ico", ".bmp" })
            {
                string p = Path.Combine(dir, fileOnly + ext);
                if (File.Exists(p))
                    return p;
            }
        }

        return string.Empty;
    }

    private static void WriteGenesDbfAndMirror(string dbfPath, List<GenesButtonItem> records)
    {
        WriteGenesDbf(dbfPath, records);

        try
        {
            string? dir = Path.GetDirectoryName(Path.GetFullPath(dbfPath));
            if (!string.IsNullOrEmpty(dir))
            {
                string stylesPath = Path.Combine(dir, "styles", "genes_productos.dbf");
                if (Directory.Exists(Path.Combine(dir, "styles")))
                {
                    WriteGenesDbf(stylesPath, records);
                }
            }
        }
        catch
        {
            // Non-blocking mirror sync
        }
    }

    private static void WriteGenesDbf(string dbfPath, List<GenesButtonItem> records)
    {
        var win1252 = Encoding.GetEncoding(1252);
        // Exact 11-column schema of genes_productos.dbf in Visual FoxPro 9:
        // ID N(6,0), NRO_GEN N(1,0), ORDEN N(4,0), NOMBRE C(50), CODIGO C(30),
        // TIPO C(10), PRECIO N(12,2), ICONO C(120), ACTUALIZAR L(1), COSTO N(12,0), SYNC_SQL L(1)
        var fields = new (string Name, char Type, byte Length, byte Decimals)[]
        {
            ("ID", 'N', 6, 0),
            ("NRO_GEN", 'N', 1, 0),
            ("ORDEN", 'N', 4, 0),
            ("NOMBRE", 'C', 50, 0),
            ("CODIGO", 'C', 30, 0),
            ("TIPO", 'C', 10, 0),
            ("PRECIO", 'N', 12, 2),
            ("ICONO", 'C', 120, 0),
            ("ACTUALIZAR", 'L', 1, 0),
            ("COSTO", 'N', 12, 0),
            ("SYNC_SQL", 'L', 1, 0)
        };

        int headerFieldsSize = fields.Length * 32;
        const int vfpBacklinkSize = 263;
        ushort headerLen = (ushort)(32 + headerFieldsSize + 1 + vfpBacklinkSize);
        ushort recordLen = 248; // 1 + 6 + 1 + 4 + 50 + 30 + 10 + 12 + 120 + 1 + 12 + 1 = 248 bytes

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms, win1252, leaveOpen: true);

        bw.Write((byte)0x30);
        var now = DateTime.Now;
        bw.Write((byte)(now.Year - 1900));
        bw.Write((byte)now.Month);
        bw.Write((byte)now.Day);
        bw.Write((uint)records.Count);
        bw.Write(headerLen);
        bw.Write(recordLen);
        bw.Write(new byte[16]);
        bw.Write((byte)0x00);
        bw.Write((byte)0x03);
        bw.Write((ushort)0);

        uint runningOffset = 1;
        foreach (var f in fields)
        {
            byte[] nameBytes = new byte[11];
            byte[] rawName = Encoding.ASCII.GetBytes(f.Name);
            Array.Copy(rawName, nameBytes, Math.Min(rawName.Length, 10));
            bw.Write(nameBytes);
            bw.Write((byte)f.Type);
            bw.Write(runningOffset);
            bw.Write(f.Length);
            bw.Write(f.Decimals);
            bw.Write(new byte[14]);
            runningOffset += f.Length;
        }

        bw.Write((byte)0x0D);
        bw.Write(new byte[vfpBacklinkSize]);

        int autoId = 1;
        foreach (var rec in records)
        {
            int id = rec.Id > 0 ? rec.Id : autoId;
            autoId = Math.Max(autoId + 1, id + 1);
            int gen = Math.Clamp(rec.GenId, 1, 9);
            int orden = rec.Orden > 0 ? rec.Orden : 1;

            bw.Write((byte)0x20); // Not deleted
            bw.Write(FormatNumeric(id, 6, 0, win1252));
            bw.Write(FormatNumeric(gen, 1, 0, win1252));
            bw.Write(FormatNumeric(orden, 4, 0, win1252));
            bw.Write(PadFixedString(rec.Nombre, 50, win1252));
            bw.Write(PadFixedString(rec.Codigo, 30, win1252));
            bw.Write(PadFixedString(string.IsNullOrWhiteSpace(rec.Tipo) ? "KG" : rec.Tipo.ToUpperInvariant(), 10, win1252));
            bw.Write(FormatDecimal(rec.Precio, 12, 2, win1252));
            bw.Write(PadFixedString(rec.Icono, 120, win1252));
            bw.Write((byte)(rec.Actualizar ? 'T' : 'F'));
            bw.Write(FormatDecimal(rec.Costo, 12, 0, win1252));
            bw.Write((byte)(rec.SyncSql ? 'T' : 'F'));
        }

        bw.Write((byte)0x1A);
        bw.Flush();

        File.WriteAllBytes(dbfPath, ms.ToArray());
    }

    private static byte[] FormatNumeric(long value, int length, int decimals, Encoding enc)
    {
        string s = value.ToString(CultureInfo.InvariantCulture).PadLeft(length, ' ');
        if (s.Length > length) s = s.Substring(s.Length - length);
        return enc.GetBytes(s);
    }

    private static byte[] FormatDecimal(decimal value, int length, int decimals, Encoding enc)
    {
        string fmt = decimals > 0 ? "0." + new string('0', decimals) : "0";
        string s = value.ToString(fmt, CultureInfo.InvariantCulture).PadLeft(length, ' ');
        if (s.Length > length) s = s.Substring(s.Length - length);
        return enc.GetBytes(s);
    }

    private static byte[] PadFixedString(string? text, int length, Encoding enc)
    {
        string safe = text ?? "";
        if (safe.Length > length)
            safe = safe.Substring(0, length);
        return enc.GetBytes(safe.PadRight(length, ' '));
    }

    private static List<Dictionary<string, string>> ReadDbf(string dbfPath)
    {
        var win1252 = Encoding.GetEncoding(1252);
        byte[] bytes = File.ReadAllBytes(dbfPath);
        if (bytes.Length < 32)
            return new List<Dictionary<string, string>>();

        uint numRecords = BitConverter.ToUInt32(bytes, 4);
        ushort headerLen = BitConverter.ToUInt16(bytes, 8);
        ushort recordLen = BitConverter.ToUInt16(bytes, 10);

        byte[]? fptBytes = null;
        ushort fptBlockSize = 64;
        string fptPath = Path.ChangeExtension(dbfPath, ".fpt");
        if (!File.Exists(fptPath))
            fptPath = Path.ChangeExtension(dbfPath, ".FPT");
        if (File.Exists(fptPath))
        {
            fptBytes = File.ReadAllBytes(fptPath);
            if (fptBytes.Length >= 8)
            {
                fptBlockSize = (ushort)((fptBytes[6] << 8) | fptBytes[7]);
                if (fptBlockSize == 0) fptBlockSize = 64;
            }
        }

        var fields = new List<DbfField>();
        int pos = 32;
        int runningOffset = 1;
        while (pos + 32 <= headerLen && pos + 32 <= bytes.Length && bytes[pos] != 0x0D)
        {
            int nameEnd = pos;
            while (nameEnd < pos + 11 && bytes[nameEnd] != 0) nameEnd++;
            string name = Encoding.ASCII.GetString(bytes, pos, nameEnd - pos).Trim().ToLowerInvariant();
            char type = (char)bytes[pos + 11];
            byte len = bytes[pos + 16];
            byte dec = bytes[pos + 17];

            fields.Add(new DbfField(name, type, runningOffset, len, dec));
            runningOffset += len;
            pos += 32;
        }

        var rows = new List<Dictionary<string, string>>((int)numRecords);
        for (int r = 0; r < numRecords; r++)
        {
            int recStart = headerLen + (r * recordLen);
            if (recStart + recordLen > bytes.Length)
                break;

            if (bytes[recStart] == 0x2A)
                continue;

            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in fields)
            {
                int fStart = recStart + f.Offset;
                if (fStart + f.Length > bytes.Length)
                    continue;

                string val = "";
                if (f.Type == 'I' && f.Length == 4)
                {
                    val = BitConverter.ToInt32(bytes, fStart).ToString(CultureInfo.InvariantCulture);
                }
                else if (f.Type == 'M')
                {
                    int blockNum = 0;
                    if (f.Length == 4)
                    {
                        blockNum = BitConverter.ToInt32(bytes, fStart);
                    }
                    else
                    {
                        string s = win1252.GetString(bytes, fStart, f.Length).Trim();
                        int.TryParse(s, out blockNum);
                    }
                    if (blockNum > 0 && fptBytes != null)
                    {
                        int bOffset = blockNum * fptBlockSize;
                        if (bOffset + 8 <= fptBytes.Length)
                        {
                            int mLen = (fptBytes[bOffset + 4] << 24) | (fptBytes[bOffset + 5] << 16) | (fptBytes[bOffset + 6] << 8) | (fptBytes[bOffset + 7]);
                            if (mLen > 0 && bOffset + 8 + mLen <= fptBytes.Length)
                            {
                                val = win1252.GetString(fptBytes, bOffset + 8, mLen);
                            }
                        }
                    }
                }
                else
                {
                    val = win1252.GetString(bytes, fStart, f.Length).Trim();
                }

                dict[f.Name] = val;
            }
            rows.Add(dict);
        }

        return rows;
    }

    private static int ParseInt(string? val)
    {
        if (string.IsNullOrWhiteSpace(val)) return 0;
        if (int.TryParse(val.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out int res))
            return res;
        return 0;
    }

    private static decimal ParseDecimal(string? val)
    {
        if (string.IsNullOrWhiteSpace(val)) return 0m;
        if (decimal.TryParse(val.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal res))
            return res;
        return 0m;
    }

    private static bool ParseBool(string? val)
    {
        if (string.IsNullOrWhiteSpace(val)) return false;
        string s = val.Trim().ToUpperInvariant();
        return s == "T" || s == ".T." || s == "1" || s == "Y" || s == "TRUE";
    }
}
