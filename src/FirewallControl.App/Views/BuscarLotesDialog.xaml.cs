using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using FirewallControl.Core.Entities;
using FirewallControl.Core.Interfaces;

namespace FirewallControl.App.Views;

/// <summary>
/// Replicates buscar_lotes_productos.scx / VtAttachBuscarLotes.
/// </summary>
public partial class BuscarLotesDialog : Window
{
    private readonly IProductRepository _productRepository;
    private readonly int _idProducto;
    private readonly int _idDeposito;

    public ProductLotEntity? SelectedLot { get; private set; }

    public BuscarLotesDialog(IProductRepository productRepository, int idProducto, int idDeposito = 1)
    {
        InitializeComponent();
        _productRepository = productRepository;
        _idProducto = idProducto;
        _idDeposito = idDeposito > 0 ? idDeposito : 1;

        Loaded += async (_, _) => await LoadLotsAsync();
    }

    private async Task LoadLotsAsync()
    {
        try
        {
            var lots = await _productRepository.GetProductLotsAsync(_idProducto, _idDeposito);
            GridLotes.ItemsSource = lots;
            if (lots.Count > 0) GridLotes.SelectedIndex = 0;
            LblInfo.Text = $"{lots.Count} lote(s) encontrado(s) en Depósito #{_idDeposito}.";
            GridLotes.Focus();
        }
        catch (Exception ex)
        {
            LblInfo.Text = $"Error cargando lotes: {ex.Message}";
        }
    }

    private void ConfirmSelection()
    {
        if (GridLotes.SelectedItem is ProductLotEntity lot)
        {
            SelectedLot = lot;
            DialogResult = true;
            Close();
        }
    }

    private void BtnSelect_Click(object sender, RoutedEventArgs e) => ConfirmSelection();
    private void GridLotes_MouseDoubleClick(object sender, MouseButtonEventArgs e) => ConfirmSelection();
    private void GridLotes_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            ConfirmSelection();
        }
    }
}
