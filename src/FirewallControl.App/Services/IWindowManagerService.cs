using System;
using System.Collections.Generic;
using FirewallControl.App.ViewModels;
using FirewallControl.Core.Entities;
using FirewallControl.Core.Interfaces;

namespace FirewallControl.App.Services;

/// <summary>
/// Centralized Window Manager ensuring a single reusable instance of ITEMSFACTURAVENTAS,
/// singleton-per-caja instances of ABMVENTAS (Cajas 1..5) supporting dual Classic/Renovado layouts,
/// and singleton/shared tools without memory leaks or duplicate windows.
/// </summary>
public interface IWindowManagerService
{
    IConfigService ConfigService { get; }
    ICargaRapidaIaService CargaRapidaIaService { get; }
    UserEntity CurrentUser { get; set; }
    UserGroupEntity CurrentUserGroup { get; set; }

    AbmVentasAutoserviceViewModel OpenOrActivateSalesWindow(int idCaja = 1);
    AbmVentasAutoserviceViewModel? GetOpenSalesViewModel(int idCaja);
    IReadOnlyCollection<AbmVentasAutoserviceViewModel> GetAllOpenSalesViewModels();

    ItemsFacturaVentasViewModel OpenOrActivateItemsSelector(
        AbmVentasAutoserviceViewModel parentSalesVm,
        string lcTipo = "A",
        int nRecNo = 0,
        SaleItemEntity? itemToModify = null);

    ItemsFacturaVentasViewModel? GetItemsSelectorViewModel();
    bool ApplyPosStyle(string style);
    int ActiveSalesCaja { get; }
    System.Windows.Rect? GetSalesWindowBounds(int idCaja);
    bool IsSalesWindowVisible(int idCaja);
    Type? GetOpenSalesWindowType(int idCaja = 1);
    Type? GetItemsSelectorWindowType();
    bool IsBackgroundLockedByItemModal { get; }
    bool IsWindowNativeInputLocked(System.Windows.Window window);

    void HandleSalesWindowClosedByUser(int idCaja, System.Windows.Window salesWindow);
    void HandleItemsSelectorClosedByUser(System.Windows.Window selectorWindow);
    void SafeHideAndRestoreFocus(System.Windows.Window closingWindow, System.Windows.Window? preferredTarget = null);
    void RefreshItemSelectorModalState();

    void SaltarACaja(int targetCaja);
    void ActivateDeleteModeFromSelector(AbmVentasAutoserviceViewModel parentSalesVm);

    void OpenGenesSelector(int genId, Action<string>? onProductCodeSelected = null);
    void OpenGenesSelectorFull(int genId, string initialMode = "SELECT", Action<GenesSelectionResult>? onProductSelected = null);

    void OpenProductsModule(int initialTab = 0, Action<string>? onProductCodeSelected = null, int auxSubTab = 0);
    void OpenAjustePreciosMasivoWindow();
    void OpenProductSubReportsWindow(int initialTab = 0);
    void OpenTrasladoDepositosWindow();
    void OpenProductImageBrowserWindow();
    void OpenFormAccessDialog(string formName, string formTitle);
    void OpenCustomersModule(int initialTab = 0, Action<CustomerEntity>? onCustomerSelected = null);
    void OpenPurchasesModule(int initialTab = 0);
    void OpenCashModule(int initialTab = 0);
    void OpenReportsAndConfigModule(int initialTab = 0);
    void OpenMasterCatalogsWindow(int initialTab = 0);
    void OpenAgendaAndOrganizerWindow(int initialTab = 0);
    void OpenCommercialTeamWindow(int initialTab = 0);
    void OpenExtendedReportsWindow(int initialTab = 0);
    void OpenCommercialOperationsWindow(int initialTab = 0, bool adminCreditMode = false, bool onlyOverdue = false);
    void OpenHumanResourcesWindow(int initialTab = 0, int lookupIndex = 0, string calcPreset = "");
    void OpenSpecialtyModulesWindow(int initialTab = 0, string? subSection = null);
    void OpenShortcutsAndPreferencesWindow(int initialTab = 0, string initialWindowKey = ShortcutCatalog.WinItemsFactura);
    bool? OpenSeleServidorDialog();

    bool ShowCobroContadoDialog(SaleEntity sale, ExchangeRateEntity exchangeRate);
    int? ShowReeditarReimprimirDialog(bool isReprint);

    // Stage 1 shared POS & Carga Rapida tools (identical in Classic, Renovado & Products)
    void OpenCargaRapidaProductos();
    void OpenCalculatorDialog(Action<decimal>? onAcceptValue = null);
    BudgetSummaryEntity? OpenPresupuestosImportDialog(out List<BudgetItemEntity> items);
    void OpenFacturaLegalWindow(SaleEntity? currentSale = null);
    void OpenAjusteStockWindow(int initialDeposito = 1);
    List<SaleInstallmentEntity>? OpenCuotaVentaDialog(SaleEntity sale, out decimal entregaInicial);
    ProductLotEntity? OpenBuscarLotesDialog(int idProducto, int idDeposito);
    void OpenCombinarCompuestoDialog(int idProductoCompuesto, string descripcionProducto);
    CustomerEntity? OpenQuickCreateCustomerDialog();
    SellerEntity? OpenQuickCreateSellerDialog();

    void ExecuteVfpMenuCommand(string comando);
    void ShutdownAllWindows();
}
