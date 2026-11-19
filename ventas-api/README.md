# API de ventas de vehículos (FastAPI)

API REST desarrollada con FastAPI para administrar **compradores** y **ventas** de vehículos. Usa MySQL para la persistencia. Se integra con la API Java de vehículos, que valida la identidad del usuario y reserva o confirma el vehículo, y con el servicio de notificaciones, al que avisa cada venta completada.

## Requisitos

| Software | Versión | Uso |
|---|---|---|
| Python | 3.10 o superior (probado con 3.13) | Ejecución de la API |
| MySQL / XAMPP | 8.x / MariaDB 10.x | Base `venta_vehiculos` en el puerto 3306 |
| API Java de vehículos | `venta-vehiculos-api_security` en ejecución | Validar el JWT (`/api/identidad`) y reservar vehículos |
| Servicio de notificaciones | `notificaciones-service` en ejecución | Recibir el aviso de venta (opcional para vender) |

Dependencias declaradas en `requirements.txt`:

- **FastAPI** y **Uvicorn**: rutas HTTP y servidor ASGI.
- **SQLAlchemy** y **PyMySQL**: acceso a MySQL.
- **Alembic**: migraciones de la base de datos.
- **Pydantic**: validación de datos.
- **HTTPX**: llamadas a la API Java y al servicio de notificaciones.
- **python-dotenv**: carga de variables desde `.env`.

## 1. Instalación (Windows, PowerShell)

Desde la carpeta `ventas-api`, crea y activa un entorno virtual:

```powershell
py -m venv .venv
.\.venv\Scripts\Activate.ps1
```

Instala las dependencias:

```powershell
python -m pip install --upgrade pip
pip install -r requirements.txt
```

> La carpeta `.venv` no se sube al repositorio. Cada equipo la crea con estos comandos.

## 2. Configuración

Crea un archivo `.env` en la **raíz del proyecto**, junto a `requirements.txt`. La aplicación no lee el `.env` que esté dentro de `.venv`. El archivo `.env` contiene secretos y está excluido de Git.

Plantilla con los puertos reales del proyecto:

```dotenv
# Base de datos
DB_HOST=127.0.0.1
DB_PORT=3306
DB_NAME=venta_vehiculos
DB_USER=root
DB_PASSWORD=

# API Java de vehículos (o NGINX: http://127.0.0.1:8090)
JAVA_URL=http://127.0.0.1:8081
VENTAS_SERVICE_KEY=REEMPLAZAR_POR_app.security.ventas-key_DE_JAVA

# Servicio de notificaciones
NOTIFICACIONES_URL=http://127.0.0.1:8082
NOTIFICACIONES_KEY=REEMPLAZAR_POR_app.notificacion-key_DE_NOTIFICACIONES
```

- `VENTAS_SERVICE_KEY` debe ser **idéntica** a `app.security.ventas-key` de la API Java.
- `NOTIFICACIONES_KEY` debe ser **idéntica** a `app.notificacion-key` del servicio de notificaciones y a `servicios.notificaciones.key` de la API Java.
- Ambas claves deben tener al menos 32 caracteres y no pueden empezar por `COLOCA_`.
- Si una URL se escribe sin `http://`, la aplicación lo agrega al arrancar.

Para generar una clave aleatoria:

```powershell
python -c "import secrets; print(secrets.token_urlsafe(32))"
```

## 3. Base de datos y migraciones

Crea la base indicada en `DB_NAME`, si todavía no existe:

```sql
CREATE DATABASE venta_vehiculos CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
```

Con MySQL en ejecución y el `.env` configurado, crea las tablas. Alembic toma la conexión del `.env`:

```powershell
python -m alembic upgrade head
```

La migración `0001` crea únicamente las tablas `compradores` y `ventas`. Las tablas `vehiculos`, `usuarios_api` y `reservas` pertenecen a la API Java, y Alembic no las toca.

## 4. Ejecución

Orden recomendado de arranque:

1. MySQL.
2. API Java de vehículos (8081).
3. Servicio de notificaciones (8082).
4. Esta API.

```powershell
python -m uvicorn app.main:app --reload
```

La API queda en `http://127.0.0.1:8000`, el puerto por defecto de uvicorn. Para usar el puerto de referencia de la guía (8002):

```powershell
python -m uvicorn app.main:app --reload --port 8002
```

### Worker de recuperación

En otra terminal, con el mismo entorno virtual activo, inicia el worker. Reintenta las ventas `PENDIENTE` o `EN_REVISION` y las notificaciones que no se pudieron enviar:

```powershell
.\.venv\Scripts\Activate.ps1
python -m app.worker           # revisa cada 10 segundos
python -m app.worker --once    # ejecuta un solo ciclo y termina
```

## 5. Comprobación

- `GET http://127.0.0.1:8000/health` debe responder `{"status":"UP"}`.
- `http://127.0.0.1:8000/docs` abre Swagger UI.
- `http://127.0.0.1:8000/openapi.json` muestra el contrato OpenAPI.
- `GET /api/compradores` sin token debe responder `401 NO_AUTENTICADO`.

Para probar con Postman, obtén primero el JWT con `POST http://127.0.0.1:8081/oauth/token` (ver el README de la API Java) y envíalo como `Authorization: Bearer <token>`.

## Endpoints

| Método | Ruta | Rol | Descripción |
|---|---|---|---|
| `GET` | `/health` | Público | Estado de la API. |
| `POST` | `/api/compradores` | OPERADOR, ADMIN | Crear un comprador (201). |
| `GET` | `/api/compradores` | OPERADOR, ADMIN | Listar compradores propios; admite `?buscar=texto`. |
| `GET` | `/api/compradores/{id}` | Dueño o ADMIN | Consultar un comprador. |
| `PUT` | `/api/compradores/{id}` | Dueño o ADMIN | Actualizar nombre y correo; el documento no cambia. |
| `POST` | `/api/ventas` | OPERADOR, ADMIN | Crear una venta. Requiere `Idempotency-Key`. Responde 201, 200 (reintento) o 202 (en revisión). |
| `GET` | `/api/ventas` | Autenticado | Listar ventas propias con `limit`, `offset` y `estado`. |
| `GET` | `/api/ventas/resumen` | OPERADOR, ADMIN | Resumen de ventas propias. |
| `GET` | `/api/ventas/{id}` | Dueño o ADMIN | Consultar una venta. |
| `GET` | `/api/ventas/{id}/comprobante` | Dueño o ADMIN | Comprobante de una venta completada. |

### Reglas importantes

- **Autenticación:** las rutas protegidas exigen `Authorization: Bearer <token>`. FastAPI reenvía el token a `GET /api/identidad` de la API Java, que lo valida y devuelve el usuario y sus roles.
- **Idempotency-Key:** de 8 a 100 caracteres (letras, números, `-` o `_`).
  - La misma clave con el mismo cuerpo devuelve la venta existente, sin duplicarla.
  - La misma clave con otro cuerpo responde `409 CLAVE_REUTILIZADA`.
- **Datos de la venta:** `vehiculoId` debe existir y estar `DISPONIBLE` en Java, y `compradorId` debe pertenecer al usuario autenticado. El precio lo fija la reserva en Java, no el cliente.
- **Notificación:** al quedar `COMPLETADA`, la venta se notifica con `POST /api/notificaciones` (tipo `VENTA`). Si no se puede enviar, queda `notificacion=PENDIENTE`, se registra un aviso en la consola y el worker la reintenta.

## Estructura del proyecto

```text
app/
  clients/        Cliente HTTP hacia la API Java (reservas)
  repositories/   Consultas a la base de datos
  routers/        Endpoints FastAPI
  services/       Lógica de ventas y notificaciones
  config.py       Carga y validación del .env
  db.py           Conexión y sesiones SQLAlchemy
  errors.py       Respuestas de error application/problem+json
  models.py       Modelos Comprador y Venta
  schemas.py      Esquemas Pydantic de entrada y salida
  security.py     Validación de identidad, rol y propiedad
  worker.py       Reintentos de ventas y notificaciones pendientes
  main.py         Aplicación FastAPI y /health
alembic/          Migraciones de esquema
alembic.ini       Configuración de Alembic
requirements.txt  Dependencias Python
```
