
import os

from pathlib import Path

from dotenv import load_dotenv

load_dotenv(dotenv_path=Path(__file__).resolve().parent.parent / ".env")


def requerido(nombre):

    valor = os.environ.get(nombre, "")

    if not valor:

        raise RuntimeError(f"Falta configurar {nombre}")

    return valor


def url_servicio(nombre):

    # httpx exige el esquema; sin "http://" la llamada falla.
    valor = requerido(nombre).strip().rstrip("/")

    if not valor.lower().startswith(("http://", "https://")):

        valor = "http://" + valor

    return valor


JAVA_URL = url_servicio("JAVA_URL")

SERVICE_KEY = requerido("VENTAS_SERVICE_KEY")

if len(SERVICE_KEY) < 32:

    raise RuntimeError("Claves de servicio de al menos 32 caracteres")

if SERVICE_KEY.startswith("COLOCA_"):

    raise RuntimeError("Reemplaza las claves de ejemplo en el archivo .env")

# RabbitMQ (Actividad 8): la venta confirmada se notifica publicando un
# evento persistente en una cola durable; el servicio de notificaciones lo
# consume (Actividad 9). Ya no hay POST HTTP directo a notificaciones.
RABBIT_URL = requerido("RABBITMQ_URL")

RABBIT_EXCHANGE = os.environ.get("RABBITMQ_EXCHANGE", "ventas.eventos")

RABBIT_COLA = os.environ.get("RABBITMQ_COLA", "notificaciones.ventas")

RABBIT_ROUTING_KEY = os.environ.get("RABBITMQ_ROUTING_KEY", "venta.completada")
