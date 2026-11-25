using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FirewallControl.Core.Entities;
using FirewallControl.Core.Interfaces;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace FirewallControl.App.Services;

/// <summary>
/// Resolves product image paths matching VFP's \\server\Reportes\Fotos and local Reportes\Fotos folders.
/// </summary>
public static class ProductPhotoHelper
{
    public static string GetPhotosDirectory(IConfigService? configService)
    {
        string baseDir = configService?.Config.BaseDirectory ?? string.Empty;
        if (string.IsNullOrWhiteSpace(baseDir))
            baseDir = AppDomain.CurrentDomain.BaseDirectory;

        string server = configService?.Config.RutaServer?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(server) &&
            !string.Equals(server, "localhost", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(server, "127.0.0.1", StringComparison.OrdinalIgnoreCase))
        {
            string unc = $@"\\{server}\Reportes\Fotos";
            if (Directory.Exists(unc))
                return unc;
        }

        string localReportes = Path.Combine(baseDir, "Reportes", "Fotos");
        if (Directory.Exists(localReportes))
            return localReportes;

        string lowerReportes = Path.Combine(baseDir, "reportes", "Fotos");
        if (Directory.Exists(lowerReportes))
            return lowerReportes;

        string altFotos = Path.Combine(baseDir, "Fotos");
        if (Directory.Exists(altFotos))
            return altFotos;

        Directory.CreateDirectory(localReportes);
        return localReportes;
    }

    public static string? ResolvePhotoFilePath(string? fileName, IConfigService? configService)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        string clean = fileName.Trim();
        if (Path.IsPathRooted(clean) && File.Exists(clean))
            return clean;

        string dir = GetPhotosDirectory(configService);
        string candidate = Path.Combine(dir, Path.GetFileName(clean));
        if (File.Exists(candidate))
            return candidate;

        string baseDir = configService?.Config.BaseDirectory ?? AppDomain.CurrentDomain.BaseDirectory;
        string[] extraDirs =
        {
            Path.Combine(baseDir, "reportes", "Fotos"),
            Path.Combine(baseDir, "Fotos"),
            Path.Combine(baseDir, "images")
        };

        foreach (var d in extraDirs)
        {
            string c = Path.Combine(d, Path.GetFileName(clean));
            if (File.Exists(c))
                return c;
        }

        return null;
    }

    public static BitmapImage? LoadBitmapSafe(string? fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath))
            return null;

        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(fullPath, UriKind.Absolute);
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch
        {
            return null;
        }
    }

    public static string CopyPhotoToRepository(string sourceFilePath, IConfigService? configService)
    {
        string targetDir = GetPhotosDirectory(configService);
        Directory.CreateDirectory(targetDir);
        string fileName = Path.GetFileName(sourceFilePath);
        if (fileName.Length > 60)
        {
            string ext = Path.GetExtension(fileName);
            fileName = fileName[..Math.Max(1, 56 - ext.Length)] + ext;
        }

        string destPath = Path.Combine(targetDir, fileName);
        if (!string.Equals(Path.GetFullPath(sourceFilePath), Path.GetFullPath(destPath), StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(sourceFilePath, destPath, overwrite: true);
        }
        return fileName;
    }
}

/// <summary>
/// Generates printable FlowDocuments for VFP-compatible Price Labels (etiprecio) and Barcode Labels (codbarra3 / codbarra4),
/// rendering scannable Code-39 vector bars without requiring external barcode fonts.
/// </summary>
public static class BarcodeLabelPrintHelper
{
    private static readonly Dictionary<char, string> Code39Patterns = new()
    {
        ['0'] = "nnnwwnwnn", ['1'] = "wnnwnnnnw", ['2'] = "nnwwnnnnw", ['3'] = "wnwwnnnnn",
        ['4'] = "nnnwwnnnw", ['5'] = "wnnwwnnnn", ['6'] = "nnwwwnnnn", ['7'] = "nnnwnnwnw",
        ['8'] = "wnnwnnwnn", ['9'] = "nnwwnnwnn", ['A'] = "wnnnnwnnw", ['B'] = "nnwnnwnnw",
        ['C'] = "wnwnnwnnn", ['D'] = "nnnnwwnnw", ['E'] = "wnnnwwnnn", ['F'] = "nnwnwwnnn",
        ['G'] = "nnnnnwwnw", ['H'] = "wnnnnwwnn", ['I'] = "nnwnnwwnn", ['J'] = "nnnnwwwnn",
        ['K'] = "wnnnnnnww", ['L'] = "nnwnnnnww", ['M'] = "wnwnnnnwn", ['N'] = "nnnnwnnww",
        ['O'] = "wnnnwnnwn", ['P'] = "nnwnwnnwn", ['Q'] = "nnnnnnwww", ['R'] = "wnnnnnwwn",
        ['S'] = "nnwnnnwwn", ['T'] = "nnnnwnwwn", ['U'] = "wwnnnnnnw", ['V'] = "nwwnnnnnw",
        ['W'] = "wwwnnnnnn", ['X'] = "nwnnwnnnw", ['Y'] = "wwnnwnnnn", ['Z'] = "nwwnwnnnn",
        ['-'] = "nwnnnnwnw", ['.'] = "wwnnnnwnn", [' '] = "nwwnnnwnn", ['$'] = "nwnwnwnnn",
        ['/'] = "nwnwnnnwn", ['+'] = "nwnnnwnwn", ['%'] = "nnnwnwnwn", ['*'] = "nwnnwnwnn"
    };

    public static FrameworkElement BuildCode39Graphic(string code, double barHeight = 34, double narrow = 1.4, double wide = 3.2)
    {
        string raw = (code ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(raw)) raw = "0";
        string filtered = "*";
        foreach (char ch in raw)
        {
            filtered += Code39Patterns.ContainsKey(ch) ? ch : '-';
        }
        filtered += "*";

        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Height = barHeight
        };

        foreach (char ch in filtered)
        {
            if (!Code39Patterns.TryGetValue(ch, out string? pattern))
                continue;

            for (int i = 0; i < pattern.Length; i++)
            {
                bool isBar = (i % 2) == 0;
                double w = pattern[i] == 'w' ? wide : narrow;
                panel.Children.Add(new Rectangle
                {
                    Width = w,
                    Height = barHeight,
                    Fill = isBar ? Brushes.Black : Brushes.White
                });
            }

            // Inter-character narrow white gap
            panel.Children.Add(new Rectangle
            {
                Width = narrow,
                Height = barHeight,
                Fill = Brushes.White
            });
        }

        return panel;
    }

    public static FlowDocument BuildLabelsDocument(
        ProductEntity product,
        int copies,
        bool isBarcodeMode,
        bool threePerRow)
    {
        int count = Math.Clamp(copies, 1, 500);
        int cols = threePerRow ? 3 : 1;

        var doc = new FlowDocument
        {
            PagePadding = new Thickness(18),
            FontFamily = new FontFamily("Segoe UI"),
            Background = Brushes.White
        };

        string desc = (product.Descripcion ?? string.Empty).Trim();
        if (desc.Length > 45) desc = desc[..45];
        string cod = string.IsNullOrWhiteSpace(product.Codigo) ? product.IdProducto.ToString() : product.Codigo.Trim();

        var table = new Table { CellSpacing = 6 };
        for (int c = 0; c < cols; c++)
            table.Columns.Add(new TableColumn { Width = new GridLength(threePerRow ? 220 : 280) });

        var rowGroup = new TableRowGroup();
        table.RowGroups.Add(rowGroup);

        TableRow? currentRow = null;
        for (int i = 0; i < count; i++)
        {
            if (i % cols == 0)
            {
                currentRow = new TableRow();
                rowGroup.Rows.Add(currentRow);
            }

            var cell = new TableCell
            {
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(0.8),
                Padding = new Thickness(8, 6, 8, 6)
            };

            var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            stack.Children.Add(new TextBlock
            {
                Text = desc,
                FontWeight = FontWeights.Bold,
                FontSize = threePerRow ? 9.5 : 11,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 4)
            });

            if (isBarcodeMode)
            {
                stack.Children.Add(BuildCode39Graphic(cod, threePerRow ? 28 : 38, threePerRow ? 1.15 : 1.5, threePerRow ? 2.5 : 3.4));
                stack.Children.Add(new TextBlock
                {
                    Text = cod,
                    FontSize = 9.5,
                    FontWeight = FontWeights.SemiBold,
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 2, 0, 2)
                });
            }

            stack.Children.Add(new TextBlock
            {
                Text = $"Gs. {product.Precio1:N0}",
                FontWeight = FontWeights.Bold,
                FontSize = isBarcodeMode ? 11 : 16,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 2, 0, 0)
            });

            cell.Blocks.Add(new BlockUIContainer(stack));
            currentRow!.Cells.Add(cell);
        }

        doc.Blocks.Add(table);
        return doc;
    }

    public static void ShowPrintPreview(Window? owner, ProductEntity product, int copies, bool isBarcodeMode, bool threePerRow)
    {
        var doc = BuildLabelsDocument(product, copies, isBarcodeMode, threePerRow);

        var viewer = new FlowDocumentScrollViewer
        {
            Document = doc,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        var btnPrint = new Button
        {
            Content = "🖨 Enviar a Impresora",
            Padding = new Thickness(16, 8, 16, 8),
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 8, 0)
        };
        var btnClose = new Button
        {
            Content = "Cerrar [ESC]",
            Padding = new Thickness(14, 8, 14, 8)
        };

        var previewWin = new Window
        {
            Title = isBarcodeMode
                ? $"Vista Previa - Código de Barras ({(threePerRow ? "3 por línea" : "Estándar")}) x{copies}"
                : $"Vista Previa - Etiqueta de Precio x{copies}",
            Width = threePerRow ? 780 : 480,
            Height = 560,
            WindowStartupLocation = owner != null ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen,
            Owner = owner,
            Background = Brushes.White
        };

        btnPrint.Click += (_, _) =>
        {
            var dlg = new PrintDialog();
            if (dlg.ShowDialog() == true)
            {
                var copyDoc = BuildLabelsDocument(product, copies, isBarcodeMode, threePerRow);
                copyDoc.PageHeight = dlg.PrintableAreaHeight;
                copyDoc.PageWidth = dlg.PrintableAreaWidth;
                copyDoc.ColumnWidth = dlg.PrintableAreaWidth;
                IDocumentPaginatorSource idp = copyDoc;
                dlg.PrintDocument(idp.DocumentPaginator, $"Etiquetas_{product.Codigo}");
            }
        };
        btnClose.Click += (_, _) => previewWin.Close();
        previewWin.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Escape)
                previewWin.Close();
        };

        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(12, 8, 12, 12)
        };
        footer.Children.Add(btnPrint);
        footer.Children.Add(btnClose);

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(viewer, 0);
        Grid.SetRow(footer, 1);
        root.Children.Add(viewer);
        root.Children.Add(footer);
        previewWin.Content = root;
        previewWin.ShowDialog();
    }
}
