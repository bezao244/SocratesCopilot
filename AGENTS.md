# AGENTS.md

Copiloto RAG local PT-BR: .NET 10, Blazor Server (Web), API ASP.NET Core, EF Core + PostgreSQL/pgvector (Docker), Ollama local. Solução: `CompanyCopilot.slnx` (não `.sln`).

## Comandos

```powershell
dotnet build CompanyCopilot.slnx -v q
dotnet test tests/CompanyCopilot.UnitTests --nologo        # 67 testes
dotnet test tests/CompanyCopilot.IntegrationTests --nologo # 10 testes, sem serviços externos (fakes)
dotnet ef database update --project src/CompanyCopilot.Infrastructure --startup-project src/CompanyCopilot.Api
dotnet ef migrations add Nome --project src/CompanyCopilot.Infrastructure --startup-project src/CompanyCopilot.Api
```

Subir tudo: `docker compose -f infra/docker-compose.yml up -d`; `ollama serve`; `dotnet run --project src/CompanyCopilot.Api` (:5081); `dotnet run --project src/CompanyCopilot.Web` (:8080). Detalhes em `docs/setup-windows.md`.

## Tarefas (protocolo de execução)

- Lista de tarefas em `docs/tarefas.md` — **executar estritamente em ordem (T1 → T2 → ...)**.
- Validar os critérios de aceite de cada tarefa (build, testes, verificações listadas) **antes** de marcá-la como concluída; se falhar, **parar e reportar**, sem avançar.
- Atualizar checkboxes e a tabela de histórico ao concluir. Novas tarefas seguem o template do arquivo.

## E2E local (Windows)

- Parar API/Web em execução: `Get-Process -Name CompanyCopilot.Api | Stop-Process -Force` (idem `.Web`). Rodar com `--no-build` **só usa DLLs recém-buildadas se `dotnet build src/CompanyCopilot.Api` (ou `.Web`) rodou depois da última edição** — build parcial (ex.: só Infrastructure) não atualiza os binários dos projetos que referenciam.
- Serviços em background: `cmd /c "start /b dotnet run --project <caminho abs> --no-build > log 2>&1"` (Start-Process com redirect falha).
- Logs de runtime: `api-run.log`/`web-run.log` em `%TEMP%\opencode\` (regerados a cada start; `--no-build` não apaga o log — remover antes de subir).

## PowerShell 5.1 — encodings (fonte de bugs reais)

- `.ps1` sem BOM é lido como **ANSI/CP1252**: acentos viram mojibake (ex.: `Horário` → `HorÃ¡rio`). Scripts com acentos precisam de BOM UTF-8 ou ASCII puro.
- Pipe para processo nativo usa `$OutputEncoding` (ASCII): acentos viram `?`. Para enviar bytes UTF-8 crus a psql/curl, usar arquivo + `cmd /c "docker exec -i ... < arquivo.sql"` (PowerShell não suporta `<`).
- Corpos JSON para curl: `[System.IO.File]::WriteAllText(path, json, [System.Text.UTF8Encoding]::new($false))` — BOM quebra o parse (`invalid character 'ï'`).

## API admin (curl)

- Requer antiforgery: cookies (`-c`/`-b`) + token de `GET /api/v1/admin/antiforgery` enviado no header `X-CSRF-TOKEN`; todas as mutações (`POST/PATCH/approve`) exigem o token.
- Upload: `curl.exe -F "file=@..."` (PS 5.1 não tem `-Form` no Invoke-RestMethod). PATCH de metadados exige corpo **completo** do `UpdateDocumentRequest`.
- `[ValidateAntiForgeryToken]` exige `AddControllersWithViews()` no `Program.cs` — `AddControllers()` sozinho quebra o admin em runtime.
- Chat: `POST /api/v1/chat/messages` retorna **NDJSON camelCase** com eventos `session`, `delta`, `sources`, `refusal`, `error`, `done` (`error` e `refusal` também emitem `done`).
- Sessões persistidas: tabelas `chat_sessions`/`chat_messages` (FK por `SessionId` string, cascade). Endpoints: `GET /api/v1/chat/sessions`, `GET /api/v1/chat/sessions/{id}/messages`, `DELETE /api/v1/chat/sessions/{id}`. O agente adota o `sessionId` enviado pelo cliente (novo id → nova conversa; id persistido → hidrata histórico do banco no `IChatSessionStore` via `Adopt`); título = 1ª pergunta truncada (200).

## Ollama

- `empresa-copiloto:v1` (base `qwen3:8b`) tem modo thinking por padrão: `message.thinking` preenchido com `content` vazio. Sempre `"think": false` no corpo de `/api/chat`.
- Streaming: linhas **JSON puro por linha** (sem prefixo `data:`), chaves minúsculas (`embeddings`, `message`, `done`) — desserializar com `JsonSerializerOptions.Web` (case-insensitive).
- Mudou `docs/Modelfile`? Recriar: `ollama create empresa-copiloto -f docs\Modelfile`.

## EF Core / pgvector

- `EF.Functions.PlainToTsQuery("portuguese", ...)` precisa ficar **dentro** da expressão LINQ (variável externa → client-evaluation → `InvalidOperationException`).
- **DbContext não é thread-safe**: busca vetorial e textual não podem rodar em paralelo no mesmo contexto (rodar sequencial).
- Não usar `.AsNoTracking()` onde o status precisa persistir: removê-lo do `GetRecentIngestionJobsAsync` destravou o loop de reindexação.
- Banco: tabelas minúsculas (`knowledge_documents`), colunas PascalCase (`"Id"`, `"Title"`) — quotar colunas no psql.
- PostgreSQL local do Windows (`postgresql-x64-18`) disputa a 5432: parar o serviço para subir o container.

## Produto/UX (não regredir)

- O modelo **não** cita fontes na resposta (`[Fonte: ID]` removido do prompt). Fontes vão no evento `sources` e aparecem no dropdown `<details>` fechado ("Fontes (N)").
- Responsividade (não regredir): breakpoints no fim de `wwwroot/app.css` — ≥1200px desktop atual; 768–1199px reduz espaçamentos; <768px sem scroll horizontal, sidebar vira drawer fixo (`chat-sidebar.open` + `sidebar-backdrop`, botão `sidebar-toggle` em `chat-mobile-bar`), tabelas admin viram cards via `data-label` + `td::before` (cada `<td>` de tabela precisa de `data-label`), `.upload-row`/`.edit-grid` empilham em coluna.
- Demo local: 5 docs em `docs/exemplos/` já ingeridos e aprovados no banco — qualquer pergunta de teste funciona sem reconfigurar.
- Avaliação manual: `docs/avaliacao-rag.md` (40 casos).
