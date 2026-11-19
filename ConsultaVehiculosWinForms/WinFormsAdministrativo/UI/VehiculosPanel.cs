using WinFormsAdministrativo.Models;
using WinFormsAdministrativo.Services;

namespace WinFormsAdministrativo.UI;

/// <summary>Pestaña Vehículos: POST /api/vehiculos y GET /api/vehiculos (API Java vía NGINX).</summary>
internal sealed class VehiculosPanel : ModuloBase
{
    // Registro
    private readonly TextBox _txtMarca = Estilo.Texto(50);
    private readonly TextBox _txtModelo = Estilo.Texto(50);
    private readonly TextBox _txtPlaca = Estilo.Texto(7, CharacterCasing.Upper);
    private readonly TextBox _txtChasis = Estilo.Texto(17, CharacterCasing.Upper);
    private readonly Label _lblChasis = Estilo.Ayuda("0 / 17 caracteres");
    private readonly NumericUpDown _numAnio = new() { Minimum = 1900, Maximum = DateTime.Now.Year + 1, Value = DateTime.Now.Year };
    private readonly TextBox _txtColor = Estilo.Texto(30);
    private readonly NumericUpDown _numPrecio = new()
    {
        Minimum = 0, Maximum = 9_999_999_999.99m, DecimalPlaces = 2, ThousandsSeparator = true,
        Increment = 500, Value = 0
    };
    private readonly Button _btnGuardar = Estilo.Boton("Guardar vehículo", principal: true);
    private readonly Button _btnLimpiar = Estilo.Boton("Limpiar");

    // Listado
    private readonly TextBox _txtFiltroMarca = new() { Width = 170, PlaceholderText = "Ej.: Toyota" };
    private readonly ComboBox _cboEstado = Estilo.Lista("Todos", "DISPONIBLE", "RESERVADO", "VENDIDO");
    private readonly Button _btnBuscar = Estilo.Boton("Buscar", principal: true);
    private readonly Button _btnLimpiarFiltro = Estilo.Boton("Ver todos");
    private readonly DataGridView _grilla = Estilo.Grilla();
    private readonly Label _lblVacio = new();
    private readonly Label _lblTotal = Estilo.Ayuda("");

    public VehiculosPanel(ApiClient api, IContextoUi ui) : base(api, ui)
    {
        // ---- Tarjeta de registro
        var form = Formulario();
        Campo(form, "Marca *", _txtMarca);
        Campo(form, "Modelo *", _txtModelo);
        Campo(form, "Placa *", _txtPlaca, "Máximo 7 caracteres, sin guion (ej.: PBA1234).");
        Campo(form, "Chasis (VIN) *", _txtChasis);
        form.Controls.Add(_lblChasis);
        Campo(form, "Año *", _numAnio);
        Campo(form, "Color *", _txtColor);
        Campo(form, "Precio (USD) *", _numPrecio, "Mayor que 0, máximo dos decimales.");
        var acciones = Estilo.Fila();
        acciones.Controls.Add(_btnGuardar);
        acciones.Controls.Add(_btnLimpiar);
        form.Controls.Add(acciones);
        form.Controls.Add(Estilo.Ayuda("* Campos obligatorios. El vehículo se crea en estado DISPONIBLE."));

        var tarjetaRegistro = Estilo.Tarjeta("Registrar vehículo");
        tarjetaRegistro.Controls.Add(form);

        // ---- Tarjeta de listado
        var filtros = Estilo.Fila();
        filtros.Controls.Add(Estilo.Etiqueta("Marca:"));
        filtros.Controls.Add(_txtFiltroMarca);
        filtros.Controls.Add(Estilo.Etiqueta("Estado:"));
        filtros.Controls.Add(_cboEstado);
        filtros.Controls.Add(_btnBuscar);
        filtros.Controls.Add(_btnLimpiarFiltro);

        Estilo.Columna(_grilla, nameof(Vehiculo.IdVehiculo), "ID", 45);
        Estilo.Columna(_grilla, nameof(Vehiculo.Marca), "Marca", 100);
        Estilo.Columna(_grilla, nameof(Vehiculo.Modelo), "Modelo", 100);
        Estilo.Columna(_grilla, nameof(Vehiculo.Placa), "Placa", 80);
        Estilo.Columna(_grilla, nameof(Vehiculo.Chasis), "Chasis", 150);
        Estilo.Columna(_grilla, nameof(Vehiculo.Anio), "Año", 55);
        Estilo.Columna(_grilla, nameof(Vehiculo.Color), "Color", 75);
        Estilo.Columna(_grilla, nameof(Vehiculo.Precio), "Precio (USD)", 95, "N2", derecha: true);
        Estilo.Columna(_grilla, nameof(Vehiculo.Estado), "Estado", 90);
        _grilla.CellFormatting += ColorearEstado;

        var tarjetaLista = Estilo.Tarjeta("Vehículos registrados");
        tarjetaLista.Controls.Add(ContenedorLista(filtros, _grilla, _lblVacio, _lblTotal));

        Controls.Add(DosColumnas(tarjetaRegistro, tarjetaLista));

        // ---- Eventos
        _txtChasis.TextChanged += (_, _) =>
        {
            int n = _txtChasis.Text.Trim().Length;
            _lblChasis.Text = $"{n} / 17 caracteres";
            _lblChasis.ForeColor = n == 17 ? Estilo.Exito : Estilo.TextoSuave;
        };
        _btnGuardar.Click += async (_, _) => await GuardarAsync();
        _btnLimpiar.Click += (_, _) => LimpiarFormulario();
        _btnBuscar.Click += async (_, _) => await RecargarAsync();
        _btnLimpiarFiltro.Click += async (_, _) =>
        {
            _txtFiltroMarca.Clear();
            _cboEstado.SelectedIndex = 0;
            await RecargarAsync();
        };
        _txtFiltroMarca.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await RecargarAsync(); }
        };
    }

    public override async Task RecargarAsync() => await CargarListaAsync(seleccionarId: null);

    private async Task CargarListaAsync(int? seleccionarId)
    {
        string? estado = _cboEstado.SelectedIndex == 0 ? null : _cboEstado.Text;
        Ui.Ocupado(true, "Consultando vehículos...");
        _btnBuscar.Enabled = false;

        var r = await Api.ListarVehiculosAsync(_txtFiltroMarca.Text, estado);

        _btnBuscar.Enabled = true;
        Ui.Ocupado(false);

        if (!r.Exito)
        {
            _grilla.DataSource = null;
            _lblTotal.Text = "";
            MostrarVacio(_lblVacio, _grilla, true, "No se pudo obtener el listado.\n" + r.Mensaje);
            Ui.MostrarError(r, "consultar vehículos");
            return;
        }

        var lista = r.Datos!;
        _grilla.DataSource = lista;
        _lblTotal.Text = $"{lista.Count} vehículo(s)";
        MostrarVacio(_lblVacio, _grilla, lista.Count == 0,
            "No hay vehículos que coincidan con los filtros.");
        Ui.Mostrar($"Vehículos consultados: {lista.Count} (GET /api/vehiculos).", TipoMensaje.Exito, r.CodigoHttp);

        if (seleccionarId is not null)
        {
            foreach (DataGridViewRow fila in _grilla.Rows)
            {
                if (fila.DataBoundItem is Vehiculo v && v.IdVehiculo == seleccionarId)
                {
                    fila.Selected = true;
                    _grilla.FirstDisplayedScrollingRowIndex = fila.Index;
                    break;
                }
            }
        }
    }

    private async Task GuardarAsync()
    {
        // Validación de forma (ayuda al usuario); las reglas de negocio las aplica la API.
        int largoChasis = _txtChasis.Text.Trim().Length;
        if (!ValidarFormulario(
                (_txtMarca, string.IsNullOrWhiteSpace(_txtMarca.Text), "Marca: es obligatoria."),
                (_txtModelo, string.IsNullOrWhiteSpace(_txtModelo.Text), "Modelo: es obligatorio."),
                (_txtPlaca, string.IsNullOrWhiteSpace(_txtPlaca.Text), "Placa: es obligatoria."),
                (_txtPlaca, _txtPlaca.Text.Trim().Length > 7, "Placa: máximo 7 caracteres."),
                (_txtChasis, largoChasis != 17, $"Chasis: debe tener exactamente 17 caracteres (tiene {largoChasis})."),
                (_txtColor, string.IsNullOrWhiteSpace(_txtColor.Text), "Color: es obligatorio."),
                (_numPrecio, _numPrecio.Value <= 0, "Precio: debe ser mayor que 0.")))
        {
            return;
        }

        var nuevo = new VehiculoNuevo
        {
            Marca = _txtMarca.Text.Trim(),
            Modelo = _txtModelo.Text.Trim(),
            Placa = _txtPlaca.Text.Trim(),
            Chasis = _txtChasis.Text.Trim(),
            Anio = (int)_numAnio.Value,
            Color = _txtColor.Text.Trim(),
            Precio = _numPrecio.Value
        };

        _btnGuardar.Enabled = false;
        Ui.Ocupado(true, "Registrando vehículo...");
        var r = await Api.CrearVehiculoAsync(nuevo);
        Ui.Ocupado(false);
        _btnGuardar.Enabled = true;

        if (!r.Exito)
        {
            Ui.MostrarError(r, "registrar el vehículo", dialogo: true);
            return;
        }

        var v = r.Datos!;
        Ui.Mostrar($"Vehículo #{v.IdVehiculo} {v.Marca} {v.Modelo} ({v.Placa}) registrado.", TipoMensaje.Exito, r.CodigoHttp);
        MessageBox.Show(this,
            $"Vehículo registrado correctamente (HTTP {r.CodigoHttp}).\n\n" +
            $"ID: {v.IdVehiculo}\nVehículo: {v.Marca} {v.Modelo}\nPlaca: {v.Placa}\n" +
            $"Precio: {v.Precio:N2} USD\nEstado: {v.Estado}",
            "Registro exitoso", MessageBoxButtons.OK, MessageBoxIcon.Information);

        LimpiarFormulario();
        _txtFiltroMarca.Clear();
        _cboEstado.SelectedIndex = 0;
        await CargarListaAsync(seleccionarId: v.IdVehiculo);
    }

    private void LimpiarFormulario()
    {
        foreach (var t in new[] { _txtMarca, _txtModelo, _txtPlaca, _txtChasis, _txtColor }) t.Clear();
        _numAnio.Value = DateTime.Now.Year;
        _numPrecio.Value = 0;
        Errores.Clear();
        _txtMarca.Focus();
    }

    private void ColorearEstado(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (_grilla.Columns[e.ColumnIndex].DataPropertyName != nameof(Vehiculo.Estado) || e.Value is not string estado) return;
        e.CellStyle!.ForeColor = estado switch
        {
            "DISPONIBLE" => Estilo.Exito,
            "RESERVADO" => Estilo.Aviso,
            "VENDIDO" => Estilo.Error,
            _ => e.CellStyle.ForeColor
        };
        e.CellStyle.Font = Estilo.Negrita;
    }
}
