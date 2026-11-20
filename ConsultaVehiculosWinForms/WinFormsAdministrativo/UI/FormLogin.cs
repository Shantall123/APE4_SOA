using WinFormsAdministrativo.Services;

namespace WinFormsAdministrativo.UI;

/// <summary>Inicio de sesión: obtiene el JWT con POST /oauth/token a través de NGINX.</summary>
internal sealed class FormLogin : Form
{
    private readonly ApiClient _api;
    private readonly TextBox _txtUsuario = new() { Dock = DockStyle.Fill, MaxLength = 50 };
    private readonly TextBox _txtClave = new() { Dock = DockStyle.Fill, UseSystemPasswordChar = true, MaxLength = 100 };
    private readonly CheckBox _chkMostrar = new() { Text = "Mostrar contraseña", AutoSize = true };
    private readonly Button _btnIngresar = Estilo.Boton("Ingresar", principal: true);
    private readonly Button _btnSalir = Estilo.Boton("Salir");
    private readonly Label _lblMensaje = new()
    {
        AutoSize = false, Dock = DockStyle.Fill, Height = 44, ForeColor = Estilo.Error,
        Font = Estilo.Normal, TextAlign = ContentAlignment.MiddleLeft
    };
    private readonly ProgressBar _progreso = new()
    {
        Style = ProgressBarStyle.Marquee, Dock = DockStyle.Fill, Height = 6, Visible = false,
        MarqueeAnimationSpeed = 25
    };

    public FormLogin(ApiClient api, string? mensajeInicial = null)
    {
        _api = api;

        Text = "Iniciar sesión – Administración de vehículos";
        // Ventana redimensionable; el contenido se mantiene centrado.
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = true;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleDimensions = new SizeF(96F, 96F);   // medidas a 100 %, escaladas por Windows
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Estilo.Fondo;
        Font = new Font("Segoe UI", 11f);
        ClientSize = new Size(620, 600);
        MinimumSize = new Size(520, 560);

        // Cabecera
        var cabecera = new Panel { Dock = DockStyle.Top, Height = 120, BackColor = Estilo.Primario };
        cabecera.Controls.Add(new Label
        {
            Text = "Administración de vehículos\nVehículos · Compradores · Ventas",
            Font = new Font("Segoe UI Semibold", 14f), ForeColor = Color.White, Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter
        });

        // Formulario: columna central de ancho máximo, centrada al redimensionar.
        var tabla = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 3, Padding = new Padding(24, 28, 24, 10),
            BackColor = Estilo.Panel
        };
        tabla.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        tabla.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 440));
        tabla.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        var etiquetaUsuario = Estilo.Etiqueta("Usuario", negrita: true);
        var etiquetaClave = Estilo.Etiqueta("Contraseña", negrita: true);
        etiquetaUsuario.Font = etiquetaClave.Font = new Font("Segoe UI Semibold", 11f);
        _txtUsuario.Font = _txtClave.Font = new Font("Segoe UI", 12f);
        _txtUsuario.Margin = _txtClave.Margin = new Padding(0, 2, 0, 10);
        _chkMostrar.Font = Estilo.Normal;
        _lblMensaje.Height = 52;

        var botones = Estilo.Fila();
        botones.FlowDirection = FlowDirection.RightToLeft;
        botones.Dock = DockStyle.Fill;
        _btnIngresar.Padding = _btnSalir.Padding = new Padding(22, 7, 22, 7);
        botones.Controls.Add(_btnIngresar);
        botones.Controls.Add(_btnSalir);

        int fila = 0;
        foreach (var c in new Control[] { etiquetaUsuario, _txtUsuario, etiquetaClave, _txtClave, _chkMostrar, _progreso, _lblMensaje, botones })
        {
            tabla.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tabla.Controls.Add(c, 1, fila++);
        }
        tabla.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // espacio libre al final

        var pie = new Label
        {
            Dock = DockStyle.Bottom, Height = 28, TextAlign = ContentAlignment.MiddleCenter,
            Font = Estilo.Pequena, ForeColor = Estilo.TextoSuave,
            Text = $"Servidor: {_api.BaseUrl}  (NGINX)"
        };

        Controls.Add(tabla);
        Controls.Add(pie);
        Controls.Add(cabecera);

        AcceptButton = _btnIngresar;
        CancelButton = _btnSalir;

        _chkMostrar.CheckedChanged += (_, _) => _txtClave.UseSystemPasswordChar = !_chkMostrar.Checked;
        _btnSalir.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        _btnIngresar.Click += async (_, _) => await IngresarAsync();

        // Usuario: letras, números, punto, guion y guion bajo (sin espacios).
        // La contraseña no se restringe: puede contener cualquier carácter.
        Restricciones.Filtrar(_txtUsuario, Restricciones.CaracterUsuario);

        if (mensajeInicial is not null)
        {
            _lblMensaje.ForeColor = Estilo.Aviso;
            _lblMensaje.Text = mensajeInicial;
        }
        Shown += (_, _) => _txtUsuario.Focus();
    }

    private async Task IngresarAsync()
    {
        string usuario = _txtUsuario.Text.Trim();
        string clave = _txtClave.Text;

        if (usuario.Length == 0 || clave.Length == 0)
        {
            _lblMensaje.ForeColor = Estilo.Error;
            _lblMensaje.Text = "Ingrese usuario y contraseña.";
            (usuario.Length == 0 ? _txtUsuario : _txtClave).Focus();
            return;
        }

        Bloquear(true);
        _lblMensaje.ForeColor = Estilo.TextoSuave;
        _lblMensaje.Text = "Validando credenciales...";

        var resultado = await _api.IniciarSesionAsync(usuario, clave);

        Bloquear(false);
        if (resultado.Exito)
        {
            DialogResult = DialogResult.OK;
            Close();
            return;
        }

        _lblMensaje.ForeColor = Estilo.Error;
        _lblMensaje.Text = resultado.Mensaje;
        _txtClave.SelectAll();
        _txtClave.Focus();
    }

    private void Bloquear(bool ocupado)
    {
        _progreso.Visible = ocupado;
        _btnIngresar.Enabled = !ocupado;
        _txtUsuario.Enabled = !ocupado;
        _txtClave.Enabled = !ocupado;
        UseWaitCursor = ocupado;
    }
}
