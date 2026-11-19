package ec.edu.uta.venta_vehiculos_api.dto;

import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Pattern;

public record ReservaRequest(
 @NotBlank @Pattern(regexp="[A-Za-z0-9-]{1,64}")
 String operacionId
) {}
