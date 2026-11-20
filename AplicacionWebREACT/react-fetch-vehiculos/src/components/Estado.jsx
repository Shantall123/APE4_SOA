/**
 * Barra de estado de una consulta: mensaje, tipo (info, ok, error) y código HTTP real.
 * role="status" permite que los lectores de pantalla anuncien el cambio.
 */
export default function Estado({ mensaje, tipo = 'info', codigoHttp = null }) {
  return (
    <div className={`estado estado-${tipo}`} role="status" aria-live="polite">
      <span>{mensaje}</span>
      {codigoHttp !== null && <strong>HTTP {codigoHttp}</strong>}
    </div>
  );
}
