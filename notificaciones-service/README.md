# Servicio de notificaciones (Java Spring Boot)

Servicio REST que recibe avisos de los otros servicios de la arquitectura y los muestra en su consola:

- **API Java de vehículos:** avisos de tipo `REGISTRO`, `ACTUALIZACION` y `ELIMINACION` de vehículos.
- **API de ventas FastAPI:** avisos de tipo `VENTA` cuando una venta queda completada.

Todas las peticiones deben incluir la cabecera `X-Notification-Key` con la clave compartida entre los servicios.

> En esta versión las notificaciones llegan por HTTP REST y no se guardan en una base de datos. El consumo desde RabbitMQ y la consulta `GET /api/notificaciones` para JavaFX todavía no están implementados.

## Requisitos

| Software | Versión |
|---|---|
| JDK | 17 o superior |
| Apache Maven | 3.9 o superior |
| Postman | Para las pruebas |

Tecnología: Spring Boot 3.5.6 (`spring-boot-starter-web`).

## 1. Configuración

El archivo `src/main/resources/application.properties` contiene la clave compartida y **no se sube a Git**. Créalo con esta plantilla:

```properties
spring.application.name=notificaciones-service
server.port=8082
server.address=127.0.0.1

# Debe ser idéntica a NOTIFICACIONES_KEY (FastAPI) y a
# servicios.notificaciones.key (API Java). Mínimo 32 caracteres.
app.notificacion-key=REEMPLAZAR_POR_CLAVE_COMPARTIDA_DE_32_O_MAS_CARACTERES
```

- **Puerto:** el proyecto usa **8082** porque la API Java de vehículos ocupa el 8081. Si lo cambias, actualiza también `servicios.notificaciones.url` en la API Java y `NOTIFICACIONES_URL` en FastAPI.
- **Clave:** si `app.notificacion-key` tiene menos de 32 caracteres, el servicio no arranca.

## 2. Compilación

```powershell
mvn clean package -DskipTests
```

El JAR queda en `target/notificaciones-service-0.0.1-SNAPSHOT.jar`.

## 3. Ejecución

Puedes arrancarlo con Maven:

```powershell
mvn spring-boot:run
```

O con el JAR ya compilado:

```powershell
java -jar target/notificaciones-service-0.0.1-SNAPSHOT.jar
```

El servicio queda en `http://127.0.0.1:8082`. Deja la terminal abierta para ver los avisos y detenlo con `Ctrl + C`.

## 4. Comprobación con Postman

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
  controller/   NotificacionController (POST /api/notificaciones)
  dto/          NotificacionRequest, NotificacionResponse
  exception/    ApiExceptionHandler (errores 400)
  security/     NotificacionKeyFilter (X-Notification-Key)
  service/      NotificacionService (validación y registro en consola)
```
