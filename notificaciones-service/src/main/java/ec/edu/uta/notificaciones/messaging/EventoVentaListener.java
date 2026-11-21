package ec.edu.uta.notificaciones.messaging;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.rabbitmq.client.Channel;
import ec.edu.uta.notificaciones.dto.EventoVenta;
import ec.edu.uta.notificaciones.service.NotificacionVentaService;
import ec.edu.uta.notificaciones.service.NotificacionVentaService.Resultado;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.amqp.core.Message;
import org.springframework.amqp.core.MessageProperties;
import org.springframework.amqp.rabbit.annotation.RabbitListener;
import org.springframework.amqp.rabbit.core.RabbitTemplate;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Component;

import java.io.IOException;

/**
 * Listener real de la cola notificaciones.ventas (Actividad 9).
 *
 * Confirmación manual:
 *  - Evento nuevo      -> se guarda en MySQL y luego basicAck.
 *  - eventoId repetido -> no se inserta; basicAck (no se procesa dos veces).
 *  - Mensaje inválido  -> se aparta en notificaciones.ventas.invalidos y basicAck.
 *  - Falla de MySQL    -> NO se confirma: espera y basicNack con requeue; el
 *                         mensaje vuelve a la cola y no se pierde.
 */
@Component
public class EventoVentaListener {

    private static final Logger log = LoggerFactory.getLogger(EventoVentaListener.class);

    private final NotificacionVentaService servicio;
    private final ObjectMapper mapper;
    private final RabbitTemplate rabbitTemplate;
    private final String colaInvalidos;
    private final long esperaReintentoMs;

    public EventoVentaListener(NotificacionVentaService servicio, ObjectMapper mapper,
                               RabbitTemplate rabbitTemplate,
                               @Value("${app.rabbitmq.cola-invalidos}") String colaInvalidos,
                               @Value("${app.rabbitmq.espera-reintento-segundos:5}") long esperaSegundos) {
        this.servicio = servicio;
        this.mapper = mapper;
        this.rabbitTemplate = rabbitTemplate;
        this.colaInvalidos = colaInvalidos;
        this.esperaReintentoMs = esperaSegundos * 1000;
    }

    @RabbitListener(queues = "${app.rabbitmq.cola}", containerFactory = "confirmacionManualFactory")
    public void recibir(Message mensaje, Channel canal) throws IOException {
        MessageProperties props = mensaje.getMessageProperties();
        long tag = props.getDeliveryTag();
        boolean reentregado = Boolean.TRUE.equals(props.getRedelivered());

        EventoVenta evento;
        try {
            evento = mapper.readValue(mensaje.getBody(), EventoVenta.class);
            servicio.validar(evento);
        } catch (IOException | EventoInvalidoException ex) {
            apartarInvalido(mensaje, canal, tag, ex.getMessage());
            return;
        }

        try {
            Resultado resultado = servicio.registrar(evento);
            canal.basicAck(tag, false);
            if (resultado == Resultado.REGISTRADA) {
                log.info("Notificación registrada: eventoId={} tipo={} ventaId={} vehiculoId={} redelivered={}",
                        evento.eventoId(), evento.tipo(), evento.ventaId(), evento.vehiculoId(), reentregado);
            } else {
                log.info("Evento {} duplicado (ventaId={}): ya estaba registrado; se confirma sin registrarlo otra vez",
                        evento.eventoId(), evento.ventaId());
            }
        } catch (RuntimeException ex) {
            log.error("No se pudo registrar el evento {} (ventaId={}): {}. Se devuelve a la cola en {} s",
                    evento.eventoId(), evento.ventaId(), ex.getMessage(), esperaReintentoMs / 1000);
            esperar();
            canal.basicNack(tag, false, true);
        }
    }

    private void apartarInvalido(Message mensaje, Channel canal, long tag, String motivo) throws IOException {
        try {
            mensaje.getMessageProperties().setHeader("x-motivo-rechazo", motivo);
            rabbitTemplate.send("", colaInvalidos, mensaje);
            canal.basicAck(tag, false);
            log.warn("Mensaje {} inválido ({}): apartado en la cola {}",
                    mensaje.getMessageProperties().getMessageId(), motivo, colaInvalidos);
        } catch (RuntimeException ex) {
            log.error("No se pudo apartar el mensaje inválido: {}. Se devuelve a la cola", ex.getMessage());
            esperar();
            canal.basicNack(tag, false, true);
        }
    }

    private void esperar() {
        try {
            Thread.sleep(esperaReintentoMs);
        } catch (InterruptedException ex) {
            Thread.currentThread().interrupt();
        }
    }
}
