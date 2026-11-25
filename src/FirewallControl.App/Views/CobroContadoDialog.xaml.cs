using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FirewallControl.Core.Entities;

namespace FirewallControl.App.Views;

/// <summary>
/// Replicates abm_cobroclientesmontos_contado.scx for multi-currency cash payment and change calculation.
/// </summary>
public partial class CobroContadoDialog : Window
{
    private readonly SaleEntity _sale;
    private readonly ExchangeRateEntity _rate;

    public CobroContadoDialog(SaleEntity sale, ExchangeRateEntity rate)
    {
        InitializeComponent();
        _sale = sale;
        _rate = rate;

        LblTotalVenta.Text = _sale.TotalVenta.ToString("N0", CultureInfo.InvariantCulture);
        TxtEntregaGuarani.Text = _sale.TotalVenta.ToString("0", CultureInfo.InvariantCulture);
        Loaded += (_, _) =>
        {
            TxtEntregaGuarani.Focus();
            TxtEntregaGuarani.SelectAll();
        };
    }

    private void OnAmountsChanged(object sender, TextChangedEventArgs e)
    {
        if (_sale == null) return;
        RecalculateChange();
    }

    private void RecalculateChange()
    {
        decimal gs = ParseDec(TxtEntregaGuarani.Text);
        decimal usd = ParseDec(TxtEntregaDolar.Text);
        decimal brl = ParseDec(TxtEntregaReal.Text);
        decimal ars = ParseDec(TxtEntregaPeso.Text);

        decimal cotUsd = _rate.CompraDolar > 0 ? _rate.CompraDolar : (_rate.VentaDolar > 0 ? _rate.VentaDolar : 7300m);
        decimal cotBrl = _rate.CompraReal > 0 ? _rate.CompraReal : (_rate.VentaReal > 0 ? _rate.VentaReal : 1450m);
        decimal cotArs = _rate.CompraPeso > 0 ? _rate.CompraPeso : (_rate.VentaPeso > 0 ? _rate.VentaPeso : 7m);

        decimal totalReceivedGs = gs + (usd * cotUsd) + (brl * cotBrl) + (ars * cotArs);
        decimal vueltoGs = Math.Max(0m, totalReceivedGs - _sale.TotalVenta);

        _sale.EntregaGuarani = gs;
        _sale.EntregaDolar = usd;
        _sale.EntregaReal = brl;
        _sale.EntregaPeso = ars;
        _sale.VueltoGuarani = Math.Round(vueltoGs, 0);

        if (LblVueltoGuarani != null)
            LblVueltoGuarani.Text = _sale.VueltoGuarani.ToString("N0", CultureInfo.InvariantCulture);
    }

    private void TxtEntrega_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter || e.Key == Key.Return)
        {
            e.Handled = true;
            ConfirmAndClose();
        }
    }

    private void BtnConfirmar_Click(object sender, RoutedEventArgs e) => ConfirmAndClose();

    private void ConfirmAndClose()
    {
        RecalculateChange();
        DialogResult = true;
        Close();
    }

    private static decimal ParseDec(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0m;
        if (decimal.TryParse(s.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal d))
            return d;
        return 0m;
    }
}
