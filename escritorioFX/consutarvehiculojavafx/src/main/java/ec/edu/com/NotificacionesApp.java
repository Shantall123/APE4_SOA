package ec.edu.com;

import javafx.animation.KeyFrame;
import javafx.animation.PauseTransition;
import javafx.animation.Timeline;
import javafx.application.Application;
import javafx.application.Platform;
import javafx.beans.property.ReadOnlyObjectWrapper;
import javafx.geometry.Insets;
import javafx.geometry.Pos;
import javafx.scene.Scene;
import javafx.scene.control.Button;
import javafx.scene.control.Label;
import javafx.scene.control.TableColumn;
import javafx.scene.control.TableRow;
import javafx.scene.control.TableView;
import javafx.scene.layout.HBox;
import javafx.scene.layout.Priority;
import javafx.scene.layout.Region;
import javafx.scene.layout.VBox;
import javafx.scene.paint.Color;
import javafx.scene.shape.Circle;
import javafx.stage.Stage;
import javafx.util.Duration;

import java.math.BigDecimal;
import java.text.DecimalFormat;
import java.text.DecimalFormatSymbols;
import java.time.Instant;
import java.time.LocalTime;
import java.time.ZoneId;
import java.time.format.DateTimeFormatter;
import java.util.HashSet;
import java.util.List;
import java.util.Locale;
import java.util.Set;
import java.util.function.Function;

/**
 * Visor de notificaciones de venta (Actividad 9).
 *
 * - Consulta GET /api/notificaciones del servicio de notificaciones a través de NGINX.
 * - Sondeo automático limitado: una consulta cada 3 a 5 s (por defecto 4 s), nunca dos a la vez.
 * - Estado de conexión visible y diferencia entre "sin notificaciones" (200 con lista vacía)
 *   y "servicio no disponible" (error de conexión o HTTP distinto de 200).
 * - Las ventas nuevas se resaltan y se anuncian sin que el usuario pulse ningún botón.
 */
public final class NotificacionesApp extends Application {

    private static final DateTimeFormatter FECHA = DateTimeFormatter.ofPattern("dd/MM/yyyy HH:mm:ss");
    private static final DateTimeFormatter HORA = DateTimeFormatter.ofPattern("HH:mm:ss");
    private static final DecimalFormat DINERO =
            new DecimalFormat("#,##0.00", DecimalFormatSymbols.getInstance(Locale.US));

    private final NotificacionesApiClient api = new NotificacionesApiClient();
    private final int intervaloSegundos = intervalo();

    private final TableView<Notificacion> tabla = new TableView<>();
    private final Circle indicador = new Circle(7, Color.GRAY);
    private final Label lblEstado = new Label("Conectando con el servicio de notificaciones…");
    private final Label lblTotal = new Label();
    private final Label lblNueva = new Label();
    private final Label lblActualizacion = new Label("Última actualización: —");
    private final Label placeholder = new Label();
    private final Button btnActualizar = new Button("Actualizar ahora");
    private final Button btnPausar = new Button("Pausar");

    private final Set<Long> conocidas = new HashSet<>();
    private final Set<Long> resaltadas = new HashSet<>();
    private Timeline sondeo;
    private boolean primeraCarga = true;
    private boolean consultando = false;
    private boolean pausado = false;
    private LocalTime ultimaCargaCorrecta;

    @Override
    public void start(Stage ventana) {
        Label titulo = new Label("Notificaciones de venta");
        titulo.setStyle("-fx-font-size: 24px; -fx-font-weight: bold;");
        Label subtitulo = new Label("Servicio: " + api.getUrlBase() + "/api/notificaciones · actualización automática cada "
                + intervaloSegundos + " s");
        subtitulo.setStyle("-fx-text-fill: #555555;");

        lblEstado.setStyle("-fx-font-weight: bold;");
        lblTotal.setStyle("-fx-text-fill: #333333;");
        Region espacio = new Region();
        HBox.setHgrow(espacio, Priority.ALWAYS);
        btnActualizar.setOnAction(e -> consultar());
        btnPausar.setOnAction(e -> alternarPausa());
        HBox barraEstado = new HBox(10, indicador, lblEstado, espacio, lblTotal, btnActualizar, btnPausar);
        barraEstado.setAlignment(Pos.CENTER_LEFT);
        barraEstado.setPadding(new Insets(10, 12, 10, 12));
        barraEstado.setStyle("-fx-background-color: white; -fx-background-radius: 6; -fx-border-color: #d0d0d0; -fx-border-radius: 6;");

        lblNueva.setVisible(false);
        lblNueva.setManaged(false);
        lblNueva.setMaxWidth(Double.MAX_VALUE);
        lblNueva.setPadding(new Insets(8, 12, 8, 12));
        lblNueva.setStyle("-fx-background-color: #d4f7dc; -fx-text-fill: #14532d; -fx-font-weight: bold; -fx-background-radius: 6;");

        configurarTabla();
        VBox.setVgrow(tabla, Priority.ALWAYS);
        lblActualizacion.setStyle("-fx-text-fill: #555555;");

        VBox contenido = new VBox(10, titulo, subtitulo, barraEstado, lblNueva, tabla, lblActualizacion);
        contenido.setPadding(new Insets(20, 28, 14, 28));
        contenido.setStyle("-fx-background-color: #f1f1f1;");

        ventana.setTitle("Notificaciones de venta");
        ventana.setMinWidth(900);
        ventana.setMinHeight(500);
        ventana.setScene(new Scene(contenido, 1150, 620));
        ventana.show();

        sondeo = new Timeline(new KeyFrame(Duration.seconds(intervaloSegundos), e -> consultar()));
        sondeo.setCycleCount(Timeline.INDEFINITE);
        consultar();
        sondeo.play();
    }

    @Override
    public void stop() {
        if (sondeo != null) {
            sondeo.stop();
        }
    }

    private void configurarTabla() {
        agregarColumna("N°", 55, Notificacion::id);
        agregarColumna("Venta", 70, Notificacion::ventaId);
        agregarColumna("Vehículo", 80, Notificacion::vehiculoId);
        agregarColumna("Precio", 120, n -> precio(n.precioAcordado(), n.moneda()));
        agregarColumna("Fecha de venta", 150, n -> fechaLocal(n.fechaVentaUTC()));
        agregarColumna("Recibida", 150, n -> fechaLocal(n.recibidaEnUTC()));
        agregarColumna("Mensaje", 360, Notificacion::mensaje);
        tabla.setColumnResizePolicy(TableView.CONSTRAINED_RESIZE_POLICY_FLEX_LAST_COLUMN);
        placeholder.setWrapText(true);
        placeholder.setText("Cargando el historial de notificaciones…");
        tabla.setPlaceholder(placeholder);
        tabla.setRowFactory(t -> new TableRow<>() {
            @Override
            protected void updateItem(Notificacion n, boolean vacia) {
                super.updateItem(n, vacia);
                setStyle(!vacia && n != null && resaltadas.contains(n.id())
                        ? "-fx-background-color: #d4f7dc;" : "");
            }
        });
    }

    private <T> void agregarColumna(String titulo, double ancho, Function<Notificacion, T> valor) {
        TableColumn<Notificacion, T> columna = new TableColumn<>(titulo);
        columna.setPrefWidth(ancho);
        columna.setCellValueFactory(fila -> new ReadOnlyObjectWrapper<>(valor.apply(fila.getValue())));
        tabla.getColumns().add(columna);
    }

    /** Una consulta a la vez: si la anterior no terminó, se omite este ciclo. */
    private void consultar() {
        if (consultando) {
            return;
        }
        consultando = true;
        btnActualizar.setDisable(true);
        api.listar().whenComplete((resultado, error) -> Platform.runLater(() -> {
            consultando = false;
            btnActualizar.setDisable(false);
            if (error != null) {
                mostrarFallo(error.getMessage());
            } else if (resultado.ok()) {
                mostrarDatos(resultado.total(), resultado.notificaciones());
            } else {
                mostrarFallo(resultado.error());
            }
        }));
    }

    private void mostrarDatos(long total, List<Notificacion> lista) {
        ultimaCargaCorrecta = LocalTime.now();
        estado(pausado ? Color.ORANGE : Color.web("#16a34a"),
                pausado ? "Conectado · actualización automática en pausa" : "Conectado al servicio de notificaciones");
        lblTotal.setText(total + " notificación(es) registrada(s)");
        lblActualizacion.setText("Última actualización: " + HORA.format(ultimaCargaCorrecta));

        if (!primeraCarga) {
            List<Notificacion> nuevas = lista.stream().filter(n -> !conocidas.contains(n.id())).toList();
            if (!nuevas.isEmpty()) {
                anunciar(nuevas);
            }
        }
        lista.forEach(n -> conocidas.add(n.id()));
        primeraCarga = false;

        tabla.getItems().setAll(lista);
        placeholder.setText("Todavía no hay notificaciones de venta. Cuando se complete una venta aparecerá aquí "
                + "automáticamente.");
    }

    private void mostrarFallo(String detalle) {
        estado(Color.web("#dc2626"), "Servicio de notificaciones no disponible (" + detalle + "). Reintentando cada "
                + intervaloSegundos + " s");
        if (tabla.getItems().isEmpty()) {
            lblTotal.setText("");
            placeholder.setText("No se pudo cargar el historial: el servicio de notificaciones no responde. "
                    + "Esto es un fallo de conexión, no una lista vacía.");
        } else {
            lblActualizacion.setText("Sin conexión: se muestran los datos de las " + HORA.format(ultimaCargaCorrecta)
                    + " hasta que el servicio vuelva a responder.");
        }
    }

    private void anunciar(List<Notificacion> nuevas) {
        nuevas.forEach(n -> resaltadas.add(n.id()));
        Notificacion ultima = nuevas.get(0);
        lblNueva.setText(nuevas.size() == 1
                ? "Nueva venta: " + ultima.mensaje()
                : nuevas.size() + " ventas nuevas. La más reciente: " + ultima.mensaje());
        lblNueva.setVisible(true);
        lblNueva.setManaged(true);

        PauseTransition quitar = new PauseTransition(Duration.seconds(12));
        quitar.setOnFinished(e -> {
            nuevas.forEach(n -> resaltadas.remove(n.id()));
            if (resaltadas.isEmpty()) {
                lblNueva.setVisible(false);
                lblNueva.setManaged(false);
            }
            tabla.refresh();
        });
        quitar.play();
    }

    private void alternarPausa() {
        pausado = !pausado;
        if (pausado) {
            sondeo.pause();
            btnPausar.setText("Reanudar");
            estado(Color.ORANGE, "Actualización automática en pausa");
        } else {
            sondeo.play();
            btnPausar.setText("Pausar");
            consultar();
        }
    }

    private void estado(Color color, String texto) {
        indicador.setFill(color);
        lblEstado.setText(texto);
    }

    private static String precio(BigDecimal valor, String moneda) {
        if (valor == null) {
            return "—";
        }
        return DINERO.format(valor) + (moneda == null ? "" : " " + moneda);
    }

    private static String fechaLocal(String utc) {
        if (utc == null || utc.isBlank()) {
            return "—";
        }
        try {
            return FECHA.format(Instant.parse(utc).atZone(ZoneId.systemDefault()));
        } catch (RuntimeException ex) {
            return utc;
        }
    }

    /** Intervalo de sondeo: -Dnotificaciones.intervalo=3..5 (segundos); por defecto 4. */
    private static int intervalo() {
        try {
            int valor = Integer.parseInt(System.getProperty("notificaciones.intervalo", "4"));
            return Math.max(3, Math.min(5, valor));
        } catch (NumberFormatException ex) {
            return 4;
        }
    }
}
