using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FirewallControl.App.Services;
using FirewallControl.Core.Entities;
using FirewallControl.Core.Interfaces;
using Microsoft.Win32;

namespace FirewallControl.App.Views;

public partial class ProductImageBrowserWindow : Window
{
    private readonly IProductRepository _productRepository;
    private readonly IConfigService? _configService;
    private List<ProductEntity> _allProducts = new();

    public ProductImageBrowserWindow(IProductRepository productRepository, IConfigService? configService = null)
    {
        InitializeComponent();
        _productRepository = productRepository;
        _configService = configService;

        TxtFolderPath.Text = $"Carpeta activa: {ProductPhotoHelper.GetPhotosDirectory(_configService)}";
        Loaded += async (_, _) => await LoadProductsAsync();
    }

    private async Task LoadProductsAsync()
    {
        try
        {
            _allProducts = await _productRepository.SearchProductsAdvancedAsync(new ProductSearchFilter
            {
                SearchText = TxtSearch.Text,
                Limit = 400
            });
            ApplyLocalPhotoFilter();
        }
        catch (Exception ex)
        {
            TxtStatus.Text = $"Error al cargar productos: {ex.Message}";
        }
    }

    private void ApplyLocalPhotoFilter()
    {
        IEnumerable<ProductEntity> query = _allProducts;
        if (ChkOnlyWithPhoto.IsChecked == true)
            query = query.Where(p => !string.IsNullOrWhiteSpace(p.Imagen));
        else if (ChkOnlyWithoutPhoto.IsChecked == true)
            query = query.Where(p => string.IsNullOrWhiteSpace(p.Imagen));

        var list = query.ToList();
        GridProducts.ItemsSource = list;
        if (list.Count > 0) GridProducts.SelectedIndex = 0;
        TxtStatus.Text = $"✓ {list.Count} productos mostrados.";
    }

    private void FilterChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        if (sender == ChkOnlyWithPhoto && ChkOnlyWithPhoto.IsChecked == true)
            ChkOnlyWithoutPhoto.IsChecked = false;
        else if (sender == ChkOnlyWithoutPhoto && ChkOnlyWithoutPhoto.IsChecked == true)
            ChkOnlyWithPhoto.IsChecked = false;
        ApplyLocalPhotoFilter();
    }

    private async void BtnSearch_Click(object sender, RoutedEventArgs e) => await LoadProductsAsync();

    private async void TxtSearch_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter || e.Key == Key.Return)
        {
            e.Handled = true;
            await LoadProductsAsync();
        }
    }

    private void GridProducts_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GridProducts.SelectedItem is not ProductEntity p)
        {
            TxtSelectedTitle.Text = "Seleccione un producto";
            TxtSelectedFile.Text = "Sin imagen asignada";
            ImgPreview.Source = null;
            TxtObs.Text = string.Empty;
            return;
        }

        TxtSelectedTitle.Text = $"[{p.Codigo}] {p.Descripcion}";
        TxtObs.Text = p.Observacion;
        string? path = ProductPhotoHelper.ResolvePhotoFilePath(p.Imagen, _configService);
        ImgPreview.Source = ProductPhotoHelper.LoadBitmapSafe(path);
        TxtSelectedFile.Text = string.IsNullOrWhiteSpace(p.Imagen)
            ? "Sin imagen asignada"
            : path != null ? $"Archivo: {p.Imagen} (encontrado)" : $"Archivo: {p.Imagen} (no encontrado en carpeta Fotos)";
    }

    private async void BtnAssignPhoto_Click(object sender, RoutedEventArgs e)
    {
        if (GridProducts.SelectedItem is not ProductEntity p)
            return;

        var ofd = new OpenFileDialog
        {
            Title = "Seleccionar imagen de producto (abm_fotoproducto)",
            Filter = "Imágenes (*.jpg;*.jpeg;*.png;*.bmp;*.gif)|*.jpg;*.jpeg;*.png;*.bmp;*.gif"
        };
        if (ofd.ShowDialog() != true)
            return;

        try
        {
            string fileName = ProductPhotoHelper.CopyPhotoToRepository(ofd.FileName, _configService);
            p.Imagen = fileName;
            await _productRepository.SaveProductFullAsync(p, "M", 1, "ADMINISTRA");
            GridProducts.Items.Refresh();
            GridProducts_SelectionChanged(this, null!);
            TxtStatus.Text = $"✓ Foto '{fileName}' guardada para [{p.Codigo}].";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al asignar imagen:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnRemovePhoto_Click(object sender, RoutedEventArgs e)
    {
        if (GridProducts.SelectedItem is not ProductEntity p)
            return;

        p.Imagen = string.Empty;
        await _productRepository.SaveProductFullAsync(p, "M", 1, "ADMINISTRA");
        GridProducts.Items.Refresh();
        GridProducts_SelectionChanged(this, null!);
        TxtStatus.Text = $"✓ Imagen quitada para [{p.Codigo}].";
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
    }
}
