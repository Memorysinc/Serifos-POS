using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Media;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FirewallControl.App.Services;
using FirewallControl.Core.Entities;
using FirewallControl.Core.Interfaces;
using Microsoft.Win32;

namespace FirewallControl.App.Views;

/// <summary>
/// Complete native WPF implementation of PROGS\GenesSelector.cs and PROGS\genes_system.prg:
/// - 4 Department themes:
///   * Gen 1: Verdulería [A] (Green #28A745)
///   * Gen 2: Carnicería [S] (Red #DC3545)
///   * Gen 3: Panadería [D] (Orange #F5780F)
///   * Gen 4: Almacén [F] (Blue #0D6EFD)
/// - 5-column product card grid with cached images from images\genes\gen1..4\
/// - Exact keyboard navigation:
///   * Left (-1), Right (+1), Up (-5), Down (+5), PageUp (-10), PageDown (+10), Home (0), End (Count-1)
///   * Enter: Opens Cargar Producto dialog (KG amount in Gs, DOCENA quantity, UNIDAD quantity)
///   * Delete: Deletes selected product from Gen
///   * A / S / D / F: Switches to Gen 1..4
///   * Ctrl+A / S / D / F: Opens Agregar Producto (+ Nuevo) for Gen 1..4
///   * Alt+A / S / D / F: Opens Editar Producto for Gen 1..4
///   * Escape: Closes and returns focus to ITEMSFACTURAVENTAS.codproducto
/// </summary>
public partial class GenesSelectorWindow : Window
{
    private sealed record GenPalette(
        int Gen,
        string Name,
        Color Primary,
        Color PrimaryDark,
        Color SoftBg,
        Color CardBorderSelected,
        Color PriceText);

    private static readonly Dictionary<int, GenPalette> Palettes = new()
    {
        [1] = new(1, "Verdulería [A]", Color.FromRgb(47, 122, 91), Color.FromRgb(33, 92, 67), Color.FromRgb(238, 246, 242), Color.FromRgb(47, 122, 91), Color.FromRgb(33, 92, 67)),
        [2] = new(2, "Carnicería [S]", Color.FromRgb(158, 71, 78), Color.FromRgb(125, 51, 57), Color.FromRgb(248, 236, 238), Color.FromRgb(158, 71, 78), Color.FromRgb(125, 51, 57)),
        [3] = new(3, "Panadería [D]", Color.FromRgb(166, 117, 59), Color.FromRgb(131, 89, 40), Color.FromRgb(249, 242, 232), Color.FromRgb(166, 117, 59), Color.FromRgb(131, 89, 40)),
        [4] = new(4, "Almacén [F]", Color.FromRgb(74, 102, 140), Color.FromRgb(54, 77, 109), Color.FromRgb(238, 242, 247), Color.FromRgb(74, 102, 140), Color.FromRgb(54, 77, 109))
    };

    private static Brush ResolveBrush(string key, Color fallback) =>
        Application.Current?.TryFindResource(key) as Brush ?? new SolidColorBrush(fallback);

    private static readonly Dictionary<string, BitmapImage> ImageCache = new(StringComparer.OrdinalIgnoreCase);

    private readonly IVfpDbfService _dbfService;
    private readonly IConfigService _configService;
    private readonly IProductRepository _productRepository;
    private readonly IWindowManagerService _windowManager;

    private Action<GenesSelectionResult>? _onProductSelected;
    private int _currentGenId = 1;
    private List<GenesButtonItem> _items = new();
    private readonly List<Border> _cardBorders = new();
    private int _selectedIndex;

    private sealed record CardMetrics(
        double CardHeight,
        double IconRowHeight,
        double IconHeight,
        double IconMaxWidth,
        double CircleSize);

    public double CurrentCardHeight { get; private set; }
    public double CurrentIconHeight { get; private set; }

    public GenesSelectorWindow(
        IVfpDbfService dbfService,
        IConfigService configService,
        IProductRepository productRepository,
        IWindowManagerService windowManager)
    {
        InitializeComponent();
        AppIconHelper.ApplySefirosIcon(this);
        _dbfService = dbfService;
        _configService = configService;
        _productRepository = productRepository;
        _windowManager = windowManager;

        ApplyFullVerticalScreenBounds();
        Loaded += (_, _) =>
        {
            ApplyFullVerticalScreenBounds();
            UpdateRenderedCardMetrics();
        };
        CardsScrollViewer.SizeChanged += (_, _) => UpdateRenderedCardMetrics();
    }

    public void ApplyFullVerticalScreenBounds()
    {
        Rect work = SystemParameters.WorkArea;
        if (work.Width <= 200 || work.Height <= 200)
            work = new Rect(0, 0, 1366, 728);

        if (WindowState != WindowState.Normal)
            WindowState = WindowState.Normal;
        WindowStartupLocation = WindowStartupLocation.Manual;

        double targetW = Math.Round(Math.Clamp(work.Width - 36, Math.Min(1180, work.Width - 16), Math.Min(1260, work.Width - 16)));
        Width = Math.Min(targetW, work.Width);
        Height = Math.Round(work.Height);
        Top = Math.Round(work.Top);
        Left = Math.Round(work.Left + Math.Max(0, (work.Width - Width) / 2.0));
    }

    private CardMetrics ComputeCardMetrics()
    {
        double availH = CardsScrollViewer.ViewportHeight > 120
            ? CardsScrollViewer.ViewportHeight - 12
            : Math.Max(460, Height - 168);

        int visibleRows = availH >= 760 ? 5 : 4;
        double cardH = Math.Clamp(Math.Floor((availH / visibleRows) - 8), 140, 178);
        double iconRowH = Math.Round(cardH * 0.64);
        double iconH = Math.Round(iconRowH - 6);
        double iconMaxW = Math.Round(iconH * 1.45);
        double circleSize = Math.Round(iconH * 0.78);

        CurrentCardHeight = cardH;
        CurrentIconHeight = iconH;
        return new CardMetrics(cardH, iconRowH, iconH, iconMaxW, circleSize);
    }

    private void UpdateRenderedCardMetrics()
    {
        var metrics = ComputeCardMetrics();
        if (_cardBorders.Count == 0)
            return;

        foreach (var border in _cardBorders)
        {
            border.Height = metrics.CardHeight;
            if (border.Child is Grid cardGrid && cardGrid.RowDefinitions.Count > 0)
            {
                cardGrid.RowDefinitions[0].Height = new GridLength(metrics.IconRowHeight);
                if (cardGrid.Children.Count > 0)
                {
                    if (cardGrid.Children[0] is Image img)
                    {
                        img.Height = metrics.IconHeight;
                        img.MaxWidth = metrics.IconMaxWidth;
                    }
                    else if (cardGrid.Children[0] is Border circle)
                    {
                        circle.Width = metrics.CircleSize;
                        circle.Height = metrics.CircleSize;
                        circle.CornerRadius = new CornerRadius(metrics.CircleSize / 2.0);
                    }
                }
            }
        }
    }

    public void Configure(int genId, string initialMode = "SELECT", Action<GenesSelectionResult>? onProductSelected = null)
    {
        ApplyFullVerticalScreenBounds();
        _currentGenId = Math.Clamp(genId, 1, 4);
        _onProductSelected = onProductSelected;
        SwitchGen(_currentGenId);
        EnsureKeyboardFocus();

        // Automatically sync prices from MySQL productos table for items with Actualizar == true
        if (_configService.Preferences.SincronizarGenesAlAbrir)
        {
            _ = SyncPricesFromMySqlAsync(showConfirmation: false);
        }

        string mode = (initialMode ?? "SELECT").Trim().ToUpperInvariant();
        if (mode == "AGREGAR")
        {
            Dispatcher.BeginInvoke(new Action(() => OpenAgregarDialog(_currentGenId)));
        }
        else if (mode == "EDITAR")
        {
            Dispatcher.BeginInvoke(new Action(() => OpenEditarDialog(_currentGenId, GetSelectedProduct())));
        }
    }

    private void Window_Activated(object sender, EventArgs e) => EnsureKeyboardFocus();

    private void Window_MouseDown(object sender, MouseButtonEventArgs e) => EnsureKeyboardFocus();

    private void EnsureKeyboardFocus()
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            RootGrid.Focus();
            Keyboard.Focus(RootGrid);
        }), System.Windows.Threading.DispatcherPriority.Input);
    }

    private string GetBaseDir()
    {
        return string.IsNullOrWhiteSpace(_configService.Config.BaseDirectory)
            ? AppDomain.CurrentDomain.BaseDirectory
            : _configService.Config.BaseDirectory;
    }

    private string GetDbfPath() => Path.Combine(GetBaseDir(), "genes_productos.dbf");

    private GenPalette GetPalette(int genId)
        => Palettes.TryGetValue(genId, out var p) ? p : Palettes[1];

    private void SwitchGen(int genId, bool preserveCurrentSelection = false)
    {
        _currentGenId = Math.Clamp(genId, 1, 4);
        var pal = GetPalette(_currentGenId);

        Background = ResolveBrush("ScreenBgBrush", Color.FromRgb(248, 247, 245));
        SubHeaderBorder.Background = ResolveBrush("CardBgBrush", Colors.White);
        SubHeaderBorder.BorderBrush = new SolidColorBrush(pal.Primary);
        BtnNuevo.Background = new SolidColorBrush(pal.Primary);

        StyleGenTabButton(BtnGen1, 1);
        StyleGenTabButton(BtnGen2, 2);
        StyleGenTabButton(BtnGen3, 3);
        StyleGenTabButton(BtnGen4, 4);

        _items = _dbfService.LoadAllGenesProducts(GetDbfPath())
            .Where(x => x.GenId == _currentGenId && !string.IsNullOrWhiteSpace(x.Nombre))
            .OrderBy(x => x.Orden <= 0 ? 999999 : x.Orden)
            .ThenBy(x => x.Id)
            .ToList();

        LblGenTitle.Text = $"GEN {_currentGenId}  —  {pal.Name.ToUpperInvariant()} ({_items.Count} productos)";

        // Always start from the first product (index 0, e.g., Tomate) when opening or switching a Gen!
        if (!preserveCurrentSelection)
        {
            _selectedIndex = 0;
            CardsScrollViewer.ScrollToHome();
        }
        else
        {
            _selectedIndex = Math.Clamp(_selectedIndex, 0, Math.Max(0, _items.Count - 1));
        }

        RenderCards(pal);
        if (_items.Count > 0)
        {
            SelectCard(_selectedIndex);
        }
        EnsureKeyboardFocus();
    }

    /// <summary>
    /// Exact replication of SincronizarPreciosAutoGen in PROGS\genes_system.prg:
    /// Queries MySQL productos table for all gene items where Actualizar == true && Precio > 1,
    /// updates any changed Precio / Costo in genes_productos.dbf (and styles\genes_productos.dbf),
    /// and refreshes the current Gen cards without losing the selection.
    /// </summary>
    private async Task SyncPricesFromMySqlAsync(bool showConfirmation)
    {
        try
        {
            string dbfPath = GetDbfPath();
            var allGenes = _dbfService.LoadAllGenesProducts(dbfPath);
            var codesToSync = allGenes
                .Where(g => g.Actualizar && g.Precio != 1m && !string.IsNullOrWhiteSpace(g.Codigo))
                .Select(g => g.Codigo.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (codesToSync.Count == 0)
            {
                if (showConfirmation)
                    LblStatus.Text = "✔ No hay productos pendientes de sincronizar con MySQL.";
                return;
            }

            var sqlPrices = await _productRepository.GetPricesByCodesAsync(codesToSync);
            int updatedCount = 0;

            foreach (var gene in allGenes)
            {
                if (!gene.Actualizar || gene.Precio == 1m || string.IsNullOrWhiteSpace(gene.Codigo))
                    continue;

                if (sqlPrices.TryGetValue(gene.Codigo.Trim(), out var sqlInfo) && sqlInfo.Precio1 > 1m)
                {
                    if (gene.Precio != sqlInfo.Precio1 || (sqlInfo.Costo1 > 0m && gene.Costo != sqlInfo.Costo1))
                    {
                        gene.Precio = sqlInfo.Precio1;
                        if (sqlInfo.Costo1 > 0m)
                            gene.Costo = sqlInfo.Costo1;
                        updatedCount++;
                    }
                }
            }

            if (updatedCount > 0)
            {
                _dbfService.SaveAllGenesProducts(dbfPath, allGenes);
                SwitchGen(_currentGenId, preserveCurrentSelection: true);
                LblStatus.Text = $"✔ {updatedCount} precio(s) actualizados automáticamente desde la base de datos MySQL.";
            }
            else if (showConfirmation)
            {
                SwitchGen(_currentGenId, preserveCurrentSelection: true);
                LblStatus.Text = $"✔ Precios sincronizados con MySQL ({sqlPrices.Count} productos verificados al día).";
            }
        }
        catch (Exception ex)
        {
            if (showConfirmation)
            {
                LblStatus.Text = $"⚠ Error al sincronizar precios con MySQL: {ex.Message}";
            }
        }
    }

    private void StyleGenTabButton(Button btn, int genId)
    {
        var pal = GetPalette(genId);
        bool isActive = genId == _currentGenId;
        btn.Background = isActive
            ? new SolidColorBrush(pal.Primary)
            : ResolveBrush("SurfaceAltBrush", Color.FromRgb(239, 237, 234));
        btn.Foreground = isActive
            ? Brushes.White
            : ResolveBrush("TextSecondaryBrush", Color.FromRgb(92, 86, 82));
        btn.BorderBrush = isActive
            ? new SolidColorBrush(pal.PrimaryDark)
            : ResolveBrush("CardBorderBrush", Color.FromRgb(222, 217, 211));
        btn.BorderThickness = new Thickness(isActive ? 1.5 : 1);
    }

    private void RenderCards(GenPalette pal)
    {
        CardsUniformGrid.Children.Clear();
        _cardBorders.Clear();
        var metrics = ComputeCardMetrics();

        for (int i = 0; i < _items.Count; i++)
        {
            int cardIndex = i;
            var prod = _items[i];

            var cardGrid = new Grid();
            cardGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(metrics.IconRowHeight) });
            cardGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            cardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Product Image or Initial Circle (proporción ampliada estilo FoxPro)
            var bmp = LoadCachedBitmap(prod.IconoResolved);
            if (bmp != null)
            {
                var img = new Image
                {
                    Source = bmp,
                    Height = metrics.IconHeight,
                    MaxWidth = metrics.IconMaxWidth,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(6, 4, 6, 2)
                };
                RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                Grid.SetRow(img, 0);
                cardGrid.Children.Add(img);
            }
            else
            {
                var circle = new Border
                {
                    Width = metrics.CircleSize,
                    Height = metrics.CircleSize,
                    CornerRadius = new CornerRadius(metrics.CircleSize / 2.0),
                    Background = new SolidColorBrush(pal.SoftBg),
                    BorderBrush = new SolidColorBrush(pal.Primary),
                    BorderThickness = new Thickness(1.5),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 4, 0, 2),
                    Child = new TextBlock
                    {
                        Text = string.IsNullOrWhiteSpace(prod.Nombre) ? "?" : prod.Nombre.Substring(0, 1).ToUpperInvariant(),
                        FontSize = 26,
                        FontWeight = FontWeights.ExtraBold,
                        Foreground = new SolidColorBrush(pal.PrimaryDark),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                };
                Grid.SetRow(circle, 0);
                cardGrid.Children.Add(circle);
            }

            // Product Name
            var lblName = new TextBlock
            {
                Text = prod.Nombre,
                FontWeight = FontWeights.SemiBold,
                FontSize = 13,
                Foreground = ResolveBrush("TextPrimaryBrush", Color.FromRgb(31, 28, 26)),
                TextWrapping = TextWrapping.Wrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 1, 6, 1)
            };
            Grid.SetRow(lblName, 1);
            cardGrid.Children.Add(lblName);

            // Price & Unit Footer
            var lblPrice = new TextBlock
            {
                Text = prod.PrecioFormatted,
                FontWeight = FontWeights.ExtraBold,
                FontSize = 12,
                Foreground = new SolidColorBrush(pal.PriceText),
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(4, 0, 4, 5)
            };
            Grid.SetRow(lblPrice, 2);
            cardGrid.Children.Add(lblPrice);

            var border = new Border
            {
                Height = metrics.CardHeight,
                Margin = new Thickness(4),
                CornerRadius = new CornerRadius(8),
                Child = cardGrid,
                Cursor = Cursors.Hand,
                Tag = cardIndex
            };

            border.MouseLeftButtonDown += (_, e) =>
            {
                SelectCard(cardIndex);
                if (e.ClickCount == 2)
                {
                    ActivateCard(cardIndex);
                }
            };

            _cardBorders.Add(border);
            CardsUniformGrid.Children.Add(border);
        }

        UpdateCardSelectionStyles();
    }

    private void SelectCard(int index)
    {
        if (_items.Count == 0) return;
        _selectedIndex = Math.Clamp(index, 0, _items.Count - 1);
        UpdateCardSelectionStyles();

        if (_selectedIndex >= 0 && _selectedIndex < _cardBorders.Count)
        {
            _cardBorders[_selectedIndex].BringIntoView();
            var p = _items[_selectedIndex];
            LblStatus.Text = $"[{_selectedIndex + 1}/{_items.Count}] {p.Nombre} — Código: {p.Codigo} — {p.PrecioFormatted} (Presione ENTER para cargar)";
        }
    }

    private void UpdateCardSelectionStyles()
    {
        var pal = GetPalette(_currentGenId);
        for (int i = 0; i < _cardBorders.Count; i++)
        {
            bool sel = i == _selectedIndex;
            var b = _cardBorders[i];
            b.Background = sel
                ? (ThemeService.CurrentIsDark ? ResolveBrush("SurfaceAccentBrush", pal.SoftBg) : new SolidColorBrush(pal.SoftBg))
                : ResolveBrush("CardBgBrush", Colors.White);
            b.BorderBrush = sel
                ? new SolidColorBrush(pal.CardBorderSelected)
                : ResolveBrush("CardBorderBrush", Color.FromRgb(222, 217, 211));
            b.BorderThickness = new Thickness(sel ? 2.5 : 1);
        }
    }

    private GenesButtonItem? GetSelectedProduct()
        => _selectedIndex >= 0 && _selectedIndex < _items.Count ? _items[_selectedIndex] : null;

    private void ActivateCard(int index)
    {
        if (index < 0 || index >= _items.Count) return;
        _selectedIndex = index;
        UpdateCardSelectionStyles();

        var prod = _items[index];

        // Exact behavior of OnCardActivated in PROGS\GenesSelector.cs:
        // Hide the big 5-column selector grid so ONLY the small "Cargar Producto" dialog is visible.
        Hide();

        var selection = ShowCargarDialog(prod);
        if (selection != null)
        {
            var callback = _onProductSelected;
            callback?.Invoke(selection);
        }
        else
        {
            // User cancelled with ESC -> restore the Genes selector grid and keyboard focus
            ApplyFullVerticalScreenBounds();
            Show();
            Activate();
            EnsureKeyboardFocus();
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (IsVisible)
                {
                    Activate();
                    RootGrid.Focus();
                    Keyboard.Focus(RootGrid);
                }
            }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }
    }

    private static BitmapImage? LoadCachedBitmap(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        if (ImageCache.TryGetValue(path, out var cached))
            return cached;

        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(path, UriKind.Absolute);
            bmp.EndInit();
            bmp.Freeze();
            ImageCache[path] = bmp;
            return bmp;
        }
        catch
        {
            return null;
        }
    }

    // =====================================================================
    // EXACT KEYBOARD NAVIGATION (PROGS\GenesSelector.cs lines 1425-1511)
    // =====================================================================
    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var mods = Keyboard.Modifiers;

        string combo = KeyShortcutHelper.NormalizeKeyEvent(e);
        if (!string.IsNullOrWhiteSpace(combo))
        {
            var binding = ShortcutCatalog.ResolveTriggeredBinding(
                _configService.Preferences,
                ShortcutCatalog.WinGenes,
                combo,
                isInputEmpty: true);

            if (binding != null)
            {
                switch (binding.ActionCode)
                {
                    case "GEN_1": e.Handled = true; SwitchGen(1); return;
                    case "GEN_2": e.Handled = true; SwitchGen(2); return;
                    case "GEN_3": e.Handled = true; SwitchGen(3); return;
                    case "GEN_4": e.Handled = true; SwitchGen(4); return;
                    case "SYNC_SQL": e.Handled = true; _ = SyncPricesFromMySqlAsync(showConfirmation: true); return;
                    case "DEL_PROD": e.Handled = true; DeleteSelectedProduct(); return;
                }
            }
        }

        if (mods == ModifierKeys.Control)
        {
            int genCtrl = KeyToGen(e.Key);
            if (genCtrl > 0)
            {
                e.Handled = true;
                SwitchGen(genCtrl);
                OpenAgregarDialog(genCtrl);
                return;
            }
        }
        else if (mods == ModifierKeys.Alt)
        {
            Key actual = e.Key == Key.System ? e.SystemKey : e.Key;
            int genAlt = KeyToGen(actual);
            if (genAlt > 0)
            {
                e.Handled = true;
                SwitchGen(genAlt);
                OpenEditarDialog(genAlt, GetSelectedProduct());
                return;
            }
        }
        else if (mods == ModifierKeys.None || mods == ModifierKeys.Shift)
        {
            int genDirect = KeyToGen(e.Key);
            if (genDirect > 0)
            {
                e.Handled = true;
                SwitchGen(genDirect);
                return;
            }

            if (e.Key == Key.F5)
            {
                e.Handled = true;
                _ = SyncPricesFromMySqlAsync(showConfirmation: true);
                return;
            }

            switch (e.Key)
            {
                case Key.Escape:
                    e.Handled = true;
                    _windowManager.SafeHideAndRestoreFocus(this);
                    return;

                case Key.Right:
                    e.Handled = true;
                    if (_selectedIndex + 1 < _items.Count) SelectCard(_selectedIndex + 1);
                    return;

                case Key.Left:
                    e.Handled = true;
                    if (_selectedIndex - 1 >= 0) SelectCard(_selectedIndex - 1);
                    return;

                case Key.Down:
                    e.Handled = true;
                    if (_items.Count > 0)
                    {
                        SelectCard(_selectedIndex + 5 < _items.Count ? _selectedIndex + 5 : _items.Count - 1);
                    }
                    return;

                case Key.Up:
                    e.Handled = true;
                    if (_selectedIndex - 5 >= 0) SelectCard(_selectedIndex - 5);
                    return;

                case Key.PageDown:
                    e.Handled = true;
                    if (_items.Count > 0)
                    {
                        SelectCard(_selectedIndex + 10 < _items.Count ? _selectedIndex + 10 : _items.Count - 1);
                    }
                    return;

                case Key.PageUp:
                    e.Handled = true;
                    if (_items.Count > 0)
                    {
                        SelectCard(Math.Max(0, _selectedIndex - 10));
                    }
                    return;

                case Key.Home:
                    e.Handled = true;
                    if (_items.Count > 0) SelectCard(0);
                    return;

                case Key.End:
                    e.Handled = true;
                    if (_items.Count > 0) SelectCard(_items.Count - 1);
                    return;

                case Key.Enter:
                    e.Handled = true;
                    if (_selectedIndex >= 0 && _selectedIndex < _items.Count)
                    {
                        ActivateCard(_selectedIndex);
                    }
                    return;

                case Key.Delete:
                    e.Handled = true;
                    DeleteSelectedProduct();
                    return;
            }
        }
    }

    private static int KeyToGen(Key k) => k switch
    {
        Key.A => 1,
        Key.S => 2,
        Key.D => 3,
        Key.F => 4,
        _ => 0
    };

    private void BtnGen_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && int.TryParse(btn.Tag?.ToString(), out int gId))
        {
            SwitchGen(gId);
        }
    }

    private async void BtnActualizarSql_Click(object sender, RoutedEventArgs e)
        => await SyncPricesFromMySqlAsync(showConfirmation: true);

    private void BtnNuevo_Click(object sender, RoutedEventArgs e) => OpenAgregarDialog(_currentGenId);
    private void BtnEditar_Click(object sender, RoutedEventArgs e) => OpenEditarDialog(_currentGenId, GetSelectedProduct());
    private void BtnReordenar_Click(object sender, RoutedEventArgs e) => OpenReordenarDialog(_currentGenId);
    private void BtnEliminar_Click(object sender, RoutedEventArgs e) => DeleteSelectedProduct();
    private void BtnCerrar_Click(object sender, RoutedEventArgs e) => _windowManager.SafeHideAndRestoreFocus(this);

    private void DeleteSelectedProduct()
    {
        var p = GetSelectedProduct();
        if (p == null) return;

        var ask = MessageBox.Show(
            this,
            $"¿Eliminar '{p.Nombre}' de este Gen?",
            "Confirmar Eliminación",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (ask == MessageBoxResult.Yes)
        {
            _dbfService.DeleteGenesProduct(GetDbfPath(), p.Id);
            SwitchGen(_currentGenId);
            LblStatus.Text = $"✔ Producto '{p.Nombre}' eliminado del Gen {_currentGenId}.";
        }
    }

    // =====================================================================
    // DIALOG 1: CARGAR PRODUCTO (Exact replica of GenesCargarForm in PROGS\GenesSelector.cs)
    // =====================================================================
    private GenesSelectionResult? ShowCargarDialog(GenesButtonItem product)
    {
        var pal = GetPalette(_currentGenId);
        string effectiveTipo = product.Precio == 1m ? "KG" : (string.IsNullOrWhiteSpace(product.Tipo) ? "KG" : product.Tipo.ToUpperInvariant());
        decimal effectivePrecioBase = product.Precio == 1m ? 1m : product.Precio;

        var dlg = new Window
        {
            Title = "Cargar Producto",
            Width = 480,
            Height = 545,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Owner = (Owner != null && Owner.IsLoaded && Owner.IsVisible) ? Owner : null,
            ResizeMode = ResizeMode.NoResize,
            Background = ResolveBrush("CardBgBrush", Colors.White),
            Foreground = ResolveBrush("TextPrimaryBrush", Color.FromRgb(31, 28, 26)),
            FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI"),
            Topmost = true,
            ShowInTaskbar = false
        };
        AppIconHelper.ApplySefirosIcon(dlg);

        var root = new StackPanel { Margin = new Thickness(24, 16, 24, 16) };

        // 1. Product Name
        root.Children.Add(new TextBlock
        {
            Text = product.Nombre,
            FontSize = 24,
            FontWeight = FontWeights.ExtraBold,
            Foreground = ResolveBrush("TextPrimaryBrush", Color.FromRgb(31, 28, 26)),
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        });

        // 2. Price Base Info
        string priceHeaderText = product.Precio == 1m
            ? "Monto Libre (Gs)"
            : effectiveTipo switch
            {
                "KG" => $"Precio Base: {effectivePrecioBase:N0} Gs/Kg",
                "DOCENA" => $"Precio Base: {effectivePrecioBase:N0} la doc.",
                _ => $"Precio Base: {effectivePrecioBase:N0} c/u"
            };

        root.Children.Add(new TextBlock
        {
            Text = priceHeaderText,
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Foreground = ResolveBrush("PrimaryBrush", pal.Primary),
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 4, 0, 8)
        });

        // 3. Instruction
        string promptText = effectiveTipo switch
        {
            "KG" => "Ingrese Monto en Guaraníes:",
            "DOCENA" => "Ingrese Cantidad de Docenas:",
            _ => "Ingrese Cantidad:"
        };

        root.Children.Add(new TextBlock
        {
            Text = promptText,
            FontSize = 14,
            Foreground = ResolveBrush("TextSecondaryBrush", Color.FromRgb(92, 86, 82)),
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 6)
        });

        // 4. Value Input Box with department color border and bottom accent bar
        var valContainer = new Border
        {
            Background = new SolidColorBrush(pal.SoftBg),
            BorderBrush = new SolidColorBrush(pal.Primary),
            BorderThickness = new Thickness(1, 1, 1, 4),
            CornerRadius = new CornerRadius(4),
            Margin = new Thickness(10, 0, 10, 12)
        };

        var txtValor = new TextBox
        {
            FontSize = 38,
            FontWeight = FontWeights.ExtraBold,
            TextAlignment = TextAlignment.Center,
            Padding = new Thickness(8, 4, 8, 4),
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(pal.PriceText),
            BorderThickness = new Thickness(0)
        };
        valContainer.Child = txtValor;
        root.Children.Add(valContainer);

        // 5. Category-colored Icon Frame (190 x 174) with white inner box & product image
        var iconOuterFrame = new Border
        {
            Width = 190,
            Height = 170,
            Background = new SolidColorBrush(pal.SoftBg),
            BorderBrush = new SolidColorBrush(pal.CardBorderSelected),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 10)
        };

        var iconInnerBox = new Border
        {
            Background = Brushes.White,
            CornerRadius = new CornerRadius(4)
        };

        var bmp = LoadCachedBitmap(product.IconoResolved);
        if (bmp != null)
        {
            iconInnerBox.Child = new Image
            {
                Source = bmp,
                Stretch = Stretch.Uniform,
                Margin = new Thickness(6)
            };
        }
        else
        {
            iconInnerBox.Child = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(product.Nombre) ? "?" : product.Nombre[..1].ToUpperInvariant(),
                FontSize = 48,
                FontWeight = FontWeights.ExtraBold,
                Foreground = new SolidColorBrush(pal.Primary),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }
        iconOuterFrame.Child = iconInnerBox;
        root.Children.Add(iconOuterFrame);

        // 6. Live Calculation Label
        var lblVivo = new TextBlock
        {
            FontSize = 17,
            FontWeight = FontWeights.Bold,
            Foreground = ResolveBrush("SuccessTextBrush", Color.FromRgb(33, 107, 80)),
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 2, 0, 12),
            Height = 26
        };
        root.Children.Add(lblVivo);

        // 7. Side-by-side Buttons: Aceptar [Enter] and Cancelar [ESC]
        var btnRow = new Grid { Margin = new Thickness(10, 0, 10, 0) };
        btnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        btnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        btnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var btnConfirmar = new Button
        {
            Content = "Aceptar [Enter]",
            Height = 50,
            FontSize = 14,
            FontWeight = FontWeights.ExtraBold,
            Background = new SolidColorBrush(pal.Primary),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand
        };
        Grid.SetColumn(btnConfirmar, 0);
        btnRow.Children.Add(btnConfirmar);

        var btnCancelar = new Button
        {
            Content = "Cancelar [ESC]",
            Height = 50,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Background = new SolidColorBrush(Color.FromRgb(240, 242, 245)),
            Foreground = new SolidColorBrush(Color.FromRgb(35, 35, 35)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(200, 205, 212)),
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand
        };
        Grid.SetColumn(btnCancelar, 2);
        btnRow.Children.Add(btnCancelar);

        root.Children.Add(btnRow);

        bool suppressTextChange = false;
        long ParseDigits(string raw)
        {
            string digits = new string((raw ?? "").Where(char.IsDigit).ToArray());
            return long.TryParse(digits, out long v) ? v : 0L;
        }

        txtValor.TextChanged += (_, _) =>
        {
            if (suppressTextChange) return;
            long val = ParseDigits(txtValor.Text);
            suppressTextChange = true;
            txtValor.Text = val > 0 ? val.ToString("N0", new CultureInfo("es-PY")) : "";
            txtValor.CaretIndex = txtValor.Text.Length;
            suppressTextChange = false;

            if (val <= 0)
            {
                lblVivo.Text = "";
            }
            else if (effectiveTipo == "KG")
            {
                if (effectivePrecioBase > 1m)
                {
                    decimal kg = Math.Round((decimal)val / effectivePrecioBase, 3);
                    lblVivo.Text = $"Equivale a: {kg:0.000} Kg";
                }
                else
                {
                    lblVivo.Text = $"Monto a cobrar: {val:N0} Gs";
                }
            }
            else if (effectiveTipo == "DOCENA")
            {
                long totalGs = (long)Math.Round(val * effectivePrecioBase, 0);
                lblVivo.Text = $"Total: {totalGs:N0} Gs ({val * 12:N0} unid.)";
            }
            else
            {
                long totalGs = (long)Math.Round(val * effectivePrecioBase, 0);
                lblVivo.Text = $"Total: {totalGs:N0} Gs";
            }
        };

        GenesSelectionResult? result = null;
        void Confirm()
        {
            long val = ParseDigits(txtValor.Text);
            if (val <= 0)
            {
                SystemSounds.Hand.Play();
                txtValor.Focus();
                return;
            }
            result = new GenesSelectionResult(product, effectiveTipo, effectivePrecioBase, val);
            dlg.DialogResult = true;
            dlg.Close();
        }

        btnConfirmar.Click += (_, _) => Confirm();
        btnCancelar.Click += (_, _) =>
        {
            dlg.DialogResult = false;
            dlg.Close();
        };

        dlg.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                dlg.DialogResult = false;
                dlg.Close();
            }
            else if (e.Key == Key.Enter || e.Key == Key.Return)
            {
                e.Handled = true;
                Confirm();
            }
        };

        dlg.Content = root;
        dlg.Loaded += (_, _) =>
        {
            dlg.Activate();
            txtValor.Focus();
        };
        return dlg.ShowDialog() == true ? result : null;
    }

    // =====================================================================
    // DIALOG 2: AGREGAR PRODUCTO (+ Nuevo / Ctrl+A..F)
    // =====================================================================
    private void OpenAgregarDialog(int genId)
    {
        var pal = GetPalette(genId);
        var dlg = new Window
        {
            Title = $"Agregar Producto - Gen {genId} ({pal.Name})",
            Width = 520,
            Height = 610,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            ResizeMode = ResizeMode.NoResize,
            Background = ResolveBrush("CardBgBrush", Colors.White),
            Foreground = ResolveBrush("TextPrimaryBrush", Color.FromRgb(31, 28, 26)),
            FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI")
        };

        var sp = new StackPanel { Margin = new Thickness(20, 14, 20, 14) };

        var btnLupita = new Button
        {
            Content = "🔍 BUSCAR PRODUCTO DEL NEGOCIO (LUPITA)",
            Height = 38,
            FontWeight = FontWeights.SemiBold,
            Background = ResolveBrush("ButtonPrimaryBgBrush", pal.Primary),
            Foreground = ResolveBrush("TextInverseBrush", Colors.White),
            BorderThickness = new Thickness(0),
            Margin = new Thickness(0, 0, 0, 10),
            Cursor = Cursors.Hand
        };
        sp.Children.Add(btnLupita);

        sp.Children.Add(new TextBlock { Text = "Código Producto (Código libre sugerido):", FontWeight = FontWeights.SemiBold });
        var txtCodigo = new TextBox
        {
            Text = _dbfService.GetNextFreeGeneCode(GetDbfPath()),
            FontWeight = FontWeights.Bold,
            Padding = new Thickness(6, 4, 6, 4),
            Margin = new Thickness(0, 2, 0, 8)
        };
        sp.Children.Add(txtCodigo);

        sp.Children.Add(new TextBlock { Text = "Tipo de Venta:", FontWeight = FontWeights.SemiBold });
        var rbPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 8) };
        var rbKg = new RadioButton { Content = "Por Kg (Granel)", IsChecked = true, Margin = new Thickness(0, 0, 18, 0), FontWeight = FontWeights.SemiBold };
        var rbUnidad = new RadioButton { Content = "Por Unidad (c/u)", Margin = new Thickness(0, 0, 18, 0), FontWeight = FontWeights.SemiBold };
        var rbDocena = new RadioButton { Content = "Por Docena", FontWeight = FontWeights.SemiBold };
        rbPanel.Children.Add(rbKg);
        rbPanel.Children.Add(rbUnidad);
        rbPanel.Children.Add(rbDocena);
        sp.Children.Add(rbPanel);

        sp.Children.Add(new TextBlock { Text = "Nombre para el Botón:", FontWeight = FontWeights.SemiBold });
        var txtNombre = new TextBox { FontWeight = FontWeights.Bold, Padding = new Thickness(6, 4, 6, 4), Margin = new Thickness(0, 2, 0, 8) };
        sp.Children.Add(txtNombre);

        sp.Children.Add(new TextBlock { Text = "Precio de Compra / Costo (Gs):", FontWeight = FontWeights.SemiBold });
        var txtCosto = new TextBox { FontWeight = FontWeights.Bold, Padding = new Thickness(6, 4, 6, 4), Margin = new Thickness(0, 2, 0, 8) };
        sp.Children.Add(txtCosto);

        sp.Children.Add(new TextBlock { Text = "Precio Base de Venta (Gs) [Use 1 para Monto Libre]:", FontWeight = FontWeights.SemiBold });
        var txtPrecio = new TextBox { FontWeight = FontWeights.Bold, Padding = new Thickness(6, 4, 6, 4), Margin = new Thickness(0, 2, 0, 8) };
        sp.Children.Add(txtPrecio);

        var chkActualizar = new CheckBox { Content = "Actualizar precio automáticamente (ON/OFF)", IsChecked = true, Margin = new Thickness(0, 2, 0, 6) };
        var chkSyncSql = new CheckBox { Content = "Sincronizar con SQL al guardar (ON/OFF)", IsChecked = true, FontWeight = FontWeights.SemiBold, Foreground = ResolveBrush("PrimaryBrush", pal.Primary), Margin = new Thickness(0, 0, 0, 10) };
        sp.Children.Add(chkActualizar);
        sp.Children.Add(chkSyncSql);

        string selectedIconPath = "";
        var iconRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        var btnPickIcon = new Button { Content = "🖼 Seleccionar Icono...", Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 0, 10, 0) };
        var lblIconName = new TextBlock { Text = "(Ninguna)", VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.Gray };
        iconRow.Children.Add(btnPickIcon);
        iconRow.Children.Add(lblIconName);
        sp.Children.Add(iconRow);

        btnPickIcon.Click += (_, _) =>
        {
            var ofd = new OpenFileDialog
            {
                Title = "Seleccionar Imagen de Producto",
                Filter = "Imágenes de Producto (*.png;*.jpg;*.ico;*.bmp)|*.png;*.jpg;*.ico;*.bmp"
            };
            if (ofd.ShowDialog(dlg) == true && !string.IsNullOrWhiteSpace(ofd.FileName))
            {
                selectedIconPath = CopyIconToGenesFolder(ofd.FileName, genId, txtNombre.Text);
                lblIconName.Text = Path.GetFileName(selectedIconPath);
            }
        };

        btnLupita.Click += (_, _) =>
        {
            _windowManager.OpenProductsModule(0, async code =>
            {
                var prod = await _productRepository.GetByCodeAsync(code, 1);
                if (prod != null)
                {
                    txtCodigo.Text = prod.Codigo;
                    txtNombre.Text = prod.Descripcion;
                    txtCosto.Text = prod.Costo1.ToString("0", CultureInfo.InvariantCulture);
                    txtPrecio.Text = prod.Precio1.ToString("0", CultureInfo.InvariantCulture);
                    rbKg.IsChecked = prod.Fraccionable == 1;
                    rbUnidad.IsChecked = prod.Fraccionable != 1;
                }
            });
        };

        var btnGuardar = new Button
        {
            Content = "GUARDAR PRODUCTO EN GEN",
            Height = 46,
            FontWeight = FontWeights.ExtraBold,
            FontSize = 14,
            Background = new SolidColorBrush(pal.Primary),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand
        };
        sp.Children.Add(btnGuardar);

        btnGuardar.Click += async (_, _) =>
        {
            string nombre = txtNombre.Text.Trim();
            string codigo = txtCodigo.Text.Trim();
            decimal.TryParse(new string(txtCosto.Text.Where(char.IsDigit).ToArray()), out decimal costo);
            decimal.TryParse(new string(txtPrecio.Text.Where(char.IsDigit).ToArray()), out decimal precio);

            if (string.IsNullOrWhiteSpace(nombre) || string.IsNullOrWhiteSpace(codigo))
            {
                MessageBox.Show(dlg, "Por favor ingrese el código y el nombre del producto.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string tipo = rbKg.IsChecked == true ? "KG" : (rbDocena.IsChecked == true ? "DOCENA" : "UNIDAD");
            bool actualizar = chkActualizar.IsChecked == true;
            if (precio == 1m)
            {
                tipo = "KG";
                actualizar = false;
                if (costo <= 1m) costo = 8000m;
            }
            else if (costo <= 0m && precio > 0m)
            {
                costo = Math.Round(precio * 0.80m, 0);
            }

            if (chkSyncSql.IsChecked == true && precio != 1m)
            {
                if (costo <= 0m || precio <= 0m || precio <= costo)
                {
                    MessageBox.Show(dlg, "El Precio Base de venta debe ser mayor al Precio de Compra (Costo).", "Sincronizar SQL", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            var item = new GenesButtonItem
            {
                GenId = genId,
                Codigo = codigo,
                Nombre = nombre,
                Tipo = tipo,
                Costo = costo,
                Precio = precio,
                Actualizar = actualizar,
                SyncSql = chkSyncSql.IsChecked == true,
                Icono = selectedIconPath
            };

            _dbfService.SaveGenesButton(GetDbfPath(), item);
            if (item.SyncSql)
            {
                await SyncGeneProductToMySqlAsync(item);
            }

            dlg.DialogResult = true;
            dlg.Close();
            SwitchGen(genId);
        };

        dlg.Content = sp;
        dlg.Loaded += (_, _) => txtNombre.Focus();
        dlg.ShowDialog();
    }

    // =====================================================================
    // DIALOG 3: EDITAR PRODUCTO (Editar / Alt+A..F)
    // =====================================================================
    private void OpenEditarDialog(int genId, GenesButtonItem? initialProduct)
    {
        var genProducts = _dbfService.LoadAllGenesProducts(GetDbfPath())
            .Where(x => x.GenId == genId)
            .OrderBy(x => x.Orden)
            .ToList();

        if (genProducts.Count == 0)
        {
            MessageBox.Show(this, "No hay productos en este Gen para editar.", "Editar", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var pal = GetPalette(genId);
        var dlg = new Window
        {
            Title = $"Editar Producto - Gen {genId} ({pal.Name})",
            Width = 480,
            Height = 580,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            ResizeMode = ResizeMode.NoResize,
            Background = ResolveBrush("CardBgBrush", Colors.White),
            Foreground = ResolveBrush("TextPrimaryBrush", Color.FromRgb(31, 28, 26)),
            FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI")
        };

        var sp = new StackPanel { Margin = new Thickness(20, 14, 20, 14) };

        sp.Children.Add(new TextBlock { Text = "Seleccionar Producto:", FontWeight = FontWeights.SemiBold });
        var cmb = new ComboBox
        {
            ItemsSource = genProducts,
            DisplayMemberPath = "Nombre",
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 2, 0, 8),
            Padding = new Thickness(6, 4, 6, 4)
        };
        sp.Children.Add(cmb);

        var lblCodigo = new TextBlock { FontWeight = FontWeights.Bold, Foreground = ResolveBrush("PrimaryBrush", pal.Primary), Margin = new Thickness(0, 0, 0, 8) };
        sp.Children.Add(lblCodigo);

        var rbPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 8) };
        var rbKg = new RadioButton { Content = "Por Kg (Granel)", Margin = new Thickness(0, 0, 16, 0), FontWeight = FontWeights.SemiBold };
        var rbUnidad = new RadioButton { Content = "Por Unidad (c/u)", Margin = new Thickness(0, 0, 16, 0), FontWeight = FontWeights.SemiBold };
        var rbDocena = new RadioButton { Content = "Por Docena", FontWeight = FontWeights.SemiBold };
        rbPanel.Children.Add(rbKg);
        rbPanel.Children.Add(rbUnidad);
        rbPanel.Children.Add(rbDocena);
        sp.Children.Add(rbPanel);

        sp.Children.Add(new TextBlock { Text = "Nombre:", FontWeight = FontWeights.SemiBold });
        var txtNombre = new TextBox { FontWeight = FontWeights.Bold, Padding = new Thickness(6, 4, 6, 4), Margin = new Thickness(0, 2, 0, 8) };
        sp.Children.Add(txtNombre);

        sp.Children.Add(new TextBlock { Text = "Precio de Compra / Costo (Gs):", FontWeight = FontWeights.SemiBold });
        var txtCosto = new TextBox { FontWeight = FontWeights.Bold, Padding = new Thickness(6, 4, 6, 4), Margin = new Thickness(0, 2, 0, 8) };
        sp.Children.Add(txtCosto);

        sp.Children.Add(new TextBlock { Text = "Precio Base de Venta (Gs):", FontWeight = FontWeights.SemiBold });
        var txtPrecio = new TextBox { FontWeight = FontWeights.Bold, Padding = new Thickness(6, 4, 6, 4), Margin = new Thickness(0, 2, 0, 8) };
        sp.Children.Add(txtPrecio);

        var chkActualizar = new CheckBox { Content = "Actualizar precio automáticamente (ON/OFF)", Margin = new Thickness(0, 2, 0, 6) };
        var chkSyncSql = new CheckBox { Content = "Sincronizar con SQL al guardar (ON/OFF)", IsChecked = true, FontWeight = FontWeights.SemiBold, Foreground = ResolveBrush("PrimaryBrush", pal.Primary), Margin = new Thickness(0, 0, 0, 10) };
        sp.Children.Add(chkActualizar);
        sp.Children.Add(chkSyncSql);

        string currentIcon = "";
        var iconRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        var btnPickIcon = new Button { Content = "🖼 Cambiar Icono...", Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 0, 10, 0) };
        var lblIconName = new TextBlock { Text = "(Ninguna)", VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.Gray };
        iconRow.Children.Add(btnPickIcon);
        iconRow.Children.Add(lblIconName);
        sp.Children.Add(iconRow);

        btnPickIcon.Click += (_, _) =>
        {
            var ofd = new OpenFileDialog
            {
                Title = "Cambiar Imagen de Producto",
                Filter = "Imágenes de Producto (*.png;*.jpg;*.ico;*.bmp)|*.png;*.jpg;*.ico;*.bmp"
            };
            if (ofd.ShowDialog(dlg) == true && !string.IsNullOrWhiteSpace(ofd.FileName))
            {
                currentIcon = CopyIconToGenesFolder(ofd.FileName, genId, txtNombre.Text);
                lblIconName.Text = Path.GetFileName(currentIcon);
            }
        };

        cmb.SelectionChanged += (_, _) =>
        {
            if (cmb.SelectedItem is GenesButtonItem sel)
            {
                lblCodigo.Text = $"Código Actual: {sel.Codigo}";
                txtNombre.Text = sel.Nombre;
                decimal c = sel.Costo > 0 ? sel.Costo : (sel.Precio > 1 ? Math.Round(sel.Precio * 0.80m, 0) : 8000m);
                txtCosto.Text = c.ToString("0", CultureInfo.InvariantCulture);
                txtPrecio.Text = sel.Precio.ToString("0", CultureInfo.InvariantCulture);
                chkActualizar.IsChecked = sel.Actualizar;
                chkSyncSql.IsChecked = sel.SyncSql;
                rbKg.IsChecked = sel.Tipo == "KG";
                rbDocena.IsChecked = sel.Tipo == "DOCENA";
                rbUnidad.IsChecked = sel.Tipo != "KG" && sel.Tipo != "DOCENA";
                currentIcon = sel.Icono;
                lblIconName.Text = string.IsNullOrWhiteSpace(sel.Icono) ? "(Ninguna)" : Path.GetFileName(sel.Icono);
            }
        };

        int initIdx = initialProduct != null ? genProducts.FindIndex(x => x.Id == initialProduct.Id) : 0;
        cmb.SelectedIndex = Math.Max(0, initIdx);

        var btnGuardar = new Button
        {
            Content = "GUARDAR CAMBIOS",
            Height = 46,
            FontWeight = FontWeights.ExtraBold,
            FontSize = 14,
            Background = new SolidColorBrush(pal.Primary),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand
        };
        sp.Children.Add(btnGuardar);

        btnGuardar.Click += async (_, _) =>
        {
            if (cmb.SelectedItem is not GenesButtonItem orig) return;
            string nomNuevo = txtNombre.Text.Trim();
            decimal.TryParse(new string(txtCosto.Text.Where(char.IsDigit).ToArray()), out decimal costo);
            decimal.TryParse(new string(txtPrecio.Text.Where(char.IsDigit).ToArray()), out decimal precio);

            if (string.IsNullOrWhiteSpace(nomNuevo))
            {
                MessageBox.Show(dlg, "El nombre no puede estar vacío.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string tipo = rbKg.IsChecked == true ? "KG" : (rbDocena.IsChecked == true ? "DOCENA" : "UNIDAD");
            bool actualizar = chkActualizar.IsChecked == true;
            if (precio == 1m)
            {
                tipo = "KG";
                actualizar = false;
                if (costo <= 1m) costo = 8000m;
            }

            if (chkSyncSql.IsChecked == true && precio != 1m)
            {
                if (costo <= 0m || precio <= 0m || precio <= costo)
                {
                    MessageBox.Show(dlg, "El Precio Base de venta debe ser mayor al Precio de Compra (Costo).", "Sincronizar SQL", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            orig.Nombre = nomNuevo;
            orig.Costo = costo;
            orig.Precio = precio;
            orig.Tipo = tipo;
            orig.Actualizar = actualizar;
            orig.SyncSql = chkSyncSql.IsChecked == true;
            orig.Icono = currentIcon;

            _dbfService.SaveGenesButton(GetDbfPath(), orig);
            if (orig.SyncSql)
            {
                await SyncGeneProductToMySqlAsync(orig);
            }

            dlg.DialogResult = true;
            dlg.Close();
            SwitchGen(genId);
        };

        dlg.Content = sp;
        dlg.ShowDialog();
    }

    // =====================================================================
    // DIALOG 4: REORDENAR PRODUCTOS (GenesReordenarForm)
    // =====================================================================
    private void OpenReordenarDialog(int genId)
    {
        var items = _dbfService.LoadAllGenesProducts(GetDbfPath())
            .Where(x => x.GenId == genId)
            .OrderBy(x => x.Orden)
            .ToList();

        if (items.Count == 0) return;

        var pal = GetPalette(genId);
        var dlg = new Window
        {
            Title = $"Reordenar - Gen {genId} ({pal.Name})",
            Width = 450,
            Height = 520,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            ResizeMode = ResizeMode.NoResize,
            Background = ResolveBrush("CardBgBrush", Colors.White),
            Foreground = ResolveBrush("TextPrimaryBrush", Color.FromRgb(31, 28, 26)),
            FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI")
        };

        var grid = new Grid { Margin = new Thickness(18) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var lbl = new TextBlock { Text = "Seleccione un producto y use Subir/Bajar:", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) };
        Grid.SetRow(lbl, 0);
        Grid.SetColumnSpan(lbl, 2);
        grid.Children.Add(lbl);

        var lst = new ListBox { FontSize = 13, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 10, 12) };
        foreach (var it in items) lst.Items.Add(it.Nombre);
        lst.SelectedIndex = 0;
        Grid.SetRow(lst, 1);
        Grid.SetColumn(lst, 0);
        grid.Children.Add(lst);

        var sidePanel = new StackPanel { VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 30, 0, 0) };
        var btnUp = new Button { Content = "Subir ▲", Width = 86, Height = 40, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 10) };
        var btnDown = new Button { Content = "Bajar ▼", Width = 86, Height = 40, FontWeight = FontWeights.Bold };
        sidePanel.Children.Add(btnUp);
        sidePanel.Children.Add(btnDown);
        Grid.SetRow(sidePanel, 1);
        Grid.SetColumn(sidePanel, 1);
        grid.Children.Add(sidePanel);

        void MoveItem(int dir)
        {
            int idx = lst.SelectedIndex;
            int target = idx + dir;
            if (idx < 0 || target < 0 || target >= items.Count) return;

            (items[idx], items[target]) = (items[target], items[idx]);
            object txt = lst.Items[idx];
            lst.Items.RemoveAt(idx);
            lst.Items.Insert(target, txt);
            lst.SelectedIndex = target;
        }

        btnUp.Click += (_, _) => MoveItem(-1);
        btnDown.Click += (_, _) => MoveItem(1);

        var btnSave = new Button
        {
            Content = "Guardar Nuevo Orden",
            Height = 44,
            FontWeight = FontWeights.ExtraBold,
            FontSize = 14,
            Background = new SolidColorBrush(pal.Primary),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0)
        };
        Grid.SetRow(btnSave, 2);
        Grid.SetColumnSpan(btnSave, 2);
        grid.Children.Add(btnSave);

        btnSave.Click += (_, _) =>
        {
            _dbfService.ReorderGenesProducts(GetDbfPath(), genId, items.Select(x => x.Id).ToList());
            dlg.DialogResult = true;
            dlg.Close();
            SwitchGen(genId);
        };

        dlg.Content = grid;
        dlg.ShowDialog();
    }

    private string CopyIconToGenesFolder(string sourcePath, int genId, string prodName)
    {
        try
        {
            string relDir = Path.Combine("images", "genes", $"gen{Math.Clamp(genId, 1, 4)}");
            string fullDir = Path.Combine(GetBaseDir(), relDir);
            Directory.CreateDirectory(fullDir);

            string ext = Path.GetExtension(sourcePath).ToLowerInvariant();
            string safeName = new string((prodName ?? "").Trim().Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray()).Trim('_');
            if (string.IsNullOrWhiteSpace(safeName))
                safeName = Path.GetFileNameWithoutExtension(sourcePath);

            string destFull = Path.Combine(fullDir, safeName + ext);
            if (!string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(destFull), StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(sourcePath, destFull, overwrite: true);
            }
            return Path.Combine(relDir, safeName + ext);
        }
        catch
        {
            return sourcePath;
        }
    }

    private async Task SyncGeneProductToMySqlAsync(GenesButtonItem item)
    {
        try
        {
            decimal syncPrice = item.Precio == 1m
                ? (item.Costo > 1m ? Math.Round(item.Costo / 0.80m, 0) : 10000m)
                : item.Precio;
            decimal syncCost = item.Costo > 1m ? item.Costo : Math.Round(syncPrice * 0.80m, 0);

            var existing = await _productRepository.GetByCodeAsync(item.Codigo, 1);
            if (existing != null && existing.IdProducto > 0)
            {
                existing.Descripcion = item.Nombre.ToUpperInvariant();
                existing.Costo1 = syncCost;
                existing.Costo2 = syncCost;
                existing.CostoMedio = syncCost;
                existing.Precio1 = syncPrice;
                existing.Precio2 = syncPrice;
                existing.Precio3 = syncPrice;
                existing.Precio4 = syncPrice;
                existing.Precio5 = syncPrice;
                existing.Fraccionable = item.Tipo == "KG" || item.Precio == 1m ? 1 : 0;
                await _productRepository.SaveProductAsync(existing, "M");
            }
            else
            {
                var prod = new ProductEntity
                {
                    Codigo = item.Codigo,
                    Descripcion = item.Nombre.ToUpperInvariant(),
                    Costo1 = syncCost,
                    Costo2 = syncCost,
                    CostoMedio = syncCost,
                    Precio1 = syncPrice,
                    Precio2 = syncPrice,
                    Precio3 = syncPrice,
                    Precio4 = syncPrice,
                    Precio5 = syncPrice,
                    Iva = 1,
                    Impuesto = 10,
                    Estado = 1,
                    Fraccionable = item.Tipo == "KG" || item.Precio == 1m ? 1 : 0,
                    NoControlaStock = 1,
                    VendeSinStock = 1
                };
                await _productRepository.SaveProductAsync(prod, "A");
            }
        }
        catch
        {
            // Non-blocking SQL sync
        }
    }
}
