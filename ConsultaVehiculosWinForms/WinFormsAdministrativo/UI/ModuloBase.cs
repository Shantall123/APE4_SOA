using WinFormsAdministrativo.Services;

namespace WinFormsAdministrativo.UI;

/// <summary>
/// Base de los módulos (pestañas): disposición en dos columnas
/// (formulario a la izquierda y listado a la derecha) y utilidades comunes.
/// </summary>
internal abstract class ModuloBase : UserControl
{
    protected readonly ApiClient Api;
    protected readonly IContextoUi Ui;
    protected readonly ErrorProvider Errores = new() { BlinkStyle = ErrorBlinkStyle.NeverBlink };
    private bool _cargado;

    protected ModuloBase(ApiClient api, IContextoUi ui)
    {
        Api = api;
        Ui = ui;
        Dock = DockStyle.Fill;
        BackColor = Estilo.Fondo;
        Font = Estilo.Normal;
        Padding = new Padding(6);
    }

    /// <summary>Carga los datos la primera vez que se muestra la pestaña.</summary>
    public async Task CargarSiHaceFaltaAsync()
    {
        if (_cargado) return;
        _cargado = true;
        await RecargarAsync();
    }

    public abstract Task RecargarAsync();

    /// <summary>Raíz con columna izquierda de ancho fijo y columna derecha flexible.</summary>
    protected static TableLayoutPanel DosColumnas(Control izquierda, Control derecha, int anchoIzquierda = 360)
    {
        var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, anchoIzquierda));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        t.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        t.Controls.Add(izquierda, 0, 0);
        t.Controls.Add(derecha, 1, 0);
        return t;
    }

    /// <summary>Tabla vertical para los campos de un formulario de registro.</summary>
    protected static TableLayoutPanel Formulario() => new()
    {
        Dock = DockStyle.Fill,
        ColumnCount = 1,
        AutoScroll = true,
        Font = Estilo.Normal,
        ForeColor = Color.FromArgb(33, 37, 41),
        Padding = new Padding(4, 6, 18, 4)
    };

    protected static void Campo(TableLayoutPanel t, string etiqueta, Control control, string? ayuda = null)
    {
        t.ColumnStyles.Clear();
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        t.Controls.Add(Estilo.Etiqueta(etiqueta, negrita: true));
        control.Dock = DockStyle.Fill;
        t.Controls.Add(control);
        if (ayuda is not null) t.Controls.Add(Estilo.Ayuda(ayuda));
    }

    /// <summary>Contenedor de lista con cabecera, grilla y un aviso cuando no hay registros.</summary>
    protected static Panel ContenedorLista(Control cabecera, DataGridView grilla, Label vacio, Control pie) =>
        ContenedorLista(new[] { cabecera }, grilla, vacio, pie);

    /// <summary>Variante con varias cabeceras apiladas de arriba hacia abajo en el orden recibido.</summary>
    protected static Panel ContenedorLista(Control[] cabeceras, DataGridView grilla, Label vacio, Control pie)
    {
        var interior = new Panel { Dock = DockStyle.Fill, Font = Estilo.Normal, ForeColor = Color.FromArgb(33, 37, 41) };
        var zonaGrilla = new Panel { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.White };
        vacio.Dock = DockStyle.Fill;
        vacio.TextAlign = ContentAlignment.MiddleCenter;
        vacio.ForeColor = Estilo.TextoSuave;
        vacio.BackColor = Color.White;
        vacio.Visible = false;
        zonaGrilla.Controls.Add(vacio);
        zonaGrilla.Controls.Add(grilla);

        pie.Dock = DockStyle.Bottom;
        interior.Controls.Add(zonaGrilla);
        interior.Controls.Add(pie);
        // En WinForms el último control agregado con Dock=Top queda más arriba:
        // se agregan en orden inverso para respetar el orden recibido.
        for (int i = cabeceras.Length - 1; i >= 0; i--)
        {
            cabeceras[i].Dock = DockStyle.Top;
            interior.Controls.Add(cabeceras[i]);
        }
        return interior;
    }

    protected static void MostrarVacio(Label vacio, DataGridView grilla, bool sinDatos, string texto)
    {
        vacio.Text = texto;
        vacio.Visible = sinDatos;
        if (sinDatos) vacio.BringToFront(); else grilla.BringToFront();
    }

    /// <summary>
    /// Marca cada campo inválido y, si hay errores, los lista en un cuadro de
    /// mensaje y lleva el cursor al primero. Devuelve true si todo es válido.
    /// </summary>
    protected bool ValidarFormulario(params (Control control, bool invalido, string mensaje)[] reglas)
    {
        Errores.Clear();
        var problemas = new List<string>();
        Control? primero = null;

        foreach (var (control, invalido, mensaje) in reglas)
        {
            if (!invalido) continue;
            Errores.SetError(control, mensaje);
            problemas.Add("•  " + mensaje);
            primero ??= control;
        }

        if (problemas.Count == 0) return true;

        Ui.Mostrar($"No se envió la solicitud: hay {problemas.Count} dato(s) por corregir.", TipoMensaje.Aviso);
        MessageBox.Show(this,
            "No se envió la solicitud. Corrija los siguientes datos:\n\n" + string.Join("\n", problemas),
            "Datos incompletos", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        primero!.Focus();
        return false;
    }
}
