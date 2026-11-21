package ec.edu.com;

import com.fasterxml.jackson.annotation.JsonIgnoreProperties;
import com.fasterxml.jackson.databind.ObjectMapper;

import java.io.IOException;
import java.net.URI;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.time.Duration;
import java.util.List;
import java.util.concurrent.CompletableFuture;

/**
 * Cliente HTTP del servicio de notificaciones (a través de NGINX).
 * JavaFX no accede a RabbitMQ ni a MySQL: solo consulta GET /api/notificaciones.
 *
 * URL configurable (en este orden):
 *   1. Propiedad del sistema  -Dnotificaciones.url=http://127.0.0.1:8090
 *   2. Variable de entorno    NOTIFICACIONES_API_URL
 *   3. Valor por defecto      http://127.0.0.1:8090 (NGINX)
 */
public final class NotificacionesApiClient {

    public static final String URL_POR_DEFECTO = "http://127.0.0.1:8090";

    private static final int LIMITE = 50;

    private final HttpClient http = HttpClient.newBuilder()
            .connectTimeout(Duration.ofSeconds(3))
            .build();
    private final ObjectMapper mapper = new ObjectMapper();
    private final String urlBase;

    public NotificacionesApiClient() {
        String url = System.getProperty("notificaciones.url");
        if (url == null || url.isBlank()) {
            url = System.getenv("NOTIFICACIONES_API_URL");
        }
        if (url == null || url.isBlank()) {
            url = URL_POR_DEFECTO;
        }
        this.urlBase = url.trim().replaceAll("/+$", "");
    }

    public String getUrlBase() {
        return urlBase;
    }

    /** Resultado de una consulta: OK (lista, posiblemente vacía) o ERROR (servicio no disponible o respuesta no válida). */
    public record Resultado(boolean ok, long total, List<Notificacion> notificaciones, String error) {

        static Resultado exito(long total, List<Notificacion> lista) {
            return new Resultado(true, total, lista, null);
        }

        static Resultado fallo(String error) {
            return new Resultado(false, 0, List.of(), error);
        }
    }

    @JsonIgnoreProperties(ignoreUnknown = true)
    private record Listado(long total, List<Notificacion> notificaciones) {
    }

    /** GET /api/notificaciones?limite=50. Nunca lanza: los errores llegan como Resultado.fallo. */
    public CompletableFuture<Resultado> listar() {
        HttpRequest solicitud = HttpRequest.newBuilder(URI.create(urlBase + "/api/notificaciones?limite=" + LIMITE))
                .timeout(Duration.ofSeconds(4))
                .header("Accept", "application/json")
                .GET()
                .build();

        return http.sendAsync(solicitud, HttpResponse.BodyHandlers.ofString())
                .thenApply(this::interpretar)
                .exceptionally(error -> {
                    Throwable causa = error.getCause() != null ? error.getCause() : error;
                    String detalle = causa.getMessage() == null ? causa.getClass().getSimpleName() : causa.getMessage();
                    if (causa instanceof java.net.ConnectException) {
                        detalle = "no se pudo conectar con " + urlBase;
                    } else if (causa instanceof java.net.http.HttpTimeoutException) {
                        detalle = "sin respuesta en 4 segundos";
                    }
                    return Resultado.fallo(detalle);
                });
    }

    private Resultado interpretar(HttpResponse<String> respuesta) {
        int codigo = respuesta.statusCode();
        if (codigo != 200) {
            String motivo = switch (codigo) {
                case 502, 504 -> "NGINX no pudo comunicarse con el servicio de notificaciones";
                case 503 -> "la base de datos de notificaciones no está disponible";
                case 404 -> "ruta no encontrada; verifique la URL";
                default -> "respuesta inesperada";
            };
            return Resultado.fallo("HTTP " + codigo + ": " + motivo);
        }
        try {
            Listado listado = mapper.readValue(respuesta.body(), Listado.class);
            List<Notificacion> lista = listado.notificaciones() == null ? List.of() : listado.notificaciones();
            return Resultado.exito(listado.total(), lista);
        } catch (IOException ex) {
            return Resultado.fallo("respuesta no válida del servicio");
        }
    }
}
