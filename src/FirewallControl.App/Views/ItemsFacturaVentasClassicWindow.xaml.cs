using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FirewallControl.App.Services;
using FirewallControl.App.ViewModels;

namespace FirewallControl.App.Views;

/// <summary>
/// Classic Visual FoxPro / Sufer POS layout for ITEMSFACTURAVENTAS (itemsfacturaventas.scx).
/// Shares the exact same ItemsFacturaVentasViewModel as ItemsFacturaVentasWindow.
/// </summary>
public partial class ItemsFacturaVentasClassicWindow : Window, IItemsSelectorWindow
{
    public ItemsFacturaVentasViewModel ViewModel { get; }
    public bool AllowPermanentClose { get; set; }
    public Window Window => this;

    public ItemsFacturaVentasClassicWindow(ItemsFacturaVentasViewModel viewModel)
    {
        InitializeComponent();
        AppIconHelper.ApplySefirosIcon(this);
        ViewModel = viewModel;
        DataContext = viewModel;

        ViewModel.RequestFocusControl += OnRequestFocusControl;
        PosWindowBehaviorHelper.AttachItemsCodProductoFocusGuard(
            this, ViewModel, TxtCodProducto, TxtCodProducto, TxtKilogramo, TxtCantidad, TxtPrecioVenta);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!AllowPermanentClose)
        {
            e.Cancel = true;
            ViewModel.HandleWindowClosedByUser(this);
            return;
        }

        ViewModel.RequestFocusControl -= OnRequestFocusControl;
        base.OnClosing(e);
    }

    private void OnRequestFocusControl(string target)
        => PosWindowBehaviorHelper.HandleItemsFocusTarget(
            this, target, TxtCodProducto, TxtKilogramo, TxtCantidad, TxtPrecioVenta, BtnGrabar);

    private async void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        => await PosWindowBehaviorHelper.HandleItemsWindowPreviewKeyDownAsync(
            ViewModel, TxtCodProducto, TxtCantidad, TxtKilogramo, TxtPrecioVenta, e);

    private async void TxtCodProducto_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter || e.Key == Key.Return)
        {
            e.Handled = true;
            await ViewModel.ValidateAndLookupProductAsync();
        }
    }

    private void TxtKilogramo_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (ViewModel == null || !TxtKilogramo.IsFocused)
            return;

        if (PosNumericParser.TryParseGuaraniAmount(TxtKilogramo.Text, out decimal parsedKg))
            ViewModel.Kilogramo = parsedKg;
    }

    private async void TxtKilogramo_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter || e.Key == Key.Return)
        {
            e.Handled = true;
            if (PosNumericParser.TryParseGuaraniAmount(TxtKilogramo.Text, out decimal parsedKg))
                ViewModel.Kilogramo = parsedKg;
            await ViewModel.CmdGrabarAsync();
        }
    }

    private void TxtCantidad_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter || e.Key == Key.Return)
        {
            e.Handled = true;
            if (!PosNumericParser.TryParseQuantity(TxtCantidad.Text, out decimal parsedQty))
            {
                ViewModel.HandleFieldValidationError("Ingrese una cantidad válida mayor que cero.", "Cantidad");
                return;
            }

            ViewModel.Cantidad = parsedQty;
            if (!ViewModel.ValidateQuantityOnEnter())
                return;

            PosWindowBehaviorHelper.FocusAndSelect(TxtPrecioVenta);
        }
    }

    private void TxtPrecioVenta_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (ViewModel == null || !TxtPrecioVenta.IsFocused)
            return;

        if (PosNumericParser.TryParseGuaraniAmount(TxtPrecioVenta.Text, out decimal parsedPrice))
            ViewModel.PrecioVenta = parsedPrice;
    }

    private async void TxtPrecioVenta_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter || e.Key == Key.Return)
        {
            e.Handled = true;
            if (!PosNumericParser.TryParseGuaraniAmount(TxtPrecioVenta.Text, out decimal parsedPrice))
            {
                ViewModel.HandleFieldValidationError("El precio no puede ser 0 ni inferior al costo.", "PrecioVenta");
                return;
            }

            ViewModel.PrecioVenta = parsedPrice;
            await ViewModel.CmdGrabarAsync();
        }
    }

    private void BtnComisPriceMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.ContextMenu != null)
        {
            btn.ContextMenu.DataContext = ViewModel;
            btn.ContextMenu.PlacementTarget = btn;
            btn.ContextMenu.IsOpen = true;
        }
    }

    private void BtnSelectorTools_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.ContextMenu != null)
        {
            btn.ContextMenu.DataContext = ViewModel;
            btn.ContextMenu.PlacementTarget = btn;
            btn.ContextMenu.IsOpen = true;
        }
    }

    private async void BtnCobrarTicket_Click(object sender, RoutedEventArgs e)
        => await ViewModel.HandleShortcutKeyAsync("Z");

    private async void BtnModoBorrado_Click(object sender, RoutedEventArgs e)
        => await ViewModel.HandleShortcutKeyAsync("DEL");
}
