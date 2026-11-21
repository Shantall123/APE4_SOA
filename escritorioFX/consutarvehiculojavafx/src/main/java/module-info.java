module ec.edu.com {
    requires javafx.controls;
    requires javafx.fxml;
    requires com.fasterxml.jackson.databind;
    requires com.fasterxml.jackson.annotation;
    requires java.net.http;
    opens ec.edu.com to javafx.fxml, com.fasterxml.jackson.databind;
    exports ec.edu.com;
}
