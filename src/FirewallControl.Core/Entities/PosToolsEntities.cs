namespace FirewallControl.Core.Entities;

public class BudgetSummaryEntity
{
    public int IdPresupuesto { get; set; }
    public int NroPresupuesto { get; set; }
    public string NroFactura { get; set; } = string.Empty;
    public DateTime FechaPresupuesto { get; set; } = DateTime.Today;
    public int IdCliente { get; set; } = 132;
    public string NombreCliente { get; set; } = "CLIENTE OCASIONAL";
    public string RucCliente { get; set; } = "X";
    public int IdVendedor { get; set; } = 10;
    public decimal Descuento { get; set; }
    public decimal TotalPresupuesto { get; set; }
    public string Observacion { get; set; } = string.Empty;
    public int Estado { get; set; } = 1;
}

public class BudgetItemEntity
{
    public int IdItemPresupuesto { get; set; }
    public int IdPresupuesto { get; set; }
    public int IdProducto { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public decimal Cantidad { get; set; } = 1m;
    public decimal PreUnitario { get; set; }
    public decimal PreExenta { get; set; }
    public decimal PreIva5 { get; set; }
    public decimal PreIva10 { get; set; }
    public int Iva { get; set; } = 1;
    public int Fraccionable { get; set; }
    public int Compuesto { get; set; }
    public int NoControlaStock { get; set; }
    public decimal CostoUnitario { get; set; }
    public decimal Subtotal => PreExenta + PreIva5 + PreIva10;
}

public class StockAdjustmentEntity
{
    public int IdAjuste { get; set; }
    public int NroAjuste { get; set; }
    public DateTime Fecha { get; set; } = DateTime.Today;
    public int IdDeposito { get; set; } = 1;
    public string AutorizadoPor { get; set; } = string.Empty;
    public int Motivo { get; set; } = 1;
    public string MotivoTexto { get; set; } = "CONTEO FÍSICO DE INVENTARIO";
    public string Especificar { get; set; } = string.Empty;
    public string Concepto { get; set; } = "AJUSTE DE STOCK";
    public decimal Sobrante { get; set; }
    public decimal Faltante { get; set; }
    public decimal TotalDiferencia { get; set; }
    public string CidUsuario { get; set; } = "ADMINISTRA";
    public int IdSucursal { get; set; } = 1;
    public string DesSucursal { get; set; } = "CASA CENTRAL";
    public List<StockAdjustmentItemEntity> Items { get; set; } = new();
}

public class StockAdjustmentItemEntity
{
    public int IdItemAjuste { get; set; }
    public int IdAjuste { get; set; }
    public int NroAjuste { get; set; }
    public int IdProducto { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public decimal StockAnterior { get; set; }
    public decimal CantidadContada { get; set; }
    public decimal Salida { get; set; }
    public decimal Entrada { get; set; }
    public decimal Precio { get; set; }
    public decimal Total { get; set; }
}

public class ProductLotEntity
{
    public int IdLote { get; set; }
    public int IdProducto { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string Lote { get; set; } = string.Empty;
    public DateTime Vencimiento { get; set; } = DateTime.Today;
    public decimal Cantidad { get; set; }
    public int IdDeposito { get; set; } = 1;
}

public class SaleInstallmentEntity
{
    public int NumeroCuota { get; set; }
    public string CuotaTexto { get; set; } = "1/1";
    public DateTime Vencimiento { get; set; } = DateTime.Today.AddDays(30);
    public int Dias { get; set; } = 30;
    public decimal Monto { get; set; }
    public decimal Entrega { get; set; }
    public decimal Saldo { get; set; }
}

public class SupervisorEntity
{
    public int IdSupervisor { get; set; }
    public string Nombres { get; set; } = string.Empty;
    public int Estado { get; set; } = 1;
}

public class FastCargaQueueItem
{
    public int ItemId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public decimal Costo { get; set; }
    public string Estado { get; set; } = "PENDIENTE";
}

public class FastCargaSnapshot
{
    public string Codigo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public decimal CostoAnterior { get; set; }
    public decimal CostoNuevo { get; set; }
    public decimal PrecioVenta { get; set; }
    public bool Fraccionable { get; set; }
    public bool NoControlaStock { get; set; } = true;
    public bool EsEdicion { get; set; }
    public decimal CostoAnteriorBd { get; set; }
    public decimal VentaAnteriorBd { get; set; }
    public string LastLookupCode { get; set; } = string.Empty;
    public string StatusMessage { get; set; } = string.Empty;
    public string LastFocusedControl { get; set; } = "TxtFastCod";
}
