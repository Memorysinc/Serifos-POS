using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FirewallControl.Core.Entities;
using FirewallControl.Core.Interfaces;

namespace FirewallControl.App.Views;

/// <summary>
/// Replicates crear_clientes.scx and crear_cobroclientes.scx.
/// </summary>
public partial class CustomersManagementWindow : Window
{
    private readonly ICustomerRepository _customerRepository;
    private Action<CustomerEntity>? _onCustomerSelected;
    private CustomerEntity _editingCustomer = new();
    private string _lcTipo = "A";

    public CustomersManagementWindow(ICustomerRepository customerRepository)
    {
        InitializeComponent();
        _customerRepository = customerRepository;

        Loaded += async (_, _) =>
        {
            await LoadCustomersAsync();
            await LoadCollectionsAsync();
        };
    }

    public void Configure(int initialTab, Action<CustomerEntity>? onCustomerSelected)
    {
        MainTabs.SelectedIndex = Math.Clamp(initialTab, 0, 1);
        _onCustomerSelected = onCustomerSelected;
    }

    private async Task LoadCustomersAsync()
    {
        try
        {
            var list = await _customerRepository.SearchCustomersAsync(TxtSearchCustomer.Text, 150);
            GridCustomers.ItemsSource = list;
            LblStatus.Text = $"{list.Count} clientes cargados.";
        }
        catch (Exception ex)
        {
            LblStatus.Text = $"Error al buscar clientes: {ex.Message}";
        }
    }

    private async Task LoadCollectionsAsync()
    {
        try
        {
            var list = await _customerRepository.GetPendingCollectionsAsync();
            GridCollections.ItemsSource = list;
        }
        catch (Exception ex)
        {
            LblStatus.Text = $"Error al cargar cobros: {ex.Message}";
        }
    }

    private async void TxtSearchCustomer_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter || e.Key == Key.Return)
        {
            e.Handled = true;
            await LoadCustomersAsync();
        }
    }

    private async void BtnSearchCustomer_Click(object sender, RoutedEventArgs e) => await LoadCustomersAsync();

    private void GridCustomers_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GridCustomers.SelectedItem is CustomerEntity c)
        {
            _editingCustomer = c;
            _lcTipo = "M";
            TxtRuc.Text = c.Ruc;
            TxtDv.Text = c.Dv;
            TxtNombres.Text = c.Nombres;
            TxtDireccion.Text = c.Direccion;
            TxtTelefono.Text = c.Telefono;
            TxtEmail.Text = c.Email;
            TxtLineaCredito.Text = c.LineaCredito.ToString("0", CultureInfo.InvariantCulture);
        }
    }

    private void GridCustomers_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (GridCustomers.SelectedItem is CustomerEntity c && _onCustomerSelected != null)
        {
            var cb = _onCustomerSelected;
            _onCustomerSelected = null;
            Hide();
            cb(c);
        }
    }

    private void BtnSelectCustomer_Click(object sender, RoutedEventArgs e)
    {
        if (GridCustomers.SelectedItem is CustomerEntity c && _onCustomerSelected != null)
        {
            var cb = _onCustomerSelected;
            _onCustomerSelected = null;
            Hide();
            cb(c);
        }
    }

    private void BtnNewCustomer_Click(object sender, RoutedEventArgs e)
    {
        _editingCustomer = new CustomerEntity { Estado = 1, Contado = 1 };
        _lcTipo = "A";
        TxtRuc.Text = "";
        TxtDv.Text = "";
        TxtNombres.Text = "";
        TxtDireccion.Text = "";
        TxtTelefono.Text = "";
        TxtEmail.Text = "";
        TxtLineaCredito.Text = "0";
        TxtRuc.Focus();
    }

    private async void BtnSaveCustomer_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _editingCustomer.Ruc = TxtRuc.Text.Trim();
            _editingCustomer.Dv = TxtDv.Text.Trim();
            _editingCustomer.Nombres = TxtNombres.Text.Trim();
            _editingCustomer.Direccion = TxtDireccion.Text.Trim();
            _editingCustomer.Telefono = TxtTelefono.Text.Trim();
            _editingCustomer.Email = TxtEmail.Text.Trim();
            _editingCustomer.LineaCredito = decimal.TryParse(TxtLineaCredito.Text.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal lc) ? lc : 0m;

            int id = await _customerRepository.SaveCustomerAsync(_editingCustomer, _lcTipo);
            LblStatus.Text = $"✔ Cliente '{_editingCustomer.Nombres}' guardado (ID #{id}).";
            await LoadCustomersAsync();
        }
        catch (Exception ex)
        {
            LblStatus.Text = $"Error al guardar cliente: {ex.Message}";
        }
    }

    private async void BtnDeleteCustomer_Click(object sender, RoutedEventArgs e)
    {
        if (_editingCustomer.IdCliente <= 0) return;
        await _customerRepository.DeleteCustomerAsync(_editingCustomer.IdCliente);
        LblStatus.Text = $"Cliente #{_editingCustomer.IdCliente} anulado.";
        await LoadCustomersAsync();
    }

    private async void BtnRefreshCollections_Click(object sender, RoutedEventArgs e) => await LoadCollectionsAsync();

    private void GridCollections_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GridCollections.SelectedItem is CustomerCollectionEntity col)
        {
            TxtMontoCobro.Text = col.Saldo > 0 ? col.Saldo.ToString("0", CultureInfo.InvariantCulture) : col.Cobro.ToString("0", CultureInfo.InvariantCulture);
        }
    }

    private async void BtnRegisterPayment_Click(object sender, RoutedEventArgs e)
    {
        if (GridCollections.SelectedItem is not CustomerCollectionEntity col)
        {
            LblStatus.Text = "Seleccione un registro de cobro en la grilla.";
            return;
        }

        if (!decimal.TryParse(TxtMontoCobro.Text.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal monto) || monto <= 0m)
        {
            LblStatus.Text = "Ingrese un monto de pago válido.";
            return;
        }

        try
        {
            await _customerRepository.RegisterCollectionPaymentAsync(col.IdCobro, monto, 1, "ADMINISTRA");
            LblStatus.Text = $"✔ Pago de {monto:N0} Gs. registrado para factura {col.NroFactura}.";
            await LoadCollectionsAsync();
        }
        catch (Exception ex)
        {
            LblStatus.Text = $"Error al registrar cobro: {ex.Message}";
        }
    }
}
