# PWA móvil – Login y registro de ventas

Aplicación web progresiva (HTML, CSS y JavaScript nativo) adaptada a móvil. Es el **único cliente que registra ventas** de la arquitectura:

1. **Login** con usuario y contraseña → `POST /oauth/token` a través de NGINX. El JWT se emite para esa cuenta; no se pega a mano.
2. **Selección** de un vehículo `DISPONIBLE` (`GET /api/vehiculos?estado=DISPONIBLE`) y de un comprador (`GET /api/compradores`).
3. **Registro** con `POST /api/ventas`, enviando `Authorization: Bearer <JWT>` e `Idempotency-Key`.
4. **Resultado real**: código HTTP (201, 200, 202, 403, 409…), estado de la venta y mensaje. Se puede consultar después con `GET /api/ventas/{id}`.
5. **Mis ventas**: historial del usuario (`GET /api/ventas`) con consulta de estado de cada venta.

## Arquitectura

```text
Navegador (PWA) ──► server.mjs (mismo origen, :5174) ──► NGINX :8090 ──► API Java (8080/8083) / FastAPI (8000)
```

- `server.mjs` sirve los archivos y actúa como **proxy con lista blanca**. Solo reenvía `POST /oauth/token`, `GET /api/identidad`, `GET /api/vehiculos`, `GET /api/compradores`, `GET /api/ventas`, `GET /api/ventas/{id}` y `POST /api/ventas`. Cualquier otra ruta responde 404.
- Las credenciales del **cliente OAuth** (`client-id` y `client-secret`) viven solo en el servidor (`.env`). El navegador envía únicamente usuario y contraseña, y recibe solo el `access_token`, sin el `refresh_token`.
- El **JWT** se guarda en `sessionStorage` solo durante la sesión. Se borra al cerrar sesión, al vencer o ante un HTTP 401. Nunca va al código, a `localStorage` ni a la caché del Service Worker.
- La **Idempotency-Key** se genera por venta (`venta-<uuid>`). Si no llega confirmación (sin red, 502, 503 o 504), la clave se conserva y el botón **"Verificar / reintentar sin duplicar"** reenvía la misma solicitud con la misma clave: FastAPI devuelve la venta existente (200) en lugar de crear otra.

## Requisitos

| Software | Versión |
|---|---|
| Node.js | 20.12 o superior (probado con 22) |
| NGINX | En 8090, con las rutas de la Actividad 3 |
| API Java, FastAPI y MySQL | En ejecución |

No necesita `npm install`: el proyecto no tiene dependencias externas.

## 1. Configuración

Copia `.env.example` como `.env` en esta carpeta y completa los valores. El archivo `.env` está excluido de Git.

```dotenv
API_BASE_URL=http://127.0.0.1:8090
OAUTH_CLIENT_ID=vehiculos-client
OAUTH_CLIENT_SECRET=<app.security.client-secret de la API Java>
PORT=5174
HOST=127.0.0.1
```

## 2. Ejecución

```powershell
cd AplicacionMovilPWA
npm start
```

Abre `http://localhost:5174`.

Para probar desde el celular en la misma red, usa `HOST=0.0.0.0`. Para **instalar** la PWA se necesita un origen seguro (`localhost` o HTTPS).

## 3. Pruebas automáticas

```powershell
npm test
```

Son 19 pruebas: el cliente HTTP (login, Idempotency-Key, 401, 409 y 504) y el proxy (inyección de Basic, filtrado del `refresh_token`, reenvío de `Idempotency-Key`, lista blanca y backend caído).

## 4. Compilación

```powershell
npm run build      # copia public/ en dist/
npm run preview    # sirve dist/ en http://localhost:4173 con el mismo proxy
```

## Respuestas de POST /api/ventas

| HTTP | Significado en la PWA |
|---|---|
| 201 | Venta registrada y COMPLETADA |
| 200 | Reintento con la misma Idempotency-Key: se devuelve la venta existente, sin duplicado |
| 202 | Venta recibida en PENDIENTE o EN_REVISION: consultar su estado |
| 401 | Token ausente o vencido: vuelve al login |
| 403 | El rol no permite vender (se requiere OPERADOR o ADMIN) |
| 404 | Comprador inexistente |
| 409 | Vehículo no disponible o conflicto |
| 422 | Datos inválidos o falta la Idempotency-Key |
| 502 / 503 / 504 / sin red | Resultado incierto: verificar con la misma clave |

## Estructura

```text
server.mjs              Servidor estático + proxy con lista blanca y login con credenciales del cliente
.env.example            Plantilla de configuración (sin secretos)
public/
  index.html            Vistas: login, nueva venta (3 pasos), resultado y "Mis ventas"
  main.js               Lógica de la interfaz
  api.js                Cliente HTTP (ErrorApi, Idempotency-Key, códigos HTTP)
  sesion.js             JWT en sessionStorage y venta pendiente
  sw.js                 Service Worker (solo recursos estáticos; nunca /api ni /oauth)
  manifest.webmanifest  Manifiesto PWA
  styles.css, icons/    Estilos adaptables e íconos 192/512
tests/                  Pruebas node:test
```
