using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace ConsultaVehiculosWinForms;

public sealed class Vehiculo
{
    [JsonPropertyName("idVehiculo")]
    public int IdVehiculo { get; set; }

    [JsonPropertyName("marca")]
    public string Marca { get; set; } = "";

    [JsonPropertyName("modelo")]
    public string Modelo { get; set; } = "";

    [JsonPropertyName("placa")]
    public string Placa { get; set; } = ""; 

    [JsonPropertyName("chasis")]
    public string Chasis { get; set; } = "";

    [JsonPropertyName("anio")]
    public int Anio { get; set; }

    [JsonPropertyName("color")]
    public string Color { get; set; } = "";

    [JsonPropertyName("precio")]
    public decimal Precio { get; set; }
    [JsonPropertyName("estado")]
    public string Estado { get; set; } = "";





}
