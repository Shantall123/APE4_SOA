using System.Text;
using System.Text.RegularExpressions;

namespace WinFormsAdministrativo.UI;

/// <summary>
/// Restricciones de entrada para los campos de texto.
///
/// 1) Filtro de caracteres: impide escribir o pegar caracteres no permitidos
///    (al pegar, se eliminan los no válidos y se conserva el resto).
/// 2) Patrones completos: se comprueban al guardar, junto con las longitudes.
///
/// Se admiten letras del español con tildes, ñ y diéresis. Se excluyen símbolos,
/// emojis y letras de otros alfabetos.
/// </summary>
internal static partial class Restricciones
{
    // ------------------------------------------------------------------
    // Conjuntos de caracteres
    // ------------------------------------------------------------------
    // Tildes, ñ y vocales con diéresis (ü del español; ë/ï en marcas como Citroën).
    private const string LetrasEspanol = "áéíóúÁÉÍÓÚñÑäëïöüÄËÏÖÜ";

    public static bool EsLetra(char c) =>
        c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' || LetrasEspanol.Contains(c);

    public static bool EsDigito(char c) => c is >= '0' and <= '9';

    public static bool EsLetraOEspacio(char c) => EsLetra(c) || c == ' ';

    /// <summary>Marca: letras, espacios y guion (Mercedes-Benz, Land Rover).</summary>
    public static bool CaracterMarca(char c) => EsLetraOEspacio(c) || c == '-';

    /// <summary>Modelo: letras, números, espacios, guion y punto (CX-5, Sportage 2.0).</summary>
    public static bool CaracterModelo(char c) => EsLetra(c) || EsDigito(c) || c is ' ' or '-' or '.';

    /// <summary>Placa: solo letras A–Z sin tildes y números.</summary>
    public static bool CaracterPlaca(char c) => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' || EsDigito(c);

    /// <summary>Chasis (VIN, ISO 3779): letras y números, excepto I, O y Q.</summary>
    public static bool CaracterChasis(char c)
    {
        char m = char.ToUpperInvariant(c);
        return (m is >= 'A' and <= 'Z' && m is not ('I' or 'O' or 'Q')) || EsDigito(c);
    }

    /// <summary>Nombre de persona: letras con tildes, espacios, apóstrofo y guion.</summary>
    public static bool CaracterNombre(char c) => EsLetraOEspacio(c) || c is '\'' or '-';

    /// <summary>Correo: caracteres válidos en una dirección (sin espacios ni tildes).</summary>
    public static bool CaracterCorreo(char c) =>
        c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' || EsDigito(c) || c is '.' or '_' or '%' or '+' or '-' or '@';

    /// <summary>Usuario de login: letras, números, punto, guion y guion bajo.</summary>
    public static bool CaracterUsuario(char c) =>
        c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' || EsDigito(c) || c is '.' or '_' or '-';

    /// <summary>Búsqueda de compradores por nombre o documento.</summary>
    public static bool CaracterBusqueda(char c) => EsLetraOEspacio(c) || EsDigito(c);

    // ------------------------------------------------------------------
    // Patrones completos (se validan al guardar)
    // ------------------------------------------------------------------
    private const string L = "a-zA-ZáéíóúÁÉÍÓÚñÑäëïöüÄËÏÖÜ";

    [GeneratedRegex($"^[{L}]+(?:[ -][{L}]+)*$")]
    public static partial Regex Marca();

    [GeneratedRegex($"^[{L}0-9]+(?:[ .-][{L}0-9]+)*$")]
    public static partial Regex Modelo();

    /// <summary>Placa ecuatoriana: 3 letras y 3 o 4 números (PBA123, PBA1234).</summary>
    [GeneratedRegex("^[A-Z]{3}[0-9]{3,4}$")]
    public static partial Regex Placa();

    [GeneratedRegex("^[A-HJ-NPR-Z0-9]{17}$")]
    public static partial Regex Chasis();

    [GeneratedRegex($"^[{L}]+(?: [{L}]+)*$")]
    public static partial Regex Color();

    [GeneratedRegex("^[0-9]{10,13}$")]
    public static partial Regex Documento();

    [GeneratedRegex($"^[{L}]+(?:[ '-][{L}]+)*$")]
    public static partial Regex Nombre();

    [GeneratedRegex(@"^[A-Za-z0-9._%+-]+@[A-Za-z0-9-]+(?:\.[A-Za-z0-9-]+)*\.[A-Za-z]{2,}$")]
    public static partial Regex Correo();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Espacios();

    /// <summary>Quita espacios al inicio y al final y deja un solo espacio entre palabras.</summary>
    public static string Normalizar(string texto) => Espacios().Replace(texto.Trim(), " ");

    // ------------------------------------------------------------------
    // Aplicación del filtro a un TextBox
    // ------------------------------------------------------------------

    /// <summary>
    /// Bloquea al escribir los caracteres que no cumplen <paramref name="permitido"/>
    /// y limpia el texto pegado. Las teclas de control (borrar, copiar o pegar) siguen funcionando.
    /// </summary>
    public static void Filtrar(TextBox caja, Func<char, bool> permitido)
    {
        caja.KeyPress += (_, e) =>
        {
            if (!char.IsControl(e.KeyChar) && !permitido(e.KeyChar))
            {
                e.Handled = true;
            }
        };

        bool limpiando = false;
        caja.TextChanged += (_, _) =>
        {
            if (limpiando) return;
            string actual = caja.Text;
            var limpio = new StringBuilder(actual.Length);
            int quitadosAntesDelCursor = 0;
            for (int i = 0; i < actual.Length; i++)
            {
                if (permitido(actual[i])) limpio.Append(actual[i]);
                else if (i < caja.SelectionStart) quitadosAntesDelCursor++;
            }
            if (limpio.Length == actual.Length) return;

            limpiando = true;
            int cursor = Math.Max(0, caja.SelectionStart - quitadosAntesDelCursor);
            caja.Text = limpio.ToString();
            caja.SelectionStart = Math.Min(cursor, caja.Text.Length);
            limpiando = false;
        };
    }
}
