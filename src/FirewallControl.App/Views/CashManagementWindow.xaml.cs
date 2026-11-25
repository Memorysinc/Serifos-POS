using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using FirewallControl.Core.Entities;
using FirewallControl.Core.Interfaces;

namespace FirewallControl.App.Views;

/// <summary>
/// Replicates crear_movimientocaja.scx and abm_cotizaciones.scx.
/// </summary>
public partial class CashManagementWindow : Window
{
    private readonly ICashRepository _cashRepository;
    private readonly IStage3Repository? _stage3Repository;
    private CashRegisterEntity _editingCaja = new() { IdSucursal = 1, DesSucursal = "CASA CENTRAL" };
    private CashRubroEntity _editingRubro = new() { NroRubro = 1, TipoCuenta = 1, Proceso = 1 };

    public CashManagementWindow(ICashRepository cashRepository, IStage3Repository? stage3Repository = null)
    {
        InitializeComponent();
        _cashRepository = cashRepository;
        _stage3Repository = stage3Repository;

        DpDesde.SelectedDate = DateTime.Today.AddDays(-7);
        DpHasta.SelectedDate = DateTime.Today;

        Loaded += async (_, _) =>
        {
            await LoadCajasAndRubrosAsync();
            await LoadMovementsAsync();
            await LoadExchangeRatesAsync();
        };
    }

    public void Configure(int initialTab)
    {
        MainTabs.SelectedIndex = Math.Clamp(initialTab, 0, 3);
    }

    private async Task LoadCajasAndRubrosAsync()
    {
        try
        {
            var cajas = await _cashRepository.GetCashRegistersAsync();
            GridCajas.ItemsSource = cajas;
            CmbCajaMov.ItemsSource = cajas;
            if (cajas.Count > 0 && CmbCajaMov.SelectedValue == null)
                CmbCajaMov.SelectedIndex = 0;

            if (_stage3Repository != null)
            {
                var rubros = await _stage3Repository.GetCashRubrosAsync();
                GridRubros.ItemsSource = rubros;
                CmbRubroMov.ItemsSource = rubros;
                if (rubros.Count > 0 && CmbRubroMov.SelectedValue == null)
                    CmbRubroMov.SelectedIndex = 0;
            }
        }
        catch { }
    }

    private async Task LoadMovementsAsync()
    {
        try
        {
            DateTime desde = DpDesde.SelectedDate ?? DateTime.Today;
            DateTime hasta = DpHasta.SelectedDate ?? DateTime.Today;
            int? idCaja = null;
            if (CmbCajaFilter.SelectedItem is ComboBoxItem item && int.TryParse(item.Tag?.ToString(), out int c) && c > 0)
                idCaja = c;

            var list = await _cashRepository.GetMovementsAsync(desde, hasta, idCaja);
            GridMovements.ItemsSource = list;

            decimal totIn = list.Sum(x => x.Ingresos);
            decimal totOut = list.Sum(x => x.Egresos);
            LblTotalIngresos.Text = $"{totIn:N0} Gs.";
            LblTotalEgresos.Text = $"{totOut:N0} Gs.";
            LblSaldoNeto.Text = $"{(totIn - totOut):N0} Gs.";
            LblStatus.Text = $"{list.Count} movimientos de caja consultados.";
        }
        catch (Exception ex)
        {
            LblStatus.Text = $"Error al consultar caja: {ex.Message}";
        }
    }

    private async Task LoadExchangeRatesAsync()
    {
        try
        {
            var rate = await _cashRepository.GetLatestExchangeRateAsync();
            TxtCompraDolar.Text = rate.CompraDolar.ToString("0.##", CultureInfo.InvariantCulture);
            TxtVentaDolar.Text = rate.VentaDolar.ToString("0.##", CultureInfo.InvariantCulture);
            TxtCompraReal.Text = rate.CompraReal.ToString("0.##", CultureInfo.InvariantCulture);
            TxtVentaReal.Text = rate.VentaReal.ToString("0.##", CultureInfo.InvariantCulture);
            TxtCompraPeso.Text = rate.CompraPeso.ToString("0.##", CultureInfo.InvariantCulture);
            TxtVentaPeso.Text = rate.VentaPeso.ToString("0.##", CultureInfo.InvariantCulture);
            TxtCompraEuro.Text = rate.CompraEuro.ToString("0.##", CultureInfo.InvariantCulture);
            TxtVentaEuro.Text = rate.VentaEuro.ToString("0.##", CultureInfo.InvariantCulture);
        }
        catch (Exception ex)
        {
            LblStatus.Text = $"Error al cargar cotizaciones: {ex.Message}";
        }
    }

    private async void BtnFilterMovements_Click(object sender, RoutedEventArgs e) => await LoadMovementsAsync();

    private async void BtnRegisterMovement_Click(object sender, RoutedEventArgs e)
    {
        if (!decimal.TryParse(TxtMontoMov.Text.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal monto) || monto <= 0m)
        {
            LblStatus.Text = "Ingrese un monto válido mayor a cero.";
            return;
        }

        string tipo = (CmbTipoMov.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "ENTRADA";
        bool isEntrada = tipo == "ENTRADA";
        int nroRubro = CmbRubroMov.SelectedValue is int r && r > 0 ? r : 1;
        int idCaja = CmbCajaMov.SelectedValue is int cj && cj > 0 ? cj : 1;

        var mov = new CashMovementEntity
        {
            NroRubro = nroRubro,
            Tipo = tipo,
            Ingresos = isEntrada ? monto : 0m,
            Egresos = isEntrada ? 0m : monto,
            Detalle = string.IsNullOrWhiteSpace(TxtDetalleMov.Text) ? $"MOVIMIENTO MANUAL ({tipo})" : TxtDetalleMov.Text.Trim(),
            FechaAlta = DateTime.Today,
            Hora = DateTime.Now.ToString("HH:mm:ss"),
            Estado = 1,
            IdCaja = idCaja,
            IdMoneda = 1,
            CidUsuario = "ADMINISTRA"
        };

        try
        {
            int id = await _cashRepository.RegisterMovementAsync(mov);
            TxtMontoMov.Text = "";
            TxtDetalleMov.Text = "";
            LblStatus.Text = $"✔ Movimiento #{id} ({tipo}: {monto:N0} Gs.) registrado.";
            await LoadMovementsAsync();
        }
        catch (Exception ex)
        {
            LblStatus.Text = $"Error al registrar movimiento: {ex.Message}";
        }
    }

    private async void BtnSaveExchangeRate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var rate = new ExchangeRateEntity
            {
                Fecha = DateTime.Today,
                Hora = DateTime.Now.ToString("HH:mm:ss"),
                CompraDolar = ParseDec(TxtCompraDolar.Text),
                VentaDolar = ParseDec(TxtVentaDolar.Text),
                CompraReal = ParseDec(TxtCompraReal.Text),
                VentaReal = ParseDec(TxtVentaReal.Text),
                CompraPeso = ParseDec(TxtCompraPeso.Text),
                VentaPeso = ParseDec(TxtVentaPeso.Text),
                CompraEuro = ParseDec(TxtCompraEuro.Text),
                VentaEuro = ParseDec(TxtVentaEuro.Text),
                CompraGuarani = 1m,
                VentaGuarani = 1m,
                IdSucursal = 1,
                DesSucursal = "CASA CENTRAL"
            };

            await _cashRepository.SaveExchangeRateAsync(rate);
            LblStatus.Text = "✔ Cotizaciones actualizadas correctamente.";
        }
        catch (Exception ex)
        {
            LblStatus.Text = $"Error al guardar cotización: {ex.Message}";
        }
    }

    private static decimal ParseDec(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0m;
        return decimal.TryParse(s.Trim().Replace(" ", "").Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal d) ? d : 0m;
    }

    // =========================================================================
    // TAB 2: CAJAS CRUD & TAB 3: RUBROS CRUD
    // =========================================================================
    private void GridCajas_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GridCajas.SelectedItem is not CashRegisterEntity c) return;
        _editingCaja = c;
        TxtCajaDesc.Text = c.Descripcion;
        TxtCajaSaldo.Text = c.Saldo.ToString("0.##", CultureInfo.InvariantCulture);
        TxtCajaSucId.Text = c.IdSucursal.ToString(CultureInfo.InvariantCulture);
        TxtCajaDesSuc.Text = c.DesSucursal;
        TxtCajaObs.Text = c.Obs;
    }

    private void BtnCajaNew_Click(object sender, RoutedEventArgs e)
    {
        _editingCaja = new CashRegisterEntity { IdSucursal = 1, DesSucursal = "CASA CENTRAL" };
        TxtCajaDesc.Clear();
        TxtCajaSaldo.Text = "0";
        TxtCajaSucId.Text = "1";
        TxtCajaDesSuc.Text = "CASA CENTRAL";
        TxtCajaObs.Clear();
        TxtCajaDesc.Focus();
    }

    private async void BtnCajaSave_Click(object sender, RoutedEventArgs e)
    {
        if (_stage3Repository == null) return;
        if (string.IsNullOrWhiteSpace(TxtCajaDesc.Text))
        {
            MessageBox.Show("Ingrese la descripción de la caja.", "Cajas", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        try
        {
            _editingCaja.Descripcion = TxtCajaDesc.Text.Trim();
            _editingCaja.Saldo = ParseDec(TxtCajaSaldo.Text);
            _editingCaja.IdSucursal = int.TryParse(TxtCajaSucId.Text, out int s) && s > 0 ? s : 1;
            _editingCaja.DesSucursal = TxtCajaDesSuc.Text.Trim();
            _editingCaja.Obs = TxtCajaObs.Text ?? string.Empty;
            await _stage3Repository.SaveCashRegisterAsync(_editingCaja, _editingCaja.IdCaja > 0 ? "M" : "A");
            await LoadCajasAndRubrosAsync();
            LblStatus.Text = $"✓ Caja '{_editingCaja.Descripcion}' guardada.";
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Cajas", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private async void BtnCajaDelete_Click(object sender, RoutedEventArgs e)
    {
        if (_stage3Repository == null || _editingCaja.IdCaja <= 0) return;
        if (MessageBox.Show($"¿Eliminar caja '{_editingCaja.Descripcion}'?", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            await _stage3Repository.DeleteCashRegisterAsync(_editingCaja.IdCaja);
            BtnCajaNew_Click(sender, e);
            await LoadCajasAndRubrosAsync();
        }
    }

    private void GridRubros_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GridRubros.SelectedItem is not CashRubroEntity r) return;
        _editingRubro = r;
        TxtRubroNro.Text = r.NroRubro.ToString(CultureInfo.InvariantCulture);
        TxtRubroNombre.Text = r.NombreRubro;
        TxtRubroTipoCta.Text = r.TipoCuenta.ToString(CultureInfo.InvariantCulture);
        TxtRubroProceso.Text = r.Proceso.ToString(CultureInfo.InvariantCulture);
    }

    private void BtnRubroNew_Click(object sender, RoutedEventArgs e)
    {
        _editingRubro = new CashRubroEntity { NroRubro = 1, TipoCuenta = 1, Proceso = 1 };
        TxtRubroNro.Text = "1";
        TxtRubroNombre.Clear();
        TxtRubroTipoCta.Text = "1";
        TxtRubroProceso.Text = "1";
        TxtRubroNombre.Focus();
    }

    private async void BtnRubroSave_Click(object sender, RoutedEventArgs e)
    {
        if (_stage3Repository == null) return;
        if (string.IsNullOrWhiteSpace(TxtRubroNombre.Text))
        {
            MessageBox.Show("Ingrese el nombre del rubro.", "Rubros", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        try
        {
            _editingRubro.NroRubro = int.TryParse(TxtRubroNro.Text, out int nr) ? nr : 1;
            _editingRubro.NombreRubro = TxtRubroNombre.Text.Trim();
            _editingRubro.TipoCuenta = int.TryParse(TxtRubroTipoCta.Text, out int tc) ? tc : 1;
            _editingRubro.Proceso = int.TryParse(TxtRubroProceso.Text, out int pr) ? pr : 1;
            await _stage3Repository.SaveCashRubroAsync(_editingRubro, _editingRubro.IdRubros > 0 ? "M" : "A");
            await LoadCajasAndRubrosAsync();
            LblStatus.Text = $"✓ Rubro '{_editingRubro.NombreRubro}' guardado.";
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Rubros", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private async void BtnRubroDelete_Click(object sender, RoutedEventArgs e)
    {
        if (_stage3Repository == null || _editingRubro.IdRubros <= 0) return;
        if (MessageBox.Show($"¿Eliminar rubro '{_editingRubro.NombreRubro}'?", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            await _stage3Repository.DeleteCashRubroAsync(_editingRubro.IdRubros);
            BtnRubroNew_Click(sender, e);
            await LoadCajasAndRubrosAsync();
        }
    }
}
