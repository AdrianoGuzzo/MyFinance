# ADR 0015 — Privacidade, backup local e criptografia adiada

- Status: aceita; parcialmente substituída pela [ADR 0016](0016-servidor-mcp-local.md) (rede local e IA)
- Data: 2026-10-01

## Contexto

Os dados são financeiros e pessoais. O pedido pede banco local, criptografia quando apropriado, nenhum envio externo sem consentimento, logs sem dados sensíveis, proteção dos arquivos importados, backup local e uma arquitetura preparada para IA com anonimização.

## Decisão

- **Local**: SQLite na pasta de dados do usuário; nenhum código de rede, telemetria ou analytics.
- **Logs**: só metadados técnicos, verificados pelo `LogPrivacyTests` (captura todos os logs, inclusive o SQL do EF, e procura dígitos do cartão, descrições, valores, nome do arquivo e conteúdo do extrato).
- **Arquivos importados**: o app não guarda o arquivo; o conteúdo de cada registro (`RawData`) fica apenas no banco local, como histórico.
- **Backup**: `VACUUM INTO` (`IDatabaseBackup`) para um arquivo escolhido pelo usuário (*Configurações › Fazer backup*); cópia automática em `backups/` antes de aplicar migrations e quando um banco de versão anterior é substituído.
- **Criptografia do banco: adiada.** SQLCipher exigiria trocar o provider nativo do SQLite e gerenciar uma chave (senha a cada abertura ou cofre do sistema operacional), com risco real de perda de dados se a senha for esquecida. Nesta versão, a proteção é a do perfil do usuário no sistema operacional, e a tela de backup avisa que o arquivo não é criptografado.
- **IA futura**: qualquer uso passa por uma etapa de agregação/anonimização (por exemplo, totais por categoria e mês, sem descrições nem estabelecimentos) e requer consentimento explícito. `ICategorizationService` é o ponto de extensão para categorização local.

## Consequências

- Quem precisa de criptografia em repouso deve usar a criptografia de disco do sistema (BitLocker, FileVault, LUKS).
- Ativar SQLCipher depois é localizado na Infrastructure (connection string e provider), mais um fluxo de senha na interface.
