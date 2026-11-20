using System.Text.Json;

namespace WinFormsAdministrativo.Config;

/// <summary>
/// Configuración local leída de appsettings.json (junto al ejecutable).
/// Contiene la URL de NGINX y las credenciales del cliente OAuth; el archivo
/// está excluido de Git y se crea a partir de appsettings.example.json.
/// </summary>
public sealed class AppConfig
{
    public string BaseUrl { get; set; } = "http://127.0.0.1:8090/";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public int TimeoutSegundos { get; set; } = 15;

    public static AppConfig Cargar()
    {
        string ruta = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

        if (!File.Exists(ruta))
        {
            throw new InvalidOperationException(
                "No se encontró appsettings.json.\n\n" +
                "Copie appsettings.example.json como appsettings.json, complete " +
                "ClientSecret con el valor de app.security.client-secret de la API Java " +
                "y vuelva a compilar.");
        }

        var config = JsonSerializer.Deserialize<AppConfig>(
            File.ReadAllText(ruta),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("appsettings.json está vacío o no es JSON válido.");

        if (string.IsNullOrWhiteSpace(config.ClientId) ||
            string.IsNullOrWhiteSpace(config.ClientSecret) ||
            config.ClientSecret.StartsWith("REEMPLAZAR", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Complete ClientId y ClientSecret en appsettings.json.");
        }

        if (!config.BaseUrl.EndsWith('/'))
        {
            config.BaseUrl += "/";
        }

        return config;
    }
}
