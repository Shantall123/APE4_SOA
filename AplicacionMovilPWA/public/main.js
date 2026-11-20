import {
  ErrorApi, iniciarSesion, obtenerIdentidad, listarVehiculos, listarCompradores,
  registrarVenta, consultarVenta, listarVentas, generarClaveIdempotencia,
} from './api.js';
import {
  guardarSesion, obtenerSesion, cerrarSesion, guardarPendiente, obtenerPendiente, borrarPendiente,
} from './sesion.js';

const $ = (selector) => document.querySelector(selector);

// ---------------------------------------------------------------- Referencias
const vistaLogin = $('#vista-login');
const vistaApp = $('#vista-app');
const subtitulo = $('#subtitulo');

const formLogin = $('#form-login');
const campoUsuario = $('#usuario');
const campoClave = $('#clave');
const botonAlternarClave = $('#alternarClave');
const botonIngresar = $('#ingresar');
const mensajeLogin = $('#mensaje-login');

const usuarioNombre = $('#usuario-nombre');
const usuarioRol = $('#usuario-rol');
const avisoRol = $('#aviso-rol');
const botonSalir = $('#salir');
const tabVenta = $('#tab-venta');
const tabHistorial = $('#tab-historial');
const seccionVenta = $('#seccion-venta');
const seccionHistorial = $('#seccion-historial');

const buscarVehiculo = $('#buscar-vehiculo');
const contenedorVehiculos = $('#vehiculos');
const selectComprador = $('#comprador');
const resumenVehiculo = $('#resumen-vehiculo');
const resumenComprador = $('#resumen-comprador');
const resumenPrecio = $('#resumen-precio');
const botonRegistrar = $('#registrar');
const mensajeVenta = $('#mensaje-venta');

const panelResultado = $('#resultado');
const resultadoHttp = $('#resultado-http');
const resultadoMensaje = $('#resultado-mensaje');
const resultadoDatos = $('#resultado-datos');
const botonConsultarEstado = $('#consultar-estado');
const botonReintentar = $('#reintentar');
const botonNuevaVenta = $('#nueva-venta');

const contenedorVentas = $('#ventas');
const mensajeHistorial = $('#mensaje-historial');
const conexion = $('#conexion');

const moneda = new Intl.NumberFormat('es-EC', { style: 'currency', currency: 'USD' });

// ---------------------------------------------------------------- Estado
let sesion = null;
let vehiculos = [];
let compradores = [];
let vehiculoSeleccionado = null;
let ultimaVenta = null;
let enviando = false;
let permisos = { vender: false, historial: false };

// ---------------------------------------------------------------- Utilidades
function mostrarMensaje(elemento, texto, tipo = 'neutro') {
  elemento.textContent = texto;
  elemento.className = `mensaje ${tipo}`;
}

function crearElemento(etiqueta, clase, contenido) {
  const elemento = document.createElement(etiqueta);
  if (clase) elemento.className = clase;
  // textContent impide interpretar HTML que pudiera venir de la API.
  if (contenido !== undefined) elemento.textContent = String(contenido);
  return elemento;
}

const texto = (v) => (v === null || v === undefined || v === '' ? '—' : String(v));

function precio(valor) {
  if (valor === null || valor === undefined || valor === '') return '—';
  const numero = Number(valor);
  return Number.isFinite(numero) ? moneda.format(numero) : texto(valor);
}

function fecha(valorUtc) {
  const f = new Date(valorUtc);
  return Number.isNaN(f.getTime()) ? texto(valorUtc) : f.toLocaleString('es-EC');
}

function claseEstado(estado) {
  const e = String(estado || '').toUpperCase();
  if (['DISPONIBLE', 'COMPLETADA', 'ENVIADA'].includes(e)) return 'disponible';
  if (['VENDIDO', 'FALLIDA'].includes(e)) return 'vendido';
  if (['EN_REVISION', 'PENDIENTE', 'RESERVADO'].includes(e)) return 'pendiente';
  return '';
}

/** Un 401 en cualquier operación termina la sesión y vuelve al login. */
function manejarError(error, elementoMensaje) {
  if (error instanceof ErrorApi && error.codigoHttp === 401) {
    salir('Su sesión venció o el token ya no es válido (HTTP 401). Inicie sesión nuevamente.', 'error');
    return true;
  }
  mostrarMensaje(elementoMensaje, error.message, 'error');
  return false;
}

// ---------------------------------------------------------------- Login
formLogin.addEventListener('submit', async (evento) => {
  evento.preventDefault();
  const usuario = campoUsuario.value.trim();
  const clave = campoClave.value;
  if (!usuario || !clave) {
    mostrarMensaje(mensajeLogin, 'Ingrese usuario y contraseña.', 'error');
    (usuario ? campoClave : campoUsuario).focus();
    return;
  }

  botonIngresar.disabled = true;
  mostrarMensaje(mensajeLogin, 'Validando credenciales…');
  try {
    const login = await iniciarSesion(usuario, clave);
    const identidad = await obtenerIdentidad(login.token);
    guardarSesion({ token: login.token, usuario: identidad.usuario, rol: identidad.rol, expiraEn: login.expiraEn });
    campoClave.value = '';
    mostrarMensaje(mensajeLogin, `HTTP ${login.codigoHttp}: sesión iniciada.`, 'exito');
    entrar();
  } catch (error) {
    mostrarMensaje(mensajeLogin, error.message, 'error');
    campoClave.select();
  } finally {
    botonIngresar.disabled = false;
  }
});

botonAlternarClave.addEventListener('click', () => {
  const revelar = campoClave.type === 'password';
  campoClave.type = revelar ? 'text' : 'password';
  botonAlternarClave.textContent = revelar ? 'Ocultar' : 'Mostrar';
  botonAlternarClave.setAttribute('aria-pressed', String(revelar));
  botonAlternarClave.setAttribute('aria-label', revelar ? 'Ocultar contraseña' : 'Mostrar contraseña');
});

function entrar() {
  sesion = obtenerSesion();
  if (!sesion) { salir('Inicie sesión para continuar.'); return; }

  vistaLogin.hidden = true;
  vistaApp.hidden = false;
  usuarioNombre.textContent = sesion.usuario;
  usuarioRol.textContent = sesion.rol || '—';

  permisos = permisosDe(sesion.rol);
  aplicarPermisos();

  mostrarPestana('venta');
  cargarVehiculos();
  if (permisos.vender) cargarCompradores();

  // Si quedó una venta sin confirmar (respuesta incierta), se ofrece verificarla.
  const pendiente = permisos.vender ? obtenerPendiente() : null;
  if (pendiente) {
    mostrarResultado({
      codigo: null,
      tipo: 'aviso',
      mensaje: 'Hay una venta enviada cuyo resultado no se confirmó. Use "Verificar / reintentar sin duplicar": '
        + 'se reenvía con la misma Idempotency-Key y el servidor no creará un duplicado.',
      incierto: true,
    });
  }
}

/**
 * Módulos visibles por rol (mismos roles que aplica el backend):
 *  - OPERADOR y ADMIN: registrar ventas y consultar "Mis ventas".
 *  - CONSULTA (o rol desconocido): solo el catálogo de vehículos disponibles.
 * Ocultar módulos es una ayuda de interfaz; el backend sigue validando con 403.
 */
const PERMISOS = {
  ADMIN: { vender: true, historial: true },
  OPERADOR: { vender: true, historial: true },
  CONSULTA: { vender: false, historial: false },
};

function permisosDe(rol) {
  return PERMISOS[rol] || PERMISOS.CONSULTA;
}

function aplicarPermisos() {
  const { vender, historial } = permisos;

  $('#pestanas').hidden = !(vender && historial);
  $('#panel-comprador').hidden = !vender;
  $('#panel-confirmar').hidden = !vender;
  if (!vender) panelResultado.hidden = true;

  // El panel de vehículos pasa a ser un catálogo de solo lectura.
  $('#paso-vehiculo').hidden = !vender;
  $('#titulo-vehiculo').textContent = vender ? 'Vehículo' : 'Vehículos disponibles';
  $('#ayuda-vehiculo').textContent = vender
    ? 'Solo se muestran vehículos DISPONIBLES. Toque uno para seleccionarlo.'
    : 'Catálogo de vehículos DISPONIBLES para la venta.';
  contenedorVehiculos.setAttribute('role', vender ? 'radiogroup' : 'list');

  subtitulo.textContent = vender
    ? 'Registro de ventas con JWT e Idempotency-Key'
    : 'Consulta del catálogo de vehículos disponibles';

  avisoRol.hidden = vender;
  if (!vender) {
    avisoRol.textContent = `Su rol (${sesion.rol || 'desconocido'}) es de solo consulta: `
      + 'puede ver los vehículos disponibles, pero no registrar ventas ni consultar ventas.';
  }
}

function salir(mensaje = 'Sesión cerrada.', tipo = 'neutro') {
  cerrarSesion();
  sesion = null;
  vehiculos = [];
  compradores = [];
  vehiculoSeleccionado = null;
  ultimaVenta = null;
  panelResultado.hidden = true;
  vistaApp.hidden = true;
  vistaLogin.hidden = false;
  subtitulo.textContent = 'Inicie sesión para registrar ventas';
  mostrarMensaje(mensajeLogin, mensaje, tipo);
  campoUsuario.focus();
}

botonSalir.addEventListener('click', () => {
  if (!window.confirm('¿Desea cerrar la sesión?')) return;
  salir('Sesión cerrada. El token fue eliminado.');
});

// ---------------------------------------------------------------- Pestañas
function mostrarPestana(nombre) {
  // Sin permiso de historial solo existe la vista principal.
  const venta = nombre === 'venta' || !permisos.historial;
  seccionVenta.hidden = !venta;
  seccionHistorial.hidden = venta;
  tabVenta.classList.toggle('activa', venta);
  tabHistorial.classList.toggle('activa', !venta);
  tabVenta.setAttribute('aria-pressed', String(venta));
  tabHistorial.setAttribute('aria-pressed', String(!venta));
  if (!venta) cargarHistorial();
}
tabVenta.addEventListener('click', () => mostrarPestana('venta'));
tabHistorial.addEventListener('click', () => mostrarPestana('historial'));

// ---------------------------------------------------------------- Vehículos
async function cargarVehiculos() {
  contenedorVehiculos.replaceChildren(crearElemento('div', 'vacio', 'Cargando vehículos disponibles…'));
  try {
    const r = await listarVehiculos(sesion.token, fetch, { estado: 'DISPONIBLE' });
    vehiculos = r.vehiculos;
    if (vehiculoSeleccionado && !vehiculos.some((v) => v.idVehiculo === vehiculoSeleccionado.idVehiculo)) {
      vehiculoSeleccionado = null;
    }
    pintarVehiculos();
  } catch (error) {
    vehiculos = [];
    if (!manejarError(error, mensajeVenta)) {
      contenedorVehiculos.replaceChildren(crearElemento('div', 'vacio', `No se pudo cargar el catálogo. ${error.message}`));
    }
  }
  actualizarResumen();
}

function pintarVehiculos() {
  const filtro = buscarVehiculo.value.trim().toLowerCase();
  const visibles = vehiculos.filter((v) =>
    `${v.marca} ${v.modelo} ${v.placa}`.toLowerCase().includes(filtro));

  contenedorVehiculos.replaceChildren();
  if (visibles.length === 0) {
    contenedorVehiculos.append(crearElemento('div', 'vacio', vehiculos.length === 0
      ? 'HTTP 200: no hay vehículos DISPONIBLES.'
      : 'Ningún vehículo coincide con la búsqueda.'));
    return;
  }

  for (const v of visibles) {
    const seleccionado = permisos.vender && vehiculoSeleccionado?.idVehiculo === v.idVehiculo;
    // Con permiso de venta la tarjeta es seleccionable; si no, es solo informativa.
    const tarjeta = permisos.vender
      ? crearElemento('button', `vehiculo opcion${seleccionado ? ' seleccionado' : ''}`)
      : crearElemento('article', 'vehiculo solo-lectura');
    if (permisos.vender) {
      tarjeta.type = 'button';
      tarjeta.setAttribute('role', 'radio');
      tarjeta.setAttribute('aria-checked', String(seleccionado));
    } else {
      tarjeta.setAttribute('role', 'listitem');
    }

    const cabecera = crearElemento('div', 'vehiculo-cabecera');
    const titulo = crearElemento('div');
    titulo.append(
      crearElemento('span', 'vehiculo-titulo', `${texto(v.marca)} ${texto(v.modelo)}`),
      crearElemento('span', 'vehiculo-id', `ID ${v.idVehiculo} · Placa ${texto(v.placa)} · ${texto(v.anio)} · ${texto(v.color)}`),
    );
    cabecera.append(titulo, crearElemento('span', 'precio-tag', precio(v.precio)));
    tarjeta.append(cabecera);
    if (seleccionado) tarjeta.append(crearElemento('span', 'marca-seleccion', '✓ Seleccionado'));

    if (permisos.vender) tarjeta.addEventListener('click', () => {
      vehiculoSeleccionado = v;
      pintarVehiculos();
      actualizarResumen();
    });
    contenedorVehiculos.append(tarjeta);
  }
}

buscarVehiculo.addEventListener('input', pintarVehiculos);
$('#recargar-vehiculos').addEventListener('click', cargarVehiculos);

// ---------------------------------------------------------------- Compradores
async function cargarCompradores() {
  selectComprador.replaceChildren(new Option('Cargando compradores…', ''));
  try {
    const r = await listarCompradores(sesion.token);
    compradores = r.compradores;
    selectComprador.replaceChildren(new Option(
      compradores.length ? 'Seleccione un comprador…' : 'No hay compradores registrados', ''));
    for (const c of compradores) {
      selectComprador.append(new Option(`${c.nombre} · ${c.correo} (ID ${c.id})`, String(c.id)));
    }
  } catch (error) {
    compradores = [];
    selectComprador.replaceChildren(new Option('No se pudieron cargar los compradores', ''));
    manejarError(error, mensajeVenta);
  }
  actualizarResumen();
}

selectComprador.addEventListener('change', actualizarResumen);
$('#recargar-compradores').addEventListener('click', cargarCompradores);

function compradorSeleccionado() {
  return compradores.find((c) => String(c.id) === selectComprador.value) || null;
}

// ---------------------------------------------------------------- Resumen y registro
function actualizarResumen() {
  const c = compradorSeleccionado();
  const v = vehiculoSeleccionado;
  resumenVehiculo.textContent = v ? `${v.marca} ${v.modelo} · ${v.placa} (ID ${v.idVehiculo})` : 'Sin seleccionar';
  resumenComprador.textContent = c ? `${c.nombre} (ID ${c.id})` : 'Sin seleccionar';
  resumenPrecio.textContent = v ? precio(v.precio) : '—';
  botonRegistrar.disabled = enviando || !(v && c);
  if (!enviando) {
    mostrarMensaje(mensajeVenta, v && c
      ? 'Listo para registrar. Revise los datos y pulse "Registrar venta".'
      : 'Seleccione un vehículo y un comprador.');
  }
}

botonRegistrar.addEventListener('click', async () => {
  const v = vehiculoSeleccionado;
  const c = compradorSeleccionado();
  if (!permisos.vender || !v || !c) return;

  // Se reutiliza la clave si hay una venta pendiente con los mismos datos;
  // así un reintento nunca genera una segunda venta.
  const anterior = obtenerPendiente();
  const mismos = anterior && anterior.vehiculoId === v.idVehiculo && anterior.compradorId === c.id;
  const pendiente = mismos ? anterior : {
    vehiculoId: v.idVehiculo, compradorId: c.id, clave: generarClaveIdempotencia(),
    descripcion: `${v.marca} ${v.modelo} (${v.placa}) → ${c.nombre}`,
  };
  guardarPendiente(pendiente);
  await enviarVenta(pendiente);
});

botonReintentar.addEventListener('click', async () => {
  const pendiente = obtenerPendiente();
  if (pendiente) await enviarVenta(pendiente);
});

async function enviarVenta(pendiente) {
  if (!permisos.vender) return;
  enviando = true;
  botonRegistrar.disabled = true;
  botonReintentar.disabled = true;
  mostrarMensaje(mensajeVenta, 'Registrando la venta… (POST /api/ventas con Idempotency-Key)');

  try {
    const r = await registrarVenta(sesion.token,
      { vehiculoId: pendiente.vehiculoId, compradorId: pendiente.compradorId }, pendiente.clave);
    borrarPendiente();
    ultimaVenta = r.venta;

    const mensajes = {
      201: 'Venta registrada y confirmada por el servidor.',
      200: 'Esta venta ya estaba registrada con la misma Idempotency-Key: no se creó un duplicado.',
      202: `Venta recibida con estado ${r.venta?.estado}. El servidor aún la está conciliando; consulte su estado en unos segundos.`,
    };
    mostrarResultado({
      codigo: r.codigoHttp,
      tipo: r.codigoHttp === 202 ? 'aviso' : 'exito',
      mensaje: mensajes[r.codigoHttp] || `HTTP ${r.codigoHttp}: operación aceptada.`,
      venta: r.venta,
    });
    mostrarMensaje(mensajeVenta, `HTTP ${r.codigoHttp}: venta #${r.venta?.id} — ${r.venta?.estado}.`, 'exito');
    vehiculoSeleccionado = null;
    selectComprador.value = '';
    await cargarVehiculos(); // el vehículo vendido deja de aparecer como DISPONIBLE
  } catch (error) {
    if (error instanceof ErrorApi && error.codigoHttp === 401) {
      manejarError(error, mensajeVenta);
      return;
    }
    if (error instanceof ErrorApi && error.incierto) {
      // Sin confirmación: se conserva la clave para verificar sin duplicar.
      mostrarResultado({
        codigo: error.codigoHttp,
        tipo: 'aviso',
        mensaje: `${error.message} No se sabe si la venta se registró. Pulse "Verificar / reintentar sin duplicar": `
          + 'se reenvía con la MISMA Idempotency-Key y, si ya existía, el servidor devolverá la venta original (HTTP 200).',
        incierto: true,
      });
      mostrarMensaje(mensajeVenta, 'Resultado incierto: verifique la venta antes de intentar otra.', 'error');
    } else {
      // Respuesta definitiva de rechazo (403, 404, 409, 422…): no hay venta nueva.
      borrarPendiente();
      const explicacion = {
        403: 'Su usuario no tiene permiso para registrar ventas (se requiere OPERADOR o ADMIN).',
        409: 'El servidor rechazó la venta: el vehículo no está disponible o la operación entra en conflicto.',
        422: 'El servidor rechazó los datos enviados.',
        404: 'El comprador o el vehículo no existe.',
      }[error.codigoHttp];
      mostrarResultado({
        codigo: error.codigoHttp,
        tipo: 'error',
        mensaje: `${explicacion ? explicacion + ' ' : ''}${error.message}`,
      });
      mostrarMensaje(mensajeVenta, error.message, 'error');
      if (error.codigoHttp === 409) await cargarVehiculos();
    }
  } finally {
    enviando = false;
    botonReintentar.disabled = false;
    actualizarResumen();
  }
}

/** Muestra el resultado real: código HTTP, mensaje y datos de la venta. */
function mostrarResultado({ codigo, tipo, mensaje, venta = null, incierto = false, origen = 'POST /api/ventas' }) {
  panelResultado.hidden = false;
  panelResultado.className = `panel resultado ${tipo}`;
  resultadoHttp.textContent = codigo ? `HTTP ${codigo}` : 'Sin respuesta';
  resultadoHttp.className = `http ${tipo}`;
  $('#titulo-resultado').textContent = origen;
  resultadoMensaje.textContent = mensaje;

  resultadoDatos.replaceChildren();
  if (venta) {
    const filas = [
      ['ID de venta', venta.id],
      ['Estado', venta.estado],
      ['Notificación', venta.notificacion],
      ['Vehículo', `ID ${venta.vehiculoId}`],
      ['Comprador', `ID ${venta.compradorId}`],
      ['Precio confirmado', venta.precioAcordado ? `${precio(venta.precioAcordado)} ${venta.moneda || ''}` : '—'],
      ['Fecha', fecha(venta.fechaUTC)],
      ['Operación', venta.operacionId],
    ];
    for (const [etiqueta, valor] of filas) {
      const fila = crearElemento('div');
      const dd = crearElemento('dd', etiqueta === 'Estado' ? `estado ${claseEstado(valor)}` : '', texto(valor));
      fila.append(crearElemento('dt', '', etiqueta), dd);
      resultadoDatos.append(fila);
    }
  } else if (incierto) {
    const p = obtenerPendiente();
    if (p) {
      const fila = crearElemento('div');
      fila.append(crearElemento('dt', '', 'Venta enviada'), crearElemento('dd', '', p.descripcion));
      resultadoDatos.append(fila);
    }
  }

  botonConsultarEstado.hidden = !venta?.id;
  botonReintentar.hidden = !incierto;
  panelResultado.scrollIntoView({ behavior: 'smooth', block: 'start' });
}

botonConsultarEstado.addEventListener('click', async () => {
  if (!ultimaVenta?.id) return;
  botonConsultarEstado.disabled = true;
  try {
    const r = await consultarVenta(sesion.token, ultimaVenta.id);
    ultimaVenta = r.venta;
    mostrarResultado({
      codigo: r.codigoHttp,
      tipo: r.venta.estado === 'COMPLETADA' ? 'exito' : r.venta.estado === 'FALLIDA' ? 'error' : 'aviso',
      mensaje: `Estado actual consultado en el servidor: ${r.venta.estado}.`,
      venta: r.venta,
      origen: `GET /api/ventas/${r.venta.id}`,
    });
  } catch (error) {
    manejarError(error, mensajeVenta);
  } finally {
    botonConsultarEstado.disabled = false;
  }
});

botonNuevaVenta.addEventListener('click', () => {
  if (obtenerPendiente()
      && !window.confirm('Hay una venta sin confirmar. Si continúa, perderá la opción de verificarla sin duplicar. ¿Continuar?')) {
    return;
  }
  borrarPendiente();
  panelResultado.hidden = true;
  ultimaVenta = null;
  vehiculoSeleccionado = null;
  selectComprador.value = '';
  pintarVehiculos();
  actualizarResumen();
  window.scrollTo({ top: 0, behavior: 'smooth' });
});

// ---------------------------------------------------------------- Mis ventas
async function cargarHistorial() {
  if (!permisos.historial) return;
  contenedorVentas.replaceChildren();
  mostrarMensaje(mensajeHistorial, 'Consultando ventas…');
  try {
    const r = await listarVentas(sesion.token, { limit: 15 });
    mostrarMensaje(mensajeHistorial,
      `HTTP ${r.codigoHttp}: ${r.ventas.length} de ${r.total} venta(s).`, 'exito');
    if (r.ventas.length === 0) {
      contenedorVentas.append(crearElemento('div', 'vacio', 'Aún no hay ventas registradas por su usuario.'));
      return;
    }
    for (const venta of r.ventas) contenedorVentas.append(tarjetaVenta(venta));
  } catch (error) {
    manejarError(error, mensajeHistorial);
  }
}

function tarjetaVenta(venta) {
  const tarjeta = crearElemento('button', 'vehiculo opcion');
  tarjeta.type = 'button';
  const cabecera = crearElemento('div', 'vehiculo-cabecera');
  const titulo = crearElemento('div');
  titulo.append(
    crearElemento('span', 'vehiculo-titulo', `Venta #${venta.id}`),
    crearElemento('span', 'vehiculo-id', `${fecha(venta.fechaUTC)} · Vehículo ${venta.vehiculoId} · Comprador ${venta.compradorId}`),
  );
  cabecera.append(titulo, crearElemento('span', `estado ${claseEstado(venta.estado)}`, venta.estado));
  const pie = crearElemento('div', 'venta-pie');
  pie.append(
    crearElemento('span', 'precio-tag', venta.precioAcordado ? precio(venta.precioAcordado) : '—'),
    crearElemento('span', 'vehiculo-id', `Notificación: ${texto(venta.notificacion)}`),
  );
  tarjeta.append(cabecera, pie);

  tarjeta.addEventListener('click', async () => {
    try {
      const r = await consultarVenta(sesion.token, venta.id);
      ultimaVenta = r.venta;
      mostrarPestana('venta');
      mostrarResultado({
        codigo: r.codigoHttp,
        tipo: r.venta.estado === 'COMPLETADA' ? 'exito' : r.venta.estado === 'FALLIDA' ? 'error' : 'aviso',
        mensaje: `Estado actual consultado en el servidor: ${r.venta.estado}.`,
        venta: r.venta,
        origen: `GET /api/ventas/${r.venta.id}`,
      });
    } catch (error) {
      manejarError(error, mensajeHistorial);
    }
  });
  return tarjeta;
}

$('#recargar-ventas').addEventListener('click', cargarHistorial);

// ---------------------------------------------------------------- Inicio
function actualizarConexion() {
  conexion.textContent = navigator.onLine ? 'Navegador conectado' : 'Sin conexión de red';
}
window.addEventListener('online', actualizarConexion);
window.addEventListener('offline', actualizarConexion);
actualizarConexion();

if (obtenerSesion()) entrar(); else campoUsuario.focus();

// El Service Worker conserva únicamente recursos visuales, nunca el JWT ni /api.
if ('serviceWorker' in navigator && window.isSecureContext) {
  window.addEventListener('load', () => {
    navigator.serviceWorker.register('/sw.js').catch(() => {
      // La aplicación funciona aunque no se instale el modo PWA.
    });
  });
}
