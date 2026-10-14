# Evidências de autorização

Este documento comprova a [matriz de autorização](MATRIZ-AUTORIZACAO.md): para cada rota de reembolsos e cada perfil, mostra o status **esperado**, o status **observado** numa execução real e o **teste unitário** que cobre o caso.

## Como foi gerado

- API local em `http://localhost:5245`, com o banco SQLite **zerado** (sem `expensehub.db`) e os sete usuários de demonstração criados por `POST /register` e `PUT /api/admin/users/{id}/roles`, como na seção I03 do [`.http`](../sources/ExpenseHub.Api/ExpenseHub.Api.http). Cada perfil fez login depois de receber a role (as roles vão dentro do token).
- 63 requisições (cada linha das tabelas abaixo), todas com o status esperado. As mesmas requisições estão na seção "I06 — Matriz de acesso" do `.http`, para o professor repetir.
- Os testes unitários citados ficam em `sources/ExpenseHub.UnitTests`. A 1ª camada (atributo `[Authorize(Roles = ...)]` no controller) é validada por `ExpensesControllerAuthorizationTests`; as demais (role, ownership e estado no **serviço**) por `ExpenseAccessPolicyTests`, `ExpenseReadScopeTests` e pelos testes de serviço.

## Perfis

| Perfil | Quem é no teste |
|---|---|
| Anônimo | requisição sem token |
| Sem role | usuário cadastrado, sem nenhuma role atribuída |
| Employee dono | `funcionario1` (Employee), dono do reembolso |
| Employee outro | `funcionario2` (Employee), que **não** é o dono |
| Approver | `aprovador` (somente Approver) |
| Approver dono | `aprovador.funcionario` (Approver + Employee), dono do próprio reembolso |
| Finance | `financeiro` (somente Finance) |
| Finance dono | `financeiro.funcionario` (Finance + Employee), dono do próprio reembolso |
| Auditor | `auditor` (somente Auditor) |
| Admin | `admin` (somente Admin) |

"Approver dono" e "Finance dono" mostram o efeito de **acumular roles**: a união das permissões vale, mas ninguém ganha acesso ao reembolso de outra pessoa por causa da role acumulada.

## Regra de decisão (ordem fixa)

1. **Role** → `403` (atributo no controller e `IsInRole` no serviço).
2. **Entrada** → `400`.
3. **Escopo** → `404` (leitura: `ReadScope` aplicado na consulta; editar e enviar: só o dono).
4. **Ownership** → `403` (aprovar, reprovar ou pagar o próprio reembolso).
5. **Estado** → `409`.
6. **Persistência** → mudança e histórico num único `SaveChangesAsync`; conflito de concorrência → `409`.

`401` vem antes de tudo: a `FallbackPolicy` exige usuário autenticado em toda rota.

## `POST /api/expenses` (criar)

| Perfil | Cenário | Esperado | Observado | Teste unitário |
|---|---|:---:|:---:|---|
| Anônimo | criar | 401 | 401 | `ExpensesController_EveryAction_DeclaresRoles` (o `401` é da `FallbackPolicy`) |
| Sem role | criar | 403 | 403 | `ExpenseServiceCreateTests.CreateAsync_ActorWithNoRole_ReturnsForbiddenAndSavesNothing` |
| Employee dono | criar | 201 | 201 | `ExpenseServiceCreateTests.CreateAsync_EmployeeWithValidData_CreatesDraftOwnedByActor` |
| Approver | criar | 403 | 403 | `ExpenseServiceCreateTests.CreateAsync_ActorWithoutEmployeeRole_ReturnsForbiddenAndSavesNothing` |
| Approver dono | criar o próprio | 201 | 201 | `ExpenseServiceCreateTests.CreateAsync_ApproverWithEmployeeRole_CreatesOwnDraft` |
| Finance | criar | 403 | 403 | `ExpenseServiceCreateTests.CreateAsync_ActorWithoutEmployeeRole_ReturnsForbiddenAndSavesNothing` |
| Finance dono | criar o próprio | 201 | 201 | `ExpenseServiceCreateTests.CreateAsync_EmployeeWithValidData_CreatesDraftOwnedByActor` |
| Auditor | criar | 403 | 403 | `ExpenseServiceCreateTests.CreateAsync_ActorWithoutEmployeeRole_ReturnsForbiddenAndSavesNothing` |
| Admin | criar | 403 | 403 | `ExpenseServiceCreateTests.CreateAsync_ActorWithoutEmployeeRole_ReturnsForbiddenAndSavesNothing` |

Atributo da rota: `ExpensesControllerAuthorizationTests.WriteActions_RequireEmployeeRole`.

## `PUT /api/expenses/{id}` (editar)

| Perfil | Cenário | Esperado | Observado | Teste unitário |
|---|---|:---:|:---:|---|
| Anônimo | rascunho de funcionario1 | 401 | 401 | `ExpensesController_EveryAction_DeclaresRoles` |
| Sem role | rascunho de funcionario1 | 403 | 403 | `ExpenseServiceUpdateTests.UpdateAsync_ActorWithNoRole_ReturnsForbidden` |
| Employee dono | próprio Draft | 200 | 200 | `ExpenseServiceUpdateTests.UpdateAsync_OwnerChangesDraft_AppliesNewData` |
| Employee dono | reembolso já enviado | 409 | 409 | `ExpenseServiceUpdateTests.UpdateAsync_ExpenseNotInDraft_ReturnsConflictAndKeepsData`, `ExpenseServiceSubmitTests.UpdateAsync_AfterSubmission_ReturnsConflictWithoutNewHistory` |
| Employee outro | rascunho de funcionario1 | 404 | 404 | `ExpenseServiceUpdateTests.UpdateAsync_AnotherEmployeesDraft_ReturnsNotFoundAndKeepsDraft`, `ExpenseAccessPolicyTests.CanManageDraft_OtherEmployee_ReturnsNotFound` |
| Approver | rascunho de funcionario1 | 403 | 403 | `ExpenseServiceUpdateTests.UpdateAsync_ActorWithoutEmployeeRole_ReturnsForbidden` |
| Approver dono | próprio Draft | 200 | 200 | `ExpenseAccessPolicyTests.CanManageDraft_AccumulatedRolesOnOwnDraft_ReturnsAllowed` |
| Approver dono | rascunho de funcionario1 | 404 | 404 | `ExpenseServiceUpdateTests.UpdateAsync_ApproverEmployeeOnAnotherDraft_ReturnsNotFound` |
| Finance | rascunho de funcionario1 | 403 | 403 | `ExpenseServiceUpdateTests.UpdateAsync_ActorWithoutEmployeeRole_ReturnsForbidden` |
| Finance dono | próprio Draft | 200 | 200 | `ExpenseAccessPolicyTests.CanManageDraft_AccumulatedRolesOnOwnDraft_ReturnsAllowed` |
| Finance dono | rascunho de funcionario1 | 404 | 404 | `ExpenseAccessPolicyTests.CanManageDraft_AccumulatedRolesOnOtherOwner_ReturnsNotFound` |
| Auditor | rascunho de funcionario1 | 403 | 403 | `ExpenseServiceUpdateTests.UpdateAsync_ActorWithoutEmployeeRole_ReturnsForbidden` |
| Admin | rascunho de funcionario1 | 403 | 403 | `ExpenseServiceUpdateTests.UpdateAsync_ActorWithoutEmployeeRole_ReturnsForbidden` |

## `POST /api/expenses/{id}/submit` (enviar)

| Perfil | Cenário | Esperado | Observado | Teste unitário |
|---|---|:---:|:---:|---|
| Anônimo | rascunho de funcionario1 | 401 | 401 | `ExpensesController_EveryAction_DeclaresRoles` |
| Sem role | rascunho de funcionario1 | 403 | 403 | `ExpenseServiceSubmitTests.SubmitAsync_ActorWithNoRole_ReturnsForbidden` |
| Employee dono | próprio Draft | 200 | 200 | `ExpenseServiceSubmitTests.SubmitAsync_OwnerDraft_ChangesStatusAndFillsSubmissionTime` |
| Employee dono | reenvio do mesmo reembolso | 409 | 409 | `ExpenseServiceSubmitTests.SubmitAsync_SecondSubmission_ReturnsConflictWithSingleSubmittedHistory` |
| Employee outro | rascunho de funcionario1 | 404 | 404 | `ExpenseServiceSubmitTests.SubmitAsync_AnotherEmployeesDraft_ReturnsNotFoundAndKeepsDraft` |
| Approver | rascunho de funcionario1 | 403 | 403 | `ExpenseServiceSubmitTests.SubmitAsync_ActorWithoutEmployeeRole_ReturnsForbidden` |
| Approver dono | próprio Draft | 200 | 200 | `ExpenseServiceSubmitTests.SubmitAsync_ApproverEmployeeOnOwnDraft_Succeeds` |
| Approver dono | rascunho de funcionario1 | 404 | 404 | `ExpenseServiceSubmitTests.SubmitAsync_ApproverEmployeeOnAnotherDraft_ReturnsNotFound` |
| Finance | rascunho de funcionario1 | 403 | 403 | `ExpenseServiceSubmitTests.SubmitAsync_ActorWithoutEmployeeRole_ReturnsForbidden` |
| Finance dono | próprio Draft | 200 | 200 | `ExpenseAccessPolicyTests.CanManageDraft_AccumulatedRolesOnOwnDraft_ReturnsAllowed` |
| Finance dono | rascunho de funcionario1 | 404 | 404 | `ExpenseAccessPolicyTests.CanManageDraft_AccumulatedRolesOnOtherOwner_ReturnsNotFound` |
| Auditor | rascunho de funcionario1 | 403 | 403 | `ExpenseServiceSubmitTests.SubmitAsync_ActorWithoutEmployeeRole_ReturnsForbidden` |
| Admin | rascunho de funcionario1 | 403 | 403 | `ExpenseServiceSubmitTests.SubmitAsync_ActorWithoutEmployeeRole_ReturnsForbidden` |

## `GET /api/expenses` (listar)

O filtro é aplicado na consulta ao banco (`ExpenseAccessPolicy.ReadScope` vira o `WHERE`). A coluna "Observado" traz o status e a quantidade de itens devolvidos, num banco com 5 reembolsos de 4 donos (2 de funcionario1, sendo um `Submitted`; 1 de funcionario2; 1 de aprovador.funcionario; 1 de financeiro.funcionario).

| Perfil | Cenário | Esperado | Observado | Teste unitário |
|---|---|:---:|:---:|---|
| Anônimo | sem token | 401 | 401 | `ExpensesController_EveryAction_DeclaresRoles` |
| Sem role | token sem roles | 403 | 403 | `ExpenseServiceQueryTests.ListAsync_ActorWithoutReadRole_ReturnsForbidden` |
| Employee dono | só os próprios | 200, 2 itens | 200, 2 itens | `ExpenseReadScopeTests.ReadScope_Employee_SeesOnlyOwnExpensesInAnyStatus`, `ExpenseServiceQueryTests.ListAsync_Employee_ReturnsOnlyOwnExpenses` |
| Employee outro | só os próprios (nenhum de funcionario1) | 200, 1 item | 200, 1 item | `ExpenseReadScopeTests.ReadScope_OtherEmployee_SeesOnlyOwnExpenses` |
| Approver | só `Submitted`, nenhum Draft | 200, 1 item | 200, 1 item | `ExpenseReadScopeTests.ReadScope_Approver_SeesOnlySubmitted`, `ExpenseServiceQueryTests.ListAsync_Approver_ReturnsSubmittedAndNeverDrafts` |
| Approver dono | próprios + `Submitted` | 200, 2 itens | 200, 2 itens | `ExpenseReadScopeTests.ReadScope_ApproverWithEmployee_SeesOwnPlusAllSubmitted` |
| Finance | só `Approved` e `Paid` (ainda nenhum) | 200, 0 itens | 200, 0 itens | `ExpenseReadScopeTests.ReadScope_Finance_SeesOnlyApprovedAndPaid` |
| Finance dono | próprios + `Approved`/`Paid` | 200, 1 item | 200, 1 item | `ExpenseReadScopeTests.ReadScope_FinanceWithEmployee_SeesOwnPlusApprovedAndPaid` |
| Auditor | todos | 200, 5 itens | 200, 5 itens | `ExpenseReadScopeTests.ReadScope_Auditor_SeesEverything`, `ExpenseServiceQueryTests.ListAsync_Auditor_ReturnsEverythingMostRecentFirst` |
| Admin | Admin sozinho | 403 | 403 | `ExpenseServiceQueryTests.ListAsync_ActorWithoutReadRole_ReturnsForbidden`, `ExpenseReadScopeTests.ReadScope_AdminOnly_SeesNothing` |

Atributo das rotas de leitura: `ExpensesControllerAuthorizationTests.ReadActions_RequireOneOfTheReaderRoles`. Coerência com a política: `ExpenseAccessPolicyTests.HasReadRole_EveryKnownRole_AgreesWithExpenseReadersConstant`.

## `GET /api/expenses/{id}` (detalhe)

Reembolso inexistente e reembolso fora do escopo respondem **exatamente igual** (`404`, mesma mensagem), então nada vaza.

| Perfil | Cenário | Esperado | Observado | Teste unitário |
|---|---|:---:|:---:|---|
| Anônimo | Draft de funcionario1 | 401 | 401 | `ExpensesController_EveryAction_DeclaresRoles` |
| Sem role | Draft de funcionario1 | 403 | 403 | `ExpenseServiceQueryTests.GetAsync_ActorWithoutReadRole_ReturnsForbidden` |
| Employee dono | próprio Draft | 200 | 200 | `ExpenseServiceQueryTests.GetAsync_Owner_ReturnsExpense` |
| Employee dono | id inexistente | 404 | 404 | `ExpenseServiceQueryTests.GetAsync_MissingAndInvisible_AreIndistinguishable` |
| Employee outro | Draft de funcionario1 | 404 | 404 | `ExpenseServiceQueryTests.GetAsync_AnotherEmployeesExpense_ReturnsNotFound` |
| Employee outro | `Submitted` de funcionario1 | 404 | 404 | `ExpenseServiceQueryTests.GetAsync_AnotherEmployeesExpense_ReturnsNotFound` |
| Approver | `Submitted` de funcionario1 | 200 | 200 | `ExpenseServiceQueryTests.GetAsync_Approver_SeesSubmittedButNotAnotherEmployeesDraft` |
| Approver | Draft de funcionario1 | 404 | 404 | `ExpenseServiceQueryTests.GetAsync_Approver_SeesSubmittedButNotAnotherEmployeesDraft` |
| Approver dono | próprio Draft | 200 | 200 | `ExpenseReadScopeTests.ReadScope_ApproverWithEmployee_SeesOwnPlusAllSubmitted` |
| Approver dono | Draft de funcionario1 | 404 | 404 | `ExpenseReadScopeTests.ReadScope_ApproverWithEmployee_SeesOwnPlusAllSubmitted` |
| Approver dono | `Submitted` de funcionario1 | 200 | 200 | `ExpenseReadScopeTests.ReadScope_ApproverWithEmployee_SeesOwnPlusAllSubmitted` |
| Finance | `Submitted` de funcionario1 | 404 | 404 | `ExpenseServiceQueryTests.GetAsync_Finance_SeesApprovedButNotSubmitted` |
| Finance | Draft de funcionario1 | 404 | 404 | `ExpenseReadScopeTests.ReadScope_Finance_SeesOnlyApprovedAndPaid` |
| Finance dono | próprio Draft | 200 | 200 | `ExpenseReadScopeTests.ReadScope_FinanceWithEmployee_SeesOwnPlusApprovedAndPaid` |
| Finance dono | `Submitted` de funcionario1 | 404 | 404 | `ExpenseReadScopeTests.ReadScope_FinanceWithEmployee_SeesOwnPlusApprovedAndPaid` |
| Auditor | Draft de funcionario1 | 200 | 200 | `ExpenseServiceQueryTests.GetAsync_Auditor_SeesAnyExpense` |
| Auditor | `Submitted` de funcionario1 | 200 | 200 | `ExpenseReadScopeTests.ReadScope_Auditor_SeesEverything` |
| Admin | `Submitted` de funcionario1 | 403 | 403 | `ExpenseServiceQueryTests.GetAsync_ActorWithoutReadRole_ReturnsForbidden` |

## Rotas da I07 e da I08 (a validar)

A política contextual já está pronta e testada (`ExpenseAccessPolicy.CanDecide` e `CanPay`, com todos os estados, donos e roles em `ExpenseAccessPolicyTests`). Estas rotas ficam para a Pessoa 3 completar com o status **observado** e o teste do serviço, depois da I07 e da I08. O esperado abaixo segue a [matriz](MATRIZ-AUTORIZACAO.md) e a ordem de decisão acima.

### `POST /api/expenses/{id}/approve` e `POST /api/expenses/{id}/reject`

| Perfil | Cenário | Esperado | Observado | Teste unitário |
|---|---|:---:|:---:|---|
| Anônimo | qualquer | 401 | a validar na I07 | a validar na I07 |
| Sem role, Employee, Finance, Auditor, Admin | qualquer | 403 | a validar na I07 | `ExpenseAccessPolicyTests.CanDecide_RoleWithoutApprover_ReturnsForbiddenForEveryOwnerAndStatus` |
| Approver | `Submitted` de outra pessoa | 200 | a validar na I07 | `ExpenseAccessPolicyTests.CanDecide_ApproverOnOthersNonDraft_ReturnsAllowedForEveryStatus` |
| Approver | Draft de outra pessoa | 404 | a validar na I07 | `ExpenseAccessPolicyTests.CanDecide_ApproverOnOthersDraft_ReturnsNotFound` |
| Approver | já decidido (`Approved`, `Rejected`) ou `Paid` | 409 | a validar na I07 | a validar na I07 |
| Approver dono | o **próprio** reembolso, em qualquer estado | 403 | a validar na I07 | `ExpenseAccessPolicyTests.CanDecide_ApproverEmployeeOnOwnExpense_ReturnsForbiddenForEveryStatus` |
| Approver dono | `Submitted` de outra pessoa | 200 | a validar na I07 | `ExpenseAccessPolicyTests.CanDecide_ApproverEmployeeOnOthersSubmitted_ReturnsAllowed` |
| Approver (`reject`) | justificativa ausente ou fora de 10 a 500 caracteres | 400 | a validar na I07 | a validar na I07 |

### `POST /api/expenses/{id}/pay`

| Perfil | Cenário | Esperado | Observado | Teste unitário |
|---|---|:---:|:---:|---|
| Anônimo | qualquer | 401 | a validar na I08 | a validar na I08 |
| Sem role, Employee, Approver, Auditor, Admin | qualquer | 403 | a validar na I08 | `ExpenseAccessPolicyTests.CanPay_RoleWithoutFinance_ReturnsForbiddenForEveryOwnerAndStatus` |
| Finance | `Approved` de outra pessoa | 200 | a validar na I08 | `ExpenseAccessPolicyTests.CanPay_FinanceOnOthersNonDraft_ReturnsAllowedForEveryStatus` |
| Finance | Draft de outra pessoa | 404 | a validar na I08 | `ExpenseAccessPolicyTests.CanPay_FinanceOnOthersDraft_ReturnsNotFound` |
| Finance | `Submitted`, `Rejected` ou `Paid` de outra pessoa | 409 | a validar na I08 | a validar na I08 |
| Finance dono | o **próprio** reembolso, em qualquer estado | 403 | a validar na I08 | `ExpenseAccessPolicyTests.CanPay_FinanceEmployeeOnOwnExpense_ReturnsForbiddenForEveryStatus` |
| Finance dono | `Approved` de outra pessoa | 200 | a validar na I08 | `ExpenseAccessPolicyTests.CanPay_FinanceEmployeeOnOthersApproved_ReturnsAllowed` |

### `GET /api/expenses/{id}/history`

Mesma visibilidade do detalhe (`ReadScope`).

| Perfil | Cenário | Esperado | Observado | Teste unitário |
|---|---|:---:|:---:|---|
| Anônimo | qualquer | 401 | a validar na I08 | a validar na I08 |
| Sem role, Admin | qualquer | 403 | a validar na I08 | a validar na I08 |
| Employee dono | próprio reembolso | 200 | a validar na I08 | a validar na I08 |
| Employee outro | reembolso alheio | 404 | a validar na I08 | a validar na I08 |
| Approver | `Submitted` / Draft alheio | 200 / 404 | a validar na I08 | a validar na I08 |
| Finance | `Approved` ou `Paid` / `Submitted` | 200 / 404 | a validar na I08 | a validar na I08 |
| Auditor | qualquer | 200 | a validar na I08 | a validar na I08 |

## Observações

- **`403` × `404`.** O `403` só aparece quando falta a role (ou, para aprovar e pagar, quando o ator é o próprio dono). Quem tem a role mas não enxerga o recurso recebe `404`, sem distinção entre "não existe" e "não é seu".
- **Admin sem acesso funcional.** O Admin só administra usuários e roles: recebe `403` em todas as rotas de reembolsos, inclusive nas de leitura.
- **Auditor não escreve.** Recebe `403` em criar, editar e enviar, e vê tudo na leitura.
- **Roles acumuladas.** Valem a união das permissões, mas Approver + Employee não acessa o rascunho de outra pessoa, e Finance + Employee não aprova nem paga o que é seu.
- **Roles vão no token.** Depois de uma alteração de roles, o usuário precisa fazer login de novo para a mudança valer.
