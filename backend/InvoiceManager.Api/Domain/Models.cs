namespace InvoiceManager.Api.Domain;

public sealed class Invoice
{
    public long Id { get; set; }
    public long Number { get; set; }
    public DateOnly IssuedOn { get; set; }
    public DateOnly DueOn { get; set; }
    public long TotalAmountCents { get; set; }
    public bool IsConsistent { get; set; }
    public string CustomerRun { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public string CustomerEmail { get; set; } = "";
    public string? PaymentMethod { get; set; }
    public DateOnly? PaidOn { get; set; }
    public List<InvoiceLine> Lines { get; set; } = [];
    public List<CreditNote> CreditNotes { get; set; } = [];
}

public sealed class InvoiceLine
{
    public long Id { get; set; }
    public long InvoiceId { get; set; }
    public string ProductName { get; set; } = "";
    public long UnitPriceCents { get; set; }
    public int Quantity { get; set; }
    public long SubtotalCents { get; set; }
}

public sealed class CreditNote
{
    public long Id { get; set; }
    public long InvoiceId { get; set; }
    public long Number { get; set; }
    public DateOnly CreatedOn { get; set; }
    public long AmountCents { get; set; }
}

// Read-only projection backed by an actual SQLite view.
public sealed class InvoiceOverview
{
    public long Id { get; set; }
    public long Number { get; set; }
    public DateOnly IssuedOn { get; set; }
    public DateOnly DueOn { get; set; }
    public long TotalAmountCents { get; set; }
    public long ProductTotalCents { get; set; }
    public long CreditTotalCents { get; set; }
    public long BalanceCents { get; set; }
    public bool IsConsistent { get; set; }
    public string CustomerName { get; set; } = "";
    public string CustomerRun { get; set; } = "";
    public string CustomerEmail { get; set; } = "";
    public DateOnly? PaidOn { get; set; }
    public string? PaymentMethod { get; set; }
    public string InvoiceStatus { get; set; } = "";
    public int CreditNoteCount { get; set; }
}
