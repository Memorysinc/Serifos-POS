using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FirewallControl.App.Services;
using FirewallControl.App.ViewModels;

namespace FirewallControl.App.Views;

/// <summary>
/// Single reusable window instance (Renovado C# design) for ITEMSFACTURAVENTAS.SCX.
/// Shares ItemsFacturaVentasViewModel with ItemsFacturaVentasClassicWindow.
/// </summary>
public partial class ItemsFacturaVentasWindow : Window, IItemsSelectorWindow
{
    public ItemsFacturaVentasViewModel ViewModel { get; }
    public bool AllowPermanentClose { get; set; }
    public Window Window => this;

    public ItemsFacturaVentasWindow(ItemsFacturaVentasViewModel viewModel)
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

    public static bool TryParseGuaraniAmount(string? rawText, out decimal value)
        => PosNumericParser.TryParseGuaraniAmount(rawText, out value);

    public static bool TryParseQuantity(string? rawText, out decimal value)
        => PosNumericParser.TryParseQuantity(rawText, out value);

    private async void BtnCobrarTicket_Click(object sender, RoutedEventArgs e)
        => await ViewModel.HandleShortcutKeyAsync("Z");

    private async void BtnCobrarSinTicket_Click(object sender, RoutedEventArgs e)
        => await ViewModel.HandleShortcutKeyAsync("X");

    private async void BtnModoBorrado_Click(object sender, RoutedEventArgs e)
        => await ViewModel.HandleShortcutKeyAsync("DEL");
}
