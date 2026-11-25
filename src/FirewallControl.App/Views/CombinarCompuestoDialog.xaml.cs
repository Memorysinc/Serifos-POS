using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using FirewallControl.App.Services;
using FirewallControl.Core.Entities;
using FirewallControl.Core.Interfaces;

namespace FirewallControl.App.Views;

/// <summary>
/// Replicates combinar.scx (componentes de producto compuesto en vw_compuesto / compuesto).
/// </summary>
public partial class CombinarCompuestoDialog : Window
{
    private readonly IProductRepository _productRepository;
    private readonly IWindowManagerService _windowManager;
    private readonly int _idProductoCompuesto;
    private ProductEntity? _candidateComponent;

    public CombinarCompuestoDialog(
        IProductRepository productRepository,
        IWindowManagerService windowManager,
        int idProductoCompuesto,
        string descripcionProducto)
    {
        InitializeComponent();
        _productRepository = productRepository;
        _windowManager = windowManager;
        _idProductoCompuesto = idProductoCompuesto;
        LblProductoPrincipal.Text = $"{descripcionProducto} (ID #{idProductoCompuesto})";
        Loaded += async (_, _) => await RefreshComponentsAsync();
    }

    private async Task RefreshComponentsAsync()
    {
        try
        {
            var rows = await _productRepository.GetCompoundComponentsAsync(_idProductoCompuesto);
            GridCompuestos.ItemsSource = rows;
            LblInfo.Text = $"{rows.Count} componente(s) en la receta.";
        }
        catch (Exception ex)
        {
            LblInfo.Text = $"Error al cargar receta: {ex.Message}";
        }
    }

    private async Task LookupComponentAsync()
    {
        string cod = TxtCompCodigo.Text.Trim();
        if (string.IsNullOrWhiteSpace(cod))
            return;

        var prod = await _productRepository.GetByCodeIncludingInactiveAsync(cod, 1);
        if (prod == null)
        {
            _candidateComponent = null;
            TxtCompDesc.Text = "NO ENCONTRADO";
            return;
        }

        _candidateComponent = prod;
        TxtCompCodigo.Text = prod.Codigo;
        TxtCompDesc.Text = prod.Descripcion;
        TxtCompCantidad.Focus();
        TxtCompCantidad.SelectAll();
    }

    private async void TxtCompCodigo_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            await LookupComponentAsync();
        }
    }

    private async void BtnBuscarComp_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var products = await _productRepository.SearchProductsAsync("", 1, 350);
            var dlg = new CatalogQuickPickDialog { Owner = this };
            dlg.Configure("Seleccionar Insumo / Componente", products
                .Where(p => p.IdProducto != _idProductoCompuesto)
                .Select(p => new QuickPickOption(p.IdProducto, $"[{p.Codigo}] {p.Descripcion}", $"Stock: {p.StockActual:N2}")));

            if (dlg.ShowDialog() == true && dlg.SelectedOption != null)
            {
                var picked = products.FirstOrDefault(p => p.IdProducto == dlg.SelectedOption.Id);
                if (picked != null)
                {
                    TxtCompCodigo.Text = picked.Codigo;
                    await LookupComponentAsync();
                }
            }
        }
        catch (Exception ex)
        {
            LblInfo.Text = $"Error al buscar insumos: {ex.Message}";
        }
    }

    private async void BtnAddComp_Click(object sender, RoutedEventArgs e)
    {
        if (_candidateComponent == null)
            await LookupComponentAsync();

        if (_candidateComponent == null)
        {
            MessageBox.Show(this, "Seleccione un insumo válido.", "Producto Compuesto", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_candidateComponent.IdProducto == _idProductoCompuesto)
        {
            MessageBox.Show(this, "Un producto compuesto no puede contenerse a sí mismo.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!PosNumericParser.TryParseQuantity(TxtCompCantidad.Text, out decimal qty) || qty <= 0m)
        {
            MessageBox.Show(this, "Ingrese una cantidad mayor a cero.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            await _productRepository.SaveCompoundComponentAsync(
                _idProductoCompuesto,
                _candidateComponent.IdProducto,
                _candidateComponent.Codigo,
                qty);
            _candidateComponent = null;
            TxtCompCodigo.Clear();
            TxtCompDesc.Clear();
            TxtCompCantidad.Text = "1";
            await RefreshComponentsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Error al agregar componente:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnDeleteComp_Click(object sender, RoutedEventArgs e)
    {
        if (GridCompuestos.SelectedItem is not CompoundProductComponent item)
            return;

        try
        {
            await _productRepository.DeleteCompoundComponentAsync(item.IdCompuesto);
            await RefreshComponentsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Error al eliminar componente:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
