package ec.edu.uta.notificaciones.config;

import org.springframework.amqp.core.AcknowledgeMode;
import org.springframework.amqp.core.Binding;
import org.springframework.amqp.core.BindingBuilder;
import org.springframework.amqp.core.DirectExchange;
import org.springframework.amqp.core.Queue;
import org.springframework.amqp.rabbit.config.SimpleRabbitListenerContainerFactory;
import org.springframework.amqp.rabbit.connection.ConnectionFactory;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;

/**
 * Topología RabbitMQ del servicio de notificaciones (Actividad 9).
 *
 * Se declaran el exchange, la cola y el binding con los MISMOS parámetros que
 * usa ventas-api (FastAPI): si ya existen, RabbitMQ no los modifica.
 */
@Configuration
public class RabbitConfig {

    /** Exchange direct y durable donde FastAPI publica las ventas confirmadas. */
    @Bean
    public DirectExchange ventasExchange(@Value("${app.rabbitmq.exchange}") String nombre) {
        return new DirectExchange(nombre, true, false);
    }

    /** Cola durable que consume este servicio. */
    @Bean
    public Queue colaVentas(@Value("${app.rabbitmq.cola}") String nombre) {
        return new Queue(nombre, true);
    }

    /** Mensajes que no se pueden interpretar: se apartan aquí para revisarlos sin bloquear la cola. */
    @Bean
    public Queue colaInvalidos(@Value("${app.rabbitmq.cola-invalidos}") String nombre) {
        return new Queue(nombre, true);
    }

    @Bean
    public Binding bindingVentas(Queue colaVentas, DirectExchange ventasExchange,
                                 @Value("${app.rabbitmq.routing-key}") String routingKey) {
        return BindingBuilder.bind(colaVentas).to(ventasExchange).with(routingKey);
    }

    /**
     * Contenedor con confirmación MANUAL: el listener llama a basicAck solo
     * después de guardar el aviso en MySQL. prefetch = 1: un mensaje a la vez.
     */
    @Bean
    public SimpleRabbitListenerContainerFactory confirmacionManualFactory(ConnectionFactory connectionFactory) {
        SimpleRabbitListenerContainerFactory factory = new SimpleRabbitListenerContainerFactory();
        factory.setConnectionFactory(connectionFactory);
        factory.setAcknowledgeMode(AcknowledgeMode.MANUAL);
        factory.setPrefetchCount(1);
        factory.setDefaultRequeueRejected(false);
        return factory;
    }
}
