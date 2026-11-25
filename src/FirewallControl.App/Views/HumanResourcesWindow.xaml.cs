using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using FirewallControl.Core.Entities;
using FirewallControl.Core.Interfaces;

namespace FirewallControl.App.Views;

/// <summary>
/// Unifies crear_legajos, crear_descuentopersonal, crear_salarios, abm_salariosparametros,
/// labor calculators (Aguinaldo, Vacaciones, Liquidación, Bonificaciones), and 6 HR lookup tables.
/// </summary>
public partial class HumanResourcesWindow : Window
{
    private readonly IStage3Repository _stage3Repository;
    private EmployeeLegajoEntity _editingEmp = new();
    private HrLookupEntity _editingHl = new();
    private SalaryParametersEntity _salaryParams = new();
    private List<EmployeeLegajoEntity> _employees = new();

    public HumanResourcesWindow(IStage3Repository stage3Repository)
    {
        InitializeComponent();
        _stage3Repository = stage3Repository;
        DpLegIngreso.SelectedDate = DateTime.Today;
        DpLegNac.SelectedDate = DateTime.Today.AddYears(-25);
        DpDpFecha.SelectedDate = DateTime.Today;
        TxtSalMes.Text = DateTime.Today.Month.ToString("D2");
        TxtSalAnio.Text = DateTime.Today.Year.ToString();
        Loaded += async (_, _) => await LoadAllAsync();
    }

    public void Configure(int initialTab, int lookupIndex = 0, string calcPreset = "")
    {
        MainTabs.SelectedIndex = Math.Clamp(initialTab, 0, 4);
        if (lookupIndex >= 0 && lookupIndex <= 5)
            CmbHrLookupTable.SelectedIndex = lookupIndex;

        if (!string.IsNullOrWhiteSpace(calcPreset))
        {
            CmbCalcPreset.SelectedIndex = calcPreset.ToUpperInvariant() switch
            {
                "VACACIONES" => 1,
                "LIQUIDACION" => 2,
                "BONIFICACION" => 3,
                _ => 0
            };
        }
    }

    private async Task LoadAllAsync()
    {
        try
        {
            _employees = await _stage3Repository.GetEmployeesAsync();
            GridLegajos.ItemsSource = _employees;
            CmbDpEmpleado.ItemsSource = _employees;
            CmbSalEmpleado.ItemsSource = _employees;
            if (_employees.Count > 0)
            {
                if (CmbDpEmpleado.SelectedValue == null) CmbDpEmpleado.SelectedIndex = 0;
                if (CmbSalEmpleado.SelectedValue == null) CmbSalEmpleado.SelectedIndex = 0;
            }

            CmbLegNac.ItemsSource = await _stage3Repository.GetHrLookupAsync("nacionalidades");
            CmbLegCargo.ItemsSource = await _stage3Repository.GetHrLookupAsync("cargos");
            CmbLegDepto.ItemsSource = await _stage3Repository.GetHrLookupAsync("departamentos");
            CmbLegArea.ItemsSource = await _stage3Repository.GetHrLookupAsync("areas");
            CmbDpTipo.ItemsSource = await _stage3Repository.GetHrLookupAsync("descuentos");
            if (CmbDpTipo.Items.Count > 0 && CmbDpTipo.SelectedIndex < 0) CmbDpTipo.SelectedIndex = 0;

            GridDescuentosPers.ItemsSource = await _stage3Repository.GetEmployeeDiscountsAsync();
            GridSalarios.ItemsSource = await _stage3Repository.GetSalaryPayrollsAsync();

            _salaryParams = await _stage3Repository.GetSalaryParametersAsync();
            TxtParSalMin.Text = _salaryParams.SalarioMinimo.ToString("0", CultureInfo.InvariantCulture);
            TxtParNroIps.Text = _salaryParams.NroIpsPatronal.ToString();
            TxtParIpsPat.Text = _salaryParams.IpsPatronal.ToString("0.##", CultureInfo.InvariantCulture);
            TxtParIpsEmp.Text = _salaryParams.IpsEmpleado.ToString("0.##", CultureInfo.InvariantCulture);
            TxtParBonif.Text = _salaryParams.BonificacionFamiliar.ToString("0.##", CultureInfo.InvariantCulture);
            TxtParEdad.Text = _salaryParams.LimiteEdadHijos.ToString();
            TxtParHoraExtra.Text = _salaryParams.PrecioHoraExtra.ToString("0", CultureInfo.InvariantCulture);

            await LoadActiveHrLookupAsync();
            LblStatus.Text = $"{_employees.Count} legajo(s) cargados.";
        }
        catch (Exception ex)
        {
            LblStatus.Text = $"Error RRHH: {ex.Message}";
        }
    }

    private static decimal ParseDec(string s, decimal fallback = 0m) =>
        decimal.TryParse((s ?? "").Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal d) ? d : fallback;
    private static int ParseInt(string s, int fallback = 0) => int.TryParse(s, out int n) ? n : fallback;

    // --- TAB 0: LEGAJOS ---
    private void GridLegajos_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GridLegajos.SelectedItem is not EmployeeLegajoEntity emp) return;
        _editingEmp = emp;
        TxtLegApellidos.Text = emp.Apellidos;
        TxtLegNombres.Text = emp.Nombres;
        TxtLegTipoDoc.Text = emp.TipoDoc;
        TxtLegDoc.Text = emp.Documento;
        TxtLegIps.Text = emp.IpsNro.ToString();
        CmbLegCargo.SelectedValue = emp.Cargo;
        CmbLegDepto.SelectedValue = emp.Departamento;
        CmbLegArea.SelectedValue = emp.Area;
        CmbLegNac.SelectedValue = emp.Nacionalidad;
        TxtLegSalario.Text = emp.Salario.ToString("0", CultureInfo.InvariantCulture);
        DpLegIngreso.SelectedDate = emp.FechaIngreso;
        DpLegNac.SelectedDate = emp.FechaNacimiento;
        TxtLegTel.Text = emp.Telefono;
        TxtLegCorreo.Text = emp.Correo;
        TxtLegDir.Text = emp.Direccion;
    }

    private void BtnNuevoLeg_Click(object sender, RoutedEventArgs e)
    {
        _editingEmp = new EmployeeLegajoEntity();
        GridLegajos.SelectedItem = null;
        TxtLegApellidos.Clear(); TxtLegNombres.Clear(); TxtLegDoc.Clear(); TxtLegTel.Clear(); TxtLegCorreo.Clear(); TxtLegDir.Clear();
        TxtLegSalario.Text = "0"; TxtLegIps.Text = "0";
    }

    private async void BtnGuardarLeg_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtLegApellidos.Text) || string.IsNullOrWhiteSpace(TxtLegNombres.Text)) return;
        _editingEmp.Apellidos = TxtLegApellidos.Text.Trim();
        _editingEmp.Nombres = TxtLegNombres.Text.Trim();
        _editingEmp.TipoDoc = string.IsNullOrWhiteSpace(TxtLegTipoDoc.Text) ? "CI" : TxtLegTipoDoc.Text.Trim();
        _editingEmp.Documento = TxtLegDoc.Text.Trim();
        _editingEmp.IpsNro = ParseInt(TxtLegIps.Text);
        _editingEmp.Cargo = (CmbLegCargo.SelectedItem as HrLookupEntity)?.Codigo ?? "";
        _editingEmp.DesCargo = (CmbLegCargo.SelectedItem as HrLookupEntity)?.Descripcion ?? "";
        _editingEmp.Departamento = (CmbLegDepto.SelectedItem as HrLookupEntity)?.Codigo ?? "";
        _editingEmp.DesDepartamento = (CmbLegDepto.SelectedItem as HrLookupEntity)?.Descripcion ?? "";
        _editingEmp.Area = (CmbLegArea.SelectedItem as HrLookupEntity)?.Codigo ?? "";
        _editingEmp.DesArea = (CmbLegArea.SelectedItem as HrLookupEntity)?.Descripcion ?? "";
        _editingEmp.Nacionalidad = (CmbLegNac.SelectedItem as HrLookupEntity)?.Codigo ?? "";
        _editingEmp.DesNacionalidad = (CmbLegNac.SelectedItem as HrLookupEntity)?.Descripcion ?? "";
        _editingEmp.Salario = ParseDec(TxtLegSalario.Text);
        _editingEmp.FechaIngreso = DpLegIngreso.SelectedDate ?? DateTime.Today;
        _editingEmp.FechaNacimiento = DpLegNac.SelectedDate ?? DateTime.Today.AddYears(-25);
        _editingEmp.Telefono = TxtLegTel.Text.Trim();
        _editingEmp.Correo = TxtLegCorreo.Text.Trim();
        _editingEmp.Direccion = TxtLegDir.Text.Trim();

        await _stage3Repository.SaveEmployeeAsync(_editingEmp, _editingEmp.IdLegajo > 0 ? "M" : "A");
        await LoadAllAsync();
    }

    private async void BtnEliminarLeg_Click(object sender, RoutedEventArgs e)
    {
        if (_editingEmp.IdLegajo <= 0) return;
        if (MessageBox.Show($"¿Eliminar legajo de {_editingEmp.NombreCompleto}?", "Confirmar", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        await _stage3Repository.DeleteEmployeeAsync(_editingEmp.IdLegajo);
        BtnNuevoLeg_Click(sender, e);
        await LoadAllAsync();
    }

    // --- TAB 1: DESCUENTOS PERSONAL ---
    private async void BtnGuardarDp_Click(object sender, RoutedEventArgs e)
    {
        if (CmbDpEmpleado.SelectedItem is not EmployeeLegajoEntity emp) return;
        decimal monto = ParseDec(TxtDpMonto.Text);
        if (monto <= 0) return;
        int cuotas = Math.Max(1, ParseInt(TxtDpCuota.Text, 1));
        var tipo = CmbDpTipo.SelectedItem as HrLookupEntity;

        await _stage3Repository.SaveEmployeeDiscountAsync(new EmployeeDiscountEntity
        {
            IdLegajo = emp.IdLegajo,
            Documento = emp.Documento,
            Nombres = emp.NombreCompleto,
            FechaDescuento = DpDpFecha.SelectedDate ?? DateTime.Today,
            Monto = monto,
            Cuota = cuotas,
            Saldo = monto,
            MontoCuota = Math.Round(monto / cuotas, 0, MidpointRounding.AwayFromZero),
            IdDescuento = tipo?.Id ?? 0,
            CodDescuento = tipo?.Codigo ?? "",
            DesDescuento = tipo?.Descripcion ?? "DESCUENTO",
            Estado = 1
        }, "A");
        TxtDpMonto.Text = "0";
        await LoadAllAsync();
    }

    // --- TAB 2: SALARIOS & CALCULADORAS ---
    private async void BtnFiltrarSalarios_Click(object sender, RoutedEventArgs e)
    {
        string? mes = string.IsNullOrWhiteSpace(TxtSalMesFilter.Text) ? null : TxtSalMesFilter.Text.Trim();
        int? anio = int.TryParse(TxtSalAnioFilter.Text, out int a) ? a : null;
        GridSalarios.ItemsSource = await _stage3Repository.GetSalaryPayrollsAsync(mes, anio);
    }

    private void CmbSalEmpleado_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ComputeLaborPreset();
    }

    private async void BtnProcesarSalario_Click(object sender, RoutedEventArgs e)
    {
        if (CmbSalEmpleado.SelectedItem is not EmployeeLegajoEntity emp) return;
        int dias = Math.Clamp(ParseInt(TxtSalDias.Text, 30), 1, 31);
        int hsExtra = Math.Max(0, ParseInt(TxtSalHorasExtra.Text, 0));
        decimal bonif = ParseDec(TxtSalBonif.Text);
        decimal otrosDesc = ParseDec(TxtSalDesc.Text);

        decimal baseProp = Math.Round(emp.Salario * (dias / 30m), 0, MidpointRounding.AwayFromZero);
        decimal mHsExtra = Math.Round(hsExtra * _salaryParams.PrecioHoraExtra, 0, MidpointRounding.AwayFromZero);
        decimal totalIng = baseProp + mHsExtra + bonif;
        decimal ips = Math.Round(totalIng * (_salaryParams.IpsEmpleado / 100m), 0, MidpointRounding.AwayFromZero);
        decimal totalEgr = ips + otrosDesc;
        decimal neto = Math.Max(0m, totalIng - totalEgr);

        await _stage3Repository.ProcessAndSaveSalaryPayrollAsync(new SalaryPayrollEntity
        {
            Mes = string.IsNullOrWhiteSpace(TxtSalMes.Text) ? DateTime.Today.Month.ToString("D2") : TxtSalMes.Text.Trim().PadLeft(2, '0'),
            Anio = ParseInt(TxtSalAnio.Text, DateTime.Today.Year),
            IdLegajo = emp.IdLegajo,
            Documento = emp.Documento,
            DesNombres = emp.NombreCompleto,
            DiasTrabajados = dias,
            HorasExtra = hsExtra,
            MontoBonificacion = bonif,
            MontoHorasExtras = mHsExtra,
            TotalIngreso = totalIng,
            Descuentos = otrosDesc,
            Salario = emp.Salario,
            MontoIps = ips,
            TotalEgresos = totalEgr,
            NetoPercibir = neto,
            FechaPagoSalario = DateTime.Today
        });

        await LoadAllAsync();
        LblStatus.Text = $"Salario procesado para {emp.NombreCompleto} -> Neto: {neto:N0} Gs.";
    }

    private void CmbCalcPreset_SelectionChanged(object sender, SelectionChangedEventArgs e) => ComputeLaborPreset();
    private void BtnCalcularLaboral_Click(object sender, RoutedEventArgs e) => ComputeLaborPreset();

    private void ComputeLaborPreset()
    {
        if (TxtCalcResultado == null) return;
        var emp = CmbSalEmpleado.SelectedItem as EmployeeLegajoEntity ?? _employees.FirstOrDefault();
        decimal sal = emp?.Salario ?? _salaryParams.SalarioMinimo;
        int meses = Math.Clamp(ParseInt(TxtCalcMesesAnio.Text, 12), 1, 12);
        DateTime ing = emp?.FechaIngreso ?? DateTime.Today.AddYears(-1);
        double years = Math.Max(0, (DateTime.Today - ing).TotalDays / 365.25);

        switch (CmbCalcPreset.SelectedIndex)
        {
            case 0: // Aguinaldo
                decimal ag = Math.Round((sal * meses) / 12m, 0, MidpointRounding.AwayFromZero);
                TxtCalcResultado.Text = $"Cálculo de Aguinaldo ({emp?.NombreCompleto ?? "Base"})\nSalario: {sal:N0} Gs. | Meses: {meses}\nAguinaldo Proporcional = {ag:N0} Gs.";
                break;
            case 1: // Vacaciones
                int vacDays = years >= 10 ? 30 : (years >= 5 ? 18 : 12);
                decimal vacVal = Math.Round((sal / 30m) * vacDays, 0, MidpointRounding.AwayFromZero);
                TxtCalcResultado.Text = $"Vacaciones Causadas ({emp?.NombreCompleto ?? "Base"})\nAntigüedad: {years:N1} años -> {vacDays} días hábiles\nRemuneración Vacaciones = {vacVal:N0} Gs.";
                break;
            case 2: // Liquidación
                decimal prop = Math.Round((sal / 30m) * DateTime.Today.Day, 0, MidpointRounding.AwayFromZero);
                decimal agProp = Math.Round((sal * DateTime.Today.Month) / 12m, 0, MidpointRounding.AwayFromZero);
                int indDias = Math.Max(1, (int)Math.Floor(years)) * 15;
                decimal indem = Math.Round((sal / 30m) * indDias, 0, MidpointRounding.AwayFromZero);
                TxtCalcResultado.Text = $"Liquidación Estimada ({emp?.NombreCompleto ?? "Base"})\nSalario Proporcional: {prop:N0} Gs. + Aguinaldo Prop.: {agProp:N0} Gs.\nIndemnización ({indDias} días): {indem:N0} Gs. | Total: {(prop + agProp + indem):N0} Gs.";
                break;
            case 3: // Bonificación Familiar
                decimal bf = Math.Round(sal * (_salaryParams.BonificacionFamiliar / 100m), 0, MidpointRounding.AwayFromZero);
                TxtCalcResultado.Text = $"Bonificación Familiar\nPorcentaje configurado: {_salaryParams.BonificacionFamiliar:N2}%\nMonto por hijo menor a {_salaryParams.LimiteEdadHijos} años = {bf:N0} Gs.";
                break;
        }
    }

    // --- TAB 3: PARÁMETROS ---
    private async void BtnGuardarParametros_Click(object sender, RoutedEventArgs e)
    {
        _salaryParams.SalarioMinimo = ParseDec(TxtParSalMin.Text);
        _salaryParams.NroIpsPatronal = ParseInt(TxtParNroIps.Text);
        _salaryParams.IpsPatronal = ParseDec(TxtParIpsPat.Text, 16.5m);
        _salaryParams.IpsEmpleado = ParseDec(TxtParIpsEmp.Text, 9m);
        _salaryParams.BonificacionFamiliar = ParseDec(TxtParBonif.Text);
        _salaryParams.LimiteEdadHijos = ParseInt(TxtParEdad.Text, 18);
        _salaryParams.PrecioHoraExtra = ParseDec(TxtParHoraExtra.Text);
        await _stage3Repository.SaveSalaryParametersAsync(_salaryParams);
        LblStatus.Text = "Parámetros salariales guardados.";
    }

    // --- TAB 4: FICHAS AUXILIARES ---
    private string GetSelectedLookupTable() =>
        (CmbHrLookupTable.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "cargos";

    private async Task LoadActiveHrLookupAsync()
    {
        if (GridHrLookup == null) return;
        GridHrLookup.ItemsSource = await _stage3Repository.GetHrLookupAsync(GetSelectedLookupTable());
    }

    private async void CmbHrLookupTable_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        _editingHl = new HrLookupEntity();
        TxtHlCodigo.Clear(); TxtHlDesc.Clear();
        await LoadActiveHrLookupAsync();
    }

    private void GridHrLookup_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GridHrLookup.SelectedItem is not HrLookupEntity row) return;
        _editingHl = row;
        TxtHlCodigo.Text = row.Codigo;
        TxtHlDesc.Text = row.Descripcion;
    }

    private void BtnNuevoHl_Click(object sender, RoutedEventArgs e)
    {
        _editingHl = new HrLookupEntity();
        GridHrLookup.SelectedItem = null;
        TxtHlCodigo.Clear(); TxtHlDesc.Clear(); TxtHlDesc.Focus();
    }

    private async void BtnGuardarHl_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtHlDesc.Text)) return;
        _editingHl.Codigo = TxtHlCodigo.Text.Trim();
        _editingHl.Descripcion = TxtHlDesc.Text.Trim();
        await _stage3Repository.SaveHrLookupAsync(GetSelectedLookupTable(), _editingHl, _editingHl.Id > 0 ? "M" : "A");
        await LoadActiveHrLookupAsync();
    }

    private async void BtnEliminarHl_Click(object sender, RoutedEventArgs e)
    {
        if (_editingHl.Id <= 0) return;
        await _stage3Repository.DeleteHrLookupAsync(GetSelectedLookupTable(), _editingHl.Id);
        BtnNuevoHl_Click(sender, e);
        await LoadActiveHrLookupAsync();
    }
}
