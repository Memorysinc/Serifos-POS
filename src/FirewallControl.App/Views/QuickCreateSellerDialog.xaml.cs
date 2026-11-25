using System;
using System.Windows;
using FirewallControl.Core.Entities;
using FirewallControl.Core.Interfaces;

namespace FirewallControl.App.Views;

public partial class QuickCreateSellerDialog : Window
{
    private readonly ISystemRepository _systemRepository;
    public SellerEntity? CreatedSeller { get; private set; }

    public QuickCreateSellerDialog(ISystemRepository systemRepository)
    {
        InitializeComponent();
        _systemRepository = systemRepository;
        Loaded += (_, _) => TxtNombres.Focus();
    }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        string nombre = TxtNombres.Text.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(nombre))
        {
            MessageBox.Show(this, "Ingrese el nombre del vendedor.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtNombres.Focus();
            return;
        }

        var entity = new SellerEntity
        {
            Nombres = nombre,
            Documento = TxtDocumento.Text.Trim(),
            Ruc = TxtDocumento.Text.Trim(),
            Telefonos = TxtTelefono.Text.Trim(),
            Celular = TxtTelefono.Text.Trim(),
            Estado = 1,
            TipoVendedor = 1
        };

        try
        {
            entity.IdVendedor = await _systemRepository.SaveSellerAsync(entity, "A");
            CreatedSeller = entity;
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Error al guardar vendedor:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
