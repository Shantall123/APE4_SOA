import { useCallback, useEffect, useState } from 'react';
import { listarVehiculos, obtenerVehiculo } from '../api/vehiculosApi.js';
import { mostrarPrecio, texto, claseEstado } from '../utils/formato.js';
import Estado from './Estado.jsx';

const ESTADOS = ['', 'DISPONIBLE', 'RESERVADO', 'VENDIDO'];

/**
 * Catálogo: GET /api/vehiculos con filtro por marca y estado (lo aplica la API),
 * indicadores calculados con los datos recibidos y detalle con GET /api/vehiculos/{id}.
 */
export default function Vehiculos({ token, alVencerSesion }) {
  const [marca, setMarca] = useState('');
  const [estadoFiltro, setEstadoFiltro] = useState('');
  const [filtrosAplicados, setFiltrosAplicados] = useState({ marca: '', estado: '' });
  const [vehiculos, setVehiculos] = useState([]);
  // El estado inicial ya representa la primera carga en curso.
  const [cargando, setCargando] = useState(true);
  const [estado, setEstado] = useState({ mensaje: 'Consultando la API de vehículos…', tipo: 'info' });
  const [detalle, setDetalle] = useState(null);
  const [estadoDetalle, setEstadoDetalle] = useState(null);

  /** Aplica a la interfaz el resultado (o el error) de una consulta ya terminada. */
  const aplicar = useCallback(({ filtros, r, error }) => {
    setCargando(false);
    if (error) {
      if (error.codigoHttp === 401) { alVencerSesion(); return; }
      setEstado({ mensaje: error.message, tipo: 'error', codigoHttp: error.codigoHttp });
      return;
    }
    setVehiculos(r.vehiculos);
    setFiltrosAplicados(filtros);
    setEstado({
      mensaje: `Consulta correcta: ${r.vehiculos.length} vehículo(s)${filtros.marca ? ` de la marca "${filtros.marca}"` : ''}.`,
      tipo: 'ok', codigoHttp: r.codigoHttp,
    });
  }, [alVencerSesion]);

  /** GET /api/vehiculos; nunca rechaza: devuelve el resultado o el error. */
  const pedir = useCallback((filtros) =>
    listarVehiculos(token, filtros).then((r) => ({ filtros, r }), (error) => ({ filtros, error })), [token]);

  const consultar = (filtros) => pedir(filtros).then(aplicar);

  // Primera carga del catálogo completo (se descarta si el componente se desmonta).
  useEffect(() => {
    let vigente = true;
    pedir({ marca: '', estado: '' }).then((res) => { if (vigente) aplicar(res); });
    return () => { vigente = false; };
  }, [pedir, aplicar]);

  /** Prepara la interfaz para una nueva consulta (desde un evento del usuario). */
  function nuevaConsulta(filtros) {
    setCargando(true);
    setDetalle(null);
    setEstadoDetalle(null);
    setEstado({ mensaje: 'Consultando la API de vehículos…', tipo: 'info' });
    setVehiculos([]); // evita confundir una consulta fallida con datos anteriores
    consultar(filtros);
  }

  function buscar(evento) {
    evento.preventDefault();
    nuevaConsulta({ marca, estado: estadoFiltro });
  }

  function limpiar() {
    setMarca('');
    setEstadoFiltro('');
    nuevaConsulta({ marca: '', estado: '' });
  }

  async function verDetalle(id) {
    setEstadoDetalle({ mensaje: `Consultando GET /api/vehiculos/${id}…`, tipo: 'info' });
    try {
      const r = await obtenerVehiculo(token, id);
      setDetalle(r.vehiculo);
      setEstadoDetalle({ mensaje: `Detalle del vehículo ${id} obtenido de la API.`, tipo: 'ok', codigoHttp: r.codigoHttp });
    } catch (error) {
      if (error.codigoHttp === 401) { alVencerSesion(); return; }
      setDetalle(null);
      setEstadoDetalle({ mensaje: error.message, tipo: 'error', codigoHttp: error.codigoHttp });
    }
  }

  const contar = (e) => vehiculos.filter((v) => v.estado === e).length;

  return (
    <>
      <section className="panel" aria-labelledby="titulo-catalogo">
        <div className="titulo-listado">
          <div>
            <h2 id="titulo-catalogo">Catálogo de vehículos</h2>
            <p className="ayuda">
              Datos reales de <code>GET /api/vehiculos</code>. El filtro por marca y estado lo aplica la API.
            </p>
          </div>
          <span className="contador">{vehiculos.length} resultado(s)</span>
        </div>

        <div className="indicadores" aria-label="Resumen del catálogo">
          <div className="indicador"><span>Total</span><strong>{vehiculos.length}</strong></div>
          <div className="indicador ok"><span>Disponibles</span><strong>{contar('DISPONIBLE')}</strong></div>
          <div className="indicador aviso"><span>Reservados</span><strong>{contar('RESERVADO')}</strong></div>
          <div className="indicador mal"><span>Vendidos</span><strong>{contar('VENDIDO')}</strong></div>
        </div>

        <form className="filtros" onSubmit={buscar}>
          <div>
            <label htmlFor="filtroMarca">Marca</label>
            <input id="filtroMarca" type="search" placeholder="Ejemplo: Toyota" value={marca}
              onChange={(e) => setMarca(e.target.value)} disabled={cargando} />
          </div>
          <div>
            <label htmlFor="filtroEstado">Estado</label>
            <select id="filtroEstado" value={estadoFiltro} onChange={(e) => setEstadoFiltro(e.target.value)} disabled={cargando}>
              {ESTADOS.map((e) => <option key={e} value={e}>{e || 'Todos'}</option>)}
            </select>
          </div>
          <div className="acciones">
            <button type="submit" disabled={cargando}>{cargando ? 'Consultando…' : 'Buscar'}</button>
            <button type="button" className="secundario" onClick={limpiar} disabled={cargando}>Ver todos</button>
          </div>
        </form>

        <Estado {...estado} />

        <div className="tabla-scroll">
          <table>
            <thead>
              <tr>
                <th scope="col">ID</th><th scope="col">Marca</th><th scope="col">Modelo</th>
                <th scope="col">Placa</th><th scope="col">Año</th><th scope="col">Precio</th><th scope="col">Estado</th>
              </tr>
            </thead>
            <tbody>
              {vehiculos.map((v) => (
                <tr key={v.idVehiculo} className={detalle?.idVehiculo === v.idVehiculo ? 'fila-activa' : undefined}>
                  <td>
                    <button className="enlace-id" type="button" onClick={() => verDetalle(v.idVehiculo)}
                      aria-label={`Ver detalle del vehículo ${v.idVehiculo}`}>
                      {v.idVehiculo}
                    </button>
                  </td>
                  <td>{texto(v.marca)}</td>
                  <td>{texto(v.modelo)}</td>
                  <td>{texto(v.placa)}</td>
                  <td>{texto(v.anio)}</td>
                  <td className="numero">{mostrarPrecio(v.precio)}</td>
                  <td><span className={`insignia ${claseEstado(v.estado)}`}>{texto(v.estado)}</span></td>
                </tr>
              ))}
              {vehiculos.length === 0 && (
                <tr>
                  <td className="sin-datos" colSpan="7">
                    {cargando
                      ? 'Cargando vehículos…'
                      : estado.tipo === 'error'
                        ? 'No hay datos para mostrar debido al error anterior.'
                        : filtrosAplicados.marca || filtrosAplicados.estado
                          ? 'No hay vehículos que coincidan con los filtros.'
                          : 'No hay vehículos registrados.'}
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </section>

      <section className="panel" aria-labelledby="titulo-detalle">
        <h2 id="titulo-detalle">Detalle del vehículo</h2>
        {estadoDetalle && <Estado {...estadoDetalle} />}
        {detalle ? (
          <dl className="detalle">
            <div><dt>ID</dt><dd>{texto(detalle.idVehiculo)}</dd></div>
            <div><dt>Marca</dt><dd>{texto(detalle.marca)}</dd></div>
            <div><dt>Modelo</dt><dd>{texto(detalle.modelo)}</dd></div>
            <div><dt>Placa</dt><dd>{texto(detalle.placa)}</dd></div>
            <div><dt>Chasis</dt><dd>{texto(detalle.chasis)}</dd></div>
            <div><dt>Año</dt><dd>{texto(detalle.anio)}</dd></div>
            <div><dt>Color</dt><dd>{texto(detalle.color)}</dd></div>
            <div><dt>Precio</dt><dd>{mostrarPrecio(detalle.precio)}</dd></div>
            <div><dt>Estado</dt><dd><span className={`insignia ${claseEstado(detalle.estado)}`}>{texto(detalle.estado)}</span></dd></div>
          </dl>
        ) : (
          !estadoDetalle && <p className="ayuda">Seleccione el ID de una fila para consultar su detalle.</p>
        )}
      </section>
    </>
  );
}
