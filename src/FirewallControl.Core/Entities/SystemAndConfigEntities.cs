namespace FirewallControl.Core.Entities;

public class UserEntity
{
    public int Nlineno { get; set; }
    public string Cidgrupo { get; set; } = "ADMIN";
    public string Cidusuario { get; set; } = string.Empty;
    public string Cdescribe { get; set; } = string.Empty;
    public string Cpassword { get; set; } = string.Empty;
    public int Csuspendido { get; set; }
    public DateTime Fecha { get; set; } = DateTime.Now;
    public string Userid { get; set; } = string.Empty;
    public int IdSucursal { get; set; } = 1;
    public int TipoVendedor { get; set; } = 1;
    public int IdCaja { get; set; } = 1;
    public int IdVendedor { get; set; } = 10;
    public int IdDeposito { get; set; } = 1;
    public string EstadoDisplay => Csuspendido == 0 ? "Activo" : "Suspendido";
}

public class UserGroupEntity
{
    public int Nlineno { get; set; }
    public string Cidgrupo { get; set; } = "ADMIN";
    public string Cdescribe { get; set; } = string.Empty;
    public string Caccesos { get; set; } = string.Empty;
    public int Avisos { get; set; } = 1;
    public int Tool { get; set; } = 1;
    public string Cidusuario { get; set; } = string.Empty;
    public int BtnCompra { get; set; } = 1;
    public int BtnVenta { get; set; } = 1;
    public int BtnPedido { get; set; } = 1;
    public int BtnPresu { get; set; } = 1;
    public int BtnVenci { get; set; } = 1;
    public int BtnCliente { get; set; } = 1;
    public int BtnProveedor { get; set; } = 1;
    public int BtnVendedor { get; set; } = 1;
    public int BtnProducto { get; set; } = 1;
    public int BtnPago { get; set; } = 1;
    public int BtnCobro { get; set; } = 1;
    public int BtnMovCaja { get; set; } = 1;
}

public class CompanyDataEntity
{
    public int IdDatosEmpresa { get; set; }
    public string Descripcion { get; set; } = "SEFIROS POS";
    public string Ruc { get; set; } = string.Empty;
    public string Direccion { get; set; } = string.Empty;
    public string Telefonos { get; set; } = string.Empty;
    public string RutaFoto { get; set; } = string.Empty;
    public string CorreoPredeterminado { get; set; } = string.Empty;
    public decimal Utilidad1 { get; set; } = 35m;
    public decimal Utilidad2 { get; set; } = 45m;
    public decimal Utilidad3 { get; set; } = 60m;
    public decimal Utilidad4 { get; set; } = 80m;
    public decimal Utilidad5 { get; set; } = 100m;
    public decimal Utilidad6 { get; set; } = 120m;
    public decimal Utilidad7 { get; set; }
    public decimal InteresCuota { get; set; } = 3m;
    public decimal InteresPunitorio { get; set; } = 6m;
    public decimal Reclamos { get; set; } = 25000m;
    public decimal HonorarioJudicial { get; set; } = 10m;
    public int CobroCaja { get; set; }
    public int VentaItemsDeposito { get; set; }
    public int IdCliente { get; set; } = 132;
    public int IdVendedor { get; set; } = 10;
    public int CantVias { get; set; } = 1;
    public string ImpreNormal { get; set; } = string.Empty;
    public string ImpreBalanza { get; set; } = string.Empty;
    public string ImpreLegal { get; set; } = string.Empty;
    public int Veterinaria { get; set; }
    public int Comercial { get; set; } = 1;
    public int Restaurant { get; set; }
    public int HabilitaCobro { get; set; }
}

public class BranchEntity
{
    public int IdSucursal { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Dir { get; set; } = string.Empty;
    public string Tel { get; set; } = string.Empty;
    public string Cel { get; set; } = string.Empty;
    public string Enc { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class SellerEntity
{
    public int IdVendedor { get; set; }
    public string Nombres { get; set; } = string.Empty;
    public string Documento { get; set; } = string.Empty;
    public string Ruc { get; set; } = string.Empty;
    public string Telefonos { get; set; } = string.Empty;
    public string Celular { get; set; } = string.Empty;
    public string Direccion { get; set; } = string.Empty;
    public decimal Comision { get; set; }
    public decimal Descuento { get; set; }
    public int Estado { get; set; } = 1;
    public int TipoVendedor { get; set; }
}

public class CashRegisterEntity
{
    public int IdCaja { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public decimal Saldo { get; set; }
    public string Obs { get; set; } = string.Empty;
    public int IdSucursal { get; set; } = 1;
    public string CidUsuario { get; set; } = string.Empty;
    public string DesSucursal { get; set; } = string.Empty;
}

public class CashMovementEntity
{
    public int IdMovimientoCaja { get; set; }
    public int NroRubro { get; set; } = 1;
    public decimal Ingresos { get; set; }
    public decimal Egresos { get; set; }
    public string Tipo { get; set; } = "ENTRADA";
    public string Detalle { get; set; } = string.Empty;
    public DateTime FechaAlta { get; set; } = DateTime.Today;
    public string Hora { get; set; } = DateTime.Now.ToString("HH:mm:ss");
    public int Estado { get; set; } = 1;
    public int IdVentas { get; set; }
    public int IdCompras { get; set; }
    public int IdCaja { get; set; } = 1;
    public int IdMoneda { get; set; } = 1;
    public string CidUsuario { get; set; } = "ADMINISTRA";
    public string Obs { get; set; } = string.Empty;
}

public class CurrencyEntity
{
    public int IdMoneda { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public decimal Compra { get; set; } = 1m;
    public decimal Venta { get; set; } = 1m;
    public int Decimales { get; set; } = 2;
    public int Tronco { get; set; }
    public string Operacion { get; set; } = "*";
    public int Tipo { get; set; }
}

public class ExchangeRateEntity
{
    public int IdCotizacion { get; set; }
    public DateTime Fecha { get; set; } = DateTime.Today;
    public string Hora { get; set; } = DateTime.Now.ToString("HH:mm:ss");
    public decimal CompraDolar { get; set; }
    public decimal CompraGuarani { get; set; } = 1m;
    public decimal CompraReal { get; set; }
    public decimal CompraPeso { get; set; }
    public decimal CompraEuro { get; set; }
    public decimal VentaDolar { get; set; }
    public decimal VentaGuarani { get; set; } = 1m;
    public decimal VentaReal { get; set; }
    public decimal VentaPeso { get; set; }
    public decimal VentaEuro { get; set; }
    public int IdSucursal { get; set; } = 1;
    public string DesSucursal { get; set; } = string.Empty;
}

public class TimbradoLocalEntity
{
    public int IdTimbradoLocal { get; set; }
    public int IdSucursal { get; set; } = 1;
    public int IdCaja { get; set; } = 1;
    public string Timbrado { get; set; } = "12345678";
    public int N1 { get; set; } = 1;
    public int N2 { get; set; } = 1;
    public int N3 { get; set; } = 1;
    public string Factura { get; set; } = "001-001-0000001";
    public int Cantidad { get; set; } = 9999999;
    public DateTime Vencimiento { get; set; } = DateTime.Today.AddYears(1);
    public DateTime FechaInicio { get; set; } = DateTime.Today;

    public string FormatNextInvoice()
        => $"{N1:D3}-{N2:D3}-{N3:D7}";
}

public class MenuPromptItem
{
    public int RecNo { get; set; }
    public int Referencia { get; set; }
    public string PromptMenu { get; set; } = string.Empty;
    public string CleanTitle => PromptMenu.Replace("\\<", "_").Replace("<", "");
    public string OpcNumero { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;
    public List<MenuBarItem> Children { get; set; } = new();
}

public class MenuBarItem
{
    public int RecNo { get; set; }
    public string OpcMenu { get; set; } = string.Empty;
    public string CleanTitle => OpcMenu.Replace("\\<", "_").Replace("<", "");
    public bool Activo { get; set; } = true;
    public string OpcNumero { get; set; } = string.Empty;
    public int ParentNu { get; set; }
    public bool Separador { get; set; }
    public string Img { get; set; } = string.Empty;
    public string Comando { get; set; } = string.Empty;
    public bool SubMenu { get; set; }
    public string ParentMenu { get; set; } = string.Empty;
    public List<MenuBarItem> SubItems { get; set; } = new();
}

public class GenesButtonItem
{
    private string _icono = string.Empty;

    public int Id { get; set; }
    public int GenId { get; set; }
    public int BotonId { get; set; }
    public int Orden
    {
        get => BotonId;
        set => BotonId = value;
    }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Tipo { get; set; } = "KG"; // "KG", "UNIDAD", "DOCENA"
    public decimal Precio { get; set; }
    public decimal Costo { get; set; }
    public bool Actualizar { get; set; } = true;
    public bool SyncSql { get; set; } = true;

    public string Icono
    {
        get => _icono;
        set => _icono = value ?? string.Empty;
    }

    public string Imagen
    {
        get => _icono;
        set => _icono = value ?? string.Empty;
    }

    public string IconoResolved { get; set; } = string.Empty;

    public string PrecioFormatted => Precio == 1m
        ? "Monto Libre (Gs)"
        : Tipo.ToUpperInvariant() switch
        {
            "KG" => $"{Precio:N0} Gs/Kg",
            "DOCENA" => $"{Precio:N0} Gs/Doc",
            _ => $"{Precio:N0} Gs c/u"
        };
}

public record GenesSelectionResult(
    GenesButtonItem Product,
    string EffectiveTipo,
    decimal EffectivePrecioBase,
    decimal EnteredValue);

public class FirewallConfig
{
    public string BaseDirectory { get; set; } = string.Empty;
    public string NameMenu { get; set; } = "Office2003";
    public string Skin { get; set; } = "Style1";
    public string Agente { get; set; } = "(Ninguno)";
    public int GuardaSesion { get; set; } = 1;
    public string RutaServer { get; set; } = "localhost";
    public string ConfigFile { get; set; } = "Firewall.sql";
    public string BaseDatos { get; set; } = "firewallcontrol";
    public int ServerType { get; set; } = 1; // 1 = MySQL
    public int Port { get; set; } = 3306;
    public string SqlServerHost { get; set; } = "localhost";
    public string SqlUser { get; set; } = "root";
    public string SqlPassword { get; set; } = string.Empty;

    // [Local] section
    public string Timbrado { get; set; } = "12345678";
    public decimal DescuentoMaximo { get; set; } = 100m;
    public int AnulacionVenta { get; set; } = 1;
    public string MonedaFactura { get; set; } = "GUARANI";
    public int IdMonedaFactura { get; set; } = 1;
    public int ListaPrecios { get; set; } = 1;
    public int IdSucursal { get; set; } = 1;
    public int IdCaja { get; set; } = 1;
    public int IdDeposito { get; set; } = 1;
    public int IdVendedor { get; set; } = 10;
    public int IdClienteDefault { get; set; } = 132;
    public int Balanza { get; set; }
    public int ImpresionBalanza { get; set; }
    public int ImpresionNormal { get; set; }
    public int EntregaMercaderia { get; set; }
    public int MostrarVencimiento { get; set; }

    // [FastCarga] section (Carga Rápida de Productos + Gemini Files API)
    public string FastCargaApiKey { get; set; } = string.Empty;
    public string FastCargaApiModel { get; set; } = "gemini-3.6-flash";
    public string FastCargaApiModelsCatalog { get; set; } = string.Empty;
}

public class AppPreferencesEntity
{
    /// <summary>
    /// Opción Global: No controlar stock en TODOS los ítems del sistema.
    /// Si está activo (true), ningún producto bloquea ni rechaza la venta por falta de stock.
    /// Por defecto: false (desactivado).
    /// </summary>
    public bool NoControlarStockGlobal { get; set; } = false;

    /// <summary>
    /// Descontar stock en depósito aunque el control de stock esté desactivado. Por defecto: true.
    /// </summary>
    public bool DescontarStockAunqueNoControle { get; set; } = true;

    /// <summary>
    /// Permitir facturar un ítem con precio menor a su costo. Por defecto: false.
    /// </summary>
    public bool PermitirVentaBajoCosto { get; set; } = false;

    /// <summary>
    /// Límite máximo de unidades por línea en ITEMSFACTURAVENTAS (0 = sin límite). Por defecto: 120.
    /// </summary>
    public decimal LimiteCantidadPorItem { get; set; } = 120m;

    /// <summary>
    /// Validar límites mínimo y máximo de precio unitario en ITEMSFACTURAVENTAS. Por defecto: true.
    /// </summary>
    public bool ValidarLimitesPrecioEnItem { get; set; } = true;

    /// <summary>
    /// Precio unitario mínimo permitido en ITEMSFACTURAVENTAS (0 = sin mínimo). Por defecto: 99.
    /// </summary>
    public decimal PrecioMinimoVenta { get; set; } = 99m;

    /// <summary>
    /// Precio unitario máximo permitido en ITEMSFACTURAVENTAS (0 = sin máximo). Por defecto: 1000000.
    /// </summary>
    public decimal PrecioMaximoVenta { get; set; } = 1000000m;

    /// <summary>
    /// Congelar inventario en productos sin control de stock (evita descuentos y valores negativos). Por defecto: true.
    /// </summary>
    public bool CongelarStockSinControl { get; set; } = true;

    /// <summary>
    /// Mantener ITEMSFACTURAVENTAS en modo Modal 1 (bloquea interacción con las ventanas de atrás mientras esté abierto). Por defecto: false.
    /// </summary>
    public bool ItemFacturaModal { get; set; } = false;

    /// <summary>
    /// Preferencia 1: En ITEMSFACTURAVENTAS, si te equivocas en cantidad o precio,
    /// borrar todo (código, descripción, cantidad, precio) y volver a codproducto para iniciar de nuevo.
    /// Por defecto: false (desactivado).
    /// </summary>
    public bool ReiniciarItemAlEquivocarseEnItemFactura { get; set; } = false;

    /// <summary>
    /// Iniciar ITEMSFACTURAVENTAS con Movimiento Rápido (1 u.) activo al abrir el sistema. Por defecto: false.
    /// </summary>
    public bool IniciarMovimientoRapidoPorDefecto { get; set; } = false;

    /// <summary>
    /// Reproducir alerta sonora cuando no se encuentra un producto o falla una validación. Por defecto: true.
    /// </summary>
    public bool SonidoAlertaEnErrores { get; set; } = true;

    /// <summary>
    /// Sincronizar automáticamente precios de Genes con MySQL al abrir la botonera. Por defecto: true.
    /// </summary>
    public bool SincronizarGenesAlAbrir { get; set; } = true;

    /// <summary>
    /// Cantidad de copias a imprimir por cada ticket (Z / Ctrl+P). Por defecto: 1 (una sola nota compacta).
    /// </summary>
    public int CopiasTicket { get; set; } = 1;

    /// <summary>
    /// Líneas en blanco de avance antes del corte de papel. Por defecto: 2 (evita tickets largos).
    /// </summary>
    public int LineasAvanceCorte { get; set; } = 2;

    /// <summary>
    /// Enviar comando ESC/POS de corte automático al finalizar el ticket. Por defecto: true.
    /// </summary>
    public bool CortarPapelAutomatico { get; set; } = true;

    /// <summary>
    /// Enviar pulso de apertura de cajón de dinero al cobrar/imprimir. Por defecto: true.
    /// </summary>
    public bool AbrirCajonConTicket { get; set; } = true;

    /// <summary>
    /// Usar impresión RAW ESC/POS directa sin salto de página GDI (evita espacios largos en blanco). Por defecto: true.
    /// </summary>
    public bool UsarImpresionRawDirecta { get; set; } = true;

    /// <summary>
    /// Multiconexión en tiempo real entre PCs (sincroniza ventas, compras, stock y caja automáticamente). Por defecto: true.
    /// </summary>
    public bool MulticonexionTiempoRealActiva { get; set; } = true;

    /// <summary>
    /// Intervalo en segundos para sincronizar cambios entre terminales conectadas en red. Por defecto: 3.
    /// </summary>
    public int IntervaloSincronizacionSegundos { get; set; } = 3;

    /// <summary>
    /// Solicitar ventana de usuario y contraseña (ClaveIn) al iniciar el programa. Por defecto: false.
    /// </summary>
    public bool SolicitarLoginAlIniciar { get; set; } = false;

    /// <summary>
    /// Paleta visual del sistema (neutro, sefiros, ocean, emerald, rose, indigo, sand, atelier).
    /// Por defecto: "sefiros" (Tema Sefiros oficial).
    /// </summary>
    public string TemaAtelier { get; set; } = "sefiros";

    /// <summary>
    /// Tema visual oscuro en la aplicación. Por defecto: false (modo claro).
    /// </summary>
    public bool TemaOscuroConfiguracion { get; set; } = false;

    /// <summary>
    /// Estilo visual de trabajo de ABMVENTAS e ITEMSFACTURAVENTAS: "Renovado" (C#) o "Clasico" (Visual FoxPro / Sufer POS).
    /// Por defecto: "Renovado".
    /// </summary>
    public string EstiloPos { get; set; } = "Renovado";

    /// <summary>
    /// Paleta de colores independiente del diseño POS: "Moderna" (Atelier C#) o "ClasicaFox" (Visual FoxPro / Sufer POS).
    /// Por defecto: "Moderna".
    /// </summary>
    public string PaletaColores { get; set; } = "Moderna";

    /// <summary>
    /// Código ISO de la moneda base activa para visualización e impresión (ej. "PYG", "USD", "BRL", "ARS", "MXN").
    /// Por defecto: "PYG" (Guaraní paraguayo).
    /// </summary>
    public string MonedaBaseIso { get; set; } = "PYG";

    /// <summary>
    /// Idioma de la interfaz de usuario ("es" = Español, "pt" = Português, "en" = English).
    /// Por defecto: "es".
    /// </summary>
    public string Idioma { get; set; } = "es";

    /// <summary>
    /// Mapa de atajos personalizados por ActionId (ej. "IFV_MOVIMIENTO_RAPIDO" -> "F10").
    /// </summary>
    public Dictionary<string, string> CustomShortcuts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public class ShortcutBindingItem
{
    public string ActionId { get; set; } = string.Empty;
    public string WindowKey { get; set; } = string.Empty;
    public string WindowTitle { get; set; } = string.Empty;
    public string ActionCode { get; set; } = string.Empty;
    public string ActionName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string DefaultKey { get; set; } = string.Empty;
    public string CurrentKey { get; set; } = string.Empty;
    public bool RequiresEmptyInput { get; set; }
}

public static class ShortcutCatalog
{
    public const string WinItemsFactura = "ITEMSFACTURAVENTAS";
    public const string WinAbmVentas = "ABM_VENTAS";
    public const string WinGenes = "GENES";
    public const string WinFondoInicio = "FONDO_INICIO";

    private static readonly List<ShortcutBindingItem> DefaultBindings = new()
    {
        // 1. ITEMSFACTURAVENTAS (Selector de Productos)
        new() { ActionId = "IFV_CAJA_1", WindowKey = WinItemsFactura, WindowTitle = "Selector de Productos (ITEMSFACTURAVENTAS)", ActionCode = "F1", ActionName = "Saltar a Caja 1", Description = "Vincula el selector y activa la Caja 1", DefaultKey = "F1", RequiresEmptyInput = false },
        new() { ActionId = "IFV_CAJA_2", WindowKey = WinItemsFactura, WindowTitle = "Selector de Productos (ITEMSFACTURAVENTAS)", ActionCode = "F2", ActionName = "Saltar a Caja 2", Description = "Vincula el selector y activa la Caja 2", DefaultKey = "F2", RequiresEmptyInput = false },
        new() { ActionId = "IFV_CAJA_3", WindowKey = WinItemsFactura, WindowTitle = "Selector de Productos (ITEMSFACTURAVENTAS)", ActionCode = "F3_CAJA", ActionName = "Saltar a Caja 3", Description = "Vincula el selector y activa la Caja 3", DefaultKey = "F3", RequiresEmptyInput = false },
        new() { ActionId = "IFV_CAJA_4", WindowKey = WinItemsFactura, WindowTitle = "Selector de Productos (ITEMSFACTURAVENTAS)", ActionCode = "F4", ActionName = "Saltar a Caja 4", Description = "Vincula el selector y activa la Caja 4", DefaultKey = "F4", RequiresEmptyInput = false },
        new() { ActionId = "IFV_CAJA_5", WindowKey = WinItemsFactura, WindowTitle = "Selector de Productos (ITEMSFACTURAVENTAS)", ActionCode = "F5", ActionName = "Saltar a Caja 5", Description = "Vincula el selector y activa la Caja 5", DefaultKey = "F5", RequiresEmptyInput = false },
        new() { ActionId = "IFV_BUSCAR_CATALOGO", WindowKey = WinItemsFactura, WindowTitle = "Selector de Productos (ITEMSFACTURAVENTAS)", ActionCode = "BUSCAR_CATALOGO", ActionName = "Buscar en Catálogo de Productos", Description = "Abre el buscador general de productos y stock", DefaultKey = "F6", RequiresEmptyInput = false },
        new() { ActionId = "IFV_MOVIMIENTO_RAPIDO", WindowKey = WinItemsFactura, WindowTitle = "Selector de Productos (ITEMSFACTURAVENTAS)", ActionCode = "F10", ActionName = "Activar / Desactivar Movimiento Rápido", Description = "Pasa 1 unidad directo al escanear y fusiona repetidos", DefaultKey = "F10", RequiresEmptyInput = false },
        new() { ActionId = "IFV_MODO_BORRADO", WindowKey = WinItemsFactura, WindowTitle = "Selector de Productos (ITEMSFACTURAVENTAS)", ActionCode = "DEL", ActionName = "Borrar Ítem de la Grilla", Description = "Oculta selector, permite elegir ítem a borrar y vuelve", DefaultKey = "DEL", RequiresEmptyInput = true },
        new() { ActionId = "IFV_GEN_1", WindowKey = WinItemsFactura, WindowTitle = "Selector de Productos (ITEMSFACTURAVENTAS)", ActionCode = "A", ActionName = "Abrir Gen 1 (Verdulería)", Description = "Abre la botonera rápida de Verdulería", DefaultKey = "A", RequiresEmptyInput = false },
        new() { ActionId = "IFV_GEN_2", WindowKey = WinItemsFactura, WindowTitle = "Selector de Productos (ITEMSFACTURAVENTAS)", ActionCode = "S", ActionName = "Abrir Gen 2 (Carnicería)", Description = "Abre la botonera rápida de Carnicería", DefaultKey = "S", RequiresEmptyInput = false },
        new() { ActionId = "IFV_GEN_3", WindowKey = WinItemsFactura, WindowTitle = "Selector de Productos (ITEMSFACTURAVENTAS)", ActionCode = "D", ActionName = "Abrir Gen 3 (Panadería)", Description = "Abre la botonera rápida de Panadería", DefaultKey = "D", RequiresEmptyInput = false },
        new() { ActionId = "IFV_GEN_4", WindowKey = WinItemsFactura, WindowTitle = "Selector de Productos (ITEMSFACTURAVENTAS)", ActionCode = "F", ActionName = "Abrir Gen 4 (Almacén)", Description = "Abre la botonera rápida de Almacén", DefaultKey = "F", RequiresEmptyInput = false },
        new() { ActionId = "IFV_COBRAR_TICKET", WindowKey = WinItemsFactura, WindowTitle = "Selector de Productos (ITEMSFACTURAVENTAS)", ActionCode = "Z", ActionName = "Cobrar e Imprimir Ticket", Description = "Procesa el cobro contado e imprime el ticket", DefaultKey = "Z", RequiresEmptyInput = true },
        new() { ActionId = "IFV_COBRAR_SIN_TICKET", WindowKey = WinItemsFactura, WindowTitle = "Selector de Productos (ITEMSFACTURAVENTAS)", ActionCode = "X", ActionName = "Cobrar sin Imprimir Ticket", Description = "Procesa el cobro contado sin imprimir", DefaultKey = "X", RequiresEmptyInput = true },
        new() { ActionId = "IFV_CAJON_DINERO", WindowKey = WinItemsFactura, WindowTitle = "Selector de Productos (ITEMSFACTURAVENTAS)", ActionCode = "H", ActionName = "Abrir Cajón / Cobro Rápido", Description = "Activa apertura de cajón de dinero", DefaultKey = "H", RequiresEmptyInput = true },
        new() { ActionId = "IFV_REEDITAR_VENTA", WindowKey = WinItemsFactura, WindowTitle = "Selector de Productos (ITEMSFACTURAVENTAS)", ActionCode = "CTRL+X", ActionName = "Reeditar Venta Anterior", Description = "Abre selector para reeditar una venta cerrada", DefaultKey = "CTRL+X", RequiresEmptyInput = false },
        new() { ActionId = "IFV_REIMPRIMIR_VENTA", WindowKey = WinItemsFactura, WindowTitle = "Selector de Productos (ITEMSFACTURAVENTAS)", ActionCode = "CTRL+Z", ActionName = "Reimprimir Venta Anterior", Description = "Abre selector para reimprimir comprobante", DefaultKey = "CTRL+Z", RequiresEmptyInput = false },
        new() { ActionId = "IFV_GEN_1_NUEVO", WindowKey = WinItemsFactura, WindowTitle = "Selector de Productos (ITEMSFACTURAVENTAS)", ActionCode = "CTRL+A", ActionName = "Nuevo Producto en Gen 1", Description = "Abre Gen 1 directamente en modo Agregar Producto", DefaultKey = "CTRL+A", RequiresEmptyInput = false },
        new() { ActionId = "IFV_GEN_2_NUEVO", WindowKey = WinItemsFactura, WindowTitle = "Selector de Productos (ITEMSFACTURAVENTAS)", ActionCode = "CTRL+S", ActionName = "Nuevo Producto en Gen 2", Description = "Abre Gen 2 directamente en modo Agregar Producto", DefaultKey = "CTRL+S", RequiresEmptyInput = false },
        new() { ActionId = "IFV_GEN_3_NUEVO", WindowKey = WinItemsFactura, WindowTitle = "Selector de Productos (ITEMSFACTURAVENTAS)", ActionCode = "CTRL+D", ActionName = "Nuevo Producto en Gen 3", Description = "Abre Gen 3 directamente en modo Agregar Producto", DefaultKey = "CTRL+D", RequiresEmptyInput = false },
        new() { ActionId = "IFV_GEN_4_NUEVO", WindowKey = WinItemsFactura, WindowTitle = "Selector de Productos (ITEMSFACTURAVENTAS)", ActionCode = "CTRL+F", ActionName = "Nuevo Producto en Gen 4", Description = "Abre Gen 4 directamente en modo Agregar Producto", DefaultKey = "CTRL+F", RequiresEmptyInput = false },
        new() { ActionId = "IFV_GEN_1_EDITAR", WindowKey = WinItemsFactura, WindowTitle = "Selector de Productos (ITEMSFACTURAVENTAS)", ActionCode = "ALT+A", ActionName = "Editar Producto en Gen 1", Description = "Abre Gen 1 directamente en modo Editar Producto", DefaultKey = "ALT+A", RequiresEmptyInput = false },
        new() { ActionId = "IFV_GEN_2_EDITAR", WindowKey = WinItemsFactura, WindowTitle = "Selector de Productos (ITEMSFACTURAVENTAS)", ActionCode = "ALT+S", ActionName = "Editar Producto en Gen 2", Description = "Abre Gen 2 directamente en modo Editar Producto", DefaultKey = "ALT+S", RequiresEmptyInput = false },
        new() { ActionId = "IFV_GEN_3_EDITAR", WindowKey = WinItemsFactura, WindowTitle = "Selector de Productos (ITEMSFACTURAVENTAS)", ActionCode = "ALT+D", ActionName = "Editar Producto en Gen 3", Description = "Abre Gen 3 directamente en modo Editar Producto", DefaultKey = "ALT+D", RequiresEmptyInput = false },
        new() { ActionId = "IFV_GEN_4_EDITAR", WindowKey = WinItemsFactura, WindowTitle = "Selector de Productos (ITEMSFACTURAVENTAS)", ActionCode = "ALT+F", ActionName = "Editar Producto en Gen 4", Description = "Abre Gen 4 directamente en modo Editar Producto", DefaultKey = "ALT+F", RequiresEmptyInput = false },
        new() { ActionId = "IFV_CARGA_RAPIDA", WindowKey = WinItemsFactura, WindowTitle = "Selector de Productos (ITEMSFACTURAVENTAS)", ActionCode = "CARGA_RAPIDA", ActionName = "Abrir Carga Rápida de Productos (IA)", Description = "Abre la Carga Rápida con Gemini Files API y cola en segundo plano (mismo F9 de FOX)", DefaultKey = "F9", RequiresEmptyInput = false },
        new() { ActionId = "IFV_PRESUPUESTOS", WindowKey = WinItemsFactura, WindowTitle = "Selector de Productos (ITEMSFACTURAVENTAS)", ActionCode = "PRESUPUESTOS", ActionName = "Importar Presupuesto", Description = "Busca e importa un presupuesto a la venta actual", DefaultKey = "F7", RequiresEmptyInput = false },
        new() { ActionId = "IFV_CALCULADORA", WindowKey = WinItemsFactura, WindowTitle = "Selector de Productos (ITEMSFACTURAVENTAS)", ActionCode = "CALCULADORA", ActionName = "Abrir Calculadora [C]", Description = "Abre la calculadora auxiliar Sufer POS", DefaultKey = "C", RequiresEmptyInput = true },

        // 2. ABM_VENTAS (Punto de Venta Autoservice)
        new() { ActionId = "ABM_ABRIR_SELECTOR", WindowKey = WinAbmVentas, WindowTitle = "Punto de Venta (ABM_VENTAS_AUTOSERVICE)", ActionCode = "ABRIR_SELECTOR", ActionName = "Abrir Selector de Productos", Description = "Muestra y activa la ventana ITEMSFACTURAVENTAS", DefaultKey = "F2", RequiresEmptyInput = false },
        new() { ActionId = "ABM_MODIFICAR_ITEM", WindowKey = WinAbmVentas, WindowTitle = "Punto de Venta (ABM_VENTAS_AUTOSERVICE)", ActionCode = "MODIFICAR_ITEM", ActionName = "Modificar Ítem Seleccionado", Description = "Abre ITEMSFACTURAVENTAS en modo modificar para la fila seleccionada", DefaultKey = "F4", RequiresEmptyInput = false },
        new() { ActionId = "ABM_COBRAR_SIN_TICKET", WindowKey = WinAbmVentas, WindowTitle = "Punto de Venta (ABM_VENTAS_AUTOSERVICE)", ActionCode = "COBRAR_SIN_TICKET", ActionName = "Cobrar Venta sin Imprimir", Description = "Procesa la venta actual sin imprimir ticket", DefaultKey = "F9", RequiresEmptyInput = false },
        new() { ActionId = "ABM_COBRAR_TICKET", WindowKey = WinAbmVentas, WindowTitle = "Punto de Venta (ABM_VENTAS_AUTOSERVICE)", ActionCode = "COBRAR_TICKET", ActionName = "Cobrar e Imprimir Ticket", Description = "Procesa la venta actual e imprime ticket", DefaultKey = "CTRL+P", RequiresEmptyInput = false },
        new() { ActionId = "ABM_REEDITAR_VENTA", WindowKey = WinAbmVentas, WindowTitle = "Punto de Venta (ABM_VENTAS_AUTOSERVICE)", ActionCode = "REEDITAR_VENTA", ActionName = "Reeditar Venta Anterior", Description = "Permite buscar y modificar una factura anterior", DefaultKey = "CTRL+X", RequiresEmptyInput = false },
        new() { ActionId = "ABM_REIMPRIMIR_VENTA", WindowKey = WinAbmVentas, WindowTitle = "Punto de Venta (ABM_VENTAS_AUTOSERVICE)", ActionCode = "REIMPRIMIR_VENTA", ActionName = "Reimprimir Venta Anterior", Description = "Permite buscar y reimprimir una factura anterior", DefaultKey = "CTRL+Z", RequiresEmptyInput = false },
        new() { ActionId = "ABM_BORRAR_ITEM", WindowKey = WinAbmVentas, WindowTitle = "Punto de Venta (ABM_VENTAS_AUTOSERVICE)", ActionCode = "BORRAR_ITEM", ActionName = "Eliminar Ítem Seleccionado", Description = "Borra el producto seleccionado de la grilla de ventas", DefaultKey = "DEL", RequiresEmptyInput = false },
        new() { ActionId = "ABM_CALCULADORA", WindowKey = WinAbmVentas, WindowTitle = "Punto de Venta (ABM_VENTAS_AUTOSERVICE)", ActionCode = "CALCULADORA", ActionName = "Abrir Calculadora", Description = "Abre la calculadora auxiliar Sufer POS", DefaultKey = "C", RequiresEmptyInput = true },

        // 3. GENES (Botonera de Departamentos)
        new() { ActionId = "GEN_CAMBIAR_1", WindowKey = WinGenes, WindowTitle = "Botonera Genes (Verdulería / Carnicería / Panadería / Almacén)", ActionCode = "GEN_1", ActionName = "Cambiar a Gen 1 (Verdulería)", Description = "Cambia instantáneamente a Verdulería sin cerrar la ventana", DefaultKey = "A", RequiresEmptyInput = false },
        new() { ActionId = "GEN_CAMBIAR_2", WindowKey = WinGenes, WindowTitle = "Botonera Genes (Verdulería / Carnicería / Panadería / Almacén)", ActionCode = "GEN_2", ActionName = "Cambiar a Gen 2 (Carnicería)", Description = "Cambia instantáneamente a Carnicería sin cerrar la ventana", DefaultKey = "S", RequiresEmptyInput = false },
        new() { ActionId = "GEN_CAMBIAR_3", WindowKey = WinGenes, WindowTitle = "Botonera Genes (Verdulería / Carnicería / Panadería / Almacén)", ActionCode = "GEN_3", ActionName = "Cambiar a Gen 3 (Panadería)", Description = "Cambia instantáneamente a Panadería sin cerrar la ventana", DefaultKey = "D", RequiresEmptyInput = false },
        new() { ActionId = "GEN_CAMBIAR_4", WindowKey = WinGenes, WindowTitle = "Botonera Genes (Verdulería / Carnicería / Panadería / Almacén)", ActionCode = "GEN_4", ActionName = "Cambiar a Gen 4 (Almacén)", Description = "Cambia instantáneamente a Almacén sin cerrar la ventana", DefaultKey = "F", RequiresEmptyInput = false },
        new() { ActionId = "GEN_ACTUALIZAR_SQL", WindowKey = WinGenes, WindowTitle = "Botonera Genes (Verdulería / Carnicería / Panadería / Almacén)", ActionCode = "SYNC_SQL", ActionName = "Actualizar Precios desde MySQL", Description = "Sincroniza precios y costos con la tabla productos de MySQL", DefaultKey = "F5", RequiresEmptyInput = false },
        new() { ActionId = "GEN_ELIMINAR_PRODUCTO", WindowKey = WinGenes, WindowTitle = "Botonera Genes (Verdulería / Carnicería / Panadería / Almacén)", ActionCode = "DEL_PROD", ActionName = "Eliminar Producto de Genes", Description = "Elimina el cuadro seleccionado en la botonera Genes", DefaultKey = "DEL", RequiresEmptyInput = false },

        // 4. FONDO_INICIO (Ventana Principal _fondo)
        new() { ActionId = "MAIN_CAJA_1", WindowKey = WinFondoInicio, WindowTitle = "Ventana Principal de Inicio (_FONDO)", ActionCode = "CAJA_1", ActionName = "Abrir Punto de Venta (Caja 1)", Description = "Abre o activa la ventana de ventas Caja 1", DefaultKey = "F1", RequiresEmptyInput = false },
        new() { ActionId = "MAIN_SELECTOR", WindowKey = WinFondoInicio, WindowTitle = "Ventana Principal de Inicio (_FONDO)", ActionCode = "SELECTOR", ActionName = "Abrir Selector de Productos", Description = "Abre o activa ITEMSFACTURAVENTAS", DefaultKey = "F2", RequiresEmptyInput = false },
        new() { ActionId = "MAIN_PRODUCTOS", WindowKey = WinFondoInicio, WindowTitle = "Ventana Principal de Inicio (_FONDO)", ActionCode = "PRODUCTOS", ActionName = "Abrir Catálogo de Productos y Stock", Description = "Abre el módulo de Productos e Inventario", DefaultKey = "F3", RequiresEmptyInput = false },
        new() { ActionId = "MAIN_CONFIGURACION", WindowKey = WinFondoInicio, WindowTitle = "Ventana Principal de Inicio (_FONDO)", ActionCode = "CONFIGURACION", ActionName = "Abrir Configuración (Asignar Teclas y Preferencias)", Description = "Abre la ventana de Asignar Teclas y Preferencias", DefaultKey = "F4", RequiresEmptyInput = false },
        new() { ActionId = "MAIN_CAMBIAR_USUARIO", WindowKey = WinFondoInicio, WindowTitle = "Ventana Principal de Inicio (_FONDO)", ActionCode = "CAMBIAR_USUARIO", ActionName = "Cambiar Usuario Activo", Description = "Abre el diálogo de inicio de sesión de usuario", DefaultKey = "F12", RequiresEmptyInput = false },
        new() { ActionId = "FONDO_NUEVA_VENTA", WindowKey = WinFondoInicio, WindowTitle = "Ventana Principal de Inicio (_FONDO)", ActionCode = "NUEVA_VENTA", ActionName = "Nueva Venta (_FONDO)", Description = "Atajo clásico _FONDO Ctrl+V para abrir venta contado + selector", DefaultKey = "CTRL+V", RequiresEmptyInput = false },
        new() { ActionId = "FONDO_NUEVA_COMPRA", WindowKey = WinFondoInicio, WindowTitle = "Ventana Principal de Inicio (_FONDO)", ActionCode = "NUEVA_COMPRA", ActionName = "Nueva Compra (_FONDO)", Description = "Atajo clásico _FONDO Ctrl+C para abrir módulo de compras", DefaultKey = "CTRL+C", RequiresEmptyInput = false },
        new() { ActionId = "FONDO_PEDIDOS", WindowKey = WinFondoInicio, WindowTitle = "Ventana Principal de Inicio (_FONDO)", ActionCode = "PEDIDOS", ActionName = "Pedidos de Compra (_FONDO)", Description = "Atajo clásico _FONDO Ctrl+P para pedidos a proveedores", DefaultKey = "CTRL+P", RequiresEmptyInput = false },
        new() { ActionId = "FONDO_PRESUPUESTOS", WindowKey = WinFondoInicio, WindowTitle = "Ventana Principal de Inicio (_FONDO)", ActionCode = "PRESUPUESTOS", ActionName = "Presupuestos (_FONDO)", Description = "Atajo clásico _FONDO Ctrl+B para abrir gestión de presupuestos", DefaultKey = "CTRL+B", RequiresEmptyInput = false }
    };

    public static List<ShortcutBindingItem> GetBindingsForWindow(AppPreferencesEntity prefs, string windowKey)
    {
        return DefaultBindings
            .Where(b => string.Equals(b.WindowKey, windowKey, StringComparison.OrdinalIgnoreCase))
            .Select(b => new ShortcutBindingItem
            {
                ActionId = b.ActionId,
                WindowKey = b.WindowKey,
                WindowTitle = b.WindowTitle,
                ActionCode = b.ActionCode,
                ActionName = b.ActionName,
                Description = b.Description,
                DefaultKey = b.DefaultKey,
                CurrentKey = GetEffectiveKey(prefs, b.ActionId, b.DefaultKey),
                RequiresEmptyInput = b.RequiresEmptyInput
            })
            .ToList();
    }

    public static string GetEffectiveKey(AppPreferencesEntity prefs, string actionId, string defaultKey)
    {
        if (prefs.CustomShortcuts != null &&
            prefs.CustomShortcuts.TryGetValue(actionId, out string? custom) &&
            !string.IsNullOrWhiteSpace(custom))
        {
            return custom.Trim().ToUpperInvariant();
        }
        return defaultKey.ToUpperInvariant();
    }

    public static ShortcutBindingItem? ResolveTriggeredBinding(
        AppPreferencesEntity prefs,
        string windowKey,
        string normalizedKeyCombo,
        bool isInputEmpty)
    {
        if (string.IsNullOrWhiteSpace(normalizedKeyCombo))
            return null;

        var bindings = GetBindingsForWindow(prefs, windowKey);
        foreach (var b in bindings)
        {
            if (string.Equals(b.CurrentKey, normalizedKeyCombo, StringComparison.OrdinalIgnoreCase))
            {
                if (b.RequiresEmptyInput && !isInputEmpty)
                    continue;
                return b;
            }
        }
        return null;
    }
}
