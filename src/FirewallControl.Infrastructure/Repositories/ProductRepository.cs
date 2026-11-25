using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using FirewallControl.Core.Entities;
using FirewallControl.Core.Interfaces;

namespace FirewallControl.Infrastructure.Repositories;

/// <summary>
/// Implements IProductRepository against the real MySQL 5.5 schema (productos, itemsdepositos, depositos, vw_compuesto, ajustes, itemsajustes).
/// Replicates ITEMSFACTURAVENTAS.codproducto.Valid, crear_productos.scx, abm_productos.scx, crear_productos_ajuste.scx, and crear_ajustes.scx.
/// </summary>
public class ProductRepository : IProductRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public ProductRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    private const string SelectProductColumns = @"
        p.idproducto AS IdProducto,
        p.codigo AS Codigo,
        p.descripcion AS Descripcion,
        p.idmoneda AS IdMoneda,
        p.cambio AS Cambio,
        p.costomedio AS CostoMedio,
        p.costo1 AS Costo1, p.p1 AS P1, p.precio1 AS Precio1,
        p.costo2 AS Costo2, p.p2 AS P2, p.precio2 AS Precio2,
        p.costo3 AS Costo3, p.p3 AS P3, p.precio3 AS Precio3,
        p.costo4 AS Costo4, p.p4 AS P4, p.precio4 AS Precio4,
        p.costo5 AS Costo5, p.p5 AS P5, p.precio5 AS Precio5,
        p.costo6 AS Costo6, p.p6 AS P6, p.precio6 AS Precio6,
        p.costo7 AS Costo7, p.p7 AS P7, p.precio7 AS Precio7,
        p.idproveedor AS IdProveedor,
        p.referencia AS Referencia,
        p.idgrupoproducto AS IdGrupoProducto,
        p.idsubgrupoproducto AS IdSubGrupoProducto,
        p.idmarcaproducto AS IdMarcaProducto,
        p.idmodeloproducto AS IdModeloProducto,
        p.familia AS Familia,
        p.aplicacion AS Aplicacion,
        p.observacion AS Observacion,
        p.stockminimo AS StockMinimo,
        p.stockmaximo AS StockMaximo,
        p.estado AS Estado,
        p.iva AS Iva,
        p.nocontrolastock AS NoControlaStock,
        p.usaserial AS UsaSerial,
        p.imprimefactura AS ImprimeFactura,
        p.autocantidad AS AutoCantidad,
        p.autoprecio AS AutoPrecio,
        p.usavencimiento AS UsaVencimiento,
        p.descripcioneditable AS DescripcionEditable,
        p.vendesinstock AS VendeSinStock,
        p.autosuma AS AutoSuma,
        p.publicarweb AS PublicarWeb,
        p.fraccionable AS Fraccionable,
        p.compuesto AS Compuesto,
        p.codigobarra AS CodigoBarra,
        p.impuesto AS Impuesto,
        p.imagen AS Imagen,
        CASE WHEN p.fechaultimacompra IS NULL OR CAST(p.fechaultimacompra AS CHAR) = '0000-00-00' THEN NULL ELSE p.fechaultimacompra END AS FechaUltimaCompra,
        p.cantidadultimacompra AS CantidadUltimaCompra,
        CASE WHEN p.fechaultimaventa IS NULL OR CAST(p.fechaultimaventa AS CHAR) = '0000-00-00' THEN NULL ELSE p.fechaultimaventa END AS FechaUltimaVenta,
        p.cantidadultimaventa AS CantidadUltimaVenta,
        p.costomonedaextranjera AS CostoMonedaExtranjera,
        p.controlagarantia AS ControlaGarantia,
        CASE WHEN p.fechainiciogarantia IS NULL OR CAST(p.fechainiciogarantia AS CHAR) = '0000-00-00' THEN NULL ELSE p.fechainiciogarantia END AS FechaInicioGarantia,
        p.diasgarantias AS DiasGarantias,
        CASE WHEN p.fechafingarantia IS NULL OR CAST(p.fechafingarantia AS CHAR) = '0000-00-00' THEN NULL ELSE p.fechafingarantia END AS FechaFinGarantia,
        p.muestrabusqueda AS MuestraBusqueda,
        p.guarnicion AS Guarnicion,
        p.menu AS Menu,
        CAST(p.comanda AS UNSIGNED) AS Comanda,
        p.cocina AS Cocina,
        p.otros AS Otros,
        p.churrasqueria AS Churrasqueria,
        p.barra AS Barra";

    private const string ProductJoinFromClause = @"
        FROM productos p
        LEFT JOIN grupoproductos g ON p.idgrupoproducto = g.idgrupoproducto
        LEFT JOIN subgrupoproductos sg ON p.idsubgrupoproducto = sg.idsubgrupoproducto
        LEFT JOIN marcaproductos m ON p.idmarcaproducto = m.idmarcaproducto
        LEFT JOIN modeloproductos mo ON p.idmodeloproducto = mo.idmodeloproducto
        LEFT JOIN proveedores pr ON p.idproveedor = pr.idproveedor";

    private const string JoinedDisplayColumns = @"
        COALESCE(g.descripcion, '**SIN GRUPO**') AS GrupoDescripcion,
        COALESCE(sg.descripcion, '') AS SubGrupoDescripcion,
        COALESCE(m.descripcion, '') AS MarcaDescripcion,
        COALESCE(mo.descripcion, '') AS ModeloDescripcion,
        COALESCE(pr.descripcion, '') AS ProveedorDescripcion,
        COALESCE(pr.telefono, '') AS ProveedorTelefono,
        COALESCE(pr.celular, '') AS ProveedorCelular";

    private static string Trunc(string? s, int maxLen)
    {
        string clean = (s ?? string.Empty).Trim();
        return clean.Length > maxLen ? clean[..maxLen] : clean;
    }

    public async Task<ProductEntity?> GetByCodeAsync(string code, int idDeposito = 1)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        using var conn = _connectionFactory.CreateConnection();
        string sql = $@"
            SELECT {SelectProductColumns},
                   {JoinedDisplayColumns},
                   COALESCE((SELECT SUM(d.cantidad) FROM itemsdepositos d WHERE d.idproducto = p.idproducto AND d.iddeposito = @IdDeposito), 0) AS StockActual
            {ProductJoinFromClause}
            WHERE (TRIM(p.codigo) = @Code OR TRIM(p.codigobarra) = @Code)
              AND p.estado = 1
            LIMIT 1;";

        return await conn.QueryFirstOrDefaultAsync<ProductEntity>(sql, new { Code = code.Trim(), IdDeposito = idDeposito });
    }

    public async Task<ProductEntity?> GetByCodeIncludingInactiveAsync(string code, int idDeposito = 1)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        using var conn = _connectionFactory.CreateConnection();
        string sql = $@"
            SELECT {SelectProductColumns},
                   {JoinedDisplayColumns},
                   COALESCE((SELECT SUM(d.cantidad) FROM itemsdepositos d WHERE d.idproducto = p.idproducto AND d.iddeposito = @IdDeposito), 0) AS StockActual
            {ProductJoinFromClause}
            WHERE (TRIM(p.codigo) = @Code OR TRIM(p.codigobarra) = @Code)
            ORDER BY p.estado = 1 DESC, p.idproducto ASC
            LIMIT 1;";

        return await conn.QueryFirstOrDefaultAsync<ProductEntity>(sql, new { Code = code.Trim(), IdDeposito = idDeposito });
    }

    public async Task<ProductEntity?> GetByIdAsync(int idProducto, int idDeposito = 1)
    {
        using var conn = _connectionFactory.CreateConnection();
        string sql = $@"
            SELECT {SelectProductColumns},
                   {JoinedDisplayColumns},
                   COALESCE((SELECT SUM(d.cantidad) FROM itemsdepositos d WHERE d.idproducto = p.idproducto AND d.iddeposito = @IdDeposito), 0) AS StockActual
            {ProductJoinFromClause}
            WHERE p.idproducto = @Id
            LIMIT 1;";

        return await conn.QueryFirstOrDefaultAsync<ProductEntity>(sql, new { Id = idProducto, IdDeposito = idDeposito });
    }

    public async Task<int> GetNextProductIdAsync()
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = "SELECT COALESCE(MAX(idproducto), 0) + 1 FROM productos;";
        return await conn.ExecuteScalarAsync<int>(sql);
    }

    public async Task<Dictionary<string, (decimal Precio1, decimal Costo1)>> GetPricesByCodesAsync(IEnumerable<string> codes)
    {
        var result = new Dictionary<string, (decimal Precio1, decimal Costo1)>(StringComparer.OrdinalIgnoreCase);
        var cleanCodes = codes
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (cleanCodes.Length == 0)
            return result;

        using var conn = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT TRIM(codigo) AS Codigo,
                   ROUND(IFNULL(precio1, 0), 0) AS Precio1,
                   ROUND(IFNULL(costo1, 0), 0) AS Costo1
            FROM productos
            WHERE TRIM(codigo) IN @Codes;";

        var rows = await conn.QueryAsync<(string Codigo, decimal Precio1, decimal Costo1)>(sql, new { Codes = cleanCodes });
        foreach (var r in rows)
        {
            if (!string.IsNullOrWhiteSpace(r.Codigo))
            {
                result[r.Codigo.Trim()] = (r.Precio1, r.Costo1);
            }
        }

        return result;
    }

    public async Task<List<ProductEntity>> SearchProductsAsync(string searchText, int idDeposito = 1, int limit = 100)
    {
        using var conn = _connectionFactory.CreateConnection();
        string term = (searchText ?? "").Trim();

        if (string.IsNullOrEmpty(term))
        {
            string sqlAll = $@"
                SELECT {SelectProductColumns},
                       {JoinedDisplayColumns},
                       COALESCE((SELECT SUM(d.cantidad) FROM itemsdepositos d WHERE d.idproducto = p.idproducto AND d.iddeposito = @IdDeposito), 0) AS StockActual
                {ProductJoinFromClause}
                WHERE p.estado = 1
                ORDER BY p.descripcion
                LIMIT @Limit;";
            var rows = await conn.QueryAsync<ProductEntity>(sqlAll, new { IdDeposito = idDeposito, Limit = limit });
            return rows.ToList();
        }

        string sqlSearch = $@"
            SELECT {SelectProductColumns},
                   {JoinedDisplayColumns},
                   COALESCE((SELECT SUM(d.cantidad) FROM itemsdepositos d WHERE d.idproducto = p.idproducto AND d.iddeposito = @IdDeposito), 0) AS StockActual
            {ProductJoinFromClause}
            WHERE p.estado = 1
              AND (p.codigo LIKE @Prefix OR p.descripcion LIKE @Contains OR p.codigobarra LIKE @Prefix)
            ORDER BY CASE WHEN TRIM(p.codigo) = @Raw THEN 0 ELSE 1 END, p.descripcion
            LIMIT @Limit;";

        var list = await conn.QueryAsync<ProductEntity>(sqlSearch, new
        {
            Raw = term,
            Prefix = $"{term}%",
            Contains = $"%{term}%",
            IdDeposito = idDeposito,
            Limit = limit
        });
        return list.ToList();
    }

    public async Task<List<ProductEntity>> SearchProductsAdvancedAsync(ProductSearchFilter filter)
    {
        using var conn = _connectionFactory.CreateConnection();
        var parameters = new DynamicParameters();
        parameters.Add("IdDeposito", filter.IdDeposito > 0 ? filter.IdDeposito : 1);
        parameters.Add("Limit", filter.Limit > 0 ? filter.Limit : 400);

        var where = new List<string>();
        if (!filter.IncludeInactive)
            where.Add("p.estado = 1");

        if (filter.IdGrupo is > 0)
        {
            where.Add("p.idgrupoproducto = @IdGrupo");
            parameters.Add("IdGrupo", filter.IdGrupo.Value);
        }
        if (filter.IdSubGrupo is > 0)
        {
            where.Add("p.idsubgrupoproducto = @IdSubGrupo");
            parameters.Add("IdSubGrupo", filter.IdSubGrupo.Value);
        }
        if (filter.IdMarca is > 0)
        {
            where.Add("p.idmarcaproducto = @IdMarca");
            parameters.Add("IdMarca", filter.IdMarca.Value);
        }
        if (filter.IdModelo is > 0)
        {
            where.Add("p.idmodeloproducto = @IdModelo");
            parameters.Add("IdModelo", filter.IdModelo.Value);
        }
        if (filter.IdProveedor is > 0)
        {
            where.Add("p.idproveedor = @IdProveedor");
            parameters.Add("IdProveedor", filter.IdProveedor.Value);
        }

        string az = (filter.AzTab ?? "Todo").Trim().ToUpperInvariant();
        if (az is "A-C" or "D-F" or "G-I" or "J-L" or "M-O" or "P-R" or "S-U" or "V-X" or "Y-Z")
        {
            var parts = az.Split('-');
            where.Add("UPPER(LEFT(TRIM(p.descripcion), 1)) BETWEEN @AzLow AND @AzHigh");
            parameters.Add("AzLow", parts[0]);
            parameters.Add("AzHigh", parts[1]);
        }

        string term = (filter.SearchText ?? string.Empty).Trim();
        if (!string.IsNullOrEmpty(term))
        {
            if (filter.Criteria == ProductSearchCriteria.Codigo)
            {
                where.Add("(UPPER(TRIM(p.codigo)) LIKE @CodLike OR UPPER(TRIM(p.codigobarra)) LIKE @CodLike)");
                parameters.Add("CodLike", $"%{term.ToUpperInvariant()}%");
            }
            else
            {
                string col = filter.Criteria switch
                {
                    ProductSearchCriteria.Grupo => "UPPER(COALESCE(g.descripcion, ''))",
                    ProductSearchCriteria.Proveedor => "UPPER(COALESCE(pr.descripcion, ''))",
                    _ => "UPPER(CONCAT(COALESCE(p.descripcion,''), ' ', COALESCE(p.familia,''), ' ', COALESCE(p.referencia,'')))"
                };

                var words = term.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                                .Take(6)
                                .ToArray();
                for (int i = 0; i < words.Length; i++)
                {
                    string pName = $"w{i}";
                    where.Add($"{col} LIKE @{pName}");
                    parameters.Add(pName, $"%{words[i].ToUpperInvariant()}%");
                }
            }
        }

        string whereSql = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : string.Empty;
        string orderSql = string.Equals(filter.SortBy, "codigo", StringComparison.OrdinalIgnoreCase)
            ? "ORDER BY p.codigo, p.descripcion"
            : string.Equals(filter.SortBy, "idproducto", StringComparison.OrdinalIgnoreCase)
                ? "ORDER BY p.idproducto DESC"
                : "ORDER BY p.descripcion";

        string sql = $@"
            SELECT {SelectProductColumns},
                   {JoinedDisplayColumns},
                   COALESCE((SELECT SUM(d.cantidad) FROM itemsdepositos d WHERE d.idproducto = p.idproducto AND d.iddeposito = @IdDeposito), 0) AS StockActual
            {ProductJoinFromClause}
            {whereSql}
            {orderSql}
            LIMIT @Limit;";

        var rows = await conn.QueryAsync<ProductEntity>(sql, parameters);
        return rows.ToList();
    }

    public async Task<decimal> GetDepositStockAsync(int idProducto, int idDeposito)
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT COALESCE(SUM(cantidad), 0)
            FROM itemsdepositos
            WHERE idproducto = @IdProducto AND iddeposito = @IdDeposito;";

        return await conn.ExecuteScalarAsync<decimal>(sql, new { IdProducto = idProducto, IdDeposito = idDeposito });
    }

    public async Task<List<CompoundProductComponent>> GetCompoundComponentsAsync(int idProductoCompuesto)
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT idcompuesto AS IdCompuesto,
                   idproductocompuesto AS IdProductoCompuesto,
                   idproducto AS IdProducto,
                   codigo AS Codigo,
                   DesProducto AS DesProducto,
                   cantidad AS Cantidad
            FROM vw_compuesto
            WHERE idproductocompuesto = @IdCompuesto;";

        var rows = await conn.QueryAsync<CompoundProductComponent>(sql, new { IdCompuesto = idProductoCompuesto });
        return rows.ToList();
    }

    public async Task<int> SaveCompoundComponentAsync(
        int idProductoCompuesto, int idProductoComponente, string codigoComponente, decimal cantidad, int idSucursal = 1, string desSucursal = "")
    {
        using var conn = _connectionFactory.CreateConnection();
        using var tx = conn.BeginTransaction();
        try
        {
            const string insSql = @"
                INSERT INTO compuesto
                (idproductocompuesto, idproducto, codigo, idsucursal0, dessucursal, cantidad)
                VALUES
                (@IdCompuesto, @IdComponente, @Codigo, @IdSucursal, @DesSucursal, @Cantidad);
                SELECT LAST_INSERT_ID();";
            int id = await conn.ExecuteScalarAsync<int>(insSql, new
            {
                IdCompuesto = idProductoCompuesto,
                IdComponente = idProductoComponente,
                Codigo = codigoComponente ?? string.Empty,
                IdSucursal = idSucursal,
                DesSucursal = desSucursal ?? string.Empty,
                Cantidad = cantidad
            }, tx);

            await conn.ExecuteAsync(
                "UPDATE productos SET compuesto = 1 WHERE idproducto = @Id;",
                new { Id = idProductoCompuesto }, tx);

            tx.Commit();
            return id;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task<bool> DeleteCompoundComponentAsync(int idCompuesto)
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = "DELETE FROM compuesto WHERE idcompuesto = @Id;";
        int rows = await conn.ExecuteAsync(sql, new { Id = idCompuesto });
        return rows > 0;
    }

    public async Task<List<ProductLotEntity>> GetProductLotsAsync(int idProducto, int? idDeposito = null)
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT l.idlote AS IdLote,
                   l.idproducto AS IdProducto,
                   COALESCE(p.codigo, '') AS Codigo,
                   COALESCE(p.descripcion, '') AS Descripcion,
                   COALESCE(l.lote, '') AS Lote,
                   CASE WHEN l.vencimiento IS NULL OR CAST(l.vencimiento AS CHAR) = '0000-00-00' THEN CURDATE() ELSE l.vencimiento END AS Vencimiento,
                   COALESCE(l.cantidad, 0) AS Cantidad,
                   COALESCE(l.iddeposito, 1) AS IdDeposito
            FROM lote_producto l
            INNER JOIN productos p ON l.idproducto = p.idproducto
            WHERE (@IdProducto <= 0 OR l.idproducto = @IdProducto)
              AND (@IdDeposito IS NULL OR l.iddeposito = @IdDeposito)
            ORDER BY Vencimiento ASC, l.idlote ASC;";
        var rows = await conn.QueryAsync<ProductLotEntity>(sql, new { IdProducto = idProducto, IdDeposito = idDeposito });
        return rows.ToList();
    }

    public async Task<int> SaveFastProductAsync(
        string codigo, string descripcion, decimal costo, decimal precioVenta, bool fraccionable, bool isEdit, bool noControlaStock = true)
    {
        string cleanCod = (codigo ?? string.Empty).Trim();
        string cleanDesc = (descripcion ?? string.Empty).Trim().ToUpperInvariant();
        if (cleanCod.Length > 25) cleanCod = cleanCod[..25];
        if (cleanDesc.Length > 250) cleanDesc = cleanDesc[..250];
        int fracVal = fraccionable ? 1 : 0;
        int noControlVal = noControlaStock ? 1 : 0;
        decimal pUtil = costo > 0m && precioVenta > 0m
            ? Math.Round(((precioVenta - costo) / costo) * 100m, 2, MidpointRounding.AwayFromZero)
            : 0m;

        using var conn = _connectionFactory.CreateConnection();
        using var tx = conn.BeginTransaction();
        try
        {
            if (isEdit)
            {
                const string updSql = @"
                    UPDATE productos SET
                        descripcion = @Desc,
                        fraccionable = @Frac,
                        nocontrolastock = @NoControlaStock,
                        costomedio = @Costo,
                        costo1 = @Costo, costo2 = @Costo, costo3 = @Costo, costo4 = @Costo,
                        costo5 = @Costo, costo6 = @Costo, costo7 = @Costo,
                        p1 = @PUtil, p2 = @PUtil, p3 = @PUtil, p4 = @PUtil,
                        p5 = @PUtil, p6 = @PUtil, p7 = @PUtil,
                        precio1 = @Venta, precio2 = @Venta, precio3 = @Venta, precio4 = @Venta,
                        precio5 = @Venta, precio6 = @Venta, precio7 = @Venta
                    WHERE TRIM(codigo) = @Cod;";
                await conn.ExecuteAsync(updSql, new
                {
                    Desc = cleanDesc,
                    Frac = fracVal,
                    NoControlaStock = noControlVal,
                    Costo = costo,
                    PUtil = pUtil,
                    Venta = precioVenta,
                    Cod = cleanCod
                }, tx);

                int existingId = await conn.ExecuteScalarAsync<int>(
                    "SELECT COALESCE(MAX(idproducto), 0) FROM productos WHERE TRIM(codigo) = @Cod;",
                    new { Cod = cleanCod }, tx);
                tx.Commit();
                return existingId;
            }
            else
            {
                const string insSql = @"
                    INSERT INTO productos (
                        codigo, descripcion, costomedio,
                        costo1, costo2, costo3, costo4, costo5, costo6, costo7,
                        p1, p2, p3, p4, p5, p6, p7,
                        precio1, precio2, precio3, precio4, precio5, precio6, precio7,
                        nocontrolastock, idgrupoproducto, idproveedor, idmarcaproducto, idmoneda, estado,
                        iva, impuesto, muestrabusqueda, aplicacion, observacion, referencia,
                        familia, codigobarra, imagen, imprimefactura, vendesinstock, autosuma,
                        publicarweb, fraccionable
                    ) VALUES (
                        @Cod, @Desc, @Costo,
                        @Costo, @Costo, @Costo, @Costo, @Costo, @Costo, @Costo,
                        @PUtil, @PUtil, @PUtil, @PUtil, @PUtil, @PUtil, @PUtil,
                        @Venta, @Venta, @Venta, @Venta, @Venta, @Venta, @Venta,
                        @NoControlaStock, 1, 30, 1, 1, 1,
                        1, 10, 1, '', '', '',
                        '', '', '', 1, 1, 1,
                        1, @Frac
                    );
                    SELECT LAST_INSERT_ID();";
                int newId = await conn.ExecuteScalarAsync<int>(insSql, new
                {
                    Cod = cleanCod,
                    Desc = cleanDesc,
                    Costo = costo,
                    PUtil = pUtil,
                    Venta = precioVenta,
                    NoControlaStock = noControlVal,
                    Frac = fracVal
                }, tx);

                if (newId > 0)
                {
                    int hasDep = await conn.ExecuteScalarAsync<int>(
                        "SELECT COUNT(*) FROM itemsdepositos WHERE idproducto = @Id AND iddeposito = 1;",
                        new { Id = newId }, tx);
                    if (hasDep == 0)
                    {
                        await conn.ExecuteAsync(
                            "INSERT INTO itemsdepositos (idproducto, iddeposito, codigo, cantidad) VALUES (@Id, 1, @Cod, 0);",
                            new { Id = newId, Cod = cleanCod }, tx);
                    }
                }

                tx.Commit();
                return newId;
            }
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task<bool> IsCodeInUseAsync(string codigo, int excludeIdProducto = 0)
    {
        string clean = (codigo ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(clean))
            return false;

        using var conn = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT COUNT(*)
            FROM productos
            WHERE UPPER(TRIM(codigo)) = UPPER(@Code)
              AND idproducto <> @ExcludeId;";
        int cnt = await conn.ExecuteScalarAsync<int>(sql, new { Code = clean, ExcludeId = excludeIdProducto });
        return cnt > 0;
    }

    public Task<int> SaveProductAsync(ProductEntity product, string lcTipo)
        => SaveProductFullAsync(product, lcTipo, 1, "ADMINISTRA");

    public async Task<int> SaveProductFullAsync(
        ProductEntity product, string lcTipo, int idDeposito = 1, string usuario = "ADMINISTRA")
    {
        string cleanCod = Trunc(product.Codigo, 25).ToUpperInvariant();
        if (string.IsNullOrEmpty(cleanCod))
            throw new InvalidOperationException("El código de producto no puede estar vacío.");

        bool isModify = string.Equals(lcTipo, "M", StringComparison.OrdinalIgnoreCase) && product.IdProducto > 0;
        if (await IsCodeInUseAsync(cleanCod, isModify ? product.IdProducto : 0))
            throw new InvalidOperationException($"Ya existe otro producto registrado con el código '{cleanCod}'.");

        product.Codigo = cleanCod;
        product.Descripcion = Trunc(product.Descripcion, 250).ToUpperInvariant();
        product.Referencia = Trunc(product.Referencia, 100).ToUpperInvariant();
        product.Familia = Trunc(product.Familia, 100).ToUpperInvariant();
        product.Aplicacion = product.Aplicacion?.Trim() ?? string.Empty;
        product.Observacion = product.Observacion?.Trim() ?? string.Empty;
        product.CodigoBarra = Trunc(string.IsNullOrWhiteSpace(product.CodigoBarra) ? cleanCod : product.CodigoBarra, 30);
        product.Imagen = Trunc(product.Imagen, 60);
        product.Impuesto = product.Iva == 2 ? 5 : (product.Iva == 3 ? 0 : 10);
        product.MuestraBusqueda = 1;
        if (product.IdMoneda <= 0) product.IdMoneda = 1;
        if (product.Cambio <= 0m) product.Cambio = 1m;
        if (product.CostoMedio <= 0m && product.Costo1 > 0m) product.CostoMedio = product.Costo1;

        // VFP abm_productos fallback rules for lists 2..7
        if (product.Costo2 <= 0m) product.Costo2 = product.Costo1;
        if (product.P2 == 0m && product.Precio2 == 0m) { product.P2 = product.P1; product.Precio2 = product.Precio1; }
        if (product.Costo3 <= 0m) product.Costo3 = product.Costo1;
        if (product.P3 == 0m && product.Precio3 == 0m) { product.P3 = product.P1; product.Precio3 = product.Precio1; }
        if (product.Costo4 <= 0m) product.Costo4 = product.Costo1;
        if (product.P4 == 0m && product.Precio4 == 0m) { product.P4 = product.P1; product.Precio4 = product.Precio1; }
        if (product.Costo5 <= 0m) product.Costo5 = product.Costo1;
        if (product.P5 == 0m && product.Precio5 == 0m) { product.P5 = product.P1; product.Precio5 = product.Precio1; }
        if (product.Costo6 <= 0m) product.Costo6 = product.Costo1;
        if (product.P6 == 0m && product.Precio6 == 0m) { product.P6 = product.P1; product.Precio6 = product.Precio1; }
        if (product.Costo7 <= 0m) product.Costo7 = product.Costo1;
        if (product.P7 == 0m && product.Precio7 == 0m) { product.P7 = product.P1; product.Precio7 = product.Precio1; }

        int depId = idDeposito > 0 ? idDeposito : 1;
        string fechaIniGar = product.FechaInicioGarantia.HasValue
            ? product.FechaInicioGarantia.Value.ToString("yyyy-MM-dd")
            : "0000-00-00";
        string fechaFinGar = product.FechaFinGarantia.HasValue
            ? product.FechaFinGarantia.Value.ToString("yyyy-MM-dd")
            : "0000-00-00";

        object BuildParams() => new
        {
            product.IdProducto,
            product.Codigo,
            product.Descripcion,
            product.IdMoneda,
            product.Cambio,
            product.CostoMedio,
            product.Costo1, product.P1, product.Precio1,
            product.Costo2, product.P2, product.Precio2,
            product.Costo3, product.P3, product.Precio3,
            product.Costo4, product.P4, product.Precio4,
            product.Costo5, product.P5, product.Precio5,
            product.Costo6, product.P6, product.Precio6,
            product.Costo7, product.P7, product.Precio7,
            product.IdProveedor,
            product.Referencia,
            product.IdGrupoProducto,
            product.IdSubGrupoProducto,
            product.IdMarcaProducto,
            product.IdModeloProducto,
            product.Familia,
            product.Aplicacion,
            product.Observacion,
            product.StockMinimo,
            product.StockMaximo,
            product.Estado,
            product.Iva,
            product.NoControlaStock,
            product.UsaSerial,
            product.ImprimeFactura,
            product.AutoCantidad,
            product.AutoPrecio,
            product.UsaVencimiento,
            product.DescripcionEditable,
            product.VendeSinStock,
            product.AutoSuma,
            product.PublicarWeb,
            product.Fraccionable,
            product.Compuesto,
            product.CodigoBarra,
            product.Impuesto,
            product.Imagen,
            product.CostoMonedaExtranjera,
            product.ControlaGarantia,
            FechaInicioGar = fechaIniGar,
            product.DiasGarantias,
            FechaFinGar = fechaFinGar,
            product.MuestraBusqueda,
            product.Guarnicion,
            product.Menu,
            ComandaVal = product.Comanda ? 1 : 0,
            product.Cocina,
            product.Otros,
            product.Churrasqueria,
            product.Barra,
            product.StockActual
        };

        using var conn = _connectionFactory.CreateConnection();
        using var tx = conn.BeginTransaction();
        try
        {
            if (!isModify)
            {
                const string insSql = @"
                    INSERT INTO productos
                    (codigo, descripcion, idmoneda, cambio, costomedio,
                     costo1, p1, precio1, costo2, p2, precio2, costo3, p3, precio3,
                     costo4, p4, precio4, costo5, p5, precio5, costo6, p6, precio6, costo7, p7, precio7,
                     idproveedor, referencia, idgrupoproducto, idsubgrupoproducto, idmarcaproducto, idmodeloproducto,
                     familia, aplicacion, observacion, stockminimo, stockmaximo, estado, iva,
                     nocontrolastock, usaserial, imprimefactura, autocantidad, autoprecio, usavencimiento,
                     descripcioneditable, vendesinstock, autosuma, publicarweb, fraccionable, compuesto,
                     codigobarra, impuesto, imagen, costomonedaextranjera,
                     controlagarantia, fechainiciogarantia, diasgarantias, fechafingarantia, muestrabusqueda,
                     guarnicion, menu, comanda, cocina, otros, churrasqueria, barra)
                    VALUES
                    (@Codigo, @Descripcion, @IdMoneda, @Cambio, @CostoMedio,
                     @Costo1, @P1, @Precio1, @Costo2, @P2, @Precio2, @Costo3, @P3, @Precio3,
                     @Costo4, @P4, @Precio4, @Costo5, @P5, @Precio5, @Costo6, @P6, @Precio6, @Costo7, @P7, @Precio7,
                     @IdProveedor, @Referencia, @IdGrupoProducto, @IdSubGrupoProducto, @IdMarcaProducto, @IdModeloProducto,
                     @Familia, @Aplicacion, @Observacion, @StockMinimo, @StockMaximo, @Estado, @Iva,
                     @NoControlaStock, @UsaSerial, @ImprimeFactura, @AutoCantidad, @AutoPrecio, @UsaVencimiento,
                     @DescripcionEditable, @VendeSinStock, @AutoSuma, @PublicarWeb, @Fraccionable, @Compuesto,
                     @CodigoBarra, @Impuesto, @Imagen, @CostoMonedaExtranjera,
                     @ControlaGarantia, @FechaInicioGar, @DiasGarantias, @FechaFinGar, @MuestraBusqueda,
                     @Guarnicion, @Menu, @ComandaVal, @Cocina, @Otros, @Churrasqueria, @Barra);
                    SELECT LAST_INSERT_ID();";

                product.IdProducto = await conn.ExecuteScalarAsync<int>(insSql, BuildParams(), tx);

                await conn.ExecuteAsync(
                    "INSERT INTO itemsdepositos (iddeposito, idproducto, codigo, cantidad) VALUES (@DepId, @ProdId, @Codigo, @Cant);",
                    new { DepId = depId, ProdId = product.IdProducto, product.Codigo, Cant = product.StockActual },
                    tx);

                if (depId != 1)
                {
                    await conn.ExecuteAsync(
                        "INSERT INTO itemsdepositos (iddeposito, idproducto, codigo, cantidad) VALUES (1, @ProdId, @Codigo, 0);",
                        new { ProdId = product.IdProducto, product.Codigo },
                        tx);
                }

                await TryLogAuditTxAsync(conn, tx, "ABM_PRODUCTOS", "productos", "A",
                    $"INSERT productos [{product.Codigo}] {product.Descripcion}", product.IdProducto, usuario);
                tx.Commit();
                return product.IdProducto;
            }
            else
            {
                const string updSql = @"
                    UPDATE productos SET
                        codigo = @Codigo,
                        descripcion = @Descripcion,
                        idmoneda = @IdMoneda,
                        cambio = @Cambio,
                        costomedio = @CostoMedio,
                        costo1 = @Costo1, p1 = @P1, precio1 = @Precio1,
                        costo2 = @Costo2, p2 = @P2, precio2 = @Precio2,
                        costo3 = @Costo3, p3 = @P3, precio3 = @Precio3,
                        costo4 = @Costo4, p4 = @P4, precio4 = @Precio4,
                        costo5 = @Costo5, p5 = @P5, precio5 = @Precio5,
                        costo6 = @Costo6, p6 = @P6, precio6 = @Precio6,
                        costo7 = @Costo7, p7 = @P7, precio7 = @Precio7,
                        idproveedor = @IdProveedor,
                        referencia = @Referencia,
                        idgrupoproducto = @IdGrupoProducto,
                        idsubgrupoproducto = @IdSubGrupoProducto,
                        idmarcaproducto = @IdMarcaProducto,
                        idmodeloproducto = @IdModeloProducto,
                        familia = @Familia,
                        aplicacion = @Aplicacion,
                        observacion = @Observacion,
                        stockminimo = @StockMinimo,
                        stockmaximo = @StockMaximo,
                        estado = @Estado,
                        iva = @Iva,
                        nocontrolastock = @NoControlaStock,
                        usaserial = @UsaSerial,
                        imprimefactura = @ImprimeFactura,
                        autocantidad = @AutoCantidad,
                        autoprecio = @AutoPrecio,
                        usavencimiento = @UsaVencimiento,
                        descripcioneditable = @DescripcionEditable,
                        vendesinstock = @VendeSinStock,
                        autosuma = @AutoSuma,
                        publicarweb = @PublicarWeb,
                        fraccionable = @Fraccionable,
                        compuesto = @Compuesto,
                        codigobarra = @CodigoBarra,
                        impuesto = @Impuesto,
                        imagen = @Imagen,
                        costomonedaextranjera = @CostoMonedaExtranjera,
                        controlagarantia = @ControlaGarantia,
                        fechainiciogarantia = @FechaInicioGar,
                        diasgarantias = @DiasGarantias,
                        fechafingarantia = @FechaFinGar,
                        muestrabusqueda = @MuestraBusqueda,
                        guarnicion = @Guarnicion,
                        menu = @Menu,
                        comanda = @ComandaVal,
                        cocina = @Cocina,
                        otros = @Otros,
                        churrasqueria = @Churrasqueria,
                        barra = @Barra
                    WHERE idproducto = @IdProducto;";

                await conn.ExecuteAsync(updSql, BuildParams(), tx);

                await conn.ExecuteAsync(
                    "UPDATE itemsdepositos SET codigo = @Codigo WHERE idproducto = @IdProducto;",
                    new { product.Codigo, product.IdProducto },
                    tx);

                int existsDep = await conn.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM itemsdepositos WHERE idproducto = @ProdId AND iddeposito = @DepId;",
                    new { ProdId = product.IdProducto, DepId = depId },
                    tx);
                if (existsDep == 0)
                {
                    await conn.ExecuteAsync(
                        "INSERT INTO itemsdepositos (iddeposito, idproducto, codigo, cantidad) VALUES (@DepId, @ProdId, @Codigo, 0);",
                        new { DepId = depId, ProdId = product.IdProducto, product.Codigo },
                        tx);
                }

                await TryLogAuditTxAsync(conn, tx, "ABM_PRODUCTOS", "productos", "M",
                    $"UPDATE productos #{product.IdProducto} [{product.Codigo}]", product.IdProducto, usuario);
                tx.Commit();
                return product.IdProducto;
            }
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task<bool> DeleteProductAsync(int idProducto)
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = "UPDATE productos SET estado = 0 WHERE idproducto = @Id;";
        int rows = await conn.ExecuteAsync(sql, new { Id = idProducto });
        if (rows > 0)
        {
            await TryLogAuditAsync(conn, "CREAR_PRODUCTOS", "productos", "E",
                $"UPDATE productos SET estado = 0 WHERE idproducto = {idProducto}", idProducto, "ADMINISTRA");
        }
        return rows > 0;
    }

    public async Task<bool> ReactivateProductAsync(int idProducto, string usuario = "ADMINISTRA")
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = "UPDATE productos SET estado = 1 WHERE idproducto = @Id;";
        int rows = await conn.ExecuteAsync(sql, new { Id = idProducto });
        if (rows > 0)
        {
            await TryLogAuditAsync(conn, "CREAR_PRODUCTOS", "productos", "M",
                $"UPDATE productos SET estado = 1 WHERE idproducto = {idProducto}", idProducto, usuario);
        }
        return rows > 0;
    }

    public async Task UpdateProductPriceAsync(
        int idProducto, decimal costo1, decimal precio1, decimal precio2, decimal precio3, decimal stockActual, int idDeposito)
    {
        using var conn = _connectionFactory.CreateConnection();
        using var tx = conn.BeginTransaction();
        try
        {
            decimal p1 = costo1 > 0 ? Math.Round(((precio1 - costo1) / costo1) * 100m, 2) : 0m;
            decimal p2 = costo1 > 0 && precio2 > 0 ? Math.Round(((precio2 - costo1) / costo1) * 100m, 2) : 0m;
            decimal p3 = costo1 > 0 && precio3 > 0 ? Math.Round(((precio3 - costo1) / costo1) * 100m, 2) : 0m;

            const string updProd = @"
                UPDATE productos
                SET costo1 = @Costo1,
                    costomedio = @Costo1,
                    p1 = @P1, precio1 = @Precio1,
                    p2 = @P2, precio2 = @Precio2,
                    p3 = @P3, precio3 = @Precio3
                WHERE idproducto = @IdProducto;";

            await conn.ExecuteAsync(updProd, new
            {
                IdProducto = idProducto,
                Costo1 = costo1,
                P1 = p1,
                Precio1 = precio1,
                P2 = p2,
                Precio2 = precio2,
                P3 = p3,
                Precio3 = precio3
            }, tx);

            const string updDep = @"
                UPDATE itemsdepositos
                SET cantidad = @Cantidad
                WHERE idproducto = @IdProducto AND iddeposito = @IdDeposito;";
            int aff = await conn.ExecuteAsync(updDep, new
            {
                Cantidad = stockActual,
                IdProducto = idProducto,
                IdDeposito = idDeposito
            }, tx);

            if (aff == 0)
            {
                const string insDep = @"
                    INSERT INTO itemsdepositos (iddeposito, idproducto, codigo, cantidad)
                    SELECT @IdDeposito, idproducto, codigo, @Cantidad
                    FROM productos WHERE idproducto = @IdProducto;";
                await conn.ExecuteAsync(insDep, new
                {
                    IdDeposito = idDeposito,
                    IdProducto = idProducto,
                    Cantidad = stockActual
                }, tx);
            }

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task<int> UpdateAllPricesByUtilityAsync(
        decimal u1, decimal u2, decimal u3, decimal u4, decimal u5, decimal u6, decimal u7)
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = @"
            UPDATE productos
            SET p1 = @U1, precio1 = ROUND(costo1 + (costo1 * @U1 / 100.0), 0),
                p2 = @U2, precio2 = ROUND(costo1 + (costo1 * @U2 / 100.0), 0),
                p3 = @U3, precio3 = ROUND(costo1 + (costo1 * @U3 / 100.0), 0),
                p4 = @U4, precio4 = ROUND(costo1 + (costo1 * @U4 / 100.0), 0),
                p5 = @U5, precio5 = ROUND(costo1 + (costo1 * @U5 / 100.0), 0),
                p6 = @U6, precio6 = ROUND(costo1 + (costo1 * @U6 / 100.0), 0),
                p7 = @U7, precio7 = ROUND(costo1 + (costo1 * @U7 / 100.0), 0)
            WHERE estado = 1 AND costo1 > 0;";

        return await conn.ExecuteAsync(sql, new
        {
            U1 = u1,
            U2 = u2,
            U3 = u3,
            U4 = u4,
            U5 = u5,
            U6 = u6,
            U7 = u7
        });
    }

    public async Task<List<DepositEntity>> GetDepositsAsync()
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT iddeposito AS IdDeposito,
                   descripcion AS Descripcion,
                   responsable AS Responsable,
                   estado AS Estado,
                   idsucursal AS IdSucursal
            FROM depositos
            WHERE estado = 1
            ORDER BY iddeposito;";
        var rows = await conn.QueryAsync<DepositEntity>(sql);
        return rows.ToList();
    }

    public async Task<int> GetNextNroAjusteAsync()
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = "SELECT COALESCE(MAX(nro_ajuste), 0) + 1 FROM ajustes;";
        return await conn.ExecuteScalarAsync<int>(sql);
    }

    public async Task<int> SaveStockAdjustmentTransactionAsync(StockAdjustmentEntity adjustment)
    {
        using var conn = _connectionFactory.CreateConnection();
        using var tx = conn.BeginTransaction();
        try
        {
            if (adjustment.NroAjuste <= 0)
            {
                adjustment.NroAjuste = await conn.ExecuteScalarAsync<int>(
                    "SELECT COALESCE(MAX(nro_ajuste), 0) + 1 FROM ajustes;", transaction: tx);
            }

            const string insAjusteSql = @"
                INSERT INTO ajustes
                (nro_ajuste, fecha, iddeposito, autorizadopor, motivo, motivotexto, especificar, concepto,
                 sobrante, faltante, total_diferencia, cidusuario, idsucursal, dessucursal)
                VALUES
                (@NroAjuste, @Fecha, @IdDeposito, @AutorizadoPor, @Motivo, @MotivoTexto, @Especificar, @Concepto,
                 @Sobrante, @Faltante, @TotalDiferencia, @CidUsuario, @IdSucursal, @DesSucursal);
                SELECT LAST_INSERT_ID();";
            adjustment.IdAjuste = await conn.ExecuteScalarAsync<int>(insAjusteSql, adjustment, tx);

            const string insItemSql = @"
                INSERT INTO itemsajustes
                (idajuste, nro_ajuste, idproducto, codigo, salida, entrada, precio, total)
                VALUES
                (@IdAjuste, @NroAjuste, @IdProducto, @Codigo, @Salida, @Entrada, @Precio, @Total);";

            foreach (var item in adjustment.Items)
            {
                item.IdAjuste = adjustment.IdAjuste;
                item.NroAjuste = adjustment.NroAjuste;
                await conn.ExecuteAsync(insItemSql, item, tx);

                decimal delta = item.Entrada - item.Salida;
                const string updDep = @"
                    UPDATE itemsdepositos
                    SET cantidad = cantidad + @Delta
                    WHERE idproducto = @IdProducto AND iddeposito = @IdDeposito;";
                int aff = await conn.ExecuteAsync(updDep, new
                {
                    Delta = delta,
                    item.IdProducto,
                    adjustment.IdDeposito
                }, tx);

                if (aff == 0)
                {
                    const string insDep = @"
                        INSERT INTO itemsdepositos (iddeposito, idproducto, codigo, cantidad)
                        VALUES (@IdDeposito, @IdProducto, @Codigo, @Cantidad);";
                    await conn.ExecuteAsync(insDep, new
                    {
                        adjustment.IdDeposito,
                        item.IdProducto,
                        Codigo = item.Codigo ?? string.Empty,
                        Cantidad = item.CantidadContada != 0m ? item.CantidadContada : delta
                    }, tx);
                }
            }

            tx.Commit();
            return adjustment.IdAjuste;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task AdjustStockAsync(
        int idProducto, int idDeposito, string codigo, decimal nuevaCantidad, string motivo, string usuario)
    {
        decimal actual = await GetDepositStockAsync(idProducto, idDeposito);
        decimal diff = nuevaCantidad - actual;
        decimal entrada = diff > 0m ? diff : 0m;
        decimal salida = diff < 0m ? Math.Abs(diff) : 0m;

        var prod = await GetByIdAsync(idProducto, idDeposito);
        decimal precio = prod?.Precio1 ?? 0m;

        var adjustment = new StockAdjustmentEntity
        {
            Fecha = DateTime.Today,
            IdDeposito = idDeposito,
            AutorizadoPor = string.IsNullOrWhiteSpace(usuario) ? "ADMINISTRA" : usuario,
            Motivo = 1,
            MotivoTexto = string.IsNullOrWhiteSpace(motivo) ? "AJUSTE MANUAL DESDE CATÁLOGO" : motivo,
            Especificar = motivo ?? string.Empty,
            Concepto = "AJUSTE DIRECTO",
            Sobrante = entrada * precio,
            Faltante = salida * precio,
            TotalDiferencia = diff * precio,
            CidUsuario = string.IsNullOrWhiteSpace(usuario) ? "ADMINISTRA" : usuario,
            Items = new List<StockAdjustmentItemEntity>
            {
                new()
                {
                    IdProducto = idProducto,
                    Codigo = codigo ?? prod?.Codigo ?? string.Empty,
                    Descripcion = prod?.Descripcion ?? string.Empty,
                    StockAnterior = actual,
                    CantidadContada = nuevaCantidad,
                    Entrada = entrada,
                    Salida = salida,
                    Precio = precio,
                    Total = Math.Abs(diff) * precio
                }
            }
        };

        await SaveStockAdjustmentTransactionAsync(adjustment);
    }

    public async Task<List<ProductDepositStockRow>> GetProductStockByDepositsAsync(int idProducto, string codigo)
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT d.iditemdeposito AS IdItemDeposito,
                   d.iddeposito AS IdDeposito,
                   COALESCE(dep.descripcion, CONCAT('DEPÓSITO #', d.iddeposito)) AS DesDeposito,
                   COALESCE(dep.responsable, '') AS Responsable,
                   d.idproducto AS IdProducto,
                   COALESCE(d.codigo, '') AS Codigo,
                   COALESCE(p.descripcion, '') AS DesProducto,
                   COALESCE(d.cantidad, 0) AS Cantidad,
                   COALESCE(p.costomedio, 0) AS CostoMedio,
                   COALESCE(p.precio1, 0) AS Precio1
            FROM itemsdepositos d
            LEFT JOIN depositos dep ON d.iddeposito = dep.iddeposito
            LEFT JOIN productos p ON d.idproducto = p.idproducto
            WHERE d.idproducto = @IdProducto
               OR (@Codigo <> '' AND TRIM(d.codigo) = @Codigo)
            ORDER BY dep.descripcion, d.iddeposito;";
        var rows = await conn.QueryAsync<ProductDepositStockRow>(sql, new
        {
            IdProducto = idProducto,
            Codigo = (codigo ?? string.Empty).Trim()
        });
        return rows.ToList();
    }

    public async Task<List<ProductLocationEntity>> GetProductLocationsAsync(int idProducto)
    {
        if (idProducto <= 0)
            return new List<ProductLocationEntity>();

        try
        {
            using var conn = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT COALESCE(suc, '') AS Suc,
                       COALESCE(gondola, '') AS Gondola,
                       COALESCE(celda, '') AS Celda
                FROM ubicacion_prod
                WHERE cve_producto = @IdProducto
                GROUP BY suc, gondola, celda;";
            var rows = await conn.QueryAsync<ProductLocationEntity>(sql, new { IdProducto = idProducto });
            return rows.ToList();
        }
        catch
        {
            return new List<ProductLocationEntity>();
        }
    }

    public async Task<List<ProductGroupEntity>> GetProductGroupsAsync()
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT idgrupoproducto AS IdGrupoProducto,
                   descripcion AS Descripcion,
                   descuento AS Descuento,
                   CASE WHEN fechainicio IS NULL OR CAST(fechainicio AS CHAR) = '0000-00-00' THEN NULL ELSE fechainicio END AS FechaInicio,
                   CASE WHEN fechafinal IS NULL OR CAST(fechafinal AS CHAR) = '0000-00-00' THEN NULL ELSE fechafinal END AS FechaFinal,
                   COALESCE(impresora, '') AS Impresora
            FROM grupoproductos
            ORDER BY descripcion;";
        return (await conn.QueryAsync<ProductGroupEntity>(sql)).ToList();
    }

    public async Task<int> SaveProductGroupAsync(ProductGroupEntity group, string lcTipo)
    {
        using var conn = _connectionFactory.CreateConnection();
        string desc = Trunc(group.Descripcion, 150).ToUpperInvariant();
        string imp = Trunc(group.Impresora, 180);
        string fini = group.FechaInicio?.ToString("yyyy-MM-dd") ?? "0000-00-00";
        string ffin = group.FechaFinal?.ToString("yyyy-MM-dd") ?? "0000-00-00";

        bool isModify = string.Equals(lcTipo, "M", StringComparison.OrdinalIgnoreCase) && group.IdGrupoProducto > 0;
        if (!isModify)
        {
            int dup = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM grupoproductos WHERE UPPER(TRIM(descripcion)) = UPPER(@Desc);",
                new { Desc = desc });
            if (dup > 0)
                throw new InvalidOperationException($"El grupo '{desc}' ya existe.");

            const string sql = @"
                INSERT INTO grupoproductos (descripcion, descuento, fechainicio, fechafinal, impresora)
                VALUES (@Desc, @DescPct, @Fini, @Ffin, @Imp);
                SELECT LAST_INSERT_ID();";
            group.IdGrupoProducto = await conn.ExecuteScalarAsync<int>(sql, new
            {
                Desc = desc,
                DescPct = group.Descuento,
                Fini = fini,
                Ffin = ffin,
                Imp = imp
            });
            return group.IdGrupoProducto;
        }
        else
        {
            const string sql = @"
                UPDATE grupoproductos SET
                    descripcion = @Desc,
                    descuento = @DescPct,
                    fechainicio = @Fini,
                    fechafinal = @Ffin,
                    impresora = @Imp
                WHERE idgrupoproducto = @Id;";
            await conn.ExecuteAsync(sql, new
            {
                Id = group.IdGrupoProducto,
                Desc = desc,
                DescPct = group.Descuento,
                Fini = fini,
                Ffin = ffin,
                Imp = imp
            });
            return group.IdGrupoProducto;
        }
    }

    public async Task<bool> DeleteProductGroupAsync(int id)
    {
        using var conn = _connectionFactory.CreateConnection();
        return await conn.ExecuteAsync("DELETE FROM grupoproductos WHERE idgrupoproducto = @Id;", new { Id = id }) > 0;
    }

    public async Task<List<ProductSubGroupEntity>> GetProductSubGroupsAsync()
    {
        using var conn = _connectionFactory.CreateConnection();
        return (await conn.QueryAsync<ProductSubGroupEntity>(
            "SELECT idsubgrupoproducto AS IdSubGrupoProducto, descripcion AS Descripcion FROM subgrupoproductos ORDER BY descripcion;")).ToList();
    }

    public async Task<int> SaveProductSubGroupAsync(ProductSubGroupEntity subGroup, string lcTipo)
    {
        using var conn = _connectionFactory.CreateConnection();
        string desc = Trunc(subGroup.Descripcion, 150).ToUpperInvariant();
        bool isModify = string.Equals(lcTipo, "M", StringComparison.OrdinalIgnoreCase) && subGroup.IdSubGrupoProducto > 0;
        if (!isModify)
        {
            int dup = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM subgrupoproductos WHERE UPPER(TRIM(descripcion)) = UPPER(@Desc);",
                new { Desc = desc });
            if (dup > 0) throw new InvalidOperationException($"El subgrupo '{desc}' ya existe.");

            subGroup.IdSubGrupoProducto = await conn.ExecuteScalarAsync<int>(
                "INSERT INTO subgrupoproductos (descripcion) VALUES (@Desc); SELECT LAST_INSERT_ID();",
                new { Desc = desc });
            return subGroup.IdSubGrupoProducto;
        }
        await conn.ExecuteAsync(
            "UPDATE subgrupoproductos SET descripcion = @Desc WHERE idsubgrupoproducto = @Id;",
            new { Id = subGroup.IdSubGrupoProducto, Desc = desc });
        return subGroup.IdSubGrupoProducto;
    }

    public async Task<bool> DeleteProductSubGroupAsync(int id)
    {
        using var conn = _connectionFactory.CreateConnection();
        return await conn.ExecuteAsync("DELETE FROM subgrupoproductos WHERE idsubgrupoproducto = @Id;", new { Id = id }) > 0;
    }

    public async Task<List<ProductBrandEntity>> GetProductBrandsAsync()
    {
        using var conn = _connectionFactory.CreateConnection();
        return (await conn.QueryAsync<ProductBrandEntity>(
            "SELECT idmarcaproducto AS IdMarcaProducto, descripcion AS Descripcion FROM marcaproductos ORDER BY descripcion;")).ToList();
    }

    public async Task<int> SaveProductBrandAsync(ProductBrandEntity brand, string lcTipo)
    {
        using var conn = _connectionFactory.CreateConnection();
        string desc = Trunc(brand.Descripcion, 150).ToUpperInvariant();
        bool isModify = string.Equals(lcTipo, "M", StringComparison.OrdinalIgnoreCase) && brand.IdMarcaProducto > 0;
        if (!isModify)
        {
            int dup = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM marcaproductos WHERE UPPER(TRIM(descripcion)) = UPPER(@Desc);",
                new { Desc = desc });
            if (dup > 0) throw new InvalidOperationException($"La marca '{desc}' ya existe.");

            brand.IdMarcaProducto = await conn.ExecuteScalarAsync<int>(
                "INSERT INTO marcaproductos (descripcion) VALUES (@Desc); SELECT LAST_INSERT_ID();",
                new { Desc = desc });
            return brand.IdMarcaProducto;
        }
        await conn.ExecuteAsync(
            "UPDATE marcaproductos SET descripcion = @Desc WHERE idmarcaproducto = @Id;",
            new { Id = brand.IdMarcaProducto, Desc = desc });
        return brand.IdMarcaProducto;
    }

    public async Task<bool> DeleteProductBrandAsync(int id)
    {
        using var conn = _connectionFactory.CreateConnection();
        return await conn.ExecuteAsync("DELETE FROM marcaproductos WHERE idmarcaproducto = @Id;", new { Id = id }) > 0;
    }

    public async Task<List<ProductModelEntity>> GetProductModelsAsync()
    {
        using var conn = _connectionFactory.CreateConnection();
        return (await conn.QueryAsync<ProductModelEntity>(
            "SELECT idmodeloproducto AS IdModeloProducto, descripcion AS Descripcion FROM modeloproductos ORDER BY descripcion;")).ToList();
    }

    public async Task<int> SaveProductModelAsync(ProductModelEntity model, string lcTipo)
    {
        using var conn = _connectionFactory.CreateConnection();
        string desc = Trunc(model.Descripcion, 150).ToUpperInvariant();
        bool isModify = string.Equals(lcTipo, "M", StringComparison.OrdinalIgnoreCase) && model.IdModeloProducto > 0;
        if (!isModify)
        {
            int dup = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM modeloproductos WHERE UPPER(TRIM(descripcion)) = UPPER(@Desc);",
                new { Desc = desc });
            if (dup > 0) throw new InvalidOperationException($"El modelo '{desc}' ya existe.");

            model.IdModeloProducto = await conn.ExecuteScalarAsync<int>(
                "INSERT INTO modeloproductos (descripcion) VALUES (@Desc); SELECT LAST_INSERT_ID();",
                new { Desc = desc });
            return model.IdModeloProducto;
        }
        await conn.ExecuteAsync(
            "UPDATE modeloproductos SET descripcion = @Desc WHERE idmodeloproducto = @Id;",
            new { Id = model.IdModeloProducto, Desc = desc });
        return model.IdModeloProducto;
    }

    public async Task<bool> DeleteProductModelAsync(int id)
    {
        using var conn = _connectionFactory.CreateConnection();
        return await conn.ExecuteAsync("DELETE FROM modeloproductos WHERE idmodeloproducto = @Id;", new { Id = id }) > 0;
    }

    public async Task<int> SaveDepositAsync(DepositEntity deposit, string lcTipo)
    {
        using var conn = _connectionFactory.CreateConnection();
        string desc = Trunc(deposit.Descripcion, 80).ToUpperInvariant();
        string resp = Trunc(deposit.Responsable, 80).ToUpperInvariant();
        int suc = deposit.IdSucursal > 0 ? deposit.IdSucursal : 1;
        bool isModify = string.Equals(lcTipo, "M", StringComparison.OrdinalIgnoreCase) && deposit.IdDeposito > 0;

        if (!isModify)
        {
            const string insSql = @"
                INSERT INTO depositos (descripcion, responsable, estado, idsucursal, dessucursal)
                VALUES (@Desc, @Resp, @Estado, @IdSuc, @IdSuc);
                SELECT LAST_INSERT_ID();";
            deposit.IdDeposito = await conn.ExecuteScalarAsync<int>(insSql, new
            {
                Desc = desc,
                Resp = resp,
                deposit.Estado,
                IdSuc = suc
            });
            return deposit.IdDeposito;
        }

        const string updSql = @"
            UPDATE depositos SET
                descripcion = @Desc,
                responsable = @Resp,
                estado = @Estado,
                idsucursal = @IdSuc,
                dessucursal = @IdSuc
            WHERE iddeposito = @Id;";
        await conn.ExecuteAsync(updSql, new
        {
            Id = deposit.IdDeposito,
            Desc = desc,
            Resp = resp,
            deposit.Estado,
            IdSuc = suc
        });
        return deposit.IdDeposito;
    }

    public async Task<bool> DeleteDepositAsync(int id)
    {
        using var conn = _connectionFactory.CreateConnection();
        return await conn.ExecuteAsync("UPDATE depositos SET estado = 0 WHERE iddeposito = @Id;", new { Id = id }) > 0;
    }

    public async Task<int> SaveProductLotAsync(ProductLotEntity lot)
    {
        using var conn = _connectionFactory.CreateConnection();
        string loteCode = Trunc(lot.Lote, 15).ToUpperInvariant();
        int dep = lot.IdDeposito > 0 ? lot.IdDeposito : 1;
        string venc = lot.Vencimiento.ToString("yyyy-MM-dd");

        if (lot.IdLote <= 0)
        {
            const string insSql = @"
                INSERT INTO lote_producto (idproducto, lote, vencimiento, cantidad, iddeposito)
                VALUES (@IdProd, @Lote, @Venc, @Cant, @Dep);
                SELECT LAST_INSERT_ID();";
            lot.IdLote = await conn.ExecuteScalarAsync<int>(insSql, new
            {
                IdProd = lot.IdProducto,
                Lote = loteCode,
                Venc = venc,
                Cant = lot.Cantidad,
                Dep = dep
            });
            return lot.IdLote;
        }

        const string updSql = @"
            UPDATE lote_producto SET
                lote = @Lote,
                vencimiento = @Venc,
                cantidad = @Cant,
                iddeposito = @Dep
            WHERE idlote = @Id;";
        await conn.ExecuteAsync(updSql, new
        {
            Id = lot.IdLote,
            Lote = loteCode,
            Venc = venc,
            Cant = lot.Cantidad,
            Dep = dep
        });
        return lot.IdLote;
    }

    public async Task<bool> DeleteProductLotAsync(int idLote)
    {
        using var conn = _connectionFactory.CreateConnection();
        return await conn.ExecuteAsync("DELETE FROM lote_producto WHERE idlote = @Id;", new { Id = idLote }) > 0;
    }

    public async Task<List<ProductLotEntity>> GetExpiringLotsReportAsync(DateTime desde, DateTime hasta)
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT l.idlote AS IdLote,
                   l.idproducto AS IdProducto,
                   COALESCE(p.codigo, '') AS Codigo,
                   COALESCE(p.descripcion, '') AS Descripcion,
                   COALESCE(l.lote, '') AS Lote,
                   CASE WHEN l.vencimiento IS NULL OR CAST(l.vencimiento AS CHAR) = '0000-00-00' THEN CURDATE() ELSE l.vencimiento END AS Vencimiento,
                   COALESCE(l.cantidad, 0) AS Cantidad,
                   COALESCE(l.iddeposito, 1) AS IdDeposito
            FROM lote_producto l
            LEFT JOIN productos p ON l.idproducto = p.idproducto
            WHERE (l.vencimiento IS NULL OR CAST(l.vencimiento AS CHAR) = '0000-00-00'
                   OR (l.vencimiento >= @Desde AND l.vencimiento <= @Hasta))
            ORDER BY Vencimiento ASC, Descripcion ASC;";
        var rows = await conn.QueryAsync<ProductLotEntity>(sql, new
        {
            Desde = desde.Date,
            Hasta = hasta.Date
        });
        return rows.ToList();
    }

    public async Task<List<ProductEntity>> GetPriceListReportAsync(
        int? idGrupo, int? idSubGrupo, int? idMarca, int? idModelo, string orderBy)
    {
        using var conn = _connectionFactory.CreateConnection();
        string ord = string.Equals(orderBy, "codigo", StringComparison.OrdinalIgnoreCase)
            ? "ORDER BY p.codigo, p.descripcion"
            : "ORDER BY p.descripcion";

        string sql = $@"
            SELECT {SelectProductColumns},
                   {JoinedDisplayColumns},
                   COALESCE((SELECT SUM(d.cantidad) FROM itemsdepositos d WHERE d.idproducto = p.idproducto AND d.iddeposito = 1), 0) AS StockActual
            {ProductJoinFromClause}
            WHERE p.estado = 1
              AND (@IdGrupo IS NULL OR p.idgrupoproducto = @IdGrupo)
              AND (@IdSubGrupo IS NULL OR p.idsubgrupoproducto = @IdSubGrupo)
              AND (@IdMarca IS NULL OR p.idmarcaproducto = @IdMarca)
              AND (@IdModelo IS NULL OR p.idmodeloproducto = @IdModelo)
            {ord};";
        var rows = await conn.QueryAsync<ProductEntity>(sql, new
        {
            IdGrupo = idGrupo is > 0 ? idGrupo : null,
            IdSubGrupo = idSubGrupo is > 0 ? idSubGrupo : null,
            IdMarca = idMarca is > 0 ? idMarca : null,
            IdModelo = idModelo is > 0 ? idModelo : null
        });
        return rows.ToList();
    }

    public async Task<List<ProductDepositStockRow>> GetStockByDepositsReportAsync(
        int? idDeposito, bool onlyStockPositive, string orderBy)
    {
        using var conn = _connectionFactory.CreateConnection();
        string ord = string.Equals(orderBy, "codigo", StringComparison.OrdinalIgnoreCase)
            ? "ORDER BY i.iddeposito, p.codigo"
            : "ORDER BY i.iddeposito, p.descripcion";

        string sql = $@"
            SELECT i.iditemdeposito AS IdItemDeposito,
                   i.iddeposito AS IdDeposito,
                   COALESCE(d.descripcion, '') AS DesDeposito,
                   COALESCE(d.responsable, '') AS Responsable,
                   i.idproducto AS IdProducto,
                   COALESCE(p.codigo, i.codigo) AS Codigo,
                   COALESCE(p.descripcion, '') AS DesProducto,
                   COALESCE(i.cantidad, 0) AS Cantidad,
                   COALESCE(p.costomedio, 0) AS CostoMedio,
                   COALESCE(p.precio1, 0) AS Precio1
            FROM itemsdepositos i
            INNER JOIN depositos d ON i.iddeposito = d.iddeposito
            INNER JOIN productos p ON i.idproducto = p.idproducto
            WHERE p.estado = 1
              AND (@IdDeposito IS NULL OR i.iddeposito = @IdDeposito)
              AND (@OnlyPositive = 0 OR i.cantidad > 0)
            {ord};";
        var rows = await conn.QueryAsync<ProductDepositStockRow>(sql, new
        {
            IdDeposito = idDeposito is > 0 ? idDeposito : null,
            OnlyPositive = onlyStockPositive ? 1 : 0
        });
        return rows.ToList();
    }

    public async Task<int> UpdateFilteredPricesByUtilityAsync(
        decimal u1, decimal u2, decimal u3, decimal u4, decimal u5, decimal u6, decimal u7,
        int? idGrupo, int? idMarca, int? idProveedor, decimal? flatPercentDelta, string usuario)
    {
        using var conn = _connectionFactory.CreateConnection();
        using var tx = conn.BeginTransaction();
        try
        {
            int affected;
            if (flatPercentDelta.HasValue)
            {
                decimal factor = 1m + (flatPercentDelta.Value / 100m);
                const string sqlDelta = @"
                    UPDATE productos SET
                        precio1 = ROUND(precio1 * @Factor, 0),
                        precio2 = ROUND(precio2 * @Factor, 0),
                        precio3 = ROUND(precio3 * @Factor, 0),
                        precio4 = ROUND(precio4 * @Factor, 0),
                        precio5 = ROUND(precio5 * @Factor, 0),
                        precio6 = ROUND(precio6 * @Factor, 0),
                        precio7 = ROUND(precio7 * @Factor, 0),
                        p1 = CASE WHEN costo1 > 0 THEN ROUND(((ROUND(precio1 * @Factor, 0) - costo1) * 100.0) / costo1, 2) ELSE p1 END,
                        p2 = CASE WHEN costo2 > 0 THEN ROUND(((ROUND(precio2 * @Factor, 0) - costo2) * 100.0) / costo2, 2) ELSE p2 END,
                        p3 = CASE WHEN costo3 > 0 THEN ROUND(((ROUND(precio3 * @Factor, 0) - costo3) * 100.0) / costo3, 2) ELSE p3 END,
                        p4 = CASE WHEN costo4 > 0 THEN ROUND(((ROUND(precio4 * @Factor, 0) - costo4) * 100.0) / costo4, 2) ELSE p4 END,
                        p5 = CASE WHEN costo5 > 0 THEN ROUND(((ROUND(precio5 * @Factor, 0) - costo5) * 100.0) / costo5, 2) ELSE p5 END,
                        p6 = CASE WHEN costo6 > 0 THEN ROUND(((ROUND(precio6 * @Factor, 0) - costo6) * 100.0) / costo6, 2) ELSE p6 END,
                        p7 = CASE WHEN costo7 > 0 THEN ROUND(((ROUND(precio7 * @Factor, 0) - costo7) * 100.0) / costo7, 2) ELSE p7 END
                    WHERE estado = 1
                      AND (@IdGrupo IS NULL OR idgrupoproducto = @IdGrupo)
                      AND (@IdMarca IS NULL OR idmarcaproducto = @IdMarca)
                      AND (@IdProveedor IS NULL OR idproveedor = @IdProveedor);";
                affected = await conn.ExecuteAsync(sqlDelta, new
                {
                    Factor = factor,
                    IdGrupo = idGrupo is > 0 ? idGrupo : null,
                    IdMarca = idMarca is > 0 ? idMarca : null,
                    IdProveedor = idProveedor is > 0 ? idProveedor : null
                }, tx);
            }
            else
            {
                const string sqlUtil = @"
                    UPDATE productos SET
                        costo2 = costo1, costo3 = costo1, costo4 = costo1, costo5 = costo1, costo6 = costo1, costo7 = costo1,
                        p1 = @U1, precio1 = ROUND(costo1 + (costo1 * @U1 / 100.0), 2),
                        p2 = @U2, precio2 = ROUND(costo1 + (costo1 * @U2 / 100.0), 2),
                        p3 = @U3, precio3 = ROUND(costo1 + (costo1 * @U3 / 100.0), 2),
                        p4 = @U4, precio4 = ROUND(costo1 + (costo1 * @U4 / 100.0), 2),
                        p5 = @U5, precio5 = ROUND(costo1 + (costo1 * @U5 / 100.0), 2),
                        p6 = @U6, precio6 = ROUND(costo1 + (costo1 * @U6 / 100.0), 2),
                        p7 = @U7, precio7 = ROUND(costo1 + (costo1 * @U7 / 100.0), 2)
                    WHERE estado = 1 AND costo1 > 0
                      AND (@IdGrupo IS NULL OR idgrupoproducto = @IdGrupo)
                      AND (@IdMarca IS NULL OR idmarcaproducto = @IdMarca)
                      AND (@IdProveedor IS NULL OR idproveedor = @IdProveedor);";
                affected = await conn.ExecuteAsync(sqlUtil, new
                {
                    U1 = u1, U2 = u2, U3 = u3, U4 = u4, U5 = u5, U6 = u6, U7 = u7,
                    IdGrupo = idGrupo is > 0 ? idGrupo : null,
                    IdMarca = idMarca is > 0 ? idMarca : null,
                    IdProveedor = idProveedor is > 0 ? idProveedor : null
                }, tx);
            }

            await TryLogAuditTxAsync(conn, tx, "CREAR_PRODUCTOS_AJUSTE", "productos", "M",
                $"Ajuste masivo de precios ({affected} productos)", affected, usuario);
            tx.Commit();
            return affected;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task<List<ProductTransferEntity>> GetDepositTransfersAsync(DateTime desde, DateTime hasta)
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT t.idtrasladodeposito AS IdTrasladoDeposito,
                   t.iddepositoorigen AS IdDepositoOrigen,
                   COALESCE(do.descripcion, '') AS DesDepositoOrigen,
                   t.iddepositodestino AS IdDepositoDestino,
                   COALESCE(dd.descripcion, '') AS DesDepositoDestino,
                   t.idproducto AS IdProducto,
                   COALESCE(p.codigo, t.codigo) AS Codigo,
                   COALESCE(p.descripcion, '') AS DesProducto,
                   COALESCE(t.cantidad, 0) AS Cantidad,
                   COALESCE(t.observaciones, '') AS Observaciones,
                   COALESCE(t.autorizado, '') AS Autorizado,
                   CASE WHEN t.fecha IS NULL OR CAST(t.fecha AS CHAR) = '0000-00-00' THEN CURDATE() ELSE t.fecha END AS Fecha,
                   COALESCE(t.hora, '') AS Hora,
                   COALESCE(t.cidusuario, '') AS CidUsuario
            FROM trasladodepositos t
            LEFT JOIN depositos do ON t.iddepositoorigen = do.iddeposito
            LEFT JOIN depositos dd ON t.iddepositodestino = dd.iddeposito
            LEFT JOIN productos p ON t.idproducto = p.idproducto
            WHERE t.fecha >= @Desde AND t.fecha <= @Hasta
            ORDER BY t.idtrasladodeposito DESC
            LIMIT 250;";
        var rows = await conn.QueryAsync<ProductTransferEntity>(sql, new { Desde = desde.Date, Hasta = hasta.Date });
        return rows.ToList();
    }

    public async Task<int> SaveDepositTransferTransactionAsync(ProductTransferEntity transfer)
    {
        if (transfer.IdDepositoOrigen <= 0 || transfer.IdDepositoDestino <= 0)
            throw new InvalidOperationException("Debe seleccionar depósito origen y depósito destino.");
        if (transfer.IdDepositoOrigen == transfer.IdDepositoDestino)
            throw new InvalidOperationException("El depósito origen y destino deben ser distintos.");
        if (transfer.IdProducto <= 0 || transfer.Cantidad <= 0m)
            throw new InvalidOperationException("Seleccione un producto válido y una cantidad mayor a cero.");

        using var conn = _connectionFactory.CreateConnection();
        using var tx = conn.BeginTransaction();
        try
        {
            string cod = Trunc(transfer.Codigo, 20);
            string obs = Trunc(transfer.Observaciones, 200);
            string aut = Trunc(transfer.Autorizado, 150);
            string hora = Trunc(string.IsNullOrWhiteSpace(transfer.Hora) ? DateTime.Now.ToString("HH:mm:ss") : transfer.Hora, 8);
            string user = Trunc(string.IsNullOrWhiteSpace(transfer.CidUsuario) ? "ADMINISTRA" : transfer.CidUsuario, 20);

            int affOri = await conn.ExecuteAsync(
                "UPDATE itemsdepositos SET cantidad = cantidad - @Cant WHERE idproducto = @IdProd AND iddeposito = @DepOri;",
                new { Cant = transfer.Cantidad, IdProd = transfer.IdProducto, DepOri = transfer.IdDepositoOrigen },
                tx);
            if (affOri == 0)
            {
                await conn.ExecuteAsync(
                    "INSERT INTO itemsdepositos (iddeposito, idproducto, codigo, cantidad) VALUES (@DepOri, @IdProd, @Cod, @NegCant);",
                    new { DepOri = transfer.IdDepositoOrigen, IdProd = transfer.IdProducto, Cod = cod, NegCant = -transfer.Cantidad },
                    tx);
            }

            int affDest = await conn.ExecuteAsync(
                "UPDATE itemsdepositos SET cantidad = cantidad + @Cant WHERE idproducto = @IdProd AND iddeposito = @DepDest;",
                new { Cant = transfer.Cantidad, IdProd = transfer.IdProducto, DepDest = transfer.IdDepositoDestino },
                tx);
            if (affDest == 0)
            {
                await conn.ExecuteAsync(
                    "INSERT INTO itemsdepositos (iddeposito, idproducto, codigo, cantidad) VALUES (@DepDest, @IdProd, @Cod, @Cant);",
                    new { DepDest = transfer.IdDepositoDestino, IdProd = transfer.IdProducto, Cod = cod, Cant = transfer.Cantidad },
                    tx);
            }

            const string insSql = @"
                INSERT INTO trasladodepositos
                (iddepositoorigen, iddepositodestino, idproducto, codigo, cantidad, observaciones, autorizado, fecha, hora, cidusuario)
                VALUES
                (@DepOri, @DepDest, @IdProd, @Cod, @Cant, @Obs, @Aut, @Fecha, @Hora, @User);
                SELECT LAST_INSERT_ID();";
            transfer.IdTrasladoDeposito = await conn.ExecuteScalarAsync<int>(insSql, new
            {
                DepOri = transfer.IdDepositoOrigen,
                DepDest = transfer.IdDepositoDestino,
                IdProd = transfer.IdProducto,
                Cod = cod,
                Cant = transfer.Cantidad,
                Obs = obs,
                Aut = aut,
                Fecha = transfer.Fecha.ToString("yyyy-MM-dd"),
                Hora = hora,
                User = user
            }, tx);

            await TryLogAuditTxAsync(conn, tx, "TRASLADO_DEPOSITOS", "trasladodepositos", "A",
                $"Traslado {cod} x {transfer.Cantidad:0.##} ({transfer.IdDepositoOrigen}->{transfer.IdDepositoDestino})",
                transfer.IdTrasladoDeposito, user);

            tx.Commit();
            return transfer.IdTrasladoDeposito;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    private static async Task TryLogAuditAsync(
        System.Data.IDbConnection conn, string formTran, string tablaTran, string tipoTran, string sqlTran, int claveTran, string user)
    {
        try
        {
            const string sql = @"
                INSERT INTO tbl_tranusu (form_tran, tabla_tran, tipo_tran, fecha_tran, sql_tran, clave_tran, userid_tran)
                VALUES (@Form, @Tabla, @Tipo, NOW(), @Sql, @Clave, @User);";
            await conn.ExecuteAsync(sql, new
            {
                Form = Trunc(formTran, 30),
                Tabla = Trunc(tablaTran, 30),
                Tipo = Trunc(tipoTran, 1),
                Sql = sqlTran ?? string.Empty,
                Clave = claveTran,
                User = Trunc(string.IsNullOrWhiteSpace(user) ? "ADMINISTRA" : user, 30)
            });
        }
        catch
        {
            // Audit failure must never block core product persistence
        }
    }

    private static async Task TryLogAuditTxAsync(
        System.Data.IDbConnection conn, System.Data.IDbTransaction tx,
        string formTran, string tablaTran, string tipoTran, string sqlTran, int claveTran, string user)
    {
        try
        {
            const string sql = @"
                INSERT INTO tbl_tranusu (form_tran, tabla_tran, tipo_tran, fecha_tran, sql_tran, clave_tran, userid_tran)
                VALUES (@Form, @Tabla, @Tipo, NOW(), @Sql, @Clave, @User);";
            await conn.ExecuteAsync(sql, new
            {
                Form = Trunc(formTran, 30),
                Tabla = Trunc(tablaTran, 30),
                Tipo = Trunc(tipoTran, 1),
                Sql = sqlTran ?? string.Empty,
                Clave = claveTran,
                User = Trunc(string.IsNullOrWhiteSpace(user) ? "ADMINISTRA" : user, 30)
            }, tx);
        }
        catch
        {
            // Audit failure must never abort transaction
        }
    }
}
