using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using FirewallControl.Core.Entities;
using FirewallControl.Core.Interfaces;

namespace FirewallControl.App.Views;

public partial class FormAccessDialog : Window
{
    private readonly ISystemRepository? _systemRepository;
    private string _formName = "CREAR_PRODUCTOS";
    private string _formTitle = "Lista de Productos";
    private List<FormAccessEntity> _rows = new();

    public FormAccessDialog(ISystemRepository? systemRepository = null)
    {
        InitializeComponent();
        _systemRepository = systemRepository;
    }

    public async void Configure(string formName, string formTitle)
    {
        _formName = string.IsNullOrWhiteSpace(formName) ? "CREAR_PRODUCTOS" : formName.Trim().ToUpperInvariant();
        _formTitle = string.IsNullOrWhiteSpace(formTitle) ? _formName : formTitle.Trim();
        TxtFormTitle.Text = $"{_formTitle} ({_formName})";

        if (_systemRepository == null)
            return;

        try
        {
            var users = await _systemRepository.GetActiveUsersAsync();
            CmbUsers.ItemsSource = users;
            if (users.Count > 0) CmbUsers.SelectedIndex = 0;

            _rows = await _systemRepository.GetFormAccessListAsync(_formName);
            GridAccess.ItemsSource = _rows;
            TxtStatus.Text = $"✓ {_rows.Count} registros de permisos cargados.";
        }
        catch (Exception ex)
        {
            TxtStatus.Text = $"Error al cargar permisos: {ex.Message}";
        }
    }

    private void BtnAddUserRow_Click(object sender, RoutedEventArgs e)
    {
        if (CmbUsers.SelectedValue is not string cid || string.IsNullOrWhiteSpace(cid))
            return;

        string clean = cid.Trim().ToUpperInvariant();
        if (_rows.Any(r => string.Equals(r.CidUsuario.Trim(), clean, StringComparison.OrdinalIgnoreCase)))
        {
            TxtStatus.Text = $"El usuario '{clean}' ya está en la lista.";
            return;
        }

        _rows.Add(new FormAccessEntity
        {
            NameForm = _formName,
            TituloForm = _formTitle,
            CidUsuario = clean,
            Buscar = true,
            Nuevo = true,
            Editar = true,
            Eliminar = false,
            Imprimir = true
        });
        GridAccess.ItemsSource = null;
        GridAccess.ItemsSource = _rows;
    }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (_systemRepository == null)
            return;

        try
        {
            foreach (var r in _rows)
            {
                r.NameForm = _formName;
                r.TituloForm = _formTitle;
                await _systemRepository.SaveFormAccessAsync(r);
            }
            TxtStatus.Text = $"✓ Se guardaron {_rows.Count} filas de acceso correctamente.";
            MessageBox.Show("Permisos actualizados.", "Acceso a Formularios", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al guardar accesos:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
    }
}
