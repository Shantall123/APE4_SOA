"""Conexión y topología de RabbitMQ (exchange, cola durable y binding).

La topología se declara de forma idempotente cada vez que se abre una
conexión: si ya existe con los mismos parámetros, RabbitMQ no cambia nada.
Ejecutar `python -m app.messaging.topologia` la crea sin publicar mensajes.
"""
import logging
import warnings

import pika

# pika 1.5 marca BlockingConnection como obsoleta (se retira en 2.0); aquí se
# usa desde un único hilo, que es su uso soportado.
warnings.filterwarnings("ignore", category=DeprecationWarning,
                        message="BlockingConnection is deprecated")

from app.config import (RABBIT_URL, RABBIT_EXCHANGE, RABBIT_COLA,
                        RABBIT_ROUTING_KEY)

log = logging.getLogger("ventas.eventos")

if not log.handlers:
    _salida = logging.StreamHandler()
    _salida.setFormatter(logging.Formatter(
        "%(asctime)s %(levelname)s [eventos] %(message)s"))
    log.addHandler(_salida)
    log.setLevel(logging.INFO)
    log.propagate = False


def parametros():

    p = pika.URLParameters(RABBIT_URL)

    p.heartbeat = 30
    p.blocked_connection_timeout = 10
    p.socket_timeout = 3       # no retener la respuesta de la venta si el broker no responde
    p.stack_timeout = 5
    p.connection_attempts = 1

    return p


def declarar(canal):

    canal.exchange_declare(RABBIT_EXCHANGE, exchange_type="direct",
                           durable=True)

    # Cola durable: sobrevive al reinicio del broker; los mensajes
    # persistentes (delivery_mode=2) se guardan en disco.
    canal.queue_declare(RABBIT_COLA, durable=True)

    canal.queue_bind(RABBIT_COLA, RABBIT_EXCHANGE, RABBIT_ROUTING_KEY)


def conectar():

    conexion = pika.BlockingConnection(parametros())

    canal = conexion.channel()

    declarar(canal)

    return conexion, canal


def destino():
    """Descripción sin credenciales para las trazas."""
    p = parametros()
    return f"{p.host}:{p.port} vhost={p.virtual_host}"


if __name__ == "__main__":

    con, _ = conectar()

    log.info("Topología lista en %s: exchange=%s cola=%s routing_key=%s",
             destino(), RABBIT_EXCHANGE, RABBIT_COLA, RABBIT_ROUTING_KEY)

    con.close()
