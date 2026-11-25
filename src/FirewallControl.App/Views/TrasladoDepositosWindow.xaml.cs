using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FirewallControl.Core.Entities;
using FirewallControl.Core.Interfaces;

namespace FirewallControl.App.Views;

public partial class TrasladoDepositosWindow : Window
{
    private readonly IProductRepository _productRepository;
    private readonly string _usuario;
    private ProductEntity? _selectedProduct;

    public TrasladoDepositosWindow(IProductRepository productRepository, string usuario = "ADMINISTRA")
    {
        InitializeComponent();
        _productRepository = productRepository;
        _usuario = string.IsNullOrWhiteSpace(usuario) ? "ADMINISTRA" : usuario;

        DpDesde.SelectedDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        DpHasta.SelectedDate = DateTime.Today;
        Loaded += async (_, _) => await LoadInitialAsync();
    }

    private async Task LoadInitialAsync()
    {
        try
        {
            var deps = await _productRepository.GetDepositsAsync();
            CmbOrigen.ItemsSource = deps;
            CmbDestino.ItemsSource = deps.ToList();
            if (deps.Count > 0) CmbOrigen.SelectedIndex = 0;
            if (deps.Count > 1) CmbDestino.SelectedIndex = 1;
            else if (deps.Count > 0) CmbDestino.SelectedIndex = 0;

            await RefreshHistoryAsync();
        }
        catch (Exception ex)
        {
            TxtStatus.Text = $"Error al cargar depósitos: {ex.Message}";
        }
    }

    private async Task RefreshHistoryAsync()
    {
        var desde = DpDesde.SelectedDate ?? DateTime.Today.AddDays(-30);
        var hasta = DpHasta.SelectedDate ?? DateTime.Today;
        var rows = await _productRepository.GetDepositTransfersAsync(desde, hasta);
        GridTransfers.ItemsSource = rows;
        TxtStatus.Text = $"✓ {rows.Count} traslados encontrados.";
    }

    private async void BtnPickProduct_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            int depId = CmbOrigen.SelectedValue is int id && id > 0 ? id : 1;
            var products = await _productRepository.SearchProductsAsync("", depId, 300);
            var dlg = new CatalogQuickPickDialog { Owner = this };
            dlg.Configure("Seleccionar Producto para Traslado", products.Select(p =>
                new QuickPickOption(p.IdProducto, $"[{p.Codigo}] {p.Descripcion}", $"Stock: {p.StockActual:N2}")));

            if (dlg.ShowDialog() == true && dlg.SelectedOption != null)
            {
                _selectedProduct = products.FirstOrDefault(p => p.IdProducto == dlg.SelectedOption.Id);
                if (_selectedProduct != null)
                {
                    TxtProductoDisplay.Text = $"[{_selectedProduct.Codigo}] {_selectedProduct.Descripcion}";
                    await UpdateSourceStockAsync();
                    TxtCantidad.Focus();
                    TxtCantidad.SelectAll();
                }
            }
        }
        catch (Exception ex)
        {
            TxtStatus.Text = $"Error al seleccionar producto: {ex.Message}";
        }
    }

    private async void SelectionChanged_UpdateStock(object sender, SelectionChangedEventArgs e)
        => await UpdateSourceStockAsync();

    private async Task UpdateSourceStockAsync()
    {
        if (_selectedProduct == null || CmbOrigen.SelectedValue is not int depId || depId <= 0)
        {
            TxtStockOrigen.Text = "0";
            return;
        }
        decimal stock = await _productRepository.GetDepositStockAsync(_selectedProduct.IdProducto, depId);
        TxtStockOrigen.Text = stock.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private async void BtnSaveTransfer_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProduct == null)
        {
            MessageBox.Show("Seleccione primero un producto.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (CmbOrigen.SelectedValue is not int ori || CmbDestino.SelectedValue is not int dest)
        {
            MessageBox.Show("Seleccione depósito origen y destino.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!decimal.TryParse((TxtCantidad.Text ?? "0").Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal cant) || cant <= 0m)
        {
            MessageBox.Show("Ingrese una cantidad mayor a cero.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var transfer = new ProductTransferEntity
            {
                IdDepositoOrigen = ori,
                IdDepositoDestino = dest,
                IdProducto = _selectedProduct.IdProducto,
                Codigo = _selectedProduct.Codigo,
                DesProducto = _selectedProduct.Descripcion,
                Cantidad = cant,
                Autorizado = (TxtAutorizado.Text ?? string.Empty).Trim(),
                Observaciones = (TxtObservaciones.Text ?? string.Empty).Trim(),
                Fecha = DateTime.Today,
                Hora = DateTime.Now.ToString("HH:mm:ss"),
                CidUsuario = _usuario
            };

            int id = await _productRepository.SaveDepositTransferTransactionAsync(transfer);
            await UpdateSourceStockAsync();
            await RefreshHistoryAsync();
            TxtStatus.Text = $"✓ Traslado #{id} registrado ({_selectedProduct.Codigo} x {cant:N2}).";
            TxtObservaciones.Clear();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al registrar traslado:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnRefresh_Click(object sender, RoutedEventArgs e) => await RefreshHistoryAsync();
    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
        else if (e.Key == Key.F3)
        {
            e.Handled = true;
            BtnPickProduct_Click(sender, new RoutedEventArgs());
        }
    }
}
