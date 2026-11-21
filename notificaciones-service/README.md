# Servicio de notificaciones (Java Spring Boot)

Servicio de notificaciones de la arquitectura. Recibe avisos por dos vías:

- **RabbitMQ (ventas):** un listener real consume la cola durable `notificaciones.ventas`, donde ventas-api (FastAPI) publica el evento `VENTA_COMPLETADA` de cada venta confirmada. Cada aviso se guarda **una sola vez por `eventoId`** en la tabla `notificaciones`.
- **HTTP REST (vehículos):** la API Java de vehículos envía avisos `REGISTRO`, `ACTUALIZACION` y `ELIMINACION` a `POST /api/notificaciones`, que se muestran en la consola. Esas peticiones deben incluir la cabecera `X-Notification-Key`.

> La consulta `GET /api/notificaciones` para JavaFX se agrega en la siguiente parte de la Actividad 9.

## Requisitos

| Software | Versión |
|---|---|
| JDK | 17 o superior |
| Apache Maven | 3.9 o superior |
| MySQL / XAMPP | Base `venta_vehiculos` en el puerto 3306 |
| RabbitMQ | Contenedor `rabbitmq-taller` (5672 y panel 15672) |
| Postman | Para las pruebas |

Tecnología: Spring Boot 3.5.6 (`spring-boot-starter-web`, `spring-boot-starter-amqp`, `spring-boot-starter-data-jpa` y `mysql-connector-j`).

## 1. Base de datos

El servicio usa la base existente `venta_vehiculos`; solo agrega la tabla `notificaciones`. Créala una vez ejecutando el script en phpMyAdmin (pestaña SQL) o en la consola de MySQL:

```powershell
mysql -u root venta_vehiculos < db/notificaciones.sql
```

| Columna | Tipo | Uso |
|---|---|---|
| `id` | BIGINT auto | Clave primaria |
| `evento_id` | VARCHAR(36) **UNIQUE** | Evita registrar dos veces el mismo evento |
| `tipo` | VARCHAR(40) | `VENTA_COMPLETADA` |
| `venta_id`, `vehiculo_id` | INT | Venta de FastAPI y vehículo vendido |
| `precio_acordado`, `moneda` | DECIMAL(12,2), CHAR(3) | Precio confirmado |
| `fecha_venta` | DATETIME | `fechaUTC` del evento (UTC) |
| `mensaje` | VARCHAR(255) | Texto del aviso, p. ej. `Venta 29 completada: vehículo 30, 20000.00 USD` |
| `recibida_en` | DATETIME | Cuándo la registró el listener (UTC) |

Hibernate no crea ni modifica la tabla (`ddl-auto=validate`): solo comprueba al arrancar que coincide con la entidad.

## 2. Configuración

El archivo `src/main/resources/application.properties` contiene claves y credenciales y **no se sube a Git**. Créalo con esta plantilla y reemplaza los valores `REEMPLAZAR_...`:

```properties
spring.application.name=notificaciones-service
server.port=8082
server.address=127.0.0.1

# Debe ser idéntica a servicios.notificaciones.key (API Java de vehículos). Mínimo 32 caracteres.
app.notificacion-key=REEMPLAZAR_POR_CLAVE_COMPARTIDA_DE_32_O_MAS_CARACTERES

# Base de datos: tabla notificaciones dentro de venta_vehiculos (db/notificaciones.sql)
spring.datasource.url=jdbc:mysql://127.0.0.1:3306/venta_vehiculos?useSSL=false&serverTimezone=UTC&preserveInstants=false&allowPublicKeyRetrieval=true
spring.datasource.username=REEMPLAZAR_USUARIO_MYSQL
spring.datasource.password=
spring.jpa.hibernate.ddl-auto=validate
spring.jpa.open-in-view=false

# RabbitMQ (guest solo en desarrollo local)
spring.rabbitmq.host=127.0.0.1
spring.rabbitmq.port=5672
spring.rabbitmq.username=guest
spring.rabbitmq.password=guest
spring.rabbitmq.virtual-host=/

# Mismos nombres que publica ventas-api (FastAPI)
app.rabbitmq.exchange=ventas.eventos
app.rabbitmq.cola=notificaciones.ventas
app.rabbitmq.routing-key=venta.completada
app.rabbitmq.cola-invalidos=notificaciones.ventas.invalidos
app.rabbitmq.espera-reintento-segundos=5
```

- **Puerto:** **8082**, porque la API Java de vehículos ocupa el 8081. Si lo cambias, actualiza también `servicios.notificaciones.url` en la API Java.
- **`app.notificacion-key`:** idéntica a `servicios.notificaciones.key` de la API Java; mínimo 32 caracteres o el servicio no arranca.
- **Base de datos:** `spring.datasource.*` apunta a `venta_vehiculos`. `preserveInstants=false` hace que las fechas UTC se guarden sin conversión de zona horaria.
- **RabbitMQ:** `spring.rabbitmq.*` y `app.rabbitmq.*` deben usar los mismos nombres que ventas-api: exchange `ventas.eventos`, cola `notificaciones.ventas` y routing key `venta.completada`.

## 3. Compilación

```powershell
mvn clean package -DskipTests
```

El JAR queda en `target/notificaciones-service-0.0.1-SNAPSHOT.jar`.

## 4. Ejecución

Puedes arrancarlo con Maven:

```powershell
mvn spring-boot:run
```

O con el JAR ya compilado:

```powershell
java -jar target/notificaciones-service-0.0.1-SNAPSHOT.jar
```

El servicio queda en `http://127.0.0.1:8082` y el listener se conecta a RabbitMQ. Deja la terminal abierta para ver los avisos y detenlo con `Ctrl + C`.

## 5. Listener de RabbitMQ (Actividad 9)

`EventoVentaListener` consume `notificaciones.ventas` con **confirmación manual** y `prefetch = 1`:

| Caso | Qué hace | Confirmación |
|---|---|---|
| Evento nuevo | Lo guarda en `notificaciones`; log `Notificación registrada: eventoId=… ventaId=…` | `basicAck` después de guardar |
| `eventoId` ya registrado | No lo inserta otra vez; log `Evento … duplicado` | `basicAck` |
| JSON inválido o sin `eventoId`, `ventaId` o `vehiculoId` | Lo aparta en la cola `notificaciones.ventas.invalidos` con el motivo en la cabecera `x-motivo-rechazo` | `basicAck` (no bloquea la cola) |
| MySQL no disponible | No lo guarda; espera 5 s | `basicNack` con requeue: vuelve a la cola y no se pierde |

El duplicado se controla dos veces: una consulta previa por `eventoId` y la restricción `UNIQUE` de la tabla, que impide el segundo registro si llegan dos entregas al mismo tiempo.

Si el servicio está detenido, los eventos quedan en la cola durable (**Ready** en el panel `http://127.0.0.1:15672`) y se registran al arrancarlo, sin repetir la venta.

> Con este listener en marcha no ejecutes `python -m app.messaging.consumidor_prueba` de ventas-api: los dos consumidores competirían por los mismos mensajes.

Consulta de los avisos registrados:

```sql
SELECT id, evento_id, venta_id, vehiculo_id, precio_acordado, moneda, fecha_venta, mensaje, recibida_en
FROM notificaciones ORDER BY id DESC;
```

## 6. Comprobación con Postman (avisos de vehículos)

```http
POST http://127.0.0.1:8082/api/notificaciones
Content-Type: application/json
X-Notification-Key: <clave configurada>
```

```json
{
  "vehiculoId": 5,
  "destinatario": "usuario@correo.com",
  "tipo": "VENTA",
  "mensaje": "Venta 10 operación 1b2c... completada"
}
```

Respuesta esperada **201 Created**:

```json
{
  "success": true,
  "estado": "ENVIADA",
  "mensaje": "Notificación procesada correctamente"
}
```

En la consola del servicio aparece el bloque `NOTIFICACIÓN RECIBIDA` con el vehículo, el tipo y el mensaje.

## Validaciones y códigos HTTP

| Caso | HTTP |
|---|---|
| Notificación válida | 201 |
| Falta `X-Notification-Key` o la clave es incorrecta | 401 `Clave no valida` |
| `vehiculoId` ausente o menor o igual a 0 | 400 |
| `destinatario` vacío | 400 |
| `mensaje` vacío | 400 |
| `tipo` distinto de `REGISTRO`, `ACTUALIZACION`, `ELIMINACION` o `VENTA` | 400 |

## Estructura

```text
src/main/java/ec/edu/uta/notificaciones/
  config/       RabbitConfig (exchange, cola durable, binding y confirmación manual)
  controller/   NotificacionController (POST /api/notificaciones)
  dto/          NotificacionRequest, NotificacionResponse, EventoVenta (mensaje RabbitMQ)
  exception/    ApiExceptionHandler (errores 400)
  messaging/    EventoVentaListener (@RabbitListener), EventoInvalidoException
  model/        Notificacion (entidad de la tabla notificaciones)
  repository/   NotificacionRepository (existsByEventoId)
  security/     NotificacionKeyFilter (X-Notification-Key)
  service/      NotificacionService (avisos REST), NotificacionVentaService (registro sin duplicados)
db/notificaciones.sql   Script de la tabla notificaciones
```
