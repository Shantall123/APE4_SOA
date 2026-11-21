import { defineConfig, loadEnv } from 'vite';
import react from '@vitejs/plugin-react';

/**
 * Proxy inverso de Vite hacia NGINX (puerta de entrada REST).
 *
 * - El navegador solo usa rutas relativas (/oauth/token y /api/...), por lo que
 *   no conoce URL internas ni necesita CORS.
 * - Las credenciales del CLIENTE OAuth se leen de .env.local (sin prefijo VITE_,
 *   así Vite nunca las incluye en el código del navegador) y el proxy las agrega
 *   como Authorization: Basic solo en POST /oauth/token.
 * - React es una plataforma de CONSULTA: el proxy solo deja pasar GET hacia /api
 *   (POST /api/ventas y cualquier otra escritura responden 404).
 */
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '');
  const destino = (env.API_BASE_URL || 'http://127.0.0.1:8090').replace(/\/$/, '');
  const basico = env.OAUTH_CLIENT_ID && env.OAUTH_CLIENT_SECRET
    ? Buffer.from(`${env.OAUTH_CLIENT_ID}:${env.OAUTH_CLIENT_SECRET}`).toString('base64')
    : null;

  // NGINX apagado o inaccesible: 502 con detalle (Vite respondería 500 genérico).
  const responderCaida = (servidor) => {
    servidor.on('error', (_error, _req, res) => {
      if (!res || res.headersSent || res.writableEnded || typeof res.writeHead !== 'function') return;
      res.writeHead(502, { 'Content-Type': 'application/json; charset=utf-8' });
      res.end(JSON.stringify({ status: 502, detail: `No se pudo conectar con NGINX (${destino}).` }));
    });
  };

  const proxy = {
    '/oauth/token': {
      target: destino,
      changeOrigin: true,
      selfHandleResponse: true,
      bypass(req, res) {
        if (req.method !== 'POST') return false; // 404
        if (!basico) {
          res.statusCode = 500;
          res.setHeader('Content-Type', 'application/json; charset=utf-8');
          res.end(JSON.stringify({
            status: 500,
            detail: 'Falta OAUTH_CLIENT_ID / OAUTH_CLIENT_SECRET en .env.local del proyecto React.',
          }));
          return req.url; // respuesta ya enviada
        }
        return undefined;
      },
      configure(servidor) {
        responderCaida(servidor);
        servidor.on('proxyReq', (peticion) => {
          peticion.setHeader('Authorization', `Basic ${basico}`);
        });
        // Al navegador solo se devuelve el access token (sin refresh_token).
        servidor.on('proxyRes', (respuestaRemota, _req, res) => {
          const partes = [];
          respuestaRemota.on('data', (parte) => partes.push(parte));
          respuestaRemota.on('end', () => {
            let cuerpo = Buffer.concat(partes);
            if (respuestaRemota.statusCode === 200) {
              try {
                const t = JSON.parse(cuerpo.toString('utf8'));
                cuerpo = Buffer.from(JSON.stringify({
                  access_token: t.access_token, token_type: t.token_type,
                  expires_in: t.expires_in, scope: t.scope,
                }));
              } catch { /* se devuelve tal cual */ }
            }
            res.statusCode = respuestaRemota.statusCode || 502;
            res.setHeader('Content-Type', respuestaRemota.headers['content-type'] || 'application/json');
            res.setHeader('Cache-Control', 'no-store');
            res.end(cuerpo);
          });
        });
      },
    },
    '/api': {
      target: destino,
      changeOrigin: true,
      // Solo lectura: cualquier método distinto de GET recibe 404 sin llegar a NGINX.
      bypass(req) {
        return req.method === 'GET' ? undefined : false;
      },
      configure: responderCaida,
    },
  };

  return {
    plugins: [react()],
    server: { port: 5173, strictPort: true, proxy },
    preview: { port: 4173, strictPort: true, proxy },
  };
});
