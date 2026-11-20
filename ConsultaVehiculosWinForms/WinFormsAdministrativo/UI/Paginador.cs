namespace WinFormsAdministrativo.UI;

/// <summary>
/// Barra de paginación reutilizable: « ‹ Mostrando a–b de N › » y selector de filas por página.
/// No consulta datos: solo mantiene la página actual y avisa con <see cref="Cambio"/>.
/// Sirve tanto para paginar en memoria (vehículos, compradores) como en el
/// servidor con limit/offset (ventas).
/// </summary>
internal sealed class Paginador : FlowLayoutPanel
{
    private readonly Button _btnPrimera = Estilo.Boton("«");
    private readonly Button _btnAnterior = Estilo.Boton("‹ Anterior");
    private readonly Button _btnSiguiente = Estilo.Boton("Siguiente ›");
    private readonly Button _btnUltima = Estilo.Boton("»");
    private readonly Label _lblRango = Estilo.Etiqueta("");
    private readonly ComboBox _cboTamano;
    private readonly string _unidad;

    public int Pagina { get; private set; } = 1;
    public int Total { get; private set; }
    public int Tamano => int.Parse(_cboTamano.Text);
    public int Offset => (Pagina - 1) * Tamano;
    public int TotalPaginas => Math.Max(1, (int)Math.Ceiling(Total / (double)Tamano));

    /// <summary>Se produce al cambiar de página o de tamaño de página.</summary>
    public event EventHandler? Cambio;

    /// <param name="unidad">Texto plural para el resumen, p. ej. "vehículos".</param>
    public Paginador(string unidad, params int[] tamanos)
    {
        _unidad = unidad;
        Dock = DockStyle.Bottom;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        WrapContents = true;
        Padding = new Padding(0, 6, 0, 0);
        Font = Estilo.Normal;

        _cboTamano = Estilo.Lista((tamanos.Length > 0 ? tamanos : new[] { 10, 20, 50 })
            .Select(t => t.ToString()).ToArray());
        _cboTamano.Width = 70;
        _cboTamano.Anchor = AnchorStyles.Left;
        _lblRango.Margin = new Padding(6, 0, 10, 0);

        foreach (var b in new[] { _btnPrimera, _btnUltima }) b.Padding = new Padding(8, 4, 8, 4);

        Controls.Add(_btnPrimera);
        Controls.Add(_btnAnterior);
        Controls.Add(_lblRango);
        Controls.Add(_btnSiguiente);
        Controls.Add(_btnUltima);
        var porPagina = Estilo.Etiqueta("Filas por página:");
        porPagina.Margin = new Padding(18, 0, 6, 0);
        Controls.Add(porPagina);
        Controls.Add(_cboTamano);

        _btnPrimera.Click += (_, _) => IrA(1);
        _btnAnterior.Click += (_, _) => IrA(Pagina - 1);
        _btnSiguiente.Click += (_, _) => IrA(Pagina + 1);
        _btnUltima.Click += (_, _) => IrA(TotalPaginas);
        _cboTamano.SelectionChangeCommitted += (_, _) => { Pagina = 1; Refrescar(); Cambio?.Invoke(this, EventArgs.Empty); };

        Refrescar();
    }

    /// <summary>Registra el total de elementos y ajusta la página si quedó fuera de rango.</summary>
    public void Actualizar(int total)
    {
        Total = Math.Max(0, total);
        if (Pagina > TotalPaginas) Pagina = TotalPaginas;
        Refrescar();
    }

    /// <summary>Vuelve a la primera página sin disparar <see cref="Cambio"/> (p. ej. al aplicar un filtro).</summary>
    public void Reiniciar() => Pagina = 1;

    /// <summary>Posiciona la página que contiene el elemento en <paramref name="indice"/> (base 0).</summary>
    public void MostrarIndice(int indice)
    {
        if (indice < 0) return;
        Pagina = indice / Tamano + 1;
        Refrescar();
    }

    public void IrA(int pagina)
    {
        int destino = Math.Clamp(pagina, 1, TotalPaginas);
        if (destino == Pagina) return;
        Pagina = destino;
        Refrescar();
        Cambio?.Invoke(this, EventArgs.Empty);
    }

    private void Refrescar()
    {
        int desde = Total == 0 ? 0 : Offset + 1;
        int hasta = Math.Min(Offset + Tamano, Total);
        _lblRango.Text = $"Mostrando {desde}–{hasta} de {Total} {_unidad}  ·  Página {Pagina} de {TotalPaginas}";
        _btnPrimera.Enabled = _btnAnterior.Enabled = Pagina > 1;
        _btnSiguiente.Enabled = _btnUltima.Enabled = Pagina < TotalPaginas;
    }
}
