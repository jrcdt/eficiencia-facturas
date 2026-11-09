# Eficiencia · Gestión de facturas

Aplicación para importar facturas desde JSON, gestionar pagos y notas de crédito y consultar reportes.

**Tecnologías:** .NET 8, Entity Framework Core, SQLite, React y TypeScript.

## Requisitos

- SDK de [.NET 8](https://dotnet.microsoft.com/download/dotnet/8.0).
- [Node.js 24](https://nodejs.org/) con npm.

La base de datos se crea automáticamente. No necesitas instalar SQLite ni ejecutar scripts SQL.

## Ejecutar manualmente en Windows

Para descargar el proyecto desde GitHub:

```powershell
git clone https://github.com/jrcdt/eficiencia-facturas.git
cd eficiencia-facturas
```

Necesitas **dos terminales PowerShell abiertas en la carpeta raíz del proyecto**: la que contiene `Eficiencia.sln` y este README.

### 1. Terminal 1: iniciar el backend

```powershell
dotnet run --project .\backend\InvoiceManager.Api
```

Espera a que aparezca `Now listening on: http://localhost:5080`. Deja esta terminal abierta.

Si PowerShell indica que **no reconoce `dotnet`**, en este equipo el SDK está instalado en tu perfil. Ejecuta lo siguiente en esa misma terminal y vuelve a intentar el comando anterior:

```powershell
$env:PATH = "$env:USERPROFILE\.dotnet;$env:PATH"
```

### 2. Terminal 2: iniciar el frontend

Desde la raíz del proyecto:

```powershell
cd .\frontend
npm ci
npm run dev
```

Espera a que aparezca `http://127.0.0.1:5173/`. Deja también esta terminal abierta.

`npm ci` instala las dependencias. Solo necesitas repetirlo cuando instales el proyecto o cambie `package-lock.json`; para los siguientes inicios basta con `npm run dev` desde `frontend`.

### 3. Abrir la aplicación

- **Aplicación:** [http://127.0.0.1:5173](http://127.0.0.1:5173)
- **Swagger de la API:** [http://localhost:5080/swagger](http://localhost:5080/swagger)

### 4. Cargar y revisar las facturas

1. Pulsa **Importar JSON** en la aplicación.
2. Selecciona `data/bd_exam_invoices.json` y confirma la importación.
3. En una base nueva se importan **50 facturas: 48 activas y 2 inconsistentes**.
4. En la columna **Acciones**, pulsa **Crear NC** para agregar una nota de crédito: ingresa el monto y confirma. El número y la fecha se asignan automáticamente.
5. Pulsa **Ver detalle** para consultar productos, pagos y el historial de notas de crédito. También puedes agregar una NC desde **Agregar nota de crédito**.
6. En **Reportes** puedes revisar pagos, vencidas e inconsistencias.

Si los datos ya estaban cargados, puedes revisarlos directamente. Reimportar el archivo omite las facturas existentes y conserva sus cambios.

Para probar la importación desde cero, pulsa **Eliminar facturas**, escribe **ELIMINAR** y confirma **Eliminar todas**. Se borran las facturas activas e inconsistentes, con sus pagos y NC. Después importa el JSON nuevamente: deben cargarse las 50 facturas. Para borrar solo una, abre **Ver detalle → Eliminar factura**; las inconsistentes tienen su propia opción **Eliminar** en el reporte.

### 5. Detener la aplicación

Presiona **Ctrl+C en cada terminal**. Para volver a iniciarla, repite los comandos de backend y frontend.

Si un puerto está ocupado, detén la ejecución anterior antes de iniciar otra. Si usaste `scripts/start.ps1`, presiona Enter en la terminal de ese script para detener ambos servidores.

## Pruebas (opcional)

Backend, desde la raíz del proyecto:

```powershell
dotnet test Eficiencia.sln
```

Flujo completo en navegador, desde `frontend` (requiere `dotnet` disponible en esa terminal):

```powershell
npx playwright install chromium
npm run test:e2e
```

## Organización y decisiones

Se utiliza el patrón **Service Layer** con inyección de dependencias: `Domain` contiene las entidades y reglas, `Application` los casos de uso e `Infrastructure` el acceso a SQLite mediante EF Core. Las consultas usan la vista SQL `vw_invoice_overview`.

Los estados se calculan automáticamente. Las facturas inconsistentes se conservan para el reporte y se excluyen de la gestión activa. Las notas de crédito no pueden superar el saldo disponible.

La base se guarda en `backend/InvoiceManager.Api/App_Data/invoices.db` y conserva los cambios entre reinicios. Las migraciones se aplican al iniciar; `scripts/schema.sql` se incluye como referencia. La aplicación no requiere credenciales.

La revisión de los requisitos del PDF está en [Verificación del ejercicio](docs/verificacion-ejercicio.md).
