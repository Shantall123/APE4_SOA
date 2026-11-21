using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using WinFormsAdministrativo.Config;
using WinFormsAdministrativo.Models;

namespace WinFormsAdministrativo.Services;

/// <summary>
/// Cliente HTTP de la aplicación administrativa. Toda la comunicación pasa
/// por NGINX (BaseUrl): /oauth/token y /api/vehiculos llegan a la API Java,
/// /api/compradores y /api/ventas llegan a FastAPI.
///
/// Responsabilidades: construir peticiones, adjuntar el JWT, interpretar el
/// código HTTP y el cuerpo de error. No contiene controles ni reglas de negocio.
/// Este cliente no expone ningún método para registrar ventas.
/// </summary>
public sealed class ApiClient : IDisposable
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        // FastAPI serializa los Decimal como texto (p. ej. "ingresos": "442001.11").
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    private readonly AppConfig _config;
    private readonly HttpClient _http;
    private string? _token;

    public ApiClient(AppConfig config)
    {
        _config = config;
        _http = new HttpClient
        {
            BaseAddress = new Uri(config.BaseUrl),
            Timeout = TimeSpan.FromSeconds(config.TimeoutSegundos)
        };
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public string BaseUrl => _config.BaseUrl;
    public Identidad? Usuario { get; private set; }
    public bool Autenticado => _token is not null;

    // =================================================================
    // Autenticación
    // =================================================================

    /// <summary>POST /oauth/token (grant_type=password) y luego GET /api/identidad.</summary>
    public async Task<ApiResultado<Identidad>> IniciarSesionAsync(string usuario, string clave)
    {
        var basic = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{_config.ClientId}:{_config.ClientSecret}"));

        using var solicitud = new HttpRequestMessage(HttpMethod.Post, "oauth/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["username"] = usuario,
                ["password"] = clave
            })
        };
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);

        var token = await EnviarAsync<TokenRespuesta>(solicitud, conToken: false);
        if (!token.Exito)
        {
            // En el login, 401 significa credenciales incorrectas (no sesión vencida).
            string mensaje = token.CodigoHttp == 401
                ? "Usuario o contraseña incorrectos (HTTP 401)."
                : token.Mensaje;
            return ApiResultado<Identidad>.Falla(token.Tipo, mensaje, token.CodigoHttp, token.DetalleServicio);
        }

        _token = token.Datos!.AccessToken;

        var identidad = await ObtenerAsync<Identidad>("api/identidad");
        if (!identidad.Exito)
        {
            CerrarSesion();
            return identidad;
        }

        Usuario = identidad.Datos;
        return ApiResultado<Identidad>.Ok(200, identidad.Datos!,
            $"Sesión iniciada como {Usuario!.Usuario} ({Usuario.Rol}).");
    }

    public void CerrarSesion()
    {
        _token = null;
        Usuario = null;
    }

    // =================================================================
    // Vehículos — API Java a través de NGINX
    // =================================================================

    public Task<ApiResultado<List<Vehiculo>>> ListarVehiculosAsync(string? marca, string? estado)
    {
        var filtros = new List<string>();
        if (!string.IsNullOrWhiteSpace(marca)) filtros.Add("marca=" + Uri.EscapeDataString(marca.Trim()));
        if (!string.IsNullOrWhiteSpace(estado)) filtros.Add("estado=" + Uri.EscapeDataString(estado));
        string ruta = "api/vehiculos" + (filtros.Count > 0 ? "?" + string.Join("&", filtros) : "");
        return ObtenerAsync<List<Vehiculo>>(ruta);
    }

    public Task<ApiResultado<Vehiculo>> CrearVehiculoAsync(VehiculoNuevo vehiculo) =>
        PublicarAsync<Vehiculo>("api/vehiculos", vehiculo);

    // =================================================================
    // Compradores — FastAPI a través de NGINX
    // =================================================================

    public Task<ApiResultado<List<Comprador>>> ListarCompradoresAsync(string? buscar)
    {
        string ruta = "api/compradores" +
            (string.IsNullOrWhiteSpace(buscar) ? "" : "?buscar=" + Uri.EscapeDataString(buscar.Trim()));
        return ObtenerAsync<List<Comprador>>(ruta);
    }

    public Task<ApiResultado<Comprador>> CrearCompradorAsync(CompradorNuevo comprador) =>
        PublicarAsync<Comprador>("api/compradores", comprador);

    // =================================================================
    // Ventas — FastAPI a través de NGINX (solo lectura)
    // =================================================================

    public Task<ApiResultado<VentasPagina>> ListarVentasAsync(int limit, int offset, string? estado)
    {
        string ruta = $"api/ventas?limit={limit}&offset={offset}" +
            (string.IsNullOrWhiteSpace(estado) ? "" : "&estado=" + Uri.EscapeDataString(estado));
        return ObtenerAsync<VentasPagina>(ruta);
    }

    public Task<ApiResultado<ResumenVentas>> ResumenVentasAsync() =>
        ObtenerAsync<ResumenVentas>("api/ventas/resumen");

    // =================================================================
    // Infraestructura HTTP
    // =================================================================

    private Task<ApiResultado<T>> ObtenerAsync<T>(string ruta) =>
        EnviarAsync<T>(new HttpRequestMessage(HttpMethod.Get, ruta), conToken: true);

    private Task<ApiResultado<T>> PublicarAsync<T>(string ruta, object cuerpo)
    {
        var solicitud = new HttpRequestMessage(HttpMethod.Post, ruta)
        {
            Content = new StringContent(JsonSerializer.Serialize(cuerpo), Encoding.UTF8, "application/json")
        };
        return EnviarAsync<T>(solicitud, conToken: true);
    }

    private async Task<ApiResultado<T>> EnviarAsync<T>(HttpRequestMessage solicitud, bool conToken)
    {
        using (solicitud)
        {
            if (conToken)
            {
                if (_token is null)
                {
                    return ApiResultado<T>.Falla(TipoResultado.ErrorHttp,
                        "No hay una sesión activa. Inicie sesión.", 401);
                }
                solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            }

            HttpResponseMessage respuesta;
            try
            {
                respuesta = await _http.SendAsync(solicitud);
            }
            catch (HttpRequestException)
            {
                return ApiResultado<T>.Falla(TipoResultado.SinConexion,
                    $"No se pudo conectar con {BaseUrl}. Verifique que NGINX y los servicios estén en ejecución.");
            }
            catch (TaskCanceledException)
            {
                return ApiResultado<T>.Falla(TipoResultado.TiempoAgotado,
                    $"El servicio no respondió en {_config.TimeoutSegundos} segundos.");
            }

            using (respuesta)
            {
                int codigo = (int)respuesta.StatusCode;
                string cuerpo = await respuesta.Content.ReadAsStringAsync();

                if (!respuesta.IsSuccessStatusCode)
                {
                    string? detalle = ExtraerDetalle(cuerpo);
                    return ApiResultado<T>.Falla(TipoResultado.ErrorHttp,
                        MensajeError(respuesta.StatusCode, detalle), codigo, detalle);
                }

                try
                {
                    T? datos = JsonSerializer.Deserialize<T>(cuerpo, Json);
                    if (datos is null)
                    {
                        return ApiResultado<T>.Falla(TipoResultado.RespuestaInvalida,
                            $"HTTP {codigo}: la respuesta llegó vacía.", codigo);
                    }
                    return ApiResultado<T>.Ok(codigo, datos, $"HTTP {codigo}: operación correcta.");
                }
                catch (JsonException)
                {
                    return ApiResultado<T>.Falla(TipoResultado.RespuestaInvalida,
                        $"HTTP {codigo}: la respuesta no tiene el formato JSON esperado.", codigo);
                }
            }
        }
    }

    /// <summary>
    /// Obtiene el texto de error de los distintos formatos del proyecto:
    /// problem+json de Java/FastAPI (detail, title), respuesta de /oauth/token
    /// (mensaje) o respuestas simples de NGINX y de seguridad (title).
    /// </summary>
    private static string? ExtraerDetalle(string cuerpo)
    {
        if (string.IsNullOrWhiteSpace(cuerpo)) return null;
        try
        {
            using var doc = JsonDocument.Parse(cuerpo);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
            foreach (var campo in new[] { "detail", "mensaje", "title", "error" })
            {
                if (doc.RootElement.TryGetProperty(campo, out var valor) &&
                    valor.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(valor.GetString()))
                {
                    return valor.GetString();
                }
            }
        }
        catch (JsonException)
        {
            // Respuesta HTML o texto plano (por ejemplo, una página de error de NGINX).
        }
        return null;
    }

    private string MensajeError(HttpStatusCode estado, string? detalle)
    {
        int codigo = (int)estado;
        string extra = string.IsNullOrWhiteSpace(detalle) ? "" : $" Detalle: {detalle}";

        return estado switch
        {
            HttpStatusCode.BadRequest =>
                $"HTTP 400: la solicitud no es válida.{extra}",
            HttpStatusCode.Unauthorized =>
                "HTTP 401: la sesión no es válida o ha vencido. Inicie sesión nuevamente.",
            HttpStatusCode.Forbidden =>
                $"HTTP 403: su rol ({Usuario?.Rol ?? "—"}) no tiene permiso para esta operación.",
            HttpStatusCode.NotFound =>
                $"HTTP 404: el recurso solicitado no existe.{extra}",
            HttpStatusCode.Conflict =>
                $"HTTP 409: conflicto con los datos existentes.{extra}",
            HttpStatusCode.UnprocessableEntity =>
                $"HTTP 422: hay datos inválidos.{extra}",
            HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout =>
                $"HTTP {codigo}: el servicio de destino no está disponible en este momento.{extra}",
            _ => $"HTTP {codigo}: no se pudo completar la operación.{extra}"
        };
    }

    public void Dispose() => _http.Dispose();
}
