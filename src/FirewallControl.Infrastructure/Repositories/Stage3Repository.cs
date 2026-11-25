using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using FirewallControl.Core.Entities;
using FirewallControl.Core.Interfaces;

namespace FirewallControl.Infrastructure.Repositories;

/// <summary>
/// Consolidated MySQL 5.5 repository backing _FONDO real-time KPIs/feeds and all Stage 3 modules
/// (Master catalogs, Agenda/SMS, Commercial team, Operations/Returns/Credit, Extended reports, Audit, RRHH & Restaurant).
/// </summary>
public class Stage3Repository : IStage3Repository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public Stage3Repository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    private static string Trunc(string? s, int maxLen)
    {
        string clean = (s ?? string.Empty).Trim();
        return clean.Length > maxLen ? clean[..maxLen] : clean;
    }

    private static string FormatPaymentType(string? raw)
    {
        string val = (raw ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(val) || val == "1") return "Contado";
        if (val == "2") return "Crédito";
        if (val == "3") return "Tarjeta";
        return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(val.ToLowerInvariant());
    }

    // =========================================================================
    // _FONDO REAL-TIME KPIS & FEEDS (fondo_ui.prg)
    // =========================================================================
    public async Task<FondoDashboardData> GetFondoDashboardDataAsync(string currentUser)
    {
        var data = new FondoDashboardData();
        using var conn = _connectionFactory.CreateConnection();

        try
        {
            var catalogCounts = await conn.QueryFirstOrDefaultAsync<(int totp, int totc)>(
                "SELECT (SELECT COUNT(*) FROM productos WHERE COALESCE(estado, 1) = 1) AS totp, (SELECT COUNT(*) FROM clientes) AS totc;");
            data.TotalProductosActivos = catalogCounts.totp;
            data.TotalClientes = catalogCounts.totc;
        }
        catch { }

        try
        {
            var cashFlow = await conn.QueryFirstOrDefaultAsync<(decimal ing, decimal egr)>(
                "SELECT COALESCE(SUM(ingresos), 0) AS ing, COALESCE(SUM(egresos), 0) AS egr FROM movimientocaja WHERE DATE(fechaalta) = CURDATE();");
            data.IngresosCajaHoy = cashFlow.ing;
            data.EgresosCajaHoy = cashFlow.egr;
        }
        catch { }

        try
        {
            var salesToday = await conn.QueryFirstOrDefaultAsync<(int cnt, decimal tot)>(
                "SELECT COUNT(*) AS cnt, COALESCE(SUM(total_venta), 0) AS tot FROM ventas WHERE DATE(fecha_venta) = CURDATE() AND COALESCE(estado, 1) <> 0;");
            data.VentasCountHoy = salesToday.cnt;
            data.VentasTotalHoy = salesToday.tot;
        }
        catch { }

        try
        {
            string u = (currentUser ?? string.Empty).Trim().ToUpperInvariant();
            data.AgendaPendientesHoy = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM agenda WHERE DATE(FECHA) = CURDATE() AND (UPPER(TRIM(COALESCE(userid, ''))) = @User OR TRIM(COALESCE(userid, '')) = '');",
                new { User = u });
        }
        catch { }

        try
        {
            const string recentSql = @"
                SELECT v.idventa AS IdVenta,
                       CAST(COALESCE(NULLIF(TRIM(CAST(COALESCE(v.nro_venta, '') AS CHAR(14))), ''), v.idventa) AS CHAR(14)) AS NroVenta,
                       CASE WHEN v.fecha_venta IS NULL OR CAST(v.fecha_venta AS CHAR) = '0000-00-00' THEN CURDATE() ELSE v.fecha_venta END AS Fecha,
                       COALESCE(NULLIF(TRIM(v.hora_venta), ''), '--:--') AS Hora,
                       CAST(COALESCE(v.forma_pago, '1') AS CHAR(12)) AS FormaPago,
                       COALESCE(NULLIF(TRIM(c.nombres), ''), 'OCASIONAL') AS Cliente,
                       COALESCE(v.total_venta, 0) AS Total
                FROM ventas v
                LEFT JOIN clientes c ON c.idcliente = v.idcliente
                WHERE COALESCE(v.estado, 1) <> 0
                ORDER BY v.idventa DESC
                LIMIT 8;";
            var recent = (await conn.QueryAsync<FondoRecentSaleRow>(recentSql)).ToList();
            foreach (var r in recent)
                r.FormaPago = FormatPaymentType(r.FormaPago);
            data.UltimasVentas = recent;
        }
        catch { }

        try
        {
            const string topTodaySql = @"
                SELECT COALESCE(TRIM(iv.codigo), '') AS Codigo,
                       COALESCE(TRIM(MAX(p.descripcion)), TRIM(MAX(iv.descripcion)), '') AS Producto,
                       COALESCE(SUM(iv.cantidad), 0) AS Cantidad,
                       COALESCE(SUM(COALESCE(iv.pre_exenta, 0) + COALESCE(iv.pre_iva5, 0) + COALESCE(iv.pre_iva10, 0)), 0) AS TotalVenta,
                       COALESCE(SUM((COALESCE(iv.pre_exenta, 0) + COALESCE(iv.pre_iva5, 0) + COALESCE(iv.pre_iva10, 0)) - (iv.cantidad * COALESCE(p.costomedio, 0))), 0) AS GananciaEstimada
                FROM itemsventas iv
                INNER JOIN ventas v ON v.idventa = iv.idventa
                LEFT JOIN productos p ON p.idproducto = iv.idproducto
                WHERE DATE(v.fecha_venta) = CURDATE() AND COALESCE(v.estado, 1) <> 0
                GROUP BY iv.idproducto, iv.codigo
                ORDER BY TotalVenta DESC
                LIMIT 10;";
            var top = (await conn.QueryAsync<FondoTopProductRow>(topTodaySql)).ToList();
            if (top.Count == 0)
            {
                const string top7Sql = @"
                    SELECT COALESCE(TRIM(iv.codigo), '') AS Codigo,
                           COALESCE(TRIM(MAX(p.descripcion)), TRIM(MAX(iv.descripcion)), '') AS Producto,
                           COALESCE(SUM(iv.cantidad), 0) AS Cantidad,
                           COALESCE(SUM(COALESCE(iv.pre_exenta, 0) + COALESCE(iv.pre_iva5, 0) + COALESCE(iv.pre_iva10, 0)), 0) AS TotalVenta,
                           COALESCE(SUM((COALESCE(iv.pre_exenta, 0) + COALESCE(iv.pre_iva5, 0) + COALESCE(iv.pre_iva10, 0)) - (iv.cantidad * COALESCE(p.costomedio, 0))), 0) AS GananciaEstimada
                    FROM itemsventas iv
                    INNER JOIN ventas v ON v.idventa = iv.idventa
                    LEFT JOIN productos p ON p.idproducto = iv.idproducto
                    WHERE v.fecha_venta >= DATE_SUB(CURDATE(), INTERVAL 7 DAY) AND COALESCE(v.estado, 1) <> 0
                    GROUP BY iv.idproducto, iv.codigo
                    ORDER BY TotalVenta DESC
                    LIMIT 10;";
                top = (await conn.QueryAsync<FondoTopProductRow>(top7Sql)).ToList();
                data.TopIsFallback7Days = top.Count > 0;
            }
            data.TopVendidos = top;
        }
        catch { }

        return data;
    }

    // =========================================================================
    // MASTER CATALOGS (MONEDAS, CIUDADES, BARRIOS, CAJAS, RUBROS, SUCURSAL)
    // =========================================================================
    public async Task<int> SaveCurrencyAsync(CurrencyEntity currency, string lcTipo)
    {
        using var conn = _connectionFactory.CreateConnection();
        string desc = Trunc(currency.Descripcion, 30).ToUpperInvariant();
        string op = string.IsNullOrWhiteSpace(currency.Operacion) ? "*" : Trunc(currency.Operacion, 1);
        bool isModify = string.Equals(lcTipo, "M", StringComparison.OrdinalIgnoreCase) && currency.IdMoneda > 0;
        if (!isModify)
        {
            const string ins = @"
                INSERT INTO monedas (descripcion, compra, venta, decimales, tronco, operacion, tipo)
                VALUES (@Desc, @Compra, @Venta, @Decimales, @Tronco, @Op, @Tipo);
                SELECT LAST_INSERT_ID();";
            currency.IdMoneda = await conn.ExecuteScalarAsync<int>(ins, new
            {
                Desc = desc, currency.Compra, currency.Venta, currency.Decimales, currency.Tronco, Op = op, Tipo = 1
            });
            return currency.IdMoneda;
        }
        await conn.ExecuteAsync(
            "UPDATE monedas SET descripcion=@Desc, compra=@Compra, venta=@Venta, decimales=@Decimales, tronco=@Tronco, operacion=@Op WHERE idmoneda=@Id;",
            new { Id = currency.IdMoneda, Desc = desc, currency.Compra, currency.Venta, currency.Decimales, currency.Tronco, Op = op });
        return currency.IdMoneda;
    }

    public async Task<bool> DeleteCurrencyAsync(int idMoneda)
    {
        using var conn = _connectionFactory.CreateConnection();
        return await conn.ExecuteAsync("DELETE FROM monedas WHERE idmoneda = @Id;", new { Id = idMoneda }) > 0;
    }

    public async Task<List<CityEntity>> GetCitiesAsync()
    {
        using var conn = _connectionFactory.CreateConnection();
        return (await conn.QueryAsync<CityEntity>(
            "SELECT idciudad AS IdCiudad, ciudad AS Ciudad, COALESCE(departamento, '') AS Departamento FROM ciudad ORDER BY ciudad;")).ToList();
    }

    public async Task<int> SaveCityAsync(CityEntity city, string lcTipo)
    {
        using var conn = _connectionFactory.CreateConnection();
        string nombre = Trunc(city.Ciudad, 100).ToUpperInvariant();
        string dpto = Trunc(city.Departamento, 100).ToUpperInvariant();
        if (string.Equals(lcTipo, "M", StringComparison.OrdinalIgnoreCase) && city.IdCiudad > 0)
        {
            await conn.ExecuteAsync("UPDATE ciudad SET ciudad=@Nombre, departamento=@Dpto WHERE idciudad=@Id;",
                new { Id = city.IdCiudad, Nombre = nombre, Dpto = dpto });
            return city.IdCiudad;
        }
        city.IdCiudad = await conn.ExecuteScalarAsync<int>(
            "INSERT INTO ciudad (ciudad, departamento) VALUES (@Nombre, @Dpto); SELECT LAST_INSERT_ID();",
            new { Nombre = nombre, Dpto = dpto });
        return city.IdCiudad;
    }

    public async Task<bool> DeleteCityAsync(int idCiudad)
    {
        using var conn = _connectionFactory.CreateConnection();
        return await conn.ExecuteAsync("DELETE FROM ciudad WHERE idciudad = @Id;", new { Id = idCiudad }) > 0;
    }

    public async Task<List<BarrioEntity>> GetBarriosAsync(int? idCiudad = null)
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT b.idbarrio AS IdBarrio,
                   b.barrio AS Barrio,
                   b.idciudad AS IdCiudad,
                   COALESCE(c.ciudad, '') AS CiudadNombre
            FROM barrio b
            LEFT JOIN ciudad c ON b.idciudad = c.idciudad
            WHERE (@IdCiudad IS NULL OR b.idciudad = @IdCiudad)
            ORDER BY b.barrio;";
        return (await conn.QueryAsync<BarrioEntity>(sql, new { IdCiudad = idCiudad is > 0 ? idCiudad : null })).ToList();
    }

    public async Task<int> SaveBarrioAsync(BarrioEntity barrio, string lcTipo)
    {
        using var conn = _connectionFactory.CreateConnection();
        string nom = Trunc(barrio.Barrio, 100).ToUpperInvariant();
        if (string.Equals(lcTipo, "M", StringComparison.OrdinalIgnoreCase) && barrio.IdBarrio > 0)
        {
            await conn.ExecuteAsync("UPDATE barrio SET barrio=@Nom, idciudad=@IdCiudad WHERE idbarrio=@Id;",
                new { Id = barrio.IdBarrio, Nom = nom, barrio.IdCiudad });
            return barrio.IdBarrio;
        }
        barrio.IdBarrio = await conn.ExecuteScalarAsync<int>(
            "INSERT INTO barrio (barrio, idciudad, cidusuario, idsucursal, dessucursal) VALUES (@Nom, @IdCiudad, 'ADMINISTRA', 1, 'CASA CENTRAL'); SELECT LAST_INSERT_ID();",
            new { Nom = nom, barrio.IdCiudad });
        return barrio.IdBarrio;
    }

    public async Task<bool> DeleteBarrioAsync(int idBarrio)
    {
        using var conn = _connectionFactory.CreateConnection();
        return await conn.ExecuteAsync("DELETE FROM barrio WHERE idbarrio = @Id;", new { Id = idBarrio }) > 0;
    }

    public async Task<int> SaveCashRegisterAsync(CashRegisterEntity cashRegister, string lcTipo)
    {
        using var conn = _connectionFactory.CreateConnection();
        string desc = Trunc(cashRegister.Descripcion, 60).ToUpperInvariant();
        string obs = cashRegister.Obs ?? string.Empty;
        string user = Trunc(string.IsNullOrWhiteSpace(cashRegister.CidUsuario) ? "ADMINISTRA" : cashRegister.CidUsuario, 20);
        string desSuc = Trunc(string.IsNullOrWhiteSpace(cashRegister.DesSucursal) ? "CASA CENTRAL" : cashRegister.DesSucursal, 50);

        if (string.Equals(lcTipo, "M", StringComparison.OrdinalIgnoreCase) && cashRegister.IdCaja > 0)
        {
            await conn.ExecuteAsync(
                "UPDATE cajas SET descripcion=@Desc, saldo=@Saldo, obs=@Obs, idsucursal=@Suc, cidusuario=@User, dessucursal=@DesSuc WHERE idcaja=@Id;",
                new { Id = cashRegister.IdCaja, Desc = desc, cashRegister.Saldo, Obs = obs, Suc = cashRegister.IdSucursal, User = user, DesSuc = desSuc });
            return cashRegister.IdCaja;
        }

        cashRegister.IdCaja = await conn.ExecuteScalarAsync<int>(
            "INSERT INTO cajas (descripcion, saldo, obs, idsucursal, cidusuario, dessucursal) VALUES (@Desc, @Saldo, @Obs, @Suc, @User, @DesSuc); SELECT LAST_INSERT_ID();",
            new { Desc = desc, cashRegister.Saldo, Obs = obs, Suc = cashRegister.IdSucursal, User = user, DesSuc = desSuc });
        return cashRegister.IdCaja;
    }

    public async Task<bool> DeleteCashRegisterAsync(int idCaja)
    {
        using var conn = _connectionFactory.CreateConnection();
        return await conn.ExecuteAsync("DELETE FROM cajas WHERE idcaja = @Id;", new { Id = idCaja }) > 0;
    }

    public async Task<List<CashRubroEntity>> GetCashRubrosAsync()
    {
        using var conn = _connectionFactory.CreateConnection();
        return (await conn.QueryAsync<CashRubroEntity>(
            "SELECT idrubros AS IdRubros, nrorubro AS NroRubro, nombrerubro AS NombreRubro, tipocuenta AS TipoCuenta, proceso AS Proceso FROM rubros ORDER BY nrorubro, nombrerubro;")).ToList();
    }

    public async Task<int> SaveCashRubroAsync(CashRubroEntity rubro, string lcTipo)
    {
        using var conn = _connectionFactory.CreateConnection();
        string nom = Trunc(rubro.NombreRubro, 35).ToUpperInvariant();
        if (string.Equals(lcTipo, "M", StringComparison.OrdinalIgnoreCase) && rubro.IdRubros > 0)
        {
            await conn.ExecuteAsync(
                "UPDATE rubros SET nrorubro=@NroRubro, nombrerubro=@Nom, tipocuenta=@TipoCuenta, proceso=@Proceso WHERE idrubros=@Id;",
                new { Id = rubro.IdRubros, rubro.NroRubro, Nom = nom, rubro.TipoCuenta, rubro.Proceso });
            return rubro.IdRubros;
        }
        rubro.IdRubros = await conn.ExecuteScalarAsync<int>(
            "INSERT INTO rubros (nrorubro, nombrerubro, tipocuenta, proceso) VALUES (@NroRubro, @Nom, @TipoCuenta, @Proceso); SELECT LAST_INSERT_ID();",
            new { rubro.NroRubro, Nom = nom, rubro.TipoCuenta, rubro.Proceso });
        return rubro.IdRubros;
    }

    public async Task<bool> DeleteCashRubroAsync(int idRubros)
    {
        using var conn = _connectionFactory.CreateConnection();
        return await conn.ExecuteAsync("DELETE FROM rubros WHERE idrubros = @Id;", new { Id = idRubros }) > 0;
    }

    public async Task<int> SaveBranchAsync(BranchEntity branch, string lcTipo)
    {
        using var conn = _connectionFactory.CreateConnection();
        if (string.Equals(lcTipo, "M", StringComparison.OrdinalIgnoreCase) && branch.IdSucursal > 0)
        {
            await conn.ExecuteAsync(
                "UPDATE sucursal SET nombre=@Nombre, dir=@Dir, tel=@Tel, cel=@Cel, enc=@Enc, email=@Email WHERE idsucursal=@IdSucursal;",
                branch);
            return branch.IdSucursal;
        }
        branch.IdSucursal = await conn.ExecuteScalarAsync<int>(
            "INSERT INTO sucursal (nombre, dir, tel, cel, enc, email) VALUES (@Nombre, @Dir, @Tel, @Cel, @Enc, @Email); SELECT LAST_INSERT_ID();",
            branch);
        return branch.IdSucursal;
    }

    // =========================================================================
    // AGENDA, ORGANIZER & SMS
    // =========================================================================
    public async Task<List<AgendaItemEntity>> GetAgendaItemsAsync(DateTime? desde, DateTime? hasta, string? filterUser = null)
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT ID AS Id,
                   CASE WHEN FECHA IS NULL OR CAST(FECHA AS CHAR) LIKE '0000-00-00%' THEN NOW() ELSE FECHA END AS Fecha,
                   COALESCE(ASUNTO, '') AS Asunto,
                   COALESCE(NOTAS, '') AS Notas,
                   COALESCE(AVISO, 0) AS Aviso,
                   COALESCE(TIPOREG, 0) AS TipoReg,
                   COALESCE(REGISTRO, '') AS Registro,
                   COALESCE(userid, '') AS UserId
            FROM agenda
            WHERE (@Desde IS NULL OR DATE(FECHA) >= @Desde)
              AND (@Hasta IS NULL OR DATE(FECHA) <= @Hasta)
              AND (@User = '' OR UPPER(TRIM(COALESCE(userid, ''))) = @User OR TRIM(COALESCE(userid, '')) = '')
            ORDER BY FECHA ASC;";
        return (await conn.QueryAsync<AgendaItemEntity>(sql, new
        {
            Desde = desde?.Date,
            Hasta = hasta?.Date,
            User = (filterUser ?? string.Empty).Trim().ToUpperInvariant()
        })).ToList();
    }

    public async Task<string> SaveAgendaItemAsync(AgendaItemEntity item, string lcTipo)
    {
        using var conn = _connectionFactory.CreateConnection();
        string id = string.IsNullOrWhiteSpace(item.Id) ? Guid.NewGuid().ToString("N")[..11].ToUpperInvariant() : Trunc(item.Id, 11);
        string asunto = Trunc(item.Asunto, 100);
        string notas = item.Notas ?? string.Empty;
        string reg = Trunc(item.Registro, 20);
        string usr = Trunc(item.UserId, 25).ToUpperInvariant();

        if (string.Equals(lcTipo, "M", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(item.Id))
        {
            await conn.ExecuteAsync(
                "UPDATE agenda SET FECHA=@Fecha, ASUNTO=@Asunto, NOTAS=@Notas, AVISO=@Aviso, TIPOREG=@TipoReg, REGISTRO=@Reg, userid=@Usr WHERE ID=@Id;",
                new { Id = id, item.Fecha, Asunto = asunto, Notas = notas, item.Aviso, item.TipoReg, Reg = reg, Usr = usr });
            return id;
        }
        await conn.ExecuteAsync(
            "INSERT INTO agenda (ID, FECHA, ASUNTO, NOTAS, AVISO, TIPOREG, REGISTRO, userid) VALUES (@Id, @Fecha, @Asunto, @Notas, @Aviso, @TipoReg, @Reg, @Usr);",
            new { Id = id, item.Fecha, Asunto = asunto, Notas = notas, item.Aviso, item.TipoReg, Reg = reg, Usr = usr });
        item.Id = id;
        return id;
    }

    public async Task<bool> DeleteAgendaItemAsync(string id)
    {
        using var conn = _connectionFactory.CreateConnection();
        return await conn.ExecuteAsync("DELETE FROM agenda WHERE ID = @Id;", new { Id = id }) > 0;
    }

    public async Task<List<OrganizerEntity>> GetOrganizerContactsAsync(string? filter = null)
    {
        using var conn = _connectionFactory.CreateConnection();
        string f = (filter ?? string.Empty).Trim();
        const string sql = @"
            SELECT idagenda AS IdAgenda, nombres AS Nombres, telefonos1 AS Telefonos1, telefonos2 AS Telefonos2, telefonos3 AS Telefonos3
            FROM organizador
            WHERE (@F = '' OR nombres LIKE @Like OR telefonos1 LIKE @Like)
            ORDER BY nombres;";
        return (await conn.QueryAsync<OrganizerEntity>(sql, new { F = f, Like = $"%{f}%" })).ToList();
    }

    public async Task<int> SaveOrganizerContactAsync(OrganizerEntity contact, string lcTipo)
    {
        using var conn = _connectionFactory.CreateConnection();
        if (string.Equals(lcTipo, "M", StringComparison.OrdinalIgnoreCase) && contact.IdAgenda > 0)
        {
            await conn.ExecuteAsync(
                "UPDATE organizador SET nombres=@Nombres, telefonos1=@Telefonos1, telefonos2=@Telefonos2, telefonos3=@Telefonos3 WHERE idagenda=@IdAgenda;",
                contact);
            return contact.IdAgenda;
        }
        contact.IdAgenda = await conn.ExecuteScalarAsync<int>(
            "INSERT INTO organizador (nombres, telefonos1, telefonos2, telefonos3) VALUES (@Nombres, @Telefonos1, @Telefonos2, @Telefonos3); SELECT LAST_INSERT_ID();",
            contact);
        return contact.IdAgenda;
    }

    public async Task<bool> DeleteOrganizerContactAsync(int idAgenda)
    {
        using var conn = _connectionFactory.CreateConnection();
        return await conn.ExecuteAsync("DELETE FROM organizador WHERE idagenda = @Id;", new { Id = idAgenda }) > 0;
    }

    public async Task<List<SmsConnectionEntity>> GetSmsConnectionsAsync()
    {
        using var conn = _connectionFactory.CreateConnection();
        return (await conn.QueryAsync<SmsConnectionEntity>(
            "SELECT idconexion AS IdConexion, descrip AS Descrip, puerto AS Puerto, bautrate AS BautRate, databits AS DataBits, stopbits AS StopBits, parity AS Parity, flow AS Flow, inactivo AS Inactivo FROM sms_conexion ORDER BY idconexion;")).ToList();
    }

    public async Task<int> SaveSmsConnectionAsync(SmsConnectionEntity sms, string lcTipo)
    {
        using var conn = _connectionFactory.CreateConnection();
        if (string.Equals(lcTipo, "M", StringComparison.OrdinalIgnoreCase) && sms.IdConexion > 0)
        {
            await conn.ExecuteAsync(
                "UPDATE sms_conexion SET descrip=@Descrip, puerto=@Puerto, bautrate=@BautRate, databits=@DataBits, stopbits=@StopBits, parity=@Parity, flow=@Flow, inactivo=@Inactivo WHERE idconexion=@IdConexion;",
                sms);
            return sms.IdConexion;
        }
        sms.IdConexion = await conn.ExecuteScalarAsync<int>(
            "INSERT INTO sms_conexion (descrip, puerto, bautrate, databits, stopbits, parity, flow, inactivo) VALUES (@Descrip, @Puerto, @BautRate, @DataBits, @StopBits, @Parity, @Flow, @Inactivo); SELECT LAST_INSERT_ID();",
            sms);
        return sms.IdConexion;
    }

    // =========================================================================
    // COMMERCIAL TEAM
    // =========================================================================
    public async Task<bool> DeleteSellerAsync(int idVendedor)
    {
        using var conn = _connectionFactory.CreateConnection();
        return await conn.ExecuteAsync("UPDATE vendedores SET estado = 0 WHERE idvendedor = @Id;", new { Id = idVendedor }) > 0;
    }

    public async Task<List<SupervisorEntity>> GetAllSupervisorsAsync()
    {
        using var conn = _connectionFactory.CreateConnection();
        return (await conn.QueryAsync<SupervisorEntity>(
            "SELECT idsupervisor AS IdSupervisor, nombres AS Nombres, estado AS Estado FROM supervisores ORDER BY nombres;")).ToList();
    }

    public async Task<int> SaveSupervisorAsync(
        SupervisorEntity sup, string name, string doc, string tel, string cel, string dir, decimal comision, int estado)
    {
        using var conn = _connectionFactory.CreateConnection();
        string nom = Trunc(name, 80).ToUpperInvariant();
        if (sup.IdSupervisor > 0)
        {
            await conn.ExecuteAsync(
                "UPDATE supervisores SET nombres=@Nom, documento=@Doc, telefonos=@Tel, celular=@Cel, direccion=@Dir, comision=@Com, estado=@Est WHERE idsupervisor=@Id;",
                new { Id = sup.IdSupervisor, Nom = nom, Doc = doc ?? "", Tel = tel ?? "", Cel = cel ?? "", Dir = dir ?? "", Com = comision, Est = estado });
            return sup.IdSupervisor;
        }
        sup.IdSupervisor = await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO supervisores (nombres, documento, ruc, telefonos, fechaalta, celular, direccion, comision, estado)
              VALUES (@Nom, @Doc, @Doc, @Tel, CURDATE(), @Cel, @Dir, @Com, @Est); SELECT LAST_INSERT_ID();",
            new { Nom = nom, Doc = doc ?? "", Tel = tel ?? "", Cel = cel ?? "", Dir = dir ?? "", Com = comision, Est = estado });
        return sup.IdSupervisor;
    }

    public async Task<bool> DeleteSupervisorAsync(int idSupervisor)
    {
        using var conn = _connectionFactory.CreateConnection();
        return await conn.ExecuteAsync("UPDATE supervisores SET estado = 0 WHERE idsupervisor = @Id;", new { Id = idSupervisor }) > 0;
    }

    public async Task<List<CollectorEntity>> GetCollectorsAsync()
    {
        using var conn = _connectionFactory.CreateConnection();
        return (await conn.QueryAsync<CollectorEntity>(
            @"SELECT idcobrador AS IdCobrador, nombres AS Nombres, documento AS Documento, ruc AS Ruc,
                     telefonos AS Telefonos, celular AS Celular, direccion AS Direccion, email AS Email,
                     comision AS Comision, descuento AS Descuento, comisionfija AS ComisionFija, estado AS Estado
              FROM cobradores ORDER BY nombres;")).ToList();
    }

    public async Task<int> SaveCollectorAsync(CollectorEntity c, string lcTipo)
    {
        using var conn = _connectionFactory.CreateConnection();
        if (string.Equals(lcTipo, "M", StringComparison.OrdinalIgnoreCase) && c.IdCobrador > 0)
        {
            await conn.ExecuteAsync(
                @"UPDATE cobradores SET nombres=@Nombres, documento=@Documento, ruc=@Ruc, telefonos=@Telefonos,
                  celular=@Celular, direccion=@Direccion, email=@Email, comision=@Comision, descuento=@Descuento,
                  comisionfija=@ComisionFija, estado=@Estado WHERE idcobrador=@IdCobrador;", c);
            return c.IdCobrador;
        }
        c.IdCobrador = await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO cobradores (nombres, documento, ruc, telefonos, fechaalta, celular, direccion, email, comision, descuento, comisionfija, estado, cidusuario, idsucursal, dessucursal)
              VALUES (@Nombres, @Documento, @Ruc, @Telefonos, CURDATE(), @Celular, @Direccion, @Email, @Comision, @Descuento, @ComisionFija, @Estado, 'ADMINISTRA', 1, 'CASA CENTRAL');
              SELECT LAST_INSERT_ID();", c);
        return c.IdCobrador;
    }

    public async Task<bool> DeleteCollectorAsync(int idCobrador)
    {
        using var conn = _connectionFactory.CreateConnection();
        return await conn.ExecuteAsync("UPDATE cobradores SET estado = 0 WHERE idcobrador = @Id;", new { Id = idCobrador }) > 0;
    }

    public async Task<List<SalesGoalEntity>> GetSalesGoalsAsync()
    {
        using var conn = _connectionFactory.CreateConnection();
        return (await conn.QueryAsync<SalesGoalEntity>(
            "SELECT idmeta AS IdMeta, desde AS Desde, hasta AS Hasta, CASE WHEN fecha IS NULL OR CAST(fecha AS CHAR)='0000-00-00' THEN CURDATE() ELSE fecha END AS Fecha FROM metasventas ORDER BY idmeta DESC;")).ToList();
    }

    public async Task<int> SaveSalesGoalAsync(SalesGoalEntity g, string lcTipo)
    {
        using var conn = _connectionFactory.CreateConnection();
        if (string.Equals(lcTipo, "M", StringComparison.OrdinalIgnoreCase) && g.IdMeta > 0)
        {
            await conn.ExecuteAsync("UPDATE metasventas SET desde=@Desde, hasta=@Hasta, fecha=@Fecha WHERE idmeta=@IdMeta;", g);
            return g.IdMeta;
        }
        g.IdMeta = await conn.ExecuteScalarAsync<int>(
            "INSERT INTO metasventas (desde, hasta, fecha) VALUES (@Desde, @Hasta, @Fecha); SELECT LAST_INSERT_ID();", g);
        return g.IdMeta;
    }

    public async Task<bool> DeleteSalesGoalAsync(int idMeta)
    {
        using var conn = _connectionFactory.CreateConnection();
        return await conn.ExecuteAsync("DELETE FROM metasventas WHERE idmeta = @Id;", new { Id = idMeta }) > 0;
    }

    public async Task<List<SalesGoalTableEntity>> GetSalesGoalTableAsync()
    {
        using var conn = _connectionFactory.CreateConnection();
        return (await conn.QueryAsync<SalesGoalTableEntity>(
            "SELECT idtablameta AS IdTablaMeta, desde AS Desde, operacion AS Operacion, hasta AS Hasta, porcentaje AS Porcentaje, monto AS Monto, orden AS Orden, calculacomision AS CalculaComision FROM tablasmetas ORDER BY orden, desde;")).ToList();
    }

    public async Task<int> SaveSalesGoalRowAsync(SalesGoalTableEntity r, string lcTipo)
    {
        using var conn = _connectionFactory.CreateConnection();
        if (string.Equals(lcTipo, "M", StringComparison.OrdinalIgnoreCase) && r.IdTablaMeta > 0)
        {
            await conn.ExecuteAsync(
                "UPDATE tablasmetas SET desde=@Desde, operacion=@Operacion, hasta=@Hasta, porcentaje=@Porcentaje, monto=@Monto, orden=@Orden, calculacomision=@CalculaComision WHERE idtablameta=@IdTablaMeta;", r);
            return r.IdTablaMeta;
        }
        r.IdTablaMeta = await conn.ExecuteScalarAsync<int>(
            "INSERT INTO tablasmetas (desde, operacion, hasta, porcentaje, monto, orden, calculacomision) VALUES (@Desde, @Operacion, @Hasta, @Porcentaje, @Monto, @Orden, @CalculaComision); SELECT LAST_INSERT_ID();", r);
        return r.IdTablaMeta;
    }

    public async Task<bool> DeleteSalesGoalRowAsync(int idTablaMeta)
    {
        using var conn = _connectionFactory.CreateConnection();
        return await conn.ExecuteAsync("DELETE FROM tablasmetas WHERE idtablameta = @Id;", new { Id = idTablaMeta }) > 0;
    }

    public async Task<List<SupplierCommissionEntity>> GetSupplierCommissionsAsync()
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT c.idcomision AS IdComision,
                   c.idproveedor AS IdProveedor,
                   COALESCE(p.descripcion, '') AS DesProveedor,
                   c.idvendedor AS IdVendedor,
                   COALESCE(v.nombres, '') AS DesVendedor,
                   c.comision AS Comision
            FROM comision_por_proveedor c
            LEFT JOIN proveedores p ON c.idproveedor = p.idproveedor
            LEFT JOIN vendedores v ON c.idvendedor = v.idvendedor
            ORDER BY p.descripcion, v.nombres;";
        return (await conn.QueryAsync<SupplierCommissionEntity>(sql)).ToList();
    }

    public async Task<int> SaveSupplierCommissionAsync(SupplierCommissionEntity comm, string lcTipo)
    {
        using var conn = _connectionFactory.CreateConnection();
        if (string.Equals(lcTipo, "M", StringComparison.OrdinalIgnoreCase) && comm.IdComision > 0)
        {
            await conn.ExecuteAsync("UPDATE comision_por_proveedor SET idproveedor=@IdProveedor, comision=@Comision, idvendedor=@IdVendedor WHERE idcomision=@IdComision;", comm);
            return comm.IdComision;
        }
        comm.IdComision = await conn.ExecuteScalarAsync<int>(
            "INSERT INTO comision_por_proveedor (idproveedor, comision, idvendedor) VALUES (@IdProveedor, @Comision, @IdVendedor); SELECT LAST_INSERT_ID();", comm);
        return comm.IdComision;
    }

    public async Task<bool> DeleteSupplierCommissionAsync(int idComision)
    {
        using var conn = _connectionFactory.CreateConnection();
        return await conn.ExecuteAsync("DELETE FROM comision_por_proveedor WHERE idcomision = @Id;", new { Id = idComision }) > 0;
    }

    public async Task<List<ReceiptAdvanceEntity>> GetReceiptsAdvancesAsync(DateTime desde, DateTime hasta)
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT r.idrecibos AS IdRecibos, COALESCE(r.letra,'A') AS Letra, COALESCE(r.recibo,'') AS Recibo,
                   CASE WHEN r.fecha IS NULL OR CAST(r.fecha AS CHAR)='0000-00-00' THEN CURDATE() ELSE r.fecha END AS Fecha,
                   r.idcliente AS IdCliente, r.idproveedor AS IdProveedor, r.idvendedor AS IdVendedor, r.idsupervisor AS IdSupervisor,
                   COALESCE(v.nombres, s.nombres, c.nombres, p.descripcion, '') AS Destinatario,
                   r.importe AS Importe, COALESCE(r.concepto,'') AS Concepto, r.idcaja AS IdCaja, r.estado AS Estado
            FROM recibos r
            LEFT JOIN vendedores v ON r.idvendedor = v.idvendedor
            LEFT JOIN supervisores s ON r.idsupervisor = s.idsupervisor
            LEFT JOIN clientes c ON r.idcliente = c.idcliente
            LEFT JOIN proveedores p ON r.idproveedor = p.idproveedor
            WHERE r.fecha >= @Desde AND r.fecha <= @Hasta
            ORDER BY r.idrecibos DESC;";
        return (await conn.QueryAsync<ReceiptAdvanceEntity>(sql, new { Desde = desde.Date, Hasta = hasta.Date })).ToList();
    }

    public async Task<int> SaveReceiptAdvanceAsync(ReceiptAdvanceEntity rec)
    {
        using var conn = _connectionFactory.CreateConnection();
        rec.Concepto ??= string.Empty;
        using var tx = conn.BeginTransaction();
        try
        {
            const string sql = @"
                INSERT INTO recibos (letra, recibo, fecha, idcliente, importe, concepto, montoenletras, idcaja, estado, idproveedor, idvendedor, idsupervisor)
                VALUES (@Letra, @Recibo, @Fecha, @IdCliente, @Importe, @Concepto, '', @IdCaja, @Estado, @IdProveedor, @IdVendedor, @IdSupervisor);
                SELECT LAST_INSERT_ID();";
            rec.IdRecibos = await conn.ExecuteScalarAsync<int>(sql, rec, tx);

            if (rec.Importe > 0m)
            {
                await conn.ExecuteAsync(
                    @"INSERT INTO movimientocaja (nrorubro, ingresos, egresos, tipo, detalle, fechaalta, hora, estado, idventas, idcompras, idcaja, idmoneda, cidusuario, obs)
                      VALUES (1, 0, @Importe, 'SALIDA', @Detalle, @Fecha, @Hora, 1, 0, 0, @IdCaja, 1, 'ADMINISTRA', @Detalle);",
                    new
                    {
                        rec.Importe,
                        Detalle = Trunc($"ADELANTO/RECIBO {rec.Letra}-{rec.Recibo}: {rec.Concepto}", 150),
                        Fecha = rec.Fecha.Date,
                        Hora = DateTime.Now.ToString("HH:mm:ss"),
                        IdCaja = rec.IdCaja > 0 ? rec.IdCaja : 1
                    }, tx);
            }

            tx.Commit();
            return rec.IdRecibos;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    // =========================================================================
    // COMMERCIAL OPERATIONS (PRESUPUESTOS, PEDIDOS, DEVOLUCIONES, DEUDAS, CRÉDITO)
    // =========================================================================
    public async Task<int> SaveBudgetFullAsync(BudgetSummaryEntity header, List<BudgetItemEntity> items, string usuario = "ADMINISTRA")
    {
        using var conn = _connectionFactory.CreateConnection();
        using var tx = conn.BeginTransaction();
        try
        {
            decimal ex = items.Sum(i => i.PreExenta);
            decimal iv5 = items.Sum(i => i.PreIva5);
            decimal iv10 = items.Sum(i => i.PreIva10);
            decimal sub = ex + iv5 + iv10;
            decimal tot = Math.Max(0m, sub - header.Descuento);

            if (header.NroPresupuesto <= 0)
            {
                header.NroPresupuesto = await conn.ExecuteScalarAsync<int>(
                    "SELECT COALESCE(MAX(nro_presupuesto), 0) + 1 FROM presupuestos;", transaction: tx);
            }
            header.TotalPresupuesto = tot;

            const string insHdr = @"
                INSERT INTO presupuestos
                (nro_presupuesto, nro_factura, fecha_presupuesto, forma_pago, iddeposito, idcaja, idcliente, idvendedor, timbrado,
                 total_exenta, total_iva5, total_iva10, subtotal, descuento, total_presupuesto, liquidacion_iva5, liquidacion_iva10, total_impuesto,
                 cidusuario, concepto, estado, observacion, idmoneda)
                VALUES
                (@Nro, @Fact, @Fecha, 1, 1, 1, @IdCliente, @IdVendedor, '',
                 @Ex, @Iv5, @Iv10, @Sub, @Desc, @Tot, ROUND(@Iv5/21,0), ROUND(@Iv10/11,0), ROUND(@Iv5/21,0)+ROUND(@Iv10/11,0),
                 @User, 'PRESUPUESTO', 1, @Obs, 1);
                SELECT LAST_INSERT_ID();";
            header.IdPresupuesto = await conn.ExecuteScalarAsync<int>(insHdr, new
            {
                Nro = header.NroPresupuesto,
                Fact = header.NroFactura ?? string.Empty,
                Fecha = header.FechaPresupuesto.Date,
                header.IdCliente,
                header.IdVendedor,
                Ex = ex, Iv5 = iv5, Iv10 = iv10, Sub = sub, Desc = header.Descuento, Tot = tot,
                User = Trunc(usuario, 20),
                Obs = header.Observacion ?? string.Empty
            }, tx);

            const string insItm = @"
                INSERT INTO itemspresupuestos
                (idpresupuesto, nro_factura, nro_presupuesto, idproducto, codigo, cantidad, pre_unitario, pre_exenta, pre_iva5, pre_iva10,
                 controlagarantia, fechainiciogarantia, diasgarantias, fechafingarantia)
                VALUES
                (@IdPres, @Fact, @Nro, @IdProd, @Cod, @Cant, @Pre, @Ex, @Iv5, @Iv10, 0, '0000-00-00', 0, '0000-00-00');";

            foreach (var it in items)
            {
                await conn.ExecuteAsync(insItm, new
                {
                    IdPres = header.IdPresupuesto,
                    Fact = header.NroFactura ?? string.Empty,
                    Nro = header.NroPresupuesto,
                    IdProd = it.IdProducto,
                    Cod = it.Codigo ?? string.Empty,
                    Cant = it.Cantidad,
                    Pre = it.PreUnitario,
                    Ex = it.PreExenta,
                    Iv5 = it.PreIva5,
                    Iv10 = it.PreIva10
                }, tx);
            }

            tx.Commit();
            return header.IdPresupuesto;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task<bool> DeleteBudgetAsync(int idPresupuesto)
    {
        using var conn = _connectionFactory.CreateConnection();
        return await conn.ExecuteAsync("UPDATE presupuestos SET estado = 0 WHERE idpresupuesto = @Id;", new { Id = idPresupuesto }) > 0;
    }

    public async Task<List<PurchaseOrderEntity>> GetPurchaseOrdersAsync(DateTime desde, DateTime hasta)
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT p.idpedido AS IdPedido, p.nro_pedido AS NroPedido, COALESCE(p.nro_factura,'') AS NroFactura,
                   CASE WHEN p.fecha_pedido IS NULL OR CAST(p.fecha_pedido AS CHAR)='0000-00-00' THEN CURDATE() ELSE p.fecha_pedido END AS FechaPedido,
                   p.iddeposito AS IdDeposito, p.idproveedor AS IdProveedor, COALESCE(pr.descripcion,'') AS DesProveedor,
                   p.total_pedido AS TotalPedido, COALESCE(p.observacion,'') AS Observacion, p.estado AS Estado, p.facturado AS Facturado
            FROM pedidos p
            LEFT JOIN proveedores pr ON p.idproveedor = pr.idproveedor
            WHERE p.fecha_pedido >= @Desde AND p.fecha_pedido <= @Hasta
            ORDER BY p.idpedido DESC;";
        return (await conn.QueryAsync<PurchaseOrderEntity>(sql, new { Desde = desde.Date, Hasta = hasta.Date })).ToList();
    }

    public async Task<int> SavePurchaseOrderTransactionAsync(PurchaseOrderEntity order, string usuario = "ADMINISTRA")
    {
        using var conn = _connectionFactory.CreateConnection();
        using var tx = conn.BeginTransaction();
        try
        {
            if (order.NroPedido <= 0)
                order.NroPedido = await conn.ExecuteScalarAsync<int>("SELECT COALESCE(MAX(nro_pedido), 0) + 1 FROM pedidos;", transaction: tx);

            decimal ex = order.Items.Sum(i => i.PreExenta);
            decimal iv5 = order.Items.Sum(i => i.PreIva5);
            decimal iv10 = order.Items.Sum(i => i.PreIva10);
            decimal sub = ex + iv5 + iv10;
            order.TotalPedido = sub;

            const string insHdr = @"
                INSERT INTO pedidos
                (nro_pedido, nro_factura, fecha_pedido, forma_pago, iddeposito, idcaja, idproveedor, timbrado,
                 total_exenta, total_iva5, total_iva10, subtotal, descuento, total_pedido, liquidacion_iva5, liquidacion_iva10, total_impuesto,
                 cidusuario, concepto, estado, observacion, facturado, idmoneda)
                VALUES
                (@Nro, @Fact, @Fecha, 1, @Dep, 1, @Prov, '',
                 @Ex, @Iv5, @Iv10, @Sub, 0, @Tot, ROUND(@Iv5/21,0), ROUND(@Iv10/11,0), ROUND(@Iv5/21,0)+ROUND(@Iv10/11,0),
                 @User, 'PEDIDO DE COMPRA', 1, @Obs, @Facturado, 1);
                SELECT LAST_INSERT_ID();";
            order.IdPedido = await conn.ExecuteScalarAsync<int>(insHdr, new
            {
                Nro = order.NroPedido,
                Fact = order.NroFactura ?? string.Empty,
                Fecha = order.FechaPedido.Date,
                Dep = order.IdDeposito > 0 ? order.IdDeposito : 1,
                Prov = order.IdProveedor,
                Ex = ex, Iv5 = iv5, Iv10 = iv10, Sub = sub, Tot = order.TotalPedido,
                User = Trunc(usuario, 20),
                Obs = order.Observacion ?? string.Empty,
                order.Facturado
            }, tx);

            const string insItm = @"
                INSERT INTO itemspedidos (idpedido, nro_factura, nro_pedido, idproducto, codigo, cantidad, pre_unitario, pre_exenta, pre_iva5, pre_iva10)
                VALUES (@IdPed, @Fact, @Nro, @IdProd, @Cod, @Cant, @Pre, @Ex, @Iv5, @Iv10);";
            foreach (var it in order.Items)
            {
                await conn.ExecuteAsync(insItm, new
                {
                    IdPed = order.IdPedido, Fact = order.NroFactura ?? string.Empty, Nro = order.NroPedido,
                    IdProd = it.IdProducto, Cod = it.Codigo ?? string.Empty, Cant = it.Cantidad,
                    Pre = it.PreUnitario, Ex = it.PreExenta, Iv5 = it.PreIva5, Iv10 = it.PreIva10
                }, tx);
            }

            tx.Commit();
            return order.IdPedido;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task<int> SaveSalesReturnTransactionAsync(ReturnHeaderEntity ret)
    {
        using var conn = _connectionFactory.CreateConnection();
        using var tx = conn.BeginTransaction();
        try
        {
            decimal total = ret.Items.Sum(i => i.Subtotal);
            ret.Total = total;
            const string insHdr = @"
                INSERT INTO devolucionventas
                (idventa, nro_boleta, idcliente, descliente, ruc, direccion, fecha_devolucion, total_exenta, total_iva5, total_iva10,
                 subtotal, impuesto, total_venta, idsucursal, dessucursal, cidusuario, forma_pago2, iddeposito, descuento, concepto)
                VALUES
                (@IdOrig, @Boleta, @IdCli, @DesCli, @Ruc, '', @Fecha, 0, 0, @Total,
                 @Total, ROUND(@Total/11,0), @Total, 1, 'CASA CENTRAL', @User, 'CONTADO', @Dep, 0, @Concepto);
                SELECT LAST_INSERT_ID();";
            ret.IdDevolucion = await conn.ExecuteScalarAsync<int>(insHdr, new
            {
                IdOrig = ret.IdDocumentoOrigen, Boleta = ret.NroBoleta ?? string.Empty,
                IdCli = ret.IdTercero, DesCli = ret.DesTercero ?? string.Empty, Ruc = ret.Ruc ?? string.Empty,
                Fecha = ret.FechaDevolucion.Date, Total = total, User = Trunc(ret.CidUsuario, 20),
                Dep = ret.IdDeposito > 0 ? ret.IdDeposito : 1, Concepto = ret.Concepto ?? "DEVOLUCIÓN VENTA"
            }, tx);

            foreach (var it in ret.Items)
            {
                decimal sub = it.Subtotal;
                await conn.ExecuteAsync(
                    @"INSERT INTO itemsdevolucionventas (iddevolucionventa, nro_factura, nro_venta, idproducto, codigo, cantidad, pre_unitario, pre_exenta, pre_iva5, pre_iva10, controlagarantia, fechainiciogarantia, diasgarantias, fechafingarantia)
                      VALUES (@IdDev, @Bol, @IdOrig, @IdProd, @Cod, @Cant, @Pre, @Ex, @I5, @I10, 0, '0000-00-00', 0, '0000-00-00');",
                    new
                    {
                        IdDev = ret.IdDevolucion, Bol = ret.NroBoleta ?? string.Empty, IdOrig = ret.IdDocumentoOrigen,
                        IdProd = it.IdProducto, Cod = it.Codigo ?? string.Empty, Cant = it.Cantidad, Pre = it.PreUnitario,
                        Ex = it.Iva == 3 ? sub : 0m, I5 = it.Iva == 2 ? sub : 0m, I10 = it.Iva == 1 ? sub : 0m
                    }, tx);

                // Devolución de venta reingresa mercadería al depósito
                await conn.ExecuteAsync(
                    "UPDATE itemsdepositos SET cantidad = cantidad + @Cant WHERE idproducto = @IdProd AND iddeposito = @Dep;",
                    new { Cant = it.Cantidad, IdProd = it.IdProducto, Dep = ret.IdDeposito > 0 ? ret.IdDeposito : 1 }, tx);
            }

            tx.Commit();
            return ret.IdDevolucion;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task<List<ReturnHeaderEntity>> GetSalesReturnsAsync(DateTime desde, DateTime hasta)
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT iddevolucionventa AS IdDevolucion, idventa AS IdDocumentoOrigen, COALESCE(nro_boleta,'') AS NroBoleta,
                   idcliente AS IdTercero, COALESCE(descliente,'') AS DesTercero, COALESCE(ruc,'') AS Ruc,
                   fecha_devolucion AS FechaDevolucion, iddeposito AS IdDeposito, total_venta AS Total,
                   COALESCE(concepto,'') AS Concepto, COALESCE(cidusuario,'') AS CidUsuario
            FROM devolucionventas
            WHERE fecha_devolucion >= @Desde AND fecha_devolucion <= @Hasta
            ORDER BY iddevolucionventa DESC;";
        return (await conn.QueryAsync<ReturnHeaderEntity>(sql, new { Desde = desde.Date, Hasta = hasta.Date })).ToList();
    }

    public async Task<int> SavePurchaseReturnTransactionAsync(ReturnHeaderEntity ret)
    {
        using var conn = _connectionFactory.CreateConnection();
        using var tx = conn.BeginTransaction();
        try
        {
            decimal total = ret.Items.Sum(i => i.Subtotal);
            ret.Total = total;
            const string insHdr = @"
                INSERT INTO devolucioncompras
                (idcompra, nro_boleta, idproveedor, desproveedor, ruc, direccion, fecha_devolucion, total_exenta, total_iva5, total_iva10,
                 subtotal, impuesto, total_compra, idsucursal, dessucursal, cidusuario, forma_pago, iddeposito, descuento, concepto)
                VALUES
                (@IdOrig, @Boleta, @IdProv, @DesProv, @Ruc, '', @Fecha, 0, 0, @Total,
                 @Total, ROUND(@Total/11,0), @Total, 1, 'CASA CENTRAL', @User, 'CONTADO', @Dep, 0, @Concepto);
                SELECT LAST_INSERT_ID();";
            ret.IdDevolucion = await conn.ExecuteScalarAsync<int>(insHdr, new
            {
                IdOrig = ret.IdDocumentoOrigen, Boleta = ret.NroBoleta ?? string.Empty,
                IdProv = ret.IdTercero, DesProv = ret.DesTercero ?? string.Empty, Ruc = ret.Ruc ?? string.Empty,
                Fecha = ret.FechaDevolucion.Date, Total = total, User = Trunc(ret.CidUsuario, 20),
                Dep = ret.IdDeposito > 0 ? ret.IdDeposito : 1, Concepto = ret.Concepto ?? "DEVOLUCIÓN COMPRA"
            }, tx);

            foreach (var it in ret.Items)
            {
                decimal sub = it.Subtotal;
                await conn.ExecuteAsync(
                    @"INSERT INTO itemsdevolucioncompras (iddevolucioncompra, nro_factura, nro_compra, idproducto, codigo, cantidad, pre_unitario, pre_exenta, pre_iva5, pre_iva10, controlagarantia, fechainiciogarantia, diasgarantias, fechafingarantia)
                      VALUES (@IdDev, @Bol, @IdOrig, @IdProd, @Cod, @Cant, @Pre, @Ex, @I5, @I10, 0, '0000-00-00', 0, '0000-00-00');",
                    new
                    {
                        IdDev = ret.IdDevolucion, Bol = ret.NroBoleta ?? string.Empty, IdOrig = ret.IdDocumentoOrigen,
                        IdProd = it.IdProducto, Cod = it.Codigo ?? string.Empty, Cant = it.Cantidad, Pre = it.PreUnitario,
                        Ex = it.Iva == 3 ? sub : 0m, I5 = it.Iva == 2 ? sub : 0m, I10 = it.Iva == 1 ? sub : 0m
                    }, tx);

                // Devolución a proveedor descuenta mercadería del depósito
                await conn.ExecuteAsync(
                    "UPDATE itemsdepositos SET cantidad = cantidad - @Cant WHERE idproducto = @IdProd AND iddeposito = @Dep;",
                    new { Cant = it.Cantidad, IdProd = it.IdProducto, Dep = ret.IdDeposito > 0 ? ret.IdDeposito : 1 }, tx);
            }

            tx.Commit();
            return ret.IdDevolucion;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task<List<ReturnHeaderEntity>> GetPurchaseReturnsAsync(DateTime desde, DateTime hasta)
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT iddevolucioncompra AS IdDevolucion, idcompra AS IdDocumentoOrigen, COALESCE(nro_boleta,'') AS NroBoleta,
                   idproveedor AS IdTercero, COALESCE(desproveedor,'') AS DesTercero, COALESCE(ruc,'') AS Ruc,
                   fecha_devolucion AS FechaDevolucion, iddeposito AS IdDeposito, total_compra AS Total,
                   COALESCE(concepto,'') AS Concepto, COALESCE(cidusuario,'') AS CidUsuario
            FROM devolucioncompras
            WHERE fecha_devolucion >= @Desde AND fecha_devolucion <= @Hasta
            ORDER BY iddevolucioncompra DESC;";
        return (await conn.QueryAsync<ReturnHeaderEntity>(sql, new { Desde = desde.Date, Hasta = hasta.Date })).ToList();
    }

    public async Task<List<SupplierDebtEntity>> GetSupplierDebtsAsync(int? idProveedor = null)
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT d.iddeuda AS IdDeuda, d.idcompra AS IdCompra, d.nro_compra AS NroCompra, COALESCE(d.nro_factura,'') AS NroFactura,
                   d.idproveedor AS IdProveedor, COALESCE(p.descripcion,'') AS DesProveedor, d.total AS Total, d.entrega AS Entrega,
                   d.saldo AS Saldo, CASE WHEN d.vencimiento IS NULL OR CAST(d.vencimiento AS CHAR)='0000-00-00' THEN CURDATE() ELSE d.vencimiento END AS Vencimiento,
                   COALESCE(d.cuota,'1/1') AS Cuota, CASE WHEN d.fecha IS NULL OR CAST(d.fecha AS CHAR)='0000-00-00' THEN CURDATE() ELSE d.fecha END AS Fecha
            FROM deudaproveedor d
            LEFT JOIN proveedores p ON d.idproveedor = p.idproveedor
            WHERE d.saldo > 0 AND (@Prov IS NULL OR d.idproveedor = @Prov)
            ORDER BY Vencimiento ASC;";
        return (await conn.QueryAsync<SupplierDebtEntity>(sql, new { Prov = idProveedor is > 0 ? idProveedor : null })).ToList();
    }

    public async Task<List<SupplierPaymentEntity>> GetSupplierPaymentsAsync(DateTime desde, DateTime hasta)
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT p.idpago AS IdPago, p.idcompra AS IdCompra, p.nro_compra AS NroCompra, COALESCE(p.nro_factura,'') AS NroFactura,
                   p.idproveedor AS IdProveedor, COALESCE(pr.descripcion,'') AS DesProveedor, p.pago AS Pago, p.entrega AS Entrega,
                   CASE WHEN p.fecha_pago IS NULL OR CAST(p.fecha_pago AS CHAR)='0000-00-00' THEN CURDATE() ELSE p.fecha_pago END AS FechaPago,
                   COALESCE(p.obs,'') AS Obs
            FROM pagoproveedor p
            LEFT JOIN proveedores pr ON p.idproveedor = pr.idproveedor
            WHERE p.fecha_pago >= @Desde AND p.fecha_pago <= @Hasta
            ORDER BY p.idpago DESC;";
        return (await conn.QueryAsync<SupplierPaymentEntity>(sql, new { Desde = desde.Date, Hasta = hasta.Date })).ToList();
    }

    public async Task RegisterSupplierPaymentAsync(int idDeuda, decimal monto, int idCaja, string obs, string usuario)
    {
        using var conn = _connectionFactory.CreateConnection();
        using var tx = conn.BeginTransaction();
        try
        {
            var debt = await conn.QueryFirstOrDefaultAsync<SupplierDebtEntity>(
                "SELECT iddeuda AS IdDeuda, idcompra AS IdCompra, nro_compra AS NroCompra, nro_factura AS NroFactura, idproveedor AS IdProveedor, saldo AS Saldo, cuota AS Cuota FROM deudaproveedor WHERE iddeuda = @Id;",
                new { Id = idDeuda }, tx)
                ?? throw new InvalidOperationException("No se encontró la deuda seleccionada.");

            decimal applied = Math.Min(monto, debt.Saldo);
            await conn.ExecuteAsync(
                "UPDATE deudaproveedor SET entrega = entrega + @M, saldo = GREATEST(0, saldo - @M) WHERE iddeuda = @Id;",
                new { M = applied, Id = idDeuda }, tx);

            await conn.ExecuteAsync(
                @"INSERT INTO pagoproveedor (idcompra, nro_compra, nro_factura, vencimiento, cuota, fecha, dia, idproveedor, pago, entrega, fecha_pago, obs)
                  VALUES (@IdCompra, @NroCompra, @NroFact, CURDATE(), @Cuota, CURDATE(), DAY(CURDATE()), @IdProv, @Pago, @Entrega, CURDATE(), @Obs);",
                new { debt.IdCompra, debt.NroCompra, NroFact = debt.NroFactura ?? "", debt.Cuota, IdProv = debt.IdProveedor, Pago = applied, Entrega = applied, Obs = obs ?? "PAGO PROVEEDOR" }, tx);

            await conn.ExecuteAsync(
                @"INSERT INTO movimientocaja (nrorubro, ingresos, egresos, tipo, detalle, fechaalta, hora, estado, idventas, idcompras, idcaja, idmoneda, cidusuario, obs)
                  VALUES (1, 0, @M, 'SALIDA', @Detalle, CURDATE(), @Hora, 1, 0, @IdCompra, @IdCaja, 1, @User, '');",
                new { M = applied, Detalle = Trunc($"PAGO PROVEEDOR COMPRA #{debt.NroCompra} FACT {debt.NroFactura}", 150), Hora = DateTime.Now.ToString("HH:mm:ss"), debt.IdCompra, IdCaja = idCaja > 0 ? idCaja : 1, User = Trunc(usuario, 20) }, tx);

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task<List<PayableAccountEntity>> GetPayableAccountsAsync()
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT c.idcuentapagar AS IdCuentaPagar,
                   CASE WHEN c.fecha_carga IS NULL OR CAST(c.fecha_carga AS CHAR)='0000-00-00' THEN CURDATE() ELSE c.fecha_carga END AS FechaCarga,
                   c.nro_pagare AS NroPagare, COALESCE(c.nro_factura,'') AS NroFactura, c.idproveedor AS IdProveedor,
                   COALESCE(p.descripcion,'') AS DesProveedor, c.total AS Total, c.entrega AS Entrega, c.saldo AS Saldo,
                   CASE WHEN c.vencimiento IS NULL OR CAST(c.vencimiento AS CHAR)='0000-00-00' THEN CURDATE() ELSE c.vencimiento END AS Vencimiento,
                   c.cuota AS Cuota
            FROM cuentapagar c
            LEFT JOIN proveedores p ON c.idproveedor = p.idproveedor
            ORDER BY Vencimiento ASC;";
        return (await conn.QueryAsync<PayableAccountEntity>(sql)).ToList();
    }

    public async Task<int> SavePayableAccountAsync(PayableAccountEntity acc, string lcTipo)
    {
        using var conn = _connectionFactory.CreateConnection();
        if (string.Equals(lcTipo, "M", StringComparison.OrdinalIgnoreCase) && acc.IdCuentaPagar > 0)
        {
            await conn.ExecuteAsync(
                "UPDATE cuentapagar SET nro_pagare=@NroPagare, nro_factura=@NroFactura, idproveedor=@IdProveedor, total=@Total, entrega=@Entrega, saldo=@Saldo, vencimiento=@Vencimiento, cuota=@Cuota WHERE idcuentapagar=@IdCuentaPagar;",
                acc);
            return acc.IdCuentaPagar;
        }
        acc.IdCuentaPagar = await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO cuentapagar (fecha_carga, fecha_proceso, nro_pagare, nro_factura, idproveedor, total, entrega, saldo, vencimiento, cuota, idsucursal, dessucursal)
              VALUES (@FechaCarga, CURDATE(), @NroPagare, @NroFactura, @IdProveedor, @Total, @Entrega, @Saldo, @Vencimiento, @Cuota, 1, 'CASA CENTRAL'); SELECT LAST_INSERT_ID();",
            acc);
        return acc.IdCuentaPagar;
    }

    public async Task<List<ReceivableAccountEntity>> GetReceivableAccountsAsync()
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT c.idcuentarecibir AS IdCuentaRecibir,
                   CASE WHEN c.fecha_carga IS NULL OR CAST(c.fecha_carga AS CHAR)='0000-00-00' THEN CURDATE() ELSE c.fecha_carga END AS FechaCarga,
                   c.nro_pagare AS NroPagare, COALESCE(c.nro_factura,'') AS NroFactura, c.idcliente AS IdCliente,
                   COALESCE(cl.nombres,'') AS DesCliente, c.total AS Total, c.entrega AS Entrega, c.saldo AS Saldo,
                   CASE WHEN c.vencimiento IS NULL OR CAST(c.vencimiento AS CHAR)='0000-00-00' THEN CURDATE() ELSE c.vencimiento END AS Vencimiento,
                   c.cuota AS Cuota, c.honorariojudicial AS HonorarioJudicial
            FROM cuentarecibir c
            LEFT JOIN clientes cl ON c.idcliente = cl.idcliente
            ORDER BY Vencimiento ASC;";
        return (await conn.QueryAsync<ReceivableAccountEntity>(sql)).ToList();
    }

    public async Task<int> SaveReceivableAccountAsync(ReceivableAccountEntity acc, string lcTipo)
    {
        using var conn = _connectionFactory.CreateConnection();
        if (string.Equals(lcTipo, "M", StringComparison.OrdinalIgnoreCase) && acc.IdCuentaRecibir > 0)
        {
            await conn.ExecuteAsync(
                "UPDATE cuentarecibir SET nro_pagare=@NroPagare, nro_factura=@NroFactura, idcliente=@IdCliente, total=@Total, entrega=@Entrega, saldo=@Saldo, vencimiento=@Vencimiento, cuota=@Cuota, honorariojudicial=@HonorarioJudicial WHERE idcuentarecibir=@IdCuentaRecibir;",
                acc);
            return acc.IdCuentaRecibir;
        }
        acc.IdCuentaRecibir = await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO cuentarecibir (fecha_carga, fecha_proceso, nro_pagare, nro_factura, idcliente, total, entrega, saldo, vencimiento, cuota, honorariojudicial, idsucursal, dessucursal)
              VALUES (@FechaCarga, CURDATE(), @NroPagare, @NroFactura, @IdCliente, @Total, @Entrega, @Saldo, @Vencimiento, @Cuota, @HonorarioJudicial, 1, 'CASA CENTRAL'); SELECT LAST_INSERT_ID();",
            acc);
        return acc.IdCuentaRecibir;
    }

    public async Task<List<CreditInstallmentRow>> GetCreditInstallmentsAsync(bool onlyOverdue = false, string? search = null)
    {
        using var conn = _connectionFactory.CreateConnection();
        string term = (search ?? string.Empty).Trim();
        const string sql = @"
            SELECT cr.idcredito AS IdCredito, cr.idventa AS IdVenta, cr.nro_venta AS NroVenta, COALESCE(cr.nro_factura,'') AS NroFactura,
                   cr.idcliente AS IdCliente, COALESCE(c.nombres,'OCASIONAL') AS DesCliente, COALESCE(c.celular, c.telefono, '') AS Telefono,
                   COALESCE(cr.cuota,'1/1') AS Cuota,
                   CASE WHEN cr.vencimiento IS NULL OR CAST(cr.vencimiento AS CHAR)='0000-00-00' THEN CURDATE() ELSE cr.vencimiento END AS Vencimiento,
                   COALESCE(GREATEST(0, DATEDIFF(CURDATE(), CASE WHEN cr.vencimiento IS NULL OR CAST(cr.vencimiento AS CHAR)='0000-00-00' THEN CURDATE() ELSE cr.vencimiento END)), 0) AS DiasMora,
                   cr.total AS Total, cr.entrega AS Entrega, cr.saldo AS Saldo, 0 AS InteresMora
            FROM creditocliente cr
            LEFT JOIN clientes c ON cr.idcliente = c.idcliente
            WHERE cr.saldo > 0
              AND (@Overdue = 0 OR cr.vencimiento < CURDATE())
              AND (@Term = '' OR c.nombres LIKE @Like OR cr.nro_factura LIKE @Like)
            ORDER BY Vencimiento ASC;";
        return (await conn.QueryAsync<CreditInstallmentRow>(sql, new { Overdue = onlyOverdue ? 1 : 0, Term = term, Like = $"%{term}%" })).ToList();
    }

    public async Task RegisterCreditInstallmentPaymentAsync(int idCredito, decimal entrega, int idCaja, string usuario)
    {
        using var conn = _connectionFactory.CreateConnection();
        using var tx = conn.BeginTransaction();
        try
        {
            var cr = await conn.QueryFirstOrDefaultAsync<CreditInstallmentRow>(
                "SELECT idcredito AS IdCredito, idventa AS IdVenta, nro_venta AS NroVenta, nro_factura AS NroFactura, idcliente AS IdCliente, cuota AS Cuota, saldo AS Saldo, COALESCE(GREATEST(0, DATEDIFF(CURDATE(), CASE WHEN vencimiento IS NULL OR CAST(vencimiento AS CHAR)='0000-00-00' THEN CURDATE() ELSE vencimiento END)), 0) AS DiasMora FROM creditocliente WHERE idcredito = @Id;",
                new { Id = idCredito }, tx)
                ?? throw new InvalidOperationException("No se encontró la cuota seleccionada.");

            decimal paid = Math.Min(entrega, cr.Saldo);
            await conn.ExecuteAsync(
                "UPDATE creditocliente SET entrega = entrega + @M, saldo = GREATEST(0, saldo - @M) WHERE idcredito = @Id;",
                new { M = paid, Id = idCredito }, tx);

            await conn.ExecuteAsync(
                @"INSERT INTO cobrocliente (idventa, nro_venta, nro_factura, vencimiento, cuota, fecha, dia, idcliente, cobro, entrega, fecha_cobro, forma_cobro, mora, idcaja, cidusuario, idsucursal, dessucursal, idcobrador, obs)
                  VALUES (@IdVenta, @NroVenta, @NroFactura, CURDATE(), @Cuota, CURDATE(), DAY(CURDATE()), @IdCliente, @Paid, @Paid, CURDATE(), 'EFECTIVO', @Mora, @IdCaja, @User, 1, 'CASA CENTRAL', 0, 'COBRO CUOTA');",
                new { cr.IdVenta, cr.NroVenta, NroFactura = cr.NroFactura ?? "", cr.Cuota, cr.IdCliente, Paid = paid, Mora = cr.DiasMora, IdCaja = idCaja > 0 ? idCaja : 1, User = Trunc(usuario, 20) }, tx);

            await conn.ExecuteAsync(
                @"INSERT INTO movimientocaja (nrorubro, ingresos, egresos, tipo, detalle, fechaalta, hora, estado, idventas, idcompras, idcaja, idmoneda, cidusuario, obs)
                  VALUES (1, @M, 0, 'ENTRADA', @Detalle, CURDATE(), @Hora, 1, @IdVenta, 0, @IdCaja, 1, @User, '');",
                new { M = paid, Detalle = Trunc($"COBRO CUOTA {cr.Cuota} VTA #{cr.NroVenta}", 150), Hora = DateTime.Now.ToString("HH:mm:ss"), cr.IdVenta, IdCaja = idCaja > 0 ? idCaja : 1, User = Trunc(usuario, 20) }, tx);

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task<List<CreditApplicationEntity>> GetCreditApplicationsAsync(int? estadoFilter = null)
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT s.idsolicitud AS IdSolicitud, s.idcliente AS IdCliente, COALESCE(c.nombres,'') AS DesCliente,
                   s.idvendedor AS IdVendedor, COALESCE(v.nombres,'') AS DesVendedor,
                   s.monto AS Monto, s.plazo AS Plazo, s.cuota AS Cuota,
                   CASE WHEN s.fechaingreso IS NULL OR CAST(s.fechaingreso AS CHAR)='0000-00-00' THEN CURDATE() ELSE s.fechaingreso END AS FechaIngreso,
                   s.estado AS Estado, COALESCE(s.observaciones,'') AS Observaciones,
                   CASE WHEN s.fecha_aprobacion IS NULL OR CAST(s.fecha_aprobacion AS CHAR)='0000-00-00' THEN NULL ELSE s.fecha_aprobacion END AS FechaAprobacion,
                   COALESCE(s.obs_estado,'') AS ObsEstado, COALESCE(s.desbanco,'') AS DesBanco
            FROM solicitudcredito s
            LEFT JOIN clientes c ON s.idcliente = c.idcliente
            LEFT JOIN vendedores v ON s.idvendedor = v.idvendedor
            WHERE (@Est IS NULL OR s.estado = @Est)
            ORDER BY s.idsolicitud DESC;";
        return (await conn.QueryAsync<CreditApplicationEntity>(sql, new { Est = estadoFilter })).ToList();
    }

    public async Task<int> SaveCreditApplicationAsync(CreditApplicationEntity app, string lcTipo)
    {
        using var conn = _connectionFactory.CreateConnection();
        string fecAprob = app.FechaAprobacion?.ToString("yyyy-MM-dd") ?? "0000-00-00";
        if (string.Equals(lcTipo, "M", StringComparison.OrdinalIgnoreCase) && app.IdSolicitud > 0)
        {
            await conn.ExecuteAsync(
                "UPDATE solicitudcredito SET idcliente=@IdCliente, monto=@Monto, plazo=@Plazo, cuota=@Cuota, observaciones=@Observaciones, idvendedor=@IdVendedor, desbanco=@DesBanco WHERE idsolicitud=@IdSolicitud;",
                app);
            return app.IdSolicitud;
        }
        app.IdSolicitud = await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO solicitudcredito (idcliente, monto, plazo, cuota, fechaingreso, estado, observaciones, fecha_aprobacion, idbancolista, idvendedor, obs_estado, idproducto, codproducto, desbanco)
              VALUES (@IdCliente, @Monto, @Plazo, @Cuota, @FechaIngreso, @Estado, @Observaciones, @FecAprob, 0, @IdVendedor, @ObsEstado, 0, '', @DesBanco); SELECT LAST_INSERT_ID();",
            new { app.IdCliente, app.Monto, app.Plazo, app.Cuota, app.FechaIngreso, app.Estado, Observaciones = app.Observaciones ?? "", FecAprob = fecAprob, app.IdVendedor, ObsEstado = app.ObsEstado ?? "", DesBanco = app.DesBanco ?? "" });
        return app.IdSolicitud;
    }

    public async Task<bool> UpdateCreditApplicationStatusAsync(int idSolicitud, int estado, string obsEstado)
    {
        using var conn = _connectionFactory.CreateConnection();
        return await conn.ExecuteAsync(
            "UPDATE solicitudcredito SET estado=@Est, fecha_aprobacion=CURDATE(), obs_estado=@Obs WHERE idsolicitud=@Id;",
            new { Id = idSolicitud, Est = estado, Obs = obsEstado ?? "" }) > 0;
    }

    // =========================================================================
    // EXTENDED INFORMES & AUDITORÍA
    // =========================================================================
    public async Task<List<ExtendedReportRow>> GetSalesBySellerReportAsync(DateTime desde, DateTime hasta)
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT CAST(v.idvendedor AS CHAR) AS Clave, COALESCE(vn.nombres, 'SIN VENDEDOR') AS Descripcion,
                   '' AS Extra1, '' AS Extra2, COUNT(*) AS Cantidad,
                   COALESCE(SUM(v.total_venta), 0) AS Monto1,
                   COALESCE(MAX(vn.comision), 0) AS Monto2,
                   ROUND(COALESCE(SUM(v.total_venta), 0) * COALESCE(MAX(vn.comision), 0) / 100.0, 0) AS Monto3
            FROM ventas v
            LEFT JOIN vendedores vn ON v.idvendedor = vn.idvendedor
            WHERE v.fecha_venta >= @Desde AND v.fecha_venta <= @Hasta AND COALESCE(v.estado, 1) <> 0
            GROUP BY v.idvendedor, vn.nombres
            ORDER BY Monto1 DESC;";
        return (await conn.QueryAsync<ExtendedReportRow>(sql, new { Desde = desde.Date, Hasta = hasta.Date })).ToList();
    }

    public async Task<List<ExtendedReportRow>> GetSalesBySupervisorReportAsync(DateTime desde, DateTime hasta)
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT CAST(COALESCE(vn.idsupervisor, 0) AS CHAR) AS Clave, COALESCE(s.nombres, 'SIN SUPERVISOR') AS Descripcion,
                   '' AS Extra1, '' AS Extra2, COUNT(*) AS Cantidad,
                   COALESCE(SUM(v.total_venta), 0) AS Monto1,
                   COALESCE(MAX(s.comision), 0) AS Monto2,
                   ROUND(COALESCE(SUM(v.total_venta), 0) * COALESCE(MAX(s.comision), 0) / 100.0, 0) AS Monto3
            FROM ventas v
            LEFT JOIN vendedores vn ON v.idvendedor = vn.idvendedor
            LEFT JOIN supervisores s ON vn.idsupervisor = s.idsupervisor
            WHERE v.fecha_venta >= @Desde AND v.fecha_venta <= @Hasta AND COALESCE(v.estado, 1) <> 0
            GROUP BY vn.idsupervisor, s.nombres
            ORDER BY Monto1 DESC;";
        return (await conn.QueryAsync<ExtendedReportRow>(sql, new { Desde = desde.Date, Hasta = hasta.Date })).ToList();
    }

    public async Task<List<ExtendedReportRow>> GetSalesByCustomerReportAsync(DateTime desde, DateTime hasta)
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT COALESCE(c.ruc, 'X') AS Clave, COALESCE(c.nombres, 'OCASIONAL') AS Descripcion,
                   '' AS Extra1, '' AS Extra2, COUNT(*) AS Cantidad,
                   COALESCE(SUM(v.total_venta), 0) AS Monto1, 0 AS Monto2, 0 AS Monto3
            FROM ventas v
            LEFT JOIN clientes c ON v.idcliente = c.idcliente
            WHERE v.fecha_venta >= @Desde AND v.fecha_venta <= @Hasta AND COALESCE(v.estado, 1) <> 0
            GROUP BY v.idcliente, c.ruc, c.nombres
            ORDER BY Monto1 DESC;";
        return (await conn.QueryAsync<ExtendedReportRow>(sql, new { Desde = desde.Date, Hasta = hasta.Date })).ToList();
    }

    public async Task<List<ExtendedReportRow>> GetSalesByDepositReportAsync(DateTime desde, DateTime hasta)
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT CAST(v.iddeposito AS CHAR) AS Clave, COALESCE(d.descripcion, CONCAT('DEPÓSITO #', v.iddeposito)) AS Descripcion,
                   '' AS Extra1, '' AS Extra2, COUNT(*) AS Cantidad,
                   COALESCE(SUM(v.total_venta), 0) AS Monto1, 0 AS Monto2, 0 AS Monto3
            FROM ventas v
            LEFT JOIN depositos d ON v.iddeposito = d.iddeposito
            WHERE v.fecha_venta >= @Desde AND v.fecha_venta <= @Hasta AND COALESCE(v.estado, 1) <> 0
            GROUP BY v.iddeposito, d.descripcion
            ORDER BY Monto1 DESC;";
        return (await conn.QueryAsync<ExtendedReportRow>(sql, new { Desde = desde.Date, Hasta = hasta.Date })).ToList();
    }

    public async Task<List<ExtendedReportRow>> GetSoldProductsReportAsync(DateTime desde, DateTime hasta)
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT iv.codigo AS Clave, COALESCE(MAX(p.descripcion), MAX(iv.descripcion), '') AS Descripcion,
                   '' AS Extra1, '' AS Extra2, SUM(iv.cantidad) AS Cantidad,
                   SUM(COALESCE(iv.pre_exenta,0)+COALESCE(iv.pre_iva5,0)+COALESCE(iv.pre_iva10,0)) AS Monto1, 0 AS Monto2, 0 AS Monto3
            FROM itemsventas iv
            INNER JOIN ventas v ON iv.idventa = v.idventa
            LEFT JOIN productos p ON iv.idproducto = p.idproducto
            WHERE v.fecha_venta >= @Desde AND v.fecha_venta <= @Hasta AND COALESCE(v.estado, 1) <> 0
            GROUP BY iv.idproducto, iv.codigo
            ORDER BY Monto1 DESC;";
        return (await conn.QueryAsync<ExtendedReportRow>(sql, new { Desde = desde.Date, Hasta = hasta.Date })).ToList();
    }

    public async Task<List<ExtendedReportRow>> GetPurchasesBySupplierReportAsync(DateTime desde, DateTime hasta)
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT COALESCE(pr.ruc, '') AS Clave, COALESCE(pr.descripcion, 'SIN PROVEEDOR') AS Descripcion,
                   '' AS Extra1, '' AS Extra2, COUNT(*) AS Cantidad,
                   COALESCE(SUM(c.total_compra), 0) AS Monto1, 0 AS Monto2, 0 AS Monto3
            FROM compras c
            LEFT JOIN proveedores pr ON c.idproveedor = pr.idproveedor
            WHERE c.fecha_compra >= @Desde AND c.fecha_compra <= @Hasta AND COALESCE(c.estado, 1) <> 0
            GROUP BY c.idproveedor, pr.ruc, pr.descripcion
            ORDER BY Monto1 DESC;";
        return (await conn.QueryAsync<ExtendedReportRow>(sql, new { Desde = desde.Date, Hasta = hasta.Date })).ToList();
    }

    public async Task<List<ExtendedReportRow>> GetPurchasedProductsReportAsync(DateTime desde, DateTime hasta)
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT ic.codigo AS Clave, COALESCE(MAX(p.descripcion), '') AS Descripcion,
                   '' AS Extra1, '' AS Extra2, SUM(ic.cantidad) AS Cantidad,
                   SUM(COALESCE(ic.pre_exenta,0)+COALESCE(ic.pre_iva5,0)+COALESCE(ic.pre_iva10,0)) AS Monto1, 0 AS Monto2, 0 AS Monto3
            FROM itemscompras ic
            INNER JOIN compras c ON ic.idcompra = c.idcompra
            LEFT JOIN productos p ON ic.idproducto = p.idproducto
            WHERE c.fecha_compra >= @Desde AND c.fecha_compra <= @Hasta AND COALESCE(c.estado, 1) <> 0
            GROUP BY ic.idproducto, ic.codigo
            ORDER BY Monto1 DESC;";
        return (await conn.QueryAsync<ExtendedReportRow>(sql, new { Desde = desde.Date, Hasta = hasta.Date })).ToList();
    }

    public async Task<List<UserTransactionLogEntity>> GetUserTransactionLogsAsync(DateTime desde, DateTime hasta, string? userFilter = null)
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT id_tranusu AS IdTranUsu, COALESCE(form_tran,'') AS FormTran, COALESCE(tabla_tran,'') AS TablaTran,
                   COALESCE(tipo_tran,'') AS TipoTran,
                   CASE WHEN fecha_tran IS NULL OR CAST(fecha_tran AS CHAR) LIKE '0000-00-00%' THEN NOW() ELSE fecha_tran END AS FechaTran,
                   COALESCE(sql_tran,'') AS SqlTran, clave_tran AS ClaveTran, COALESCE(userid_tran,'') AS UserIdTran
            FROM tbl_tranusu
            WHERE DATE(fecha_tran) >= @Desde AND DATE(fecha_tran) <= @Hasta
              AND (@User = '' OR UPPER(userid_tran) LIKE @UserLike)
            ORDER BY id_tranusu DESC
            LIMIT 400;";
        string u = (userFilter ?? string.Empty).Trim().ToUpperInvariant();
        return (await conn.QueryAsync<UserTransactionLogEntity>(sql, new { Desde = desde.Date, Hasta = hasta.Date, User = u, UserLike = $"%{u}%" })).ToList();
    }

    // =========================================================================
    // RRHH / PERSONALES
    // =========================================================================
    public async Task<SalaryParametersEntity> GetSalaryParametersAsync()
    {
        using var conn = _connectionFactory.CreateConnection();
        var row = await conn.QueryFirstOrDefaultAsync<SalaryParametersEntity>(
            @"SELECT idsalarioparametro AS IdSalarioParametro, salario_minimo AS SalarioMinimo, ips_patronal AS IpsPatronal,
                     ips_empleado AS IpsEmpleado, bonificacion_familiar AS BonificacionFamiliar, limite_edad_hijos AS LimiteEdadHijos,
                     precio_hora_extra AS PrecioHoraExtra, nro_ips_patronal AS NroIpsPatronal
              FROM salariosparametros LIMIT 1;");
        return row ?? new SalaryParametersEntity();
    }

    public async Task SaveSalaryParametersAsync(SalaryParametersEntity p)
    {
        using var conn = _connectionFactory.CreateConnection();
        if (p.IdSalarioParametro <= 0)
        {
            p.IdSalarioParametro = await conn.ExecuteScalarAsync<int>(
                @"INSERT INTO salariosparametros (salario_minimo, ips_patronal, ips_empleado, bonificacion_familiar, limite_edad_hijos, precio_hora_extra, nro_ips_patronal)
                  VALUES (@SalarioMinimo, @IpsPatronal, @IpsEmpleado, @BonificacionFamiliar, @LimiteEdadHijos, @PrecioHoraExtra, @NroIpsPatronal); SELECT LAST_INSERT_ID();", p);
        }
        else
        {
            await conn.ExecuteAsync(
                @"UPDATE salariosparametros SET salario_minimo=@SalarioMinimo, ips_patronal=@IpsPatronal, ips_empleado=@IpsEmpleado,
                  bonificacion_familiar=@BonificacionFamiliar, limite_edad_hijos=@LimiteEdadHijos, precio_hora_extra=@PrecioHoraExtra, nro_ips_patronal=@NroIpsPatronal
                  WHERE idsalarioparametro=@IdSalarioParametro;", p);
        }
    }

    public async Task<List<EmployeeLegajoEntity>> GetEmployeesAsync()
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT idlegajo AS IdLegajo, apellidos AS Apellidos, nombres AS Nombres, documento AS Documento,
                   COALESCE(tipo_doc,'CI') AS TipoDoc, COALESCE(nacionalidad,'') AS Nacionalidad, COALESCE(desnacionalidad,'') AS DesNacionalidad,
                   COALESCE(cargo,'') AS Cargo, COALESCE(descargo,'') AS DesCargo, COALESCE(departamento,'') AS Departamento,
                   COALESCE(desdepartamento,'') AS DesDepartamento, COALESCE(area,'') AS Area, COALESCE(desarea,'') AS DesArea,
                   COALESCE(telefono,'') AS Telefono, COALESCE(direccion,'') AS Direccion, COALESCE(correo,'') AS Correo,
                   CASE WHEN fecha_nacimiento IS NULL OR CAST(fecha_nacimiento AS CHAR)='0000-00-00' THEN '1990-01-01' ELSE fecha_nacimiento END AS FechaNacimiento,
                   CASE WHEN fecha_ingreso IS NULL OR CAST(fecha_ingreso AS CHAR)='0000-00-00' THEN CURDATE() ELSE fecha_ingreso END AS FechaIngreso,
                   salario AS Salario, ips_nro AS IpsNro, estadocivil AS EstadoCivil, sexo AS Sexo
            FROM legajos ORDER BY apellidos, nombres;";
        return (await conn.QueryAsync<EmployeeLegajoEntity>(sql)).ToList();
    }

    public async Task<int> SaveEmployeeAsync(EmployeeLegajoEntity emp, string lcTipo)
    {
        using var conn = _connectionFactory.CreateConnection();
        if (string.Equals(lcTipo, "M", StringComparison.OrdinalIgnoreCase) && emp.IdLegajo > 0)
        {
            await conn.ExecuteAsync(
                @"UPDATE legajos SET apellidos=@Apellidos, nombres=@Nombres, documento=@Documento, tipo_doc=@TipoDoc,
                  nacionalidad=@Nacionalidad, desnacionalidad=@DesNacionalidad, cargo=@Cargo, descargo=@DesCargo,
                  departamento=@Departamento, desdepartamento=@DesDepartamento, area=@Area, desarea=@DesArea,
                  telefono=@Telefono, direccion=@Direccion, correo=@Correo, fecha_nacimiento=@FechaNacimiento,
                  fecha_ingreso=@FechaIngreso, salario=@Salario, ips_nro=@IpsNro, estadocivil=@EstadoCivil, sexo=@Sexo
                  WHERE idlegajo=@IdLegajo;", emp);
            return emp.IdLegajo;
        }
        emp.IdLegajo = await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO legajos (apellidos, nombres, nacionalidad, desnacionalidad, tipo_doc, cargo, descargo, documento, cuenta, telefono, direccion, fecha_nacimiento, fecha_ingreso, salario, foto, correo, estadocivil, ips_nro, sexo, lugar_nac, departamento, desdepartamento, area, desarea)
              VALUES (@Apellidos, @Nombres, @Nacionalidad, @DesNacionalidad, @TipoDoc, @Cargo, @DesCargo, @Documento, '', @Telefono, @Direccion, @FechaNacimiento, @FechaIngreso, @Salario, '', @Correo, @EstadoCivil, @IpsNro, @Sexo, '', @Departamento, @DesDepartamento, @Area, @DesArea);
              SELECT LAST_INSERT_ID();", emp);
        return emp.IdLegajo;
    }

    public async Task<bool> DeleteEmployeeAsync(int idLegajo)
    {
        using var conn = _connectionFactory.CreateConnection();
        return await conn.ExecuteAsync("DELETE FROM legajos WHERE idlegajo = @Id;", new { Id = idLegajo }) > 0;
    }

    public async Task<List<EmployeeDiscountEntity>> GetEmployeeDiscountsAsync()
    {
        using var conn = _connectionFactory.CreateConnection();
        return (await conn.QueryAsync<EmployeeDiscountEntity>(
            @"SELECT iddescuentopersonal AS IdDescuentoPersonal, idlegajo AS IdLegajo, COALESCE(documento,'') AS Documento,
                     COALESCE(nombres,'') AS Nombres, CASE WHEN fecha_descuento IS NULL OR CAST(fecha_descuento AS CHAR)='0000-00-00' THEN CURDATE() ELSE fecha_descuento END AS FechaDescuento,
                     monto AS Monto, cuota AS Cuota, saldo AS Saldo, iddescuento AS IdDescuento,
                     COALESCE(coddescuento,'') AS CodDescuento, COALESCE(desdescuento,'') AS DesDescuento, estado AS Estado, monto_cuota AS MontoCuota
              FROM descuentopersonal ORDER BY iddescuentopersonal DESC;")).ToList();
    }

    public async Task<int> SaveEmployeeDiscountAsync(EmployeeDiscountEntity d, string lcTipo)
    {
        using var conn = _connectionFactory.CreateConnection();
        if (string.Equals(lcTipo, "M", StringComparison.OrdinalIgnoreCase) && d.IdDescuentoPersonal > 0)
        {
            await conn.ExecuteAsync(
                "UPDATE descuentopersonal SET monto=@Monto, cuota=@Cuota, saldo=@Saldo, desdescuento=@DesDescuento, monto_cuota=@MontoCuota, estado=@Estado WHERE iddescuentopersonal=@IdDescuentoPersonal;", d);
            return d.IdDescuentoPersonal;
        }
        d.IdDescuentoPersonal = await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO descuentopersonal (idlegajo, documento, nombres, fecha_descuento, monto, cuota, saldo, iddescuento, coddescuento, desdescuento, estado, monto_cuota)
              VALUES (@IdLegajo, @Documento, @Nombres, @FechaDescuento, @Monto, @Cuota, @Saldo, @IdDescuento, @CodDescuento, @DesDescuento, @Estado, @MontoCuota);
              SELECT LAST_INSERT_ID();", d);
        return d.IdDescuentoPersonal;
    }

    public async Task<List<SalaryPayrollEntity>> GetSalaryPayrollsAsync(string? mes = null, int? anio = null)
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT idsalario AS IdSalario, COALESCE(mes,'') AS Mes, COALESCE(`año`, YEAR(CURDATE())) AS Anio,
                   idlegajo AS IdLegajo, COALESCE(documento,'') AS Documento, COALESCE(desnombres,'') AS DesNombres,
                   dias_trabajados AS DiasTrabajados, horas_extra AS HorasExtra, monto_bonificacion AS MontoBonificacion,
                   total_ingreso AS TotalIngreso, descuentos AS Descuentos, salario AS Salario,
                   monto_horas_extras AS MontoHorasExtras, bonificacion_familiar AS BonificacionFamiliar,
                   monto_ips AS MontoIps, total_egresos AS TotalEgresos, neto_percibir AS NetoPercibir,
                   cantidad_feriado_mes AS CantidadFeriadoMes,
                   CASE WHEN fecha_pago_salario IS NULL OR CAST(fecha_pago_salario AS CHAR)='0000-00-00' THEN CURDATE() ELSE fecha_pago_salario END AS FechaPagoSalario
            FROM salarios
            WHERE (@Mes = '' OR mes = @Mes) AND (@Anio IS NULL OR `año` = @Anio)
            ORDER BY idsalario DESC;";
        return (await conn.QueryAsync<SalaryPayrollEntity>(sql, new { Mes = mes ?? "", Anio = anio })).ToList();
    }

    public async Task<int> ProcessAndSaveSalaryPayrollAsync(SalaryPayrollEntity s)
    {
        using var conn = _connectionFactory.CreateConnection();
        const string insSql = @"
            INSERT INTO salarios (mes, `año`, idlegajo, documento, desnombres, dias_trabajados, horas_extra, monto_bonificacion,
                                  total_ingreso, descuentos, salario, monto_horas_extras, bonificacion_familiar, monto_ips, total_egresos, neto_percibir, cantidad_feriado_mes, fecha_pago_salario)
            VALUES (@Mes, @Anio, @IdLegajo, @Documento, @DesNombres, @DiasTrabajados, @HorasExtra, @MontoBonificacion,
                    @TotalIngreso, @Descuentos, @Salario, @MontoHorasExtras, @BonificacionFamiliar, @MontoIps, @TotalEgresos, @NetoPercibir, @CantidadFeriadoMes, @FechaPagoSalario);
            SELECT LAST_INSERT_ID();";
        s.IdSalario = await conn.ExecuteScalarAsync<int>(insSql, s);
        return s.IdSalario;
    }

    private static (string Table, string Pk) ResolveHrTable(string tableName) => tableName.Trim().ToLowerInvariant() switch
    {
        "nacionalidades" => ("nacionalidades", "idnacionalidad"),
        "cargos" => ("cargos", "idcargo"),
        "departamentos" => ("departamentos", "iddepartamento"),
        "areas" => ("areas", "idarea"),
        "tipodocumentos" => ("tipodocumentos", "idtipodoc"),
        "descuentos" => ("descuentos", "iddescuento"),
        _ => throw new ArgumentException($"Tabla RRHH no soportada: {tableName}")
    };

    public async Task<List<HrLookupEntity>> GetHrLookupAsync(string tableName)
    {
        var (tbl, pk) = ResolveHrTable(tableName);
        using var conn = _connectionFactory.CreateConnection();
        string sql = $"SELECT {pk} AS Id, codigo AS Codigo, descripcion AS Descripcion FROM {tbl} ORDER BY descripcion;";
        return (await conn.QueryAsync<HrLookupEntity>(sql)).ToList();
    }

    public async Task<int> SaveHrLookupAsync(string tableName, HrLookupEntity row, string lcTipo)
    {
        var (tbl, pk) = ResolveHrTable(tableName);
        using var conn = _connectionFactory.CreateConnection();
        int maxCod = 3;
        int maxDesc = 45;
        try
        {
            var lens = (await conn.QueryAsync<(string Col, int? MaxLen)>(
                "SELECT LOWER(COLUMN_NAME), CHARACTER_MAXIMUM_LENGTH FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @Tbl AND COLUMN_NAME IN ('codigo', 'descripcion');",
                new { Tbl = tbl })).ToList();
            foreach (var (col, len) in lens)
            {
                if (col == "codigo" && len is > 0) maxCod = len.Value;
                if (col == "descripcion" && len is > 0) maxDesc = len.Value;
            }
        }
        catch { }

        string cod = Trunc(row.Codigo, maxCod);
        string desc = Trunc(row.Descripcion, maxDesc);
        bool hasSuc = tbl is "cargos" or "areas";
        if (string.Equals(lcTipo, "M", StringComparison.OrdinalIgnoreCase) && row.Id > 0)
        {
            await conn.ExecuteAsync(
                $"UPDATE {tbl} SET codigo=@Codigo, descripcion=@Descripcion WHERE {pk}=@Id;",
                new { row.Id, Codigo = cod, Descripcion = desc });
            return row.Id;
        }
        string sql = hasSuc
            ? $"INSERT INTO {tbl} (codigo, descripcion, idsucursal, dessucursal) VALUES (@Codigo, @Descripcion, 1, 'CASA CENTRAL'); SELECT LAST_INSERT_ID();"
            : $"INSERT INTO {tbl} (codigo, descripcion) VALUES (@Codigo, @Descripcion); SELECT LAST_INSERT_ID();";
        row.Id = await conn.ExecuteScalarAsync<int>(sql, new { Codigo = cod, Descripcion = desc });
        return row.Id;
    }

    public async Task<bool> DeleteHrLookupAsync(string tableName, int id)
    {
        var (tbl, pk) = ResolveHrTable(tableName);
        using var conn = _connectionFactory.CreateConnection();
        return await conn.ExecuteAsync($"DELETE FROM {tbl} WHERE {pk} = @Id;", new { Id = id }) > 0;
    }

    // =========================================================================
    // RESTAURANT & VETERINARIA HELPERS
    // =========================================================================
    public async Task<List<RestaurantOrderItemRow>> GetRestaurantOrdersBySectorAsync(string? sector = null)
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT v.idventa AS IdVenta, v.nro_venta AS NroVenta, v.mesa AS Mesa, COALESCE(v.hora_venta,'') AS HoraVenta,
                   iv.codigo AS Codigo, COALESCE(iv.descripcion, p.descripcion, '') AS Descripcion, iv.cantidad AS Cantidad,
                   CASE
                       WHEN COALESCE(iv.cocina, p.cocina, 0) = 1 THEN 'COCINA'
                       WHEN COALESCE(iv.barra, p.barra, 0) = 1 THEN 'BARRA'
                       WHEN COALESCE(iv.churrasqueria, p.churrasqueria, 0) = 1 THEN 'CHURRASQUERIA'
                       ELSE 'OTROS'
                   END AS Sector,
                   COALESCE(iv.observaciones, iv.obs, '') AS Observaciones,
                   COALESCE(iv.precio_total, iv.pre_iva10+iv.pre_iva5+iv.pre_exenta) AS Total
            FROM itemsventas iv
            INNER JOIN ventas v ON iv.idventa = v.idventa
            LEFT JOIN productos p ON iv.idproducto = p.idproducto
            WHERE COALESCE(v.estado, 1) <> 0
              AND v.fecha_venta >= DATE_SUB(CURDATE(), INTERVAL 1 DAY)
            ORDER BY v.idventa DESC, iv.iditemventa DESC
            LIMIT 150;";
        var rows = (await conn.QueryAsync<RestaurantOrderItemRow>(sql)).ToList();
        if (!string.IsNullOrWhiteSpace(sector))
            rows = rows.Where(r => string.Equals(r.Sector, sector.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        return rows;
    }

    public async Task<bool> TableExistsAsync(string tableName)
    {
        try
        {
            using var conn = _connectionFactory.CreateConnection();
            int cnt = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND LOWER(TABLE_NAME) = LOWER(@Tbl);",
                new { Tbl = (tableName ?? string.Empty).Trim() });
            return cnt > 0;
        }
        catch
        {
            return false;
        }
    }
}
