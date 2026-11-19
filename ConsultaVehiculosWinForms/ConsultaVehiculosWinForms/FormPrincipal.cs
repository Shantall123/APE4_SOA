using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace ConsultaVehiculosWinForms;

public partial class FormPrincipal : Form
{

    private const string UrlJava = "http://localhost:8090/";
    private static readonly HttpClient ClienteHttp = new()
    {
        BaseAddress = new Uri(UrlJava),
        Timeout = TimeSpan.FromSeconds(15)
    };
    public FormPrincipal()
    {
        InitializeComponent();
    }

    private async void btnConsultar_Click(object? sender, EventArgs e)
    {
        string token = txtToken.Text.Trim();
        if (token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            token = token[7..].Trim();

        }

        if (string.IsNullOrWhiteSpace(token))
        {
            MostrarEstado("Ingrese un token valido.", esError: true);
            txtToken.Focus();
            LimpiarDatos();
            return;
        }

        btnConsultar.Enabled = false;
        MostrarEstado("consultando vehiculos...");

        try 
        {
            using var solicitud = new HttpRequestMessage(HttpMethod.Get, "api/vehiculos");
            solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using HttpResponseMessage respuesta = await ClienteHttp.SendAsync(solicitud);
            if (respuesta.StatusCode == HttpStatusCode.Unauthorized)
            {
                LimpiarDatos();
                MostrarEstado("codigo 401: token invalido o caducado", esError: true);
                return; 
            }

            if (respuesta.StatusCode == HttpStatusCode.Forbidden)
            {
                LimpiarDatos();
                MostrarEstado("codigo 403: no tiene permisos para acceder a este recurso", esError: true);
                return;
            }

            if (!respuesta.IsSuccessStatusCode)
            {
                LimpiarDatos();
                MostrarEstado($"error Http {(int)respuesta.StatusCode} al consultar", esError: true);
                return;
            }

            string contenidoJson = await respuesta.Content.ReadAsStringAsync();
            List<Vehiculo> lista = JsonSerializer.Deserialize<List<Vehiculo>>(
                contenidoJson,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true 
                }) ?? new List<Vehiculo>();

            dgvVehiculos.DataSource = null;
            dgvVehiculos.DataSource = lista;

            lblTotal.Text = $"Total de vehiculos : {lista.Count}";

            MostrarEstado(lista.Count==0 ? "HTTP 200: no hay vehiculos registrados." : "HTTP 200: consulta exitosa."); 
        }
        catch (HttpRequestException)
        {
            LimpiarDatos();
            MostrarEstado("no se pudo conectar a spring boot", esError: true); 
        }
        catch (TaskCanceledException)
        {
            LimpiarDatos();
            MostrarEstado("la api no respondio en 15 segundos.", esError: true);

        }

        catch (JsonException)
        {
            LimpiarDatos();
            MostrarEstado("formato json invalido", esError: true);

        }
        finally
        {
            btnConsultar.Enabled = true;
        }

    }



    private void MostrarEstado(string mensaje, bool esError = false)
    {
        lblEstado.ForeColor = esError ? Color.Firebrick : Color.FromArgb(50, 64, 76);
        lblEstado.Text = mensaje;
    }

    private void LimpiarDatos()
    {
        dgvVehiculos.DataSource = null;
        lblTotal.Text = "Total de vehículos: 0";
    }


}