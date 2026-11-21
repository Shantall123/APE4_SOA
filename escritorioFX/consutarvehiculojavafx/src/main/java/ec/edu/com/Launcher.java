package ec.edu.com;

import javafx.application.Application;

/** Punto de entrada alternativo (IDE): abre el visor de notificaciones de venta. */
public class Launcher {
    private Launcher() {
    }

    public static void main(String[] args) {
        Application.launch(NotificacionesApp.class, args);
    }
}
