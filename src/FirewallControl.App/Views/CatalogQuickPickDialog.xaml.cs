using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FirewallControl.App.Views;

public record QuickPickOption(int Id, string Nombre, string Extra = "");

public partial class CatalogQuickPickDialog : Window
{
    private List<QuickPickOption> _allItems = new();

    public QuickPickOption? SelectedOption { get; private set; }

    public CatalogQuickPickDialog()
    {
        InitializeComponent();
    }

    public void Configure(string title, IEnumerable<QuickPickOption> options)
    {
        Title = title;
        TxtTitle.Text = title;
        _allItems = options.ToList();
        ApplyFilter();
        Loaded += (_, _) => TxtFilter.Focus();
    }

    private void ApplyFilter()
    {
        string term = (TxtFilter.Text ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(term))
        {
            GridItems.ItemsSource = _allItems;
        }
        else
        {
            var words = term.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            GridItems.ItemsSource = _allItems.Where(o =>
            {
                string haystack = $"{o.Id} {o.Nombre} {o.Extra}".ToUpperInvariant();
                return words.All(w => haystack.Contains(w, StringComparison.Ordinal));
            }).ToList();
        }

        if (GridItems.Items.Count > 0)
            GridItems.SelectedIndex = 0;
    }

    private void TxtFilter_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

    private void TxtFilter_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Down && GridItems.Items.Count > 0)
        {
            e.Handled = true;
            GridItems.Focus();
        }
        else if (e.Key == Key.Enter || e.Key == Key.Return)
        {
            e.Handled = true;
            ConfirmSelection();
        }
    }

    private void GridItems_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter || e.Key == Key.Return)
        {
            e.Handled = true;
            ConfirmSelection();
        }
    }

    private void GridItems_MouseDoubleClick(object sender, MouseButtonEventArgs e) => ConfirmSelection();

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        TxtFilter.Clear();
        TxtFilter.Focus();
    }

    private void BtnSelect_Click(object sender, RoutedEventArgs e) => ConfirmSelection();

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ConfirmSelection()
    {
        if (GridItems.SelectedItem is QuickPickOption opt)
        {
            SelectedOption = opt;
            DialogResult = true;
            Close();
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            DialogResult = false;
            Close();
        }
    }
}
