using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using FirewallControl.Core.Entities;
using FirewallControl.Core.Interfaces;

namespace FirewallControl.Infrastructure.Repositories;

/// <summary>
/// Implements ICashRepository against cajas, movimientocaja, monedas, and cotizaciones.
/// </summary>
public class CashRepository : ICashRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public CashRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<List<CashRegisterEntity>> GetCashRegistersAsync()
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT idcaja AS IdCaja,
                   descripcion AS Descripcion,
                   saldo AS Saldo,
                   obs AS Obs,
                   idsucursal AS IdSucursal,
                   cidusuario AS CidUsuario,
                   dessucursal AS DesSucursal
            FROM cajas
            ORDER BY idcaja;";
        var rows = await conn.QueryAsync<CashRegisterEntity>(sql);
        return rows.ToList();
    }

    public async Task<List<CashMovementEntity>> GetMovementsAsync(DateTime desde, DateTime hasta, int? idCaja = null)
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT idmovimientocaja AS IdMovimientoCaja,
                   nrorubro AS NroRubro,
                   ingresos AS Ingresos,
                   egresos AS Egresos,
                   tipo AS Tipo,
                   detalle AS Detalle,
                   fechaalta AS FechaAlta,
                   hora AS Hora,
                   estado AS Estado,
                   idventas AS IdVentas,
                   idcompras AS IdCompras,
                   idcaja AS IdCaja,
                   idmoneda AS IdMoneda,
                   cidusuario AS CidUsuario,
                   obs AS Obs
            FROM movimientocaja
            WHERE fechaalta BETWEEN @Desde AND @Hasta
              AND (@IdCaja IS NULL OR idcaja = @IdCaja)
            ORDER BY idmovimientocaja DESC
            LIMIT 300;";

        var rows = await conn.QueryAsync<CashMovementEntity>(sql, new
        {
            Desde = desde.Date,
            Hasta = hasta.Date,
            IdCaja = idCaja
        });
        return rows.ToList();
    }

    public async Task<int> RegisterMovementAsync(CashMovementEntity movement)
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = @"
            INSERT INTO movimientocaja
            (nrorubro, ingresos, egresos, tipo, detalle, fechaalta, hora,
             estado, idventas, idcompras, idcaja, idmoneda, cidusuario, obs)
            VALUES
            (@NroRubro, @Ingresos, @Egresos, @Tipo, @Detalle, @FechaAlta, @Hora,
             @Estado, @IdVentas, @IdCompras, @IdCaja, @IdMoneda, @CidUsuario, @Obs);
            SELECT LAST_INSERT_ID();";

        movement.IdMovimientoCaja = await conn.ExecuteScalarAsync<int>(sql, movement);
        return movement.IdMovimientoCaja;
    }

    public async Task<List<CurrencyEntity>> GetCurrenciesAsync()
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT idmoneda AS IdMoneda,
                   descripcion AS Descripcion,
                   compra AS Compra,
                   venta AS Venta,
                   decimales AS Decimales,
                   tronco AS Tronco,
                   operacion AS Operacion,
                   tipo AS Tipo
            FROM monedas
            ORDER BY idmoneda;";
        var rows = await conn.QueryAsync<CurrencyEntity>(sql);
        return rows.ToList();
    }

    public async Task<ExchangeRateEntity> GetLatestExchangeRateAsync()
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT idcotizacion AS IdCotizacion,
                   fecha AS Fecha,
                   hora AS Hora,
                   compra_dolar AS CompraDolar,
                   compra_guarani AS CompraGuarani,
                   compra_real AS CompraReal,
                   compra_peso AS CompraPeso,
                   compra_euro AS CompraEuro,
                   venta_dolar AS VentaDolar,
                   venta_guarani AS VentaGuarani,
                   venta_real AS VentaReal,
                   venta_peso AS VentaPeso,
                   venta_euro AS VentaEuro,
                   idsucursal AS IdSucursal,
                   dessucursal AS DesSucursal
            FROM cotizaciones
            ORDER BY idcotizacion DESC
            LIMIT 1;";

        var rate = await conn.QueryFirstOrDefaultAsync<ExchangeRateEntity>(sql);
        return rate ?? new ExchangeRateEntity();
    }

    public async Task SaveExchangeRateAsync(ExchangeRateEntity rate)
    {
        using var conn = _connectionFactory.CreateConnection();
        const string insSql = @"
            INSERT INTO cotizaciones
            (fecha, hora, compra_dolar, compra_guarani, compra_real, compra_peso, compra_euro,
             venta_dolar, venta_guarani, venta_real, venta_peso, venta_euro, idsucursal, dessucursal)
            VALUES
            (@Fecha, @Hora, @CompraDolar, @CompraGuarani, @CompraReal, @CompraPeso, @CompraEuro,
             @VentaDolar, @VentaGuarani, @VentaReal, @VentaPeso, @VentaEuro, @IdSucursal, @DesSucursal);";

        await conn.ExecuteAsync(insSql, rate);
    }
}
