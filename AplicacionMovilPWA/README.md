# PWA móvil – Consulta de vehículos

Aplicación web progresiva (PWA) en HTML, CSS y JavaScript nativo, adaptada a móvil. Consulta `GET /api/vehiculos` de la API Java con un JWT. Incluye:

- **Manifiesto e íconos** (192 y 512 px) para instalarla.
- **Service Worker** que guarda en caché solo los archivos estáticos, no las respuestas con JWT.
- **Servidor Node sin dependencias** (`server.mjs`) que sirve la aplicación y reenvía `GET /api/vehiculos` a Spring Boot desde el mismo origen, para evitar CORS.

> La guía APE 04 prevé que la PWA tenga login (`POST /oauth/token`) y sea el único cliente que registra ventas (`POST /api/ventas`). Esa parte todavía no está implementada. Hoy el servidor solo permite `GET /api/vehiculos` y responde 404 a cualquier otra ruta `/api/*`.

## Requisitos

| Software | Versión |
|---|---|
| Node.js | 20 o superior (probado con 22) |
| API Java de vehículos | En ejecución en `http://127.0.0.1:8081` |

No necesita `npm install`: el proyecto no tiene dependencias externas.

## 1. Configuración

El servidor se configura con variables de entorno:

| Variable | Valor por defecto | Uso |
|---|---|---|
| `PORT` | `5173` | Puerto de la PWA |
| `HOST` | `127.0.0.1` | Interfaz de escucha (`0.0.0.0` para abrirla desde el celular en la misma red) |
| `API_BASE_URL` | `http://127.0.0.1:8081` | API Java (o NGINX: `http://127.0.0.1:8090`) |

Ejemplo en PowerShell, para usar el puerto 5174 y no chocar con React en el 5173:

```powershell
$env:PORT = "5174"
$env:API_BASE_URL = "http://127.0.0.1:8081"
```

## 2. Ejecución en desarrollo

```powershell
cd AplicacionMovilPWA
npm start          # equivale a: node server.mjs
```

Abre `http://localhost:5173`, o el puerto indicado en `PORT`. En la consola aparece el proxy activo.

## 3. Pruebas

```powershell
npm test
```

Ejecuta las 7 pruebas de `tests/` (cliente y servidor) con el runner nativo de Node.

## 4. Compilación y vista previa

```powershell
npm run build      # copia public/ en dist/
npm run preview    # sirve dist/ en http://localhost:4173 con el mismo proxy
```

Para instalar la PWA en un celular se necesita un origen seguro: `localhost` o HTTPS. Desde otra máquina de la red local, publícala detrás de HTTPS, por ejemplo con NGINX.

## 5. Uso

1. Obtén un JWT con `POST http://127.0.0.1:8081/oauth/token` desde Postman.
2. Pégalo en **Token de usuario**. Se acepta con o sin `Bearer`.
3. Pulsa **Consultar vehículos**: se listan las tarjetas de vehículos con su precio en USD. **Borrar token** limpia el campo.

| Respuesta | Mensaje |
|---|---|
| 401 | "JWT ausente, inválido o vencido" |
| 403 | "el usuario no tiene permiso" |
| API apagada | 502: "no se pudo establecer conexión con Spring Boot" |
| Tiempo agotado (12 s) | 504 |

## Estructura

```text
server.mjs                Servidor estático + proxy GET /api/vehiculos
scripts/build.mjs         Copia public/ a dist/
public/
  index.html              Interfaz móvil
  main.js                 Lógica de la interfaz
  api.js                  Cliente HTTP (normaliza el JWT e interpreta códigos)
  sw.js                   Service Worker
  manifest.webmanifest    Manifiesto PWA
  styles.css, icons/      Estilos e íconos
tests/                    Pruebas node:test
docs/                     Guía práctica y pantalla de referencia
```
