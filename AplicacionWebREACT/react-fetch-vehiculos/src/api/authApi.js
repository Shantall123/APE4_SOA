import { ErrorApi, solicitar } from './http.js';

/**
 * Inicio de sesión con POST /oauth/token. El navegador envía solo usuario y
 * contraseña; el proxy de Vite agrega las credenciales del cliente OAuth.
 */
export async function iniciarSesion(usuario, clave) {
  try {
    const { codigoHttp, datos } = await solicitar('/oauth/token', {
      metodo: 'POST',
      cuerpo: new URLSearchParams({ grant_type: 'password', username: usuario, password: clave }),
    });
    if (!datos?.access_token) {
      throw new ErrorApi('La respuesta del login no contiene un access_token.', { codigoHttp, tipo: 'formato' });
    }
    return { codigoHttp, token: datos.access_token, expiraEn: Number(datos.expires_in) || 0 };
  } catch (error) {
    if (error.codigoHttp === 401) {
      throw new ErrorApi('HTTP 401: usuario o contraseña incorrectos.', { codigoHttp: 401 });
    }
    throw error;
  }
}

/** GET /api/identidad: usuario y rol del token (CONSULTA, OPERADOR o ADMIN). */
export async function obtenerIdentidad(token) {
  const { datos } = await solicitar('/api/identidad', { token });
  return {
    usuario: datos?.usuario ?? '',
    rol: (datos?.roles?.[0] ?? '').replace(/^ROLE_/, ''),
  };
}

// ------------------------------------------------------------------ Sesión
// El JWT se conserva solo durante la sesión del navegador (sessionStorage):
// se borra al cerrar la pestaña, al cerrar sesión, al vencer o ante un 401.
const CLAVE = 'react-consulta-sesion';

export function guardarSesion({ token, usuario, rol, expiraEn }) {
  const expira = Date.now() + Math.max(0, expiraEn - 30) * 1000;
  sessionStorage.setItem(CLAVE, JSON.stringify({ token, usuario, rol, expira }));
}

export function leerSesion() {
  try {
    const s = JSON.parse(sessionStorage.getItem(CLAVE) ?? 'null');
    if (s?.token && Date.now() < s.expira) return s;
  } catch { /* sesión corrupta */ }
  sessionStorage.removeItem(CLAVE);
  return null;
}

export function borrarSesion() {
  sessionStorage.removeItem(CLAVE);
}
