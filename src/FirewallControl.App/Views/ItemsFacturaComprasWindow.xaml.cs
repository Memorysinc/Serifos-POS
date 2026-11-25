using System;
using System.Globalization;
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
/// Replicates fuentes\forms\itemsfacturacompras.scx (child window of abm_compras.scx).
/// Handles product lookup, purchase quantity, purchase cost, weighted average cost (promedio),
/// two-way calculation of utility % (p1..p7) and sale prices (precio1..precio7), IVA regime,
/// lot/expiration, and warranty.
/// </summary>
public partial class ItemsFacturaComprasWindow : Window
{
    private readonly IProductRepository _productRepository;
    private readonly ISystemRepository _systemRepository;
    private readonly IWindowManagerService _windowManager;
    private readonly int _idDeposito;
    private readonly string _lcTipo;

    private ProductEntity? _currentProduct;
    private CompanyDataEntity _companyData = new();
    private bool _suppressRecalc;

    public PurchaseItemEntity? ResultItem { get; private set; }

    public ItemsFacturaComprasWindow(
        IProductRepository productRepository,
        ISystemRepository systemRepository,
        IWindowManagerService windowManager,
        int idDeposito = 1,
        string lcTipo = "A",
        PurchaseItemEntity? existingItem = null)
    {
        InitializeComponent();
        _productRepository = productRepository;
        _systemRepository = systemRepository;
        _windowManager = windowManager;
        _idDeposito = idDeposito > 0 ? idDeposito : 1;
        _lcTipo = string.IsNullOrWhiteSpace(lcTipo) ? "A" : lcTipo.ToUpperInvariant();

        DpVencimiento.SelectedDate = DateTime.Today.AddMonths(6);
        DpInicioGarantia.SelectedDate = DateTime.Today;

        Loaded += async (_, _) =>
        {
            await LoadCompanyDefaultsAsync();
            if (_lcTipo == "M" && existingItem != null)
            {
                TxtWindowTitle.Text = $"✏ MODIFICAR ÍTEM DE COMPRA #{existingItem.RecNo} (ITEMSFACTURACOMPRAS)";
                await LoadExistingItemAsync(existingItem);
            }
            else
            {
                TxtCodigo.Focus();
                TxtCodigo.SelectAll();
            }
        };
    }

    private async Task LoadCompanyDefaultsAsync()
    {
        try
        {
            _companyData = await _systemRepository.GetCompanyDataAsync();
            _suppressRecalc = true;
            TxtP1.Text = (_companyData.Utilidad1 > 0 ? _companyData.Utilidad1 : 35m).ToString("0.##", CultureInfo.InvariantCulture);
            TxtP2.Text = (_companyData.Utilidad2 > 0 ? _companyData.Utilidad2 : 45m).ToString("0.##", CultureInfo.InvariantCulture);
            TxtP3.Text = (_companyData.Utilidad3 > 0 ? _companyData.Utilidad3 : 60m).ToString("0.##", CultureInfo.InvariantCulture);
            TxtP4.Text = (_companyData.Utilidad4 > 0 ? _companyData.Utilidad4 : 80m).ToString("0.##", CultureInfo.InvariantCulture);
            TxtP5.Text = (_companyData.Utilidad5 > 0 ? _companyData.Utilidad5 : 100m).ToString("0.##", CultureInfo.InvariantCulture);
            TxtP6.Text = (_companyData.Utilidad6 > 0 ? _companyData.Utilidad6 : 120m).ToString("0.##", CultureInfo.InvariantCulture);
            TxtP7.Text = _companyData.Utilidad7.ToString("0.##", CultureInfo.InvariantCulture);
            _suppressRecalc = false;
        }
        catch
        {
            _suppressRecalc = false;
        }
    }

    private async Task LoadExistingItemAsync(PurchaseItemEntity item)
    {
        TxtCodigo.Text = item.Codigo;
        var prod = item.IdProducto > 0
            ? await _productRepository.GetByIdAsync(item.IdProducto, _idDeposito)
            : await _productRepository.GetByCodeAsync(item.Codigo, _idDeposito);

        if (prod != null)
        {
            ApplyProductToFields(prod);
        }
        else
        {
            TxtDescripcion.Text = item.Descripcion;
        }

        _suppressRecalc = true;
        TxtCantidad.Text = item.Cantidad.ToString("0.###", CultureInfo.InvariantCulture);
        TxtCostoCompra.Text = item.PreUnitario.ToString("0", CultureInfo.InvariantCulture);
        TxtCostoPromedio.Text = (item.CostoMedio > 0 ? item.CostoMedio : item.PreUnitario).ToString("0", CultureInfo.InvariantCulture);
        SelectIvaCombo(item.Iva);

        if (item.P1 > 0) TxtP1.Text = item.P1.ToString("0.##", CultureInfo.InvariantCulture);
        if (item.Precio1 > 0) TxtPrecio1.Text = item.Precio1.ToString("0", CultureInfo.InvariantCulture);
        if (item.P2 > 0) TxtP2.Text = item.P2.ToString("0.##", CultureInfo.InvariantCulture);
        if (item.Precio2 > 0) TxtPrecio2.Text = item.Precio2.ToString("0", CultureInfo.InvariantCulture);
        if (item.P3 > 0) TxtP3.Text = item.P3.ToString("0.##", CultureInfo.InvariantCulture);
        if (item.Precio3 > 0) TxtPrecio3.Text = item.Precio3.ToString("0", CultureInfo.InvariantCulture);
        if (item.P4 > 0) TxtP4.Text = item.P4.ToString("0.##", CultureInfo.InvariantCulture);
        if (item.Precio4 > 0) TxtPrecio4.Text = item.Precio4.ToString("0", CultureInfo.InvariantCulture);
        if (item.P5 > 0) TxtP5.Text = item.P5.ToString("0.##", CultureInfo.InvariantCulture);
        if (item.Precio5 > 0) TxtPrecio5.Text = item.Precio5.ToString("0", CultureInfo.InvariantCulture);
        if (item.P6 > 0) TxtP6.Text = item.P6.ToString("0.##", CultureInfo.InvariantCulture);
        if (item.Precio6 > 0) TxtPrecio6.Text = item.Precio6.ToString("0", CultureInfo.InvariantCulture);
        if (item.P7 > 0) TxtP7.Text = item.P7.ToString("0.##", CultureInfo.InvariantCulture);
        if (item.Precio7 > 0) TxtPrecio7.Text = item.Precio7.ToString("0", CultureInfo.InvariantCulture);

        ChkActualizarPrecios.IsChecked = item.ActualizarPreciosVenta;
        ChkUsaVencimiento.IsChecked = item.UsaVencimiento == 1;
        TxtLote.Text = item.Lote;
        if (item.FechaVencimiento.HasValue) DpVencimiento.SelectedDate = item.FechaVencimiento.Value;
        ChkControlaGarantia.IsChecked = item.ControlaGarantia == 1;
        TxtDiasGarantia.Text = item.DiasGarantias.ToString();
        if (item.FechaInicioGarantia.HasValue) DpInicioGarantia.SelectedDate = item.FechaInicioGarantia.Value;
        _suppressRecalc = false;

        UpdateSubtotalPreview();
        TxtCantidad.Focus();
        TxtCantidad.SelectAll();
    }

    private async void TxtCodigo_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            await LookupProductByCodeAsync(TxtCodigo.Text);
        }
    }

    private async Task LookupProductByCodeAsync(string rawCode)
    {
        string code = VfpCompat.TransformaCodigoProducto(rawCode);
        if (string.IsNullOrWhiteSpace(code))
        {
            TxtStatus.Text = "Ingrese un código o presione [F3] para buscar en el catálogo.";
            return;
        }

        TxtCodigo.Text = code;
        var prod = await _productRepository.GetByCodeAsync(code, _idDeposito);
        if (prod == null)
        {
            TxtStatus.Text = $"⚠ El producto '{code}' no existe. Puede crearlo con [F7] Crear Producto.";
            TxtCodigo.SelectAll();
            return;
        }

        ApplyProductToFields(prod);
        TxtCantidad.Focus();
        TxtCantidad.SelectAll();
    }

    private void ApplyProductToFields(ProductEntity prod)
    {
        _currentProduct = prod;
        _suppressRecalc = true;

        TxtCodigo.Text = prod.Codigo;
        TxtDescripcion.Text = prod.Descripcion;
        TxtStockAnterior.Text = prod.StockActual.ToString("N2", CultureInfo.InvariantCulture);
        decimal prevCost = prod.Costo1 > 0 ? prod.Costo1 : prod.CostoMedio;
        TxtCostoAnterior.Text = prevCost.ToString("0", CultureInfo.InvariantCulture);
        TxtCostoCompra.Text = prevCost.ToString("0", CultureInfo.InvariantCulture);
        SelectIvaCombo(prod.Iva);

        decimal p1 = prod.P1 > 0 ? prod.P1 : (_companyData.Utilidad1 > 0 ? _companyData.Utilidad1 : 35m);
        decimal p2 = prod.P2 > 0 ? prod.P2 : (_companyData.Utilidad2 > 0 ? _companyData.Utilidad2 : 45m);
        decimal p3 = prod.P3 > 0 ? prod.P3 : (_companyData.Utilidad3 > 0 ? _companyData.Utilidad3 : 60m);
        decimal p4 = prod.P4 > 0 ? prod.P4 : (_companyData.Utilidad4 > 0 ? _companyData.Utilidad4 : 80m);
        decimal p5 = prod.P5 > 0 ? prod.P5 : (_companyData.Utilidad5 > 0 ? _companyData.Utilidad5 : 100m);
        decimal p6 = prod.P6 > 0 ? prod.P6 : (_companyData.Utilidad6 > 0 ? _companyData.Utilidad6 : 120m);
        decimal p7 = prod.P7 > 0 ? prod.P7 : _companyData.Utilidad7;

        TxtP1.Text = p1.ToString("0.##", CultureInfo.InvariantCulture);
        TxtP2.Text = p2.ToString("0.##", CultureInfo.InvariantCulture);
        TxtP3.Text = p3.ToString("0.##", CultureInfo.InvariantCulture);
        TxtP4.Text = p4.ToString("0.##", CultureInfo.InvariantCulture);
        TxtP5.Text = p5.ToString("0.##", CultureInfo.InvariantCulture);
        TxtP6.Text = p6.ToString("0.##", CultureInfo.InvariantCulture);
        TxtP7.Text = p7.ToString("0.##", CultureInfo.InvariantCulture);

        TxtPrecio1.Text = prod.Precio1.ToString("0", CultureInfo.InvariantCulture);
        TxtPrecio2.Text = prod.Precio2.ToString("0", CultureInfo.InvariantCulture);
        TxtPrecio3.Text = prod.Precio3.ToString("0", CultureInfo.InvariantCulture);
        TxtPrecio4.Text = prod.Precio4.ToString("0", CultureInfo.InvariantCulture);
        TxtPrecio5.Text = prod.Precio5.ToString("0", CultureInfo.InvariantCulture);
        TxtPrecio6.Text = prod.Precio6.ToString("0", CultureInfo.InvariantCulture);
        TxtPrecio7.Text = prod.Precio7.ToString("0", CultureInfo.InvariantCulture);

        ChkUsaVencimiento.IsChecked = prod.UsaVencimiento == 1;
        ChkControlaGarantia.IsChecked = prod.ControlaGarantia == 1;
        TxtDiasGarantia.Text = prod.DiasGarantias.ToString();

        _suppressRecalc = false;
        RecalculateCostAndPrices();
        TxtStatus.Text = $"✓ Producto cargado: {prod.Descripcion} | Stock actual: {prod.StockActual:N2} | Costo ant.: {prevCost:N0} Gs.";
    }

    private void SelectIvaCombo(int iva)
    {
        string targetTag = iva is >= 1 and <= 3 ? iva.ToString() : "1";
        foreach (ComboBoxItem item in CmbIva.Items)
        {
            if (item.Tag?.ToString() == targetTag)
            {
                CmbIva.SelectedItem = item;
                break;
            }
        }
    }

    private int GetSelectedIva()
    {
        if (CmbIva.SelectedItem is ComboBoxItem item && int.TryParse(item.Tag?.ToString(), out int iva))
            return iva;
        return 1;
    }

    private void TxtCantidadOrCosto_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressRecalc || !IsLoaded)
            return;
        RecalculateCostAndPrices();
    }

    private void RecalculateCostAndPrices()
    {
        decimal stockAnt = ParseDec(TxtStockAnterior.Text);
        decimal costoAnt = ParseDec(TxtCostoAnterior.Text);
        decimal cant = ParseDec(TxtCantidad.Text);
        decimal costoCompra = ParseDec(TxtCostoCompra.Text);

        decimal promedio = SalesCalculationEngine.CalculateWeightedAverageCost(stockAnt, costoAnt, cant, costoCompra);
        _suppressRecalc = true;
        TxtCostoPromedio.Text = Math.Round(promedio, 0).ToString("0", CultureInfo.InvariantCulture);

        if (promedio > 0)
        {
            TxtPrecio1.Text = SalesCalculationEngine.CalculateSalePriceFromUtility(promedio, ParseDec(TxtP1.Text)).ToString("0", CultureInfo.InvariantCulture);
            TxtPrecio2.Text = SalesCalculationEngine.CalculateSalePriceFromUtility(promedio, ParseDec(TxtP2.Text)).ToString("0", CultureInfo.InvariantCulture);
            TxtPrecio3.Text = SalesCalculationEngine.CalculateSalePriceFromUtility(promedio, ParseDec(TxtP3.Text)).ToString("0", CultureInfo.InvariantCulture);
            TxtPrecio4.Text = SalesCalculationEngine.CalculateSalePriceFromUtility(promedio, ParseDec(TxtP4.Text)).ToString("0", CultureInfo.InvariantCulture);
            TxtPrecio5.Text = SalesCalculationEngine.CalculateSalePriceFromUtility(promedio, ParseDec(TxtP5.Text)).ToString("0", CultureInfo.InvariantCulture);
            TxtPrecio6.Text = SalesCalculationEngine.CalculateSalePriceFromUtility(promedio, ParseDec(TxtP6.Text)).ToString("0", CultureInfo.InvariantCulture);
            decimal p7 = ParseDec(TxtP7.Text);
            if (p7 > 0)
                TxtPrecio7.Text = SalesCalculationEngine.CalculateSalePriceFromUtility(promedio, p7).ToString("0", CultureInfo.InvariantCulture);
        }
        _suppressRecalc = false;
        UpdateSubtotalPreview();
    }

    private void UpdateSubtotalPreview()
    {
        if (TxtCantidad == null || TxtCostoCompra == null || TxtSubtotalPreview == null)
            return;

        decimal cant = ParseDec(TxtCantidad.Text);
        decimal costo = ParseDec(TxtCostoCompra.Text);
        decimal sub = Math.Round(cant * costo, 0);
        TxtSubtotalPreview.Text = $"{sub:N0} Gs.";
    }

    private void TxtUtility_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_suppressRecalc || sender is not TextBox tb)
            return;

        decimal baseCost = ParseDec(TxtCostoPromedio.Text);
        if (baseCost <= 0) baseCost = ParseDec(TxtCostoCompra.Text);
        if (baseCost <= 0) return;

        decimal p = ParseDec(tb.Text);
        decimal price = SalesCalculationEngine.CalculateSalePriceFromUtility(baseCost, p);

        _suppressRecalc = true;
        switch (tb.Tag?.ToString())
        {
            case "1": TxtPrecio1.Text = price.ToString("0", CultureInfo.InvariantCulture); break;
            case "2": TxtPrecio2.Text = price.ToString("0", CultureInfo.InvariantCulture); break;
            case "3": TxtPrecio3.Text = price.ToString("0", CultureInfo.InvariantCulture); break;
            case "4": TxtPrecio4.Text = price.ToString("0", CultureInfo.InvariantCulture); break;
            case "5": TxtPrecio5.Text = price.ToString("0", CultureInfo.InvariantCulture); break;
            case "6": TxtPrecio6.Text = price.ToString("0", CultureInfo.InvariantCulture); break;
            case "7": TxtPrecio7.Text = price.ToString("0", CultureInfo.InvariantCulture); break;
        }
        _suppressRecalc = false;
    }

    private void TxtPrice_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_suppressRecalc || sender is not TextBox tb)
            return;

        decimal baseCost = ParseDec(TxtCostoPromedio.Text);
        if (baseCost <= 0) baseCost = ParseDec(TxtCostoCompra.Text);
        if (baseCost <= 0) return;

        decimal price = ParseDec(tb.Text);
        decimal util = SalesCalculationEngine.CalculateUtilityFromSalePrice(baseCost, price);

        _suppressRecalc = true;
        switch (tb.Tag?.ToString())
        {
            case "1": TxtP1.Text = util.ToString("0.##", CultureInfo.InvariantCulture); break;
            case "2": TxtP2.Text = util.ToString("0.##", CultureInfo.InvariantCulture); break;
            case "3": TxtP3.Text = util.ToString("0.##", CultureInfo.InvariantCulture); break;
            case "4": TxtP4.Text = util.ToString("0.##", CultureInfo.InvariantCulture); break;
            case "5": TxtP5.Text = util.ToString("0.##", CultureInfo.InvariantCulture); break;
            case "6": TxtP6.Text = util.ToString("0.##", CultureInfo.InvariantCulture); break;
            case "7": TxtP7.Text = util.ToString("0.##", CultureInfo.InvariantCulture); break;
        }
        _suppressRecalc = false;
    }

    private void BtnRecalcPrices_Click(object sender, RoutedEventArgs e)
    {
        RecalculateCostAndPrices();
        TxtStatus.Text = "✓ Precios de venta (1 al 7) recalculados según costo promedio y % de utilidad.";
    }

    private void CmbIva_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateSubtotalPreview();
    }

    private void TxtCantidad_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            TxtCostoCompra.Focus();
            TxtCostoCompra.SelectAll();
        }
    }

    private void TxtCostoCompra_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            RecalculateCostAndPrices();
            TxtPrecio1.Focus();
            TxtPrecio1.SelectAll();
        }
    }

    private void TxtPrecio1_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            TxtPrice_LostFocus(TxtPrecio1, new RoutedEventArgs());
            AcceptAndClose();
        }
    }

    private void BtnSearchCatalog_Click(object sender, RoutedEventArgs e)
    {
        _windowManager.OpenProductsModule(0, async selectedCode =>
        {
            if (!string.IsNullOrWhiteSpace(selectedCode))
            {
                TxtCodigo.Text = selectedCode;
                await LookupProductByCodeAsync(selectedCode);
            }
        });
    }

    private void BtnNewProduct_Click(object sender, RoutedEventArgs e)
    {
        _windowManager.OpenProductsModule(1, async selectedCode =>
        {
            if (!string.IsNullOrWhiteSpace(selectedCode))
            {
                TxtCodigo.Text = selectedCode;
                await LookupProductByCodeAsync(selectedCode);
            }
        });
    }

    private void BtnAccept_Click(object sender, RoutedEventArgs e) => AcceptAndClose();

    private void AcceptAndClose()
    {
        if (_currentProduct == null || _currentProduct.IdProducto <= 0)
        {
            MessageBox.Show("Seleccione un producto válido antes de aceptar.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtCodigo.Focus();
            return;
        }

        decimal cant = ParseDec(TxtCantidad.Text);
        if (cant <= 0)
        {
            MessageBox.Show("La cantidad comprada debe ser mayor a 0.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtCantidad.Focus();
            return;
        }

        decimal costoCompra = ParseDec(TxtCostoCompra.Text);
        if (costoCompra <= 0)
        {
            MessageBox.Show("El costo unitario de compra debe ser mayor a 0.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtCostoCompra.Focus();
            return;
        }

        decimal costoProm = ParseDec(TxtCostoPromedio.Text);
        if (costoProm <= 0) costoProm = costoCompra;

        decimal lineTotal = Math.Round(cant * costoCompra, 2, MidpointRounding.AwayFromZero);
        int iva = GetSelectedIva();

        decimal preExenta = iva == 3 ? lineTotal : 0m;
        decimal preIva5 = iva == 2 ? lineTotal : 0m;
        decimal preIva10 = iva == 1 ? lineTotal : 0m;

        int diasGar = int.TryParse(TxtDiasGarantia.Text, out int dg) ? dg : 0;
        DateTime iniGar = DpInicioGarantia.SelectedDate ?? DateTime.Today;

        ResultItem = new PurchaseItemEntity
        {
            IdProducto = _currentProduct.IdProducto,
            Codigo = _currentProduct.Codigo,
            Descripcion = _currentProduct.Descripcion,
            Cantidad = cant,
            PreUnitario = costoCompra,
            PreExenta = preExenta,
            PreIva5 = preIva5,
            PreIva10 = preIva10,
            Iva = iva,
            StockAnterior = ParseDec(TxtStockAnterior.Text),
            CostoAnterior = ParseDec(TxtCostoAnterior.Text),
            CostoMedio = costoProm,
            ActualizarPreciosVenta = ChkActualizarPrecios.IsChecked == true,
            P1 = ParseDec(TxtP1.Text),
            Precio1 = ParseDec(TxtPrecio1.Text),
            P2 = ParseDec(TxtP2.Text),
            Precio2 = ParseDec(TxtPrecio2.Text),
            P3 = ParseDec(TxtP3.Text),
            Precio3 = ParseDec(TxtPrecio3.Text),
            P4 = ParseDec(TxtP4.Text),
            Precio4 = ParseDec(TxtPrecio4.Text),
            P5 = ParseDec(TxtP5.Text),
            Precio5 = ParseDec(TxtPrecio5.Text),
            P6 = ParseDec(TxtP6.Text),
            Precio6 = ParseDec(TxtPrecio6.Text),
            P7 = ParseDec(TxtP7.Text),
            Precio7 = ParseDec(TxtPrecio7.Text),
            UsaVencimiento = ChkUsaVencimiento.IsChecked == true ? 1 : 0,
            Lote = TxtLote.Text.Trim(),
            FechaVencimiento = DpVencimiento.SelectedDate,
            ControlaGarantia = ChkControlaGarantia.IsChecked == true ? 1 : 0,
            DiasGarantias = diasGar,
            FechaInicioGarantia = iniGar,
            FechaFinGarantia = iniGar.AddDays(diasGar)
        };

        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            DialogResult = false;
            Close();
        }
        else if (e.Key == Key.F3)
        {
            e.Handled = true;
            BtnSearchCatalog_Click(this, new RoutedEventArgs());
        }
        else if (e.Key == Key.F7)
        {
            e.Handled = true;
            BtnNewProduct_Click(this, new RoutedEventArgs());
        }
        else if (e.Key == Key.F4)
        {
            e.Handled = true;
            AcceptAndClose();
        }
    }

    private static decimal ParseDec(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0m;
        string clean = text.Trim().Replace(" ", "").Replace(",", ".");
        return decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal v) ? v : 0m;
    }
}
