using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FirewallControl.Core.Business;
using FirewallControl.Core.Entities;
using FirewallControl.Core.Interfaces;

namespace FirewallControl.App.Views;

/// <summary>
/// Replicates ClaveIn.scx and Usuarios.scx.
/// Pre-populates password "123456" as in ClaveIn.scx and validates via VfpCompat.ValidateUserPassword (SYS(2007)).
/// </summary>
public partial class LoginWindow : Window
{
    private readonly ISystemRepository _systemRepository;
    public UserEntity? AuthenticatedUser { get; private set; }
    public UserGroupEntity? AuthenticatedGroup { get; private set; }

    public LoginWindow(ISystemRepository systemRepository)
    {
        InitializeComponent();
        _systemRepository = systemRepository;
        TxtPassword.Password = "123456"; // Default from ClaveIn.scx

        Loaded += async (_, _) =>
        {
            try
            {
                var users = await _systemRepository.GetActiveUsersAsync();
                CmbUsuarios.ItemsSource = users;
                var admin = users.FirstOrDefault(u => u.Cidusuario.Trim().Equals("ADMINISTRA", StringComparison.OrdinalIgnoreCase))
                            ?? users.FirstOrDefault();
                CmbUsuarios.SelectedItem = admin;
                TxtPassword.Focus();
            }
            catch (Exception ex)
            {
                LblError.Text = $"Error al cargar usuarios: {ex.Message}";
            }
        };
    }

    private void CmbUsuarios_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbUsuarios.SelectedItem is UserEntity user)
        {
            LblNombreUsuario.Text = user.Cdescribe;
        }
    }

    private void TxtPassword_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter || e.Key == Key.Return)
        {
            e.Handled = true;
            AttemptLogin();
        }
    }

    private void BtnIngresar_Click(object sender, RoutedEventArgs e) => AttemptLogin();

    private async void AttemptLogin()
    {
        if (CmbUsuarios.SelectedItem is not UserEntity selectedUser)
        {
            LblError.Text = "Seleccione un usuario.";
            return;
        }

        string entered = TxtPassword.Password;
        if (!VfpCompat.ValidateUserPassword(entered, selectedUser.Cpassword))
        {
            LblError.Text = "Contraseña incorrecta. Verifique e intente de nuevo.";
            TxtPassword.SelectAll();
            TxtPassword.Focus();
            return;
        }

        AuthenticatedUser = selectedUser;
        AuthenticatedGroup = await _systemRepository.GetGroupByCodeAsync(selectedUser.Cidgrupo) ?? new UserGroupEntity();
        DialogResult = true;
        Close();
    }
}
