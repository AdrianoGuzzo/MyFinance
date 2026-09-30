using System.Text;

namespace MyFinance.Infrastructure.Tests.Imports;

internal static class ImportTestHelpers
{
    static ImportTestHelpers() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public static Stream Utf8(string content) => new MemoryStream(Encoding.UTF8.GetBytes(content));

    public static Stream Windows1252(string content) => new MemoryStream(Encoding.GetEncoding(1252).GetBytes(content));

    public static Stream File(string name) => System.IO.File.OpenRead(Path.Combine(AppContext.BaseDirectory, "TestFiles", name));

    public static DateOnly Day(int day, int month = 9) => new(2026, month, day);
}