package ec.edu.uta.venta_vehiculos_api.controller;

import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.DeleteMapping;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.PutMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;

import ec.edu.uta.venta_vehiculos_api.dto.ReservaRequest;
import ec.edu.uta.venta_vehiculos_api.dto.ReservaResponse;
import ec.edu.uta.venta_vehiculos_api.service.ReservaService;
import jakarta.validation.Valid;

@RestController
public class ReservaController {
 private final ReservaService service;

 public ReservaController(ReservaService service) {
  this.service=service;
 }

 @PostMapping("/api/vehiculos/{id}/reservas")

 public ReservaResponse reservar(@PathVariable int id,
   @Valid @RequestBody ReservaRequest d) {

  return service.reservar(id,d.operacionId());
 }

 @GetMapping("/api/reservas/{id}")

 public ReservaResponse obtener(@PathVariable String id) {

  return service.obtener(id);
 }

 @GetMapping("/api/reservas")

 public ReservaResponse operacion(@RequestParam String operacionId) {

  return service.porOperacion(operacionId);
 }

 @PutMapping("/api/reservas/{id}/confirmacion")

 public ReservaResponse confirmar(@PathVariable String id) {

  return service.confirmar(id);
 }

 @DeleteMapping("/api/reservas/{id}")

 public ResponseEntity<Void> liberar(@PathVariable String id) {
  service.liberar(id); return ResponseEntity.noContent().build();
 }
}