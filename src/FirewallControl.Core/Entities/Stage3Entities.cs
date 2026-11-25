namespace FirewallControl.Core.Entities;

public class FondoDashboardData
{
    public int TotalProductosActivos { get; set; }
    public int TotalClientes { get; set; }
    public decimal IngresosCajaHoy { get; set; }
    public decimal EgresosCajaHoy { get; set; }
    public decimal FlujoNetoCajaHoy => IngresosCajaHoy - EgresosCajaHoy;
    public int VentasCountHoy { get; set; }
    public decimal VentasTotalHoy { get; set; }
    public int AgendaPendientesHoy { get; set; }
    public bool TopIsFallback7Days { get; set; }
    public List<FondoRecentSaleRow> UltimasVentas { get; set; } = new();
    public List<FondoTopProductRow> TopVendidos { get; set; } = new();
}

public class FondoRecentSaleRow
{
    public int IdVenta { get; set; }
    public string NroVenta { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public string Hora { get; set; } = string.Empty;
    public string FormaPago { get; set; } = "Contado";
    public string Cliente { get; set; } = "OCASIONAL";
    public decimal Total { get; set; }
}

public class FondoTopProductRow
{
    public string Codigo { get; set; } = string.Empty;
    public string Producto { get; set; } = string.Empty;
    public decimal Cantidad { get; set; }
    public decimal TotalVenta { get; set; }
    public decimal GananciaEstimada { get; set; }
}

public class AgendaItemEntity
{
    public string Id { get; set; } = string.Empty;
    public DateTime Fecha { get; set; } = DateTime.Now;
    public string Asunto { get; set; } = string.Empty;
    public string Notas { get; set; } = string.Empty;
    public int Aviso { get; set; } = 1;
    public int TipoReg { get; set; } = 1;
    public string Registro { get; set; } = string.Empty;
    public string UserId { get; set; } = "ADMINISTRA";
}

public class OrganizerEntity
{
    public int IdAgenda { get; set; }
    public string Nombres { get; set; } = string.Empty;
    public string Telefonos1 { get; set; } = string.Empty;
    public string Telefonos2 { get; set; } = string.Empty;
    public string Telefonos3 { get; set; } = string.Empty;
}

public class SmsConnectionEntity
{
    public int IdConexion { get; set; }
    public string Descrip { get; set; } = string.Empty;
    public int Puerto { get; set; } = 1;
    public string BautRate { get; set; } = "9600";
    public string DataBits { get; set; } = "8";
    public string StopBits { get; set; } = "1";
    public string Parity { get; set; } = "N";
    public string Flow { get; set; } = "None";
    public int Inactivo { get; set; }
}

public class CityEntity
{
    public int IdCiudad { get; set; }
    public string Ciudad { get; set; } = string.Empty;
    public string Departamento { get; set; } = string.Empty;
}

public class BarrioEntity
{
    public int IdBarrio { get; set; }
    public string Barrio { get; set; } = string.Empty;
    public int IdCiudad { get; set; }
    public string CiudadNombre { get; set; } = string.Empty;
}

public class CashRubroEntity
{
    public int IdRubros { get; set; }
    public int NroRubro { get; set; }
    public string NombreRubro { get; set; } = string.Empty;
    public int TipoCuenta { get; set; } = 1;
    public int Proceso { get; set; } = 1;
}

public class CollectorEntity
{
    public int IdCobrador { get; set; }
    public string Nombres { get; set; } = string.Empty;
    public string Documento { get; set; } = string.Empty;
    public string Ruc { get; set; } = string.Empty;
    public string Telefonos { get; set; } = string.Empty;
    public string Celular { get; set; } = string.Empty;
    public string Direccion { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public decimal Comision { get; set; }
    public decimal Descuento { get; set; }
    public decimal ComisionFija { get; set; }
    public int Estado { get; set; } = 1;
}

public class SalesGoalEntity
{
    public int IdMeta { get; set; }
    public decimal Desde { get; set; }
    public decimal Hasta { get; set; }
    public DateTime Fecha { get; set; } = DateTime.Today;
}

public class SalesGoalTableEntity
{
    public int IdTablaMeta { get; set; }
    public decimal Desde { get; set; }
    public string Operacion { get; set; } = ">=";
    public decimal Hasta { get; set; }
    public decimal Porcentaje { get; set; }
    public decimal Monto { get; set; }
    public int Orden { get; set; } = 1;
    public int CalculaComision { get; set; } = 1;
}

public class SupplierCommissionEntity
{
    public int IdComision { get; set; }
    public int IdProveedor { get; set; }
    public string DesProveedor { get; set; } = string.Empty;
    public int IdVendedor { get; set; }
    public string DesVendedor { get; set; } = string.Empty;
    public decimal Comision { get; set; }
}

public class ReceiptAdvanceEntity
{
    public int IdRecibos { get; set; }
    public string Letra { get; set; } = "A";
    public string Recibo { get; set; } = string.Empty;
    public DateTime Fecha { get; set; } = DateTime.Today;
    public int IdCliente { get; set; }
    public int IdProveedor { get; set; }
    public int IdVendedor { get; set; }
    public int IdSupervisor { get; set; }
    public string Destinatario { get; set; } = string.Empty;
    public decimal Importe { get; set; }
    public string Concepto { get; set; } = string.Empty;
    public int IdCaja { get; set; } = 1;
    public int Estado { get; set; } = 1;
}

public class PurchaseOrderEntity
{
    public int IdPedido { get; set; }
    public int NroPedido { get; set; }
    public string NroFactura { get; set; } = string.Empty;
    public DateTime FechaPedido { get; set; } = DateTime.Today;
    public int IdDeposito { get; set; } = 1;
    public int IdProveedor { get; set; } = 1;
    public string DesProveedor { get; set; } = string.Empty;
    public decimal TotalPedido { get; set; }
    public string Observacion { get; set; } = string.Empty;
    public int Estado { get; set; } = 1;
    public int Facturado { get; set; }
    public List<PurchaseOrderItemEntity> Items { get; set; } = new();
}

public class PurchaseOrderItemEntity
{
    public int IdProducto { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public decimal Cantidad { get; set; } = 1m;
    public decimal PreUnitario { get; set; }
    public int Iva { get; set; } = 1;
    public decimal PreExenta { get; set; }
    public decimal PreIva5 { get; set; }
    public decimal PreIva10 { get; set; }
    public decimal Subtotal => PreExenta + PreIva5 + PreIva10;
}

public class ReturnHeaderEntity
{
    public int IdDevolucion { get; set; }
    public int IdDocumentoOrigen { get; set; }
    public string NroBoleta { get; set; } = string.Empty;
    public int IdTercero { get; set; }
    public string DesTercero { get; set; } = string.Empty;
    public string Ruc { get; set; } = string.Empty;
    public DateTime FechaDevolucion { get; set; } = DateTime.Today;
    public int IdDeposito { get; set; } = 1;
    public decimal Total { get; set; }
    public string Concepto { get; set; } = string.Empty;
    public string CidUsuario { get; set; } = "ADMINISTRA";
    public List<ReturnItemEntity> Items { get; set; } = new();
}

public class ReturnItemEntity
{
    public int IdProducto { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public decimal Cantidad { get; set; } = 1m;
    public decimal PreUnitario { get; set; }
    public int Iva { get; set; } = 1;
    public decimal Subtotal => Math.Round(Cantidad * PreUnitario, 0, MidpointRounding.AwayFromZero);
}

public class SupplierDebtEntity
{
    public int IdDeuda { get; set; }
    public int IdCompra { get; set; }
    public int NroCompra { get; set; }
    public string NroFactura { get; set; } = string.Empty;
    public int IdProveedor { get; set; }
    public string DesProveedor { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public decimal Entrega { get; set; }
    public decimal Saldo { get; set; }
    public DateTime Vencimiento { get; set; } = DateTime.Today;
    public string Cuota { get; set; } = "1/1";
    public DateTime Fecha { get; set; } = DateTime.Today;
}

public class SupplierPaymentEntity
{
    public int IdPago { get; set; }
    public int IdCompra { get; set; }
    public int NroCompra { get; set; }
    public string NroFactura { get; set; } = string.Empty;
    public int IdProveedor { get; set; }
    public string DesProveedor { get; set; } = string.Empty;
    public decimal Pago { get; set; }
    public decimal Entrega { get; set; }
    public DateTime FechaPago { get; set; } = DateTime.Today;
    public string Obs { get; set; } = string.Empty;
}

public class PayableAccountEntity
{
    public int IdCuentaPagar { get; set; }
    public DateTime FechaCarga { get; set; } = DateTime.Today;
    public int NroPagare { get; set; }
    public string NroFactura { get; set; } = string.Empty;
    public int IdProveedor { get; set; }
    public string DesProveedor { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public decimal Entrega { get; set; }
    public decimal Saldo { get; set; }
    public DateTime Vencimiento { get; set; } = DateTime.Today;
    public int Cuota { get; set; } = 1;
}

public class ReceivableAccountEntity
{
    public int IdCuentaRecibir { get; set; }
    public DateTime FechaCarga { get; set; } = DateTime.Today;
    public int NroPagare { get; set; }
    public string NroFactura { get; set; } = string.Empty;
    public int IdCliente { get; set; }
    public string DesCliente { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public decimal Entrega { get; set; }
    public decimal Saldo { get; set; }
    public DateTime Vencimiento { get; set; } = DateTime.Today;
    public int Cuota { get; set; } = 1;
    public decimal HonorarioJudicial { get; set; }
}

public class CreditInstallmentRow
{
    public int IdCredito { get; set; }
    public int IdVenta { get; set; }
    public int NroVenta { get; set; }
    public string NroFactura { get; set; } = string.Empty;
    public int IdCliente { get; set; }
    public string DesCliente { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public string Cuota { get; set; } = "1/1";
    public DateTime Vencimiento { get; set; } = DateTime.Today;
    public int DiasMora { get; set; }
    public decimal Total { get; set; }
    public decimal Entrega { get; set; }
    public decimal Saldo { get; set; }
    public decimal InteresMora { get; set; }
}

public class CreditApplicationEntity
{
    public int IdSolicitud { get; set; }
    public int IdCliente { get; set; }
    public string DesCliente { get; set; } = string.Empty;
    public int IdVendedor { get; set; }
    public string DesVendedor { get; set; } = string.Empty;
    public decimal Monto { get; set; }
    public int Plazo { get; set; } = 6;
    public decimal Cuota { get; set; }
    public DateTime FechaIngreso { get; set; } = DateTime.Today;
    public int Estado { get; set; } // 0=Pendiente, 1=Aprobada, 2=Rechazada
    public string Observaciones { get; set; } = string.Empty;
    public DateTime? FechaAprobacion { get; set; }
    public string ObsEstado { get; set; } = string.Empty;
    public string DesBanco { get; set; } = string.Empty;
    public string EstadoDisplay => Estado switch { 1 => "APROBADA", 2 => "RECHAZADA", _ => "PENDIENTE" };
}

public class ExtendedReportRow
{
    public string Clave { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string Extra1 { get; set; } = string.Empty;
    public string Extra2 { get; set; } = string.Empty;
    public decimal Cantidad { get; set; }
    public decimal Monto1 { get; set; }
    public decimal Monto2 { get; set; }
    public decimal Monto3 { get; set; }
}

public class UserTransactionLogEntity
{
    public int IdTranUsu { get; set; }
    public string FormTran { get; set; } = string.Empty;
    public string TablaTran { get; set; } = string.Empty;
    public string TipoTran { get; set; } = string.Empty;
    public DateTime FechaTran { get; set; }
    public string SqlTran { get; set; } = string.Empty;
    public int ClaveTran { get; set; }
    public string UserIdTran { get; set; } = string.Empty;
}

public class EmployeeLegajoEntity
{
    public int IdLegajo { get; set; }
    public string Apellidos { get; set; } = string.Empty;
    public string Nombres { get; set; } = string.Empty;
    public string Documento { get; set; } = string.Empty;
    public string TipoDoc { get; set; } = "CI";
    public string Nacionalidad { get; set; } = string.Empty;
    public string DesNacionalidad { get; set; } = string.Empty;
    public string Cargo { get; set; } = string.Empty;
    public string DesCargo { get; set; } = string.Empty;
    public string Departamento { get; set; } = string.Empty;
    public string DesDepartamento { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public string DesArea { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public string Direccion { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public DateTime FechaNacimiento { get; set; } = DateTime.Today.AddYears(-25);
    public DateTime FechaIngreso { get; set; } = DateTime.Today;
    public decimal Salario { get; set; }
    public int IpsNro { get; set; }
    public int EstadoCivil { get; set; }
    public int Sexo { get; set; }
    public string NombreCompleto => $"{Apellidos} {Nombres}".Trim();
}

public class EmployeeDiscountEntity
{
    public int IdDescuentoPersonal { get; set; }
    public int IdLegajo { get; set; }
    public string Documento { get; set; } = string.Empty;
    public string Nombres { get; set; } = string.Empty;
    public DateTime FechaDescuento { get; set; } = DateTime.Today;
    public decimal Monto { get; set; }
    public int Cuota { get; set; } = 1;
    public decimal Saldo { get; set; }
    public int IdDescuento { get; set; }
    public string CodDescuento { get; set; } = string.Empty;
    public string DesDescuento { get; set; } = string.Empty;
    public int Estado { get; set; } = 1;
    public decimal MontoCuota { get; set; }
}

public class SalaryPayrollEntity
{
    public int IdSalario { get; set; }
    public string Mes { get; set; } = DateTime.Today.Month.ToString("D2");
    public int Anio { get; set; } = DateTime.Today.Year;
    public int IdLegajo { get; set; }
    public string Documento { get; set; } = string.Empty;
    public string DesNombres { get; set; } = string.Empty;
    public int DiasTrabajados { get; set; } = 30;
    public int HorasExtra { get; set; }
    public decimal MontoBonificacion { get; set; }
    public decimal TotalIngreso { get; set; }
    public decimal Descuentos { get; set; }
    public decimal Salario { get; set; }
    public decimal MontoHorasExtras { get; set; }
    public decimal BonificacionFamiliar { get; set; }
    public decimal MontoIps { get; set; }
    public decimal TotalEgresos { get; set; }
    public decimal NetoPercibir { get; set; }
    public int CantidadFeriadoMes { get; set; }
    public DateTime FechaPagoSalario { get; set; } = DateTime.Today;
}

public class SalaryParametersEntity
{
    public int IdSalarioParametro { get; set; }
    public decimal SalarioMinimo { get; set; }
    public decimal IpsPatronal { get; set; } = 16.5m;
    public decimal IpsEmpleado { get; set; } = 9m;
    public decimal BonificacionFamiliar { get; set; }
    public int LimiteEdadHijos { get; set; } = 18;
    public decimal PrecioHoraExtra { get; set; }
    public int NroIpsPatronal { get; set; }
}

public class HrLookupEntity
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
}

public class RestaurantOrderItemRow
{
    public int IdVenta { get; set; }
    public int NroVenta { get; set; }
    public int Mesa { get; set; }
    public string HoraVenta { get; set; } = string.Empty;
    public string Codigo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public decimal Cantidad { get; set; }
    public string Sector { get; set; } = string.Empty;
    public string Observaciones { get; set; } = string.Empty;
    public decimal Total { get; set; }
}
