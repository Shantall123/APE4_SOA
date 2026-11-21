package ec.edu.uta.notificaciones.dto;

import java.util.List;

/** Respuesta de GET /api/notificaciones: total registrado y los avisos más recientes primero. */
public record ListadoNotificaciones(long total, List<NotificacionDto> notificaciones) {
}
