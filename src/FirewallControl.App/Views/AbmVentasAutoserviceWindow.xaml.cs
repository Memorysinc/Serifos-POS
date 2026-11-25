using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FirewallControl.App.Services;
using FirewallControl.App.ViewModels;
using FirewallControl.Core.Entities;

namespace FirewallControl.App.Views;

/// <summary>
/// Singleton-per-Caja POS window (Renovado C# design) for ABM_VENTAS_AUTOSERVICE_COBRO_CONTADO.
/// Shares AbmVentasAutoserviceViewModel with AbmVentasClassicWindow.
/// </summary>
public partial class AbmVentasAutoserviceWindow : Window, ISalesPosWindow
{
    public AbmVentasAutoserviceViewModel ViewModel { get; }
    public bool AllowPermanentClose { get; set; }
    public Window Window => this;

    public AbmVentasAutoserviceWindow(AbmVentasAutoserviceViewModel viewModel)
    {
        InitializeComponent();
        AppIconHelper.ApplySefirosIcon(this);
        ViewModel = viewModel;
        DataContext = viewModel;

        ViewModel.RequestFocusGrid += OnRequestFocusGrid;
        ViewModel.Items.CollectionChanged += OnItemsCollectionChanged;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
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

    private void BtnToolsMenu_Click(object sender, RoutedEventArgs e)
    {
        if (BtnToolsMenu.ContextMenu != null)
        {
            BtnToolsMenu.ContextMenu.PlacementTarget = BtnToolsMenu;
            BtnToolsMenu.ContextMenu.DataContext = ViewModel;
            BtnToolsMenu.ContextMenu.IsOpen = true;
        }
    }
}
