# API de vehículos con seguridad JWT (Java Spring Boot)

API REST que administra el catálogo de **vehículos**, autentica a los usuarios y emite **JWT** (`POST /oauth/token`). También expone las rutas internas de **reservas** que la API de ventas FastAPI usa para reservar y confirmar un vehículo vendido. Cada alta, modificación o eliminación de vehículo se avisa al servicio de notificaciones.

## Requisitos

| Software | Versión |
|---|---|
| JDK | 17 o superior |
| Apache Maven | 3.9 o superior (o `mvnw.cmd`, que descarga Maven 3.9.16 la primera vez) |
| MySQL / XAMPP | Base `venta_vehiculos` en el puerto 3306 |
| Servicio de notificaciones | `notificaciones-service` en 8082 (opcional: si no responde, la operación continúa y solo se registra un aviso) |

Tecnologías: Spring Boot 4.1.1, Spring Security, Spring Data JPA, JJWT 0.12.6, MySQL Connector/J y Bean Validation.

## 1. Base de datos

La API usa `spring.jpa.hibernate.ddl-auto=validate`: **no crea tablas**, solo comprueba que existan. Antes del primer arranque, crea la base y las tablas:

```sql
CREATE DATABASE IF NOT EXISTS venta_vehiculos
  CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE venta_vehiculos;

CREATE TABLE vehiculos (
  id_vehiculo INT NOT NULL AUTO_INCREMENT,
  marca       VARCHAR(50)  NOT NULL,
  modelo      VARCHAR(50)  NOT NULL,
  placa       VARCHAR(10)  NOT NULL,
  chasis      VARCHAR(17)  NOT NULL,
  anio        INT          NOT NULL,
  color       VARCHAR(30)  NOT NULL,
  precio      DECIMAL(10,2) NOT NULL,
  estado      VARCHAR(20)  NOT NULL DEFAULT 'DISPONIBLE',
  PRIMARY KEY (id_vehiculo),
  UNIQUE KEY placa (placa),
  UNIQUE KEY chasis (chasis)
) ENGINE=InnoDB;

CREATE TABLE usuarios_api (
  id       INT NOT NULL AUTO_INCREMENT,
  username VARCHAR(50)  NOT NULL,
  password VARCHAR(255) NOT NULL,   -- hash BCrypt
  rol      VARCHAR(20)  NOT NULL,   -- CONSULTA | OPERADOR | ADMIN
  PRIMARY KEY (id),
  UNIQUE KEY username (username)
) ENGINE=InnoDB;

CREATE TABLE reservas (
  id           VARCHAR(36)   NOT NULL,
  operacion_id VARCHAR(64)   NOT NULL,
  vehiculo_id  INT           NOT NULL,
  precio       DECIMAL(12,2) NOT NULL,
  moneda       VARCHAR(3)    NOT NULL,
  estado       VARCHAR(20)   NOT NULL,
  PRIMARY KEY (id),
  UNIQUE KEY operacion_id (operacion_id),
  CONSTRAINT fk_reserva_vehiculo FOREIGN KEY (vehiculo_id) REFERENCES vehiculos (id_vehiculo),
  CONSTRAINT ck_reserva_estado CHECK (estado IN ('RESERVADA','CONFIRMADA','LIBERADA'))
) ENGINE=InnoDB;
```

Las tablas `compradores` y `ventas` de la misma base pertenecen a la API FastAPI y se crean con sus migraciones Alembic.

## 2. Configuración

El archivo `src/main/resources/application.properties` contiene secretos y **no se sube a Git**. Créalo con esta plantilla y reemplaza los valores:

```properties
spring.application.name=venta-vehiculos-api
server.port=8081
server.address=127.0.0.1

# Base de datos
spring.datasource.url=jdbc:mysql://localhost:3306/venta_vehiculos?useSSL=false&serverTimezone=UTC
spring.datasource.username=root
spring.datasource.password=
spring.jpa.hibernate.ddl-auto=validate
spring.jpa.open-in-view=false
spring.jpa.show-sql=true

# JWT: clave HMAC de al menos 32 caracteres
app.security.jwt-secret=REEMPLAZAR_POR_CLAVE_JWT_DE_32_O_MAS_CARACTERES
app.security.access-token-minutes=30
app.security.refresh-token-minutes=1200

# Credenciales del cliente (Basic Auth en /oauth/token)
app.security.client-id=vehiculos-client
app.security.client-secret=REEMPLAZAR_POR_SECRETO_DEL_CLIENTE

# Clave de integración con FastAPI (= VENTAS_SERVICE_KEY), mínimo 32 caracteres
app.security.ventas-key=REEMPLAZAR_POR_CLAVE_COMPARTIDA_CON_FASTAPI

# Servicio de notificaciones (= app.notificacion-key del servicio)
servicios.notificaciones.url=http://127.0.0.1:8082
servicios.notificaciones.key=REEMPLAZAR_POR_CLAVE_DE_NOTIFICACIONES
servicios.notificaciones.destinatario=prueba@uta.edu

# Usuario inicial (solo con el perfil "local")
app.bootstrap.user=admin
app.bootstrap.password=REEMPLAZAR_POR_CONTRASEÑA_INICIAL
app.bootstrap.role=ADMIN
```

- `app.security.ventas-key` debe tener al menos 32 caracteres, o la aplicación no arranca.
- **Usuarios:** si la tabla `usuarios_api` está vacía, arranca una vez con el perfil `local`. Así se crea el usuario `app.bootstrap.user` con su contraseña cifrada en BCrypt:

  ```powershell
  mvn spring-boot:run "-Dspring-boot.run.profiles=local"
  ```

- **Roles válidos:** `CONSULTA`, `OPERADOR` y `ADMIN`.

## 3. Compilación

```powershell
mvn clean package -DskipTests
```

El JAR queda en `target/venta-vehiculos-api-0.0.1-SNAPSHOT.jar`.

## 4. Ejecución

Instancia A, en el puerto configurado (8081):

```powershell
mvn spring-boot:run
# o bien
java -jar target/venta-vehiculos-api-0.0.1-SNAPSHOT.jar
```

Instancia B, para el balanceo con NGINX. Se cambia el puerto al arrancar, sin tocar el código:

```powershell
java -jar target/venta-vehiculos-api-0.0.1-SNAPSHOT.jar --server.port=8084
```

> No uses el 8082, que ocupa el servicio de notificaciones. Si el equipo tiene Oracle instalado, el 8080 puede estar ocupado por `TNSLSNR.EXE`.

## 5. Comprobación con Postman

**Obtener el token**

```http
POST http://127.0.0.1:8081/oauth/token
Authorization: Basic <client-id:client-secret en Base64>
Content-Type: application/x-www-form-urlencoded

grant_type=password&username=<usuario>&password=<contraseña>
```

En Postman: pestaña **Authorization → Basic Auth** con `client-id` y `client-secret`, y el body en **x-www-form-urlencoded**. La respuesta 200 incluye `access_token`, `token_type=bearer`, `refresh_token`, `expires_in` y `scope`.

**Consultar vehículos**

```http
GET http://127.0.0.1:8081/api/vehiculos
Authorization: Bearer <access_token>
```

## Endpoints

| Método | Ruta | Rol | HTTP principales |
|---|---|---|---|
| `POST` | `/oauth/token` | Público (Basic del cliente) | 200, 400, 401 |
| `GET` | `/api/vehiculos?marca=&estado=` | CONSULTA, OPERADOR, ADMIN | 200, 400, 401, 403 |
| `GET` | `/api/vehiculos/{id}` | CONSULTA, OPERADOR, ADMIN | 200, 401, 403, 404 |
| `POST` | `/api/vehiculos` | OPERADOR, ADMIN | 201, 401, 403, 409, 422 |
| `PUT` | `/api/vehiculos/{id}` | OPERADOR, ADMIN | 200, 404, 409, 422 |
| `PATCH` | `/api/vehiculos/{id}` (`application/merge-patch+json`, solo `precio`) | OPERADOR, ADMIN | 200, 404, 409, 422 |
| `DELETE` | `/api/vehiculos/{id}` | ADMIN | 204, 404, 409 |
| `GET` | `/api/identidad` | CONSULTA, OPERADOR, ADMIN | 200 (usada por FastAPI) |
| `POST` | `/api/vehiculos/{id}/reservas` | Interno (`X-Service-Key`) | 200, 401, 404, 409 |
| `GET` | `/api/reservas?operacionId=` · `/api/reservas/{id}` | Interno | 200, 401, 404 |
| `PUT` | `/api/reservas/{id}/confirmacion` | Interno | 200, 401, 404, 409 |
| `DELETE` | `/api/reservas/{id}` | Interno | 204, 401, 404, 409 |

Los errores se devuelven como `application/problem+json`, con los campos `codigo` y `traceId`. Cualquier otra ruta se rechaza.

Ejemplo de cuerpo para `POST /api/vehiculos`:

```json
{
  "marca": "Toyota",
  "modelo": "Corolla",
  "placa": "PBA-1234",
  "chasis": "JTDBR32E720123456",
  "anio": 2024,
  "color": "Blanco",
  "precio": 25000.00
}
```

## Estructura

```text
src/main/java/ec/edu/uta/venta_vehiculos_api/
  client/       NotificacionClient (aviso HTTP a notificaciones)
  controller/   AuthController, VehiculoController, ReservaController, IdentidadController
  dto/          Request/Response de vehículos, reservas, token y notificaciones
  exception/    ApiError y ApiExceptionHandler (problem+json)
  model/        Vehiculo, Reserva, UsuarioApi
  repository/   Repositorios JPA (bloqueo FOR UPDATE)
  security/     SecurityConfig, JwtService, JwtAuthenticationFilter, IntegrationFilter, LocalUsers
  service/      VehiculoService, ReservaService, UsuarioApiService
```
