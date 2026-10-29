/**
 * Seguimiento de remitos por QR - Web App (Google Apps Script)
 * Version 1.2 (bloquea estados repetidos: Entregado es final; parametro del cliente "cl")
 *
 * El QR impreso en el remito abre:
 *   https://script.google.com/macros/s/<ID>/exec?r=<remito>&cl=<cliente>&k=<control>
 *
 * IMPORTANTE: el cliente viaja como "cl" y NO como "c". Apps Script reserva los
 * parametros "c" y "sid": si la URL trae "c=", Google responde "No se puede abrir el
 * archivo en estos momentos" y el script ni siquiera se ejecuta.
 *
 * Hojas (las crea la funcion configurar()):
 *   Tablero         -> una fila por remito con su ESTADO ACTUAL (lo mantiene el script)
 *   Eventos         -> historial: una fila por cada vez que alguien marca un estado
 *   Transportistas  -> lista de nombres que aparecen en el desplegable (la mantiene el duenio)
 *   Clientes        -> (opcional) codigo y razon social, para mostrar el nombre del cliente
 */

// ---------------------------------------------------------------- Configuracion
var CONFIG = {
  // Debe coincidir con la formula @URL_QR del RPT (semilla, multiplicador y modulo).
  SEMILLA: 7919,
  MULTIPLICADOR: 31,
  MODULO: 999983,
  ESTADOS: ['Despachado', 'Entregado', 'Rechazado'],
  // Estados que se pueden marcar segun el estado actual del remito ('' = sin registros).
  // Entregado es final: no permite ningun cambio (si hubo un error, se corrige a mano en el Sheet).
  TRANSICIONES: {
    '':           ['Despachado', 'Entregado', 'Rechazado'],
    'Despachado': ['Entregado', 'Rechazado'],
    'Rechazado':  ['Despachado', 'Entregado'],
    'Entregado':  []
  },
  HOJA_TABLERO: 'Tablero',
  HOJA_EVENTOS: 'Eventos',
  HOJA_TRANSPORTISTAS: 'Transportistas',
  HOJA_CLIENTES: 'Clientes'
};

// ---------------------------------------------------------------- Web App
function doGet(e) {
  var p = (e && e.parameter) || {};
  var remito = String(p.r || '').trim();
  var cliente = String(p.cl || '').trim();
  var k = String(p.k || '').trim();

  var t = HtmlService.createTemplateFromFile('Pagina');
  t.datos = JSON.stringify(datosPagina_(remito, cliente, k));
  return t.evaluate()
    .setTitle('Remito ' + (remito || ''))
    .addMetaTag('viewport', 'width=device-width, initial-scale=1');
}

function datosPagina_(remito, cliente, k) {
  if (!remito || !k || !controlValido_(remito, cliente, k)) {
    return { valido: false };
  }
  var ultimo = ultimoEstado_(remito);
  return {
    valido: true,
    remito: remito,
    cliente: cliente,
    k: k,
    nombreCliente: nombreCliente_(cliente),
    estados: CONFIG.ESTADOS,
    permitidos: estadosPermitidos_(ultimo),
    transportistas: listaTransportistas_(),
    ultimo: ultimo
  };
}

function estadosPermitidos_(ultimo) {
  var actual = (ultimo && ultimo.estado) ? String(ultimo.estado) : '';
  var lista = CONFIG.TRANSICIONES[actual];
  return lista ? lista.slice() : CONFIG.ESTADOS.slice();
}

/**
 * Lo llama la pagina (google.script.run) cuando el transportista confirma un estado.
 * datos = { r, c, k, estado, transportista, observacion }
 */
function registrar(datos) {
  datos = datos || {};
  var remito = String(datos.r || '').trim();
  var cliente = String(datos.c || '').trim();
  var k = String(datos.k || '').trim();
  var estado = String(datos.estado || '').trim();
  var transportista = String(datos.transportista || '').trim();
  var obs = String(datos.observacion || '').trim().substring(0, 500);

  if (!controlValido_(remito, cliente, k)) throw new Error('El codigo del remito no es valido.');
  if (CONFIG.ESTADOS.indexOf(estado) < 0) throw new Error('Estado no valido.');
  if (!transportista) throw new Error('Elegi tu nombre.');
  if (listaTransportistas_().indexOf(transportista) < 0) throw new Error('El nombre no esta en la lista de transportistas.');

  var lock = LockService.getScriptLock();
  lock.waitLock(20000);
  try {
    // Se vuelve a leer el estado actual dentro del bloqueo: si la pagina estaba
    // desactualizada o se toco dos veces el boton, no se registra un estado repetido.
    var actual = ultimoEstado_(remito);
    if (estadosPermitidos_(actual).indexOf(estado) < 0) {
      throw new Error(actual && actual.estado
        ? 'El remito ya esta "' + actual.estado + '". No se puede marcar como "' + estado + '".'
        : 'No se puede marcar como "' + estado + '".');
    }
    var ss = SpreadsheetApp.getActive();
    var ahora = new Date();
    ss.getSheetByName(CONFIG.HOJA_EVENTOS)
      .appendRow([ahora, "'" + remito, "'" + cliente, estado, transportista, obs]);
    actualizarTablero_(ss, remito, cliente, estado, transportista, obs, ahora);
  } finally {
    lock.releaseLock();
  }
  var nuevo = ultimoEstado_(remito);
  return { ok: true, ultimo: nuevo, permitidos: estadosPermitidos_(nuevo) };
}

// ---------------------------------------------------------------- Control "k"
// Replica exacta de la formula de Crystal:
//   s = remito + "|" + cliente (con espacios como %20)
//   h = 7919; por cada caracter: h = (h * 31 + ASCII) mod 999983
function calcularControl_(remito, cliente) {
  var s = remito + '|' + String(cliente).replace(/ /g, '%20');
  var h = CONFIG.SEMILLA;
  for (var i = 0; i < s.length; i++) {
    h = (h * CONFIG.MULTIPLICADOR + s.charCodeAt(i)) % CONFIG.MODULO;
  }
  return h;
}

function controlValido_(remito, cliente, k) {
  return String(calcularControl_(remito, cliente)) === String(k);
}

// ---------------------------------------------------------------- Datos auxiliares
function listaTransportistas_() {
  var sh = SpreadsheetApp.getActive().getSheetByName(CONFIG.HOJA_TRANSPORTISTAS);
  if (!sh || sh.getLastRow() < 2) return [];
  return sh.getRange(2, 1, sh.getLastRow() - 1, 1).getValues()
    .map(function (f) { return String(f[0]).trim(); })
    .filter(function (n) { return n; });
}

function nombreCliente_(codigo) {
  var sh = SpreadsheetApp.getActive().getSheetByName(CONFIG.HOJA_CLIENTES);
  if (!sh || sh.getLastRow() < 2) return '';
  var filas = sh.getRange(2, 1, sh.getLastRow() - 1, 2).getValues();
  for (var i = 0; i < filas.length; i++) {
    if (String(filas[i][0]).trim() === codigo) return String(filas[i][1]).trim();
  }
  return '';
}

function ultimoEstado_(remito) {
  var sh = SpreadsheetApp.getActive().getSheetByName(CONFIG.HOJA_TABLERO);
  var fila = buscarFilaTablero_(sh, remito);
  if (!fila) return null;
  var v = sh.getRange(fila, 1, 1, 7).getValues()[0];
  return {
    estado: v[3],
    fecha: v[4] ? Utilities.formatDate(new Date(v[4]), Session.getScriptTimeZone(), 'dd/MM HH:mm') : '',
    transportista: v[5]
  };
}

function buscarFilaTablero_(sh, remito) {
  if (!sh || sh.getLastRow() < 2) return 0;
  var hallado = sh.getRange(2, 1, sh.getLastRow() - 1, 1)
    .createTextFinder(remito).matchEntireCell(true).findNext();
  return hallado ? hallado.getRow() : 0;
}

// Tablero: Remito | Cliente | Razon social | Estado actual | Fecha/hora | Transportista |
//          Observacion | Despachado | Entregado | Rechazado | Cant. registros
function actualizarTablero_(ss, remito, cliente, estado, transportista, obs, ahora) {
  var sh = ss.getSheetByName(CONFIG.HOJA_TABLERO);
  var fila = buscarFilaTablero_(sh, remito);
  if (!fila) {
    sh.appendRow(["'" + remito, "'" + cliente, nombreCliente_(cliente), '', '', '', '', '', '', '', 0]);
    fila = sh.getLastRow();
  }
  sh.getRange(fila, 4, 1, 4).setValues([[estado, ahora, transportista, obs]]);
  var colFechaEstado = { 'Despachado': 8, 'Entregado': 9, 'Rechazado': 10 }[estado];
  if (colFechaEstado) sh.getRange(fila, colFechaEstado).setValue(ahora);
  var cant = sh.getRange(fila, 11);
  cant.setValue((Number(cant.getValue()) || 0) + 1);
}

// ---------------------------------------------------------------- Configuracion inicial
/**
 * Ejecutar UNA VEZ desde el editor (selector de funciones -> configurar -> Ejecutar).
 * Crea las hojas con sus encabezados, formatos y colores. No borra datos existentes.
 */
function configurar() {
  var ss = SpreadsheetApp.getActive();

  var tablero = hoja_(ss, CONFIG.HOJA_TABLERO, [
    'Remito', 'Cliente', 'Razon social', 'Estado actual', 'Fecha/hora', 'Transportista',
    'Observacion', 'Despachado', 'Entregado', 'Rechazado', 'Cant. registros']);
  tablero.getRange('E:E').setNumberFormat('dd/mm/yyyy hh:mm');
  tablero.getRange('H:J').setNumberFormat('dd/mm/yyyy hh:mm');
  tablero.getRange('A:B').setNumberFormat('@');
  var rangoEstado = tablero.getRange('D2:D');
  var reglas = [
    ['Entregado', '#c8e6c9'],
    ['Despachado', '#fff3c4'],
    ['Rechazado', '#ffcdd2']
  ].map(function (r) {
    return SpreadsheetApp.newConditionalFormatRule()
      .whenTextEqualTo(r[0]).setBackground(r[1]).setRanges([rangoEstado]).build();
  });
  tablero.setConditionalFormatRules(reglas);

  var eventos = hoja_(ss, CONFIG.HOJA_EVENTOS, [
    'Fecha/hora', 'Remito', 'Cliente', 'Estado', 'Transportista', 'Observacion']);
  eventos.getRange('A:A').setNumberFormat('dd/mm/yyyy hh:mm:ss');
  eventos.getRange('B:C').setNumberFormat('@');

  var transp = hoja_(ss, CONFIG.HOJA_TRANSPORTISTAS, ['Nombre']);
  if (transp.getLastRow() < 2) transp.getRange(2, 1, 2, 1).setValues([['Deposito'], ['Transportista 1']]);

  var clientes = hoja_(ss, CONFIG.HOJA_CLIENTES, ['Codigo', 'Razon social']);
  clientes.getRange('A:A').setNumberFormat('@');

  // Saca la "Hoja 1" vacia que trae un Sheet nuevo.
  var hoja1 = ss.getSheetByName('Hoja 1') || ss.getSheetByName('Sheet1');
  if (hoja1 && ss.getSheets().length > 1 && hoja1.getLastRow() === 0) ss.deleteSheet(hoja1);

  ss.setActiveSheet(tablero);
  return 'Listo';
}

function hoja_(ss, nombre, encabezados) {
  var sh = ss.getSheetByName(nombre) || ss.insertSheet(nombre);
  sh.getRange(1, 1, 1, encabezados.length).setValues([encabezados])
    .setFontWeight('bold').setBackground('#37474f').setFontColor('#ffffff');
  sh.setFrozenRows(1);
  return sh;
}

// ---------------------------------------------------------------- Prueba
/** Verifica el control con el remito de prueba (debe mostrar 249497 en el registro de ejecucion). */
function probarControl() {
  Logger.log(calcularControl_('00001-00000001', '000004'));
}
