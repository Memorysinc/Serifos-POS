using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using FirewallControl.Core.Entities;
using FirewallControl.Core.Interfaces;

namespace FirewallControl.Infrastructure.Repositories;

/// <summary>
/// Implements ICustomerRepository against clientes, cobrocliente, and movimientocaja.
/// </summary>
public class CustomerRepository : ICustomerRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public CustomerRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<CustomerEntity?> GetByIdAsync(int idCliente)
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = "SELECT * FROM clientes WHERE idcliente = @Id LIMIT 1;";
        return await conn.QueryFirstOrDefaultAsync<CustomerEntity>(sql, new { Id = idCliente });
    }

    public async Task<List<CustomerEntity>> SearchCustomersAsync(string searchText, int limit = 100)
    {
        using var conn = _connectionFactory.CreateConnection();
        string term = (searchText ?? "").Trim();
        const string sql = @"
            SELECT *
            FROM clientes
            WHERE estado = 1
              AND (@Term = '' OR nombres LIKE @LikeTerm OR ruc LIKE @LikeTerm)
            ORDER BY nombres
            LIMIT @Limit;";

        var rows = await conn.QueryAsync<CustomerEntity>(sql, new
        {
            Term = term,
            LikeTerm = $"%{term}%",
            Limit = limit
        });
        return rows.ToList();
    }

    public async Task<int> SaveCustomerAsync(CustomerEntity customer, string lcTipo)
    {
        using var conn = _connectionFactory.CreateConnection();
        bool isModify = string.Equals(lcTipo, "M", StringComparison.OrdinalIgnoreCase) && customer.IdCliente > 0;

        if (!isModify)
        {
            const string insSql = @"
                INSERT INTO clientes
                (ruc, dv, nombres, direccion, idciudad,
                 telefono, celular, email, idvendedor, lineacredito,
                 contado, credito, estado,
                 precio1, precio2, precio3, precio4, precio5, precio6, precio7,
                 idsucursal)
                VALUES
                (@Ruc, @Dv, @Nombres, @Direccion, @IdCiudad,
                 @Telefono, @Celular, @Email, @IdVendedor, @LineaCredito,
                 @Contado, @Credito, @Estado,
                 @Precio1, @Precio2, @Precio3, @Precio4, @Precio5, @Precio6, @Precio7,
                 @IdSucursal);
                SELECT LAST_INSERT_ID();";

            customer.IdCliente = await conn.ExecuteScalarAsync<int>(insSql, customer);
            return customer.IdCliente;
        }
        else
        {
            const string updSql = @"
                UPDATE clientes SET
                    ruc = @Ruc,
                    dv = @Dv,
                    nombres = @Nombres,
                    direccion = @Direccion,
                    idciudad = @IdCiudad,
                    telefono = @Telefono,
                    celular = @Celular,
                    email = @Email,
                    idvendedor = @IdVendedor,
                    lineacredito = @LineaCredito,
                    contado = @Contado,
                    credito = @Credito,
                    estado = @Estado,
                    precio1 = @Precio1,
                    precio2 = @Precio2,
                    precio3 = @Precio3
                WHERE idcliente = @IdCliente;";

            await conn.ExecuteAsync(updSql, customer);
            return customer.IdCliente;
        }
    }

    public async Task<bool> DeleteCustomerAsync(int idCliente)
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = "UPDATE clientes SET estado = 0 WHERE idcliente = @Id;";
        int rows = await conn.ExecuteAsync(sql, new { Id = idCliente });
        return rows > 0;
    }

    public async Task<List<CustomerCollectionEntity>> GetPendingCollectionsAsync(int? idCliente = null)
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT cc.idcobro AS IdCobro,
                   cc.idventa AS IdVenta,
                   cc.nro_venta AS NroVenta,
                   cc.nro_factura AS NroFactura,
                   cc.vencimiento AS Vencimiento,
                   cc.cuota AS Cuota,
                   cc.fecha AS Fecha,
                   cc.dia AS Dia,
                   cc.idcliente AS IdCliente,
                   COALESCE(c.nombres, 'CLIENTE') AS NombreCliente,
                   cc.cobro AS Cobro,
                   cc.entrega AS Entrega,
                   cc.fecha_cobro AS FechaCobro,
                   cc.forma_cobro AS FormaCobro,
                   cc.mora AS Mora,
                   cc.idcaja AS IdCaja,
                   cc.cidusuario AS CidUsuario,
                   cc.idsucursal AS IdSucursal,
                   cc.obs AS Obs
            FROM cobrocliente cc
            LEFT JOIN clientes c ON cc.idcliente = c.idcliente
            WHERE (@IdCliente IS NULL OR cc.idcliente = @IdCliente)
            ORDER BY cc.idcobro DESC
            LIMIT 200;";

        var rows = await conn.QueryAsync<CustomerCollectionEntity>(sql, new { IdCliente = idCliente });
        return rows.ToList();
    }

    public async Task RegisterCollectionPaymentAsync(int idCobro, decimal montoEntrega, int idCaja, string usuario)
    {
        using var conn = _connectionFactory.CreateConnection();
        using var tx = conn.BeginTransaction();
        try
        {
            const string updSql = @"
                UPDATE cobrocliente
                SET entrega = LEAST(cobro, entrega + @Monto),
                    fecha_cobro = CURDATE(),
                    idcaja = @IdCaja,
                    cidusuario = @Usuario
                WHERE idcobro = @IdCobro;";
            await conn.ExecuteAsync(updSql, new
            {
                Monto = montoEntrega,
                IdCaja = idCaja,
                Usuario = usuario,
                IdCobro = idCobro
            }, tx);

            const string insMovSql = @"
                INSERT INTO movimientocaja
                (nrorubro, ingresos, egresos, tipo, detalle, fechaalta, hora,
                 estado, idventas, idcompras, idcaja, idmoneda, cidusuario, obs)
                VALUES
                (1, @Monto, 0, 'ENTRADA', @Detalle, CURDATE(), DATE_FORMAT(NOW(), '%H:%i:%s'),
                 1, 0, 0, @IdCaja, 1, @Usuario, 'COBRO DE CLIENTE');";
            await conn.ExecuteAsync(insMovSql, new
            {
                Monto = montoEntrega,
                Detalle = $"COBRO RECIBO #{idCobro}",
                IdCaja = idCaja,
                Usuario = usuario
            }, tx);

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }
}
