using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using FirewallControl.App.Services;
using FirewallControl.Core.Entities;
using FirewallControl.Core.Interfaces;
using Microsoft.Win32;

namespace FirewallControl.App.Views;

public partial class ProductSubReportsWindow : Window
{
    private readonly IProductRepository _productRepository;
    private readonly IConfigService? _configService;
    private string _consultAz = "Todo";

    public ProductSubReportsWindow(IProductRepository productRepository, IConfigService? configService = null)
    {
        InitializeComponent();
        _productRepository = productRepository;
        _configService = configService;

        DpVencDesde.SelectedDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        DpVencHasta.SelectedDate = DateTime.Today.AddMonths(3);
        Loaded += async (_, _) => await LoadCatalogFiltersAsync();
    }

    public async void Configure(int initialTab)
    {
        SubReportTabs.SelectedIndex = Math.Clamp(initialTab, 0, 3);
        if (IsLoaded)
            await RunActiveTabQueryAsync();
    }

    private async Task LoadCatalogFiltersAsync()
    {
        try
        {
            var groups = new List<ProductGroupEntity> { new() { IdGrupoProducto = 0, Descripcion = "<Todos>" } };
            groups.AddRange(await _productRepository.GetProductGroupsAsync());
            CmbLpGrupo.ItemsSource = groups;
            CmbLpGrupo.SelectedIndex = 0;

            var subs = new List<ProductSubGroupEntity> { new() { IdSubGrupoProducto = 0, Descripcion = "<Todos>" } };
            subs.AddRange(await _productRepository.GetProductSubGroupsAsync());
            CmbLpSubGrupo.ItemsSource = subs;
            CmbLpSubGrupo.SelectedIndex = 0;

            var brands = new List<ProductBrandEntity> { new() { IdMarcaProducto = 0, Descripcion = "<Todas>" } };
            brands.AddRange(await _productRepository.GetProductBrandsAsync());
            CmbLpMarca.ItemsSource = brands;
            CmbLpMarca.SelectedIndex = 0;

            var models = new List<ProductModelEntity> { new() { IdModeloProducto = 0, Descripcion = "<Todos>" } };
            models.AddRange(await _productRepository.GetProductModelsAsync());
            CmbLpModelo.ItemsSource = models;
            CmbLpModelo.SelectedIndex = 0;

            var deps = await _productRepository.GetDepositsAsync();
            CmbStockDeposito.ItemsSource = deps;
            if (deps.Count > 0) CmbStockDeposito.SelectedIndex = 0;

            await RunActiveTabQueryAsync();
        }
        catch (Exception ex)
        {
            TxtStatus.Text = $"Error al cargar filtros: {ex.Message}";
        }
    }

    private Task RunActiveTabQueryAsync() => SubReportTabs.SelectedIndex switch
    {
        0 => QueryPriceListAsync(),
        1 => QueryStockByDepositAsync(),
        2 => QueryVencimientosAsync(),
        3 => QueryConsultaPreciosAsync(),
        _ => Task.CompletedTask
    };

    // =========================================================================
    // TAB 0: INFORME LISTA DE PRECIOS
    // =========================================================================
    private void LpColumns_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        ColLpCosto.Visibility = ChkLpCosto.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        ColLpP1.Visibility = ChkLpP1.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        ColLpP2.Visibility = ChkLpP2.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        ColLpP3.Visibility = ChkLpP3.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        ColLpP4.Visibility = ChkLpP4.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        ColLpP5.Visibility = ChkLpP5.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        ColLpP6.Visibility = ChkLpP6.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        ColLpP7.Visibility = ChkLpP7.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private async Task QueryPriceListAsync()
    {
        int? idGrp = CmbLpGrupo.SelectedValue is int g && g > 0 ? g : null;
        int? idSub = CmbLpSubGrupo.SelectedValue is int sg && sg > 0 ? sg : null;
        int? idMrc = CmbLpMarca.SelectedValue is int m && m > 0 ? m : null;
        int? idMod = CmbLpModelo.SelectedValue is int mo && mo > 0 ? mo : null;
        string ord = RbLpOrdCod.IsChecked == true ? "codigo" : "descripcion";

        var rows = await _productRepository.GetPriceListReportAsync(idGrp, idSub, idMrc, idMod, ord);
        GridListaPrecios.ItemsSource = rows;
        TxtStatus.Text = $"✓ Lista de precios: {rows.Count} productos.";
    }

    private async void BtnConsultarListaPrecios_Click(object sender, RoutedEventArgs e) => await QueryPriceListAsync();

    private void BtnExportListaPrecios_Click(object sender, RoutedEventArgs e)
    {
        if (GridListaPrecios.ItemsSource is not IEnumerable<ProductEntity> items) return;
        ExportCsv("InformeListaPrecios", sb =>
        {
            sb.AppendLine("Codigo;Descripcion;Grupo;Costo1;Precio1;Precio2;Precio3;Precio4;Precio5;Precio6;Precio7");
            foreach (var p in items)
            {
                sb.AppendLine($"{p.Codigo};\"{p.Descripcion}\";\"{p.GrupoDescripcion}\";{p.Costo1:0};{p.Precio1:0};{p.Precio2:0};{p.Precio3:0};{p.Precio4:0};{p.Precio5:0};{p.Precio6:0};{p.Precio7:0}");
            }
        });
    }

    private void BtnPrintListaPrecios_Click(object sender, RoutedEventArgs e)
    {
        if (GridListaPrecios.ItemsSource is not IEnumerable<ProductEntity> items) return;
        var doc = new FlowDocument { PagePadding = new Thickness(24), FontFamily = new FontFamily("Segoe UI"), Background = Brushes.White };
        doc.Blocks.Add(new Paragraph(new Run($"LISTA DE PRECIOS - {DateTime.Now:dd/MM/yyyy HH:mm}")) { FontSize = 15, FontWeight = FontWeights.Bold });

        var table = new Table { CellSpacing = 0 };
        var headers = new List<(string Name, Func<ProductEntity, string> Getter, double Width)>
        {
            ("Código", p => p.Codigo, 85),
            ("Descripción", p => p.Descripcion, 240)
        };
        if (ChkLpCosto.IsChecked == true) headers.Add(("Costo", p => p.Costo1.ToString("N0"), 72));
        if (ChkLpP1.IsChecked == true) headers.Add(("P1", p => p.Precio1.ToString("N0"), 72));
        if (ChkLpP2.IsChecked == true) headers.Add(("P2", p => p.Precio2.ToString("N0"), 72));
        if (ChkLpP3.IsChecked == true) headers.Add(("P3", p => p.Precio3.ToString("N0"), 72));
        if (ChkLpP4.IsChecked == true) headers.Add(("P4", p => p.Precio4.ToString("N0"), 72));
        if (ChkLpP5.IsChecked == true) headers.Add(("P5", p => p.Precio5.ToString("N0"), 72));
        if (ChkLpP6.IsChecked == true) headers.Add(("P6", p => p.Precio6.ToString("N0"), 72));
        if (ChkLpP7.IsChecked == true) headers.Add(("P7", p => p.Precio7.ToString("N0"), 72));

        foreach (var h in headers) table.Columns.Add(new TableColumn { Width = new GridLength(h.Width) });
        var rg = new TableRowGroup();
        table.RowGroups.Add(rg);

        var hRow = new TableRow { Background = Brushes.WhiteSmoke };
        foreach (var h in headers)
            hRow.Cells.Add(new TableCell(new Paragraph(new Run(h.Name)) { FontWeight = FontWeights.Bold, FontSize = 10, Margin = new Thickness(4) }));
        rg.Rows.Add(hRow);

        foreach (var item in items.Take(400))
        {
            var r = new TableRow();
            foreach (var h in headers)
                r.Cells.Add(new TableCell(new Paragraph(new Run(h.Getter(item))) { FontSize = 9.5, Margin = new Thickness(4, 2, 4, 2) }));
            rg.Rows.Add(r);
        }

        doc.Blocks.Add(table);
        ShowSimplePrintPreview("Vista Previa - Informe Lista de Precios", doc);
    }

    // =========================================================================
    // TAB 1: STOCK POR DEPÓSITOS
    // =========================================================================
    private void ChkTodosDepositos_Changed(object sender, RoutedEventArgs e)
    {
        if (CmbStockDeposito != null)
            CmbStockDeposito.IsEnabled = ChkTodosDepositos.IsChecked != true;
    }

    private async Task QueryStockByDepositAsync()
    {
        int? idDep = ChkTodosDepositos.IsChecked == true
            ? null
            : (CmbStockDeposito.SelectedValue is int d && d > 0 ? d : null);
        bool onlyPos = ChkSoloStockPositivo.IsChecked == true;
        string ord = RbSdOrdCod.IsChecked == true ? "codigo" : "descripcion";

        var rows = await _productRepository.GetStockByDepositsReportAsync(idDep, onlyPos, ord);
        GridStockDeposito.ItemsSource = rows;

        decimal totalUnits = rows.Sum(r => r.Cantidad);
        decimal totalCost = rows.Sum(r => r.ValorCosto);
        decimal totalSale = rows.Sum(r => r.ValorVenta);
        TxtStockTotals.Text = $"Registros: {rows.Count}   |   Unidades totales: {totalUnits:N2}   |   Valorizado Costo: {totalCost:N0} Gs.   |   Valorizado Venta: {totalSale:N0} Gs.";
        TxtStatus.Text = $"✓ Stock por depósito consultado: {rows.Count} filas.";
    }

    private async void BtnConsultarStockDeposito_Click(object sender, RoutedEventArgs e) => await QueryStockByDepositAsync();

    private void BtnExportStockDeposito_Click(object sender, RoutedEventArgs e)
    {
        if (GridStockDeposito.ItemsSource is not IEnumerable<ProductDepositStockRow> items) return;
        ExportCsv("StockPorDeposito", sb =>
        {
            sb.AppendLine("Deposito;Codigo;Producto;Cantidad;CostoMedio;Precio1;ValorCosto;ValorVenta");
            foreach (var r in items)
            {
                sb.AppendLine($"\"{r.DesDeposito}\";{r.Codigo};\"{r.DesProducto}\";{r.Cantidad:0.##};{r.CostoMedio:0};{r.Precio1:0};{r.ValorCosto:0};{r.ValorVenta:0}");
            }
        });
    }

    private void BtnPrintStockDeposito_Click(object sender, RoutedEventArgs e)
    {
        if (GridStockDeposito.ItemsSource is not IEnumerable<ProductDepositStockRow> items) return;
        var doc = new FlowDocument { PagePadding = new Thickness(24), FontFamily = new FontFamily("Segoe UI"), Background = Brushes.White };
        doc.Blocks.Add(new Paragraph(new Run($"STOCK POR DEPÓSITOS - {DateTime.Now:dd/MM/yyyy HH:mm}")) { FontSize = 15, FontWeight = FontWeights.Bold });
        doc.Blocks.Add(new Paragraph(new Run(TxtStockTotals.Text)) { FontSize = 10, Margin = new Thickness(0, 0, 0, 10) });

        var table = new Table { CellSpacing = 0 };
        table.Columns.Add(new TableColumn { Width = new GridLength(130) });
        table.Columns.Add(new TableColumn { Width = new GridLength(85) });
        table.Columns.Add(new TableColumn { Width = new GridLength(240) });
        table.Columns.Add(new TableColumn { Width = new GridLength(75) });
        table.Columns.Add(new TableColumn { Width = new GridLength(95) });
        var rg = new TableRowGroup();
        table.RowGroups.Add(rg);

        var hRow = new TableRow { Background = Brushes.WhiteSmoke };
        foreach (string h in new[] { "Depósito", "Código", "Producto", "Cant.", "Valor Venta" })
            hRow.Cells.Add(new TableCell(new Paragraph(new Run(h)) { FontWeight = FontWeights.Bold, FontSize = 10, Margin = new Thickness(4) }));
        rg.Rows.Add(hRow);

        foreach (var r in items.Take(400))
        {
            var tr = new TableRow();
            foreach (string v in new[] { r.DesDeposito, r.Codigo, r.DesProducto, r.Cantidad.ToString("N2"), r.ValorVenta.ToString("N0") })
                tr.Cells.Add(new TableCell(new Paragraph(new Run(v)) { FontSize = 9.5, Margin = new Thickness(4, 2, 4, 2) }));
            rg.Rows.Add(tr);
        }

        doc.Blocks.Add(table);
        ShowSimplePrintPreview("Vista Previa - Stock por Depósitos", doc);
    }

    // =========================================================================
    // TAB 2: VENCIMIENTOS
    // =========================================================================
    private async Task QueryVencimientosAsync()
    {
        var desde = DpVencDesde.SelectedDate ?? DateTime.Today.AddMonths(-1);
        var hasta = DpVencHasta.SelectedDate ?? DateTime.Today.AddMonths(3);
        var rows = await _productRepository.GetExpiringLotsReportAsync(desde, hasta);
        GridVencimientos.ItemsSource = rows;
        TxtStatus.Text = $"✓ Lotes / vencimientos encontrados: {rows.Count}.";
    }

    private async void BtnConsultarVencimientos_Click(object sender, RoutedEventArgs e) => await QueryVencimientosAsync();

    private void BtnExportVencimientos_Click(object sender, RoutedEventArgs e)
    {
        if (GridVencimientos.ItemsSource is not IEnumerable<ProductLotEntity> items) return;
        ExportCsv("VencimientoProductos", sb =>
        {
            sb.AppendLine("Codigo;Descripcion;Lote;Vencimiento;Cantidad;IdDeposito");
            foreach (var r in items)
                sb.AppendLine($"{r.Codigo};\"{r.Descripcion}\";{r.Lote};{r.Vencimiento:yyyy-MM-dd};{r.Cantidad:0.##};{r.IdDeposito}");
        });
    }

    // =========================================================================
    // TAB 3: CONSULTA P1..P7 + DETALLE SOLO LECTURA
    // =========================================================================
    private async Task QueryConsultaPreciosAsync()
    {
        var rows = await _productRepository.SearchProductsAdvancedAsync(new ProductSearchFilter
        {
            AzTab = _consultAz,
            SearchText = TxtConsultSearch.Text,
            Limit = 300
        });
        GridConsultaPrecios.ItemsSource = rows;
        if (rows.Count > 0) GridConsultaPrecios.SelectedIndex = 0;
        TxtStatus.Text = $"✓ Consulta de precios: {rows.Count} productos.";
    }

    private async void ConsultAz_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded || sender is not RadioButton rb) return;
        _consultAz = rb.Tag?.ToString() ?? "Todo";
        await QueryConsultaPreciosAsync();
    }

    private async void BtnConsultSearch_Click(object sender, RoutedEventArgs e) => await QueryConsultaPreciosAsync();

    private async void TxtConsultSearch_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter || e.Key == Key.Return)
        {
            e.Handled = true;
            await QueryConsultaPreciosAsync();
        }
    }

    private void GridConsultaPrecios_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GridConsultaPrecios.SelectedItem is not ProductEntity p) return;
        TxtDetTitulo.Text = $"[{p.Codigo}] {p.Descripcion}\nGrupo: {p.GrupoDescripcion} | Stock: {p.StockActual:N2}";
        TxtDetPreciosCol1.Text =
            $"Precio 1: {p.Precio1:N0} Gs.\nPrecio 2: {p.Precio2:N0} Gs.\nPrecio 3: {p.Precio3:N0} Gs.\nPrecio 4: {p.Precio4:N0} Gs.";
        TxtDetPreciosCol2.Text =
            $"Precio 5: {p.Precio5:N0} Gs.\nPrecio 6: {p.Precio6:N0} Gs.\nPrecio 7: {p.Precio7:N0} Gs.\nIVA: {p.IvaDescripcion}";
        TxtDetAplicacion.Text = p.Aplicacion;
        TxtDetObservacion.Text = p.Observacion;

        string? photo = ProductPhotoHelper.ResolvePhotoFilePath(p.Imagen, _configService);
        ImgDetFoto.Source = ProductPhotoHelper.LoadBitmapSafe(photo);
    }

    // =========================================================================
    // HELPERS
    // =========================================================================
    private void ExportCsv(string prefix, Action<StringBuilder> writeRows)
    {
        var sfd = new SaveFileDialog
        {
            Title = "Exportar CSV",
            Filter = "Archivo CSV (*.csv)|*.csv",
            FileName = $"{prefix}_{DateTime.Now:yyyyMMdd_HHmm}.csv"
        };
        if (sfd.ShowDialog() != true) return;

        var sb = new StringBuilder();
        writeRows(sb);
        File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
        TxtStatus.Text = $"✓ Archivo exportado: {sfd.FileName}";
    }

    private void ShowSimplePrintPreview(string title, FlowDocument doc)
    {
        var viewer = new FlowDocumentScrollViewer { Document = doc };
        var btnPrint = new Button { Content = "🖨 Imprimir", Padding = new Thickness(14, 7, 14, 7), Margin = new Thickness(0, 0, 8, 0) };
        btnPrint.Click += (_, _) =>
        {
            var pd = new PrintDialog();
            if (pd.ShowDialog() == true)
            {
                doc.PageHeight = pd.PrintableAreaHeight;
                doc.PageWidth = pd.PrintableAreaWidth;
                doc.ColumnWidth = pd.PrintableAreaWidth;
                pd.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator, title);
            }
        };
        var btnClose = new Button { Content = "Cerrar [ESC]", Padding = new Thickness(14, 7, 14, 7) };

        var win = new Window
        {
            Title = title,
            Width = 900,
            Height = 640,
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = Brushes.White
        };
        btnClose.Click += (_, _) => win.Close();

        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(12) };
        footer.Children.Add(btnPrint);
        footer.Children.Add(btnClose);

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(viewer, 0);
        Grid.SetRow(footer, 1);
        grid.Children.Add(viewer);
        grid.Children.Add(footer);
        win.Content = grid;
        win.ShowDialog();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
        else if (e.Key == Key.F5)
        {
            e.Handled = true;
            _ = RunActiveTabQueryAsync();
        }
    }
}
