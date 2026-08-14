# Copiloto RAG da Empresa

Assistente de chat consultivo (RAG) 100% local, em português, que responde apenas com
base em documentos aprovados pela própria empresa.

## Visão geral

- **Web pública** — Blazor Server em `http://127.0.0.1:8080` (chat + área administrativa).
- **API interna** — ASP.NET Core em `http://127.0.0.1:5081` (chat, administração, health).
- **Conhecimento** — PostgreSQL + pgvector (`pgvector/pgvector:pg17`) via Docker,
  busca híbrida (cosseno + texto em português) com fusão RRF.
- **Geração** — Ollama local com modelo `empresa-copiloto:v1` (derivado de `qwen3:8b`)
  e embeddings `embeddinggemma` (768 dimensões).
- **Exposição externa** — Cloudflare Quick Tunnel aponta apenas para a Web; a área
  `/admin` é bloqueada fora do loopback.

## Repositório

```
src/CompanyCopilot.Domain          entidades, enums e regras de domínio
src/CompanyCopilot.Application     orquestração RAG, evidência, limitação, chat
src/CompanyCopilot.Infrastructure  EF Core/pgvector, busca, Ollama, extração de arquivos
src/CompanyCopilot.Api             endpoints HTTP (chat streaming NDJSON, admin, health)
src/CompanyCopilot.Web             Blazor Server (chat público + admin)
tests/CompanyCopilot.UnitTests     61 testes unitários
tests/CompanyCopilot.IntegrationTests  8 testes de integração (WebApplicationFactory)
infra/docker-compose.yml           PostgreSQL com pgvector
docs/Modelfile                     prompt e parâmetros do modelo local
docs/exemplos/                     documentos de demonstração
docs/avaliacao-rag.md              40 casos de avaliação funcional
docs/setup-windows.md              guia de instalação no Windows
```

## Regras do copiloto

- Busca somente documentos **Public**, **Approved** e vigentes.
- Sem evidência → recusa educada, sem inventar resposta.
- Fontes de mesma prioridade divergentes → recusa por conflito.
- Categorias autoritativas (Regras, Pagamento, Horários, Política) prevalecem sobre
  FAQ/institucional.
- Histórico limitado a 6 turnos; sessão expira após 20 min de inatividade.
- 8 perguntas por IP a cada 10 min; 1 geração por vez com fila de 5.
- Documentos são tratados como dados não confiáveis (instruções embutidas são ignoradas
  pelo prompt restrito).
- Uploads: `.pdf`, `.docx`, `.xlsx`, `.txt`, `.md`, `.html`, `.htm`, até 10 MB,
  deduplicação por SHA-256.

## Início rápido

```powershell
Copy-Item .env.example .env        # ajuste a senha
docker compose -f infra/docker-compose.yml up -d
Start-Process ollama serve -WindowStyle Hidden
dotnet ef database update --project src/CompanyCopilot.Infrastructure --startup-project src/CompanyCopilot.Api
dotnet run --project src/CompanyCopilot.Api
dotnet run --project src/CompanyCopilot.Web
cloudflared tunnel --url http://127.0.0.1:8080
```

Detalhes em `docs/setup-windows.md`.

## Testes

```powershell
dotnet test tests/CompanyCopilot.UnitTests
dotnet test tests/CompanyCopilot.IntegrationTests
```

## Avaliação

`docs/avaliacao-rag.md` define 40 casos (sucesso, recusa sem evidência, conflito,
robustez) com critérios de aceite e tabela de registro.
