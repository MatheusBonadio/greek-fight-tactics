# Estimativa de custo da IA por sessão

Card [BCK-05]. Os números alimentam a avaliação do [AVA-01] e devem ser recalculados com dados reais no [BCK-06].

## O que foi medido

| Parâmetro | Valor | Origem |
|---|---|---|
| Tokens de entrada por chamada | ~730 (2.500 a 2.600 caracteres: sistema, 2 few-shot e schema) | `buildPrompt` em `functions/src/prompt.ts`, medido para as 3 falácias do Thrasybios |
| Tokens de saída por chamada | teto de 400 (`MAX_COMPLETION_TOKENS`); um argumento válido tem até 300 caracteres de fala mais premissas, na prática ~250 | `functions/src/index.ts`, `ArgumentValidator` |
| Tentativas por argumento | 1, ou 2 quando o modelo devolve `fallacy_id` inesperado | `handleGenerateArgument` |
| Argumentos online por batalha | 3, repostos depois de derrota ou fuga | `SandboxBattleDirector.PrewarmCount` |
| Variantes de seed por falácia | 8 | `PrewarmedArgumentSource.SeedVariants` |
| Teto por jogador | 60 chamadas por hora | `RATE_LIMIT_PER_HOUR` |

Os demais argumentos de uma batalha vêm do banco de fallback, sem custo.

## Por que o custo não cresce com o número de jogadores

A chave do cache compartilhado é `oponente|falácia|seed`, e a seed fica em `0..7`. Cada oponente tem, portanto, no máximo `|pool de falácias| × 8` prompts distintos. Depois que o `argument_cache` guarda todos eles (TTL de 90 dias), toda chamada é cache hit e não chega à OpenAI.

| Escopo | Prompts distintos | Chamadas pagas no pior caso (com retry) |
|---|---|---|
| Thrasybios (3 falácias) | 24 | 48 |
| Ato I completo (6 oponentes, ~3 falácias cada) | ~144 | ~288 |
| Catálogo inteiro (20 falácias) × 7 oponentes | 1.120 | 2.240 |

## Fórmula

```
custo por chamada = 730 × preço_entrada / 1.000.000 + 400 × preço_saída / 1.000.000
custo de encher o cache = prompts distintos × 2 × custo por chamada
custo de uma sessão de 1 h, cache frio = min(3 × batalhas, prompts distintos, 60) × 2 × custo por chamada
custo de uma sessão de 1 h, cache cheio = 0
```

Uma sessão de 1 h do vertical slice tem de 4 a 6 batalhas, então pede no máximo 18 argumentos online. Com o cache frio, isso dá 36 chamadas no pior caso, ou cerca de 41 mil tokens.

## Exemplo com preços de referência

Os preços abaixo são ilustrativos. Confira a tabela vigente da OpenAI e grave os valores reais em `INPUT_PRICE_PER_MILLION_TOKENS` e `OUTPUT_PRICE_PER_MILLION_TOKENS`, que alimentam o `usage/{dia}`.

| Preço (US$ por milhão de tokens, entrada / saída) | Custo por chamada | Sessão de 1 h, cache frio | Encher o cache do Ato I |
|---|---|---|---|
| 0,15 / 0,60 | US$ 0,00035 | US$ 0,013 | US$ 0,10 |
| 2,50 / 10,00 | US$ 0,0058 | US$ 0,21 | US$ 1,68 |

## Como medir de verdade

`npm run usage-report -- <início> <fim>` em `functions/` lê `usage/{yyyy-mm-dd}` e mostra chamadas, cache hits, taxa de acerto e custo diário e acumulado. Rode depois das sessões de playtest do [AVA-02] e substitua esta estimativa pelos valores medidos.
