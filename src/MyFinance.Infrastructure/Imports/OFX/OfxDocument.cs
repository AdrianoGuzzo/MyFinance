using System.Net;

namespace MyFinance.Infrastructure.Imports.OFX;

/// <summary>Elemento OFX: agregado (com filhos) ou folha (com valor).</summary>
internal sealed class OfxElement(string name)
{
    private readonly List<OfxElement> _children = [];

    public string Name { get; } = name;

    public string? Value { get; set; }

    public IReadOnlyList<OfxElement> Children => _children;

    public void Add(OfxElement child) => _children.Add(child);

    /// <summary>
    /// Valor do primeiro descendente com este nome. Busca em profundidade (e não só nos filhos diretos) porque,
    /// em SGML, uma folha vazia sem fechamento (<c>&lt;MEMO&gt;</c>) "adota" as tags seguintes como filhas.
    /// </summary>
    public string? FindValue(string name) => Descendants(name).FirstOrDefault(e => e.Value is not null)?.Value;

    /// <summary>Folhas (elementos com valor) em ordem de documento.</summary>
    public IEnumerable<OfxElement> Leaves()
    {
        foreach (var child in _children)
        {
            if (child.Value is not null)
            {
                yield return child;
            }

            foreach (var leaf in child.Leaves())
            {
                yield return leaf;
            }
        }
    }

    public IEnumerable<OfxElement> Descendants(string name)
    {
        foreach (var child in _children)
        {
            if (string.Equals(child.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                yield return child;
            }

            foreach (var descendant in child.Descendants(name))
            {
                yield return descendant;
            }
        }
    }
}

/// <summary>
/// Parser tolerante de OFX. Aceita:
/// <list type="bullet">
/// <item><description>OFX 1.x (SGML), em que elementos de valor não têm tag de fechamento (<c>&lt;TRNAMT&gt;-10.00</c>);</description></item>
/// <item><description>OFX 1.x com todas as tags fechadas (formato usado por vários bancos, inclusive o Nubank);</description></item>
/// <item><description>OFX 2.x (XML), com declaração <c>&lt;?xml?&gt;</c> e <c>&lt;?OFX?&gt;</c>.</description></item>
/// </list>
/// Regra: texto após uma tag de abertura torna o elemento uma folha, que é fechada implicitamente.
/// Tags de fechamento sem abertura correspondente na pilha são ignoradas.
/// </summary>
internal static class OfxDocument
{
    /// <param name="text">Conteúdo a partir de <c>&lt;OFX&gt;</c> (o cabeçalho SGML deve ter sido removido).</param>
    public static OfxElement Parse(string text)
    {
        var root = new OfxElement("#document");
        var stack = new Stack<OfxElement>();
        stack.Push(root);

        var position = 0;
        while (position < text.Length)
        {
            var open = text.IndexOf('<', position);
            if (open < 0)
            {
                break;
            }

            AssignText(stack, text[position..open]);

            var close = text.IndexOf('>', open);
            if (close < 0)
            {
                break;
            }

            var tag = text[(open + 1)..close].Trim();
            position = close + 1;

            if (tag.Length == 0 || tag[0] is '?' or '!')
            {
                continue; // declaração XML, instrução de processamento ou comentário
            }

            if (tag[0] == '/')
            {
                CloseElement(stack, tag[1..].Trim());
                continue;
            }

            var selfClosing = tag.EndsWith('/');
            var name = tag.TrimEnd('/').Split(' ', 2)[0];
            var element = new OfxElement(name.ToUpperInvariant());
            stack.Peek().Add(element);

            if (!selfClosing)
            {
                stack.Push(element);
            }
        }

        return root;
    }

    private static void AssignText(Stack<OfxElement> stack, string rawText)
    {
        var value = rawText.Trim();
        if (value.Length == 0 || stack.Count <= 1)
        {
            return;
        }

        var current = stack.Pop(); // folha: fecha implicitamente (SGML) ou será fechada pela tag </X> (ignorada depois)
        current.Value = WebUtility.HtmlDecode(value);
    }

    private static void CloseElement(Stack<OfxElement> stack, string name)
    {
        if (!stack.Any(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        while (stack.Count > 1)
        {
            var popped = stack.Pop();
            if (string.Equals(popped.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }
    }
}