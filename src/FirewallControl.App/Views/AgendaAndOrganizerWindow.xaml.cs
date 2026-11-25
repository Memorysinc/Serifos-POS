using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using FirewallControl.Core.Entities;
using FirewallControl.Core.Interfaces;

namespace FirewallControl.App.Views;

/// <summary>
/// Unifies agenda, revisaragenda, organizador, envio_sms, and conexionsms.
/// </summary>
public partial class AgendaAndOrganizerWindow : Window
{
    private readonly IStage3Repository _stage3Repository;
    private readonly string _currentUser;

    private AgendaItemEntity _editingAgenda = new();
    private OrganizerEntity _editingOrg = new();
    private SmsConnectionEntity _editingSms = new();

    public AgendaAndOrganizerWindow(IStage3Repository stage3Repository, string currentUser = "ADMINISTRA")
    {
        InitializeComponent();
        _stage3Repository = stage3Repository;
        _currentUser = string.IsNullOrWhiteSpace(currentUser) ? "ADMINISTRA" : currentUser;

        DpAgDesde.SelectedDate = DateTime.Today.AddDays(-7);
        DpAgHasta.SelectedDate = DateTime.Today.AddDays(30);
        TxtAgFechaHora.Text = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
        TxtAgUsuario.Text = _currentUser;

        Loaded += async (_, _) =>
        {
            await LoadAgendaAsync();
            await LoadOrganizadorAsync();
            await LoadSmsConnectionsAsync();
        };
    }

    public void Configure(int initialTab)
    {
        MainTabs.SelectedIndex = Math.Clamp(initialTab, 0, 2);
    }

    // --- AGENDA ---
    private async Task LoadAgendaAsync()
    {
        try
        {
            var items = await _stage3Repository.GetAgendaItemsAsync(
                DpAgDesde.SelectedDate,
                DpAgHasta.SelectedDate,
                string.IsNullOrWhiteSpace(TxtAgUserFilter.Text) ? null : TxtAgUserFilter.Text.Trim());

            if (ChkAgSoloPendientes.IsChecked == true)
                items = items.FindAll(x => x.Aviso == 1);

            GridAgenda.ItemsSource = items;
            LblStatus.Text = $"{items.Count} recordatorio(s) cargados en Agenda.";
        }
        catch (Exception ex)
        {
            LblStatus.Text = $"Error en Agenda: {ex.Message}";
        }
    }

    private async void BtnBuscarAgenda_Click(object sender, RoutedEventArgs e) => await LoadAgendaAsync();

    private void GridAgenda_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GridAgenda.SelectedItem is not AgendaItemEntity item) return;
        _editingAgenda = item;
        TxtAgFechaHora.Text = item.Fecha.ToString("dd/MM/yyyy HH:mm");
        TxtAgAsunto.Text = item.Asunto;
        TxtAgNotas.Text = item.Notas;
        TxtAgUsuario.Text = item.UserId;
        ChkAgAvisoActivo.IsChecked = item.Aviso == 1;
        CmbAgTipoReg.SelectedIndex = Math.Clamp(item.TipoReg - 1, 0, 2);
    }

    private void BtnNuevoAgenda_Click(object sender, RoutedEventArgs e)
    {
        _editingAgenda = new AgendaItemEntity { UserId = _currentUser };
        GridAgenda.SelectedItem = null;
        TxtAgFechaHora.Text = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
        TxtAgAsunto.Clear();
        TxtAgNotas.Clear();
        TxtAgUsuario.Text = _currentUser;
        ChkAgAvisoActivo.IsChecked = true;
        CmbAgTipoReg.SelectedIndex = 0;
        TxtAgAsunto.Focus();
    }

    private async void BtnGuardarAgenda_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtAgAsunto.Text))
        {
            MessageBox.Show("Ingrese un asunto para el recordatorio.", "Agenda", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!DateTime.TryParseExact(TxtAgFechaHora.Text.Trim(), new[] { "dd/MM/yyyy HH:mm", "dd/MM/yyyy", "yyyy-MM-dd HH:mm" },
                CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime fecha))
        {
            fecha = DateTime.Now;
        }

        string lcTipo = string.IsNullOrWhiteSpace(_editingAgenda.Id) ? "A" : "M";
        _editingAgenda.Fecha = fecha;
        _editingAgenda.Asunto = TxtAgAsunto.Text.Trim();
        _editingAgenda.Notas = TxtAgNotas.Text.Trim();
        _editingAgenda.Aviso = ChkAgAvisoActivo.IsChecked == true ? 1 : 0;
        _editingAgenda.TipoReg = CmbAgTipoReg.SelectedIndex + 1;
        _editingAgenda.UserId = string.IsNullOrWhiteSpace(TxtAgUsuario.Text) ? _currentUser : TxtAgUsuario.Text.Trim();
        _editingAgenda.Registro = string.IsNullOrWhiteSpace(_editingAgenda.Registro) ? DateTime.Now.ToString("yyyyMMddHHmmss") : _editingAgenda.Registro;

        await _stage3Repository.SaveAgendaItemAsync(_editingAgenda, lcTipo);
        await LoadAgendaAsync();
        LblStatus.Text = "Recordatorio guardado en Agenda.";
    }

    private async void BtnAtenderAgenda_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_editingAgenda.Id)) return;
        _editingAgenda.Aviso = 0;
        ChkAgAvisoActivo.IsChecked = false;
        await _stage3Repository.SaveAgendaItemAsync(_editingAgenda, "M");
        await LoadAgendaAsync();
    }

    private async void BtnEliminarAgenda_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_editingAgenda.Id)) return;
        if (MessageBox.Show($"¿Eliminar '{_editingAgenda.Asunto}'?", "Confirmar", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        await _stage3Repository.DeleteAgendaItemAsync(_editingAgenda.Id);
        BtnNuevoAgenda_Click(sender, e);
        await LoadAgendaAsync();
    }

    // --- ORGANIZADOR ---
    private async Task LoadOrganizadorAsync()
    {
        try
        {
            var contacts = await _stage3Repository.GetOrganizerContactsAsync(
                string.IsNullOrWhiteSpace(TxtOrgFilter.Text) ? null : TxtOrgFilter.Text.Trim());
            GridOrganizador.ItemsSource = contacts;
        }
        catch (Exception ex)
        {
            LblStatus.Text = $"Error en Organizador: {ex.Message}";
        }
    }

    private async void BtnBuscarOrg_Click(object sender, RoutedEventArgs e) => await LoadOrganizadorAsync();

    private void GridOrganizador_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GridOrganizador.SelectedItem is not OrganizerEntity c) return;
        _editingOrg = c;
        TxtOrgNombres.Text = c.Nombres;
        TxtOrgTel1.Text = c.Telefonos1;
        TxtOrgTel2.Text = c.Telefonos2;
        TxtOrgTel3.Text = c.Telefonos3;
    }

    private void BtnNuevoOrg_Click(object sender, RoutedEventArgs e)
    {
        _editingOrg = new OrganizerEntity();
        GridOrganizador.SelectedItem = null;
        TxtOrgNombres.Clear();
        TxtOrgTel1.Clear();
        TxtOrgTel2.Clear();
        TxtOrgTel3.Clear();
        TxtOrgNombres.Focus();
    }

    private async void BtnGuardarOrg_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtOrgNombres.Text)) return;
        _editingOrg.Nombres = TxtOrgNombres.Text.Trim();
        _editingOrg.Telefonos1 = TxtOrgTel1.Text.Trim();
        _editingOrg.Telefonos2 = TxtOrgTel2.Text.Trim();
        _editingOrg.Telefonos3 = TxtOrgTel3.Text.Trim();
        await _stage3Repository.SaveOrganizerContactAsync(_editingOrg, _editingOrg.IdAgenda > 0 ? "M" : "A");
        await LoadOrganizadorAsync();
    }

    private async void BtnEliminarOrg_Click(object sender, RoutedEventArgs e)
    {
        if (_editingOrg.IdAgenda <= 0) return;
        if (MessageBox.Show($"¿Eliminar contacto '{_editingOrg.Nombres}'?", "Confirmar", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        await _stage3Repository.DeleteOrganizerContactAsync(_editingOrg.IdAgenda);
        BtnNuevoOrg_Click(sender, e);
        await LoadOrganizadorAsync();
    }

    private void BtnEnviarSmsContacto_Click(object sender, RoutedEventArgs e)
    {
        string phone = !string.IsNullOrWhiteSpace(TxtOrgTel1.Text) ? TxtOrgTel1.Text : TxtOrgTel2.Text;
        TxtSmsDestino.Text = phone.Trim();
        MainTabs.SelectedIndex = 2;
        TxtSmsMensaje.Focus();
    }

    // --- SMS ---
    private async Task LoadSmsConnectionsAsync()
    {
        try
        {
            var conns = await _stage3Repository.GetSmsConnectionsAsync();
            GridSmsConns.ItemsSource = conns;
            if (conns.Count > 0)
            {
                _editingSms = conns[0];
                PopulateSmsForm(conns[0]);
            }
        }
        catch { }
    }

    private void PopulateSmsForm(SmsConnectionEntity c)
    {
        TxtSmsDescrip.Text = c.Descrip;
        TxtSmsPuerto.Text = c.Puerto.ToString();
        CmbSmsBaud.Text = c.BautRate;
        CmbSmsParity.Text = c.Parity;
        ChkSmsInactivo.IsChecked = c.Inactivo == 1;
    }

    private void GridSmsConns_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GridSmsConns.SelectedItem is not SmsConnectionEntity c) return;
        _editingSms = c;
        PopulateSmsForm(c);
    }

    private void BtnNuevoSmsConn_Click(object sender, RoutedEventArgs e)
    {
        _editingSms = new SmsConnectionEntity();
        GridSmsConns.SelectedItem = null;
        TxtSmsDescrip.Text = "Módem GSM";
        TxtSmsPuerto.Text = "1";
        CmbSmsBaud.Text = "9600";
        ChkSmsInactivo.IsChecked = false;
    }

    private async void BtnGuardarSmsConn_Click(object sender, RoutedEventArgs e)
    {
        _editingSms.Descrip = string.IsNullOrWhiteSpace(TxtSmsDescrip.Text) ? "Módem GSM" : TxtSmsDescrip.Text.Trim();
        _editingSms.Puerto = int.TryParse(TxtSmsPuerto.Text, out int p) ? p : 1;
        _editingSms.BautRate = string.IsNullOrWhiteSpace(CmbSmsBaud.Text) ? "9600" : CmbSmsBaud.Text.Trim();
        _editingSms.Parity = string.IsNullOrWhiteSpace(CmbSmsParity.Text) ? "N" : CmbSmsParity.Text.Trim();
        _editingSms.Inactivo = ChkSmsInactivo.IsChecked == true ? 1 : 0;

        await _stage3Repository.SaveSmsConnectionAsync(_editingSms, _editingSms.IdConexion > 0 ? "M" : "A");
        await LoadSmsConnectionsAsync();
        LblStatus.Text = $"Configuración SMS guardada (COM{_editingSms.Puerto} @ {_editingSms.BautRate}).";
    }

    private async void BtnEnviarSms_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtSmsDestino.Text) || string.IsNullOrWhiteSpace(TxtSmsMensaje.Text))
        {
            MessageBox.Show("Ingrese número de destino y mensaje SMS.", "SMS", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string destino = TxtSmsDestino.Text.Trim();
        string msg = TxtSmsMensaje.Text.Trim();
        int puerto = int.TryParse(TxtSmsPuerto.Text, out int p) ? p : 1;

        TxtSmsLog.AppendText($"[{DateTime.Now:HH:mm:ss}] Puerto COM{puerto} ({CmbSmsBaud.Text} 8N1)\n");
        TxtSmsLog.AppendText($"> AT+CMGF=1\n> AT+CMGS=\"{destino}\"\n> {msg} <Ctrl+Z>\n");

        await _stage3Repository.SaveAgendaItemAsync(new AgendaItemEntity
        {
            Fecha = DateTime.Now,
            Asunto = $"SMS -> {destino}",
            Notas = msg,
            Aviso = 0,
            TipoReg = 2,
            UserId = _currentUser,
            Registro = DateTime.Now.ToString("yyyyMMddHHmmss")
        }, "A");

        TxtSmsLog.AppendText("✔ Envío registrado en historial de Agenda.\n\n");
        LblStatus.Text = $"SMS preparado y registrado para {destino}.";
    }
}
