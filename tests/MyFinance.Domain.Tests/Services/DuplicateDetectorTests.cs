using MyFinance.Domain.Enums;
using MyFinance.Domain.Services;

using static MyFinance.Domain.Tests.TestData;

namespace MyFinance.Domain.Tests.Services;

public sealed class DuplicateDetectorTests
{
    private static DuplicateCandidate Candidate(
        DateOnly date, decimal amount, string description, string? externalId = null, decimal? balance = null) =>
        new(date, amount, description, externalId, TransactionFingerprint.ComputeImportHash(date, amount, description, balance));

    private static ExistingTransaction Existing(
        DateOnly date, decimal amount, string description, string? externalId = null, decimal? balance = null, bool withHash = true) =>
        new(Guid.CreateVersion7(), date, amount, description, externalId,
            withHash ? TransactionFingerprint.ComputeImportHash(date, amount, description, balance) : null);

    [Fact]
    public void Sem_transacoes_existentes_todas_sao_novas()
    {
        var results = DuplicateDetector.Detect([Candidate(Day(1), -10m, "A"), Candidate(Day(2), -20m, "B")], []);

        results.Should().AllSatisfy(r => r.IsDuplicate.Should().BeFalse());
    }

    [Fact]
    public void Mesmo_ExternalId_e_duplicada_mesmo_com_dados_diferentes()
    {
        var existing = Existing(Day(1), -10m, "Netflix", "FIT-1");

        var results = DuplicateDetector.Detect([Candidate(Day(2), -10m, "NETFLIX.COM", "FIT-1")], [existing]);

        results.Single().Should().Be(new DuplicateCheck(DuplicateReason.ExternalId, existing.Id));
    }

    [Fact]
    public void Mesmo_hash_e_duplicada_por_ImportHash()
    {
        var existing = Existing(Day(1), -49.90m, "Netflix", balance: 500m);

        var results = DuplicateDetector.Detect([Candidate(Day(1), -49.90m, "NETFLIX", balance: 500m)], [existing]);

        results.Single().Should().Be(new DuplicateCheck(DuplicateReason.ImportHash, existing.Id));
    }

    [Fact]
    public void Mesma_combinacao_de_dados_e_duplicada_quando_hash_difere()
    {
        // Lançamento criado manualmente (sem hash) ou vindo de outro formato (OFX x CSV).
        var existing = Existing(Day(1), -35.90m, "Uber *Trip", withHash: false);

        var results = DuplicateDetector.Detect([Candidate(Day(1), -35.90m, "UBER  *TRIP", balance: 100m)], [existing]);

        results.Single().Should().Be(new DuplicateCheck(DuplicateReason.SameData, existing.Id));
    }

    [Fact]
    public void ExternalIds_diferentes_nunca_sao_duplicadas_mesmo_com_dados_iguais()
    {
        var existing = Existing(Day(1), -5m, "Café", "FIT-1");

        var results = DuplicateDetector.Detect([Candidate(Day(1), -5m, "Café", "FIT-2")], [existing]);

        results.Single().IsDuplicate.Should().BeFalse();
    }

    [Fact]
    public void Compras_identicas_no_arquivo_consomem_uma_existente_cada()
    {
        // Duas compras iguais no mesmo dia; só uma já existe no banco de dados.
        var existing = Existing(Day(1), -5m, "Café");

        var results = DuplicateDetector.Detect([Candidate(Day(1), -5m, "Café"), Candidate(Day(1), -5m, "Café")], [existing]);

        results.Select(r => r.IsDuplicate).Should().Equal(true, false);
        results[0].ExistingTransactionId.Should().Be(existing.Id);
    }

    [Fact]
    public void ExternalId_repetido_no_proprio_arquivo_e_marcado()
    {
        var results = DuplicateDetector.Detect([Candidate(Day(1), -5m, "Café", "FIT-1"), Candidate(Day(1), -5m, "Café", "FIT-1")], []);

        results[0].IsDuplicate.Should().BeFalse();
        results[1].Reason.Should().Be(DuplicateReason.RepeatedInFile);
    }

    [Fact]
    public void ExternalId_repetido_no_arquivo_com_dados_diferentes_nao_e_duplicada()
    {
        // Alguns bancos reutilizam FITIDs: mesmo identificador, transações diferentes.
        var results = DuplicateDetector.Detect([Candidate(Day(1), -5m, "Café", "FIT-1"), Candidate(Day(2), -80m, "Farmácia", "FIT-1")], []);

        results.Should().AllSatisfy(r => r.IsDuplicate.Should().BeFalse());
    }

    [Fact]
    public void FITID_reutilizado_pelo_banco_entre_importacoes_nao_descarta_transacoes_novas()
    {
        // Banco que usa o mesmo FITID ("0") para tudo: o mês 1 já está gravado.
        var existing = new[] { Existing(Day(1), -5m, "Café", "0"), Existing(Day(2), -20m, "Uber", "0") };
        var monthTwo = new[]
        {
            Candidate(Day(1, 10), -30m, "Padaria", "0"),
            Candidate(Day(2, 10), -90m, "Farmácia", "0"),
            Candidate(Day(1), -5m, "Café", "0"),
        };

        var results = DuplicateDetector.Detect(monthTwo, existing);

        results.Select(r => r.IsDuplicate).Should().Equal(false, false, true);
        results[2].ExistingTransactionId.Should().Be(existing[0].Id);
    }

    [Fact]
    public void Cada_existente_casada_por_ExternalId_e_consumida_uma_unica_vez()
    {
        var existing = Existing(Day(1), -5m, "Café", "0");
        var candidates = new[] { Candidate(Day(1), -5m, "Café", "0"), Candidate(Day(3), -5m, "Chá", "0") };

        var results = DuplicateDetector.Detect(candidates, [existing]);

        results.Select(r => r.IsDuplicate).Should().Equal(true, false);
    }

    [Fact]
    public void Mesmo_ExternalId_com_valor_diferente_nao_e_duplicada()
    {
        var existing = Existing(Day(1), -10m, "Loja", "FIT-1");

        var results = DuplicateDetector.Detect([Candidate(Day(1), -99m, "Loja", "FIT-1")], [existing]);

        results.Single().IsDuplicate.Should().BeFalse();
    }

    [Fact]
    public void ExternalId_tem_prioridade_sobre_outras_estrategias()
    {
        // A existente com mesmo FITID não pode ser "roubada" por outra candidata casada por dados.
        var existing = Existing(Day(1), -5m, "Café", "FIT-1");
        var candidates = new[]
        {
            Candidate(Day(1), -5m, "Café"),
            Candidate(Day(1), -5m, "Café", "FIT-1"),
        };

        var results = DuplicateDetector.Detect(candidates, [existing]);

        results[1].Should().Be(new DuplicateCheck(DuplicateReason.ExternalId, existing.Id));
        results[0].IsDuplicate.Should().BeFalse();
    }

    [Fact]
    public void Reimportar_o_mesmo_extrato_marca_todas_como_duplicadas()
    {
        var file = Enumerable.Range(1, 20)
            .Select(i => Candidate(Day(i), -i, $"Compra {i}", $"FIT-{i}"))
            .ToList();
        var persisted = file
            .Select(c => new ExistingTransaction(Guid.CreateVersion7(), c.Date, c.Amount, c.Description, c.ExternalId, c.ImportHash))
            .ToList();

        var results = DuplicateDetector.Detect(file, persisted);

        results.Should().AllSatisfy(r => r.Reason.Should().Be(DuplicateReason.ExternalId));
        results.Select(r => r.ExistingTransactionId).Should().OnlyHaveUniqueItems();
    }
}