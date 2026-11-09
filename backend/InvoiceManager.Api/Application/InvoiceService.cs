using System.Data;
using System.Text.Json;
using InvoiceManager.Api.Domain;
using InvoiceManager.Api.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InvoiceManager.Api.Application;

public sealed class InvoiceService(InvoiceDbContext db, IBusinessClock clock)
{
    private IQueryable<InvoiceOverview> Active => db.Overview.Where(x => x.IsConsistent);

    public async Task<PageResult<InvoiceListItem>> Search(long? number, string? invoiceStatus,
        string? paymentStatus, int page, int pageSize, bool overdueWithoutActivity, CancellationToken ct)
    {
        if (page < 1 || page > 1_000_000 || pageSize is < 1 or > 100)
            throw new RuleException("Página inválida. El tamaño debe estar entre 1 y 100.", 400);
        if (number is <= 0) throw new RuleException("El número de factura debe ser positivo.", 400);
        if (invoiceStatus is not null && !InvoiceRules.InvoiceStatuses.Contains(invoiceStatus))
            throw new RuleException("Estado de factura inválido.", 400);
        if (paymentStatus is not null && !InvoiceRules.PaymentStatuses.Contains(paymentStatus))
            throw new RuleException("Estado de pago inválido.", 400);

        var today = clock.Today;
        var query = Active;
        if (number.HasValue) query = query.Where(x => x.Number == number);
        if (invoiceStatus is not null) query = query.Where(x => x.InvoiceStatus == invoiceStatus);
        if (paymentStatus == "Paid") query = query.Where(x => x.PaidOn != null);
        if (paymentStatus == "Pending") query = query.Where(x => x.PaidOn == null && x.DueOn >= today);
        if (paymentStatus == "Overdue") query = query.Where(x => x.PaidOn == null && x.DueOn < today);
        if (overdueWithoutActivity)
        {
            var cutoff = today.AddDays(-30);
            query = query.Where(x => x.PaidOn == null && x.CreditNoteCount == 0 && x.DueOn < cutoff);
        }
        var count = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.IssuedOn).ThenByDescending(x => x.Number)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(rows.Select(ToItem).ToList(), count, page, pageSize);
    }

    public async Task<InvoiceDetail> Detail(long number, CancellationToken ct)
    {
        var row = await Active.SingleOrDefaultAsync(x => x.Number == number, ct)
            ?? throw new RuleException("Factura activa no encontrada.", 404);
        var invoice = await db.Invoices.AsNoTracking().Include(x => x.Lines).Include(x => x.CreditNotes)
            .AsSplitQuery().SingleAsync(x => x.Id == row.Id, ct);
        return new(ToItem(row), row.CustomerEmail, row.PaymentMethod, row.PaidOn,
            invoice.Lines.OrderBy(x => x.Id).Select(x => new LineItem(x.ProductName, x.UnitPriceCents / 100m,
                x.Quantity, x.SubtotalCents / 100m)).ToList(),
            invoice.CreditNotes.OrderByDescending(x => x.CreatedOn).ThenByDescending(x => x.Number)
                .Select(x => new CreditNoteItem(x.Number, x.CreatedOn, x.AmountCents / 100m)).ToList());
    }

    public async Task<CreditNoteItem> AddCredit(long number, decimal amount, CancellationToken ct)
    {
        var cents = InvoiceRules.ToCents(amount);
        // BEGIN IMMEDIATE serializes writers before reading the balance, including across processes.
        await using var transaction = await BeginWrite(ct);
        var invoice = await db.Invoices.Include(x => x.CreditNotes)
            .SingleOrDefaultAsync(x => x.Number == number && x.IsConsistent, ct)
            ?? throw new RuleException("Factura activa no encontrada.", 404);
        InvoiceRules.ValidateCredit(cents, invoice.TotalAmountCents - invoice.CreditNotes.Sum(x => x.AmountCents));
        var next = (await db.CreditNotes.MaxAsync(x => (long?)x.Number, ct) ?? 0) + 1;
        var credit = new CreditNote { InvoiceId = invoice.Id, Number = next, CreatedOn = clock.Today, AmountCents = cents };
        db.CreditNotes.Add(credit);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(credit.Number, credit.CreatedOn, amount);
    }

    public async Task RegisterPayment(long number, string? method, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(method) || method.Trim().Length > 100)
            throw new RuleException("Ingresa un medio de pago de hasta 100 caracteres.");
        await using var transaction = await BeginWrite(ct);
        var invoice = await db.Invoices.Include(x => x.CreditNotes)
            .SingleOrDefaultAsync(x => x.Number == number && x.IsConsistent, ct)
            ?? throw new RuleException("Factura activa no encontrada.", 404);
        if (invoice.PaidOn.HasValue) throw new RuleException("La factura ya tiene un pago registrado.", 409);
        if (invoice.CreditNotes.Sum(x => x.AmountCents) == invoice.TotalAmountCents)
            throw new RuleException("La factura está anulada y no tiene saldo por pagar.");
        invoice.PaymentMethod = method.Trim();
        invoice.PaidOn = clock.Today;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task DeleteInvoice(long number, CancellationToken ct)
    {
        await using var transaction = await BeginWrite(ct);
        var deleted = await db.Invoices.Where(x => x.Number == number).ExecuteDeleteAsync(ct);
        if (deleted == 0) throw new RuleException("Factura no encontrada.", 404);
        await transaction.CommitAsync(ct);
    }

    public async Task<DeleteInvoicesResult> DeleteAll(string? confirmation, CancellationToken ct)
    {
        if (!string.Equals(confirmation, "ELIMINAR", StringComparison.Ordinal))
            throw new RuleException("Escribe ELIMINAR para confirmar el borrado de todas las facturas.");
        await using var transaction = await BeginWrite(ct);
        // Foreign keys cascade to products and credit notes. Payments belong to the invoice row.
        var deleted = await db.Invoices.ExecuteDeleteAsync(ct);
        await transaction.CommitAsync(ct);
        return new(deleted);
    }

    public async Task<DashboardReport> Dashboard(CancellationToken ct)
    {
        var today = clock.Today;
        var rows = await Active.ToListAsync(ct);
        var summary = InvoiceRules.PaymentStatuses.Select(status =>
        {
            var group = rows.Where(x => InvoiceRules.PaymentStatus(x.PaidOn, x.DueOn, today) == status).ToList();
            return new PaymentSummaryItem(status, group.Count,
                rows.Count == 0 ? 0 : Math.Round(group.Count * 100m / rows.Count, 2),
                group.Sum(x => x.TotalAmountCents) / 100m);
        }).ToList();
        return new(rows.Count, await db.Invoices.CountAsync(x => !x.IsConsistent, ct),
            rows.Count(x => x.PaidOn == null && x.CreditNoteCount == 0 && x.DueOn < today.AddDays(-30)),
            rows.Sum(x => x.TotalAmountCents) / 100m, rows.Sum(x => x.CreditTotalCents) / 100m,
            rows.Where(x => x.PaidOn == null).Sum(x => x.BalanceCents) / 100m, today, summary);
    }

    public async Task<PageResult<InconsistentItem>> Inconsistent(int page, int pageSize, CancellationToken ct)
    {
        if (page < 1 || page > 1_000_000 || pageSize is < 1 or > 100)
            throw new RuleException("Paginación inválida.", 400);
        var query = db.Overview.Where(x => !x.IsConsistent);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.Number).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(rows.Select(x => new InconsistentItem(x.Number, x.CustomerName, x.TotalAmountCents / 100m,
            x.ProductTotalCents / 100m, (x.TotalAmountCents - x.ProductTotalCents) / 100m)).ToList(), total, page, pageSize);
    }

    public async Task<ImportResult> Import(Stream stream, CancellationToken ct)
    {
        ImportDocument document;
        try
        {
            document = await JsonSerializer.DeserializeAsync<ImportDocument>(stream,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower }, ct)
                ?? throw new RuleException("El archivo JSON está vacío.");
        }
        catch (JsonException)
        {
            throw new RuleException("JSON inválido. Verifica los campos requeridos, los tipos y las fechas (AAAA-MM-DD).");
        }
        if (document.Invoices is null || document.Invoices.Count is < 1 or > 10000)
            throw new RuleException("El JSON debe contener un arreglo 'invoices' con entre 1 y 10.000 facturas.");
        if (document.Invoices.Any(x => x is null)) throw new RuleException("El arreglo contiene una factura nula.");
        if (document.Invoices.Select(x => x.InvoiceNumber).Distinct().Count() != document.Invoices.Count)
            throw new RuleException("El archivo contiene números de factura duplicados. No se importó ninguna factura.");

        // Validate the complete file before writing; malformed imports never partially commit.
        var invoices = document.Invoices.Select(MapImport).ToList();
        await using var transaction = await BeginWrite(ct);
        var existing = (await db.Invoices.Select(x => x.Number).ToListAsync(ct)).ToHashSet();
        var skipped = invoices.Where(x => existing.Contains(x.Number)).Select(x => x.Number).ToList();
        var incoming = invoices.Where(x => !existing.Contains(x.Number)).ToList();
        db.Invoices.AddRange(incoming);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        var inconsistent = incoming.Where(x => !x.IsConsistent).Select(x => x.Number).ToList();
        return new(incoming.Count, incoming.Count - inconsistent.Count, inconsistent.Count, skipped.Count, skipped, inconsistent);
    }

    private Invoice MapImport(ImportInvoice source)
    {
        void Require(bool valid, string message)
        {
            if (!valid) throw new RuleException($"Factura Nº {source.InvoiceNumber}: {message}");
        }
        Require(source.InvoiceNumber > 0, "el número debe ser positivo.");
        Require(source.TotalAmount > 0, "el total debe ser positivo.");
        Require(source.InvoiceDate != default && source.PaymentDueDate >= source.InvoiceDate, "fechas inválidas.");
        Require(source.Customer is not null && !string.IsNullOrWhiteSpace(source.Customer.CustomerName)
            && !string.IsNullOrWhiteSpace(source.Customer.CustomerRun)
            && !string.IsNullOrWhiteSpace(source.Customer.CustomerEmail), "los datos del cliente son obligatorios.");
        Require(source.Customer!.CustomerName.Length <= 200 && source.Customer.CustomerRun.Length <= 40
            && source.Customer.CustomerEmail.Length <= 254, "los datos del cliente exceden el largo permitido.");
        Require(source.InvoiceDetail is { Count: > 0 and <= 1000 }, "debe incluir entre 1 y 1.000 productos.");
        var lines = source.InvoiceDetail!.Select(line =>
        {
            Require(line is not null && !string.IsNullOrWhiteSpace(line.ProductName)
                && line.ProductName.Length <= 300 && line.Quantity > 0, "producto inválido.");
            return new InvoiceLine { ProductName = line!.ProductName, Quantity = line.Quantity,
                UnitPriceCents = InvoiceRules.ToCents(line.UnitPrice), SubtotalCents = InvoiceRules.ToCents(line.Subtotal) };
        }).ToList();
        var credits = source.InvoiceCreditNote ?? [];
        Require(credits.Count <= 1000, "demasiadas notas de crédito.");
        Require(credits.All(x => x is not null && x.CreditNoteNumber > 0 && x.CreditNoteAmount > 0
            && x.CreditNoteDate >= source.InvoiceDate), "nota de crédito inválida.");
        Require(credits.Select(x => x.CreditNoteNumber).Distinct().Count() == credits.Count, "notas de crédito duplicadas.");
        var notes = credits.Select(x => new CreditNote { Number = x.CreditNoteNumber, CreatedOn = x.CreditNoteDate,
            AmountCents = InvoiceRules.ToCents(x.CreditNoteAmount) }).ToList();
        var total = InvoiceRules.ToCents(source.TotalAmount);
        Require(notes.Sum(x => x.AmountCents) <= total, "las notas de crédito superan el total.");
        var payment = source.InvoicePayment;
        Require(payment is null || payment.PaymentDate.HasValue == !string.IsNullOrWhiteSpace(payment.PaymentMethod),
            "el pago debe incluir fecha y medio de pago, o ambos vacíos.");
        Require(payment?.PaymentDate is null || (payment.PaymentDate >= source.InvoiceDate && payment.PaymentDate <= clock.Today),
            "la fecha de pago debe estar entre la emisión y hoy.");
        Require(payment?.PaymentMethod is null || payment.PaymentMethod.Length <= 100, "medio de pago demasiado largo.");
        return new Invoice { Number = source.InvoiceNumber, IssuedOn = source.InvoiceDate, DueOn = source.PaymentDueDate,
            TotalAmountCents = total, IsConsistent = lines.Sum(x => x.SubtotalCents) == total,
            CustomerName = source.Customer.CustomerName.Trim(), CustomerRun = source.Customer.CustomerRun.Trim(),
            CustomerEmail = source.Customer.CustomerEmail.Trim(), Lines = lines, CreditNotes = notes,
            PaidOn = payment?.PaymentDate, PaymentMethod = payment?.PaymentMethod?.Trim() };
    }

    private InvoiceListItem ToItem(InvoiceOverview x) => new(x.Number, x.IssuedOn, x.DueOn,
        x.CustomerName, x.CustomerRun, x.TotalAmountCents / 100m, x.CreditTotalCents / 100m,
        x.BalanceCents / 100m, x.InvoiceStatus, InvoiceRules.PaymentStatus(x.PaidOn, x.DueOn, clock.Today),
        Math.Max(0, clock.Today.DayNumber - x.DueOn.DayNumber));

    private async Task<WriteTransaction> BeginWrite(CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        var connection = (SqliteConnection)db.Database.GetDbConnection();
        var transaction = connection.BeginTransaction(IsolationLevel.Serializable, deferred: false);
        try
        {
            var enlisted = (await db.Database.UseTransactionAsync(transaction, ct))!;
            return new WriteTransaction(transaction, enlisted);
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    // UseTransaction does not own the external SQLite transaction; dispose both on every path.
    private sealed class WriteTransaction(SqliteTransaction sqlite,
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction enlisted) : IAsyncDisposable
    {
        public Task CommitAsync(CancellationToken ct) => enlisted.CommitAsync(ct);
        public async ValueTask DisposeAsync()
        {
            await enlisted.DisposeAsync();
            await sqlite.DisposeAsync();
        }
    }
}
