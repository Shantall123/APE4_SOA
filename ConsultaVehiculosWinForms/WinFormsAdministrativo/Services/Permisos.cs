namespace WinFormsAdministrativo.Services;

/// <summary>
/// Módulos que la interfaz habilita según el rol del JWT (claim "rol").
/// Refleja las reglas reales del backend:
///
///   Operación                         CONSULTA  OPERADOR  ADMIN
///   GET  /api/vehiculos                   ✔         ✔        ✔
///   POST /api/vehiculos                   ✘         ✔        ✔
///   GET/POST /api/compradores             ✘         ✔        ✔
///   GET /api/ventas, /api/ventas/resumen  ✘         ✔        ✔
///
/// Ocultar un módulo solo mejora la experiencia: la API sigue validando
/// cada petición y responde 403 si el rol no tiene permiso.
/// </summary>
public sealed record Permisos(
    bool VerVehiculos,
    bool RegistrarVehiculos,
    bool GestionarCompradores,
    bool ConsultarVentas)
{
    public static Permisos Para(string? rol) => (rol ?? "").ToUpperInvariant() switch
    {
        "ADMIN" => new(true, true, true, true),
        "OPERADOR" => new(true, true, true, true),
        "CONSULTA" => new(true, false, false, false),
        _ => new(false, false, false, false)
    };

    /// <summary>Texto breve para la cabecera de la ventana principal.</summary>
    public string Descripcion
    {
        get
        {
            var partes = new List<string>();
            if (VerVehiculos) partes.Add(RegistrarVehiculos ? "registro y consulta de vehículos" : "consulta de vehículos");
            if (GestionarCompradores) partes.Add("registro y consulta de compradores");
            if (ConsultarVentas) partes.Add("consulta de ventas");
            return partes.Count == 0
                ? "Sin módulos habilitados para este rol"
                : char.ToUpper(partes[0][0]) + string.Join(" · ", partes)[1..];
        }
    }
}
