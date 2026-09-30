using System.Security.Cryptography;
using System.Text;

using MyFinance.Domain.Exceptions;

namespace MyFinance.Domain.ValueObjects;

/// <summary>
/// Hash SHA-256 em hexadecimal minúsculo (64 caracteres).
/// </summary>
public sealed record Sha256Hash
{
    private Sha256Hash(string value) => Value = value;

    public string Value { get; }

    public static Sha256Hash FromHex(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normalized.Length != 64 || !normalized.All(char.IsAsciiHexDigitLower))
        {
            throw new DomainException("Hash SHA-256 inválido.");
        }

        return new Sha256Hash(normalized);
    }

    public static Sha256Hash Compute(ReadOnlySpan<byte> data) => new(Convert.ToHexStringLower(SHA256.HashData(data)));

    public static Sha256Hash Compute(string text) => Compute(Encoding.UTF8.GetBytes(text));

    public static async Task<Sha256Hash> ComputeAsync(Stream stream, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var bytes = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return new Sha256Hash(Convert.ToHexStringLower(bytes));
    }

    public override string ToString() => Value;
}