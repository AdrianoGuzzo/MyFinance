using System.ComponentModel;

using ModelContextProtocol.Server;

using MyFinance.Application.Categories;

namespace MyFinance.Mcp.Tools;

/// <summary>Categorias e regras de categorização automática.</summary>
[McpServerToolType]
internal static class CategoriaTools
{
    [McpServerTool(Name = "listar_categorias", Title = "Listar categorias", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Categorias em árvore (cada principal seguida das subcategorias). nomeCompleto tem o formato \"Alimentação > Delivery\". "
        + "Consulte antes de categorizar para usar os ids corretos.")]
    public static async Task<IReadOnlyList<Categoria>> ListarCategoriasAsync(
        CategoryService categories,
        [Description("Incluir categorias desativadas (não podem receber lançamentos).")] bool incluirInativas = false,
        CancellationToken cancellationToken = default) =>
        [.. (await categories.ListAsync(incluirInativas, cancellationToken))
            .Select(c => new Categoria(c.Id, c.Name, c.FullName, c.ParentCategoryId, c.IsActive))];

    [McpServerTool(Name = "listar_regras", Title = "Listar regras de categorização", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Regras de categorização automática: quando a descrição contém o padrão, o lançamento recebe a categoria. "
        + "Com mais de uma regra válida, vence a de maior prioridade (depois o padrão mais longo).")]
    public static async Task<IReadOnlyList<Regra>> ListarRegrasAsync(CategoryRuleService rules, CancellationToken cancellationToken = default) =>
        [.. (await rules.ListAsync(cancellationToken))
            .Select(r => new Regra(r.Id, r.Pattern, r.CategoryId, r.CategoryName, r.Priority, r.IsActive))];

    [McpServerTool(Name = "criar_categoria", Title = "Criar categoria", Destructive = false, OpenWorld = false)]
    [Description("Cria uma categoria principal ou uma subcategoria (só um nível). Use quando nenhuma categoria existente servir.")]
    public static async Task<CategoriaCriada> CriarCategoriaAsync(
        CategoryService categories,
        DataChangeNotifier notifier,
        [Description("Nome (até 60 caracteres, único entre categorias irmãs).")] string nome,
        [Description("Id da categoria principal para criar uma subcategoria. Vazio = categoria principal.")] Guid? categoriaPaiId = null,
        [Description("Cor no formato #RRGGBB (opcional; subcategorias herdam a cor da principal).")] string? cor = null,
        CancellationToken cancellationToken = default)
    {
        var id = await categories.CreateAsync(new CreateCategoryCommand(nome, cor, categoriaPaiId), cancellationToken);
        notifier.NotifyChanged();
        var created = (await categories.ListAsync(includeInactive: true, cancellationToken)).Single(c => c.Id == id);
        return new CategoriaCriada(id, created.FullName);
    }

    [McpServerTool(Name = "criar_regra", Title = "Criar regra de categorização", Destructive = false, OpenWorld = false)]
    [Description("Cria uma regra: lançamentos futuros cuja descrição contém o padrão recebem a categoria. Depois use "
        + "aplicar_regras_pendentes para categorizar os lançamentos já existentes sem categoria.")]
    public static async Task<IdResult> CriarRegraAsync(
        CategoryRuleService rules,
        DataChangeNotifier notifier,
        [Description("Texto procurado na descrição, a partir do início de uma palavra (sem diferenciar maiúsculas e acentos). Ex.: IFOOD.")] string padrao,
        [Description("Id da categoria.")] Guid categoriaId,
        [Description("Prioridade de 0 a 1000; a maior vence quando mais de uma regra casa.")] int prioridade = 0,
        CancellationToken cancellationToken = default)
    {
        var id = await rules.CreateAsync(new SaveCategoryRuleCommand(padrao, categoriaId, prioridade), cancellationToken);
        notifier.NotifyChanged();
        return new IdResult(id);
    }

    [McpServerTool(Name = "editar_regra", Title = "Editar regra de categorização", Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Altera padrão, categoria e prioridade de uma regra. Não muda lançamentos já categorizados.")]
    public static async Task<OkResult> EditarRegraAsync(
        CategoryRuleService rules,
        DataChangeNotifier notifier,
        [Description("Id da regra.")] Guid regraId,
        [Description("Novo padrão.")] string padrao,
        [Description("Id da categoria.")] Guid categoriaId,
        [Description("Prioridade de 0 a 1000.")] int prioridade,
        CancellationToken cancellationToken = default)
    {
        await rules.UpdateAsync(regraId, new SaveCategoryRuleCommand(padrao, categoriaId, prioridade), cancellationToken);
        notifier.NotifyChanged();
        return new OkResult();
    }

    [McpServerTool(Name = "desativar_regra", Title = "Desativar regra", Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Desativa uma regra (deixa de categorizar lançamentos novos). Pode ser reativada com ativar_regra.")]
    public static async Task<OkResult> DesativarRegraAsync(
        CategoryRuleService rules, DataChangeNotifier notifier, [Description("Id da regra.")] Guid regraId, CancellationToken cancellationToken = default)
    {
        await rules.DeactivateAsync(regraId, cancellationToken);
        notifier.NotifyChanged();
        return new OkResult();
    }

    [McpServerTool(Name = "ativar_regra", Title = "Ativar regra", Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Reativa uma regra desativada.")]
    public static async Task<OkResult> AtivarRegraAsync(
        CategoryRuleService rules, DataChangeNotifier notifier, [Description("Id da regra.")] Guid regraId, CancellationToken cancellationToken = default)
    {
        await rules.ActivateAsync(regraId, cancellationToken);
        notifier.NotifyChanged();
        return new OkResult();
    }

    [McpServerTool(Name = "aplicar_regras_pendentes", Title = "Aplicar regras aos pendentes", Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Aplica as regras ativas aos lançamentos ainda sem categoria. Nunca altera uma categoria já atribuída.")]
    public static async Task<AplicacaoRegras> AplicarRegrasPendentesAsync(
        CategoryRuleService rules, DataChangeNotifier notifier, CancellationToken cancellationToken = default)
    {
        var count = await rules.ApplyToUncategorizedAsync(cancellationToken);
        notifier.NotifyChanged();
        return new AplicacaoRegras(count);
    }
}