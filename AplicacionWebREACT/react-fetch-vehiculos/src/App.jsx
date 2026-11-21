import { useCallback, useState } from 'react';
import { borrarSesion, guardarSesion, leerSesion } from './api/authApi.js';
import Login from './components/Login.jsx';
import Vehiculos from './components/Vehiculos.jsx';
import Ventas from './components/Ventas.jsx';
import './App.css';

/**
 * Plataforma web de CONSULTA (React + Fetch).
 * - Vehículos: listado, filtro por marca y estado, detalle e indicadores.
 * - Ventas: listado, resumen y detalle (solo OPERADOR y ADMIN, igual que en la PWA).
 * React no registra ventas: no hay formularios de venta ni POST /api/ventas.
 */
const PUEDE_VER_VENTAS = ['OPERADOR', 'ADMIN'];

export default function App() {
  const [sesion, setSesion] = useState(() => leerSesion());
  const [pestana, setPestana] = useState('vehiculos');
  const [aviso, setAviso] = useState(null);

  function alIngresar(datos) {
    guardarSesion(datos);
    setSesion(leerSesion());
    setPestana('vehiculos');
    setAviso(null);
  }

  // Estable con useCallback: los módulos lo usan como dependencia de sus consultas.
  const alVencerSesion = useCallback(() => {
    borrarSesion();
    setSesion(null);
    setAviso({ mensaje: 'Su sesión venció o el token ya no es válido (HTTP 401). Inicie sesión nuevamente.', tipo: 'error', codigoHttp: 401 });
  }, []);

  function cerrarSesion() {
    if (!window.confirm('¿Desea cerrar la sesión?')) return;
    borrarSesion();
    setSesion(null);
    setAviso({ mensaje: 'Sesión cerrada. El token fue eliminado.', tipo: 'info' });
  }

  const verVentas = sesion && PUEDE_VER_VENTAS.includes(sesion.rol);

  return (
    <main className="contenedor">
      <header className="cabecera">
        <div className="marca-producto">PLATAFORMA WEB DE CONSULTA · REACT + FETCH</div>
        <h1>Vehículos y ventas</h1>
        <p>Consulta de datos reales a través de NGINX: API Java de vehículos y FastAPI de ventas.</p>
      </header>

      {!sesion ? (
        <Login alIngresar={alIngresar} mensajeInicial={aviso} />
      ) : (
        <>
          <section className="barra-sesion" aria-label="Sesión">
            <div>
              <span><span className="etiqueta">Usuario:</span> <strong>{sesion.usuario}</strong></span>
              <span><span className="etiqueta">Rol:</span> <span className="rol">{sesion.rol || '—'}</span></span>
            </div>
            <button type="button" className="secundario" onClick={cerrarSesion}>Cerrar sesión</button>
          </section>

          {verVentas ? (
            <nav className="pestanas" aria-label="Módulos">
              <button type="button" className={pestana === 'vehiculos' ? 'activa' : ''}
                aria-pressed={pestana === 'vehiculos'} onClick={() => setPestana('vehiculos')}>Vehículos</button>
              <button type="button" className={pestana === 'ventas' ? 'activa' : ''}
                aria-pressed={pestana === 'ventas'} onClick={() => setPestana('ventas')}>Ventas</button>
            </nav>
          ) : (
            <p className="aviso-rol">
              Su rol ({sesion.rol || 'desconocido'}) es de solo consulta del catálogo de vehículos.
            </p>
          )}

          {pestana === 'ventas' && verVentas
            ? <Ventas token={sesion.token} alVencerSesion={alVencerSesion} />
            : <Vehiculos token={sesion.token} alVencerSesion={alVencerSesion} />}
        </>
      )}
    </main>
  );
}
