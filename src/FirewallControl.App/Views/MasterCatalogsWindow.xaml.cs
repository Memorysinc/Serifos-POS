using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using FirewallControl.Core.Business;
using FirewallControl.Core.Entities;
using FirewallControl.Core.Interfaces;

namespace FirewallControl.App.Views;

/// <summary>
/// Unifies abm_monedas, abm_ciudades, abm_barrios, abm_sucursal, and timbrado_local.
/// </summary>
public partial class MasterCatalogsWindow : Window
{
    private readonly IStage3Repository _stage3Repository;
    private readonly ICashRepository _cashRepository;
    private readonly ISystemRepository _systemRepository;

    private CurrencyEntity _editingMoneda = new();
    private CityEntity _editingCiudad = new();
    private BarrioEntity _editingBarrio = new();
    private BranchEntity _editingSucursal = new();
    private TimbradoLocalEntity _editingTimbrado = new();

    public MasterCatalogsWindow(
        IStage3Repository stage3Repository,
        ICashRepository cashRepository,
        ISystemRepository systemRepository)
    {
        InitializeComponent();
        _stage3Repository = stage3Repository;
        _cashRepository = cashRepository;
        _systemRepository = systemRepository;
        DpTimInicio.SelectedDate = DateTime.Today;
        DpTimVenc.SelectedDate = DateTime.Today.AddYears(1);
        Loaded += async (_, _) => await LoadAllAsync();
    }

    public void Configure(int initialTab)
    {
        MainTabs.SelectedIndex = Math.Clamp(initialTab, 0, 3);
    }

    private async Task LoadAllAsync()
    {
        try
        {
            var monedas = await _cashRepository.GetCurrenciesAsync();
            GridMonedas.ItemsSource = monedas;

            var ciudades = await _stage3Repository.GetCitiesAsync();
            GridCiudades.ItemsSource = ciudades;
            CmbBarrioCiudad.ItemsSource = ciudades;
            if (ciudades.Count > 0 && CmbBarrioCiudad.SelectedValue == null)
                CmbBarrioCiudad.SelectedIndex = 0;

            GridBarrios.ItemsSource = await _stage3Repository.GetBarriosAsync();

            var sucursales = await _systemRepository.GetBranchesAsync();
            GridSucursales.ItemsSource = sucursales;
            CmbTimSucursal.ItemsSource = sucursales;
            if (sucursales.Count > 0 && CmbTimSucursal.SelectedValue == null)
                CmbTimSucursal.SelectedIndex = 0;

            var cajas = await _cashRepository.GetCashRegistersAsync();
            CmbTimCaja.ItemsSource = cajas;
            if (cajas.Count > 0 && CmbTimCaja.SelectedValue == null)
                CmbTimCaja.SelectedIndex = 0;

            GridTimbrados.ItemsSource = await _systemRepository.GetTimbradosLocalesAsync();
            LblStatus.Text = "Catálogos maestros actualizados.";
        }
        catch (Exception ex)
        {
            GridMonedas.ItemsSource = LatamCurrencyCatalog.ToCurrencyEntities();
            LblStatus.Text = $"Modo fábrica / sin MySQL ({ex.Message}). Mostrando {LatamCurrencyCatalog.All.Count} monedas LATAM predefinidas.";
        }
    }

    private static decimal ParseDec(string text, decimal fallback = 0m) =>
        decimal.TryParse(text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal d) ? d : fallback;

    private static int ParseInt(string text, int fallback = 0) =>
        int.TryParse(text, out int n) ? n : fallback;

    // --- MONEDAS ---
    private void GridMonedas_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GridMonedas.SelectedItem is not CurrencyEntity m) return;
        _editingMoneda = m;
        TxtMonedaDesc.Text = m.Descripcion;
        TxtMonedaCompra.Text = m.Compra.ToString("0.##", CultureInfo.InvariantCulture);
        TxtMonedaVenta.Text = m.Venta.ToString("0.##", CultureInfo.InvariantCulture);
        TxtMonedaDecimales.Text = m.Decimales.ToString();
        TxtMonedaTipo.Text = m.Tipo.ToString();
        CmbMonedaOp.SelectedIndex = m.Operacion == "/" ? 1 : 0;
    }

    private void BtnNuevoMoneda_Click(object sender, RoutedEventArgs e)
    {
        _editingMoneda = new CurrencyEntity();
        GridMonedas.SelectedItem = null;
        TxtMonedaDesc.Clear();
        TxtMonedaCompra.Text = "1";
        TxtMonedaVenta.Text = "1";
        TxtMonedaDecimales.Text = "2";
        TxtMonedaTipo.Text = "0";
        CmbMonedaOp.SelectedIndex = 0;
        TxtMonedaDesc.Focus();
    }

    private async void BtnGuardarMoneda_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtMonedaDesc.Text))
        {
            MessageBox.Show("Ingrese la descripción de la moneda.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        _editingMoneda.Descripcion = TxtMonedaDesc.Text.Trim();
        _editingMoneda.Compra = ParseDec(TxtMonedaCompra.Text, 1m);
        _editingMoneda.Venta = ParseDec(TxtMonedaVenta.Text, 1m);
        _editingMoneda.Decimales = ParseInt(TxtMonedaDecimales.Text, 2);
        _editingMoneda.Tipo = ParseInt(TxtMonedaTipo.Text, 0);
        _editingMoneda.Operacion = CmbMonedaOp.SelectedIndex == 1 ? "/" : "*";

        string lcTipo = _editingMoneda.IdMoneda > 0 ? "M" : "A";
        await _stage3Repository.SaveCurrencyAsync(_editingMoneda, lcTipo);
        await LoadAllAsync();
        LblStatus.Text = $"Moneda '{_editingMoneda.Descripcion}' guardada.";
    }

    private async void BtnEliminarMoneda_Click(object sender, RoutedEventArgs e)
    {
        if (_editingMoneda.IdMoneda <= 1)
        {
            MessageBox.Show("Seleccione una moneda distinta de la moneda base (ID > 1).", "Monedas", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (MessageBox.Show($"¿Eliminar moneda '{_editingMoneda.Descripcion}'?", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        await _stage3Repository.DeleteCurrencyAsync(_editingMoneda.IdMoneda);
        BtnNuevoMoneda_Click(sender, e);
        await LoadAllAsync();
    }

    private async void BtnSeedLatamCurrencies_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var existing = await _cashRepository.GetCurrenciesAsync();
            var existingNames = new HashSet<string>(
                existing.Select(m => m.Descripcion.Trim()),
                StringComparer.OrdinalIgnoreCase);

            int added = 0;
            foreach (var cur in LatamCurrencyCatalog.ToCurrencyEntities())
            {
                if (existingNames.Any(n => n.StartsWith(cur.Descripcion[..3], StringComparison.OrdinalIgnoreCase) ||
                                           n.Contains(cur.Descripcion.Split('-')[1].Split('(')[0].Trim(), StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }
                cur.IdMoneda = 0;
                await _stage3Repository.SaveCurrencyAsync(cur, "A");
                added++;
            }

            await LoadAllAsync();
            LblStatus.Text = added > 0
                ? $"✔ Se agregaron {added} monedas latinoamericanas al catálogo."
                : "✔ Todas las monedas latinoamericanas ya están registradas.";
        }
        catch (Exception ex)
        {
            GridMonedas.ItemsSource = LatamCurrencyCatalog.ToCurrencyEntities();
            LblStatus.Text = $"Vista previa de {LatamCurrencyCatalog.All.Count} monedas LATAM cargada (sin conexión MySQL: {ex.Message}).";
        }
    }

    // --- CIUDADES & BARRIOS ---
    private void GridCiudades_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GridCiudades.SelectedItem is not CityEntity c) return;
        _editingCiudad = c;
        TxtCiudadNombre.Text = c.Ciudad;
        TxtCiudadDepto.Text = c.Departamento;
        CmbBarrioCiudad.SelectedValue = c.IdCiudad;
    }

    private void BtnNuevoCiudad_Click(object sender, RoutedEventArgs e)
    {
        _editingCiudad = new CityEntity();
        GridCiudades.SelectedItem = null;
        TxtCiudadNombre.Clear();
        TxtCiudadDepto.Clear();
        TxtCiudadNombre.Focus();
    }

    private async void BtnGuardarCiudad_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtCiudadNombre.Text)) return;
        _editingCiudad.Ciudad = TxtCiudadNombre.Text.Trim();
        _editingCiudad.Departamento = TxtCiudadDepto.Text.Trim();
        await _stage3Repository.SaveCityAsync(_editingCiudad, _editingCiudad.IdCiudad > 0 ? "M" : "A");
        await LoadAllAsync();
    }

    private async void BtnEliminarCiudad_Click(object sender, RoutedEventArgs e)
    {
        if (_editingCiudad.IdCiudad <= 0) return;
        if (MessageBox.Show($"¿Eliminar ciudad '{_editingCiudad.Ciudad}'?", "Confirmar", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        await _stage3Repository.DeleteCityAsync(_editingCiudad.IdCiudad);
        BtnNuevoCiudad_Click(sender, e);
        await LoadAllAsync();
    }

    private void GridBarrios_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GridBarrios.SelectedItem is not BarrioEntity b) return;
        _editingBarrio = b;
        TxtBarrioNombre.Text = b.Barrio;
        CmbBarrioCiudad.SelectedValue = b.IdCiudad;
    }

    private void BtnNuevoBarrio_Click(object sender, RoutedEventArgs e)
    {
        _editingBarrio = new BarrioEntity();
        GridBarrios.SelectedItem = null;
        TxtBarrioNombre.Clear();
        TxtBarrioNombre.Focus();
    }

    private async void BtnGuardarBarrio_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtBarrioNombre.Text)) return;
        _editingBarrio.Barrio = TxtBarrioNombre.Text.Trim();
        _editingBarrio.IdCiudad = CmbBarrioCiudad.SelectedValue is int idC ? idC : 1;
        await _stage3Repository.SaveBarrioAsync(_editingBarrio, _editingBarrio.IdBarrio > 0 ? "M" : "A");
        await LoadAllAsync();
    }

    private async void BtnEliminarBarrio_Click(object sender, RoutedEventArgs e)
    {
        if (_editingBarrio.IdBarrio <= 0) return;
        if (MessageBox.Show($"¿Eliminar barrio '{_editingBarrio.Barrio}'?", "Confirmar", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        await _stage3Repository.DeleteBarrioAsync(_editingBarrio.IdBarrio);
        BtnNuevoBarrio_Click(sender, e);
        await LoadAllAsync();
    }

    // --- SUCURSALES ---
    private void GridSucursales_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GridSucursales.SelectedItem is not BranchEntity s) return;
        _editingSucursal = s;
        TxtSucNombre.Text = s.Nombre;
        TxtSucDir.Text = s.Dir;
        TxtSucTel.Text = s.Tel;
        TxtSucCel.Text = s.Cel;
        TxtSucEnc.Text = s.Enc;
        TxtSucEmail.Text = s.Email;
    }

    private void BtnNuevoSucursal_Click(object sender, RoutedEventArgs e)
    {
        _editingSucursal = new BranchEntity();
        GridSucursales.SelectedItem = null;
        TxtSucNombre.Clear();
        TxtSucDir.Clear();
        TxtSucTel.Clear();
        TxtSucCel.Clear();
        TxtSucEnc.Clear();
        TxtSucEmail.Clear();
        TxtSucNombre.Focus();
    }

    private async void BtnGuardarSucursal_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtSucNombre.Text)) return;
        _editingSucursal.Nombre = TxtSucNombre.Text.Trim();
        _editingSucursal.Dir = TxtSucDir.Text.Trim();
        _editingSucursal.Tel = TxtSucTel.Text.Trim();
        _editingSucursal.Cel = TxtSucCel.Text.Trim();
        _editingSucursal.Enc = TxtSucEnc.Text.Trim();
        _editingSucursal.Email = TxtSucEmail.Text.Trim();
        await _stage3Repository.SaveBranchAsync(_editingSucursal, _editingSucursal.IdSucursal > 0 ? "M" : "A");
        await LoadAllAsync();
    }

    // --- TIMBRADOS LOCALES ---
    private void GridTimbrados_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GridTimbrados.SelectedItem is not TimbradoLocalEntity t) return;
        _editingTimbrado = t;
        CmbTimSucursal.SelectedValue = t.IdSucursal;
        CmbTimCaja.SelectedValue = t.IdCaja;
        TxtTimNro.Text = t.Timbrado;
        TxtTimN1.Text = t.N1.ToString();
        TxtTimN2.Text = t.N2.ToString();
        TxtTimN3.Text = t.N3.ToString();
        TxtTimCantidad.Text = t.Cantidad.ToString();
        DpTimInicio.SelectedDate = t.FechaInicio;
        DpTimVenc.SelectedDate = t.Vencimiento;
    }

    private void BtnNuevoTimbrado_Click(object sender, RoutedEventArgs e)
    {
        _editingTimbrado = new TimbradoLocalEntity();
        GridTimbrados.SelectedItem = null;
        TxtTimNro.Clear();
        TxtTimN1.Text = "1";
        TxtTimN2.Text = "1";
        TxtTimN3.Text = "1";
        TxtTimCantidad.Text = "9999999";
        DpTimInicio.SelectedDate = DateTime.Today;
        DpTimVenc.SelectedDate = DateTime.Today.AddYears(1);
    }

    private async void BtnGuardarTimbrado_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtTimNro.Text))
        {
            MessageBox.Show("Ingrese el número de timbrado.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        _editingTimbrado.IdSucursal = CmbTimSucursal.SelectedValue is int s ? s : 1;
        _editingTimbrado.IdCaja = CmbTimCaja.SelectedValue is int c ? c : 1;
        _editingTimbrado.Timbrado = TxtTimNro.Text.Trim();
        _editingTimbrado.N1 = ParseInt(TxtTimN1.Text, 1);
        _editingTimbrado.N2 = ParseInt(TxtTimN2.Text, 1);
        _editingTimbrado.N3 = ParseInt(TxtTimN3.Text, 1);
        _editingTimbrado.Cantidad = ParseInt(TxtTimCantidad.Text, 9999999);
        _editingTimbrado.FechaInicio = DpTimInicio.SelectedDate ?? DateTime.Today;
        _editingTimbrado.Vencimiento = DpTimVenc.SelectedDate ?? DateTime.Today.AddYears(1);
        _editingTimbrado.Factura = _editingTimbrado.FormatNextInvoice();

        await _systemRepository.SaveTimbradoLocalAsync(_editingTimbrado);
        await LoadAllAsync();
        LblStatus.Text = $"Timbrado guardado. Próxima factura: {_editingTimbrado.Factura}";
    }

    private async void BtnReloadAll_Click(object sender, RoutedEventArgs e) => await LoadAllAsync();
}
