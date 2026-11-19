from decimal import Decimal

from pydantic import BaseModel, ConfigDict, Field


class CrearComprador(BaseModel):

    model_config = ConfigDict(extra="forbid", str_strip_whitespace=True)

    documento: str = Field(min_length=3, max_length=30)

    nombre: str = Field(min_length=2, max_length=120)

    correo: str = Field(min_length=5, max_length=200,

                        pattern=r"^[^@\s]+@[^@\s]+\.[^@\s]+$")


class ActualizarComprador(BaseModel):

    model_config = ConfigDict(extra="forbid", str_strip_whitespace=True)

    nombre: str = Field(min_length=2, max_length=120)

    correo: str = Field(min_length=5, max_length=200,

                        pattern=r"^[^@\s]+@[^@\s]+\.[^@\s]+$")


class CrearVenta(BaseModel):

    model_config = ConfigDict(extra="forbid")

    vehiculoId: int = Field(gt=0, strict=True)

    compradorId: int = Field(gt=0, strict=True)


class VentaSalida(BaseModel):

    id: int

    operacionId: str

    vehiculoId: int

    compradorId: int

    precioAcordado: str | None

    moneda: str | None

    estado: str

    notificacion: str

    fechaUTC: str


class VentasPaginadas(BaseModel):

    items: list[VentaSalida]

    total: int

    limit: int

    offset: int


class ResumenVentas(BaseModel):

    completadas: int

    ingresos: Decimal

    pendientes: int

    enRevision: int

    fallidas: int


class CompradorComprobante(BaseModel):

    id: int

    documento: str

    nombre: str


class ComprobanteVenta(BaseModel):

    id: int

    comprador: CompradorComprobante

    vehiculoId: int

    precioConfirmado: str

    moneda: str

    fechaUTC: str
