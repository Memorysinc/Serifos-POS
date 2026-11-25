using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using FirewallControl.App.Services;
using FirewallControl.Core.Business;
using FirewallControl.Core.Entities;
using FirewallControl.Core.Interfaces;

namespace FirewallControl.App.Views;

/// <summary>
/// Unifies crear_pedidos, crear_presupuestos, crear_devolucionventas, crear_devolucioncompras,
/// crear_pagoproveedores, crear_cuentapagar, crear_cobroclientes_cuotas, crear_controlmorosos,
/// crear_cuentarecibir, crear_solicitudcredito, crear_solicitudcredito_admin, and informe_solicitudcredito.
/// </summary>
public partial class CommercialOperationsWindow : Window
{
    private readonly IStage3Repository _stage3Repository;
    private readonly ISalesRepository _salesRepository;
    private readonly IPurchaseRepository _purchaseRepository;
    private readonly IProductRepository _productRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly ICashRepository _cashRepository;
    private readonly ISystemRepository _systemRepository;
    private readonly IWindowManagerService _windowManager;

    private readonly ObservableCollection<PurchaseOrderItemEntity> _pedItems = new();
    private readonly ObservableCollection<BudgetItemEntity> _preItems = new();
    private readonly ObservableCollection<ReturnItemEntity> _dvItems = new();
    private readonly ObservableCollection<ReturnItemEntity> _dcItems = new();

    private ProductEntity? _pedSelectedProd;
    private ProductEntity? _preSelectedProd;
    private BudgetSummaryEntity _editingBudget = new();
    private SaleEntity? _loadedDvSale;
    private PurchaseEntity? _loadedDcPurchase;
    private SupplierDebtEntity? _selectedDebt;
    private CreditInstallmentRow? _selectedInstallment;
    private CreditApplicationEntity _editingSol = new();
    private bool _adminCreditMode;

    public CommercialOperationsWindow(
        IStage3Repository stage3Repository,
        ISalesRepository salesRepository,
        IPurchaseRepository purchaseRepository,
        IProductRepository productRepository,
        ICustomerRepository customerRepository,
        ICashRepository cashRepository,
        ISystemRepository systemRepository,
        IWindowManagerService windowManager)
    {
        InitializeComponent();
        _stage3Repository = stage3Repository;
        _salesRepository = salesRepository;
        _purchaseRepository = purchaseRepository;
        _productRepository = productRepository;
        _customerRepository = customerRepository;
        _cashRepository = cashRepository;
        _systemRepository = systemRepository;
        _windowManager = windowManager;

        GridPedItems.ItemsSource = _pedItems;
        GridPreItems.ItemsSource = _preItems;
        GridDvItems.ItemsSource = _dvItems;
        GridDcItems.ItemsSource = _dcItems;

        DpCpVenc.SelectedDate = DateTime.Today.AddDays(30);
        DpCrVenc.SelectedDate = DateTime.Today.AddDays(30);

        Loaded += async (_, _) => await LoadAllAsync();
    }

    public void Configure(int initialTab, bool adminCreditMode = false, bool onlyOverdue = false)
    {
        MainTabs.SelectedIndex = Math.Clamp(initialTab, 0, 6);
        _adminCreditMode = adminCreditMode;
        ChkSoloMorosos.IsChecked = onlyOverdue;
        bool isAdmin = _adminCreditMode || VfpPermissionHelper.IsAdmin(_windowManager.CurrentUser, _windowManager.CurrentUserGroup);
        PanelSolAdmin.IsEnabled = isAdmin;
        PanelSolAdmin.Opacity = isAdmin ? 1.0 : 0.55;
    }

    private async Task LoadAllAsync()
    {
        try
        {
            var suppliers = await _purchaseRepository.SearchSuppliersAsync("", 300);
            CmbPedProveedor.ItemsSource = suppliers;
            CmbCpProv.ItemsSource = suppliers;
            if (suppliers.Count > 0)
            {
                if (CmbPedProveedor.SelectedValue == null) CmbPedProveedor.SelectedIndex = 0;
                if (CmbCpProv.SelectedValue == null) CmbCpProv.SelectedIndex = 0;
            }

            var deposits = await _productRepository.GetDepositsAsync();
            CmbPedDeposito.ItemsSource = deposits;
            if (deposits.Count > 0 && CmbPedDeposito.SelectedValue == null) CmbPedDeposito.SelectedIndex = 0;

            var customers = await _customerRepository.SearchCustomersAsync("", 300);
            CmbPreCliente.ItemsSource = customers;
            CmbCrCliente.ItemsSource = customers;
            CmbSolCliente.ItemsSource = customers;
            if (customers.Count > 0)
            {
                if (CmbPreCliente.SelectedValue == null) CmbPreCliente.SelectedIndex = 0;
                if (CmbCrCliente.SelectedValue == null) CmbCrCliente.SelectedIndex = 0;
                if (CmbSolCliente.SelectedValue == null) CmbSolCliente.SelectedIndex = 0;
            }

            var sellers = await _systemRepository.GetSellersAsync();
            CmbPreVendedor.ItemsSource = sellers;
            CmbSolVendedor.ItemsSource = sellers;
            if (sellers.Count > 0)
            {
                if (CmbPreVendedor.SelectedValue == null) CmbPreVendedor.SelectedIndex = 0;
                if (CmbSolVendedor.SelectedValue == null) CmbSolVendedor.SelectedIndex = 0;
            }

            var cajas = await _cashRepository.GetCashRegistersAsync();
            CmbPagProvCaja.ItemsSource = cajas;
            CmbCuotaCaja.ItemsSource = cajas;
            if (cajas.Count > 0)
            {
                if (CmbPagProvCaja.SelectedValue == null) CmbPagProvCaja.SelectedIndex = 0;
                if (CmbCuotaCaja.SelectedValue == null) CmbCuotaCaja.SelectedIndex = 0;
            }

            DateTime from = DateTime.Today.AddMonths(-3);
            GridPedidos.ItemsSource = await _stage3Repository.GetPurchaseOrdersAsync(from, DateTime.Today);
            GridPresupuestos.ItemsSource = await _salesRepository.SearchBudgetsAsync("", 100);
            GridDevVentasHist.ItemsSource = await _stage3Repository.GetSalesReturnsAsync(from, DateTime.Today);
            GridDevComprasHist.ItemsSource = await _stage3Repository.GetPurchaseReturnsAsync(from, DateTime.Today);

            var recentPurchases = await _purchaseRepository.GetRecentPurchasesAsync(from, DateTime.Today, 100);
            CmbDcCompras.ItemsSource = recentPurchases.Select(p => new PurchasePickItem(p.IdCompra, $"#{p.NroCompra} | {p.FechaCompra:dd/MM/yy} | {p.NombreProveedor}")).ToList();
            if (CmbDcCompras.Items.Count > 0 && CmbDcCompras.SelectedIndex < 0) CmbDcCompras.SelectedIndex = 0;

            GridDeudasProv.ItemsSource = await _stage3Repository.GetSupplierDebtsAsync();
            GridPagosProv.ItemsSource = await _stage3Repository.GetSupplierPaymentsAsync(from, DateTime.Today);
            GridCuentasPagar.ItemsSource = await _stage3Repository.GetPayableAccountsAsync();
            GridCuentasRecibir.ItemsSource = await _stage3Repository.GetReceivableAccountsAsync();

            await RefreshCuotasAsync();
            await RefreshSolicitudesAsync();
            LblStatus.Text = "Módulos de operaciones comerciales actualizados.";
        }
        catch (Exception ex)
        {
            LblStatus.Text = $"Error al cargar operaciones: {ex.Message}";
        }
    }

    private async void BtnReloadAll_Click(object sender, RoutedEventArgs e) => await LoadAllAsync();

    private static decimal ParseDec(string text, decimal fallback = 0m) =>
        decimal.TryParse((text ?? "").Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal d) ? d : fallback;

    private static int ParseInt(string text, int fallback = 0) => int.TryParse(text, out int n) ? n : fallback;

    // --- TAB 0: PEDIDOS COMPRA ---
    private async void TxtPedProdCod_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        int idDep = CmbPedDeposito.SelectedValue is int d ? d : 1;
        var p = await _productRepository.GetByCodeAsync(TxtPedProdCod.Text.Trim(), idDep);
        if (p == null)
        {
            LblStatus.Text = "Código de producto no encontrado.";
            return;
        }
        _pedSelectedProd = p;
        TxtPedProdDesc.Text = p.Descripcion;
        TxtPedProdCosto.Text = p.Costo1.ToString("0", CultureInfo.InvariantCulture);
        TxtPedProdCant.Focus();
    }

    private void BtnPedAddItem_Click(object sender, RoutedEventArgs e)
    {
        if (_pedSelectedProd == null && string.IsNullOrWhiteSpace(TxtPedProdDesc.Text)) return;
        decimal qty = Math.Max(0.01m, ParseDec(TxtPedProdCant.Text, 1m));
        decimal unit = ParseDec(TxtPedProdCosto.Text);
        decimal sub = Math.Round(qty * unit, 0, MidpointRounding.AwayFromZero);
        int iva = _pedSelectedProd?.Iva ?? 1;

        _pedItems.Add(new PurchaseOrderItemEntity
        {
            IdProducto = _pedSelectedProd?.IdProducto ?? 0,
            Codigo = _pedSelectedProd?.Codigo ?? TxtPedProdCod.Text.Trim(),
            Descripcion = _pedSelectedProd?.Descripcion ?? TxtPedProdDesc.Text.Trim(),
            Cantidad = qty,
            PreUnitario = unit,
            Iva = iva,
            PreExenta = iva == 3 ? sub : 0m,
            PreIva5 = iva == 2 ? sub : 0m,
            PreIva10 = iva == 1 ? sub : 0m
        });
        _pedSelectedProd = null;
        TxtPedProdCod.Clear(); TxtPedProdDesc.Clear(); TxtPedProdCant.Text = "1"; TxtPedProdCosto.Text = "0";
        LblPedTotal.Text = $"Total: {_pedItems.Sum(x => x.Subtotal):N0} Gs.";
        TxtPedProdCod.Focus();
    }

    private void BtnPedRemoveItem_Click(object sender, RoutedEventArgs e)
    {
        if (GridPedItems.SelectedItem is PurchaseOrderItemEntity row)
        {
            _pedItems.Remove(row);
            LblPedTotal.Text = $"Total: {_pedItems.Sum(x => x.Subtotal):N0} Gs.";
        }
    }

    private async void BtnGuardarPedido_Click(object sender, RoutedEventArgs e)
    {
        if (_pedItems.Count == 0)
        {
            MessageBox.Show("Agregue al menos un ítem al pedido.", "Pedidos", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var order = new PurchaseOrderEntity
        {
            FechaPedido = DateTime.Today,
            IdProveedor = CmbPedProveedor.SelectedValue is int p ? p : 1,
            IdDeposito = CmbPedDeposito.SelectedValue is int d ? d : 1,
            NroFactura = TxtPedRef.Text.Trim(),
            Observacion = TxtPedObs.Text.Trim(),
            TotalPedido = _pedItems.Sum(x => x.Subtotal),
            Estado = 1,
            Items = _pedItems.ToList()
        };
        int nro = await _stage3Repository.SavePurchaseOrderTransactionAsync(order, _windowManager.CurrentUser.Cidusuario);
        _pedItems.Clear();
        TxtPedRef.Clear(); TxtPedObs.Clear();
        LblPedTotal.Text = "Total: 0 Gs.";
        await LoadAllAsync();
        LblStatus.Text = $"Pedido de compra #{nro} registrado.";
    }

    // --- TAB 1: PRESUPUESTOS ---
    private async void BtnSearchPre_Click(object sender, RoutedEventArgs e)
    {
        GridPresupuestos.ItemsSource = await _salesRepository.SearchBudgetsAsync(TxtPreSearch.Text.Trim(), 100);
    }

    private async void GridPresupuestos_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GridPresupuestos.SelectedItem is not BudgetSummaryEntity b) return;
        var full = await _salesRepository.GetBudgetByNumberOrIdAsync(b.IdPresupuesto, byNumber: false);
        if (full == null) return;

        _editingBudget = full.Value.Header;
        LblPreHeader.Text = $"Presupuesto #{_editingBudget.NroPresupuesto}";
        CmbPreCliente.SelectedValue = _editingBudget.IdCliente;
        CmbPreVendedor.SelectedValue = _editingBudget.IdVendedor;
        TxtPreDesc.Text = _editingBudget.Descuento.ToString("0", CultureInfo.InvariantCulture);
        TxtPreObs.Text = _editingBudget.Observacion;

        _preItems.Clear();
        foreach (var it in full.Value.Items) _preItems.Add(it);
        UpdatePreTotal();
    }

    private void BtnNuevoPre_Click(object sender, RoutedEventArgs e)
    {
        _editingBudget = new BudgetSummaryEntity();
        GridPresupuestos.SelectedItem = null;
        LblPreHeader.Text = "Nuevo Presupuesto";
        TxtPreDesc.Text = "0"; TxtPreObs.Clear();
        _preItems.Clear();
        UpdatePreTotal();
    }

    private async void TxtPreProdCod_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        var p = await _productRepository.GetByCodeAsync(TxtPreProdCod.Text.Trim(), 1);
        if (p == null) return;
        _preSelectedProd = p;
        TxtPreProdDesc.Text = p.Descripcion;
        TxtPreProdPrec.Text = p.Precio1.ToString("0", CultureInfo.InvariantCulture);
        TxtPreProdCant.Focus();
    }

    private void BtnPreAddItem_Click(object sender, RoutedEventArgs e)
    {
        if (_preSelectedProd == null && string.IsNullOrWhiteSpace(TxtPreProdDesc.Text)) return;
        decimal qty = Math.Max(0.01m, ParseDec(TxtPreProdCant.Text, 1m));
        decimal unit = ParseDec(TxtPreProdPrec.Text);
        decimal sub = Math.Round(qty * unit, 0, MidpointRounding.AwayFromZero);
        int iva = _preSelectedProd?.Iva ?? 1;

        _preItems.Add(new BudgetItemEntity
        {
            IdProducto = _preSelectedProd?.IdProducto ?? 0,
            Codigo = _preSelectedProd?.Codigo ?? TxtPreProdCod.Text.Trim(),
            Descripcion = _preSelectedProd?.Descripcion ?? TxtPreProdDesc.Text.Trim(),
            Cantidad = qty,
            PreUnitario = unit,
            Iva = iva,
            PreExenta = iva == 3 ? sub : 0m,
            PreIva5 = iva == 2 ? sub : 0m,
            PreIva10 = iva == 1 ? sub : 0m,
            CostoUnitario = _preSelectedProd?.Costo1 ?? 0m
        });
        _preSelectedProd = null;
        TxtPreProdCod.Clear(); TxtPreProdDesc.Clear(); TxtPreProdCant.Text = "1"; TxtPreProdPrec.Text = "0";
        UpdatePreTotal();
    }

    private void BtnPreRemoveItem_Click(object sender, RoutedEventArgs e)
    {
        if (GridPreItems.SelectedItem is BudgetItemEntity row)
        {
            _preItems.Remove(row);
            UpdatePreTotal();
        }
    }

    private void UpdatePreTotal()
    {
        decimal desc = ParseDec(TxtPreDesc.Text);
        decimal tot = Math.Max(0m, _preItems.Sum(i => i.Subtotal) - desc);
        LblPreTotal.Text = $"Total: {tot:N0} Gs.";
    }

    private async void BtnGuardarPre_Click(object sender, RoutedEventArgs e)
    {
        if (_preItems.Count == 0) return;
        var cli = CmbPreCliente.SelectedItem as CustomerEntity;
        _editingBudget.IdCliente = cli?.IdCliente ?? 132;
        _editingBudget.NombreCliente = cli?.Nombres ?? "CLIENTE OCASIONAL";
        _editingBudget.RucCliente = cli?.Ruc ?? "X";
        _editingBudget.IdVendedor = CmbPreVendedor.SelectedValue is int v ? v : 10;
        _editingBudget.Descuento = ParseDec(TxtPreDesc.Text);
        _editingBudget.Observacion = TxtPreObs.Text.Trim();
        _editingBudget.TotalPresupuesto = Math.Max(0m, _preItems.Sum(i => i.Subtotal) - _editingBudget.Descuento);

        await _stage3Repository.SaveBudgetFullAsync(_editingBudget, _preItems.ToList(), _windowManager.CurrentUser.Cidusuario);
        await LoadAllAsync();
        LblStatus.Text = "Presupuesto guardado correctamente.";
    }

    private async void BtnEliminarPre_Click(object sender, RoutedEventArgs e)
    {
        if (_editingBudget.IdPresupuesto <= 0) return;
        if (MessageBox.Show($"¿Anular Presupuesto #{_editingBudget.NroPresupuesto}?", "Confirmar", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        await _stage3Repository.DeleteBudgetAsync(_editingBudget.IdPresupuesto);
        BtnNuevoPre_Click(sender, e);
        await LoadAllAsync();
    }

    private void BtnEnviarPrePos_Click(object sender, RoutedEventArgs e)
    {
        if (_preItems.Count == 0) return;
        var salesVm = _windowManager.OpenOrActivateSalesWindow(1);
        salesVm.ApplyBudgetToSale(_editingBudget, _preItems.ToList());
        LblStatus.Text = $"Presupuesto #{_editingBudget.NroPresupuesto} cargado en Caja 1.";
    }

    // --- TAB 2: DEVOLUCIÓN VENTAS ---
    private async void BtnDvCargarVenta_Click(object sender, RoutedEventArgs e)
    {
        int nro = ParseInt(TxtDvNroVenta.Text);
        if (nro <= 0) return;
        var sale = await _salesRepository.GetSaleByNroVentaAsync(nro);
        if (sale == null)
        {
            MessageBox.Show($"No se encontró la venta #{nro}.", "Devolución", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        _loadedDvSale = sale;
        LblDvCliente.Text = $"Cliente: {sale.NombreCliente} (RUC: {sale.RucCliente})";
        _dvItems.Clear();
        foreach (var it in sale.Items)
        {
            _dvItems.Add(new ReturnItemEntity
            {
                IdProducto = it.IdProducto,
                Codigo = it.Codigo,
                Descripcion = it.Descripcion,
                Cantidad = it.Cantidad,
                PreUnitario = it.PreUnitario,
                Iva = it.Iva
            });
        }
    }

    private void BtnDvQuitar_Click(object sender, RoutedEventArgs e)
    {
        if (GridDvItems.SelectedItem is ReturnItemEntity row) _dvItems.Remove(row);
    }

    private async void BtnDvProcesar_Click(object sender, RoutedEventArgs e)
    {
        if (_loadedDvSale == null || _dvItems.Count == 0) return;
        var ret = new ReturnHeaderEntity
        {
            IdDocumentoOrigen = _loadedDvSale.IdVenta,
            NroBoleta = _loadedDvSale.NroVenta.ToString(),
            IdTercero = _loadedDvSale.IdCliente,
            DesTercero = _loadedDvSale.NombreCliente,
            Ruc = _loadedDvSale.RucCliente,
            FechaDevolucion = DateTime.Today,
            IdDeposito = _loadedDvSale.IdDeposito,
            Total = _dvItems.Sum(i => i.Subtotal),
            Concepto = TxtDvMotivo.Text.Trim(),
            CidUsuario = _windowManager.CurrentUser.Cidusuario,
            Items = _dvItems.ToList()
        };
        int idDev = await _stage3Repository.SaveSalesReturnTransactionAsync(ret);
        _dvItems.Clear(); _loadedDvSale = null; LblDvCliente.Text = "Cliente: -";
        await LoadAllAsync();
        LblStatus.Text = $"Devolución de venta #{idDev} procesada (stock restaurado).";
    }

    // --- TAB 3: DEVOLUCIÓN COMPRAS ---
    private async void BtnDcCargar_Click(object sender, RoutedEventArgs e)
    {
        if (CmbDcCompras.SelectedItem is not PurchasePickItem pick) return;
        var purch = await _purchaseRepository.GetPurchaseByIdAsync(pick.IdCompra);
        if (purch == null) return;
        _loadedDcPurchase = purch;
        _dcItems.Clear();
        foreach (var it in purch.Items)
        {
            int iva = it.PreIva5 > 0 ? 2 : (it.PreExenta > 0 && it.PreIva10 == 0 ? 3 : 1);
            _dcItems.Add(new ReturnItemEntity
            {
                IdProducto = it.IdProducto,
                Codigo = it.Codigo,
                Descripcion = it.Descripcion,
                Cantidad = it.Cantidad,
                PreUnitario = it.PreUnitario,
                Iva = iva
            });
        }
    }

    private void BtnDcQuitar_Click(object sender, RoutedEventArgs e)
    {
        if (GridDcItems.SelectedItem is ReturnItemEntity row) _dcItems.Remove(row);
    }

    private async void BtnDcProcesar_Click(object sender, RoutedEventArgs e)
    {
        if (_loadedDcPurchase == null || _dcItems.Count == 0) return;
        var ret = new ReturnHeaderEntity
        {
            IdDocumentoOrigen = _loadedDcPurchase.IdCompra,
            NroBoleta = _loadedDcPurchase.NroFactura,
            IdTercero = _loadedDcPurchase.IdProveedor,
            DesTercero = _loadedDcPurchase.NombreProveedor,
            FechaDevolucion = DateTime.Today,
            IdDeposito = _loadedDcPurchase.IdDeposito,
            Total = _dcItems.Sum(i => i.Subtotal),
            Concepto = TxtDcMotivo.Text.Trim(),
            CidUsuario = _windowManager.CurrentUser.Cidusuario,
            Items = _dcItems.ToList()
        };
        int idDev = await _stage3Repository.SavePurchaseReturnTransactionAsync(ret);
        _dcItems.Clear(); _loadedDcPurchase = null;
        await LoadAllAsync();
        LblStatus.Text = $"Devolución a proveedor #{idDev} procesada.";
    }

    // --- TAB 4: PAGOS PROVEEDORES & CUENTAS PAGAR ---
    private void GridDeudasProv_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GridDeudasProv.SelectedItem is not SupplierDebtEntity d) return;
        _selectedDebt = d;
        TxtPagProvMonto.Text = d.Saldo.ToString("0", CultureInfo.InvariantCulture);
    }

    private async void BtnPagarProveedor_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedDebt == null) return;
        decimal monto = ParseDec(TxtPagProvMonto.Text);
        if (monto <= 0) return;
        int idCaja = CmbPagProvCaja.SelectedValue is int c ? c : 1;
        await _stage3Repository.RegisterSupplierPaymentAsync(_selectedDebt.IdDeuda, monto, idCaja, TxtPagProvObs.Text.Trim(), _windowManager.CurrentUser.Cidusuario);
        _selectedDebt = null; TxtPagProvMonto.Clear(); TxtPagProvObs.Clear();
        await LoadAllAsync();
        LblStatus.Text = "Pago a proveedor y egreso de caja registrados.";
    }

    private async void BtnGuardarCuentaPagar_Click(object sender, RoutedEventArgs e)
    {
        decimal tot = ParseDec(TxtCpTotal.Text);
        if (tot <= 0) return;
        var prov = CmbCpProv.SelectedItem as SupplierEntity;
        var acc = new PayableAccountEntity
        {
            FechaCarga = DateTime.Today,
            NroPagare = ParseInt(TxtCpPagare.Text, 1),
            NroFactura = TxtCpFactura.Text.Trim(),
            IdProveedor = prov?.IdProveedor ?? 1,
            DesProveedor = prov?.Descripcion ?? string.Empty,
            Total = tot,
            Saldo = tot,
            Vencimiento = DpCpVenc.SelectedDate ?? DateTime.Today.AddDays(30),
            Cuota = 1
        };
        await _stage3Repository.SavePayableAccountAsync(acc, "A");
        TxtCpTotal.Text = "0";
        await LoadAllAsync();
    }

    // --- TAB 5: CUOTAS, MOROSOS & CUENTAS RECIBIR ---
    private async Task RefreshCuotasAsync()
    {
        GridCuotas.ItemsSource = await _stage3Repository.GetCreditInstallmentsAsync(
            ChkSoloMorosos.IsChecked == true,
            TxtCuotaSearch.Text.Trim());
    }

    private async void BtnFiltrarCuotas_Click(object sender, RoutedEventArgs e) => await RefreshCuotasAsync();

    private void GridCuotas_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GridCuotas.SelectedItem is not CreditInstallmentRow c) return;
        _selectedInstallment = c;
        TxtCuotaEntrega.Text = c.Saldo.ToString("0", CultureInfo.InvariantCulture);
    }

    private async void BtnCobrarCuota_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedInstallment == null) return;
        decimal entrega = ParseDec(TxtCuotaEntrega.Text);
        if (entrega <= 0) return;
        int idCaja = CmbCuotaCaja.SelectedValue is int c ? c : 1;
        await _stage3Repository.RegisterCreditInstallmentPaymentAsync(_selectedInstallment.IdCredito, entrega, idCaja, _windowManager.CurrentUser.Cidusuario);
        _selectedInstallment = null; TxtCuotaEntrega.Clear();
        await RefreshCuotasAsync();
        LblStatus.Text = "Cobro de cuota e ingreso a caja registrados.";
    }

    private async void BtnGuardarCuentaRecibir_Click(object sender, RoutedEventArgs e)
    {
        decimal tot = ParseDec(TxtCrTotal.Text);
        if (tot <= 0) return;
        var cli = CmbCrCliente.SelectedItem as CustomerEntity;
        var acc = new ReceivableAccountEntity
        {
            FechaCarga = DateTime.Today,
            NroPagare = ParseInt(TxtCrPagare.Text, 1),
            IdCliente = cli?.IdCliente ?? 132,
            DesCliente = cli?.Nombres ?? "CLIENTE",
            Total = tot,
            Saldo = tot,
            Vencimiento = DpCrVenc.SelectedDate ?? DateTime.Today.AddDays(30),
            Cuota = 1
        };
        await _stage3Repository.SaveReceivableAccountAsync(acc, "A");
        TxtCrTotal.Text = "0";
        await LoadAllAsync();
    }

    // --- TAB 6: SOLICITUD CRÉDITO ---
    private async Task RefreshSolicitudesAsync()
    {
        int? estadoFilter = CmbSolEstadoFilter.SelectedIndex switch
        {
            1 => 0,
            2 => 1,
            3 => 2,
            _ => null
        };
        GridSolicitudes.ItemsSource = await _stage3Repository.GetCreditApplicationsAsync(estadoFilter);
    }

    private async void BtnFiltrarSol_Click(object sender, RoutedEventArgs e) => await RefreshSolicitudesAsync();

    private void GridSolicitudes_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GridSolicitudes.SelectedItem is not CreditApplicationEntity s) return;
        _editingSol = s;
        CmbSolCliente.SelectedValue = s.IdCliente;
        CmbSolVendedor.SelectedValue = s.IdVendedor;
        TxtSolMonto.Text = s.Monto.ToString("0", CultureInfo.InvariantCulture);
        TxtSolPlazo.Text = s.Plazo.ToString();
        TxtSolCuota.Text = s.Cuota.ToString("N0");
        TxtSolBanco.Text = s.DesBanco;
        TxtSolObs.Text = s.Observaciones;
        TxtSolObsEstado.Text = s.ObsEstado;
    }

    private void SolCalcChanged(object sender, TextChangedEventArgs e)
    {
        if (TxtSolMonto == null || TxtSolPlazo == null || TxtSolCuota == null)
            return;
        decimal m = ParseDec(TxtSolMonto.Text);
        int p = Math.Max(1, ParseInt(TxtSolPlazo.Text, 6));
        TxtSolCuota.Text = Math.Round(m / p, 0, MidpointRounding.AwayFromZero).ToString("N0");
    }

    private void BtnNuevoSol_Click(object sender, RoutedEventArgs e)
    {
        _editingSol = new CreditApplicationEntity();
        GridSolicitudes.SelectedItem = null;
        TxtSolMonto.Text = "0"; TxtSolPlazo.Text = "6"; TxtSolBanco.Clear(); TxtSolObs.Clear(); TxtSolObsEstado.Clear();
    }

    private async void BtnGuardarSol_Click(object sender, RoutedEventArgs e)
    {
        decimal m = ParseDec(TxtSolMonto.Text);
        if (m <= 0) return;
        int plazo = Math.Max(1, ParseInt(TxtSolPlazo.Text, 6));
        _editingSol.IdCliente = CmbSolCliente.SelectedValue is int c ? c : 132;
        _editingSol.IdVendedor = CmbSolVendedor.SelectedValue is int v ? v : 10;
        _editingSol.Monto = m;
        _editingSol.Plazo = plazo;
        _editingSol.Cuota = Math.Round(m / plazo, 0, MidpointRounding.AwayFromZero);
        _editingSol.DesBanco = TxtSolBanco.Text.Trim();
        _editingSol.Observaciones = TxtSolObs.Text.Trim();
        await _stage3Repository.SaveCreditApplicationAsync(_editingSol, _editingSol.IdSolicitud > 0 ? "M" : "A");
        await RefreshSolicitudesAsync();
    }

    private async void BtnAprobarSol_Click(object sender, RoutedEventArgs e)
    {
        if (_editingSol.IdSolicitud <= 0) return;
        await _stage3Repository.UpdateCreditApplicationStatusAsync(_editingSol.IdSolicitud, 1, TxtSolObsEstado.Text.Trim());
        await RefreshSolicitudesAsync();
    }

    private async void BtnRechazarSol_Click(object sender, RoutedEventArgs e)
    {
        if (_editingSol.IdSolicitud <= 0) return;
        await _stage3Repository.UpdateCreditApplicationStatusAsync(_editingSol.IdSolicitud, 2, TxtSolObsEstado.Text.Trim());
        await RefreshSolicitudesAsync();
    }

    private void BtnImprimirSolicitud_Click(object sender, RoutedEventArgs e)
    {
        var sol = GridSolicitudes.SelectedItem as CreditApplicationEntity ?? _editingSol;
        var doc = new FlowDocument
        {
            PagePadding = new Thickness(48),
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
            FontSize = 12
        };
        doc.Blocks.Add(new Paragraph(new Run("HOJA DE SOLICITUD DE CRÉDITO")) { FontSize = 18, FontWeight = FontWeights.Bold });
        doc.Blocks.Add(new Paragraph(new Run($"Solicitud N°: {sol.IdSolicitud}   |   Fecha: {sol.FechaIngreso:dd/MM/yyyy}   |   Estado: {sol.EstadoDisplay}")));
        doc.Blocks.Add(new Paragraph(new Run($"Cliente: {(CmbSolCliente.SelectedItem as CustomerEntity)?.Nombres ?? sol.DesCliente}")));
        doc.Blocks.Add(new Paragraph(new Run($"Vendedor: {(CmbSolVendedor.SelectedItem as SellerEntity)?.Nombres ?? sol.DesVendedor}")));
        doc.Blocks.Add(new Paragraph(new Run($"Monto Solicitado: {sol.Monto:N0} Gs.   |   Plazo: {sol.Plazo} cuotas   |   Cuota Estimada: {sol.Cuota:N0} Gs.")));
        doc.Blocks.Add(new Paragraph(new Run($"Referencia Bancaria: {sol.DesBanco}")));
        doc.Blocks.Add(new Paragraph(new Run($"Observaciones: {sol.Observaciones}")));
        doc.Blocks.Add(new Paragraph(new Run("\n\n_____________________________          _____________________________\n        Firma del Solicitante                              Aprobación Administrativa")));

        var dlg = new PrintDialog();
        if (dlg.ShowDialog() == true)
            dlg.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator, $"Solicitud_Credito_{sol.IdSolicitud}");
    }

    private sealed class PurchasePickItem
    {
        public int IdCompra { get; }
        public string Display { get; }
        public PurchasePickItem(int idCompra, string display) { IdCompra = idCompra; Display = display; }
        public override string ToString() => Display;
    }
}
