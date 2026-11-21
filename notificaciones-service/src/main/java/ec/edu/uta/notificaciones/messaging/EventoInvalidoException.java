package ec.edu.uta.notificaciones.messaging;

/** El mensaje no tiene el formato de un evento VENTA_COMPLETADA válido. */
public class EventoInvalidoException extends RuntimeException {

    public EventoInvalidoException(String mensaje) {
        super(mensaje);
    }
}
