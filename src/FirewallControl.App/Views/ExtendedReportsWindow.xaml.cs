using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using FirewallControl.Core.Entities;
using FirewallControl.Core.Interfaces;
using Microsoft.Win32;

namespace FirewallControl.App.Views;

/// <summary>
/// Unifies informe_ventavendedor, informe_ventaxcliente, informe_ventaxdeposito,
/// informe_productosvendidos, informe_ventaxsupervisor, informe_comprasxproveedor,
/// informe_productoscomprados, and tbl_tranusu audit log.
/// </summary>
public partial class ExtendedReportsWindow : Window
{
    private readonly IStage3Repository _stage3Repository;
    private bool _isLoaded;

    public ExtendedReportsWindow(IStage3Repository stage3Repository)
    {
        InitializeComponent();
        _stage3Repository = stage3Repository;
        DpDesde.SelectedDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        DpHasta.SelectedDate = DateTime.Today;
        Loaded += async (_, _) =>
        {
            _isLoaded = true;
            await LoadActiveReportAsync();
        };
    }

    public void Configure(int initialTab)
    {
        MainTabs.SelectedIndex = Math.Clamp(initialTab, 0, 6);
    }

    private async void MainTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded || e.Source != MainTabs) return;
        await LoadActiveReportAsync();
    }

    private async void BtnConsultar_Click(object sender, RoutedEventArgs e) => await LoadActiveReportAsync();

    private async Task LoadActiveReportAsync()
    {
        try
        {
            DateTime desde = DpDesde.SelectedDate ?? DateTime.Today;
            DateTime hasta = DpHasta.SelectedDate ?? DateTime.Today;
            string filter = (TxtFilter.Text ?? string.Empty).Trim();

            switch (MainTabs.SelectedIndex)
            {
                case 0:
                {
                    var rows = FilterRows(await _stage3Repository.GetSalesBySellerReportAsync(desde, hasta), filter);
                    GridVendedor.ItemsSource = rows;
                    SetSummary(rows.Count, rows.Sum(r => r.Monto1));
                    break;
                }
                case 1:
                {
                    var rows = FilterRows(await _stage3Repository.GetSalesByCustomerReportAsync(desde, hasta), filter);
                    GridCliente.ItemsSource = rows;
                    SetSummary(rows.Count, rows.Sum(r => r.Monto1));
                    break;
                }
                case 2:
                {
                    var rows = FilterRows(await _stage3Repository.GetSalesByDepositReportAsync(desde, hasta), filter);
                    GridDeposito.ItemsSource = rows;
                    SetSummary(rows.Count, rows.Sum(r => r.Monto1));
                    break;
                }
                case 3:
                {
                    var rows = FilterRows(await _stage3Repository.GetSoldProductsReportAsync(desde, hasta), filter);
                    GridProdVendidos.ItemsSource = rows;
                    SetSummary(rows.Count, rows.Sum(r => r.Monto1));
                    break;
                }
                case 4:
                {
                    var rows = FilterRows(await _stage3Repository.GetSalesBySupervisorReportAsync(desde, hasta), filter);
                    GridSupervisor.ItemsSource = rows;
                    SetSummary(rows.Count, rows.Sum(r => r.Monto1));
                    break;
                }
                case 5:
                {
                    var prov = FilterRows(await _stage3Repository.GetPurchasesBySupplierReportAsync(desde, hasta), filter);
                    var prod = FilterRows(await _stage3Repository.GetPurchasedProductsReportAsync(desde, hasta), filter);
                    GridComprasProv.ItemsSource = prov;
                    GridProdComprados.ItemsSource = prod;
                    SetSummary(prov.Count + prod.Count, prov.Sum(r => r.Monto1));
                    break;
                }
                case 6:
                {
                    var logs = await _stage3Repository.GetUserTransactionLogsAsync(desde, hasta, string.IsNullOrWhiteSpace(filter) ? null : filter);
                    GridAudit.ItemsSource = logs;
                    LblStatus.Text = $"{logs.Count} evento(s) de auditoría encontrados.";
                    LblTotalFooter.Text = $"Registros: {logs.Count}";
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            LblStatus.Text = $"Error al generar informe: {ex.Message}";
        }
    }

    private static List<ExtendedReportRow> FilterRows(List<ExtendedReportRow> rows, string filter)
    {
        if (string.IsNullOrWhiteSpace(filter)) return rows;
        return rows.FindAll(r =>
            r.Clave.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
            r.Descripcion.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
            r.Extra1.Contains(filter, StringComparison.OrdinalIgnoreCase));
    }

    private void SetSummary(int count, decimal total)
    {
        LblStatus.Text = $"{count} fila(s) en el informe.";
        LblTotalFooter.Text = $"Total período: {total:N0} Gs.";
    }

    private void BtnExportCsv_Click(object sender, RoutedEventArgs e)
    {
        DataGrid? activeGrid = MainTabs.SelectedIndex switch
        {
            0 => GridVendedor,
            1 => GridCliente,
            2 => GridDeposito,
            3 => GridProdVendidos,
            4 => GridSupervisor,
            5 => GridProdComprados,
            6 => GridAudit,
            _ => null
        };
        if (activeGrid == null) return;

        var dlg = new SaveFileDialog
        {
            Filter = "CSV (*.csv)|*.csv",
            FileName = $"Informe_Etapa3_{DateTime.Now:yyyyMMdd_HHmm}.csv"
        };
        if (dlg.ShowDialog() != true) return;

        var sb = new StringBuilder();
        if (activeGrid == GridAudit && GridAudit.ItemsSource is IEnumerable<UserTransactionLogEntity> logs)
        {
            sb.AppendLine("Id;Fecha;Usuario;Formulario;Tabla;Tipo;Detalle");
            foreach (var l in logs)
                sb.AppendLine($"{l.IdTranUsu};{l.FechaTran:yyyy-MM-dd HH:mm};{Escape(l.UserIdTran)};{Escape(l.FormTran)};{Escape(l.TablaTran)};{Escape(l.TipoTran)};{Escape(l.SqlTran)}");
        }
        else if (activeGrid.ItemsSource is IEnumerable<ExtendedReportRow> rows)
        {
            sb.AppendLine("Clave;Descripcion;Extra;Cantidad;Monto1;Monto2;Monto3");
            foreach (var r in rows)
                sb.AppendLine($"{Escape(r.Clave)};{Escape(r.Descripcion)};{Escape(r.Extra1)};{r.Cantidad};{r.Monto1};{r.Monto2};{r.Monto3}");
        }

        File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true));
        LblStatus.Text = $"Exportado a {dlg.FileName}";
    }

    private static string Escape(string s) => $"\"{(s ?? string.Empty).Replace("\"", "\"\"")}\"";
}
