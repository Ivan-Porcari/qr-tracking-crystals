# 5. Pendientes y mejoras

## Decisiones abiertas

### ¿"Rechazado" es un estado final?
Hoy un remito rechazado se puede volver a marcar como **Despachado** (reintento de entrega) o **Entregado**. Hay que definirlo con el cliente según cómo trabaje:

- **Si un rechazo cierra el remito**, en `apps_script/Codigo.gs`, `CONFIG.TRANSICIONES`, cambiar:
  ```javascript
  'Rechazado':  ['Despachado', 'Entregado'],
  ```
  por:
  ```javascript
  'Rechazado':  [],
  ```
  y en `Pagina.html` adaptar el aviso de estado final, que hoy dice "Remito entregado ✓", para que use el estado real.
- **Si se reintenta la entrega**, dejarlo como está. Opcionalmente, agregar un estado "Reprogramado".

Después del cambio: publicar una **nueva versión** del Web App.

### ¿El texto de la URL queda impreso en el remito?
Durante las pruebas se imprimió `@URL_QR` como texto, para comparar. En producción se puede quitar o dejar chico debajo del QR.

## Próximas fases

### Fase 2: Llevar los estados al ERP
- Exportar el **Tablero** (CSV) e importarlo a una tabla o campo adicional en SQL Server.
- Recomendado: una **tabla propia** (por ejemplo `remito_entregas`) relacionada con el remito, en lugar de escribir en tablas nativas de Bejerman.
- Evaluar un informe en Bejerman que lea esa tabla.

### Datos del cliente en la página
- Cargar la hoja **Clientes** con un export de Bejerman (código y razón social), para que la página y el Tablero muestren el nombre del cliente.

## Mejoras técnicas

- **Firmar la DLL con nombre seguro** (`sn.exe` / `/keyfile`) para eliminar la advertencia RA0000 de RegAsm y evitar conflictos de versiones.
- **Versionado de la DLL:** agregar `[assembly: AssemblyVersion(...)]` para identificar la versión instalada.
- **Instalador:** empaquetar DLL y script en un único ejecutable o MSI si hay muchas PC.
- **Tamaño del BMP:** fijar la resolución en el archivo para que Crystal lo muestre siempre del mismo tamaño físico.
- **Control `k`:** si se quisiera más seguridad, reemplazar el hash simple por un HMAC con una clave secreta (implica cambiar la fórmula de Crystal y el script, y reimprimir).
- **Transportistas:** opcionalmente, un PIN por transportista.
- **Foto del remito firmado o geolocalización** al marcar Entregado.
- Borrar el proyecto de prueba de Apps Script que quedó en Gmail (`AKfycbw0LZDi…`).
