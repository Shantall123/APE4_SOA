# Cliente Windows Forms – Consulta de vehículos (C#)

Aplicación de escritorio Windows Forms que consulta `GET /api/vehiculos` de la API Java y muestra el resultado en un `DataGridView`. El JWT se pega en un campo de texto. La aplicación muestra:

- El total de vehículos.
- Mensajes específicos para HTTP 401, 403, otros errores HTTP, fallos de conexión, tiempo de espera agotado (15 s) y JSON inválido.

> La guía APE 04 prevé ampliar este cliente para crear vehículos y compradores y listar compradores y ventas. Esa parte todavía no está implementada.

## Requisitos

| Software | Versión |
|---|---|
| Windows | 10 u 11 |
| .NET SDK | **10.0** (el proyecto usa `net10.0-windows`) |
| Visual Studio 2022/2026 (opcional) | Con la carga de trabajo "Desarrollo de escritorio de .NET" |
| API Java de vehículos | En ejecución en `http://localhost:8081` |

> Para usar .NET 8, como indica la guía, cambia en `ConsultaVehiculosWinForms.csproj` la línea `<TargetFramework>net10.0-windows</TargetFramework>` por `net8.0-windows` e instala el SDK 8.

## 1. Configuración

La URL base está en `ConsultaVehiculosWinForms/FormPrincipal.cs`:

```csharp
private const string UrlJava = "http://localhost:8081/";
```

Para pasar por NGINX, cámbiala a `http://localhost:8090/`.

## 2. Compilación

Desde la carpeta `ConsultaVehiculosWinForms` (la que contiene `ConsultaVehiculosWinForms.slnx`):

```powershell
dotnet build ConsultaVehiculosWinForms\ConsultaVehiculosWinForms.csproj
```

Los binarios quedan en `ConsultaVehiculosWinForms\bin\Debug\net10.0-windows\`. Las carpetas `bin` y `obj` no se suben a Git.

## 3. Ejecución

```powershell
dotnet run --project ConsultaVehiculosWinForms\ConsultaVehiculosWinForms.csproj
```

Desde Visual Studio: abre `ConsultaVehiculosWinForms.slnx` y pulsa **F5**.

## 4. Uso

1. Obtén un JWT con `POST http://127.0.0.1:8081/oauth/token` desde Postman.
2. Pega el `access_token` en **Ingrese el JWT**. Se acepta con o sin el prefijo `Bearer`.
3. Pulsa **Consultar**.

| Respuesta | Mensaje |
|---|---|
| 200 con datos | "HTTP 200: consulta exitosa." |
| 200 sin datos | "HTTP 200: no hay vehiculos registrados." |
| 401 | "codigo 401: token invalido o caducado" |
| 403 | "codigo 403: no tiene permisos para acceder a este recurso" |
| API apagada | "no se pudo conectar a spring boot" |

## Estructura

```text
ConsultaVehiculosWinForms.slnx
ConsultaVehiculosWinForms/
  ConsultaVehiculosWinForms.csproj
  Program.cs                   Punto de entrada
  FormPrincipal.cs             Lógica de consulta HTTP y manejo de errores
  FormPrincipal.Designer.cs    Controles del formulario
  Vehiculo.cs                  Modelo con JsonPropertyName
```
