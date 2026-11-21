package ec.edu.uta.notificaciones.exception;

import ec.edu.uta.notificaciones.dto.NotificacionResponse;
import org.springframework.dao.DataAccessException;
import org.springframework.http.HttpStatus;
import org.springframework.web.method.annotation.MethodArgumentTypeMismatchException;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.ExceptionHandler;
import org.springframework.web.bind.annotation.RestControllerAdvice;

@RestControllerAdvice
public class ApiExceptionHandler {

    @ExceptionHandler(IllegalArgumentException.class)
    public ResponseEntity<NotificacionResponse> manejarValidacion(
            IllegalArgumentException ex) {

        NotificacionResponse response =
                new NotificacionResponse(
                        false,
                        null,
                        ex.getMessage()
                );

        return ResponseEntity
                .status(HttpStatus.BAD_REQUEST)
                .body(response);
    }

    /** limite=abc u otro parámetro con tipo incorrecto: 400 en lugar de 500. */
    @ExceptionHandler(MethodArgumentTypeMismatchException.class)
    public ResponseEntity<NotificacionResponse> manejarTipo(MethodArgumentTypeMismatchException ex) {
        return ResponseEntity.status(HttpStatus.BAD_REQUEST)
                .body(new NotificacionResponse(false, null, "Parámetro no válido: " + ex.getName()));
    }

    /** MySQL no disponible: 503 para que JavaFX lo distinga de una lista vacía. */
    @ExceptionHandler(DataAccessException.class)
    public ResponseEntity<NotificacionResponse> manejarBaseDatos(DataAccessException ex) {
        return ResponseEntity.status(HttpStatus.SERVICE_UNAVAILABLE)
                .body(new NotificacionResponse(false, null, "Base de datos de notificaciones no disponible"));
    }
}
