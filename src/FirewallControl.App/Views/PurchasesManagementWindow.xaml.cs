using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FirewallControl.App.Services;
using FirewallControl.Core.Entities;
using FirewallControl.Core.Interfaces;

namespace FirewallControl.App.Views;

/// <summary>
/// Full implementation of abm_compras.scx (Purchase Invoice POS/ABM), itemsfacturacompras.scx launcher,
/// crear_compras.scx (Purchase History), and crear_proveedores / abm_proveedores.scx.
/// </summary>
public partial class PurchasesManagementWindow : Window
{
    private readonly IPurchaseRepository _purchaseRepository;
    private readonly IProductRepository _productRepository;
    private readonly ISystemRepository _systemRepository;
    private readonly IWindowManagerService _windowManager;
    private readonly IConfigService _configService;

    private readonly ObservableCollection<PurchaseItemEntity> _currentItems = new();
    private SupplierEntity? _selectedSupplier;

    public PurchasesManagementWindow(
        IPurchaseRepository purchaseRepository,
        IProductRepository productRepository,
        ISystemRepository systemRepository,
        IWindowManagerService windowManager,
        IConfigService configService,
        int initialTab = 0)
    {
        InitializeComponent();
        _purchaseRepository = purchaseRepository;
        _productRepository = productRepository;
        _systemRepository = systemRepository;
        _windowManager = windowManager;
        _configService = configService;

        MainTabs.SelectedIndex = Math.Clamp(initialTab, 0, 2);
        GridCurrentPurchaseItems.ItemsSource = _currentItems;

        DpPurFecha.SelectedDate = DateTime.Today;
        DpHistDesde.SelectedDate = DateTime.Today.AddDays(-60);
        DpHistHasta.SelectedDate = DateTime.Today;

        Loaded += async (_, _) =>
        {
            await LoadDepositsAsync();
            await LoadSuppliersAsync();
            await ResetPurchaseHeaderAsync();
            await LoadPurchasesAsync();
        };
    }

    public void Configure(int initialTab = 0)
    {
        MainTabs.SelectedIndex = Math.Clamp(initialTab, 0, 2);
    }

    private async Task LoadDepositsAsync()
    {
        try
        {
            var deposits = await _productRepository.GetDepositsAsync();
            if (deposits.Count == 0)
            {
                deposits.Add(new DepositEntity { IdDeposito = 1, Descripcion = "DEPOSITO CENTRAL" });
            }
            CmbPurDeposito.ItemsSource = deposits;
            int defDep = _configService.Config.IdDeposito > 0 ? _configService.Config.IdDeposito : 1;
            CmbPurDeposito.SelectedValue = defDep;
            if (CmbPurDeposito.SelectedIndex < 0)
                CmbPurDeposito.SelectedIndex = 0;
        }
        catch
        {
            // Ignore
        }
    }

    private async Task ResetPurchaseHeaderAsync()
    {
        try
        {
            int nextNro = await _purchaseRepository.GetNextNroCompraAsync();
            TxtPurNroCompra.Text = nextNro.ToString();
        }
        catch
        {
            TxtPurNroCompra.Text = "1";
        }

        DpPurFecha.SelectedDate = DateTime.Today;
        TxtPurDescuento.Text = "0";
        _currentItems.Clear();
        RecalculatePurchaseTotals();
    }

    private async Task LoadSuppliersAsync()
    {
        try
        {
            var list = await _purchaseRepository.SearchSuppliersAsync(TxtSearchSupplier.Text, 200);
            GridSuppliers.ItemsSource = list;
            CmbPurSupplier.ItemsSource = list;
            if (list.Count > 0 && CmbPurSupplier.SelectedIndex < 0)
            {
                CmbPurSupplier.SelectedIndex = 0;
            }
            TxtStatus.Text = $"Proveedores cargados: {list.Count}. Presione [F2] para agregar ítems a la factura de compra.";
        }
        catch (Exception ex)
        {
            TxtStatus.Text = $"Error al cargar proveedores: {ex.Message}";
        }
    }

    private async Task LoadPurchasesAsync()
    {
        try
        {
            var list = await _purchaseRepository.GetRecentPurchasesAsync(DpHistDesde.SelectedDate, DpHistHasta.SelectedDate, 150);
            GridPurchases.ItemsSource = list;
        }
        catch (Exception ex)
        {
            TxtStatus.Text = $"Error al cargar compras: {ex.Message}";
        }
    }

    private void CmbPurSupplier_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbPurSupplier.SelectedItem is SupplierEntity sup && !string.IsNullOrWhiteSpace(sup.Timbrado))
        {
            TxtPurTimbrado.Text = sup.Timbrado;
        }
    }

    private void BtnGoToSuppliers_Click(object sender, RoutedEventArgs e)
    {
        MainTabs.SelectedIndex = 2;
        BtnNewSupplier_Click(sender, e);
    }

    // =====================================================================
    // ABM_COMPRAS.SCX + ITEMSFACTURACOMPRAS.SCX OPERATIONS
    // =====================================================================
    private int GetSelectedDepositoId()
    {
        if (CmbPurDeposito.SelectedValue is int id && id > 0)
            return id;
        return 1;
    }

    private void BtnAddItemModal_Click(object sender, RoutedEventArgs e)
    {
        OpenItemsFacturaComprasModal("A", null);
    }

    private void BtnEditItemModal_Click(object sender, RoutedEventArgs e)
    {
        if (GridCurrentPurchaseItems.SelectedItem is not PurchaseItemEntity selected)
        {
            MessageBox.Show("Seleccione un ítem de la grilla para modificar.", "Modificar Ítem", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        OpenItemsFacturaComprasModal("M", selected);
    }

    private void GridCurrentPurchaseItems_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (GridCurrentPurchaseItems.SelectedItem is PurchaseItemEntity selected)
        {
            OpenItemsFacturaComprasModal("M", selected);
        }
    }

    private async void TxtQuickScanCode_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        e.Handled = true;
        string code = TxtQuickScanCode.Text.Trim();
        if (string.IsNullOrWhiteSpace(code))
        {
            OpenItemsFacturaComprasModal("A", null);
            return;
        }

        int depId = GetSelectedDepositoId();
        var prod = await _productRepository.GetByCodeAsync(code, depId);
        if (prod == null)
        {
            TxtStatus.Text = $"⚠ Producto '{code}' no encontrado. Abriendo buscador ITEMSFACTURACOMPRAS...";
            OpenItemsFacturaComprasModal("A", null);
            return;
        }

        TxtQuickScanCode.Clear();
        var seed = new PurchaseItemEntity
        {
            IdProducto = prod.IdProducto,
            Codigo = prod.Codigo,
            Descripcion = prod.Descripcion,
            Cantidad = 1m,
            PreUnitario = prod.Costo1 > 0 ? prod.Costo1 : prod.CostoMedio,
            Iva = prod.Iva
        };
        OpenItemsFacturaComprasModal("A", seed, preloadInAddMode: true);
    }

    private void OpenItemsFacturaComprasModal(string lcTipo, PurchaseItemEntity? item, bool preloadInAddMode = false)
    {
        int depId = GetSelectedDepositoId();
        var dlg = new ItemsFacturaComprasWindow(
            _productRepository,
            _systemRepository,
            _windowManager,
            depId,
            preloadInAddMode ? "M" : lcTipo,
            item)
        {
            Owner = this
        };

        if (dlg.ShowDialog() == true && dlg.ResultItem != null)
        {
            var result = dlg.ResultItem;
            if (lcTipo == "M" && !preloadInAddMode && item != null)
            {
                int idx = _currentItems.IndexOf(item);
                if (idx >= 0)
                {
                    result.RecNo = item.RecNo;
                    _currentItems[idx] = result;
                }
            }
            else
            {
                result.RecNo = _currentItems.Count + 1;
                _currentItems.Add(result);
            }

            RenumberPurchaseItems();
            RecalculatePurchaseTotals();
            GridCurrentPurchaseItems.SelectedItem = result;
            GridCurrentPurchaseItems.UpdateLayout();
            GridCurrentPurchaseItems.ScrollIntoView(result);
            TxtStatus.Text = $"✓ Ítem {(lcTipo == "M" && !preloadInAddMode ? "modificado" : "agregado")}: {result.Cantidad:N2} x {result.Descripcion} = {result.PrecioTotal:N0} Gs.";
        }
    }

    private void BtnDeleteItem_Click(object sender, RoutedEventArgs e)
    {
        if (GridCurrentPurchaseItems.SelectedItem is not PurchaseItemEntity selected)
        {
            MessageBox.Show("Seleccione un ítem de la grilla para borrar.", "Borrar Ítem", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _currentItems.Remove(selected);
        RenumberPurchaseItems();
        RecalculatePurchaseTotals();
        TxtStatus.Text = $"Ítem '{selected.Descripcion}' quitado de la factura de compra.";
    }

    private async void BtnResetPurchase_Click(object sender, RoutedEventArgs e)
    {
        await ResetPurchaseHeaderAsync();
        TxtStatus.Text = "Nueva factura de compra lista.";
    }

    private void RenumberPurchaseItems()
    {
        for (int i = 0; i < _currentItems.Count; i++)
        {
            _currentItems[i].RecNo = i + 1;
        }
        GridCurrentPurchaseItems.Items.Refresh();
    }

    private void TxtPurDescuento_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        RecalculatePurchaseTotals();
    }

    /// <summary>
    /// Exact replication of sumarproductos() in abm_compras.scx:
    /// SUM pre_exenta, pre_iva5, pre_iva10
    /// liquidacion_iva5 = total_iva5 / 21 (or 22 in VFP)
    /// liquidacion_iva10 = total_iva10 / 11
    /// total_compra = subtotal - descuento
    /// </summary>
    private void RecalculatePurchaseTotals()
    {
        decimal totExenta = _currentItems.Sum(x => x.PreExenta);
        decimal totIva5 = _currentItems.Sum(x => x.PreIva5);
        decimal totIva10 = _currentItems.Sum(x => x.PreIva10);
        decimal subtotal = totExenta + totIva5 + totIva10;
        decimal desc = ParseDec(TxtPurDescuento.Text);
        if (desc < 0) desc = 0;
        if (desc > subtotal) desc = subtotal;

        decimal liq5 = Math.Round(totIva5 / 21m, 0, MidpointRounding.AwayFromZero);
        decimal liq10 = Math.Round(totIva10 / 11m, 0, MidpointRounding.AwayFromZero);
        decimal totImp = liq5 + liq10;
        decimal totalCompra = subtotal - desc;

        if (TxtTotExenta != null) TxtTotExenta.Text = $"{totExenta:N0} Gs.";
        if (TxtTotIva5 != null) TxtTotIva5.Text = $"{totIva5:N0} Gs.";
        if (TxtTotIva10 != null) TxtTotIva10.Text = $"{totIva10:N0} Gs.";
        if (TxtTotImpuesto != null) TxtTotImpuesto.Text = $"{totImp:N0} Gs.";
        if (TxtPurTotalGeneral != null) TxtPurTotalGeneral.Text = $"{totalCompra:N0} Gs.";
    }

    private async void BtnSavePurchase_Click(object sender, RoutedEventArgs e)
    {
        if (CmbPurSupplier.SelectedItem is not SupplierEntity sup)
        {
            MessageBox.Show("Seleccione un proveedor antes de grabar la factura de compra.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_currentItems.Count == 0)
        {
            MessageBox.Show("Agregue al menos un ítem a la factura de compra con [F2].", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        decimal totExenta = _currentItems.Sum(x => x.PreExenta);
        decimal totIva5 = _currentItems.Sum(x => x.PreIva5);
        decimal totIva10 = _currentItems.Sum(x => x.PreIva10);
        decimal subtotal = totExenta + totIva5 + totIva10;
        decimal desc = Math.Clamp(ParseDec(TxtPurDescuento.Text), 0, subtotal);
        decimal totalCompra = subtotal - desc;
        decimal liq5 = Math.Round(totIva5 / 21m, 0, MidpointRounding.AwayFromZero);
        decimal liq10 = Math.Round(totIva10 / 11m, 0, MidpointRounding.AwayFromZero);

        int formaPago = 1;
        if (CmbPurCondicion.SelectedItem is ComboBoxItem cbi && int.TryParse(cbi.Tag?.ToString(), out int fp))
            formaPago = fp;

        var purchase = new PurchaseEntity
        {
            NroCompra = int.TryParse(TxtPurNroCompra.Text, out int nc) ? nc : 0,
            NroFactura = string.IsNullOrWhiteSpace(TxtPurFactura.Text) ? "001-001-0000001" : TxtPurFactura.Text.Trim(),
            Timbrado = string.IsNullOrWhiteSpace(TxtPurTimbrado.Text) ? sup.Timbrado : TxtPurTimbrado.Text.Trim(),
            FechaCompra = DpPurFecha.SelectedDate ?? DateTime.Today,
            FormaPago = formaPago,
            IdDeposito = GetSelectedDepositoId(),
            IdCaja = _configService.Config.IdCaja > 0 ? _configService.Config.IdCaja : 1,
            IdSucursal = _configService.Config.IdSucursal > 0 ? _configService.Config.IdSucursal : 1,
            IdProveedor = sup.IdProveedor,
            NombreProveedor = sup.Descripcion,
            TotalExenta = totExenta,
            TotalIva5 = totIva5,
            TotalIva10 = totIva10,
            Subtotal = subtotal,
            Descuento = desc,
            TotalCompra = totalCompra,
            LiquidacionIva5 = liq5,
            LiquidacionIva10 = liq10,
            TotalImpuesto = liq5 + liq10,
            CidUsuario = _windowManager.CurrentUser.Cidusuario,
            Items = _currentItems.ToList()
        };

        try
        {
            int idCompra = await _purchaseRepository.ProcessPurchaseTransactionAsync(purchase);
            TxtStatus.Text = $"✓ Compra #{purchase.NroCompra} (ID {idCompra}) grabada por {totalCompra:N0} Gs. Stock, costos y precios actualizados.";
            MessageBox.Show(
                $"Factura de Compra #{purchase.NroCompra} grabada exitosamente.\n\n" +
                $"• Proveedor: {sup.Descripcion}\n" +
                $"• Factura: {purchase.NroFactura}\n" +
                $"• Ítems cargados: {purchase.Items.Count}\n" +
                $"• Total Compra: {totalCompra:N0} Gs.\n\n" +
                "Se incrementó el stock en depósito y se actualizaron los costos y precios de venta.",
                "Compra Registrada (ABM_COMPRAS)",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            await ResetPurchaseHeaderAsync();
            await LoadPurchasesAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al grabar factura de compra:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // =====================================================================
    // HISTORIAL DE COMPRAS (CREAR_COMPRAS.SCX)
    // =====================================================================
    private async void BtnFilterPurchases_Click(object sender, RoutedEventArgs e) => await LoadPurchasesAsync();

    private async void BtnViewPurchaseDetail_Click(object sender, RoutedEventArgs e)
    {
        if (GridPurchases.SelectedItem is not PurchaseEntity selected)
        {
            MessageBox.Show("Seleccione una compra del historial.", "Detalle de Compra", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            var full = await _purchaseRepository.GetPurchaseByIdAsync(selected.IdCompra);
            if (full == null)
                return;

            string lines = string.Join("\n", full.Items.Select(i =>
                $"• {i.Cantidad:N2} x [{i.Codigo}] {i.Descripcion} @ {i.PreUnitario:N0} Gs. = {i.PrecioTotal:N0} Gs."));

            MessageBox.Show(
                $"COMPRA #{full.NroCompra} (ID: {full.IdCompra})\n" +
                $"Factura: {full.NroFactura} | Timbrado: {full.Timbrado}\n" +
                $"Fecha: {full.FechaCompra:dd/MM/yyyy} | Proveedor: {full.NombreProveedor}\n" +
                $"------------------------------------------------------------\n" +
                $"{(string.IsNullOrWhiteSpace(lines) ? "(Sin detalle de ítems)" : lines)}\n" +
                $"------------------------------------------------------------\n" +
                $"Total Exentas: {full.TotalExenta:N0} Gs.\n" +
                $"Total Grav. 5%: {full.TotalIva5:N0} Gs.\n" +
                $"Total Grav. 10%: {full.TotalIva10:N0} Gs.\n" +
                $"TOTAL COMPRA: {full.TotalCompra:N0} Gs.",
                $"Detalle de Factura de Compra #{full.NroCompra}",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al consultar detalle:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnDeletePurchase_Click(object sender, RoutedEventArgs e)
    {
        if (GridPurchases.SelectedItem is not PurchaseEntity selected)
        {
            MessageBox.Show("Seleccione una compra para eliminar.", "Eliminar Compra", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (MessageBox.Show(
                $"¿Confirma eliminar la Compra #{selected.NroCompra} (Factura {selected.NroFactura}) por {selected.TotalCompra:N0} Gs.?\n\n" +
                "Se descontará del depósito el stock ingresado por esta compra.",
                "Confirmar Eliminación de Compra",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await _purchaseRepository.DeletePurchaseAsync(selected.IdCompra);
            await LoadPurchasesAsync();
            TxtStatus.Text = $"✓ Compra #{selected.NroCompra} eliminada y stock revertido.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al eliminar compra:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // =====================================================================
    // PROVEEDORES (ABM_PROVEEDORES.SCX)
    // =====================================================================
    private async void BtnSearchSupplier_Click(object sender, RoutedEventArgs e) => await LoadSuppliersAsync();

    private async void TxtSearchSupplier_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            await LoadSuppliersAsync();
        }
    }

    private void GridSuppliers_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GridSuppliers.SelectedItem is SupplierEntity s)
        {
            _selectedSupplier = s;
            TxtSupFormTitle.Text = $"EDITANDO PROVEEDOR #{s.IdProveedor}";
            TxtSupRuc.Text = s.Ruc;
            TxtSupNombre.Text = s.Descripcion;
            TxtSupContacto.Text = s.Contacto;
            TxtSupTel.Text = s.Telefono;
            TxtSupTimbrado.Text = s.Timbrado;
            TxtSupDir.Text = s.Direccion;
        }
    }

    private void BtnNewSupplier_Click(object sender, RoutedEventArgs e)
    {
        _selectedSupplier = null;
        TxtSupFormTitle.Text = "NUEVO PROVEEDOR";
        TxtSupRuc.Clear();
        TxtSupNombre.Clear();
        TxtSupContacto.Clear();
        TxtSupTel.Clear();
        TxtSupTimbrado.Clear();
        TxtSupDir.Clear();
        TxtSupRuc.Focus();
    }

    private async void BtnSaveSupplier_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtSupNombre.Text))
        {
            MessageBox.Show("Ingrese la Razón Social del proveedor.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string lcTipo = _selectedSupplier != null && _selectedSupplier.IdProveedor > 0 ? "M" : "A";
        var entity = _selectedSupplier ?? new SupplierEntity();
        entity.Ruc = TxtSupRuc.Text.Trim();
        entity.Descripcion = TxtSupNombre.Text.Trim().ToUpperInvariant();
        entity.Contacto = TxtSupContacto.Text.Trim().ToUpperInvariant();
        entity.Telefono = TxtSupTel.Text.Trim();
        entity.Timbrado = TxtSupTimbrado.Text.Trim();
        entity.Direccion = TxtSupDir.Text.Trim().ToUpperInvariant();
        entity.Estado = 1;

        try
        {
            await _purchaseRepository.SaveSupplierAsync(entity, lcTipo);
            await LoadSuppliersAsync();
            CmbPurSupplier.SelectedValue = entity.IdProveedor;
            TxtStatus.Text = $"✓ Proveedor '{entity.Descripcion}' guardado correctamente.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al guardar proveedor:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnDeleteSupplier_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSupplier == null || _selectedSupplier.IdProveedor <= 0)
            return;

        if (MessageBox.Show($"¿Dar de baja al proveedor '{_selectedSupplier.Descripcion}'?", "Confirmar",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            await _purchaseRepository.DeleteSupplierAsync(_selectedSupplier.IdProveedor);
            BtnNewSupplier_Click(sender, e);
            await LoadSuppliersAsync();
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
            return;
        }

        if (MainTabs.SelectedIndex == 0)
        {
            if (e.Key == Key.F2)
            {
                e.Handled = true;
                BtnAddItemModal_Click(this, new RoutedEventArgs());
            }
            else if (e.Key == Key.F4)
            {
                e.Handled = true;
                BtnSavePurchase_Click(this, new RoutedEventArgs());
            }
            else if (e.Key == Key.Delete && Keyboard.FocusedElement is not TextBox)
            {
                e.Handled = true;
                BtnDeleteItem_Click(this, new RoutedEventArgs());
            }
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
