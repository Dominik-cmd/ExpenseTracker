using ExpenseTracker.Core.Enums;
using ExpenseTracker.Infrastructure;
using FluentAssertions;

namespace ExpenseTracker.UnitTests;

public sealed class SmsParserTests
{
    private readonly OtpBankaSmsParser _parser = new();

    [Fact]
    public void Parse_ShouldParseTransferOutSms()
    {
        var sms = "Odliv 15.03.2024; Iz racuna: SI56 1234 5678 9012 345; Prejemnik: TELEKOM SLOVENIJE; Namen: PLACILO RACUNA; Znesek: 45,99 EUR OTP banka d.d.";

        var result = _parser.Parse(sms);

        result.Should().NotBeNull();
        result!.Direction.Should().Be(Direction.Debit);
        result.TransactionType.Should().Be(TransactionType.TransferOut);
        result.Amount.Should().Be(45.99m);
        result.Currency.Should().Be("EUR");
        result.TransactionDate.Should().Be(new DateTime(2024, 3, 15, 0, 0, 0, DateTimeKind.Utc));
        result.MerchantRaw.Should().Be("TELEKOM SLOVENIJE");
        result.MerchantNormalized.Should().Be("TELEKOM SLOVENIJE");
        result.Notes.Should().Be("PLACILO RACUNA");
    }

    [Fact]
    public void Parse_ShouldParseTransferInSms()
    {
        var sms = "Priliv 20.03.2024; Racun: SI56 1234 5678 9012 345; Placnik: PODJETJE DOO; Namen: PLACA MAREC 2024; Znesek: 2.150,00 EUR OTP banka d.d.";

        var result = _parser.Parse(sms);

        result.Should().NotBeNull();
        result!.Direction.Should().Be(Direction.Credit);
        result.TransactionType.Should().Be(TransactionType.TransferIn);
        result.Amount.Should().Be(2150.00m);
        result.TransactionDate.Should().Be(new DateTime(2024, 3, 20, 0, 0, 0, DateTimeKind.Utc));
        result.MerchantRaw.Should().Be("PODJETJE DOO");
        result.Notes.Should().Be("PLACA MAREC 2024");
    }

    [Fact]
    public void Parse_ShouldParsePurchaseSmsWithTime()
    {
        var sms = "POS NAKUP 22.03.2024 14:35, kartica ***1234, znesek 23,45 EUR, MERCATOR MARIBOR SI. Info: 041123456. OTP banka";

        var result = _parser.Parse(sms);

        result.Should().NotBeNull();
        result!.Direction.Should().Be(Direction.Debit);
        result.TransactionType.Should().Be(TransactionType.Purchase);
        result.Amount.Should().Be(23.45m);
        result.TransactionDate.Should().Be(new DateTime(2024, 3, 22, 14, 35, 0, DateTimeKind.Utc));
        result.MerchantRaw.Should().Be("MERCATOR MARIBOR SI");
        result.MerchantNormalized.Should().Be("MERCATOR");
        result.Notes.Should().BeNull();
    }

    [Fact]
    public void Parse_ShouldParseOnlinePurchaseSms()
    {
        var sms = "SPLET/TEL NAKUP 10.05.2026 11:06, kartica ***9044, znesek 48,96 EUR, IKEA Slovenia online, Ljubljana SI. Info: +38615834183. OTP banka";

        var result = _parser.Parse(sms);

        result.Should().NotBeNull();
        result!.Direction.Should().Be(Direction.Debit);
        result.TransactionType.Should().Be(TransactionType.Purchase);
        result.Amount.Should().Be(48.96m);
        result.TransactionDate.Should().Be(new DateTime(2026, 5, 10, 11, 6, 0, DateTimeKind.Utc));
        result.MerchantRaw.Should().Be("IKEA Slovenia online");
        result.Notes.Should().BeNull();
    }

    [Fact]
    public void Parse_ShouldReturnNullForInvalidOrEmptyText()
    {
        _parser.Parse(null!).Should().BeNull();
        _parser.Parse(string.Empty).Should().BeNull();
        _parser.Parse("This is not a bank SMS.").Should().BeNull();
    }

    [Fact]
    public void Parse_ShouldHandleThousandSeparatorsAndCommaDecimals()
    {
        var sms = "Odliv 15.03.2024; Iz racuna: SI56 1234 5678 9012 345; Prejemnik: TELEKOM SLOVENIJE; Namen: PLACILO RACUNA; Znesek: 1.234,56 EUR OTP banka d.d.";

        var result = _parser.Parse(sms);

        result.Should().NotBeNull();
        result!.Amount.Should().Be(1234.56m);
        result.TransactionDate.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void Parse_ShouldParseStornoSpletTelAsCreditRefund()
    {
        var sms = "STORNO SPLET/TEL 12.05.2026 11:47, kartica ***3306, znesek 1,00 EUR, GOOGLE*YOUTUBEPREMIUM, LONDON GB. Info: +38615834183. OTP banka";

        var result = _parser.Parse(sms);

        result.Should().NotBeNull();
        result!.Direction.Should().Be(Direction.Credit);
        result.TransactionType.Should().Be(TransactionType.Refund);
        result.Amount.Should().Be(1.00m);
        result.Currency.Should().Be("EUR");
        result.TransactionDate.Should().Be(new DateTime(2026, 5, 12, 11, 47, 0, DateTimeKind.Utc));
        result.MerchantRaw.Should().Be("GOOGLE*YOUTUBEPREMIUM");
        result.Notes.Should().BeNull();
    }

    [Fact]
    public void Parse_ShouldParseStornoPosAsCreditRefund()
    {
        var sms = "STORNO POS 10.05.2026 09:30, kartica ***1234, znesek 25,50 EUR, MERCATOR MARIBOR SI. Info: 041123456. OTP banka";

        var result = _parser.Parse(sms);

        result.Should().NotBeNull();
        result!.Direction.Should().Be(Direction.Credit);
        result.TransactionType.Should().Be(TransactionType.Refund);
        result.Amount.Should().Be(25.50m);
        result.TransactionDate.Should().Be(new DateTime(2026, 5, 10, 9, 30, 0, DateTimeKind.Utc));
        result.MerchantRaw.Should().Be("MERCATOR MARIBOR SI");
        result.Notes.Should().BeNull();
    }
}
