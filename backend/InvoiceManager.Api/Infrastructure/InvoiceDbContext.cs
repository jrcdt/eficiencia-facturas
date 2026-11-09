using InvoiceManager.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace InvoiceManager.Api.Infrastructure;

public sealed class InvoiceDbContext(DbContextOptions<InvoiceDbContext> options) : DbContext(options)
{
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<CreditNote> CreditNotes => Set<CreditNote>();
    public DbSet<InvoiceOverview> Overview => Set<InvoiceOverview>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Invoice>(entity =>
        {
            entity.ToTable("Invoices", table => table.HasCheckConstraint("CK_Invoice_Total", "TotalAmountCents > 0"));
            entity.HasIndex(x => x.Number).IsUnique();
            entity.HasIndex(x => new { x.IsConsistent, x.DueOn });
            entity.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.CreditNotes).WithOne().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<InvoiceLine>().ToTable("InvoiceLines", table =>
            table.HasCheckConstraint("CK_Line_Amounts", "UnitPriceCents >= 0 AND SubtotalCents >= 0 AND Quantity > 0"));
        model.Entity<CreditNote>(entity =>
        {
            entity.ToTable("CreditNotes", table => table.HasCheckConstraint("CK_Credit_Amount", "AmountCents > 0"));
            entity.HasIndex(x => new { x.InvoiceId, x.Number }).IsUnique();
        });
        model.Entity<InvoiceOverview>().HasNoKey().ToView("vw_invoice_overview");
    }
}
