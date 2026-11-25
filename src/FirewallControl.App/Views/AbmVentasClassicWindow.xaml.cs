using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FirewallControl.App.Services;
using FirewallControl.App.ViewModels;

namespace FirewallControl.App.Views;

/// <summary>
/// Classic Visual FoxPro / Sufer POS layout for ABMVENTAS (abm_ventas_autoservice_cobro_contado.scx).
/// Shares the exact same AbmVentasAutoserviceViewModel and database transactions as Renovado.
/// </summary>
public partial class AbmVentasClassicWindow : Window, ISalesPosWindow
{
    public AbmVentasAutoserviceViewModel ViewModel { get; }
    public bool AllowPermanentClose { get; set; }
    public Window Window => this;

    public AbmVentasClassicWindow(AbmVentasAutoserviceViewModel viewModel)
    {
        InitializeComponent();
        AppIconHelper.ApplySefirosIcon(this);
        ViewModel = viewModel;
        DataContext = viewModel;

        ViewModel.RequestFocusGrid += OnRequestFocusGrid;
        ViewModel.Items.CollectionChanged += OnItemsCollectionChanged;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        UpdateReeditandoBadge();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!AllowPermanentClose)
        {
            e.Cancel = true;
            ViewModel.HandleWindowClosedByUser(this);
            return;
        }

        ViewModel.RequestFocusGrid -= OnRequestFocusGrid;
        ViewModel.Items.CollectionChanged -= OnItemsCollectionChanged;
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        base.OnClosing(e);
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (ViewModel.Items.Count > 0)
        {
            PosWindowBehaviorHelper.ScrollItemIntoView(this, GridItems, ViewModel.SelectedItem ?? ViewModel.Items.LastOrDefault());
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AbmVentasAutoserviceViewModel.SelectedItem) && ViewModel.SelectedItem != null)
        {
            PosWindowBehaviorHelper.ScrollItemIntoView(this, GridItems, ViewModel.SelectedItem);
        }
        else if (e.PropertyName == nameof(AbmVentasAutoserviceViewModel.LcTipo))
        {
            UpdateReeditandoBadge();
        }
    }

    private void UpdateReeditandoBadge()
    {
        if (BadgeReeditando != null)
        {
            BadgeReeditando.Visibility = ViewModel.LcTipo == "M" ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void OnRequestFocusGrid() => PosWindowBehaviorHelper.FocusGrid(this, GridItems, ViewModel.SelectedItem);

    private async void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        => await PosWindowBehaviorHelper.HandleSalesWindowPreviewKeyDownAsync(ViewModel, GridItems, e);

    private void GridItems_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel.SelectedItem != null)
            ViewModel.MantenimientoItems("M", ViewModel.SelectedItem);
    }

    private void GridItems_PreviewKeyDown(object sender, KeyEventArgs e)
        => PosWindowBehaviorHelper.HandleSalesGridKeyDown(ViewModel, e);

    private void TxtRucCliente_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter || e.Key == Key.Return)
        {
            e.Handled = true;
            ViewModel.LookupCustomerByRucCommand.Execute(null);
        }
        else if (e.Key == Key.F3)
        {
            e.Handled = true;
            ViewModel.SearchCustomerCommand.Execute(null);
        }
    }

    private void TxtPresupuesto_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter || e.Key == Key.Return)
        {
            e.Handled = true;
            ViewModel.LookupPresupuestoByNumberCommand.Execute(null);
        }
    }

    private void TxtDescuento_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter || e.Key == Key.Return)
        {
            e.Handled = true;
            if (sender is TextBox tb)
            {
                tb.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            }
        }
    }

    private void BtnToolsMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.ContextMenu != null)
        {
            btn.ContextMenu.DataContext = ViewModel;
            btn.ContextMenu.PlacementTarget = btn;
            btn.ContextMenu.IsOpen = true;
        }
    }

    private void MenuComprobanteTicket_Click(object sender, RoutedEventArgs e)
        => ViewModel.TipoBoleta = 1;

    private void MenuComprobanteFactura_Click(object sender, RoutedEventArgs e)
        => ViewModel.TipoBoleta = 2;

    private void BtnSalir_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.HandleWindowClosedByUser(this);
    }
}
