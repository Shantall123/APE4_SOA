namespace ConsultaVehiculosWinForms;

partial class FormPrincipal
{
    /// <summary>
    ///  Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    ///  Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    ///  Required method for Designer support - do not modify
    ///  the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        btnConsultar = new Button();
        lblTitulo = new Label();
        lblToken = new Label();
        lblTotal = new Label();
        lblEstado = new Label();
        txtToken = new TextBox();
        dgvVehiculos = new DataGridView();
        ((System.ComponentModel.ISupportInitialize)dgvVehiculos).BeginInit();
        SuspendLayout();
        // 
        // btnConsultar
        // 
        btnConsultar.Location = new Point(949, 141);
        btnConsultar.Name = "btnConsultar";
        btnConsultar.Size = new Size(150, 46);
        btnConsultar.TabIndex = 0;
        btnConsultar.Text = "Consultar";
        btnConsultar.UseVisualStyleBackColor = true;
        btnConsultar.Click += btnConsultar_Click;
        // 
        // lblTitulo
        // 
        lblTitulo.AutoSize = true;
        lblTitulo.Location = new Point(53, 29);
        lblTitulo.Name = "lblTitulo";
        lblTitulo.Size = new Size(239, 32);
        lblTitulo.TabIndex = 1;
        lblTitulo.Text = "Vehículos registrados";
        // 
        // lblToken
        // 
        lblToken.AutoSize = true;
        lblToken.Location = new Point(53, 99);
        lblToken.Name = "lblToken";
        lblToken.Size = new Size(169, 32);
        lblToken.TabIndex = 2;
        lblToken.Text = "Ingrese el JWT";
        // 
        // lblTotal
        // 
        lblTotal.AutoSize = true;
        lblTotal.Location = new Point(53, 547);
        lblTotal.Name = "lblTotal";
        lblTotal.Size = new Size(231, 32);
        lblTotal.TabIndex = 3;
        lblTotal.Text = "Total de vehículos: 0";
        // 
        // lblEstado
        // 
        lblEstado.AutoSize = true;
        lblEstado.Location = new Point(53, 187);
        lblEstado.Name = "lblEstado";
        lblEstado.Size = new Size(220, 32);
        lblEstado.TabIndex = 4;
        lblEstado.Text = "Listo para consultar";
        // 
        // txtToken
        // 
        txtToken.Location = new Point(53, 145);
        txtToken.Name = "txtToken";
        txtToken.Size = new Size(890, 39);
        txtToken.TabIndex = 6;
        // 
        // dgvVehiculos
        // 
        dgvVehiculos.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        dgvVehiculos.Location = new Point(53, 236);
        dgvVehiculos.Name = "dgvVehiculos";
        dgvVehiculos.RowHeadersWidth = 82;
        dgvVehiculos.Size = new Size(1046, 300);
        dgvVehiculos.TabIndex = 7;
        // 
        // FormPrincipal
        // 
        AutoScaleDimensions = new SizeF(13F, 32F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1158, 621);
        Controls.Add(dgvVehiculos);
        Controls.Add(txtToken);
        Controls.Add(lblEstado);
        Controls.Add(lblTotal);
        Controls.Add(lblToken);
        Controls.Add(lblTitulo);
        Controls.Add(btnConsultar);
        Name = "FormPrincipal";
        Text = "Form1";
        ((System.ComponentModel.ISupportInitialize)dgvVehiculos).EndInit();
        ResumeLayout(false);
        PerformLayout();
    }

    #endregion

    private Button btnConsultar;
    private Label lblTitulo;
    private Label lblToken;
    private Label lblTotal;
    private Label lblEstado;
    private TextBox txtToken;
    private DataGridView dgvVehiculos;
}
