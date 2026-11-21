import test from 'node:test';
import assert from 'node:assert/strict';
import { createServer } from 'node:http';
import { once } from 'node:events';
import { crearServidor } from '../server.mjs';

/**
 * Pruebas de integración locales: un backend simulado (en lugar de NGINX)
 * y el servidor de la PWA, ambos en puertos aleatorios.
 */
async function conServidores(manejador, prueba, opciones = {}) {
  const recibidas = [];
  const backend = createServer(async (req, res) => {
    const partes = [];
    for await (const p of req) partes.push(p);
    const peticion = { metodo: req.method, url: req.url, headers: req.headers, cuerpo: Buffer.concat(partes).toString() };
    recibidas.push(peticion);
    manejador(peticion, res);
  });
  backend.listen(0, '127.0.0.1'); await once(backend, 'listening');
  const frontend = crearServidor({
    apiBase: `http://127.0.0.1:${backend.address().port}`,
    clientId: 'cliente-prueba', clientSecret: 'secreto-prueba', ...opciones,
  });
  frontend.listen(0, '127.0.0.1'); await once(frontend, 'listening');
  try {
    await prueba(`http://127.0.0.1:${frontend.address().port}`, recibidas);
  } finally {
    await new Promise((r) => frontend.close(r));
    await new Promise((r) => backend.close(r));
  }
}

const jsonRes = (res, estado, cuerpo, extra = {}) => {
  res.writeHead(estado, { 'Content-Type': 'application/json', ...extra });
  res.end(JSON.stringify(cuerpo));
};

test('proxy reenvía Bearer y respuesta HTTP del backend', async () => {
  await conServidores((_p, res) => jsonRes(res, 200, []), async (base, recibidas) => {
    const respuesta = await fetch(`${base}/api/vehiculos?estado=DISPONIBLE`, {
      headers: { Authorization: 'Bearer prueba.jwt' },
    });
    assert.equal(respuesta.status, 200);
    assert.equal(recibidas[0].headers.authorization, 'Bearer prueba.jwt');
    assert.equal(recibidas[0].url, '/api/vehiculos?estado=DISPONIBLE');
    assert.equal(respuesta.headers.get('cache-control'), 'no-store');
    assert.deepEqual(await respuesta.json(), []);
  });
});

test('login: el servidor agrega Basic del cliente y no expone refresh_token', async () => {
  const token = { access_token: 'a.b.c', token_type: 'bearer', refresh_token: 'NO-EXPONER', expires_in: 1800, scope: 'vehiculos' };
  await conServidores((_p, res) => jsonRes(res, 200, token), async (base, recibidas) => {
    const respuesta = await fetch(`${base}/oauth/token`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
      body: 'username=operador&password=clave',
    });
    assert.equal(respuesta.status, 200);
    const cuerpo = await respuesta.json();
    assert.equal(cuerpo.access_token, 'a.b.c');
    assert.equal(cuerpo.refresh_token, undefined);

    const enviada = recibidas[0];
    const basico = Buffer.from('cliente-prueba:secreto-prueba').toString('base64');
    assert.equal(enviada.headers.authorization, `Basic ${basico}`);
    const form = new URLSearchParams(enviada.cuerpo);
    assert.equal(form.get('grant_type'), 'password');
    assert.equal(form.get('username'), 'operador');
  });
});

test('login con credenciales incorrectas conserva el 401 real', async () => {
  await conServidores((_p, res) => jsonRes(res, 401, { success: false, mensaje: 'Usuario o contraseña incorrectos' }), async (base) => {
    const respuesta = await fetch(`${base}/oauth/token`, { method: 'POST', body: 'username=x&password=y' });
    assert.equal(respuesta.status, 401);
  });
});

test('login sin credenciales de cliente configuradas responde 500 sin llamar al backend', async () => {
  await conServidores(() => assert.fail('no debe llamar al backend'), async (base, recibidas) => {
    const respuesta = await fetch(`${base}/oauth/token`, { method: 'POST', body: 'username=x&password=y' });
    assert.equal(respuesta.status, 500);
    assert.equal(recibidas.length, 0);
  }, { clientId: '', clientSecret: '' });
});

test('POST /api/ventas reenvía JSON, Bearer, Idempotency-Key y Location', async () => {
  const venta = { id: 7, estado: 'COMPLETADA' };
  await conServidores((_p, res) => jsonRes(res, 201, venta, { Location: '/api/ventas/7' }), async (base, recibidas) => {
    const respuesta = await fetch(`${base}/api/ventas`, {
      method: 'POST',
      headers: { Authorization: 'Bearer t', 'Content-Type': 'application/json', 'Idempotency-Key': 'venta-abc12345' },
      body: JSON.stringify({ vehiculoId: 3, compradorId: 1 }),
    });
    assert.equal(respuesta.status, 201);
    assert.equal(respuesta.headers.get('location'), '/api/ventas/7');
    const enviada = recibidas[0];
    assert.equal(enviada.metodo, 'POST');
    assert.equal(enviada.headers['idempotency-key'], 'venta-abc12345');
    assert.equal(enviada.headers.authorization, 'Bearer t');
    assert.deepEqual(JSON.parse(enviada.cuerpo), { vehiculoId: 3, compradorId: 1 });
  });
});

test('rutas fuera de la lista blanca no llegan al backend', async () => {
  await conServidores(() => assert.fail('no debe llamar al backend'), async (base, recibidas) => {
    for (const [metodo, ruta] of [['POST', '/api/vehiculos'], ['POST', '/api/compradores'], ['DELETE', '/api/ventas/1'], ['GET', '/api/reservas']]) {
      const respuesta = await fetch(`${base}${ruta}`, { method: metodo });
      assert.equal(respuesta.status, 404, `${metodo} ${ruta}`);
    }
    assert.equal(recibidas.length, 0);
  });
});

test('backend apagado responde 502 controlado', async () => {
  const frontend = crearServidor({ apiBase: 'http://127.0.0.1:9', clientId: 'a', clientSecret: 'b' });
  frontend.listen(0, '127.0.0.1'); await once(frontend, 'listening');
  try {
    const respuesta = await fetch(`http://127.0.0.1:${frontend.address().port}/api/vehiculos`);
    assert.equal(respuesta.status, 502);
  } finally {
    await new Promise((r) => frontend.close(r));
  }
});
