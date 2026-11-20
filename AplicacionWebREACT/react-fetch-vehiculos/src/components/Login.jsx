import { useState } from 'react';
import { iniciarSesion, obtenerIdentidad } from '../api/authApi.js';
import Estado from './Estado.jsx';

/**
 * Inicio de sesión: POST /oauth/token (vía proxy de Vite → NGINX) y luego
 * GET /api/identidad para conocer el rol. El JWT no se pega manualmente.
 */
export default function Login({ alIngresar, mensajeInicial }) {
  const [usuario, setUsuario] = useState('');
  const [clave, setClave] = useState('');
  const [mostrarClave, setMostrarClave] = useState(false);
  const [cargando, setCargando] = useState(false);
  const [estado, setEstado] = useState(mensajeInicial ?? { mensaje: 'Ingrese sus credenciales.', tipo: 'info' });

  async function ingresar(evento) {
    evento.preventDefault();
    if (!usuario.trim() || !clave) {
      setEstado({ mensaje: 'Ingrese usuario y contraseña.', tipo: 'error' });
      return;
    }
    setCargando(true);
    setEstado({ mensaje: 'Validando credenciales…', tipo: 'info' });
    try {
      const login = await iniciarSesion(usuario.trim(), clave);
      const identidad = await obtenerIdentidad(login.token);
      setClave('');
      alIngresar({ token: login.token, expiraEn: login.expiraEn, ...identidad });
    } catch (error) {
      setEstado({ mensaje: error.message, tipo: 'error', codigoHttp: error.codigoHttp ?? null });
    } finally {
      setCargando(false);
    }
  }

  return (
    <section className="panel login" aria-labelledby="titulo-login">
      <h2 id="titulo-login">Iniciar sesión</h2>
      <p className="ayuda">
        El token JWT se obtiene con <code>POST /oauth/token</code> a través de NGINX. Las credenciales del
        cliente OAuth las agrega el proxy de Vite; el navegador no las conoce.
      </p>
      <form onSubmit={ingresar} noValidate>
        <label htmlFor="usuario">Usuario</label>
        <input id="usuario" autoComplete="username" value={usuario}
          onChange={(e) => setUsuario(e.target.value)} disabled={cargando} />
        <label htmlFor="clave">Contraseña</label>
        <div className="linea">
          <input id="clave" type={mostrarClave ? 'text' : 'password'} autoComplete="current-password"
            value={clave} onChange={(e) => setClave(e.target.value)} disabled={cargando} />
          <button type="button" className="secundario" onClick={() => setMostrarClave((v) => !v)}
            aria-label={mostrarClave ? 'Ocultar contraseña' : 'Mostrar contraseña'}>
            {mostrarClave ? 'Ocultar' : 'Mostrar'}
          </button>
        </div>
        <div className="acciones">
          <button type="submit" disabled={cargando}>{cargando ? 'Ingresando…' : 'Ingresar'}</button>
        </div>
      </form>
      <Estado {...estado} />
      <p className="nota">El JWT se conserva solo durante la sesión del navegador (sessionStorage).</p>
    </section>
  );
}
