# 4. Solución de problemas

## Al imprimir el remito

| Síntoma | Causa | Solución |
|---|---|---|
| Error **"Falta UFL 'u2lcom.dll' que implementa esta función"** | El visor de Bejerman no encuentra el puente COM de Crystal | Copiar `u2lcom.dll` a `C:\Windows\crystal\` (lo hace `instalar_qr_remito.ps1`). Cerrar y abrir Bejerman. |
| El QR **no aparece** y no hay error | La DLL devolvió vacío | Revisar `%TEMP%\QRRemito_error.log` del usuario que imprime. Verificar que la DLL esté registrada (correr de nuevo el instalador). |
| El QR sale **cortado** | La imagen excede la sección o el borde de la hoja | Ajustar el tamaño (~2,5 x 2,5 cm) y dejar margen inferior dentro del pie de página. |
| El QR sale pero **no se escanea** | Muy chico, impresora matricial o con poca tinta | Agrandar un poco el QR; probar en otra impresora. |
| Sale el QR del remito anterior | Muy improbable: los archivos se nombran por el hash del contenido | Revisar que `@URL_QR` use `{@Cab_Nro}` y el cliente correctos. |

## En el diseñador de Crystal

| Síntoma | Causa | Solución |
|---|---|---|
| No aparece **"UFLs de Visual Basic (u2lcom.dll)"** ni `QRRemitoMain…` | DLL no registrada, registrada en 64 bits o compilada como AutoDual | Correr el instalador como administrador. Cerrar y abrir Crystal (lee la lista al arrancar). |
| Error OLE **800A01AD** con `RutinasComunesMainLlamarFuncion` | Las funciones de Bejerman necesitan el contexto de Bejerman | Normal en el diseñador; probar imprimiendo desde Bejerman. (No afecta a `QRRemitoMain…`.) |
| "No se han pasado suficientes parámetros a esta función" | Se insertó la función con los paréntesis vacíos | Escribir el texto entre comillas: `QRRemitoMainGenerarQR("…")`. |
| Un campo largo se ve cortado en el pie | Las secciones de pie de página no crecen | Agrandar el campo a mano o reducir la fuente. |

## Compilación / registro de la DLL

| Síntoma | Causa | Solución |
|---|---|---|
| `error CS0016: … está siendo utilizado por otro proceso` | Crystal, Bejerman o un PowerShell de 32 bits tienen la DLL cargada | Cerrar esos procesos (ver `crw32.exe` y `SBRptRdoEXEMP.exe` en el Administrador de tareas) y volver a compilar. |
| RegAsm: "warning RA0000 … sin signo con /codebase" | La DLL no tiene nombre seguro | Esperable; no impide el funcionamiento (ver [pendientes](05-pendientes.md)). |
| Prueba rápida fuera de Crystal | — | PowerShell de 32 bits (`C:\Windows\SysWOW64\WindowsPowerShell\v1.0\powershell.exe`): `$o = New-Object -ComObject CRUFLQRRemito.Main; $o.GenerarQR("hola")` |

## Web App / celular

| Síntoma | Causa | Solución |
|---|---|---|
| **"No se puede abrir el archivo en estos momentos"** (pantalla de Google Drive) | 1) La URL tiene el parámetro **`c=`** (reservado por Apps Script). 2) Varias cuentas de Google abiertas en el navegador. 3) La política de Workspace bloquea el acceso anónimo. | 1) Usar `cl=`. 2) Probar en incógnito o con datos móviles. 3) Usar una Gmail dedicada o pedir al administrador que habilite el acceso público a Web Apps. |
| **"Código QR no válido"** | El control `k` no coincide | Verificar que la versión publicada sea la última (Nueva versión), que la URL del RPT apunte al Web App correcto y que la fórmula use `&cl=`. |
| No aparece ningún `doGet` en **Ejecuciones** | El pedido no llega al script (ver la primera fila de esta tabla) | Revisar la URL y los parámetros. |
| Los cambios de código no se ven | No se publicó una versión nueva | Gestionar implementaciones → ✏️ → Nueva versión → Implementar. |
| "El nombre no está en la lista de transportistas" | El nombre se borró o se cambió en la hoja | Agregarlo de nuevo en **Transportistas**. |
| "No se registró: el remito ya está …" | Otro escaneo cambió el estado mientras la página estaba abierta | Volver a escanear para ver el estado actualizado. |
| Diagnóstico de parámetros | — | Agregar temporalmente en `doGet`: `console.log('PARAMS ' + JSON.stringify(p) + ' | esperado=' + calcularControl_(remito, cliente));` y mirar **Ejecuciones → doGet**. |
