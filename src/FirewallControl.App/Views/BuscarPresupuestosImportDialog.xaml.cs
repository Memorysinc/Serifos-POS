using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FirewallControl.Core.Entities;
using FirewallControl.Core.Interfaces;

namespace FirewallControl.App.Views;

/// <summary>
/// Busca e importa presupuestos (presupuestos / itemspresupuestos) hacia ABMVENTAS.
/// </summary>
public partial class BuscarPresupuestosImportDialog : Window
{
    private readonly ISalesRepository _salesRepository;

    public BudgetSummaryEntity? SelectedBudget { get; private set; }
    public List<BudgetItemEntity> SelectedItems { get; private set; } = new();

    public BuscarPresupuestosImportDialog(ISalesRepository salesRepository)
    {
        InitializeComponent();
        _salesRepository = salesRepository;
        Loaded += async (_, _) =>
        {
            await LoadBudgetsAsync();
            TxtFilter.Focus();
        };
    }

    private async Task LoadBudgetsAsync()
    {
        try
        {
            var rows = await _salesRepository.SearchBudgetsAsync(TxtFilter.Text, 120);
            GridBudgets.ItemsSource = rows;
            if (rows.Count > 0)
            {
                GridBudgets.SelectedIndex = 0;
            }
            else
            {
                GridBudgetItems.ItemsSource = null;
                SelectedBudget = null;
                SelectedItems = new List<BudgetItemEntity>();
            }
            LblInfo.Text = $"{rows.Count} presupuesto(s) encontrado(s).";
        }
        catch (Exception ex)
        {
            LblInfo.Text = $"Error al buscar presupuestos: {ex.Message}";
        }
    }

    private async void BtnSearch_Click(object sender, RoutedEventArgs e) => await LoadBudgetsAsync();

    private async void TxtFilter_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            await LoadBudgetsAsync();
        }
    }

    private async void GridBudgets_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GridBudgets.SelectedItem is not BudgetSummaryEntity hdr)
            return;

        try
        {
            var full = await _salesRepository.GetBudgetByNumberOrIdAsync(hdr.IdPresupuesto, byNumber: false);
            if (full != null)
            {
                SelectedBudget = full.Value.Header;
                SelectedItems = full.Value.Items;
                GridBudgetItems.ItemsSource = SelectedItems;
            }
        }
        catch (Exception ex)
        {
            LblInfo.Text = $"Error cargando ítems: {ex.Message}";
        }
    }

    private void ConfirmSelection()
    {
        if (SelectedBudget == null)
        {
            MessageBox.Show(this, "Seleccione un presupuesto de la lista.", "Presupuestos", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        DialogResult = true;
        Close();
    }

    private void BtnImport_Click(object sender, RoutedEventArgs e) => ConfirmSelection();
    private void GridBudgets_MouseDoubleClick(object sender, MouseButtonEventArgs e) => ConfirmSelection();

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && GridBudgets.IsFocused)
        {
            e.Handled = true;
            ConfirmSelection();
        }
    }
}
