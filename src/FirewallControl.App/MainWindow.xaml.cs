using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using FirewallControl.App.Services;
using FirewallControl.App.Views;
using FirewallControl.Core.Business;
using FirewallControl.Core.Entities;
using FirewallControl.Core.Interfaces;
using FirewallControl.Infrastructure.Database;
using FirewallControl.Infrastructure.Repositories;

namespace FirewallControl.App;

/// <summary>
/// Ventana Principal (_FONDO): integra la estética moderna C# Atelier con los 4 KPIs en vivo,
/// pestañas Últimas Ventas / Top Vendidos, menú dinámico DBF completo con matriz `caccesos`,
/// flags `btn*` y `tipovendedor`, paneles condicionales Veterinaria/Restaurant y atajos clásicos.
/// </summary>
public partial class MainWindow : Window
{
    private readonly IConfigService _configService;
    private readonly ILicenseService _licenseService;
    private readonly IVfpDbfService _dbfService;
    private readonly ISystemRepository _systemRepository;
    private readonly ICashRepository _cashRepository;
    private readonly ISalesRepository _salesRepository;
    private readonly IWindowManagerService _windowManager;
    private readonly IStage3Repository _stage3Repository;
    private readonly DispatcherTimer _clockAndRatesTimer;
    private readonly DispatcherTimer _multiPcSyncTimer;
    private CompanyDataEntity _companyData = new();
    private int _lastKnownMaxNroVenta;
    private decimal _lastCajaNetoHoy;
    private decimal _lastVentasTotalHoy;
    private bool _isFactoryOffline;

    public MainWindow(
        IConfigService configService,
        ILicenseService licenseService,
        IVfpDbfService dbfService,
        ISystemRepository systemRepository,
        ICashRepository cashRepository,
        ISalesRepository salesRepository,
        IWindowManagerService windowManager,
        IStage3Repository? stage3Repository = null)
    {
        InitializeComponent();
        AppIconHelper.ApplySefirosIcon(this);
        _configService = configService;
        _licenseService = licenseService;
        _dbfService = dbfService;
        _systemRepository = systemRepository;
        _cashRepository = cashRepository;
        _salesRepository = salesRepository;
        _windowManager = windowManager;
        _stage3Repository = stage3Repository ?? new Stage3Repository(new MySqlConnectionFactory(configService));

        _clockAndRatesTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
        _clockAndRatesTimer.Tick += async (_, _) => await RefreshDashboardAndRatesAsync();

        int syncSec = Math.Clamp(_configService.Preferences.IntervaloSincronizacionSegundos, 2, 60);
        _multiPcSyncTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(syncSec) };
        _multiPcSyncTimer.Tick += async (_, _) => await RefreshMultiPcLiveSyncAsync();

        Loaded += MainWindow_Loaded;
        Activated += (_, _) => UpdateStatusBadges();
        ThemeService.ThemeChanged += OnThemeChanged;
        LocalizationService.LanguageChanged += OnLanguageChanged;
        Closed += (_, _) =>
        {
            ThemeService.ThemeChanged -= OnThemeChanged;
            LocalizationService.LanguageChanged -= OnLanguageChanged;
            _clockAndRatesTimer.Stop();
            _multiPcSyncTimer.Stop();
            _windowManager.ShutdownAllWindows();
        };
    }

    private void OnLanguageChanged()
    {
        if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
            return;
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(OnLanguageChanged);
            return;
        }
        UpdateStatusBadges();
    }

    private void OnThemeChanged()
    {
        if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
            return;
        if (!Dispatcher.CheckAccess())
            Dispatcher.BeginInvoke(UpdateStatusBadges);
        else
            UpdateStatusBadges();
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _licenseService.VerifyLicense();
        LblLicenseStatus.Text = $"✔ {_licenseService.RegisteredCompany}";
        LblLicenseSerial.Text = $"Licencia: {_licenseService.LicenseKeyDisplay}";

        var todayText = DateTime.Now.ToString("dddd, d 'de' MMMM 'de' yyyy", new CultureInfo("es-ES"));
        LblTodayDate.Text = string.IsNullOrEmpty(todayText)
            ? DateTime.Now.ToShortDateString()
            : char.ToUpper(todayText[0]) + todayText[1..];

        UpdateStatusBadges();
        BuildDynamicMenu(_companyData.Veterinaria == 1);
        LblClock.Text = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");

        try
        {
            if (_configService.Preferences.SolicitarLoginAlIniciar)
            {
                var loginWin = new LoginWindow(_systemRepository) { Owner = this };
                if (loginWin.ShowDialog() == true && loginWin.AuthenticatedUser != null)
                {
                    _windowManager.CurrentUser = loginWin.AuthenticatedUser;
                    _windowManager.CurrentUserGroup = await ResolveUserGroupAsync(loginWin.AuthenticatedUser, loginWin.AuthenticatedGroup);
                }
            }
            else
            {
                var adminUser = await _systemRepository.GetUserByUsernameAsync("ADMINISTRA");
                if (adminUser != null)
                {
                    _windowManager.CurrentUser = adminUser;
                    _windowManager.CurrentUserGroup = await ResolveUserGroupAsync(adminUser, null);
                }
            }

            _companyData = await _systemRepository.GetCompanyDataAsync();
            if (!string.IsNullOrWhiteSpace(_companyData.Descripcion))
            {
                LblEmpresaNombre.Text = _companyData.Descripcion;
                LblEmpresaSub.Text = $"RUC: {_companyData.Ruc} | Dir: {_companyData.Direccion} | Tel: {_companyData.Telefonos}";
            }

            ApplyCompanyModesAndUserPermissions();
            BuildDynamicMenu(_companyData.Veterinaria == 1);
            await RefreshDashboardAndRatesAsync();
            await RefreshMultiPcLiveSyncAsync();

            _clockAndRatesTimer.Start();
            _multiPcSyncTimer.Start();

            var salesVm = _windowManager.OpenOrActivateSalesWindow(1);
            _windowManager.OpenOrActivateItemsSelector(salesVm, "A", 0, null);
        }
        catch (Exception)
        {
            _isFactoryOffline = true;
            LblEmpresaNombre.Text = "Sefiros POS";
            LblEmpresaSub.Text = LocalizationService.Get("Str_Factory_OfflineNotice", "Inicio de fábrica listo · Configura tu servidor MySQL desde Ajustes [F4] o 🌐 MySQL.");
            LblSyncBadge.Text = LocalizationService.Get("Str_Factory_ModeBadge", "● MODO FÁBRICA");
            ApplyCompanyModesAndUserPermissions();
            BuildDynamicMenu(false);
        }
    }

    private async Task<UserGroupEntity> ResolveUserGroupAsync(UserEntity user, UserGroupEntity? preferred)
    {
        try
        {
            var allGroups = await _systemRepository.GetAllGroupsAsync();
            var byUser = allGroups.FirstOrDefault(g =>
                !string.IsNullOrWhiteSpace(g.Cidusuario) &&
                string.Equals(g.Cidusuario.Trim(), user.Cidusuario.Trim(), StringComparison.OrdinalIgnoreCase));
            if (byUser != null) return byUser;

            var byGroup = allGroups.FirstOrDefault(g =>
                string.Equals(g.Cidgrupo.Trim(), user.Cidgrupo.Trim(), StringComparison.OrdinalIgnoreCase));
            if (byGroup != null) return byGroup;
        }
        catch { }

        return preferred ?? new UserGroupEntity { Cidgrupo = user.Cidgrupo };
    }

    private void ApplyCompanyModesAndUserPermissions()
    {
        UpdateStatusBadges();

        var cfg = _configService.Config;
        var user = _windowManager.CurrentUser;
        var grp = _windowManager.CurrentUserGroup;
        bool isAdmin = VfpPermissionHelper.IsAdmin(user, grp);
        bool vendorRestricted = VfpPermissionHelper.IsVendorToolbarRestricted(user, grp);

        string modeText = _companyData.Veterinaria == 1 && _companyData.Restaurant == 1
            ? "Sistema Comercial + Veterinaria + Restaurant"
            : _companyData.Veterinaria == 1
                ? "Sistema Comercial y Veterinaria"
                : _companyData.Restaurant == 1
                    ? "Sistema Comercial y Restaurant"
                    : "Sistema Comercial POS";
        LblModeBadge.Text = modeText;

        PanelVeterinaria.Visibility = _companyData.Veterinaria == 1 ? Visibility.Visible : Visibility.Collapsed;
        PanelRestaurant.Visibility = _companyData.Restaurant == 1 ? Visibility.Visible : Visibility.Collapsed;

        // Top nav & operational tiles: visibility via `grupos.btn*` (unless ADMIN), enabled via `tipovendedor`
        bool showProd = isAdmin || grp.BtnProducto == 1;
        bool showCompra = isAdmin || grp.BtnCompra == 1;
        bool showVenta = isAdmin || grp.BtnVenta == 1;
        bool showCliente = isAdmin || grp.BtnCliente == 1;
        bool showProv = isAdmin || grp.BtnProveedor == 1;
        bool showVend = isAdmin || grp.BtnVendedor == 1;
        bool showCobro = isAdmin || grp.BtnCobro == 1;
        bool showPago = isAdmin || grp.BtnPago == 1;
        bool showCaja = isAdmin || grp.BtnMovCaja == 1;
        bool showPedido = isAdmin || grp.BtnPedido == 1;
        bool showPresu = isAdmin || grp.BtnPresu == 1;
        bool showVenci = cfg.MostrarVencimiento == 1 || (!isAdmin && grp.BtnVenci == 1);

        BtnNavProductos.Visibility = showProd ? Visibility.Visible : Visibility.Collapsed;
        BtnNavClientes.Visibility = showCliente ? Visibility.Visible : Visibility.Collapsed;
        BtnNavProveedores.Visibility = showProv ? Visibility.Visible : Visibility.Collapsed;
        BtnNavVendedores.Visibility = showVend ? Visibility.Visible : Visibility.Collapsed;
        BtnNavCobros.Visibility = showCobro ? Visibility.Visible : Visibility.Collapsed;
        BtnNavPagos.Visibility = showPago ? Visibility.Visible : Visibility.Collapsed;
        BtnNavCajaMov.Visibility = showCaja ? Visibility.Visible : Visibility.Collapsed;

        // VFP tipovendedor restriction disables toolbar quick-access pills
        BtnNavProductos.IsEnabled = !vendorRestricted;
        BtnNavClientes.IsEnabled = !vendorRestricted;
        BtnNavProveedores.IsEnabled = !vendorRestricted;
        BtnNavVendedores.IsEnabled = !vendorRestricted;
        BtnNavCobros.IsEnabled = !vendorRestricted;
        BtnNavPagos.IsEnabled = !vendorRestricted;
        BtnNavCajaMov.IsEnabled = !vendorRestricted;

        BtnTileVenta.Visibility = showVenta ? Visibility.Visible : Visibility.Collapsed;
        BtnTileCompra.Visibility = showCompra ? Visibility.Visible : Visibility.Collapsed;
        BtnTilePedido.Visibility = showPedido ? Visibility.Visible : Visibility.Collapsed;
        BtnTilePresup.Visibility = showPresu ? Visibility.Visible : Visibility.Collapsed;
        BtnTileClientes.Visibility = showCliente ? Visibility.Visible : Visibility.Collapsed;
        BtnTileAgenda.Visibility = vendorRestricted ? Visibility.Collapsed : Visibility.Visible;

        BtnTileVencimientos.Visibility = showVenci ? Visibility.Visible : Visibility.Collapsed;
        BtnTileCobroCaja.Visibility = _companyData.CobroCaja == 1 ? Visibility.Visible : Visibility.Collapsed;
        BtnTileBalanza.Visibility = cfg.Balanza == 1 ? Visibility.Visible : Visibility.Collapsed;
        BtnTileEntrega.Visibility = cfg.EntregaMercaderia == 1 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateStatusBadges()
    {
        var cfg = _configService.Config;
        var pref = _configService.Preferences;
        var baseCur = LatamCurrencyCatalog.GetByCode(pref.MonedaBaseIso);

        LblKpiCajaNeto.Text = LatamCurrencyCatalog.FormatAmount(_lastCajaNetoHoy, baseCur.IsoCode);
        LblKpiVentasTotal.Text = LatamCurrencyCatalog.FormatAmount(_lastVentasTotalHoy, baseCur.IsoCode);
        if (_isFactoryOffline)
            LblEmpresaSub.Text = LocalizationService.Get("Str_Factory_OfflineNotice", LblEmpresaSub.Text);

        LblServerInfo.Text = $"{Environment.MachineName} · {cfg.SqlServerHost}:{cfg.Port}/{cfg.BaseDatos} · {baseCur.IsoCode} · {LocalizationService.CurrentLanguage.ToUpperInvariant()}";
        LblLiveServerDetail.Text = $"MySQL: {cfg.SqlServerHost}:{cfg.Port} ({cfg.BaseDatos}) · Terminal Caja #{cfg.IdCaja}";

        var u = _windowManager.CurrentUser;
        var g = _windowManager.CurrentUserGroup;
        string groupName = string.IsNullOrWhiteSpace(g.Cidgrupo) ? u.Cidgrupo : g.Cidgrupo;
        LblCurrentUser.Text = $"{u.Cidusuario} [{groupName}] ({u.Cdescribe})";
        LblCurrentUserTop.Text = string.IsNullOrWhiteSpace(u.Cdescribe) ? u.Cidusuario : u.Cdescribe;
        LblCurrentGroupTop.Text = $"Grupo: {groupName}";

        if (pref.NoControlarStockGlobal)
        {
            BadgeStockGlobal.Background = ResolveBrush("WarningBgBrush", Color.FromRgb(254, 243, 199));
            BadgeStockGlobal.BorderBrush = ResolveBrush("WarningBorderBrush", Color.FromRgb(253, 230, 138));
            LblBadgeStockGlobal.Foreground = ResolveBrush("WarningTextBrush", Color.FromRgb(146, 64, 14));
            LblBadgeStockGlobal.Text = "● MODO LIBRE: SIN CONTROL DE STOCK";
        }
        else
        {
            BadgeStockGlobal.Background = ResolveBrush("SuccessBgBrush", Color.FromRgb(236, 253, 245));
            BadgeStockGlobal.BorderBrush = ResolveBrush("SuccessBorderBrush", Color.FromRgb(167, 243, 208));
            LblBadgeStockGlobal.Foreground = ResolveBrush("SuccessTextBrush", Color.FromRgb(6, 95, 70));
            LblBadgeStockGlobal.Text = "● CONTROL DE STOCK ACTIVO";
        }
    }

    private static Brush ResolveBrush(string key, Color fallback) =>
        Application.Current?.TryFindResource(key) as Brush ?? new SolidColorBrush(fallback);

    private async Task RefreshDashboardAndRatesAsync()
    {
        LblClock.Text = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");

        try
        {
            var dash = await _stage3Repository.GetFondoDashboardDataAsync(_windowManager.CurrentUser.Cidusuario);
            LblKpiProductos.Text = dash.TotalProductosActivos.ToString("N0");
            LblKpiClientesSub.Text = dash.TotalClientes == 1
                ? "1 cliente activo"
                : $"{dash.TotalClientes:N0} clientes activos";

            _lastCajaNetoHoy = dash.FlujoNetoCajaHoy;
            _lastVentasTotalHoy = dash.VentasTotalHoy;
            string baseIso = _configService.Preferences.MonedaBaseIso;

            LblKpiCajaNeto.Text = LatamCurrencyCatalog.FormatAmount(dash.FlujoNetoCajaHoy, baseIso);
            LblKpiCajaNeto.Foreground = dash.FlujoNetoCajaHoy < 0m
                ? ResolveBrush("DangerTextBrush", Color.FromRgb(185, 28, 28))
                : ResolveBrush("SuccessTextBrush", Color.FromRgb(21, 128, 61));
            LblKpiCajaSub.Text = $"Ing: {dash.IngresosCajaHoy:N0} · Egr: {dash.EgresosCajaHoy:N0}";

            LblKpiVentasTotal.Text = LatamCurrencyCatalog.FormatAmount(dash.VentasTotalHoy, baseIso);
            LblKpiVentasSub.Text = $"{dash.VentasCountHoy} {(dash.VentasCountHoy == 1 ? "op" : "ops")} · Última #{_lastKnownMaxNroVenta}";

            LblKpiAgenda.Text = dash.AgendaPendientesHoy == 1
                ? "1 pendiente"
                : $"{dash.AgendaPendientesHoy} pendientes";
            LblKpiAgenda.Foreground = dash.AgendaPendientesHoy > 0
                ? ResolveBrush("WarningTextBrush", Color.FromRgb(180, 83, 9))
                : ResolveBrush("TextPrimaryBrush", Color.FromRgb(24, 24, 27));
            LblKpiAgendaSub.Text = dash.AgendaPendientesHoy > 0
                ? "Revisar agenda de hoy"
                : "Sin alertas pendientes hoy";
            LblTileAgendaBadge.Text = dash.AgendaPendientesHoy > 0
                ? $"🔔 {dash.AgendaPendientesHoy} alerta(s) para hoy"
                : "Sin alertas pendientes hoy";

            GridUltimasVentas.ItemsSource = dash.UltimasVentas;
            GridTopVendidos.ItemsSource = dash.TopVendidos;
            LblTopPeriodo.Text = dash.TopIsFallback7Days
                ? "RANKING ÚLTIMOS 7 DÍAS (SIN VENTAS HOY)"
                : "RANKING DE HOY";
        }
        catch { }

        try
        {
            var rate = await _cashRepository.GetLatestExchangeRateAsync();
            LblCompraDolar.Text = rate.CompraDolar.ToString("N2");
            LblVentaDolar.Text = rate.VentaDolar.ToString("N2");
            LblCompraReal.Text = rate.CompraReal.ToString("N2");
            LblVentaReal.Text = rate.VentaReal.ToString("N2");
            LblCompraPeso.Text = rate.CompraPeso.ToString("N2");
            LblVentaPeso.Text = rate.VentaPeso.ToString("N2");
            LblCompraEuro.Text = rate.CompraEuro.ToString("N2");
            LblVentaEuro.Text = rate.VentaEuro.ToString("N2");
            LblRatesUpdated.Text = $"Última actualización: {DateTime.Now:HH:mm:ss} · Automática";
        }
        catch { }
    }

    private async Task RefreshMultiPcLiveSyncAsync()
    {
        UpdateStatusBadges();
        if (!_configService.Preferences.MulticonexionTiempoRealActiva)
        {
            LblSyncBadge.Text = "● PAUSADO";
            return;
        }

        try
        {
            var (_, maxNroVenta, totalHoy) = await _salesRepository.GetLiveSyncStatusAsync();
            _isFactoryOffline = false;
            _lastVentasTotalHoy = totalHoy;
            LblSyncBadge.Text = "● EN LÍNEA";
            LblKpiVentasTotal.Text = LatamCurrencyCatalog.FormatAmount(totalHoy, _configService.Preferences.MonedaBaseIso);

            if (maxNroVenta > _lastKnownMaxNroVenta)
            {
                _lastKnownMaxNroVenta = maxNroVenta;
                await RefreshDashboardAndRatesAsync();
                foreach (var vm in _windowManager.GetAllOpenSalesViewModels())
                {
                    vm.SyncFromMultiTerminal(maxNroVenta);
                }
            }
        }
        catch
        {
            LblSyncBadge.Text = _isFactoryOffline
                ? LocalizationService.Get("Str_Factory_ModeBadge", "● MODO FÁBRICA")
                : "● SIN RED";
        }
    }

    private void BuildDynamicMenu(bool includeVeterinaria)
    {
        DynamicMainMenu.Items.Clear();

        string baseDir = string.IsNullOrWhiteSpace(_configService.Config.BaseDirectory)
            ? AppDomain.CurrentDomain.BaseDirectory
            : _configService.Config.BaseDirectory;
        string stylesDir = Path.Combine(baseDir, "Styles");

        var user = _windowManager.CurrentUser;
        var grp = _windowManager.CurrentUserGroup;

        var menuPrompts = _dbfService.LoadMenuStructure(stylesDir, includeVeterinaria);
        foreach (var prompt in menuPrompts)
        {
            bool promptAllowed = prompt.Activo &&
                                 VfpPermissionHelper.IsMenuPromptAllowed(user, grp, prompt.Referencia);

            var topItem = new MenuItem
            {
                Header = prompt.CleanTitle,
                IsEnabled = promptAllowed,
                Padding = new Thickness(10, 4, 10, 4)
            };

            foreach (var bar in prompt.Children)
            {
                bool barAllowed = promptAllowed &&
                                  bar.Activo &&
                                  VfpPermissionHelper.IsMenuBarAllowed(user, grp, prompt.Referencia, bar.ParentNu);

                var childMenuItem = CreateMenuBarItem(bar, barAllowed);
                topItem.Items.Add(childMenuItem);
                if (bar.Separador)
                    topItem.Items.Add(new Separator());
            }

            DynamicMainMenu.Items.Add(topItem);
        }
    }

    private MenuItem CreateMenuBarItem(MenuBarItem bar, bool allowed)
    {
        bool hasSubItems = bar.SubMenu && bar.SubItems.Count > 0;
        string effectiveCmd = !string.IsNullOrWhiteSpace(bar.Comando)
            ? bar.Comando
            : (!hasSubItems && bar.Activo ? $"__menu:{bar.CleanTitle.Trim()}" : string.Empty);

        var item = new MenuItem
        {
            Header = bar.CleanTitle,
            Tag = effectiveCmd,
            IsEnabled = allowed && (!string.IsNullOrWhiteSpace(effectiveCmd) || hasSubItems)
        };

        if (hasSubItems)
        {
            foreach (var sub in bar.SubItems)
            {
                item.Items.Add(CreateMenuBarItem(sub, allowed && sub.Activo));
                if (sub.Separador)
                    item.Items.Add(new Separator());
            }
        }
        else if (!string.IsNullOrWhiteSpace(effectiveCmd))
        {
            item.Click += (_, _) => _windowManager.ExecuteVfpMenuCommand(effectiveCmd);
        }

        return item;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        string combo = KeyShortcutHelper.NormalizeKeyEvent(e);
        var binding = ShortcutCatalog.ResolveTriggeredBinding(
            _configService.Preferences,
            ShortcutCatalog.WinFondoInicio,
            combo,
            isInputEmpty: true);

        if (binding == null) return;

        switch (binding.ActionCode)
        {
            case "CAJA_1":
            case "NUEVA_VENTA":
                e.Handled = true;
                _windowManager.SaltarACaja(1);
                return;
            case "SELECTOR":
                e.Handled = true;
                var salesVm = _windowManager.OpenOrActivateSalesWindow(1);
                _windowManager.OpenOrActivateItemsSelector(salesVm, "A", 0, null);
                return;
            case "PRODUCTOS":
                e.Handled = true;
                _windowManager.OpenProductsModule(0);
                return;
            case "NUEVA_COMPRA":
                e.Handled = true;
                _windowManager.OpenPurchasesModule(0);
                return;
            case "PEDIDOS":
                e.Handled = true;
                _windowManager.ExecuteVfpMenuCommand("Do Form Crear_pedidos");
                return;
            case "PRESUPUESTOS":
                e.Handled = true;
                _windowManager.ExecuteVfpMenuCommand("Do Form Crear_presupuestos");
                return;
            case "CONFIGURACION":
                e.Handled = true;
                _windowManager.OpenShortcutsAndPreferencesWindow(0, ShortcutCatalog.WinItemsFactura);
                return;
            case "CAMBIAR_USUARIO":
                e.Handled = true;
                BtnCambiarUsuario_Click(this, new RoutedEventArgs());
                return;
        }
    }

    private void FeedGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        => _windowManager.OpenReportsAndConfigModule(FeedTabs.SelectedIndex == 1 ? 2 : 0);

    private async void BtnRefreshDashboard_Click(object sender, RoutedEventArgs e)
    {
        _companyData = await _systemRepository.GetCompanyDataAsync();
        ApplyCompanyModesAndUserPermissions();
        BuildDynamicMenu(_companyData.Veterinaria == 1);
        await RefreshDashboardAndRatesAsync();
    }

    private void BtnCaja1_Click(object sender, RoutedEventArgs e) => _windowManager.SaltarACaja(1);
    private void BtnMinimizar_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void BtnProductos_Click(object sender, RoutedEventArgs e) => _windowManager.OpenProductsModule(0);
    private void BtnClientes_Click(object sender, RoutedEventArgs e) => _windowManager.OpenCustomersModule(0);
    private void BtnCompras_Click(object sender, RoutedEventArgs e) => _windowManager.OpenPurchasesModule(0);
    private void BtnProveedores_Click(object sender, RoutedEventArgs e) => _windowManager.OpenPurchasesModule(2);
    private void BtnVendedores_Click(object sender, RoutedEventArgs e) => _windowManager.ExecuteVfpMenuCommand("Do Form Crear_vendedores");
    private void BtnCobros_Click(object sender, RoutedEventArgs e) => _windowManager.OpenCustomersModule(1);
    private void BtnPagos_Click(object sender, RoutedEventArgs e) => _windowManager.ExecuteVfpMenuCommand("Do Form Crear_pagoproveedores");
    private void BtnCajaMov_Click(object sender, RoutedEventArgs e) => _windowManager.OpenCashModule(0);
    private void BtnInformes_Click(object sender, RoutedEventArgs e) => _windowManager.OpenReportsAndConfigModule(0);
    private void BtnInformeLucro_Click(object sender, RoutedEventArgs e) => _windowManager.OpenReportsAndConfigModule(1);

    private void BtnPedidos_Click(object sender, RoutedEventArgs e) => _windowManager.ExecuteVfpMenuCommand("Do Form Crear_pedidos");
    private void BtnPresupuestos_Click(object sender, RoutedEventArgs e) => _windowManager.ExecuteVfpMenuCommand("Do Form Crear_presupuestos");
    private void BtnAgenda_Click(object sender, RoutedEventArgs e) => _windowManager.ExecuteVfpMenuCommand("Do Form Agenda");
    private void BtnRrhh_Click(object sender, RoutedEventArgs e) => _windowManager.ExecuteVfpMenuCommand("Do Form Crear_legajos");
    private void BtnVencimientos_Click(object sender, RoutedEventArgs e) => _windowManager.OpenProductSubReportsWindow(2);
    private void BtnCobroCaja_Click(object sender, RoutedEventArgs e) => _windowManager.OpenCashModule(0);
    private void BtnBalanza_Click(object sender, RoutedEventArgs e) => _windowManager.OpenGenesSelectorFull(1, "SELECT");
    private void BtnEntregaMercaderia_Click(object sender, RoutedEventArgs e) => _windowManager.ExecuteVfpMenuCommand("Do Form Abm_entrega_mercaderia");

    private void BtnOtrosModulos_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();

        void AddEntry(string header, Action onClick)
        {
            var mi = new MenuItem { Header = header };
            mi.Click += (_, _) => onClick();
            menu.Items.Add(mi);
        }

        AddEntry("⚡ Carga Rápida de Productos (IA)", () => _windowManager.OpenCargaRapidaProductos());
        AddEntry("🧾 Emisión Factura Legal", () => _windowManager.OpenFacturaLegalWindow());
        AddEntry("🏷 Ajuste Masivo de Precios", () => _windowManager.OpenAjustePreciosMasivoWindow());
        AddEntry("🔄 Traslados entre Depósitos", () => _windowManager.OpenTrasladoDepositosWindow());
        AddEntry("📅 Vencimientos de Productos", () => _windowManager.OpenProductSubReportsWindow(2));
        AddEntry("⚖ Ventas Balanza (Genes)", () => _windowManager.OpenGenesSelector(1));
        AddEntry("📦 Entrega de Mercadería", () => _windowManager.OpenSpecialtyModulesWindow(2, "Entrega"));
        menu.Items.Add(new Separator());
        AddEntry("↩ Devoluciones (Ventas / Compras)", () => _windowManager.OpenCommercialOperationsWindow(2));
        AddEntry("💳 Control de Morosos y Crédito", () => _windowManager.OpenCommercialOperationsWindow(5, onlyOverdue: true));
        AddEntry("📊 Informes Extendidos", () => _windowManager.OpenExtendedReportsWindow(0));
        AddEntry("🗂 Catálogos Maestros (Monedas, Ciudades, Sucursales, Timbrados)", () => _windowManager.OpenMasterCatalogsWindow(0));
        menu.Items.Add(new Separator());
        AddEntry("🐾 Módulo Veterinaria", () => _windowManager.OpenSpecialtyModulesWindow(1, "Pacientes"));
        AddEntry("🍽 Módulo Restaurant y Comandas", () => _windowManager.OpenSpecialtyModulesWindow(0, "Cocina"));

        var cajasMenu = new MenuItem { Header = "💻 Cajas POS" };
        for (int caja = 1; caja <= 5; caja++)
        {
            int targetCaja = caja;
            var cajaItem = new MenuItem { Header = $"Caja {targetCaja}" };
            cajaItem.Click += (_, _) => _windowManager.OpenOrActivateSalesWindow(targetCaja);
            cajasMenu.Items.Add(cajaItem);
        }
        menu.Items.Add(cajasMenu);

        if (sender is UIElement anchor)
        {
            menu.PlacementTarget = anchor;
            menu.Placement = PlacementMode.Bottom;
        }
        menu.IsOpen = true;
    }

    // Conditional Veterinaria & Restaurant buttons
    private void BtnVetPacientes_Click(object sender, RoutedEventArgs e) => _windowManager.ExecuteVfpMenuCommand("Do Form Pacientes");
    private void BtnVetPeluqueria_Click(object sender, RoutedEventArgs e) => _windowManager.ExecuteVfpMenuCommand("Do Form Peluqueria");
    private void BtnVetVacunas_Click(object sender, RoutedEventArgs e) => _windowManager.ExecuteVfpMenuCommand("Do Form Avisos_vacunas");
    private void BtnVetAvisos_Click(object sender, RoutedEventArgs e) => _windowManager.ExecuteVfpMenuCommand("Do Form Avisos");
    private void BtnVetGuarderia_Click(object sender, RoutedEventArgs e) => _windowManager.ExecuteVfpMenuCommand("Do Form Gentorno.scx");
    private void BtnVetAlimentos_Click(object sender, RoutedEventArgs e) => _windowManager.ExecuteVfpMenuCommand("Do Form Alimentos.scx");

    private void BtnRestCocina_Click(object sender, RoutedEventArgs e) => _windowManager.ExecuteVfpMenuCommand("Do Form abm_comanda");
    private void BtnRestMesas_Click(object sender, RoutedEventArgs e) => _windowManager.ExecuteVfpMenuCommand("Do Form abm_mesas");
    private void BtnRestMozos_Click(object sender, RoutedEventArgs e) => _windowManager.ExecuteVfpMenuCommand("Do Form abm_mozos");
    private void BtnRestOtros_Click(object sender, RoutedEventArgs e) => _windowManager.ExecuteVfpMenuCommand("Do Form abm_comanda2");

    private void BtnConfiguracionPreferenciasDirect_Click(object sender, RoutedEventArgs e) => _windowManager.OpenShortcutsAndPreferencesWindow(0, ShortcutCatalog.WinItemsFactura);
    private void BtnAbrirPreferenciasStock_Click(object sender, RoutedEventArgs e) => _windowManager.OpenShortcutsAndPreferencesWindow(1, ShortcutCatalog.WinItemsFactura);
    private void BtnAbrirImpresoraTicket_Click(object sender, RoutedEventArgs e) => _windowManager.OpenShortcutsAndPreferencesWindow(3, ShortcutCatalog.WinItemsFactura);
    private void BtnServidor_Click(object sender, RoutedEventArgs e) => _windowManager.OpenShortcutsAndPreferencesWindow(4, ShortcutCatalog.WinItemsFactura);
    private void BtnAcercaDe_Click(object sender, RoutedEventArgs e) => _windowManager.OpenShortcutsAndPreferencesWindow(8, ShortcutCatalog.WinItemsFactura);

    private void BtnOpenThemePicker_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new ColorAtelierDialog(_configService.Preferences.TemaAtelier) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            _configService.Preferences.TemaAtelier = dlg.SelectedThemeKey;
            _configService.SavePreferences();
            ThemeService.ApplyTheme(
                dlg.SelectedThemeKey,
                _configService.Preferences.TemaOscuroConfiguracion,
                _configService.Preferences.PaletaColores);
        }
    }

    private async void BtnCambiarUsuario_Click(object sender, RoutedEventArgs e)
    {
        var loginWin = new LoginWindow(_systemRepository) { Owner = this };
        if (loginWin.ShowDialog() == true && loginWin.AuthenticatedUser != null)
        {
            _windowManager.CurrentUser = loginWin.AuthenticatedUser;
            _windowManager.CurrentUserGroup = await ResolveUserGroupAsync(loginWin.AuthenticatedUser, loginWin.AuthenticatedGroup);
            ApplyCompanyModesAndUserPermissions();
            BuildDynamicMenu(_companyData.Veterinaria == 1);
            await RefreshDashboardAndRatesAsync();
        }
    }
}
