# Resumo para Apresentação — Copiloto RAG da Empresa

> Roteiro de apresentação oral (~5 min): fluxo do sistema + justificativa de cada tecnologia.

---

## 1. O que é (30 s)

Um **assistente de chat consultivo (RAG)** em português que responde **somente** com base em
documentos internos aprovados pela empresa. É **100% local**: nenhum dado sai da máquina,
nem pergunta, nem documento, nem resposta. Roda numa única máquina Windows com GPU.

**Stack:** .NET 10 · Blazor Server · ASP.NET Core · PostgreSQL + pgvector (Docker) · Ollama (Qwen3 8B).

---

## 2. Fluxo resumido (1 a 2 min)

```
Usuário (navegador)
   │ 1. digita a pergunta
   ▼
Web Blazor Server (:8080)   ← camada de interface
   │ 2. encaminha via HTTP JSON/NDJSON (servidor→servidor)
   ▼
API ASP.NET Core (:5081)    ← orquestra todo o RAG
   │ 3. converte a pergunta em vetor (embedding 768d — Ollama)
   │ 4. busca nos documentos: vetorial (similaridade) + textual (tsvector)
   │ 5. funde os resultados (RRF) e avalia a evidência
   │    → sem evidência ou com conflito entre fontes: RECUSA a responder
   ▼
PostgreSQL 17 + pgvector (Docker :5432)  ← trechos dos documentos aprovados
   │ 6. monta o prompt: contexto + histórico + pergunta
   ▼
Ollama (:11434)             ← gera a resposta em streaming (modelo local)
   │ 7. resposta volta em tempo real (deltas) + lista de fontes
   ▼
Usuário vê a resposta e as fontes no chat
```

**Em uma frase:** a pergunta vira vetor → recupera os trechos certos no banco → o modelo
escreve a resposta só com base nesses trechos → mostra as fontes.

---

## 3. Por que cada tecnologia (3 min)

### .NET 10 (C#) — linguagem e runtime
- **Por quê:** mesma linguagem para front, back e regras de negócio (um só ecossistema, um só
  time de conhecimentos), fortemente tipada, ótima produtividade e desempenho para APIs.
- **Arquitetura limpa em 5 projetos:** Web (UI), API (HTTP), Application (regras), Infrastructure
  (banco/Ollama), Domain (entidades) — separação que permite testar cada camada isolada.

### Blazor Server — front-end
- **Por quê:** a UI é escrita em C#/Razor (sem trocar de linguagem para JavaScript); a lógica de
  interface roda no servidor, então o navegador é apenas "espelho" — essencial para manter o
  **código proprietário protegido no servidor** e facilitar o streaming de resposta.
- Streaming de resposta por `JS interop` para rolagem automática do chat.

### SignalR (WebSockets) — comunicação navegador ↔ servidor
- **Por quê:** o Blazor Server mantém um **circuito persistente** (SignalR) com o navegador.
  Com o RAG, cada resposta é um fluxo contínuo de tokens; SignalR entrega cada "delta" ao
  navegador **sem recarregar a página e sem novos requests HTTP**, dando a sensação de
  digitação em tempo real, como um ChatGPT local.

### Docker — PostgreSQL em container
- **Por quê:** elimina a instalação manual do PostgreSQL e da extensão pgvector no Windows;
  a subida é `docker compose up -d`, reproduzível e idêntica em qualquer máquina; o banco fica
  **isolado em loopback (127.0.0.1)**, invisível para a rede externa.

### PostgreSQL + pgvector — banco dos documentos
- **Por quê:** PostgreSQL é o banco relacional mais robusto do mercado aberto; a extensão
  **pgvector** permite guardar os **vetores (embeddings)** dos trechos dentro do próprio banco,
  com índice HNSW de busca por similaridade cosseno — evita adicionar um banco vetorial
  separado (ex.: Pinecone/Weaviate) e mantém tudo local.
- Uma só fonte de verdade: documentos, trechos, vetores, sessões de chat, auditoria e feedback
  em 8 tabelas relacionadas.

### Busca híbrida + RRF — qualidade da recuperação
- **Por quê:** busca só vetorial erra sinônimos; busca só textual erra semântica. O sistema faz
  **as duas** (vetorial com similaridade cosseno + textual com `tsvector` em português) e funde
  os rankings com **RRF (Reciprocal Rank Fusion)** — o trecho que aparece bem nas duas sobe.
  Resultado: respostas com base mais confiável e menos alucinação.

### Ollama — IA local
- **Por quê:** é o servidor de inferência que roda LLMs **na própria máquina**, sem nuvem —
  requisito de privacidade do projeto (documentos internos não podem ir para APIs externas).
- Modelo de chat: **Qwen3 8B** (8 GB de VRAM, cabe na RTX 5060) personalizado como
  `empresa-copiloto:v1` (prompt de sistema em PT-BR, temperatura 0.1 para respostas precisas).
- Modelo de embeddings: **embeddinggemma** (vetores 768d).

### Separação Web + API — segurança e organização
- **Por quê:** o navegador **nunca fala com o banco nem com o Ollama** — só com a Web; a Web
  chama a API servidor-a-servidor. Assim a API (única que conhece banco e IA) fica em loopback,
  e apenas a Web pode ser exposta (ex.: túnel Cloudflare) para acesso externo.

### EF Core + Npgsql — acesso a dados
- **Por quê:** ORM maduro com suporte nativo a pgvector; consultas vetoriais (CosineDistance) e
  textuais (PlainToTsQuery) direto em LINQ; migrations versionam o esquema do banco.

### Medidas de segurança (diferencial)
- **Rate limit** por IP (8 perguntas/10 min) + **fila** de 1 geração simultânea — a GPU só
  aguenta um chat por vez.
- **Recusa inteligente:** se não há evidência ou as fontes se contradizem, o modelo **não
  responde** — previne alucinação.
- **Auditoria:** toda ação administrativa grava `audit_events`; feedback dos usuários é
  coletado.

---

## 4. Fechamento (30 s)

- **Diferenciais:** privacidade total (IA local), respostas com fonte verificável, recusa a
  respostas sem base, português nativo, interface responsiva (PC e celular).
- **Limitação assumida:** modelo 8B local gera mais devagar que um LLM em nuvem — troca
  consciente: velocidade por **confidencialidade dos dados da empresa**.
