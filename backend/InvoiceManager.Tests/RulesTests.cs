using InvoiceManager.Api.Domain;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace InvoiceManager.Tests;

public sealed class RulesTests
{
    [Theory]
    [InlineData("2026-11-08", null, "Overdue")]
    [InlineData("2026-11-09", null, "Pending")]
    [InlineData("2026-11-10", null, "Pending")]
    [InlineData("2026-11-08", "2026-11-07", "Paid")]
    public void PaymentStatusUsesDueDateAndRecordedPayment(string due, string? paid, string expected) =>
        Assert.Equal(expected, InvoiceRules.PaymentStatus(paid is null ? null : DateOnly.Parse(paid),
            DateOnly.Parse(due), new DateOnly(2026, 11, 9)));

    [Theory]
    [InlineData("-1")]
    [InlineData("0.001")]
    [InlineData("1000000000001")]
    public void InvalidMoneyIsRejected(string value) => Assert.Throws<RuleException>(() =>
        InvoiceRules.ToCents(decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture)));

    [Fact]
    public void MoneyIsConvertedWithoutFloatingPointRounding() => Assert.Equal(1001, InvoiceRules.ToCents(10.01m));

    [Fact]
    public void ExactCreditBalanceIsAllowed() => InvoiceRules.ValidateCredit(10000, 10000);
}
