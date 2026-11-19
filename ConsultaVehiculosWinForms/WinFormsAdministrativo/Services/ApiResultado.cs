namespace WinFormsAdministrativo.Services;

/// <summary>Clasifica el resultado de una llamada HTTP para que la interfaz lo presente.</summary>
public enum TipoResultado
{
    Exito,
    ErrorHttp,      // El servicio respondió con 4xx o 5xx
    SinConexion,    // NGINX o el servicio no aceptan la conexión
    TiempoAgotado,  // No hubo respuesta dentro del tiempo límite
    RespuestaInvalida // Llegó una respuesta que no es el JSON esperado
}

/// <summary>
/// Resultado de una operación contra la API: código HTTP real, datos y
/// un mensaje comprensible para el usuario. El formulario no conoce HttpClient.
/// </summary>
public sealed class ApiResultado<T>
{
    public TipoResultado Tipo { get; init; }
    public int? CodigoHttp { get; init; }
    public T? Datos { get; init; }
    public string Mensaje { get; init; } = "";

    /// <summary>Detalle devuelto por el servicio (detail, mensaje o title).</summary>
    public string? DetalleServicio { get; init; }

    public bool Exito => Tipo == TipoResultado.Exito;
    public bool SesionVencida => CodigoHttp == 401;

    public static ApiResultado<T> Ok(int codigo, T datos, string mensaje) =>
        new() { Tipo = TipoResultado.Exito, CodigoHttp = codigo, Datos = datos, Mensaje = mensaje };

    public static ApiResultado<T> Falla(TipoResultado tipo, string mensaje, int? codigo = null, string? detalle = null) =>
        new() { Tipo = tipo, CodigoHttp = codigo, Mensaje = mensaje, DetalleServicio = detalle };
}
