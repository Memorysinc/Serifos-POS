using System.Windows;
using FirewallControl.Core.Interfaces;

namespace FirewallControl.App.Views;

/// <summary>
/// Replicates SeleServidor.scx to test and persist encrypted MySQL server credentials in Firewall.sql.
/// </summary>
public partial class SeleServidorWindow : Window
{
    private readonly IConfigService _configService;
    private readonly IDbConnectionFactory _connectionFactory;

    public SeleServidorWindow(IConfigService configService, IDbConnectionFactory connectionFactory)
    {
        InitializeComponent();
        _configService = configService;
        _connectionFactory = connectionFactory;

        var cfg = _configService.Config;
        TxtHost.Text = string.IsNullOrWhiteSpace(cfg.SqlServerHost) ? "localhost" : cfg.SqlServerHost;
        TxtPort.Text = cfg.Port.ToString();
        TxtDatabase.Text = cfg.BaseDatos;
        TxtUser.Text = cfg.SqlUser;
        TxtPassword.Password = cfg.SqlPassword;
    }

    private async void BtnProbar_Click(object sender, RoutedEventArgs e)
    {
        int port = int.TryParse(TxtPort.Text, out int p) ? p : 3306;
        string testConn = $"Server={TxtHost.Text.Trim()};Port={port};Database={TxtDatabase.Text.Trim()};User ID={TxtUser.Text.Trim()};Password={TxtPassword.Password};ConnectionTimeout=4;";
        LblStatus.Text = "Probando conexión...";
        bool ok = await _connectionFactory.TestConnectionAsync(testConn);
        LblStatus.Text = ok
            ? "✔ Conexión exitosa al servidor MySQL."
            : "✖ No se pudo conectar con los parámetros indicados.";
    }

    private async void BtnGuardar_Click(object sender, RoutedEventArgs e)
    {
        int port = int.TryParse(TxtPort.Text, out int p) ? p : 3306;
        _configService.SaveServerCredentials(
            TxtHost.Text.Trim(),
            TxtUser.Text.Trim(),
            TxtPassword.Password,
            port,
            TxtDatabase.Text.Trim());

        bool ok = await _connectionFactory.TestConnectionAsync();
        if (ok)
        {
            DialogResult = true;
            Close();
        }
        else
        {
            LblStatus.Text = "Credenciales guardadas en Firewall.sql, pero el servidor no responde.";
        }
    }
}
