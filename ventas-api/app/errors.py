import logging

from uuid import uuid4

from fastapi.responses import JSONResponse
from fastapi.exceptions import RequestValidationError

from starlette.exceptions import HTTPException

from sqlalchemy.exc import SQLAlchemyError, IntegrityError


class ApiError(Exception):

    def __init__(self, status, codigo, detalle):

        self.status, self.codigo, self.detalle = status, codigo, detalle


def respuesta(req, status, codigo, detalle):

    trace = str(uuid4())

    return JSONResponse(status_code=status,

                        media_type="application/problem+json", content={

                            "type": "urn:ventas:" + codigo.lower(),

                            "title": codigo, "status": status, "detail": detalle,

                            "instance": req.url.path, "codigo": codigo, "traceId": trace,
                        })


def mensaje_validacion(error):

    tipo = error.get("type")
    contexto = error.get("ctx") or {}

    if tipo == "less_than_equal":

        return f"Debe ser menor o igual que {contexto.get('le')}"

    if tipo == "greater_than_equal":

        return f"Debe ser mayor o igual que {contexto.get('ge')}"

    if tipo == "less_than":

        return f"Debe ser menor que {contexto.get('lt')}"

    if tipo == "greater_than":

        return f"Debe ser mayor que {contexto.get('gt')}"

    mensajes = {

        "int_parsing": "Debe ser un número entero",

        "int_type": "Debe ser un número entero",

        "float_parsing": "Debe ser un número",

        "missing": "Este campo es obligatorio",

        "string_too_short": "El texto es demasiado corto",

        "string_too_long": "El texto es demasiado largo",

        "string_pattern_mismatch": "El formato no es válido",

        "extra_forbidden": "Este campo no está permitido",

        "literal_error": "El valor no está entre las opciones permitidas",

    }

    return mensajes.get(tipo, "El valor no es válido")


def instalar(app):

    @app.exception_handler(ApiError)
    async def negocio(req, ex):

        return respuesta(req, ex.status, ex.codigo, ex.detalle)

    @app.exception_handler(RequestValidationError)
    async def validar(req, ex):

        return respuesta(req, 422, "VALIDACION", "; ".join(

            ".".join(map(str, e["loc"])) + ": " + mensaje_validacion(e)

            for e in ex.errors()))

    @app.exception_handler(HTTPException)
    async def http(req, ex):

        return respuesta(req, ex.status_code, "ERROR_HTTP", str(ex.detail))

    @app.exception_handler(IntegrityError)
    async def integridad(req, ex):

        return respuesta(req, 409, "CONFLICTO_DATOS", "Registro duplicado")

    @app.exception_handler(SQLAlchemyError)
    async def bd(req, ex):

        logging.error("Fallo de persistencia", exc_info=(
            type(ex), ex, ex.__traceback__))

        return respuesta(req, 503, "BD_NO_DISPONIBLE",

                         "No se pudo acceder a la persistencia")

    @app.exception_handler(Exception)
    async def inesperado(req, ex):

        logging.error("Fallo inesperado", exc_info=(
            type(ex), ex, ex.__traceback__))

        return respuesta(req, 500, "ERROR_INTERNO", "Error interno registrado")
