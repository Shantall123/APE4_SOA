using WinFormsAdministrativo.Services;

namespace WinFormsAdministrativo.UI;

/// <summary>
/// Ventana principal: cabecera con el usuario, pestañas por módulo y barra de
/// estado con el último código HTTP. Implementa el manejo común de mensajes.
/// </summary>
internal sealed class FormAdministracion : Form, IContextoUi
{
    private readonly ApiClient _api;
    private readonly TabControl _pestanas = new() { Dock = DockStyle.Fill, Padding = new Point(18, 6) };
    private readonly Label _lblUsuario = new()
    {
        AutoSize = true,
        ForeColor = Color.White,
        Font = Estilo.Normal,
        Margin = new Padding(0, 12, 12, 0)
    };
    private readonly Button _btnSalir = Estilo.Boton("Cerrar sesión");

    private readonly ToolStripStatusLabel _lblEstado = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly ToolStripStatusLabel _lblHttp = new() { BorderSides = ToolStripStatusLabelBorderSides.Left, Padding = new Padding(8, 0, 8, 0) };
    private readonly ToolStripProgressBar _progreso = new() { Style = ProgressBarStyle.Marquee, Visible = false, MarqueeAnimationSpeed = 25 };
    private readonly ToolStripStatusLabel _lblServidor = new() { BorderSides = ToolStripStatusLabelBorderSides.Left, ForeColor = Estilo.TextoSuave };

    private readonly ModuloBase[] _modulos;
    private bool _reautenticando;
    private int _operaciones;

    /// <summary>true si el usuario cerró sesión (Program vuelve a mostrar el login).</summary>
    public bool CerroSesion { get; private set; }

    public FormAdministracion(ApiClient api)
    {
        _api = api;

        Text = "Administración de vehículos y compradores";
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Estilo.Fondo;
        Font = Estilo.Normal;
        MinimumSize = new Size(1100, 680);
        Size = new Size(1280, 760);

        // ---- Cabecera
        var cabecera = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = Estilo.Primario, Padding = new Padding(18, 0, 12, 0) };
        var titulo = new Label
        {
            Text = "Panel administrativo",
            Font = Estilo.Titulo,
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(18, 8)
        };
        var subtitulo = new Label
        {
            Text = "Registro y consulta de vehículos y compradores · consulta de ventas",
            Font = Estilo.Pequena,
            ForeColor = Color.FromArgb(200, 215, 230),
            AutoSize = true,
            Location = new Point(20, 38)
        };
        var derecha = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 8, 0, 0),
            BackColor = Color.Transparent
        };
        _btnSalir.Margin = new Padding(0, 4, 0, 0);
        derecha.Controls.Add(_lblUsuario);
        derecha.Controls.Add(_btnSalir);
        cabecera.Controls.Add(derecha);
        cabecera.Controls.Add(titulo);
        cabecera.Controls.Add(subtitulo);

        // ---- Pestañas
        _modulos = new ModuloBase[]
        {
            new VehiculosPanel(api, this),
            new CompradoresPanel(api, this),
            new VentasPanel(api, this)
        };
        string[] nombres = { "🚗  Vehículos", "👤  Compradores", "📄  Ventas (consulta)" };
        for (int i = 0; i < _modulos.Length; i++)
        {
            var pagina = new TabPage(nombres[i]) { BackColor = Estilo.Fondo, Padding = new Padding(4) };
            pagina.Controls.Add(_modulos[i]);
            _pestanas.TabPages.Add(pagina);
        }
        _pestanas.SelectedIndexChanged += async (_, _) => await _modulos[_pestanas.SelectedIndex].CargarSiHaceFaltaAsync();

        var contenedor = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10, 10, 10, 6) };
        contenedor.Controls.Add(_pestanas);

        // ---- Barra de estado
        var barra = new StatusStrip { SizingGrip = false, BackColor = Color.White, Font = Estilo.Normal };
        _lblServidor.Text = $"NGINX: {_api.BaseUrl}";
        barra.Items.AddRange(new ToolStripItem[] { _lblEstado, _progreso, _lblHttp, _lblServidor });

        Controls.Add(contenedor);
        Controls.Add(barra);
        Controls.Add(cabecera);

        ActualizarUsuario();
        Mostrar("Listo.", TipoMensaje.Info);

        _btnSalir.Click += (_, _) =>
        {
            if (MessageBox.Show(this, "¿Desea cerrar la sesión?", "Cerrar sesión",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            _api.CerrarSesion();
            CerroSesion = true;
            Close();
        };
        Shown += async (_, _) => await _modulos[0].CargarSiHaceFaltaAsync();
    }

    private void ActualizarUsuario() =>
        _lblUsuario.Text = $"👤 {_api.Usuario?.Usuario}   ·   Rol: {_api.Usuario?.Rol}";

    // =================================================================
    // IContextoUi
    // =================================================================

    public void Mostrar(string mensaje, TipoMensaje tipo, int? codigoHttp = null)
    {
        _lblEstado.Text = mensaje;
        _lblEstado.ForeColor = tipo switch
        {
            TipoMensaje.Exito => Estilo.Exito,
            TipoMensaje.Aviso => Estilo.Aviso,
            TipoMensaje.Error => Estilo.Error,
            _ => Color.FromArgb(33, 37, 41)
        };

        if (codigoHttp is int c)
        {
            _lblHttp.Text = $"Último HTTP: {c}";
            _lblHttp.ForeColor = c < 300 ? Estilo.Exito : c < 500 ? Estilo.Aviso : Estilo.Error;
        }
        else if (tipo == TipoMensaje.Error)
        {
            _lblHttp.Text = "Sin respuesta HTTP";
            _lblHttp.ForeColor = Estilo.Error;
        }
    }

    public void Ocupado(bool ocupado, string? mensaje = null)
    {
        _operaciones = Math.Max(0, _operaciones + (ocupado ? 1 : -1));
        bool activo = _operaciones > 0;
        _progreso.Visible = activo;
        UseWaitCursor = activo;
        if (ocupado && mensaje is not null) Mostrar(mensaje, TipoMensaje.Info);
    }

    private void InitializeComponent()
    {

    }

    public void MostrarError<T>(ApiResultado<T> resultado, string operacion, bool dialogo = false)
    {
        Mostrar($"No se pudo {operacion}. {resultado.Mensaje}", TipoMensaje.Error, resultado.CodigoHttp);

        if (resultado.SesionVencida)
        {
            ReautenticarAsync();
            return;
        }

        if (!dialogo) return;

        var (titulo, icono) = resultado.CodigoHttp switch
        {
            403 => ("Permiso denegado", MessageBoxIcon.Warning),
            409 or 422 or 400 => ("Datos no aceptados", MessageBoxIcon.Warning),
            404 => ("Recurso no encontrado", MessageBoxIcon.Warning),
            null => ("Error de conexión", MessageBoxIcon.Error),
            _ => ("Error del servicio", MessageBoxIcon.Error)
        };
        MessageBox.Show(this, $"No se pudo {operacion}.\n\n{resultado.Mensaje}", titulo, MessageBoxButtons.OK, icono);
    }

    /// <summary>Ante un 401 la sesión venció: se solicita iniciar sesión otra vez.</summary>
    private async void ReautenticarAsync()
    {
        if (_reautenticando) return;
        _reautenticando = true;
        try
        {
            _api.CerrarSesion();
            using var login = new FormLogin(_api, "Su sesión venció o el token ya no es válido (HTTP 401). Inicie sesión nuevamente.");
            if (login.ShowDialog(this) == DialogResult.OK)
            {
                ActualizarUsuario();
                Mostrar("Sesión renovada. Repita la operación.", TipoMensaje.Exito);
                await _modulos[_pestanas.SelectedIndex].RecargarAsync();
            }
            else
            {
                CerroSesion = true;
                Close();
            }
        }
        finally
        {
            _reautenticando = false;
        }
    }
}
