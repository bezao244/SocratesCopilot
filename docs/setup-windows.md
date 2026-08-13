# Setup — Windows

Pré-requisitos instalados e verificados nesta máquina:

| Ferramenta | Versão | Checagem |
|------------|--------|----------|
| .NET SDK | 10.x | `dotnet --version` |
| Docker Desktop | 29.x | `docker --version` |
| Ollama | 0.32.x | `ollama --version` |

## 1. Configuração

```powershell
# Copie o exemplo de configuração e ajuste a senha do banco
Copy-Item .env.example .env
# edite .env conforme necessário (senha do PostgreSQL, etc.)
```

O arquivo `appsettings.json` de cada projeto (`src/CompanyCopilot.Api` e
`src/CompanyCopilot.Web`) já contém os valores padrão de loopback
(API `127.0.0.1:5081`, Web `127.0.0.1:8080`, PostgreSQL `127.0.0.1:5432`,
Ollama `127.0.0.1:11434`).

## 2. Banco de dados (Docker)

```powershell
# Se houver um PostgreSQL local usando a porta 5432, pare o serviço:
Stop-Service postgresql-x64-18   # (ajuste o nome do serviço; requer PowerShell elevado)

docker compose -f infra/docker-compose.yml up -d
```

## 3. Ollama

```powershell
# Inicie o servidor em segundo plano (uma vez)
Start-Process ollama serve -WindowStyle Hidden

# Verifique os modelos (baixados na primeira configuração)
ollama list
#   empresa-copiloto:v1
#   embeddinggemma:latest

# Se necessário, recrie o modelo a partir do Modelfile:
# ollama create empresa-copiloto:v1 -f docs/Modelfile
```

## 4. Banco de dados (migrations)

```powershell
dotnet ef database update --project src/CompanyCopilot.Infrastructure --startup-project src/CompanyCopilot.Api
```

## 5. Executar

```powershell
# Terminal 1 — API (http://127.0.0.1:5081)
dotnet run --project src/CompanyCopilot.Api

# Terminal 2 — Web (http://127.0.0.1:8080)
dotnet run --project src/CompanyCopilot.Web
```

Verificação rápida:

```powershell
Invoke-RestMethod http://127.0.0.1:5081/health/live   # {"status":"live"}
Invoke-RestMethod http://127.0.0.1:5081/health/ready  # {"status":"ready",...}
```

## 6. Conteúdo de demonstração

1. Abra `http://127.0.0.1:8080/admin` (apenas em loopback).
2. Carregue os arquivos de `docs/exemplos/` (institucional, horário, condições de
   pagamento, política de troca e FAQ).
3. Aprove cada documento. O status deve evoluir para **Approved** após a indexação.

## 7. Exposição externa (Cloudflare Quick Tunnel)

```powershell
cloudflared tunnel --url http://127.0.0.1:8080
```

Somente a Web é exposta. A API, o PostgreSQL e o Ollama permanecem em loopback.
O acesso à área `/admin` permanece bloqueado fora do loopback.

## 8. Testes

```powershell
dotnet test tests/CompanyCopilot.UnitTests
dotnet test tests/CompanyCopilot.IntegrationTests
```

A avaliação funcional do copiloto está em `docs/avaliacao-rag.md`.

## Interrupção

```powershell
docker compose -f infra/docker-compose.yml down    # (sem apagar o volume)
# Para religar o PostgreSQL local parado no passo 2:
Start-Service postgresql-x64-18
```
