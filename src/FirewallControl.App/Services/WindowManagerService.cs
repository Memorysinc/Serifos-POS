using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using FirewallControl.App.ViewModels;
using FirewallControl.App.Views;
using FirewallControl.Core.Entities;
using FirewallControl.Core.Interfaces;
using FirewallControl.Infrastructure.Repositories;
using FirewallControl.Infrastructure.Services;

namespace FirewallControl.App.Services;

/// <summary>
/// Centralized Window Manager ensuring:
/// - Single reusable instance of ITEMSFACTURAVENTAS (Clásico Sufer POS o Renovado C#).
/// - Singleton-per-Caja (1..5) instances of ABMVENTAS (Clásico Sufer POS o Renovado C#).
/// - Live hot-swap between Clasico and Renovado preserving loaded cart items.
/// - Single reusable shared CargaRapidaProductosWindow across ITEMSFACTURAVENTAS [F9] and Products.
/// </summary>
public class WindowManagerService : IWindowManagerService
{
    private readonly IConfigService _configService;
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IVfpDbfService _dbfService;
    private readonly IProductRepository _productRepository;
    private readonly ISalesRepository _salesRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IPurchaseRepository _purchaseRepository;
    private readonly ICashRepository _cashRepository;
    private readonly ISystemRepository _systemRepository;
    private readonly ITicketPrintService _ticketPrintService;
    private readonly ICargaRapidaIaService _cargaRapidaIaService;
    private readonly IStage3Repository? _stage3Repository;

    private readonly Dictionary<int, ISalesPosWindow> _salesWindowsByCaja = new();
    private readonly HashSet<int> _renovadoPositionedCajas = new();
    private IItemsSelectorWindow? _itemsSelectorWindow;
    private int _activeSalesCaja = 1;
    private double _sharedSalesLeft = double.NaN;
    private double _sharedSalesTop = double.NaN;
    private double _sharedSalesWidth = 1440;
    private double _sharedSalesHeight = 790;
    private bool _selectorPositionedOnce;
    private bool _isBackgroundLockedByItemModal;
    private bool _isShuttingDown;
    private readonly HashSet<IntPtr> _nativeModalLockedHwnds = new();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnableWindow(IntPtr hWnd, [MarshalAs(UnmanagedType.Bool)] bool bEnable);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowEnabled(IntPtr hWnd);
    private GenesSelectorWindow? _genesWindow;
    private ProductsManagementWindow? _productsWindow;
    private CustomersManagementWindow? _customersWindow;
    private PurchasesManagementWindow? _purchasesWindow;
    private CashManagementWindow? _cashWindow;
    private ReportsAndConfigWindow? _reportsWindow;
    private MasterCatalogsWindow? _masterCatalogsWindow;
    private AgendaAndOrganizerWindow? _agendaWindow;
    private CommercialTeamWindow? _commercialTeamWindow;
    private ExtendedReportsWindow? _extendedReportsWindow;
    private CommercialOperationsWindow? _commercialOpsWindow;
    private HumanResourcesWindow? _hrWindow;
    private SpecialtyModulesWindow? _specialtyWindow;
    private ConfiguracionPreferenciasWindow? _configPrefWindow;
    private CargaRapidaProductosWindow? _cargaRapidaWindow;

    public IConfigService ConfigService => _configService;
    public ICargaRapidaIaService CargaRapidaIaService => _cargaRapidaIaService;
    public UserEntity CurrentUser { get; set; } = new() { Cidusuario = "ADMINISTRA", Cdescribe = "ADMINISTRADOR DEL SISTEMA", Cidgrupo = "ADMIN" };
    public UserGroupEntity CurrentUserGroup { get; set; } = new();

    public WindowManagerService(
        IConfigService configService,
        IDbConnectionFactory connectionFactory,
        IVfpDbfService dbfService,
        IProductRepository productRepository,
        ISalesRepository salesRepository,
        ICustomerRepository customerRepository,
        IPurchaseRepository purchaseRepository,
        ICashRepository cashRepository,
        ISystemRepository systemRepository,
        ITicketPrintService ticketPrintService,
        ICargaRapidaIaService? cargaRapidaIaService = null,
        IStage3Repository? stage3Repository = null)
    {
        _configService = configService;
        _connectionFactory = connectionFactory;
        _dbfService = dbfService;
        _productRepository = productRepository;
        _salesRepository = salesRepository;
        _customerRepository = customerRepository;
        _purchaseRepository = purchaseRepository;
        _cashRepository = cashRepository;
        _systemRepository = systemRepository;
        _ticketPrintService = ticketPrintService;
        _cargaRapidaIaService = cargaRapidaIaService ?? new CargaRapidaIaService();
        _stage3Repository = stage3Repository ?? new Stage3Repository(connectionFactory);
    }

    private bool IsClassicPosStyle()
        => string.Equals(_configService.Preferences.EstiloPos, "Clasico", StringComparison.OrdinalIgnoreCase);

    private ISalesPosWindow CreateSalesPosWindow(AbmVentasAutoserviceViewModel vm)
        => IsClassicPosStyle()
            ? new AbmVentasClassicWindow(vm)
            : new AbmVentasAutoserviceWindow(vm);

    private IItemsSelectorWindow CreateItemsSelectorWindow(ItemsFacturaVentasViewModel vm)
        => IsClassicPosStyle()
            ? new ItemsFacturaVentasClassicWindow(vm)
            : new ItemsFacturaVentasWindow(vm);

    public int ActiveSalesCaja => _activeSalesCaja;
    public bool IsBackgroundLockedByItemModal => _isBackgroundLockedByItemModal;

    public bool IsWindowNativeInputLocked(Window window)
    {
        if (window == null)
            return false;
        IntPtr hwnd = new WindowInteropHelper(window).Handle;
        return hwnd != IntPtr.Zero && !IsWindowEnabled(hwnd);
    }

    public Rect? GetSalesWindowBounds(int idCaja)
    {
        int caja = Math.Clamp(idCaja, 1, 5);
        if (!_salesWindowsByCaja.TryGetValue(caja, out var win))
            return null;

        var w = win.Window;
        return new Rect(w.Left, w.Top, w.Width, w.Height);
    }

    public bool IsSalesWindowVisible(int idCaja)
    {
        int caja = Math.Clamp(idCaja, 1, 5);
        return _salesWindowsByCaja.TryGetValue(caja, out var win) && win.Window.Visibility == Visibility.Visible;
    }

    private static Rect GetWorkAreaSafe()
    {
        Rect work = SystemParameters.WorkArea;
        if (work.Width <= 200 || work.Height <= 200)
            work = new Rect(0, 0, 1366, 728);
        return work;
    }

    private static Rect GetRenovadoCascadedBounds(int caja)
    {
        Rect work = GetWorkAreaSafe();
        double minW = Math.Min(980, Math.Max(640, work.Width - 24));
        double maxW = Math.Max(minW, Math.Min(1140, work.Width - 150));
        double renW = Math.Round(Math.Clamp(work.Width * 0.74, minW, maxW));

        double minH = Math.Min(585, Math.Max(520, work.Height - 24));
        double maxH = Math.Max(minH, Math.Min(700, work.Height - 132));
        double renH = Math.Round(Math.Clamp(work.Height - 135, minH, maxH));

        double baseLeft = work.Left + 16;
        double baseTop = work.Top + 52;
        double stepX = 30;
        double stepY = 20;
        int idx = Math.Clamp(caja, 1, 5) - 1;

        double left = Math.Clamp(baseLeft + idx * stepX, work.Left, Math.Max(work.Left, work.Right - renW));
        double top = Math.Clamp(baseTop + idx * stepY, work.Top, Math.Max(work.Top, work.Bottom - renH));
        return new Rect(left, top, renW, renH);
    }

    private void ApplyRenovadoCascadedBounds(Window window, int caja)
    {
        Rect b = GetRenovadoCascadedBounds(caja);
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        if (window.WindowState != WindowState.Normal)
            window.WindowState = WindowState.Normal;
        window.Width = b.Width;
        window.Height = b.Height;
        window.Left = b.Left;
        window.Top = b.Top;
        _renovadoPositionedCajas.Add(caja);
    }

    private void PositionSelectorForCurrentStyle(Window selWin)
    {
        Rect work = GetWorkAreaSafe();
        selWin.WindowStartupLocation = WindowStartupLocation.Manual;
        if (IsClassicPosStyle())
        {
            double selW = double.IsNaN(selWin.Width) || selWin.Width <= 0 ? 574 : selWin.Width;
            double selH = double.IsNaN(selWin.Height) || selWin.Height <= 0 ? 368 : selWin.Height;
            selWin.Left = Math.Clamp(_sharedSalesLeft + (_sharedSalesWidth - selW) / 2.0, work.Left, Math.Max(work.Left, work.Right - selW));
            selWin.Top = Math.Clamp(_sharedSalesTop + (_sharedSalesHeight - selH) / 2.0, work.Top, Math.Max(work.Top, work.Bottom - selH));
            return;
        }

        double renSelW = Math.Min(double.IsNaN(selWin.Width) || selWin.Width <= 0 ? 890 : selWin.Width, Math.Max(760, work.Width - 220));
        double renSelH = Math.Min(double.IsNaN(selWin.Height) || selWin.Height <= 0 ? 690 : selWin.Height, Math.Max(600, work.Height - 36));
        selWin.Width = Math.Round(renSelW);
        selWin.Height = Math.Round(renSelH);
        selWin.Left = Math.Round(Math.Clamp(work.Right - renSelW - 120, work.Left + 210, Math.Max(work.Left, work.Right - renSelW - 16)));
        selWin.Top = Math.Round(Math.Clamp(work.Top + 26, work.Top, Math.Max(work.Top, work.Bottom - renSelH)));
    }

    private void EnsureSharedSalesBounds()
    {
        Rect work = GetWorkAreaSafe();

        if (!double.IsNaN(_sharedSalesLeft) &&
            !double.IsNaN(_sharedSalesTop) &&
            _salesWindowsByCaja.TryGetValue(_activeSalesCaja, out var cur) &&
            cur.Window.Visibility == Visibility.Visible &&
            cur.Window.IsLoaded &&
            cur.Window.WindowState == WindowState.Normal &&
            !double.IsNaN(cur.Window.Left) &&
            !double.IsNaN(cur.Window.Top) &&
            cur.Window.Width > 200 &&
            cur.Window.Height > 200)
        {
            _sharedSalesWidth = Math.Min(Math.Max(cur.Window.Width, 1000), work.Width);
            _sharedSalesHeight = Math.Min(Math.Max(cur.Window.Height, 620), work.Height);
            _sharedSalesLeft = Math.Clamp(cur.Window.Left, work.Left, Math.Max(work.Left, work.Right - _sharedSalesWidth));
            _sharedSalesTop = Math.Clamp(cur.Window.Top, work.Top, Math.Max(work.Top, work.Bottom - _sharedSalesHeight));
            return;
        }

        if (double.IsNaN(_sharedSalesLeft) || double.IsNaN(_sharedSalesTop))
        {
            double minW = Math.Min(1040, work.Width);
            double maxW = Math.Max(minW, work.Width - 24);
            double minH = Math.Min(640, work.Height);
            double maxH = Math.Max(minH, work.Height - 16);

            _sharedSalesWidth = Math.Round(Math.Clamp(work.Width * 0.94, minW, maxW));
            _sharedSalesHeight = Math.Round(Math.Clamp(work.Height * 0.96, minH, maxH));
            _sharedSalesLeft = Math.Round(work.Left + Math.Max(0, (work.Width - _sharedSalesWidth) / 2.0));
            _sharedSalesTop = Math.Round(work.Top + Math.Max(0, (work.Height - _sharedSalesHeight) / 2.0));
        }
    }

    private void ApplySharedBounds(Window window)
    {
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        if (window.WindowState != WindowState.Normal)
            window.WindowState = WindowState.Normal;
        window.Width = _sharedSalesWidth;
        window.Height = _sharedSalesHeight;
        window.Left = _sharedSalesLeft;
        window.Top = _sharedSalesTop;
    }

    private void ReParentSelectorAndHideInactiveCajas(int activeCaja, Window activeWindow)
    {
        if (_itemsSelectorWindow != null &&
            _itemsSelectorWindow.Window != activeWindow &&
            _itemsSelectorWindow.Window.IsLoaded &&
            activeWindow.IsLoaded &&
            activeWindow.Visibility == Visibility.Visible)
        {
            try
            {
                if (_itemsSelectorWindow.Window.Owner != activeWindow)
                    _itemsSelectorWindow.Window.Owner = activeWindow;
            }
            catch { }
        }

        foreach (var kv in _salesWindowsByCaja)
        {
            if (kv.Key == activeCaja)
                continue;

            if (IsClassicPosStyle())
            {
                ApplySharedBounds(kv.Value.Window);
                if (kv.Value.Window.Visibility == Visibility.Visible || kv.Value.IsVisible)
                    kv.Value.Window.Hide();
            }
        }
    }

    public AbmVentasAutoserviceViewModel OpenOrActivateSalesWindow(int idCaja = 1)
    {
        int caja = Math.Clamp(idCaja, 1, 5);
        if (IsClassicPosStyle())
            EnsureSharedSalesBounds();

        bool isNew = false;
        if (!_salesWindowsByCaja.TryGetValue(caja, out var win))
        {
            var vm = new AbmVentasAutoserviceViewModel(
                _salesRepository,
                _customerRepository,
                _cashRepository,
                _systemRepository,
                _ticketPrintService,
                this,
                caja,
                _productRepository);

            win = CreateSalesPosWindow(vm);
            _salesWindowsByCaja[caja] = win;
            isNew = true;
        }

        if (IsClassicPosStyle())
        {
            ApplySharedBounds(win.Window);
        }
        else if (isNew || !_renovadoPositionedCajas.Contains(caja))
        {
            ApplyRenovadoCascadedBounds(win.Window, caja);
        }

        if (isNew)
            _ = win.ViewModel.InitializeNewSaleAsync();

        EnsureNativeWindowEnabled(win.Window);
        win.Window.IsEnabled = true;
        ShowRestoreAndActivate(win.Window);
        ReParentSelectorAndHideInactiveCajas(caja, win.Window);
        _activeSalesCaja = caja;
        return win.ViewModel;
    }

    public AbmVentasAutoserviceViewModel? GetOpenSalesViewModel(int idCaja)
    {
        return _salesWindowsByCaja.TryGetValue(idCaja, out var win) ? win.ViewModel : null;
    }

    public IReadOnlyCollection<AbmVentasAutoserviceViewModel> GetAllOpenSalesViewModels()
    {
        return _salesWindowsByCaja.Values.Select(w => w.ViewModel).ToList();
    }

    public ItemsFacturaVentasViewModel? GetItemsSelectorViewModel()
        => _itemsSelectorWindow?.ViewModel;

    public Type? GetOpenSalesWindowType(int idCaja = 1)
        => _salesWindowsByCaja.TryGetValue(Math.Clamp(idCaja, 1, 5), out var win) ? win.Window.GetType() : null;

    public Type? GetItemsSelectorWindowType()
        => _itemsSelectorWindow?.Window.GetType();

    public ItemsFacturaVentasViewModel OpenOrActivateItemsSelector(
        AbmVentasAutoserviceViewModel parentSalesVm,
        string lcTipo = "A",
        int nRecNo = 0,
        SaleItemEntity? itemToModify = null)
    {
        int parentCaja = Math.Clamp(parentSalesVm.IdCaja, 1, 5);
        if (IsClassicPosStyle())
            EnsureSharedSalesBounds();

        Window? parentWindow = null;
        if (_salesWindowsByCaja.TryGetValue(parentCaja, out var parentWin))
        {
            if (IsClassicPosStyle())
                ApplySharedBounds(parentWin.Window);
            else if (!_renovadoPositionedCajas.Contains(parentCaja))
                ApplyRenovadoCascadedBounds(parentWin.Window, parentCaja);

            ShowRestoreAndActivate(parentWin.Window);
            parentWindow = parentWin.Window;
            ReParentSelectorAndHideInactiveCajas(parentCaja, parentWin.Window);
            _activeSalesCaja = parentCaja;
        }

        if (_itemsSelectorWindow == null)
        {
            var vm = new ItemsFacturaVentasViewModel(_productRepository, this);
            _itemsSelectorWindow = CreateItemsSelectorWindow(vm);
            _itemsSelectorWindow.Window.IsVisibleChanged += OnItemsSelectorIsVisibleChanged;
            _selectorPositionedOnce = false;
        }

        if (!_selectorPositionedOnce)
        {
            PositionSelectorForCurrentStyle(_itemsSelectorWindow.Window);
            _selectorPositionedOnce = true;
        }

        EnsureNativeWindowEnabled(_itemsSelectorWindow.Window);
        _itemsSelectorWindow.Window.IsEnabled = true;
        _itemsSelectorWindow.ViewModel.VincularPadre(parentSalesVm, lcTipo, nRecNo, itemToModify);
        ShowRestoreAndActivateWithOwner(_itemsSelectorWindow.Window, parentWindow);
        ApplyItemSelectorModalState(true);
        _itemsSelectorWindow.ViewModel.RequestFocusCurrentField();
        return _itemsSelectorWindow.ViewModel;
    }

    public bool ApplyPosStyle(string style)
    {
        string normalized = string.Equals(style, "Clasico", StringComparison.OrdinalIgnoreCase)
            ? "Clasico"
            : "Renovado";

        _configService.Preferences.EstiloPos = normalized;
        _configService.SavePreferences();

        if (_salesWindowsByCaja.Values.Any(w => w.ViewModel.IsProcessingSale))
            return false;

        bool anySalesVisible = _salesWindowsByCaja.Values.Any(w => w.Window.Visibility == Visibility.Visible);
        _selectorPositionedOnce = false;
        _renovadoPositionedCajas.Clear();

        if (IsClassicPosStyle())
        {
            _sharedSalesLeft = double.NaN;
            _sharedSalesTop = double.NaN;
            EnsureSharedSalesBounds();

            foreach (int caja in _salesWindowsByCaja.Keys.OrderBy(k => k).ToList())
            {
                var oldWin = _salesWindowsByCaja[caja];
                var vm = oldWin.ViewModel;

                var newWin = CreateSalesPosWindow(vm);
                ApplySharedBounds(newWin.Window);

                _salesWindowsByCaja[caja] = newWin;
                oldWin.AllowPermanentClose = true;
                oldWin.Window.Close();

                if (caja == _activeSalesCaja && anySalesVisible)
                {
                    ShowRestoreAndActivate(newWin.Window);
                }
            }
        }
        else
        {
            var orderedCajas = _salesWindowsByCaja.Keys.OrderBy(k => k).ToList();
            foreach (int caja in orderedCajas)
            {
                var oldWin = _salesWindowsByCaja[caja];
                var vm = oldWin.ViewModel;

                var newWin = CreateSalesPosWindow(vm);
                ApplyRenovadoCascadedBounds(newWin.Window, caja);

                _salesWindowsByCaja[caja] = newWin;
                oldWin.AllowPermanentClose = true;
                oldWin.Window.Close();
            }

            if (anySalesVisible)
            {
                foreach (int caja in orderedCajas)
                {
                    if (caja != _activeSalesCaja)
                        ShowRestoreAndActivate(_salesWindowsByCaja[caja].Window);
                }

                if (_salesWindowsByCaja.TryGetValue(_activeSalesCaja, out var actWin))
                {
                    ShowRestoreAndActivate(actWin.Window);
                }
            }
        }

        if (_itemsSelectorWindow != null)
        {
            var oldSel = _itemsSelectorWindow;
            var selVm = oldSel.ViewModel;
            bool wasVisible = oldSel.Window.Visibility == Visibility.Visible;

            var newSel = CreateItemsSelectorWindow(selVm);
            PositionSelectorForCurrentStyle(newSel.Window);
            _selectorPositionedOnce = true;

            _itemsSelectorWindow = newSel;
            oldSel.Window.IsVisibleChanged -= OnItemsSelectorIsVisibleChanged;
            oldSel.AllowPermanentClose = true;
            oldSel.Window.Close();

            newSel.Window.IsVisibleChanged += OnItemsSelectorIsVisibleChanged;
            selVm.ActualizarEspejo();
            if (wasVisible)
            {
                Window? activeOwner = _salesWindowsByCaja.TryGetValue(_activeSalesCaja, out var actWin) ? actWin.Window : null;
                ShowRestoreAndActivateWithOwner(newSel.Window, activeOwner);
                ApplyItemSelectorModalState(true);
            }
            else
            {
                ApplyItemSelectorModalState(false);
            }
        }

        return true;
    }

    public void SaltarACaja(int targetCaja)
    {
        int caja = Math.Clamp(targetCaja, 1, 5);
        var targetSalesVm = OpenOrActivateSalesWindow(caja);
        OpenOrActivateItemsSelector(targetSalesVm, "A", 0, null);
    }

    public void ActivateDeleteModeFromSelector(AbmVentasAutoserviceViewModel parentSalesVm)
    {
        if (parentSalesVm.Items.Count == 0)
            return;

        ApplyItemSelectorModalState(false);

        if (_salesWindowsByCaja.TryGetValue(parentSalesVm.IdCaja, out var salesWin))
        {
            if (_itemsSelectorWindow != null)
                SafeHideAndRestoreFocus(_itemsSelectorWindow.Window, salesWin.Window);
            ShowRestoreAndActivate(salesWin.Window);
        }
        else if (_itemsSelectorWindow != null)
        {
            SafeHideAndRestoreFocus(_itemsSelectorWindow.Window, TryGetAccessibleMainWindow());
        }

        parentSalesVm.ActivarModoBorrado();
    }

    public void OpenGenesSelector(int genId, Action<string>? onProductCodeSelected = null)
    {
        OpenGenesSelectorFull(genId, "SELECT", result =>
        {
            onProductCodeSelected?.Invoke(result.Product.Codigo);
        });
    }

    public void OpenGenesSelectorFull(int genId, string initialMode = "SELECT", Action<GenesSelectionResult>? onProductSelected = null)
    {
        if (_genesWindow == null || !_genesWindow.IsLoaded)
        {
            _genesWindow = new GenesSelectorWindow(_dbfService, _configService, _productRepository, this);
            _genesWindow.Closing += (_, e) =>
            {
                if (_isShuttingDown) return;
                e.Cancel = true;
                SafeHideAndRestoreFocus(_genesWindow);
            };
        }

        ShowRestoreAndActivateWithOwner(_genesWindow, GetBestDialogOwner(_activeSalesCaja));
        _genesWindow.Configure(genId, initialMode, onProductSelected);
    }

    private bool CheckModulePermission(Func<UserGroupEntity, int> flagSelector, string moduleName)
    {
        if (HasAdminPrivilege())
            return true;

        if (flagSelector(CurrentUserGroup) == 1)
            return true;

        var res = MessageBox.Show(
            $"El usuario actual '{CurrentUser.Cidusuario}' (Rango: {CurrentUserGroup.Cidgrupo} - {CurrentUserGroup.Cdescribe}) no tiene permiso asignado para acceder a '{moduleName}'.\n\n¿Desea iniciar sesión con un usuario de rango superior?",
            "Control de Acceso Multiusuario (grupos / usuarios)",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (res == MessageBoxResult.Yes)
        {
            var loginWin = new LoginWindow(_systemRepository);
            var mainWin = TryGetAccessibleMainWindow();
            if (mainWin != null && mainWin.IsVisible)
                loginWin.Owner = mainWin;

            if (loginWin.ShowDialog() == true && loginWin.AuthenticatedUser != null)
            {
                CurrentUser = loginWin.AuthenticatedUser;
                CurrentUserGroup = loginWin.AuthenticatedGroup ?? new UserGroupEntity { Cidgrupo = loginWin.AuthenticatedUser.Cidgrupo };

                if (HasAdminPrivilege() || flagSelector(CurrentUserGroup) == 1)
                    return true;

                MessageBox.Show(
                    $"El usuario '{CurrentUser.Cidusuario}' tampoco posee permiso asignado para '{moduleName}'.",
                    "Acceso Denegado",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        return false;
    }

    private bool HasAdminPrivilege()
        => string.IsNullOrWhiteSpace(CurrentUserGroup.Cidgrupo) ||
           string.Equals(CurrentUserGroup.Cidgrupo.Trim(), "ADMIN", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(CurrentUser.Cidusuario.Trim(), "ADMINISTRA", StringComparison.OrdinalIgnoreCase);

    public void OpenProductsModule(int initialTab = 0, Action<string>? onProductCodeSelected = null, int auxSubTab = 0)
    {
        if (!CheckModulePermission(g => g.BtnProducto, "Catálogo de Productos y Stock"))
            return;

        if (_productsWindow == null || !_productsWindow.IsLoaded)
        {
            _productsWindow = new ProductsManagementWindow(
                _productRepository, _systemRepository, this, _purchaseRepository, _cashRepository);
            _productsWindow.Closing += (_, e) =>
            {
                if (_isShuttingDown) return;
                e.Cancel = true;
                SafeHideAndRestoreFocus(_productsWindow);
            };
        }

        _productsWindow.Configure(initialTab, onProductCodeSelected, auxSubTab);
        EnsureNativeWindowEnabled(_productsWindow);
        ShowRestoreAndActivateWithOwner(_productsWindow, GetBestModuleOwner());
    }

    public void OpenAjustePreciosMasivoWindow()
    {
        var win = new AjustePreciosMasivoWindow(_productRepository, _systemRepository, _purchaseRepository, this)
        {
            Owner = GetBestDialogOwner(_itemsSelectorWindow?.ViewModel?.SelectedCaja ?? 1)
        };
        win.ShowDialog();
    }

    public void OpenProductSubReportsWindow(int initialTab = 0)
    {
        if (initialTab == 2 && !CheckModulePermission(g => g.BtnVenci, "Vencimiento de Productos"))
            return;

        var win = new ProductSubReportsWindow(_productRepository, _configService)
        {
            Owner = GetBestDialogOwner(_itemsSelectorWindow?.ViewModel?.SelectedCaja ?? 1)
        };
        win.Configure(initialTab);
        win.ShowDialog();
    }

    public void OpenTrasladoDepositosWindow()
    {
        if (!CheckModulePermission(g => g.BtnProducto, "Traslados entre Depósitos"))
            return;

        var win = new TrasladoDepositosWindow(_productRepository, CurrentUser.Cidusuario)
        {
            Owner = GetBestDialogOwner(_itemsSelectorWindow?.ViewModel?.SelectedCaja ?? 1)
        };
        win.ShowDialog();
    }

    public void OpenProductImageBrowserWindow()
    {
        if (!CheckModulePermission(g => g.BtnProducto, "Buscador de Imágenes de Productos"))
            return;

        var win = new ProductImageBrowserWindow(_productRepository, _configService)
        {
            Owner = GetBestDialogOwner(_itemsSelectorWindow?.ViewModel?.SelectedCaja ?? 1)
        };
        win.ShowDialog();
    }

    public void OpenFormAccessDialog(string formName, string formTitle)
    {
        var win = new FormAccessDialog(_systemRepository)
        {
            Owner = GetBestDialogOwner(_itemsSelectorWindow?.ViewModel?.SelectedCaja ?? 1)
        };
        win.Configure(formName, formTitle);
        win.ShowDialog();
    }

    public void OpenCustomersModule(int initialTab = 0, Action<CustomerEntity>? onCustomerSelected = null)
    {
        if (!CheckModulePermission(
                g => initialTab == 1 ? g.BtnCobro : g.BtnCliente,
                initialTab == 1 ? "Cobros a Clientes" : "Clientes y Cobros"))
            return;

        if (_customersWindow == null || !_customersWindow.IsLoaded)
        {
            _customersWindow = new CustomersManagementWindow(_customerRepository);
            _customersWindow.Closing += (_, e) =>
            {
                if (_isShuttingDown) return;
                e.Cancel = true;
                SafeHideAndRestoreFocus(_customersWindow);
            };
        }

        _customersWindow.Configure(initialTab, onCustomerSelected);
        ShowRestoreAndActivateWithOwner(_customersWindow, GetBestModuleOwner());
    }

    public void OpenPurchasesModule(int initialTab = 0)
    {
        if (!CheckModulePermission(
                g => initialTab == 2 ? g.BtnProveedor : g.BtnCompra,
                "Compras y Proveedores (abm_compras)"))
        {
            return;
        }

        if (_purchasesWindow == null || !_purchasesWindow.IsLoaded)
        {
            _purchasesWindow = new PurchasesManagementWindow(_purchaseRepository, _productRepository, _systemRepository, this, _configService);
            _purchasesWindow.Closing += (_, e) =>
            {
                if (_isShuttingDown) return;
                e.Cancel = true;
                SafeHideAndRestoreFocus(_purchasesWindow);
            };
        }

        _purchasesWindow.Configure(initialTab);
        ShowRestoreAndActivateWithOwner(_purchasesWindow, GetBestModuleOwner());
    }

    public void OpenCashModule(int initialTab = 0)
    {
        if (!CheckModulePermission(g => g.BtnMovCaja, "Movimientos de Caja y Cotizaciones"))
            return;

        if (_cashWindow == null || !_cashWindow.IsLoaded)
        {
            _cashWindow = new CashManagementWindow(_cashRepository, _stage3Repository);
            _cashWindow.Closing += (_, e) =>
            {
                if (_isShuttingDown) return;
                e.Cancel = true;
                SafeHideAndRestoreFocus(_cashWindow);
            };
        }

        _cashWindow.Configure(initialTab);
        ShowRestoreAndActivateWithOwner(_cashWindow, TryGetAccessibleMainWindow());
    }

    public void OpenReportsAndConfigModule(int initialTab = 0)
    {
        if (!CheckModulePermission(g => g.Avisos, "Informes y Estadísticas"))
            return;

        if (_reportsWindow == null || !_reportsWindow.IsLoaded)
        {
            _reportsWindow = new ReportsAndConfigWindow(_salesRepository, _systemRepository, _ticketPrintService, _customerRepository, _productRepository, this);
            _reportsWindow.Closing += (_, e) =>
            {
                if (_isShuttingDown) return;
                e.Cancel = true;
                SafeHideAndRestoreFocus(_reportsWindow);
            };
        }

        _reportsWindow.Configure(initialTab);
        ShowRestoreAndActivateWithOwner(_reportsWindow, TryGetAccessibleMainWindow());
    }

    private IStage3Repository GetStage3Repo() => _stage3Repository ?? new Stage3Repository(_connectionFactory);

    public void OpenMasterCatalogsWindow(int initialTab = 0)
    {
        if (_masterCatalogsWindow == null || !_masterCatalogsWindow.IsLoaded)
        {
            _masterCatalogsWindow = new MasterCatalogsWindow(GetStage3Repo(), _cashRepository, _systemRepository);
            AttachSafeCloseFocusRestore(_masterCatalogsWindow);
        }

        _masterCatalogsWindow.Configure(initialTab);
        ShowRestoreAndActivateWithOwner(_masterCatalogsWindow, TryGetAccessibleMainWindow());
    }

    public void OpenAgendaAndOrganizerWindow(int initialTab = 0)
    {
        if (_agendaWindow == null || !_agendaWindow.IsLoaded)
        {
            _agendaWindow = new AgendaAndOrganizerWindow(GetStage3Repo(), CurrentUser.Cidusuario);
            AttachSafeCloseFocusRestore(_agendaWindow);
        }

        _agendaWindow.Configure(initialTab);
        ShowRestoreAndActivateWithOwner(_agendaWindow, TryGetAccessibleMainWindow());
    }

    public void OpenCommercialTeamWindow(int initialTab = 0)
    {
        if (initialTab == 0 && !CheckModulePermission(g => g.BtnVendedor, "Vendedores"))
            return;

        if (_commercialTeamWindow == null || !_commercialTeamWindow.IsLoaded)
        {
            _commercialTeamWindow = new CommercialTeamWindow(GetStage3Repo(), _systemRepository, _purchaseRepository, _cashRepository);
            AttachSafeCloseFocusRestore(_commercialTeamWindow);
        }

        _commercialTeamWindow.Configure(initialTab);
        ShowRestoreAndActivateWithOwner(_commercialTeamWindow, TryGetAccessibleMainWindow());
    }

    public void OpenExtendedReportsWindow(int initialTab = 0)
    {
        if (!CheckModulePermission(g => g.Avisos, "Informes Gerenciales y Auditoría"))
            return;

        if (_extendedReportsWindow == null || !_extendedReportsWindow.IsLoaded)
        {
            _extendedReportsWindow = new ExtendedReportsWindow(GetStage3Repo());
            AttachSafeCloseFocusRestore(_extendedReportsWindow);
        }

        _extendedReportsWindow.Configure(initialTab);
        ShowRestoreAndActivateWithOwner(_extendedReportsWindow, TryGetAccessibleMainWindow());
    }

    public void OpenCommercialOperationsWindow(int initialTab = 0, bool adminCreditMode = false, bool onlyOverdue = false)
    {
        bool allowed = initialTab switch
        {
            0 => CheckModulePermission(g => g.BtnPedido, "Pedidos de Compra"),
            1 => CheckModulePermission(g => g.BtnPresu, "Presupuestos"),
            2 => CheckModulePermission(g => g.BtnVenta, "Devolución de Ventas"),
            3 => CheckModulePermission(g => g.BtnCompra, "Devolución de Compras"),
            4 => CheckModulePermission(g => g.BtnPago, "Pagos a Proveedores y Cuentas"),
            5 => CheckModulePermission(g => g.BtnCobro, "Cobro de Cuotas y Morosos"),
            _ => CheckModulePermission(g => g.BtnCliente, "Solicitudes de Crédito")
        };
        if (!allowed)
            return;

        if (_commercialOpsWindow == null || !_commercialOpsWindow.IsLoaded)
        {
            _commercialOpsWindow = new CommercialOperationsWindow(
                GetStage3Repo(), _salesRepository, _purchaseRepository, _productRepository,
                _customerRepository, _cashRepository, _systemRepository, this);
            AttachSafeCloseFocusRestore(_commercialOpsWindow);
        }

        _commercialOpsWindow.Configure(initialTab, adminCreditMode, onlyOverdue);
        ShowRestoreAndActivateWithOwner(_commercialOpsWindow, TryGetAccessibleMainWindow());
    }

    public void OpenHumanResourcesWindow(int initialTab = 0, int lookupIndex = 0, string calcPreset = "")
    {
        if (_hrWindow == null || !_hrWindow.IsLoaded)
        {
            _hrWindow = new HumanResourcesWindow(GetStage3Repo());
            AttachSafeCloseFocusRestore(_hrWindow);
        }

        _hrWindow.Configure(initialTab, lookupIndex, calcPreset);
        ShowRestoreAndActivateWithOwner(_hrWindow, TryGetAccessibleMainWindow());
    }

    public void OpenSpecialtyModulesWindow(int initialTab = 0, string? subSection = null)
    {
        if (_specialtyWindow == null || !_specialtyWindow.IsLoaded)
        {
            _specialtyWindow = new SpecialtyModulesWindow(GetStage3Repo(), _systemRepository, _salesRepository, this);
            AttachSafeCloseFocusRestore(_specialtyWindow);
        }

        _specialtyWindow.Configure(initialTab, subSection);
        ShowRestoreAndActivateWithOwner(_specialtyWindow, TryGetAccessibleMainWindow());
    }

    public void OpenShortcutsAndPreferencesWindow(int initialTab = 0, string initialWindowKey = ShortcutCatalog.WinItemsFactura)
    {
        if (_configPrefWindow == null || !_configPrefWindow.IsLoaded)
        {
            _configPrefWindow = new ConfiguracionPreferenciasWindow(_configService, _systemRepository, _connectionFactory, _ticketPrintService, this);
            _configPrefWindow.Closing += (_, e) =>
            {
                if (_isShuttingDown) return;
                e.Cancel = true;
                SafeHideAndRestoreFocus(_configPrefWindow);
            };
        }

        _configPrefWindow.Configure(initialTab, initialWindowKey);
        ShowRestoreAndActivateWithOwner(_configPrefWindow, TryGetAccessibleMainWindow());
    }

    public bool? OpenSeleServidorDialog()
    {
        var dlg = new SeleServidorWindow(_configService, _connectionFactory);
        var mainWin = TryGetAccessibleMainWindow();
        if (mainWin != null && mainWin.IsVisible)
            dlg.Owner = mainWin;
        return dlg.ShowDialog();
    }

    private bool? ShowOwnedDialogPreservingModal1(Window dlg, Window? owner)
    {
        dlg.Owner = owner;
        try
        {
            return dlg.ShowDialog();
        }
        finally
        {
            RefreshItemSelectorModalState();
        }
    }

    public bool ShowCobroContadoDialog(SaleEntity sale, ExchangeRateEntity exchangeRate)
    {
        var dlg = new CobroContadoDialog(sale, exchangeRate);
        return ShowOwnedDialogPreservingModal1(dlg, GetBestDialogOwner(sale.IdCaja)) == true;
    }

    public int? ShowReeditarReimprimirDialog(bool isReprint)
    {
        var dlg = new ReeditarReimprimirDialog(_salesRepository, isReprint);
        int activeCaja = _itemsSelectorWindow?.ViewModel?.SelectedCaja ?? 1;
        var result = ShowOwnedDialogPreservingModal1(dlg, GetBestDialogOwner(activeCaja)) == true ? dlg.SelectedNroVenta : null;

        if (_salesWindowsByCaja.TryGetValue(activeCaja, out var salesWin))
        {
            ShowRestoreAndActivate(salesWin.Window);
        }
        if (_itemsSelectorWindow != null && _itemsSelectorWindow.IsVisible)
        {
            ShowRestoreAndActivate(_itemsSelectorWindow.Window);
            RefreshItemSelectorModalState();
            _itemsSelectorWindow.ViewModel.RequestFocusCurrentField();
        }

        return result;
    }

    public void OpenCargaRapidaProductos()
    {
        if (_cargaRapidaWindow == null)
        {
            _cargaRapidaWindow = new CargaRapidaProductosWindow(_productRepository, _configService, _cargaRapidaIaService, this);
        }

        ShowRestoreAndActivateWithOwner(_cargaRapidaWindow, GetBestDialogOwner(_activeSalesCaja));
        _cargaRapidaWindow.ResumeOrSyncState();
    }

    public void OpenCalculatorDialog(Action<decimal>? onAcceptValue = null)
    {
        var dlg = new CalculatorDialog(onAcceptValue);
        ShowOwnedDialogPreservingModal1(dlg, GetBestDialogOwner(_itemsSelectorWindow?.ViewModel?.SelectedCaja ?? 1));
    }

    public BudgetSummaryEntity? OpenPresupuestosImportDialog(out List<BudgetItemEntity> items)
    {
        items = new List<BudgetItemEntity>();
        var dlg = new BuscarPresupuestosImportDialog(_salesRepository);
        if (ShowOwnedDialogPreservingModal1(dlg, GetBestDialogOwner(_itemsSelectorWindow?.ViewModel?.SelectedCaja ?? 1)) == true && dlg.SelectedBudget != null)
        {
            items = dlg.SelectedItems;
            return dlg.SelectedBudget;
        }
        return null;
    }

    public void OpenFacturaLegalWindow(SaleEntity? currentSale = null)
    {
        var win = new EmisionFacturaLegalWindow(_salesRepository, _systemRepository, _ticketPrintService, currentSale);
        ShowOwnedDialogPreservingModal1(win, GetBestDialogOwner(currentSale?.IdCaja ?? 1));
    }

    public void OpenAjusteStockWindow(int initialDeposito = 1)
    {
        var win = new AjusteStockWindow(_productRepository, this, initialDeposito);
        ShowOwnedDialogPreservingModal1(win, GetBestDialogOwner(_itemsSelectorWindow?.ViewModel?.SelectedCaja ?? 1));
    }

    public List<SaleInstallmentEntity>? OpenCuotaVentaDialog(SaleEntity sale, out decimal entregaInicial)
    {
        entregaInicial = 0m;
        var dlg = new CuotaVentaDialog(sale);
        if (ShowOwnedDialogPreservingModal1(dlg, GetBestDialogOwner(sale.IdCaja)) == true)
        {
            entregaInicial = dlg.EntregaInicial;
            return dlg.GeneratedInstallments;
        }
        return null;
    }

    public ProductLotEntity? OpenBuscarLotesDialog(int idProducto, int idDeposito)
    {
        var dlg = new BuscarLotesDialog(_productRepository, idProducto, idDeposito);
        return ShowOwnedDialogPreservingModal1(dlg, GetBestDialogOwner(_itemsSelectorWindow?.ViewModel?.SelectedCaja ?? 1)) == true ? dlg.SelectedLot : null;
    }

    public void OpenCombinarCompuestoDialog(int idProductoCompuesto, string descripcionProducto)
    {
        var dlg = new CombinarCompuestoDialog(_productRepository, this, idProductoCompuesto, descripcionProducto);
        ShowOwnedDialogPreservingModal1(dlg, GetBestDialogOwner(_itemsSelectorWindow?.ViewModel?.SelectedCaja ?? 1));
    }

    public CustomerEntity? OpenQuickCreateCustomerDialog()
    {
        var dlg = new QuickCreateCustomerDialog(_customerRepository);
        return ShowOwnedDialogPreservingModal1(dlg, GetBestDialogOwner(_itemsSelectorWindow?.ViewModel?.SelectedCaja ?? 1)) == true ? dlg.CreatedCustomer : null;
    }

    public SellerEntity? OpenQuickCreateSellerDialog()
    {
        var dlg = new QuickCreateSellerDialog(_systemRepository);
        return ShowOwnedDialogPreservingModal1(dlg, GetBestDialogOwner(_itemsSelectorWindow?.ViewModel?.SelectedCaja ?? 1)) == true ? dlg.CreatedSeller : null;
    }

    private Window? GetBestModuleOwner()
    {
        if (_itemsSelectorWindow != null && _itemsSelectorWindow.IsLoaded && _itemsSelectorWindow.IsVisible)
            return _itemsSelectorWindow.Window;

        var activeSales = _salesWindowsByCaja.Values
            .Select(v => v.Window)
            .FirstOrDefault(w => w.IsLoaded && w.IsVisible && w.IsActive);
        if (activeSales != null)
            return activeSales;

        var mainWin = TryGetAccessibleMainWindow();
        if (mainWin != null && mainWin.IsLoaded && mainWin.IsVisible)
            return mainWin;

        return null;
    }

    private Window? GetBestDialogOwner(int idCaja)
    {
        if (_itemsSelectorWindow != null && _itemsSelectorWindow.IsLoaded && _itemsSelectorWindow.IsVisible)
            return _itemsSelectorWindow.Window;
        if (_salesWindowsByCaja.TryGetValue(idCaja, out var salesWin) && salesWin.IsLoaded && salesWin.IsVisible)
            return salesWin.Window;
        var mainWin = TryGetAccessibleMainWindow();
        if (mainWin != null && mainWin.IsLoaded && mainWin.IsVisible)
            return mainWin;
        return null;
    }

    public void ExecuteVfpMenuCommand(string comando)
    {
        if (string.IsNullOrWhiteSpace(comando))
            return;

        string cmd = comando.Trim().ToLowerInvariant();

        // 1. Exit / Utilities / Config
        if (cmd.Contains("quit") || cmd.Contains("salir")) { Application.Current?.Shutdown(); return; }
        if (cmd.Contains("sys(1037)") || cmd.Contains("__menu:parametros impresora") || cmd.Contains("impresora")) { OpenShortcutsAndPreferencesWindow(3); return; }
        if (cmd.Contains("acercade")) { OpenShortcutsAndPreferencesWindow(8); return; }
        if (cmd.Contains("configsys")) { OpenShortcutsAndPreferencesWindow(1); return; }
        if (cmd.Contains("tranusu")) { OpenExtendedReportsWindow(6); return; }
        if (cmd.Contains("creagrupo") || cmd.Contains("creausu")) { OpenShortcutsAndPreferencesWindow(5); return; }
        if (cmd.Contains("abm_datosempresa") || cmd.Contains("datosempresa")) { OpenShortcutsAndPreferencesWindow(6); return; }
        if (cmd.Contains("servidor")) { OpenShortcutsAndPreferencesWindow(4); return; }

        // 2. Master Catalogs & Cash Registers / Rubros / Exchange Rates
        if (cmd.Contains("crear_timbradolocales") || cmd.Contains("timbrado")) { OpenMasterCatalogsWindow(3); return; }
        if (cmd.Contains("crear_sucursal") || cmd.Contains("sucursal")) { OpenMasterCatalogsWindow(2); return; }
        if (cmd.Contains("abm_cotizaciones") || cmd.Contains("cotizacion")) { OpenCashModule(1); return; }
        if (cmd.Contains("crear_monedas") || cmd.Contains("moneda")) { OpenMasterCatalogsWindow(0); return; }
        if (cmd.Contains("crear_ciudad") || cmd.Contains("ciudad") || cmd.Contains("barrio")) { OpenMasterCatalogsWindow(1); return; }
        if (cmd.Contains("crear_cajas")) { OpenCashModule(2); return; }
        if (cmd.Contains("crear_rubros")) { OpenCashModule(3); return; }
        if (cmd.Contains("crear_movimientocaja") || cmd.Contains("caja")) { OpenCashModule(0); return; }

        // 3. Products, Transfers, Pictures, Adjustments & Product Sub-reports (specific before general!)
        if (cmd.Contains("crear_productos_ajuste") || cmd.Contains("abm_ajusteprecio")) { OpenAjustePreciosMasivoWindow(); return; }
        if (cmd.Contains("crear_trasladodepositos") || cmd.Contains("trasladodepositos")) { OpenTrasladoDepositosWindow(); return; }
        if (cmd.Contains("buscar_imagenproductos") || cmd.Contains("fotoproducto")) { OpenProductImageBrowserWindow(); return; }
        if (cmd.Contains("subgrupoproductos")) { OpenProductsModule(2, null, 1); return; }
        if (cmd.Contains("grupoproductos")) { OpenProductsModule(2, null, 0); return; }
        if (cmd.Contains("marcaproductos")) { OpenProductsModule(2, null, 2); return; }
        if (cmd.Contains("modeloproductos")) { OpenProductsModule(2, null, 3); return; }
        if (cmd.Contains("crear_depositos")) { OpenProductsModule(2, null, 4); return; }
        if (cmd.Contains("crear_ajustes")) { OpenAjusteStockWindow(); return; }
        if (cmd.Contains("informe_listaprecios")) { OpenProductSubReportsWindow(0); return; }
        if (cmd.Contains("informe_productosxdepositos")) { OpenProductSubReportsWindow(1); return; }
        if (cmd.Contains("vencimiento_productos")) { OpenProductSubReportsWindow(2); return; }
        if (cmd.Contains("crear_preciosproductos") || cmd.Contains("abm_consultaproductos")) { OpenProductSubReportsWindow(3); return; }

        // 4. Reports (specific before general!)
        if (cmd.Contains("informe_lucroproductos")) { OpenReportsAndConfigModule(2); return; }
        if (cmd.Contains("informe_lucro")) { OpenReportsAndConfigModule(1); return; }
        if (cmd.Contains("informe_ventasporvendedores")) { OpenExtendedReportsWindow(0); return; }
        if (cmd.Contains("informe_ventasporclientes")) { OpenExtendedReportsWindow(1); return; }
        if (cmd.Contains("informe_ventaspordepositos")) { OpenExtendedReportsWindow(2); return; }
        if (cmd.Contains("informe_productosvendidos")) { OpenExtendedReportsWindow(3); return; }
        if (cmd.Contains("informe_ventasporsupervisores")) { OpenExtendedReportsWindow(4); return; }
        if (cmd.Contains("informe_comprasporproveedores") || cmd.Contains("informe_productoscomprados")) { OpenExtendedReportsWindow(5); return; }
        if (cmd.Contains("informe_solicitudcredito")) { OpenCommercialOperationsWindow(6); return; }
        if (cmd.Contains("informe_comision_venta_por_proveedor")) { OpenCommercialTeamWindow(4); return; }
        if (cmd.Contains("informe_ventas")) { OpenReportsAndConfigModule(0); return; }

        // 5. Commercial Operations (specific before general!)
        if (cmd.Contains("crear_devolucionventas")) { OpenCommercialOperationsWindow(2); return; }
        if (cmd.Contains("crear_devolucioncompras")) { OpenCommercialOperationsWindow(3); return; }
        if (cmd.Contains("crear_pedidos")) { OpenCommercialOperationsWindow(0); return; }
        if (cmd.Contains("crear_presupuestos") || cmd.Contains("presupuesto")) { OpenCommercialOperationsWindow(1); return; }
        if (cmd.Contains("crear_pagoproveedores") || cmd.Contains("crear_cuentapagar")) { OpenCommercialOperationsWindow(4); return; }
        if (cmd.Contains("crear_solicitudcredito_admin")) { OpenCommercialOperationsWindow(6, adminCreditMode: true); return; }
        if (cmd.Contains("crear_solicitudcredito")) { OpenCommercialOperationsWindow(6, adminCreditMode: false); return; }
        if (cmd.Contains("crear_controlmorosos")) { OpenCommercialOperationsWindow(5, onlyOverdue: true); return; }
        if (cmd.Contains("crear_cobroclientes_cuotas") || cmd.Contains("crear_cuentarecibir")) { OpenCommercialOperationsWindow(5); return; }
        if (cmd.Contains("crear_cobroclientes") || cmd.Contains("cobro")) { OpenCustomersModule(1); return; }
        if (cmd.Contains("crear_clientes") || cmd.Contains("cliente")) { OpenCustomersModule(0); return; }
        if (cmd.Contains("crear_proveedores") || cmd.Contains("proveedor")) { OpenPurchasesModule(2); return; }
        if (cmd.Contains("crear_compras") || cmd.Contains("compra")) { OpenPurchasesModule(0); return; }
        if (cmd.Contains("factura_legal") || cmd.Contains("emision_factura_legal")) { OpenFacturaLegalWindow(); return; }
        if (cmd.Contains("crear_ventas") || cmd.Contains("itemsfacturaventas") || cmd.Contains("abmventas") || cmd.Contains("venta"))
        {
            var salesVm = OpenOrActivateSalesWindow(1);
            OpenOrActivateItemsSelector(salesVm, "A", 0, null);
            return;
        }

        // 6. Commercial Team & Agenda/SMS
        if (cmd.Contains("crear_vendedores")) { OpenCommercialTeamWindow(0); return; }
        if (cmd.Contains("crear_supervisores")) { OpenCommercialTeamWindow(1); return; }
        if (cmd.Contains("crear_cobradores")) { OpenCommercialTeamWindow(2); return; }
        if (cmd.Contains("crear_metasvendedores") || cmd.Contains("crear_tablametas")) { OpenCommercialTeamWindow(3); return; }
        if (cmd.Contains("abm_adelantopagosvendedores") || cmd.Contains("crear_recibo")) { OpenCommercialTeamWindow(4); return; }
        if (cmd.Contains("crear_organizer") || cmd.Contains("organizer")) { OpenAgendaAndOrganizerWindow(1); return; }
        if (cmd.Contains("sms_enviar") || cmd.Contains("crear_sms_conexion") || cmd.Contains("sms")) { OpenAgendaAndOrganizerWindow(2); return; }
        if (cmd.Contains("agenda")) { OpenAgendaAndOrganizerWindow(0); return; }

        // 7. RRHH / Personales (specific before general!)
        if (cmd.Contains("abm_salariosparametros")) { OpenHumanResourcesWindow(3); return; }
        if (cmd.Contains("crear_legajos")) { OpenHumanResourcesWindow(0); return; }
        if (cmd.Contains("crear_descuentopersonal")) { OpenHumanResourcesWindow(1); return; }
        if (cmd.Contains("crear_salarios") || cmd.Contains("__menu:historial de salarios")) { OpenHumanResourcesWindow(2); return; }
        if (cmd.Contains("__menu:gestión de vacaciones") || cmd.Contains("__menu:preparar vacaciones")) { OpenHumanResourcesWindow(2, 0, "VACACIONES"); return; }
        if (cmd.Contains("__menu:preparar aguinaldo")) { OpenHumanResourcesWindow(2, 0, "AGUINALDO"); return; }
        if (cmd.Contains("__menu:preparar liquidación")) { OpenHumanResourcesWindow(2, 0, "LIQUIDACION"); return; }
        if (cmd.Contains("__menu:registrar bonificaciones")) { OpenHumanResourcesWindow(2, 0, "BONIFICACION"); return; }
        if (cmd.Contains("crear_nacionalidades")) { OpenHumanResourcesWindow(4, 0); return; }
        if (cmd.Contains("crear_cargos")) { OpenHumanResourcesWindow(4, 1); return; }
        if (cmd.Contains("crear_departamentos")) { OpenHumanResourcesWindow(4, 2); return; }
        if (cmd.Contains("crear_areas")) { OpenHumanResourcesWindow(4, 3); return; }
        if (cmd.Contains("crear_tipodocumentos")) { OpenHumanResourcesWindow(4, 4); return; }
        if (cmd.Contains("crear_descuentos")) { OpenHumanResourcesWindow(4, 5); return; }

        // 8. Restaurant, Veterinaria & Entrega
        if (cmd.Contains("abm_comanda2")) { OpenSpecialtyModulesWindow(0, "Otros"); return; }
        if (cmd.Contains("abm_comanda")) { OpenSpecialtyModulesWindow(0, "Cocina"); return; }
        if (cmd.Contains("abm_mesas")) { OpenSpecialtyModulesWindow(0, "Mesas"); return; }
        if (cmd.Contains("abm_mozos")) { OpenSpecialtyModulesWindow(0, "Mozos"); return; }
        if (cmd.Contains("abm_entrega_mercaderia")) { OpenSpecialtyModulesWindow(2, "Entrega"); return; }
        if (cmd.Contains("avisos_vacunas")) { OpenSpecialtyModulesWindow(1, "Vacunas"); return; }
        if (cmd.Contains("pacientes")) { OpenSpecialtyModulesWindow(1, "Pacientes"); return; }
        if (cmd.Contains("peluqueria")) { OpenSpecialtyModulesWindow(1, "Peluqueria"); return; }
        if (cmd.Contains("avisos")) { OpenSpecialtyModulesWindow(1, "Avisos"); return; }
        if (cmd.Contains("gentorno")) { OpenSpecialtyModulesWindow(1, "Guarderia"); return; }
        if (cmd.Contains("alimentos")) { OpenSpecialtyModulesWindow(1, "Alimentos"); return; }
        if (cmd.Contains("val_nor")) { OpenSpecialtyModulesWindow(1, "ValoresNormales"); return; }
        if (cmd.Contains("parametros")) { OpenSpecialtyModulesWindow(1, "Parametros"); return; }

        // 9. Products / Users fallback
        if (cmd.Contains("producto") || cmd.Contains("ajuste"))
        {
            OpenProductsModule(cmd.Contains("abm_producto") ? 1 : 0);
            return;
        }

        if (cmd.Contains("usu") || cmd.Contains("clave") || cmd.Contains("grupo"))
        {
            OpenShortcutsAndPreferencesWindow(5);
            return;
        }

        OpenReportsAndConfigModule(0);
    }

    public void ShutdownAllWindows()
    {
        _isShuttingDown = true;
        ApplyItemSelectorModalState(false);

        if (_cargaRapidaWindow != null)
        {
            _cargaRapidaWindow.AllowPermanentClose = true;
            _cargaRapidaWindow.Close();
            _cargaRapidaWindow = null;
        }

        if (_itemsSelectorWindow != null)
        {
            _itemsSelectorWindow.Window.IsVisibleChanged -= OnItemsSelectorIsVisibleChanged;
            _itemsSelectorWindow.AllowPermanentClose = true;
            _itemsSelectorWindow.Window.Close();
            _itemsSelectorWindow = null;
        }

        foreach (var win in _salesWindowsByCaja.Values.ToList())
        {
            win.AllowPermanentClose = true;
            win.Window.Close();
        }
        _salesWindowsByCaja.Clear();
        _renovadoPositionedCajas.Clear();

        foreach (Window? w in new Window?[]
        {
            _genesWindow, _productsWindow, _customersWindow, _purchasesWindow,
            _cashWindow, _reportsWindow, _configPrefWindow,
            _masterCatalogsWindow, _agendaWindow, _commercialTeamWindow,
            _extendedReportsWindow, _commercialOpsWindow, _hrWindow, _specialtyWindow
        })
        {
            if (w != null && w.IsLoaded)
                w.Close();
        }
    }

    public void HandleSalesWindowClosedByUser(int idCaja, Window salesWindow)
    {
        _renovadoPositionedCajas.Remove(idCaja);
        GetOpenSalesViewModel(idCaja)?.ResetSaleForWindowClose();

        if (_itemsSelectorWindow != null &&
            (_itemsSelectorWindow.Window.Owner == salesWindow || _itemsSelectorWindow.ViewModel.SelectedCaja == idCaja))
        {
            try
            {
                if (_itemsSelectorWindow.Window.Owner == salesWindow)
                    _itemsSelectorWindow.Window.Owner = null;
            }
            catch { }

            ApplyItemSelectorModalState(false);
            _itemsSelectorWindow.ViewModel.ActualizarEspejo();
            _itemsSelectorWindow.ViewModel.LimpiarCampos();
            if (_itemsSelectorWindow.Window.Visibility == Visibility.Visible)
                _itemsSelectorWindow.Window.Hide();
        }

        DetachOwnerIfMatches(salesWindow, _productsWindow, _cargaRapidaWindow, _genesWindow, _customersWindow, _purchasesWindow);

        Window? preferredTarget = null;
        if (!IsClassicPosStyle())
        {
            var otherVisible = _salesWindowsByCaja
                .Where(kv => kv.Key != idCaja &&
                             kv.Value.Window.Visibility == Visibility.Visible &&
                             kv.Value.Window.WindowState != WindowState.Minimized)
                .OrderByDescending(kv => kv.Key)
                .FirstOrDefault();
            if (otherVisible.Value != null)
            {
                _activeSalesCaja = otherVisible.Key;
                preferredTarget = otherVisible.Value.Window;
            }
        }

        preferredTarget ??= TryGetAccessibleMainWindow();
        SafeHideAndRestoreFocus(salesWindow, preferredTarget);
    }

    public void HandleItemsSelectorClosedByUser(Window selectorWindow)
    {
        ApplyItemSelectorModalState(false);

        Window? preferredTarget = null;
        if (_salesWindowsByCaja.TryGetValue(_activeSalesCaja, out var activeSales) &&
            activeSales.Window.Visibility == Visibility.Visible &&
            activeSales.Window.WindowState != WindowState.Minimized)
        {
            preferredTarget = activeSales.Window;
        }
        preferredTarget ??= TryGetAccessibleMainWindow();

        DetachOwnerIfMatches(selectorWindow, _productsWindow, _cargaRapidaWindow, _genesWindow);
        SafeHideAndRestoreFocus(selectorWindow, preferredTarget);
    }

    public void RefreshItemSelectorModalState()
    {
        bool selectorVisible = _itemsSelectorWindow != null &&
                               _itemsSelectorWindow.Window.Visibility == Visibility.Visible;
        ApplyItemSelectorModalState(selectorVisible);
    }

    private void OnItemsSelectorIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is Window w && !w.IsVisible)
        {
            ApplyItemSelectorModalState(false);
        }
    }

    private void ApplyItemSelectorModalState(bool selectorVisible)
    {
        bool shouldLock = selectorVisible && _configService.Preferences.ItemFacturaModal;
        _isBackgroundLockedByItemModal = shouldLock;

        // Mantener IsEnabled = true en WPF para que las ventanas traseras NO se pongan grises;
        // aplicar bloqueo nativo Win32 EnableWindow(hwnd, false/true) cuando Modal 1 está activo.
        var mainWin = TryGetAccessibleMainWindow();
        if (!shouldLock)
        {
            foreach (IntPtr lockedHwnd in _nativeModalLockedHwnds.ToList())
            {
                if (lockedHwnd != IntPtr.Zero)
                    EnableWindow(lockedHwnd, true);
            }
            _nativeModalLockedHwnds.Clear();

            if (mainWin != null)
            {
                mainWin.IsEnabled = true;
                EnsureNativeWindowEnabled(mainWin);
            }
            foreach (var salesWin in _salesWindowsByCaja.Values)
            {
                salesWin.Window.IsEnabled = true;
                EnsureNativeWindowEnabled(salesWin.Window);
            }
        }
        else
        {
            if (mainWin != null)
            {
                mainWin.IsEnabled = true;
                LockNativeWindowInput(mainWin);
            }
            foreach (var salesWin in _salesWindowsByCaja.Values)
            {
                salesWin.Window.IsEnabled = true;
                LockNativeWindowInput(salesWin.Window);
            }
        }

        if (_itemsSelectorWindow != null)
        {
            _itemsSelectorWindow.Window.IsEnabled = true;
            EnsureNativeWindowEnabled(_itemsSelectorWindow.Window);
        }
    }

    private void LockNativeWindowInput(Window window)
    {
        IntPtr hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd != IntPtr.Zero)
        {
            EnableWindow(hwnd, false);
            _nativeModalLockedHwnds.Add(hwnd);
        }
    }

    private void EnsureNativeWindowEnabled(Window? window)
    {
        if (window == null)
            return;
        IntPtr hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd != IntPtr.Zero)
        {
            EnableWindow(hwnd, true);
            _nativeModalLockedHwnds.Remove(hwnd);
        }
    }

    private static void DetachOwnerIfMatches(Window parentCandidate, params Window?[] ownedWindows)
    {
        foreach (var w in ownedWindows)
        {
            if (w == null)
                continue;
            try
            {
                if (w.Owner == parentCandidate)
                    w.Owner = TryGetAccessibleMainWindow();
            }
            catch { }
        }
    }

    private bool TryKeepModal1SelectorInFocus(Window? closingOrHidingWindow, Window? proposedTarget)
    {
        if (!_isBackgroundLockedByItemModal ||
            _itemsSelectorWindow == null ||
            ReferenceEquals(proposedTarget, _itemsSelectorWindow.Window))
        {
            return false;
        }

        if (_itemsSelectorWindow.Window.IsLoaded &&
            _itemsSelectorWindow.Window.Visibility == Visibility.Visible &&
            !ReferenceEquals(_itemsSelectorWindow.Window, closingOrHidingWindow))
        {
            ApplyItemSelectorModalState(true);
            if (_itemsSelectorWindow.Window.WindowState == WindowState.Minimized)
                _itemsSelectorWindow.Window.WindowState = WindowState.Normal;
            _itemsSelectorWindow.Window.Activate();
            _itemsSelectorWindow.ViewModel.RequestFocusCurrentField();
            return true;
        }

        return false;
    }

    public void SafeHideAndRestoreFocus(Window closingWindow, Window? preferredTarget = null)
    {
        Window? target = ResolveBestFocusTarget(closingWindow, preferredTarget);
        if (!TryKeepModal1SelectorInFocus(closingWindow, target) && target != null)
        {
            target.IsEnabled = true;
            EnsureNativeWindowEnabled(target);
            if (target.WindowState == WindowState.Minimized)
                target.WindowState = WindowState.Normal;
            if (target.Visibility == Visibility.Visible && target.IsLoaded)
                target.Activate();
        }

        if (closingWindow.Visibility == Visibility.Visible)
            closingWindow.Hide();

        Window? deferredTarget = target ?? (_isBackgroundLockedByItemModal ? _itemsSelectorWindow?.Window : null);
        if (deferredTarget != null && !deferredTarget.Dispatcher.HasShutdownStarted)
        {
            deferredTarget.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (TryKeepModal1SelectorInFocus(closingWindow, target))
                    return;

                if (target != null &&
                    target.IsLoaded &&
                    target.Visibility == Visibility.Visible &&
                    target.WindowState != WindowState.Minimized)
                {
                    if (_isBackgroundLockedByItemModal &&
                        _itemsSelectorWindow != null &&
                        ReferenceEquals(target, _itemsSelectorWindow.Window))
                    {
                        RefreshItemSelectorModalState();
                    }
                    else if (_isBackgroundLockedByItemModal &&
                             _itemsSelectorWindow != null &&
                             !ReferenceEquals(target, _itemsSelectorWindow.Window))
                    {
                        return;
                    }

                    target.IsEnabled = true;
                    EnsureNativeWindowEnabled(target);
                    target.Activate();
                    target.Focus();
                    if (ReferenceEquals(target, _itemsSelectorWindow?.Window))
                        _itemsSelectorWindow.ViewModel.RequestFocusCurrentField();
                }
            }), DispatcherPriority.ApplicationIdle);
        }
    }

    private void AttachSafeCloseFocusRestore(Window window)
    {
        window.Closing += (_, _) =>
        {
            var target = ResolveBestFocusTarget(window, null);
            bool keptModal1 = TryKeepModal1SelectorInFocus(window, target);
            if (!keptModal1 && target != null && target.IsLoaded && target.Visibility == Visibility.Visible)
            {
                target.IsEnabled = true;
                EnsureNativeWindowEnabled(target);
                target.Activate();
                target.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (TryKeepModal1SelectorInFocus(window, target))
                        return;

                    if (target.IsLoaded &&
                        target.Visibility == Visibility.Visible &&
                        target.WindowState != WindowState.Minimized)
                    {
                        if (_isBackgroundLockedByItemModal &&
                            _itemsSelectorWindow != null &&
                            ReferenceEquals(target, _itemsSelectorWindow.Window))
                        {
                            RefreshItemSelectorModalState();
                        }
                        else if (_isBackgroundLockedByItemModal &&
                                 _itemsSelectorWindow != null &&
                                 !ReferenceEquals(target, _itemsSelectorWindow.Window))
                        {
                            return;
                        }

                        target.IsEnabled = true;
                        EnsureNativeWindowEnabled(target);
                        target.Activate();
                        target.Focus();
                        if (ReferenceEquals(target, _itemsSelectorWindow?.Window))
                            _itemsSelectorWindow.ViewModel.RequestFocusCurrentField();
                    }
                }), DispatcherPriority.ApplicationIdle);
            }
        };
    }

    private Window? ResolveBestFocusTarget(Window closingWindow, Window? preferredTarget)
    {
        if (_isBackgroundLockedByItemModal &&
            _itemsSelectorWindow != null &&
            !ReferenceEquals(_itemsSelectorWindow.Window, closingWindow) &&
            IsValidVisibleTarget(_itemsSelectorWindow.Window, closingWindow))
        {
            return _itemsSelectorWindow.Window;
        }

        if (IsValidVisibleTarget(preferredTarget, closingWindow))
            return preferredTarget;

        try
        {
            if (IsValidVisibleTarget(closingWindow.Owner, closingWindow))
                return closingWindow.Owner;
        }
        catch { }

        if (_itemsSelectorWindow != null &&
            !ReferenceEquals(_itemsSelectorWindow.Window, closingWindow) &&
            IsValidVisibleTarget(_itemsSelectorWindow.Window, closingWindow))
        {
            return _itemsSelectorWindow.Window;
        }

        if (_salesWindowsByCaja.TryGetValue(_activeSalesCaja, out var actSales) &&
            IsValidVisibleTarget(actSales.Window, closingWindow))
        {
            return actSales.Window;
        }

        var mainWin = TryGetAccessibleMainWindow();
        return IsValidVisibleTarget(mainWin, closingWindow) ? mainWin : null;
    }

    private static bool IsValidVisibleTarget(Window? candidate, Window closingWindow)
        => candidate != null &&
           !ReferenceEquals(candidate, closingWindow) &&
           candidate.Visibility == Visibility.Visible &&
           candidate.WindowState != WindowState.Minimized;

    private static Window? TryGetAccessibleMainWindow()
    {
        var app = Application.Current;
        if (app == null || !app.Dispatcher.CheckAccess())
            return null;

        return app.MainWindow;
    }

    private static void ShowRestoreAndActivate(Window window)
    {
        var mainWin = TryGetAccessibleMainWindow();
        if (window.Owner == null &&
            mainWin != null &&
            mainWin != window &&
            mainWin.IsLoaded &&
            mainWin.IsVisible)
        {
            window.Owner = mainWin;
        }

        if (window.Visibility != Visibility.Visible || !window.IsVisible)
        {
            window.Show();
        }

        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.Activate();
        window.Focus();
    }

    private static void ShowRestoreAndActivateWithOwner(Window window, Window? explicitOwner)
    {
        if (explicitOwner != null &&
            explicitOwner != window &&
            explicitOwner.IsLoaded &&
            explicitOwner.Visibility == Visibility.Visible)
        {
            try
            {
                if (window.Owner != explicitOwner)
                    window.Owner = explicitOwner;
            }
            catch { }
        }

        ShowRestoreAndActivate(window);
    }
}
