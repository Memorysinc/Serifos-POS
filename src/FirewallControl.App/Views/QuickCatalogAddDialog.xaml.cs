using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using FirewallControl.Core.Entities;
using FirewallControl.Core.Interfaces;

namespace FirewallControl.App.Views;

public enum QuickCatalogKind
{
    Proveedor,
    Grupo,
    SubGrupo,
    Marca,
    Modelo
}

public partial class QuickCatalogAddDialog : Window
{
    private IProductRepository? _productRepository;
    private IPurchaseRepository? _purchaseRepository;
    private QuickCatalogKind _kind = QuickCatalogKind.Grupo;

    public int CreatedId { get; private set; }
    public string CreatedDescription { get; private set; } = string.Empty;

    public QuickCatalogAddDialog()
    {
        InitializeComponent();
    }

    public void Configure(QuickCatalogKind kind, IProductRepository productRepository, IPurchaseRepository? purchaseRepository = null)
    {
        _kind = kind;
        _productRepository = productRepository;
        _purchaseRepository = purchaseRepository;

        PanelSupplierExtra.Visibility = kind == QuickCatalogKind.Proveedor ? Visibility.Visible : Visibility.Collapsed;
        PanelGroupExtra.Visibility = kind == QuickCatalogKind.Grupo ? Visibility.Visible : Visibility.Collapsed;

        (Title, TxtHeader.Text, LblPrimary.Text) = kind switch
        {
            QuickCatalogKind.Proveedor => ("Nuevo Proveedor (abm_proveedores)", "ALTA RÁPIDA DE PROVEEDOR", "Razón Social / Nombre (*)"),
            QuickCatalogKind.Grupo => ("Nuevo Grupo de Productos (abm_grupoproductos)", "ALTA RÁPIDA DE GRUPO DE PRODUCTOS", "Descripción del Grupo (*)"),
            QuickCatalogKind.SubGrupo => ("Nuevo Subgrupo (abm_subgrupoproductos)", "ALTA RÁPIDA DE SUBGRUPO", "Descripción del Subgrupo (*)"),
            QuickCatalogKind.Marca => ("Nueva Marca (abm_marcaproductos)", "ALTA RÁPIDA DE MARCA", "Descripción de la Marca (*)"),
            QuickCatalogKind.Modelo => ("Nuevo Modelo (abm_modeloproductos)", "ALTA RÁPIDA DE MODELO", "Descripción del Modelo (*)"),
            _ => ("Nuevo Catálogo", "ALTA RÁPIDA", "Descripción (*)")
        };

        Loaded += (_, _) => TxtDescripcion.Focus();
    }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        string desc = (TxtDescripcion.Text ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(desc))
        {
            MessageBox.Show("La descripción es obligatoria.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtDescripcion.Focus();
            return;
        }

        if (_productRepository == null)
            return;

        try
        {
            switch (_kind)
            {
                case QuickCatalogKind.Proveedor:
                    if (_purchaseRepository == null)
                        throw new InvalidOperationException("Repositorio de proveedores no disponible.");
                    var sup = new SupplierEntity
                    {
                        Descripcion = desc,
                        Ruc = (TxtRuc.Text ?? string.Empty).Trim(),
                        Telefono = (TxtTelefono.Text ?? string.Empty).Trim(),
                        Direccion = (TxtDireccion.Text ?? string.Empty).Trim(),
                        Estado = 1,
                        Fecha = DateTime.Today,
                        UserId = "ADMINISTRA"
                    };
                    CreatedId = await _purchaseRepository.SaveSupplierAsync(sup, "A");
                    break;

                case QuickCatalogKind.Grupo:
                    decimal.TryParse((TxtGrupoDescuento.Text ?? "0").Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal descPct);
                    var grp = new ProductGroupEntity
                    {
                        Descripcion = desc,
                        Descuento = descPct,
                        FechaInicio = DpFechaInicio.SelectedDate,
                        FechaFinal = DpFechaFinal.SelectedDate,
                        Impresora = (TxtGrupoImpresora.Text ?? string.Empty).Trim()
                    };
                    CreatedId = await _productRepository.SaveProductGroupAsync(grp, "A");
                    break;

                case QuickCatalogKind.SubGrupo:
                    CreatedId = await _productRepository.SaveProductSubGroupAsync(new ProductSubGroupEntity { Descripcion = desc }, "A");
                    break;

                case QuickCatalogKind.Marca:
                    CreatedId = await _productRepository.SaveProductBrandAsync(new ProductBrandEntity { Descripcion = desc }, "A");
                    break;

                case QuickCatalogKind.Modelo:
                    CreatedId = await _productRepository.SaveProductModelAsync(new ProductModelEntity { Descripcion = desc }, "A");
                    break;
            }

            CreatedDescription = desc;
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"No se pudo guardar el registro:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            BtnCancel_Click(sender, e);
        }
        else if (e.Key == Key.Enter && e.KeyboardDevice.FocusedElement is not System.Windows.Controls.Button)
        {
            e.Handled = true;
            BtnSave_Click(sender, e);
        }
    }
}
