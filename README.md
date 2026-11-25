# Sefiros POS — Sistema Comercial y Punto de Venta (.NET 10 WPF)

![.NET 10](https://img.shields.io/badge/.NET-10.0--windows-512BD4?style=flat-square)
![UI](https://img.shields.io/badge/UI-WPF%20MVVM-2563EB?style=flat-square)
![Database](https://img.shields.io/badge/Database-MySQL%20%2B%20Dapper-0E9F6E?style=flat-square)
![Currency](https://img.shields.io/badge/Currencies-20%20LATAM%20%2B%20EUR-D97706?style=flat-square)
![Languages](https://img.shields.io/badge/Languages-ES%20%7C%20PT%20%7C%20EN-E11D48?style=flat-square)
![License](https://img.shields.io/badge/License-MIT-181C24?style=flat-square)

**Sefiros POS** es un sistema moderno de **Punto de Venta (POS) y Gestión Comercial ERP** construido íntegramente en **C# / .NET 10 (WPF + MVVM)**, diseñado para alto volumen de facturación por teclado o lector de códigos de barras, operación multicaja en tiempo real y soporte completo para comercios de **Latinoamérica**.

> **Origen e inspiración / Attribution:** Este repositorio contiene **exclusivamente la implementación moderna en C# `.NET 10`**. Sus reglas de negocio fiscales, cálculos exactos de facturación, flujos ultrarrápidos de caja (`ABMVENTAS`, `ITEMSFACTURAVENTAS`), botoneras pesables (`Genes`) y atajos operativos fueron inspirados y migrados a partir del sistema heredado **Visual FoxPro 9 (`Sufer POS / FireWall Control`)**.

---

## Características Principales

### 1. Punto de Venta Multicaja de Alto Rendimiento
- **5 Cajas simultáneas (`F1`–`F5`)** con carritos independientes por caja.
- **Doble diseño intercambiable en caliente (sin perder ítems):**
  - **Renovado (C# Atelier):** ventanas en cascada, cuadro en vivo de *Stock Actual* y selector ampliado.
  - **Clásico (Sufer POS / FOX):** distribución tradicional optimizada para cajeros rápidos.
- **Guardián de foco para escáner de barras** y soporte de **Movimiento Rápido (`F10`)** + productos fraccionables por peso (`KG`) a 3–6 decimales.
- **Botón rápido por departamentos (Genes `A/S/D/F`)** para verdulería, carnicería, panadería y almacén.
- **Modo Modal 1 nativo Win32** (bloqueo de fondo sin volver grises las ventanas).

### 2. Todas las Monedas de Latinoamérica (`20 LATAM + EUR`)
Desde **⚙ Ajustes [F4] → Apariencia → Región, idioma y moneda base** puedes operar con cualquiera de las **20 monedas latinoamericanas + Euro**:

| Código | Moneda | Símbolo | País / Región |
| :--- | :--- | :---: | :--- |
| `PYG` | Guaraní Paraguayo *(por defecto de fábrica)* | `Gs.` | Paraguay |
| `ARS` | Peso Argentino | `$` | Argentina |
| `BOB` | Boliviano | `Bs` | Bolivia |
| `BRL` | Real Brasileño | `R$` | Brasil |
| `CLP` | Peso Chileno | `$` | Chile |
| `COP` | Peso Colombiano | `$` | Colombia |
| `CRC` | Colón Costarricense | `₡` | Costa Rica |
| `CUP` | Peso Cubano | `$` | Cuba |
| `DOP` | Peso Dominicano | `RD$` | Rep. Dominicana |
| `USD` | Dólar Estadounidense | `US$` | Ecuador / El Salvador / Panamá / Regional |
| `GTQ` | Quetzal Guatemalteco | `Q` | Guatemala |
| `HNL` | Lempira Hondureño | `L` | Honduras |
| `HTG` | Gourde Haitiano | `G` | Haití |
| `MXN` | Peso Mexicano | `$` | México |
| `NIO` | Córdoba Nicaragüense | `C$` | Nicaragua |
| `PAB` | Balboa Panameño | `B/.` | Panamá |
| `PEN` | Sol Peruano | `S/` | Perú |
| `UYU` | Peso Uruguayo | `$U` | Uruguay |
| `VES` | Bolívar Venezolano | `Bs.` | Venezuela |
| `BZD` | Dólar Beliceño | `BZ$` | Belice |
| `EUR` | Euro *(referencia internacional)* | `€` | Internacional |

También incluye conversión automática en importes, tickets térmicos 80mm, escritura de montos en letras (`VfpCompat.Letras`) y botón **"🌎 Cargar Monedas LATAM"** en el catálogo maestro.

### 3. Multi-Idioma en Caliente (`ES` · `PT` · `EN`)
- Cambio inmediato entre **Español (`es`)**, **Português (`pt`)** e **English (`en`)** desde Ajustes sin necesidad de reiniciar la aplicación.

### 4. Inicio Limpio de Fábrica y Personalización Visual
- **Arranca listo de fábrica:** tema oficial **Tema Sefiros** (claro), moneda `PYG`, idioma `es`, control de inventario seguro por defecto y arranque amigable incluso si MySQL aún no fue configurado (**Modo Fábrica**).
- **Restauración rápida:** botón **"🔄 Restablecer POS a valores de fábrica"** en *Ajustes → Sistema y copias de seguridad*.
- **8 paletas de color + Modo Oscuro** y **pantalla de presentación animada (`SefirosSplashWindow`)** con audio estéreo sintetizado en tiempo real.

### 5. Gestión Comercial, Impresión ESC/POS y Red LAN
- **Impresión RAW ESC/POS directa** para impresoras térmicas de 80mm (1 copia compacta por defecto, corte automático, apertura de cajón) + factura legal.
- **Módulos completos:** Clientes y cuotas, Proveedores, Compras, Pedidos, Presupuestos, Caja y movimientos, Informes y Lucro, Agenda, RRHH, Traslados y Ajustes de stock, Multiconexión LAN, Carga Rápida asistida por IA (opcional con API Key del usuario).

---

## Arquitectura de la Solución

```text
├── FirewallControl.sln
├── src/
│   ├── FirewallControl.Core/           # Sefiros.Core.dll — Entidades, interfaces, LatamCurrencyCatalog, cálculo POS
│   ├── FirewallControl.Infrastructure/ # Sefiros.Infrastructure.dll — MySQL/Dapper, ConfigService, ESC/POS, DBF compat
│   ├── FirewallControl.App/            # Sefiros.App.exe — UI WPF MVVM, Temas, Multi-idioma, Vistas y Splash
│   └── FirewallControl.Tests/          # Suite xUnit (cálculo, temas, monedas LATAM, localización, seguridad)
├── database/
│   └── sefiros_factory_schema.sql      # Esquema MySQL limpio de fábrica + semillas iniciales (sin datos privados)
├── config/
│   ├── FireWall.Ini.example            # Plantilla limpia de configuración local
│   └── preferencias_atajos.example.json# Plantilla de preferencias de fábrica
├── docs/
│   └── ARQUITECTURA.md                 # Documentación técnica detallada
└── .github/workflows/dotnet.yml        # CI automatizado (build + test en Windows)
```

Más detalles técnicos en [`docs/ARQUITECTURA.md`](docs/ARQUITECTURA.md).

---

## Puesta en Marcha desde Cero (Modo Fábrica)

### Requisitos
- **Windows 10 / 11 (x64)**
- **[.NET 10 SDK](https://dotnet.microsoft.com/download)**
- **MySQL 5.5+ / 8.0+ o MariaDB** *(opcional para probar el dashboard en modo fábrica, necesario para guardar ventas/productos)*

### 1. Clonar y compilar
```powershell
git clone https://github.com/TU_USUARIO/sefiros-pos.git
cd sefiros-pos
dotnet restore .\FirewallControl.sln
dotnet test .\FirewallControl.sln --verbosity normal
```

### 2. Inicializar Base de Datos de Fábrica (Opcional)
Importa el esquema limpio y las 21 monedas LATAM en tu MySQL local:
```powershell
mysql -u root -p < .\database\sefiros_factory_schema.sql
```
- Usuario administrador inicial de fábrica en DB: `ADMINISTRA` / Contraseña por defecto: `123456` *(cámbiala en **Mi perfil** al desplegar en producción)*.

### 3. Ejecutar o Publicar Portable (`--self-contained true`)
```powershell
# Ejecutar desde código fuente
dotnet run --project .\src\FirewallControl.App\FirewallControl.App.csproj

# Publicar carpeta portable autocontenida para otra PC (sin requerir .NET instalado)
dotnet publish .\src\FirewallControl.App\FirewallControl.App.csproj -c Release -r win-x64 --self-contained true -o .\publish\SefirosPOS
```

Al abrir la app por primera vez, entra a **⚙ Ajustes `[F4]` → Multiconexión y servidor MySQL** para guardar tus credenciales locales en tu equipo (`Firewall.sql` / `FireWall.Ini`, ambos excluidos de Git).

---

## Atajos Principales del Teclado

| Ventana | Tecla | Acción |
| :--- | :---: | :--- |
| Inicio (`MainWindow`) | `F1`–`F5` | Abrir / cambiar entre Cajas 1 a 5 |
| Inicio (`MainWindow`) | `F3` / `F4` | Productos / Ajustes y Preferencias |
| Punto de Venta | `F2` / `F9` / `Ctrl+P` | Selector de ítems / Cobrar / Cobrar e imprimir |
| Selector de Ítems | `F10` / `A, S, D, F` / `Z` / `X` | Movimiento Rápido / Genes / Cobro c/ ticket / Cobro s/ ticket |

---

## Seguridad
- Este repositorio no contiene credenciales ni IPs/rutas personales (verificado por prueba automatizada en `Stage6GitHubReadinessTests`). Consulta [`SECURITY.md`](SECURITY.md).

## Licencia
Distribuido bajo la licencia [MIT](LICENSE).
