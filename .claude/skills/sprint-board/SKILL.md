---
name: sprint-board
description: Gerencia as tarefas do Greek Fight Tactics (GFT) usando docs/trello_board.json como fonte de verdade. Use SEMPRE que a conversa tocar em o que fazer agora, qual o próximo passo, progresso, sprint, card, bloqueio, dependência entre tarefas, ou quando terminar de implementar qualquer coisa — mesmo que o usuário não cite o board, não peça para atualizar nada e só diga algo como "pronto", "terminei isso", "o que falta" ou "vamos para a próxima". Use também antes de começar a codificar qualquer feature, para descobrir em qual card ela cai e quais critérios de aceitação ela fecha.
---

# Sprint board

`docs/trello_board.json` é a fonte de verdade das tarefas. Ele é versionado junto com o código, então o estado das tarefas anda no mesmo commit da mudança que as fechou: dá para olhar um commit antigo e saber exatamente o que estava pronto naquele momento.

O Trello é um espelho. Se os dois divergirem, o JSON vence.

Todas as operações passam pelo script:

```bash
python .claude/skills/sprint-board/scripts/board.py <command>
```

Nunca edite `trello_board.json` à mão. O script preserva a formatação do arquivo byte a byte e mantém os checkboxes consistentes com o que os outros comandos leem; um edit manual quebra isso em silêncio.

---

## Formato do board

O script depende destas convenções. Ao criar ou alterar cards, siga-as, ou `next`, `blockers` e `verify` passam a mentir.

| Elemento | Onde fica | Exemplo |
|---|---|---|
| ID | Início do `title`, entre colchetes | `[GRD-01] GridModel e TileData` |
| Sprint | Label que começa com `Sprint NN` | `Sprint 01 · Fundação, Grid & Dados` |
| Bloqueante | Label `Bloqueante` | — |
| Dependências | Linha `**Depende de:**` da `description` | `**Depende de:** [GRD-01], [GRD-02] · **Estimativa:** 2 dias` |
| Checklist | Linhas `- [ ]` / `- [x]` da `description` | `- [ ] Teste de borda: células fora do grid retornam inválido` |
| Travado | Campo `blocked` do card, gravado pelo script | `{"reason": "...", "since": "2026-09-15"}` |

Um card está concluído quando todos os itens da checklist estão marcados. Não existe campo de status separado — é isso que impede o estado de divergir do conteúdo.

Cards sem label de sprint (`[EVO-*]`) ficam fora de `next` e `blockers`: são ideias fora do escopo.

---

## Os três momentos em que usar

### 1. Antes de começar a codificar

Descubra em que card o trabalho cai e quais itens ele fecha:

```bash
python .claude/skills/sprint-board/scripts/board.py next
python .claude/skills/sprint-board/scripts/board.py card GRD-01
python .claude/skills/sprint-board/scripts/board.py sprint 2
```

Os cards carregam contexto que não está no código: a descrição diz *por que* a tarefa existe, os critérios de aceitação frequentemente trazem restrições técnicas (um limite de performance, um caso de teste obrigatório) e `**Depende de:**` revela o que precisa estar pronto antes.

Se o trabalho pedido não corresponde a nenhum card, diga isso ao usuário antes de codificar. Ou é escopo novo — e aí vale um card —, ou há um mal-entendido sobre o que está sendo construído.

Trechos de código em critérios seguem a convenção do `CLAUDE.md`: identificadores em inglês, iguais aos dos tipos centrais. Ao acrescentar um critério, mantenha isso.

### 2. Ao terminar algo

Marque os itens correspondentes. É o que faz `next` continuar apontando para o lugar certo na próxima sessão.

```bash
board.py check GRD-01 "teste de borda" "gizmos"
board.py check SET-01 "#3"
board.py close SET-03
```

Ao marcar o último item, o script informa quais cards isso desbloqueou. Mencione isso ao usuário — é a informação mais acionável do momento.

Marque só o que de fato foi feito e verificado. Um item que diz "testado em modo avião" marcado sem teste transforma o board num relato ficcional, e a partir daí ele para de valer para qualquer decisão. A régua é o card [SET-03] (Definition of Done).

### 3. Quando algo trava

```bash
board.py block GRD-02 "aguardando decisão sobre props que transbordam a célula"
board.py unblock GRD-02
board.py blockers
```

`blockers` mostra há quantos dias cada card está travado, em vermelho a partir de três, e os cards do sprint atual ainda aguardando dependência. Três dias é o limiar do [SET-03]: além disso o sprint é replanejado, não esperado.

---

## Comandos

| Comando | O que faz |
|---|---|
| `status` | Progresso por sprint, com barra e contagem de abertos |
| `next` | Primeiro card aberto, desbloqueado e sem trava, com os itens pendentes |
| `sprint <n>` | Cards do sprint, estado e marcação de bloqueantes |
| `card <ID>` | Descrição completa com a checklist numerada |
| `check <ID> <termo\|#n>...` | Marca item(ns) como feito |
| `uncheck <ID> <termo\|#n>...` | Desmarca |
| `close <ID>` | Marca todos os itens do card |
| `block <ID> "<motivo>"` / `unblock <ID>` | Trava com motivo e data |
| `blockers` | Travados e aguardando dependência no sprint atual |
| `add <ID> "<texto>"` | Acrescenta item ao fim da checklist |
| `verify` | Integridade do board |

Os termos de `check` casam por trecho de texto, sem diferenciar maiúsculas. Se um termo casar com mais de um item, o script lista as opções e não marca nada — use `#n`, que aparece na saída de `card`.

Legenda: `✓` concluído, `·` disponível, `▸` aguardando dependência, `x` travado.

---

## `verify`: as regras que ele checa

- IDs ausentes ou repetidos, dependências para cards inexistentes e ciclos.
- Dependência agendada num sprint posterior ao do consumidor, ou listada depois dele no mesmo sprint.
- A regra de planejamento do board:

> Todo card Bloqueante fica no mesmo sprint do seu primeiro consumidor, puxado antes dele, ou no sprint imediatamente anterior.

O raciocínio: um bloqueante errado só se revela quando alguém tenta usá-lo. Se o primeiro uso estiver a três sprints de distância, o erro aparece depois de já existir código construído em cima dele. Com distância de no máximo um sprint, o consumidor funciona como teste do bloqueante.

Rode `verify` sempre que mexer em dependências, mover um card de sprint ou acrescentar cards. Quando ele apontar um bloqueante distante, há duas saídas legítimas: adiantar o consumidor, ou registrar a mitigação nos dois cards — normalmente um item de checklist em cada ponta testando a integração.

---

## Acrescentar trabalho novo

Escopo que não existe no board entra no board, não fica improvisado fora dele:

```bash
board.py add GRD-02 "testar ator atrás de coluna de 56px"
```

Para um card inteiramente novo, siga a tabela de formato acima e preencha `**Depende de:**` de verdade. É a linha que alimenta `next`, `blockers` e `verify`; um card sem dependências declaradas parece sempre disponível e fura a ordem do plano. Rode `verify` em seguida.

---

## Espelhar no Trello

O sentido é sempre JSON → Trello, e não há script de importação versionado ainda. Não existe sincronização de volta: marcar um item no app do Trello não atualiza o JSON. Se o usuário quiser trabalhar pelo app, avise isso antes, em vez de deixar o board divergir em silêncio.

---

## Contexto do projeto

As regras de arquitetura estão no `CLAUDE.md`. Duas aparecem com frequência nos critérios e vale reconhecê-las: a chave da OpenAI nunca entra no cliente Unity, e o LLM gera texto enquanto a engine julga a regra. Itens que mencionam modo avião, fallback, cache ou Cloud Function estão sempre ligados a uma das duas.

A `board_description` do JSON explica a ordem dos sprints e o ponto de corte da entrega acadêmica. `docs/GDD.md` tem história, personagens e especificação de arte — consulte quando um card de conteúdo não trouxer detalhe suficiente. Cards de arte usam também a skill `pixel-art-pipeline`.
