using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FirewallControl.App.Services;
using FirewallControl.Core.Business;
using FirewallControl.Core.Entities;
using FirewallControl.Core.Interfaces;
using Microsoft.Win32;

namespace FirewallControl.App.Views;

/// <summary>
/// Unified migration of crear_productos.scx, abm_productos.scx (72 columns), crear_productos_ajuste.scx,
/// and auxiliary product catalogs (grupos, subgrupos, marcas, modelos, depósitos).
/// </summary>
public partial class ProductsManagementWindow : Window
{
    private readonly IProductRepository _productRepository;
    private readonly ISystemRepository? _systemRepository;
    private readonly IWindowManagerService? _windowManager;
    private readonly IPurchaseRepository? _purchaseRepository;
    private readonly ICashRepository? _cashRepository;

    private Action<string>? _onProductCodeSelected;
    private ProductEntity _editingProduct = new();
    private CompanyDataEntity _companyData = new();
    private string _lcTipo = "A";
    private string _azTab = "Todo";
    private bool _suppressCalc;
    private bool _autoCodigoPressed;
    private decimal _originalDepositStock;

    // Auxiliary catalog editing state
    private ProductGroupEntity _editingAuxGrupo = new();
    private ProductSubGroupEntity _editingAuxSubGrupo = new();
    private ProductBrandEntity _editingAuxMarca = new();
    private ProductModelEntity _editingAuxModelo = new();
    private DepositEntity _editingAuxDeposito = new() { Estado = 1, IdSucursal = 1 };

    // Cached lookups for F3 / combos
    private List<SupplierEntity> _suppliers = new();
    private List<ProductGroupEntity> _groups = new();
    private List<ProductSubGroupEntity> _subGroups = new();
    private List<ProductBrandEntity> _brands = new();
    private List<ProductModelEntity> _models = new();
    private List<CurrencyEntity> _currencies = new();
    private List<DepositEntity> _deposits = new();

    public ProductsManagementWindow(
        IProductRepository productRepository,
        ISystemRepository? systemRepository = null,
        IWindowManagerService? windowManager = null,
        IPurchaseRepository? purchaseRepository = null,
        ICashRepository? cashRepository = null)
    {
        InitializeComponent();
        AppIconHelper.ApplySefirosIcon(this);
        _productRepository = productRepository;
        _systemRepository = systemRepository;
        _windowManager = windowManager;
        _purchaseRepository = purchaseRepository;
        _cashRepository = cashRepository;

        Loaded += async (_, _) => await InitializeWindowAsync();
    }

    private async Task InitializeWindowAsync()
    {
        try
        {
            if (_systemRepository != null)
            {
                try { _companyData = await _systemRepository.GetCompanyDataAsync(); } catch { }
            }

            bool isAdmin = _windowManager == null ||
                string.Equals(_windowManager.CurrentUserGroup.Cidgrupo?.Trim(), "ADMIN", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(_windowManager.CurrentUser.Cidusuario?.Trim(), "ADMINISTRA", StringComparison.OrdinalIgnoreCase);
            BtnFormAccess.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;

            await LoadLookupListsAsync();
            await RunSearchAsync();

            if (_editingProduct.IdProducto == 0 && string.IsNullOrEmpty(_editingProduct.Codigo))
            {
                PrepareNewProductForm(switchToTab1: false);
            }

            if (MainTabs.SelectedIndex == 0)
                TxtSearch.Focus();
        }
        catch (Exception ex)
        {
            LblStatus.Text = $"Error al inicializar módulo de productos: {ex.Message}";
        }
    }

    public void Configure(int initialTab, Action<string>? onProductCodeSelected, int auxSubTab = 0)
    {
        int clampedTab = Math.Clamp(initialTab, 0, 2);
        MainTabs.SelectedIndex = clampedTab;
        if (clampedTab == 2)
            AuxCatalogTabs.SelectedIndex = Math.Clamp(auxSubTab, 0, 4);

        _onProductCodeSelected = onProductCodeSelected;
        if (clampedTab == 1 && _editingProduct.IdProducto <= 0)
        {
            PrepareNewProductForm(switchToTab1: true);
        }

        if (clampedTab == 0)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!IsVisible)
                    return;
                Activate();
                TxtSearch.Focus();
                Keyboard.Focus(TxtSearch);
                TxtSearch.SelectAll();
            }), System.Windows.Threading.DispatcherPriority.Input);
        }
    }

    private int CurrentDepositId => CmbDepositos.SelectedValue is int id && id > 0 ? id : 1;

    private string CurrentUsername =>
        string.IsNullOrWhiteSpace(_windowManager?.CurrentUser.Cidusuario)
            ? "ADMINISTRA"
            : _windowManager.CurrentUser.Cidusuario.Trim();

    private (TextBox Costo, TextBox P, TextBox Precio)[] GetPriceRows() => new[]
    {
        (TxtCosto1, TxtP1, TxtPrecio1),
        (TxtCosto2, TxtP2, TxtPrecio2),
        (TxtCosto3, TxtP3, TxtPrecio3),
        (TxtCosto4, TxtP4, TxtPrecio4),
        (TxtCosto5, TxtP5, TxtPrecio5),
        (TxtCosto6, TxtP6, TxtPrecio6),
        (TxtCosto7, TxtP7, TxtPrecio7)
    };

    private async Task LoadLookupListsAsync()
    {
        _deposits = await _productRepository.GetDepositsAsync();
        object? prevDep = CmbDepositos.SelectedValue;
        CmbDepositos.ItemsSource = _deposits;
        if (prevDep is int depId && _deposits.Any(d => d.IdDeposito == depId))
            CmbDepositos.SelectedValue = depId;
        else if (_deposits.Count > 0)
            CmbDepositos.SelectedIndex = 0;
        GridAuxDepositos.ItemsSource = _deposits;

        _groups = await _productRepository.GetProductGroupsAsync();
        var groupItems = new List<ProductGroupEntity> { new() { IdGrupoProducto = 0, Descripcion = "(Seleccione grupo)" } };
        groupItems.AddRange(_groups);
        CmbGrupo.ItemsSource = groupItems;
        GridAuxGrupos.ItemsSource = _groups;

        _subGroups = await _productRepository.GetProductSubGroupsAsync();
        var subItems = new List<ProductSubGroupEntity> { new() { IdSubGrupoProducto = 0, Descripcion = "(Sin subgrupo)" } };
        subItems.AddRange(_subGroups);
        CmbSubGrupo.ItemsSource = subItems;
        GridAuxSubGrupos.ItemsSource = _subGroups;

        _brands = await _productRepository.GetProductBrandsAsync();
        var brandItems = new List<ProductBrandEntity> { new() { IdMarcaProducto = 0, Descripcion = "(Sin marca)" } };
        brandItems.AddRange(_brands);
        CmbMarca.ItemsSource = brandItems;
        GridAuxMarcas.ItemsSource = _brands;

        _models = await _productRepository.GetProductModelsAsync();
        var modelItems = new List<ProductModelEntity> { new() { IdModeloProducto = 0, Descripcion = "(Sin modelo)" } };
        modelItems.AddRange(_models);
        CmbModelo.ItemsSource = modelItems;
        GridAuxModelos.ItemsSource = _models;

        if (_purchaseRepository != null)
            _suppliers = await _purchaseRepository.SearchSuppliersAsync("", 300);
        else
            _suppliers = new List<SupplierEntity>();
        var supItems = new List<SupplierEntity> { new() { IdProveedor = 0, Descripcion = "(Seleccione proveedor)" } };
        supItems.AddRange(_suppliers);
        CmbProveedor.ItemsSource = supItems;

        if (_cashRepository != null)
            _currencies = await _cashRepository.GetCurrenciesAsync();
        else
            _currencies = new List<CurrencyEntity> { new() { IdMoneda = 1, Descripcion = "GUARANI" } };
        CmbMoneda.ItemsSource = _currencies;
        if (_currencies.Count > 0 && CmbMoneda.SelectedValue == null)
            CmbMoneda.SelectedIndex = 0;
    }

    // =========================================================================
    // TAB 0: CATÁLOGO GENERAL (CREAR_PRODUCTOS.SCX)
    // =========================================================================
    private async Task RunSearchAsync()
    {
        try
        {
            var criteria = CmbSearchCriteria.SelectedIndex switch
            {
                1 => ProductSearchCriteria.Codigo,
                2 => ProductSearchCriteria.Grupo,
                3 => ProductSearchCriteria.Proveedor,
                _ => ProductSearchCriteria.Descripcion
            };

            var list = await _productRepository.SearchProductsAdvancedAsync(new ProductSearchFilter
            {
                Criteria = criteria,
                SearchText = TxtSearch.Text,
                AzTab = _azTab,
                IdDeposito = CurrentDepositId,
                IncludeInactive = ChkIncludeInactive.IsChecked == true,
                Limit = 400
            });

            GridProducts.ItemsSource = list;
            LblCatalogCount.Text = $"{list.Count} productos";
            LblStatus.Text = $"✓ Catálogo cargado ({list.Count} registros) · Índice: {_azTab} · Depósito #{CurrentDepositId}.";
        }
        catch (Exception ex)
        {
            LblStatus.Text = $"Error al buscar productos: {ex.Message}";
        }
    }

    private async void AzTab_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded || sender is not RadioButton rb) return;
        _azTab = rb.Tag?.ToString() ?? "Todo";
        await RunSearchAsync();
    }

    private async void ChkIncludeInactive_Changed(object sender, RoutedEventArgs e)
    {
        if (IsLoaded) await RunSearchAsync();
    }

    private void CmbSearchCriteria_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded) TxtSearch.Focus();
    }

    private async void TxtSearch_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter || e.Key == Key.Return)
        {
            e.Handled = true;
            await RunSearchAsync();
            TxtSearch.SelectAll();
        }
        else if (e.Key == Key.Down && GridProducts.Items.Count > 0)
        {
            e.Handled = true;
            GridProducts.SelectedIndex = 0;
            GridProducts.Focus();
        }
    }

    private async void BtnSearch_Click(object sender, RoutedEventArgs e) => await RunSearchAsync();

    private async void BtnClearFilter_Click(object sender, RoutedEventArgs e)
    {
        TxtSearch.Clear();
        _azTab = "Todo";
        foreach (var rb in PanelAzFilter.Children.OfType<RadioButton>())
        {
            if (rb.Tag?.ToString() == "Todo") rb.IsChecked = true;
        }
        await RunSearchAsync();
        TxtSearch.Focus();
    }

    private async void CmbDepositos_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded)
        {
            await RunSearchAsync();
            if (_editingProduct.IdProducto > 0)
            {
                decimal depStock = await _productRepository.GetDepositStockAsync(_editingProduct.IdProducto, CurrentDepositId);
                _originalDepositStock = depStock;
                TxtStockActual.Text = depStock.ToString("0.##", CultureInfo.InvariantCulture);
            }
        }
    }

    private async void GridProducts_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GridProducts.SelectedItem is ProductEntity prod)
        {
            BtnReactivateCatalog.Visibility = prod.Estado == 1 ? Visibility.Collapsed : Visibility.Visible;
            await LoadProductToFormAsync(prod, "M");
        }
        else
        {
            BtnReactivateCatalog.Visibility = Visibility.Collapsed;
        }
    }

    private void GridProducts_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (GridProducts.SelectedItem is ProductEntity prod)
            SelectOrOpenForEdit(prod);
    }

    private void BtnSelectForSale_Click(object sender, RoutedEventArgs e)
    {
        if (GridProducts.SelectedItem is ProductEntity prod)
            SelectOrOpenForEdit(prod);
    }

    private void SelectOrOpenForEdit(ProductEntity prod)
    {
        if (_onProductCodeSelected != null)
        {
            var cb = _onProductCodeSelected;
            _onProductCodeSelected = null;
            if (_windowManager != null)
                _windowManager.SafeHideAndRestoreFocus(this);
            else
                Hide();
            cb(prod.Codigo);
        }
        else
        {
            _ = LoadProductToFormAsync(prod, "M");
            MainTabs.SelectedIndex = 1;
        }
    }

    private async void BtnEditSelectedProduct_Click(object sender, RoutedEventArgs e)
    {
        if (GridProducts.SelectedItem is not ProductEntity prod)
        {
            MessageBox.Show("Seleccione un producto de la grilla para editar.", "Catálogo de Productos", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        await LoadProductToFormAsync(prod, "M");
        MainTabs.SelectedIndex = 1;
    }

    private async void BtnDeleteFromCatalog_Click(object sender, RoutedEventArgs e)
    {
        if (GridProducts.SelectedItem is not ProductEntity prod) return;

        if (MessageBox.Show($"¿Está seguro de dar de baja (inactivar) al producto [{prod.Codigo}] {prod.Descripcion}?",
                "Confirmar Baja Lógica", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            await _productRepository.DeleteProductAsync(prod.IdProducto);
            LblStatus.Text = $"✓ Producto [{prod.Codigo}] dado de baja.";
            await RunSearchAsync();
        }
    }

    private async void BtnReactivateFromCatalog_Click(object sender, RoutedEventArgs e)
    {
        if (GridProducts.SelectedItem is not ProductEntity prod) return;
        await _productRepository.ReactivateProductAsync(prod.IdProducto, CurrentUsername);
        LblStatus.Text = $"✓ Producto [{prod.Codigo}] reactivado.";
        await RunSearchAsync();
    }

    private void BtnUpdateAllPricesUtility_Click(object sender, RoutedEventArgs e)
    {
        if (_windowManager != null)
        {
            _windowManager.OpenAjustePreciosMasivoWindow();
        }
        else
        {
            var win = new AjustePreciosMasivoWindow(_productRepository, _systemRepository, _purchaseRepository) { Owner = this };
            win.ShowDialog();
        }
        _ = RunSearchAsync();
    }

    private void BtnInfoPrecios_Click(object sender, RoutedEventArgs e) => OpenSubReports(0);
    private void BtnStockPorDeposito_Click(object sender, RoutedEventArgs e) => OpenSubReports(1);
    private void BtnVencimientos_Click(object sender, RoutedEventArgs e) => OpenSubReports(2);
    private void BtnConsultaPrecios_Click(object sender, RoutedEventArgs e) => OpenSubReports(3);

    private void OpenSubReports(int tab)
    {
        if (_windowManager != null)
        {
            _windowManager.OpenProductSubReportsWindow(tab);
        }
        else
        {
            var win = new ProductSubReportsWindow(_productRepository) { Owner = this };
            win.Configure(tab);
            win.ShowDialog();
        }
    }

    private void BtnTraslados_Click(object sender, RoutedEventArgs e)
    {
        if (_windowManager != null)
        {
            _windowManager.OpenTrasladoDepositosWindow();
        }
        else
        {
            var win = new TrasladoDepositosWindow(_productRepository, CurrentUsername) { Owner = this };
            win.ShowDialog();
        }
        _ = RunSearchAsync();
    }

    private void BtnImagenes_Click(object sender, RoutedEventArgs e)
    {
        if (_windowManager != null)
        {
            _windowManager.OpenProductImageBrowserWindow();
        }
        else
        {
            var win = new ProductImageBrowserWindow(_productRepository) { Owner = this };
            win.ShowDialog();
        }
        _ = RunSearchAsync();
    }

    private void BtnFormAccess_Click(object sender, RoutedEventArgs e)
    {
        if (_windowManager != null)
        {
            _windowManager.OpenFormAccessDialog("CREAR_PRODUCTOS", "Lista de Productos");
        }
        else if (_systemRepository != null)
        {
            var win = new FormAccessDialog(_systemRepository) { Owner = this };
            win.Configure("CREAR_PRODUCTOS", "Lista de Productos");
            win.ShowDialog();
        }
    }

    private void BtnExportPriceList_Click(object sender, RoutedEventArgs e)
    {
        if (GridProducts.ItemsSource is not IEnumerable<ProductEntity> items) return;

        var sfd = new SaveFileDialog
        {
            Title = "Exportar Lista de Productos",
            Filter = "Archivo CSV (*.csv)|*.csv",
            FileName = $"ListaProductos_{DateTime.Now:yyyyMMdd_HHmm}.csv"
        };
        if (sfd.ShowDialog() != true) return;

        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("ID;Codigo;CodigoBarra;Descripcion;Costo1;Precio1;Precio2;Precio3;Grupo;Proveedor;Telefono;StockActual;IVA;Estado");
            foreach (var p in items)
            {
                sb.AppendLine($"{p.IdProducto};{p.Codigo};{p.CodigoBarra};\"{p.Descripcion}\";{p.Costo1:0};{p.Precio1:0};{p.Precio2:0};{p.Precio3:0};\"{p.GrupoDescripcion}\";\"{p.ProveedorDescripcion}\";\"{p.ProveedorTelefono}\";{p.StockActual:0.##};{p.IvaDescripcion};{p.EstadoDescripcion}");
            }
            File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
            LblStatus.Text = $"✓ Lista de productos exportada a {sfd.FileName}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al exportar:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // =========================================================================
    // TAB 1: ABM_PRODUCTOS.SCX (72 COLUMNAS)
    // =========================================================================
    private void BtnNewProduct_Click(object sender, RoutedEventArgs e)
        => PrepareNewProductForm(switchToTab1: true);

    private void PrepareNewProductForm(bool switchToTab1)
    {
        var fresh = new ProductEntity
        {
            Estado = 1,
            Iva = 1,
            Impuesto = 10,
            IdMoneda = 1,
            Cambio = 1m,
            IdProveedor = _suppliers.Count > 0 ? _suppliers[0].IdProveedor : 0,
            IdGrupoProducto = _groups.Count > 0 ? _groups[0].IdGrupoProducto : 0,
            ImprimeFactura = 1,
            VendeSinStock = 1,
            AutoSuma = 1,
            PublicarWeb = 1,
            MuestraBusqueda = 1,
            P1 = _companyData.Utilidad1 > 0 ? _companyData.Utilidad1 : 35m,
            P2 = _companyData.Utilidad2 > 0 ? _companyData.Utilidad2 : 45m,
            P3 = _companyData.Utilidad3 > 0 ? _companyData.Utilidad3 : 60m,
            P4 = _companyData.Utilidad4 > 0 ? _companyData.Utilidad4 : 80m,
            P5 = _companyData.Utilidad5 > 0 ? _companyData.Utilidad5 : 100m,
            P6 = _companyData.Utilidad6 > 0 ? _companyData.Utilidad6 : 120m,
            P7 = _companyData.Utilidad7
        };
        _ = LoadProductToFormAsync(fresh, "A");
        if (switchToTab1)
        {
            MainTabs.SelectedIndex = 1;
            TxtCodigo.Focus();
        }
    }

    private async void BtnAutoCodigo_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            int nextId = await _productRepository.GetNextProductIdAsync();
            _suppressCalc = true;
            TxtCodigo.Text = nextId.ToString(CultureInfo.InvariantCulture);
            if (string.IsNullOrWhiteSpace(TxtCodigoBarra.Text))
                TxtCodigoBarra.Text = nextId.ToString(CultureInfo.InvariantCulture);
            _suppressCalc = false;
            _autoCodigoPressed = true;
            TxtDescripcion.Focus();
        }
        catch (Exception ex)
        {
            LblStatus.Text = $"Error al obtener código automático: {ex.Message}";
        }
    }

    private void TxtCodigo_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_suppressCalc)
            _autoCodigoPressed = false;
    }

    private async Task LoadProductToFormAsync(ProductEntity p, string lcTipo)
    {
        _editingProduct = p;
        _lcTipo = lcTipo;
        _autoCodigoPressed = false;
        _suppressCalc = true;

        bool isEdit = lcTipo == "M" && p.IdProducto > 0;
        TxtAbmTitle.Text = isEdit
            ? $"EDITANDO PRODUCTO #{p.IdProducto} ({p.Codigo})"
            : "NUEVO PRODUCTO (ABM_PRODUCTOS)";
        BtnAutoCodigo.Visibility = isEdit ? Visibility.Collapsed : Visibility.Visible;
        BtnReactivateAbm.Visibility = isEdit && p.Estado != 1 ? Visibility.Visible : Visibility.Collapsed;

        TxtIdProducto.Text = p.IdProducto > 0 ? p.IdProducto.ToString(CultureInfo.InvariantCulture) : "NUEVO";
        TxtCodigo.Text = p.Codigo;
        TxtCodigoBarra.Text = p.CodigoBarra;
        TxtDescripcion.Text = p.Descripcion;
        CmbMoneda.SelectedValue = p.IdMoneda > 0 ? p.IdMoneda : 1;
        TxtCambio.Text = (p.Cambio > 0m ? p.Cambio : 1m).ToString("0.##", CultureInfo.InvariantCulture);

        CmbProveedor.SelectedValue = p.IdProveedor;
        CmbGrupo.SelectedValue = p.IdGrupoProducto;
        CmbSubGrupo.SelectedValue = p.IdSubGrupoProducto;
        CmbMarca.SelectedValue = p.IdMarcaProducto;
        CmbModelo.SelectedValue = p.IdModeloProducto;

        TxtReferencia.Text = p.Referencia;
        TxtFamilia.Text = p.Familia;
        TxtAplicacion.Text = p.Aplicacion;
        TxtObservacion.Text = p.Observacion;

        decimal depStock = isEdit
            ? await _productRepository.GetDepositStockAsync(p.IdProducto, CurrentDepositId)
            : p.StockActual;
        _originalDepositStock = depStock;
        TxtStockActual.Text = depStock.ToString("0.##", CultureInfo.InvariantCulture);
        TxtStockMinimo.Text = p.StockMinimo.ToString(CultureInfo.InvariantCulture);
        TxtStockMaximo.Text = p.StockMaximo.ToString(CultureInfo.InvariantCulture);

        RbEstActivo.IsChecked = p.Estado is not (2 or 3);
        RbEstInactivo.IsChecked = p.Estado == 2;
        RbEstBloqueado.IsChecked = p.Estado == 3;

        RbIva10.IsChecked = p.Iva is not (2 or 3);
        RbIva5.IsChecked = p.Iva == 2;
        RbIvaExenta.IsChecked = p.Iva == 3;

        ChkNoControlaStock.IsChecked = p.NoControlaStock == 1;
        ChkVendeSinStock.IsChecked = p.VendeSinStock == 1;
        ChkImprimeFactura.IsChecked = p.ImprimeFactura == 1;
        ChkAutoSuma.IsChecked = p.AutoSuma == 1;
        ChkPublicarWeb.IsChecked = p.PublicarWeb == 1;
        ChkFraccionable.IsChecked = p.Fraccionable == 1;
        ChkUsaSerial.IsChecked = p.UsaSerial == 1;
        ChkAutoCantidad.IsChecked = p.AutoCantidad == 1;
        ChkAutoPrecio.IsChecked = p.AutoPrecio == 1;
        ChkUsaVencimiento.IsChecked = p.UsaVencimiento == 1;
        ChkDescripcionEditable.IsChecked = p.DescripcionEditable == 1;
        ChkControlaGarantia.IsChecked = p.ControlaGarantia == 1;
        ChkCompuesto.IsChecked = p.Compuesto == 1;

        PanelGarantiaExtra.Visibility = p.ControlaGarantia == 1 ? Visibility.Visible : Visibility.Collapsed;
        BtnConfigCompuesto.Visibility = p.Compuesto == 1 ? Visibility.Visible : Visibility.Collapsed;
        DpGarantiaInicio.SelectedDate = p.FechaInicioGarantia;
        TxtDiasGarantia.Text = p.DiasGarantias.ToString(CultureInfo.InvariantCulture);
        DpGarantiaFin.SelectedDate = p.FechaFinGarantia;

        ChkCocina.IsChecked = p.Cocina == 1;
        ChkBarra.IsChecked = p.Barra == 1;
        ChkChurrasqueria.IsChecked = p.Churrasqueria == 1;
        ChkOtros.IsChecked = p.Otros == 1;
        ChkGuarnicion.IsChecked = p.Guarnicion == 1;
        ChkMenu.IsChecked = p.Menu == 1;
        ChkComanda.IsChecked = p.Comanda;

        TxtCostoMedio.Text = (p.CostoMedio > 0 ? p.CostoMedio : p.Costo1).ToString("0.##", CultureInfo.InvariantCulture);
        TxtCostoMonedaExt.Text = p.CostoMonedaExtranjera.ToString("0.##", CultureInfo.InvariantCulture);

        decimal[] costs = { p.Costo1, p.Costo2, p.Costo3, p.Costo4, p.Costo5, p.Costo6, p.Costo7 };
        decimal[] pcts = { Math.Max(0m, p.P1), Math.Max(0m, p.P2), Math.Max(0m, p.P3), Math.Max(0m, p.P4), Math.Max(0m, p.P5), Math.Max(0m, p.P6), Math.Max(0m, p.P7) };
        decimal[] prices = { p.Precio1, p.Precio2, p.Precio3, p.Precio4, p.Precio5, p.Precio6, p.Precio7 };

        var rows = GetPriceRows();
        for (int i = 0; i < 7; i++)
        {
            rows[i].Costo.Text = costs[i].ToString("0.##", CultureInfo.InvariantCulture);
            rows[i].P.Text = pcts[i].ToString("0.##", CultureInfo.InvariantCulture);
            rows[i].Precio.Text = prices[i].ToString("0.##", CultureInfo.InvariantCulture);
        }

        // Photo & last movements
        RefreshPhotoPreview(p.Imagen);
        TxtUltCompFecha.Text = p.FechaUltimaCompra?.ToString("dd/MM/yyyy") ?? "--";
        TxtUltCompCant.Text = p.CantidadUltimaCompra.ToString("0.##", CultureInfo.InvariantCulture);
        TxtUltVentFecha.Text = p.FechaUltimaVenta?.ToString("dd/MM/yyyy") ?? "--";
        TxtUltVentCant.Text = p.CantidadUltimaVenta.ToString("0.##", CultureInfo.InvariantCulture);

        _suppressCalc = false;

        // Load deposit stocks, physical locations & lots
        if (isEdit)
        {
            try
            {
                GridAbmStockDepositos.ItemsSource = await _productRepository.GetProductStockByDepositsAsync(p.IdProducto, p.Codigo);
                GridAbmUbicaciones.ItemsSource = await _productRepository.GetProductLocationsAsync(p.IdProducto);
                GridAbmLotes.ItemsSource = await _productRepository.GetProductLotsAsync(p.IdProducto, null);
            }
            catch { }
        }
        else
        {
            GridAbmStockDepositos.ItemsSource = null;
            GridAbmUbicaciones.ItemsSource = null;
            GridAbmLotes.ItemsSource = null;
        }
    }

    // --- 21-Cell Calculator & VFP Bi-directional Cost/Margin Engine ---
    private void TxtCostoRow_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_suppressCalc || sender is not TextBox tb || !int.TryParse(tb.Tag?.ToString(), out int idx))
            return;
        ApplyCostoChange(idx - 1);
    }

    private void ApplyCostoChange(int rowIdx)
    {
        var rows = GetPriceRows();
        decimal costo = ParseDec(rows[rowIdx].Costo.Text);
        _suppressCalc = true;

        if (rowIdx == 0 && costo > 0m)
        {
            TxtCostoMedio.Text = costo.ToString("0.##", CultureInfo.InvariantCulture);
            for (int k = 1; k < 7; k++)
            {
                if (ParseDec(rows[k].Costo.Text) == 0m)
                    rows[k].Costo.Text = costo.ToString("0.##", CultureInfo.InvariantCulture);
            }
        }

        for (int k = rowIdx == 0 ? 0 : rowIdx; k <= (rowIdx == 0 ? 6 : rowIdx); k++)
        {
            decimal ck = ParseDec(rows[k].Costo.Text);
            decimal pk = ParseDec(rows[k].P.Text);
            if (ck > 0m && pk > 0m)
                rows[k].Precio.Text = Math.Round(ck + (ck * pk / 100m), 2, MidpointRounding.AwayFromZero).ToString("0.##", CultureInfo.InvariantCulture);
        }

        _suppressCalc = false;
    }

    private void TxtUtility_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_suppressCalc || sender is not TextBox tb || !int.TryParse(tb.Tag?.ToString(), out int idx))
            return;
        ApplyUtilityChange(idx - 1);
    }

    private void ApplyUtilityChange(int rowIdx)
    {
        var rows = GetPriceRows();
        decimal costo = ParseDec(rows[rowIdx].Costo.Text);
        if (costo <= 0m) return;

        decimal pct = ParseDec(rows[rowIdx].P.Text);
        decimal price = Math.Round(costo + (costo * pct / 100m), 2, MidpointRounding.AwayFromZero);
        _suppressCalc = true;
        rows[rowIdx].Precio.Text = price.ToString("0.##", CultureInfo.InvariantCulture);
        _suppressCalc = false;
    }

    private void TxtPrice_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_suppressCalc || sender is not TextBox tb || !int.TryParse(tb.Tag?.ToString(), out int idx))
            return;
        ApplyPriceChange(idx - 1);
    }

    private void ApplyPriceChange(int rowIdx)
    {
        var rows = GetPriceRows();
        decimal precio = ParseDec(rows[rowIdx].Precio.Text);
        if (precio <= 0m) return;

        decimal costo = ParseDec(rows[rowIdx].Costo.Text);
        _suppressCalc = true;
        if (costo > 0m)
        {
            decimal pct = Math.Round(((precio - costo) * 100m) / costo, 2, MidpointRounding.AwayFromZero);
            if (pct > 9999m || pct < 0m) pct = 0m;
            rows[rowIdx].P.Text = pct.ToString("0.##", CultureInfo.InvariantCulture);
        }
        else
        {
            // VFP abm_productos exact fallback: sets default 20% utility and back-calculates cost
            rows[rowIdx].P.Text = "20";
            decimal calcCosto = Math.Round(precio - (precio * 20m / 100m), 2, MidpointRounding.AwayFromZero);
            rows[rowIdx].Costo.Text = calcCosto.ToString("0.##", CultureInfo.InvariantCulture);
            if (rowIdx == 0) TxtCostoMedio.Text = calcCosto.ToString("0.##", CultureInfo.InvariantCulture);
        }
        _suppressCalc = false;
    }

    private void PriceCell_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox tb || !int.TryParse(tb.Tag?.ToString(), out int idx))
            return;

        if (e.Key == Key.C)
        {
            e.Handled = true;
            void OnAccept(decimal val)
            {
                tb.Text = val.ToString("0.##", CultureInfo.InvariantCulture);
                TriggerCellRecalc(tb, idx - 1);
            }

            if (_windowManager != null)
            {
                _windowManager.OpenCalculatorDialog(OnAccept);
            }
            else
            {
                var dlg = new CalculatorDialog(OnAccept) { Owner = this };
                dlg.ShowDialog();
            }
        }
        else if (e.Key == Key.Enter || e.Key == Key.Return)
        {
            e.Handled = true;
            TriggerCellRecalc(tb, idx - 1);
            tb.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        }
    }

    private void TriggerCellRecalc(TextBox tb, int rowIdx)
    {
        if (tb.Name.StartsWith("TxtCosto", StringComparison.Ordinal)) ApplyCostoChange(rowIdx);
        else if (tb.Name.StartsWith("TxtP", StringComparison.Ordinal) && tb.Name.Length <= 5) ApplyUtilityChange(rowIdx);
        else ApplyPriceChange(rowIdx);
    }

    private void BtnAutoEmpresaPercent_Click(object sender, RoutedEventArgs e)
    {
        decimal[] defs =
        {
            _companyData.Utilidad1 > 0 ? _companyData.Utilidad1 : 80m,
            _companyData.Utilidad2 > 0 ? _companyData.Utilidad2 : 65m,
            _companyData.Utilidad3 > 0 ? _companyData.Utilidad3 : 40m,
            _companyData.Utilidad4 > 0 ? _companyData.Utilidad4 : 25m,
            _companyData.Utilidad5 > 0 ? _companyData.Utilidad5 : 100m,
            _companyData.Utilidad6 > 0 ? _companyData.Utilidad6 : 107m,
            _companyData.Utilidad7 > 0 ? _companyData.Utilidad7 : 120m
        };

        var rows = GetPriceRows();
        decimal c1 = ParseDec(TxtCosto1.Text);
        _suppressCalc = true;
        for (int i = 0; i < 7; i++)
        {
            rows[i].P.Text = defs[i].ToString("0.##", CultureInfo.InvariantCulture);
            if (ParseDec(rows[i].Costo.Text) == 0m && c1 > 0m)
                rows[i].Costo.Text = c1.ToString("0.##", CultureInfo.InvariantCulture);
            decimal ck = ParseDec(rows[i].Costo.Text);
            if (ck > 0m)
                rows[i].Precio.Text = Math.Round(ck + (ck * defs[i] / 100m), 2, MidpointRounding.AwayFromZero).ToString("0.##", CultureInfo.InvariantCulture);
        }
        _suppressCalc = false;
        LblStatus.Text = "✓ Porcentajes de utilidad de empresa aplicados a Listas 1–7.";
    }

    private void BtnRecalcProductPrices_Click(object sender, RoutedEventArgs e)
    {
        var rows = GetPriceRows();
        decimal costo1 = ParseDec(TxtCosto1.Text);
        if (costo1 <= 0m)
        {
            MessageBox.Show("Ingrese primero un Costo 1 mayor a 0.", "Cálculo de Precios", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _suppressCalc = true;
        for (int i = 0; i < 7; i++)
        {
            decimal ck = ParseDec(rows[i].Costo.Text);
            if (ck <= 0m)
            {
                ck = costo1;
                rows[i].Costo.Text = ck.ToString("0.##", CultureInfo.InvariantCulture);
            }
            decimal pct = ParseDec(rows[i].P.Text);
            if (pct > 0m || i < 6)
                rows[i].Precio.Text = Math.Round(ck + (ck * pct / 100m), 2, MidpointRounding.AwayFromZero).ToString("0.##", CultureInfo.InvariantCulture);
        }
        _suppressCalc = false;
        LblStatus.Text = "✓ Precios 1 al 7 recalculados a partir de costos y % de utilidad.";
    }

    // --- F3 Pickers & + Quick Create for 5 Relational Catalogs ---
    private void CatalogCombo_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F3 && sender is ComboBox cb)
        {
            e.Handled = true;
            OpenCatalogQuickPick(cb.Tag?.ToString());
        }
    }

    private void BtnPickCatalog_Click(object sender, RoutedEventArgs e)
        => OpenCatalogQuickPick((sender as FrameworkElement)?.Tag?.ToString());

    private void OpenCatalogQuickPick(string? kindTag)
    {
        var dlg = new CatalogQuickPickDialog { Owner = this };
        switch (kindTag)
        {
            case "Proveedor":
                dlg.Configure("Seleccionar Proveedor [F3]", _suppliers.Select(s => new QuickPickOption(s.IdProveedor, s.Descripcion, s.Ruc)));
                if (dlg.ShowDialog() == true && dlg.SelectedOption != null) CmbProveedor.SelectedValue = dlg.SelectedOption.Id;
                break;
            case "Grupo":
                dlg.Configure("Seleccionar Grupo de Productos [F3]", _groups.Select(g => new QuickPickOption(g.IdGrupoProducto, g.Descripcion)));
                if (dlg.ShowDialog() == true && dlg.SelectedOption != null) CmbGrupo.SelectedValue = dlg.SelectedOption.Id;
                break;
            case "SubGrupo":
                dlg.Configure("Seleccionar Subgrupo [F3]", _subGroups.Select(g => new QuickPickOption(g.IdSubGrupoProducto, g.Descripcion)));
                if (dlg.ShowDialog() == true && dlg.SelectedOption != null) CmbSubGrupo.SelectedValue = dlg.SelectedOption.Id;
                break;
            case "Marca":
                dlg.Configure("Seleccionar Marca [F3]", _brands.Select(g => new QuickPickOption(g.IdMarcaProducto, g.Descripcion)));
                if (dlg.ShowDialog() == true && dlg.SelectedOption != null) CmbMarca.SelectedValue = dlg.SelectedOption.Id;
                break;
            case "Modelo":
                dlg.Configure("Seleccionar Modelo [F3]", _models.Select(g => new QuickPickOption(g.IdModeloProducto, g.Descripcion)));
                if (dlg.ShowDialog() == true && dlg.SelectedOption != null) CmbModelo.SelectedValue = dlg.SelectedOption.Id;
                break;
        }
    }

    private async void BtnAddCatalog_Click(object sender, RoutedEventArgs e)
    {
        string? tag = (sender as FrameworkElement)?.Tag?.ToString();
        if (!Enum.TryParse<QuickCatalogKind>(tag, out var kind))
            return;

        var dlg = new QuickCatalogAddDialog { Owner = this };
        dlg.Configure(kind, _productRepository, _purchaseRepository);
        if (dlg.ShowDialog() != true || dlg.CreatedId <= 0)
            return;

        await LoadLookupListsAsync();
        switch (kind)
        {
            case QuickCatalogKind.Proveedor: CmbProveedor.SelectedValue = dlg.CreatedId; break;
            case QuickCatalogKind.Grupo: CmbGrupo.SelectedValue = dlg.CreatedId; break;
            case QuickCatalogKind.SubGrupo: CmbSubGrupo.SelectedValue = dlg.CreatedId; break;
            case QuickCatalogKind.Marca: CmbMarca.SelectedValue = dlg.CreatedId; break;
            case QuickCatalogKind.Modelo: CmbModelo.SelectedValue = dlg.CreatedId; break;
        }
        LblStatus.Text = $"✓ Catálogo '{dlg.CreatedDescription}' creado y seleccionado.";
    }

    // --- Toggles, Photo & Lots ---
    private void ChkControlaGarantia_Changed(object sender, RoutedEventArgs e)
    {
        if (PanelGarantiaExtra != null)
            PanelGarantiaExtra.Visibility = ChkControlaGarantia.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ChkCompuesto_Changed(object sender, RoutedEventArgs e)
    {
        if (BtnConfigCompuesto != null)
            BtnConfigCompuesto.Visibility = ChkCompuesto.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void BtnConfigCompuesto_Click(object sender, RoutedEventArgs e)
    {
        if (_editingProduct.IdProducto <= 0)
        {
            if (MessageBox.Show("Para configurar la receta del producto compuesto primero debemos guardar la ficha del producto.\n\n¿Desea guardarlo ahora?",
                    "Producto Compuesto", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            bool saved = await SaveCurrentProductAsync(closeAfterCallback: false);
            if (!saved || _editingProduct.IdProducto <= 0) return;
        }

        if (_windowManager != null)
        {
            _windowManager.OpenCombinarCompuestoDialog(_editingProduct.IdProducto, _editingProduct.Descripcion);
        }
        else
        {
            MessageBox.Show("Abra esta ventana desde el sistema principal para editar componentes de receta.",
                "Producto Compuesto", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var comps = await _productRepository.GetCompoundComponentsAsync(_editingProduct.IdProducto);
        if (comps.Count == 0 && ChkCompuesto.IsChecked == true)
        {
            if (MessageBox.Show("El producto no tiene ingredientes configurados en su receta.\n¿Desea mantener marcada la opción Compuesto?",
                    "Producto Compuesto", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                ChkCompuesto.IsChecked = false;
            }
        }
    }

    private void RefreshPhotoPreview(string? imageFileName)
    {
        string clean = (imageFileName ?? string.Empty).Trim();
        string? fullPath = ProductPhotoHelper.ResolvePhotoFilePath(clean, _windowManager?.ConfigService);
        ImgAbmPhoto.Source = ProductPhotoHelper.LoadBitmapSafe(fullPath);
        TxtImagenNombre.Text = string.IsNullOrEmpty(clean) ? "(Sin foto)" : clean;
    }

    private void ImgAbmPhoto_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => BtnChangePhoto_Click(sender, e);
    private void ImgAbmPhoto_MouseRightButtonDown(object sender, MouseButtonEventArgs e) => BtnClearPhoto_Click(sender, e);

    private void BtnChangePhoto_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new OpenFileDialog
        {
            Title = "Seleccionar imagen del producto (abm_fotoproducto)",
            Filter = "Imágenes (*.jpg;*.jpeg;*.png;*.bmp;*.gif)|*.jpg;*.jpeg;*.png;*.bmp;*.gif"
        };
        if (ofd.ShowDialog() != true) return;

        try
        {
            string fileName = ProductPhotoHelper.CopyPhotoToRepository(ofd.FileName, _windowManager?.ConfigService);
            _editingProduct.Imagen = fileName;
            RefreshPhotoPreview(fileName);
            LblStatus.Text = $"✓ Imagen '{fileName}' preparada (presione Guardar Ficha para persistir).";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al copiar imagen:\n{ex.Message}", "Imagen", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnClearPhoto_Click(object sender, RoutedEventArgs e)
    {
        _editingProduct.Imagen = string.Empty;
        RefreshPhotoPreview(string.Empty);
    }

    private async void BtnAddLot_Click(object sender, RoutedEventArgs e)
    {
        if (_editingProduct.IdProducto <= 0)
        {
            MessageBox.Show("Guarde primero el producto antes de cargar lotes.", "Lotes", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        string loteCode = (TxtNewLotCode.Text ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(loteCode))
        {
            MessageBox.Show("Ingrese el código de lote.", "Lotes", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var lot = new ProductLotEntity
        {
            IdProducto = _editingProduct.IdProducto,
            Lote = loteCode,
            Vencimiento = DpNewLotVenc.SelectedDate ?? DateTime.Today.AddMonths(6),
            Cantidad = ParseDec(TxtNewLotQty.Text),
            IdDeposito = CurrentDepositId
        };
        await _productRepository.SaveProductLotAsync(lot);
        TxtNewLotCode.Clear();
        GridAbmLotes.ItemsSource = await _productRepository.GetProductLotsAsync(_editingProduct.IdProducto, null);
        LblStatus.Text = $"✓ Lote '{loteCode}' agregado.";
    }

    private async void BtnDeleteLot_Click(object sender, RoutedEventArgs e)
    {
        if (GridAbmLotes.SelectedItem is not ProductLotEntity lot) return;
        await _productRepository.DeleteProductLotAsync(lot.IdLote);
        GridAbmLotes.ItemsSource = await _productRepository.GetProductLotsAsync(_editingProduct.IdProducto, null);
    }

    // --- Label Printing (etiprecio / codbarra3 / codbarra4) ---
    private void BtnPrintPriceLabel_Click(object sender, RoutedEventArgs e) => PrintLabels(isBarcodeMode: false);
    private void BtnPrintBarcodeLabel_Click(object sender, RoutedEventArgs e) => PrintLabels(isBarcodeMode: true);

    private void PrintLabels(bool isBarcodeMode)
    {
        int copies = int.TryParse(TxtLabelCopies.Text, out int c) ? Math.Clamp(c, 1, 500) : 1;
        bool threePerRow = Chk3EtiquetasLinea.IsChecked == true;
        var previewProd = new ProductEntity
        {
            IdProducto = _editingProduct.IdProducto,
            Codigo = TxtCodigo.Text.Trim(),
            Descripcion = TxtDescripcion.Text.Trim(),
            Precio1 = ParseDec(TxtPrecio1.Text),
            Precio2 = ParseDec(TxtPrecio2.Text),
            Precio3 = ParseDec(TxtPrecio3.Text)
        };
        BarcodeLabelPrintHelper.ShowPrintPreview(this, previewProd, copies, isBarcodeMode, threePerRow);
    }

    // --- Save Product (Full 72-column persistence + VFP validations) ---
    private async void BtnSaveProduct_Click(object sender, RoutedEventArgs e)
        => await SaveCurrentProductAsync(closeAfterCallback: true);

    private async Task<bool> SaveCurrentProductAsync(bool closeAfterCallback)
    {
        if (_lcTipo == "A" && _autoCodigoPressed)
        {
            int freshNextId = await _productRepository.GetNextProductIdAsync();
            _suppressCalc = true;
            TxtCodigo.Text = freshNextId.ToString(CultureInfo.InvariantCulture);
            if (string.IsNullOrWhiteSpace(TxtCodigoBarra.Text))
                TxtCodigoBarra.Text = freshNextId.ToString(CultureInfo.InvariantCulture);
            _suppressCalc = false;
        }

        string codigo = (TxtCodigo.Text ?? string.Empty).Trim().ToUpperInvariant();
        string desc = (TxtDescripcion.Text ?? string.Empty).Trim().ToUpperInvariant();
        decimal costo1 = ParseDec(TxtCosto1.Text);
        decimal precio1 = ParseDec(TxtPrecio1.Text);
        int idProv = CmbProveedor.SelectedValue is int pr ? pr : 0;
        int idGrp = CmbGrupo.SelectedValue is int gr ? gr : 0;

        if (string.IsNullOrEmpty(codigo) || string.IsNullOrEmpty(desc))
        {
            MessageBox.Show("El Código y la Descripción del producto son obligatorios.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        if (costo1 <= 0m)
        {
            MessageBox.Show("El Costo 1 debe ser mayor a cero.", "Validación (abm_productos)", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtCosto1.Focus();
            return false;
        }
        if (precio1 <= 0m)
        {
            MessageBox.Show("El Precio 1 debe ser mayor a cero.", "Validación (abm_productos)", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtPrecio1.Focus();
            return false;
        }
        if (idProv <= 0 && _suppliers.Count > 0)
        {
            MessageBox.Show("Seleccione un Proveedor.", "Validación (abm_productos)", MessageBoxButton.OK, MessageBoxImage.Warning);
            CmbProveedor.Focus();
            return false;
        }
        if (idGrp <= 0 && _groups.Count > 0)
        {
            MessageBox.Show("Seleccione un Grupo de Productos.", "Validación (abm_productos)", MessageBoxButton.OK, MessageBoxImage.Warning);
            CmbGrupo.Focus();
            return false;
        }

        try
        {
            var rows = GetPriceRows();
            _editingProduct.Codigo = codigo;
            _editingProduct.CodigoBarra = string.IsNullOrWhiteSpace(TxtCodigoBarra.Text) ? codigo : TxtCodigoBarra.Text.Trim();
            _editingProduct.Descripcion = desc;
            _editingProduct.IdMoneda = CmbMoneda.SelectedValue is int mon && mon > 0 ? mon : 1;
            _editingProduct.Cambio = ParseDec(TxtCambio.Text) > 0m ? ParseDec(TxtCambio.Text) : 1m;
            _editingProduct.CostoMedio = ParseDec(TxtCostoMedio.Text) > 0m ? ParseDec(TxtCostoMedio.Text) : costo1;
            _editingProduct.CostoMonedaExtranjera = ParseDec(TxtCostoMonedaExt.Text);

            _editingProduct.Costo1 = ParseDec(rows[0].Costo.Text); _editingProduct.P1 = ParseDec(rows[0].P.Text); _editingProduct.Precio1 = ParseDec(rows[0].Precio.Text);
            _editingProduct.Costo2 = ParseDec(rows[1].Costo.Text); _editingProduct.P2 = ParseDec(rows[1].P.Text); _editingProduct.Precio2 = ParseDec(rows[1].Precio.Text);
            _editingProduct.Costo3 = ParseDec(rows[2].Costo.Text); _editingProduct.P3 = ParseDec(rows[2].P.Text); _editingProduct.Precio3 = ParseDec(rows[2].Precio.Text);
            _editingProduct.Costo4 = ParseDec(rows[3].Costo.Text); _editingProduct.P4 = ParseDec(rows[3].P.Text); _editingProduct.Precio4 = ParseDec(rows[3].Precio.Text);
            _editingProduct.Costo5 = ParseDec(rows[4].Costo.Text); _editingProduct.P5 = ParseDec(rows[4].P.Text); _editingProduct.Precio5 = ParseDec(rows[4].Precio.Text);
            _editingProduct.Costo6 = ParseDec(rows[5].Costo.Text); _editingProduct.P6 = ParseDec(rows[5].P.Text); _editingProduct.Precio6 = ParseDec(rows[5].Precio.Text);
            _editingProduct.Costo7 = ParseDec(rows[6].Costo.Text); _editingProduct.P7 = ParseDec(rows[6].P.Text); _editingProduct.Precio7 = ParseDec(rows[6].Precio.Text);

            _editingProduct.IdProveedor = idProv;
            _editingProduct.IdGrupoProducto = idGrp;
            _editingProduct.IdSubGrupoProducto = CmbSubGrupo.SelectedValue is int sg ? sg : 0;
            _editingProduct.IdMarcaProducto = CmbMarca.SelectedValue is int mr ? mr : 0;
            _editingProduct.IdModeloProducto = CmbModelo.SelectedValue is int mo ? mo : 0;
            _editingProduct.Referencia = (TxtReferencia.Text ?? string.Empty).Trim().ToUpperInvariant();
            _editingProduct.Familia = (TxtFamilia.Text ?? string.Empty).Trim().ToUpperInvariant();
            _editingProduct.Aplicacion = (TxtAplicacion.Text ?? string.Empty).Trim();
            _editingProduct.Observacion = (TxtObservacion.Text ?? string.Empty).Trim();

            decimal newStock = ParseDec(TxtStockActual.Text);
            _editingProduct.StockActual = newStock;
            _editingProduct.StockMinimo = int.TryParse(TxtStockMinimo.Text, out int smin) ? smin : 0;
            _editingProduct.StockMaximo = int.TryParse(TxtStockMaximo.Text, out int smax) ? smax : 0;

            _editingProduct.Estado = RbEstInactivo.IsChecked == true ? 2 : (RbEstBloqueado.IsChecked == true ? 3 : 1);
            _editingProduct.Iva = RbIva5.IsChecked == true ? 2 : (RbIvaExenta.IsChecked == true ? 3 : 1);
            _editingProduct.Impuesto = _editingProduct.Iva == 2 ? 5 : (_editingProduct.Iva == 3 ? 0 : 10);

            _editingProduct.NoControlaStock = ChkNoControlaStock.IsChecked == true ? 1 : 0;
            _editingProduct.VendeSinStock = ChkVendeSinStock.IsChecked == true ? 1 : 0;
            _editingProduct.ImprimeFactura = ChkImprimeFactura.IsChecked == true ? 1 : 0;
            _editingProduct.AutoSuma = ChkAutoSuma.IsChecked == true ? 1 : 0;
            _editingProduct.PublicarWeb = ChkPublicarWeb.IsChecked == true ? 1 : 0;
            _editingProduct.Fraccionable = ChkFraccionable.IsChecked == true ? 1 : 0;
            _editingProduct.UsaSerial = ChkUsaSerial.IsChecked == true ? 1 : 0;
            _editingProduct.AutoCantidad = ChkAutoCantidad.IsChecked == true ? 1 : 0;
            _editingProduct.AutoPrecio = ChkAutoPrecio.IsChecked == true ? 1 : 0;
            _editingProduct.UsaVencimiento = ChkUsaVencimiento.IsChecked == true ? 1 : 0;
            _editingProduct.DescripcionEditable = ChkDescripcionEditable.IsChecked == true ? 1 : 0;
            _editingProduct.ControlaGarantia = ChkControlaGarantia.IsChecked == true ? 1 : 0;
            _editingProduct.Compuesto = ChkCompuesto.IsChecked == true ? 1 : 0;
            _editingProduct.FechaInicioGarantia = DpGarantiaInicio.SelectedDate;
            _editingProduct.DiasGarantias = int.TryParse(TxtDiasGarantia.Text, out int dgar) ? dgar : 0;
            _editingProduct.FechaFinGarantia = DpGarantiaFin.SelectedDate;

            _editingProduct.Cocina = ChkCocina.IsChecked == true ? 1 : 0;
            _editingProduct.Barra = ChkBarra.IsChecked == true ? 1 : 0;
            _editingProduct.Churrasqueria = ChkChurrasqueria.IsChecked == true ? 1 : 0;
            _editingProduct.Otros = ChkOtros.IsChecked == true ? 1 : 0;
            _editingProduct.Guarnicion = ChkGuarnicion.IsChecked == true ? 1 : 0;
            _editingProduct.Menu = ChkMenu.IsChecked == true ? 1 : 0;
            _editingProduct.Comanda = ChkComanda.IsChecked == true;
            _editingProduct.MuestraBusqueda = 1;

            bool wasModify = string.Equals(_lcTipo, "M", StringComparison.OrdinalIgnoreCase) && _editingProduct.IdProducto > 0;
            int savedId = await _productRepository.SaveProductFullAsync(_editingProduct, _lcTipo, CurrentDepositId, CurrentUsername);

            if (wasModify && Math.Abs(newStock - _originalDepositStock) > 0.0001m)
            {
                await _productRepository.AdjustStockAsync(
                    savedId, CurrentDepositId, _editingProduct.Codigo, newStock, "AJUSTE FICHA ABM", CurrentUsername);
            }

            _originalDepositStock = newStock;
            _lcTipo = "M";
            _autoCodigoPressed = false;
            _editingProduct.IdProducto = savedId;
            TxtIdProducto.Text = savedId.ToString(CultureInfo.InvariantCulture);
            BtnAutoCodigo.Visibility = Visibility.Collapsed;
            TxtAbmTitle.Text = $"EDITANDO PRODUCTO #{savedId} ({_editingProduct.Codigo})";
            LblStatus.Text = $"✓ Producto '{_editingProduct.Descripcion}' guardado exitosamente (ID #{savedId}).";
            GridAbmStockDepositos.ItemsSource = await _productRepository.GetProductStockByDepositsAsync(savedId, _editingProduct.Codigo);
            await RunSearchAsync();

            if (closeAfterCallback && _onProductCodeSelected != null)
            {
                var cb = _onProductCodeSelected;
                _onProductCodeSelected = null;
                if (_windowManager != null)
                    _windowManager.SafeHideAndRestoreFocus(this);
                else
                    Hide();
                cb(_editingProduct.Codigo);
            }

            return true;
        }
        catch (Exception ex)
        {
            LblStatus.Text = $"Error al guardar producto: {ex.Message}";
            MessageBox.Show($"Error al guardar producto:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private async void BtnQuickAdjustPriceStock_Click(object sender, RoutedEventArgs e)
    {
        if (_editingProduct.IdProducto <= 0)
        {
            LblStatus.Text = "Seleccione primero un producto existente.";
            return;
        }

        try
        {
            await _productRepository.UpdateProductPriceAsync(
                _editingProduct.IdProducto,
                ParseDec(TxtCosto1.Text),
                ParseDec(TxtPrecio1.Text),
                ParseDec(TxtPrecio2.Text),
                ParseDec(TxtPrecio3.Text),
                ParseDec(TxtStockActual.Text),
                CurrentDepositId);
            _originalDepositStock = ParseDec(TxtStockActual.Text);
            LblStatus.Text = $"✓ Precios y stock actualizados para '{_editingProduct.Descripcion}'.";
            await RunSearchAsync();
        }
        catch (Exception ex)
        {
            LblStatus.Text = $"Error al actualizar precios/stock: {ex.Message}";
        }
    }

    private async void BtnDeleteProduct_Click(object sender, RoutedEventArgs e)
    {
        if (_editingProduct.IdProducto <= 0) return;
        if (MessageBox.Show($"¿Dar de baja al producto '{_editingProduct.Descripcion}'?", "Confirmar Baja",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        await _productRepository.DeleteProductAsync(_editingProduct.IdProducto);
        LblStatus.Text = $"✓ Producto #{_editingProduct.IdProducto} dado de baja.";
        await RunSearchAsync();
        MainTabs.SelectedIndex = 0;
    }

    private async void BtnReactivateAbm_Click(object sender, RoutedEventArgs e)
    {
        if (_editingProduct.IdProducto <= 0) return;
        await _productRepository.ReactivateProductAsync(_editingProduct.IdProducto, CurrentUsername);
        RbEstActivo.IsChecked = true;
        BtnReactivateAbm.Visibility = Visibility.Collapsed;
        LblStatus.Text = $"✓ Producto #{_editingProduct.IdProducto} reactivado.";
        await RunSearchAsync();
    }

    // =========================================================================
    // TAB 2: CATÁLOGOS AUXILIARES CRUD
    // =========================================================================
    private void GridAuxGrupos_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GridAuxGrupos.SelectedItem is not ProductGroupEntity g) return;
        _editingAuxGrupo = g;
        TxtAuxGrupoDesc.Text = g.Descripcion;
        TxtAuxGrupoDto.Text = g.Descuento.ToString("0.##", CultureInfo.InvariantCulture);
        TxtAuxGrupoImp.Text = g.Impresora;
        DpAuxGrupoIni.SelectedDate = g.FechaInicio;
        DpAuxGrupoFin.SelectedDate = g.FechaFinal;
    }

    private void BtnAuxGrupoNew_Click(object sender, RoutedEventArgs e)
    {
        _editingAuxGrupo = new ProductGroupEntity();
        TxtAuxGrupoDesc.Clear();
        TxtAuxGrupoDto.Text = "0";
        TxtAuxGrupoImp.Clear();
        DpAuxGrupoIni.SelectedDate = null;
        DpAuxGrupoFin.SelectedDate = null;
        TxtAuxGrupoDesc.Focus();
    }

    private async void BtnAuxGrupoSave_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _editingAuxGrupo.Descripcion = TxtAuxGrupoDesc.Text.Trim();
            _editingAuxGrupo.Descuento = ParseDec(TxtAuxGrupoDto.Text);
            _editingAuxGrupo.Impresora = TxtAuxGrupoImp.Text.Trim();
            _editingAuxGrupo.FechaInicio = DpAuxGrupoIni.SelectedDate;
            _editingAuxGrupo.FechaFinal = DpAuxGrupoFin.SelectedDate;
            await _productRepository.SaveProductGroupAsync(_editingAuxGrupo, _editingAuxGrupo.IdGrupoProducto > 0 ? "M" : "A");
            await LoadLookupListsAsync();
            LblStatus.Text = $"✓ Grupo '{_editingAuxGrupo.Descripcion}' guardado.";
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Grupo", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private async void BtnAuxGrupoDelete_Click(object sender, RoutedEventArgs e)
    {
        if (_editingAuxGrupo.IdGrupoProducto <= 0) return;
        if (MessageBox.Show($"¿Eliminar grupo '{_editingAuxGrupo.Descripcion}'?", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            await _productRepository.DeleteProductGroupAsync(_editingAuxGrupo.IdGrupoProducto);
            BtnAuxGrupoNew_Click(sender, e);
            await LoadLookupListsAsync();
        }
    }

    private void GridAuxSubGrupos_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GridAuxSubGrupos.SelectedItem is not ProductSubGroupEntity g) return;
        _editingAuxSubGrupo = g;
        TxtAuxSubGrupoDesc.Text = g.Descripcion;
    }
    private void BtnAuxSubGrupoNew_Click(object sender, RoutedEventArgs e) { _editingAuxSubGrupo = new(); TxtAuxSubGrupoDesc.Clear(); TxtAuxSubGrupoDesc.Focus(); }
    private async void BtnAuxSubGrupoSave_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _editingAuxSubGrupo.Descripcion = TxtAuxSubGrupoDesc.Text.Trim();
            await _productRepository.SaveProductSubGroupAsync(_editingAuxSubGrupo, _editingAuxSubGrupo.IdSubGrupoProducto > 0 ? "M" : "A");
            await LoadLookupListsAsync();
            LblStatus.Text = "✓ Subgrupo guardado.";
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Subgrupo", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
    private async void BtnAuxSubGrupoDelete_Click(object sender, RoutedEventArgs e)
    {
        if (_editingAuxSubGrupo.IdSubGrupoProducto <= 0) return;
        if (MessageBox.Show($"¿Eliminar subgrupo '{_editingAuxSubGrupo.Descripcion}'?", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            await _productRepository.DeleteProductSubGroupAsync(_editingAuxSubGrupo.IdSubGrupoProducto);
            BtnAuxSubGrupoNew_Click(sender, e);
            await LoadLookupListsAsync();
        }
    }

    private void GridAuxMarcas_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GridAuxMarcas.SelectedItem is not ProductBrandEntity b) return;
        _editingAuxMarca = b;
        TxtAuxMarcaDesc.Text = b.Descripcion;
    }
    private void BtnAuxMarcaNew_Click(object sender, RoutedEventArgs e) { _editingAuxMarca = new(); TxtAuxMarcaDesc.Clear(); TxtAuxMarcaDesc.Focus(); }
    private async void BtnAuxMarcaSave_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _editingAuxMarca.Descripcion = TxtAuxMarcaDesc.Text.Trim();
            await _productRepository.SaveProductBrandAsync(_editingAuxMarca, _editingAuxMarca.IdMarcaProducto > 0 ? "M" : "A");
            await LoadLookupListsAsync();
            LblStatus.Text = "✓ Marca guardada.";
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Marca", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
    private async void BtnAuxMarcaDelete_Click(object sender, RoutedEventArgs e)
    {
        if (_editingAuxMarca.IdMarcaProducto <= 0) return;
        if (MessageBox.Show($"¿Eliminar marca '{_editingAuxMarca.Descripcion}'?", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            await _productRepository.DeleteProductBrandAsync(_editingAuxMarca.IdMarcaProducto);
            BtnAuxMarcaNew_Click(sender, e);
            await LoadLookupListsAsync();
        }
    }

    private void GridAuxModelos_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GridAuxModelos.SelectedItem is not ProductModelEntity m) return;
        _editingAuxModelo = m;
        TxtAuxModeloDesc.Text = m.Descripcion;
    }
    private void BtnAuxModeloNew_Click(object sender, RoutedEventArgs e) { _editingAuxModelo = new(); TxtAuxModeloDesc.Clear(); TxtAuxModeloDesc.Focus(); }
    private async void BtnAuxModeloSave_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _editingAuxModelo.Descripcion = TxtAuxModeloDesc.Text.Trim();
            await _productRepository.SaveProductModelAsync(_editingAuxModelo, _editingAuxModelo.IdModeloProducto > 0 ? "M" : "A");
            await LoadLookupListsAsync();
            LblStatus.Text = "✓ Modelo guardado.";
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Modelo", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
    private async void BtnAuxModeloDelete_Click(object sender, RoutedEventArgs e)
    {
        if (_editingAuxModelo.IdModeloProducto <= 0) return;
        if (MessageBox.Show($"¿Eliminar modelo '{_editingAuxModelo.Descripcion}'?", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            await _productRepository.DeleteProductModelAsync(_editingAuxModelo.IdModeloProducto);
            BtnAuxModeloNew_Click(sender, e);
            await LoadLookupListsAsync();
        }
    }

    private void GridAuxDepositos_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GridAuxDepositos.SelectedItem is not DepositEntity d) return;
        _editingAuxDeposito = d;
        TxtAuxDepositoDesc.Text = d.Descripcion;
        TxtAuxDepositoResp.Text = d.Responsable;
        TxtAuxDepositoSuc.Text = d.IdSucursal.ToString(CultureInfo.InvariantCulture);
        ChkAuxDepositoActivo.IsChecked = d.Estado == 1;
    }
    private void BtnAuxDepositoNew_Click(object sender, RoutedEventArgs e)
    {
        _editingAuxDeposito = new DepositEntity { Estado = 1, IdSucursal = 1 };
        TxtAuxDepositoDesc.Clear();
        TxtAuxDepositoResp.Clear();
        TxtAuxDepositoSuc.Text = "1";
        ChkAuxDepositoActivo.IsChecked = true;
        TxtAuxDepositoDesc.Focus();
    }
    private async void BtnAuxDepositoSave_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _editingAuxDeposito.Descripcion = TxtAuxDepositoDesc.Text.Trim();
            _editingAuxDeposito.Responsable = TxtAuxDepositoResp.Text.Trim();
            _editingAuxDeposito.IdSucursal = int.TryParse(TxtAuxDepositoSuc.Text, out int suc) && suc > 0 ? suc : 1;
            _editingAuxDeposito.Estado = ChkAuxDepositoActivo.IsChecked == true ? 1 : 0;
            await _productRepository.SaveDepositAsync(_editingAuxDeposito, _editingAuxDeposito.IdDeposito > 0 ? "M" : "A");
            await LoadLookupListsAsync();
            LblStatus.Text = "✓ Depósito guardado.";
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Depósito", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
    private async void BtnAuxDepositoDelete_Click(object sender, RoutedEventArgs e)
    {
        if (_editingAuxDeposito.IdDeposito <= 0) return;
        if (MessageBox.Show($"¿Dar de baja el depósito '{_editingAuxDeposito.Descripcion}'?", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            await _productRepository.DeleteDepositAsync(_editingAuxDeposito.IdDeposito);
            BtnAuxDepositoNew_Click(sender, e);
            await LoadLookupListsAsync();
        }
    }

    // =========================================================================
    // SHORTCUTS & SHARED FOOTER ACTIONS
    // =========================================================================
    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    private void BtnOpenCargaRapida_Click(object sender, RoutedEventArgs e) => _windowManager?.OpenCargaRapidaProductos();

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
            return;
        }

        if (e.Key == Key.F9)
        {
            e.Handled = true;
            if (MainTabs.SelectedIndex == 1)
                _ = SaveCurrentProductAsync(closeAfterCallback: true);
            else
                _windowManager?.OpenCargaRapidaProductos();
            return;
        }

        if (e.Key == Key.F4 && MainTabs.SelectedIndex == 1)
        {
            e.Handled = true;
            _ = SaveCurrentProductAsync(closeAfterCallback: true);
            return;
        }

        if (MainTabs.SelectedIndex == 0)
        {
            if (e.Key == Key.F3)
            {
                e.Handled = true;
                TxtSearch.Focus();
                TxtSearch.SelectAll();
            }
            else if (e.Key == Key.F5)
            {
                e.Handled = true;
                BtnNewProduct_Click(sender, new RoutedEventArgs());
            }
            else if (e.Key == Key.F6)
            {
                e.Handled = true;
                BtnEditSelectedProduct_Click(sender, new RoutedEventArgs());
            }
            else if (e.Key == Key.F7)
            {
                e.Handled = true;
                BtnDeleteFromCatalog_Click(sender, new RoutedEventArgs());
            }
            else if (e.Key == Key.Enter && GridProducts.IsKeyboardFocusWithin && GridProducts.SelectedItem is ProductEntity prod)
            {
                e.Handled = true;
                SelectOrOpenForEdit(prod);
            }
        }
    }

    private static decimal ParseDec(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0m;
        string clean = s.Trim().Replace(" ", "").Replace(",", ".");
        return decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal d) ? d : 0m;
    }
}
