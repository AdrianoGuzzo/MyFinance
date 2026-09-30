using System.Text;

using MyFinance.Application.Common.Exceptions;
using MyFinance.Application.Imports;

namespace MyFinance.Infrastructure.Imports.Common;

/// <summary>Leitura do arquivo de extrato como texto, com detecção de encoding.</summary>
internal static class ImportText
{
    public const int MaxFileSizeBytes = ImportLimits.MaxFileSizeBytes;

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    static ImportText() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public static async Task<string> ReadAllTextAsync(Stream stream, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > MaxFileSizeBytes)
            {
                throw new ImportException($"O arquivo é grande demais (limite de {MaxFileSizeBytes / 1024 / 1024} MB).");
            }

            buffer.Write(chunk, 0, read);
        }

        var text = Decode(buffer.ToArray());
        return string.IsNullOrWhiteSpace(text) ? throw new ImportException("O arquivo está vazio.") : text;
    }

    /// <summary>
    /// Respeita BOM (UTF-8/UTF-16). Sem BOM, tenta UTF-8 estrito e, se houver bytes inválidos,
    /// usa Windows-1252 — padrão de muitos bancos brasileiros (OFX com CHARSET:1252).
    /// </summary>
    internal static string Decode(byte[] bytes)
    {
        if (bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble))
        {
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        }

        if (bytes.AsSpan().StartsWith(Encoding.Unicode.Preamble))
        {
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        }

        if (bytes.AsSpan().StartsWith(Encoding.BigEndianUnicode.Preamble))
        {
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        }

        try
        {
            return StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.GetEncoding(1252).GetString(bytes);
        }
    }
}