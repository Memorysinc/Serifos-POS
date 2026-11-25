using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using FirewallControl.App.Services;
using FirewallControl.Core.Business;
using FirewallControl.Core.Entities;
using FirewallControl.Core.Interfaces;

namespace FirewallControl.App.Views;

public class MassPricePreviewRow
{
    public string Codigo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public decimal Costo1 { get; set; }
    public decimal Precio1Actual { get; set; }
    public decimal Precio1Nuevo { get; set; }
    public decimal Precio2Nuevo { get; set; }
    public decimal Precio3Nuevo { get; set; }
}

public partial class AjustePreciosMasivoWindow : Window
{
    private readonly IProductRepository _productRepository;
    private readonly ISystemRepository? _systemRepository;
    private readonly IPurchaseRepository? _purchaseRepository;
    private readonly IWindowManagerService? _windowManager;

    public AjustePreciosMasivoWindow(
        IProductRepository productRepository,
        ISystemRepository? systemRepository = null,
        IPurchaseRepository? purchaseRepository = null,
        IWindowManagerService? windowManager = null)
    {
        InitializeComponent();
        _productRepository = productRepository;
        _systemRepository = systemRepository;
        _purchaseRepository = purchaseRepository;
        _windowManager = windowManager;

        Loaded += async (_, _) => await InitializeDataAsync();
    }

    private async Task InitializeDataAsync()
    {
        try
        {
            if (_systemRepository != null)
            {
                var company = await _systemRepository.GetCompanyDataAsync();
                TxtU1.Text = (company.Utilidad1 > 0 ? company.Utilidad1 : 80m).ToString("0.##", CultureInfo.InvariantCulture);
                TxtU2.Text = (company.Utilidad2 > 0 ? company.Utilidad2 : 65m).ToString("0.##", CultureInfo.InvariantCulture);
                TxtU3.Text = (company.Utilidad3 > 0 ? company.Utilidad3 : 40m).ToString("0.##", CultureInfo.InvariantCulture);
                TxtU4.Text = (company.Utilidad4 > 0 ? company.Utilidad4 : 25m).ToString("0.##", CultureInfo.InvariantCulture);
                TxtU5.Text = (company.Utilidad5 > 0 ? company.Utilidad5 : 100m).ToString("0.##", CultureInfo.InvariantCulture);
                TxtU6.Text = (company.Utilidad6 > 0 ? company.Utilidad6 : 107m).ToString("0.##", CultureInfo.InvariantCulture);
                TxtU7.Text = (company.Utilidad7 > 0 ? company.Utilidad7 : 120m).ToString("0.##", CultureInfo.InvariantCulture);
            }

            var groups = new List<ProductGroupEntity> { new() { IdGrupoProducto = 0, Descripcion = "<TODOS LOS GRUPOS>" } };
            groups.AddRange(await _productRepository.GetProductGroupsAsync());
            CmbGrupo.ItemsSource = groups;
            CmbGrupo.SelectedIndex = 0;

            var brands = new List<ProductBrandEntity> { new() { IdMarcaProducto = 0, Descripcion = "<TODAS LAS MARCAS>" } };
            brands.AddRange(await _productRepository.GetProductBrandsAsync());
            CmbMarca.ItemsSource = brands;
            CmbMarca.SelectedIndex = 0;

            var suppliers = new List<SupplierEntity> { new() { IdProveedor = 0, Descripcion = "<TODOS LOS PROVEEDORES>" } };
            if (_purchaseRepository != null)
                suppliers.AddRange(await _purchaseRepository.SearchSuppliersAsync("", 300));
            CmbProveedor.ItemsSource = suppliers;
            CmbProveedor.SelectedIndex = 0;

            await BuildPreviewAsync();
        }
        catch (Exception ex)
        {
            TxtStatus.Text = $"Error inicializando ajuste masivo: {ex.Message}";
        }
    }

    private void ModeRadio_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        bool isUtil = RbModoUtilidad.IsChecked == true;
        PanelUtilidades.IsEnabled = isUtil;
        TxtFlatDelta.IsEnabled = !isUtil;
    }

    private int? SelectedId(object? value) => value is int id && id > 0 ? id : null;

    private async Task<List<MassPricePreviewRow>> BuildPreviewAsync()
    {
        var list = await _productRepository.SearchProductsAdvancedAsync(new ProductSearchFilter
        {
            IdGrupo = SelectedId(CmbGrupo.SelectedValue),
            IdMarca = SelectedId(CmbMarca.SelectedValue),
            IdProveedor = SelectedId(CmbProveedor.SelectedValue),
            Limit = 500
        });

        bool useDelta = RbModoDelta.IsChecked == true;
        decimal u1 = ParseDec(TxtU1.Text), u2 = ParseDec(TxtU2.Text), u3 = ParseDec(TxtU3.Text);
        decimal delta = ParseDec(TxtFlatDelta.Text);

        var preview = new List<MassPricePreviewRow>();
        foreach (var p in list)
        {
            if (!useDelta && p.Costo1 <= 0m) continue;

            decimal p1New = useDelta
                ? Math.Round(p.Precio1 * (1m + delta / 100m), 0, MidpointRounding.AwayFromZero)
                : SalesCalculationEngine.CalculateSalePriceFromUtility(p.Costo1, u1);
            decimal p2New = useDelta
                ? Math.Round(p.Precio2 * (1m + delta / 100m), 0, MidpointRounding.AwayFromZero)
                : SalesCalculationEngine.CalculateSalePriceFromUtility(p.Costo1, u2);
            decimal p3New = useDelta
                ? Math.Round(p.Precio3 * (1m + delta / 100m), 0, MidpointRounding.AwayFromZero)
                : SalesCalculationEngine.CalculateSalePriceFromUtility(p.Costo1, u3);

            preview.Add(new MassPricePreviewRow
            {
                Codigo = p.Codigo,
                Descripcion = p.Descripcion,
                Costo1 = p.Costo1,
                Precio1Actual = p.Precio1,
                Precio1Nuevo = p1New,
                Precio2Nuevo = p2New,
                Precio3Nuevo = p3New
            });
        }

        GridPreview.ItemsSource = preview;
        TxtStatus.Text = $"Vista previa: {preview.Count} productos alcanzados (máx. 500 mostrados en grilla).";
        return preview;
    }

    private async void BtnPreview_Click(object sender, RoutedEventArgs e) => await BuildPreviewAsync();

    private async void BtnExecute_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureAdminAuthorized())
            return;

        var preview = await BuildPreviewAsync();
        bool useDelta = RbModoDelta.IsChecked == true;
        string summary = useDelta
            ? $"Variación directa de {ParseDec(TxtFlatDelta.Text):+0.##;-0.##;0}% sobre precios vigentes."
            : $"Recálculo VFP con utilidades U1={ParseDec(TxtU1.Text)}%, U2={ParseDec(TxtU2.Text)}%, U3={ParseDec(TxtU3.Text)}%, U4={ParseDec(TxtU4.Text)}%, U5={ParseDec(TxtU5.Text)}%, U6={ParseDec(TxtU6.Text)}%, U7={ParseDec(TxtU7.Text)}%.";

        if (MessageBox.Show(
                $"¿Confirmar actualización masiva de precios en MySQL?\n\n" +
                $"• Modo: {summary}\n" +
                $"• Alcance mínimo previsualizado: {preview.Count} productos activos.\n\n" +
                $"Esta acción modificará todos los productos activos que coincidan con el filtro.",
                "Confirmación Requerida (Ajuste Masivo ADMIN)",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            string usuario = _windowManager?.CurrentUser.Cidusuario ?? "ADMINISTRA";
            int updated = await _productRepository.UpdateFilteredPricesByUtilityAsync(
                ParseDec(TxtU1.Text), ParseDec(TxtU2.Text), ParseDec(TxtU3.Text),
                ParseDec(TxtU4.Text), ParseDec(TxtU5.Text), ParseDec(TxtU6.Text), ParseDec(TxtU7.Text),
                SelectedId(CmbGrupo.SelectedValue),
                SelectedId(CmbMarca.SelectedValue),
                SelectedId(CmbProveedor.SelectedValue),
                useDelta ? ParseDec(TxtFlatDelta.Text) : null,
                usuario);

            await BuildPreviewAsync();
            TxtStatus.Text = $"✓ Ajuste completado: {updated} productos actualizados correctamente.";
            MessageBox.Show($"Se actualizaron {updated} productos exitosamente.", "Ajuste Masivo Completado", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al ejecutar ajuste masivo:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool EnsureAdminAuthorized()
    {
        if (_windowManager == null)
            return true;

        bool isAdmin = string.Equals(_windowManager.CurrentUserGroup.Cidgrupo?.Trim(), "ADMIN", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(_windowManager.CurrentUser.Cidusuario?.Trim(), "ADMINISTRA", StringComparison.OrdinalIgnoreCase);
        if (isAdmin)
            return true;

        if (_systemRepository == null)
        {
            MessageBox.Show("Solo usuarios del grupo ADMIN pueden ejecutar ajustes masivos de precios.", "Acceso Denegado", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (MessageBox.Show(
                $"El usuario actual '{_windowManager.CurrentUser.Cidusuario}' no pertenece al grupo ADMIN.\n¿Desea autenticarse como Administrador para continuar?",
                "Permiso ADMIN requerido",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return false;
        }

        var loginWin = new LoginWindow(_systemRepository) { Owner = this };
        if (loginWin.ShowDialog() == true && loginWin.AuthenticatedUser != null)
        {
            string grp = loginWin.AuthenticatedGroup?.Cidgrupo?.Trim() ?? string.Empty;
            if (string.Equals(grp, "ADMIN", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(loginWin.AuthenticatedUser.Cidusuario.Trim(), "ADMINISTRA", StringComparison.OrdinalIgnoreCase))
            {
                _windowManager.CurrentUser = loginWin.AuthenticatedUser;
                _windowManager.CurrentUserGroup = loginWin.AuthenticatedGroup ?? _windowManager.CurrentUserGroup;
                return true;
            }
            MessageBox.Show("El usuario autenticado tampoco posee privilegios ADMIN.", "Acceso Denegado", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        return false;
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
        else if (e.Key == Key.F5)
        {
            e.Handled = true;
            BtnPreview_Click(sender, new RoutedEventArgs());
        }
    }

    private static decimal ParseDec(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0m;
        string clean = s.Trim().Replace(" ", "").Replace(",", ".");
        return decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal d) ? d : 0m;
    }
}
