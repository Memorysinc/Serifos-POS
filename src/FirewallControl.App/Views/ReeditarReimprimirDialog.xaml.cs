using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FirewallControl.Core.Entities;
using FirewallControl.Core.Interfaces;

namespace FirewallControl.App.Views;

/// <summary>
/// Replicates reeditar_ventas.scx (Ctrl+X) and reimprimir_ventas.scx (Ctrl+Z).
/// Automatically positions on the latest sale (index 0) when opened, allows seamless navigation
/// with Up/Down arrow keys from anywhere in the window, and selects with ENTER.
/// </summary>
public partial class ReeditarReimprimirDialog : Window
{
    private readonly ISalesRepository _salesRepository;
    private List<SaleEntity> _sales = new();
    private bool _suppressTextSync;

    public int? SelectedNroVenta { get; private set; }

    public ReeditarReimprimirDialog(ISalesRepository salesRepository, bool isReprint)
    {
        InitializeComponent();
        _salesRepository = salesRepository;

        Title = isReprint ? "REIMPRIMIR VENTA (Ctrl+Z)" : "REEDITAR VENTA (Ctrl+X)";
        LblHeader.Text = isReprint
            ? "🖨 REIMPRIMIR COMPROBANTE DE VENTA (Ctrl+Z)"
            : "🔄 REEDITAR VENTA Y AJUSTAR STOCK (Ctrl+X)";

        Loaded += async (_, _) =>
        {
            try
            {
                _sales = await _salesRepository.GetRecentSalesAsync(DateTime.Today.AddDays(-365), DateTime.Today.AddDays(1), 100);
                GridRecentSales.ItemsSource = _sales;
                if (_sales.Count > 0)
                {
                    GridRecentSales.SelectedIndex = 0;
                    GridRecentSales.ScrollIntoView(_sales[0]);
                    UpdateSelectedSaleDisplay(_sales[0]);
                    GridRecentSales.Focus();
                }
                else
                {
                    TxtNroVenta.Focus();
                }
            }
            catch
            {
                TxtNroVenta.Focus();
            }
        };
    }

    private void GridRecentSales_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressTextSync)
            return;

        if (GridRecentSales.SelectedItem is SaleEntity sale)
        {
            UpdateSelectedSaleDisplay(sale);
        }
    }

    private void UpdateSelectedSaleDisplay(SaleEntity sale)
    {
        _suppressTextSync = true;
        TxtNroVenta.Text = sale.NroVenta.ToString();
        LblSaleSummary.Text = $"Venta #{sale.NroVenta} — {sale.NombreCliente} ({sale.TotalVenta:N0} Gs.)";
        _suppressTextSync = false;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            DialogResult = false;
            Close();
            return;
        }

        if (e.Key == Key.Enter || e.Key == Key.Return)
        {
            e.Handled = true;
            AcceptSelection();
            return;
        }

        if (_sales.Count == 0)
            return;

        if (e.Key == Key.Down)
        {
            e.Handled = true;
            int next = GridRecentSales.SelectedIndex < 0
                ? 0
                : Math.Min(GridRecentSales.SelectedIndex + 1, _sales.Count - 1);
            GridRecentSales.SelectedIndex = next;
            GridRecentSales.ScrollIntoView(_sales[next]);
        }
        else if (e.Key == Key.Up)
        {
            e.Handled = true;
            int prev = GridRecentSales.SelectedIndex <= 0
                ? 0
                : GridRecentSales.SelectedIndex - 1;
            GridRecentSales.SelectedIndex = prev;
            GridRecentSales.ScrollIntoView(_sales[prev]);
        }
        else if (e.Key == Key.PageDown)
        {
            e.Handled = true;
            int next = Math.Min(Math.Max(GridRecentSales.SelectedIndex, 0) + 10, _sales.Count - 1);
            GridRecentSales.SelectedIndex = next;
            GridRecentSales.ScrollIntoView(_sales[next]);
        }
        else if (e.Key == Key.PageUp)
        {
            e.Handled = true;
            int prev = Math.Max(GridRecentSales.SelectedIndex - 10, 0);
            GridRecentSales.SelectedIndex = prev;
            GridRecentSales.ScrollIntoView(_sales[prev]);
        }
    }

    private void GridRecentSales_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (GridRecentSales.SelectedItem is SaleEntity sale)
        {
            SelectedNroVenta = sale.NroVenta;
            DialogResult = true;
            Close();
        }
    }

    private void BtnAceptar_Click(object sender, RoutedEventArgs e) => AcceptSelection();

    private void BtnCancelar_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void AcceptSelection()
    {
        if (int.TryParse(TxtNroVenta.Text.Trim(), out int nro) && nro > 0)
        {
            SelectedNroVenta = nro;
            DialogResult = true;
            Close();
            return;
        }

        if (GridRecentSales.SelectedItem is SaleEntity sale)
        {
            SelectedNroVenta = sale.NroVenta;
            DialogResult = true;
            Close();
        }
    }
}
