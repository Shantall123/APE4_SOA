import { useCallback, useEffect, useRef, useState } from 'react';
import { listarVehiculos, obtenerVehiculo } from '../api/vehiculosApi.js';
import { mostrarPrecio, texto, claseEstado } from '../utils/formato.js';
import Estado from './Estado.jsx';
import Paginacion from './Paginacion.jsx';

const ESTADOS = ['', 'DISPONIBLE', 'RESERVADO', 'VENDIDO'];
const POR_PAGINA = 10;
const ESPERA_ESCRITURA_MS = 350; // evita una petición por cada tecla

/**
 * Catálogo: GET /api/vehiculos con búsqueda dinámica por marca y estado
 * (filtros aplicados por la API), paginación, indicadores y detalle con
 * GET /api/vehiculos/{id} al pulsar cualquier parte de la fila.
 */
export default function Vehiculos({ token, alVencerSesion }) {
  const [marca, setMarca] = useState('');
  const [estadoFiltro, setEstadoFiltro] = useState('');
  const [recarga, setRecarga] = useState(0);
  const [filtrosAplicados, setFiltrosAplicados] = useState({ marca: '', estado: '' });
  const [vehiculos, setVehiculos] = useState([]);
  const [pagina, setPagina] = useState(1);
  const [cargando, setCargando] = useState(true);
  const [estado, setEstado] = useState({ mensaje: 'Consultando la API de vehículos…', tipo: 'info' });
  const [detalle, setDetalle] = useState(null);
  const [estadoDetalle, setEstadoDetalle] = useState(null);
  const refDetalle = useRef(null);

  /** Aplica a la interfaz el resultado (o el error) de una consulta ya terminada. */
  const aplicar = useCallback(({ filtros, r, error }) => {
    setCargando(false);
    setPagina(1);
    if (error) {
      if (error.codigoHttp === 401) { alVencerSesion(); return; }
      setVehiculos([]); // nunca se muestran datos anteriores como si fueran actuales
      setEstado({ mensaje: error.message, tipo: 'error', codigoHttp: error.codigoHttp });
      return;
    }
    setVehiculos(r.vehiculos);
    setFiltrosAplicados(filtros);
    const partes = [filtros.marca && `marca "${filtros.marca}"`, filtros.estado && `estado ${filtros.estado}`].filter(Boolean);
    setEstado({
      mensaje: `Consulta correcta: ${r.vehiculos.length} vehículo(s)${partes.length ? ` con ${partes.join(' y ')}` : ''}.`,
      tipo: 'ok', codigoHttp: r.codigoHttp,
    });
  }, [alVencerSesion]);

  /** GET /api/vehiculos; nunca rechaza: devuelve el resultado o el error. */
  const pedir = useCallback((filtros) =>
    listarVehiculos(token, filtros).then((r) => ({ filtros, r }), (error) => ({ filtros, error })), [token]);

  /**
   * Búsqueda dinámica: se consulta al escribir o al cambiar el estado.
   * Se espera a que el usuario deje de escribir y se descartan las respuestas
   * de búsquedas anteriores (evita resultados desordenados).
   */
  useEffect(() => {
    let vigente = true;
    const filtros = { marca: marca.trim(), estado: estadoFiltro };
    const temporizador = setTimeout(() => {
      setCargando(true);
      setEstado({ mensaje: 'Consultando la API de vehículos…', tipo: 'info' });
      pedir(filtros).then((res) => { if (vigente) aplicar(res); });
    }, filtros.marca ? ESPERA_ESCRITURA_MS : 0);
    return () => { vigente = false; clearTimeout(temporizador); };
  }, [marca, estadoFiltro, recarga, pedir, aplicar]);

  function limpiar() {
    setMarca('');
    setEstadoFiltro('');
  }

  async function verDetalle(id) {
    setEstadoDetalle({ mensaje: `Consultando GET /api/vehiculos/${id}…`, tipo: 'info' });
    refDetalle.current?.scrollIntoView({ behavior: 'smooth', block: 'start' });
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

  /** Enter o espacio sobre una fila enfocada también abre el detalle (teclado). */
  function teclaFila(evento, id) {
    if (evento.key === 'Enter' || evento.key === ' ') {
      evento.preventDefault();
      verDetalle(id);
    }
  }

  const contar = (e) => vehiculos.filter((v) => v.estado === e).length;
  const paginas = Math.max(1, Math.ceil(vehiculos.length / POR_PAGINA));
  const visibles = vehiculos.slice((pagina - 1) * POR_PAGINA, pagina * POR_PAGINA);

  return (
    <>
      <section className="panel" aria-labelledby="titulo-catalogo">
        <div className="titulo-listado">
          <div>
            <h2 id="titulo-catalogo">Catálogo de vehículos</h2>
            <p className="ayuda">
              Datos reales de <code>GET /api/vehiculos</code>. La búsqueda se actualiza mientras escribe;
              el filtro por marca y estado lo aplica la API.
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

        <div className="filtros">
          <div>
            <label htmlFor="filtroMarca">Marca</label>
            <input id="filtroMarca" type="search" placeholder="Escriba una marca, p. ej. Toyota" value={marca}
              autoComplete="off" onChange={(e) => setMarca(e.target.value)} />
          </div>
          <div>
            <label htmlFor="filtroEstado">Estado</label>
            <select id="filtroEstado" value={estadoFiltro} onChange={(e) => setEstadoFiltro(e.target.value)}>
              {ESTADOS.map((e) => <option key={e} value={e}>{e || 'Todos'}</option>)}
            </select>
          </div>
          <div className="acciones">
            <button type="button" className="secundario" onClick={limpiar}
              disabled={!marca && !estadoFiltro}>Limpiar filtros</button>
            <button type="button" className="secundario" onClick={() => setRecarga((n) => n + 1)}
              disabled={cargando}>↻ Actualizar</button>
          </div>
        </div>

        <Estado {...estado} />

        <div className="tabla-scroll">
          <table className={cargando ? 'actualizando' : undefined}>
            <thead>
              <tr>
                <th scope="col">ID</th><th scope="col">Marca</th><th scope="col">Modelo</th>
                <th scope="col">Placa</th><th scope="col">Año</th><th scope="col">Precio</th><th scope="col">Estado</th>
              </tr>
            </thead>
            <tbody>
              {visibles.map((v) => (
                <tr key={v.idVehiculo} tabIndex={0}
                  className={`fila-clic${detalle?.idVehiculo === v.idVehiculo ? ' fila-activa' : ''}`}
                  onClick={() => verDetalle(v.idVehiculo)} onKeyDown={(e) => teclaFila(e, v.idVehiculo)}
                  aria-label={`Ver detalle del vehículo ${v.idVehiculo}: ${v.marca} ${v.modelo}`}>
                  <td><strong>{v.idVehiculo}</strong></td>
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

        {vehiculos.length > 0 && (
          <Paginacion pagina={pagina} paginas={paginas} total={vehiculos.length}
            porPagina={POR_PAGINA} alCambiar={setPagina} />
        )}
      </section>

      <section className="panel" aria-labelledby="titulo-detalle" ref={refDetalle}>
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
          !estadoDetalle && <p className="ayuda">Pulse cualquier fila del catálogo para consultar el detalle del vehículo.</p>
        )}
      </section>
    </>
  );
}
