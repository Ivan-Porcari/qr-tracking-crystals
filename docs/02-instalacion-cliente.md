# 2. Instalación en un cliente

Checklist para llevar la solución a producción. Probar siempre primero en un entorno de prueba.

## 0. Relevamiento previo

- [ ] Versión del runtime de Crystal en las PC que imprimen: `craxdrt.dll` en `C:\Program Files (x86)\Business Objects\Common\3.5\bin\` → Propiedades → Detalles. Debe ser **11.5.x** (Crystal XI R2).
- [ ] Lista de **todas las PC** desde donde se imprimen remitos (incluido el servidor, si se imprime desde ahí).
- [ ] RPT de remito que usa el cliente (normalmente `RTCTLCT6.rpt`) y dónde está: carpeta local o de red.
- [ ] Nombre de la fórmula que imprime el número de remito (en el RPT estándar: `@Cab_Nro`) y del campo de código de cliente (`TmpRem.CodigoCliProv`).
- [ ] Tipo de impresora de remitos. Láser o chorro de tinta: OK. **Matricial: probar antes**, el QR puede no salir legible.
- [ ] Cuenta de Google que va a usar el cliente: Gmail dedicada o Google Workspace (ver punto 3).

## 1. DLL en cada PC que imprime remitos

1. Copiar a la PC la carpeta `scripts\` y la DLL (`ufl\bin\CRUFLQRRemito.dll`), o el repositorio completo.
2. **Cerrar Bejerman y Crystal.**
3. Abrir **PowerShell como administrador** y ejecutar:
   ```powershell
   powershell -ExecutionPolicy Bypass -File .\scripts\instalar_qr_remito.ps1
   ```
4. El script:
   - copia la DLL a `C:\Bejerman\QR\ufl\` (se cambia con `-Destino "D:\otra\carpeta"`);
   - la registra con `RegAsm` de 32 bits (`/codebase /tlb`). La advertencia de "nombre seguro" es esperable;
   - copia `u2lcom.dll` a `C:\Windows\crystal\` si falta (el visor de Bejerman busca las UFL ahí);
   - genera un QR de prueba desde un proceso de 32 bits. Tiene que terminar con **"Instalación completa"**.

> Si la carpeta de destino es de red o la usan varios usuarios, verificar que todos los usuarios que imprimen tengan permiso de lectura sobre la DLL.

## 2. RPT del remito

1. **Backup** del RPT original.
2. En una PC con **Crystal Reports XI R2** y la DLL instalada (punto 1), abrir el RPT **del cliente**. No reemplazarlo por el de prueba: puede tener personalizaciones propias.
3. Aplicar `crystal/formulas_crystal_remito.txt`:
   - crear `@URL_QR`, con la **URL del Web App del cliente** (punto 3) en `base`;
   - insertar la imagen en el pie de página (~2,5 x 2,5 cm, **con margen inferior**) con "Ubicación del gráfico" = `QRRemitoMainGenerarQR({@URL_QR})`.
4. Guardar y cerrar el RPT (si queda abierto, Bejerman puede no poder usarlo).
5. Imprimir un remito de prueba desde Bejerman y escanear el QR.

## 3. Google Sheet y Web App

### Cuenta de Google
- **Gmail dedicada** (por ejemplo `remitos.cliente@gmail.com`): lo más simple.
- **Google Workspace del cliente:** funciona si el administrador permite el acceso de "Cualquier usuario" a las aplicaciones web. Verificarlo con la prueba del paso 7.

### Pasos
1. Con la cuenta elegida (en un navegador o perfil donde **solo** esté esa cuenta), crear una hoja de cálculo: "Seguimiento de remitos".
2. **Extensiones → Apps Script.**
3. Reemplazar `Código.gs` por `apps_script/Codigo.gs`. Crear el archivo HTML **`Pagina`** (sin ".html") con `apps_script/Pagina.html`. Guardar.
4. Ejecutar **`configurar`** (autorizar los permisos) y después **`probarControl`**: el registro tiene que mostrar `249497`.
5. **Implementar → Nueva implementación → Aplicación web**: Ejecutar como **Yo**, acceso **Cualquier usuario** → **Implementar**. Copiar la URL (termina en `/exec`).
6. Cargar los transportistas en la hoja **Transportistas** y, opcionalmente, los clientes en **Clientes**.
7. **Prueba de acceso anónimo:** desde un celular con **datos móviles** y en **incógnito**, abrir:
   ```
   <URL>/exec?r=00001-00000001&cl=000004&k=249497
   ```
   Tiene que abrir la página del remito. Si dice "No se puede abrir el archivo", ver [solución de problemas](04-solucion-de-problemas.md).

> **Cada vez que se cambie el código:** Implementar → Gestionar implementaciones → ✏️ → Versión: **Nueva versión** → Implementar. Sin esto, la URL `/exec` sigue usando la versión anterior. La URL no cambia.

## 4. Puesta en marcha

- [ ] Imprimir un remito real, escanear y marcar **Despachado** → aparece amarillo en el Tablero.
- [ ] Volver a escanear → el botón Despachado aparece gris.
- [ ] Marcar **Entregado** → al escanear de nuevo muestra "Remito entregado ✓".
- [ ] Compartir el Sheet con el dueño (solo lectura o edición, según corresponda).
- [ ] Explicar a los transportistas: escanear con la cámara, elegir el nombre (el celular lo recuerda) y tocar el estado.
