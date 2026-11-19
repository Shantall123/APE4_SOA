package ec.edu.com;
import java.math.BigDecimal;

import com.fasterxml.jackson.annotation.JsonIgnoreProperties;
@JsonIgnoreProperties(ignoreUnknown = true)
public record Vehiculo (    int idVehiculo,
    String marca,
    String modelo,
    String placa,
    String color,
    String chasis,
    int anio, 
    BigDecimal precio,
    String estado) {

    
}
