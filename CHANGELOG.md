# Changelog — Sefiros POS (.NET 10)

## [1.0.0] - 2026-11-25
### Novedades Principales
- **Arquitectura completa en C# `.NET 10` + WPF MVVM** inspirada en las reglas operativas de alta velocidad del sistema heredado **Visual FoxPro 9 (`Sufer POS / FireWall Control`)**.
- **Inicio limpio de fábrica:** Arranque operativo listo sin configuración previa (`Tema Sefiros` claro por defecto, modo fábrica amigable si MySQL aún no está configurado y botón **"Restablecer POS a valores de fábrica"** en Ajustes).
- **Catálogo completo de Monedas de Latinoamérica (`LatamCurrencyCatalog`):** 20 monedas latinoamericanas (`PYG`, `ARS`, `BOB`, `BRL`, `CLP`, `COP`, `CRC`, `CUP`, `DOP`, `USD`, `GTQ`, `HNL`, `HTG`, `MXN`, `NIO`, `PAB`, `PEN`, `UYU`, `VES`, `BZD`) + `EUR` internacional, con formateo en KPIs, tickets térmicos, importes en letras y carga automática en catálogo maestro (`Monedas`).
- **Multi-idioma en caliente (`es` / `pt` / `en`):** Soporte instantáneo para **Español**, **Português** y **English** desde Ajustes sin reiniciar el sistema.
- **5 Cajas simultáneas en modo dual (`Renovado C#` / `Clásico FOX`)**: Con intercambio en vivo sin perder carritos, `Modal 1` Win32 nativo sin grisear y guardián de foco para lector de códigos de barras.
- **Presentación animada de inicio:** Emblema "S" de trazo continuo con aura prismática, cometa, destello estelar final y audio estéreo sintetizado en C#.
- **Impresión térmica ESC/POS RAW 80mm compacta** (1 copia por defecto, corte automático, apertura de cajón) y facturación legal con timbrados.
- **Auditoría de seguridad para GitHub:** Cero contraseñas embebidas, cero rutas personales locales y pruebas portables con CI en GitHub Actions.
