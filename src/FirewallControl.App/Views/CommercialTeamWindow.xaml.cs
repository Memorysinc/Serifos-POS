using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using FirewallControl.Core.Entities;
using FirewallControl.Core.Interfaces;

namespace FirewallControl.App.Views;

/// <summary>
/// Unifies abm_vendedor, abm_supervisor, abm_cobrador, abm_metas, abm_tablametas, crear_recibo, and abm_comisionproveedor.
/// </summary>
public partial class CommercialTeamWindow : Window
{
    private readonly IStage3Repository _stage3Repository;
    private readonly ISystemRepository _systemRepository;
    private readonly IPurchaseRepository _purchaseRepository;
    private readonly ICashRepository _cashRepository;

    private SellerEntity _editingVen = new();
    private SupervisorEntity _editingSup = new();
    private CollectorEntity _editingCob = new();
    private SalesGoalEntity _editingMeta = new();
    private SalesGoalTableEntity _editingTm = new();
    private SupplierCommissionEntity _editingCp = new();

    public CommercialTeamWindow(
        IStage3Repository stage3Repository,
        ISystemRepository systemRepository,
        IPurchaseRepository purchaseRepository,
        ICashRepository cashRepository)
    {
        InitializeComponent();
        _stage3Repository = stage3Repository;
        _systemRepository = systemRepository;
        _purchaseRepository = purchaseRepository;
        _cashRepository = cashRepository;
        DpMetaFecha.SelectedDate = DateTime.Today;
        Loaded += async (_, _) => await LoadAllAsync();
    }

    public void Configure(int initialTab)
    {
        MainTabs.SelectedIndex = Math.Clamp(initialTab, 0, 4);
    }

    private async Task LoadAllAsync()
    {
        try
        {
            var sellers = await _systemRepository.GetSellersAsync();
            GridVendedores.ItemsSource = sellers;
            CmbCpVendedor.ItemsSource = sellers;
            if (sellers.Count > 0 && CmbCpVendedor.SelectedValue == null) CmbCpVendedor.SelectedIndex = 0;

            GridSupervisores.ItemsSource = await _stage3Repository.GetAllSupervisorsAsync();
            GridCobradores.ItemsSource = await _stage3Repository.GetCollectorsAsync();
            GridMetas.ItemsSource = await _stage3Repository.GetSalesGoalsAsync();
            GridTablaMetas.ItemsSource = await _stage3Repository.GetSalesGoalTableAsync();
            GridComProv.ItemsSource = await _stage3Repository.GetSupplierCommissionsAsync();
            GridRecibos.ItemsSource = await _stage3Repository.GetReceiptsAdvancesAsync(DateTime.Today.AddMonths(-3), DateTime.Today);

            var suppliers = await _purchaseRepository.SearchSuppliersAsync("", 300);
            CmbCpProveedor.ItemsSource = suppliers;
            if (suppliers.Count > 0 && CmbCpProveedor.SelectedValue == null) CmbCpProveedor.SelectedIndex = 0;

            var cajas = await _cashRepository.GetCashRegistersAsync();
            CmbRecCaja.ItemsSource = cajas;
            if (cajas.Count > 0 && CmbRecCaja.SelectedValue == null) CmbRecCaja.SelectedIndex = 0;

            LblStatus.Text = "Equipo comercial y metas cargados.";
        }
        catch (Exception ex)
        {
            LblStatus.Text = $"Error: {ex.Message}";
        }
    }

    private static decimal ParseDec(string s, decimal fallback = 0m) =>
        decimal.TryParse(s.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal d) ? d : fallback;

    private static int ParseInt(string s, int fallback = 0) => int.TryParse(s, out int n) ? n : fallback;

    // --- VENDEDORES ---
    private void GridVendedores_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GridVendedores.SelectedItem is not SellerEntity v) return;
        _editingVen = v;
        TxtVenNombres.Text = v.Nombres;
        TxtVenDoc.Text = v.Documento;
        TxtVenRuc.Text = v.Ruc;
        TxtVenTel.Text = v.Telefonos;
        TxtVenCel.Text = v.Celular;
        TxtVenDir.Text = v.Direccion;
        TxtVenComision.Text = v.Comision.ToString("0.##", CultureInfo.InvariantCulture);
        TxtVenDescuento.Text = v.Descuento.ToString("0.##", CultureInfo.InvariantCulture);
        TxtVenTipo.Text = v.TipoVendedor.ToString();
        ChkVenActivo.IsChecked = v.Estado == 1;
    }

    private void BtnNuevoVen_Click(object sender, RoutedEventArgs e)
    {
        _editingVen = new SellerEntity();
        GridVendedores.SelectedItem = null;
        TxtVenNombres.Clear(); TxtVenDoc.Clear(); TxtVenRuc.Clear(); TxtVenTel.Clear(); TxtVenCel.Clear(); TxtVenDir.Clear();
        TxtVenComision.Text = "0"; TxtVenDescuento.Text = "0"; TxtVenTipo.Text = "0"; ChkVenActivo.IsChecked = true;
    }

    private async void BtnGuardarVen_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtVenNombres.Text)) return;
        _editingVen.Nombres = TxtVenNombres.Text.Trim();
        _editingVen.Documento = TxtVenDoc.Text.Trim();
        _editingVen.Ruc = TxtVenRuc.Text.Trim();
        _editingVen.Telefonos = TxtVenTel.Text.Trim();
        _editingVen.Celular = TxtVenCel.Text.Trim();
        _editingVen.Direccion = TxtVenDir.Text.Trim();
        _editingVen.Comision = ParseDec(TxtVenComision.Text);
        _editingVen.Descuento = ParseDec(TxtVenDescuento.Text);
        _editingVen.TipoVendedor = ParseInt(TxtVenTipo.Text);
        _editingVen.Estado = ChkVenActivo.IsChecked == true ? 1 : 0;

        await _systemRepository.SaveSellerAsync(_editingVen, _editingVen.IdVendedor > 0 ? "M" : "A");
        await LoadAllAsync();
    }

    private async void BtnEliminarVen_Click(object sender, RoutedEventArgs e)
    {
        if (_editingVen.IdVendedor <= 0) return;
        if (MessageBox.Show($"¿Eliminar vendedor '{_editingVen.Nombres}'?", "Confirmar", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        await _stage3Repository.DeleteSellerAsync(_editingVen.IdVendedor);
        BtnNuevoVen_Click(sender, e);
        await LoadAllAsync();
    }

    // --- SUPERVISORES ---
    private void GridSupervisores_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GridSupervisores.SelectedItem is not SupervisorEntity s) return;
        _editingSup = s;
        TxtSupNombre.Text = s.Nombres;
        ChkSupActivo.IsChecked = s.Estado == 1;
    }

    private void BtnNuevoSup_Click(object sender, RoutedEventArgs e)
    {
        _editingSup = new SupervisorEntity();
        GridSupervisores.SelectedItem = null;
        TxtSupNombre.Clear(); TxtSupDoc.Clear(); TxtSupTel.Clear(); TxtSupCel.Clear(); TxtSupDir.Clear();
        TxtSupComision.Text = "0"; ChkSupActivo.IsChecked = true;
    }

    private async void BtnGuardarSup_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtSupNombre.Text)) return;
        await _stage3Repository.SaveSupervisorAsync(
            _editingSup,
            TxtSupNombre.Text.Trim(),
            TxtSupDoc.Text.Trim(),
            TxtSupTel.Text.Trim(),
            TxtSupCel.Text.Trim(),
            TxtSupDir.Text.Trim(),
            ParseDec(TxtSupComision.Text),
            ChkSupActivo.IsChecked == true ? 1 : 0);
        await LoadAllAsync();
    }

    private async void BtnEliminarSup_Click(object sender, RoutedEventArgs e)
    {
        if (_editingSup.IdSupervisor <= 0) return;
        if (MessageBox.Show($"¿Eliminar supervisor '{_editingSup.Nombres}'?", "Confirmar", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        await _stage3Repository.DeleteSupervisorAsync(_editingSup.IdSupervisor);
        BtnNuevoSup_Click(sender, e);
        await LoadAllAsync();
    }

    // --- COBRADORES ---
    private void GridCobradores_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GridCobradores.SelectedItem is not CollectorEntity c) return;
        _editingCob = c;
        TxtCobNombres.Text = c.Nombres;
        TxtCobDoc.Text = c.Documento;
        TxtCobCel.Text = c.Celular;
        TxtCobDir.Text = c.Direccion;
        TxtCobComision.Text = c.Comision.ToString("0.##", CultureInfo.InvariantCulture);
        TxtCobFija.Text = c.ComisionFija.ToString("0.##", CultureInfo.InvariantCulture);
        ChkCobActivo.IsChecked = c.Estado == 1;
    }

    private void BtnNuevoCob_Click(object sender, RoutedEventArgs e)
    {
        _editingCob = new CollectorEntity();
        GridCobradores.SelectedItem = null;
        TxtCobNombres.Clear(); TxtCobDoc.Clear(); TxtCobCel.Clear(); TxtCobDir.Clear();
        TxtCobComision.Text = "0"; TxtCobFija.Text = "0"; ChkCobActivo.IsChecked = true;
    }

    private async void BtnGuardarCob_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtCobNombres.Text)) return;
        _editingCob.Nombres = TxtCobNombres.Text.Trim();
        _editingCob.Documento = TxtCobDoc.Text.Trim();
        _editingCob.Celular = TxtCobCel.Text.Trim();
        _editingCob.Direccion = TxtCobDir.Text.Trim();
        _editingCob.Comision = ParseDec(TxtCobComision.Text);
        _editingCob.ComisionFija = ParseDec(TxtCobFija.Text);
        _editingCob.Estado = ChkCobActivo.IsChecked == true ? 1 : 0;
        await _stage3Repository.SaveCollectorAsync(_editingCob, _editingCob.IdCobrador > 0 ? "M" : "A");
        await LoadAllAsync();
    }

    private async void BtnEliminarCob_Click(object sender, RoutedEventArgs e)
    {
        if (_editingCob.IdCobrador <= 0) return;
        if (MessageBox.Show($"¿Eliminar cobrador '{_editingCob.Nombres}'?", "Confirmar", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        await _stage3Repository.DeleteCollectorAsync(_editingCob.IdCobrador);
        BtnNuevoCob_Click(sender, e);
        await LoadAllAsync();
    }

    // --- METAS & TABLA DE METAS ---
    private void GridMetas_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GridMetas.SelectedItem is not SalesGoalEntity m) return;
        _editingMeta = m;
        DpMetaFecha.SelectedDate = m.Fecha;
        TxtMetaDesde.Text = m.Desde.ToString("0", CultureInfo.InvariantCulture);
        TxtMetaHasta.Text = m.Hasta.ToString("0", CultureInfo.InvariantCulture);
    }

    private void BtnNuevoMeta_Click(object sender, RoutedEventArgs e)
    {
        _editingMeta = new SalesGoalEntity();
        GridMetas.SelectedItem = null;
        DpMetaFecha.SelectedDate = DateTime.Today;
        TxtMetaDesde.Text = "0"; TxtMetaHasta.Text = "0";
    }

    private async void BtnGuardarMeta_Click(object sender, RoutedEventArgs e)
    {
        _editingMeta.Fecha = DpMetaFecha.SelectedDate ?? DateTime.Today;
        _editingMeta.Desde = ParseDec(TxtMetaDesde.Text);
        _editingMeta.Hasta = ParseDec(TxtMetaHasta.Text);
        await _stage3Repository.SaveSalesGoalAsync(_editingMeta, _editingMeta.IdMeta > 0 ? "M" : "A");
        await LoadAllAsync();
    }

    private async void BtnEliminarMeta_Click(object sender, RoutedEventArgs e)
    {
        if (_editingMeta.IdMeta <= 0) return;
        await _stage3Repository.DeleteSalesGoalAsync(_editingMeta.IdMeta);
        BtnNuevoMeta_Click(sender, e);
        await LoadAllAsync();
    }

    private void GridTablaMetas_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GridTablaMetas.SelectedItem is not SalesGoalTableEntity t) return;
        _editingTm = t;
        TxtTmDesde.Text = t.Desde.ToString("0", CultureInfo.InvariantCulture);
        TxtTmOp.Text = t.Operacion;
        TxtTmHasta.Text = t.Hasta.ToString("0", CultureInfo.InvariantCulture);
        TxtTmPorc.Text = t.Porcentaje.ToString("0.##", CultureInfo.InvariantCulture);
        TxtTmMonto.Text = t.Monto.ToString("0", CultureInfo.InvariantCulture);
    }

    private void BtnNuevoTm_Click(object sender, RoutedEventArgs e)
    {
        _editingTm = new SalesGoalTableEntity();
        GridTablaMetas.SelectedItem = null;
        TxtTmDesde.Text = "0"; TxtTmOp.Text = ">="; TxtTmHasta.Text = "0"; TxtTmPorc.Text = "0"; TxtTmMonto.Text = "0";
    }

    private async void BtnGuardarTm_Click(object sender, RoutedEventArgs e)
    {
        _editingTm.Desde = ParseDec(TxtTmDesde.Text);
        _editingTm.Operacion = string.IsNullOrWhiteSpace(TxtTmOp.Text) ? ">=" : TxtTmOp.Text.Trim();
        _editingTm.Hasta = ParseDec(TxtTmHasta.Text);
        _editingTm.Porcentaje = ParseDec(TxtTmPorc.Text);
        _editingTm.Monto = ParseDec(TxtTmMonto.Text);
        await _stage3Repository.SaveSalesGoalRowAsync(_editingTm, _editingTm.IdTablaMeta > 0 ? "M" : "A");
        await LoadAllAsync();
    }

    private async void BtnEliminarTm_Click(object sender, RoutedEventArgs e)
    {
        if (_editingTm.IdTablaMeta <= 0) return;
        await _stage3Repository.DeleteSalesGoalRowAsync(_editingTm.IdTablaMeta);
        BtnNuevoTm_Click(sender, e);
        await LoadAllAsync();
    }

    // --- ADELANTOS / RECIBOS Y COMISIONES ---
    private async void BtnGuardarRecibo_Click(object sender, RoutedEventArgs e)
    {
        decimal imp = ParseDec(TxtRecImporte.Text);
        if (string.IsNullOrWhiteSpace(TxtRecDest.Text) || imp <= 0)
        {
            MessageBox.Show("Ingrese destinatario e importe válido.", "Adelanto / Recibo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var rec = new ReceiptAdvanceEntity
        {
            Letra = "A",
            Recibo = string.IsNullOrWhiteSpace(TxtRecNro.Text) ? DateTime.Now.ToString("HHmmss") : TxtRecNro.Text.Trim(),
            Fecha = DateTime.Today,
            Destinatario = TxtRecDest.Text.Trim(),
            Importe = imp,
            Concepto = TxtRecConcepto.Text.Trim(),
            IdCaja = CmbRecCaja.SelectedValue is int c ? c : 1,
            Estado = 1
        };
        await _stage3Repository.SaveReceiptAdvanceAsync(rec);
        TxtRecDest.Clear(); TxtRecImporte.Clear(); TxtRecConcepto.Clear();
        await LoadAllAsync();
        LblStatus.Text = $"Recibo/Adelanto por {imp:N0} Gs. guardado.";
    }

    private void GridComProv_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GridComProv.SelectedItem is not SupplierCommissionEntity cp) return;
        _editingCp = cp;
        CmbCpProveedor.SelectedValue = cp.IdProveedor;
        CmbCpVendedor.SelectedValue = cp.IdVendedor;
        TxtCpComision.Text = cp.Comision.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private async void BtnGuardarCp_Click(object sender, RoutedEventArgs e)
    {
        _editingCp.IdProveedor = CmbCpProveedor.SelectedValue is int p ? p : 1;
        _editingCp.IdVendedor = CmbCpVendedor.SelectedValue is int v ? v : 10;
        _editingCp.Comision = ParseDec(TxtCpComision.Text);
        await _stage3Repository.SaveSupplierCommissionAsync(_editingCp, _editingCp.IdComision > 0 ? "M" : "A");
        await LoadAllAsync();
    }

    private async void BtnEliminarCp_Click(object sender, RoutedEventArgs e)
    {
        if (_editingCp.IdComision <= 0) return;
        await _stage3Repository.DeleteSupplierCommissionAsync(_editingCp.IdComision);
        _editingCp = new SupplierCommissionEntity();
        await LoadAllAsync();
    }
}
