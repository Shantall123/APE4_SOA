# Cliente web React – Consulta de vehículos (Vite + Fetch)

Aplicación web en React que consulta `GET /api/vehiculos` de la API Java mediante Fetch. Permite:

- Filtrar por marca, en el navegador.
- Ver el detalle de un vehículo al seleccionar su ID.
- Ver el código HTTP de cada consulta.
- Manejar los estados de carga, listado vacío, errores HTTP (401, 403, 404, 500, 502, 503) y fallos de red.

El JWT se pega en la interfaz y se mantiene solo en memoria, sin `localStorage`.

> La guía APE 04 prevé añadir la consulta de ventas (`GET /api/ventas`) y un resumen de indicadores. Esa parte todavía no está implementada. Este cliente no debe registrar ventas.

## Requisitos

| Software | Versión |
|---|---|
| Node.js | 20.19+ o 22.12+ (exigido por Vite 8; probado con 22.16) |
| npm | 10 o superior |
| API Java de vehículos | En ejecución en `http://127.0.0.1:8081` |

Dependencias: React 19, Vite 8, `@vitejs/plugin-react` y oxlint.

## 1. Instalación

```powershell
cd AplicacionWebREACT\react-fetch-vehiculos
npm install
```

La carpeta `node_modules` no se sube a Git; se recrea con `npm install`.

## 2. Configuración

En `vite.config.js`, el servidor de desarrollo usa el puerto **5173** y reenvía todas las rutas `/api` a la API Java. Así se evitan problemas de CORS y el navegador no necesita conocer la URL interna:

```js
proxy: {
  '/api': { target: 'http://127.0.0.1:8081', changeOrigin: true },
},
```

Para pasar por NGINX, cambia `target` a `http://127.0.0.1:8090`.

## 3. Ejecución en desarrollo

```powershell
npm run dev
```

Abre `http://localhost:5173`. Como `strictPort` está activado, si el 5173 está ocupado (por ejemplo, por la PWA) Vite se detiene en lugar de buscar otro puerto.

## 4. Compilación

```powershell
npm run build      # genera dist/
npm run preview    # sirve dist/ en http://localhost:4173
npm run lint       # análisis estático con oxlint
```

> `npm run preview` no tiene el proxy `/api` configurado. Para consultar la API usa `npm run dev` o publica `dist/` detrás de NGINX.

## 5. Uso

1. Obtén un JWT con `POST http://127.0.0.1:8081/oauth/token` desde Postman.
2. Pégalo en **Token JWT**, sin escribir `Bearer`.
3. Pulsa **Consultar vehículos**.
4. Escribe una marca en **Filtrar por marca** y pulsa el ID de una fila para ver su detalle.
5. Si dejas el token vacío, la API responde 401 y el mensaje aparece en pantalla.

## Estructura

```text
index.html
vite.config.js        Puerto 5173 y proxy /api
src/
  main.jsx            Montaje de React
  App.jsx             Interfaz: token, catálogo, filtro y detalle
  App.css, index.css  Estilos
  api/vehiculosApi.js Cliente Fetch: JWT, códigos HTTP y validación del JSON
```
