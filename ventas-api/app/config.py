
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

    # httpx exige el esquema; sin "http://" la llamada falla y la
    # notificación de venta quedaba siempre PENDIENTE sin aviso.
    valor = requerido(nombre).strip().rstrip("/")

    if not valor.lower().startswith(("http://", "https://")):

        valor = "http://" + valor

    return valor


JAVA_URL = url_servicio("JAVA_URL")

SERVICE_KEY = requerido("VENTAS_SERVICE_KEY")

NOTIF_URL = url_servicio("NOTIFICACIONES_URL")

NOTIF_KEY = requerido("NOTIFICACIONES_KEY")

if min(len(SERVICE_KEY), len(NOTIF_KEY)) < 32:

    raise RuntimeError("Claves de servicio de al menos 32 caracteres")

if SERVICE_KEY.startswith("COLOCA_") or NOTIF_KEY.startswith("COLOCA_"):

    raise RuntimeError("Reemplaza las claves de ejemplo en el archivo .env")
