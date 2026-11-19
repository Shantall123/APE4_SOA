from decimal import Decimal

from sqlalchemy import select, or_, func, case, literal, Numeric

from app.models import Venta


class VentaRepository:

    def __init__(self, db):

        self.db = db

    def por_clave(self, usuario, clave):

        return self.db.scalar(select(Venta).where(

            Venta.usuario == usuario, Venta.idempotency_key == clave))

    def bloquear(self, id):

        return self.db.scalar(select(Venta).where(Venta.id == id)

                              .with_for_update())

    def pagina_por_usuario(self, usuario, limit, offset, estado=None):

        filtros = [Venta.usuario == usuario]

        if estado is not None:

            filtros.append(Venta.estado == estado)

        total = self.db.scalar(

            select(func.count()).select_from(Venta).where(*filtros)

        )

        items = self.db.scalars(

            select(Venta).where(*filtros)

            .order_by(Venta.fecha.desc(), Venta.id.desc())

            .limit(limit).offset(offset)

        ).all()

        return items, total

    def resumen_por_usuario(self, usuario):

        ingresos_completadas = case(

            (Venta.estado == "COMPLETADA", Venta.precio_acordado),

            else_=None,

        )

        consulta = select(

            func.count(case((Venta.estado == "COMPLETADA", 1))).label(
                "completadas"
            ),

            func.coalesce(
                func.sum(ingresos_completadas),
                literal(Decimal("0.00"), type_=Numeric(12, 2)),
            ).label("ingresos"),

            func.count(case((Venta.estado == "PENDIENTE", 1))).label(
                "pendientes"
            ),

            func.count(case((Venta.estado == "EN_REVISION", 1))).label(
                "enRevision"
            ),

            func.count(case((Venta.estado == "FALLIDA", 1))).label(
                "fallidas"
            ),

        ).where(Venta.usuario == usuario)

        return self.db.execute(consulta).one()

    def pendientes(self):

        return self.db.scalars(select(Venta.id).where(or_(

            Venta.estado.in_(["PENDIENTE", "EN_REVISION"]),

            (Venta.estado == "COMPLETADA") &

            (Venta.notificacion != "ENVIADA")))

            .order_by(Venta.id).limit(100)).all()
