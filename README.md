# APE 04 – Arquitectura multicanal de venta de vehículos

Sistema de venta de vehículos con cuatro clientes (Windows Forms, PWA móvil, React y JavaFX) que consumen, a través de NGINX, tres servicios: la API Java de vehículos (dos instancias balanceadas), la API Python de compradores y ventas y el servicio Java de notificaciones. Cada venta confirmada se publica en RabbitMQ y llega a JavaFX como notificación.

Cada proyecto tiene su propio README con el detalle de su configuración. Este documento explica cómo encajan las piezas y cómo levantar todo el sistema.

---

## 1. Arquitectura

```
 Windows Forms      PWA móvil :5174     React :5173          JavaFX
 (administración)   (únicas ventas)     (consulta)           (notificaciones)
       │                  │                  │                   ▲
       └──────────────────┴────────┬─────────┴───────────────────┘
                                   ▼  REST + JWT
                       ┌───────────────────────┐
                       │      NGINX :8090      │  puerta de entrada única
                       └───────────────────────┘
          3:1 │          │              │                    │
              ▼          ▼              ▼                    ▼
      API Java A    API Java B    FastAPI ventas-api   notificaciones-service
        :8080         :8083           :8000                 :8082
              │          │       │      │ publica           ▲ consume
              │          │       │      ▼                   │
              │          │       │   RabbitMQ :5672 ────────┘
              │          │       │   ventas.eventos → notificaciones.ventas
              ▼          ▼       ▼                          ▼
        ┌──────────────────────────────────────────────────────────┐
        │ MySQL :3306 – base venta_vehiculos (tablas por servicio)  │
        │ API Java: vehiculos, reservas, usuarios_api               │
        │ FastAPI: compradores, ventas                              │
        │ notificaciones-service: notificaciones                    │
        └──────────────────────────────────────────────────────────┘
```

| Componente | Carpeta | Tecnología | Puerto | Responsabilidad |
|---|---|---|---|---|
| API de vehículos (A y B) | `venta-vehiculos-api_security` | Java 17+, Spring Boot, Spring Security | 8080 / 8083 | Emite y valida el JWT (`/oauth/token`), gestiona vehículos y reservas |
| API de ventas | `ventas-api` | Python 3.13, FastAPI, SQLAlchemy, Alembic, pika | 8000 | Compradores y ventas con `Idempotency-Key`; publica el evento de venta |
| Servicio de notificaciones | `notificaciones-service` | Java 17+, Spring Boot, Spring AMQP, JPA | 8082 | Consume RabbitMQ, guarda avisos sin duplicados y expone `GET /api/notificaciones` |
| NGINX | `nginx/nginx.conf` | NGINX para Windows | 8090 | Entrada REST única y balanceo 3:1 de la API Java |
| RabbitMQ | `ventas-api/rabbitmq` | Docker, `rabbitmq:management` | 5672 / 15672 | Cola durable `notificaciones.ventas` |
| MySQL | — | XAMPP (MySQL/MariaDB) | 3306 | Base `venta_vehiculos` |
| Windows Forms | `ConsultaVehiculosWinForms` | C#, Windows Forms | — | Crea vehículos y compradores; lista vehículos, compradores y ventas |
| PWA móvil | `AplicacionMovilPWA` | HTML/CSS/JS, Node.js | 5174 | Login y **único** cliente que registra ventas |
| React | `AplicacionWebREACT/react-fetch-vehiculos` | React + Vite | 5173 | Consulta y filtro de vehículos, consulta de ventas e indicadores |
| JavaFX | `escritorioFX/consutarvehiculojavafx` | JavaFX | — | Muestra las notificaciones de venta (sondeo cada 4 s) |

### Flujo de una venta

1. Windows Forms registra el vehículo (`POST /api/vehiculos`, API Java) y el comprador (`POST /api/compradores`, FastAPI).
2. La PWA inicia sesión con usuario y contraseña (`POST /oauth/token`) y registra la venta (`POST /api/ventas` con `Authorization: Bearer` e `Idempotency-Key`).
3. FastAPI valida el JWT con `GET /api/identidad`, reserva el vehículo en la API Java (`X-Service-Key`) y deja la venta `COMPLETADA`. El vehículo pasa a `VENDIDO`.
4. FastAPI publica el evento `VENTA_COMPLETADA` (mensaje persistente) en el exchange `ventas.eventos`. Si RabbitMQ no está disponible, la venta queda con notificación `PENDIENTE` y el worker la reenvía cada 10 s.
5. El listener de notificaciones-service consume la cola, guarda el aviso en la tabla `notificaciones` (un registro por `eventoId`) y confirma el mensaje.
6. JavaFX consulta `GET /api/notificaciones` por NGINX y muestra la venta nueva sin actualización manual. React y Windows Forms consultan el mismo resultado.

### Roles

| Rol | Permisos |
|---|---|
| `ADMIN` | Todo: registrar vehículos y compradores, vender y consultar |
| `OPERADOR` | Registrar vehículos y compradores, vender y consultar |
| `CONSULTA` | Solo consultar vehículos (403 al intentar vender) |

---

## 2. Requisitos

| Herramienta | Versión usada |
|---|---|
| JDK | 17 o superior (probado con Java 23) |
| Maven | 3.9 o superior |
| Python | 3.13 |
| Node.js y npm | 22 o superior |
| .NET SDK | 10 (el proyecto Windows Forms apunta a `net10.0-windows`) |
| XAMPP (MySQL) | MariaDB 10.4 o MySQL 8 |
| Docker Desktop | Para RabbitMQ |
| NGINX para Windows | Instalado en `C:\nginx` |

---

## 3. Configuración local

Los secretos **no** se suben a Git. Cada proyecto tiene una plantilla que se copia y se completa:

| Proyecto | Archivo local (excluido de Git) | Plantilla |
|---|---|---|
| API Java | `src/main/resources/application.properties` | Sección "2. Configuración" de su README |
| notificaciones-service | `src/main/resources/application.properties` | Sección "2. Configuración" de su README |
| ventas-api | `.env` | `.env.example` |
| Windows Forms | `WinFormsAdministrativo/appsettings.json` | `appsettings.example.json` |
| React | `.env.local` | `.env.example` |
| PWA | `.env` | `.env.example` |

Valores que deben coincidir entre proyectos:

| Valor | Dónde se define | Dónde se repite |
|---|---|---|
| Client-id y client-secret de OAuth | API Java: `app.security.client-id` y `app.security.client-secret` | `OAUTH_CLIENT_ID` / `OAUTH_CLIENT_SECRET` (React y PWA), `ClientId` / `ClientSecret` (Windows Forms) |
| Clave de servicio de ventas (32+ caracteres) | API Java: `app.security.ventas-key` | ventas-api: `VENTAS_SERVICE_KEY` |
| Clave de notificaciones (32+ caracteres) | API Java: `servicios.notificaciones.key` | notificaciones-service: `app.notificacion-key` |
| URL de entrada | — | `http://127.0.0.1:8090` (NGINX) en todos los clientes y en `JAVA_URL` de ventas-api |
| RabbitMQ | ventas-api: `RABBITMQ_URL`, exchange, cola y routing key | notificaciones-service: `spring.rabbitmq.*` y `app.rabbitmq.*` |

---

## 4. Preparación (solo la primera vez)

### 4.1 Base de datos

Inicia MySQL desde el panel de XAMPP y crea las tablas en este orden:

1. **API Java** (`vehiculos`, `usuarios_api`, `reservas`): ejecuta el SQL de la sección "1. Base de datos" del README de `venta-vehiculos-api_security`. También crea la base `venta_vehiculos`.
2. **FastAPI** (`compradores`, `ventas`), con migraciones de Alembic:
   ```powershell
   cd ventas-api
   python -m venv .venv
   .\.venv\Scripts\activate
   pip install -r requirements.txt
   python -m alembic upgrade head
   ```
3. **Notificaciones** (`notificaciones`):
   ```powershell
   Get-Content notificaciones-service\db\notificaciones.sql | C:\xampp\mysql\bin\mysql.exe -u <usuario> -p venta_vehiculos
   ```

### 4.2 Usuario inicial

Si la tabla `usuarios_api` está vacía, arranca una vez la API Java con el perfil `local`. Así se crea el usuario `app.bootstrap.user` con su contraseña cifrada:

```powershell
cd venta-vehiculos-api_security
mvn spring-boot:run "-Dspring-boot.run.profiles=local"
```

### 4.3 RabbitMQ

```powershell
docker compose -f ventas-api\rabbitmq\docker-compose.yml up -d
cd ventas-api; .\.venv\Scripts\python.exe -m app.messaging.topologia
```

El segundo comando declara el exchange `ventas.eventos`, la cola durable `notificaciones.ventas` y su binding. El panel queda en `http://127.0.0.1:15672`.

### 4.4 NGINX

Copia la configuración del repositorio y comprueba la sintaxis:

```powershell
Copy-Item nginx\nginx.conf C:\nginx\conf\nginx.conf
cd C:\nginx; .\nginx.exe -t
```

### 4.5 Dependencias de los clientes

```powershell
cd AplicacionWebREACT\react-fetch-vehiculos; npm install
```

La PWA no tiene dependencias externas. Maven y `dotnet` descargan las suyas al compilar.

---

## 5. Arranque del sistema

Ejecuta cada paso en su propia terminal y en este orden; cada servicio necesita que los anteriores estén disponibles.

| Paso | Componente | Comando |
|---|---|---|
| 1 | MySQL | Iniciar desde el panel de XAMPP |
| 2 | RabbitMQ | `docker start rabbitmq-taller` |
| 3 | API Java A (8080) | ver abajo |
| 4 | API Java B (8083) | ver abajo |
| 5 | NGINX (8090) | `cd C:\nginx; start nginx` |
| 6 | FastAPI (8000) | `cd ventas-api; .\.venv\Scripts\activate; python -m uvicorn app.main:app --port 8000` |
| 7 | Worker de eventos pendientes | `cd ventas-api; .\.venv\Scripts\activate; python -m app.worker` |
| 8 | notificaciones-service (8082) | `cd notificaciones-service; mvn spring-boot:run` |
| 9 | Windows Forms | `cd ConsultaVehiculosWinForms\WinFormsAdministrativo; dotnet run` |
| 10 | React (5173) | `cd AplicacionWebREACT\react-fetch-vehiculos; npm run dev` |
| 11 | PWA (5174) | `cd AplicacionMovilPWA; npm run dev` |
| 12 | JavaFX | `cd escritorioFX\consutarvehiculojavafx; mvn clean javafx:run` |

Las dos instancias Java se ejecutan desde el mismo JAR:

```powershell
cd venta-vehiculos-api_security
mvn clean package -DskipTests
java -jar target\venta-vehiculos-api-0.0.1-SNAPSHOT.jar --server.port=8080
java -jar target\venta-vehiculos-api-0.0.1-SNAPSHOT.jar --server.port=8083
```

Direcciones de los clientes web: React en `http://localhost:5173` y PWA en `http://localhost:5174`.

---

## 6. Verificación rápida

```powershell
curl.exe http://127.0.0.1:8090/nginx-ping
curl.exe http://127.0.0.1:8090/ventas/health
curl.exe -i http://127.0.0.1:8090/api/ventas
curl.exe "http://127.0.0.1:8090/api/notificaciones?limite=5"
docker exec rabbitmq-taller rabbitmqctl list_queues name messages_ready consumers
```

| Comando | Resultado esperado |
|---|---|
| `/nginx-ping` | `pong` (no toca los servicios) |
| `/ventas/health` | `{"status":"UP"}` |
| `/api/ventas` sin token | `401 NO_AUTENTICADO` |
| `/api/notificaciones` | `{"total": n, "notificaciones": [...]}` |
| `list_queues` | `notificaciones.ventas` con `consumers` = 1 |

Para comprobar el balanceo, los registros de NGINX (`C:\nginx\logs\access.log`) indican en cada línea la instancia que atendió la petición (`backend=127.0.0.1:8080` o `:8083`).

---

## 7. Rutas expuestas por NGINX (8090)

| Ruta | Servicio | Notas |
|---|---|---|
| `POST /oauth/token` | API Java (A/B, 3:1) | Login; Basic con el client-id/secret y usuario/contraseña |
| `/api/vehiculos`, `/api/identidad`, `/api/reservas` | API Java (A/B, 3:1) | Si una instancia cae, NGINX reintenta en la otra |
| `/api/compradores`, `/api/ventas` | FastAPI (8000) | Las ventas nunca se reintentan en otro backend |
| `GET /ventas/health` | FastAPI (8000) | Salud del servicio |
| `/api/notificaciones` | notificaciones-service (8082) | Consulta de JavaFX |
| `GET /nginx-ping` | NGINX | Salud de NGINX |
| Cualquier otra | — | 404 `Ruta no expuesta por NGINX` |

---

## 8. Problemas frecuentes

| Síntoma | Causa probable |
|---|---|
| HTTP 502 en los clientes | El servicio de destino está detenido; revisa su terminal y `C:\nginx\logs\error.log` |
| HTTP 401 al iniciar sesión | Usuario o contraseña incorrectos, o client-secret distinto al de la API Java |
| HTTP 403 al vender | El usuario tiene rol `CONSULTA`; vender requiere `OPERADOR` o `ADMIN` |
| La API Java o el servicio de notificaciones no arrancan | Falta una tabla (`ddl-auto=validate`) o una clave tiene menos de 32 caracteres |
| La venta queda con notificación `PENDIENTE` | RabbitMQ estaba detenido; con el worker en marcha se publica al volver el broker |
| JavaFX muestra el indicador rojo | notificaciones-service o NGINX no responden; los datos anteriores se conservan |
| El puerto 8080 está ocupado | Otro programa (por ejemplo Oracle `TNSLSNR.EXE`) lo usa; libéralo o cambia el puerto en `nginx.conf` |

---

## 9. Qué no se sube al repositorio

El `.gitignore` excluye `target`, `bin`, `obj`, `node_modules`, `.venv`, `dist`, cachés, archivos `.env`, `.env.local`, `application.properties` y `appsettings.json`. En su lugar se publican las plantillas indicadas en la sección 3.
