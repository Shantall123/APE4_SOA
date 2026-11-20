using WinFormsAdministrativo.Models;
using WinFormsAdministrativo.Services;

namespace WinFormsAdministrativo.UI;

/// <summary>
/// Pestaña Ventas: SOLO CONSULTA. GET /api/ventas (paginado) y GET /api/ventas/resumen.
/// Las ventas se registran exclusivamente desde la PWA móvil; aquí no existe
/// ningún botón ni método para crearlas.
/// </summary>
internal sealed class VentasPanel : ModuloBase
{
    private readonly ComboBox _cboEstado = Estilo.Lista("Todas", "PENDIENTE", "EN_REVISION", "COMPLETADA", "FALLIDA");
    private readonly Button _btnConsultar = Estilo.Boton("Consultar", principal: true);
    // Paginación en el servidor: la página y el tamaño se envían como offset y limit.
    private readonly Paginador _paginador = new("ventas", 10, 20, 50);
    private readonly DataGridView _grilla = Estilo.Grilla();
    private readonly Label _lblVacio = new();

    private readonly Label _kCompletadas = new();
    private readonly Label _kIngresos = new();
    private readonly Label _kPendientes = new();
    private readonly Label _kRevision = new();
    private readonly Label _kFallidas = new();
    private readonly FlowLayoutPanel _indicadores = Estilo.Fila();

    public VentasPanel(ApiClient api, IContextoUi ui) : base(api, ui)
    {
        _cboEstado.Anchor = AnchorStyles.Left;

        var aviso = new Label
        {
            Dock = DockStyle.Top, AutoSize = true, TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Color.FromArgb(232, 240, 248), ForeColor = Estilo.Primario,
            Font = Estilo.Normal, Padding = new Padding(10, 8, 10, 8),
            Text = "ℹ  Módulo de consulta. Las ventas se registran únicamente desde la aplicación móvil (PWA)."
        };
        _indicadores.Padding = new Padding(0, 4, 0, 10);

        _indicadores.Controls.Add(Indicador("Completadas", _kCompletadas, Estilo.Exito));
        _indicadores.Controls.Add(Indicador("Ingresos (USD)", _kIngresos, Estilo.Primario));
        _indicadores.Controls.Add(Indicador("Pendientes", _kPendientes, Estilo.TextoSuave));
        _indicadores.Controls.Add(Indicador("En revisión", _kRevision, Estilo.Aviso));
        _indicadores.Controls.Add(Indicador("Fallidas", _kFallidas, Estilo.Error));

        var filtros = Estilo.Fila();
        filtros.Controls.Add(Estilo.Etiqueta("Estado:"));
        filtros.Controls.Add(_cboEstado);
        filtros.Controls.Add(_btnConsultar);


        Estilo.Columna(_grilla, nameof(Venta.Id), "ID", 40);
        Estilo.Columna(_grilla, nameof(Venta.FechaLocal), "Fecha", 105, "dd/MM/yyyy HH:mm");
        Estilo.Columna(_grilla, nameof(Venta.VehiculoId), "Vehículo", 60);
        Estilo.Columna(_grilla, nameof(Venta.CompradorId), "Comprador", 70);
        Estilo.Columna(_grilla, nameof(Venta.Precio), "Precio", 85, "N2", derecha: true);
        Estilo.Columna(_grilla, nameof(Venta.Moneda), "Moneda", 55);
        Estilo.Columna(_grilla, nameof(Venta.Estado), "Estado", 90);
        Estilo.Columna(_grilla, nameof(Venta.Notificacion), "Notificación", 85);
        Estilo.Columna(_grilla, nameof(Venta.OperacionId), "Operación", 210);
        _grilla.CellFormatting += ColorearEstado;

        var tarjeta = Estilo.Tarjeta("Ventas registradas");
        // Arriba los indicadores, debajo los filtros y luego la grilla.
        tarjeta.Controls.Add(ContenedorLista(new Control[] { _indicadores, filtros }, _grilla, _lblVacio, _paginador));

        Controls.Add(tarjeta);
        Controls.Add(aviso);

        _btnConsultar.Click += async (_, _) => { _paginador.Reiniciar(); await RecargarAsync(); };
        _cboEstado.SelectionChangeCommitted += async (_, _) => { _paginador.Reiniciar(); await RecargarAsync(); };
        _paginador.Cambio += async (_, _) => await RecargarAsync();
    }

    public override async Task RecargarAsync()
    {
        string? estado = _cboEstado.SelectedIndex == 0 ? null : _cboEstado.Text;
        Ui.Ocupado(true, "Consultando ventas...");
        _btnConsultar.Enabled = false;

        var r = await Api.ListarVentasAsync(_paginador.Tamano, _paginador.Offset, estado);
        var resumen = await Api.ResumenVentasAsync();

        _btnConsultar.Enabled = true;
        Ui.Ocupado(false);

        MostrarResumen(resumen);

        if (!r.Exito)
        {
            _grilla.DataSource = null;
            _paginador.Actualizar(0);
            MostrarVacio(_lblVacio, _grilla, true, "No se pudo obtener el listado.\n" + r.Mensaje);
            Ui.MostrarError(r, "consultar ventas");
            return;
        }

        var pagina = r.Datos!;
        _grilla.DataSource = pagina.Items;
        _paginador.Actualizar(pagina.Total);
        MostrarVacio(_lblVacio, _grilla, pagina.Items.Count == 0,
            estado is null ? "No hay ventas registradas para su usuario." : $"No hay ventas en estado {estado}.");
        Ui.Mostrar($"Ventas consultadas: {pagina.Items.Count} de {pagina.Total} (GET /api/ventas).",
            TipoMensaje.Exito, r.CodigoHttp);
    }

    private void MostrarResumen(ApiResultado<ResumenVentas> r)
    {
        _indicadores.Visible = r.Exito;
        if (!r.Exito) return; // p. ej. 403 para el rol CONSULTA: se oculta sin interrumpir
        var d = r.Datos!;
        _kCompletadas.Text = d.Completadas.ToString();
        _kIngresos.Text = d.Ingresos.ToString("N2");
        _kPendientes.Text = d.Pendientes.ToString();
        _kRevision.Text = d.EnRevision.ToString();
        _kFallidas.Text = d.Fallidas.ToString();
    }

    /// <summary>
    /// Tarjeta de indicador: título pequeño y valor grande. Su tamaño se calcula
    /// según el texto (AutoSize), así no se recorta con distintas escalas de pantalla.
    /// </summary>
    private static Control Indicador(string titulo, Label valor, Color color)
    {
        var tarjeta = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.White,
            Margin = new Padding(0, 0, 12, 6),
            Padding = new Padding(16, 8, 18, 10),   // 16 a la izquierda deja sitio a la franja de color
            MinimumSize = new Size(165, 0)
        };
        tarjeta.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tarjeta.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var lblTitulo = new Label
        {
            Text = titulo,
            AutoSize = true,
            Font = Estilo.Pequena,
            ForeColor = Estilo.TextoSuave,
            Margin = new Padding(0)
        };
        valor.Text = "—";
        valor.AutoSize = true;
        valor.Font = new Font("Segoe UI Semibold", 16f);
        valor.ForeColor = color;
        valor.Margin = new Padding(0, 2, 0, 0);

        tarjeta.Controls.Add(lblTitulo, 0, 0);
        tarjeta.Controls.Add(valor, 0, 1);

        // Borde gris y franja de color a la izquierda.
        tarjeta.Paint += (_, e) =>
        {
            using var franja = new SolidBrush(color);
            e.Graphics.FillRectangle(franja, 0, 0, 5, tarjeta.Height);
            using var borde = new Pen(Estilo.Borde);
            e.Graphics.DrawRectangle(borde, 0, 0, tarjeta.Width - 1, tarjeta.Height - 1);
        };
        tarjeta.Resize += (_, _) => tarjeta.Invalidate();
        return tarjeta;
    }

    private void ColorearEstado(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (_grilla.Columns[e.ColumnIndex].DataPropertyName != nameof(Venta.Estado) || e.Value is not string estado) return;
        e.CellStyle!.ForeColor = estado switch
        {
            "COMPLETADA" => Estilo.Exito,
            "FALLIDA" => Estilo.Error,
            "EN_REVISION" => Estilo.Aviso,
            _ => Estilo.TextoSuave
        };
        e.CellStyle.Font = Estilo.Negrita;
    }
}
