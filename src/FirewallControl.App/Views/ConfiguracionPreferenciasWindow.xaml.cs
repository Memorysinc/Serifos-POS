using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FirewallControl.App.Services;
using FirewallControl.Core.Business;
using FirewallControl.Core.Entities;
using FirewallControl.Core.Interfaces;
using FirewallControl.Infrastructure.Services;
using Microsoft.Win32;

namespace FirewallControl.App.Views;

/// <summary>
/// Redesigned Ajustes &amp; Configuración hub matching the user's reference photos with the dark POS theme.
/// Houses:
/// - Main Ajustes Hub
/// - Mi Perfil (active user, role permissions, SYS(2007) password update)
/// - Preferencias de Venta y Control de Stock (NoControlarStockGlobal disabled by default, Movimiento Rápido, etc.)
/// - Asignar Teclas y Atajos de Ventanas
/// - Impresión de Tickets y Facturas (1 single compact copy, RAW ESC/POS, short feed)
/// - Multiconexión entre PCs y Red LAN (Server/Client IP, Firewall.sql + FireWall.Ini, real-time sync)
/// - Usuarios, Contraseñas y Rangos (Multiusuario FoxPro usuarios + grupos)
/// - Datos de Empresa, Timbrado y Utilidades 1..7
/// - Sistema, Licencia y Respaldo (.json export/import)
/// </summary>
public partial class ConfiguracionPreferenciasWindow : Window
{
    private readonly IConfigService _configService;
    private readonly ISystemRepository? _systemRepository;
    private readonly IDbConnectionFactory? _connectionFactory;
    private readonly ITicketPrintService? _ticketPrintService;
    private readonly IWindowManagerService? _windowManager;

    private string _currentPage = "HUB";
    private string _selectedWindowKey = ShortcutCatalog.WinItemsFactura;
    private bool _isCapturingKey;
    private bool _suppressEvents;

    private CompanyDataEntity _companyData = new();
    private UserEntity? _selectedUserForEdit;
    private List<UserGroupEntity> _groups = new();

    public ConfiguracionPreferenciasWindow(
        IConfigService configService,
        ISystemRepository? systemRepository = null,
        IDbConnectionFactory? connectionFactory = null,
        ITicketPrintService? ticketPrintService = null,
        IWindowManagerService? windowManager = null)
    {
        InitializeComponent();
        AppIconHelper.ApplySefirosIcon(this);
        _configService = configService;
        _systemRepository = systemRepository;
        _connectionFactory = connectionFactory;
        _ticketPrintService = ticketPrintService;
        _windowManager = windowManager;

        if (CmbIdioma != null)
            CmbIdioma.ItemsSource = LocalizationService.Languages;
        if (CmbMonedaBase != null)
            CmbMonedaBase.ItemsSource = LatamCurrencyCatalog.All;

        Loaded += async (_, _) =>
        {
            LoadPreferencesIntoControls();
            LoadNetworkControls();
            RefreshShortcutsGrid();
            await LoadDatabaseBackedSettingsAsync();
            UpdateHubSummaries();
        };
        ThemeService.ThemeChanged += OnExternalThemeChanged;
        LocalizationService.LanguageChanged += OnExternalThemeChanged;
        Closed += (_, _) =>
        {
            ThemeService.ThemeChanged -= OnExternalThemeChanged;
            LocalizationService.LanguageChanged -= OnExternalThemeChanged;
        };
    }

    private void OnExternalThemeChanged()
    {
        if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
            return;

        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(OnExternalThemeChanged);
            return;
        }

        LoadPreferencesIntoControls();
        UpdateHubSummaries();
    }

    public void Configure(int initialTab = 0, string initialWindowKey = ShortcutCatalog.WinItemsFactura)
    {
        _selectedWindowKey = string.IsNullOrWhiteSpace(initialWindowKey)
            ? ShortcutCatalog.WinItemsFactura
            : initialWindowKey;

        SelectRadioForWindowKey(_selectedWindowKey);
        LoadPreferencesIntoControls();
        LoadNetworkControls();
        RefreshShortcutsGrid();
        UpdateHubSummaries();

        // Map initialTab: 0 = Hub, 1 = Preferencias Stock/POS, 2 = Asignar Teclas, 3 = Impresion,
        // 4 = Multiconexion, 5 = Usuarios, 6 = Empresa, 7 = Mi Perfil, 8 = Sistema y Respaldo,
        // 9 = Apariencia y Diseno POS
        switch (initialTab)
        {
            case 1:
                NavigateToPage("PREFERENCIAS", "Control de stock, caja y límites");
                break;
            case 2:
                NavigateToPage("ATAJOS", "Asignar teclas y atajos");
                break;
            case 3:
                NavigateToPage("IMPRESION", "Impresión de tickets y facturas");
                break;
            case 4:
                NavigateToPage("MULTICONEXION", "Multiconexión y servidor MySQL");
                break;
            case 5:
                NavigateToPage("USUARIOS", "Usuarios, contraseñas y rangos");
                break;
            case 6:
                NavigateToPage("EMPRESA", "Datos de empresa y utilidades");
                break;
            case 7:
                NavigateToPage("PERFIL", "Mi perfil");
                break;
            case 8:
                NavigateToPage("SISTEMA", "Sistema, licencia y respaldo");
                break;
            case 9:
                NavigateToPage("APARIENCIA", "Apariencia y diseño del POS");
                break;
            default:
                NavigateToPage("HUB", "Ajustes");
                break;
        }
    }

    // =========================================================================
    // NAVIGATION ENGINE (HUB <-> SUB-PAGES WITH TOP-LEFT "<" BACK BUTTON)
    // =========================================================================
    private void NavigateToPage(string pageKey, string title)
    {
        _isCapturingKey = false;
        _currentPage = pageKey;

        ViewHub.Visibility = pageKey == "HUB" ? Visibility.Visible : Visibility.Collapsed;
        ViewApariencia.Visibility = pageKey == "APARIENCIA" ? Visibility.Visible : Visibility.Collapsed;
        ViewMiPerfil.Visibility = pageKey == "PERFIL" ? Visibility.Visible : Visibility.Collapsed;
        ViewPreferencias.Visibility = pageKey == "PREFERENCIAS" ? Visibility.Visible : Visibility.Collapsed;
        ViewAtajos.Visibility = pageKey == "ATAJOS" ? Visibility.Visible : Visibility.Collapsed;
        ViewImpresion.Visibility = pageKey == "IMPRESION" ? Visibility.Visible : Visibility.Collapsed;
        ViewMulticonexion.Visibility = pageKey == "MULTICONEXION" ? Visibility.Visible : Visibility.Collapsed;
        ViewUsuarios.Visibility = pageKey == "USUARIOS" ? Visibility.Visible : Visibility.Collapsed;
        ViewEmpresa.Visibility = pageKey == "EMPRESA" ? Visibility.Visible : Visibility.Collapsed;
        ViewSistemaRespaldo.Visibility = pageKey == "SISTEMA" ? Visibility.Visible : Visibility.Collapsed;

        TxtHeaderTitle.Text = title;
        TxtHeaderBackLabel.Text = pageKey == "HUB" ? "Cerrar" : "Ajustes";
        UpdateHubSummaries();
    }

    private void BtnHeaderBack_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPage != "HUB")
        {
            NavigateToPage("HUB", "Ajustes");
        }
        else
        {
            Close();
        }
    }

    private void NavMiPerfil_Click(object sender, RoutedEventArgs e) => NavigateToPage("PERFIL", "Mi perfil");
    private void NavApariencia_Click(object sender, RoutedEventArgs e) => NavigateToPage("APARIENCIA", "Apariencia y diseño del POS");
    private void NavPreferencias_Click(object sender, RoutedEventArgs e) => NavigateToPage("PREFERENCIAS", "Control de stock, caja y límites");
    private void NavAtajos_Click(object sender, RoutedEventArgs e) => NavigateToPage("ATAJOS", "Asignar teclas y atajos");
    private void NavImpresion_Click(object sender, RoutedEventArgs e) => NavigateToPage("IMPRESION", "Impresión de tickets y facturas");
    private void NavMulticonexion_Click(object sender, RoutedEventArgs e) => NavigateToPage("MULTICONEXION", "Multiconexión y servidor MySQL");
    private void NavUsuarios_Click(object sender, RoutedEventArgs e) => NavigateToPage("USUARIOS", "Usuarios, contraseñas y rangos");
    private void NavEmpresa_Click(object sender, RoutedEventArgs e) => NavigateToPage("EMPRESA", "Datos de empresa y utilidades");
    private void NavSistemaRespaldo_Click(object sender, RoutedEventArgs e) => NavigateToPage("SISTEMA", "Sistema, licencia y respaldo");

    // =========================================================================
    // LOAD & SYNC PREFERENCES
    // =========================================================================
    private void LoadPreferencesIntoControls()
    {
        _suppressEvents = true;
        var p = _configService.Preferences;

        bool isClassicPos = string.Equals(p.EstiloPos, "Clasico", StringComparison.OrdinalIgnoreCase);
        if (RbEstiloClasico != null) RbEstiloClasico.IsChecked = isClassicPos;
        if (RbEstiloRenovado != null) RbEstiloRenovado.IsChecked = !isClassicPos;

        bool isClassicFoxPalette = string.Equals(p.PaletaColores, "ClasicaFox", StringComparison.OrdinalIgnoreCase);
        if (RbPaletaClasicaFox != null) RbPaletaClasicaFox.IsChecked = isClassicFoxPalette;
        if (RbPaletaModerna != null) RbPaletaModerna.IsChecked = !isClassicFoxPalette;

        ChkPrefNoControlarStockGlobal.IsChecked = p.NoControlarStockGlobal;
        ChkPrefDescontarStock.IsChecked = p.DescontarStockAunqueNoControle;
        ChkPrefCongelarStockSinControl.IsChecked = p.CongelarStockSinControl;
        ChkPrefItemFacturaModal.IsChecked = p.ItemFacturaModal;
        ChkPrefMovRapido.IsChecked = p.IniciarMovimientoRapidoPorDefecto;
        ChkPrefReiniciarAlEquivocarse.IsChecked = p.ReiniciarItemAlEquivocarseEnItemFactura;
        ChkPrefPermitirBajoCosto.IsChecked = p.PermitirVentaBajoCosto;
        ChkPrefValidarLimitesPrecio.IsChecked = p.ValidarLimitesPrecioEnItem;
        ChkPrefSonidoAlerta.IsChecked = p.SonidoAlertaEnErrores;
        ChkPrefSyncGenes.IsChecked = p.SincronizarGenesAlAbrir;
        TxtPrefLimiteCantidad.Text = Math.Max(0m, p.LimiteCantidadPorItem).ToString("0.##", CultureInfo.InvariantCulture);
        TxtPrefPrecioMinimo.Text = Math.Max(0m, p.PrecioMinimoVenta).ToString("0.##", CultureInfo.InvariantCulture);
        TxtPrefPrecioMaximo.Text = Math.Max(0m, p.PrecioMaximoVenta).ToString("0.##", CultureInfo.InvariantCulture);

        TxtPrintCopias.Text = (p.CopiasTicket > 0 ? p.CopiasTicket : 1).ToString();
        TxtPrintLineasAvance.Text = Math.Clamp(p.LineasAvanceCorte, 0, 8).ToString();
        ChkPrintRawDirecto.IsChecked = p.UsarImpresionRawDirecta;
        ChkPrintAutoCut.IsChecked = p.CortarPapelAutomatico;
        ChkPrintOpenDrawer.IsChecked = p.AbrirCajonConTicket;

        ChkNetSyncRealTime.IsChecked = p.MulticonexionTiempoRealActiva;
        ChkSolicitarLoginInicio.IsChecked = p.SolicitarLoginAlIniciar;
        ChkTemaOscuro.IsChecked = p.TemaOscuroConfiguracion;
        if (ChkTemaOscuroApariencia != null)
            ChkTemaOscuroApariencia.IsChecked = p.TemaOscuroConfiguracion;

        var printers = TicketPrintService.GetInstalledPrinters();
        CmbPrinterTicket.ItemsSource = printers;
        CmbPrinterLegal.ItemsSource = printers;

        if (CmbIdioma != null)
            CmbIdioma.SelectedValue = LocalizationService.NormalizeCode(p.Idioma);
        if (CmbMonedaBase != null)
            CmbMonedaBase.SelectedValue = LatamCurrencyCatalog.GetByCode(p.MonedaBaseIso).IsoCode;

        _suppressEvents = false;
        UpdateThemeSummary();
    }

    private void UpdateHubSummaries()
    {
        var p = _configService.Preferences;
        var user = _windowManager?.CurrentUser ?? new UserEntity { Cidusuario = "ADMINISTRA", Cdescribe = "ADMINISTRADOR DEL SISTEMA", Cidgrupo = "ADMIN" };
        var grp = _windowManager?.CurrentUserGroup ?? new UserGroupEntity { Cidgrupo = user.Cidgrupo };

        TxtHubAccountSubtitle.Text = $"{user.Cidusuario} · {user.Cdescribe} (Rango: {user.Cidgrupo})";
        TxtHubAccountBadge.Text = $"● Cuenta activa · {user.Cidgrupo}";

        TxtPerfilHeaderName.Text = user.Cdescribe;
        TxtPerfilHeaderRoleBadge.Text = user.Cidgrupo;
        TxtPerfilUsuario.Text = user.Cidusuario;
        TxtPerfilNombre.Text = user.Cdescribe;
        TxtPerfilTerminalInfo.Text = $"Sucursal #{_configService.Config.IdSucursal} · Caja #{_configService.Config.IdCaja} · Depósito #{_configService.Config.IdDeposito}";

        var perms = new List<string>();
        if (grp.BtnVenta == 1) perms.Add("Ventas POS");
        if (grp.BtnCompra == 1) perms.Add("Compras");
        if (grp.BtnProducto == 1) perms.Add("Productos");
        if (grp.BtnCliente == 1) perms.Add("Clientes");
        if (grp.BtnProveedor == 1) perms.Add("Proveedores");
        if (grp.BtnMovCaja == 1) perms.Add("Caja");
        TxtPerfilRolPermisos.Text = $"{user.Cidgrupo} — Módulos habilitados: {(perms.Count > 0 ? string.Join(", ", perms) : "Acceso estándar")}";

        var pal = ThemeService.GetPalette(p.TemaAtelier);
        bool isSefiros = string.Equals(pal.Key, "sefiros", StringComparison.OrdinalIgnoreCase);
        string posLabel = string.Equals(p.EstiloPos, "Clasico", StringComparison.OrdinalIgnoreCase) ? "Clásico (FOX)" : "Renovado (C#)";
        string palLabel = string.Equals(p.PaletaColores, "ClasicaFox", StringComparison.OrdinalIgnoreCase) ? "Clásica FOX" : "Moderna";
        string stockStatus = p.NoControlarStockGlobal ? "Modo Libre ACTIVADO" : "Control estricto por defecto";
        string modalLabel = p.ItemFacturaModal ? "Modal 1 activo" : "Libre";

        if (TxtHubEmpresaSummary != null)
        {
            string empName = !string.IsNullOrWhiteSpace(_companyData.Descripcion) ? _companyData.Descripcion.Trim() : "Empresa";
            TxtHubEmpresaSummary.Text = $"{empName} · Timbrado {_configService.Config.Timbrado} · Lista #{_configService.Config.ListaPrecios} · 7 márgenes de utilidad";
        }
        if (TxtHubAparienciaSummary != null)
        {
            string baseIso = LatamCurrencyCatalog.GetByCode(p.MonedaBaseIso).IsoCode;
            string langCode = LocalizationService.NormalizeCode(p.Idioma).ToUpperInvariant();
            TxtHubAparienciaSummary.Text = isSefiros
                ? $"Tema: {pal.Label} · POS: {posLabel} · {langCode} / {baseIso}"
                : $"Tema: {pal.Label} · POS: {posLabel} · {palLabel} · {langCode} / {baseIso}";
        }
        TxtHubPrefSummary.Text = $"Stock: {stockStatus} · Selector: {modalLabel} · Cant. máx.: {p.LimiteCantidadPorItem:0.##}";
        TxtHubPrintSummary.Text = $"Copias por ticket: {p.CopiasTicket} nota · Corte automático: {(p.CortarPapelAutomatico ? "Sí" : "No")} · Modo RAW: {(p.UsarImpresionRawDirecta ? "Activo" : "GDI")}";
        TxtHubNetworkSummary.Text = $"Servidor MySQL: {_configService.Config.SqlServerHost}:{_configService.Config.Port} ({_configService.Config.BaseDatos}) · Sync tiempo real: {(p.MulticonexionTiempoRealActiva ? "Activo" : "Inactivo")}";
        UpdateThemeSummary();
    }

    private void UpdateThemeSummary()
    {
        var p = _configService.Preferences;
        var pal = ThemeService.GetPalette(p.TemaAtelier);
        var swatchBrush = ThemeService.CreatePaletteSwatchBrush(pal);
        bool isSefiros = string.Equals(pal.Key, "sefiros", StringComparison.OrdinalIgnoreCase);

        if (TxtCurrentThemeName != null)
            TxtCurrentThemeName.Text = pal.Label;
        if (SwatchCurrentTheme != null)
            SwatchCurrentTheme.Background = swatchBrush;
        if (TxtTemaOscuroSub != null)
            TxtTemaOscuroSub.Text = p.TemaOscuroConfiguracion ? "Activado" : "Desactivado";

        if (TxtAparienciaThemeName != null)
            TxtAparienciaThemeName.Text = pal.Label;
        if (TxtAparienciaThemeDesc != null)
            TxtAparienciaThemeDesc.Text = isSefiros
                ? "Paleta oficial del Tema Sefiros aplicada a todo el sistema (incluye versión Clásica FOX de ABMVENTAS e ITEMSFACTURAVENTAS)."
                : "Paleta Atelier C#. En el diseño Clásico/Renovado aplica la paleta POS Moderna o Clásica FOX elegida abajo.";
        if (SwatchAparienciaTheme != null)
            SwatchAparienciaTheme.Background = swatchBrush;
        if (TxtTemaOscuroAparienciaSub != null)
            TxtTemaOscuroAparienciaSub.Text = p.TemaOscuroConfiguracion ? "Activado (fondo oscuro de alto contraste)" : "Desactivado (modo claro)";
        if (BtnActivateSefirosTheme != null)
            BtnActivateSefirosTheme.Content = isSefiros ? "✓ Tema Sefiros Activo" : "✨ Activar Tema Sefiros";
        if (BannerSefirosOverrideInfo != null)
            BannerSefirosOverrideInfo.Visibility = isSefiros ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BtnActivateSefirosTheme_Click(object sender, RoutedEventArgs e)
    {
        _configService.Preferences.TemaAtelier = "sefiros";
        _configService.SavePreferences();
        ThemeService.ApplyTheme(
            "sefiros",
            _configService.Preferences.TemaOscuroConfiguracion,
            _configService.Preferences.PaletaColores);
        UpdateHubSummaries();
        LblFooterStatus.Text = "✓ Tema Sefiros aplicado a todo el sistema, incluyendo la versión FOX.";
    }

    private void BtnOpenColorAtelier_Click(object sender, RoutedEventArgs e)
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
            UpdateHubSummaries();
            LblFooterStatus.Text = string.Equals(dlg.SelectedThemeKey, "sefiros", StringComparison.OrdinalIgnoreCase)
                ? "✓ Tema Sefiros aplicado a todo el sistema, incluyendo la versión FOX."
                : $"✓ Paleta '{ThemeService.GetPalette(dlg.SelectedThemeKey).Label}' aplicada a todo el sistema.";
        }
    }

    private void OnThemeDarkModeChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressEvents)
            return;

        bool isDark = (sender as CheckBox)?.IsChecked == true;
        _suppressEvents = true;
        if (ChkTemaOscuro != null) ChkTemaOscuro.IsChecked = isDark;
        if (ChkTemaOscuroApariencia != null) ChkTemaOscuroApariencia.IsChecked = isDark;
        _suppressEvents = false;

        _configService.Preferences.TemaOscuroConfiguracion = isDark;
        _configService.SavePreferences();
        ThemeService.ApplyTheme(
            _configService.Preferences.TemaAtelier,
            isDark,
            _configService.Preferences.PaletaColores);
        UpdateHubSummaries();
        LblFooterStatus.Text = isDark
            ? "✓ Tema oscuro activado en todo el sistema."
            : "✓ Tema claro activado en todo el sistema.";
    }

    private void OnColorPaletteChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressEvents || !IsLoaded)
            return;

        string targetPalette = RbPaletaClasicaFox?.IsChecked == true ? "ClasicaFox" : "Moderna";
        _configService.Preferences.PaletaColores = targetPalette;
        _configService.SavePreferences();

        ThemeService.ApplyTheme(
            _configService.Preferences.TemaAtelier,
            _configService.Preferences.TemaOscuroConfiguracion,
            targetPalette);
        UpdateHubSummaries();

        LblFooterStatus.Text = targetPalette == "ClasicaFox"
            ? "✓ Paleta 'Clásica FOX (Visual FoxPro / Sufer POS)' aplicada al sistema manteniendo la distribución y funciones."
            : "✓ Paleta 'Moderna (C# Atelier)' aplicada al sistema manteniendo la distribución y funciones.";
    }

    private void OnPosStyleChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressEvents || !IsLoaded)
            return;

        string targetStyle = RbEstiloClasico?.IsChecked == true ? "Clasico" : "Renovado";
        _configService.Preferences.EstiloPos = targetStyle;
        _configService.SavePreferences();
        _windowManager?.RefreshItemSelectorModalState();
        UpdateHubSummaries();

        if (_windowManager != null)
        {
            bool appliedNow = _windowManager.ApplyPosStyle(targetStyle);
            LblFooterStatus.Text = appliedNow
                ? $"✓ Diseño POS '{(targetStyle == "Clasico" ? "Clásico (Sufer POS / FOX)" : "Renovado (C# Atelier)")}' aplicado inmediatamente conservando tus ítems."
                : "✓ Preferencia guardada. Se aplicará al terminar el cobro actual en curso.";
        }
        else
        {
            LblFooterStatus.Text = $"✓ Diseño POS '{targetStyle}' guardado en preferencias.";
        }
    }

    private void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressEvents || !IsLoaded || CmbIdioma?.SelectedValue is not string langCode)
            return;

        string normalized = LocalizationService.NormalizeCode(langCode);
        _configService.Preferences.Idioma = normalized;
        _configService.SavePreferences();
        LocalizationService.ApplyLanguage(normalized);
        UpdateHubSummaries();
        LblFooterStatus.Text = $"✓ Idioma cambiado a '{normalized.ToUpperInvariant()}'.";
    }

    private void OnBaseCurrencyChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressEvents || !IsLoaded || CmbMonedaBase?.SelectedValue is not string isoCode)
            return;

        var cur = LatamCurrencyCatalog.GetByCode(isoCode);
        _configService.Preferences.MonedaBaseIso = cur.IsoCode;
        _configService.Config.MonedaFactura = cur.Name.ToUpperInvariant();
        _configService.SavePreferences();
        _configService.SaveLocalSettings();
        LocalizationService.ApplyLanguage(_configService.Preferences.Idioma);
        UpdateHubSummaries();
        LblFooterStatus.Text = $"✓ Moneda base del POS configurada: {cur.IsoCode} ({cur.Name} · {cur.Symbol}).";
    }

    private void BtnFactoryReset_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(
                "¿Restablecer todas las preferencias visuales, idioma (ES), moneda base (PYG) y atajos del POS a sus valores de fábrica?\n\nNota: Tus ventas, productos y clientes en MySQL no se eliminarán.",
                "Restablecer POS de Fábrica",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        _configService.ResetToFactoryDefaults();
        ThemeService.ApplyTheme(
            _configService.Preferences.TemaAtelier,
            _configService.Preferences.TemaOscuroConfiguracion,
            _configService.Preferences.PaletaColores);
        LocalizationService.ApplyLanguage(_configService.Preferences.Idioma);
        _windowManager?.ApplyPosStyle(_configService.Preferences.EstiloPos);
        LoadPreferencesIntoControls();
        RefreshShortcutsGrid();
        UpdateHubSummaries();
        LblFooterStatus.Text = "✓ Sefiros POS restablecido a su configuración limpia de fábrica.";
    }

    private void OnPreferenceToggleChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressEvents)
            return;

        SavePreferencesFromControls();
    }

    private void TxtPrefLimiteCantidad_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_suppressEvents)
            return;
        SavePreferencesFromControls();
    }

    private void SavePreferencesFromControls()
    {
        var p = _configService.Preferences;
        p.NoControlarStockGlobal = ChkPrefNoControlarStockGlobal.IsChecked == true;
        p.DescontarStockAunqueNoControle = ChkPrefDescontarStock.IsChecked == true;
        p.CongelarStockSinControl = ChkPrefCongelarStockSinControl.IsChecked == true;
        p.ItemFacturaModal = ChkPrefItemFacturaModal.IsChecked == true;
        p.IniciarMovimientoRapidoPorDefecto = ChkPrefMovRapido.IsChecked == true;
        p.ReiniciarItemAlEquivocarseEnItemFactura = ChkPrefReiniciarAlEquivocarse.IsChecked == true;
        p.PermitirVentaBajoCosto = ChkPrefPermitirBajoCosto.IsChecked == true;
        p.ValidarLimitesPrecioEnItem = ChkPrefValidarLimitesPrecio.IsChecked == true;
        p.SonidoAlertaEnErrores = ChkPrefSonidoAlerta.IsChecked == true;
        p.SincronizarGenesAlAbrir = ChkPrefSyncGenes.IsChecked == true;
        p.SolicitarLoginAlIniciar = ChkSolicitarLoginInicio.IsChecked == true;
        p.MulticonexionTiempoRealActiva = ChkNetSyncRealTime.IsChecked == true;

        if (decimal.TryParse(TxtPrefLimiteCantidad.Text.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal lim) && lim >= 0m)
            p.LimiteCantidadPorItem = lim;

        if (decimal.TryParse(TxtPrefPrecioMinimo.Text.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal pMin) && pMin >= 0m)
            p.PrecioMinimoVenta = pMin;

        if (decimal.TryParse(TxtPrefPrecioMaximo.Text.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal pMax) && pMax >= 0m)
            p.PrecioMaximoVenta = pMax;

        _configService.SavePreferences();
        _windowManager?.RefreshItemSelectorModalState();
        UpdateHubSummaries();

        LblFooterStatus.Text = p.NoControlarStockGlobal
            ? "✓ Guardado: Opción 'No controlar stock en todos los ítems' ACTIVADA."
            : "✓ Preferencias guardadas automáticamente.";
    }

    // =========================================================================
    // DATABASE-BACKED SETTINGS (COMPANY, PRINTERS, USERS, GROUPS)
    // =========================================================================
    private async Task LoadDatabaseBackedSettingsAsync()
    {
        if (_systemRepository == null)
            return;

        try
        {
            _companyData = await _systemRepository.GetCompanyDataAsync();
            TxtCfgEmpNombre.Text = _companyData.Descripcion;
            TxtCfgEmpRuc.Text = _companyData.Ruc;
            TxtCfgEmpDir.Text = _companyData.Direccion;
            TxtCfgEmpTel.Text = _companyData.Telefonos;
            TxtCfgTimbrado.Text = _configService.Config.Timbrado;
            TxtCfgListaPrecios.Text = _configService.Config.ListaPrecios.ToString();
            ChkCfgCobroCaja.IsChecked = _companyData.CobroCaja == 1;
            ChkCfgComercial.IsChecked = _companyData.Comercial == 1;
            ChkCfgVeterinaria.IsChecked = _companyData.Veterinaria == 1;
            ChkCfgRestaurant.IsChecked = _companyData.Restaurant == 1;
            TxtCfgInteresCuota.Text = _companyData.InteresCuota.ToString("0.##", CultureInfo.InvariantCulture);
            TxtCfgInteresPunitorio.Text = _companyData.InteresPunitorio.ToString("0.##", CultureInfo.InvariantCulture);
            TxtCfgReclamos.Text = _companyData.Reclamos.ToString("0.##", CultureInfo.InvariantCulture);
            TxtCfgHonorarioJud.Text = _companyData.HonorarioJudicial.ToString("0.##", CultureInfo.InvariantCulture);

            TxtCfgU1.Text = _companyData.Utilidad1.ToString("0.##", CultureInfo.InvariantCulture);
            TxtCfgU2.Text = _companyData.Utilidad2.ToString("0.##", CultureInfo.InvariantCulture);
            TxtCfgU3.Text = _companyData.Utilidad3.ToString("0.##", CultureInfo.InvariantCulture);
            TxtCfgU4.Text = _companyData.Utilidad4.ToString("0.##", CultureInfo.InvariantCulture);
            TxtCfgU5.Text = _companyData.Utilidad5.ToString("0.##", CultureInfo.InvariantCulture);
            TxtCfgU6.Text = _companyData.Utilidad6.ToString("0.##", CultureInfo.InvariantCulture);
            TxtCfgU7.Text = _companyData.Utilidad7.ToString("0.##", CultureInfo.InvariantCulture);

            CmbPrinterTicket.Text = !string.IsNullOrWhiteSpace(_companyData.ImpreBalanza)
                ? _companyData.ImpreBalanza
                : _companyData.ImpreNormal;
            CmbPrinterLegal.Text = _companyData.ImpreLegal;

            UpdateHubSummaries();
            await ReloadUsersAndGroupsAsync();
        }
        catch
        {
            // Ignore if DB offline
        }
    }

    private async Task ReloadUsersAndGroupsAsync()
    {
        if (_systemRepository == null)
            return;

        var users = await _systemRepository.GetActiveUsersAsync();
        GridConfigUsers.ItemsSource = users;
        _groups = await _systemRepository.GetAllGroupsAsync();
    }

    // =========================================================================
    // SUB-PAGE 1: MI PERFIL & CAMBIO DE USUARIO
    // =========================================================================
    private void BtnSwitchUserQuick_Click(object sender, RoutedEventArgs e)
    {
        if (_systemRepository == null || _windowManager == null)
            return;

        var loginWin = new LoginWindow(_systemRepository) { Owner = this };
        if (loginWin.ShowDialog() == true && loginWin.AuthenticatedUser != null)
        {
            _windowManager.CurrentUser = loginWin.AuthenticatedUser;
            _windowManager.CurrentUserGroup = loginWin.AuthenticatedGroup ?? new UserGroupEntity { Cidgrupo = loginWin.AuthenticatedUser.Cidgrupo };
            UpdateHubSummaries();
            LblFooterStatus.Text = $"✓ Sesión cambiada a: {loginWin.AuthenticatedUser.Cidusuario} (Rango: {loginWin.AuthenticatedUser.Cidgrupo})";
        }
    }

    private async void BtnUpdateMyPassword_Click(object sender, RoutedEventArgs e)
    {
        if (_systemRepository == null || _windowManager == null)
            return;

        string p1 = PwdPerfilNueva.Password.Trim();
        string p2 = PwdPerfilConfirmar.Password.Trim();
        if (string.IsNullOrWhiteSpace(p1))
        {
            MessageBox.Show("Ingrese la nueva contraseña.", "Seguridad", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (p1 != p2)
        {
            MessageBox.Show("Las contraseñas no coinciden.", "Seguridad", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var u = _windowManager.CurrentUser;
            if (u.Nlineno > 0)
            {
                await _systemRepository.SaveUserPasswordAsync(u.Nlineno, p1);
            }
            PwdPerfilNueva.Clear();
            PwdPerfilConfirmar.Clear();
            LblFooterStatus.Text = $"✓ Contraseña actualizada con encriptación SYS(2007) ({VfpCompat.Sys2007(p1)}).";
            MessageBox.Show("Tu contraseña ha sido actualizada exitosamente.", "Seguridad", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al actualizar contraseña:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // =========================================================================
    // SUB-PAGE 4: IMPRESIÓN DE TICKETS Y FACTURAS
    // =========================================================================
    private async void BtnSavePrintSettings_Click(object sender, RoutedEventArgs e)
    {
        var p = _configService.Preferences;
        p.CopiasTicket = int.TryParse(TxtPrintCopias.Text.Trim(), out int c) ? Math.Clamp(c, 1, 5) : 1;
        p.LineasAvanceCorte = int.TryParse(TxtPrintLineasAvance.Text.Trim(), out int l) ? Math.Clamp(l, 0, 8) : 2;
        p.UsarImpresionRawDirecta = ChkPrintRawDirecto.IsChecked == true;
        p.CortarPapelAutomatico = ChkPrintAutoCut.IsChecked == true;
        p.AbrirCajonConTicket = ChkPrintOpenDrawer.IsChecked == true;
        _configService.SavePreferences();

        if (_systemRepository != null)
        {
            try
            {
                _companyData.ImpreBalanza = CmbPrinterTicket.Text.Trim();
                _companyData.ImpreNormal = CmbPrinterTicket.Text.Trim();
                _companyData.ImpreLegal = CmbPrinterLegal.Text.Trim();
                _companyData.CantVias = p.CopiasTicket;
                await _systemRepository.SaveCompanyDataAsync(_companyData);
            }
            catch
            {
                // Ignore DB error
            }
        }

        UpdateHubSummaries();
        LblFooterStatus.Text = $"✓ Impresión guardada: {p.CopiasTicket} copia(s), avance {p.LineasAvanceCorte} líneas, modo RAW directo.";
    }

    private async void BtnTestShortTicket_Click(object sender, RoutedEventArgs e)
    {
        BtnSavePrintSettings_Click(sender, e);
        if (_ticketPrintService == null)
            return;

        var sampleSale = new SaleEntity
        {
            NroVenta = 9999,
            FechaVenta = DateTime.Today,
            HoraVenta = DateTime.Now.ToString("HH:mm:ss"),
            IdCliente = 132,
            NombreCliente = "CLIENTE OCASIONAL",
            CidUsuario = _windowManager?.CurrentUser.Cidusuario ?? "ADMINISTRA",
            TotalVenta = 15000m,
            EntregaGuarani = 20000m,
            VueltoGuarani = 5000m,
            Items = new List<SaleItemEntity>
            {
                new() { Codigo = "TEST", Descripcion = "PRUEBA DE TICKET COMPACTO", Cantidad = 1m, PreUnitario = 15000m, PrecioTotal = 15000m }
            }
        };

        await _ticketPrintService.PrintSaleAsync(sampleSale, _companyData);
        LblFooterStatus.Text = "✓ Se envió 1 nota compacta de prueba a la impresora.";
    }

    // =========================================================================
    // SUB-PAGE 5: MULTICONEXIÓN ENTRE PCS Y SERVIDOR MYSQL
    // =========================================================================
    private void LoadNetworkControls()
    {
        TxtNetHost.Text = _configService.Config.SqlServerHost;
        TxtNetPort.Text = _configService.Config.Port.ToString();
        TxtNetDatabase.Text = _configService.Config.BaseDatos;
        TxtNetUser.Text = _configService.Config.SqlUser;
        PwdNetPassword.Password = _configService.Config.SqlPassword;
        TxtNetIdCajaLocal.Text = _configService.Config.IdCaja.ToString();

        try
        {
            var ips = Dns.GetHostEntry(Dns.GetHostName())
                .AddressList
                .Where(ip => ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                .Select(ip => ip.ToString())
                .ToList();
            TxtNetLocalIps.Text = ips.Count > 0
                ? $"📡 IP de esta PC en la red LAN: {string.Join(" / ", ips)} (Nombre: {Environment.MachineName})"
                : $"📡 Nombre de equipo en red: {Environment.MachineName}";
        }
        catch
        {
            TxtNetLocalIps.Text = $"📡 Equipo: {Environment.MachineName}";
        }
    }

    private void BtnSetLocalhostServer_Click(object sender, RoutedEventArgs e)
    {
        TxtNetHost.Text = "localhost";
        TxtNetPort.Text = "3306";
    }

    private async void BtnTestNetworkConnection_Click(object sender, RoutedEventArgs e)
    {
        if (_connectionFactory == null)
            return;

        string host = TxtNetHost.Text.Trim();
        int port = int.TryParse(TxtNetPort.Text.Trim(), out int p) ? p : 3306;
        string db = TxtNetDatabase.Text.Trim();
        string user = TxtNetUser.Text.Trim();
        string pwd = PwdNetPassword.Password;

        string testConn = $"Server={host};Port={port};Database={db};User ID={user};Password={pwd};AllowZeroDateTime=True;ConvertZeroDateTime=True;ConnectionTimeout=4;CharacterSet=latin1;";
        bool ok = await _connectionFactory.TestConnectionAsync(testConn);
        if (ok)
        {
            LblFooterStatus.Text = $"✓ Conexión exitosa con el servidor MySQL en {host}:{port} ({db}).";
            MessageBox.Show($"Conexión exitosa con el servidor MySQL en '{host}:{port}' ({db}).", "Multiconexión en Red", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            LblFooterStatus.Text = $"⚠ No se pudo conectar con {host}:{port}. Verifique IP, puerto 3306 y Firewall de Windows.";
            MessageBox.Show($"No se pudo conectar al servidor '{host}:{port}'.\n\nVerifique:\n1. Que el servicio MySQL esté iniciado en la PC Servidor.\n2. Que el puerto TCP 3306 esté permitido en el Firewall de Windows.", "Error de Conexión", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void BtnSaveNetworkConnection_Click(object sender, RoutedEventArgs e)
    {
        string host = string.IsNullOrWhiteSpace(TxtNetHost.Text) ? "localhost" : TxtNetHost.Text.Trim();
        int port = int.TryParse(TxtNetPort.Text.Trim(), out int p) ? p : 3306;
        string db = string.IsNullOrWhiteSpace(TxtNetDatabase.Text) ? "firewallcontrol" : TxtNetDatabase.Text.Trim();
        string user = string.IsNullOrWhiteSpace(TxtNetUser.Text) ? "root" : TxtNetUser.Text.Trim();
        string pwd = PwdNetPassword.Password;

        if (int.TryParse(TxtNetIdCajaLocal.Text.Trim(), out int idCaja) && idCaja is >= 1 and <= 5)
            _configService.Config.IdCaja = idCaja;

        _configService.Preferences.MulticonexionTiempoRealActiva = ChkNetSyncRealTime.IsChecked == true;
        _configService.SavePreferences();
        _configService.SaveServerCredentials(host, user, pwd, port, db);

        UpdateHubSummaries();
        LblFooterStatus.Text = $"✓ Configuración de Multiconexión guardada en Firewall.sql y FireWall.Ini ({host}:{port}, Caja #{_configService.Config.IdCaja}).";
        MessageBox.Show(
            $"Configuración de red guardada exitosamente:\n\n" +
            $"• Servidor MySQL: {host}:{port}\n" +
            $"• Base de Datos: {db}\n" +
            $"• Caja Local: #{_configService.Config.IdCaja}\n" +
            $"• Sincronización en tiempo real: {(ChkNetSyncRealTime.IsChecked == true ? "ACTIVA" : "Inactiva")}",
            "Multiconexión Guardada",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    // =========================================================================
    // SUB-PAGE 6: USUARIOS, CONTRASEÑAS Y RANGOS (MULTIUSUARIO)
    // =========================================================================
    private void GridConfigUsers_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GridConfigUsers.SelectedItem is not UserEntity u)
            return;

        _selectedUserForEdit = u;
        TxtUserFormTitle.Text = $"EDITANDO USUARIO #{u.Nlineno} ({u.Cidusuario})";
        TxtCfgUsrLogin.Text = u.Cidusuario;
        TxtCfgUsrNombre.Text = u.Cdescribe;
        TxtCfgUsrCaja.Text = (u.IdCaja > 0 ? u.IdCaja : 1).ToString();
        TxtCfgUsrDeposito.Text = (u.IdDeposito > 0 ? u.IdDeposito : 1).ToString();
        PwdCfgUsrPass.Clear();

        string gCode = (u.Cidgrupo ?? "ADMIN").Trim().ToUpperInvariant();
        foreach (ComboBoxItem item in CmbCfgUsrGrupo.Items)
        {
            if (string.Equals(item.Tag?.ToString(), gCode, StringComparison.OrdinalIgnoreCase))
            {
                CmbCfgUsrGrupo.SelectedItem = item;
                break;
            }
        }
        ApplyGroupCheckboxes(gCode);
    }

    private void CmbCfgUsrGrupo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        if (CmbCfgUsrGrupo.SelectedItem is ComboBoxItem item && item.Tag is string gCode)
        {
            ApplyGroupCheckboxes(gCode);
        }
    }

    private void ApplyGroupCheckboxes(string groupCode)
    {
        var existingGrp = _groups.FirstOrDefault(g => string.Equals(g.Cidgrupo?.Trim(), groupCode, StringComparison.OrdinalIgnoreCase));
        if (existingGrp != null && !string.Equals(groupCode, "ADMIN", StringComparison.OrdinalIgnoreCase))
        {
            ChkPermVenta.IsChecked = existingGrp.BtnVenta == 1;
            ChkPermCompra.IsChecked = existingGrp.BtnCompra == 1;
            ChkPermProducto.IsChecked = existingGrp.BtnProducto == 1;
            ChkPermCliente.IsChecked = existingGrp.BtnCliente == 1;
            ChkPermProveedor.IsChecked = existingGrp.BtnProveedor == 1;
            ChkPermCaja.IsChecked = existingGrp.BtnMovCaja == 1;
            ChkPermPedido.IsChecked = existingGrp.BtnPedido == 1;
            ChkPermPresu.IsChecked = existingGrp.BtnPresu == 1;
            ChkPermVenci.IsChecked = existingGrp.BtnVenci == 1;
            ChkPermVendedor.IsChecked = existingGrp.BtnVendedor == 1;
            ChkPermPago.IsChecked = existingGrp.BtnPago == 1;
            ChkPermCobro.IsChecked = existingGrp.BtnCobro == 1;
            TxtCfgCaccesos.Text = string.IsNullOrWhiteSpace(existingGrp.Caccesos)
                ? VfpPermissionHelper.BuildDefaultAccessMatrix(existingGrp)
                : existingGrp.Caccesos;
            return;
        }

        switch (groupCode.ToUpperInvariant())
        {
            case "CAJERO":
                ChkPermVenta.IsChecked = true;
                ChkPermCompra.IsChecked = false;
                ChkPermProducto.IsChecked = false;
                ChkPermCliente.IsChecked = true;
                ChkPermProveedor.IsChecked = false;
                ChkPermCaja.IsChecked = true;
                ChkPermPedido.IsChecked = false;
                ChkPermPresu.IsChecked = true;
                ChkPermVenci.IsChecked = false;
                ChkPermVendedor.IsChecked = false;
                ChkPermPago.IsChecked = false;
                ChkPermCobro.IsChecked = true;
                break;
            case "VENDEDOR":
                ChkPermVenta.IsChecked = true;
                ChkPermCompra.IsChecked = false;
                ChkPermProducto.IsChecked = false;
                ChkPermCliente.IsChecked = true;
                ChkPermProveedor.IsChecked = false;
                ChkPermCaja.IsChecked = false;
                ChkPermPedido.IsChecked = false;
                ChkPermPresu.IsChecked = true;
                ChkPermVenci.IsChecked = false;
                ChkPermVendedor.IsChecked = false;
                ChkPermPago.IsChecked = false;
                ChkPermCobro.IsChecked = false;
                break;
            case "DEPOSITO":
                ChkPermVenta.IsChecked = false;
                ChkPermCompra.IsChecked = true;
                ChkPermProducto.IsChecked = true;
                ChkPermCliente.IsChecked = false;
                ChkPermProveedor.IsChecked = true;
                ChkPermCaja.IsChecked = false;
                ChkPermPedido.IsChecked = true;
                ChkPermPresu.IsChecked = false;
                ChkPermVenci.IsChecked = true;
                ChkPermVendedor.IsChecked = false;
                ChkPermPago.IsChecked = false;
                ChkPermCobro.IsChecked = false;
                break;
            default: // ADMIN / SUPERVISOR
                ChkPermVenta.IsChecked = true;
                ChkPermCompra.IsChecked = true;
                ChkPermProducto.IsChecked = true;
                ChkPermCliente.IsChecked = true;
                ChkPermProveedor.IsChecked = true;
                ChkPermCaja.IsChecked = true;
                ChkPermPedido.IsChecked = true;
                ChkPermPresu.IsChecked = true;
                ChkPermVenci.IsChecked = true;
                ChkPermVendedor.IsChecked = true;
                ChkPermPago.IsChecked = true;
                ChkPermCobro.IsChecked = true;
                break;
        }
        TxtCfgCaccesos.Text = VfpPermissionHelper.BuildDefaultAccessMatrix(BuildGroupFromCheckboxes(groupCode));
    }

    private UserGroupEntity BuildGroupFromCheckboxes(string groupCode, UserGroupEntity? existing = null)
    {
        var grp = existing ?? new UserGroupEntity { Cidgrupo = groupCode, Cdescribe = groupCode };
        grp.BtnVenta = ChkPermVenta.IsChecked == true ? 1 : 0;
        grp.BtnCompra = ChkPermCompra.IsChecked == true ? 1 : 0;
        grp.BtnProducto = ChkPermProducto.IsChecked == true ? 1 : 0;
        grp.BtnCliente = ChkPermCliente.IsChecked == true ? 1 : 0;
        grp.BtnProveedor = ChkPermProveedor.IsChecked == true ? 1 : 0;
        grp.BtnMovCaja = ChkPermCaja.IsChecked == true ? 1 : 0;
        grp.BtnPedido = ChkPermPedido.IsChecked == true ? 1 : 0;
        grp.BtnPresu = ChkPermPresu.IsChecked == true ? 1 : 0;
        grp.BtnVenci = ChkPermVenci.IsChecked == true ? 1 : 0;
        grp.BtnVendedor = ChkPermVendedor.IsChecked == true ? 1 : 0;
        grp.BtnPago = ChkPermPago.IsChecked == true ? 1 : 0;
        grp.BtnCobro = ChkPermCobro.IsChecked == true ? 1 : 0;
        return grp;
    }

    private void BtnBuildCaccesosFromChecks_Click(object sender, RoutedEventArgs e)
    {
        string groupCode = (CmbCfgUsrGrupo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "CAJERO";
        TxtCfgCaccesos.Text = VfpPermissionHelper.BuildDefaultAccessMatrix(BuildGroupFromCheckboxes(groupCode));
    }

    private void BtnNewUser_Click(object sender, RoutedEventArgs e)
    {
        _selectedUserForEdit = null;
        TxtUserFormTitle.Text = "NUEVO USUARIO DEL SISTEMA";
        TxtCfgUsrLogin.Clear();
        TxtCfgUsrNombre.Clear();
        PwdCfgUsrPass.Clear();
        TxtCfgUsrCaja.Text = "1";
        TxtCfgUsrDeposito.Text = "1";
        CmbCfgUsrGrupo.SelectedIndex = 2; // CAJERO default for new user
        TxtCfgUsrLogin.Focus();
    }

    private async void BtnSaveConfigUser_Click(object sender, RoutedEventArgs e)
    {
        if (_systemRepository == null)
            return;

        string login = TxtCfgUsrLogin.Text.Trim().ToUpperInvariant();
        string nombre = TxtCfgUsrNombre.Text.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(nombre))
        {
            MessageBox.Show("Complete el Usuario (Login) y Nombre Completo.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string groupCode = (CmbCfgUsrGrupo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "ADMIN";
        bool isModify = _selectedUserForEdit != null && _selectedUserForEdit.Nlineno > 0;
        var entity = _selectedUserForEdit ?? new UserEntity();

        entity.Cidusuario = login;
        entity.Cdescribe = nombre;
        entity.Cidgrupo = groupCode;
        entity.IdCaja = int.TryParse(TxtCfgUsrCaja.Text.Trim(), out int cj) ? cj : 1;
        entity.IdDeposito = int.TryParse(TxtCfgUsrDeposito.Text.Trim(), out int dp) ? dp : 1;
        entity.IdSucursal = _configService.Config.IdSucursal > 0 ? _configService.Config.IdSucursal : 1;
        entity.Csuspendido = 0;

        if (!string.IsNullOrWhiteSpace(PwdCfgUsrPass.Password))
        {
            entity.Cpassword = VfpCompat.Sys2007(PwdCfgUsrPass.Password.Trim());
        }
        else if (!isModify || string.IsNullOrWhiteSpace(entity.Cpassword))
        {
            entity.Cpassword = VfpCompat.Sys2007("123456");
        }

        try
        {
            await _systemRepository.SaveUserAsync(entity, isModify ? "M" : "A");

            // Also save or update all 12 Btn* permissions + caccesos in `grupos`
            var existingGroup = _groups.FirstOrDefault(g => string.Equals(g.Cidgrupo?.Trim(), groupCode, StringComparison.OrdinalIgnoreCase));
            var grpEntity = BuildGroupFromCheckboxes(groupCode, existingGroup);
            if (!string.IsNullOrWhiteSpace(TxtCfgCaccesos.Text))
                grpEntity.Caccesos = TxtCfgCaccesos.Text.Trim();
            await _systemRepository.SaveGroupAsync(grpEntity, existingGroup != null && existingGroup.Nlineno > 0 ? "M" : "A");

            await ReloadUsersAndGroupsAsync();
            PwdCfgUsrPass.Clear();
            LblFooterStatus.Text = $"✓ Usuario '{login}' (Rango: {groupCode}) y permisos guardados en MySQL.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al guardar usuario:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnActivateSelectedUser_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedUserForEdit == null || _windowManager == null)
        {
            MessageBox.Show("Seleccione primero un usuario de la tabla.", "Cambiar Usuario", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _windowManager.CurrentUser = _selectedUserForEdit;
        if (_systemRepository != null)
        {
            var grp = await _systemRepository.GetGroupByCodeAsync(_selectedUserForEdit.Cidgrupo);
            _windowManager.CurrentUserGroup = grp ?? new UserGroupEntity { Cidgrupo = _selectedUserForEdit.Cidgrupo };
        }
        UpdateHubSummaries();
        LblFooterStatus.Text = $"✓ Usuario activo cambiado a: {_selectedUserForEdit.Cidusuario} (Rango: {_selectedUserForEdit.Cidgrupo}).";
    }

    private async void BtnDeleteConfigUser_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedUserForEdit == null || _selectedUserForEdit.Nlineno <= 0 || _systemRepository == null)
            return;

        if (string.Equals(_selectedUserForEdit.Cidusuario.Trim(), "ADMINISTRA", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show("No se puede eliminar el usuario principal ADMINISTRA.", "Seguridad", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (MessageBox.Show($"¿Dar de baja al usuario '{_selectedUserForEdit.Cidusuario}'?", "Confirmar",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            await _systemRepository.DeleteUserAsync(_selectedUserForEdit.Nlineno);
            BtnNewUser_Click(sender, e);
            await ReloadUsersAndGroupsAsync();
        }
    }

    // =========================================================================
    // SUB-PAGE 7: DATOS DE EMPRESA Y UTILIDADES
    // =========================================================================
    private async void BtnSaveCompanyConfig_Click(object sender, RoutedEventArgs e)
    {
        _configService.Config.Timbrado = TxtCfgTimbrado.Text.Trim();
        if (int.TryParse(TxtCfgListaPrecios.Text.Trim(), out int lp) && lp is >= 1 and <= 7)
            _configService.Config.ListaPrecios = lp;
        _configService.SaveLocalSettings();

        if (_systemRepository != null)
        {
            try
            {
                _companyData.Descripcion = TxtCfgEmpNombre.Text.Trim().ToUpperInvariant();
                _companyData.Ruc = TxtCfgEmpRuc.Text.Trim();
                _companyData.Direccion = TxtCfgEmpDir.Text.Trim();
                _companyData.Telefonos = TxtCfgEmpTel.Text.Trim();
                _companyData.CobroCaja = ChkCfgCobroCaja.IsChecked == true ? 1 : 0;
                _companyData.Comercial = ChkCfgComercial.IsChecked == true ? 1 : 0;
                _companyData.Veterinaria = ChkCfgVeterinaria.IsChecked == true ? 1 : 0;
                _companyData.Restaurant = ChkCfgRestaurant.IsChecked == true ? 1 : 0;
                _companyData.InteresCuota = ParseDec(TxtCfgInteresCuota.Text);
                _companyData.InteresPunitorio = ParseDec(TxtCfgInteresPunitorio.Text);
                _companyData.Reclamos = ParseDec(TxtCfgReclamos.Text);
                _companyData.HonorarioJudicial = ParseDec(TxtCfgHonorarioJud.Text);
                _companyData.Utilidad1 = ParseDec(TxtCfgU1.Text);
                _companyData.Utilidad2 = ParseDec(TxtCfgU2.Text);
                _companyData.Utilidad3 = ParseDec(TxtCfgU3.Text);
                _companyData.Utilidad4 = ParseDec(TxtCfgU4.Text);
                _companyData.Utilidad5 = ParseDec(TxtCfgU5.Text);
                _companyData.Utilidad6 = ParseDec(TxtCfgU6.Text);
                _companyData.Utilidad7 = ParseDec(TxtCfgU7.Text);

                await _systemRepository.SaveCompanyDataAsync(_companyData);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar en MySQL:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
        }

        LblFooterStatus.Text = "✓ Datos de empresa, timbrado y porcentajes de utilidad (1..7) guardados.";
        MessageBox.Show("Datos de la empresa y utilidades guardados exitosamente.", "Empresa", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnOpenSucursalesCatalog_Click(object sender, RoutedEventArgs e)
        => _windowManager?.OpenMasterCatalogsWindow(2);

    private void BtnOpenTimbradosCatalog_Click(object sender, RoutedEventArgs e)
        => _windowManager?.OpenMasterCatalogsWindow(3);

    // =========================================================================
    // SUB-PAGE 8: EXPORTAR / IMPORTAR RESPALDO (.JSON)
    // =========================================================================
    private void BtnExportBackup_Click(object sender, RoutedEventArgs e)
    {
        var sfd = new SaveFileDialog
        {
            Title = "Exportar Copia de Seguridad de Configuración y Ajustes",
            Filter = "Respaldo Sefiros POS (*.json)|*.json",
            FileName = $"Respaldo_SefirosPOS_{DateTime.Now:yyyyMMdd_HHmm}.json"
        };

        if (sfd.ShowDialog() != true)
            return;

        try
        {
            var payload = new
            {
                ExportedAt = DateTime.Now,
                Preferences = _configService.Preferences,
                LocalConfig = _configService.Config,
                Company = _companyData
            };
            string json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(sfd.FileName, json, Encoding.UTF8);
            LblFooterStatus.Text = $"✓ Copia de seguridad exportada en {sfd.FileName}";
            MessageBox.Show($"Copia de seguridad guardada exitosamente en:\n{sfd.FileName}", "Respaldo Exportado", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al exportar respaldo:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnImportBackup_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new OpenFileDialog
        {
            Title = "Importar Copia de Seguridad (.json)",
            Filter = "Respaldo Sefiros POS (*.json)|*.json"
        };

        if (ofd.ShowDialog() != true)
            return;

        try
        {
            string json = File.ReadAllText(ofd.FileName, Encoding.UTF8);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("Preferences", out var prefElem))
            {
                var loaded = JsonSerializer.Deserialize<AppPreferencesEntity>(prefElem.GetRawText());
                if (loaded != null)
                {
                    _configService.Preferences.NoControlarStockGlobal = loaded.NoControlarStockGlobal;
                    _configService.Preferences.DescontarStockAunqueNoControle = loaded.DescontarStockAunqueNoControle;
                    _configService.Preferences.CongelarStockSinControl = loaded.CongelarStockSinControl;
                    _configService.Preferences.ItemFacturaModal = loaded.ItemFacturaModal;
                    _configService.Preferences.IniciarMovimientoRapidoPorDefecto = loaded.IniciarMovimientoRapidoPorDefecto;
                    _configService.Preferences.ReiniciarItemAlEquivocarseEnItemFactura = loaded.ReiniciarItemAlEquivocarseEnItemFactura;
                    _configService.Preferences.PermitirVentaBajoCosto = loaded.PermitirVentaBajoCosto;
                    _configService.Preferences.ValidarLimitesPrecioEnItem = loaded.ValidarLimitesPrecioEnItem;
                    _configService.Preferences.PrecioMinimoVenta = loaded.PrecioMinimoVenta >= 0m ? loaded.PrecioMinimoVenta : 99m;
                    _configService.Preferences.PrecioMaximoVenta = loaded.PrecioMaximoVenta >= 0m ? loaded.PrecioMaximoVenta : 1000000m;
                    _configService.Preferences.SonidoAlertaEnErrores = loaded.SonidoAlertaEnErrores;
                    _configService.Preferences.SincronizarGenesAlAbrir = loaded.SincronizarGenesAlAbrir;
                    _configService.Preferences.LimiteCantidadPorItem = loaded.LimiteCantidadPorItem >= 0m ? loaded.LimiteCantidadPorItem : 120m;
                    _configService.Preferences.CopiasTicket = loaded.CopiasTicket > 0 ? loaded.CopiasTicket : 1;
                    _configService.Preferences.LineasAvanceCorte = Math.Clamp(loaded.LineasAvanceCorte, 0, 8);
                    _configService.Preferences.CortarPapelAutomatico = loaded.CortarPapelAutomatico;
                    _configService.Preferences.AbrirCajonConTicket = loaded.AbrirCajonConTicket;
                    _configService.Preferences.UsarImpresionRawDirecta = loaded.UsarImpresionRawDirecta;
                    _configService.Preferences.MulticonexionTiempoRealActiva = loaded.MulticonexionTiempoRealActiva;
                    _configService.Preferences.IntervaloSincronizacionSegundos = loaded.IntervaloSincronizacionSegundos >= 2 ? loaded.IntervaloSincronizacionSegundos : 3;
                    _configService.Preferences.SolicitarLoginAlIniciar = loaded.SolicitarLoginAlIniciar;
                    _configService.Preferences.TemaAtelier = string.IsNullOrWhiteSpace(loaded.TemaAtelier) ? "sefiros" : loaded.TemaAtelier;
                    _configService.Preferences.TemaOscuroConfiguracion = loaded.TemaOscuroConfiguracion;
                    _configService.Preferences.EstiloPos = string.Equals(loaded.EstiloPos, "Clasico", StringComparison.OrdinalIgnoreCase) ? "Clasico" : "Renovado";
                    _configService.Preferences.PaletaColores = string.Equals(loaded.PaletaColores, "ClasicaFox", StringComparison.OrdinalIgnoreCase) ? "ClasicaFox" : "Moderna";
                    _configService.Preferences.MonedaBaseIso = LatamCurrencyCatalog.GetByCode(loaded.MonedaBaseIso).IsoCode;
                    _configService.Preferences.Idioma = LocalizationService.NormalizeCode(loaded.Idioma);
                    _configService.Preferences.CustomShortcuts = new Dictionary<string, string>(
                        loaded.CustomShortcuts ?? new Dictionary<string, string>(),
                        StringComparer.OrdinalIgnoreCase);
                    _configService.SavePreferences();

                    ThemeService.ApplyTheme(
                        _configService.Preferences.TemaAtelier,
                        _configService.Preferences.TemaOscuroConfiguracion,
                        _configService.Preferences.PaletaColores);
                    LocalizationService.ApplyLanguage(_configService.Preferences.Idioma);
                    _windowManager?.ApplyPosStyle(_configService.Preferences.EstiloPos);
                }
            }
            LoadPreferencesIntoControls();
            RefreshShortcutsGrid();
            UpdateHubSummaries();
            LblFooterStatus.Text = "✓ Copia de seguridad importada y aplicada exitosamente.";
            MessageBox.Show("Copia de seguridad restaurada exitosamente.", "Importar Respaldo", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al importar respaldo:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // =========================================================================
    // SUB-PAGE 3: KEYBOARD SHORTCUTS TABLE & CAPTURE
    // =========================================================================
    private void SelectRadioForWindowKey(string windowKey)
    {
        if (RbWinItemsFactura == null) return;
        switch (windowKey.ToUpperInvariant())
        {
            case ShortcutCatalog.WinAbmVentas:
                RbWinAbmVentas.IsChecked = true;
                break;
            case ShortcutCatalog.WinGenes:
                RbWinGenes.IsChecked = true;
                break;
            case ShortcutCatalog.WinFondoInicio:
                RbWinFondo.IsChecked = true;
                break;
            default:
                RbWinItemsFactura.IsChecked = true;
                break;
        }
    }

    private void RbWindowFilter_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;

        if (RbWinAbmVentas.IsChecked == true)
            _selectedWindowKey = ShortcutCatalog.WinAbmVentas;
        else if (RbWinGenes.IsChecked == true)
            _selectedWindowKey = ShortcutCatalog.WinGenes;
        else if (RbWinFondo.IsChecked == true)
            _selectedWindowKey = ShortcutCatalog.WinFondoInicio;
        else
            _selectedWindowKey = ShortcutCatalog.WinItemsFactura;

        _isCapturingKey = false;
        RefreshShortcutsGrid();
    }

    private void RefreshShortcutsGrid()
    {
        if (GridShortcuts == null) return;
        var list = ShortcutCatalog.GetBindingsForWindow(_configService.Preferences, _selectedWindowKey);
        GridShortcuts.ItemsSource = list;
        if (list.Count > 0 && GridShortcuts.SelectedIndex < 0)
            GridShortcuts.SelectedIndex = 0;
    }

    private void BtnStartCapture_Click(object sender, RoutedEventArgs e) => BeginKeyCaptureForSelectedRow();
    private void GridShortcuts_MouseDoubleClick(object sender, MouseButtonEventArgs e) => BeginKeyCaptureForSelectedRow();

    private void BeginKeyCaptureForSelectedRow()
    {
        if (GridShortcuts.SelectedItem is not ShortcutBindingItem selected)
        {
            MessageBox.Show("Seleccione primero una acción de la lista para asignarle una tecla.",
                "Asignar Tecla", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _isCapturingKey = true;
        LblCaptureStatus.Text = $"🔴 PRESIONE AHORA LA NUEVA TECLA PARA: '{selected.ActionName}' (ESC para cancelar)...";
        LblCaptureStatus.Foreground = TryFindResource("DangerTextBrush") as Brush ?? new SolidColorBrush(Color.FromRgb(185, 28, 28));
    }

    private void BtnResetSingleKey_Click(object sender, RoutedEventArgs e)
    {
        if (GridShortcuts.SelectedItem is not ShortcutBindingItem selected)
            return;

        _configService.Preferences.CustomShortcuts.Remove(selected.ActionId);
        _configService.SavePreferences();
        RefreshShortcutsGrid();
        LblFooterStatus.Text = $"✓ Se restauró '{selected.ActionName}' a su tecla original ({selected.DefaultKey}).";
    }

    private void BtnResetAllKeys_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(
                "¿Restaurar todos los atajos de teclado de todas las ventanas a sus valores originales?",
                "Confirmar Restauración",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        _configService.Preferences.CustomShortcuts.Clear();
        _configService.SavePreferences();
        RefreshShortcutsGrid();
        LblFooterStatus.Text = "✓ Todos los atajos fueron restaurados a sus teclas por defecto.";
    }

    private void BtnSaveAndClose_Click(object sender, RoutedEventArgs e)
    {
        SavePreferencesFromControls();
        Close();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_isCapturingKey)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                if (_currentPage != "HUB")
                    NavigateToPage("HUB", "Ajustes");
                else
                    Close();
            }
            return;
        }

        Key actualKey = e.Key == Key.System ? e.SystemKey : e.Key;

        if (actualKey is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
        {
            e.Handled = true;
            return;
        }

        e.Handled = true;

        if (actualKey == Key.Escape)
        {
            _isCapturingKey = false;
            LblCaptureStatus.Text = "Captura cancelada.";
            LblCaptureStatus.Foreground = TryFindResource("TextSecondaryBrush") as Brush ?? new SolidColorBrush(Color.FromRgb(92, 86, 82));
            return;
        }

        if (GridShortcuts.SelectedItem is not ShortcutBindingItem targetBinding)
        {
            _isCapturingKey = false;
            return;
        }

        string combo = KeyShortcutHelper.FormatKeyCombo(actualKey, Keyboard.Modifiers);
        if (string.IsNullOrWhiteSpace(combo))
            return;

        var windowBindings = ShortcutCatalog.GetBindingsForWindow(_configService.Preferences, _selectedWindowKey);
        var duplicate = windowBindings.FirstOrDefault(b =>
            b.ActionId != targetBinding.ActionId &&
            string.Equals(b.CurrentKey, combo, StringComparison.OrdinalIgnoreCase));

        if (duplicate != null)
        {
            _isCapturingKey = false;
            LblCaptureStatus.Text = $"⚠ La tecla '{combo}' ya está en uso por '{duplicate.ActionName}'.";
            MessageBox.Show(
                $"La tecla '{combo}' ya está asignada a la función:\n\n• {duplicate.ActionName}\n\nElija otra tecla disponible.",
                "Tecla Duplicada",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        _configService.Preferences.CustomShortcuts[targetBinding.ActionId] = combo;
        _configService.SavePreferences();
        _isCapturingKey = false;

        RefreshShortcutsGrid();
        LblCaptureStatus.Text = $"✓ Tecla [{combo}] asignada a '{targetBinding.ActionName}'.";
        LblCaptureStatus.Foreground = TryFindResource("SuccessTextBrush") as Brush ?? new SolidColorBrush(Color.FromRgb(6, 95, 70));
    }

    private static decimal ParseDec(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0m;
        string clean = s.Trim().Replace(" ", "").Replace(",", ".");
        return decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal d) ? d : 0m;
    }
}
