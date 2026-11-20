/**
 * Cliente HTTP de la PWA. No conoce la interfaz ni accede a bases de datos.
 * Todas las rutas son relativas: server.mjs las reenvía a NGINX (mismo origen).
 *
 *  POST /oauth/token            → login (el servidor agrega las credenciales del cliente)
 *  GET  /api/identidad          → usuario y rol del token
 *  GET  /api/vehiculos          → catálogo (API Java)
 *  GET  /api/compradores        → compradores del usuario (FastAPI)
 *  POST /api/ventas             → registro de venta con Idempotency-Key (FastAPI)
 *  GET  /api/ventas[/{id}]      → estado y listado de ventas (FastAPI)
 */

const TIEMPO_LIMITE_MS = 20000;

/** Error con el código HTTP real (o null si no hubo respuesta). */
export class ErrorApi extends Error {
  constructor(mensaje, { codigoHttp = null, tipo = 'http', detalle = null } = {}) {
    super(mensaje);
    this.name = 'ErrorApi';
    this.codigoHttp = codigoHttp;
    this.tipo = tipo;       // 'http' | 'red' | 'tiempo' | 'formato'
    this.detalle = detalle; // texto devuelto por el servicio
  }

  /** Sin confirmación del servidor: la operación pudo o no haberse ejecutado. */
  get incierto() {
    return this.tipo === 'red' || this.tipo === 'tiempo' || [502, 503, 504].includes(this.codigoHttp);
  }
}

export function normalizarToken(entrada = '') {
  // Se aceptan tanto JWT sin prefijo como textos que empiezan con Bearer.
  return String(entrada).trim().replace(/^Bearer\s+/i, '').trim();
}

/** Idempotency-Key: 8–100 caracteres [A-Za-z0-9_-]. Funciona también fuera de HTTPS. */
export function generarClaveIdempotencia() {
  if (globalThis.crypto?.randomUUID) return `venta-${globalThis.crypto.randomUUID()}`;
  const bytes = new Uint8Array(16);
  globalThis.crypto.getRandomValues(bytes);
  return `venta-${Array.from(bytes, (b) => b.toString(16).padStart(2, '0')).join('')}`;
}

function textoDetalle(datos) {
  if (!datos || typeof datos !== 'object') return null;
  return datos.detail || datos.mensaje || datos.title || datos.error || null;
}

function mensajePorCodigo(codigo, detalle) {
  const extra = detalle ? ` ${detalle}` : '';
  const mensajes = {
    400: `HTTP 400: solicitud no válida.${extra}`,
    401: 'HTTP 401: JWT ausente, inválido o vencido. Inicie sesión nuevamente.',
    403: 'HTTP 403: su usuario no tiene permiso para esta operación.',
    404: `HTTP 404: recurso no encontrado.${extra}`,
    409: `HTTP 409: conflicto.${extra}`,
    422: `HTTP 422: datos inválidos.${extra}`,
    502: 'HTTP 502: no se pudo establecer conexión con el servicio.',
    503: `HTTP 503: el servicio no está disponible.${extra}`,
    504: 'HTTP 504: el servicio no respondió a tiempo.',
  };
  return mensajes[codigo] || `HTTP ${codigo}: no se completó la operación.${extra}`;
}

/** Ejecuta una petición y devuelve { codigoHttp, datos, ubicacion } o lanza ErrorApi. */
export async function solicitar(ruta, { metodo = 'GET', token, cuerpo, encabezados = {} } = {}, ejecutarFetch = fetch) {
  const headers = new Headers({ Accept: 'application/json', ...encabezados });
  const jwt = normalizarToken(token || '');
  // Si no hay JWT, el backend es quien determina si responde HTTP 401.
  if (jwt) headers.set('Authorization', `Bearer ${jwt}`);

  const opciones = { method: metodo, headers, cache: 'no-store' };
  if (cuerpo !== undefined) {
    if (cuerpo instanceof URLSearchParams) {
      headers.set('Content-Type', 'application/x-www-form-urlencoded');
      opciones.body = cuerpo.toString();
    } else {
      headers.set('Content-Type', 'application/json');
      opciones.body = JSON.stringify(cuerpo);
    }
  }

  const abortador = typeof AbortController === 'function' ? new AbortController() : null;
  if (abortador) opciones.signal = abortador.signal;
  const temporizador = abortador ? setTimeout(() => abortador.abort(), TIEMPO_LIMITE_MS) : null;

  let respuesta;
  try {
    respuesta = await ejecutarFetch(ruta, opciones);
  } catch (error) {
    if (error?.name === 'AbortError') {
      throw new ErrorApi('No hubo respuesta a tiempo. El resultado es incierto.', { tipo: 'tiempo' });
    }
    throw new ErrorApi('No se pudo conectar con el servicio. Compruebe la red, NGINX y el servidor de la PWA.', { tipo: 'red' });
  } finally {
    if (temporizador) clearTimeout(temporizador);
  }

  const texto = await respuesta.text();
  let datos = null;
  if (texto) {
    try { datos = JSON.parse(texto); } catch { datos = null; }
  }

  if (!respuesta.ok) {
    const detalle = textoDetalle(datos);
    throw new ErrorApi(mensajePorCodigo(respuesta.status, detalle), { codigoHttp: respuesta.status, detalle });
  }
  if (texto && datos === null) {
    throw new ErrorApi('La API devolvió una respuesta que no es JSON válido.', { codigoHttp: respuesta.status, tipo: 'formato' });
  }
  return { codigoHttp: respuesta.status, datos, ubicacion: respuesta.headers.get('location') };
}

// ------------------------------------------------------------------ Autenticación

export async function iniciarSesion(usuario, clave, ejecutarFetch = fetch) {
  try {
    const { codigoHttp, datos } = await solicitar('/oauth/token', {
      metodo: 'POST',
      cuerpo: new URLSearchParams({ username: usuario, password: clave }),
    }, ejecutarFetch);
    if (!datos?.access_token) {
      throw new ErrorApi('La respuesta del login no contiene un access_token.', { codigoHttp, tipo: 'formato' });
    }
    return { codigoHttp, token: datos.access_token, expiraEn: Number(datos.expires_in) || 0 };
  } catch (error) {
    if (error.codigoHttp === 401) {
      throw new ErrorApi('HTTP 401: usuario o contraseña incorrectos.', { codigoHttp: 401, detalle: error.detalle });
    }
    throw error;
  }
}

export async function obtenerIdentidad(token, ejecutarFetch = fetch) {
  const { datos } = await solicitar('/api/identidad', { token }, ejecutarFetch);
  const rol = (datos?.roles?.[0] || '').replace(/^ROLE_/, '');
  return { usuario: datos?.usuario || '', rol };
}

// ------------------------------------------------------------------ Consultas

export async function listarVehiculos(token, ejecutarFetch = fetch, { estado } = {}) {
  const ruta = estado ? `/api/vehiculos?estado=${encodeURIComponent(estado)}` : '/api/vehiculos';
  const { codigoHttp, datos } = await solicitar(ruta, { token }, ejecutarFetch);
  if (!Array.isArray(datos)) {
    throw new ErrorApi('La API debe devolver un arreglo JSON de vehículos.', { codigoHttp, tipo: 'formato' });
  }
  return { codigoHttp, vehiculos: datos };
}

export async function listarCompradores(token, ejecutarFetch = fetch) {
  const { codigoHttp, datos } = await solicitar('/api/compradores', { token }, ejecutarFetch);
  if (!Array.isArray(datos)) {
    throw new ErrorApi('La API debe devolver un arreglo JSON de compradores.', { codigoHttp, tipo: 'formato' });
  }
  return { codigoHttp, compradores: datos };
}

export async function listarVentas(token, { limit = 10, offset = 0 } = {}, ejecutarFetch = fetch) {
  const { codigoHttp, datos } = await solicitar(`/api/ventas?limit=${limit}&offset=${offset}`, { token }, ejecutarFetch);
  return { codigoHttp, ventas: datos?.items || [], total: datos?.total || 0 };
}

export async function consultarVenta(token, id, ejecutarFetch = fetch) {
  const { codigoHttp, datos } = await solicitar(`/api/ventas/${encodeURIComponent(id)}`, { token }, ejecutarFetch);
  return { codigoHttp, venta: datos };
}

// ------------------------------------------------------------------ Venta

/**
 * POST /api/ventas. La misma clave con el mismo cuerpo no crea otra venta
 * (FastAPI devuelve la existente con 200); con otro cuerpo responde 409.
 */
export async function registrarVenta(token, { vehiculoId, compradorId }, claveIdempotencia, ejecutarFetch = fetch) {
  const { codigoHttp, datos, ubicacion } = await solicitar('/api/ventas', {
    metodo: 'POST',
    token,
    cuerpo: { vehiculoId: Number(vehiculoId), compradorId: Number(compradorId) },
    encabezados: { 'Idempotency-Key': claveIdempotencia },
  }, ejecutarFetch);
  return { codigoHttp, venta: datos, ubicacion };
}
