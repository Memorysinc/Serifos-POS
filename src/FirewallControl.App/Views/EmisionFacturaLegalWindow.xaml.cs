using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using FirewallControl.Core.Entities;
using FirewallControl.Core.Interfaces;

namespace FirewallControl.App.Views;

/// <summary>
/// Replicates emision_factura_legal_laser.scx: numeración N1-N2-N3, registro en factura_legal,
/// actualización de timbradolocal e impresión fiscal.
/// </summary>
public partial class EmisionFacturaLegalWindow : Window
{
    private readonly ISalesRepository _salesRepository;
    private readonly ISystemRepository _systemRepository;
    private readonly ITicketPrintService _ticketPrintService;
    private readonly SaleEntity _sale;
    private CompanyDataEntity _company = new();

    public EmisionFacturaLegalWindow(
        ISalesRepository salesRepository,
        ISystemRepository systemRepository,
        ITicketPrintService ticketPrintService,
        SaleEntity? currentSale = null)
    {
        InitializeComponent();
        _salesRepository = salesRepository;
        _systemRepository = systemRepository;
        _ticketPrintService = ticketPrintService;
        _sale = currentSale ?? new SaleEntity();

        Loaded += async (_, _) => await InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            _company = await _systemRepository.GetCompanyDataAsync();
            var tim = await _salesRepository.GetTimbradoLocalAsync(
                _sale.IdSucursal > 0 ? _sale.IdSucursal : 1,
                _sale.IdCaja > 0 ? _sale.IdCaja : 1);

            TxtN1.Text = tim.N1.ToString("0");
            TxtN2.Text = tim.N2.ToString("0");
            TxtN3.Text = tim.N3.ToString("0");
            TxtTimbrado.Text = !string.IsNullOrWhiteSpace(_sale.Timbrado) ? _sale.Timbrado : tim.Timbrado;
            TxtClienteInfo.Text = $"{_sale.NombreCliente} (RUC: {_sale.RucCliente})";

            RefreshPreview();
        }
        catch (Exception ex)
        {
            TxtPreview.Text = $"Error al cargar timbrado: {ex.Message}";
        }
    }

    private void RefreshPreview()
    {
        int n1 = int.TryParse(TxtN1.Text, out int v1) ? v1 : 1;
        int n2 = int.TryParse(TxtN2.Text, out int v2) ? v2 : 1;
        int n3 = int.TryParse(TxtN3.Text, out int v3) ? v3 : 1;

        _sale.NroFactura = $"{n1:D3}-{n2:D3}-{n3:D7}";
        _sale.Timbrado = TxtTimbrado.Text.Trim();
        _sale.TipoBoleta = 2;

        TxtPreview.Text = _ticketPrintService.FormatLegalInvoice(_sale, _company);
    }

    private void BtnRefreshPreview_Click(object sender, RoutedEventArgs e) => RefreshPreview();

    private async void BtnPrintLegal_Click(object sender, RoutedEventArgs e)
    {
        RefreshPreview();
        int n1 = int.TryParse(TxtN1.Text, out int v1) ? v1 : 1;
        int n2 = int.TryParse(TxtN2.Text, out int v2) ? v2 : 1;
        int n3 = int.TryParse(TxtN3.Text, out int v3) ? v3 : 1;

        try
        {
            await _salesRepository.RegisterLegalInvoiceRecordAsync(
                n1, n2, n3,
                _sale.IdCliente > 0 ? _sale.IdCliente : 132,
                _sale.IdVenta,
                _sale.IdSucursal > 0 ? _sale.IdSucursal : 1,
                _sale.IdCaja > 0 ? _sale.IdCaja : 1,
                ChkAvanzarNumeracion.IsChecked == true);

            await _ticketPrintService.PrintSaleAsync(_sale, _company);
            MessageBox.Show(this, $"Factura Legal {_sale.NroFactura} emitida y registrada.", "Factura Legal", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Error al registrar factura legal:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
    }
}
