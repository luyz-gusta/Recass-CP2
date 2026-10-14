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

### Criar e editar rascunho (I04)

| Rota | Quem acessa | Resultado |
|---|---|---|
| `POST /api/expenses` | `Employee` | `201` com o reembolso em `Draft` e `Location: /api/expenses/{id}`. Dados inválidos → `400`; sem a role Employee → `403` |
| `PUT /api/expenses/{id}` | `Employee`, somente o proprietário | `200` com o reembolso atualizado. Dados inválidos → `400`; sem a role Employee → `403`; inexistente **ou de outra pessoa** → `404`; fora de `Draft` → `409` |

Corpo de `POST` e de `PUT` (os dois têm exatamente os mesmos quatro campos, todos obrigatórios):

| Campo | Regra |
|---|---|
| `description` | texto de 10 a 500 caracteres |
| `amount` | número entre `0.01` e `2147483647`, com ponto decimal (`decimal` no servidor, 2 casas na persistência) |
| `expenseDate` | data `yyyy-MM-dd`, nunca futura (a comparação usa o dia UTC do servidor) |
| `categoryId` | identificador de uma categoria existente (1 a 5, veja a tabela de categorias acima) |

```json
{ "description": "Almoço com cliente em São Paulo", "amount": 150.50, "expenseDate": "2026-09-15", "categoryId": 1 }
```

- **Sem mass assignment:** proprietário, estado, identificador, ator e horários **nunca** vêm do cliente. O proprietário é o usuário do token; o estado inicial é `Draft`; o `id` e os horários são do servidor. Qualquer campo fora dos quatro acima (`ownerId`, `status`, `createdAtUtc`...) é rejeitado com `400`. Campo ausente também é `400`: o servidor não transforma um valor faltante em `0` em silêncio.
- **Ordem das verificações** (a mesma em todos os serviços): role (`403`) → entrada, como data futura e categoria inexistente (`400`) → escopo, só o proprietário (`404`) → estado (`409`) → persistência.
- **Edição:** o `PUT` substitui os quatro campos de um reembolso em `Draft` do próprio usuário. Reembolso de outra pessoa responde `404`, sem revelar que existe, mesmo para quem acumula roles (por exemplo, `Approver` + `Employee`). Depois de enviado, o reembolso não pode mais ser editado (`409`).
- **Histórico:** a criação grava a ação `Created` (sem estado anterior, novo estado `Draft`) e cada edição que muda algo grava `Updated` (`Draft` → `Draft`), com o texto das alterações no formato `campo: antigo -> novo; ...`, por exemplo `amount: 150.50 -> 210.00; description: ... -> ...`. Reenviar os mesmos dados responde `200` sem histórico novo e sem mexer em `updatedAtUtc`. Reembolso e histórico são gravados juntos, num único `SaveChanges`.
- **Concorrência:** cada alteração renova o `ConcurrencyStamp`. Se outra operação alterar o mesmo reembolso antes da gravação, a resposta é `409`.

### Enviar, listar e consultar (I05)

| Rota | Quem acessa | Resultado |
|---|---|---|
| `POST /api/expenses/{id}/submit` | `Employee`, somente o proprietário | `200` com o reembolso em `Submitted`. Sem a role Employee → `403`; inexistente **ou de outra pessoa** → `404`; já enviado ou fora de `Draft` → `409` |
| `GET /api/expenses` | `Employee`, `Approver`, `Finance` ou `Auditor` | `200` com os reembolsos visíveis ao perfil (lista vazia quando não há nenhum), do mais recente para o mais antigo. Admin sozinho ou usuário sem role → `403` |
| `GET /api/expenses/{id}` | `Employee`, `Approver`, `Finance` ou `Auditor` | `200` com o reembolso. Admin sozinho ou sem role → `403`; inexistente **ou fora do escopo de leitura** → `404` |

**Visibilidade de leitura** (lista e detalhe):

| Role | Vê |
|---|---|
| `Employee` | os próprios reembolsos, em qualquer estado |
| `Approver` | somente os `Submitted` (nunca o rascunho de outra pessoa) |
| `Finance` | somente os `Approved` e os `Paid` |
| `Auditor` | todos |
| `Admin` | nenhum: a role Admin, sozinha, não dá acesso funcional |

Quem acumula roles recebe a **união** das visibilidades (por exemplo, `Approver` + `Employee` vê os próprios reembolsos mais todos os `Submitted`).

- **Filtro no banco:** a visibilidade é uma expressão (`ExpenseAccessPolicy.ReadScope`) aplicada à consulta do EF Core antes de materializar os dados, e vira o `WHERE` do SQL. Nada é carregado para ser filtrado em memória.
- **Sem vazamento:** reembolso inexistente e reembolso fora do escopo respondem `404` exatamente igual (mesmo status e mesma mensagem). O `403` só aparece quando o usuário não tem nenhuma role de leitura.
- **Envio:** muda `Draft` → `Submitted`, preenche `submittedAtUtc` e grava o histórico `Submitted` (`Draft` → `Submitted`) no mesmo `SaveChanges`. Não existe reenvio: enviar de novo responde `409` e não cria histórico duplicado. Depois de enviado, o reembolso não pode mais ser editado (`409`).
- **Somente leitura para Auditor e Admin:** nenhum dos dois cria, edita ou envia reembolsos (`403`).

### Ownership e matriz de acesso (I06)

A autorização tem **duas camadas**, e as duas valem:

1. **Role no controller:** `[Authorize(Roles = ...)]` em cada rota. Sem token → `401`; sem a role → `403`.
2. **Decisão contextual no serviço:** role, ownership e estado combinados em `ExpenseAccessPolicy`. A regra vale mesmo que o serviço seja chamado sem o controller. Um atributo de role isolado nunca basta, e um filtro de tela não é autorização.

| Rota | Employee | Approver | Finance | Auditor | Admin | Regra contextual (serviço) |
|---|:---:|:---:|:---:|:---:|:---:|---|
| `POST /api/expenses` | sim | não | não | não | não | dono = usuário do token |
| `PUT /api/expenses/{id}` | sim* | não | não | não | não | só o dono, só em `Draft` |
| `POST /api/expenses/{id}/submit` | sim* | não | não | não | não | só o dono, só em `Draft` |
| `GET /api/expenses` | sim* | sim* | sim* | sim | não | `ReadScope` aplicado na consulta |
| `GET /api/expenses/{id}` | sim* | sim* | sim* | sim | não | fora do escopo → `404` |
| `POST /api/expenses/{id}/approve` e `/reject` | não | sim* | não | não | não | não pode ser o próprio reembolso; só `Submitted` |
| `POST /api/expenses/{id}/pay` | não | não | sim* | não | não | não pode ser o próprio reembolso; só `Approved` |
| `GET /api/expenses/{id}/history` | sim* | sim* | sim* | sim | não | mesma visibilidade do reembolso |

`*` a operação também depende de ownership, estado ou visibilidade. Quem acumula roles recebe a **união** das permissões, mas isso nunca elimina as proibições: `Approver` + `Employee` não aprova o próprio reembolso, `Finance` + `Employee` não paga o próprio, e a role `Employee` sozinha não abre o reembolso de outra pessoa.

**`ExpenseAccessPolicy`** (em `Services/`) concentra as decisões e recebe dados primitivos, então qualquer combinação de role, dono e estado é testável sem banco:

| Método | Decisão |
|---|---|
| `HasReadRole(actor)` | tem Employee, Approver, Finance ou Auditor? (Admin sozinho e sem role: não) |
| `ReadScope(actor)` | expressão de visibilidade, aplicada **na consulta SQL** (Auditor: tudo; Employee: próprios; Approver: `Submitted`; Finance: `Approved` e `Paid`) |
| `CanManageDraft(actor, ownerId)` | sem Employee → `Forbidden`; não é o dono → `NotFound`; senão `Allowed` |
| `CanDecide(actor, ownerId, status)` | sem Approver → `Forbidden`; rascunho de outra pessoa → `NotFound`; o próprio reembolso → `Forbidden`; senão `Allowed` |
| `CanPay(actor, ownerId, status)` | sem Finance → `Forbidden`; rascunho de outra pessoa → `NotFound`; o próprio reembolso → `Forbidden`; senão `Allowed` |

Códigos de resposta: `401` sem identidade; `403` sem a role, ou ao aprovar/pagar o próprio reembolso; `404` para o que o usuário não enxerga (inexistente e fora do escopo são indistinguíveis); `409` para transição inválida ou repetida.

As evidências, com o status esperado, o observado e o teste unitário de cada combinação de rota e perfil, estão em [docs/EVIDENCIAS-AUTORIZACAO.md](docs/EVIDENCIAS-AUTORIZACAO.md). As requisições para repetir os casos estão na seção "I06 — Matriz de acesso" do `.http`.

## Aprovação, pagamento e histórico (I07–I08)

### Aprovar e reprovar (I07)

| Rota | Quem acessa | Resultado |
|---|---|---|
| `POST /api/expenses/{id}/approve` | `Approver`, exceto o proprietário | `200` com o reembolso em `Approved`, `decidedById` e `decidedAtUtc`. Sem a role Approver → `403`; o próprio reembolso → `403`; inexistente ou rascunho de outra pessoa → `404`; fora de `Submitted` → `409` |
| `POST /api/expenses/{id}/reject` | `Approver`, exceto o proprietário | `200` com o reembolso em `Rejected` e `rejectionReason`. Justificativa ausente ou fora de 10 a 500 caracteres → `400`; as demais respostas são iguais às da aprovação |

Corpo da reprovação (único campo aceito):

```json
{ "justification": "Falta a nota fiscal da hospedagem; envie o comprovante." }
```

- **Transições:** só `Submitted` recebe decisão: `Submitted` → `Approved` ou `Submitted` → `Rejected`, e `Rejected` é final. A decisão é única: aprovar de novo, reprovar de novo ou trocar a decisão responde `409`, sem gravar histórico.
- **Justificativa:** obrigatória na reprovação, com 10 a 500 caracteres contados sem os espaços nas pontas, e gravada sem eles no reembolso (`rejectionReason`) e no histórico. O DTO valida o corpo (`400` automático), o serviço valida de novo depois de remover os espaços, e o domínio (`Expense.Reject`) recusa justificativa inválida como defesa em profundidade.
- **Quem decide:** só quem tem a role `Approver` e **não é o dono**. Quem acumula `Approver` + `Employee` decide sobre os reembolsos dos outros, mas recebe `403` no próprio (autoaprovação proibida). Employee, Finance, Auditor e Admin recebem `403`.
- **Escopo:** reembolso inexistente ou rascunho de outra pessoa → `404`, porque o Approver não enxerga rascunhos.
- **Ator e horário do servidor:** `decidedById` vem do token e `decidedAtUtc` do relógio do servidor (`TimeProvider`). O corpo aceita apenas `justification`: `decidedById`, `status` ou qualquer outro campo → `400`.
- **Histórico e atomicidade:** a decisão grava `Approved` ou `Rejected` (estado anterior `Submitted`, com o aprovador, o horário UTC e, na reprovação, a justificativa) no mesmo `SaveChanges` da mudança de estado. Conflito de concorrência → `409`.
- **Visibilidade depois da decisão:** um reembolso aprovado sai da visão do Approver (que só vê `Submitted`) e entra na do Finance (`Approved` e `Paid`).

Ordem das verificações: role (`403`) → justificativa (`400`) → escopo (`404`) → ownership (`403`) → estado (`409`) → persistência. As requisições estão na seção "I07 — Aprovar e reprovar" do `.http`, e os status observados em [docs/EVIDENCIAS-AUTORIZACAO.md](docs/EVIDENCIAS-AUTORIZACAO.md).

### Pagamento e histórico (I08)

| Rota | Quem acessa | Resultado |
|---|---|---|
| `POST /api/expenses/{id}/pay` | `Finance`, exceto o proprietário | `200` com o reembolso em `Paid`, `paidById` e `paidAtUtc`. Sem a role Finance → `403`; o próprio reembolso → `403`; inexistente ou rascunho de outra pessoa → `404`; fora de `Approved` → `409` |
| `GET /api/expenses/{id}/history` | `Employee`, `Approver`, `Finance` ou `Auditor` | `200` com o histórico em ordem cronológica. Admin sozinho ou sem role → `403`; inexistente **ou fora do escopo de leitura** → `404` |

- **Transição:** só `Approved` é pago (`Approved` → `Paid`), e `Paid` é final. Pagar de novo, pagar um `Submitted` ou pagar um `Rejected` responde `409`, sem pagamento e sem histórico.
- **Registro de pagamento:** o pagamento é simulado, sem integração bancária. O `PaymentRecord` (1:1 com o reembolso) guarda o valor do reembolso, quem pagou (do token) e quando (relógio do servidor). A rota não tem corpo: nada disso vem do cliente.
- **Quem paga:** só quem tem a role `Finance` e **não é o dono**. Quem acumula `Finance` + `Employee` paga os reembolsos dos outros, mas recebe `403` no próprio (autopagamento proibido). Employee, Approver, Auditor e Admin recebem `403`.
- **Atomicidade:** a mudança de estado, o `PaymentRecord` e o histórico `Paid` entram no mesmo `SaveChangesAsync`, numa única transação: ou os três são gravados, ou nenhum, então estado, pagamento e histórico nunca divergem. Dois pagamentos simultâneos do mesmo reembolso não geram pagamento duplicado: o `ConcurrencyStamp` faz um deles responder `409`. Num teste manual com 12 pagamentos paralelos do mesmo reembolso, a API respondeu um `200` e onze `409`, e gravou um único `PaymentRecord` e um único histórico `Paid`.
- **Histórico:** cada ação aceita grava uma entrada com a ação, o reembolso, o ator, o horário UTC e os estados anterior e posterior, na mesma operação lógica da alteração. Tentativas recusadas (`400`, `403`, `404`, `409`) não geram histórico.

  | Ação | Estado anterior → posterior | Ator | Detalhe |
  |---|---|---|---|
  | `Created` | (nenhum) → `Draft` | dono | |
  | `Updated` | `Draft` → `Draft` | dono | `changes`, no formato `campo: antigo -> novo; ...` |
  | `Submitted` | `Draft` → `Submitted` | dono | |
  | `Approved` | `Submitted` → `Approved` | aprovador | |
  | `Rejected` | `Submitted` → `Rejected` | aprovador | `justification` |
  | `Paid` | `Approved` → `Paid` | financeiro | |

- **Consulta do histórico:** tem a mesma visibilidade do detalhe, com `ReadScope` aplicado na consulta: o dono vê o histórico dos próprios reembolsos, o Approver o dos `Submitted`, o Finance o dos `Approved` e `Paid` e o Auditor o de todos. Fora do escopo → `404`, igual a um reembolso inexistente.

Histórico de um reembolso que passou pelo fluxo completo (ids encurtados):

```json
[
  { "id": 26, "action": "Created", "actorId": "<funcionario1>", "occurredAtUtc": "2026-10-14T21:21:37.7679396Z", "previousStatus": null, "newStatus": "Draft", "justification": null, "changes": null },
  { "id": 27, "action": "Updated", "actorId": "<funcionario1>", "occurredAtUtc": "2026-10-14T21:21:37.7766263Z", "previousStatus": "Draft", "newStatus": "Draft", "justification": null, "changes": "description: Jantar com o time do projeto Atlas -> Jantar com o time do projeto Atlas, com gorjeta; amount: 89.90 -> 98.90" },
  { "id": 28, "action": "Submitted", "actorId": "<funcionario1>", "occurredAtUtc": "2026-10-14T21:21:37.7835565Z", "previousStatus": "Draft", "newStatus": "Submitted", "justification": null, "changes": null },
  { "id": 29, "action": "Approved", "actorId": "<aprovador>", "occurredAtUtc": "2026-10-14T21:21:37.8083199Z", "previousStatus": "Submitted", "newStatus": "Approved", "justification": null, "changes": null },
  { "id": 30, "action": "Paid", "actorId": "<financeiro>", "occurredAtUtc": "2026-10-14T21:21:37.8186185Z", "previousStatus": "Approved", "newStatus": "Paid", "justification": null, "changes": null }
]
```

Ordem das verificações no pagamento: role (`403`) → escopo (`404`) → ownership (`403`) → estado (`409`) → persistência. As requisições do fluxo completo, do fluxo de reprovação e dos casos negativos estão na seção "I08 — Pagamento e histórico" do `.http`, e os status observados em [docs/EVIDENCIAS-AUTORIZACAO.md](docs/EVIDENCIAS-AUTORIZACAO.md).

## Testes (I09)

Testes unitários com MSTest, sem banco, rede ou serviço externo: os serviços dependem de `IExpenseRepository` e de `TimeProvider`, substituídos nos testes por `InMemoryExpenseRepository` e `FixedTimeProvider`.

```shell
dotnet test ./sources/ExpenseHub.slnx
```

Detalhamento da cobertura a completar na I09.

## Qualidade (I10)

O workflow `code-quality` roda a cada push e publica o relatório como artefato. O mesmo script pode ser executado localmente com `pwsh ./scripts/Invoke-CodeQuality.ps1`. Score final a registrar na I10.
