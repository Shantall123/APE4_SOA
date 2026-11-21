package ec.edu.uta.notificaciones.controller;

import ec.edu.uta.notificaciones.dto.ListadoNotificaciones;
import ec.edu.uta.notificaciones.dto.NotificacionRequest;
import ec.edu.uta.notificaciones.dto.NotificacionResponse;
import ec.edu.uta.notificaciones.service.NotificacionService;
import ec.edu.uta.notificaciones.service.NotificacionVentaService;
import org.springframework.http.HttpStatus;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.*;

@RestController
@RequestMapping("/api/notificaciones")
public class NotificacionController {

    private final NotificacionService notificacionService;
    private final NotificacionVentaService notificacionVentaService;

    public NotificacionController(NotificacionService notificacionService,
                                  NotificacionVentaService notificacionVentaService) {
        this.notificacionService = notificacionService;
        this.notificacionVentaService = notificacionVentaService;
    }

    /**
     * Historial de notificaciones de venta para JavaFX (solo lectura).
     * Ejemplo: GET /api/notificaciones?limite=50 -> 200 {"total": n, "notificaciones": [...]}
     * Una lista vacía responde 200 con "notificaciones": [] (no es un error).
     */
    @GetMapping
    public ListadoNotificaciones listar(@RequestParam(defaultValue = "50") int limite) {
        return notificacionVentaService.listar(limite);
    }

    @PostMapping
    public ResponseEntity<NotificacionResponse> crear(
            @RequestBody NotificacionRequest request) {

        NotificacionResponse response =
                notificacionService.procesar(request);

        return ResponseEntity
                .status(HttpStatus.CREATED)
                .body(response);
    }
}
