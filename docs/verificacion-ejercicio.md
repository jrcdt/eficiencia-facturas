# Verificación del ejercicio técnico

Revisión de la implementación contra las dos páginas de «Ejercicio Técnico - Fullstack Eficiencia.pdf».

| Punto del PDF | Implementación y comprobación |
| --- | --- |
| Importar JSON en tiempo de ejecución | Botón **Importar JSON** y `POST /api/imports`. El archivo recibido importa 50 facturas. |
| `invoice_number` único | Índice único en SQLite. Duplicados dentro del archivo se rechazan; números ya guardados se omiten. Pruebas de duplicados e idempotencia. |
| Validar total contra suma de subtotales | Las facturas 41 y 48 se guardan como inconsistentes y aparecen en su reporte. Las otras 48 quedan activas. |
| Excluir inconsistentes del sistema activo | No aparecen en búsqueda, detalle activo ni resumen de pagos; tampoco aceptan pagos ni NC. Su eliminación solo ocurre mediante la acción explícita de borrado añadida para las pruebas manuales. |
| Estados Issued, Partial y Cancelled automáticos | La vista SQL calcula el estado según las NC. Las pruebas comprueban la transición Emitida → Parcial → Anulada. |
| Estados Pending, Overdue y Paid automáticos | Se evalúan fecha de vencimiento y pago registrado. Se prueban vencimiento hoy, futuro, pasado y prioridad del pago. La fecha de negocio usa America/Santiago. |
| Buscar por número, estado de factura y estado de pago usando vistas | Filtros combinables en React y API sobre `vw_invoice_overview`, mapeada por EF Core. Incluye paginación. |
| Agregar NC con fecha automática | **Crear NC** en la tabla o **Ver detalle → Agregar nota de crédito**. La API asigna número y fecha; el detalle muestra su historial. |
| Impedir NC mayores al saldo | Validación en formulario y API. Una transacción serializa las escrituras. Las pruebas cubren exceso, cero, negativos, decimales y solicitudes simultáneas. |
| Consistentes con más de 30 días vencidas, sin pago ni NC | **Reportes → Vencidas +30 días**. Se prueba que exactamente 30 días no entra, que 31 sí entra y que pagos, NC e inconsistencias excluyen la factura. |
| Resumen total y porcentaje por estado de pago | **Reportes → Resumen de pagos** muestra cantidad, porcentaje y monto por Paid, Pending y Overdue. Se prueba también una base vacía. |
| Reporte de inconsistencias | **Reportes → Inconsistencias** muestra total declarado, suma de productos y diferencia. |
| Backend .NET 6 u 8 y API REST | ASP.NET Core sobre .NET 8, con endpoints de consulta, importación, NC, pagos y borrado. |
| Swagger y códigos HTTP | `/swagger` documenta los endpoints. Pruebas de 200, 201, 204, 400, 404, 409 y 422; el límite de carga tiene respuesta 413. |
| SQLite y Entity Framework Core | Base SQLite persistente, migración, snapshot y vista SQL. Las pruebas de API usan SQLite real aislado. |
| React e interfaz con validaciones | React y TypeScript. Prueba en Chromium de escritorio y móvil: importación, filtros, NC, pago, reportes, confirmaciones de borrado y reimportación. |
| Patrón de diseño mencionado en README | Service Layer con inyección de dependencias, separado en Domain, Application e Infrastructure. |
| README de instalación y ejecución | Instrucciones de inicio manual en dos terminales, direcciones de aplicación y Swagger, carga de datos y detención. |
| Código de frontend, backend y scripts de base | Incluidos en `frontend/`, `backend/` y `scripts/schema.sql`; las migraciones se aplican automáticamente. |

La autenticación es opcional en el PDF y no se habilitó; no se requieren credenciales.

## Comprobar la nueva opción de borrado

1. **Una factura:** Ver detalle → Eliminar factura → confirmar. Al importar el mismo JSON, solo vuelve a cargarse ese número y los demás se omiten.
2. **Todas:** Eliminar facturas → escribir ELIMINAR → Eliminar todas. Incluye inconsistentes y elimina productos, pagos y NC relacionados. No borra el esquema de la base ni el archivo JSON.
3. **Reimportar:** el archivo original vuelve a producir 50 importadas, 48 activas y 2 inconsistentes.

Las pruebas usan bases independientes de los datos que se revisan manualmente en la aplicación.
