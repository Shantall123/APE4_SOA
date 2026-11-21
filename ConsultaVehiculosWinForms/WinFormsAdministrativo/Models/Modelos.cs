using System.Globalization;
using System.Text.Json.Serialization;

namespace WinFormsAdministrativo.Models;

// ---------------------------------------------------------------------
// Autenticación (API Java: POST /oauth/token y GET /api/identidad)
// ---------------------------------------------------------------------
public sealed class TokenRespuesta
{
    [JsonPropertyName("access_token")] public string AccessToken { get; set; } = "";
    [JsonPropertyName("token_type")] public string TokenType { get; set; } = "";
    [JsonPropertyName("expires_in")] public long ExpiresIn { get; set; }
    [JsonPropertyName("scope")] public string Scope { get; set; } = "";
}

public sealed class Identidad
{
    [JsonPropertyName("usuario")] public string Usuario { get; set; } = "";
    [JsonPropertyName("roles")] public List<string> Roles { get; set; } = new();

    /// <summary>Rol sin el prefijo ROLE_ (CONSULTA, OPERADOR o ADMIN).</summary>
    public string Rol => Roles.Count == 0 ? "—" : Roles[0].Replace("ROLE_", "");
}

// ---------------------------------------------------------------------
// Vehículos (API Java)
// ---------------------------------------------------------------------
public sealed class Vehiculo
{
    [JsonPropertyName("idVehiculo")] public int IdVehiculo { get; set; }
    [JsonPropertyName("marca")] public string Marca { get; set; } = "";
    [JsonPropertyName("modelo")] public string Modelo { get; set; } = "";
    [JsonPropertyName("placa")] public string Placa { get; set; } = "";
    [JsonPropertyName("chasis")] public string Chasis { get; set; } = "";
    [JsonPropertyName("anio")] public int Anio { get; set; }
    [JsonPropertyName("color")] public string? Color { get; set; }
    [JsonPropertyName("precio")] public decimal Precio { get; set; }
    [JsonPropertyName("estado")] public string Estado { get; set; } = "";
}

/// <summary>Cuerpo de POST /api/vehiculos (VehiculoRequest de Java).</summary>
public sealed class VehiculoNuevo
{
    [JsonPropertyName("marca")] public string Marca { get; set; } = "";
    [JsonPropertyName("modelo")] public string Modelo { get; set; } = "";
    [JsonPropertyName("placa")] public string Placa { get; set; } = "";
    [JsonPropertyName("chasis")] public string Chasis { get; set; } = "";
    [JsonPropertyName("anio")] public int Anio { get; set; }
    [JsonPropertyName("color")] public string Color { get; set; } = "";
    [JsonPropertyName("precio")] public decimal Precio { get; set; }
}

// ---------------------------------------------------------------------
// Compradores (FastAPI)
// ---------------------------------------------------------------------
public sealed class Comprador
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("nombre")] public string Nombre { get; set; } = "";
    [JsonPropertyName("correo")] public string Correo { get; set; } = "";
}

/// <summary>Cuerpo de POST /api/compradores (CrearComprador de FastAPI).</summary>
public sealed class CompradorNuevo
{
    [JsonPropertyName("documento")] public string Documento { get; set; } = "";
    [JsonPropertyName("nombre")] public string Nombre { get; set; } = "";
    [JsonPropertyName("correo")] public string Correo { get; set; } = "";
}

// ---------------------------------------------------------------------
// Ventas (FastAPI) — solo consulta; este cliente NO registra ventas.
// ---------------------------------------------------------------------
public sealed class Venta
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("operacionId")] public string OperacionId { get; set; } = "";
    [JsonPropertyName("vehiculoId")] public int VehiculoId { get; set; }
    [JsonPropertyName("compradorId")] public int CompradorId { get; set; }
    [JsonPropertyName("precioAcordado")] public string? PrecioAcordado { get; set; }
    [JsonPropertyName("moneda")] public string? Moneda { get; set; }
    [JsonPropertyName("estado")] public string Estado { get; set; } = "";
    [JsonPropertyName("notificacion")] public string Notificacion { get; set; } = "";
    [JsonPropertyName("fechaUTC")] public string FechaUtc { get; set; } = "";

    /// <summary>Precio como número para mostrarlo con formato de moneda.</summary>
    [JsonIgnore]
    public decimal? Precio => decimal.TryParse(PrecioAcordado, NumberStyles.Number,
        CultureInfo.InvariantCulture, out var p) ? p : null;

    /// <summary>Fecha convertida de UTC a la hora local del equipo.</summary>
    [JsonIgnore]
    public DateTime? FechaLocal => DateTime.TryParse(FechaUtc, CultureInfo.InvariantCulture,
        DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var f)
        ? f.ToLocalTime() : null;
}

public sealed class VentasPagina
{
    [JsonPropertyName("items")] public List<Venta> Items { get; set; } = new();
    [JsonPropertyName("total")] public int Total { get; set; }
    [JsonPropertyName("limit")] public int Limit { get; set; }
    [JsonPropertyName("offset")] public int Offset { get; set; }
}

public sealed class ResumenVentas
{
    [JsonPropertyName("completadas")] public int Completadas { get; set; }
    [JsonPropertyName("ingresos")] public decimal Ingresos { get; set; }
    [JsonPropertyName("pendientes")] public int Pendientes { get; set; }
    [JsonPropertyName("enRevision")] public int EnRevision { get; set; }
    [JsonPropertyName("fallidas")] public int Fallidas { get; set; }
}
