package ec.edu.uta.notificaciones.service;

import ec.edu.uta.notificaciones.dto.EventoVenta;
import ec.edu.uta.notificaciones.messaging.EventoInvalidoException;
import ec.edu.uta.notificaciones.model.Notificacion;
import ec.edu.uta.notificaciones.repository.NotificacionRepository;
import org.springframework.dao.DataIntegrityViolationException;
import org.springframework.stereotype.Service;

import java.time.Instant;
import java.time.LocalDateTime;
import java.time.ZoneOffset;
import java.time.format.DateTimeParseException;

/** Registra en la tabla notificaciones los avisos de venta, sin duplicar por eventoId. */
@Service
public class NotificacionVentaService {

    public static final String TIPO_VENTA_COMPLETADA = "VENTA_COMPLETADA";

    public enum Resultado { REGISTRADA, DUPLICADA }

    private final NotificacionRepository repositorio;

    public NotificacionVentaService(NotificacionRepository repositorio) {
        this.repositorio = repositorio;
    }

    /** Comprueba que el evento tenga los datos mínimos; si no, lanza EventoInvalidoException. */
    public void validar(EventoVenta e) {
        if (e == null) {
            throw new EventoInvalidoException("Mensaje vacío");
        }
        if (e.eventoId() == null || e.eventoId().isBlank() || e.eventoId().length() > 36) {
            throw new EventoInvalidoException("eventoId ausente o con más de 36 caracteres");
        }
        if (!TIPO_VENTA_COMPLETADA.equals(e.tipo())) {
            throw new EventoInvalidoException("Tipo de evento no soportado: " + e.tipo());
        }
        if (e.ventaId() == null || e.ventaId() <= 0) {
            throw new EventoInvalidoException("ventaId ausente o no válido");
        }
        if (e.vehiculoId() == null || e.vehiculoId() <= 0) {
            throw new EventoInvalidoException("vehiculoId ausente o no válido");
        }
        if (e.moneda() != null && e.moneda().length() > 3) {
            throw new EventoInvalidoException("moneda no válida");
        }
        fechaUtc(e.fechaUTC());
    }

    /**
     * Guarda el aviso una sola vez. Si el eventoId ya existe (reentrega del
     * mensaje o evento publicado dos veces), no inserta y devuelve DUPLICADA.
     * Los errores de base de datos se propagan para que el listener no confirme.
     */
    public Resultado registrar(EventoVenta e) {
        validar(e);
        if (repositorio.existsByEventoId(e.eventoId())) {
            return Resultado.DUPLICADA;
        }
        Notificacion n = new Notificacion();
        n.setEventoId(e.eventoId());
        n.setTipo(e.tipo());
        n.setVentaId(e.ventaId());
        n.setVehiculoId(e.vehiculoId());
        n.setPrecioAcordado(e.precioAcordado());
        n.setMoneda(e.moneda());
        n.setFechaVenta(fechaUtc(e.fechaUTC()));
        n.setMensaje(mensaje(e));
        n.setRecibidaEn(LocalDateTime.now(ZoneOffset.UTC));
        try {
            repositorio.saveAndFlush(n);
        } catch (DataIntegrityViolationException ex) {
            // Dos entregas simultáneas del mismo evento: la restricción UNIQUE
            // (uq_notificaciones_evento) impide el segundo registro.
            if (repositorio.existsByEventoId(e.eventoId())) {
                return Resultado.DUPLICADA;
            }
            throw ex;
        }
        return Resultado.REGISTRADA;
    }

    static String mensaje(EventoVenta e) {
        String precio = e.precioAcordado() == null ? "precio no informado"
                : e.precioAcordado().toPlainString() + (e.moneda() == null ? "" : " " + e.moneda());
        return "Venta " + e.ventaId() + " completada: vehículo " + e.vehiculoId() + ", " + precio;
    }

    private static LocalDateTime fechaUtc(String fecha) {
        if (fecha == null || fecha.isBlank()) {
            return null;
        }
        try {
            return LocalDateTime.ofInstant(Instant.parse(fecha), ZoneOffset.UTC);
        } catch (DateTimeParseException ex) {
            throw new EventoInvalidoException("fechaUTC no válida: " + fecha);
        }
    }
}
