// Define el paquete dentro del proyecto donde se ubica el controlador REST
package ec.edu.uta.venta_vehiculos_api.controller;

// Importa la interfaz List para retornar colecciones de objetos
import java.net.URI;
import java.util.List;
import java.util.Map;

import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.DeleteMapping;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PatchMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.PutMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;

import ec.edu.uta.venta_vehiculos_api.client.NotificacionClient;
import ec.edu.uta.venta_vehiculos_api.dto.VehiculoRequest;
import ec.edu.uta.venta_vehiculos_api.dto.VehiculoResponse;
import ec.edu.uta.venta_vehiculos_api.service.VehiculoService;
import jakarta.validation.Valid;

@RestController

@RequestMapping("/api/vehiculos")
public class VehiculoController {

    private final VehiculoService service;

    private final NotificacionClient avisos;

    public VehiculoController(
            VehiculoService service,
            NotificacionClient avisos) {

        this.service = service;

        this.avisos = avisos;
    }

    @GetMapping
    public List<VehiculoResponse> listar(

            @RequestParam(required = false) String marca,
            @RequestParam(required = false) String estado) {

        return service.listar(marca, estado);
    }


    @GetMapping("/{id}")
    public VehiculoResponse obtener(

            @PathVariable int id) {

        return service.obtener(id);
    }

    @PostMapping
    public ResponseEntity<VehiculoResponse> crear(

            @Valid @RequestBody VehiculoRequest d) {

        var r = service.crear(d);

        avisos.enviar(
                r.idVehiculo(),
                "REGISTRO",
                "Vehículo registrado"
        );

        return ResponseEntity.created(
                URI.create("/api/vehiculos/" + r.idVehiculo())
        ).body(r);
    }

    @PutMapping("/{id}")
    public VehiculoResponse actualizar(

            @PathVariable int id,

            @Valid @RequestBody VehiculoRequest d) {

        var r = service.actualizar(id, d);

        avisos.enviar(
                id,
                "ACTUALIZACION",
                "Vehículo actualizado"
        );

        return r;
    }

    @PatchMapping(
            value = "/{id}",
            consumes = "application/merge-patch+json"
    )
    public VehiculoResponse precio(

            @PathVariable int id,

            @RequestBody Map<String, Object> d) {

        var r = service.precio(id, d);

        avisos.enviar(
                id,
                "ACTUALIZACION",
                "Precio actualizado"
        );

        return r;
    }

    @DeleteMapping("/{id}")
    public ResponseEntity<Void> eliminar(

            @PathVariable int id) {

        service.eliminar(id);

        avisos.enviar(
                id,
                "ELIMINACION",
                "Vehículo eliminado"
        );

        return ResponseEntity.noContent().build();
    }

}
