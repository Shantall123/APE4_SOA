/**
 * Utilidad común de Fetch: agrega el JWT, aplica un tiempo límite e
 * interpreta el código HTTP. No contiene código visual ni reglas de negocio.
 * Las rutas son relativas: el proxy de Vite las reenvía a NGINX.
 */

const TIEMPO_LIMITE_MS = 15000;

/** Error con el código HTTP real (null si no hubo respuesta). */
export class ErrorApi extends Error {
  constructor(mensaje, { codigoHttp = null, tipo = 'http', detalle = null } = {}) {
    super(mensaje);
    this.name = 'ErrorApi';
    this.codigoHttp = codigoHttp;
    this.tipo = tipo; // 'http' | 'red' | 'tiempo' | 'formato'
    this.detalle = detalle;
  }
}

function detalleDe(datos) {
  if (!datos || typeof datos !== 'object') return null;
  return datos.detail || datos.mensaje || datos.title || null;
}

function mensajePorCodigo(codigo, detalle) {
  const extra = detalle ? ` ${detalle}` : '';
  const mensajes = {
    400: `HTTP 400: solicitud no válida.${extra}`,
    401: 'HTTP 401: falta el JWT o el token ya no es válido. Inicie sesión nuevamente.',
    403: 'HTTP 403: su usuario no tiene permiso para esta consulta.',
    404: `HTTP 404: el recurso solicitado no existe.${extra}`,
    422: `HTTP 422: parámetros inválidos.${extra}`,
    500: `HTTP 500: error interno del servicio.${extra}`,
    502: `HTTP 502: el servidor intermedio no pudo contactar al servicio.${extra}`,
    503: `HTTP 503: el servicio no está disponible.${extra}`,
    504: 'HTTP 504: el servicio no respondió a tiempo.',
  };
  return mensajes[codigo] ?? `HTTP ${codigo}: la consulta no se completó.${extra}`;
}

/**
 * Ejecuta una petición y devuelve { codigoHttp, datos } o lanza ErrorApi.
 * fetch NO lanza excepciones por 4xx/5xx: se revisa respuesta.ok manualmente.
 */
export async function solicitar(ruta, { token, metodo = 'GET', cuerpo } = {}) {
  const headers = new Headers({ Accept: 'application/json' });
  if (token) headers.set('Authorization', `Bearer ${token}`);

  const opciones = { method: metodo, headers, cache: 'no-store' };
  if (cuerpo instanceof URLSearchParams) {
    headers.set('Content-Type', 'application/x-www-form-urlencoded');
    opciones.body = cuerpo.toString();
  }

  const abortador = new AbortController();
  opciones.signal = abortador.signal;
  const temporizador = setTimeout(() => abortador.abort(), TIEMPO_LIMITE_MS);

  let respuesta;
  try {
    respuesta = await fetch(ruta, opciones);
  } catch (error) {
    if (error?.name === 'AbortError') {
      throw new ErrorApi('No hubo respuesta en 15 segundos. Verifique NGINX y los servicios.', { tipo: 'tiempo' });
    }
    throw new ErrorApi('No se pudo conectar. Verifique el servidor de Vite, NGINX y la red.', { tipo: 'red' });
  } finally {
    clearTimeout(temporizador);
  }

  const texto = await respuesta.text();
  let datos = null;
  if (texto) {
    try { datos = JSON.parse(texto); } catch { datos = null; }
  }

  if (!respuesta.ok) {
    const detalle = detalleDe(datos);
    throw new ErrorApi(mensajePorCodigo(respuesta.status, detalle), { codigoHttp: respuesta.status, detalle });
  }
  if (texto && datos === null) {
    throw new ErrorApi('La API respondió, pero el JSON no pudo interpretarse.', {
      codigoHttp: respuesta.status, tipo: 'formato',
    });
  }
  return { codigoHttp: respuesta.status, datos };
}
