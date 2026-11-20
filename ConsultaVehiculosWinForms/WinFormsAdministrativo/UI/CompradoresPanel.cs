using WinFormsAdministrativo.Models;
using WinFormsAdministrativo.Services;

namespace WinFormsAdministrativo.UI;

/// <summary>Pestaña Compradores: POST /api/compradores y GET /api/compradores (FastAPI vía NGINX).</summary>
internal sealed partial class CompradoresPanel : ModuloBase
{
    private readonly TextBox _txtDocumento = Estilo.Texto(13);
    private readonly TextBox _txtNombre = Estilo.Texto(120);
    private readonly TextBox _txtCorreo = Estilo.Texto(200);
    private readonly Button _btnGuardar = Estilo.Boton("Guardar comprador", principal: true);
    private readonly Button _btnLimpiar = Estilo.Boton("Limpiar");

    private readonly TextBox _txtBuscar = new() { Width = 220, PlaceholderText = "Nombre o documento" };
    private readonly Button _btnBuscar = Estilo.Boton("Buscar", principal: true);
    private readonly Button _btnTodos = Estilo.Boton("Ver todos");
    private readonly DataGridView _grilla = Estilo.Grilla();
    private readonly Label _lblVacio = new();
    private readonly Label _lblTotal = Estilo.Ayuda("");

    public CompradoresPanel(ApiClient api, IContextoUi ui) : base(api, ui)
    {
        // Caracteres permitidos al escribir o pegar
        Restricciones.Filtrar(_txtDocumento, Restricciones.EsDigito);
        Restricciones.Filtrar(_txtNombre, Restricciones.CaracterNombre);
        Restricciones.Filtrar(_txtCorreo, Restricciones.CaracterCorreo);
        Restricciones.Filtrar(_txtBuscar, Restricciones.CaracterBusqueda);
        _txtBuscar.MaxLength = 100;

        var form = Formulario();
        Campo(form, "Documento (cédula / RUC) *", _txtDocumento, "Máximo 13 caracteres (cédula de 10 o RUC de 13). No se repite para su usuario.");
        Campo(form, "Nombre completo *", _txtNombre);
        Campo(form, "Correo electrónico *", _txtCorreo, "Se usa para notificar las ventas.");
        var acciones = Estilo.Fila();
        acciones.Controls.Add(_btnGuardar);
        acciones.Controls.Add(_btnLimpiar);
        form.Controls.Add(acciones);
        form.Controls.Add(Estilo.Ayuda("* Campos obligatorios."));

        var tarjetaRegistro = Estilo.Tarjeta("Registrar comprador");
        tarjetaRegistro.Controls.Add(form);

        var filtros = Estilo.Fila();
        filtros.Controls.Add(Estilo.Etiqueta("Buscar:"));
        filtros.Controls.Add(_txtBuscar);
        filtros.Controls.Add(_btnBuscar);
        filtros.Controls.Add(_btnTodos);

        Estilo.Columna(_grilla, nameof(Comprador.Id), "ID", 40);
        Estilo.Columna(_grilla, nameof(Comprador.Nombre), "Nombre", 160);
        Estilo.Columna(_grilla, nameof(Comprador.Correo), "Correo", 180);

        var tarjetaLista = Estilo.Tarjeta("Compradores registrados");
        tarjetaLista.Controls.Add(ContenedorLista(filtros, _grilla, _lblVacio, _lblTotal));

        Controls.Add(DosColumnas(tarjetaRegistro, tarjetaLista));

        _btnGuardar.Click += async (_, _) => await GuardarAsync();
        _btnLimpiar.Click += (_, _) => LimpiarFormulario();
        _btnBuscar.Click += async (_, _) => await RecargarAsync();
        _btnTodos.Click += async (_, _) => { _txtBuscar.Clear(); await RecargarAsync(); };
        _txtBuscar.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await RecargarAsync(); }
        };
    }

    public override async Task RecargarAsync() => await CargarListaAsync(null);

    private async Task CargarListaAsync(int? seleccionarId)
    {
        Ui.Ocupado(true, "Consultando compradores...");
        _btnBuscar.Enabled = false;
        var r = await Api.ListarCompradoresAsync(_txtBuscar.Text);
        _btnBuscar.Enabled = true;
        Ui.Ocupado(false);

        if (!r.Exito)
        {
            _grilla.DataSource = null;
            _lblTotal.Text = "";
            MostrarVacio(_lblVacio, _grilla, true, "No se pudo obtener el listado.\n" + r.Mensaje);
            Ui.MostrarError(r, "consultar compradores");
            return;
        }

        var lista = r.Datos!;
        _grilla.DataSource = lista;
        _lblTotal.Text = $"{lista.Count} comprador(es) registrados por el usuario {Api.Usuario?.Usuario}";
        MostrarVacio(_lblVacio, _grilla, lista.Count == 0,
            string.IsNullOrWhiteSpace(_txtBuscar.Text)
                ? "Aún no hay compradores registrados por su usuario."
                : "Ningún comprador coincide con la búsqueda.");
        Ui.Mostrar($"Compradores consultados: {lista.Count} (GET /api/compradores).", TipoMensaje.Exito, r.CodigoHttp);

        if (seleccionarId is null) return;
        foreach (DataGridViewRow fila in _grilla.Rows)
        {
            if (fila.DataBoundItem is Comprador c && c.Id == seleccionarId)
            {
                fila.Selected = true;
                _grilla.FirstDisplayedScrollingRowIndex = fila.Index;
                break;
            }
        }
    }

    private async Task GuardarAsync()
    {
        string documento = _txtDocumento.Text.Trim();
        string nombre = Restricciones.Normalizar(_txtNombre.Text);
        string correo = _txtCorreo.Text.Trim().ToLowerInvariant();

        if (!ValidarFormulario(
                (_txtDocumento, documento.Length == 0, "Documento: es obligatorio."),
                (_txtDocumento, !Restricciones.Documento().IsMatch(documento),
                    $"Documento: solo números, entre 10 y 13 dígitos (tiene {documento.Length})."),
                (_txtNombre, nombre.Length == 0, "Nombre: es obligatorio."),
                (_txtNombre, nombre.Length < 3, "Nombre: debe tener al menos 3 letras."),
                (_txtNombre, !Restricciones.Nombre().IsMatch(nombre),
                    "Nombre: solo letras (con tildes) y espacios; no puede empezar ni terminar en apóstrofo o guion."),
                (_txtCorreo, correo.Length == 0, "Correo: es obligatorio."),
                (_txtCorreo, !Restricciones.Correo().IsMatch(correo),
                    "Correo: ingrese un correo válido, por ejemplo nombre@dominio.com.")))
        {
            return;
        }

        var nuevo = new CompradorNuevo
        {
            Documento = documento,
            Nombre = nombre,
            Correo = correo
        };

        _btnGuardar.Enabled = false;
        Ui.Ocupado(true, "Registrando comprador...");
        var r = await Api.CrearCompradorAsync(nuevo);
        Ui.Ocupado(false);
        _btnGuardar.Enabled = true;

        if (!r.Exito)
        {
            Ui.MostrarError(r, "registrar el comprador", dialogo: true);
            return;
        }

        var c = r.Datos!;
        Ui.Mostrar($"Comprador #{c.Id} {c.Nombre} registrado.", TipoMensaje.Exito, r.CodigoHttp);
        MessageBox.Show(this,
            $"Comprador registrado correctamente (HTTP {r.CodigoHttp}).\n\nID: {c.Id}\nNombre: {c.Nombre}\nCorreo: {c.Correo}",
            "Registro exitoso", MessageBoxButtons.OK, MessageBoxIcon.Information);

        LimpiarFormulario();
        _txtBuscar.Clear();
        await CargarListaAsync(c.Id);
    }

    private void LimpiarFormulario()
    {
        _txtDocumento.Clear();
        _txtNombre.Clear();
        _txtCorreo.Clear();
        Errores.Clear();
        _txtDocumento.Focus();
    }
}
