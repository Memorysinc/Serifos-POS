using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using FirewallControl.Core.Business;
using FirewallControl.Core.Entities;
using FirewallControl.Core.Interfaces;

namespace FirewallControl.Infrastructure.Services;

/// <summary>
/// Replicates ABM_VENTAS_AUTOSERVICE_COBRO_CONTADO.SCX ticket & invoice printing:
/// - PROGS\ticket_01.prg (32-col thermal ticket when idcliente == 132 / Ocasional)
/// - PROGS\ticket_01_factura.prg (32-col legal thermal invoice when idcliente != 132 or TipoBoleta == 2)
/// Prints directly to the Windows printer configured in datosempresa.imprebalanza / imprenormal (or Windows Default Printer)
/// using Win32 GDI (Courier New 9pt) and sends the ESC/POS cash drawer + paper cut sequence via WinSpool RAW.
/// </summary>
public class TicketPrintService : ITicketPrintService
{
    private readonly IConfigService? _configService;

    public TicketPrintService() : this(null)
    {
    }

    public TicketPrintService(IConfigService? configService)
    {
        _configService = configService;
    }

    public string FormatTicket80mm(SaleEntity sale, CompanyDataEntity company)
    {
        var sb = new StringBuilder();
        var activeCurrency = LatamCurrencyCatalog.GetByCode(_configService?.Preferences?.MonedaBaseIso);
        string numFmt = activeCurrency.Decimals == 0 ? "#,##0" : "#,##0.00";
        string empNombre = (!string.IsNullOrWhiteSpace(company?.Descripcion)
            ? company.Descripcion.Trim()
            : "SEFIROS POS").ToUpperInvariant();

        string fechaStr = sale.FechaVenta.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        string horaStr = !string.IsNullOrWhiteSpace(sale.HoraVenta)
            ? sale.HoraVenta.Trim()
            : DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

        // Exact header from PROGS\ticket_01.prg lines 257-262
        sb.AppendLine(empNombre);
        sb.AppendLine($"Fecha y Hora.: {fechaStr} {horaStr}");
        sb.AppendLine($"Numero ticket: {sale.NroVenta}");
        sb.AppendLine(new string('=', 32));
        sb.AppendLine("Cant.    Desc. Precio    Total");
        sb.AppendLine(new string('=', 32));

        // Exact item lines from PROGS\ticket_01.prg lines 105-121
        foreach (var item in sale.Items)
        {
            string vdesc = (item.Descripcion ?? string.Empty).Trim();
            sb.AppendLine(vdesc);

            var (effectiveQty, lineTotal) = ResolvePrintableLineValues(item);
            string cantStr = effectiveQty.ToString("0.000", CultureInfo.InvariantCulture).PadLeft(9);
            string preStr = Math.Round(item.PreUnitario, 0, MidpointRounding.AwayFromZero).ToString("#,##0", CultureInfo.InvariantCulture).PadLeft(10);
            string totStr = Math.Round(lineTotal, 0, MidpointRounding.AwayFromZero).ToString("#,##0", CultureInfo.InvariantCulture).PadLeft(10);
            sb.AppendLine($"{cantStr} {preStr} {totStr}");
        }

        sb.AppendLine(new string('-', 32));

        if (sale.Descuento > 0)
        {
            sb.AppendLine($"{"Descuento:",-11}{Math.Round(sale.Descuento, activeCurrency.Decimals).ToString(numFmt, CultureInfo.InvariantCulture),12}");
        }

        string totalLbl = $"Total {activeCurrency.Symbol}:";
        sb.AppendLine($"{totalLbl,-11}{Math.Round(sale.TotalVenta, activeCurrency.Decimals).ToString(numFmt, CultureInfo.InvariantCulture),12}");
        sb.AppendLine(new string('-', 32));

        decimal entregaGs = sale.EntregaGuarani > 0 ? sale.EntregaGuarani : sale.TotalVenta;
        decimal vueltoGs = sale.VueltoGuarani;

        if (entregaGs > 0)
        {
            string entregaLbl = $"Entrega {activeCurrency.Symbol}:";
            sb.AppendLine($"{entregaLbl,-13}{Math.Round(entregaGs, activeCurrency.Decimals).ToString(numFmt, CultureInfo.InvariantCulture),12}");
        }
        sb.AppendLine(new string('-', 32));
        if (entregaGs > 0)
        {
            string vueltoLbl = $"Vuelto {activeCurrency.Symbol}:";
            sb.AppendLine($"{vueltoLbl,-11}{Math.Round(vueltoGs, activeCurrency.Decimals).ToString(numFmt, CultureInfo.InvariantCulture),12}");
        }
        sb.AppendLine(new string('-', 32));
        sb.AppendLine("Gracias por su preferencia..");
        sb.AppendLine($"Ud. fue atendido por :{(string.IsNullOrWhiteSpace(sale.NombreVendedor) ? sale.CidUsuario : sale.NombreVendedor.Trim())}");
        sb.AppendLine("Obs.: Nota sin valor fiscal..");
        sb.AppendLine(Center("** USO INTERNO **", 32));
        sb.AppendLine("    *779156817481*");

        return sb.ToString();
    }

    public string FormatLegalInvoice(SaleEntity sale, CompanyDataEntity company)
    {
        var sb = new StringBuilder();
        string empNombre = !string.IsNullOrWhiteSpace(company?.Descripcion) ? company.Descripcion.Trim() : "SEFIROS POS";
        string empRuc = company?.Ruc?.Trim() ?? "";
        string empTel = company?.Telefonos?.Trim() ?? "";
        string empDir = company?.Direccion?.Trim() ?? "";
        string timbrado = !string.IsNullOrWhiteSpace(sale.Timbrado) ? sale.Timbrado.Trim() : "12345678";
        string nroFac = !string.IsNullOrWhiteSpace(sale.NroFactura) ? sale.NroFactura.Trim() : sale.NroVenta.ToString();

        // Exact layout from PROGS\ticket_01_factura.prg lines 562-682
        sb.AppendLine($"Timbrado: {timbrado}");
        sb.AppendLine($"Empresa: {empNombre}");
        if (!string.IsNullOrWhiteSpace(empRuc))
            sb.AppendLine($"Ruc: {empRuc}");
        if (!string.IsNullOrWhiteSpace(empTel))
            sb.AppendLine($"Telefono: {empTel}");
        if (!string.IsNullOrWhiteSpace(empDir))
            sb.AppendLine($"Direccion: {empDir}");
        sb.AppendLine(sale.FormaPago == 1 ? "            Factura Contado" : "            Factura Credito");
        sb.AppendLine("               ****FACTURA****");
        sb.AppendLine($"            Numero Factura: {nroFac}");
        sb.AppendLine(new string('=', 32));
        sb.AppendLine("Cant.    Desc. Precio    Total");
        sb.AppendLine(new string('=', 32));

        decimal totalCantItems = 0m;
        foreach (var item in sale.Items)
        {
            var (effectiveQty, lineTotal) = ResolvePrintableLineValues(item);
            totalCantItems += effectiveQty;
            string vdesc = (item.Descripcion ?? string.Empty).Trim();
            if (vdesc.Length > 50) vdesc = vdesc.Substring(0, 50);
            sb.AppendLine(vdesc);

            string cantStr = effectiveQty.ToString("0.000", CultureInfo.InvariantCulture).PadLeft(9);
            string preStr = Math.Round(item.PreUnitario, 0, MidpointRounding.AwayFromZero).ToString("#,##0", CultureInfo.InvariantCulture).PadLeft(10);
            string totStr = Math.Round(lineTotal, 0, MidpointRounding.AwayFromZero).ToString("#,##0", CultureInfo.InvariantCulture).PadLeft(10);
            sb.AppendLine($"{cantStr} {preStr} {totStr}");
        }

        sb.AppendLine(new string('-', 32));
        sb.AppendLine($"{"TOTAL:",-11}{Math.Round(sale.TotalVenta, 0).ToString("#,##0", CultureInfo.InvariantCulture),12}");
        sb.AppendLine($"{"RES 347 SEDECO:",-15}{0,8}");
        sb.AppendLine($"{"TOTAL A PAGAR:",-14}{Math.Round(sale.TotalVenta, 0).ToString("#,##0", CultureInfo.InvariantCulture),9}");
        sb.AppendLine(new string('-', 32));
        sb.AppendLine($"Nombre: {(string.IsNullOrWhiteSpace(sale.NombreCliente) ? "CLIENTE OCASIONAL" : sale.NombreCliente.Trim())}");
        sb.AppendLine($"R.U.C........: {(string.IsNullOrWhiteSpace(sale.RucCliente) ? "X" : sale.RucCliente.Trim())}");
        sb.AppendLine();
        sb.AppendLine("SUB TOTALES");
        sb.AppendLine($"{"TOTAL EXENTO:",-17}{Math.Round(sale.TotalExenta, 0).ToString("#,##0", CultureInfo.InvariantCulture),12}");
        sb.AppendLine($"{"TOTAL GRAVADA 10:",-17}{Math.Round(sale.TotalIva10, 0).ToString("#,##0", CultureInfo.InvariantCulture),12}");
        sb.AppendLine($"{"TOTAL GRAVADA 5:",-17}{Math.Round(sale.TotalIva5, 0).ToString("#,##0", CultureInfo.InvariantCulture),12}");
        sb.AppendLine();
        sb.AppendLine("TOTALES");
        sb.AppendLine($"{"TOTAL IVA 10:",-14}{Math.Round(sale.LiquidacionIva10, 0).ToString("#,##0", CultureInfo.InvariantCulture),12}");
        sb.AppendLine($"{"TOTAL IVA 5:",-14}{Math.Round(sale.LiquidacionIva5, 0).ToString("#,##0", CultureInfo.InvariantCulture),12}");
        sb.AppendLine($"{"TOTAL IVA:",-14}{Math.Round(sale.LiquidacionIva10 + sale.LiquidacionIva5, 0).ToString("#,##0", CultureInfo.InvariantCulture),12}");
        sb.AppendLine();
        sb.AppendLine("Tipo de Factura: Efectivo");
        sb.AppendLine($"{"FECHA:",-11}{sale.FechaVenta:dd/MM/yyyy}");
        sb.AppendLine($"{"HORA:",-11}{sale.HoraVenta}");
        sb.AppendLine($"{"CAJA:",-11}{sale.IdCaja}");
        sb.AppendLine($"{"CAJERO:",-11}{sale.CidUsuario}");
        sb.AppendLine($"{"VENDEDOR:",-11}{sale.NombreVendedor}");
        sb.AppendLine($"TOTAL ITEMS VENDIDOS: {(int)Math.Ceiling(totalCantItems)}");
        sb.AppendLine("Gracias por su preferencia..");
        sb.AppendLine("ORIGINAL: CLIENTE");
        sb.AppendLine("DUPLICADO: ARCHIVO TRIBUTARIO");

        return sb.ToString();
    }

    /// <summary>
    /// Replicates PROGS\ticket_01.prg / ticket_01_factura.prg fractionable item printing fix:
    /// prefers exact tax bucket total (pre_exenta + pre_iva5 + pre_iva10) over cantidad * pre_unitario
    /// and recalculates 3-decimal printable weight/quantity when DB cantidad was rounded to 2 decimals.
    /// </summary>
    public static (decimal EffectiveQty, decimal EffectiveTotal) ResolvePrintableLineValues(SaleItemEntity item)
    {
        decimal lineTotal = item.PreExenta + item.PreIva5 + item.PreIva10;
        if (lineTotal <= 0m)
            lineTotal = item.PrecioTotal > 0m ? item.PrecioTotal : item.PreUnitario * item.Cantidad;

        decimal effectiveQty = item.Cantidad;
        if (item.PreUnitario > 0m && Math.Abs((item.PreUnitario * item.Cantidad) - lineTotal) >= 1m)
            effectiveQty = lineTotal / item.PreUnitario;

        return (effectiveQty, lineTotal);
    }

    public async Task PrintSaleAsync(SaleEntity sale, CompanyDataEntity company, bool showPreview = false)
    {
        // Replicate ABM_VENTAS_AUTOSERVICE_COBRO_CONTADO.SCX lines 549-553:
        // If idcliente == 132 (Ocasional) and TipoBoleta == 1 -> ticket_01.prg
        // Else -> ticket_01_factura.prg
        bool isLegalFactura = sale.TipoBoleta == 2 ||
                              (sale.IdCliente > 0 && sale.IdCliente != 132 &&
                               !string.Equals(sale.RucCliente?.Trim(), "X", StringComparison.OrdinalIgnoreCase));

        string content = isLegalFactura
            ? FormatLegalInvoice(sale, company)
            : FormatTicket80mm(sale, company);

        // 1. Save backup copy in Tickets folder
        try
        {
            string outDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tickets");
            Directory.CreateDirectory(outDir);
            string fileName = isLegalFactura
                ? $"Factura_{sale.NroVenta}_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
                : $"Ticket_{sale.NroVenta}_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            await File.WriteAllTextAsync(Path.Combine(outDir, fileName), content, Encoding.UTF8);
        }
        catch
        {
            // Non-blocking
        }

        // 2. Send directly to the Windows printer (datosempresa.imprebalanza / imprenormal / default printer)
        await Task.Run(() =>
        {
            string printerName = ResolvePrinterName(company, isLegalFactura);
            if (string.IsNullOrWhiteSpace(printerName))
                return;

            var prefs = _configService?.Preferences;
            // Print 1 single compact copy by default (do not multiply by datosempresa.CantVias=3!)
            int copies = prefs != null && prefs.CopiasTicket > 0
                ? Math.Clamp(prefs.CopiasTicket, 1, 5)
                : 1;
            int feedLines = prefs != null ? Math.Clamp(prefs.LineasAvanceCorte, 0, 8) : 2;
            bool autoCut = prefs?.CortarPapelAutomatico ?? true;
            bool openDrawer = prefs?.AbrirCajonConTicket ?? true;
            bool useRaw = (prefs?.UsarImpresionRawDirecta ?? true) && !IsVirtualDocumentPrinter(printerName);

            for (int c = 0; c < copies; c++)
            {
                if (useRaw)
                {
                    // Single compact RAW ESC/POS stream (just like VFP ??? CHR(27)+'@'):
                    // Avoids Windows GDI EndPage() full-page 297mm blank paper feed!
                    byte[] rawTicket = BuildFullEscPosRawTicket(content, feedLines, autoCut, openDrawer && c == 0);
                    if (TrySendRawBytesToPrinter(printerName, rawTicket, $"Ticket_{sale.NroVenta}"))
                        continue;
                }

                // Fallback for virtual document printers (PDF/XPS) or non-RAW drivers
                TryPrintWithWin32Gdi(printerName, content, $"Ticket_{sale.NroVenta}");
                if (autoCut && !IsVirtualDocumentPrinter(printerName))
                {
                    byte[] cutAndKick = BuildEscPosCutAndDrawerPayload(feedLines, autoCut, openDrawer && c == 0);
                    TrySendRawBytesToPrinter(printerName, cutAndKick, $"Cut_{sale.NroVenta}");
                }
            }
        });
    }

    private static bool IsVirtualDocumentPrinter(string printerName)
    {
        if (string.IsNullOrWhiteSpace(printerName))
            return false;
        string u = printerName.ToUpperInvariant();
        return u.Contains("PDF") || u.Contains("XPS") || u.Contains("ONENOTE") || u.Contains("FAX");
    }

    public static List<string> GetInstalledPrinters()
    {
        var list = new List<string>();
        try
        {
            string def = GetWindowsDefaultPrinterName();
            if (!string.IsNullOrWhiteSpace(def))
                list.Add(def);

            // Query installed printers from registry HKLM (Windows)
            if (OperatingSystem.IsWindows())
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Print\Printers");
                if (key != null)
                {
                    foreach (string name in key.GetSubKeyNames())
                    {
                        if (!string.IsNullOrWhiteSpace(name) && !list.Contains(name, StringComparer.OrdinalIgnoreCase))
                            list.Add(name);
                    }
                }
            }
        }
        catch
        {
            // Ignore registry errors
        }
        return list;
    }

    private static string ResolvePrinterName(CompanyDataEntity? company, bool isLegalFactura)
    {
        var candidates = new List<string>();
        if (company != null)
        {
            // ticket_01.prg and ticket_01_factura.prg both use datosempresa.imprebalanza first!
            if (!string.IsNullOrWhiteSpace(company.ImpreBalanza))
                candidates.Add(company.ImpreBalanza.Trim());
            if (isLegalFactura && !string.IsNullOrWhiteSpace(company.ImpreLegal))
                candidates.Add(company.ImpreLegal.Trim());
            if (!string.IsNullOrWhiteSpace(company.ImpreNormal))
                candidates.Add(company.ImpreNormal.Trim());
            if (!string.IsNullOrWhiteSpace(company.ImpreLegal))
                candidates.Add(company.ImpreLegal.Trim());
        }

        foreach (string candidate in candidates)
        {
            if (CanOpenPrinter(candidate))
                return candidate;
        }

        // Fallback to Windows Default Printer if configured printer name from DB belongs to another PC
        string defaultPrinter = GetWindowsDefaultPrinterName();
        if (!string.IsNullOrWhiteSpace(defaultPrinter) && CanOpenPrinter(defaultPrinter))
            return defaultPrinter;

        return candidates.Count > 0 ? candidates[0] : string.Empty;
    }

    private static bool CanOpenPrinter(string printerName)
    {
        if (string.IsNullOrWhiteSpace(printerName))
            return false;

        if (OpenPrinter(printerName, out IntPtr hPrinter, IntPtr.Zero) && hPrinter != IntPtr.Zero)
        {
            ClosePrinter(hPrinter);
            return true;
        }
        return false;
    }

    private static string GetWindowsDefaultPrinterName()
    {
        int pcchBuffer = 0;
        GetDefaultPrinter(null, ref pcchBuffer);
        if (pcchBuffer <= 0)
            return string.Empty;

        var sb = new StringBuilder(pcchBuffer);
        if (GetDefaultPrinter(sb, ref pcchBuffer))
            return sb.ToString();

        return string.Empty;
    }

    /// <summary>
    /// Exact equivalent of VFP:
    /// SET DEVICE TO PRINTER
    /// SET PRINTER TO NAME (lcprinter)
    /// SET PRINTER FONT 'Courier', 9
    /// @ fila, 00 SAY ...
    /// </summary>
    private static bool TryPrintWithWin32Gdi(string printerName, string content, string docName)
    {
        IntPtr hdc = IntPtr.Zero;
        IntPtr hFont = IntPtr.Zero;
        IntPtr hOldFont = IntPtr.Zero;
        try
        {
            hdc = CreateDC("WINSPOOL", printerName, null, IntPtr.Zero);
            if (hdc == IntPtr.Zero)
                return false;

            var di = new DOCINFOGDI
            {
                cbSize = Marshal.SizeOf<DOCINFOGDI>(),
                lpszDocName = docName
            };

            if (StartDoc(hdc, ref di) <= 0)
                return false;

            if (StartPage(hdc) <= 0)
            {
                EndDoc(hdc);
                return false;
            }

            int dpiY = GetDeviceCaps(hdc, LOGPIXELSY);
            if (dpiY <= 0) dpiY = 203;
            int fontHeight = -MulDiv(9, dpiY, 72); // 9pt Courier just like SET PRINTER FONT 'Courier', 9

            hFont = CreateFont(
                fontHeight, 0, 0, 0,
                400, // FW_NORMAL
                0, 0, 0,
                0, // ANSI_CHARSET
                0, 0, 0,
                49, // FIXED_PITCH | FF_MODERN
                "Courier New");

            if (hFont != IntPtr.Zero)
                hOldFont = SelectObject(hdc, hFont);

            int lineHeight = Math.Max(Math.Abs(fontHeight) + (dpiY / 18), 16);
            string[] lines = content.Replace("\r\n", "\n").Split('\n');
            int y = 4;

            foreach (string line in lines)
            {
                TextOut(hdc, 2, y, line, line.Length);
                y += lineHeight;
            }

            if (hOldFont != IntPtr.Zero)
            {
                SelectObject(hdc, hOldFont);
                hOldFont = IntPtr.Zero;
            }

            EndPage(hdc);
            EndDoc(hdc);
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (hFont != IntPtr.Zero)
                DeleteObject(hFont);
            if (hdc != IntPtr.Zero)
                DeleteDC(hdc);
        }
    }

    private static byte[] BuildEscPosCutAndDrawerPayload(int feedLines = 2, bool autoCut = true, bool openDrawer = true)
    {
        using var ms = new MemoryStream();
        if (openDrawer)
        {
            ms.Write(new byte[] { 27, 112, 0, 48, 55, 121 });
        }
        byte[] dashLine = Encoding.Latin1.GetBytes("--------------------------------\r\n");
        ms.Write(dashLine);
        for (int i = 0; i < Math.Clamp(feedLines, 0, 8); i++)
        {
            ms.Write(new byte[] { 13, 10 });
        }
        if (autoCut)
        {
            ms.Write(new byte[] { 29, 86, 0 }); // GS V 0 (Full Cut)
        }
        return ms.ToArray();
    }

    private static byte[] BuildFullEscPosRawTicket(string content, int feedLines = 2, bool autoCut = true, bool openDrawer = true)
    {
        using var ms = new MemoryStream();
        ms.Write(new byte[] { 27, (byte)'@' }); // ESC @ (Initialize printer)
        byte[] body = Encoding.Latin1.GetBytes(content.Replace("\r\n", "\n").Replace("\n", "\r\n"));
        ms.Write(body);
        byte[] footer = BuildEscPosCutAndDrawerPayload(feedLines, autoCut, openDrawer);
        ms.Write(footer);
        return ms.ToArray();
    }

    private static bool TrySendRawBytesToPrinter(string printerName, byte[] bytes, string docName)
    {
        if (!OpenPrinter(printerName, out IntPtr hPrinter, IntPtr.Zero) || hPrinter == IntPtr.Zero)
            return false;

        try
        {
            var di = new DOC_INFO_1
            {
                pDocName = docName,
                pDatatype = "RAW"
            };

            if (!StartDocPrinter(hPrinter, 1, ref di))
                return false;

            try
            {
                if (!StartPagePrinter(hPrinter))
                    return false;

                try
                {
                    IntPtr pUnmanagedBytes = Marshal.AllocCoTaskMem(bytes.Length);
                    try
                    {
                        Marshal.Copy(bytes, 0, pUnmanagedBytes, bytes.Length);
                        return WritePrinter(hPrinter, pUnmanagedBytes, bytes.Length, out _);
                    }
                    finally
                    {
                        Marshal.FreeCoTaskMem(pUnmanagedBytes);
                    }
                }
                finally
                {
                    EndPagePrinter(hPrinter);
                }
            }
            finally
            {
                EndDocPrinter(hPrinter);
            }
        }
        catch
        {
            return false;
        }
        finally
        {
            ClosePrinter(hPrinter);
        }
    }

    private static string Center(string text, int width)
    {
        if (string.IsNullOrEmpty(text)) return "";
        if (text.Length >= width) return text.Substring(0, width);
        int leftPadding = (width - text.Length) / 2;
        return text.PadLeft(leftPadding + text.Length);
    }

    private const int LOGPIXELSY = 90;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DOCINFOGDI
    {
        public int cbSize;
        public string lpszDocName;
        public string? lpszOutput;
        public string? lpszDatatype;
        public uint fwType;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DOC_INFO_1
    {
        public string pDocName;
        public string? pOutputFile;
        public string pDatatype;
    }

    [DllImport("winspool.Drv", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetDefaultPrinter(StringBuilder? pszBuffer, ref int pcchBuffer);

    [DllImport("winspool.Drv", EntryPoint = "OpenPrinterW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool OpenPrinter(string szPrinter, out IntPtr hPrinter, IntPtr pd);

    [DllImport("winspool.Drv", SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", EntryPoint = "StartDocPrinterW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool StartDocPrinter(IntPtr hPrinter, int level, ref DOC_INFO_1 di);

    [DllImport("winspool.Drv", SetLastError = true)]
    private static extern bool EndDocPrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", SetLastError = true)]
    private static extern bool StartPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", SetLastError = true)]
    private static extern bool EndPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", SetLastError = true)]
    private static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, int dwCount, out int dwWritten);

    [DllImport("gdi32.dll", EntryPoint = "CreateDCW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateDC(string lpszDriver, string lpszDevice, string? lpszOutput, IntPtr lpInitData);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll", EntryPoint = "StartDocW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int StartDoc(IntPtr hdc, ref DOCINFOGDI lpdi);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern int EndDoc(IntPtr hdc);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern int StartPage(IntPtr hdc);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern int EndPage(IntPtr hdc);

    [DllImport("gdi32.dll", EntryPoint = "TextOutW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool TextOut(IntPtr hdc, int nXStart, int nYStart, string lpString, int cbString);

    [DllImport("gdi32.dll", EntryPoint = "CreateFontW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateFont(
        int nHeight, int nWidth, int nEscapement, int nOrientation, int fnWeight,
        uint fdwItalic, uint fdwUnderline, uint fdwStrikeOut, uint fdwCharSet,
        uint fdwOutputPrecision, uint fdwClipPrecision, uint fdwQuality,
        uint fdwPitchAndFamily, string lpszFace);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

    [DllImport("kernel32.dll")]
    private static extern int MulDiv(int nNumber, int nNumerator, int nDenominator);
}
