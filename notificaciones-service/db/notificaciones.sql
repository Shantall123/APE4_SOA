-- Actividad 9: tabla de notificaciones de venta del servicio notificaciones-service.
-- Se crea en la base existente venta_vehiculos (no se crea una base nueva).
-- Cada fila es un aviso recibido desde la cola RabbitMQ notificaciones.ventas.
-- evento_id es UNIQUE: un mismo evento nunca se registra dos veces.

USE venta_vehiculos;

CREATE TABLE IF NOT EXISTS notificaciones (
    id               BIGINT        NOT NULL AUTO_INCREMENT,
    evento_id        VARCHAR(36)   NOT NULL,
    tipo             VARCHAR(40)   NOT NULL,
    venta_id         INT           NOT NULL,
    vehiculo_id      INT           NOT NULL,
    precio_acordado  DECIMAL(12,2) NULL,
    moneda           CHAR(3)       NULL,
    fecha_venta      DATETIME      NULL      COMMENT 'fechaUTC del evento (UTC)',
    mensaje          VARCHAR(255)  NOT NULL,
    recibida_en      DATETIME      NOT NULL  COMMENT 'Momento en que el listener la registró (UTC)',
    PRIMARY KEY (id),
    CONSTRAINT uq_notificaciones_evento UNIQUE (evento_id),
    INDEX ix_notificaciones_recibida (recibida_en)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
