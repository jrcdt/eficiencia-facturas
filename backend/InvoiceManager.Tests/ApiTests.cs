using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using InvoiceManager.Api.Application;
using InvoiceManager.Api.Domain;
using InvoiceManager.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace InvoiceManager.Tests;

public sealed class ApiTests : IDisposable
{
    private readonly ApiFactory factory = new();
    private readonly HttpClient client;
    public ApiTests() => client = factory.CreateClient();
    public void Dispose() { client.Dispose(); factory.Dispose(); }

    private async Task<HttpResponseMessage> Import(string? json = null)
    {
        json ??= await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures/invoices.json"));
        using var body = new MultipartFormDataContent();
        body.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(json)), "file", "invoices.json");
        return await client.PostAsync("/api/imports", body);
    }

    private static string Document(params JsonObject[] invoices) =>
        new JsonObject { ["invoices"] = new JsonArray(invoices.Select(x => (JsonNode)x).ToArray()) }.ToJsonString();

    private static JsonObject Invoice(long number = 101, string due = "2026-11-10", decimal total = 100) => new()
    {
        ["invoice_number"] = number, ["invoice_date"] = "2026-01-01", ["payment_due_date"] = due,
        ["total_amount"] = total, ["invoice_status"] = "cancelled", ["payment_status"] = "Paid",
        ["invoice_detail"] = new JsonArray(new JsonObject { ["product_name"] = "Producto", ["quantity"] = 1,
            ["unit_price"] = total, ["subtotal"] = total }),
        ["customer"] = new JsonObject { ["customer_run"] = "12345678-9", ["customer_name"] = "Cliente de prueba",
            ["customer_email"] = "cliente@example.com" },
        ["invoice_payment"] = new JsonObject { ["payment_method"] = null, ["payment_date"] = null },
        ["invoice_credit_note"] = new JsonArray()
    };

    [Fact]
    public async Task DatasetImports50AndExcludesExactly41And48()
    {
        var response = await Import();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<ImportResult>())!;
        Assert.Equal(50, result.Imported); Assert.Equal(48, result.Active); Assert.Equal(2, result.Inconsistent);
        Assert.Equal(new long[] { 41, 48 }, result.InconsistentNumbers);
        var active = (await client.GetFromJsonAsync<PageResult<InvoiceListItem>>("/api/invoices?pageSize=100"))!;
        Assert.Equal(48, active.TotalCount);
        Assert.DoesNotContain(active.Items, x => x.Number is 41 or 48);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/invoices/41")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/invoices/41/credit-notes", new { amount = 1 })).StatusCode);
        var report = (await client.GetFromJsonAsync<PageResult<InconsistentItem>>("/api/reports/inconsistent"))!;
        Assert.Equal(2, report.TotalCount);
        Assert.Equal(-137165m, report.Items.Single(x => x.Number == 41).Difference);
        Assert.Equal(-6m, report.Items.Single(x => x.Number == 48).Difference);
        var summary = (await client.GetFromJsonAsync<DashboardReport>("/api/reports/summary"))!;
        Assert.Equal(13, summary.PaymentSummary.Single(x => x.Status == "Paid").Count);
        Assert.Equal(35, summary.PaymentSummary.Single(x => x.Status == "Overdue").Count);
        Assert.Equal(0, summary.PaymentSummary.Single(x => x.Status == "Pending").Count);
        Assert.Equal(30, summary.OverdueWithoutActivityCount);
    }

    [Fact]
    public async Task ReimportIsIdempotentAndPreservesNewCreditNotes()
    {
        Assert.Equal(HttpStatusCode.OK, (await Import()).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/invoices/1/credit-notes", new { amount = 10 })).StatusCode);
        var result = (await (await Import()).Content.ReadFromJsonAsync<ImportResult>())!;
        Assert.Equal(0, result.Imported); Assert.Equal(50, result.Skipped);
        var detail = (await client.GetFromJsonAsync<InvoiceDetail>("/api/invoices/1"))!;
        Assert.Equal(10, detail.Invoice.CreditTotal);
    }

    [Fact]
    public async Task CreditNotesTransitionIssuedToPartialToCancelled()
    {
        await Import(Document(Invoice()));
        var before = (await client.GetFromJsonAsync<InvoiceDetail>("/api/invoices/101"))!;
        Assert.Equal("Issued", before.Invoice.InvoiceStatus);
        Assert.Equal("Pending", before.Invoice.PaymentStatus);
        var response = await client.PostAsJsonAsync("/api/invoices/101/credit-notes", new { amount = 40 });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var note = (await response.Content.ReadFromJsonAsync<CreditNoteItem>())!;
        Assert.Equal(new DateOnly(2026, 11, 9), note.CreatedOn);
        var partial = (await client.GetFromJsonAsync<InvoiceDetail>("/api/invoices/101"))!;
        Assert.Equal("Partial", partial.Invoice.InvoiceStatus); Assert.Equal(60, partial.Invoice.Balance);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/invoices/101/credit-notes", new { amount = 60 })).StatusCode);
        var cancelled = (await client.GetFromJsonAsync<InvoiceDetail>("/api/invoices/101"))!;
        Assert.Equal("Cancelled", cancelled.Invoice.InvoiceStatus); Assert.Equal(0, cancelled.Invoice.Balance);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsJsonAsync("/api/invoices/101/credit-notes", new { amount = 1 })).StatusCode);
    }

    [Theory]
    [InlineData(-1)] [InlineData(0)] [InlineData(100.01)] [InlineData(0.001)]
    public async Task InvalidCreditAmountsNeverChangeTheInvoice(decimal amount)
    {
        await Import(Document(Invoice()));
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await client.PostAsJsonAsync("/api/invoices/101/credit-notes", new { amount })).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<InvoiceDetail>("/api/invoices/101"))!.CreditNotes);
    }

    [Fact]
    public async Task ConcurrentCreditsCannotExceedBalance()
    {
        await Import(Document(Invoice()));
        var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ =>
            client.PostAsJsonAsync("/api/invoices/101/credit-notes", new { amount = 70 })));
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.Created);
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.UnprocessableEntity);
        var detail = (await client.GetFromJsonAsync<InvoiceDetail>("/api/invoices/101"))!;
        Assert.Single(detail.CreditNotes); Assert.Equal(30, detail.Invoice.Balance);
    }

    [Fact]
    public async Task DuplicateNumbersWithinAFileRejectTheEntireImport()
    {
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Import(Document(Invoice(), Invoice()))).StatusCode);
        Assert.Equal(0, (await client.GetFromJsonAsync<DashboardReport>("/api/reports/summary"))!.ActiveCount);
    }

    [Fact]
    public async Task InvalidLaterRowRollsBackAllRows()
    {
        var invalid = Invoice(102); invalid["invoice_detail"] = null;
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Import(Document(Invoice(), invalid))).StatusCode);
        Assert.Equal(0, (await client.GetFromJsonAsync<DashboardReport>("/api/reports/summary"))!.ActiveCount);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{}")]
    [InlineData("{\"invoices\":[]}")]
    [InlineData("{\"invoices\":[null]}")]
    public async Task MalformedJsonReturns422(string json) =>
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Import(json)).StatusCode);

    [Fact]
    public async Task OverdueReportUsesStrict30DayBoundaryAndExcludesAnyCreditOrPayment()
    {
        var paid = Invoice(104, "2026-10-01");
        paid["invoice_payment"] = new JsonObject { ["payment_date"] = "2026-10-02", ["payment_method"] = "Efectivo" };
        var credited = Invoice(105, "2026-10-01");
        credited["invoice_credit_note"] = new JsonArray(new JsonObject { ["credit_note_number"] = 1,
            ["credit_note_date"] = "2026-10-02", ["credit_note_amount"] = 1 });
        var inconsistent = Invoice(106, "2026-10-01"); inconsistent["total_amount"] = 99;
        await Import(Document(Invoice(101, "2026-10-09"), Invoice(102, "2026-10-10"),
            Invoice(103, "2026-10-11"), paid, credited, inconsistent));
        var report = (await client.GetFromJsonAsync<PageResult<InvoiceListItem>>("/api/reports/overdue"))!;
        Assert.Single(report.Items); Assert.Equal(101, report.Items[0].Number); Assert.Equal(31, report.Items[0].DaysOverdue);
    }

    [Fact]
    public async Task CombinedFiltersAndPaginationAreAppliedInSql()
    {
        await Import(Document(Invoice(101, "2026-11-08"), Invoice(102), Invoice(103)));
        await client.PostAsJsonAsync("/api/invoices/101/credit-notes", new { amount = 10 });
        var response = (await client.GetFromJsonAsync<PageResult<InvoiceListItem>>(
            "/api/invoices?number=101&invoiceStatus=Partial&paymentStatus=Overdue&pageSize=1"))!;
        Assert.Single(response.Items); Assert.Equal(101, response.Items[0].Number);
        var page = (await client.GetFromJsonAsync<PageResult<InvoiceListItem>>("/api/invoices?page=2&pageSize=2"))!;
        Assert.Single(page.Items); Assert.Equal(3, page.TotalCount);
    }

    [Fact]
    public async Task SummaryUsesOnlyConsistentInvoicesAndHandlesEmptyDatabase()
    {
        var empty = (await client.GetFromJsonAsync<DashboardReport>("/api/reports/summary"))!;
        Assert.All(empty.PaymentSummary, x => Assert.Equal(0, x.Percentage));
        var inconsistent = Invoice(104); inconsistent["total_amount"] = 99;
        await Import(Document(Invoice(101, "2026-11-08"), Invoice(102, "2026-11-09"), Invoice(103), inconsistent));
        await client.PostAsJsonAsync("/api/invoices/103/payments", new { method = "Efectivo" });
        var summary = (await client.GetFromJsonAsync<DashboardReport>("/api/reports/summary"))!;
        Assert.Equal(3, summary.ActiveCount); Assert.Equal(1, summary.InconsistentCount);
        Assert.Equal(300, summary.TotalAmount); Assert.Equal(200, summary.OutstandingAmount);
        Assert.All(summary.PaymentSummary, x => { Assert.Equal(1, x.Count); Assert.Equal(33.33m, x.Percentage); });
    }

    [Fact]
    public async Task PaymentIsRecordedAndCannotBeRegisteredTwice()
    {
        await Import(Document(Invoice(101, "2026-11-08")));
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/invoices/101/payments", new { method = "Transferencia" })).StatusCode);
        var detail = (await client.GetFromJsonAsync<InvoiceDetail>("/api/invoices/101"))!;
        Assert.Equal("Paid", detail.Invoice.PaymentStatus); Assert.Equal(new DateOnly(2026, 11, 9), detail.PaidOn);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/invoices/101/payments", new { method = "Transferencia" })).StatusCode);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=101")]
    [InlineData("invoiceStatus=invalid")]
    [InlineData("paymentStatus=invalid")]
    [InlineData("number=-1")]
    public async Task InvalidQueryReturns400(string query) =>
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/invoices?{query}")).StatusCode);

    [Fact]
    public async Task SwaggerDescribesTheUploadAndMutationEndpoints()
    {
        var doc = await client.GetFromJsonAsync<JsonObject>("/swagger/v1/swagger.json");
        Assert.NotNull(doc!["paths"]!["/api/imports"]);
        Assert.NotNull(doc["paths"]!["/api/invoices/{number}/credit-notes"]);
    }

    [Fact]
    public async Task DecimalCreditsUseExactCentsAndCancelWithoutResiduals()
    {
        await Import(Document(Invoice(total: 0.30m)));
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/invoices/101/credit-notes", new { amount = 0.10m })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/invoices/101/credit-notes", new { amount = 0.20m })).StatusCode);
        var detail = (await client.GetFromJsonAsync<InvoiceDetail>("/api/invoices/101"))!;
        Assert.Equal(0, detail.Invoice.Balance);
        Assert.Equal("Cancelled", detail.Invoice.InvoiceStatus);
    }

    [Fact]
    public async Task ExcessiveImportedCreditsRejectTheWholeFile()
    {
        var invoice = Invoice();
        invoice["invoice_credit_note"] = new JsonArray(new JsonObject { ["credit_note_number"] = 1,
            ["credit_note_date"] = "2026-10-02", ["credit_note_amount"] = 101 });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Import(Document(invoice))).StatusCode);
        Assert.Equal(0, (await client.GetFromJsonAsync<DashboardReport>("/api/reports/summary"))!.ActiveCount);
    }

    private Task<HttpResponseMessage> DeleteAll(string? confirmation = "ELIMINAR") => client.SendAsync(
        new HttpRequestMessage(HttpMethod.Delete, "/api/invoices")
        {
            Content = JsonContent.Create(new { confirmation })
        });

    [Fact]
    public async Task DeleteOneRemovesItsRelationsAndAllowsReimportWithoutChangingOtherInvoices()
    {
        await Import(Document(Invoice(101), Invoice(102)));
        await client.PostAsJsonAsync("/api/invoices/101/credit-notes", new { amount = 10 });
        await client.PostAsJsonAsync("/api/invoices/101/payments", new { method = "Efectivo" });
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/invoices/101")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/invoices/101")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/invoices/102")).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<InvoiceDbContext>();
            Assert.Equal(1, await db.Invoices.CountAsync());
            Assert.Equal(1, await db.Set<InvoiceLine>().CountAsync());
            Assert.Empty(await db.CreditNotes.ToListAsync());
        }
        var imported = (await (await Import(Document(Invoice(101), Invoice(102)))).Content.ReadFromJsonAsync<ImportResult>())!;
        Assert.Equal(1, imported.Imported); Assert.Equal(1, imported.Skipped);
        var detail = (await client.GetFromJsonAsync<InvoiceDetail>("/api/invoices/101"))!;
        Assert.Null(detail.PaidOn); Assert.Empty(detail.CreditNotes); Assert.Equal(100, detail.Invoice.Balance);
    }

    [Fact]
    public async Task ExplicitDeletionCanRemoveAnInconsistentInvoice()
    {
        await Import();
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/invoices/41")).StatusCode);
        var report = (await client.GetFromJsonAsync<PageResult<InconsistentItem>>("/api/reports/inconsistent"))!;
        Assert.Single(report.Items); Assert.Equal(48, report.Items[0].Number);
        Assert.Equal(48, (await client.GetFromJsonAsync<DashboardReport>("/api/reports/summary"))!.ActiveCount);
    }

    [Fact]
    public async Task DeleteAllIncludesInconsistentInvoicesAndRelationsAndPreservesSchemaForReimport()
    {
        await Import();
        var response = await DeleteAll();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(50, (await response.Content.ReadFromJsonAsync<DeleteInvoicesResult>())!.DeletedCount);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<InvoiceDbContext>();
            Assert.Empty(await db.Invoices.ToListAsync());
            Assert.Empty(await db.CreditNotes.ToListAsync());
            Assert.Empty(await db.Set<InvoiceLine>().ToListAsync());
        }
        var summary = (await client.GetFromJsonAsync<DashboardReport>("/api/reports/summary"))!;
        Assert.Equal(0, summary.ActiveCount); Assert.Equal(0, summary.InconsistentCount);
        Assert.All(summary.PaymentSummary, x => Assert.Equal(0, x.Percentage));
        var result = (await (await Import()).Content.ReadFromJsonAsync<ImportResult>())!;
        Assert.Equal(50, result.Imported); Assert.Equal(48, result.Active); Assert.Equal(2, result.Inconsistent);
    }

    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData("eliminar")]
    public async Task DeleteAllRequiresExplicitConfirmationAndLeavesDataUntouched(string? confirmation)
    {
        await Import(Document(Invoice()));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await DeleteAll(confirmation)).StatusCode);
        Assert.Equal(1, (await client.GetFromJsonAsync<DashboardReport>("/api/reports/summary"))!.ActiveCount);
    }

    [Fact]
    public async Task DeletingMissingInvoiceReturns404() =>
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync("/api/invoices/999")).StatusCode);

    [Fact]
    public async Task DeletingAllFromEmptyDatabaseReturnsZero() =>
        Assert.Equal(0, (await (await DeleteAll()).Content.ReadFromJsonAsync<DeleteInvoicesResult>())!.DeletedCount);
}
