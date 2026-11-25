namespace FirewallControl.Core.Entities;

public class CustomerEntity
{
    public int IdCliente { get; set; }
    public string Ruc { get; set; } = string.Empty;
    public string Dv { get; set; } = string.Empty;
    public string Nombres { get; set; } = string.Empty;
    public string Contacto { get; set; } = string.Empty;
    public string TelefonoContacto { get; set; } = string.Empty;
    public string Direccion { get; set; } = string.Empty;
    public int IdCiudad { get; set; }
    public string Telefono { get; set; } = string.Empty;
    public string Celular { get; set; } = string.Empty;
    public string Fax { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int IdVendedor { get; set; } = 10;
    public decimal LineaCredito { get; set; }
    public int FijarPrecios { get; set; }
    public int Contado { get; set; } = 1;
    public int Credito { get; set; }
    public int Cuota { get; set; }
    public int Estado { get; set; } = 1;
    public DateTime FechaAlta { get; set; } = DateTime.Today;
    public string UserId { get; set; } = "ADMINISTRA";
    public int Precio1 { get; set; } = 1;
    public int Precio2 { get; set; }
    public int Precio3 { get; set; }
    public int Precio4 { get; set; }
    public int Precio5 { get; set; }
    public int Precio6 { get; set; }
    public int Precio7 { get; set; }
    public int IdSucursal { get; set; } = 1;
    public string DesSucursal { get; set; } = "CASA CENTRAL";

    public int GetAssignedPriceList(int defaultList)
    {
        if (FijarPrecios == 0) return defaultList;
        if (Precio1 == 1) return 1;
        if (Precio2 == 1) return 2;
        if (Precio3 == 1) return 3;
        if (Precio4 == 1) return 4;
        if (Precio5 == 1) return 5;
        if (Precio6 == 1) return 6;
        if (Precio7 == 1) return 7;
        return defaultList;
    }
}

public class CustomerCollectionEntity
{
    public int IdCobro { get; set; }
    public int IdVenta { get; set; }
    public int NroVenta { get; set; }
    public string NroFactura { get; set; } = string.Empty;
    public DateTime Vencimiento { get; set; } = DateTime.Today;
    public string Cuota { get; set; } = "1";
    public DateTime Fecha { get; set; } = DateTime.Today;
    public int Dia { get; set; }
    public int IdCliente { get; set; }
    public string NombreCliente { get; set; } = string.Empty;
    public decimal Cobro { get; set; }
    public decimal Entrega { get; set; }
    public decimal Saldo => Cobro - Entrega;
    public DateTime FechaCobro { get; set; } = DateTime.Today;
    public string FormaCobro { get; set; } = "CONTADO";
    public int Mora { get; set; }
    public int IdCaja { get; set; } = 1;
    public string CidUsuario { get; set; } = "ADMINISTRA";
    public int IdSucursal { get; set; } = 1;
    public string DesSucursal { get; set; } = string.Empty;
    public int IdCobrador { get; set; }
    public string Obs { get; set; } = string.Empty;
}

public class SupplierEntity
{
    public int IdProveedor { get; set; }
    public string Ruc { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string Contacto { get; set; } = string.Empty;
    public string Direccion { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public string Celular { get; set; } = string.Empty;
    public string Fax { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int Estado { get; set; } = 1;
    public string Timbrado { get; set; } = string.Empty;
    public DateTime Fecha { get; set; } = DateTime.Today;
    public string UserId { get; set; } = "ADMINISTRA";
    public int IdCiudad { get; set; }
}

public class PurchaseEntity
{
    public int IdCompra { get; set; }
    public int NroCompra { get; set; }
    public string NroFactura { get; set; } = string.Empty;
    public DateTime FechaCompra { get; set; } = DateTime.Today;
    public int FormaPago { get; set; } = 1;
    public int IdDeposito { get; set; } = 1;
    public int IdCaja { get; set; } = 1;
    public int IdProveedor { get; set; }
    public string NombreProveedor { get; set; } = string.Empty;
    public string Timbrado { get; set; } = string.Empty;
    public decimal TotalExenta { get; set; }
    public decimal TotalIva5 { get; set; }
    public decimal TotalIva10 { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Descuento { get; set; }
    public decimal TotalCompra { get; set; }
    public decimal LiquidacionIva5 { get; set; }
    public decimal LiquidacionIva10 { get; set; }
    public decimal TotalImpuesto { get; set; }
    public string CidUsuario { get; set; } = "ADMINISTRA";
    public string Concepto { get; set; } = "COMPRA DE MERCADERIAS";
    public int Estado { get; set; } = 1;
    public int TipoBoleta { get; set; } = 1;
    public int NroBoleta { get; set; }
    public string Observacion { get; set; } = string.Empty;
    public DateTime Vencimiento { get; set; } = DateTime.Today;
    public int IdMoneda { get; set; } = 1;
    public int IdSucursal { get; set; } = 1;
    public string DesSucursal { get; set; } = "CASA CENTRAL";
    public List<PurchaseItemEntity> Items { get; set; } = new();
}

public class PurchaseItemEntity
{
    public int RecNo { get; set; }
    public int IdItemCompra { get; set; }
    public int IdCompra { get; set; }
    public string NroFactura { get; set; } = string.Empty;
    public int NroCompra { get; set; }
    public int IdProducto { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public decimal Cantidad { get; set; } = 1m;
    public decimal PreUnitario { get; set; }
    public decimal PreExenta { get; set; }
    public decimal PreIva5 { get; set; }
    public decimal PreIva10 { get; set; }
    public decimal PrecioTotal => PreExenta + PreIva5 + PreIva10;

    // Fields from itemsfacturacompras.scx for cost & sale price recalculation
    public int Iva { get; set; } = 1; // 1 = 10%, 2 = 5%, 3 = Exenta
    public decimal StockAnterior { get; set; }
    public decimal CostoAnterior { get; set; }
    public decimal CostoMedio { get; set; }
    public bool ActualizarPreciosVenta { get; set; } = true;
    public decimal P1 { get; set; }
    public decimal Precio1 { get; set; }
    public decimal P2 { get; set; }
    public decimal Precio2 { get; set; }
    public decimal P3 { get; set; }
    public decimal Precio3 { get; set; }
    public decimal P4 { get; set; }
    public decimal Precio4 { get; set; }
    public decimal P5 { get; set; }
    public decimal Precio5 { get; set; }
    public decimal P6 { get; set; }
    public decimal Precio6 { get; set; }
    public decimal P7 { get; set; }
    public decimal Precio7 { get; set; }

    // Expiration & Warranty fields from itemsfacturacompras.scx
    public int UsaVencimiento { get; set; }
    public string Lote { get; set; } = string.Empty;
    public DateTime? FechaVencimiento { get; set; }
    public int ControlaGarantia { get; set; }
    public DateTime? FechaInicioGarantia { get; set; }
    public int DiasGarantias { get; set; }
    public DateTime? FechaFinGarantia { get; set; }
}
