# Cliente de escritorio JavaFX – Consulta de vehículos

Aplicación de escritorio en JavaFX que consulta el catálogo de la API Java de vehículos (`GET /api/vehiculos`) y lo muestra en una tabla. Para usarla, el JWT se pega en un campo de contraseña. La aplicación:

- Muestra el total de vehículos y el estado de la consulta.
- Indica el código HTTP cuando la API rechaza la petición (401, 403, etc.).
- Hace la llamada HTTP en segundo plano, sin bloquear la interfaz.

> La guía APE 04 prevé que este cliente muestre las notificaciones de venta. Esa parte todavía no está implementada.

## Requisitos

| Software | Versión |
|---|---|
| JDK | **23 o superior** (el `pom.xml` compila con `release 23`) |
| Apache Maven | 3.9 o superior |
| API Java de vehículos | En ejecución en `http://localhost:8081` |

Dependencias (las descarga Maven): JavaFX 23.0.2 (`javafx-controls`, `javafx-fxml`) y Jackson 2.18.3.

## 1. Configuración

El proyecto Maven está en la subcarpeta `consutarvehiculojavafx`:

```powershell
cd escritorioFX\consutarvehiculojavafx
```

La URL de la API está fija en `src/main/java/ec/edu/com/VehiculosApiClient.java`:

```java
private static final String URL_API = "http://localhost:8081/api/vehiculos";
```

Para pasar por NGINX, cámbiala a `http://localhost:8090/api/vehiculos`.

## 2. Compilación

```powershell
mvn clean compile
```

## 3. Ejecución

```powershell
mvn javafx:run
```

La clase principal es `ec.edu.com.VehiculosApp`, configurada en `javafx-maven-plugin`. También existe `ec.edu.com.Launcher` para ejecutarla desde el IDE.

## 4. Uso

1. Obtén un JWT con `POST http://127.0.0.1:8081/oauth/token` desde Postman. Sirve un usuario con rol CONSULTA, OPERADOR o ADMIN.
2. Pega el `access_token` en el campo **Ingrese el JWT**, sin escribir `Bearer`.
3. Pulsa **Consultar**: la tabla muestra ID, marca, modelo, placa, color, chasis, año, precio y estado.
4. Pulsa **Limpiar** para borrar el token y la tabla.

| Situación | Mensaje mostrado |
|---|---|
| Token vacío | "Ingrese el JWT para consultar." |
| Token inválido o vencido | "La API respondió con código HTTP 401…" |
| API apagada | "No se pudo consultar la API: …" |

## Estructura

```text
consutarvehiculojavafx/
  pom.xml
  src/main/java/
    module-info.java              Módulo ec.edu.com (javafx, jackson, java.net.http)
    ec/edu/com/
      VehiculosApp.java           Interfaz (tabla, campo JWT, botones)
      VehiculosApiClient.java     Cliente HTTP asíncrono (HttpClient + Jackson)
      Vehiculo.java               Record del vehículo
      Launcher.java               Punto de entrada alternativo
  src/main/resources/ec/edu/com/  primary.fxml, secondary.fxml (plantilla, no utilizadas)
```
