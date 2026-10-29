# Seguimiento de remitos por QR — Bejerman ERP

Imprime en cada remito de Bejerman un **código QR**. El transportista lo escanea con el celular y marca el estado de la entrega (**Despachado / Entregado / Rechazado**). Cada marca queda registrada en un **Google Sheet** que el dueño ve en tiempo real.

No requiere el SDK ni el Application Pool de Bejerman, no escribe en la base del ERP y no necesita una app instalada en los celulares: alcanza con la cámara.

```
 BEJERMAN (Crystal XI R2)                       GOOGLE (Apps Script + Sheets)
 ─────────────────────────                      ──────────────────────────────
 Remito RTCTLCT6.rpt
   @URL_QR  ──► URL con remito, cliente y control
   Imagen   ──► QRRemitoMainGenerarQR(@URL_QR)
                  │  (UFL propia: CRUFLQRRemito.dll)
                  ▼
            QR impreso en el remito ──── celular ───►  Web App (doGet)
                                                         │ valida el control "k"
                                                         ▼
                                                       Pagina.html
                                                         │ Despachado / Entregado / Rechazado
                                                         ▼
                                                       Sheet: Eventos (historial)
                                                              Tablero (estado actual)
```

## Contenido del repositorio

| Carpeta / archivo | Qué es |
|---|---|
| `ufl/QRRemito.cs` | Código fuente de la función propia para Crystal (UFL COM en C# / .NET 4). Genera el QR como BMP y devuelve su ruta. Incluye su propio codificador QR, sin dependencias externas. |
| `ufl/compilar.ps1` | Compila la DLL con el `csc.exe` que trae Windows (sin Visual Studio). Salida: `ufl/bin/CRUFLQRRemito.dll`. |
| `ufl/bin/` | DLL compilada (`CRUFLQRRemito.dll`). |
| `scripts/instalar_qr_remito.ps1` | Instalación en una PC que imprime remitos: registra la DLL (RegAsm 32 bits), copia `u2lcom.dll` a `C:\Windows\crystal\` y hace una prueba. |
| `scripts/desinstalar_qr_remito.ps1` | Revierte la instalación. |
| `crystal/formulas_crystal_remito.txt` | Cambios a aplicar en el RPT del remito: fórmula `@URL_QR` e imagen del QR. |
| `apps_script/Codigo.gs` | Servidor del Web App: valida el QR, registra estados y mantiene el tablero. |
| `apps_script/Pagina.html` | Página que ve el transportista en el celular. |
| `apps_script/appsscript.json` | Manifiesto del proyecto de Apps Script (zona horaria, publicación). Opcional; útil si se usa `clasp`. |
| `docs/` | Documentación (ver abajo). |

## Documentación

1. [Arquitectura y flujo](docs/01-arquitectura-y-flujo.md): cómo funciona cada parte, formato de la URL, control `k`, estados y reglas.
2. [Instalación en un cliente](docs/02-instalacion-cliente.md): checklist paso a paso para llevarlo a producción.
3. [Decisiones y pruebas](docs/03-decisiones-y-pruebas.md): qué se probó, qué se descartó y por qué.
4. [Solución de problemas](docs/04-solucion-de-problemas.md): errores conocidos y cómo resolverlos.
5. [Pendientes y mejoras](docs/05-pendientes.md): decisiones abiertas y próximas fases.

## Requisitos

- **Bejerman ERP** con impresión de remitos por Crystal Reports **XI R2** (runtime `craxdrt.dll` 11.5).
- **Windows** con **.NET Framework 4.x** (incluido en Windows 10/11) en cada PC que imprime remitos.
- **Crystal Reports XI R2** (diseñador), solo en la PC donde se edita el RPT.
- Una **cuenta de Google** (Gmail o Workspace) para el Sheet y el Web App.

## Inicio rápido

```powershell
# 1) Compilar la DLL (o usar la de ufl\bin)
powershell -ExecutionPolicy Bypass -File .\ufl\compilar.ps1

# 2) Instalar en cada PC que imprime remitos (PowerShell como administrador, Bejerman y Crystal cerrados)
powershell -ExecutionPolicy Bypass -File .\scripts\instalar_qr_remito.ps1
```

3. Crear el Sheet y publicar el Web App con `apps_script/` (ver [instalación](docs/02-instalacion-cliente.md#3-google-sheet-y-web-app)).
4. Aplicar `crystal/formulas_crystal_remito.txt` al RPT del remito, con la URL del Web App.
5. Imprimir un remito, escanear el QR y marcar un estado.

## Archivos binarios de referencia

Para poder reproducir la solución sin depender del entorno de prueba:

| Archivo | Dónde | ¿Versionar? |
|---|---|---|
| `CRUFLQRRemito.dll` (v0.3, 12.800 bytes) | `ufl/bin/` | **Sí.** Se puede regenerar con `ufl/compilar.ps1`, pero conviene tener la DLL probada. |
| `RTCTLCT6_original.rpt` | `crystal/referencia/` | **Sí** (con `git add -f`). Es el remito estándar antes del cambio. |
| `RTCTLCT6_con_QR.rpt` | `crystal/referencia/` | **Sí** (con `git add -f`). Es el remito de prueba ya modificado; antes de guardarlo, **sacar los campos de prueba** y desmarcar **Archivo → Guardar datos con el informe**, así no lleva datos. |
| `CRUFLQRRemito.tlb` | — | **No.** Se genera al registrar con RegAsm `/tlb`. |
| `u2lcom.dll` | — | **No.** Es parte del runtime de Crystal XI R2 (SAP) y ya está en las PC con Bejerman: el instalador solo la copia de carpeta. Versión usada: 47.616 bytes, 19/02/2010, de `C:\Program Files (x86)\Business Objects\Common\3.5\bin\`. |

> Los RPT son diseños de Bejerman y pueden tener personalizaciones del cliente: mantener el repositorio **privado**.

## Versiones

| Componente | Versión | Notas |
|---|---|---|
| UFL `CRUFLQRRemito.dll` | 0.3 | `QRRemitoMainProbar`, `QRRemitoMainGenerarQR` |
| Apps Script | 1.2 | Parámetro `cl` para el cliente; bloquea estados repetidos; Entregado es final |

## Archivos binarios de referencia

| Archivo | Dónde | ¿Versionar? |
|---|---|---|
| `CRUFLQRRemito.dll` (v0.3, 12.800 bytes) | `ufl/bin/` | **Sí.** Se puede regenerar con `ufl/compilar.ps1`, pero conviene tener la DLL probada. |
| `RTCTLCT6_original.rpt` | `crystal/referencia/` | **Sí** (con `git add -f`). Remito estándar antes del cambio. |
| `RTCTLCT6_con_QR.rpt` | `crystal/referencia/` | **Sí** (con `git add -f`). Remito modificado; antes de guardarlo, sacar los campos de prueba y desmarcar **Archivo → Guardar datos con el informe**. |
| `CRUFLQRRemito.tlb` | — | **No.** Se genera al registrar con RegAsm `/tlb`. |
| `u2lcom.dll` | — | **No.** Es parte del runtime de Crystal XI R2 (SAP) y ya está en las PC con Bejerman; el instalador solo la copia de carpeta. Versión usada: 47.616 bytes, 19/02/2010. |

> Los RPT son diseños de Bejerman y pueden tener personalizaciones del cliente: mantener el repositorio **privado**.
