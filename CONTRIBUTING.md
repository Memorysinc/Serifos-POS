# Guía de Contribución — Sefiros POS

¡Las contribuciones, traducciones y mejoras son bienvenidas!

## Requisitos
- Windows 10 / 11 (x64)
- [.NET 10 SDK](https://dotnet.microsoft.com/) (`net10.0` / `net10.0-windows`)
- MySQL 5.5+ / 8.0+ o MariaDB (opcional para probar integración SQL completa; el sistema también arranca en modo fábrica offline)

## Compilación y Pruebas Locales
```powershell
dotnet restore .\FirewallControl.sln
dotnet build .\FirewallControl.sln -c Debug
dotnet test .\FirewallControl.sln --verbosity normal
```

## Reglas Técnicas Clave
1. **Nitidez WPF ClearType:** Mantener `UseLayoutRounding="True"`, `SnapsToDevicePixels="True"`, `TextOptions.TextFormattingMode="Ideal"` y `TextOptions.TextRenderingMode="ClearType"` en todas las ventanas. Evitar `DropShadowEffect` sobre contenedores con texto.
2. **Animaciones XAML:** No animar `Transform` (`Freezable`) directamente con `Storyboard.SetTarget`; apuntar al `FrameworkElement` dueño mediante `PropertyPath`.
3. **Cero Credenciales:** Nunca incluir contraseñas, IPs privadas reales, API keys ni archivos `.ini` / `.sql` / `.dbf` en commits.
4. **Localización (`es` / `pt` / `en`):** Al agregar nuevas etiquetas visibles en ventanas principales, añadir las claves correspondientes en `Themes/Strings.es.xaml`, `Strings.pt.xaml` y `Strings.en.xaml`.
