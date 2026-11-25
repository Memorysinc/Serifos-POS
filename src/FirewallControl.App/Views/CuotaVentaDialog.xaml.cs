using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FirewallControl.App.Services;
using FirewallControl.Core.Business;
using FirewallControl.Core.Entities;

namespace FirewallControl.App.Views;

/// <summary>
/// Replicates abm_cuotaventa.scx / VtAttachCuotaVenta.
/// </summary>
public partial class CuotaVentaDialog : Window
{
    private readonly SaleEntity _sale;

    public List<SaleInstallmentEntity> GeneratedInstallments { get; private set; } = new();
    public decimal EntregaInicial { get; private set; }

    public CuotaVentaDialog(SaleEntity sale)
    {
        InitializeComponent();
        _sale = sale;
        TxtNroVenta.Text = sale.NroVenta.ToString();
        TxtNroFactura.Text = sale.NroFactura;
        TxtTotalVenta.Text = sale.TotalVenta.ToString("N0");
        LblClienteTag.Text = $"{sale.NombreCliente} (RUC: {sale.RucCliente})";
        GenerateSchedule();
    }

    private void OnRecalcInputChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        UpdateSaldoPreview();
    }

    private void UpdateSaldoPreview()
    {
        decimal entrega = PosNumericParser.TryParseGuaraniAmount(TxtEntrega.Text, out decimal ent) ? ent : 0m;
        decimal saldo = Math.Max(0m, _sale.TotalVenta - entrega);
        TxtSaldo.Text = saldo.ToString("N0");
    }

    private void GenerateSchedule()
    {
        int cuotas = int.TryParse(TxtCuotas.Text, out int c) ? Math.Clamp(c, 1, 60) : 1;
        int dias = int.TryParse(TxtDias.Text, out int d) ? Math.Clamp(d, 1, 365) : 30;
        decimal interes = decimal.TryParse(TxtInteres.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal iPct) ? Math.Max(0m, iPct) : 0m;
        decimal entrega = PosNumericParser.TryParseGuaraniAmount(TxtEntrega.Text, out decimal ent) ? ent : 0m;

        EntregaInicial = Math.Clamp(entrega, 0m, _sale.TotalVenta);
        UpdateSaldoPreview();

        GeneratedInstallments = FastCargaParser.CalculateInstallments(
            _sale.TotalVenta,
            EntregaInicial,
            cuotas,
            dias,
            _sale.FechaVenta == default ? DateTime.Today : _sale.FechaVenta,
            interes);

        GridCuotas.ItemsSource = GeneratedInstallments;
        LblTotalCuotas.Text = $"Total Financiado: {GeneratedInstallments.Sum(x => x.Monto):N0} Gs.";
    }

    private void BtnGenerate_Click(object sender, RoutedEventArgs e) => GenerateSchedule();

    private void BtnAccept_Click(object sender, RoutedEventArgs e)
    {
        GenerateSchedule();
        if (GeneratedInstallments.Count == 0)
        {
            MessageBox.Show(this, "El saldo a financiar debe ser mayor a cero para generar cuotas.", "Cuotas", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        DialogResult = true;
        Close();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F9)
        {
            e.Handled = true;
            BtnAccept_Click(sender, new RoutedEventArgs());
        }
    }
}
