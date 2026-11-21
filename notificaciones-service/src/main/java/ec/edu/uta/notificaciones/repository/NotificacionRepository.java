package ec.edu.uta.notificaciones.repository;

import ec.edu.uta.notificaciones.model.Notificacion;
import org.springframework.data.jpa.repository.JpaRepository;

public interface NotificacionRepository extends JpaRepository<Notificacion, Long> {

    boolean existsByEventoId(String eventoId);
}
