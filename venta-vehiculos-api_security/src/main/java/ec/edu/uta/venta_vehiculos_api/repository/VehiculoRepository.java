package ec.edu.uta.venta_vehiculos_api.repository;

import java.util.List;
import java.util.Optional;

import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

import ec.edu.uta.venta_vehiculos_api.model.Vehiculo;

public interface VehiculoRepository

    extends JpaRepository<Vehiculo, Integer> {

    Optional<Vehiculo> findByPlaca(String placa);

    Optional<Vehiculo> findByChasis(String chasis);

    List<Vehiculo> findByMarcaContainingIgnoreCase(String marca);
    List<Vehiculo> findByEstadoIgnoreCase(String estado);
    List<Vehiculo> findByMarcaContainingIgnoreCaseAndEstadoIgnoreCase(String marca, String estado);


    @Query(
    value = """
        SELECT *
        FROM vehiculos
        WHERE id_vehiculo = :id
        FOR UPDATE
        """,
    nativeQuery = true
)
Optional<Vehiculo> bloquear(@Param("id") Integer id);
}
