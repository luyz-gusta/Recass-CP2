# Arquitetura do ExpenseHub

Este documento é o contrato técnico combinado pelo grupo. Toda issue segue estas decisões; quando algo precisar mudar, a mudança é discutida no PR e registrada aqui.

## 1. Stack

| Tema | Decisão |
|---|---|
| Plataforma | .NET 10 (`net10.0`), `Nullable` ligado e `ImplicitUsings` desligado (definidos em `Directory.Build.props`) |
| API | ASP.NET Core **Controllers** com `[ApiController]`. O `/health` do template é o único endpoint mínimo |
| Identidade | ASP.NET Core Identity (`IdentityUser`, `IdentityRole`) persistido via `IdentityDbContext<IdentityUser>` |
| Autenticação | Token bearer nativo do Identity (`AddBearerToken`). Sem JWT e sem chave de assinatura, portanto sem segredo de token na configuração |
| Persistência | EF Core 10 + SQLite, pacotes na versão 10.0.12 |
| Migrations | `sources/ExpenseHub.Api/Data/Migrations`, geradas com o `dotnet-ef` local (`dotnet-tools.json`). A API aplica as pendentes ao iniciar |
| Relógio | Todo "agora" vem de `TimeProvider` (`TimeProvider.System` em produção, `FixedTimeProvider` nos testes) |
| Testes | MSTest 4, sem biblioteca de mock: dublês escritos à mão em `ExpenseHub.UnitTests/TestDoubles` |

O EF Core 9+ lança exceção no `Migrate` quando o modelo tem mudanças sem migration. Toda alteração de modelo gera uma migration nova; confira com `dotnet tool run dotnet-ef migrations has-pending-model-changes --project sources/ExpenseHub.Api`.

## 2. Pastas, namespaces e visibilidade

```text
sources/ExpenseHub.Api/
  Program.cs     internal static class Program, async Task Main
  Controllers/   AuthController, AdminUsersController, ExpensesController, ExpenseWorkflowController
  Dtos/          somente DTOs de ENTRADA; todo arquivo tem atributo de validação
  Contracts/     respostas (records)
  Domain/        entidades, enums e AppRoles
  Data/          ExpenseHubDbContext, Configurations/, Migrations/, IExpenseRepository, ExpenseRepository,
                 ConcurrencyConflictException, UtcDateTimeConverter, DatabaseInitializer
  Services/      Actor, ServiceError, ServiceResult, ExpenseAccessPolicy e serviços (interface + implementação)
  Http/          ClaimsPrincipalExtensions (ToActor) e ControllerResultExtensions (ToActionResult)
sources/ExpenseHub.UnitTests/
  TestDoubles/   InMemoryExpenseRepository, FixedTimeProvider, TestActors
  Http/  Domain/  Identity/  Expenses/  Workflow/  Validation/
```

- Namespace = `ExpenseHub.Api.<Pasta>`, com declaração file-scoped.
- Não existe pasta `Models/`: entidades ficam em `Domain/`, e controllers recebem somente DTOs.
- Todo arquivo em `Dtos/` tem pelo menos um atributo de validação (`[Required]`, `[StringLength]`, `[Range]`, `[EmailAddress]`...). Respostas ficam em `Contracts/`.
- `public` só onde é obrigatório: controllers, interfaces de serviço usadas por controllers, DTOs, Contracts, `Actor`, `ServiceError`, `ServiceResult`, `AppRoles` e os enums expostos. Todo tipo e membro `public` tem `/// <summary>`, e, quando há `<param>`, todos os parâmetros são documentados.
- O restante é `internal sealed` ou `internal static`. O projeto de testes enxerga esses tipos via `InternalsVisibleTo`. Classes e métodos de teste são `public` (exigência do MSTest) e também têm `/// <summary>`.

## 3. Domínio (`Domain/`)

| Tipo | Descrição |
|---|---|
| `AppRoles` | Constantes `Admin`, `Employee`, `Approver`, `Finance`, `Auditor`; `ExpenseReaders` (as 4 roles com leitura de reembolsos, para `[Authorize(Roles = ...)]`); `All` |
| `ExpenseStatus` | `Draft`, `Submitted`, `Approved`, `Rejected`, `Paid` (gravado como texto) |
| `ExpenseAction` | `Created`, `Updated`, `Submitted`, `Approved`, `Rejected`, `Paid` (gravado como texto) |
| `ExpenseCategory` | `Id` e `Name` (único, até 100). Seed por `HasData`: 1 Alimentação, 2 Transporte, 3 Hospedagem, 4 Material de escritório, 5 Outros |
| `Expense` | Reembolso com um único valor (ver campos abaixo) |
| `ExpenseHistory` | Ação, reembolso, ator, horário UTC, estado anterior e posterior, justificativa e alterações. Criado por `ExpenseHistory.Record(...)` |
| `PaymentRecord` | Pagamento 1:1 com o reembolso: valor, quem pagou e quando. Criado por `PaymentRecord.Create(...)` |

Campos de `Expense` (setters privados; o EF usa o construtor privado):

| Campo | Tipo e regra |
|---|---|
| `Id` | `Guid` gerado pelo servidor (o domínio define o valor; o banco não gera) |
| `OwnerId` | `string`, FK para `AspNetUsers` (`Restrict`); vem do usuário autenticado |
| `CategoryId` / `Category` | FK para `ExpenseCategories` (`Restrict`) |
| `Description` | `string`, 10 a 500 caracteres |
| `Amount` | `decimal`, precisão (18,2), entre 0,01 e 2.147.483.647 |
| `ExpenseDate` | `DateOnly`, nunca futura |
| `Status` | `ExpenseStatus`, definido somente pelo servidor |
| `CreatedAtUtc`, `UpdatedAtUtc`, `SubmittedAtUtc` | `DateTime` UTC |
| `DecidedById`, `DecidedAtUtc` | quem aprovou ou reprovou, e quando |
| `RejectionReason` | justificativa da reprovação, até 500 caracteres |
| `ConcurrencyStamp` | `Guid` com `IsConcurrencyToken`, renovado a cada alteração |
| `Payment` | `PaymentRecord?` |

Comportamento do `Expense`, implementado issue a issue:

| Método | Issue |
|---|---|
| `static Expense CreateDraft(string ownerId, int categoryId, string description, decimal amount, DateOnly expenseDate, DateTime nowUtc)` | I04 |
| `string? UpdateDraft(int categoryId, string description, decimal amount, DateOnly expenseDate, DateTime nowUtc)`: devolve o texto das alterações, ou `null` se nada mudou | I04 |
| `void Submit(DateTime nowUtc)` | I05 |
| `void Approve(string approverId, DateTime nowUtc)` e `void Reject(string approverId, string justification, DateTime nowUtc)` | I07 |
| `PaymentRecord MarkAsPaid(string financeUserId, DateTime nowUtc)` | I08 |

Cada método protege a própria invariante (estado de origem errado gera `InvalidOperationException`), atualiza `UpdatedAtUtc` e renova `ConcurrencyStamp`. Os serviços checam o estado antes e respondem 409; a exceção do domínio é defesa em profundidade.

Datas e horas são `DateTime` em UTC, porque o SQLite não ordena `DateTimeOffset`. O `UtcDateTimeConverter` marca `DateTimeKind.Utc` na leitura, e o JSON sai com `Z`.

## 4. Persistência (`Data/`)

`IExpenseRepository` é o único acesso a dados dos serviços de reembolso:

```csharp
Task<bool> CategoryExistsAsync(int categoryId, CancellationToken cancellationToken);
Task<Expense?> FindAsync(Guid id, Expression<Func<Expense, bool>> scope, CancellationToken cancellationToken);   // rastreado; inclui Category e Payment
Task<IReadOnlyList<Expense>> ListAsync(Expression<Func<Expense, bool>> scope, CancellationToken cancellationToken); // filtro no SQL; sem rastreamento; mais recentes primeiro
Task<IReadOnlyList<ExpenseHistory>> ListHistoryAsync(Guid expenseId, CancellationToken cancellationToken);      // ordem cronológica
void Add(Expense expense);
void AddHistory(ExpenseHistory entry);
void AddPayment(PaymentRecord payment);
Task SaveChangesAsync(CancellationToken cancellationToken); // um commit; concorrência vira ConcurrencyConflictException
```

- O `scope` é aplicado ao `IQueryable`, antes de materializar: nada é carregado para depois ser filtrado em memória.
- Uma operação lógica (mudança de estado + histórico + pagamento) é confirmada com um único `SaveChangesAsync`, numa única transação.
- `DatabaseInitializer` aplica as migrations ao iniciar a API e faz o seed de roles e da conta Admin.

## 5. Serviços e HTTP

- `Actor(string UserId, IReadOnlySet<string> Roles)` é montado por `User.ToActor()` a partir das claims `NameIdentifier` e `Role` do token. Nunca vem do corpo da requisição.
- Os serviços devolvem `ServiceResult<T>`. As fábricas ficam na classe estática `ServiceResult` (`Success`, `NotFound<T>`, `Forbidden<T>`, `Conflict<T>`, `Invalid<T>`), e `ToFailure<TOther>()` propaga uma falha para outro tipo de resultado.
- `ControllerResultExtensions.ToActionResult(...)` traduz: `Validation` → 400 (`ValidationProblemDetails`), `NotFound` → 404, `Forbidden` → 403, `Conflict` → 409, todos em `ProblemDetails`.
- `Program.cs` configura `AddProblemDetails`, `UseExceptionHandler` e `UseStatusCodePages` (respostas vazias de 401/403/404 também saem em ProblemDetails). O JSON usa enums como texto e `UnmappedMemberHandling.Disallow`, então campo desconhecido gera 400.
- Autorização: `FallbackPolicy` exige usuário autenticado; rotas públicas usam `[AllowAnonymous]`.
- Recurso criado responde `201` com `Created($"/api/expenses/{id}", body)`. `CreatedAtAction` não é usado com actions terminadas em `Async`.

## 6. Regra de decisão (ordem fixa em todos os serviços)

1. **Role**: `[Authorize(Roles = ...)]` no controller e `actor.IsInRole(...)` no serviço → 403.
2. **Entrada**: DataAnnotations (automático) e validações de serviço (data futura, categoria inexistente, justificativa) → 400.
3. **Escopo** → 404:
   - leitura (lista, detalhe, histórico): `ExpenseAccessPolicy.ReadScope(actor)` aplicado na consulta;
   - editar e enviar: só o proprietário; reembolso de outra pessoa → 404, sem revelar que existe;
   - aprovar, reprovar e pagar: id inexistente ou `Draft` de outra pessoa → 404.
4. **Ownership**: aprovar, reprovar ou pagar o próprio reembolso → 403, mesmo acumulando roles.
5. **Estado**: transição inválida ou repetida → 409, sem gravar histórico.
6. **Persistência**: mudança + histórico (+ pagamento) num único `SaveChangesAsync`; `ConcurrencyConflictException` → 409.

`ExpenseAccessPolicy` (I05/I06) recebe dados primitivos, para ser testável em qualquer estado:

```csharp
static bool HasReadRole(Actor actor);                                               // Employee, Approver, Finance ou Auditor
static Expression<Func<Expense, bool>> ReadScope(Actor actor);                      // OR das roles: Auditor → tudo | Employee → próprios
                                                                                    // | Approver → Submitted | Finance → Approved ou Paid
static AccessDecision CanManageDraft(Actor actor, string ownerId);                  // !Employee → Forbidden | não é dono → NotFound | Allowed
static AccessDecision CanDecide(Actor actor, string ownerId, ExpenseStatus status); // !Approver → Forbidden | Draft de outro → NotFound | dono → Forbidden | Allowed
static AccessDecision CanPay(Actor actor, string ownerId, ExpenseStatus status);    // !Finance → Forbidden | Draft de outro → NotFound | dono → Forbidden | Allowed
```

## 7. Contrato HTTP

| Método e rota | Roles | Corpo | Sucesso e erros específicos |
|---|---|---|---|
| `POST /register` | anônimo | `RegisterRequest` | 201 `UserResponse` sem roles; e-mail duplicado → 409; senha fora da política → 400 |
| `POST /login` | anônimo | `LoginRequest` | 200 `{ tokenType, accessToken, expiresIn, refreshToken }`; credencial inválida ou bloqueio → 401 |
| `GET /api/me` | autenticado | — | 200 `UserResponse` com as roles do token |
| `GET /api/admin/users` | Admin | — | 200 `UserResponse[]` |
| `PUT /api/admin/users/{id}/roles` | Admin | `UpdateUserRolesRequest` | 200 `UserResponse`; role desconhecida → 400; usuário inexistente → 404; Admin removendo a própria role Admin → 409 |
| `POST /api/expenses` | Employee | `CreateExpenseRequest` | 201 `ExpenseResponse` |
| `PUT /api/expenses/{id}` | Employee | `UpdateExpenseRequest` | 200 `ExpenseResponse` |
| `GET /api/expenses` | `ExpenseReaders` | — | 200 `ExpenseResponse[]` |
| `GET /api/expenses/{id}` | `ExpenseReaders` | — | 200 `ExpenseResponse` |
| `POST /api/expenses/{id}/submit` | Employee | — | 200 `ExpenseResponse` |
| `POST /api/expenses/{id}/approve` | Approver | — | 200 `ExpenseResponse` |
| `POST /api/expenses/{id}/reject` | Approver | `RejectExpenseRequest` | 200 `ExpenseResponse` |
| `POST /api/expenses/{id}/pay` | Finance | — | 200 `ExpenseResponse` |
| `GET /api/expenses/{id}/history` | `ExpenseReaders` | — | 200 `ExpenseHistoryResponse[]` |

Para todas as rotas: anônimo → 401; autenticado sem a role da rota → 403.

DTOs de entrada:

| DTO | Campos e validação |
|---|---|
| `RegisterRequest` | `Email`: `[Required][EmailAddress][StringLength(256)]` · `Password`: `[Required][StringLength(100, MinimumLength = 8)]` + política do Identity |
| `LoginRequest` | `Email`: `[Required][EmailAddress]` · `Password`: `[Required]` |
| `UpdateUserRolesRequest` | `Roles`: `[Required]`, lista que substitui o conjunto atual (`[]` remove todas) |
| `CreateExpenseRequest` / `UpdateExpenseRequest` | `Description`: `[Required][StringLength(500, MinimumLength = 10)]` · `decimal? Amount`: `[Required][Range(typeof(decimal), "0.01", "2147483647", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]` · `DateOnly? ExpenseDate`: `[Required]` · `int? CategoryId`: `[Required][Range(1, int.MaxValue)]` |
| `RejectExpenseRequest` | `Justification`: `[Required][StringLength(500, MinimumLength = 10)]` |

Tipos anuláveis com `[Required]` evitam que um campo ausente vire 0 ou 0001-01-01 em silêncio.

Respostas:

- `UserResponse(string Id, string Email, IReadOnlyList<string> Roles)`
- `ExpenseResponse(Guid Id, string OwnerId, int CategoryId, string CategoryName, string Description, decimal Amount, DateOnly ExpenseDate, ExpenseStatus Status, DateTime CreatedAtUtc, DateTime UpdatedAtUtc, DateTime? SubmittedAtUtc, string? DecidedById, DateTime? DecidedAtUtc, string? RejectionReason, string? PaidById, DateTime? PaidAtUtc)`
- `ExpenseHistoryResponse(long Id, ExpenseAction Action, string ActorId, DateTime OccurredAtUtc, ExpenseStatus? PreviousStatus, ExpenseStatus NewStatus, string? Justification, string? Changes)`

## 8. Testes

- `TestDoubles/InMemoryExpenseRepository`: inclusões ficam pendentes até `SaveChangesAsync`, que as confirma juntas. `FailNextSave` e `ConflictOnNextSave` simulam falha de persistência e conflito de concorrência (as pendências são descartadas). `Seed(expense)` grava direto como confirmado. `SaveChangesCount` conta as chamadas. Os escopos são aplicados como expressões, como no banco.
- `TestDoubles/FixedTimeProvider`: relógio fixo (01/10/2026 12:00 UTC por padrão), com `Advance(...)`.
- `TestDoubles/TestActors`: `Employee1`, `Employee2`, `Approver`, `ApproverEmployee`, `Finance`, `FinanceEmployee`, `Auditor`, `Admin`, `NoRole`.
- Nomes `Metodo_Cenario_ResultadoEsperado`, uma regra por teste, verificando o resultado e os efeitos colaterais (histórico, pagamento, `SaveChangesCount`).
- Senhas de teste nunca são literais atribuídos a identificadores como `Password`, `Senha`, `Token` ou `Secret`. Use `string.Empty` ou `new string('x', 12)`; o scanner do CI trata o literal como segredo versionado.

## 9. Qualidade

O relatório `code-quality` precisa ficar em 100/100 em todo PR:

- zero warnings de compilador e de analisadores;
- `dotnet format --verify-no-changes --severity warn` limpo;
- nenhum segredo versionado (o Gitleaks varre o histórico inteiro);
- nenhum arquivo gerado (`bin/`, `obj/`, `*.db`) no Git.

Não se altera `.editorconfig`, `Directory.Build.props`, `.github/`, `scripts/` nem `tests/`.
