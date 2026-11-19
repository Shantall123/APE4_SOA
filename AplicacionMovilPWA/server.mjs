/**
 * Servidor de la PWA (sin dependencias externas).
 *
 * 1. Sirve los archivos estáticos de la aplicación (public/ o dist/).
 * 2. Actúa como proxy del mismo origen hacia NGINX (API_BASE_URL), por lo que
 *    el navegador no necesita CORS ni conoce las URL internas.
 * 3. En el login agrega las credenciales del CLIENTE OAuth (Basic) que viven
 *    solo en el servidor (.env); el navegador envía únicamente usuario y contraseña.
 *
 * Solo se reenvían las rutas que la PWA necesita (lista blanca).
 */
import { createServer } from 'node:http';
import { readFile, stat } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import { resolve, extname, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = resolve(fileURLToPath(new URL('.', import.meta.url)));

// Configuración local fuera de Git: AplicacionMovilPWA/.env (ver .env.example).
const ARCHIVO_ENV = resolve(ROOT, '.env');
if (existsSync(ARCHIVO_ENV) && typeof process.loadEnvFile === 'function') {
  process.loadEnvFile(ARCHIVO_ENV);
}

const TIPOS = {
  '.html': 'text/html; charset=utf-8', '.css': 'text/css; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8', '.png': 'image/png',
  '.webmanifest': 'application/manifest+json; charset=utf-8',
  '.json': 'application/json; charset=utf-8', '.svg': 'image/svg+xml',
};

const LIMITE_CUERPO = 16 * 1024; // bytes
const TIEMPO_ESPERA_MS = 15000;

/**
 * Rutas permitidas hacia el backend. `prueba` recibe método y ruta.
 * La PWA es el único cliente que registra ventas (POST /api/ventas).
 */
const RUTAS = [
  { metodo: 'GET', prueba: (r) => r === '/api/identidad' },
  { metodo: 'GET', prueba: (r) => r === '/api/vehiculos' },
  { metodo: 'GET', prueba: (r) => r === '/api/compradores' },
  { metodo: 'GET', prueba: (r) => r === '/api/ventas' },
  { metodo: 'GET', prueba: (r) => /^\/api\/ventas\/\d+$/.test(r) },
  { metodo: 'POST', prueba: (r) => r === '/api/ventas' },
];

function json(respuesta, estado, cuerpo) {
  respuesta.writeHead(estado, { 'Content-Type': 'application/json; charset=utf-8', 'Cache-Control': 'no-store' });
  respuesta.end(JSON.stringify(cuerpo));
}

async function leerCuerpo(peticion) {
  const partes = [];
  let total = 0;
  for await (const parte of peticion) {
    total += parte.length;
    if (total > LIMITE_CUERPO) throw Object.assign(new Error('Cuerpo demasiado grande'), { estado: 413 });
    partes.push(parte);
  }
  return Buffer.concat(partes);
}

/** Envía la petición al backend con tiempo límite; distingue caída y espera agotada. */
async function reenviar(url, opciones, respuesta, transformar) {
  const abortador = new AbortController();
  const temporizador = setTimeout(() => abortador.abort(), TIEMPO_ESPERA_MS);
  try {
    const remoto = await fetch(url, { ...opciones, signal: abortador.signal, cache: 'no-store' });
    const cuerpo = Buffer.from(await remoto.arrayBuffer());
    const encabezados = {
      'Content-Type': remoto.headers.get('content-type') || 'application/json; charset=utf-8',
      'Cache-Control': 'no-store', Vary: 'Authorization',
    };
    const ubicacion = remoto.headers.get('location');
    if (ubicacion) encabezados.Location = ubicacion;
    const final = transformar && remoto.ok ? transformar(cuerpo) : cuerpo;
    respuesta.writeHead(remoto.status, encabezados);
    respuesta.end(final);
  } catch (error) {
    const tiempoAgotado = error.name === 'AbortError';
    json(respuesta, tiempoAgotado ? 504 : 502, {
      status: tiempoAgotado ? 504 : 502,
      detail: tiempoAgotado
        ? 'El servicio no respondió a tiempo. El resultado de la operación es incierto.'
        : 'No se pudo conectar con NGINX o con el servicio de destino.',
    });
  } finally {
    clearTimeout(temporizador);
  }
}

export function crearServidor({
  directorio = resolve(ROOT, 'public'),
  apiBase = process.env.API_BASE_URL || 'http://127.0.0.1:8090',
  clientId = process.env.OAUTH_CLIENT_ID || '',
  clientSecret = process.env.OAUTH_CLIENT_SECRET || '',
} = {}) {
  const carpeta = resolve(directorio);
  const base = apiBase.replace(/\/$/, '');

  return createServer(async (peticion, respuesta) => {
    const url = new URL(peticion.url || '/', 'http://localhost');
    respuesta.setHeader('X-Content-Type-Options', 'nosniff');
    respuesta.setHeader('Referrer-Policy', 'no-referrer');
    respuesta.setHeader('Cache-Control', 'no-store');

    // ------------------------------------------------------------ Login
    if (url.pathname === '/oauth/token') {
      if (peticion.method !== 'POST') return json(respuesta, 405, { status: 405, detail: 'Use POST' });
      if (!clientId || !clientSecret) {
        return json(respuesta, 500, {
          status: 500,
          detail: 'Servidor de la PWA sin OAUTH_CLIENT_ID / OAUTH_CLIENT_SECRET. Configure el archivo .env.',
        });
      }
      let datos;
      try {
        datos = new URLSearchParams((await leerCuerpo(peticion)).toString('utf8'));
      } catch (error) {
        return json(respuesta, error.estado || 400, { status: error.estado || 400, detail: error.message });
      }
      const formulario = new URLSearchParams({
        grant_type: 'password',
        username: datos.get('username') || '',
        password: datos.get('password') || '',
      });
      const basico = Buffer.from(`${clientId}:${clientSecret}`).toString('base64');
      // Al navegador solo se devuelve el access token (sin refresh_token).
      const filtrar = (cuerpo) => {
        try {
          const t = JSON.parse(cuerpo.toString('utf8'));
          return JSON.stringify({
            access_token: t.access_token, token_type: t.token_type,
            expires_in: t.expires_in, scope: t.scope,
          });
        } catch { return cuerpo; }
      };
      return reenviar(`${base}/oauth/token`, {
        method: 'POST',
        headers: {
          Authorization: `Basic ${basico}`,
          'Content-Type': 'application/x-www-form-urlencoded',
          Accept: 'application/json',
        },
        body: formulario.toString(),
      }, respuesta, filtrar);
    }

    // ------------------------------------------------------------ API
    if (url.pathname.startsWith('/api/')) {
      const permitida = RUTAS.some((r) => r.metodo === peticion.method && r.prueba(url.pathname));
      if (!permitida) return json(respuesta, 404, { status: 404, detail: 'Endpoint no disponible en la PWA' });

      const encabezados = { Accept: 'application/json' };
      if (peticion.headers.authorization) encabezados.Authorization = peticion.headers.authorization;

      const opciones = { method: peticion.method, headers: encabezados };
      if (peticion.method === 'POST') {
        try {
          opciones.body = await leerCuerpo(peticion);
        } catch (error) {
          return json(respuesta, error.estado || 400, { status: error.estado || 400, detail: error.message });
        }
        encabezados['Content-Type'] = 'application/json';
        if (peticion.headers['idempotency-key']) encabezados['Idempotency-Key'] = peticion.headers['idempotency-key'];
      }
      return reenviar(`${base}${url.pathname}${url.search}`, opciones, respuesta);
    }

    // ------------------------------------------------------------ Estáticos
    if (peticion.method !== 'GET' && peticion.method !== 'HEAD') {
      respuesta.writeHead(405); respuesta.end(); return;
    }
    let ruta;
    try { ruta = decodeURIComponent(url.pathname); }
    catch { respuesta.writeHead(400); respuesta.end(); return; }
    const archivo = resolve(carpeta, '.' + (ruta === '/' ? '/index.html' : ruta));
    if (!(archivo === carpeta || archivo.startsWith(carpeta + sep))) {
      respuesta.writeHead(403); respuesta.end(); return;
    }
    try {
      const detalle = await stat(archivo);
      if (!detalle.isFile()) throw new Error('Archivo no encontrado');
      respuesta.setHeader('Content-Type', TIPOS[extname(archivo)] || 'application/octet-stream');
      if (peticion.method === 'HEAD') { respuesta.writeHead(200); respuesta.end(); return; }
      respuesta.writeHead(200);
      respuesta.end(await readFile(archivo));
    } catch {
      respuesta.writeHead(404, { 'Content-Type': 'text/plain; charset=utf-8' });
      respuesta.end('Recurso no encontrado');
    }
  });
}

// Uso como módulo en pruebas sin abrir un puerto al importarlo.
if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const argPuerto = process.argv.find((arg) => arg.startsWith('--port='));
  const argCarpeta = process.argv.find((arg) => arg.startsWith('--dir='));
  const puerto = Number(argPuerto?.split('=')[1] || process.env.PORT || 5174);
  const host = process.env.HOST || '127.0.0.1';
  const carpeta = resolve(ROOT, argCarpeta?.split('=')[1] || 'public');
  const apiBase = process.env.API_BASE_URL || 'http://127.0.0.1:8090';
  crearServidor({ directorio: carpeta }).listen(puerto, host, () => {
    console.log(`PWA disponible: http://localhost:${puerto}`);
    console.log(`Proxy: /oauth/token y /api/* -> ${apiBase} (NGINX)`);
    if (!process.env.OAUTH_CLIENT_ID || !process.env.OAUTH_CLIENT_SECRET) {
      console.warn('AVISO: falta OAUTH_CLIENT_ID / OAUTH_CLIENT_SECRET en .env; el login no funcionará.');
    }
    console.log('Para cerrar: Ctrl + C');
  });
}
