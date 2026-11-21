package ec.edu.uta.notificaciones.model;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.Table;

import java.math.BigDecimal;
import java.time.LocalDateTime;

/** Aviso de venta recibido desde RabbitMQ (tabla notificaciones, db/notificaciones.sql). */
@Entity
@Table(name = "notificaciones")
public class Notificacion {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    /** UNIQUE en la tabla: el mismo evento no se registra dos veces. */
    @Column(name = "evento_id", nullable = false, unique = true, length = 36)
    private String eventoId;

    @Column(nullable = false, length = 40)
    private String tipo;

    @Column(name = "venta_id", nullable = false)
    private Integer ventaId;

    @Column(name = "vehiculo_id", nullable = false)
    private Integer vehiculoId;

    @Column(name = "precio_acordado", precision = 12, scale = 2)
    private BigDecimal precioAcordado;

    @Column(columnDefinition = "char(3)")
    private String moneda;

    /** fechaUTC del evento (UTC). */
    @Column(name = "fecha_venta")
    private LocalDateTime fechaVenta;

    @Column(nullable = false, length = 255)
    private String mensaje;

    /** Momento en que el listener registró el aviso (UTC). */
    @Column(name = "recibida_en", nullable = false)
    private LocalDateTime recibidaEn;

    public Long getId() { return id; }
    public String getEventoId() { return eventoId; }
    public void setEventoId(String eventoId) { this.eventoId = eventoId; }
    public String getTipo() { return tipo; }
    public void setTipo(String tipo) { this.tipo = tipo; }
    public Integer getVentaId() { return ventaId; }
    public void setVentaId(Integer ventaId) { this.ventaId = ventaId; }
    public Integer getVehiculoId() { return vehiculoId; }
    public void setVehiculoId(Integer vehiculoId) { this.vehiculoId = vehiculoId; }
    public BigDecimal getPrecioAcordado() { return precioAcordado; }
    public void setPrecioAcordado(BigDecimal precioAcordado) { this.precioAcordado = precioAcordado; }
    public String getMoneda() { return moneda; }
    public void setMoneda(String moneda) { this.moneda = moneda; }
    public LocalDateTime getFechaVenta() { return fechaVenta; }
    public void setFechaVenta(LocalDateTime fechaVenta) { this.fechaVenta = fechaVenta; }
    public String getMensaje() { return mensaje; }
    public void setMensaje(String mensaje) { this.mensaje = mensaje; }
    public LocalDateTime getRecibidaEn() { return recibidaEn; }
    public void setRecibidaEn(LocalDateTime recibidaEn) { this.recibidaEn = recibidaEn; }
}
