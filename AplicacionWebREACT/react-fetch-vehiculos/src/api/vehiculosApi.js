import { ErrorApi, solicitar } from './http.js';

/**
 * Cliente de la API de vehículos (Java Spring Boot vía NGINX).
 * Solo consultas GET: este cliente no registra ni modifica datos.
 */

/** GET /api/vehiculos con filtros opcionales ?marca= y ?estado= (aplicados por la API). */
export async function listarVehiculos(token, { marca = '', estado = '' } = {}) {
  const parametros = new URLSearchParams();
  if (marca.trim()) parametros.set('marca', marca.trim());
  if (estado) parametros.set('estado', estado);
  const consulta = parametros.toString();

  const { codigoHttp, datos } = await solicitar(`/api/vehiculos${consulta ? `?${consulta}` : ''}`, { token });
  // Contrato: GET /api/vehiculos devuelve un arreglo JSON.
  if (!Array.isArray(datos)) {
    throw new ErrorApi('Se esperaba una lista JSON de vehículos (un arreglo).', { codigoHttp, tipo: 'formato' });
  }
  return { codigoHttp, vehiculos: datos };
}

/** GET /api/vehiculos/{id}: detalle actualizado de un vehículo. */
export async function obtenerVehiculo(token, id) {
  const { codigoHttp, datos } = await solicitar(`/api/vehiculos/${encodeURIComponent(id)}`, { token });
  return { codigoHttp, vehiculo: datos };
}
