package ec.edu.com;

import com.fasterxml.jackson.annotation.JsonIgnoreProperties;

import java.math.BigDecimal;

/** Aviso de venta devuelto por GET /api/notificaciones (fechas en UTC, ISO-8601). */
@JsonIgnoreProperties(ignoreUnknown = true)
public record Notificacion(
        long id,
        String eventoId,
        String tipo,
        Integer ventaId,
        Integer vehiculoId,
        BigDecimal precioAcordado,
        String moneda,
        String fechaVentaUTC,
        String mensaje,
        String recibidaEnUTC) {
}
