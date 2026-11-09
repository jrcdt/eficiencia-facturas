using InvoiceManager.Api.Application;
using InvoiceManager.Api.Domain;
using InvoiceManager.Api.Infrastructure;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();
builder.Services.AddSingleton<IBusinessClock, BusinessClock>();
builder.Services.AddScoped<InvoiceService>();
builder.Services.AddDbContext<InvoiceDbContext>(options => options.UseSqlite(
    builder.Configuration.GetConnectionString("Invoices") ?? "Data Source=App_Data/invoices.db"));
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? ["http://localhost:5173"])
    .AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => options.SwaggerDoc("v1", new OpenApiInfo
{
    Title = "Eficiencia · API de facturas", Version = "v1",
    Description = "Importación atómica de JSON, consultas desde vistas SQLite, pagos, notas de crédito y reportes. Los montos usan hasta dos decimales."
}));
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 10 * 1024 * 1024);

var app = builder.Build();
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var error = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    var status = error switch
    {
        RuleException rule => rule.StatusCode,
        BadHttpRequestException bad => bad.StatusCode,
        SqliteException { SqliteErrorCode: 19 } => 409,
        DbUpdateException { InnerException: SqliteException { SqliteErrorCode: 19 } } => 409,
        SqliteException { SqliteErrorCode: 5 or 6 } => 409,
        _ => 500
    };
    var detail = error is RuleException ? error.Message : status switch
    {
        400 => "La solicitud es inválida. Verifica los parámetros y el cuerpo enviado.",
        413 => "El archivo supera el máximo de 10 MB.",
        409 => "La operación entra en conflicto con los datos actuales. Actualiza e inténtalo nuevamente.",
        _ => "No se pudo completar la operación. Revisa los registros del servidor."
    };
    await Results.Problem(statusCode: status, title: status == 500 ? "Error del servidor" : "No se pudo completar la solicitud",
        detail: detail).ExecuteAsync(context);
}));
app.UseCors();
app.UseSwagger();
app.UseSwaggerUI(options => options.DocumentTitle = "Eficiencia · Swagger");

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<InvoiceDbContext>();
    var path = new SqliteConnectionStringBuilder(db.Database.GetConnectionString()).DataSource;
    if (path != ":memory:") Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
    await db.Database.MigrateAsync();
    await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
}

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" })).WithTags("Sistema");

app.MapGet("/api/invoices", async (InvoiceService service, CancellationToken ct, long? number = null,
    string? invoiceStatus = null, string? paymentStatus = null, int page = 1, int pageSize = 10) =>
    Results.Ok(await service.Search(number, invoiceStatus, paymentStatus, page, pageSize, false, ct)))
    .WithTags("Facturas").WithSummary("Buscar facturas consistentes desde una vista SQL")
    .Produces<PageResult<InvoiceListItem>>().ProducesProblem(400);

app.MapGet("/api/invoices/{number:long}", async (long number, InvoiceService service, CancellationToken ct) =>
    Results.Ok(await service.Detail(number, ct))).WithTags("Facturas")
    .WithSummary("Obtener detalle, productos, pago y notas de crédito")
    .Produces<InvoiceDetail>().ProducesProblem(404);

app.MapDelete("/api/invoices/{number:long}", async (long number, InvoiceService service, CancellationToken ct) =>
{
    await service.DeleteInvoice(number, ct);
    return Results.NoContent();
}).WithTags("Facturas").WithSummary("Eliminar una factura y sus productos, pago y notas de crédito")
    .Produces(204).ProducesProblem(404).ProducesProblem(409);

app.MapDelete("/api/invoices", async ([FromBody] DeleteAllInvoicesRequest request, InvoiceService service, CancellationToken ct) =>
    Results.Ok(await service.DeleteAll(request.Confirmation, ct)))
    .WithTags("Facturas").WithSummary("Eliminar todas las facturas, incluidas las inconsistentes; requiere confirmation: ELIMINAR")
    .Produces<DeleteInvoicesResult>().ProducesProblem(400).ProducesProblem(422).ProducesProblem(409);

app.MapPost("/api/invoices/{number:long}/credit-notes", async (long number, CreateCreditNoteRequest request,
    InvoiceService service, CancellationToken ct) =>
{
    var note = await service.AddCredit(number, request.Amount, ct);
    return Results.Created($"/api/invoices/{number}", note);
}).WithTags("Notas de crédito").WithSummary("Crear NC con fecha automática y validación atómica del saldo")
    .Produces<CreditNoteItem>(201).ProducesProblem(404).ProducesProblem(422).ProducesProblem(409);

app.MapPost("/api/invoices/{number:long}/payments", async (long number, RegisterPaymentRequest request,
    InvoiceService service, CancellationToken ct) =>
{
    await service.RegisterPayment(number, request.Method, ct);
    return Results.NoContent();
}).WithTags("Pagos").WithSummary("Registrar el pago completo del saldo con fecha automática")
    .Produces(204).ProducesProblem(404).ProducesProblem(409).ProducesProblem(422);

app.MapPost("/api/imports", async (IFormFile file, InvoiceService service, CancellationToken ct) =>
{
    if (file.Length is <= 0 or > 10 * 1024 * 1024)
        throw new RuleException("Selecciona un archivo JSON de hasta 10 MB.", file.Length > 10 * 1024 * 1024 ? 413 : 422);
    if (!Path.GetExtension(file.FileName).Equals(".json", StringComparison.OrdinalIgnoreCase))
        throw new RuleException("El archivo debe tener extensión .json.");
    await using var stream = file.OpenReadStream();
    var result = await service.Import(stream, ct);
    return Results.Ok(result);
}).DisableAntiforgery().WithTags("Importación").WithSummary("Importar JSON; conservar inconsistentes y omitir existentes")
    .Produces<ImportResult>().ProducesProblem(422).ProducesProblem(413).ProducesProblem(409);

app.MapGet("/api/reports/summary", async (InvoiceService service, CancellationToken ct) =>
    Results.Ok(await service.Dashboard(ct))).WithTags("Reportes").WithSummary("Totales y porcentajes de pago sobre facturas consistentes")
    .Produces<DashboardReport>();
app.MapGet("/api/reports/overdue", async (InvoiceService service, CancellationToken ct, int page = 1, int pageSize = 10) =>
    Results.Ok(await service.Search(null, null, null, page, pageSize, true, ct)))
    .WithTags("Reportes").WithSummary("Más de 30 días vencidas, sin pago y sin ninguna NC")
    .Produces<PageResult<InvoiceListItem>>().ProducesProblem(400);
app.MapGet("/api/reports/inconsistent", async (InvoiceService service, CancellationToken ct, int page = 1, int pageSize = 10) =>
    Results.Ok(await service.Inconsistent(page, pageSize, ct)))
    .WithTags("Reportes").WithSummary("Facturas excluidas por diferencia entre total declarado y productos")
    .Produces<PageResult<InconsistentItem>>().ProducesProblem(400);

app.Run();
public partial class Program;
