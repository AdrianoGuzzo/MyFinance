namespace MyFinance.Mcp;

/// <summary>
/// Avisa que uma ferramenta MCP alterou dados (categorias, regras, metas...), para a interface recarregar a tela aberta.
/// O evento é disparado na thread da requisição HTTP; quem assina deve voltar para a thread de UI.
/// </summary>
public sealed class DataChangeNotifier
{
    public event EventHandler? DataChanged;

    public void NotifyChanged() => DataChanged?.Invoke(this, EventArgs.Empty);
}
