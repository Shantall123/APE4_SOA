import { useCallback, useEffect, useRef, useState } from 'react';
import { listarVentas, resumenVentas, consultarVenta } from '../api/ventasApi.js';
import { mostrarPrecio, mostrarFecha, texto, claseEstado } from '../utils/formato.js';
import Estado from './Estado.jsx';
import Paginacion from './Paginacion.jsx';

const ESTADOS = ['', 'PENDIENTE', 'EN_REVISION', 'COMPLETADA', 'FALLIDA'];
const POR_PAGINA = 10;

/**
 * Consulta de ventas (solo lectura): GET /api/ventas paginado, GET /api/ventas/resumen
 * para los indicadores y GET /api/ventas/{id} para el detalle.
 * Aquí no existe formulario ni botón para registrar ventas.
 */
export default function Ventas({ token, alVencerSesion }) {
  const [estadoFiltro, setEstadoFiltro] = useState('');
  const [offset, setOffset] = useState(0);
  const [ventas, setVentas] = useState([]);
  const [total, setTotal] = useState(0);
  const [resumen, setResumen] = useState(null);
  // El estado inicial ya representa la primera carga en curso.
  const [cargando, setCargando] = useState(true);
  const [estado, setEstado] = useState({ mensaje: 'Consultando la API de ventas…', tipo: 'info' });
  const [detalle, setDetalle] = useState(null);
  const [estadoDetalle, setEstadoDetalle] = useState(null);
  const refDetalle = useRef(null);

  /** Aplica a la interfaz el resultado (o el error) de una consulta ya terminada. */
  const aplicar = useCallback(({ filtro, lista, indicadores, error }) => {
    setCargando(false);
    if (error) {
      if (error.codigoHttp === 401) { alVencerSesion(); return; }
      setTotal(0);
      setEstado({ mensaje: error.message, tipo: 'error', codigoHttp: error.codigoHttp });
      return;
    }
    setVentas(lista.ventas);
    setTotal(lista.total);
    setResumen(indicadores?.resumen ?? null);
    setEstado({
      mensaje: `Consulta correcta: ${lista.ventas.length} de ${lista.total} venta(s)${filtro ? ` en estado ${filtro}` : ''}.`,
      tipo: 'ok', codigoHttp: lista.codigoHttp,
    });
  }, [alVencerSesion]);

  /** GET /api/ventas + GET /api/ventas/resumen; nunca rechaza: devuelve datos o error. */
  const pedir = useCallback((filtro, desde) => Promise.all([
    listarVentas(token, { limit: POR_PAGINA, offset: desde, estado: filtro }),
    resumenVentas(token).catch(() => null), // los indicadores no bloquean el listado
  ]).then(([lista, indicadores]) => ({ filtro, lista, indicadores }), (error) => ({ filtro, error })), [token]);

  const consultar = (filtro, desde) => pedir(filtro, desde).then(aplicar);

  // Primera carga (sin filtro, primera página); se descarta si el componente se desmonta.
  useEffect(() => {
    let vigente = true;
    pedir('', 0).then((res) => { if (vigente) aplicar(res); });
    return () => { vigente = false; };
  }, [pedir, aplicar]);

  /** Prepara la interfaz y consulta con el filtro y la página indicados. */
  function nuevaConsulta(filtro, desde) {
    setEstadoFiltro(filtro);
    setOffset(desde);
    setCargando(true);
    setDetalle(null);
    setEstadoDetalle(null);
    setEstado({ mensaje: 'Consultando la API de ventas…', tipo: 'info' });
    setVentas([]);
    consultar(filtro, desde);
  }

  async function verDetalle(id) {
    setEstadoDetalle({ mensaje: `Consultando GET /api/ventas/${id}…`, tipo: 'info' });
    refDetalle.current?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    try {
      const r = await consultarVenta(token, id);
      setDetalle(r.venta);
      setEstadoDetalle({ mensaje: `Estado actual de la venta ${id}.`, tipo: 'ok', codigoHttp: r.codigoHttp });
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

  const paginas = Math.max(1, Math.ceil(total / POR_PAGINA));
  const pagina = Math.floor(offset / POR_PAGINA) + 1;

  return (
    <>
      <section className="panel" aria-labelledby="titulo-ventas">
        <div className="titulo-listado">
          <div>
            <h2 id="titulo-ventas">Ventas registradas</h2>
            <p className="ayuda">
              Consulta de <code>GET /api/ventas</code>. Las ventas se registran únicamente desde la PWA móvil.
            </p>
          </div>
          <span className="contador">{total} venta(s)</span>
        </div>

        {resumen && (
          <div className="indicadores" aria-label="Resumen de ventas (GET /api/ventas/resumen)">
            <div className="indicador ok"><span>Completadas</span><strong>{resumen.completadas}</strong></div>
            <div className="indicador"><span>Ingresos</span><strong>{mostrarPrecio(resumen.ingresos)}</strong></div>
            <div className="indicador aviso"><span>Pendientes</span><strong>{resumen.pendientes}</strong></div>
            <div className="indicador aviso"><span>En revisión</span><strong>{resumen.enRevision}</strong></div>
            <div className="indicador mal"><span>Fallidas</span><strong>{resumen.fallidas}</strong></div>
          </div>
        )}

        <div className="filtros">
          <div>
            <label htmlFor="filtroVenta">Estado</label>
            <select id="filtroVenta" value={estadoFiltro}
              onChange={(e) => nuevaConsulta(e.target.value, 0)}>
              {ESTADOS.map((e) => <option key={e} value={e}>{e || 'Todas'}</option>)}
            </select>
          </div>
          <div className="acciones">
            <button type="button" className="secundario" disabled={cargando}
              onClick={() => nuevaConsulta(estadoFiltro, offset)}>
              {cargando ? 'Consultando…' : '↻ Actualizar'}
            </button>
          </div>
        </div>

        <Estado {...estado} />

        <div className="tabla-scroll">
          <table>
            <thead>
              <tr>
                <th scope="col">ID</th><th scope="col">Fecha</th><th scope="col">Vehículo</th>
                <th scope="col">Comprador</th><th scope="col">Precio</th><th scope="col">Estado</th>
                <th scope="col">Notificación</th>
              </tr>
            </thead>
            <tbody>
              {ventas.map((v) => (
                <tr key={v.id} tabIndex={0}
                  className={`fila-clic${detalle?.id === v.id ? ' fila-activa' : ''}`}
                  onClick={() => verDetalle(v.id)} onKeyDown={(e) => teclaFila(e, v.id)}
                  aria-label={`Ver detalle de la venta ${v.id}`}>
                  <td><strong>{v.id}</strong></td>
                  <td>{mostrarFecha(v.fechaUTC)}</td>
                  <td>{texto(v.vehiculoId)}</td>
                  <td>{texto(v.compradorId)}</td>
                  <td className="numero">{mostrarPrecio(v.precioAcordado)}</td>
                  <td><span className={`insignia ${claseEstado(v.estado)}`}>{texto(v.estado)}</span></td>
                  <td>{texto(v.notificacion)}</td>
                </tr>
              ))}
              {ventas.length === 0 && (
                <tr>
                  <td className="sin-datos" colSpan="7">
                    {cargando
                      ? 'Cargando ventas…'
                      : estado.tipo === 'error'
                        ? 'No hay datos para mostrar debido al error anterior.'
                        : estadoFiltro ? `No hay ventas en estado ${estadoFiltro}.` : 'No hay ventas registradas para su usuario.'}
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>

        {total > 0 && (
          <Paginacion pagina={pagina} paginas={paginas} total={total} porPagina={POR_PAGINA}
            deshabilitado={cargando} alCambiar={(p) => nuevaConsulta(estadoFiltro, (p - 1) * POR_PAGINA)} />
        )}
      </section>

      <section className="panel" aria-labelledby="titulo-detalle-venta" ref={refDetalle}>
        <h2 id="titulo-detalle-venta">Detalle de la venta</h2>
        {estadoDetalle && <Estado {...estadoDetalle} />}
        {detalle ? (
          <dl className="detalle">
            <div><dt>ID de venta</dt><dd>{texto(detalle.id)}</dd></div>
            <div><dt>Estado</dt><dd><span className={`insignia ${claseEstado(detalle.estado)}`}>{texto(detalle.estado)}</span></dd></div>
            <div><dt>Vehículo</dt><dd>{texto(detalle.vehiculoId)}</dd></div>
            <div><dt>Comprador</dt><dd>{texto(detalle.compradorId)}</dd></div>
            <div><dt>Precio confirmado</dt><dd>{mostrarPrecio(detalle.precioAcordado)} {detalle.moneda ?? ''}</dd></div>
            <div><dt>Notificación</dt><dd>{texto(detalle.notificacion)}</dd></div>
            <div><dt>Fecha</dt><dd>{mostrarFecha(detalle.fechaUTC)}</dd></div>
            <div className="ancho"><dt>Operación</dt><dd>{texto(detalle.operacionId)}</dd></div>
          </dl>
        ) : (
          !estadoDetalle && <p className="ayuda">Pulse cualquier fila del listado para consultar el estado actual de la venta.</p>
        )}
      </section>
    </>
  );
}
