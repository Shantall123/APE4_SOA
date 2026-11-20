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

    private readonly Label _lblSubtitulo = new()
    {
        Font = Estilo.Pequena,
        ForeColor = Color.FromArgb(200, 215, 230),
        AutoSize = true,
        Location = new Point(20, 38)
    };
    private readonly Label _lblSinModulos = new()
    {
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleCenter,
        ForeColor = Estilo.TextoSuave,
        Font = Estilo.Subtitulo,
        Visible = false
    };

    // Solo contiene los módulos que el rol actual puede usar.
    private readonly List<ModuloBase> _modulos = new();
    private string? _rolConstruido;
    private bool _construyendo;
    private bool _reautenticando;
    private int _operaciones;

    /// <summary>true si el usuario cerró sesión (Program vuelve a mostrar el login).</summary>
    public bool CerroSesion { get; private set; }

    public FormAdministracion(ApiClient api)
    {
        _api = api;

        Text = "Administración de vehículos y compradores";
        StartPosition = FormStartPosition.CenterScreen;
        // Las medidas del código están pensadas a 96 DPI (100 %); Windows las
        // escala automáticamente en pantallas con 125 %, 150 % o 200 %.
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Estilo.Fondo;
        Font = Estilo.Normal;
        MinimumSize = new Size(1100, 680);
        Size = new Size(1280, 760);

        // ---- Cabecera
        // Cabecera en dos columnas: título y subtítulo apilados a la izquierda,
        // usuario y botón a la derecha. Todo con tamaño automático (sin posiciones fijas).
        var cabecera = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Estilo.Primario,
            Padding = new Padding(18, 8, 14, 8)
        };
        cabecera.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        cabecera.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var titulos = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(0),
            BackColor = Color.Transparent
        };
        var titulo = new Label
        {
            Text = "Panel administrativo",
            Font = Estilo.Titulo,
            ForeColor = Color.White,
            AutoSize = true,
            Margin = new Padding(0)
        };
        _lblSubtitulo.Location = Point.Empty;
        _lblSubtitulo.Margin = new Padding(2, 0, 0, 0);
        titulos.Controls.Add(titulo);
        titulos.Controls.Add(_lblSubtitulo);

        var derecha = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(0),
            BackColor = Color.Transparent
        };
        _lblUsuario.Anchor = AnchorStyles.Left;      // centrado vertical respecto al botón
        _lblUsuario.Margin = new Padding(0, 0, 12, 0);
        _btnSalir.Margin = new Padding(0);
        derecha.Controls.Add(_lblUsuario);
        derecha.Controls.Add(_btnSalir);

        cabecera.Controls.Add(titulos, 0, 0);
        cabecera.Controls.Add(derecha, 1, 0);

        // ---- Pestañas: se crean según el rol (ver ConstruirModulos)
        _pestanas.SelectedIndexChanged += async (_, _) => await CargarModuloActualAsync();

        var contenedor = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10, 10, 10, 6) };
        contenedor.Controls.Add(_pestanas);
        contenedor.Controls.Add(_lblSinModulos);

        // ---- Barra de estado
        var barra = new StatusStrip { SizingGrip = false, BackColor = Color.White, Font = Estilo.Normal };
        _lblServidor.Text = $"NGINX: {_api.BaseUrl}";
        barra.Items.AddRange(new ToolStripItem[] { _lblEstado, _progreso, _lblHttp, _lblServidor });

        Controls.Add(contenedor);
        Controls.Add(barra);
        Controls.Add(cabecera);

        ConstruirModulos();
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
        Shown += async (_, _) => await CargarModuloActualAsync();
    }

    /// <summary>
    /// Crea únicamente las pestañas que el rol puede usar:
    /// ADMIN y OPERADOR → Vehículos, Compradores y Ventas;
    /// CONSULTA → solo Vehículos, sin el formulario de registro.
    /// </summary>
    private void ConstruirModulos()
    {
        var permisos = Permisos.Para(_api.Usuario?.Rol);
        _rolConstruido = _api.Usuario?.Rol;
        _construyendo = true;

        // Liberar los módulos de una sesión anterior (p. ej. tras volver a iniciar sesión con otro rol).
        var anteriores = _pestanas.TabPages.Cast<TabPage>().ToList();
        _pestanas.TabPages.Clear();
        anteriores.ForEach(p => p.Dispose());
        _modulos.Clear();

        if (permisos.VerVehiculos)
            AgregarModulo("🚗  Vehículos", new VehiculosPanel(_api, this, permisos.RegistrarVehiculos));
        if (permisos.GestionarCompradores)
            AgregarModulo("👤  Compradores", new CompradoresPanel(_api, this));
        if (permisos.ConsultarVentas)
            AgregarModulo("📄  Ventas (consulta)", new VentasPanel(_api, this));

        bool hayModulos = _modulos.Count > 0;
        _pestanas.Visible = hayModulos;
        _lblSinModulos.Visible = !hayModulos;
        _lblSinModulos.Text = $"El rol «{_api.Usuario?.Rol}» no tiene módulos habilitados en esta aplicación.";
        _lblSubtitulo.Text = permisos.Descripcion;
        _construyendo = false;
    }

    private void AgregarModulo(string titulo, ModuloBase modulo)
    {
        var pagina = new TabPage(titulo) { BackColor = Estilo.Fondo, Padding = new Padding(4) };
        pagina.Controls.Add(modulo);
        if (IsHandleCreated)
        {
            // Módulos creados después de mostrar la ventana (p. ej. al volver a iniciar
            // sesión con otro rol): se les aplica la misma escala DPI que al resto.
            float f = DeviceDpi / 96f;
            modulo.Scale(new SizeF(f, f));
        }
        _pestanas.TabPages.Add(pagina);
        _modulos.Add(modulo);
    }

    private async Task CargarModuloActualAsync()
    {
        if (_construyendo) return;
        int i = _pestanas.SelectedIndex;
        if (i >= 0 && i < _modulos.Count) await _modulos[i].CargarSiHaceFaltaAsync();
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
                if (_api.Usuario?.Rol != _rolConstruido)
                {
                    // Se ingresó con otro rol: rehacer las pestañas según sus permisos.
                    ConstruirModulos();
                    await CargarModuloActualAsync();
                }
                else if (_pestanas.SelectedIndex >= 0 && _pestanas.SelectedIndex < _modulos.Count)
                {
                    await _modulos[_pestanas.SelectedIndex].RecargarAsync();
                }
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
