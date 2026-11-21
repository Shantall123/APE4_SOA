package ec.edu.uta.notificaciones.dto;

import ec.edu.uta.notificaciones.model.Notificacion;

import java.math.BigDecimal;
import java.time.LocalDateTime;
import java.time.format.DateTimeFormatter;

/** Aviso de venta tal como lo consulta JavaFX en GET /api/notificaciones. Fechas en UTC (ISO-8601 con Z). */
public record NotificacionDto(
        Long id,
        String eventoId,
        String tipo,
        Integer ventaId,
        Integer vehiculoId,
        BigDecimal precioAcordado,
        String moneda,
        String fechaVentaUTC,
        String mensaje,
        String recibidaEnUTC) {

    public static NotificacionDto de(Notificacion n) {
        return new NotificacionDto(n.getId(), n.getEventoId(), n.getTipo(), n.getVentaId(), n.getVehiculoId(),
                n.getPrecioAcordado(), n.getMoneda(), utc(n.getFechaVenta()), n.getMensaje(), utc(n.getRecibidaEn()));
    }

    private static final DateTimeFormatter ISO = DateTimeFormatter.ofPattern("yyyy-MM-dd'T'HH:mm:ss'Z'");

    private static String utc(LocalDateTime fecha) {
        return fecha == null ? null : ISO.format(fecha);
    }
}
