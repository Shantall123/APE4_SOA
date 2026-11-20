using WinFormsAdministrativo.Services;

namespace WinFormsAdministrativo.UI;

public enum TipoMensaje { Info, Exito, Aviso, Error }

/// <summary>
/// Servicios que la ventana principal ofrece a cada módulo: barra de estado,
/// indicador de carga y manejo uniforme de los errores HTTP.
/// </summary>
internal interface IContextoUi
{
    void Mostrar(string mensaje, TipoMensaje tipo, int? codigoHttp = null);
    void Ocupado(bool ocupado, string? mensaje = null);

    /// <summary>
    /// Presenta un resultado fallido. Con <paramref name="dialogo"/> también abre
    /// un cuadro de mensaje (útil al guardar). Ante un 401 ofrece iniciar sesión otra vez.
    /// </summary>
    void MostrarError<T>(ApiResultado<T> resultado, string operacion, bool dialogo = false);
}
