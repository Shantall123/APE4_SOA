package ec.edu.com;

import java.util.concurrent.CompletionException;

import javafx.application.Application;
import javafx.application.Platform;
import javafx.beans.property.ReadOnlyObjectWrapper;
import javafx.geometry.Insets;
import javafx.scene.Scene;
import javafx.scene.control.Button;
import javafx.scene.control.Label;
import javafx.scene.control.PasswordField;
import javafx.scene.control.TableColumn;
import javafx.scene.control.TableView;
import javafx.scene.layout.HBox;
import javafx.scene.layout.Priority;
import javafx.scene.layout.VBox;
import javafx.stage.Stage;

public final class VehiculosApp extends Application {

    private final VehiculosApiClient api = new VehiculosApiClient();
    private final PasswordField txtToken = new PasswordField();
    private final Button btnConsultar = new Button("Consultar");
    private final Button btnLimpiar = new Button("Limpiar");
    private final Label lblEstado = new Label("Listo para consultar");
    private final Label lblTotal = new Label("Total de vehículos: 0");
    private final TableView<Vehiculo> tabla = new TableView<>();

    @Override
    public void start(Stage ventana) {
        Label titulo = new Label("Vehículos registrados");
        titulo.setStyle("-fx-font-size: 24px;");

        Label etiquetaToken = new Label("Ingrese el JWT");
        etiquetaToken.setStyle("-fx-font-size: 20px;");
        txtToken.setPromptText("Pegue aquí el token JWT");
        HBox.setHgrow(txtToken, Priority.ALWAYS);
        btnConsultar.setOnAction(event -> consultar());
        btnLimpiar.setOnAction(event -> {
            txtToken.clear();
            tabla.getItems().clear();
            lblTotal.setText("Total de vehículos: 0");
            lblEstado.setText("Listo para consultar");
        });
        HBox filaToken = new HBox(8, txtToken, btnConsultar, btnLimpiar);


        configurarTabla();
        VBox.setVgrow(tabla, Priority.ALWAYS);

        VBox contenido = new VBox(10, titulo, etiquetaToken, filaToken, lblEstado, tabla, lblTotal);
        contenido.setPadding(new Insets(24, 36, 12, 36));
        contenido.setStyle("-fx-background-color: #f1f1f1;");

        ventana.setTitle("Consulta de vehículos");
        ventana.setMinWidth(800);
        ventana.setMinHeight(500);
        ventana.setScene(new Scene(contenido, 1100, 590));
        ventana.show();
    }

    private void configurarTabla() {
        agregarColumna("ID", 65, Vehiculo::idVehiculo);
        agregarColumna("Marca", 110, Vehiculo::marca);
        agregarColumna("Modelo", 120, Vehiculo::modelo);
        agregarColumna("Placa", 100, Vehiculo::placa);
        agregarColumna("Color", 100, Vehiculo::color);
        agregarColumna("Chasis", 170, Vehiculo::chasis);
        agregarColumna("Año", 80, Vehiculo::anio);
        agregarColumna("Precio", 100, Vehiculo::precio);
        agregarColumna("Estado", 100, Vehiculo::estado);
        tabla.setPlaceholder(new Label("Los vehículos consultados aparecerán aquí"));
        tabla.setColumnResizePolicy(TableView.CONSTRAINED_RESIZE_POLICY);
    }

    private <T> void agregarColumna(String titulo, double ancho,
                                    java.util.function.Function<Vehiculo, T> valor) {
        TableColumn<Vehiculo, T> columna = new TableColumn<>(titulo);
        columna.setPrefWidth(ancho);
        columna.setCellValueFactory(fila -> new ReadOnlyObjectWrapper<>(valor.apply(fila.getValue())));
        tabla.getColumns().add(columna);
    }

    private void consultar() {
        String jwt = txtToken.getText().trim();
        if (jwt.isEmpty()) {
            lblEstado.setText("Ingrese el JWT para consultar.");
            txtToken.requestFocus();
             tabla.getItems().clear();
            return;
        }



        btnConsultar.setDisable(true);
        lblEstado.setText("Consultando vehículos...");
        api.consultarVehiculos(jwt).whenComplete((resultado, error) -> Platform.runLater(() -> {
            btnConsultar.setDisable(false);
            if (error != null) {
                Throwable causa = error;
                while (causa instanceof CompletionException && causa.getCause() != null) {
                    causa = causa.getCause();
                }
                lblEstado.setText("No se pudo consultar la API: " + causa.getMessage());
                return;
            }

            if (resultado.codigoHttp() == 200) {
                tabla.getItems().setAll(resultado.vehiculos());
                lblTotal.setText("Total de vehículos: " + resultado.vehiculos().size());
                lblEstado.setText("Consulta completada.");
            } else {
                tabla.getItems().clear();
                lblTotal.setText("Total de vehículos: 0");
                lblEstado.setText("La API respondió con código HTTP " + resultado.codigoHttp() + ". Verifique el JWT.");
            }
        }));
    }

    private void mostrarEstado(String mensaje, boolean esError) {
        lblEstado.setText(mensaje);
        if (esError) {
            lblEstado.setStyle("-fx-text-fill: red;");
        } else {
            lblEstado.setStyle("-fx-text-fill: black;");
        }

    }
}
