namespace InvoiceManager.Api.Application;

public sealed record InvoiceListItem(
    long Number, DateOnly IssuedOn, DateOnly DueOn, string CustomerName, string CustomerRun,
    decimal TotalAmount, decimal CreditTotal, decimal Balance, string InvoiceStatus,
    string PaymentStatus, int DaysOverdue);
public sealed record PageResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);
public sealed record InvoiceDetail(InvoiceListItem Invoice, string CustomerEmail, string? PaymentMethod,
    DateOnly? PaidOn, IReadOnlyList<LineItem> Lines, IReadOnlyList<CreditNoteItem> CreditNotes);
public sealed record LineItem(string ProductName, decimal UnitPrice, int Quantity, decimal Subtotal);
public sealed record CreditNoteItem(long Number, DateOnly CreatedOn, decimal Amount);
public sealed record CreateCreditNoteRequest(decimal Amount);
public sealed record RegisterPaymentRequest(string Method);
public sealed record DeleteAllInvoicesRequest(string? Confirmation);
public sealed record DeleteInvoicesResult(int DeletedCount);
public sealed record PaymentSummaryItem(string Status, int Count, decimal Percentage, decimal Amount);
public sealed record DashboardReport(int ActiveCount, int InconsistentCount, int OverdueWithoutActivityCount,
    decimal TotalAmount, decimal CreditTotal, decimal OutstandingAmount, DateOnly AsOf,
    IReadOnlyList<PaymentSummaryItem> PaymentSummary);
public sealed record InconsistentItem(long Number, string CustomerName, decimal DeclaredTotal,
    decimal ProductTotal, decimal Difference);
public sealed record ImportResult(int Imported, int Active, int Inconsistent, int Skipped,
    IReadOnlyList<long> SkippedNumbers, IReadOnlyList<long> InconsistentNumbers);

// JSON fields use the naming in the supplied dataset; source statuses are deliberately ignored.
public sealed class ImportDocument
{
    public required List<ImportInvoice> Invoices { get; init; }
}
public sealed class ImportInvoice
{
    public required long InvoiceNumber { get; init; }
    public required DateOnly InvoiceDate { get; init; }
    public required DateOnly PaymentDueDate { get; init; }
    public required decimal TotalAmount { get; init; }
    public required List<ImportLine> InvoiceDetail { get; init; }
    public required ImportCustomer Customer { get; init; }
    public ImportPayment? InvoicePayment { get; init; }
    public List<ImportCredit>? InvoiceCreditNote { get; init; }
}
public sealed class ImportLine
{
    public required string ProductName { get; init; }
    public required decimal UnitPrice { get; init; }
    public required int Quantity { get; init; }
    public required decimal Subtotal { get; init; }
}
public sealed class ImportCustomer
{
    public required string CustomerRun { get; init; }
    public required string CustomerName { get; init; }
    public required string CustomerEmail { get; init; }
}
public sealed class ImportPayment
{
    public string? PaymentMethod { get; init; }
    public DateOnly? PaymentDate { get; init; }
}
public sealed class ImportCredit
{
    public required long CreditNoteNumber { get; init; }
    public required DateOnly CreditNoteDate { get; init; }
    public required decimal CreditNoteAmount { get; init; }
}
