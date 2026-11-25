namespace FirewallControl.Core.Entities;

public class ProductGroupEntity
{
    public int IdGrupoProducto { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public decimal Descuento { get; set; }
    public DateTime? FechaInicio { get; set; }
    public DateTime? FechaFinal { get; set; }
    public string Impresora { get; set; } = string.Empty;
}

public class ProductSubGroupEntity
{
    public int IdSubGrupoProducto { get; set; }
    public string Descripcion { get; set; } = string.Empty;
}

public class ProductBrandEntity
{
    public int IdMarcaProducto { get; set; }
    public string Descripcion { get; set; } = string.Empty;
}

public class ProductModelEntity
{
    public int IdModeloProducto { get; set; }
    public string Descripcion { get; set; } = string.Empty;
}

public class ProductLocationEntity
{
    public string Suc { get; set; } = string.Empty;
    public string Gondola { get; set; } = string.Empty;
    public string Celda { get; set; } = string.Empty;
}

public class ProductDepositStockRow
{
    public int IdItemDeposito { get; set; }
    public int IdDeposito { get; set; }
    public string DesDeposito { get; set; } = string.Empty;
    public string Responsable { get; set; } = string.Empty;
    public int IdProducto { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string DesProducto { get; set; } = string.Empty;
    public decimal Cantidad { get; set; }
    public decimal CostoMedio { get; set; }
    public decimal Precio1 { get; set; }
    public decimal ValorCosto => Math.Round(Cantidad * CostoMedio, 0, MidpointRounding.AwayFromZero);
    public decimal ValorVenta => Math.Round(Cantidad * Precio1, 0, MidpointRounding.AwayFromZero);
}

public class ProductTransferEntity
{
    public int IdTrasladoDeposito { get; set; }
    public int IdDepositoOrigen { get; set; }
    public string DesDepositoOrigen { get; set; } = string.Empty;
    public int IdDepositoDestino { get; set; }
    public string DesDepositoDestino { get; set; } = string.Empty;
    public int IdProducto { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string DesProducto { get; set; } = string.Empty;
    public decimal Cantidad { get; set; }
    public string Observaciones { get; set; } = string.Empty;
    public string Autorizado { get; set; } = string.Empty;
    public DateTime Fecha { get; set; } = DateTime.Today;
    public string Hora { get; set; } = DateTime.Now.ToString("HH:mm:ss");
    public string CidUsuario { get; set; } = "ADMINISTRA";
}

public class FormAccessEntity
{
    public int Id { get; set; }
    public string TituloForm { get; set; } = string.Empty;
    public string NameForm { get; set; } = string.Empty;
    public string CidUsuario { get; set; } = string.Empty;
    public bool Buscar { get; set; }
    public bool Nuevo { get; set; }
    public bool Editar { get; set; }
    public bool Eliminar { get; set; }
    public bool Imprimir { get; set; }
}

public enum ProductSearchCriteria
{
    Descripcion = 0,
    Codigo = 1,
    Grupo = 2,
    Proveedor = 3
}

public class ProductSearchFilter
{
    public ProductSearchCriteria Criteria { get; set; } = ProductSearchCriteria.Descripcion;
    public string SearchText { get; set; } = string.Empty;
    public string AzTab { get; set; } = "Todo";
    public int IdDeposito { get; set; } = 1;
    public int? IdGrupo { get; set; }
    public int? IdSubGrupo { get; set; }
    public int? IdMarca { get; set; }
    public int? IdModelo { get; set; }
    public int? IdProveedor { get; set; }
    public bool IncludeInactive { get; set; }
    public string SortBy { get; set; } = "descripcion";
    public int Limit { get; set; } = 400;
}
