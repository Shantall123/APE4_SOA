/**
 * Controles de paginación reutilizables: anterior / siguiente y rango mostrado.
 */
export default function Paginacion({ pagina, paginas, total, porPagina, alCambiar, deshabilitado = false }) {
  const desde = total === 0 ? 0 : (pagina - 1) * porPagina + 1;
  const hasta = Math.min(pagina * porPagina, total);

  return (
    <nav className="paginacion" aria-label="Paginación">
      <button type="button" className="secundario" disabled={deshabilitado || pagina <= 1}
        onClick={() => alCambiar(pagina - 1)}>◀ Anterior</button>
      <span>
        Página <strong>{pagina}</strong> de {paginas} · {desde}–{hasta} de {total}
      </span>
      <button type="button" className="secundario" disabled={deshabilitado || pagina >= paginas}
        onClick={() => alCambiar(pagina + 1)}>Siguiente ▶</button>
    </nav>
  );
}
