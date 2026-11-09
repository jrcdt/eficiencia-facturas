using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

namespace InvoiceManager.Api.Infrastructure;

public sealed partial class InitialSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE Invoices (
            Id INTEGER NOT NULL CONSTRAINT PK_Invoices PRIMARY KEY AUTOINCREMENT,
            Number INTEGER NOT NULL,
            IssuedOn TEXT NOT NULL,
            DueOn TEXT NOT NULL,
            TotalAmountCents INTEGER NOT NULL CHECK (TotalAmountCents > 0),
            IsConsistent INTEGER NOT NULL,
            CustomerRun TEXT NOT NULL,
            CustomerName TEXT NOT NULL,
            CustomerEmail TEXT NOT NULL,
            PaymentMethod TEXT NULL,
            PaidOn TEXT NULL
        );
        CREATE UNIQUE INDEX IX_Invoices_Number ON Invoices(Number);
        CREATE INDEX IX_Invoices_IsConsistent_DueOn ON Invoices(IsConsistent, DueOn);
        CREATE TABLE InvoiceLines (
            Id INTEGER NOT NULL CONSTRAINT PK_InvoiceLines PRIMARY KEY AUTOINCREMENT,
            InvoiceId INTEGER NOT NULL REFERENCES Invoices(Id) ON DELETE CASCADE,
            ProductName TEXT NOT NULL,
            UnitPriceCents INTEGER NOT NULL,
            Quantity INTEGER NOT NULL,
            SubtotalCents INTEGER NOT NULL,
            CHECK (UnitPriceCents >= 0 AND SubtotalCents >= 0 AND Quantity > 0)
        );
        CREATE INDEX IX_InvoiceLines_InvoiceId ON InvoiceLines(InvoiceId);
        CREATE TABLE CreditNotes (
            Id INTEGER NOT NULL CONSTRAINT PK_CreditNotes PRIMARY KEY AUTOINCREMENT,
            InvoiceId INTEGER NOT NULL REFERENCES Invoices(Id) ON DELETE CASCADE,
            Number INTEGER NOT NULL,
            CreatedOn TEXT NOT NULL,
            AmountCents INTEGER NOT NULL CHECK (AmountCents > 0)
        );
        CREATE UNIQUE INDEX IX_CreditNotes_InvoiceId_Number ON CreditNotes(InvoiceId, Number);
        CREATE VIEW vw_invoice_overview AS
        WITH products AS (
            SELECT InvoiceId, SUM(SubtotalCents) AS ProductTotalCents FROM InvoiceLines GROUP BY InvoiceId
        ), credits AS (
            SELECT InvoiceId, SUM(AmountCents) AS CreditTotalCents, COUNT(*) AS CreditNoteCount
            FROM CreditNotes GROUP BY InvoiceId
        )
        SELECT i.*, COALESCE(p.ProductTotalCents, 0) AS ProductTotalCents,
            COALESCE(c.CreditTotalCents, 0) AS CreditTotalCents,
            i.TotalAmountCents - COALESCE(c.CreditTotalCents, 0) AS BalanceCents,
            COALESCE(c.CreditNoteCount, 0) AS CreditNoteCount,
            CASE WHEN COALESCE(c.CreditNoteCount, 0) = 0 THEN 'Issued'
                WHEN c.CreditTotalCents = i.TotalAmountCents THEN 'Cancelled'
                ELSE 'Partial' END AS InvoiceStatus
        FROM Invoices i
        LEFT JOIN products p ON p.InvoiceId = i.Id
        LEFT JOIN credits c ON c.InvoiceId = i.Id;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP VIEW IF EXISTS vw_invoice_overview;
        DROP TABLE IF EXISTS CreditNotes;
        DROP TABLE IF EXISTS InvoiceLines;
        DROP TABLE IF EXISTS Invoices;
        """);
}
