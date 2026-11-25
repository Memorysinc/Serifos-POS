using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using FirewallControl.App.Services;
using FirewallControl.Core.Entities;
using FirewallControl.Core.Interfaces;

namespace FirewallControl.App.Views;

/// <summary>
/// Replicates abm_ajustes.scx / VtAttachAjustes: writes transactional stock adjustment to
/// ajustes + itemsajustes + itemsdepositos.
/// </summary>
public partial class AjusteStockWindow : Window
{
    private readonly IProductRepository _productRepository;
    private readonly IWindowManagerService _windowManager;
    private readonly int _initialDeposito;
    private readonly ObservableCollection<StockAdjustmentItemEntity> _items = new();
    private ProductEntity? _currentProduct;

    public AjusteStockWindow(IProductRepository productRepository, IWindowManagerService windowManager, int initialDeposito = 1)
    {
        InitializeComponent();
        _productRepository = productRepository;
        _windowManager = windowManager;
        _initialDeposito = initialDeposito > 0 ? initialDeposito : 1;

        GridAjustes.ItemsSource = _items;
        DpFecha.SelectedDate = DateTime.Today;
        Loaded += async (_, _) => await InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            TxtNroAjuste.Text = (await _productRepository.GetNextNroAjusteAsync()).ToString();
            var deposits = await _productRepository.GetDepositsAsync();
            CmbDeposito.ItemsSource = deposits;
            CmbDeposito.SelectedValue = _initialDeposito;
            if (CmbDeposito.SelectedIndex < 0 && deposits.Count > 0)
                CmbDeposito.SelectedIndex = 0;

            TxtAutorizado.Text = _windowManager.CurrentUser?.Cidusuario ?? "ADMINISTRA";
            TxtProdCodigo.Focus();
        }
        catch (Exception ex)
        {
            LblStatus.Text = $"Error inicializando ajuste: {ex.Message}";
        }
    }

    private int SelectedDepositoId => CmbDeposito.SelectedValue is int id && id > 0 ? id : _initialDeposito;

    private async Task LookupProductAsync()
    {
        string code = TxtProdCodigo.Text.Trim();
        if (string.IsNullOrWhiteSpace(code))
            return;

        var prod = await _productRepository.GetByCodeIncludingInactiveAsync(code, SelectedDepositoId);
        if (prod == null)
        {
            _currentProduct = null;
            TxtProdDesc.Text = "NO ENCONTRADO";
            TxtStockActual.Text = "0";
            return;
        }

        _currentProduct = prod;
        TxtProdCodigo.Text = prod.Codigo;
        TxtProdDesc.Text = prod.Descripcion;
        TxtStockActual.Text = prod.StockActual.ToString("0.##");
        TxtStockContado.Text = prod.StockActual.ToString("0.##");
        TxtStockContado.Focus();
        TxtStockContado.SelectAll();
    }

    private async void TxtProdCodigo_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            await LookupProductAsync();
        }
        else if (e.Key == Key.F3)
        {
            e.Handled = true;
            BtnLookupProd_Click(sender, new RoutedEventArgs());
        }
    }

    private void BtnLookupProd_Click(object sender, RoutedEventArgs e)
    {
        _windowManager.OpenProductsModule(0, async code =>
        {
            TxtProdCodigo.Text = code;
            await LookupProductAsync();
        });
    }

    private void TxtStockContado_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            BtnAddItem_Click(sender, new RoutedEventArgs());
        }
    }

    private void BtnAddItem_Click(object sender, RoutedEventArgs e)
    {
        if (_currentProduct == null)
        {
            LblStatus.Text = "Busque primero un producto válido.";
            TxtProdCodigo.Focus();
            return;
        }

        if (!PosNumericParser.TryParseQuantity(TxtStockContado.Text, out decimal contado) || contado < 0m)
        {
            LblStatus.Text = "Ingrese una cantidad contada válida (>= 0).";
            return;
        }

        decimal ant = _currentProduct.StockActual;
        decimal diff = contado - ant;
        decimal entrada = diff > 0m ? diff : 0m;
        decimal salida = diff < 0m ? Math.Abs(diff) : 0m;
        decimal precio = _currentProduct.Costo1 > 0m ? _currentProduct.Costo1 : _currentProduct.Precio1;

        _items.Add(new StockAdjustmentItemEntity
        {
            IdProducto = _currentProduct.IdProducto,
            Codigo = _currentProduct.Codigo,
            Descripcion = _currentProduct.Descripcion,
            StockAnterior = ant,
            CantidadContada = contado,
            Entrada = entrada,
            Salida = salida,
            Precio = precio,
            Total = Math.Abs(diff) * precio
        });

        RecalculateTotals();
        _currentProduct = null;
        TxtProdCodigo.Clear();
        TxtProdDesc.Clear();
        TxtStockActual.Text = "0";
        TxtStockContado.Text = "0";
        TxtProdCodigo.Focus();
    }

    private void BtnRemoveItem_Click(object sender, RoutedEventArgs e)
    {
        if (GridAjustes.SelectedItem is StockAdjustmentItemEntity item)
        {
            _items.Remove(item);
            RecalculateTotals();
        }
    }

    private void RecalculateTotals()
    {
        decimal sob = _items.Sum(i => i.Entrada * i.Precio);
        decimal fal = _items.Sum(i => i.Salida * i.Precio);
        LblSobrante.Text = $"{sob:N0} Gs.";
        LblFaltante.Text = $"{fal:N0} Gs.";
        LblTotalDif.Text = $"{(sob - fal):N0} Gs.";
    }

    private async void BtnSaveAjuste_Click(object sender, RoutedEventArgs e)
    {
        if (_items.Count == 0)
        {
            MessageBox.Show(this, "Agregue al menos un producto al ajuste.", "Ajuste de Stock", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        decimal sob = _items.Sum(i => i.Entrada * i.Precio);
        decimal fal = _items.Sum(i => i.Salida * i.Precio);

        var adj = new StockAdjustmentEntity
        {
            NroAjuste = int.TryParse(TxtNroAjuste.Text, out int nro) ? nro : 0,
            Fecha = DpFecha.SelectedDate ?? DateTime.Today,
            IdDeposito = SelectedDepositoId,
            AutorizadoPor = TxtAutorizado.Text.Trim(),
            Motivo = CmbMotivo.SelectedIndex + 1,
            MotivoTexto = CmbMotivo.Text,
            Especificar = TxtEspecificar.Text.Trim(),
            Concepto = CmbMotivo.Text,
            Sobrante = sob,
            Faltante = fal,
            TotalDiferencia = sob - fal,
            CidUsuario = _windowManager.CurrentUser?.Cidusuario ?? "ADMINISTRA",
            Items = _items.ToList()
        };

        try
        {
            int savedId = await _productRepository.SaveStockAdjustmentTransactionAsync(adj);
            MessageBox.Show(this, $"Ajuste #{adj.NroAjuste} (ID {savedId}) guardado y existencias actualizadas.", "Ajuste de Stock", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Error al guardar ajuste:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F9)
        {
            e.Handled = true;
            BtnSaveAjuste_Click(sender, new RoutedEventArgs());
        }
    }
}
