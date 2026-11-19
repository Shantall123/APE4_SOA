package ec.edu.uta.venta_vehiculos_api.dto;

import java.math.BigDecimal;

import jakarta.validation.constraints.Digits;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;
import jakarta.validation.constraints.Positive;
import jakarta.validation.constraints.Size;

public record VehiculoRequest(

    @NotBlank String marca,

    @NotBlank String modelo,

    @NotBlank String placa,

    @NotBlank @Size(min=17, max=17) String chasis,

    @NotNull Integer anio,

    String color,

    @NotNull @Positive @Digits(integer=10, fraction=2)
    BigDecimal precio

) {}
