// Ecuador usa dólares estadounidenses. Solo da formato; no modifica los datos de la API.
const dinero = new Intl.NumberFormat('es-EC', { style: 'currency', currency: 'USD' });

export function mostrarPrecio(valor) {
  if (valor === null || valor === undefined || valor === '') return '—';
  const numero = Number(valor);
  return Number.isFinite(numero) ? dinero.format(numero) : String(valor);
}

export function mostrarFecha(utc) {
  if (!utc) return '—';
  const fecha = new Date(utc);
  return Number.isNaN(fecha.getTime()) ? String(utc) : fecha.toLocaleString('es-EC');
}

export const texto = (v) => (v === null || v === undefined || v === '' ? '—' : String(v));

/** Clase de color para los distintivos de estado. */
export function claseEstado(estado) {
  const e = String(estado ?? '').toUpperCase();
  if (['DISPONIBLE', 'COMPLETADA', 'ENVIADA'].includes(e)) return 'ok';
  if (['VENDIDO', 'FALLIDA'].includes(e)) return 'mal';
  if (['RESERVADO', 'PENDIENTE', 'EN_REVISION'].includes(e)) return 'aviso';
  return '';
}
