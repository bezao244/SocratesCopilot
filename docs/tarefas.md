# Tarefas do Projeto — Companhia Copiloto

Protocolo de execução para a IA (leia junto com `AGENTS.md`):

1. Executar as tarefas **estritamente em ordem** (T1 → T2 → T3 → T4).
2. Antes de marcar uma tarefa como concluída, **validar os critérios de aceite** com os comandos/verificações listados em cada tarefa.
3. Se uma tarefa falhar ou ficar incompleta: **parar**, reportar o problema e não avançar.
4. Ao concluir: marcar o checkbox `[x]`, preencher a linha de conclusão (data + como validou).
5. Tarefas novas seguem o template no fim deste arquivo.

## T1 — Diagrama do funcionamento da IA local (Ollama)

- [x] Status: concluída
- **Objetivo:** criar um diagrama do funcionamento da IA rodando localmente no Ollama, para uso em slide futuro. Deve mostrar o fluxo completo da pergunta do usuário até a resposta do modelo, passando pelo RAG.
- **Entregável:** `docs/diagramas/ia-local-ollama.md` (criar pasta `docs/diagramas/`) com:
  - Diagrama Mermaid (fluxo: pergunta do usuário → API → busca vetorial pgvector + busca textual → montagem do contexto/prompt → Ollama → resposta + fontes).
  - Tabela com o modelo usado: `empresa-copiloto:v1` (base `qwen3:8b`, ~5.2 GB, contexto 8192, `think: false`) e `embeddinggemma` (621 MB, 768 dimensões).
  - Justificativa da escolha do modelo (PT-BR competente, tamanho compatível com hardware local sem GPU dedicada pesada, rodado via Ollama com `docs/Modelfile` customizado).
  - Tabela de configurações do computador (confirmar com os comandos abaixo):
    - CPU: Intel Core i5-14600K (14 cores / 20 threads)
    - RAM: 31.7 GB total (16.5 GB livre)
    - GPU: NVIDIA GeForce RTX 5060 — **confirmar VRAM real com `nvidia-smi --query-gpu=name,memory.total --format=csv`** (o WMI reporta 4 GB por limitação de 32 bits; a placa tem 8 GB GDDR7)
    - OS: Windows 11 Home (build 26200)
    - Disco: 644/931 GB usados no C:
  - Observação sobre execução local: Ollama como serviço em `127.0.0.1:11434`, modelos via `ollama list`.
- **Validação:** arquivo existe; diagrama Mermaid com sintaxe válida; specs batem com o hardware real (rodar `nvidia-smi`); dados do Ollama conferem com `ollama list`.
- **Dependências:** nenhuma.

## T2 — Diagrama de tecnologias com logos

- [x] Status: concluída
- **Objetivo:** diagrama simples das tecnologias do projeto com as **logos reais** (buscar na internet).
- **Entregável:** `docs/diagramas/tecnologias.md` com diagrama (HTML com `<img>` ou SVG) contendo logo + nome de cada tecnologia:
  - .NET 10, C#, ASP.NET Core (API), Blazor Server (Web), EF Core
  - PostgreSQL + pgvector, Docker, Ollama, Qwen3 (modelo), NVIDIA (hardware)
- **Logos:** buscar URLs oficiais/confiáveis na internet (site oficial da tecnologia, Wikimedia/Wikipedia ou CDN Simple Icons). Registrar em comentário a URL usada por item.
- **Validação:** todas as URLs de logo respondem 200 (verificar com `Invoke-WebRequest -Method Head`); nenhum placeholder; diagrama legível para uso em slide.
- **Dependências:** nenhuma.

## T3 — Instruções .md para outra IA gerar o slide do sistema

- [x] Status: concluída
- **Objetivo:** gerar um arquivo de instruções pronto para colar em outra IA gerar um slide (deck) sobre o funcionamento completo do sistema.
- **Entregável:** `docs/prompt-geracao-slide.md` contendo um prompt/instruções prontas com:
  - Público-alvo e tom (apresentação de projeto, linguagem simples, sem detalhes técnicos profundos).
  - Roteiro de conteúdo por slide: visão geral, fluxo do chat até o Ollama (passo a passo em linguagem simples), tecnologias, padrões de projeto utilizados, como o RAG funciona (busca, contexto, resposta) e como isso melhora o dia a dia da empresa.
  - Restrições: simplificar partes muito técnicas; foco em fluxo, valor e resultado.
- **Validação:** arquivo existe; instruções autocontidas (uma outra IA conseguiria gerar o slide sem consultar o código); nada de detalhes internos de código.
- **Dependências:** T1 e T2 (podem servir de referência).

## T4 — Workbench do modelo local (agente `plan`)

- [x] Status: concluída
- **Objetivo:** usar o agente `plan` do opencode (sem editar código) para estruturar um **workbench de avaliação do modelo de IA rodando no Ollama**, com justificativa, resultados esperados e plano de ação para executar e visualizar — para exibição no slide geral do projeto.
- **Como executar:** iniciar no agente `plan` (ou chamar `@plan` / subagente de análise) e pesquisar padrões de workbench/avaliação para RAG + LLM local (ex.: RAGAS, LLM-as-judge, avaliação por casos curados). Usar como base o acervo existente: `docs/avaliacao-rag.md` (40 casos manuais) e as métricas/limites de config em `src/CompanyCopilot.Application/Options.cs` e `docs/arquitetura.md` (§ configuração).
- **Entregável:** `docs/workbench/plano-workbench.md` com:
  - Modelo de workbench escolhido + **explicação do motivo da escolha** (por que faz sentido para este projeto/hardware).
  - Resultados esperados (ex.: fidelidade/aderência às fontes, taxa de alucinação, qualidade da recuperação, latência local vs. nuvem).
  - Plano de ação passo a passo: preparar casos (reuso dos 40 de `docs/avaliacao-rag.md`), rodar avaliações, coletar métricas, gerar visualização (tabelas/gráficos Mermaid ou HTML).
  - Conteúdo final para o slide: resultados, modelo de workbench utilizado, explicação do motivo da escolha.
- **Validação:** arquivo existe; plano executável com comandos concretos; justificativa e resultados esperados coerentes com o projeto; o agente `plan` não deve alterar código.
- **Dependências:** T3 (slide final usa os resultados).

## Template para novas tarefas

```markdown
## T<N> — <Título curto>

- [ ] Status: pendente
- **Objetivo:** <o que deve ser feito e por quê>
- **Entregável:** <arquivo(s) a criar/alterar>
- **Critérios de aceite:** <o que define "concluído" de forma objetiva>
- **Validação:** <comandos/verificações antes de marcar concluído>
- **Dependências:** <T-IDs anteriores ou "nenhuma">
- [ ] T<N> concluída | <data> | <como validou>
```

## Histórico

| Tarefa | Concluída | Data | Validação |
|--------|-----------|------|-----------|
| T1     | ✔ (2026-08-07) | 07/08/2026 | Arquivo criado; `nvidia-smi` confirma 8151 MiB; `ollama list` confirma modelos; Mermaid sem sintaxe quebrada |
| T2     | ✔ (2026-08-07) | 07/08/2026 | `docs/diagramas/tecnologias.md` criado; 9 URLs de logos verificadas via HEAD (todas 200) |
| T3     | ✔ (2026-08-07) | 07/08/2026 | `docs/prompt-geracao-slide.md` criado; instruções autocontidas (contexto, fluxo, roteiro, regras e prompt pronto) |
| T4     | ✔ (2026-08-07) | 07/08/2026 | `docs/workbench/plano-workbench.md` criado via agente plan (sem alterar código); seções verificadas (padrão escolhido, justificativa, métricas, resultados, plano faseado, conteúdo do slide, referências) |
