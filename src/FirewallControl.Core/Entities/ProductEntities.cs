using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FirewallControl.Core.Entities;

public class ProductEntity
{
    public int IdProducto { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public int IdMoneda { get; set; } = 1;
    public decimal Cambio { get; set; } = 1m;
    public decimal CostoMedio { get; set; }
    public decimal Costo1 { get; set; }
    public decimal P1 { get; set; }
    public decimal Precio1 { get; set; }
    public decimal Costo2 { get; set; }
    public decimal P2 { get; set; }
    public decimal Precio2 { get; set; }
    public decimal Costo3 { get; set; }
    public decimal P3 { get; set; }
    public decimal Precio3 { get; set; }
    public decimal Costo4 { get; set; }
    public decimal P4 { get; set; }
    public decimal Precio4 { get; set; }
    public decimal Costo5 { get; set; }
    public decimal P5 { get; set; }
    public decimal Precio5 { get; set; }
    public decimal Costo6 { get; set; }
    public decimal P6 { get; set; }
    public decimal Precio6 { get; set; }
    public decimal Costo7 { get; set; }
    public decimal P7 { get; set; }
    public decimal Precio7 { get; set; }
    public int IdProveedor { get; set; }
    public string Referencia { get; set; } = string.Empty;
    public int IdGrupoProducto { get; set; }
    public int IdSubGrupoProducto { get; set; }
    public int IdMarcaProducto { get; set; }
    public int IdModeloProducto { get; set; }
    public string Familia { get; set; } = string.Empty;
    public string Aplicacion { get; set; } = string.Empty;
    public string Observacion { get; set; } = string.Empty;
    public int StockMinimo { get; set; }
    public int StockMaximo { get; set; }
    public int Estado { get; set; } = 1;
    public int Iva { get; set; } = 1; // 1 = 10%, 2 = 5%, 3 = Exenta
    public int NoControlaStock { get; set; }
    public int UsaSerial { get; set; }
    public int ImprimeFactura { get; set; } = 1;
    public int AutoCantidad { get; set; }
    public int AutoPrecio { get; set; }
    public int UsaVencimiento { get; set; }
    public int DescripcionEditable { get; set; }
    public int VendeSinStock { get; set; } = 1;
    public int AutoSuma { get; set; } = 1;
    public int PublicarWeb { get; set; } = 1;
    public int Fraccionable { get; set; }
    public int Compuesto { get; set; }
    public string CodigoBarra { get; set; } = string.Empty;
    public int Impuesto { get; set; } = 10;
    public string Imagen { get; set; } = string.Empty;
    public DateTime? FechaUltimaCompra { get; set; }
    public decimal CantidadUltimaCompra { get; set; }
    public DateTime? FechaUltimaVenta { get; set; }
    public decimal CantidadUltimaVenta { get; set; }
    public decimal CostoMonedaExtranjera { get; set; }
    public int ControlaGarantia { get; set; }
    public DateTime? FechaInicioGarantia { get; set; }
    public int DiasGarantias { get; set; }
    public DateTime? FechaFinGarantia { get; set; }
    public int MuestraBusqueda { get; set; } = 1;
    public int Guarnicion { get; set; }
    public int Menu { get; set; }
    public bool Comanda { get; set; }
    public int Cocina { get; set; }
    public int Otros { get; set; }
    public int Churrasqueria { get; set; }
    public int Barra { get; set; }

    // Calculated/Joined field when querying with stock
    public decimal StockActual { get; set; }
    public string GrupoDescripcion { get; set; } = string.Empty;
    public string SubGrupoDescripcion { get; set; } = string.Empty;
    public string MarcaDescripcion { get; set; } = string.Empty;
    public string ModeloDescripcion { get; set; } = string.Empty;
    public string ProveedorDescripcion { get; set; } = string.Empty;
    public string ProveedorTelefono { get; set; } = string.Empty;
    public string ProveedorCelular { get; set; } = string.Empty;

    public string EstadoDescripcion => Estado switch
    {
        1 => "Activo",
        2 => "Inactivo",
        3 => "Bloqueado",
        _ => "Baja"
    };

    public decimal GetPrice(int listNumber) => listNumber switch
    {
        1 => Precio1,
        2 => Precio2 > 0 ? Precio2 : Precio1,
        3 => Precio3 > 0 ? Precio3 : Precio1,
        4 => Precio4 > 0 ? Precio4 : Precio1,
        5 => Precio5 > 0 ? Precio5 : Precio1,
        6 => Precio6 > 0 ? Precio6 : Precio1,
        7 => Precio7 > 0 ? Precio7 : Precio1,
        _ => Precio1
    };

    public decimal GetCost(int listNumber) => listNumber switch
    {
        1 => Costo1,
        2 => Costo2 > 0 ? Costo2 : Costo1,
        3 => Costo3 > 0 ? Costo3 : Costo1,
        4 => Costo4 > 0 ? Costo4 : Costo1,
        5 => Costo5 > 0 ? Costo5 : Costo1,
        6 => Costo6 > 0 ? Costo6 : Costo1,
        7 => Costo7 > 0 ? Costo7 : Costo1,
        _ => Costo1
    };

    public string IvaDescripcion => Iva switch
    {
        1 => "10%",
        2 => "5%",
        3 => "EXENTA",
        _ => "10%"
    };
}

public class CompoundProductComponent
{
    public int IdCompuesto { get; set; }
    public int IdProductoCompuesto { get; set; }
    public int IdProducto { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string DesProducto { get; set; } = string.Empty;
    public decimal Cantidad { get; set; }
}

public class DepositEntity
{
    public int IdDeposito { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public string Responsable { get; set; } = string.Empty;
    public int Estado { get; set; } = 1;
    public int IdSucursal { get; set; } = 1;
}

public class DepositStockEntity
{
    public int IdItemDeposito { get; set; }
    public int IdDeposito { get; set; }
    public int IdProducto { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public decimal Cantidad { get; set; }
}
