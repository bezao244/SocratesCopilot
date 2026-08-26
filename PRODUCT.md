# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Users

- Funcionários e equipes da empresa que precisam responder dúvidas operacionais com base em documentos internos aprovados.
- Usuários do chat público, que consultam regras, políticas, horários, condições de pagamento e informações institucionais.
- Administradores da organização, que fazem ingestão, aprovação e manutenção do conhecimento disponível ao sistema.

## Product Purpose

O Copiloto RAG da Empresa é um assistente de chat consultivo 100% local em português, projetado para responder somente com base em documentos aprovados pela própria empresa. O objetivo principal é reduzir a busca manual em políticas, regras internas e materiais operacionais, mantendo respostas conservadoras e verificáveis.

O sucesso do produto é medido por respostas corretas, citando fontes quando aplicável, sem inventar informações, e por recusa adequada quando não houver evidência suficiente ou houver conflito entre fontes.

## Positioning

O produto se diferencia por operar em um modelo de recuperação fundamentado em documentos internos aprovados, em ambiente local, com guardrails explícitas de segurança e evidência. Ele não responde como um assistente genérico livre; sua vantagem é a confiabilidade operacional dentro do contexto corporativo.

## Operating Context

- A interface web pública é acessada em Blazor Server em http://127.0.0.1:8080.
- A API interna roda em ASP.NET Core em http://127.0.0.1:5081.
- A área administrativa é local e não é exposta fora do loopback; o acesso público via túnel temporário é bloqueado para /admin.
- O conhecimento é alimentado por documentos aprovados e públicos, incluindo materiais de exemplo e uploads adicionais realizados pela administração.
- O ambiente de execução exige Docker para PostgreSQL + pgvector, Ollama local para geração e embeddings, e .NET para a aplicação.
- O sistema atua em um contexto de empresa com regras e políticas internas, onde decisões operacionais dependem de documentos confiáveis e atualizados.

## Capabilities and Constraints

- Busca híbrida sobre documentos públicos e aprovados, combinando recuperação textual em português com busca vetorial via pgvector.
- Respostas baseadas somente em documentos aprovados, ignorando instruções embutidas presentes em arquivos não confiáveis.
- Recusa educada quando não há evidência, quando há conflito entre fontes com mesma prioridade ou quando a informação não é suportada pelo conhecimento disponível.
- Limitação de histórico: sessões com até 6 turnos e expiração após 20 minutos de inatividade.
- Limitação de uso: 8 perguntas por IP a cada 10 minutos e fila de geração com um item por vez e capacidade de 5.
- Upload de documentos suportados: PDF, DOCX, XLSX, TXT, MD e HTML/HTM, até 10 MB, com deduplicação por SHA-256.
- Categorias autoritativas como Regras, Pagamento, Horários e Política prevalecem sobre FAQ e material institucional.
- O produto trata documentos como dados não confiáveis e não aceita respostas baseadas em suposições.

## Brand Commitments

- Nome do produto: Copiloto RAG da Empresa.
- Tom de voz: consultivo, preciso, conservador e orientado a evidência.
- A identidade do produto é definida como um assistente interno de confiança para uso corporativo, não como uma experiência de consumo geral.

## Evidence on Hand

- README principal do projeto com visão geral, regras do copiloto e início rápido: [README.md](README.md)
- Documentação de avaliação funcional com casos e critérios: [docs/avaliacao-rag.md](docs/avaliacao-rag.md)
- Guia de instalação e ambiente local no Windows: [docs/setup-windows.md](docs/setup-windows.md)
- Exemplos de documentos de domínio: [docs/exemplos/](docs/exemplos/)
- Configuração de infraestrutura local: [infra/docker-compose.yml](infra/docker-compose.yml)
- Implementação principal da web app: [src/CompanyCopilot.Web/Program.cs](src/CompanyCopilot.Web/Program.cs)
- Estrutura do sistema e módulos de domínio/aplicação/infraestrutura: [src/](src/)

Não há evidência de uma identidade visual formal, marcação de produto externa, biblioteca de design existente ou ativos de branding que devam ser preservados em um redesign visual.

## Product Principles

- Grounded answers only: toda resposta deve ter suporte no conhecimento aprovado.
- Evidence over convenience: ausência de suporte é motivo para recusa, não para inferência.
- Local-first trust: a solução prioriza execução local e controle do ambiente de dados.
- Conservative correctness: conflito e incerteza devem ser tratados como limite de resposta.
- Operational clarity: o sistema deve servir a decisão empresarial sem inventar contexto.

## Accessibility & Inclusion

Não há um requisito específico de acessibilidade declarado para o produto além das boas práticas padrão da web. O principal compromisso de inclusão aqui é garantir que o sistema ajude usuários de negócio a consultar informações internas com clareza e segurança, sem depender de inferências não sustentadas.
