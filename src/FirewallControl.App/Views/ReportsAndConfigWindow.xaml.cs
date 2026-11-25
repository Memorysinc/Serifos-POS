using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Media;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FirewallControl.App.Services;
using FirewallControl.Core.Business;
using FirewallControl.Core.Entities;
using FirewallControl.Core.Interfaces;

namespace FirewallControl.App.Views;

/// <summary>
/// Replicates informe_ventas.scx, informe_lucro.scx, informe_lucroproductos.scx,
/// abm_datosempresa.scx, creausu.scx, and userclave.scx.
/// </summary>
public partial class ReportsAndConfigWindow : Window
{
    private readonly ISalesRepository _salesRepository;
    private readonly ISystemRepository _systemRepository;
    private readonly ITicketPrintService _ticketPrintService;
    private readonly ICustomerRepository _customerRepository;
    private readonly IProductRepository _productRepository;
    private readonly IWindowManagerService _windowManager;

    private CompanyDataEntity _company = new();
    private UserEntity? _selectedUser;
    private IReadOnlyList<SaleEntity> _currentSales = Array.Empty<SaleEntity>();
    private IReadOnlyList<ProfitReportItemEntity> _currentLucroItems = Array.Empty<ProfitReportItemEntity>();
    private IReadOnlyList<ProfitReportItemEntity> _currentLucroProdItems = Array.Empty<ProfitReportItemEntity>();

    public ReportsAndConfigWindow(
        ISalesRepository salesRepository,
        ISystemRepository systemRepository,
        ITicketPrintService ticketPrintService,
        ICustomerRepository customerRepository,
        IProductRepository productRepository,
        IWindowManagerService windowManager)
    {
        InitializeComponent();
        _salesRepository = salesRepository;
        _systemRepository = systemRepository;
        _ticketPrintService = ticketPrintService;
        _customerRepository = customerRepository;
        _productRepository = productRepository;
        _windowManager = windowManager;

        var defaultFrom = DateTime.Today.AddDays(-30);
        var defaultTo = DateTime.Today;

        DpVentasDesde.SelectedDate = defaultFrom;
        DpVentasHasta.SelectedDate = defaultTo;

        DpLucroDesde.SelectedDate = defaultFrom;
        DpLucroHasta.SelectedDate = defaultTo;

        DpLucroProdDesde.SelectedDate = defaultFrom;
        DpLucroProdHasta.SelectedDate = defaultTo;

        Loaded += async (_, _) =>
        {
            await LoadCompanyDataAsync();
            await LoadSalesReportAsync();
            await LoadLucroReportAsync();
            await LoadLucroProductosReportAsync();
            await LoadUsersAsync();
        };
    }

    public void Configure(int initialTab)
    {
        MainTabs.SelectedIndex = Math.Clamp(initialTab, 0, 4);
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
            return;
        }

        if (e.Key == Key.F3)
        {
            if (MainTabs.SelectedIndex == 1 && ChkLucroTodosClientes.IsChecked != true)
            {
                e.Handled = true;
                OpenCustomerLookupForLucro();
            }
            else if (MainTabs.SelectedIndex == 2 && ChkLucroTodosProductos.IsChecked != true)
            {
                e.Handled = true;
                OpenProductLookupForLucro();
            }
        }
    }

    // =========================================================================
    // TAB 0: INFORME DE VENTAS (informe_ventas.scx)
    // =========================================================================

    private async Task LoadSalesReportAsync()
    {
        try
        {
            _currentSales = await _salesRepository.GetRecentSalesAsync(DpVentasDesde.SelectedDate, DpVentasHasta.SelectedDate, 1000);
            ApplySalesFilter();
        }
        catch (Exception ex)
        {
            LblStatus.Text = $"Error al generar informe de ventas: {ex.Message}";
        }
    }

    private void ApplySalesFilter()
    {
        if (GridSalesReport == null)
            return;

        string filter = TxtBuscarVenta?.Text?.Trim() ?? string.Empty;
        IReadOnlyList<SaleEntity> filtered = _currentSales;
        if (!string.IsNullOrWhiteSpace(filter))
        {
            filtered = _currentSales.Where(s =>
                s.NroVenta.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                (s.NombreCliente ?? string.Empty).Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                (s.NroFactura ?? string.Empty).Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                (s.HoraVenta ?? string.Empty).Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                (s.CidUsuario ?? string.Empty).Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        GridSalesReport.ItemsSource = filtered;
        LblCountSales.Text = filtered.Count.ToString("N0");
        LblTotalSalesAmount.Text = $"{filtered.Sum(x => x.TotalVenta):N0} Gs.";

        if (filtered.Count > 0)
        {
            GridSalesReport.SelectedIndex = 0;
        }
        else
        {
            GridSaleDetails.ItemsSource = null;
            LblDetalleVentaHeader.Text = "QUÉ SE VENDIÓ EN LA VENTA SELECCIONADA";
            LblDetalleVentaSub.Text = "No hay ventas para el filtro actual.";
            LblDetalleFooterLeft.Text = "Entrega: 0 Gs.  ·  Vuelto: 0 Gs.";
            LblDetalleFooterRight.Text = "Total Venta: 0 Gs.";
        }

        LblStatus.Text = $"✔ Informe de Ventas (informe_ventas.scx): {filtered.Count} operaciones mostradas.";
    }

    private async void BtnFiltroHoy_Click(object sender, RoutedEventArgs e)
    {
        DpVentasDesde.SelectedDate = DateTime.Today;
        DpVentasHasta.SelectedDate = DateTime.Today;
        await LoadSalesReportAsync();
    }

    private void TxtBuscarVenta_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplySalesFilter();
    }

    private async void BtnDeleteSaleRow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is SaleEntity sale)
        {
            GridSalesReport.SelectedItem = sale;
            await DeleteSelectedSaleWithPromptAsync();
        }
    }

    private async void GridSalesReport_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GridSalesReport.SelectedItem is not SaleEntity row)
        {
            GridSaleDetails.ItemsSource = null;
            return;
        }

        try
        {
            var fullSale = await _salesRepository.GetSaleByIdAsync(row.IdVenta);
            if (fullSale != null)
            {
                GridSaleDetails.ItemsSource = fullSale.Items;
                LblDetalleVentaHeader.Text = $"VENTA #{row.NroVenta}  ·  HORA EXACTA: {row.HoraVenta}  ({fullSale.Items.Count} ítems)";
                LblDetalleVentaSub.Text = $"Fecha: {row.FechaVenta:dd/MM/yyyy}  ·  Caja {row.IdCaja}  ·  Cliente: {row.NombreCliente}  ·  Usuario: {row.CidUsuario}";
                LblDetalleFooterLeft.Text = $"Entrega: {fullSale.EntregaGuarani:N0} Gs.  ·  Vuelto: {fullSale.VueltoGuarani:N0} Gs.";
                LblDetalleFooterRight.Text = $"Total Venta: {fullSale.TotalVenta:N0} Gs.";
            }
        }
        catch (Exception ex)
        {
            LblStatus.Text = $"Error al cargar detalle de venta #{row.NroVenta}: {ex.Message}";
        }
    }

    private async void GridSalesReport_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            await ReprintSelectedSaleWithPromptAsync();
        }
        else if (e.Key == Key.Delete)
        {
            e.Handled = true;
            await DeleteSelectedSaleWithPromptAsync();
        }
    }

    private async void BtnRunSalesReport_Click(object sender, RoutedEventArgs e) => await LoadSalesReportAsync();

    private async void BtnReprintSelectedSale_Click(object sender, RoutedEventArgs e) => await ReprintSelectedSaleWithPromptAsync();

    private async void BtnDeleteSelectedSale_Click(object sender, RoutedEventArgs e) => await DeleteSelectedSaleWithPromptAsync();

    private async Task ReprintSelectedSaleWithPromptAsync()
    {
        if (GridSalesReport.SelectedItem is not SaleEntity row)
        {
            SystemSounds.Hand.Play();
            LblStatus.Text = "⚠ Seleccione una venta del listado para reimprimir.";
            return;
        }

        var confirm = MessageBox.Show(
            this,
            $"¿Desea REIMPRIMIR el ticket de esta venta?\n\nVenta Nro: {row.NroVenta}\nCliente: {row.NombreCliente}\nTotal: {row.TotalVenta:N0} Gs.",
            "Confirmar Reimpresión (informe_ventas)",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes)
            return;

        var fullSale = await _salesRepository.GetSaleByIdAsync(row.IdVenta);
        if (fullSale != null)
        {
            await _ticketPrintService.PrintSaleAsync(fullSale, _company);
            LblStatus.Text = $"✔ Ticket de Venta #{fullSale.NroVenta} reimpreso correctamente.";
        }
    }

    /// <summary>
    /// Exact replica of SUPR / DEL in informe_ventas.scx:
    /// Prompts user, deletes from movimientocaja, itemsventas, and ventas, restores stock, and refreshes list.
    /// </summary>
    private async Task DeleteSelectedSaleWithPromptAsync()
    {
        if (GridSalesReport.SelectedItem is not SaleEntity row)
        {
            SystemSounds.Hand.Play();
            LblStatus.Text = "⚠ Seleccione una venta del listado para eliminar.";
            return;
        }

        var confirm = MessageBox.Show(
            this,
            $"¿Está seguro de que desea BORRAR PERMANENTEMENTE esta venta y su movimiento de caja?\n\n" +
            $"ID Venta: {row.IdVenta} | Nro. Venta: {row.NroVenta}\n" +
            $"Fecha: {row.FechaVenta:dd/MM/yyyy} {row.HoraVenta}\n" +
            $"Cliente: {row.NombreCliente}\n" +
            $"Total: {row.TotalVenta:N0} Gs.",
            "Confirmar Eliminación Permanente (informe_ventas)",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
            return;

        try
        {
            bool deleted = await _salesRepository.DeleteSalePermanentlyAsync(row.IdVenta);
            if (deleted)
            {
                LblStatus.Text = $"✔ La venta #{row.NroVenta} y su movimiento de caja han sido eliminados correctamente.";
                await LoadSalesReportAsync();
            }
            else
            {
                LblStatus.Text = $"⚠ No se encontró la venta #{row.IdVenta} para eliminar.";
            }
        }
        catch (Exception ex)
        {
            SystemSounds.Hand.Play();
            MessageBox.Show(this, $"Ocurrió un error al intentar borrar la venta:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnExportSalesReport_Click(object sender, RoutedEventArgs e)
    {
        if (_currentSales.Count == 0)
        {
            LblStatus.Text = "No hay ventas cargadas para exportar.";
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("==========================================================================================");
        sb.AppendLine($"  {_company.Descripcion} - RUC: {_company.Ruc}");
        sb.AppendLine($"  INFORME DE VENTAS DESDE {DpVentasDesde.SelectedDate:dd/MM/yyyy} HASTA {DpVentasHasta.SelectedDate:dd/MM/yyyy}");
        sb.AppendLine("==========================================================================================");
        sb.AppendLine($"{"FECHA",-12} {"HORA",-9} {"VENTA",-10} {"FACTURA",-15} {"CLIENTE",-26} {"TOTAL Gs.",14}");
        sb.AppendLine("------------------------------------------------------------------------------------------");

        foreach (var s in _currentSales)
        {
            string cli = s.NombreCliente.Length > 25 ? s.NombreCliente[..25] : s.NombreCliente;
            sb.AppendLine($"{s.FechaVenta:dd/MM/yyyy,-12} {s.HoraVenta,-9} {s.NroVenta,-10} {s.NroFactura,-15} {cli,-26} {s.TotalVenta,14:N0}");
        }

        sb.AppendLine("------------------------------------------------------------------------------------------");
        sb.AppendLine($"CANTIDAD DE OPERACIONES: {_currentSales.Count}   |   TOTAL VENTAS: {_currentSales.Sum(x => x.TotalVenta):N0} Gs.");
        sb.AppendLine("==========================================================================================");

        SaveAndOpenReportFile("informe_ventas", sb.ToString());
    }

    // =========================================================================
    // TAB 1: INFORME COSTO BENEFICIO Y EGRESOS (informe_lucro.scx)
    // =========================================================================

    private void ChkLucroTodosClientes_Changed(object sender, RoutedEventArgs e)
    {
        if (TxtLucroRuc == null || BtnBuscarClienteLucro == null || LblLucroClienteNombre == null)
            return;

        bool todos = ChkLucroTodosClientes.IsChecked == true;
        TxtLucroRuc.IsEnabled = !todos;
        BtnBuscarClienteLucro.IsEnabled = !todos;
        if (todos)
        {
            TxtLucroRuc.Text = "";
            LblLucroClienteNombre.Text = "(TODOS LOS CLIENTES)";
        }
        else
        {
            TxtLucroRuc.Focus();
        }
    }

    private async void TxtLucroRuc_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F3)
        {
            e.Handled = true;
            OpenCustomerLookupForLucro();
        }
        else if (e.Key == Key.Enter)
        {
            e.Handled = true;
            string ruc = TxtLucroRuc.Text.Trim();
            if (!string.IsNullOrEmpty(ruc))
            {
                var foundList = await _customerRepository.SearchCustomersAsync(ruc, 5);
                var c = foundList.FirstOrDefault(x => string.Equals(x.Ruc.Trim(), ruc, StringComparison.OrdinalIgnoreCase)) ?? foundList.FirstOrDefault();
                if (c != null)
                {
                    TxtLucroRuc.Text = c.Ruc;
                    LblLucroClienteNombre.Text = c.Nombres;
                }
                else
                {
                    SystemSounds.Hand.Play();
                    LblLucroClienteNombre.Text = "⚠ CLIENTE NO EXISTE";
                }
            }
            await LoadLucroReportAsync();
        }
    }

    private void BtnBuscarClienteLucro_Click(object sender, RoutedEventArgs e) => OpenCustomerLookupForLucro();

    private void OpenCustomerLookupForLucro()
    {
        _windowManager.OpenCustomersModule(0, customer =>
        {
            ChkLucroTodosClientes.IsChecked = false;
            TxtLucroRuc.Text = customer.Ruc;
            LblLucroClienteNombre.Text = customer.Nombres;
            _ = LoadLucroReportAsync();
        });
    }

    private async void BtnLucroHoy_Click(object sender, RoutedEventArgs e)
    {
        DpLucroDesde.SelectedDate = DateTime.Today;
        DpLucroHasta.SelectedDate = DateTime.Today;
        await LoadLucroReportAsync();
    }

    private async void BtnRunLucroReport_Click(object sender, RoutedEventArgs e) => await LoadLucroReportAsync();

    private async Task LoadLucroReportAsync()
    {
        try
        {
            DateTime from = DpLucroDesde.SelectedDate ?? DateTime.Today.AddDays(-30);
            DateTime to = DpLucroHasta.SelectedDate ?? DateTime.Today;
            string? ruc = ChkLucroTodosClientes.IsChecked == true ? null : TxtLucroRuc.Text.Trim();

            var items = await _salesRepository.GetInformeLucroAsync(from, to, ruc);

            // If Simple mode is selected, show Grupo 1 (Ventas) in grid or all if Expandido, while always computing full summary
            bool showEgresosInGrid = RbLucroExpandido.IsChecked == true;
            _currentLucroItems = showEgresosInGrid ? items : items.Where(x => x.Grupo == 1).ToList();

            GridLucroReport.ItemsSource = _currentLucroItems;

            decimal totalVentas = items.Where(x => x.Grupo == 1).Sum(x => x.TotalVenta);
            decimal totalCosto = items.Where(x => x.Grupo == 1).Sum(x => x.TotalCosto);
            decimal beneficioBruto = totalVentas - totalCosto;
            decimal totalEgresos = items.Where(x => x.Grupo == 2).Sum(x => x.Egresos);
            decimal lucroNetoReal = beneficioBruto - totalEgresos;
            decimal pctMargen = totalVentas > 0 ? Math.Round((beneficioBruto / totalVentas) * 100m, 2) : 0m;

            LblLucroTotalVentas.Text = $"{totalVentas:N0} Gs.";
            LblLucroTotalCosto.Text = $"{totalCosto:N0} Gs.";
            LblLucroBeneficioBruto.Text = $"{beneficioBruto:N0} Gs.";
            LblLucroTotalEgresos.Text = $"{totalEgresos:N0} Gs.";
            LblLucroNetoReal.Text = $"{lucroNetoReal:N0} Gs.";
            LblLucroPorcentaje.Text = $"{pctMargen:N2} %";

            LblStatus.Text = $"✔ Informe Costo/Beneficio generado ({items.Count} registros). Lucro Neto: {lucroNetoReal:N0} Gs.";
        }
        catch (Exception ex)
        {
            LblStatus.Text = $"Error al generar Informe Costo/Beneficio: {ex.Message}";
        }
    }

    private void BtnExportLucroReport_Click(object sender, RoutedEventArgs e)
    {
        var sb = new StringBuilder();
        sb.AppendLine("==============================================================================================================");
        sb.AppendLine($"  {_company.Descripcion} - RUC: {_company.Ruc}");
        sb.AppendLine($"  INFORME COSTO BENEFICIO Y EGRESOS ({DpLucroDesde.SelectedDate:dd/MM/yyyy} AL {DpLucroHasta.SelectedDate:dd/MM/yyyy})");
        sb.AppendLine("==============================================================================================================");
        sb.AppendLine($"{"FECHA",-11} {"TIPO",-12} {"CLIENTE / RUBRO",-30} {"VENTA Gs.",14} {"COSTO Gs.",14} {"EGRESO Gs.",13} {"BENEFICIO",14}");
        sb.AppendLine("--------------------------------------------------------------------------------------------------------------");

        foreach (var r in _currentLucroItems)
        {
            string nom = r.Nombre.Length > 29 ? r.Nombre[..29] : r.Nombre;
            sb.AppendLine($"{r.Fecha:dd/MM/yyyy,-11} {r.TipoMovimiento,-12} {nom,-30} {r.TotalVenta,14:N0} {r.TotalCosto,14:N0} {r.Egresos,13:N0} {r.Beneficio,14:N0}");
        }

        sb.AppendLine("==============================================================================================================");
        sb.AppendLine($"TOTAL VENTAS: {LblLucroTotalVentas.Text} | COSTO: {LblLucroTotalCosto.Text} | BENEFICIO BRUTO: {LblLucroBeneficioBruto.Text}");
        sb.AppendLine($"EGRESOS CAJA: {LblLucroTotalEgresos.Text} | LUCRO NETO REAL: {LblLucroNetoReal.Text} | MARGEN: {LblLucroPorcentaje.Text}");
        sb.AppendLine("==============================================================================================================");

        SaveAndOpenReportFile("informe_lucro", sb.ToString());
    }

    // =========================================================================
    // TAB 2: INFORME LUCRO POR PRODUCTOS (informe_lucroproductos.scx)
    // =========================================================================

    private void ChkLucroTodosProductos_Changed(object sender, RoutedEventArgs e)
    {
        if (TxtLucroCodProd == null || BtnBuscarProdLucro == null || LblLucroProdNombre == null)
            return;

        bool todos = ChkLucroTodosProductos.IsChecked == true;
        TxtLucroCodProd.IsEnabled = !todos;
        BtnBuscarProdLucro.IsEnabled = !todos;
        if (todos)
        {
            TxtLucroCodProd.Text = "";
            LblLucroProdNombre.Text = "(TODOS LOS PRODUCTOS)";
        }
        else
        {
            TxtLucroCodProd.Focus();
        }
    }

    private async void TxtLucroCodProd_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F3)
        {
            e.Handled = true;
            OpenProductLookupForLucro();
        }
        else if (e.Key == Key.Enter)
        {
            e.Handled = true;
            string code = TxtLucroCodProd.Text.Trim();
            if (!string.IsNullOrEmpty(code))
            {
                var p = await _productRepository.GetByCodeAsync(code);
                if (p != null)
                {
                    LblLucroProdNombre.Text = p.Descripcion;
                }
                else
                {
                    SystemSounds.Hand.Play();
                    LblLucroProdNombre.Text = "⚠ PRODUCTO NO EXISTE";
                }
            }
            await LoadLucroProductosReportAsync();
        }
    }

    private void BtnBuscarProdLucro_Click(object sender, RoutedEventArgs e) => OpenProductLookupForLucro();

    private void OpenProductLookupForLucro()
    {
        _windowManager.OpenProductsModule(0, async code =>
        {
            ChkLucroTodosProductos.IsChecked = false;
            TxtLucroCodProd.Text = code;
            var prod = await _productRepository.GetByCodeAsync(code);
            LblLucroProdNombre.Text = prod?.Descripcion ?? code;
            await LoadLucroProductosReportAsync();
        });
    }

    private async void BtnLucroProdHoy_Click(object sender, RoutedEventArgs e)
    {
        DpLucroProdDesde.SelectedDate = DateTime.Today;
        DpLucroProdHasta.SelectedDate = DateTime.Today;
        await LoadLucroProductosReportAsync();
    }

    private async void BtnRunLucroProductos_Click(object sender, RoutedEventArgs e) => await LoadLucroProductosReportAsync();

    private async Task LoadLucroProductosReportAsync()
    {
        try
        {
            DateTime from = DpLucroProdDesde.SelectedDate ?? DateTime.Today.AddDays(-30);
            DateTime to = DpLucroProdHasta.SelectedDate ?? DateTime.Today;
            string? codProd = ChkLucroTodosProductos.IsChecked == true ? null : TxtLucroCodProd.Text.Trim();

            _currentLucroProdItems = await _salesRepository.GetInformeLucroProductosAsync(from, to, codProd);
            GridLucroProductosReport.ItemsSource = _currentLucroProdItems;

            decimal totalCant = _currentLucroProdItems.Sum(x => x.Cantidad);
            decimal totalVentas = _currentLucroProdItems.Sum(x => x.TotalVenta);
            decimal totalCosto = _currentLucroProdItems.Sum(x => x.TotalCosto);
            decimal totalBeneficio = totalVentas - totalCosto;
            decimal pctMargen = totalVentas > 0 ? Math.Round((totalBeneficio / totalVentas) * 100m, 2) : 0m;

            LblProdCount.Text = _currentLucroProdItems.Count.ToString("N0");
            LblProdTotalCant.Text = totalCant.ToString("N2");
            LblProdTotalVentas.Text = $"{totalVentas:N0} Gs.";
            LblProdTotalCosto.Text = $"{totalCosto:N0} Gs.";
            LblProdTotalBeneficio.Text = $"{totalBeneficio:N0} Gs.";
            LblProdPorcentaje.Text = $"{pctMargen:N2} %";

            LblStatus.Text = $"✔ Informe Lucro por Productos generado ({_currentLucroProdItems.Count} productos). Beneficio Total: {totalBeneficio:N0} Gs.";
        }
        catch (Exception ex)
        {
            LblStatus.Text = $"Error al generar Informe de Productos: {ex.Message}";
        }
    }

    private void BtnExportLucroProductos_Click(object sender, RoutedEventArgs e)
    {
        var sb = new StringBuilder();
        sb.AppendLine("==============================================================================================================");
        sb.AppendLine($"  {_company.Descripcion} - RUC: {_company.Ruc}");
        sb.AppendLine($"  INFORME DE PRODUCTOS VENDIDOS / LUCRO ({DpLucroProdDesde.SelectedDate:dd/MM/yyyy} AL {DpLucroProdHasta.SelectedDate:dd/MM/yyyy})");
        sb.AppendLine("==============================================================================================================");
        sb.AppendLine($"{"CÓDIGO",-15} {"DESCRIPCIÓN PRODUCTO",-34} {"CANT.",9} {"VENTAS Gs.",14} {"COSTO Gs.",14} {"BENEFICIO",14} {"% MARG",7}");
        sb.AppendLine("--------------------------------------------------------------------------------------------------------------");

        foreach (var p in _currentLucroProdItems)
        {
            string desc = p.DescripcionProducto.Length > 33 ? p.DescripcionProducto[..33] : p.DescripcionProducto;
            sb.AppendLine($"{p.CodigoProducto,-15} {desc,-34} {p.Cantidad,9:N2} {p.TotalVenta,14:N0} {p.TotalCosto,14:N0} {p.Beneficio,14:N0} {p.PorcentajeLucro,6:N1}%");
        }

        sb.AppendLine("==============================================================================================================");
        sb.AppendLine($"PRODUCTOS: {LblProdCount.Text} | UNIDADES: {LblProdTotalCant.Text} | TOTAL VENTAS: {LblProdTotalVentas.Text}");
        sb.AppendLine($"TOTAL COSTO: {LblProdTotalCosto.Text} | BENEFICIO TOTAL: {LblProdTotalBeneficio.Text} | MARGEN: {LblProdPorcentaje.Text}");
        sb.AppendLine("==============================================================================================================");

        SaveAndOpenReportFile("informe_lucroproductos", sb.ToString());
    }

    private void SaveAndOpenReportFile(string reportPrefix, string content)
    {
        try
        {
            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tickets_emitidos");
            Directory.CreateDirectory(dir);
            string filePath = Path.Combine(dir, $"{reportPrefix}_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            File.WriteAllText(filePath, content, Encoding.UTF8);

            Process.Start(new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            });

            LblStatus.Text = $"✔ Informe exportado y abierto: {filePath}";
        }
        catch (Exception ex)
        {
            LblStatus.Text = $"Informe generado pero no se pudo abrir automáticamente: {ex.Message}";
        }
    }

    // =========================================================================
    // TAB 3 & 4: EMPRESA Y USUARIOS
    // =========================================================================

    private async Task LoadCompanyDataAsync()
    {
        try
        {
            _company = await _systemRepository.GetCompanyDataAsync();
            TxtEmpDescripcion.Text = _company.Descripcion;
            TxtEmpRuc.Text = _company.Ruc;
            TxtEmpDireccion.Text = _company.Direccion;
            TxtEmpTelefonos.Text = _company.Telefonos;
            TxtEmpImpreNormal.Text = _company.ImpreNormal;
            TxtEmpImpreLegal.Text = _company.ImpreLegal;
            ChkCobroCaja.IsChecked = _company.CobroCaja == 1;
        }
        catch (Exception ex)
        {
            LblStatus.Text = $"Error al cargar empresa: {ex.Message}";
        }
    }

    private async Task LoadUsersAsync()
    {
        try
        {
            var users = await _systemRepository.GetActiveUsersAsync();
            GridUsers.ItemsSource = users;
        }
        catch (Exception ex)
        {
            LblStatus.Text = $"Error al cargar usuarios: {ex.Message}";
        }
    }

    private async void BtnSaveCompany_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _company.Descripcion = TxtEmpDescripcion.Text.Trim();
            _company.Ruc = TxtEmpRuc.Text.Trim();
            _company.Direccion = TxtEmpDireccion.Text.Trim();
            _company.Telefonos = TxtEmpTelefonos.Text.Trim();
            _company.ImpreNormal = TxtEmpImpreNormal.Text.Trim();
            _company.ImpreLegal = TxtEmpImpreLegal.Text.Trim();
            _company.CobroCaja = ChkCobroCaja.IsChecked == true ? 1 : 0;

            await _systemRepository.SaveCompanyDataAsync(_company);
            LblStatus.Text = "✔ Parámetros de la empresa guardados correctamente.";
        }
        catch (Exception ex)
        {
            LblStatus.Text = $"Error al guardar empresa: {ex.Message}";
        }
    }

    private void GridUsers_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GridUsers.SelectedItem is UserEntity u)
        {
            _selectedUser = u;
            TxtUsrCodigo.Text = u.Cidusuario;
            TxtUsrNombre.Text = u.Cdescribe;
            TxtUsrGrupo.Text = u.Cidgrupo;
            TxtUsrNewPass.Password = "";
        }
    }

    private async void BtnSaveUser_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var user = _selectedUser ?? new UserEntity();
            string lcTipo = user.Nlineno > 0 ? "M" : "A";
            user.Cidusuario = TxtUsrCodigo.Text.Trim();
            user.Cdescribe = TxtUsrNombre.Text.Trim();
            user.Cidgrupo = TxtUsrGrupo.Text.Trim();

            if (!string.IsNullOrWhiteSpace(TxtUsrNewPass.Password))
            {
                user.Cpassword = VfpCompat.Sys2007(TxtUsrNewPass.Password.Trim());
            }
            else if (string.IsNullOrWhiteSpace(user.Cpassword))
            {
                user.Cpassword = VfpCompat.Sys2007("123456");
            }

            await _systemRepository.SaveUserAsync(user, lcTipo);
            LblStatus.Text = $"✔ Usuario '{user.Cidusuario}' actualizado (Hash SYS(2007): {user.Cpassword}).";
            await LoadUsersAsync();
        }
        catch (Exception ex)
        {
            LblStatus.Text = $"Error al guardar usuario: {ex.Message}";
        }
    }
}
