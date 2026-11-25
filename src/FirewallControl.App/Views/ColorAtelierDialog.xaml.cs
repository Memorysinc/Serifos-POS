using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FirewallControl.App.Services;

namespace FirewallControl.App.Views;

public partial class ColorAtelierDialog : Window
{
    private readonly List<(string Key, Border Card, TextBlock Check)> _cards = new();
    public string SelectedThemeKey { get; private set; }

    public ColorAtelierDialog(string? currentThemeKey)
    {
        InitializeComponent();
        SelectedThemeKey = ThemeService.GetPalette(currentThemeKey).Key;
        BuildPaletteCards();
        UpdateSelectionVisuals();
    }

    private void BuildPaletteCards()
    {
        PalettesGrid.Children.Clear();
        _cards.Clear();

        foreach (var palette in ThemeService.Palettes)
        {
            var swatch = new Border
            {
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(8),
                Background = ThemeService.CreatePaletteSwatchBrush(palette),
                BorderBrush = new SolidColorBrush(Color.FromArgb(30, 0, 0, 0)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            var checkIcon = new TextBlock
            {
                Text = "✓",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            swatch.Child = checkIcon;

            var labelStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center
            };

            var label = new TextBlock
            {
                Text = palette.Label,
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            label.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
            labelStack.Children.Add(label);

            if (string.Equals(palette.Key, "sefiros", StringComparison.OrdinalIgnoreCase))
            {
                var subLabel = new TextBlock
                {
                    Text = "Paleta Sefiros · Todo el POS + FOX",
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                subLabel.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
                labelStack.Children.Add(subLabel);
            }

            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            row.Children.Add(swatch);
            row.Children.Add(labelStack);

            var card = new Border
            {
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(11, 9, 11, 9),
                Margin = new Thickness(4),
                BorderThickness = new Thickness(1.5),
                Cursor = Cursors.Hand,
                Child = row
            };

            string key = palette.Key;
            card.MouseLeftButtonDown += (_, e) =>
            {
                e.Handled = true;
                SelectedThemeKey = key;
                UpdateSelectionVisuals();
            };

            _cards.Add((key, card, checkIcon));
            PalettesGrid.Children.Add(card);
        }
    }

    private void UpdateSelectionVisuals()
    {
        foreach (var (key, card, check) in _cards)
        {
            bool selected = string.Equals(key, SelectedThemeKey, StringComparison.OrdinalIgnoreCase);
            check.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
            card.SetResourceReference(Border.BackgroundProperty, selected ? "SurfaceAltBrush" : "InputBgBrush");
            card.SetResourceReference(Border.BorderBrushProperty, selected ? "TextPrimaryBrush" : "CardBorderBrush");
        }
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && e.ButtonState == MouseButtonState.Pressed)
        {
            try { DragMove(); } catch { }
        }
    }

    private void BtnAceptar_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void BtnCancelar_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            DialogResult = false;
            Close();
        }
        else if (e.Key == Key.Enter)
        {
            e.Handled = true;
            DialogResult = true;
            Close();
        }
    }
}
