# Cliente de escritorio JavaFX – Notificaciones de venta

Aplicación de escritorio que muestra **en tiempo casi real** las notificaciones de las ventas completadas. Corresponde al cliente JavaFX del diagrama de arquitectura:

```
FastAPI ──► RabbitMQ ──► notificaciones-service ──► tabla notificaciones (MySQL)
                                   ▲
JavaFX ── GET /api/notificaciones ─┘  (a través de NGINX :8090)
```

JavaFX **no accede a RabbitMQ ni a MySQL**: solo consulta `GET /api/notificaciones` del servicio de notificaciones a través de NGINX.

La aplicación:

- Muestra una tabla con las notificaciones: N°, venta, vehículo, precio, fecha de venta, fecha de recepción y mensaje.
- Consulta el servicio automáticamente cada **4 segundos** (sondeo limitado de 3 a 5 s). Nunca envía dos consultas a la vez.
- Muestra el **estado de conexión** con un indicador de color:
  - Verde: conectado al servicio.
  - Rojo: servicio no disponible, con el motivo. Reintenta en el siguiente ciclo.
  - Naranja: actualización automática en pausa.
- Diferencia una **lista vacía** ("Todavía no hay notificaciones de venta…") de un **fallo del servicio** ("No se pudo cargar el historial… no es una lista vacía").
- Si el servicio se cae después de haber cargado datos, conserva la tabla e indica la hora de los últimos datos.
- Cuando llega una venta nueva, la resalta en verde y muestra el aviso "Nueva venta: …", sin que el usuario pulse ningún botón.
- Tiene los botones **Actualizar ahora** y **Pausar / Reanudar**.

## Requisitos

| Software | Versión |
|---|---|
| JDK | **23 o superior** (el `pom.xml` compila con `release 23`) |
| Apache Maven | 3.9 o superior |
| NGINX | En `http://127.0.0.1:8090`, con la ruta `/api/notificaciones` hacia el puerto 8082 |
| notificaciones-service | En ejecución en el puerto 8082 |

Dependencias (las descarga Maven): JavaFX 23.0.2 (`javafx-controls`, `javafx-fxml`) y Jackson 2.18.3.

## 1. Configuración

El proyecto Maven está en la subcarpeta `consutarvehiculojavafx`. No necesita credenciales: `GET /api/notificaciones` es una consulta de solo lectura.

| Parámetro | Cómo se cambia | Por defecto |
|---|---|---|
| URL del servicio | `-Dnotificaciones.url=...` o la variable de entorno `NOTIFICACIONES_API_URL` | `http://127.0.0.1:8090` (NGINX) |
| Intervalo de sondeo | `-Dnotificaciones.intervalo=3`, `4` o `5` (segundos) | `4` |

## 2. Compilación

```powershell
cd escritorioFX\consutarvehiculojavafx
mvn clean compile
```

## 3. Ejecución

```powershell
mvn javafx:run
```

Para usar otra URL desde PowerShell:

```powershell
$env:NOTIFICACIONES_API_URL = "http://127.0.0.1:8082"; mvn javafx:run
```

La clase principal es `ec.edu.com.NotificacionesApp`, configurada en `javafx-maven-plugin`. También existe `ec.edu.com.Launcher` para ejecutarla desde el IDE.

## 4. Comprobación

1. Inicia MySQL, RabbitMQ, NGINX, notificaciones-service y la aplicación JavaFX.
2. Con historial, la tabla muestra las notificaciones y el indicador está en verde. Si la tabla está vacía, aparece "Todavía no hay notificaciones de venta…".
3. Registra una venta desde la PWA. En 4 segundos como máximo aparece la fila nueva resaltada y el aviso "Nueva venta: Venta N completada…".
4. Detén notificaciones-service. El indicador pasa a rojo con "HTTP 502: NGINX no pudo comunicarse con el servicio de notificaciones" y la tabla conserva los últimos datos.
5. Vuelve a iniciar el servicio. En el siguiente ciclo el indicador vuelve a verde sin reiniciar JavaFX.

| Situación | Lo que muestra |
|---|---|
| Servicio con avisos | Indicador verde, total y tabla |
| Servicio sin avisos (200 con lista vacía) | Indicador verde y "Todavía no hay notificaciones de venta…" |
| notificaciones-service apagado (NGINX responde 502) | Indicador rojo: "HTTP 502: NGINX no pudo comunicarse…" |
| NGINX apagado | Indicador rojo: "no se pudo conectar con http://127.0.0.1:8090" |
| MySQL caído (503) | Indicador rojo: "la base de datos de notificaciones no está disponible" |

## Estructura

```text
consutarvehiculojavafx/
  pom.xml
  src/main/java/
    module-info.java                Módulo ec.edu.com (javafx, jackson, java.net.http)
    ec/edu/com/
      NotificacionesApp.java        Interfaz: tabla, estado de conexión, sondeo y aviso de ventas nuevas
      NotificacionesApiClient.java  Cliente HTTP asíncrono de GET /api/notificaciones (URL configurable)
      Notificacion.java             Record de la notificación
      Launcher.java                 Punto de entrada alternativo
  src/main/resources/ec/edu/com/    primary.fxml, secondary.fxml (plantilla, no utilizadas)
```

> La versión anterior consultaba el catálogo de vehículos con un JWT pegado a mano. Siguiendo la guía APE 04 y el diagrama de arquitectura, se reemplazó por el visor de notificaciones. El catálogo se consulta desde React, la PWA y Windows Forms.
