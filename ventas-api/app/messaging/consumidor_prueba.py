"""Consumidor de verificación de la cola notificaciones.ventas (Actividad 8).

Sirve para demostrar el comportamiento de la cola hasta que el servicio de
notificaciones Java tenga su @RabbitListener (Actividad 9). Después se apaga
para que los dos consumidores no compitan por los mismos mensajes.

    python -m app.messaging.consumidor_prueba

- prefetch=1 y confirmación manual (basic_ack) DESPUÉS de procesar: si se
  detiene antes de confirmar, RabbitMQ vuelve a entregar el mensaje.
- Solo lee eventos: nunca llama a POST /api/ventas, por lo que procesar o
  reprocesar un mensaje no repite la venta.
- Un eventoId ya procesado se confirma sin volver a procesarlo.
"""
import json

from pika.exceptions import AMQPError

from app.config import RABBIT_COLA
from app.messaging.topologia import conectar, destino, log

procesados = set()


def recibir(canal, entrega, propiedades, cuerpo):

    try:

        evento = json.loads(cuerpo)

    except ValueError:

        log.error("Mensaje %s no es JSON válido; se descarta",
                  propiedades.message_id)

        canal.basic_nack(entrega.delivery_tag, requeue=False)

        return

    evento_id = evento.get("eventoId") or propiedades.message_id

    if evento_id in procesados:

        log.info("Evento %s duplicado (redelivered=%s): se confirma sin "
                 "reprocesar", evento_id, entrega.redelivered)

        canal.basic_ack(entrega.delivery_tag)

        return

    log.info("Evento %s recibido: tipo=%s ventaId=%s vehiculoId=%s "
             "fechaUTC=%s redelivered=%s", evento_id, evento.get("tipo"),
             evento.get("ventaId"), evento.get("vehiculoId"),
             evento.get("fechaUTC"), entrega.redelivered)

    procesados.add(evento_id)

    canal.basic_ack(entrega.delivery_tag)


def main():

    conexion, canal = conectar()

    canal.basic_qos(prefetch_count=1)

    canal.basic_consume(RABBIT_COLA, recibir)

    log.info("Consumidor de prueba conectado a %s; esperando eventos en la "
             "cola %s (Ctrl+C para detener)", destino(), RABBIT_COLA)

    try:

        canal.start_consuming()

    except KeyboardInterrupt:

        log.info("Consumidor detenido; los mensajes nuevos quedarán Ready "
                 "en la cola %s", RABBIT_COLA)

    finally:

        try:
            if conexion.is_open:
                conexion.close()
        except AMQPError:
            pass


if __name__ == "__main__":
    main()
