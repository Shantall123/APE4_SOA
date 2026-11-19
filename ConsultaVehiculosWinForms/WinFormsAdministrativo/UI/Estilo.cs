namespace WinFormsAdministrativo.UI;

/// <summary>Paleta, fuentes y fábricas de controles para una apariencia uniforme.</summary>
internal static class Estilo
{
    public static readonly Color Primario = Color.FromArgb(31, 78, 121);
    public static readonly Color PrimarioHover = Color.FromArgb(24, 62, 97);
    public static readonly Color Fondo = Color.FromArgb(244, 246, 249);
    public static readonly Color Panel = Color.White;
    public static readonly Color Borde = Color.FromArgb(214, 220, 228);
    public static readonly Color TextoSuave = Color.FromArgb(96, 108, 122);
    public static readonly Color Exito = Color.FromArgb(25, 135, 84);
    public static readonly Color Error = Color.FromArgb(185, 28, 28);
    public static readonly Color Aviso = Color.FromArgb(180, 110, 0);
    public static readonly Color FilaAlterna = Color.FromArgb(247, 249, 252);

    public static readonly Font Titulo = new("Segoe UI Semibold", 15f);
    public static readonly Font Subtitulo = new("Segoe UI Semibold", 11f);
    public static readonly Font Normal = new("Segoe UI", 9.75f);
    public static readonly Font Negrita = new("Segoe UI Semibold", 9.75f);
    public static readonly Font Pequena = new("Segoe UI", 8.75f);

    public static Button Boton(string texto, bool principal = false)
    {
        var b = new Button
        {
            Text = texto,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(12, 4, 12, 4),
            Margin = new Padding(0, 4, 8, 4),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Font = principal ? Negrita : Normal,
            BackColor = principal ? Primario : Color.White,
            ForeColor = principal ? Color.White : Primario,
            UseVisualStyleBackColor = false
        };
        b.FlatAppearance.BorderColor = principal ? Primario : Borde;
        b.FlatAppearance.MouseOverBackColor = principal ? PrimarioHover : Color.FromArgb(232, 240, 248);
        return b;
    }

    public static Label Etiqueta(string texto, bool negrita = false) => new()
    {
        Text = texto,
        AutoSize = true,
        Font = negrita ? Negrita : Normal,
        Margin = new Padding(0, 8, 8, 2),
        Anchor = AnchorStyles.Left
    };

    public static Label Ayuda(string texto) => new()
    {
        Text = texto,
        AutoSize = true,
        Font = Pequena,
        ForeColor = TextoSuave,
        Margin = new Padding(0, 0, 0, 6)
    };

    public static TextBox Texto(int maximo, CharacterCasing mayusculas = CharacterCasing.Normal) => new()
    {
        MaxLength = maximo,
        CharacterCasing = mayusculas,
        Dock = DockStyle.Fill,
        Margin = new Padding(0, 0, 0, 2)
    };

    public static ComboBox Lista(params string[] opciones)
    {
        var c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
        c.Items.AddRange(opciones);
        c.SelectedIndex = 0;
        return c;
    }

    /// <summary>Contenedor blanco con título, usado como "tarjeta".</summary>
    public static GroupBox Tarjeta(string titulo) => new()
    {
        Text = titulo,
        Dock = DockStyle.Fill,
        Font = Subtitulo,
        ForeColor = Primario,
        BackColor = Panel,
        Padding = new Padding(12, 10, 12, 12),
        Margin = new Padding(6)
    };

    public static DataGridView Grilla()
    {
        var g = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            AutoGenerateColumns = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            RowHeadersVisible = false,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            GridColor = Borde,
            EnableHeadersVisualStyles = false,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            ColumnHeadersHeight = 34,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            Font = Normal,
            ForeColor = Color.FromArgb(33, 37, 41)
        };
        g.RowTemplate.Height = 30;
        g.ColumnHeadersDefaultCellStyle.BackColor = Primario;
        g.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        g.ColumnHeadersDefaultCellStyle.Font = Negrita;
        g.ColumnHeadersDefaultCellStyle.SelectionBackColor = Primario;
        g.AlternatingRowsDefaultCellStyle.BackColor = FilaAlterna;
        g.DefaultCellStyle.SelectionBackColor = Color.FromArgb(207, 226, 243);
        g.DefaultCellStyle.SelectionForeColor = Color.Black;
        g.DefaultCellStyle.Padding = new Padding(4, 0, 4, 0);
        return g;
    }

    public static void Columna(DataGridView g, string propiedad, string encabezado,
        float peso = 100, string? formato = null, bool derecha = false)
    {
        var c = new DataGridViewTextBoxColumn
        {
            DataPropertyName = propiedad,
            HeaderText = encabezado,
            FillWeight = peso,
            SortMode = DataGridViewColumnSortMode.NotSortable
        };
        if (formato is not null) c.DefaultCellStyle.Format = formato;
        if (derecha) c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        g.Columns.Add(c);
    }

    /// <summary>Panel horizontal que acomoda controles en una fila.</summary>
    public static FlowLayoutPanel Fila() => new()
    {
        Dock = DockStyle.Top,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        WrapContents = true,
        FlowDirection = FlowDirection.LeftToRight,
        Margin = new Padding(0),
        Padding = new Padding(0, 0, 0, 4)
    };
}
