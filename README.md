# ExpenseHub — Checkpoint 2 de C#

[![code-quality](https://github.com/luyz-gusta/Recass-CP2/actions/workflows/build.yml/badge.svg)](https://github.com/luyz-gusta/Recass-CP2/actions/workflows/build.yml)

API REST corporativa de reembolsos em ASP.NET Core 10. Funcionários registram despesas, aprovadores decidem, o financeiro registra pagamentos e auditores consultam todo o histórico. Cada operação é protegida por identidade autenticada, role, proprietário do reembolso, estado atual e regra de transição.

Enunciado, issues e rubrica: [Racass/checkpoint-csharpracass-expensehub](https://github.com/Racass/checkpoint-csharpracass-expensehub). Arquitetura e contratos internos: [docs/ARQUITETURA.md](docs/ARQUITETURA.md).

## Equipe e divisão

| Integrante | Issues |
|---|---|
| Luiz Gustavo da Silva ([@luyz-gusta](https://github.com/luyz-gusta)) | I01 Fundação e EF Core · I02 Identity, Admin e autenticação · I03 Cadastro e roles · I10 Qualidade (auditoria final) |
| Pessoa 2 | I04 Criar e editar rascunho · I05 Enviar, listar e consultar · I06 Ownership e matriz de acesso |
| Pessoa 3 | I07 Aprovar e reprovar · I08 Pagamento e histórico · I09 Testes unitários |

## Pré-requisitos

- [.NET SDK 10.0](https://dotnet.microsoft.com/download/dotnet/10.0)
- Opcional, para rodar localmente o mesmo script de qualidade do CI: PowerShell 7 (`pwsh`) e [Gitleaks](https://github.com/gitleaks/gitleaks)

## Banco de dados

| Item | Valor |
|---|---|
| Provider | SQLite |
| Pacote | `Microsoft.EntityFrameworkCore.Sqlite` 10.0.12 (com `Microsoft.EntityFrameworkCore.Design` 10.0.12 para as migrations) |
| Connection string | `ConnectionStrings:ExpenseHub` em `sources/ExpenseHub.Api/appsettings.json`: `Data Source=expensehub.db` |
| Arquivo do banco | `sources/ExpenseHub.Api/expensehub.db`, criado ao executar a API. Não é versionado (`*.db` no `.gitignore`) |
| Migrations | `sources/ExpenseHub.Api/Data/Migrations` |

**Criação e atualização.** A API aplica as migrations pendentes ao iniciar (`Database.MigrateAsync()`), então basta executá-la. Para aplicar manualmente, use a ferramenta local `dotnet-ef` declarada em `dotnet-tools.json`:

```shell
dotnet tool restore
dotnet tool run dotnet-ef database update --project sources/ExpenseHub.Api
```

**Recriar do zero.** Pare a API, apague `sources/ExpenseHub.Api/expensehub.db*` e inicie-a de novo.

**Nova migration** (toda mudança de modelo precisa de uma; nunca edite uma migration que já está na `main`):

```shell
dotnet tool run dotnet-ef migrations add NomeDaMudanca --project sources/ExpenseHub.Api --output-dir Data/Migrations
dotnet tool run dotnet-ef migrations has-pending-model-changes --project sources/ExpenseHub.Api
```

**Categorias de despesa.** São dados de referência criados pela migration inicial, com IDs fixos:

| Id | Categoria |
|---:|---|
| 1 | Alimentação |
| 2 | Transporte |
| 3 | Hospedagem |
| 4 | Material de escritório |
| 5 | Outros |

## Como executar

```shell
dotnet restore ./sources/ExpenseHub.slnx
dotnet build ./sources/ExpenseHub.slnx
dotnet test ./sources/ExpenseHub.slnx
dotnet run --project ./sources/ExpenseHub.Api/ExpenseHub.Api.csproj
```

A API sobe em `http://localhost:5245`. `GET /health` confirma que ela está no ar, e o documento OpenAPI fica em `/openapi/v1.json` (ambiente Development). As requisições de exemplo estão em [`sources/ExpenseHub.Api/ExpenseHub.Api.http`](sources/ExpenseHub.Api/ExpenseHub.Api.http).

## Estrutura

```text
sources/
├── ExpenseHub.slnx
├── ExpenseHub.Api/
│   ├── Controllers/   endpoints HTTP (recebem DTOs, nunca entidades)
│   ├── Dtos/          DTOs de entrada com validação declarativa
│   ├── Contracts/     respostas da API
│   ├── Domain/        entidades, estados e roles
│   ├── Data/          DbContext, mapeamentos, migrations e repositório
│   ├── Services/      regras de negócio e autorização contextual
│   └── Http/          tradução de claims e de resultados para HTTP
└── ExpenseHub.UnitTests/
    ├── TestDoubles/   repositório em memória, relógio fixo e atores de teste
    └── ...            testes por área
```

Toda resposta de erro segue `ProblemDetails`. Campos desconhecidos no JSON (por exemplo `ownerId`, `status` ou `roles`) são rejeitados com `400`, o que impede mass assignment.

## Autenticação (I02)

A completar na issue correspondente.

## Usuários e roles (I03)

A completar na issue correspondente.

## Reembolsos (I04–I06)

A completar nas issues correspondentes.

## Aprovação, pagamento e histórico (I07–I08)

A completar nas issues correspondentes.

## Testes (I09)

Testes unitários com MSTest, sem banco, rede ou serviço externo: os serviços dependem de `IExpenseRepository` e de `TimeProvider`, substituídos nos testes por `InMemoryExpenseRepository` e `FixedTimeProvider`.

```shell
dotnet test ./sources/ExpenseHub.slnx
```

Detalhamento da cobertura a completar na I09.

## Qualidade (I10)

O workflow `code-quality` roda a cada push e publica o relatório como artefato. O mesmo script pode ser executado localmente com `pwsh ./scripts/Invoke-CodeQuality.ps1`. Score final a registrar na I10.
