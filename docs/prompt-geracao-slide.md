# Instruções para gerar o slide do sistema

Cole este documento (ou o prompt da seção final) em uma IA geradora de slides/apresentações.
O objetivo é produzir um slide/deck simples sobre o copiloto de IA da empresa.

## O que é o sistema (contexto para a IA)

Uma empresa brasileira criou um **copiloto de IA interno** que responde perguntas sobre os
documentos dela (regras, políticas, preços, horários). A IA roda **localmente no computador da
empresa** — nada de nuvem, os dados nunca saem da empresa.

## Informações-chave (para as instruções)

- **Interface:** o funcionário digita a pergunta em português em uma tela web.
- **Fluxo do chat até a IA:**
  1. A pergunta chega no servidor web (Blazor Server — o site roda no servidor e o navegador apenas exibe).
  2. O servidor busca os documentos: primeiro **entende o sentido** da pergunta (vira números, chamados de "vetores") e procura trechos parecidos no banco de dados (busca por semelhança) e também por palavras-chave (busca por texto). Os dois resultados são combinados.
  3. Os melhores trechos são montados em um "contexto" junto com o histórico da conversa.
  4. O contexto é enviado para a IA, que roda localmente no **Ollama** (software que executa modelos de IA no próprio computador), usando o modelo **Qwen3 8B** — um modelo aberto, competente em português e leve o suficiente para rodar em casa, sem custo de nuvem.
  5. A IA responde baseada **somente** nesses trechos (não inventa nada; se não achar, indica procurar atendimento humano) e a resposta aparece com as fontes consultadas.
- **Tecnologias:** .NET (C#), Blazor (interface), ASP.NET Core (API), PostgreSQL + pgvector (banco de dados com busca vetorial), Docker (infraestrutura do banco), Ollama (execução da IA) e uma placa de vídeo NVIDIA RTX 5060 (aceleração).
- **Como o RAG funciona (em linguagem simples):** RAG = "geração aumentada por recuperação". Em vez de a IA "saber" tudo de cor, ela **busca no material da empresa** cada vez que é perguntada e responde **apenas com o que encontrou**. Isso garante respostas atuais, fieis aos documentos e sem invenção.
- **O que melhora para a empresa:**
  - Funcionários acham respostas sobre regras/políticas **sem precisar de especialista** (ex.: horários, preços, políticas).
  - Respostas **baseadas nos documentos oficiais**, sempre atualizadas (é só trocar o documento no banco).
  - **Privacidade total**: os dados ficam na empresa, rodando localmente, sem custo por uso em nuvem.
  - Funciona **mesmo sem internet**.

## Roteiro sugerido para o slide (simples, ~8 a 10 slides)

1. **Título:** "Copiloto de IA da Empresa — respostas de documentos em português, rodando 100% local"
2. **Problema:** funcionários perdem tempo procurando regras/políticas em vários documentos; respostas inconsistentes entre setores.
3. **Solução:** um copiloto que responde perguntas em linguagem natural usando os documentos oficiais da empresa.
4. **Como funciona (o fluxo):** pergunta → busca nos documentos → contexto → IA local → resposta + fontes (use um diagrama simples com 5 caixas/setas).
5. **A IA local (Ollama):** modelo Qwen3 8B rodando no computador da empresa via Ollama; sem nuvem, sem envio de dados.
6. **O RAG em uma frase:** a IA não decora o conteúdo — ela busca nos documentos a cada pergunta e responde só com o que encontrou.
7. **Tecnologias:** linha de logos — .NET, Blazor, ASP.NET Core, PostgreSQL, Docker, Ollama, NVIDIA.
8. **Benefícios:** rapidez nas respostas, respostas fiéis aos documentos, privacidade total, custo zero por uso, funciona offline.
9. **Resultado/avanços futuros** (opcional): avaliação do modelo com casos reais, expansão dos documentos.

## Regras de estilo

- Linguagem **simples e clara**, para uma audiência de gestores e funcionários — sem jargão técnico.
- **Nada de detalhes de código**, nome de classes, comandos ou configurações.
- Máximo de 10 slides; frases curtas; ícones/diagramas no lugar de parágrafos.
- Manter a marca da empresa e tom profissional.
- Tudo em português do Brasil.

## Prompt pronto para colar em outra IA

> Você vai criar uma apresentação de ~8 a 10 slides sobre um "copiloto de IA" interno de uma empresa, em português do Brasil, para audiência de gestores e funcionários (linguagem simples, sem jargão técnico).
>
> **O sistema:** um copiloto que responde perguntas sobre os documentos da empresa (regras, políticas, preços, horários). O funcionário pergunta em uma tela web e recebe a resposta com as fontes consultadas.
>
> **Fluxo:** pergunta na web → servidor busca nos documentos (entende o sentido da pergunta e procura trechos parecidos no banco, combinando busca por semelhança e por palavras) → monta o contexto → envia para a IA, que roda localmente via Ollama com o modelo Qwen3 8B → resposta baseada somente nos trechos encontrados (nunca inventa; se não achar, indica atendimento humano).
>
> **RAG em uma frase:** a IA não decora o conteúdo — busca nos documentos a cada pergunta e responde só com o que encontrou, garantindo respostas fiéis e sempre atualizadas.
>
> **Tecnologias:** .NET, Blazor, ASP.NET Core, PostgreSQL (banco), Docker, Ollama, placa NVIDIA RTX 5060.
>
> **Benefícios para a empresa:** respostas rápidas sem depender de especialista; respostas fiéis aos documentos oficiais; privacidade total (dados nunca saem da empresa); custo zero por uso; funciona sem internet.
>
> **Roteiro:** título → problema → solução → fluxo em diagrama simples → IA local (Ollama) → RAG em uma frase → tecnologias → benefícios → próximos passos.
>
> **Regras:** frases curtas, ícones/diagramas, máximo 10 slides, nada de código ou detalhes técnicos, tom profissional, português do Brasil.
