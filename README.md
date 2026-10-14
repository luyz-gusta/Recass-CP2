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

- **Identity:** usuários e roles do ASP.NET Core Identity ficam no mesmo banco SQLite. As senhas são guardadas só como hash.
- **Login:** `POST /login` com `{ "email": "...", "password": "..." }` devolve o token bearer nativo do Identity:

  ```json
  { "tokenType": "Bearer", "accessToken": "...", "expiresIn": 3600, "refreshToken": "..." }
  ```

  Envie o token nas demais requisições com `Authorization: Bearer <accessToken>`. Credencial inválida responde `401`. Depois de 5 tentativas erradas, a conta fica bloqueada por 5 minutos.
- **Rotas protegidas:** todas as rotas exigem autenticação, exceto `POST /register`, `POST /login`, `GET /health` e o OpenAPI de desenvolvimento.
  - sem token, ou com token inválido → `401`;
  - autenticado, mas sem a role exigida → `403`.
  - Todas as respostas de erro seguem `ProblemDetails`.
- **`GET /api/me`** mostra o id, o e-mail e as roles presentes no token.
- **Roles e token:** as roles entram no token no momento do login. **Depois de qualquer alteração de roles, o usuário precisa fazer login novamente.**

### Conta Admin inicial (seed)

Ao iniciar, a API cria, se ainda não existirem, as roles `Admin`, `Employee`, `Approver`, `Finance` e `Auditor` e **uma única conta: o Admin**. Nenhum outro usuário é criado pelo seed. O seed é idempotente: reiniciar a API não duplica roles nem o Admin, e não altera a senha de um Admin que já existe.

- O e-mail vem de `AdminSeed:Email` no `appsettings.json` (`admin@expensehub.local`).
- A senha **nunca fica no repositório**. Configure-a de uma destas formas antes de iniciar a API:

  ```shell
  # opção 1: user-secrets (fica no perfil do seu usuário, fora do repositório)
  dotnet user-secrets set "AdminSeed:Password" "<senha forte>" --project sources/ExpenseHub.Api

  # opção 2: variável de ambiente (PowerShell)
  $env:AdminSeed__Password = "<senha forte>"
  ```

- A senha precisa seguir a política do Identity: 6 ou mais caracteres, com letra maiúscula, letra minúscula, dígito e símbolo.
- Sem a senha configurada, a API sobe e cria as roles, mas não cria o Admin: um aviso no log explica o que falta.

### Requisições de exemplo (`.http`)

O arquivo [`ExpenseHub.Api.http`](sources/ExpenseHub.Api/ExpenseHub.Api.http) lê as senhas de um `.env` local, ignorado pelo Git. Crie `sources/ExpenseHub.Api/.env` com:

```text
EXPENSEHUB_ADMIN_PASS=<a mesma senha configurada em AdminSeed:Password>
EXPENSEHUB_DEMO_PASS=<senha para os usuários de demonstração>
```

O arquivo funciona com a extensão REST Client do VS Code ou com o Visual Studio 2022 17.12+.

## Usuários e roles (I03)

| Rota | Quem acessa | Resultado |
|---|---|---|
| `POST /register` | público | `201` com o usuário criado **sem nenhuma role**. E-mail já cadastrado → `409`; senha fora da política → `400`, com as mensagens em português |
| `GET /api/admin/users` | somente `Admin` | `200` com id, e-mail e roles de cada usuário |
| `PUT /api/admin/users/{id}/roles` | somente `Admin` | `200` com o usuário atualizado |

- **Sem roles no cadastro:** o cadastro não aceita roles. Um corpo com `roles`, ou qualquer outro campo desconhecido, é rejeitado com `400`. Todo usuário novo começa sem roles e, por isso, recebe `403` nas rotas de reembolso até um Admin atribuir uma role.
- **Substituição de roles:** o `PUT .../roles` recebe o conjunto completo, por exemplo `{ "roles": ["Approver", "Employee"] }`, e substitui as roles atuais. `[]` remove todas.
  - Só as roles `Admin`, `Employee`, `Approver`, `Finance` e `Auditor` são aceitas, sem diferenciar maiúsculas de minúsculas e ignorando duplicatas.
  - Role desconhecida → `400`. Roles nunca são criadas implicitamente.
  - Usuário inexistente → `404`.
  - O Admin não pode remover a própria role Admin → `409`.
- **Atomicidade:** remover roles, incluir roles e renovar o carimbo de segurança acontecem numa única transação.
- **Novo login obrigatório:** as roles vão dentro do token, então **depois de uma alteração de roles o usuário precisa fazer login novamente**. O token antigo continua com as roles anteriores até expirar; `GET /api/me` mostra o que está no token atual.

Usuários de demonstração criados pelo `.http` (todos com a senha `EXPENSEHUB_DEMO_PASS` do `.env`):

| E-mail | Roles |
|---|---|
| `funcionario1@expensehub.local` | Employee |
| `funcionario2@expensehub.local` | Employee |
| `aprovador@expensehub.local` | Approver |
| `financeiro@expensehub.local` | Finance |
| `auditor@expensehub.local` | Auditor |
| `aprovador.funcionario@expensehub.local` | Approver + Employee |
| `financeiro.funcionario@expensehub.local` | Finance + Employee |

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
