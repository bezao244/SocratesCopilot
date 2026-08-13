# Avaliação do Copiloto RAG — 40 casos

## Como usar

1. Suba a solução local (veja `docs/setup-windows.md`).
2. Na área administrativa (`http://127.0.0.1:8080/admin`), carregue os documentos de
   demonstração de `docs/exemplos/` e aprove cada um deles.
3. No chat público (`http://127.0.0.1:8080/chat`) ou via
   `POST http://127.0.0.1:5081/api/v1/chat/messages` (NDJSON), faça as perguntas abaixo.
4. Registre para cada caso: **respondeu** (A), **recusou sem evidência** (RNE),
   **recusou por conflito** (RC), **erro** (E), e se as **fontes citadas** correspondem
   ao tema (S/N).

Critérios de aceite: casos A devem responder usando apenas as fontes aprovadas e citar
as fontes corretas; RNE/RC devem recusar sem inventar conteúdo; E não deve ocorrer em
nenhum caso com os serviços íntegros.

## Casos de sucesso esperado (A)

| # | Pergunta | Fonte esperada |
|---|----------|----------------|
| 1 | Qual o horário de atendimento telefônico? | horario-atendimento |
| 2 | O atendimento presencial funciona aos sábados? | horario-atendimento |
| 3 | Em feriados, o atendimento telefônico funciona? | horario-atendimento |
| 4 | Em quanto tempo respondem e-mails? | horario-atendimento |
| 5 | Quais formas de pagamento são aceitas? | condicoes-pagamento |
| 6 | Em quantas vezes posso parcelar no cartão? | condicoes-pagamento |
| 7 | Há juros no parcelamento? Em quais parcelas? | condicoes-pagamento |
| 8 | Qual o desconto pagando com Pix à vista? | condicoes-pagamento |
| 9 | O que acontece se eu atrasar o pagamento? | condicoes-pagamento |
| 10 | Em quanto tempo o reembolso cai na conta? | condicoes-pagamento |
| 11 | Qual o prazo de troca por defeito? | politica-troca |
| 12 | Posso devolver uma compra feita pelo site? Em quanto tempo? | politica-troca |
| 13 | Quais condições para aceitar a troca de um produto? | politica-troca |
| 14 | Como solicito uma troca? | politica-troca |
| 15 | Troca-se produto sem nota fiscal? | politica-troca / faq |
| 16 | Como crio uma conta no site? | faq |
| 17 | Esqueci minha senha. Como recuperar? | faq |
| 18 | Como acompanho meu pedido? | faq |
| 19 | O envio é gratuito? | faq |
| 20 | Quanto tempo leva a entrega? | faq |
| 21 | A Empresa emite nota fiscal? | faq |
| 22 | Como falo com um atendente humano? | faq |
| 23 | Qual o propósito da Empresa? | institucional |
| 24 | Quais são os valores da Empresa? | institucional |
| 25 | Qual o canal de contato institucional? | institucional |
| 26 | Qual o horário do atendimento digital? | horario-atendimento |
| 27 | Quantos dias úteis levam as entregas para capitais? | faq |
| 28 | Produto personalizado pode ser trocado? | politica-troca |
| 29 | Qual a multa e juros de atraso? | condicoes-pagamento |
| 30 | Quais categorias de produto não são aceitas em troca? | politica-troca |

## Casos de recusa sem evidência (RNE)

| # | Pergunta | Motivo esperado |
|---|----------|-----------------|
| 31 | Qual o preço do plano premium? | sem informação nas fontes |
| 32 | A Empresa atende clientes em outros países? | sem informação nas fontes |
| 33 | Qual a política de demissão da Empresa? | sem informação nas fontes |
| 34 | Recomende um investimento lucrativo. | fora do escopo |
| 35 | Quantos funcionários a Empresa tem? | sem informação nas fontes |

## Casos de conflito (RC)

Para os casos 36–38, carregue uma versão alternativa do documento (ex.: um arquivo
dizendo "atendimento telefônico das 8h às 22h") na mesma categoria autoritativa,
aprove, e então pergunte.

| # | Pergunta | Situação |
|---|----------|----------|
| 36 | Qual o horário de atendimento telefônico? | duas fontes autoritativas divergentes |
| 37 | Qual o prazo de troca por defeito? | duas fontes autoritativas divergentes |
| 38 | Qual a taxa de juros do parcelamento? | duas fontes autoritativas divergentes |

## Casos de robustez (E/S)

| # | Pergunta | Comportamento esperado |
|---|----------|------------------------|
| 39 | (enviar pergunta com mais de 2.000 caracteres) | recusa com mensagem de limite (400) |
| 40 | (enviar 9 perguntas em menos de 10 minutos) | recusa com "limite de perguntas" (429) |

## Registro

| # | Resultado | Citou fontes? | Observação |
|---|-----------|---------------|------------|
| 1 | | | |
| ... | | | |
