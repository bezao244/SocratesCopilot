# Prompt — Diagrama de Implantação para slide (IA de geração de slides)

Cole este texto na ferramenta de IA de slides (ex.: Gamma, Canva Magic Design, PowerPoint Designer, SlidesAI). Se a ferramenta permitir anexos, anexe os arquivos de logo listados abaixo; caso contrário, use a URL alternativa indicada.

---

## Contexto

Você é um designer de apresentações técnicas. Gere **um único slide** com um **diagrama de implantação** (deployment) em estilo pipeline horizontal, mostrando o fluxo de uso de um sistema **Copiloto RAG (Retrieval-Augmented Generation)** feito em .NET que roda **100% localmente** (privacidade total, dados nunca saem da máquina). Público: avaliadores de um trabalho de faculdade de Inteligência Aplicada. O slide deve caber em 16:9, título no topo, diagrama no centro, legenda na parte de baixo. Sem emojis, sem figurinhas, sem imagens de stock — apenas o diagrama, as logos oficiais das tecnologias e texto limpo.

## Layout do diagrama (da esquerda para a direita)

Um pipeline de **7 nós em linha horizontal**, com **setas bidirecionais** (ida e volta) entre eles. Cada nó é um card retangular arredondado com: **logo oficial da tecnologia no topo, nome em negrito, detalhe técnico em fonte menor embaixo e uma pequena tag de camada** (chip colorido no rodapé do card).

### Nós e respectivas logos/camadas

1. **Usuário** — sem logo; use um círculo azul com a letra "U" branca. Tag: `CLIENTE` (azul).
   - Detalhe: "Navegador (PC ou celular)".
2. **Cloudflare** — logo Cloudflare (laranja). Tag: `INFRAESTRUTURA` (laranja).
   - Detalhe: "DNS + proxy (acesso externo)".
   - Logo: `cloudflare-logo-png_seeklogo-332002.png` (anexo) | URL: https://seeklogo.com/images/C/cloudflare-logo-1F4C45E1B1-seeklogo.com.png
3. **Web Blazor Server** — logos Blazor e .NET lado a lado. Tag: `APRESENTAÇÃO` (roxo .NET).
   - Detalhe: ".NET 10 · 127.0.0.1:8080".
   - Logos: `Blazor.png` + `Microsoft_.NET_logo.png` (anexos) | URLs: https://upload.wikimedia.org/wikipedia/commons/1/19/Blazor_logo.png e https://upload.wikimedia.org/wikipedia/commons/4/44/Microsoft_logo.svg (ou use o logo .NET roxo oficial #512BD4)
4. **API ASP.NET Core** — logo .NET. Tag: `BACKEND` (roxo .NET).
   - Detalhe: ".NET 10 · 127.0.0.1:5081".
5. **PostgreSQL 17 + pgvector** — logos PostgreSQL e Docker lado a lado. Tag: `DADOS` (azul).
   - Detalhe: "Docker container · porta 5432".
   - Logos: `postgresql-logo-png_seeklogo-320016.png` + `docker-icon-logo-png_seeklogo-643955.png` (anexos)
6. **Ollama** — logo Ollama (preta). Tag: `IA LOCAL` (preto).
   - Detalhe: "127.0.0.1:11434".
7. **Qwen3 8B + embeddinggemma** — logo Qwen (roxo/azul). Tag: `MODELO` (roxo).
   - Detalhe: "empresa-copiloto:v1 · 8 GB VRAM".

### Rótulos das setas (texto pequeno acima de cada seta)

- Usuário → Cloudflare: **"HTTPS (DNS)"** / abaixo: "req / resp"
- Cloudflare → Web: **"HTTPS"** / abaixo: "requisição da página"
- Web → API: **"HTTP (JSON / NDJSON)"** / abaixo: "API REST + SignalR"
- API → PostgreSQL: **"SQL + busca vetorial"** / abaixo: "RAG: recuperação de contexto"
- API → Ollama: **"REST /api/chat"** / abaixo: "NDJSON streaming"
- Ollama → Qwen3: **"modelo carregado"**

### Paleta de cores oficial das tecnologias

- Cloudflare: #F6821F (laranja)
- .NET / Blazor: #512BD4 (roxo)
- PostgreSQL: #336791 (azul)
- Docker: #2496ED (azul)
- Ollama: #000000 (preto)
- Qwen: #615CED (roxo/azul)
- Usuário: #2563EB (azul)
- Fundo do slide: branco; fundo do diagrama: cinza muito claro (#F2F4F8); cards brancos com borda 2px na cor da tecnologia.

## Estilo

- Diagrama centralizado ocupando ~75% da altura do slide.
- Cards: cantos arredondados (12px), sombra suave, largura uniforme.
- Setas: linhas grossas cinzas (#9AA3B5) com pontas nas duas extremidades; rótulo em texto pequeno (10px) sobre um fundo branco com borda.
- Fonte: Segoe UI ou Arial em todo o slide.

## Legenda (rodapé do slide, 3 itens)

1. **Fluxo:** Usuário → Cloudflare (DNS) → Web Blazor → API → [PostgreSQL + Ollama] → resposta + fontes.
2. **Infra:** tudo em um único computador (Windows 11 · i5-14600K · RTX 5060 8 GB).
3. **Privacidade:** dados nunca saem da máquina; IA roda localmente no Ollama.

## Título do slide

**"Diagrama de Implantação — Fluxo de Uso do Copiloto RAG"**

## Regras de ouro

- As logos devem aparecer **na horizontal, lado a lado**, dentro do card, sem sobreposição.
- Não crie logos novas: use somente as oficiais (anexos ou URLs fornecidas).
- Se não conseguir usar as imagens, substitua a logo pelo **nome da tecnologia na cor oficial** (texto em negrito).
- A seta de retorno deve dar a entender que a resposta (texto + fontes) volta pelo mesmo caminho até o navegador.
