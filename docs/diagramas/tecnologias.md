# Tecnologias do Projeto

Diagrama com as logos oficiais das tecnologias usadas no copiloto RAG local.
Fontes: Wikimedia Commons, Simple Icons, GitHub oficial dos projetos. URLs verificadas (HTTP 200) em 07/08/2026.

> Notas:
> - **ASP.NET Core** não tem logo próprio — usa o logotipo oficial da marca .NET (fonte: `github.com/dotnet/brand`).
> - **pgvector** não possui um logotipo oficial de uso público acessível; representado pelo logotipo do PostgreSQL (sua base) + nome.
> - Logotipo do EF Core: pacote comunitário campusMVP (CC BY-SA 4.0, cores oficiais Microsoft).

<div style="display:flex;flex-wrap:wrap;gap:24px;align-items:center;font-family:Segoe UI,sans-serif">

<div style="display:flex;flex-direction:column;align-items:center;gap:6px;width:150px">
  <img src="https://upload.wikimedia.org/wikipedia/commons/e/ee/.NET_Core_Logo.svg" alt=".NET" style="height:56px"/>
  <strong>.NET 10</strong>
</div>

<div style="display:flex;flex-direction:column;align-items:center;gap:6px;width:150px">
  <img src="https://upload.wikimedia.org/wikipedia/commons/b/bd/Logo_C_sharp.svg" alt="C#" style="height:56px"/>
  <strong>C#</strong>
</div>

<div style="display:flex;flex-direction:column;align-items:center;gap:6px;width:150px">
  <img src="https://cdn.simpleicons.org/blazor" alt="Blazor" style="height:56px"/>
  <strong>Blazor Server</strong>
</div>

<div style="display:flex;flex-direction:column;align-items:center;gap:6px;width:150px">
  <img src="https://upload.wikimedia.org/wikipedia/commons/e/ee/.NET_Core_Logo.svg" alt="ASP.NET Core" style="height:56px"/>
  <strong>ASP.NET Core</strong>
  <small style="color:#888">logo da marca .NET</small>
</div>

<div style="display:flex;flex-direction:column;align-items:center;gap:6px;width:150px">
  <img src="https://raw.githubusercontent.com/campusMVP/dotnetLogoPack/main/.samples/dotNET-EF.png" alt="Entity Framework Core" style="height:56px"/>
  <strong>EF Core</strong>
  <small style="color:#888">CC BY-SA 4.0</small>
</div>

<div style="display:flex;flex-direction:column;align-items:center;gap:6px;width:150px">
  <img src="https://upload.wikimedia.org/wikipedia/commons/2/29/Postgresql_elephant.svg" alt="PostgreSQL" style="height:56px"/>
  <strong>PostgreSQL</strong>
</div>

<div style="display:flex;flex-direction:column;align-items:center;gap:6px;width:150px">
  <img src="https://upload.wikimedia.org/wikipedia/commons/2/29/Postgresql_elephant.svg" alt="pgvector" style="height:56px"/>
  <strong>pgvector</strong>
  <small style="color:#888">extensão do Postgres</small>
</div>

<div style="display:flex;flex-direction:column;align-items:center;gap:6px;width:150px">
  <img src="https://upload.wikimedia.org/wikipedia/commons/4/4e/Docker_%28container_engine%29_logo.svg" alt="Docker" style="height:56px"/>
  <strong>Docker</strong>
</div>

<div style="display:flex;flex-direction:column;align-items:center;gap:6px;width:150px">
  <img src="https://upload.wikimedia.org/wikipedia/commons/3/31/Ollama-logo.svg" alt="Ollama" style="height:56px"/>
  <strong>Ollama</strong>
</div>

<div style="display:flex;flex-direction:column;align-items:center;gap:6px;width:150px">
  <img src="https://upload.wikimedia.org/wikipedia/commons/6/69/Qwen_logo.svg" alt="Qwen" style="height:56px"/>
  <strong>Qwen3 (8B)</strong>
  <small style="color:#888">modelo de IA</small>
</div>

<div style="display:flex;flex-direction:column;align-items:center;gap:6px;width:150px">
  <img src="https://upload.wikimedia.org/wikipedia/commons/2/21/Nvidia_logo.svg" alt="NVIDIA" style="height:56px"/>
  <strong>NVIDIA RTX 5060</strong>
</div>

</div>

## Origens das logos (comentário)

- `.NET_Core_Logo.svg` — https://upload.wikimedia.org/wikipedia/commons/e/ee/.NET_Core_Logo.svg (fonte oficial: `github.com/dotnet/brand`, CC0)
- `Logo_C_sharp.svg` — https://upload.wikimedia.org/wikipedia/commons/b/bd/Logo_C_sharp.svg (Wikimedia Commons)
- `blazor` — https://cdn.simpleicons.org/blazor (Simple Icons)
- `dotNET-EF.png` — https://raw.githubusercontent.com/campusMVP/dotnetLogoPack/main/.samples/dotNET-EF.png (campusMVP, CC BY-SA 4.0)
- `Postgresql_elephant.svg` — https://upload.wikimedia.org/wikipedia/commons/2/29/Postgresql_elephant.svg
- `Docker_(container_engine)_logo.svg` — https://upload.wikimedia.org/wikipedia/commons/4/4e/Docker_%28container_engine%29_logo.svg
- `Ollama-logo.svg` — https://upload.wikimedia.org/wikipedia/commons/3/31/Ollama-logo.svg (fonte: `github.com/ollama/ollama`)
- `Qwen_logo.svg` — https://upload.wikimedia.org/wikipedia/commons/6/69/Qwen_logo.svg
- `Nvidia_logo.svg` — https://upload.wikimedia.org/wikipedia/commons/2/21/Nvidia_logo.svg

## Mapa de uso no projeto

| Tecnologia | Papel no projeto |
|---|---|
| .NET 10 / C# | Plataforma principal (todos os projetos) |
| ASP.NET Core | API REST (`src/CompanyCopilot.Api`) |
| Blazor Server | Interface web (`src/CompanyCopilot.Web`) |
| EF Core | ORM + migrations (PostgreSQL) |
| PostgreSQL + pgvector | Banco de dados + busca vetorial |
| Docker | Infra do banco (Postgres 17 + pgvector) |
| Ollama | Execução local dos modelos de IA |
| Qwen3 8B | Modelo de chat (`empresa-copiloto:v1`) |
| NVIDIA RTX 5060 | Hardware de aceleração (8 GB VRAM) |
