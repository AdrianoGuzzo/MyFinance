namespace MyFinance.Application.Backup;

/// <summary>Cópia de segurança do banco local para um arquivo escolhido pelo usuário.</summary>
public interface IDatabaseBackup
{
    /// <summary>Grava uma cópia consistente do banco em <paramref name="destinationPath"/> (substitui o arquivo, se existir).</summary>
    Task BackupToAsync(string destinationPath, CancellationToken cancellationToken);
}