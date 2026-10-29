# 1. Arquitectura y flujo

## Problema que resuelve

El transportista entrega la mercadería y hace firmar el remito. Hasta que vuelve o manda un mensaje, el dueño no sabe qué se entregó y qué no. El ERP (Bejerman) no tiene el SDK / Application Pool contratado, así que no hay forma de escribir en la base desde afuera en tiempo real.

**Solución:** cada remito sale impreso con un QR. Al escanearlo, el transportista marca el estado y queda registrado en un Google Sheet. La vuelta al ERP queda para una fase 2 (ver [pendientes](05-pendientes.md)).

## Componentes

### 1. Remito en Crystal Reports (`RTCTLCT6.rpt`)
- Bejerman imprime los remitos con **Crystal Reports XI R2** (formato RDC / ActiveX Designer).
- Se agregan dos cosas al RPT:
  - **Fórmula `@URL_QR`:** arma la URL con el número de remito, el código de cliente y un número de control.
  - **Imagen del QR:** su "Ubicación del gráfico" es `QRRemitoMainGenerarQR({@URL_QR})`.
- Es el mismo mecanismo que usa Bejerman para el QR de AFIP en la factura electrónica (`VTALCT6.rpt`, objeto `Picture1` con `{@PathImagenQR}`): una función genera la imagen en disco y Crystal la carga desde esa ruta.

### 2. Función propia para Crystal (UFL) — `CRUFLQRRemito.dll`
- DLL COM en C# (.NET Framework 4, x86), registrada con `RegAsm`.
- Crystal la expone mediante su puente estándar `u2lcom.dll` ("UFLs de Visual Basic").
- Nombre en Crystal = ProgID sin "CRUFL" + clase + método:

| Método .NET | Función en Crystal | Qué hace |
|---|---|---|
| `Main.Probar(texto)` | `QRRemitoMainProbar(texto)` | Devuelve `"OK-UFL: " + texto`. Sirve para probar el puente. |
| `Main.GenerarQR(texto)` | `QRRemitoMainGenerarQR(texto)` | Genera un BMP con el QR del texto en `%TEMP%\QRRemito\qr_<hash>.bmp` y devuelve la ruta. Si ya existe para ese texto, lo reutiliza. Borra los de más de 2 días. Si falla, devuelve `""` y escribe en `%TEMP%\QRRemito_error.log` (nunca muestra errores en Crystal). |

- Codificador QR propio: modo byte, corrección de errores nivel M, versiones 1 a 40, con selección de la máscara por penalización (ISO/IEC 18004). Basado en la estructura del generador de código abierto de Project Nayuki (MIT). No usa librerías externas ni System.Drawing.
- BMP de 24 bits, 8 px por módulo, 4 módulos de margen blanco.

### 3. Web App (Google Apps Script) + Google Sheet
- `Codigo.gs` (servidor) y `Pagina.html` (interfaz móvil).
- Publicado como **Aplicación web**: "Ejecutar como: Yo" y "Quién tiene acceso: Cualquier usuario" (el transportista no necesita cuenta de Google).
- Hojas del Sheet (las crea `configurar()`):

| Hoja | Contenido | Quién la mantiene |
|---|---|---|
| **Tablero** | Una fila por remito: estado actual (coloreado), fecha/hora, transportista, observación, fecha de cada estado y cantidad de registros. | El script |
| **Eventos** | Historial: una fila por cada marca. | El script |
| **Transportistas** | Nombres que aparecen en la lista de la página. | El dueño |
| **Clientes** | (Opcional) código y razón social, para mostrar el nombre del cliente. | El dueño (o un export de Bejerman) |

## Formato de la URL del QR

```
https://script.google.com/macros/s/<ID_WEB_APP>/exec?r=<remito>&cl=<cliente>&k=<control>
```

| Parámetro | Origen en el RPT | Ejemplo |
|---|---|---|
| `r` | `{@Cab_Nro}` sin espacios (punto de venta + número, tal como se imprime) | `00001-00000002` |
| `cl` | `{TmpRem.CodigoCliProv}` (espacios como `%20`) | `000005` |
| `k` | Número de control (ver abajo) | `331330` |

> ⚠️ **El cliente viaja como `cl`, no como `c`.** Apps Script reserva los parámetros `c` y `sid`. Con `c=` en la URL, Google responde "No se puede abrir el archivo en estos momentos" y el script ni siquiera se ejecuta.

**¿Por qué `@Cab_Nro` y no un campo de tabla?** En `RTCTLCT6.rpt` el número impreso sale del parámetro `{?NumeroDesde}`. En talonarios con `rtl_ContTransp = "3"` (formularios continuos) se le suma el número de página. Usando `@Cab_Nro`, el QR siempre coincide con el número impreso.

## Número de control `k`

Evita que alguien cambie el estado de otro remito editando la URL a mano. Es un hash simple, **no criptográfico**, suficiente para este uso:

```
s = remito + "|" + cliente          (cliente con espacios reemplazados por %20)
h = 7919
por cada carácter c de s:  h = (h * 31 + ASCII(c)) mod 999983
k = h
```

- Se calcula igual en Crystal (`@URL_QR`) y en Apps Script (`calcularControl_`).
- Valores verificados: `00001-00000001|000004` → **249497**; `00001-00000002|000005` → **331330**.
- Si se cambia la semilla (7919), el multiplicador (31) o el módulo (999983), hay que cambiarlos **en los dos lados**. Los QR ya impresos con la fórmula anterior dejarían de validar.

## Estados y reglas

| Estado actual | Botones habilitados |
|---|---|
| Sin registros | Despachado, Entregado, Rechazado |
| Despachado | Entregado, Rechazado |
| Rechazado | Despachado (reintento de entrega), Entregado |
| Entregado | Ninguno: es el estado final. La página muestra "Remito entregado ✓" |

- Los botones no permitidos se muestran grises y deshabilitados.
- **El servidor también valida la regla** dentro de un bloqueo (`LockService`): una página desactualizada o un doble toque no generan registros repetidos.
- Las reglas están en `CONFIG.TRANSICIONES` de `Codigo.gs`. El tratamiento de **Rechazado** es una decisión abierta (ver [pendientes](05-pendientes.md)).
- Si se marcó algo por error, se corrige a mano en el **Tablero** (columna "Estado actual").

## Flujo de uso

1. **Administración** emite e imprime el remito en Bejerman. El QR se genera solo al imprimir.
2. **Depósito** (opcional) escanea al cargar el camión → **Despachado**.
3. **Transportista** escanea en el cliente → **Entregado**, o **Rechazado** con una observación (cerrado, rechazó la mercadería, etc.).
4. **Dueño** mira el **Tablero** del Sheet: amarillo = en reparto, verde = entregado, rojo = rechazado.
5. La página recuerda el nombre del transportista en ese celular, así no lo elige cada vez.
