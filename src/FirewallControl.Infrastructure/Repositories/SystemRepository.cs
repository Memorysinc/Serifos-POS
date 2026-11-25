using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using FirewallControl.Core.Business;
using FirewallControl.Core.Entities;
using FirewallControl.Core.Interfaces;

namespace FirewallControl.Infrastructure.Repositories;

/// <summary>
/// Implements ISystemRepository against datosempresa, sucursal, vendedores, usuarios, and grupos.
/// </summary>
public class SystemRepository : ISystemRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public SystemRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<CompanyDataEntity> GetCompanyDataAsync()
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT iddatosempresa AS IdDatosEmpresa,
                   descripcion AS Descripcion,
                   ruc AS Ruc,
                   direccion AS Direccion,
                   telefonos AS Telefonos,
                   rutafoto AS RutaFoto,
                   correopredeterminado AS CorreoPredeterminado,
                   utilidad1 AS Utilidad1,
                   utilidad2 AS Utilidad2,
                   utilidad3 AS Utilidad3,
                   utilidad4 AS Utilidad4,
                   utilidad5 AS Utilidad5,
                   utilidad6 AS Utilidad6,
                   utilidad7 AS Utilidad7,
                   interescuota AS InteresCuota,
                   interespunitorio AS InteresPunitorio,
                   reclamos AS Reclamos,
                   honorariojudicial AS HonorarioJudicial,
                   cobro_caja AS CobroCaja,
                   ventaitemsdeposito AS VentaItemsDeposito,
                   idcliente AS IdCliente,
                   idvendedor AS IdVendedor,
                   cantvias AS CantVias,
                   imprenormal AS ImpreNormal,
                   imprebalanza AS ImpreBalanza,
                   imprelegal AS ImpreLegal,
                   veterinaria AS Veterinaria,
                   comercial AS Comercial,
                   restaurant AS Restaurant,
                   habilita_cobro AS HabilitaCobro
            FROM datosempresa
            LIMIT 1;";

        var data = await conn.QueryFirstOrDefaultAsync<CompanyDataEntity>(sql);
        return data ?? new CompanyDataEntity();
    }

    public async Task SaveCompanyDataAsync(CompanyDataEntity data)
    {
        using var conn = _connectionFactory.CreateConnection();
        if (data.IdDatosEmpresa <= 0)
        {
            const string insSql = @"
                INSERT INTO datosempresa
                (descripcion, ruc, direccion, telefonos, rutafoto, correopredeterminado,
                 utilidad1, utilidad2, utilidad3, utilidad4, utilidad5, utilidad6, utilidad7,
                 interescuota, interespunitorio, reclamos, honorariojudicial,
                 cobro_caja, ventaitemsdeposito, idcliente, idvendedor, cantvias,
                 imprenormal, imprebalanza, imprelegal, veterinaria, comercial, restaurant, habilita_cobro)
                VALUES
                (@Descripcion, @Ruc, @Direccion, @Telefonos, @RutaFoto, @CorreoPredeterminado,
                 @Utilidad1, @Utilidad2, @Utilidad3, @Utilidad4, @Utilidad5, @Utilidad6, @Utilidad7,
                 @InteresCuota, @InteresPunitorio, @Reclamos, @HonorarioJudicial,
                 @CobroCaja, @VentaItemsDeposito, @IdCliente, @IdVendedor, @CantVias,
                 @ImpreNormal, @ImpreBalanza, @ImpreLegal, @Veterinaria, @Comercial, @Restaurant, @HabilitaCobro);";
            await conn.ExecuteAsync(insSql, data);
        }
        else
        {
            const string updSql = @"
                UPDATE datosempresa SET
                    descripcion = @Descripcion,
                    ruc = @Ruc,
                    direccion = @Direccion,
                    telefonos = @Telefonos,
                    correopredeterminado = @CorreoPredeterminado,
                    utilidad1 = @Utilidad1,
                    utilidad2 = @Utilidad2,
                    utilidad3 = @Utilidad3,
                    utilidad4 = @Utilidad4,
                    utilidad5 = @Utilidad5,
                    utilidad6 = @Utilidad6,
                    utilidad7 = @Utilidad7,
                    interescuota = @InteresCuota,
                    interespunitorio = @InteresPunitorio,
                    reclamos = @Reclamos,
                    honorariojudicial = @HonorarioJudicial,
                    cobro_caja = @CobroCaja,
                    ventaitemsdeposito = @VentaItemsDeposito,
                    idcliente = @IdCliente,
                    idvendedor = @IdVendedor,
                    cantvias = @CantVias,
                    imprenormal = @ImpreNormal,
                    imprebalanza = @ImpreBalanza,
                    imprelegal = @ImpreLegal,
                    veterinaria = @Veterinaria,
                    comercial = @Comercial,
                    restaurant = @Restaurant,
                    habilita_cobro = @HabilitaCobro
                WHERE iddatosempresa = @IdDatosEmpresa;";
            await conn.ExecuteAsync(updSql, data);
        }
    }

    public async Task<List<BranchEntity>> GetBranchesAsync()
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT idsucursal AS IdSucursal,
                   nombre AS Nombre,
                   dir AS Dir,
                   tel AS Tel,
                   cel AS Cel,
                   enc AS Enc,
                   email AS Email
            FROM sucursal
            ORDER BY idsucursal;";
        var rows = await conn.QueryAsync<BranchEntity>(sql);
        return rows.ToList();
    }

    public async Task<List<SellerEntity>> GetSellersAsync()
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT idvendedor AS IdVendedor,
                   nombres AS Nombres,
                   documento AS Documento,
                   ruc AS Ruc,
                   telefonos AS Telefonos,
                   celular AS Celular,
                   direccion AS Direccion,
                   comision AS Comision,
                   descuento AS Descuento,
                   estado AS Estado,
                   tipovendedor AS TipoVendedor
            FROM vendedores
            WHERE estado = 1
            ORDER BY idvendedor;";
        var rows = await conn.QueryAsync<SellerEntity>(sql);
        return rows.ToList();
    }

    public async Task<List<SupervisorEntity>> GetSupervisorsAsync()
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT idsupervisor AS IdSupervisor,
                   nombres AS Nombres,
                   estado AS Estado
            FROM supervisores
            WHERE estado = 1
            ORDER BY nombres;";
        var rows = await conn.QueryAsync<SupervisorEntity>(sql);
        return rows.ToList();
    }

    public async Task<int> SaveSellerAsync(SellerEntity seller, string lcTipo)
    {
        using var conn = _connectionFactory.CreateConnection();
        bool isModify = string.Equals(lcTipo, "M", StringComparison.OrdinalIgnoreCase) && seller.IdVendedor > 0;
        if (!isModify)
        {
            const string insSql = @"
                INSERT INTO vendedores (nombres, documento, ruc, telefonos, celular, direccion, comision, descuento, estado, tipovendedor)
                VALUES (@Nombres, @Documento, @Ruc, @Telefonos, @Celular, @Direccion, @Comision, @Descuento, @Estado, @TipoVendedor);
                SELECT LAST_INSERT_ID();";
            seller.IdVendedor = await conn.ExecuteScalarAsync<int>(insSql, seller);
            return seller.IdVendedor;
        }
        else
        {
            const string updSql = @"
                UPDATE vendedores SET
                    nombres = @Nombres,
                    documento = @Documento,
                    ruc = @Ruc,
                    telefonos = @Telefonos,
                    celular = @Celular,
                    direccion = @Direccion,
                    comision = @Comision,
                    descuento = @Descuento,
                    estado = @Estado,
                    tipovendedor = @TipoVendedor
                WHERE idvendedor = @IdVendedor;";
            await conn.ExecuteAsync(updSql, seller);
            return seller.IdVendedor;
        }
    }

    public async Task<List<UserEntity>> GetActiveUsersAsync()
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT Nlineno,
                   Cidgrupo,
                   Cidusuario,
                   Cdescribe,
                   Cpassword,
                   Csuspendido,
                   Fecha,
                   Userid,
                   idsucursal AS IdSucursal,
                   tipovendedor AS TipoVendedor,
                   idcaja AS IdCaja,
                   idvendedor AS IdVendedor,
                   iddeposito AS IdDeposito
            FROM usuarios
            WHERE Csuspendido = 0
            ORDER BY Cidusuario;";
        var rows = await conn.QueryAsync<UserEntity>(sql);
        return rows.ToList();
    }

    public async Task<UserEntity?> GetUserByUsernameAsync(string cidUsuario)
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT Nlineno,
                   Cidgrupo,
                   Cidusuario,
                   Cdescribe,
                   Cpassword,
                   Csuspendido,
                   Fecha,
                   Userid,
                   idsucursal AS IdSucursal,
                   tipovendedor AS TipoVendedor,
                   idcaja AS IdCaja,
                   idvendedor AS IdVendedor,
                   iddeposito AS IdDeposito
            FROM usuarios
            WHERE UPPER(TRIM(Cidusuario)) = UPPER(TRIM(@User))
            LIMIT 1;";
        return await conn.QueryFirstOrDefaultAsync<UserEntity>(sql, new { User = cidUsuario ?? "" });
    }

    public async Task<UserGroupEntity?> GetGroupByCodeAsync(string cidGrupo)
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT Nlineno,
                   Cidgrupo,
                   Cdescribe,
                   Caccesos,
                   avisos AS Avisos,
                   tool AS Tool,
                   cidusuario AS Cidusuario,
                   btncompra AS BtnCompra,
                   btnventa AS BtnVenta,
                   btnpedido AS BtnPedido,
                   btnpresu AS BtnPresu,
                   btnvenci AS BtnVenci,
                   btncliente AS BtnCliente,
                   btnproveedor AS BtnProveedor,
                   btnvendedor AS BtnVendedor,
                   btnproducto AS BtnProducto,
                   btnpago AS BtnPago,
                   btncobro AS BtnCobro,
                   btnmovcaja AS BtnMovCaja
            FROM grupos
            WHERE UPPER(TRIM(Cidgrupo)) = UPPER(TRIM(@Group))
            LIMIT 1;";
        return await conn.QueryFirstOrDefaultAsync<UserGroupEntity>(sql, new { Group = cidGrupo ?? "" });
    }

    public async Task<List<UserGroupEntity>> GetAllGroupsAsync()
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT Nlineno,
                   Cidgrupo,
                   Cdescribe,
                   Caccesos,
                   avisos AS Avisos,
                   tool AS Tool,
                   cidusuario AS Cidusuario,
                   btncompra AS BtnCompra,
                   btnventa AS BtnVenta,
                   btnpedido AS BtnPedido,
                   btnpresu AS BtnPresu,
                   btnvenci AS BtnVenci,
                   btncliente AS BtnCliente,
                   btnproveedor AS BtnProveedor,
                   btnvendedor AS BtnVendedor,
                   btnproducto AS BtnProducto,
                   btnpago AS BtnPago,
                   btncobro AS BtnCobro,
                   btnmovcaja AS BtnMovCaja
            FROM grupos
            ORDER BY Cidgrupo;";
        var rows = await conn.QueryAsync<UserGroupEntity>(sql);
        return rows.ToList();
    }

    public async Task SaveGroupAsync(UserGroupEntity group, string lcTipo)
    {
        using var conn = _connectionFactory.CreateConnection();
        bool isModify = string.Equals(lcTipo, "M", StringComparison.OrdinalIgnoreCase) && group.Nlineno > 0;
        if (!isModify)
        {
            const string insSql = @"
                INSERT INTO grupos
                (Cidgrupo, Cdescribe, Caccesos, avisos, tool, cidusuario,
                 btncompra, btnventa, btnpedido, btnpresu, btnvenci,
                 btncliente, btnproveedor, btnvendedor, btnproducto, btnpago, btncobro, btnmovcaja)
                VALUES
                (@Cidgrupo, @Cdescribe, @Caccesos, @Avisos, @Tool, @Cidusuario,
                 @BtnCompra, @BtnVenta, @BtnPedido, @BtnPresu, @BtnVenci,
                 @BtnCliente, @BtnProveedor, @BtnVendedor, @BtnProducto, @BtnPago, @BtnCobro, @BtnMovCaja);";
            await conn.ExecuteAsync(insSql, group);
        }
        else
        {
            const string updSql = @"
                UPDATE grupos SET
                    Cidgrupo = @Cidgrupo,
                    Cdescribe = @Cdescribe,
                    Caccesos = @Caccesos,
                    avisos = @Avisos,
                    tool = @Tool,
                    btncompra = @BtnCompra,
                    btnventa = @BtnVenta,
                    btnpedido = @BtnPedido,
                    btnpresu = @BtnPresu,
                    btnvenci = @BtnVenci,
                    btncliente = @BtnCliente,
                    btnproveedor = @BtnProveedor,
                    btnvendedor = @BtnVendedor,
                    btnproducto = @BtnProducto,
                    btnpago = @BtnPago,
                    btncobro = @BtnCobro,
                    btnmovcaja = @BtnMovCaja
                WHERE Nlineno = @Nlineno;";
            await conn.ExecuteAsync(updSql, group);
        }
    }

    public async Task SaveUserPasswordAsync(int nlineno, string newPlainPassword)
    {
        using var conn = _connectionFactory.CreateConnection();
        string crc = VfpCompat.Sys2007(newPlainPassword);
        const string sql = "UPDATE usuarios SET Cpassword = @Pwd WHERE Nlineno = @Id;";
        await conn.ExecuteAsync(sql, new { Pwd = crc, Id = nlineno });
    }

    public async Task SaveUserAsync(UserEntity user, string lcTipo)
    {
        using var conn = _connectionFactory.CreateConnection();
        bool isModify = string.Equals(lcTipo, "M", StringComparison.OrdinalIgnoreCase) && user.Nlineno > 0;
        if (!isModify)
        {
            const string insSql = @"
                INSERT INTO usuarios
                (Cidgrupo, Cidusuario, Cdescribe, Cpassword, Csuspendido, Fecha, Userid, idsucursal, tipovendedor, idcaja, idvendedor, iddeposito)
                VALUES
                (@Cidgrupo, @Cidusuario, @Cdescribe, @Cpassword, @Csuspendido, @Fecha, @Userid, @IdSucursal, @TipoVendedor, @IdCaja, @IdVendedor, @IdDeposito);";
            await conn.ExecuteAsync(insSql, user);
        }
        else
        {
            const string updSql = @"
                UPDATE usuarios SET
                    Cidgrupo = @Cidgrupo,
                    Cidusuario = @Cidusuario,
                    Cdescribe = @Cdescribe,
                    Cpassword = @Cpassword,
                    Csuspendido = @Csuspendido,
                    idsucursal = @IdSucursal,
                    tipovendedor = @TipoVendedor,
                    idcaja = @IdCaja,
                    idvendedor = @IdVendedor,
                    iddeposito = @IdDeposito
                WHERE Nlineno = @Nlineno;";
            await conn.ExecuteAsync(updSql, user);
        }
    }

    public async Task<bool> DeleteUserAsync(int nlineno)
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = "UPDATE usuarios SET Csuspendido = 1 WHERE Nlineno = @Id;";
        int rows = await conn.ExecuteAsync(sql, new { Id = nlineno });
        return rows > 0;
    }

    public async Task<List<TimbradoLocalEntity>> GetTimbradosLocalesAsync()
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
            ORDER BY idsucursal, idcaja;";
        var rows = await conn.QueryAsync<TimbradoLocalEntity>(sql);
        return rows.ToList();
    }

    public async Task SaveTimbradoLocalAsync(TimbradoLocalEntity timbrado)
    {
        using var conn = _connectionFactory.CreateConnection();
        timbrado.Factura = timbrado.FormatNextInvoice();
        if (timbrado.IdTimbradoLocal <= 0)
        {
            const string insSql = @"
                INSERT INTO timbradolocal
                (idsucursal, idcaja, timbrado, n1, n2, n3, factura, cantidad, vencimiento, fechainicio)
                VALUES
                (@IdSucursal, @IdCaja, @Timbrado, @N1, @N2, @N3, @Factura, @Cantidad, @Vencimiento, @FechaInicio);";
            await conn.ExecuteAsync(insSql, timbrado);
        }
        else
        {
            const string updSql = @"
                UPDATE timbradolocal SET
                    idsucursal = @IdSucursal,
                    idcaja = @IdCaja,
                    timbrado = @Timbrado,
                    n1 = @N1,
                    n2 = @N2,
                    n3 = @N3,
                    factura = @Factura,
                    cantidad = @Cantidad,
                    vencimiento = @Vencimiento,
                    fechainicio = @FechaInicio
                WHERE idtimbradolocal = @IdTimbradoLocal;";
            await conn.ExecuteAsync(updSql, timbrado);
        }
    }

    public async Task<List<FormAccessEntity>> GetFormAccessListAsync(string nameForm)
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT id AS Id,
                   COALESCE(tituloform, '') AS TituloForm,
                   COALESCE(nameform, '') AS NameForm,
                   COALESCE(cidusuario, '') AS CidUsuario,
                   COALESCE(buscar, 0) AS Buscar,
                   COALESCE(nuevo, 0) AS Nuevo,
                   COALESCE(editar, 0) AS Editar,
                   COALESCE(eliminar, 0) AS Eliminar,
                   COALESCE(imprimir, 0) AS Imprimir
            FROM accesoforms
            WHERE (@NameForm = '' OR UPPER(TRIM(nameform)) = UPPER(@NameForm))
            ORDER BY cidusuario, nameform;";
        var rows = await conn.QueryAsync<FormAccessEntity>(sql, new { NameForm = (nameForm ?? string.Empty).Trim() });
        return rows.ToList();
    }

    public async Task SaveFormAccessAsync(FormAccessEntity access)
    {
        using var conn = _connectionFactory.CreateConnection();
        string title = (access.TituloForm ?? string.Empty).Trim();
        if (title.Length > 100) title = title[..100];
        string name = (access.NameForm ?? string.Empty).Trim().ToUpperInvariant();
        if (name.Length > 80) name = name[..80];
        string user = (access.CidUsuario ?? string.Empty).Trim().ToUpperInvariant();
        if (user.Length > 20) user = user[..20];

        int existingId = access.Id > 0
            ? access.Id
            : await conn.ExecuteScalarAsync<int>(
                "SELECT COALESCE(MAX(id), 0) FROM accesoforms WHERE UPPER(TRIM(nameform)) = @Name AND UPPER(TRIM(cidusuario)) = @User;",
                new { Name = name, User = user });

        if (existingId <= 0)
        {
            const string insSql = @"
                INSERT INTO accesoforms (tituloform, buscar, nuevo, editar, eliminar, imprimir, nameform, cidusuario)
                VALUES (@Titulo, @Buscar, @Nuevo, @Editar, @Eliminar, @Imprimir, @Name, @User);";
            await conn.ExecuteAsync(insSql, new
            {
                Titulo = title,
                Buscar = access.Buscar ? 1 : 0,
                Nuevo = access.Nuevo ? 1 : 0,
                Editar = access.Editar ? 1 : 0,
                Eliminar = access.Eliminar ? 1 : 0,
                Imprimir = access.Imprimir ? 1 : 0,
                Name = name,
                User = user
            });
        }
        else
        {
            const string updSql = @"
                UPDATE accesoforms SET
                    tituloform = @Titulo,
                    buscar = @Buscar,
                    nuevo = @Nuevo,
                    editar = @Editar,
                    eliminar = @Eliminar,
                    imprimir = @Imprimir
                WHERE id = @Id;";
            await conn.ExecuteAsync(updSql, new
            {
                Id = existingId,
                Titulo = title,
                Buscar = access.Buscar ? 1 : 0,
                Nuevo = access.Nuevo ? 1 : 0,
                Editar = access.Editar ? 1 : 0,
                Eliminar = access.Eliminar ? 1 : 0,
                Imprimir = access.Imprimir ? 1 : 0
            });
        }
    }

    public async Task LogUserTransactionAsync(
        string formTran, string tablaTran, string tipoTran, string sqlTran, int claveTran, string userId)
    {
        try
        {
            using var conn = _connectionFactory.CreateConnection();
            const string sql = @"
                INSERT INTO tbl_tranusu (form_tran, tabla_tran, tipo_tran, fecha_tran, sql_tran, clave_tran, userid_tran)
                VALUES (@Form, @Tabla, @Tipo, NOW(), @Sql, @Clave, @User);";
            string tForm = (formTran ?? string.Empty).Trim();
            string tTabla = (tablaTran ?? string.Empty).Trim();
            string tTipo = (tipoTran ?? "M").Trim();
            string tUser = string.IsNullOrWhiteSpace(userId) ? "ADMINISTRA" : userId.Trim();
            await conn.ExecuteAsync(sql, new
            {
                Form = tForm.Length > 30 ? tForm[..30] : tForm,
                Tabla = tTabla.Length > 30 ? tTabla[..30] : tTabla,
                Tipo = tTipo.Length > 1 ? tTipo[..1] : tTipo,
                Sql = sqlTran ?? string.Empty,
                Clave = claveTran,
                User = tUser.Length > 30 ? tUser[..30] : tUser
            });
        }
        catch
        {
            // Non-blocking audit log
        }
    }
}
