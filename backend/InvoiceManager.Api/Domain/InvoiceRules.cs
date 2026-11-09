namespace InvoiceManager.Api.Domain;

public static class InvoiceRules
{
    public const decimal MaximumAmount = 1_000_000_000_000m;
    public static readonly string[] InvoiceStatuses = ["Issued", "Partial", "Cancelled"];
    public static readonly string[] PaymentStatuses = ["Paid", "Pending", "Overdue"];

    public static long ToCents(decimal amount)
    {
        if (amount < 0 || amount > MaximumAmount || decimal.Round(amount, 2) != amount)
            throw new RuleException("El monto debe estar entre 0 y 1.000.000.000.000 y tener como máximo dos decimales.");
        return checked((long)(amount * 100));
    }

    public static string PaymentStatus(DateOnly? paidOn, DateOnly dueOn, DateOnly today) =>
        paidOn.HasValue ? "Paid" : dueOn < today ? "Overdue" : "Pending";

    public static void ValidateCredit(long amountCents, long balanceCents)
    {
        if (amountCents <= 0) throw new RuleException("El monto de la nota de crédito debe ser mayor a cero.");
        if (amountCents > balanceCents)
            throw new RuleException("La nota de crédito supera el saldo disponible de la factura.");
    }
}

public sealed class RuleException(string message, int statusCode = 422) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

public interface IBusinessClock
{
    DateOnly Today { get; }
}

public sealed class BusinessClock(IConfiguration configuration) : IBusinessClock
{
    private readonly TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById(
        configuration["BusinessTimeZone"] ?? "America/Santiago");

    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).DateTime);
}
