# Política de Seguridad / Security Policy

## Manejo de Credenciales y Configuración Local

**Sefiros POS** no incluye contraseñas, claves de API ni datos privados dentro del código fuente ni en este repositorio.

- **`FireWall.Ini`**, **`Firewall.sql`** y **`preferencias_atajos.json`** están bloqueados en `.gitignore`.
- Al iniciar desde una copia limpia de GitHub (modo fábrica), las propiedades por defecto (`SqlPassword = ""`) evitan cualquier credencial embebida.
- Configura tu servidor MySQL y clave desde la ventana integrada **⚙ Ajustes [F4] → 🌐 Multiconexión y servidor MySQL** (o copiando las plantillas en `config/`).
- Si utilizas **Carga Rápida con IA (Gemini)**, ingresa tu API Key desde la UI en tiempo de ejecución; nunca la guardes en archivos rastreados por Git.

## Reporte de Vulnerabilidades

Si detectas una fuga de configuración o vulnerabilidad en el manejo de datos o impresión, abre un **Security Advisory** privado o un issue sin exponer credenciales reales.
