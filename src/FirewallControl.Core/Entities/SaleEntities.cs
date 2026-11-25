using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FirewallControl.Core.Entities;

public class SaleEntity
{
    public int IdVenta { get; set; }
    public int NroVenta { get; set; }
    public string NroFactura { get; set; } = string.Empty;
    public DateTime FechaVenta { get; set; } = DateTime.Today;
    public string HoraVenta { get; set; } = DateTime.Now.ToString("HH:mm:ss");
    public int FormaPago { get; set; } = 1; // 1 = Contado, 2 = Crédito
    public int IdDeposito { get; set; } = 1;
    public int IdCaja { get; set; } = 1;
    public int IdCliente { get; set; } = 132; // 132 = OCASIONAL
    public string NombreCliente { get; set; } = "CLIENTE OCASIONAL";
    public string RucCliente { get; set; } = "X";
    public int IdVendedor { get; set; } = 10;
    public string NombreVendedor { get; set; } = string.Empty;
    public string Timbrado { get; set; } = string.Empty;
    public decimal TotalExenta { get; set; }
    public decimal TotalIva5 { get; set; }
    public decimal TotalIva10 { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Descuento { get; set; }
    public decimal TotalVenta { get; set; }
    public decimal LiquidacionIva5 { get; set; }
    public decimal LiquidacionIva10 { get; set; }
    public decimal TotalImpuesto { get; set; }
    public string CidUsuario { get; set; } = "ADMINISTRA";
    public string Concepto { get; set; } = "VENTA DE MERCADERIAS";
    public int Estado { get; set; } = 1;
    public int TipoBoleta { get; set; } = 1; // 1 = Ticket, 2 = Factura Legal
    public int NroBoleta { get; set; }
    public decimal CotDolar { get; set; }
    public decimal CotReal { get; set; }
    public decimal CotPeso { get; set; }
    public decimal CotEuro { get; set; }
    public decimal CotGuarani { get; set; } = 1m;
    public decimal MonedaGuarani { get; set; }
    public decimal MonedaDolar { get; set; }
    public decimal MonedaReal { get; set; }
    public decimal MonedaPeso { get; set; }
    public decimal MonedaEuro { get; set; }
    public string Observacion { get; set; } = string.Empty;
    public DateTime Vencimiento { get; set; } = DateTime.Today;
    public int IdMoneda { get; set; } = 1;
    public int TipoVenta { get; set; } = 1;
    public decimal EntregaGuarani { get; set; }
    public decimal EntregaDolar { get; set; }
    public decimal EntregaReal { get; set; }
    public decimal EntregaPeso { get; set; }
    public decimal EntregaEuro { get; set; }
    public decimal VueltoGuarani { get; set; }
    public decimal VueltoReal { get; set; }
    public decimal VueltoPeso { get; set; }
    public decimal VueltoDolar { get; set; }
    public int CobroCaja { get; set; }
    public string Terminal { get; set; } = Environment.MachineName;
    public int Cuota { get; set; } = 1;
    public int IdSucursal { get; set; } = 1;
    public string FormaPagoNombre => FormaPago == 2 ? "CRÉDITO" : "CONTADO";
    public List<SaleItemEntity> Items { get; set; } = new();
}

public class SaleItemEntity : INotifyPropertyChanged
{
    private int _recNo;
    private int _idProducto;
    private string _codigo = string.Empty;
    private string _descripcion = string.Empty;
    private decimal _cantidad = 1m;
    private decimal _preUnitario;
    private decimal _preExenta;
    private decimal _preIva5;
    private decimal _preIva10;
    private decimal _precioTotal;
    private int _controlaGarantia;
    private DateTime _fechaInicioGarantia = DateTime.Today;
    private int _diasGarantias;
    private DateTime _fechaFinGarantia = DateTime.Today;

    public int RecNo
    {
        get => _recNo;
        set { _recNo = value; OnPropertyChanged(); }
    }

    public int IdItemVenta { get; set; }
    public int IdVenta { get; set; }
    public int NroVenta { get; set; }
    public string NroFactura { get; set; } = string.Empty;

    public int IdProducto
    {
        get => _idProducto;
        set { _idProducto = value; OnPropertyChanged(); }
    }

    public string Codigo
    {
        get => _codigo;
        set { _codigo = value; OnPropertyChanged(); }
    }

    public string Descripcion
    {
        get => _descripcion;
        set { _descripcion = value; OnPropertyChanged(); }
    }

    public decimal Cantidad
    {
        get => _cantidad;
        set { _cantidad = value; OnPropertyChanged(); }
    }

    public decimal PreUnitario
    {
        get => _preUnitario;
        set { _preUnitario = value; OnPropertyChanged(); OnPropertyChanged(nameof(PrecioUnitario)); }
    }

    public decimal PrecioUnitario => _preUnitario;

    public decimal PreExenta
    {
        get => _preExenta;
        set { _preExenta = value; OnPropertyChanged(); }
    }

    public decimal PreIva5
    {
        get => _preIva5;
        set { _preIva5 = value; OnPropertyChanged(); }
    }

    public decimal PreIva10
    {
        get => _preIva10;
        set { _preIva10 = value; OnPropertyChanged(); }
    }

    public decimal PrecioTotal
    {
        get => _precioTotal;
        set { _precioTotal = value; OnPropertyChanged(); }
    }

    public int IdDeposito { get; set; } = 1;

    public int ControlaGarantia
    {
        get => _controlaGarantia;
        set { _controlaGarantia = value; OnPropertyChanged(); }
    }

    public DateTime FechaInicioGarantia
    {
        get => _fechaInicioGarantia;
        set { _fechaInicioGarantia = value; OnPropertyChanged(); }
    }

    public int DiasGarantias
    {
        get => _diasGarantias;
        set { _diasGarantias = value; OnPropertyChanged(); }
    }

    public DateTime FechaFinGarantia
    {
        get => _fechaFinGarantia;
        set { _fechaFinGarantia = value; OnPropertyChanged(); }
    }

    public int Estado { get; set; } = 1;
    public int Iva { get; set; } = 1; // 1=10%, 2=5%, 3=Exenta
    public int Fraccionable { get; set; }
    public int Compuesto { get; set; }
    public int NoControlaStock { get; set; }
    public decimal CostoUnitario { get; set; }
    public decimal Kilogramo { get; set; }
    public bool IsFromGenes { get; set; }

    public SaleItemEntity Clone() => (SaleItemEntity)MemberwiseClone();

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>
/// Represents a row in informe_lucro.scx (Costo Beneficio + Egresos) and informe_lucroproductos.scx (Productos Vendidos).
/// </summary>
public class ProfitReportItemEntity
{
    public int Grupo { get; set; } = 1; // 1 = Ventas/Lucros, 2 = Egresos de Caja
    public int NroVenta { get; set; }
    public DateTime FechaVenta { get; set; } = DateTime.Today;
    public string DesCliente { get; set; } = string.Empty;
    public string Ruc { get; set; } = string.Empty;
    public decimal TotalExenta { get; set; }
    public decimal TotalIva10 { get; set; }
    public decimal TotalIva5 { get; set; }
    public decimal TotalImpuesto { get; set; }
    public decimal TotalVenta { get; set; }
    public decimal CostoMedio { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string DesProducto { get; set; } = string.Empty;
    public decimal Cantidad { get; set; }
    public decimal PreUnitario { get; set; }
    public decimal PreExenta { get; set; }
    public decimal PreIva5 { get; set; }
    public decimal PreIva10 { get; set; }
    public int IdProducto { get; set; }
    public decimal Total { get; set; }
    public decimal Beneficio { get; set; }

    public string TipoMovimiento => Grupo == 2 ? "EGRESO CAJA" : "VENTA";
    public string ConceptoDisplay => Grupo == 2 ? $"[EGRESO] {DesProducto}" : (!string.IsNullOrWhiteSpace(DesCliente) ? DesCliente : DesProducto);
    public decimal MargenPorcentaje => TotalVenta > 0m ? Math.Round(((TotalVenta - CostoMedio) / TotalVenta) * 100m, 2) : 0m;

    // Convenience properties for XAML and report formatting
    public DateTime Fecha => FechaVenta;
    public string NroFactura => NroVenta > 0 ? NroVenta.ToString() : "-";
    public string Nombre => ConceptoDisplay;
    public decimal TotalCosto => CostoMedio;
    public decimal Egresos => Grupo == 2 ? Total : 0m;
    public decimal PorcentajeLucro => MargenPorcentaje;
    public string CodigoProducto => Codigo;
    public string DescripcionProducto => DesProducto;
    public decimal PrecioUnitarioPromedio => PreUnitario;
}
