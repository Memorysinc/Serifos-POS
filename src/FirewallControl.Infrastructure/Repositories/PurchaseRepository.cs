using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using FirewallControl.Core.Entities;
using FirewallControl.Core.Interfaces;

namespace FirewallControl.Infrastructure.Repositories;

/// <summary>
/// Implements IPurchaseRepository against proveedores, compras, itemscompras, itemsdepositos, and productos.
/// </summary>
public class PurchaseRepository : IPurchaseRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public PurchaseRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<int> GetNextNroCompraAsync()
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = "SELECT COALESCE(MAX(nro_compra), 0) + 1 FROM compras;";
        return await conn.ExecuteScalarAsync<int>(sql);
    }

    public async Task<PurchaseEntity?> GetPurchaseByIdAsync(int idCompra)
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT c.idcompra AS IdCompra,
                   c.nro_compra AS NroCompra,
                   c.nro_factura AS NroFactura,
                   c.fecha_compra AS FechaCompra,
                   c.forma_pago AS FormaPago,
                   c.iddeposito AS IdDeposito,
                   c.idcaja AS IdCaja,
                   c.idproveedor AS IdProveedor,
                   COALESCE(p.descripcion, 'PROVEEDOR') AS NombreProveedor,
                   c.timbrado AS Timbrado,
                   c.total_exenta AS TotalExenta,
                   c.total_iva5 AS TotalIva5,
                   c.total_iva10 AS TotalIva10,
                   c.subtotal AS Subtotal,
                   c.descuento AS Descuento,
                   c.total_compra AS TotalCompra,
                   c.liquidacion_iva5 AS LiquidacionIva5,
                   c.liquidacion_iva10 AS LiquidacionIva10,
                   c.total_impuesto AS TotalImpuesto,
                   c.cidusuario AS CidUsuario,
                   c.estado AS Estado,
                   c.idsucursal AS IdSucursal
            FROM compras c
            LEFT JOIN proveedores p ON c.idproveedor = p.idproveedor
            WHERE c.idcompra = @Id
            LIMIT 1;";

        var purchase = await conn.QueryFirstOrDefaultAsync<PurchaseEntity>(sql, new { Id = idCompra });
        if (purchase == null)
            return null;

        const string itemsSql = @"
            SELECT i.iditemcompra AS IdItemCompra,
                   i.idcompra AS IdCompra,
                   i.nro_factura AS NroFactura,
                   i.nro_compra AS NroCompra,
                   i.idproducto AS IdProducto,
                   i.codigo AS Codigo,
                   COALESCE(p.descripcion, i.codigo) AS Descripcion,
                   i.cantidad AS Cantidad,
                   i.pre_unitario AS PreUnitario,
                   i.pre_exenta AS PreExenta,
                   i.pre_iva5 AS PreIva5,
                   i.pre_iva10 AS PreIva10
            FROM itemscompras i
            LEFT JOIN productos p ON i.idproducto = p.idproducto
            WHERE i.idcompra = @Id
            ORDER BY i.iditemcompra;";

        var items = await conn.QueryAsync<PurchaseItemEntity>(itemsSql, new { Id = idCompra });
        purchase.Items = items.ToList();
        return purchase;
    }

    public async Task<bool> DeletePurchaseAsync(int idCompra)
    {
        using var conn = _connectionFactory.CreateConnection();
        using var tx = conn.BeginTransaction();
        try
        {
            var purchase = await conn.QueryFirstOrDefaultAsync<PurchaseEntity>(
                "SELECT idcompra AS IdCompra, iddeposito AS IdDeposito FROM compras WHERE idcompra = @Id;",
                new { Id = idCompra }, tx);

            if (purchase == null)
                return false;

            var items = await conn.QueryAsync<PurchaseItemEntity>(
                "SELECT idproducto AS IdProducto, cantidad AS Cantidad FROM itemscompras WHERE idcompra = @Id;",
                new { Id = idCompra }, tx);

            foreach (var item in items)
            {
                await conn.ExecuteAsync(@"
                    UPDATE itemsdepositos
                    SET cantidad = cantidad - @Cantidad
                    WHERE iddeposito = @IdDeposito AND idproducto = @IdProducto;",
                    new { item.Cantidad, purchase.IdDeposito, item.IdProducto }, tx);
            }

            await conn.ExecuteAsync("DELETE FROM itemscompras WHERE idcompra = @Id;", new { Id = idCompra }, tx);
            await conn.ExecuteAsync("DELETE FROM movimientocaja WHERE idcompras = @Id;", new { Id = idCompra }, tx);
            int rows = await conn.ExecuteAsync("DELETE FROM compras WHERE idcompra = @Id;", new { Id = idCompra }, tx);

            tx.Commit();
            return rows > 0;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task<List<SupplierEntity>> SearchSuppliersAsync(string searchText, int limit = 100)
    {
        using var conn = _connectionFactory.CreateConnection();
        string term = (searchText ?? "").Trim();
        const string sql = @"
            SELECT idproveedor AS IdProveedor,
                   ruc AS Ruc,
                   descripcion AS Descripcion,
                   contacto AS Contacto,
                   direccion AS Direccion,
                   telefono AS Telefono,
                   celular AS Celular,
                   fax AS Fax,
                   email AS Email,
                   estado AS Estado,
                   timbrado AS Timbrado,
                   fecha AS Fecha,
                   userid AS UserId,
                   idciudad AS IdCiudad
            FROM proveedores
            WHERE estado = 1
              AND (@Term = '' OR descripcion LIKE @LikeTerm OR ruc LIKE @LikeTerm)
            ORDER BY descripcion
            LIMIT @Limit;";

        var rows = await conn.QueryAsync<SupplierEntity>(sql, new
        {
            Term = term,
            LikeTerm = $"%{term}%",
            Limit = limit
        });
        return rows.ToList();
    }

    public async Task<int> SaveSupplierAsync(SupplierEntity supplier, string lcTipo)
    {
        using var conn = _connectionFactory.CreateConnection();
        bool isModify = string.Equals(lcTipo, "M", StringComparison.OrdinalIgnoreCase) && supplier.IdProveedor > 0;

        if (!isModify)
        {
            const string insSql = @"
                INSERT INTO proveedores
                (ruc, descripcion, contacto, direccion, telefono, celular, fax, email, estado, timbrado, fecha, userid, idciudad)
                VALUES
                (@Ruc, @Descripcion, @Contacto, @Direccion, @Telefono, @Celular, @Fax, @Email, @Estado, @Timbrado, @Fecha, @UserId, @IdCiudad);
                SELECT LAST_INSERT_ID();";
            supplier.IdProveedor = await conn.ExecuteScalarAsync<int>(insSql, supplier);
            return supplier.IdProveedor;
        }
        else
        {
            const string updSql = @"
                UPDATE proveedores SET
                    ruc = @Ruc,
                    descripcion = @Descripcion,
                    contacto = @Contacto,
                    direccion = @Direccion,
                    telefono = @Telefono,
                    celular = @Celular,
                    fax = @Fax,
                    email = @Email,
                    estado = @Estado,
                    timbrado = @Timbrado
                WHERE idproveedor = @IdProveedor;";
            await conn.ExecuteAsync(updSql, supplier);
            return supplier.IdProveedor;
        }
    }

    public async Task<bool> DeleteSupplierAsync(int idProveedor)
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = "UPDATE proveedores SET estado = 0 WHERE idproveedor = @Id;";
        int rows = await conn.ExecuteAsync(sql, new { Id = idProveedor });
        return rows > 0;
    }

    public async Task<List<PurchaseEntity>> GetRecentPurchasesAsync(DateTime? desde = null, DateTime? hasta = null, int limit = 100)
    {
        using var conn = _connectionFactory.CreateConnection();
        DateTime from = (desde ?? DateTime.Today.AddDays(-60)).Date;
        DateTime to = (hasta ?? DateTime.Today).Date;

        const string sql = @"
            SELECT c.idcompra AS IdCompra,
                   c.nro_compra AS NroCompra,
                   c.nro_factura AS NroFactura,
                   c.fecha_compra AS FechaCompra,
                   c.forma_pago AS FormaPago,
                   c.iddeposito AS IdDeposito,
                   c.idcaja AS IdCaja,
                   c.idproveedor AS IdProveedor,
                   COALESCE(p.descripcion, 'PROVEEDOR') AS NombreProveedor,
                   c.timbrado AS Timbrado,
                   c.total_exenta AS TotalExenta,
                   c.total_iva5 AS TotalIva5,
                   c.total_iva10 AS TotalIva10,
                   c.subtotal AS Subtotal,
                   c.descuento AS Descuento,
                   c.total_compra AS TotalCompra,
                   c.liquidacion_iva5 AS LiquidacionIva5,
                   c.liquidacion_iva10 AS LiquidacionIva10,
                   c.total_impuesto AS TotalImpuesto,
                   c.cidusuario AS CidUsuario,
                   c.estado AS Estado,
                   c.idsucursal AS IdSucursal
            FROM compras c
            LEFT JOIN proveedores p ON c.idproveedor = p.idproveedor
            WHERE c.fecha_compra BETWEEN @From AND @To
            ORDER BY c.idcompra DESC
            LIMIT @Limit;";

        var rows = await conn.QueryAsync<PurchaseEntity>(sql, new { From = from, To = to, Limit = limit });
        return rows.ToList();
    }

    public async Task<int> ProcessPurchaseTransactionAsync(PurchaseEntity purchase)
    {
        using var conn = _connectionFactory.CreateConnection();
        using var tx = conn.BeginTransaction();
        try
        {
            if (purchase.NroCompra <= 0)
            {
                purchase.NroCompra = await conn.ExecuteScalarAsync<int>(
                    "SELECT COALESCE(MAX(nro_compra), 0) + 1 FROM compras;", transaction: tx);
            }

            const string insCompraSql = @"
                INSERT INTO compras
                (nro_compra, nro_factura, fecha_compra, forma_pago, iddeposito, idcaja,
                 idproveedor, timbrado, total_exenta, total_iva5, total_iva10,
                 subtotal, descuento, total_compra, liquidacion_iva5, liquidacion_iva10, total_impuesto,
                 cidusuario, concepto, estado, tipoboleta, nro_boleta, observacion, vencimiento, idmoneda, idsucursal, dessucursal)
                VALUES
                (@NroCompra, @NroFactura, @FechaCompra, @FormaPago, @IdDeposito, @IdCaja,
                 @IdProveedor, @Timbrado, @TotalExenta, @TotalIva5, @TotalIva10,
                 @Subtotal, @Descuento, @TotalCompra, @LiquidacionIva5, @LiquidacionIva10, @TotalImpuesto,
                 @CidUsuario, @Concepto, @Estado, @TipoBoleta, @NroBoleta, @Observacion, @Vencimiento, @IdMoneda, @IdSucursal, @DesSucursal);
                SELECT LAST_INSERT_ID();";

            purchase.IdCompra = await conn.ExecuteScalarAsync<int>(insCompraSql, purchase, tx);

            const string insItemSql = @"
                INSERT INTO itemscompras
                (idcompra, nro_factura, nro_compra, idproducto, codigo,
                 cantidad, pre_unitario, pre_exenta, pre_iva5, pre_iva10)
                VALUES
                (@IdCompra, @NroFactura, @NroCompra, @IdProducto, @Codigo,
                 @Cantidad, @PreUnitario, @PreExenta, @PreIva5, @PreIva10);";

            foreach (var item in purchase.Items)
            {
                item.IdCompra = purchase.IdCompra;
                item.NroCompra = purchase.NroCompra;
                item.NroFactura = purchase.NroFactura;
                await conn.ExecuteAsync(insItemSql, item, tx);

                const string updDep = @"
                    UPDATE itemsdepositos
                    SET cantidad = cantidad + @Cantidad
                    WHERE iddeposito = @IdDeposito AND idproducto = @IdProducto;";
                int aff = await conn.ExecuteAsync(updDep, new
                {
                    item.Cantidad,
                    purchase.IdDeposito,
                    item.IdProducto
                }, tx);

                if (aff == 0)
                {
                    const string insDep = @"
                        INSERT INTO itemsdepositos (iddeposito, idproducto, codigo, cantidad)
                        VALUES (@IdDeposito, @IdProducto, @Codigo, @Cantidad);";
                    await conn.ExecuteAsync(insDep, new
                    {
                        purchase.IdDeposito,
                        item.IdProducto,
                        item.Codigo,
                        item.Cantidad
                    }, tx);
                }

                if (item.PreUnitario > 0)
                {
                    decimal effectiveCostoMedio = item.CostoMedio > 0 ? item.CostoMedio : item.PreUnitario;
                    if (item.ActualizarPreciosVenta && item.Precio1 > 0)
                    {
                        const string updFull = @"
                            UPDATE productos
                            SET costo1 = @PreUnitario,
                                costomedio = @CostoMedio,
                                fechaultimacompra = @Fecha,
                                cantidadultimacompra = @Cantidad,
                                p1 = @P1, precio1 = @Precio1,
                                p2 = CASE WHEN @Precio2 > 0 THEN @P2 ELSE p2 END,
                                precio2 = CASE WHEN @Precio2 > 0 THEN @Precio2 ELSE precio2 END,
                                p3 = CASE WHEN @Precio3 > 0 THEN @P3 ELSE p3 END,
                                precio3 = CASE WHEN @Precio3 > 0 THEN @Precio3 ELSE precio3 END,
                                p4 = CASE WHEN @Precio4 > 0 THEN @P4 ELSE p4 END,
                                precio4 = CASE WHEN @Precio4 > 0 THEN @Precio4 ELSE precio4 END,
                                p5 = CASE WHEN @Precio5 > 0 THEN @P5 ELSE p5 END,
                                precio5 = CASE WHEN @Precio5 > 0 THEN @Precio5 ELSE precio5 END,
                                p6 = CASE WHEN @Precio6 > 0 THEN @P6 ELSE p6 END,
                                precio6 = CASE WHEN @Precio6 > 0 THEN @Precio6 ELSE precio6 END,
                                p7 = CASE WHEN @Precio7 > 0 THEN @P7 ELSE p7 END,
                                precio7 = CASE WHEN @Precio7 > 0 THEN @Precio7 ELSE precio7 END,
                                controlagarantia = @ControlaGarantia,
                                diasgarantias = @DiasGarantias
                            WHERE idproducto = @IdProducto;";
                        await conn.ExecuteAsync(updFull, new
                        {
                            item.PreUnitario,
                            CostoMedio = effectiveCostoMedio,
                            Fecha = purchase.FechaCompra,
                            item.Cantidad,
                            item.P1,
                            item.Precio1,
                            item.P2,
                            item.Precio2,
                            item.P3,
                            item.Precio3,
                            item.P4,
                            item.Precio4,
                            item.P5,
                            item.Precio5,
                            item.P6,
                            item.Precio6,
                            item.P7,
                            item.Precio7,
                            item.ControlaGarantia,
                            item.DiasGarantias,
                            item.IdProducto
                        }, tx);
                    }
                    else
                    {
                        const string updCost = @"
                            UPDATE productos
                            SET costo1 = @PreUnitario,
                                costomedio = @CostoMedio,
                                fechaultimacompra = @Fecha,
                                cantidadultimacompra = @Cantidad
                            WHERE idproducto = @IdProducto;";
                        await conn.ExecuteAsync(updCost, new
                        {
                            item.PreUnitario,
                            CostoMedio = effectiveCostoMedio,
                            Fecha = purchase.FechaCompra,
                            item.Cantidad,
                            item.IdProducto
                        }, tx);
                    }
                }
            }

            if (purchase.FormaPago == 1 && purchase.TotalCompra > 0)
            {
                const string insCaja = @"
                    INSERT INTO movimientocaja
                    (nrorubro, ingresos, egresos, tipo, detalle, fechaalta, hora, estado, idventas, idcompras, idcaja, idmoneda, cidusuario, obs)
                    VALUES
                    (2, 0, @TotalCompra, 'SALIDA', @Detalle, @FechaCompra, @Hora, 1, 0, @IdCompra, @IdCaja, @IdMoneda, @CidUsuario, @Obs);";
                await conn.ExecuteAsync(insCaja, new
                {
                    purchase.TotalCompra,
                    Detalle = $"COMPRA FACT. {purchase.NroFactura} ({purchase.NombreProveedor})",
                    purchase.FechaCompra,
                    Hora = DateTime.Now.ToString("HH:mm:ss"),
                    purchase.IdCompra,
                    purchase.IdCaja,
                    purchase.IdMoneda,
                    purchase.CidUsuario,
                    Obs = purchase.Observacion ?? ""
                }, tx);
            }

            tx.Commit();
            return purchase.IdCompra;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }
}
