from typing import Annotated, Literal

from fastapi import APIRouter, Depends, Header, Query, Response
from sqlalchemy import select, or_

from app.db import SessionLocal
from app.models import Venta, Comprador
from app.repositories.venta_repository import VentaRepository
from app.schemas import (
    ActualizarComprador,
    CrearVenta,
    CrearComprador,
    ResumenVentas,
    ComprobanteVenta,
    VentasPaginadas,
)
from app.security import identidad, vendedor, propietario
from app.errors import ApiError
from app.services import venta_service as service

router = APIRouter(prefix="/api")


@router.post("/compradores", status_code=201)
def crear_comprador(d: CrearComprador, u=Depends(identidad)):
    vendedor(u)

    with SessionLocal.begin() as db:
        c = Comprador(usuario=u["usuario"], **d.model_dump())
        db.add(c)
        db.flush()
        return {"id": c.id, "nombre": c.nombre, "correo": c.correo}


@router.get("/compradores")
def listar_compradores(
    buscar: str | None = Query(default=None, max_length=100),
    u=Depends(identidad),
):
    """
    F01: Listar exclusivamente los compradores del usuario autenticado.
    Si no tiene registros asociados, retorna una lista vacía [].
    """
    vendedor(u)

    with SessionLocal() as db:
        texto = buscar.strip() if buscar is not None else ""
        stmt = (
            select(Comprador)
            .where(Comprador.usuario == u["usuario"])
        )

        if texto:
            stmt = stmt.where(
                or_(
                    Comprador.nombre.contains(texto, autoescape=True),
                    Comprador.documento.contains(texto, autoescape=True),
                )
            )

        stmt = stmt.order_by(Comprador.id.desc())
        compradores = db.scalars(stmt).all()

        return [
            {"id": c.id, "nombre": c.nombre, "correo": c.correo}
            for c in compradores
        ]


@router.get("/compradores/{id}")
def obtener_comprador(id: int, u=Depends(identidad)):
    """
    F02: Consultar un comprador por ID.
    Valida existencia (404), verifica propiedad o rol ADMIN (403),
    y devuelve el DTO público.
    """
    with SessionLocal() as db:
        c = db.get(Comprador, id)

        if c is None:
            raise ApiError(404, "COMPRADOR_NO_EXISTE", "Comprador inexistente")

        propietario(u, c.usuario)

        return {"id": c.id, "nombre": c.nombre, "correo": c.correo}


@router.put("/compradores/{id}")
def actualizar_comprador(
    id: int,
    datos: ActualizarComprador,
    u=Depends(identidad),
):
    with SessionLocal.begin() as db:
        c = db.get(Comprador, id)

        if c is None:
            raise ApiError(404, "COMPRADOR_NO_EXISTE", "Comprador inexistente")

        propietario(u, c.usuario)

        c.nombre = datos.nombre
        c.correo = datos.correo
        db.flush()

        return {"id": c.id, "nombre": c.nombre, "correo": c.correo}


@router.post("/ventas")
def crear(
    d: CrearVenta,
    response: Response,
    idempotency_key: Annotated[
        str,
        Header(min_length=8, max_length=100, pattern=r"^[A-Za-z0-9_-]+$"),
    ],
    u=Depends(identidad),
):
    vendedor(u)

    resultado, codigo = service.crear(d, idempotency_key, u)
    response.status_code = codigo
    response.headers["Location"] = f"/api/ventas/{resultado['id']}"
    return resultado


@router.get("/ventas", response_model=VentasPaginadas)
def listar_ventas(
    limit: Annotated[int, Query(ge=1, le=50)] = 10,
    offset: Annotated[int, Query(ge=0)] = 0,
    estado: Literal["PENDIENTE", "EN_REVISION", "COMPLETADA", "FALLIDA"] | None = Query(
        default=None
    ),
    u=Depends(identidad),
):
    with SessionLocal() as db:
        ventas, total = VentaRepository(db).pagina_por_usuario(
            u["usuario"], limit, offset, estado
        )
        return {
            "items": [service.salida(venta) for venta in ventas],
            "total": total,
            "limit": limit,
            "offset": offset,
        }


@router.get("/ventas/resumen", response_model=ResumenVentas)
def resumen_ventas(u=Depends(identidad)):
    vendedor(u)

    with SessionLocal() as db:
        resumen = VentaRepository(db).resumen_por_usuario(u["usuario"])
        return {
            "completadas": resumen.completadas,
            "ingresos": resumen.ingresos,
            "pendientes": resumen.pendientes,
            "enRevision": resumen.enRevision,
            "fallidas": resumen.fallidas,
        }


@router.get("/ventas/{id}/comprobante", response_model=ComprobanteVenta)
def obtener_comprobante(id: int, u=Depends(identidad)):
    with SessionLocal() as db:
        v = db.get(Venta, id)

        if v is None:
            raise ApiError(404, "VENTA_NO_EXISTE", "Venta inexistente")

        propietario(u, v.usuario)

        return service.comprobante(v)


@router.get("/ventas/{id}")
def obtener(id: int, u=Depends(identidad)):
    with SessionLocal() as db:
        v = db.get(Venta, id)

        if v is None:
            raise ApiError(404, "VENTA_NO_EXISTE", "Venta inexistente")

        propietario(u, v.usuario)

        return service.salida(v)
