using System;
using System.Windows;
using FirewallControl.Core.Entities;
using FirewallControl.Core.Interfaces;

namespace FirewallControl.App.Views;

public partial class QuickCreateCustomerDialog : Window
{
    private readonly ICustomerRepository _customerRepository;
    public CustomerEntity? CreatedCustomer { get; private set; }

    public QuickCreateCustomerDialog(ICustomerRepository customerRepository)
    {
        InitializeComponent();
        _customerRepository = customerRepository;
        Loaded += (_, _) => TxtRuc.Focus();
    }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        string nombre = TxtNombres.Text.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(nombre))
        {
            MessageBox.Show(this, "Ingrese el nombre o razón social del cliente.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtNombres.Focus();
            return;
        }

        var entity = new CustomerEntity
        {
            Ruc = string.IsNullOrWhiteSpace(TxtRuc.Text) ? "X" : TxtRuc.Text.Trim(),
            Dv = TxtDv.Text.Trim(),
            Nombres = nombre,
            Telefono = TxtTelefono.Text.Trim(),
            Celular = TxtTelefono.Text.Trim(),
            Direccion = TxtDireccion.Text.Trim(),
            Estado = 1,
            Contado = 1,
            Precio1 = 1
        };

        try
        {
            entity.IdCliente = await _customerRepository.SaveCustomerAsync(entity, "A");
            CreatedCustomer = entity;
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Error al guardar cliente:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
