using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using MyFinance.Domain.ValueObjects;

namespace MyFinance.Infrastructure.Persistence.Converters;

/// <summary>SQLite não guarda o <see cref="DateTimeKind"/>; datas de auditoria são sempre UTC.</summary>
internal sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

internal sealed class Sha256HashConverter() : ValueConverter<Sha256Hash, string>(
    v => v.Value,
    v => Sha256Hash.FromHex(v));

internal sealed class HexColorConverter() : ValueConverter<HexColor, string>(
    v => v.Value,
    v => HexColor.Create(v));

internal sealed class AccountNumberConverter() : ValueConverter<AccountNumber, string>(
    v => v.Value,
    v => AccountNumber.Create(v));

internal sealed class LastFourDigitsConverter() : ValueConverter<LastFourDigits, string>(
    v => v.Value,
    v => LastFourDigits.Create(v));

internal sealed class DayOfMonthConverter() : ValueConverter<DayOfMonth, int>(
    v => v.Value,
    v => DayOfMonth.Create(v));