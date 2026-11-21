package ec.edu.uta.notificaciones.dto;

import com.fasterxml.jackson.annotation.JsonIgnoreProperties;

import java.math.BigDecimal;

/** Cuerpo del mensaje VENTA_COMPLETADA que publica ventas-api en RabbitMQ. */
@JsonIgnoreProperties(ignoreUnknown = true)
public record EventoVenta(
        String eventoId,
        String tipo,
        Integer ventaId,
        Integer vehiculoId,
        BigDecimal precioAcordado,
        String moneda,
        String fechaUTC) {
}
