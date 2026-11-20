# Plataforma web de consulta – React + Fetch

Cliente web de **solo consulta** construido con React 19 y Vite 8. Usa Fetch y pasa siempre por NGINX:

- **Inicio de sesión** con usuario y contraseña (`POST /oauth/token`). El JWT no se pega a mano.
- **Vehículos**: listado (`GET /api/vehiculos`), filtro por **marca** y estado aplicado por la API (`?marca=&estado=`), indicadores del catálogo (total, disponibles, reservados y vendidos) y **detalle** (`GET /api/vehiculos/{id}`).
- **Ventas** (roles OPERADOR y ADMIN): listado paginado (`GET /api/ventas`), filtro por estado, **resumen** con datos reales (`GET /api/ventas/resumen`) y detalle (`GET /api/ventas/{id}`).
- **Estados de la interfaz**: carga, listado vacío, errores HTTP (400, 401, 403, 404, 422, 500, 502, 503, 504), fallos de conexión y tiempo agotado.
- **No registra ventas**: no hay formularios de venta ni `POST /api/ventas`, y el proxy rechaza cualquier método distinto de GET hacia `/api` con un 404.

## Arquitectura

```text
Navegador (React) ──► Vite :5173 (proxy) ──► NGINX :8090 ──► API Java (8080/8083) / FastAPI (8000)
```

- El navegador solo usa rutas relativas (`/oauth/token` y `/api/...`): no necesita CORS ni conoce URL internas.
- Las credenciales del **cliente OAuth** se leen de `.env.local`, sin el prefijo `VITE_`, así que Vite **no las incluye** en el código del navegador. El proxy las agrega como `Authorization: Basic` solo en `POST /oauth/token` y devuelve únicamente el `access_token`, sin el `refresh_token`.
- El JWT se guarda en `sessionStorage` solo durante la sesión. Se borra al cerrar sesión (con confirmación), al vencer o ante un HTTP 401.

## Permisos por rol

| Rol | Módulos |
|---|---|
| ADMIN, OPERADOR | Vehículos y Ventas |
| CONSULTA | Solo Vehículos |

## Requisitos

| Software | Versión |
|---|---|
| Node.js | 20.19+ o 22.12+ (exigido por Vite 8; probado con 22.16) |
| NGINX | En 8090, con las rutas de la Actividad 3 |
| API Java, FastAPI y MySQL | En ejecución |

## 1. Instalación y configuración

```powershell
cd AplicacionWebREACT\react-fetch-vehiculos
npm install
```

Copia `.env.example` como `.env.local`. El archivo `*.local` está excluido de Git.

```dotenv
API_BASE_URL=http://127.0.0.1:8090
OAUTH_CLIENT_ID=vehiculos-client
OAUTH_CLIENT_SECRET=<app.security.client-secret de la API Java>
```

## 2. Ejecución

```powershell
npm run dev        # http://localhost:5173
```

## 3. Compilación y calidad

```powershell
npm run build      # genera dist/
npm run preview    # sirve dist/ en http://localhost:4173 con el mismo proxy
npm run lint       # oxlint
```

## Estructura

```text
vite.config.js            Proxy a NGINX: login con credenciales del cliente y solo GET hacia /api
.env.example              Plantilla de configuración (sin secretos)
src/
  App.jsx                 Sesión, barra de usuario/rol, pestañas según el rol
  components/
    Login.jsx             Inicio de sesión
    Vehiculos.jsx         Catálogo, filtro por marca/estado, indicadores y detalle
    Ventas.jsx            Ventas paginadas, filtro, resumen y detalle (solo lectura)
    Estado.jsx            Mensaje de estado con el código HTTP real
  api/
    http.js               Fetch común: JWT, tiempo límite, ErrorApi y mensajes por código
    authApi.js            Login, identidad y sesión
    vehiculosApi.js       GET /api/vehiculos y /api/vehiculos/{id}
    ventasApi.js          GET /api/ventas, /resumen y /{id} (sin POST)
  utils/formato.js        Formato de moneda, fecha y estados
```
