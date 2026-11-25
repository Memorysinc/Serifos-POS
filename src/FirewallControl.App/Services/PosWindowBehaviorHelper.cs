using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using FirewallControl.App.ViewModels;
using FirewallControl.Core.Entities;

namespace FirewallControl.App.Services;

/// <summary>
/// Shared keyboard routing, grid scroll, and field focus helpers reused by both
/// Renovado (Atelier) and Clasico (Sufer POS) ABMVENTAS / ITEMSFACTURAVENTAS windows.
/// </summary>
public static class PosWindowBehaviorHelper
{
    public static void FocusAndSelect(TextBox? textBox)
    {
        if (textBox == null || !textBox.IsVisible || !textBox.IsEnabled)
            return;
        textBox.Focus();
        Keyboard.Focus(textBox);
        textBox.SelectAll();
    }

    public static void HandleItemsFocusTarget(
        Window window,
        string target,
        TextBox txtCodProducto,
        TextBox txtKilogramo,
        TextBox txtCantidad,
        TextBox txtPrecioVenta,
        Button btnGrabar)
    {
        window.Dispatcher.BeginInvoke(new Action(() =>
        {
            switch (target)
            {
                case "CodProducto":
                    FocusAndSelect(txtCodProducto);
                    break;
                case "Kilogramo":
                    FocusAndSelect(txtKilogramo);
                    break;
                case "Cantidad":
                    FocusAndSelect(txtCantidad);
                    break;
                case "PrecioVenta":
                    FocusAndSelect(txtPrecioVenta);
                    break;
                case "CmdGrabar":
                    btnGrabar.Focus();
                    break;
            }
        }), DispatcherPriority.Input);
    }

    /// <summary>
    /// Mantiene el foco en TxtCodProducto cuando la ventana se activa, se muestra o se hace clic fuera
    /// de campos editables numéricos, garantizando captura continua de escáner/teclado en caja.
    /// </summary>
    public static void AttachItemsCodProductoFocusGuard(
        Window window,
        ItemsFacturaVentasViewModel vm,
        TextBox txtCodProducto,
        params FrameworkElement[] editableInputs)
    {
        void EnsureCodFocus(bool forceWhenAdding, bool requireActive, DispatcherPriority priority)
        {
            if (!window.IsVisible)
                return;
            if (requireActive && !window.IsActive)
                return;
            if (Mouse.LeftButton == MouseButtonState.Pressed || Mouse.RightButton == MouseButtonState.Pressed)
                return;

            window.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!window.IsVisible)
                    return;
                if (requireActive && !window.IsActive)
                    return;
                if (Mouse.LeftButton == MouseButtonState.Pressed || Mouse.RightButton == MouseButtonState.Pressed)
                    return;
                if (!forceWhenAdding && ReferenceEquals(Keyboard.FocusedElement, txtCodProducto))
                    return;

                bool inEditable = IsFocusInsideEditableControl(Keyboard.FocusedElement as DependencyObject, editableInputs);
                if (!inEditable && (forceWhenAdding || string.Equals(vm.LcTipo, "A", StringComparison.OrdinalIgnoreCase)))
                {
                    FocusAndSelect(txtCodProducto);
                }
            }), priority);
        }

        window.Loaded += (_, _) => EnsureCodFocus(forceWhenAdding: true, requireActive: false, DispatcherPriority.Input);
        window.Activated += (_, _) => EnsureCodFocus(forceWhenAdding: false, requireActive: true, DispatcherPriority.Input);
        window.IsVisibleChanged += (_, _) =>
        {
            if (window.IsVisible)
                EnsureCodFocus(forceWhenAdding: false, requireActive: true, DispatcherPriority.Input);
        };
        window.PreviewMouseUp += (_, _) => EnsureCodFocus(forceWhenAdding: false, requireActive: true, DispatcherPriority.Background);
        window.GotKeyboardFocus += (_, e) =>
        {
            if (Mouse.LeftButton == MouseButtonState.Pressed || Mouse.RightButton == MouseButtonState.Pressed)
                return;
            if (e.NewFocus is System.Windows.Controls.Primitives.ButtonBase)
                return;
            if (IsFocusInsideEditableControl(e.NewFocus as DependencyObject, editableInputs))
                return;
            EnsureCodFocus(forceWhenAdding: false, requireActive: true, DispatcherPriority.Background);
        };
    }

    private static bool IsFocusInsideEditableControl(DependencyObject? focused, FrameworkElement[] editableInputs)
    {
        if (focused == null)
            return false;

        foreach (var ctrl in editableInputs)
        {
            if (ReferenceEquals(focused, ctrl))
                return true;
            if (focused is FrameworkElement fe && IsVisualOrLogicalDescendant(fe, ctrl))
                return true;
        }

        if (focused is DependencyObject dep && IsInsideOpenComboBox(dep))
            return true;

        return focused is TextBox;
    }

    private static bool IsInsideOpenComboBox(DependencyObject? node)
    {
        while (node != null)
        {
            if (node is ComboBox cb)
                return cb.IsDropDownOpen;

            if (node is ComboBoxItem cbi && ItemsControl.ItemsControlFromItemContainer(cbi) is ComboBox parentCombo)
                return parentCombo.IsDropDownOpen;

            node = System.Windows.Media.VisualTreeHelper.GetParent(node) ?? LogicalTreeHelper.GetParent(node);
        }

        return false;
    }

    private static bool IsVisualOrLogicalDescendant(DependencyObject? node, DependencyObject ancestor)
    {
        while (node != null)
        {
            if (ReferenceEquals(node, ancestor))
                return true;
            node = System.Windows.Media.VisualTreeHelper.GetParent(node) ?? LogicalTreeHelper.GetParent(node);
        }
        return false;
    }

    public static void ScrollItemIntoView(Window window, DataGrid grid, SaleItemEntity? item)
    {
        if (item == null)
            return;

        window.Dispatcher.BeginInvoke(new Action(() =>
        {
            if (grid.Items.Count == 0)
                return;
            grid.UpdateLayout();
            grid.ScrollIntoView(item);
        }), DispatcherPriority.Background);
    }

    public static void FocusGrid(Window window, DataGrid grid, SaleItemEntity? selectedItem)
    {
        window.Dispatcher.BeginInvoke(new Action(() =>
        {
            window.Activate();
            window.Focus();
            grid.Focus();
            if (selectedItem != null)
            {
                grid.ScrollIntoView(selectedItem);
                grid.UpdateLayout();
                if (grid.ItemContainerGenerator.ContainerFromItem(selectedItem) is DataGridRow row)
                {
                    row.Focus();
                }
            }
        }));
    }

    public static async Task HandleItemsWindowPreviewKeyDownAsync(
        ItemsFacturaVentasViewModel vm,
        TextBox txtCodProducto,
        TextBox txtCantidad,
        TextBox txtKilogramo,
        TextBox txtPrecioVenta,
        KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            vm.LimpiarCommand.Execute(null);
            return;
        }

        bool isEditingNumericField = Keyboard.FocusedElement == txtCantidad
                                     || Keyboard.FocusedElement == txtKilogramo
                                     || Keyboard.FocusedElement == txtPrecioVenta;

        bool isInputEmpty = !isEditingNumericField && string.IsNullOrWhiteSpace(txtCodProducto.Text);
        string combo = KeyShortcutHelper.NormalizeKeyEvent(e);

        if (!string.IsNullOrWhiteSpace(combo))
        {
            var binding = ShortcutCatalog.ResolveTriggeredBinding(
                vm.Preferences,
                ShortcutCatalog.WinItemsFactura,
                combo,
                isInputEmpty);

            if (binding != null)
            {
                e.Handled = true;
                await vm.HandleShortcutKeyAsync(binding.ActionCode);
                return;
            }
        }

        var modifiers = Keyboard.Modifiers;
        if (modifiers == ModifierKeys.Control && e.Key == Key.B)
        {
            e.Handled = true;
            await vm.HandleShortcutKeyAsync("BUSCAR_CATALOGO");
            return;
        }
        if (modifiers == ModifierKeys.Control && e.Key == Key.P)
        {
            e.Handled = true;
            await vm.HandleShortcutKeyAsync("CTRL+P");
            return;
        }
    }

    public static async Task HandleSalesWindowPreviewKeyDownAsync(
        AbmVentasAutoserviceViewModel vm,
        DataGrid gridItems,
        KeyEventArgs e)
    {
        if (vm.ModoBorradoGrilla)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                vm.SalirModoBorrado();
                return;
            }

            if (e.Key == Key.Delete || e.Key == Key.Enter || e.Key == Key.Return)
            {
                e.Handled = true;
                vm.DeleteSelectedItem(vm.SelectedItem);
                return;
            }

            if (e.Key == Key.Up && vm.Items.Count > 0)
            {
                e.Handled = true;
                int idx = vm.SelectedItem != null ? vm.Items.IndexOf(vm.SelectedItem) : 0;
                vm.SelectedItem = vm.Items[Math.Max(0, idx - 1)];
                gridItems.ScrollIntoView(vm.SelectedItem);
                return;
            }

            if (e.Key == Key.Down && vm.Items.Count > 0)
            {
                e.Handled = true;
                int idx = vm.SelectedItem != null ? vm.Items.IndexOf(vm.SelectedItem) : -1;
                vm.SelectedItem = vm.Items[Math.Min(vm.Items.Count - 1, idx + 1)];
                gridItems.ScrollIntoView(vm.SelectedItem);
                return;
            }
        }

        bool isTypingInTextBox = Keyboard.FocusedElement is TextBox;
        string combo = KeyShortcutHelper.NormalizeKeyEvent(e);
        if (!string.IsNullOrWhiteSpace(combo))
        {
            var binding = ShortcutCatalog.ResolveTriggeredBinding(
                vm.Preferences,
                ShortcutCatalog.WinAbmVentas,
                combo,
                isInputEmpty: !isTypingInTextBox);

            if (binding != null)
            {
                switch (binding.ActionCode)
                {
                    case "ABRIR_SELECTOR":
                        e.Handled = true;
                        vm.MantenimientoItems("A");
                        return;
                    case "MODIFICAR_ITEM":
                        e.Handled = true;
                        vm.MantenimientoItems("M");
                        return;
                    case "COBRAR_SIN_TICKET":
                        e.Handled = true;
                        await vm.ProcesarVentaAsync(false);
                        return;
                    case "COBRAR_TICKET":
                        e.Handled = true;
                        await vm.ProcesarVentaAsync(true);
                        return;
                    case "REEDITAR_VENTA":
                        e.Handled = true;
                        await vm.ReeditarVentaAsync();
                        return;
                    case "REIMPRIMIR_VENTA":
                        e.Handled = true;
                        await vm.ReimprimirVentaAsync();
                        return;
                    case "CALCULADORA":
                        e.Handled = true;
                        vm.OpenCalculatorCommand.Execute(null);
                        return;
                    case "BORRAR_ITEM":
                        if (vm.SelectedItem != null && !isTypingInTextBox)
                        {
                            e.Handled = true;
                            vm.DeleteSelectedItem(vm.SelectedItem);
                            return;
                        }
                        break;
                }
            }
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.S)
        {
            e.Handled = true;
            await vm.ProcesarVentaAsync(false);
        }
    }

    public static void HandleSalesGridKeyDown(AbmVentasAutoserviceViewModel vm, KeyEventArgs e)
    {
        if (vm.ModoBorradoGrilla)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                vm.SalirModoBorrado();
                return;
            }

            if (e.Key == Key.Enter || e.Key == Key.Return || e.Key == Key.Delete)
            {
                e.Handled = true;
                vm.DeleteSelectedItem(vm.SelectedItem);
                return;
            }
        }
        else if (e.Key == Key.Delete && vm.SelectedItem != null)
        {
            e.Handled = true;
            vm.DeleteSelectedItem(vm.SelectedItem);
        }
        else if (e.Key == Key.Enter && vm.SelectedItem != null)
        {
            e.Handled = true;
            vm.MantenimientoItems("M", vm.SelectedItem);
        }
    }
}
