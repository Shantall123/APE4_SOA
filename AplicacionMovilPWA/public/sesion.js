/**
 * Sesión de la PWA. El JWT se guarda en sessionStorage: dura mientras la
 * pestaña o la app instalada esté abierta, se borra al cerrar sesión, al
 * vencer o ante un HTTP 401. Nunca se escribe en el código, en localStorage
 * ni en la caché del Service Worker.
 */
const CLAVE_SESION = 'pwa-ventas-sesion';
const CLAVE_PENDIENTE = 'pwa-ventas-pendiente';

function almacen() {
  try { return globalThis.sessionStorage ?? null; } catch { return null; }
}

export function guardarSesion({ token, usuario, rol, expiraEn }) {
  const expira = Date.now() + Math.max(0, (Number(expiraEn) || 0) - 30) * 1000; // margen de 30 s
  almacen()?.setItem(CLAVE_SESION, JSON.stringify({ token, usuario, rol, expira }));
}

/** Devuelve la sesión vigente o null si no existe o ya venció. */
export function obtenerSesion() {
  const crudo = almacen()?.getItem(CLAVE_SESION);
  if (!crudo) return null;
  try {
    const sesion = JSON.parse(crudo);
    if (!sesion.token || Date.now() >= sesion.expira) {
      cerrarSesion();
      return null;
    }
    return sesion;
  } catch {
    cerrarSesion();
    return null;
  }
}

export function cerrarSesion() {
  almacen()?.removeItem(CLAVE_SESION);
  almacen()?.removeItem(CLAVE_PENDIENTE);
}

/**
 * Venta cuyo resultado aún no es definitivo (sin respuesta, 502/503/504).
 * Se conserva la Idempotency-Key para reintentar SIN duplicar.
 */
export function guardarPendiente(pendiente) {
  almacen()?.setItem(CLAVE_PENDIENTE, JSON.stringify(pendiente));
}

export function obtenerPendiente() {
  try { return JSON.parse(almacen()?.getItem(CLAVE_PENDIENTE) || 'null'); } catch { return null; }
}

export function borrarPendiente() {
  almacen()?.removeItem(CLAVE_PENDIENTE);
}
