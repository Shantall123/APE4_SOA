import { solicitar } from './http.js';

/**
 * Cliente de consulta de ventas (FastAPI vía NGINX).
 * React NO registra ventas: aquí no existe POST /api/ventas (además, el proxy
 * de Vite rechaza cualquier método distinto de GET hacia /api).
 */

/** GET /api/ventas?limit=&offset=&estado= → { items, total, limit, offset } */
export async function listarVentas(token, { limit = 10, offset = 0, estado = '' } = {}) {
  const parametros = new URLSearchParams({ limit: String(limit), offset: String(offset) });
  if (estado) parametros.set('estado', estado);
  const { codigoHttp, datos } = await solicitar(`/api/ventas?${parametros}`, { token });
  return { codigoHttp, ventas: datos?.items ?? [], total: datos?.total ?? 0 };
}

/** GET /api/ventas/resumen: indicadores calculados en el servidor con datos reales. */
export async function resumenVentas(token) {
  const { codigoHttp, datos } = await solicitar('/api/ventas/resumen', { token });
  return { codigoHttp, resumen: datos };
}

/** GET /api/ventas/{id}: estado actual de una venta. */
export async function consultarVenta(token, id) {
  const { codigoHttp, datos } = await solicitar(`/api/ventas/${encodeURIComponent(id)}`, { token });
  return { codigoHttp, venta: datos };
}
