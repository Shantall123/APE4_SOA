import test from 'node:test';
import assert from 'node:assert/strict';
import {
  listarVehiculos, normalizarToken, iniciarSesion, registrarVenta,
  generarClaveIdempotencia, ErrorApi,
} from '../public/api.js';

// Los datos de estas pruebas son simulados y no sustituyen a la API real.
const respuestaJson = (estado, cuerpo, encabezados = {}) => new Response(JSON.stringify(cuerpo), {
  status: estado, headers: { 'Content-Type': 'application/json', ...encabezados },
});

test('normaliza un token pegado con prefijo Bearer', () => {
  assert.equal(normalizarToken('  bearer abc.def.ghi  '), 'abc.def.ghi');
});

test('GET transmite Authorization y recibe una lista JSON', async () => {
  let solicitud;
  const simularFetch = async (ruta, opciones) => {
    solicitud = { ruta, opciones };
    return respuestaJson(200, [{ idVehiculo: 1, marca: 'Ejemplo' }]);
  };
  const resultado = await listarVehiculos('mi.token', simularFetch);
  assert.equal(solicitud.ruta, '/api/vehiculos');
  assert.equal(solicitud.opciones.headers.get('Authorization'), 'Bearer mi.token');
  assert.equal(solicitud.opciones.cache, 'no-store');
  assert.equal(resultado.vehiculos.length, 1);
});

test('filtra vehículos DISPONIBLES mediante el parámetro estado', async () => {
  let ruta;
  await listarVehiculos('t', async (r) => { ruta = r; return respuestaJson(200, []); }, { estado: 'DISPONIBLE' });
  assert.equal(ruta, '/api/vehiculos?estado=DISPONIBLE');
});

test('sin token no agrega Authorization (el backend decide 401)', async () => {
  const simularFetch = async (_ruta, opciones) => {
    assert.equal(opciones.headers.has('Authorization'), false);
    return new Response('[]', { status: 200 });
  };
  await listarVehiculos('', simularFetch);
});

test('identifica HTTP 401 sin mostrar una lista inventada', async () => {
  await assert.rejects(
    listarVehiculos('vencido', async () => new Response('No autorizado', { status: 401 })),
    (error) => error.codigoHttp === 401 && /401/.test(error.message),
  );
});

test('rechaza un objeto en lugar del arreglo JSON contratado', async () => {
  await assert.rejects(
    listarVehiculos('jwt', async () => new Response('{"data":[]}', { status: 200 })),
    /arreglo JSON/,
  );
});

test('informa error de conectividad como resultado incierto', async () => {
  await assert.rejects(
    listarVehiculos('jwt', async () => { throw new Error('sin red'); }),
    (error) => /conectar/.test(error.message) && error.incierto === true,
  );
});

test('login envía solo usuario y contraseña (sin credenciales del cliente)', async () => {
  let solicitud;
  const r = await iniciarSesion('operador', 'clave', async (ruta, opciones) => {
    solicitud = { ruta, opciones };
    return respuestaJson(200, { access_token: 'x.y.z', expires_in: 1800 });
  });
  assert.equal(solicitud.ruta, '/oauth/token');
  assert.equal(solicitud.opciones.method, 'POST');
  assert.equal(solicitud.opciones.headers.has('Authorization'), false);
  const form = new URLSearchParams(solicitud.opciones.body);
  assert.equal(form.get('username'), 'operador');
  assert.equal(form.get('password'), 'clave');
  assert.equal(r.token, 'x.y.z');
});

test('login con 401 informa credenciales incorrectas', async () => {
  await assert.rejects(
    iniciarSesion('a', 'b', async () => respuestaJson(401, { mensaje: 'Usuario o contraseña incorrectos' })),
    (error) => error.codigoHttp === 401 && /incorrectos/.test(error.message),
  );
});

test('la Idempotency-Key cumple el formato exigido por FastAPI', () => {
  const clave = generarClaveIdempotencia();
  assert.match(clave, /^[A-Za-z0-9_-]{8,100}$/);
  assert.notEqual(clave, generarClaveIdempotencia());
});

test('registrarVenta envía Bearer, Idempotency-Key y cuerpo JSON; devuelve el HTTP real', async () => {
  let solicitud;
  const r = await registrarVenta('jwt', { vehiculoId: '3', compradorId: 1 }, 'venta-clave-001', async (ruta, opciones) => {
    solicitud = { ruta, opciones };
    return respuestaJson(201, { id: 9, estado: 'COMPLETADA' }, { Location: '/api/ventas/9' });
  });
  assert.equal(solicitud.ruta, '/api/ventas');
  assert.equal(solicitud.opciones.method, 'POST');
  assert.equal(solicitud.opciones.headers.get('Authorization'), 'Bearer jwt');
  assert.equal(solicitud.opciones.headers.get('Idempotency-Key'), 'venta-clave-001');
  assert.deepEqual(JSON.parse(solicitud.opciones.body), { vehiculoId: 3, compradorId: 1 });
  assert.equal(r.codigoHttp, 201);
  assert.equal(r.venta.id, 9);
  assert.equal(r.ubicacion, '/api/ventas/9');
});

test('un 409 es un rechazo definitivo y un 504 es incierto', async () => {
  await assert.rejects(
    registrarVenta('jwt', { vehiculoId: 1, compradorId: 1 }, 'venta-clave-002',
      async () => respuestaJson(409, { detail: 'Vehículo inexistente o no disponible' })),
    (e) => e instanceof ErrorApi && e.codigoHttp === 409 && !e.incierto && /no disponible/.test(e.message),
  );
  await assert.rejects(
    registrarVenta('jwt', { vehiculoId: 1, compradorId: 1 }, 'venta-clave-003',
      async () => respuestaJson(504, { detail: 'sin respuesta' })),
    (e) => e.codigoHttp === 504 && e.incierto,
  );
});
