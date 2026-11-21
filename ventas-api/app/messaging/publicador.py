"""Publicador de eventos de venta hacia RabbitMQ (Actividad 8).

Es independiente de los endpoints: el router no lo conoce. Lo usa
venta_service.notificar() después de que la venta quedó COMPLETADA en
MySQL, y el worker para las ventas cuyo evento quedó pendiente.

- El mensaje es persistente (delivery_mode=2) y la cola es durable, así que
  se conserva en RabbitMQ aunque el consumidor esté detenido.
- Se usan publisher confirms: solo si el broker confirma que lo guardó, la
  venta pasa a notificacion=ENVIADA. Si RabbitMQ no responde, se lanza la
  excepción y la venta queda notificacion=PENDIENTE para el worker.
- El eventoId se deriva del operacion_id de la venta (UUID v5): si el evento
  se reintenta, viaja con el MISMO eventoId y el consumidor puede descartar
  el duplicado.
"""
import json
from uuid import NAMESPACE_URL, uuid5

import pika

from app.config import RABBIT_EXCHANGE, RABBIT_ROUTING_KEY
from app.messaging.topologia import conectar

TIPO_VENTA_COMPLETADA = "VENTA_COMPLETADA"


def evento_venta(venta):
    """Cuerpo del evento: sin contraseñas, JWT ni datos del comprador."""
    return {
        "eventoId": str(uuid5(NAMESPACE_URL,
                              f"ventas-api/{TIPO_VENTA_COMPLETADA}/"
                              f"{venta.operacion_id}")),
        "tipo": TIPO_VENTA_COMPLETADA,
        "ventaId": venta.id,
        "vehiculoId": venta.vehiculo_id,
        "precioAcordado": str(venta.precio_acordado)
        if venta.precio_acordado is not None else None,
        "moneda": venta.moneda,
        "fechaUTC": venta.fecha.isoformat(timespec="seconds") + "Z",
    }


def publicar_venta(venta):
    """Publica el evento de una venta COMPLETADA y espera la confirmación.

    Devuelve el evento publicado. Lanza AMQPError/OSError si RabbitMQ no está
    disponible o no confirma el mensaje.
    """
    evento = evento_venta(venta)

    conexion, canal = conectar()

    try:

        canal.confirm_delivery()

        canal.basic_publish(
            exchange=RABBIT_EXCHANGE,
            routing_key=RABBIT_ROUTING_KEY,
            body=json.dumps(evento, ensure_ascii=False,
                            separators=(",", ":")).encode("utf-8"),
            properties=pika.BasicProperties(
                content_type="application/json",
                content_encoding="utf-8",
                delivery_mode=pika.DeliveryMode.Persistent,
                message_id=evento["eventoId"],
                type=TIPO_VENTA_COMPLETADA,
                app_id="ventas-api",
            ),
            mandatory=True,  # si no llega a ninguna cola, se rechaza
        )

        return evento

    finally:

        if conexion.is_open:
            conexion.close()
