using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using FirewallControl.Core.Business;
using FirewallControl.Core.Entities;
using FirewallControl.Core.Interfaces;

namespace FirewallControl.Infrastructure.Repositories;

/// <summary>
/// Full transactional sales repository replicating ABM_VENTAS_AUTOSERVICE_COBRO_CONTADO.SCX:
/// - GetNextNroVentaAsync: SELECT COALESCE(MAX(nro_venta), 0) + 1 FROM ventas
/// - GetTimbradoLocalAsync: SELECT * FROM timbradolocal WHERE idsucursal = @IdSucursal AND idcaja = @IdCaja
/// - GetSaleByIdAsync / GetSaleByNroVentaAsync: loads ventas + itemsventas with RecNo 1..N
/// - ProcessSaleTransactionAsync:
///   * lctipo == "M": restores previous itemsdepositos stock (including compound components via vw_compuesto) and deletes old itemsventas.
///   * Inserts/Updates ventas.
///   * Inserts itemsventas and deducts stock in itemsdepositos (or compound components via vw_compuesto).
///   * Updates timbradolocal (n3 = n3 + 1) when TipoBoleta == 2.
///   * Inserts cobrocliente and movimientocaja (for FormaPago == 1 Contado or when CobroCaja == 1).
/// </summary>
public class SalesRepository : ISalesRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IConfigService? _configService;

    public SalesRepository(IDbConnectionFactory connectionFactory, IConfigService? configService = null)
    {
        _connectionFactory = connectionFactory;
        _configService = configService;
    }

    /// <summary>
    /// Determina si debe congelarse la actualización de stock en depósito para un ítem sin control de stock,
    /// evitando descuentos que terminen en existencias negativas.
    /// </summary>
    public static bool ShouldSkipStockUpdate(int itemNoControlaStock, AppPreferencesEntity? preferences)
    {
        bool freeze = preferences?.CongelarStockSinControl ?? true;
        if (!freeze)
            return false;

        if (itemNoControlaStock == 1)
            return true;

        if (preferences != null && preferences.NoControlarStockGlobal && !preferences.DescontarStockAunqueNoControle)
            return true;

        return false;
    }

    public async Task<int> GetNextNroVentaAsync()
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = "SELECT COALESCE(MAX(nro_venta), 0) + 1 FROM ventas;";
        return await conn.ExecuteScalarAsync<int>(sql);
    }

    public async Task<(int MaxIdVenta, int MaxNroVenta, decimal TotalHoy)> GetLiveSyncStatusAsync()
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT COALESCE(MAX(idventa), 0) AS MaxIdVenta,
                   COALESCE(MAX(nro_venta), 0) AS MaxNroVenta,
                   COALESCE(SUM(CASE WHEN fecha_venta = CURDATE() AND estado = 1 THEN total_venta ELSE 0 END), 0) AS TotalHoy
            FROM ventas;";
        return await conn.QueryFirstOrDefaultAsync<(int MaxIdVenta, int MaxNroVenta, decimal TotalHoy)>(sql);
    }

    public async Task<TimbradoLocalEntity> GetTimbradoLocalAsync(int idSucursal, int idCaja)
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT idtimbradolocal AS IdTimbradoLocal,
                   idsucursal AS IdSucursal,
                   idcaja AS IdCaja,
                   timbrado AS Timbrado,
                   n1 AS N1,
                   n2 AS N2,
                   n3 AS N3,
                   factura AS Factura,
                   cantidad AS Cantidad,
                   vencimiento AS Vencimiento,
                   fechainicio AS FechaInicio
            FROM timbradolocal
            WHERE idsucursal = @IdSucursal AND idcaja = @IdCaja
            LIMIT 1;";

        var timbrado = await conn.QueryFirstOrDefaultAsync<TimbradoLocalEntity>(sql, new
        {
            IdSucursal = idSucursal,
            IdCaja = idCaja
        });

        if (timbrado == null)
        {
            const string fallbackSql = @"
                SELECT idtimbradolocal AS IdTimbradoLocal,
                       idsucursal AS IdSucursal,
                       idcaja AS IdCaja,
                       timbrado AS Timbrado,
                       n1 AS N1,
                       n2 AS N2,
                       n3 AS N3,
                       factura AS Factura,
                       cantidad AS Cantidad,
                       vencimiento AS Vencimiento,
                       fechainicio AS FechaInicio
                FROM timbradolocal
                LIMIT 1;";
            timbrado = await conn.QueryFirstOrDefaultAsync<TimbradoLocalEntity>(fallbackSql)
                       ?? new TimbradoLocalEntity { IdSucursal = idSucursal, IdCaja = idCaja };
        }

        return timbrado;
    }

    public async Task<SaleEntity?> GetSaleByIdAsync(int idVenta)
    {
        using var conn = _connectionFactory.CreateConnection();
        return await LoadSaleAsync(conn, "v.idventa = @Val", new { Val = idVenta });
    }

    public async Task<SaleEntity?> GetSaleByNroVentaAsync(int nroVenta)
    {
        using var conn = _connectionFactory.CreateConnection();
        return await LoadSaleAsync(conn, "v.nro_venta = @Val", new { Val = nroVenta });
    }

    private static async Task<SaleEntity?> LoadSaleAsync(IDbConnection conn, string whereClause, object param)
    {
        string sqlHeader = $@"
            SELECT v.idventa AS IdVenta,
                   v.nro_venta AS NroVenta,
                   v.nro_factura AS NroFactura,
                   CASE WHEN v.fecha_venta IS NULL OR CAST(v.fecha_venta AS CHAR) = '0000-00-00' THEN CURDATE() ELSE v.fecha_venta END AS FechaVenta,
                   COALESCE(NULLIF((SELECT m.hora FROM movimientocaja m WHERE m.idventas = v.idventa LIMIT 1), ''), v.hora_venta) AS HoraVenta,
                   v.forma_pago AS FormaPago,
                   v.iddeposito AS IdDeposito,
                   v.idcaja AS IdCaja,
                   v.idcliente AS IdCliente,
                   COALESCE(c.nombres, 'CLIENTE OCASIONAL') AS NombreCliente,
                   COALESCE(c.ruc, 'X') AS RucCliente,
                   v.idvendedor AS IdVendedor,
                   v.timbrado AS Timbrado,
                   v.total_exenta AS TotalExenta,
                   v.total_iva5 AS TotalIva5,
                   v.total_iva10 AS TotalIva10,
                   v.subtotal AS Subtotal,
                   v.descuento AS Descuento,
                   v.total_venta AS TotalVenta,
                   v.liquidacion_iva5 AS LiquidacionIva5,
                   v.liquidacion_iva10 AS LiquidacionIva10,
                   v.total_impuesto AS TotalImpuesto,
                   v.cidusuario AS CidUsuario,
                   v.concepto AS Concepto,
                   v.estado AS Estado,
                   v.tipoboleta AS TipoBoleta,
                   v.nro_boleta AS NroBoleta,
                   v.cot_dolar AS CotDolar,
                   v.cot_real AS CotReal,
                   v.cot_peso AS CotPeso,
                   v.cot_euro AS CotEuro,
                   v.cot_guarani AS CotGuarani,
                   v.moneda_guarani AS MonedaGuarani,
                   v.moneda_dolar AS MonedaDolar,
                   v.moneda_real AS MonedaReal,
                   v.moneda_peso AS MonedaPeso,
                   v.moneda_euro AS MonedaEuro,
                   v.observacion AS Observacion,
                   CASE WHEN v.vencimiento IS NULL OR CAST(v.vencimiento AS CHAR) = '0000-00-00' THEN CURDATE() ELSE v.vencimiento END AS Vencimiento,
                   v.idmoneda AS IdMoneda,
                   v.tipo_venta AS TipoVenta,
                   v.entrega_guarani AS EntregaGuarani,
                   v.entrega_dolar AS EntregaDolar,
                   v.entrega_real AS EntregaReal,
                   v.entrega_peso AS EntregaPeso,
                   v.entrega_euro AS EntregaEuro,
                   v.vuelto_guarani AS VueltoGuarani,
                   v.vuelto_real AS VueltoReal,
                   v.vuelto_peso AS VueltoPeso,
                   v.vuelto_dolar AS VueltoDolar,
                   v.cobro_caja AS CobroCaja,
                   v.terminal AS Terminal,
                   v.cuota AS Cuota,
                   v.idsucursal AS IdSucursal
            FROM ventas v
            LEFT JOIN clientes c ON v.idcliente = c.idcliente
            WHERE {whereClause}
            ORDER BY v.idventa DESC
            LIMIT 1;";

        var sale = await conn.QueryFirstOrDefaultAsync<SaleEntity>(sqlHeader, param);
        if (sale == null)
            return null;

        const string sqlItems = @"
            SELECT i.iditemventa AS IdItemVenta,
                   i.idventa AS IdVenta,
                   i.nro_venta AS NroVenta,
                   i.nro_factura AS NroFactura,
                   i.idproducto AS IdProducto,
                   i.codigo AS Codigo,
                   i.descripcion AS Descripcion,
                   i.cantidad AS Cantidad,
                   i.pre_unitario AS PreUnitario,
                   i.pre_exenta AS PreExenta,
                   i.pre_iva5 AS PreIva5,
                   i.pre_iva10 AS PreIva10,
                   CASE WHEN (i.pre_exenta + i.pre_iva5 + i.pre_iva10) > 0 THEN (i.pre_exenta + i.pre_iva5 + i.pre_iva10) ELSE i.precio_total END AS PrecioTotal,
                   i.iddeposito AS IdDeposito,
                   i.controlagarantia AS ControlaGarantia,
                   CASE WHEN i.fechainiciogarantia IS NULL OR CAST(i.fechainiciogarantia AS CHAR) = '0000-00-00' THEN CURDATE() ELSE i.fechainiciogarantia END AS FechaInicioGarantia,
                   i.diasgarantias AS DiasGarantias,
                   CASE WHEN i.fechafingarantia IS NULL OR CAST(i.fechafingarantia AS CHAR) = '0000-00-00' THEN CURDATE() ELSE i.fechafingarantia END AS FechaFinGarantia,
                   i.estado AS Estado,
                   COALESCE(p.iva, 1) AS Iva,
                   COALESCE(p.fraccionable, 0) AS Fraccionable,
                   COALESCE(p.compuesto, 0) AS Compuesto,
                   COALESCE(p.nocontrolastock, 0) AS NoControlaStock,
                   COALESCE(p.costo1, 0) AS CostoUnitario
            FROM itemsventas i
            LEFT JOIN productos p ON i.idproducto = p.idproducto
            WHERE i.idventa = @IdVenta
            ORDER BY i.iditemventa;";

        var items = (await conn.QueryAsync<SaleItemEntity>(sqlItems, new { sale.IdVenta })).ToList();
        for (int idx = 0; idx < items.Count; idx++)
        {
            items[idx].RecNo = idx + 1;
        }
        sale.Items = items;
        return sale;
    }

    public async Task<List<SaleEntity>> GetRecentSalesAsync(DateTime? desde = null, DateTime? hasta = null, int limit = 300)
    {
        using var conn = _connectionFactory.CreateConnection();
        DateTime from = (desde ?? DateTime.Today.AddDays(-30)).Date;
        DateTime to = (hasta ?? DateTime.Today).Date;

        const string sql = @"
            SELECT v.idventa AS IdVenta,
                   v.nro_venta AS NroVenta,
                   v.nro_factura AS NroFactura,
                   v.fecha_venta AS FechaVenta,
                   COALESCE(NULLIF((SELECT m.hora FROM movimientocaja m WHERE m.idventas = v.idventa LIMIT 1), ''), v.hora_venta) AS HoraVenta,
                   v.forma_pago AS FormaPago,
                   v.iddeposito AS IdDeposito,
                   v.idcaja AS IdCaja,
                   v.idcliente AS IdCliente,
                   COALESCE(c.nombres, 'CLIENTE OCASIONAL') AS NombreCliente,
                   COALESCE(c.ruc, 'X') AS RucCliente,
                   v.idvendedor AS IdVendedor,
                   v.timbrado AS Timbrado,
                   v.total_exenta AS TotalExenta,
                   v.total_iva5 AS TotalIva5,
                   v.total_iva10 AS TotalIva10,
                   v.subtotal AS Subtotal,
                   v.descuento AS Descuento,
                   v.total_venta AS TotalVenta,
                   v.liquidacion_iva5 AS LiquidacionIva5,
                   v.liquidacion_iva10 AS LiquidacionIva10,
                   v.total_impuesto AS TotalImpuesto,
                   v.cidusuario AS CidUsuario,
                   v.estado AS Estado,
                   v.tipoboleta AS TipoBoleta,
                   v.idsucursal AS IdSucursal
            FROM ventas v
            LEFT JOIN clientes c ON v.idcliente = c.idcliente
            WHERE v.fecha_venta >= @From AND v.fecha_venta <= @To
              AND v.estado IN (0, 1)
            ORDER BY v.fecha_venta DESC, v.idventa DESC
            LIMIT @Limit;";

        var rows = await conn.QueryAsync<SaleEntity>(sql, new { From = from, To = to, Limit = limit });
        return rows.ToList();
    }

    public async Task<bool> DeleteSalePermanentlyAsync(int idVenta)
    {
        using var conn = _connectionFactory.CreateConnection();
        using var tx = conn.BeginTransaction();
        try
        {
            // Restore stock before deleting (safe inventory consistency; respect frozen no-control products)
            const string prevItemsSql = @"
                SELECT i.idproducto AS IdProducto,
                       i.cantidad AS Cantidad,
                       i.iddeposito AS IdDeposito,
                       COALESCE(p.compuesto, 0) AS Compuesto,
                       COALESCE(p.nocontrolastock, 0) AS NoControlaStock
                FROM itemsventas i
                LEFT JOIN productos p ON i.idproducto = p.idproducto
                WHERE i.idventa = @IdVenta;";
            var prevItems = (await conn.QueryAsync<SaleItemEntity>(prevItemsSql, new { IdVenta = idVenta }, tx)).ToList();

            foreach (var oldItem in prevItems)
            {
                if (ShouldSkipStockUpdate(oldItem.NoControlaStock, _configService?.Preferences))
                    continue;

                int depId = oldItem.IdDeposito > 0 ? oldItem.IdDeposito : 1;
                if (oldItem.Compuesto == 1)
                {
                    const string compSql = @"
                        SELECT idproducto AS IdProducto, cantidad AS Cantidad
                        FROM vw_compuesto
                        WHERE idproductocompuesto = @IdCompuesto;";
                    var comps = await conn.QueryAsync<CompoundProductComponent>(compSql, new { IdCompuesto = oldItem.IdProducto }, tx);
                    foreach (var c in comps)
                    {
                        await UpdateDepositStockAsync(conn, tx, depId, c.IdProducto, "", c.Cantidad * oldItem.Cantidad);
                    }
                }
                else
                {
                    await UpdateDepositStockAsync(conn, tx, depId, oldItem.IdProducto, "", oldItem.Cantidad);
                }
            }

            // Exact replication of informe_ventas.scx SUPR deletion:
            await conn.ExecuteAsync("DELETE FROM movimientocaja WHERE idventas = @IdVenta;", new { IdVenta = idVenta }, tx);
            await conn.ExecuteAsync("DELETE FROM cobrocliente WHERE idventa = @IdVenta;", new { IdVenta = idVenta }, tx);
            await conn.ExecuteAsync("DELETE FROM itemsventas WHERE idventa = @IdVenta;", new { IdVenta = idVenta }, tx);
            int affected = await conn.ExecuteAsync("DELETE FROM ventas WHERE idventa = @IdVenta;", new { IdVenta = idVenta }, tx);

            tx.Commit();
            return affected > 0;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    /// <summary>
    /// Exact replication of informe_lucro.scx ("Informe de Costo Beneficio" + Egresos de Caja).
    /// Combines lucros1 (ventas grouped by cliente) and egresos (movimientocaja joined with rubros where tipocuenta &lt;&gt; 4).
    /// </summary>
    public async Task<List<ProfitReportItemEntity>> GetInformeLucroAsync(DateTime desde, DateTime hasta, string? rucCliente = null)
    {
        using var conn = _connectionFactory.CreateConnection();
        DateTime from = desde.Date;
        DateTime to = hasta.Date;
        string? cleanRuc = string.IsNullOrWhiteSpace(rucCliente) ? null : rucCliente.Split('-')[0].Trim();

        string filterClient = string.IsNullOrWhiteSpace(cleanRuc)
            ? ""
            : " AND (c.ruc = @Ruc OR CAST(c.idcliente AS CHAR) = @Ruc) ";

        string sqlLucros1 = $@"
            SELECT 1 AS Grupo,
                   v.nro_venta AS NroVenta,
                   v.fecha_venta AS FechaVenta,
                   c.nombres AS DesCliente,
                   c.ruc AS Ruc,
                   CAST(v.total_exenta AS DECIMAL(12,2)) AS TotalExenta,
                   CAST(v.total_iva10 AS DECIMAL(12,2)) AS TotalIva10,
                   CAST(v.total_iva5 AS DECIMAL(12,2)) AS TotalIva5,
                   CAST(v.total_impuesto AS DECIMAL(12,2)) AS TotalImpuesto,
                   CAST(SUM(i.pre_exenta + i.pre_iva5 + i.pre_iva10) AS DECIMAL(12,2)) AS TotalVenta,
                   CAST(SUM((CASE WHEN p.costo1 > 0 THEN p.costo1 WHEN p.costo2 > 0 THEN p.costo2 ELSE p.costomedio END) * i.cantidad) AS DECIMAL(12,2)) AS CostoMedio,
                   p.codigo AS Codigo,
                   p.descripcion AS DesProducto,
                   CAST(SUM(i.cantidad) AS DECIMAL(12,2)) AS Cantidad,
                   CAST(SUM(i.pre_exenta + i.pre_iva5 + i.pre_iva10) / NULLIF(SUM(i.cantidad), 0) AS DECIMAL(12,2)) AS PreUnitario,
                   CAST(SUM(i.pre_exenta) AS DECIMAL(12,2)) AS PreExenta,
                   CAST(SUM(i.pre_iva5) AS DECIMAL(12,2)) AS PreIva5,
                   CAST(SUM(i.pre_iva10) AS DECIMAL(12,2)) AS PreIva10,
                   i.idproducto AS IdProducto,
                   CAST(SUM(i.pre_exenta + i.pre_iva5 + i.pre_iva10) AS DECIMAL(12,2)) AS Total,
                   CAST(GREATEST(SUM(i.pre_exenta + i.pre_iva5 + i.pre_iva10) - SUM((CASE WHEN p.costo1 > 0 THEN p.costo1 WHEN p.costo2 > 0 THEN p.costo2 ELSE p.costomedio END) * i.cantidad), 0) AS DECIMAL(12,2)) AS Beneficio
            FROM ventas v
            INNER JOIN clientes c ON v.idcliente = c.idcliente
            INNER JOIN itemsventas i ON v.idventa = i.idventa
            INNER JOIN productos p ON i.idproducto = p.idproducto
            WHERE v.fecha_venta >= @From AND v.fecha_venta <= @To
              {filterClient}
            GROUP BY v.idcliente;";

        const string sqlEgresos = @"
            SELECT 2 AS Grupo,
                   0 AS NroVenta,
                   m.fechaalta AS FechaVenta,
                   '' AS DesCliente,
                   '' AS Ruc,
                   0 AS TotalExenta,
                   0 AS TotalIva10,
                   0 AS TotalIva5,
                   0 AS TotalImpuesto,
                   CAST(m.egresos * -1 AS DECIMAL(12,2)) AS TotalVenta,
                   0 AS CostoMedio,
                   '' AS Codigo,
                   r.nombrerubro AS DesProducto,
                   0 AS Cantidad,
                   0 AS PreUnitario,
                   0 AS PreExenta,
                   0 AS PreIva5,
                   0 AS PreIva10,
                   0 AS IdProducto,
                   CAST(m.egresos AS DECIMAL(12,2)) AS Total,
                   0 AS Beneficio
            FROM movimientocaja m
            INNER JOIN rubros r ON m.nrorubro = r.nrorubro
            WHERE m.fechaalta >= @From AND m.fechaalta <= @To
              AND m.egresos > 0
              AND r.tipocuenta <> 4;";

        var lucros1 = (await conn.QueryAsync<ProfitReportItemEntity>(sqlLucros1, new { From = from, To = to, Ruc = cleanRuc })).ToList();
        var egresos = (await conn.QueryAsync<ProfitReportItemEntity>(sqlEgresos, new { From = from, To = to })).ToList();

        var combined = new List<ProfitReportItemEntity>(lucros1.Count + egresos.Count);
        combined.AddRange(lucros1);
        combined.AddRange(egresos);
        return combined;
    }

    /// <summary>
    /// Exact replication of informe_lucroproductos.scx ("Productos Vendidos" / Informe Costo Beneficio Agrupado por Productos).
    /// </summary>
    public async Task<List<ProfitReportItemEntity>> GetInformeLucroProductosAsync(DateTime desde, DateTime hasta, string? codigoProducto = null)
    {
        using var conn = _connectionFactory.CreateConnection();
        DateTime from = desde.Date;
        DateTime to = hasta.Date;
        string? cleanCode = string.IsNullOrWhiteSpace(codigoProducto) ? null : codigoProducto.Trim();

        string filterProd = string.IsNullOrWhiteSpace(cleanCode)
            ? ""
            : " AND p.codigo = @CodProducto ";

        string sql = $@"
            SELECT 1 AS Grupo,
                   v.nro_venta AS NroVenta,
                   v.fecha_venta AS FechaVenta,
                   c.nombres AS DesCliente,
                   c.ruc AS Ruc,
                   v.total_exenta AS TotalExenta,
                   v.total_iva10 AS TotalIva10,
                   v.total_iva5 AS TotalIva5,
                   v.total_impuesto AS TotalImpuesto,
                   CAST(SUM(i.pre_exenta + i.pre_iva5 + i.pre_iva10) AS DECIMAL(12,2)) AS TotalVenta,
                   CAST(SUM((CASE WHEN p.costo1 > 0 THEN p.costo1 WHEN p.costo2 > 0 THEN p.costo2 ELSE p.costomedio END) * i.cantidad) AS DECIMAL(12,2)) AS CostoMedio,
                   p.codigo AS Codigo,
                   p.descripcion AS DesProducto,
                   CAST(SUM(i.cantidad) AS DECIMAL(12,2)) AS Cantidad,
                   CAST(SUM(i.pre_exenta + i.pre_iva5 + i.pre_iva10) / NULLIF(SUM(i.cantidad), 0) AS DECIMAL(12,2)) AS PreUnitario,
                   CAST(SUM(i.pre_exenta) AS DECIMAL(12,2)) AS PreExenta,
                   CAST(SUM(i.pre_iva5) AS DECIMAL(12,2)) AS PreIva5,
                   CAST(SUM(i.pre_iva10) AS DECIMAL(12,2)) AS PreIva10,
                   i.idproducto AS IdProducto,
                   CAST(SUM(i.pre_exenta + i.pre_iva5 + i.pre_iva10) AS DECIMAL(12,2)) AS Total,
                   CAST(GREATEST(SUM(i.pre_exenta + i.pre_iva5 + i.pre_iva10) - SUM((CASE WHEN p.costo1 > 0 THEN p.costo1 WHEN p.costo2 > 0 THEN p.costo2 ELSE p.costomedio END) * i.cantidad), 0) AS DECIMAL(12,2)) AS Beneficio
            FROM ventas v
            INNER JOIN clientes c ON v.idcliente = c.idcliente
            INNER JOIN itemsventas i ON v.idventa = i.idventa
            INNER JOIN productos p ON i.idproducto = p.idproducto
            WHERE v.fecha_venta >= @From AND v.fecha_venta <= @To
              {filterProd}
            GROUP BY i.idproducto
            ORDER BY DesProducto ASC;";

        var rows = await conn.QueryAsync<ProfitReportItemEntity>(sql, new { From = from, To = to, CodProducto = cleanCode });
        return rows.ToList();
    }

    public async Task<int> ProcessSaleTransactionAsync(SaleEntity sale, string lcTipo)
    {
        using var conn = _connectionFactory.CreateConnection();
        using var tx = conn.BeginTransaction();
        try
        {
            int idVenta = await ExecuteSaleInTransactionAsync(conn, tx, sale, lcTipo);
            tx.Commit();
            return idVenta;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    /// <summary>
    /// Exposed for unit/integration testing with an external transaction that can be rolled back.
    /// </summary>
    public async Task<int> ExecuteSaleInTransactionAsync(
        IDbConnection conn, IDbTransaction tx, SaleEntity sale, string lcTipo)
    {
        bool isModify = string.Equals(lcTipo, "M", StringComparison.OrdinalIgnoreCase) && sale.IdVenta > 0;

        if (!isModify)
        {
            if (sale.NroVenta <= 0)
            {
                sale.NroVenta = await conn.ExecuteScalarAsync<int>(
                    "SELECT COALESCE(MAX(nro_venta), 0) + 1 FROM ventas;", transaction: tx);
            }
            if (string.IsNullOrWhiteSpace(sale.NroFactura))
            {
                sale.NroFactura = sale.NroVenta.ToString();
            }

            const string insVentaSql = @"
                INSERT INTO ventas
                (nro_venta, nro_factura, fecha_venta, hora_venta, forma_pago, iddeposito, idcaja,
                 idcliente, idvendedor, timbrado, total_exenta, total_iva5, total_iva10,
                 subtotal, descuento, total_venta, liquidacion_iva5, liquidacion_iva10, total_impuesto,
                 cidusuario, concepto, estado, tipoboleta, nro_boleta,
                 cot_dolar, cot_real, cot_peso, cot_euro, cot_guarani,
                 moneda_guarani, moneda_dolar, moneda_real, moneda_peso, moneda_euro,
                 observacion, vencimiento, idmoneda, tipo_venta,
                 entrega_guarani, entrega_dolar, entrega_real, entrega_peso, entrega_euro,
                 vuelto_guarani, vuelto_real, vuelto_peso, vuelto_dolar,
                 cobro_caja, terminal, cuota, idsucursal)
                VALUES
                (@NroVenta, @NroFactura, @FechaVenta, @HoraVenta, @FormaPago, @IdDeposito, @IdCaja,
                 @IdCliente, @IdVendedor, @Timbrado, @TotalExenta, @TotalIva5, @TotalIva10,
                 @Subtotal, @Descuento, @TotalVenta, @LiquidacionIva5, @LiquidacionIva10, @TotalImpuesto,
                 @CidUsuario, @Concepto, @Estado, @TipoBoleta, @NroBoleta,
                 @CotDolar, @CotReal, @CotPeso, @CotEuro, @CotGuarani,
                 @MonedaGuarani, @MonedaDolar, @MonedaReal, @MonedaPeso, @MonedaEuro,
                 @Observacion, @Vencimiento, @IdMoneda, @TipoVenta,
                 @EntregaGuarani, @EntregaDolar, @EntregaReal, @EntregaPeso, @EntregaEuro,
                 @VueltoGuarani, @VueltoReal, @VueltoPeso, @VueltoDolar,
                 @CobroCaja, @Terminal, @Cuota, @IdSucursal);
                SELECT LAST_INSERT_ID();";

            sale.IdVenta = await conn.ExecuteScalarAsync<int>(insVentaSql, sale, tx);
        }
        else
        {
            // Restore stock from old itemsventas before updating (exact VFP grabaritems logic for lctipo='M')
            const string prevItemsSql = @"
                SELECT i.idproducto AS IdProducto,
                       i.cantidad AS Cantidad,
                       i.iddeposito AS IdDeposito,
                       COALESCE(p.compuesto, 0) AS Compuesto,
                       COALESCE(p.nocontrolastock, 0) AS NoControlaStock
                FROM itemsventas i
                LEFT JOIN productos p ON i.idproducto = p.idproducto
                WHERE i.idventa = @IdVenta;";
            var prevItems = (await conn.QueryAsync<SaleItemEntity>(prevItemsSql, new { sale.IdVenta }, tx)).ToList();

            foreach (var oldItem in prevItems)
            {
                if (ShouldSkipStockUpdate(oldItem.NoControlaStock, _configService?.Preferences))
                    continue;

                int depId = oldItem.IdDeposito > 0 ? oldItem.IdDeposito : sale.IdDeposito;
                if (oldItem.Compuesto == 1)
                {
                    const string compSql = @"
                        SELECT idproducto AS IdProducto, cantidad AS Cantidad
                        FROM vw_compuesto
                        WHERE idproductocompuesto = @IdCompuesto;";
                    var comps = await conn.QueryAsync<CompoundProductComponent>(compSql, new { IdCompuesto = oldItem.IdProducto }, tx);
                    foreach (var c in comps)
                    {
                        await UpdateDepositStockAsync(conn, tx, depId, c.IdProducto, "", c.Cantidad * oldItem.Cantidad);
                    }
                }
                else
                {
                    await UpdateDepositStockAsync(conn, tx, depId, oldItem.IdProducto, "", oldItem.Cantidad);
                }
            }

            await conn.ExecuteAsync("DELETE FROM itemsventas WHERE idventa = @IdVenta;", new { sale.IdVenta }, tx);
            await conn.ExecuteAsync("DELETE FROM cobrocliente WHERE idventa = @IdVenta;", new { sale.IdVenta }, tx);
            await conn.ExecuteAsync("DELETE FROM movimientocaja WHERE idventas = @IdVenta;", new { sale.IdVenta }, tx);

            const string updVentaSql = @"
                UPDATE ventas SET
                    nro_venta = @NroVenta,
                    nro_factura = @NroFactura,
                    fecha_venta = @FechaVenta,
                    hora_venta = @HoraVenta,
                    forma_pago = @FormaPago,
                    iddeposito = @IdDeposito,
                    idcaja = @IdCaja,
                    idcliente = @IdCliente,
                    idvendedor = @IdVendedor,
                    timbrado = @Timbrado,
                    total_exenta = @TotalExenta,
                    total_iva5 = @TotalIva5,
                    total_iva10 = @TotalIva10,
                    subtotal = @Subtotal,
                    descuento = @Descuento,
                    total_venta = @TotalVenta,
                    liquidacion_iva5 = @LiquidacionIva5,
                    liquidacion_iva10 = @LiquidacionIva10,
                    total_impuesto = @TotalImpuesto,
                    cidusuario = @CidUsuario,
                    concepto = @Concepto,
                    estado = @Estado,
                    tipoboleta = @TipoBoleta,
                    nro_boleta = @NroBoleta,
                    entrega_guarani = @EntregaGuarani,
                    entrega_dolar = @EntregaDolar,
                    entrega_real = @EntregaReal,
                    entrega_peso = @EntregaPeso,
                    entrega_euro = @EntregaEuro,
                    vuelto_guarani = @VueltoGuarani,
                    vuelto_real = @VueltoReal,
                    vuelto_peso = @VueltoPeso,
                    vuelto_dolar = @VueltoDolar,
                    cobro_caja = @CobroCaja,
                    idsucursal = @IdSucursal
                WHERE idventa = @IdVenta;";

            await conn.ExecuteAsync(updVentaSql, sale, tx);
        }

        // Insert itemsventas and deduct stock
        const string insItemSql = @"
            INSERT INTO itemsventas
            (idventa, nro_factura, nro_venta, idproducto, codigo, descripcion,
             cantidad, pre_unitario, pre_exenta, pre_iva5, pre_iva10, precio_total,
             iddeposito, controlagarantia, fechainiciogarantia, diasgarantias, fechafingarantia, estado)
            VALUES
            (@IdVenta, @NroFactura, @NroVenta, @IdProducto, @Codigo, @Descripcion,
             @Cantidad, @PreUnitario, @PreExenta, @PreIva5, @PreIva10, @PrecioTotal,
             @IdDeposito, @ControlaGarantia, @FechaInicioGarantia, @DiasGarantias, @FechaFinGarantia, @Estado);";

        foreach (var item in sale.Items)
        {
            SalesCalculationEngine.CalculateItemAmounts(item);
            item.IdVenta = sale.IdVenta;
            item.NroVenta = sale.NroVenta;
            item.NroFactura = sale.NroFactura;
            if (item.IdDeposito <= 0) item.IdDeposito = sale.IdDeposito;

            await conn.ExecuteAsync(insItemSql, item, tx);

            int effectiveNoControl = item.NoControlaStock;
            if (effectiveNoControl == 0 && item.IdProducto > 0)
            {
                effectiveNoControl = await conn.ExecuteScalarAsync<int>(
                    "SELECT COALESCE(nocontrolastock, 0) FROM productos WHERE idproducto = @IdProducto LIMIT 1;",
                    new { item.IdProducto },
                    tx);
            }

            if (ShouldSkipStockUpdate(effectiveNoControl, _configService?.Preferences))
                continue;

            if (item.Compuesto == 1)
            {
                const string compSql = @"
                    SELECT idproducto AS IdProducto, codigo AS Codigo, cantidad AS Cantidad
                    FROM vw_compuesto
                    WHERE idproductocompuesto = @IdCompuesto;";
                var comps = await conn.QueryAsync<CompoundProductComponent>(compSql, new { IdCompuesto = item.IdProducto }, tx);
                foreach (var c in comps)
                {
                    decimal deduct = -(c.Cantidad * item.Cantidad);
                    await UpdateDepositStockAsync(conn, tx, item.IdDeposito, c.IdProducto, c.Codigo, deduct);
                }
            }
            else
            {
                await UpdateDepositStockAsync(conn, tx, item.IdDeposito, item.IdProducto, item.Codigo, -item.Cantidad);
            }
        }

        // Update timbradolocal when TipoBoleta == 2 (Legal Invoice)
        if (!isModify && sale.TipoBoleta == 2)
        {
            const string updTimSql = @"
                UPDATE timbradolocal
                SET n3 = n3 + 1,
                    factura = CONCAT(LPAD(n1, 3, '0'), '-', LPAD(n2, 3, '0'), '-', LPAD(n3 + 1, 7, '0'))
                WHERE idsucursal = @IdSucursal AND idcaja = @IdCaja;";
            await conn.ExecuteAsync(updTimSql, new { sale.IdSucursal, sale.IdCaja }, tx);
        }

        // Register cobrocliente & movimientocaja (exact Command9.Click / cobro contado & credito logic)
        const string insCobroSql = @"
            INSERT INTO cobrocliente
            (idventa, nro_venta, nro_factura, vencimiento, cuota, fecha, dia,
             idcliente, cobro, entrega, fecha_cobro, forma_cobro, mora,
             idcaja, cidusuario, idsucursal, idcobrador, obs)
            VALUES
            (@IdVenta, @NroVenta, @NroFactura, @Vencimiento, @Cuota, @Fecha, 0,
             @IdCliente, @Cobro, @Entrega, @FechaCobro, @FormaCobro, 0,
             @IdCaja, @CidUsuario, @IdSucursal, @IdVendedor, @Obs);";

        bool isContado = sale.FormaPago == 1;
        await conn.ExecuteAsync(insCobroSql, new
        {
            sale.IdVenta,
            sale.NroVenta,
            sale.NroFactura,
            sale.Vencimiento,
            Cuota = sale.Cuota.ToString(),
            Fecha = sale.FechaVenta,
            sale.IdCliente,
            Cobro = sale.TotalVenta,
            Entrega = isContado ? sale.TotalVenta : 0m,
            FechaCobro = sale.FechaVenta,
            FormaCobro = isContado ? "CONTADO" : "CREDITO",
            sale.IdCaja,
            sale.CidUsuario,
            sale.IdSucursal,
            sale.IdVendedor,
            Obs = sale.Observacion ?? ""
        }, tx);

        if (isContado)
        {
            const string insMovCajaSql = @"
                INSERT INTO movimientocaja
                (nrorubro, ingresos, egresos, tipo, detalle, fechaalta, hora,
                 estado, idventas, idcompras, idcaja, idmoneda, cidusuario, obs)
                VALUES
                (1, @Ingresos, 0, 'ENTRADA', @Detalle, @FechaAlta, @Hora,
                 1, @IdVentas, 0, @IdCaja, @IdMoneda, @CidUsuario, @Obs);";

            await conn.ExecuteAsync(insMovCajaSql, new
            {
                Ingresos = sale.TotalVenta,
                Detalle = $"VENTA CONTADO NRO. {sale.NroVenta} - FACT. {sale.NroFactura}",
                FechaAlta = sale.FechaVenta,
                Hora = sale.HoraVenta,
                IdVentas = sale.IdVenta,
                sale.IdCaja,
                sale.IdMoneda,
                sale.CidUsuario,
                Obs = sale.NombreCliente ?? ""
            }, tx);
        }

        return sale.IdVenta;
    }

    private static async Task UpdateDepositStockAsync(
        IDbConnection conn, IDbTransaction tx, int idDeposito, int idProducto, string codigo, decimal delta)
    {
        const string updSql = @"
            UPDATE itemsdepositos
            SET cantidad = cantidad + @Delta
            WHERE iddeposito = @IdDeposito AND idproducto = @IdProducto;";
        int aff = await conn.ExecuteAsync(updSql, new
        {
            Delta = delta,
            IdDeposito = idDeposito,
            IdProducto = idProducto
        }, tx);

        if (aff == 0)
        {
            const string insSql = @"
                INSERT INTO itemsdepositos (iddeposito, idproducto, codigo, cantidad)
                VALUES (@IdDeposito, @IdProducto, @Codigo, @Delta);";
            await conn.ExecuteAsync(insSql, new
            {
                IdDeposito = idDeposito,
                IdProducto = idProducto,
                Codigo = codigo ?? "",
                Delta = delta
            }, tx);
        }
    }

    public async Task<List<BudgetSummaryEntity>> SearchBudgetsAsync(string? filter = null, int limit = 100)
    {
        using var conn = _connectionFactory.CreateConnection();
        string term = (filter ?? string.Empty).Trim();
        const string sql = @"
            SELECT p.idpresupuesto AS IdPresupuesto,
                   p.nro_presupuesto AS NroPresupuesto,
                   COALESCE(p.nro_factura, '') AS NroFactura,
                   CASE WHEN p.fecha_presupuesto IS NULL OR CAST(p.fecha_presupuesto AS CHAR) = '0000-00-00'
                        THEN CURDATE() ELSE p.fecha_presupuesto END AS FechaPresupuesto,
                   p.idcliente AS IdCliente,
                   COALESCE(c.nombres, 'CLIENTE OCASIONAL') AS NombreCliente,
                   COALESCE(c.ruc, 'X') AS RucCliente,
                   p.idvendedor AS IdVendedor,
                   p.descuento AS Descuento,
                   p.total_presupuesto AS TotalPresupuesto,
                   COALESCE(p.observacion, '') AS Observacion,
                   p.estado AS Estado
            FROM presupuestos p
            LEFT JOIN clientes c ON p.idcliente = c.idcliente
            WHERE (@Term = ''
                   OR CAST(p.nro_presupuesto AS CHAR) LIKE @LikeTerm
                   OR COALESCE(c.nombres, '') LIKE @LikeTerm
                   OR COALESCE(c.ruc, '') LIKE @LikeTerm
                   OR COALESCE(p.observacion, '') LIKE @LikeTerm)
            ORDER BY p.nro_presupuesto DESC, p.idpresupuesto DESC
            LIMIT @Limit;";
        var rows = await conn.QueryAsync<BudgetSummaryEntity>(sql, new
        {
            Term = term,
            LikeTerm = $"%{term}%",
            Limit = limit
        });
        return rows.ToList();
    }

    public async Task<(BudgetSummaryEntity Header, List<BudgetItemEntity> Items)?> GetBudgetByNumberOrIdAsync(
        int nroOrId, bool byNumber = true)
    {
        using var conn = _connectionFactory.CreateConnection();
        string where = byNumber ? "p.nro_presupuesto = @Val" : "p.idpresupuesto = @Val";
        string sqlHdr = $@"
            SELECT p.idpresupuesto AS IdPresupuesto,
                   p.nro_presupuesto AS NroPresupuesto,
                   COALESCE(p.nro_factura, '') AS NroFactura,
                   CASE WHEN p.fecha_presupuesto IS NULL OR CAST(p.fecha_presupuesto AS CHAR) = '0000-00-00'
                        THEN CURDATE() ELSE p.fecha_presupuesto END AS FechaPresupuesto,
                   p.idcliente AS IdCliente,
                   COALESCE(c.nombres, 'CLIENTE OCASIONAL') AS NombreCliente,
                   COALESCE(c.ruc, 'X') AS RucCliente,
                   p.idvendedor AS IdVendedor,
                   p.descuento AS Descuento,
                   p.total_presupuesto AS TotalPresupuesto,
                   COALESCE(p.observacion, '') AS Observacion,
                   p.estado AS Estado
            FROM presupuestos p
            LEFT JOIN clientes c ON p.idcliente = c.idcliente
            WHERE {where}
            ORDER BY p.idpresupuesto DESC
            LIMIT 1;";

        var header = await conn.QueryFirstOrDefaultAsync<BudgetSummaryEntity>(sqlHdr, new { Val = nroOrId });
        if (header == null)
            return null;

        const string sqlItems = @"
            SELECT i.iditempresupuesto AS IdItemPresupuesto,
                   i.idpresupuesto AS IdPresupuesto,
                   i.idproducto AS IdProducto,
                   COALESCE(i.codigo, pr.codigo, '') AS Codigo,
                   COALESCE(pr.descripcion, i.codigo, '') AS Descripcion,
                   i.cantidad AS Cantidad,
                   i.pre_unitario AS PreUnitario,
                   i.pre_exenta AS PreExenta,
                   i.pre_iva5 AS PreIva5,
                   i.pre_iva10 AS PreIva10,
                   COALESCE(pr.iva, 1) AS Iva,
                   COALESCE(pr.fraccionable, 0) AS Fraccionable,
                   COALESCE(pr.compuesto, 0) AS Compuesto,
                   COALESCE(pr.nocontrolastock, 0) AS NoControlaStock,
                   COALESCE(pr.costo1, 0) AS CostoUnitario
            FROM itemspresupuestos i
            LEFT JOIN productos pr ON i.idproducto = pr.idproducto
            WHERE i.idpresupuesto = @IdPresupuesto
            ORDER BY i.iditempresupuesto;";

        var items = (await conn.QueryAsync<BudgetItemEntity>(sqlItems, new { header.IdPresupuesto })).ToList();
        return (header, items);
    }

    public async Task SaveSaleInstallmentsAsync(
        int idVenta, int nroVenta, string nroFactura, int idCliente, DateTime fechaVenta,
        decimal entregaInicial, List<SaleInstallmentEntity> installments,
        int idCaja, int idSucursal, string cidUsuario)
    {
        if (installments == null || installments.Count == 0)
            return;

        using var conn = _connectionFactory.CreateConnection();
        using var tx = conn.BeginTransaction();
        try
        {
            await conn.ExecuteAsync("DELETE FROM creditocliente WHERE idventa = @IdVenta;", new { IdVenta = idVenta }, tx);
            await conn.ExecuteAsync("DELETE FROM cobrocliente WHERE idventa = @IdVenta;", new { IdVenta = idVenta }, tx);

            const string insCredSql = @"
                INSERT INTO creditocliente
                (idventa, nro_venta, nro_factura, total, entrega, saldo, vencimiento, cuota, fecha, dia, idcliente, cidusuario, idsucursal, dessucursal)
                VALUES
                (@IdVenta, @NroVenta, @NroFactura, @Total, 0, @Saldo, @Vencimiento, @Cuota, @Fecha, @Dia, @IdCliente, @CidUsuario, @IdSucursal, '');";

            const string insCobroSql = @"
                INSERT INTO cobrocliente
                (idventa, nro_venta, nro_factura, vencimiento, cuota, fecha, dia,
                 idcliente, cobro, entrega, fecha_cobro, forma_cobro, mora,
                 idcaja, cidusuario, idsucursal, idcobrador, obs)
                VALUES
                (@IdVenta, @NroVenta, @NroFactura, @Vencimiento, @Cuota, @Fecha, @Dia,
                 @IdCliente, @Cobro, 0, @FechaCobro, 'CREDITO', 0,
                 @IdCaja, @CidUsuario, @IdSucursal, 0, @Obs);";

            foreach (var inst in installments)
            {
                await conn.ExecuteAsync(insCredSql, new
                {
                    IdVenta = idVenta,
                    NroVenta = nroVenta,
                    NroFactura = nroFactura ?? nroVenta.ToString(),
                    Total = inst.Monto,
                    Saldo = inst.Saldo,
                    inst.Vencimiento,
                    Cuota = inst.CuotaTexto,
                    Fecha = fechaVenta.Date,
                    inst.Dias,
                    Dia = inst.Dias,
                    IdCliente = idCliente,
                    CidUsuario = cidUsuario ?? "ADMINISTRA",
                    IdSucursal = idSucursal
                }, tx);

                await conn.ExecuteAsync(insCobroSql, new
                {
                    IdVenta = idVenta,
                    NroVenta = nroVenta,
                    NroFactura = nroFactura ?? nroVenta.ToString(),
                    inst.Vencimiento,
                    Cuota = inst.CuotaTexto,
                    Fecha = fechaVenta.Date,
                    Dia = inst.Dias,
                    IdCliente = idCliente,
                    Cobro = inst.Monto,
                    FechaCobro = fechaVenta.Date,
                    IdCaja = idCaja,
                    CidUsuario = cidUsuario ?? "ADMINISTRA",
                    IdSucursal = idSucursal,
                    Obs = $"CUOTA {inst.CuotaTexto}"
                }, tx);
            }

            if (entregaInicial > 0m)
            {
                const string insMovSql = @"
                    INSERT INTO movimientocaja
                    (nrorubro, ingresos, egresos, tipo, detalle, fechaalta, hora,
                     estado, idventas, idcompras, idcaja, idmoneda, cidusuario, obs)
                    VALUES
                    (1, @Entrega, 0, 'ENTRADA', @Detalle, CURDATE(), DATE_FORMAT(NOW(), '%H:%i:%s'),
                     1, @IdVenta, 0, @IdCaja, 1, @CidUsuario, 'ENTREGA INICIAL VENTA CREDITO');";
                await conn.ExecuteAsync(insMovSql, new
                {
                    Entrega = entregaInicial,
                    Detalle = $"ENTREGA INICIAL CREDITO NRO. {nroVenta}",
                    IdVenta = idVenta,
                    IdCaja = idCaja,
                    CidUsuario = cidUsuario ?? "ADMINISTRA"
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

    public async Task<int> RegisterLegalInvoiceRecordAsync(
        int n1, int n2, int n3, int idCliente, int idVenta, int idSucursal, int idCaja, bool advanceTimbrado = true)
    {
        using var conn = _connectionFactory.CreateConnection();
        using var tx = conn.BeginTransaction();
        try
        {
            const string insSql = @"
                INSERT INTO factura_legal (n1, n2, n3, idcliente, idventa)
                VALUES (@N1, @N2, @N3, @IdCliente, @IdVenta);
                SELECT LAST_INSERT_ID();";
            int newId = await conn.ExecuteScalarAsync<int>(insSql, new
            {
                N1 = n1,
                N2 = n2,
                N3 = n3,
                IdCliente = idCliente,
                IdVenta = idVenta
            }, tx);

            if (advanceTimbrado)
            {
                int nextN3 = n3 + 1;
                string nextFac = $"{n1:D3}-{n2:D3}-{nextN3:D7}";
                const string updTim = @"
                    UPDATE timbradolocal
                    SET n3 = @NextN3, factura = @NextFac
                    WHERE idsucursal = @IdSucursal AND idcaja = @IdCaja;";
                await conn.ExecuteAsync(updTim, new
                {
                    NextN3 = nextN3,
                    NextFac = nextFac,
                    IdSucursal = idSucursal,
                    IdCaja = idCaja
                }, tx);
            }

            tx.Commit();
            return newId;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }
}
