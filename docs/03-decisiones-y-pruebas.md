# 3. Decisiones y pruebas

Registro de lo que se investigó, probó y decidió durante el desarrollo (octubre 2026).

## Entorno relevado

- **Runtime de Crystal de Bejerman:** Crystal Reports XI R2 — `craxdrt.dll` 11.5.0.313 (formato RDC / ActiveX Designer).
- **Diseñador usado:** Crystal Reports XI R2, la misma versión que el runtime, así que no hay riesgo de compatibilidad al guardar.
- **Librería de rutinas de Bejerman:** `C:\Program Files (x86)\SistemasBejermanSQL\WFRCRRUT.DLL`, expuesta en Crystal mediante `u2lsb.dll` (funciones `RutinasComunesMain…`, clase COM `SbuflRutinasComunes.Main`).
- **Carpetas de UFL:** el visor de reportes de Bejerman (`SBRptRdoEXEMP`) carga las UFL desde `C:\Windows\crystal\`. El diseñador las carga desde `C:\Program Files (x86)\Business Objects\Common\3.5\bin\`.
- **Tablas del remito (`RTCTLCT6.rpt`):** `SegTiposV`, `TmpRem` (tabla temporal con cabecera y datos del cliente), `Talonar`, `TipComp`, `RelTCompLetTal`, `ParamGen`. No usa `SegCabV`.

## Cómo hace Bejerman el QR de AFIP en la factura (`VTALCT6.rpt`)

- Objeto `Picture1` en "Pie de página b". La imagen incrustada es un marcador de 20x20 px.
- "Ubicación del gráfico" = `{@PathImagenQR}`.
- `@PathImagenQR` llama a `RutinasComunesMainLlamarFuncion("GeneraQRCode", …)` con los datos de AFIP (fecha, CUIT, punto de venta, tipo, número, importe, moneda, cotización, documento del receptor, tipo de CAE, CAI). La función genera la imagen y devuelve la ruta.
- "Suprimir": solo se muestra en FC/ND/NC con punto de venta de factura electrónica (`pvt_factelec = 'S'`) y CAI cargado.

## Opciones evaluadas para generar el QR

| Opción | Resultado |
|---|---|
| **A.** Reutilizar una función de Bejerman | ❌ `GeneraQRCode` solo arma el QR de AFIP. `GenQR` (visto en la DLL) no se puede llamar desde `LlamarFuncion`. No existe una función de QR con texto libre. |
| **B.** Pregenerar los QR por número de remito en una carpeta | Viable, pero el QR solo podría llevar el número y hay que regenerar. Quedó como alternativa. |
| **C.** Script que vigila la base y genera los QR | Descartada: un proceso más corriendo y el riesgo de imprimir antes de que se genere el QR. |
| **D.** UFL propia para Crystal ✅ | **Elegida.** El QR se genera en el momento de imprimir, con cualquier dato, igual que el de AFIP. |

## Pruebas realizadas

| Prueba | Resultado |
|---|---|
| Crystal XI R2 carga una imagen desde una **URL** en "Ubicación del gráfico" | ❌ No (solo archivos en disco) |
| Crystal XI R2 carga un **.bmp desde disco** | ✅ Sí, y el QR se escanea |
| `LlamarFuncion` desde el diseñador suelto | ❌ Error OLE 800A01AD: necesita el contexto de Bejerman |
| `LlamarFuncion("MonedaSegunAFIP")` imprimiendo desde Bejerman | ✅ Devuelve "PES" |
| `LlamarFuncion("GenQR")` / nombre inexistente | Devuelve vacío: `GenQR` no se puede llamar |
| `LlamarFuncion("GeneraQRCode")` con datos de prueba en el remito | Vacío; además solo sirve para AFIP |
| UFL propia v0.1 (`ClassInterfaceType.AutoDual`) | ❌ Crystal no la muestra |
| UFL propia v0.2 (interfaz dual explícita) | ✅ Aparece en "UFLs de Visual Basic (u2lcom.dll)" |
| UFL v0.3 `GenerarQR` en el diseñador | ✅ QR visible y escaneable |
| UFL imprimiendo desde Bejerman | ❌ "Falta UFL 'u2lcom.dll'" → ✅ resuelto copiando `u2lcom.dll` a `C:\Windows\crystal\` |
| Codificador QR | ✅ 38 códigos de 1 a 1.200 caracteres leídos con zbar y OpenCV |
| Control `k` Crystal vs. Apps Script | ✅ Coinciden (249497, 331330) |
| Web App con `c=` en la URL | ❌ "No se puede abrir el archivo" (parámetro reservado) → ✅ renombrado a `cl` |
| Web App (cuenta Workspace) desde un celular con datos móviles e incógnito | ✅ Abre |
| Flujo completo: imprimir → escanear → registrar → Tablero | ✅ Varios remitos |
| Bloqueo de estados repetidos (v1.2) | ✅ |

## Lecciones aprendidas

1. **UFL en .NET para Crystal:** no usar `ClassInterfaceType.AutoDual`. .NET expone también `ToString`, `Equals`, `GetHashCode` y `GetType`; `GetType` devuelve un tipo que Crystal no entiende y descarta el componente entero. Usar una **interfaz dual explícita** + `ClassInterfaceType.None`.
2. **ProgID con prefijo `CRUFL`** obligatorio; el nombre de la función en Crystal es ProgID sin prefijo + clase + método.
3. **Compilar en x86 y registrar con el RegAsm de 32 bits** (`Framework`, no `Framework64`).
4. **`u2lcom.dll` en `C:\Windows\crystal\`**: el visor de Bejerman no busca en la carpeta de Crystal.
5. Si una DLL quedó cargada (Crystal, Bejerman o un PowerShell de 32 bits que creó el objeto), no se puede sobrescribir (CS0016): cerrar esos procesos.
6. **Apps Script reserva los parámetros `c` y `sid`** en la URL de un Web App.
7. **Publicar una versión nueva** después de cada cambio de código (Gestionar implementaciones → Nueva versión). `/exec` no toma los cambios guardados por sí solo.
8. Con varias cuentas de Google abiertas en el navegador, es fácil crear o editar el proyecto en la cuenta equivocada. Verificar siempre el ID de la implementación (`AKfycb…`) y la cuenta en "Ejecutar como".
9. `LlamarFuncion` de Bejerman devuelve vacío (sin error) ante un nombre de función desconocido.
10. En el pie de página, un campo de texto no crece aunque tenga "Puede crecer": para ver textos largos, agrandar el campo a mano o bajar la fuente.

## Entorno de prueba usado

- PC con Bejerman ERP, base y RPT de prueba (con backup del RPT original).
- Web App de prueba: cuenta `soporte2@ebvinzia.com.ar`, ID `AKfycbzuTV2sSFlUbMKyQz2-tUSsWev4U5QgeSs5zTw34jvwuWdliAHDFmOQjFW5TEiZlzd1`.
- Un segundo proyecto en `ivanporcari.dev@gmail.com` (`AKfycbw0LZDi…`) quedó con la versión 1.0: descartar.
