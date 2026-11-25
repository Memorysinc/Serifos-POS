using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using FirewallControl.App.Services;
using FirewallControl.Core.Entities;
using FirewallControl.Core.Interfaces;

namespace FirewallControl.App.Views;

/// <summary>
/// Unifies Restaurant (comandas/mesas/mozos), conditional Veterinaria, and Entrega de Mercaderías (abm_entrega_mercaderia).
/// </summary>
public partial class SpecialtyModulesWindow : Window
{
    private readonly IStage3Repository _stage3Repository;
    private readonly ISystemRepository _systemRepository;
    private readonly ISalesRepository _salesRepository;
    private readonly IWindowManagerService? _windowManager;
    private SaleEntity? _selectedSale;

    public SpecialtyModulesWindow(
        IStage3Repository stage3Repository,
        ISystemRepository systemRepository,
        ISalesRepository salesRepository,
        IWindowManagerService? windowManager = null)
    {
        InitializeComponent();
        _stage3Repository = stage3Repository;
        _systemRepository = systemRepository;
        _salesRepository = salesRepository;
        _windowManager = windowManager;
        Loaded += async (_, _) => await LoadAllAsync();
    }

    public void Configure(int initialTab, string? subSection = null)
    {
        MainTabs.SelectedIndex = Math.Clamp(initialTab, 0, 2);
        if (!string.IsNullOrWhiteSpace(subSection))
        {
            if (MainTabs.SelectedIndex == 0)
                LblRestSub.Text = $"Monitor Restaurant - Sección: {subSection}";
            else if (MainTabs.SelectedIndex == 1)
                LblVetSection.Text = $"Sección Veterinaria Activa: {subSection}";
        }
    }

    private async Task LoadAllAsync()
    {
        try
        {
            var company = await _systemRepository.GetCompanyDataAsync();
            LblRestMode.Text = company.Restaurant == 1 ? "Modo Restaurant: ACTIVO" : "Modo Restaurant: Desactivado en Empresa";
            LblVetStatus.Text = company.Veterinaria == 1
                ? "Modo Veterinaria: ACTIVO en configuración de Empresa"
                : "Modo Veterinaria: Desactivado en Empresa (habilitar en Preferencias -> Empresa)";

            await RefreshRestaurantAsync();
            GridMozos.ItemsSource = await _systemRepository.GetSellersAsync();

            var vetChecks = new List<VetTableRow>();
            foreach (var (modulo, tbl) in new[]
            {
                ("Pacientes", "pacientes"),
                ("Peluquería", "peluqueria"),
                ("Vacunas / Avisos", "vacunas"),
                ("Guardería", "guarderia"),
                ("Dosis Alimentos", "alimentos"),
                ("Valores Normales", "val_nor")
            })
            {
                bool exists = await _stage3Repository.TableExistsAsync(tbl);
                vetChecks.Add(new VetTableRow(modulo, tbl, exists ? "✔ Tabla disponible en MySQL" : "— Esquema opcional no instalado"));
            }
            GridVetTables.ItemsSource = vetChecks;

            await RefreshEntregaAsync();
            LblStatus.Text = "Módulos sectoriales y entrega cargados.";
        }
        catch (Exception ex)
        {
            LblStatus.Text = $"Error al cargar módulos: {ex.Message}";
        }
    }

    private async Task RefreshRestaurantAsync()
    {
        string? sector = (CmbRestSector.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        if (string.IsNullOrWhiteSpace(sector)) sector = null;
        GridComandas.ItemsSource = await _stage3Repository.GetRestaurantOrdersBySectorAsync(sector);
    }

    private async void BtnRefreshRest_Click(object sender, RoutedEventArgs e) => await RefreshRestaurantAsync();

    private void BtnAbrirAgendaVet_Click(object sender, RoutedEventArgs e)
    {
        _windowManager?.OpenAgendaAndOrganizerWindow(0);
    }

    private async Task RefreshEntregaAsync()
    {
        GridEntVentas.ItemsSource = await _salesRepository.GetRecentSalesAsync(DateTime.Today.AddDays(-7), DateTime.Today, 100);
    }

    private async void BtnRefreshEntrega_Click(object sender, RoutedEventArgs e) => await RefreshEntregaAsync();

    private async void GridEntVentas_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GridEntVentas.SelectedItem is not SaleEntity s) return;
        _selectedSale = await _salesRepository.GetSaleByIdAsync(s.IdVenta) ?? s;
        LblEntVentaInfo.Text = $"Venta #{_selectedSale.NroVenta} - {_selectedSale.NombreCliente} ({_selectedSale.TotalVenta:N0} Gs.)";
        GridEntItems.ItemsSource = _selectedSale.Items;
    }

    private void BtnImprimirEntrega_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSale == null) return;
        var doc = new FlowDocument
        {
            PagePadding = new Thickness(42),
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
            FontSize = 12
        };
        doc.Blocks.Add(new Paragraph(new Run("COMPROBANTE DE ENTREGA DE MERCADERÍAS")) { FontSize = 16, FontWeight = FontWeights.Bold });
        doc.Blocks.Add(new Paragraph(new Run($"Venta N°: {_selectedSale.NroVenta}  |  Fecha: {_selectedSale.FechaVenta:dd/MM/yyyy}  |  Cliente: {_selectedSale.NombreCliente}")));
        foreach (var it in _selectedSale.Items)
            doc.Blocks.Add(new Paragraph(new Run($"• [   ] {it.Cantidad:N2} x {it.Codigo} - {it.Descripcion}")));
        doc.Blocks.Add(new Paragraph(new Run("\nRecibido conforme por: ____________________________   C.I.: ______________")));

        var dlg = new PrintDialog();
        if (dlg.ShowDialog() == true)
            dlg.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator, $"Entrega_Venta_{_selectedSale.NroVenta}");
    }

    private async void BtnConfirmarEntrega_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSale == null) return;
        string user = _windowManager?.CurrentUser?.Cidusuario ?? "ADMINISTRA";
        await _systemRepository.LogUserTransactionAsync(
            "abm_entrega_mercaderia",
            "ventas",
            "ENTREGA",
            $"Entrega confirmada Venta #{_selectedSale.NroVenta} ({_selectedSale.Items.Count} ítems)",
            _selectedSale.IdVenta,
            user);
        LblStatus.Text = $"Entrega confirmada y auditada para Venta #{_selectedSale.NroVenta}.";
    }

    private sealed record VetTableRow(string Modulo, string Tabla, string Estado);
}
