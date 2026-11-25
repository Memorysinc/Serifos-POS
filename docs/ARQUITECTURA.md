# Informe Técnico de Arquitectura y Lógica — Sefiros POS (`.NET 10`)

Documento de referencia rápida para que otra IA o desarrollador comprenda la arquitectura, reglas de negocio, mapeo Visual FoxPro 9 → C# / WPF, persistencia, pruebas y puntos críticos sin tener que releer todo el repositorio.

---

## 1. Rutas Críticas, Entorno y Prohibiciones

- **Raíz de la solución (.NET 10):**
  `.\` (`FirewallControl.sln`)
- **Solución principal:**
  `.\FirewallControl.sln`
- **Carpeta de despliegue / ejecución productiva (`Release win-x64`):**
  `.\ParaOtraPC_FirewallControl_NET10`
- **SDK .NET requerido:**
  `dotnet` (`.NET SDK 10.0+`, TargetFramework `net10.0` / `net10.0-windows`).
- **Archivo heredado excluido:**
  - Si existe un binario heredado antiguo en entorno de desarrollo, **NO ejecutarlo ni tomarlo como referencia**. Trabajar siempre sobre `src\` y las plantillas de configuración.
- **Archivos protegidos en publicación (NUNCA sobrescribir al desplegar en `ParaOtraPC_FirewallControl_NET10`):**
  - `FireWall.Ini`, `Firewall.sql`, `genes_productos.dbf`, `preferencias_atajos.json`
  - Carpetas `Styles\`, `images\`, `Tickets\`, `tickets_emitidos\`, `Backups\` y cualquier `*.ini`, `*.sql`, `*.dbf`.
  - Nota: SÍ debe desplegarse `Assets\sefiros_pos.ico` (copiado mediante `CopyToOutputDirectory="PreserveNewest"` desde `FirewallControl.App.csproj`) y los binarios compilados.

### Comandos estándar verificados
- **Compilar y ejecutar todas las pruebas:**
  ```powershell
  dotnet test .\FirewallControl.sln --verbosity normal
  ```
- **Publicación segura Release `win-x64` portable autocontenida (vía staging + robocopy):**
  - **REGLA CRÍTICA:** `ParaOtraPC_FirewallControl_NET10` es una carpeta portable autocontenida con `hostfxr.dll` y el runtime `.NET 10` local. Publicar **SIEMPRE** con `--self-contained true`. Si se usa `--self-contained false`, el archivo `Sefiros.App.runtimeconfig.json` queda con `"frameworks"` en lugar de `"includedFrameworks"` y el apphost local falla pidiendo instalar `.NET` aunque esté instalado en el equipo.
  1. Verificar que no exista un proceso `Sefiros.App` activo bloqueando binarios.
  2. Respaldar `ParaOtraPC_FirewallControl_NET10` hacia `Backups\ParaOtraPC_FirewallControl_NET10_<stamp>`.
  3. Publicar a una carpeta temporal `<staging>`:
     `dotnet publish .\src\FirewallControl.App\FirewallControl.App.csproj -c Release -r win-x64 --self-contained true -o <staging>`
  4. Copiar con `robocopy <staging> ParaOtraPC_FirewallControl_NET10 /E /XD Styles images Tickets tickets_emitidos Backups /XF FireWall.Ini Firewall.sql genes_productos.dbf preferencias_atajos.json *.ini *.sql *.dbf` (códigos `0..7` = éxito).
  5. Eliminar `<staging>` y notificar a Explorer con `SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero)`.

---

## 2. Estructura de Proyectos (`src\`)

| Proyecto | Responsabilidad | Archivos principales |
| --- | --- | --- |
| **FirewallControl.Core** (`Sefiros.Core.dll`) | Entidades de dominio, interfaces y reglas puras de negocio/compatibilidad VFP | `Business\SalesCalculationEngine.cs`, `VfpCompat.cs`, `VfpPermissionHelper.cs`, `FastCargaParser.cs`<br>`Entities\SaleEntities.cs`, `ProductEntities.cs`, `ProductCatalogEntities.cs`, `PartyAndPurchaseEntities.cs`, `PosToolsEntities.cs`, `Stage3Entities.cs`, `SystemAndConfigEntities.cs` (`ShortcutCatalog`, `AppPreferencesEntity`)<br>`Interfaces\ServiceAndRepositoryInterfaces.cs`, `ICargaRapidaIaService.cs`, `IStage3Repository.cs` |
| **FirewallControl.Infrastructure** (`Sefiros.Infrastructure.dll`) | Acceso MySQL (`Dapper`/ADO), lectura/escritura DBF VFP, INI/JSON, tickets, Gemini Files API | `Database\MySqlConnectionFactory.cs`<br>`Repositories\ProductRepository.cs`, `SalesRepository.cs`, `PurchaseRepository.cs`, `CustomerRepository.cs`, `CashRepository.cs`, `SystemRepository.cs`, `Stage3Repository.cs`<br>`Services\ConfigService.cs`, `VfpDbfService.cs`, `VfpsLicenseService.cs`, `TicketPrintService.cs`, `CargaRapidaIaService.cs` |
| **FirewallControl.App** (`Sefiros.App.exe` / `Sefiros.App.dll`) | Aplicación WPF `.NET 10`, DI en `App.xaml.cs`, temas, `WindowManagerService`, vistas y ViewModels | `App.xaml`, `App.xaml.cs`, `MainWindow.xaml/.cs`<br>`ViewModels\ViewModelBase.cs`, `AbmVentasAutoserviceViewModel.cs`, `ItemsFacturaVentasViewModel.cs`<br>`Services\WindowManagerService.cs`, `IWindowManagerService.cs`, `PosWindowBehaviorHelper.cs`, `AppIconHelper.cs`, `SefirosStartupSoundService.cs`, `ThemeService.cs`, `KeyShortcutHelper.cs`, `PosNumericParser.cs`, `BarcodeLabelPrintHelper.cs`, `ISalesPosWindow.cs`<br>`Themes\ThemeColors.xaml`, `DesignSystem.xaml`, `FoxPosStyles.xaml` |
| **FirewallControl.Tests** | Suite xUnit STA/WPF e integración MySQL + DBF (paralelización desactivada en `TestAssemblyConfig.cs`) | `MigrationVerificationTests.cs`, `Stage1VerificationTests.cs`, `Stage2VerificationTests.cs`, `Stage3VerificationTests.cs`, `Stage4PosUxVerificationTests.cs`, `Stage5SefirosPolishTests.cs` |

---

## 3. Mapa VFP 9 (`forms\` / `PROGS\`) → C# WPF (`src\FirewallControl.App\`)

- **Ventana Principal (`_FONDO.scx`) → `MainWindow.xaml` / `MainWindow.xaml.cs`**:
  - Conserva estética moderna C# Atelier con 4 KPIs en tiempo real (`CATÁLOGO ACTIVO`, `FLUJO DE CAJA HOY`, `VENTAS DE HOY`, `AGENDA DE HOY`), pestañas `Últimas Ventas` y `Top Vendidos`, cotizaciones del día, tarjeta de licencia `Sefiros POS`, acceso directo `Informes y Lucro`, paneles condicionales `Veterinaria` (`datosempresa.veterinaria = 1`) y `Restaurant` (`datosempresa.restaurant = 1`).
  - **Barra superior (`DynamicMainMenu`)**: cargada 1:1 desde `_menuprompts.dbf` / `_menubars.dbf` + matriz de permisos `caccesos` (o fallback de 7 menús si faltan DBFs). **Importante:** `Stage1VerificationTests.DynamicMenu_DoesNotDropSeparatedItems` verifica coincidencia 1:1 en cantidad de ítems de `DynamicMainMenu`. Por eso los accesos extra que no tienen tarjeta fija en `_FONDO` se exponen en el botón **`BtnOtrosModulos` (`📂 Más Módulos ▾`)** dentro de `PanelOperationalFlags`.
- **Punto de Venta (`ABMVENTAS` / `abm_ventas_autoservice_cobro_contado.scx`)**:
  - **ViewModel único compartido:** `AbmVentasAutoserviceViewModel`
  - **Vista Clásica FOX (Sufer POS):** `Views\AbmVentasClassicWindow.xaml/.cs`
  - **Vista Renovada C#:** `Views\AbmVentasAutoserviceWindow.xaml/.cs`
  - Ambas implementan `ISalesPosWindow` y pueden intercambiarse en vivo (`WindowManagerService.ApplyPosStyle`) conservando los carritos cargados.
- **Selector de Ítems (`ITEMSFACTURAVENTAS.scx`)**:
  - **ViewModel único compartido:** `ItemsFacturaVentasViewModel`
  - **Vista Clásica FOX:** `Views\ItemsFacturaVentasClassicWindow.xaml/.cs`
  - **Vista Renovada C#:** `Views\ItemsFacturaVentasWindow.xaml/.cs`
  - Ambas implementan `IItemsSelectorWindow`.
- **Botonera rápida por departamento (`genes_system.prg` / `GenesSelector.cs`) → `Views\GenesSelectorWindow.xaml/.cs`**:
  - 4 departamentos configurados en `genes_productos.dbf`: `Gen 1 Verdulería [A]`, `Gen 2 Carnicería [S]`, `Gen 3 Panadería [D]`, `Gen 4 Almacén [F]`.
  - Ocupa verticalmente el **100% del `WorkArea` útil de pantalla** (`ApplyFullVerticalScreenBounds()`: `Top = work.Top`, `Height = work.Height`, `Width ~ 1220` para evitar recorte en `Cerrar [ESC]`), calculando dinámicamente métricas de tarjetas (`140–178 DIPs`) e íconos ampliados con `BitmapScalingMode.HighQuality` (`84–108 DIPs`, estilo FoxPro) adaptados a 4 o 5 filas visibles según el alto de pantalla.
  - Carga diálogo modal inline `ShowCargarDialog` (`KG` monto en Gs → cantidad a 3–6 decimales, `DOCENA`, `UNIDAD`, `Precio == 1` Monto Libre), más creación (`Ctrl+A/S/D/F`), edición (`Alt+A/S/D/F`), eliminación (`DEL`), reordenamiento y sincronización de precios con MySQL (`F5`).
- **Productos e Inventario (`crear_productos.scx`, `abm_productos.scx`, `crear_productos_ajuste.scx`)**:
  - `Views\ProductsManagementWindow.xaml/.cs`: catálogo A–Z con búsqueda y selección para POS (`_onProductCodeSelected`), ficha completa de **72 columnas** (`P1`–`P7`, depósito, parámetros, lotes/garantía/fraccionable/compuesto, baja lógica `Estado = 0` para proteger historial de ventas), catálogos auxiliares (grupos, subgrupos, marcas, modelos, depósitos) y etiquetas/códigos de barras (`BarcodeLabelPrintHelper`).
  - Sub-vistas relacionadas: `AjustePreciosMasivoWindow`, `AjusteStockWindow`, `TrasladoDepositosWindow`, `ProductSubReportsWindow`, `ProductImageBrowserWindow`, `QuickCatalogAddDialog`.
- **Carga Rápida de Productos con IA (`fast_carga.ini` / Gemini Files API)**:
  - `Views\CargaRapidaProductosWindow.xaml/.cs` + `CargaRapidaIaService.cs` + `FastCargaParser.cs`.
  - Es la **misma instancia** reutilizable tanto desde Productos como desde `ITEMSFACTURAVENTAS` con **`F9`**.
  - `ChkFastNoControlaStock` inicia marcada por defecto (sube productos con `nocontrolastock = 1`, editable por el usuario).
- **Otros módulos migrados**:
  - **Compras y Proveedores**: `PurchasesManagementWindow`, `ItemsFacturaComprasWindow`, `QuickCreateCustomerDialog`, `QuickCreateSellerDialog`.
  - **Clientes y Caja**: `CustomersManagementWindow`, `CashManagementWindow`, `CobroContadoDialog` (solo cobro contado efectivo), `CuotaVentaDialog`, `BuscarPresupuestosImportDialog`, `ReeditarReimprimirDialog`, `CalculatorDialog`, `EmisionFacturaLegalWindow`, `BuscarLotesDialog`, `CombinarCompuestoDialog`.
  - **Informes, Operaciones y RRHH/Especialidades**: `ReportsAndConfigWindow` (botón **"Hoy"** en las 3 pestañas, incluido Informe de Lucro), `ExtendedReportsWindow`, `CommercialOperationsWindow`, `CommercialTeamWindow`, `MasterCatalogsWindow`, `AgendaAndOrganizerWindow`, `HumanResourcesWindow`, `SpecialtyModulesWindow` (Veterinaria / Restaurant / Entrega).
  - **Configuración, Usuarios y Temas**: `ConfiguracionPreferenciasWindow` (reorganizado en 6 categorías lógicas + subpágina `ViewApariencia` `initialTab = 9`), `ColorAtelierDialog` (8 temas incl. `Tema Sefiros`), `FormAccessDialog`, `LoginWindow`, `SeleServidorWindow`.

---

## 4. Invariantes Críticos de Lógica y UX (No Romper)

### 4.1 Gestión de las 5 Cajas (`Clasico` vs `Renovado`) y Tamaño (`WindowManagerService`)
- `WindowManagerService` mantiene **singleton por caja (1..5)** en `_salesWindowsByCaja` y **una única instancia reutilizable** de `_itemsSelectorWindow`.
- **Comportamiento bifurcado según `Preferences.EstiloPos`:**
  - **Modo `Clasico` (Visual FoxPro / Sufer POS — NO alterar):** `_sharedSalesWidth` y `_sharedSalesHeight` se calculan proporcionalmente al `SystemParameters.WorkArea` (**~94% ancho × ~96% alto**, mínimo `1040×640`) centrados en `(_sharedSalesLeft, _sharedSalesTop)`. Las 5 cajas comparten exactamente las mismas coordenadas; al cambiar entre Cajas `1..5`, solo la caja activa queda visible delante y las demás permanecen ocultas detrás con su carrito conservado.
  - **Modo `Renovado` (C# Atelier — cascada + cuadro Stock Actual):** las Cajas `1..5` usan dimensiones compactas proporcionales (`~74%` ancho `980..1140px`, altura `work.Height - 135` `585..700px`) escalonadas en **cascada visible ("uno detrás de otro")** (`+30px` X, `+20px` Y desde `(work.Left + 16, work.Top + 52)`), **sin ocultar las cajas inactivas** para que puedan verse escalonadas detrás mientras la caja elegida pasa al frente. El selector `ItemsFacturaVentasWindow` se ubica hacia la derecha dejando visibles las 5 cabeceras en cascada a la izquierda y sincroniza el cuadro **`BorderStockActualRenovado`** (`StockActualValor` / `StockActualProducto`) tanto en `AbmVentasAutoserviceWindow` como en **`BorderStockActualSelector`** (`ItemsFacturaVentasWindow`). El intercambio en caliente (`ApplyPosStyle`) reconfigura visibilidad, geometría y dueño sin perder ítems cargados.
- **Cerrar con `X` vs Minimizar**:
  - Minimizar conserva todos los ítems cargados de esa caja.
  - Cerrar `ABMVENTAS` con `X` (`HandleSalesWindowClosedByUser`) limpia completamente la venta de esa caja (`ResetSaleForWindowClose()`) y oculta la ventana y su selector sin afectar las otras cajas (en `Renovado`, pasa el foco a otra caja visible si existe).

### 4.2 Modal 1 Nativo Win32 sin Grisear y Foco al Cerrar
- La preferencia `Preferences.ItemFacturaModal` (`Modal 1`, desactivada por defecto) bloquea la interacción con `_FONDO` y `ABMVENTAS` cuando el selector está abierto **sin asignar `Window.IsEnabled = false` en WPF** (lo que volvería grises los controles de atrás).
- En su lugar, `ApplyItemSelectorModalState` llama Win32 `EnableWindow(hwnd, false)` solo sobre los HWND de fondo (`_nativeModalLockedHwnds`) manteniendo `IsEnabled = true` en WPF.
- **Regla de oro de ciclo de vida:**
  - `SafeHideAndRestoreFocus`, `AttachSafeCloseFocusRestore`, `ResolveBestFocusTarget` (`TryKeepModal1SelectorInFocus`) y todos los `ShowDialog()` auxiliares (`ShowOwnedDialogPreservingModal1`) **nunca** deben re-habilitar o activar `_FONDO`/`ABMVENTAS` mientras `_isBackgroundLockedByItemModal` esté activo ni dentro de callbacks diferidos en `DispatcherPriority.ApplicationIdle`.
  - Si un diálogo hijo oculta y cierra, siempre debe restaurar el foco a `_itemsSelectorWindow` (cuando Modal 1 está activo o el selector visible) o a la ventana activa superior para evitar que Windows minimice/mande atrás el sistema al cerrar ventanas.

### 4.3 Foco Permanente en `TxtCodProducto` (`PosWindowBehaviorHelper`) y Lupa
- `ItemsFacturaVentasClassicWindow` y `ItemsFacturaVentasWindow` usan `PosWindowBehaviorHelper.AttachItemsCodProductoFocusGuard(...)`.
- **Por qué no usar `GotKeyboardFocus` agresivo en `DispatcherPriority.Input`:** si se roba foco durante `MouseDown` antes de `MouseUp`, `ButtonBase` pierde captura y **anula el evento `Click` / `Command`** (rompiendo la Lupa y botones).
- Reglas actuales en `AttachItemsCodProductoFocusGuard`:
  1. Ignora cualquier evento si `Mouse.LeftButton == Pressed` o `Mouse.RightButton == Pressed`.
  2. Ignora `GotKeyboardFocus` cuando `e.NewFocus` es `ButtonBase` o está dentro de un input editable / `ComboBox` desplegado (`IsInsideOpenComboBox`).
  3. Usa `requireActive: true` en `Activated`, `IsVisibleChanged`, `PreviewMouseUp` y `GotKeyboardFocus` (en prioridad `DispatcherPriority.Background` para MouseUp/GotKeyboardFocus), de modo que si el clic/atajo abrió otro formulario (`ProductsManagementWindow` vía Lupa/`F3`, `GenesSelectorWindow`, etc.), `ITEMSFACTURAVENTAS` queda `IsActive == false` y jamás roba el foco al formulario recién abierto.
  4. Botones no enfocables en la UI (`Focusable="False"` en Lupa `BtnBuscarProducto`, Presupuesto, `BtnComisPriceMenu`, `BtnSelectorTools`, `Movimiento Rápido`, Cajas `1..5`, Limpiar/Salir, etc.) evitan perder el foco de escáner.

### 4.4 Comportamiento de `ITEMSFACTURAVENTAS` y `GenesSelectorWindow`
- **`Cantidad` inicial = `0`**: En `ItemsFacturaVentasViewModel`, `_cantidad = 0m` al iniciar, al ejecutar `LimpiarCampos()` y tras buscar un producto en `LcTipo == "A"` (salvo cuando `IsMovimientoRapido == true` o `_autoCantidad == 1`, que pasa `1m` automático).
- **Genes (`A`, `S`, `D`, `F`) siempre activos desde `codproducto`**: En `ShortcutCatalog` (`SystemAndConfigEntities.cs`), `IFV_GEN_1..4` tienen `RequiresEmptyInput = false`, igual que VFP `itemsfacturaventas.sct` (`IF nshiftaltctrl = 0 AND INLIST(nkeycode, 65, 97, ...)`). Si el foco no está editando cantidad/kg/precio, presionar `A/S/D/F` abre Genes inmediatamente aunque exista texto en `codproducto` (ej. `"2"` + `"a"`).
- **Retorno de foco en `GenesSelectorWindow` al cancelar monto con `ESC`**:
  - Al pulsar `Enter`/doble clic en un producto (ej. Tomate), `ActivateCard` oculta la grilla y abre `ShowCargarDialog(prod)` (`Owner` heredado si visible).
  - Si el usuario pulsa `ESC` en el diálogo de monto (`selection == null`), `ActivateCard` ejecuta `Show(); Activate(); EnsureKeyboardFocus();` más un refuerzo en `DispatcherPriority.ApplicationIdle` sobre `RootGrid`, garantizando que las flechas / teclas vuelvan a mover las tarjetas de Genes sin tener que pulsar `A` otra vez.
  - Al pulsar `ESC` en `GenesSelectorWindow` para salir de Genes, llama a `_windowManager.SafeHideAndRestoreFocus(this)` devolviendo el foco a `TxtCodProducto`.

### 4.5 Reglas de Cálculo, Stock, Límites Editables y Fraccionables
- **`SalesCalculationEngine`**:
  - Cantidad máxima por ítem configurable en `Preferences.LimiteCantidadPorItem` (default `120`, `0` = sin límite).
  - Límites de precio unitario editables en `Preferences`: `ValidarLimitesPrecioEnItem`, `PrecioMinimoVenta` (default `99`), `PrecioMaximoVenta` (default `1000000`), `PermitirVentaBajoCosto`.
  - Mensajes siempre formales y profesionales (verificado por prueba automática `Codebase_HasZeroUnprofessionalValidationMessages`).
- **Congelación de stock sin control (`CongelarStockSinControl`)**:
  - `SalesRepository.ShouldSkipStockUpdate(prodNoControlaStock, prefs)` omite el descuento/modificación en `itemsdepositos` cuando `CongelarStockSinControl == true` y (`prodNoControlaStock == 1` o `NoControlarStockGlobal == true` sin `DescontarStockAunqueNoControle`), evitando inventarios negativos.
- **Fraccionables en impresión (`TicketPrintService`)**:
  - Recalcula peso exacto a **3 decimales** (`EffectiveTotal / PreUnitario` cuando aplica, como `2,000 Gs / 9,000 = 0.222 kg`) y total de línea real tanto en `FormatTicket80mm` como en `FormatLegalInvoice`.

### 4.6 Temas Atelier + Paleta FOX Independiente + Tema Sefiros
- **Ejes independientes**: `EstiloPos` (`"Clasico"` / `"Renovado"`), `PaletaColores` (`"Moderna"` / `"ClasicaFox"`), `TemaAtelier` (8 paletas: `neutro`, `sefiros`, `ocean`, `emerald`, `rose`, `indigo`, `sand`, `atelier`), `TemaOscuroConfiguracion` (`bool`), `MonedaBaseIso` (20 monedas LATAM + EUR) e `Idioma` (`es`, `pt`, `en`).
- **`ThemeService.ApplyTheme(...)`:**
  - **Prioridad del `Tema Sefiros` (`key = "sefiros"`, paleta oficial de Sefiros POS):** cuando `IsSefirosTheme` está activo (tanto en fondos neutro perla `#F7F8FA`/`#F8F9FB` claro como en obsidiana-grafito `#111318` oscuro), `ApplyPosSemanticBrushes` evalúa `IsSefirosTheme` **antes** que `IsClassicFoxPalette`, recoloreando todo el sistema con equilibrio de los 4 colores de la paleta Sefiros (azul, verde esmeralda, ámbar y coral-rojo) **incluyendo la versión Clásica FOX de `ABMVENTAS` (`AbmVentasClassicWindow`) e `ITEMSFACTURAVENTAS` (`ItemsFacturaVentasClassicWindow`)** incluso si `PaletaColores == "ClasicaFox"`.
  - Para los demás 7 temas (`!IsSefirosTheme`): cuando `PaletaColores == "ClasicaFox"`, solo recolorea recursos `Pos*Brush` consumidos por `ABMVENTAS` y `ITEMSFACTURAVENTAS` (`FoxPosStyles.xaml`), manteniendo todo el resto del sistema bajo el `TemaAtelier` elegido.
  - `CreatePaletteSwatchBrush(AtelierPalette)` genera el swatch diagonal multicolor del `Tema Sefiros` y color sólido para los demás temas.
  - Preserva tonos semánticos reales (`SuccessTextBrush` verde y `InfoTextBrush` azul) incluso dentro del tema `neutro`.
- **Estructura lógica de `ConfiguracionPreferenciasWindow`:**
  - Hub agrupado en **6 categorías en orden natural**: *(1) Empresa y operación comercial*, *(2) Apariencia y diseño del POS* (`ViewApariencia`, `initialTab = 9`: Tema + Sefiros, Tema Oscuro, Diseño POS Renovado/Clásico y Paleta Moderna/Clásica FOX), *(3) Punto de venta, stock y atajos* (`ViewPreferencias` `initialTab = 1` enfocado en Stock, Caja/Modal 1 y Límites + `ViewAtajos` `initialTab = 2`), *(4) Impresión y periféricos* (`initialTab = 3`), *(5) Equipo, red LAN y seguridad* (`initialTab = 4, 5, 7`), *(6) Sistema y copias de seguridad* (`initialTab = 8`).

### 4.7 Ícono de Barra de Tareas Windows 10/11 y Ciclo de Vida WPF en Pruebas
- **Ícono en barra de tareas Windows 11 (`AppIconHelper` + `sefiros_pos.ico` + `FirewallControl.App.csproj` + `App.xaml.cs`)**:
  - `Assets\sefiros_pos.ico` contiene 7 frames (`16, 24, 32, 48, 64` DIB 32bpp con `biSizeImage` explícito + máscara AND 1bpp + `128, 256` PNG) y se copia físicamente a `Assets\sefiros_pos.ico` junto al binario (`CopyToOutputDirectory="PreserveNewest"`).
  - `App.OnStartup` invoca `AppIconHelper.InitializeProcessAppUserModelId()` (`"Sefiros.POS.Net10.App"`).
  - `AppIconHelper.ApplySefirosIcon(window)` aplica `WM_SETICON` (`ICON_BIG`/`ICON_SMALL`), `SetClassLongPtr` (`GCLP_HICON` / `GCLP_HICONSM`) y `SHGetPropertyStoreForWindow` (`IPropertyStore`: `PKEY_AppUserModel_ID`, `RelaunchDisplayNameResource`, `RelaunchCommand`, `RelaunchIconResource` apuntando a `Assets\sefiros_pos.ico,0`) tanto en `SourceInitialized` como en `Loaded`.
- **`ShutdownMode` en `App.xaml` / `App.xaml.cs`, Presentación de Inicio Animada y Nitidez Global ClearType (`AppIconHelper.ApplyCrispRendering`)**:
  - **Estándar de nitidez (43+ ventanas):** Todas las `<Window>` XAML y cualquier diálogo creado en runtime reciben `UseLayoutRounding="True"`, `SnapsToDevicePixels="True"`, `TextOptions.TextFormattingMode="Ideal"` y `TextOptions.TextRenderingMode="ClearType"` tanto en XAML como mediante `EventManager.RegisterClassHandler(typeof(Window), LoadedEvent)` en `App.EnsureCrispWindowDefaults()` y `AppIconHelper.ApplySefirosIcon(...)`. Además está prohibido aplicar `DropShadowEffect` (`SoftCardShadow`) sobre contenedores que envuelven texto/controles (degrada ClearType a rasterizado borroso) y los popups de `ComboBox` mantienen `RenderOptions.ClearTypeHint="Enabled"`; las píldoras usan `CornerRadius="12"` en lugar de `"99"`.
  - **Inicio animado (`Views\SefirosSplashWindow.xaml/.cs` + `Services\SefirosStartupSoundService.cs` + `App.ShowSplashThenMainWindow`):** En cada apertura real (`App.OnStartup`), después de aplicar `ThemeService.ApplyTheme(...)`, se presenta `SefirosSplashWindow` acompañada del audio de inicio (`SefirosStartupSoundService.PlayStartupChime()`, síntesis PCM estéreo 44.1 kHz de pad grave profundo + tintineo cristalino final): la `"S"` C1 continua del logo Sefiros se traza progresivamente de punta a punta estilo Google `"G"` calculando `StrokeDashArray`/`StrokeDashOffset` desde `ComputePathLength(Geometry)`, flanqueada por **6 brillos alados con silueta astroide de 4 puntas de Gemini** (`SparkleGemini1..6`) y un destello especular final (`PathSSheen`), adaptándose al tema Claro, Oscuro o `Tema Sefiros`. La instancia de `MainWindow` se preconstruye en segundo plano (`DispatcherPriority.Background` / `PresentationReadyToTransition`); al finalizar la disolución (~2.2s, o al hacer clic / presionar `Esc`/`Enter`/`Space`), `PresentationCompleted` asigna `MainWindow`, conmuta `ShutdownMode = ShutdownMode.OnMainWindowClose`, muestra y activa `MainWindow` y cierra el splash al instante antes de abrir ventanas secundarias o `LoginWindow`. Además `App.xaml.cs` captura excepciones de `DispatcherUnhandledException`, `TaskScheduler.UnobservedTaskException` y `AppDomain.UnhandledException` con registro en `sefiros_error.log`.
  - `App.xaml` declara `ShutdownMode="OnExplicitShutdown"`, mientras `App.OnStartup` conserva ese modo durante el splash y lo cambia a `ShutdownMode.OnMainWindowClose` en cuanto `MainWindow` es revelada.
  - Motivo crítico en pruebas: en `FirewallControl.Tests`, múltiples pruebas en distintos hilos STA comparten el singleton `Application.Current` inicializado sin `OnStartup`; si `ShutdownMode` fuera `OnLastWindowClose`, al cerrar ventanas una prueba apagaría `Application.Current` (`Application.IsShuttingDown = true`) y las pruebas posteriores fallarían al no crear HWNDs (`hwnd == 0`).

---

## 5. Persistencia de Configuración, BD y Archivos DBF

- **`FireWall.Ini`** (resuelto subiendo hasta 10 niveles + `ParaOtraPC_FirewallControl_NET10` en `ConfigService`): servidor/puerto/BD MySQL, sucursal/caja/depósito/vendedor/cliente por defecto, timbrado, moneda, API key/catálogo de Gemini (`FastCarga.*`).
- **`Firewall.sql`**: cadena cifrada descifrada mediante `VfpCompat.Encriptar("OFF", line, "###$$$$")`.
- **`preferencias_atajos.json`**: preferencias del usuario (`EstiloPos`, `PaletaColores`, `TemaAtelier`, `ItemFacturaModal`, `CongelarStockSinControl`, límites editables de cantidad/precio, flags de impresión ESC/POS, atajos personalizados `CustomShortcuts`, etc.).
- **Tablas VFP (`DBF`)**:
  - `genes_productos.dbf` (y copia en `styles\`): ítems de Genes `1..4` con orden, código, precio, costo, tipo (`KG`/`DOCENA`/`UNIDAD`), ícono y bandera `Actualizar`.
  - `Styles\_menuprompts.dbf` y `Styles\_menubars.dbf` (resolución de hasta 12 niveles en `VfpDbfService` + menú de respaldo): árbol del menú superior de `MainWindow`.
- **Base de datos MySQL (`firewallcontrol`)**: utilizada en tiempo de ejecución y en las pruebas de integración (`Stage1`–`Stage5` y `MigrationVerificationTests`).

---

## 6. Mapa de la Suite de Pruebas (`src\FirewallControl.Tests\`)

- `MigrationVerificationTests.cs`: motor de cálculo `SalesCalculationEngine`, funciones `VfpCompat`, carga de DBFs, ViewModels POS base y transacciones MySQL con rollback.
- `Stage1VerificationTests.cs`: `FastCargaParser`, `CargaRapidaIaService`, `KeyShortcutHelper`, menú dinámico con separadores 1:1 y hot-swap `EstiloPos`.
- `Stage2VerificationTests.cs`: catálogo de 72 columnas (`SaveProductFull`), guardia de duplicados, baja lógica y etiquetas Code39 (`BarcodeLabelPrintHelper`).
- `Stage3VerificationTests.cs`: árbol completo de menús DBF, matriz de permisos `caccesos`, KPIs de `_FONDO` e informes extendidos.
- `Stage4PosUxVerificationTests.cs`: posición/tamaño compartido y exclusividad visible de 5 cajas en Clásico (FOX), cascada visible "uno detrás de otro" en Renovado + hot-swap en caliente sin pérdida de carritos, independencia entre `PaletaColores` y `EstiloPos`, fraccionables 3 decimales e `Informe de Lucro` + botón `Hoy`.
- `Stage5SefirosPolishTests.cs`: validación de mensajes profesionales, restricciones editables, `CongelarStockSinControl`, `Cantidad = 0` por defecto, cierre `X` vs minimizar, Modal 1 nativo sin grisear, carga con `F9` sin control de stock por defecto, `BtnOtrosModulos` + `DynamicMainMenu`, atajos Genes `A/S/D/F` con input no vacío, botón Lupa en `ITEMSFACTURAVENTAS`, dimensiones ~94%x96% de `ABMVENTAS`, handles Win32 del ícono Sefiros POS, nitidez ClearType global sin `DropShadowEffect` en texto, cuadro `Stock Actual` en Renovado, **`Tema Sefiros` aplicado también a FOX clásico**, navegación completa (`Configure(0..9)`) de la reorganización en 6 categorías de Ajustes, **`SefirosSplashWindow` animado (S progresiva estilo Google "G" + 6 brillos alados Gemini + sonido profundo y tintineo final + transición limpia)**, **`GenesSelectorWindow` a altura vertical completa de pantalla con íconos ampliados** y **validación de lanzamiento (`Sefiros.App.exe`, `Sefiros.Core.dll`, `Sefiros.Infrastructure.dll` y ausencia de menciones previas)**.
