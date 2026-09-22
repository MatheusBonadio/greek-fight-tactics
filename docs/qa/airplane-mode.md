# Protocolo manual — modo avião ([QA-02])

Critério não negociável: o jogo precisa ser completável sem internet. Este roteiro verifica isso na build real de Windows. Os testes automáticos cobrem a lógica com uma rede falsa. O roteiro cobre o que eles não alcançam: a pilha de rede do Windows, o `UnityWebRequest` de verdade, o Firebase real e o que só um humano percebe na tela.

| Item do card | Automático | Manual |
|---|---|---|
| 1. Tudo que existe jogável com Wi-Fi desligado | parcial | **obrigatório; repetir no [ENT-01] com o conteúdo final** |
| 2. Nenhum diálogo vazio, travamento ou popup de erro | sim | confirmação visual |
| 3. Tempo de turno idêntico ao modo online | sim | cronometragem |
| 4. Save funciona e sincroniza ao reconectar | sim | conferência no Firestore |

> **Item 1 fica aberto até o [ENT-01].** Rode este roteiro agora com o conteúdo atual e de novo no [ENT-01] com o conteúdo da entrega. O card só fecha o item 1 depois da segunda rodada.

---

## O que os testes automáticos já provam

Rode `./tools/test.ps1 -Mode All` antes do roteiro manual. Se algum destes testes falhar, não vale a pena seguir.

| Teste | Prova |
|---|---|
| `Game.Tests.EditMode.Net.AirplaneModeTests` | Com timeout, conexão recusada e 5xx: o pré-aquecimento tenta uma vez e reintenta uma vez, e depois para. O fallback serve argumentos válidos sem nenhuma requisição. Com a rede travada, uma batalha inteira termina em menos tempo que um único timeout, enquanto o pedido continua pendurado. Os saves feitos offline ficam no disco, e ao reconectar só o mais novo sobe. Ao reconciliar, vence o `timestamp` mais recente. A conta anônima sobrevive à queda e é renovada, sem criar outra. |
| `Game.Tests.PlayMode.AirplaneModePlayTests` | Com a rede travada, o boot libera o jogo dentro de `GameServices.OnlineBootBudgetSeconds` (2 s). O save automático é gravado na hora e sobe ao reconectar, sem precisar de outro save. Um save antigo de um boot offline nunca sobrescreve um save mais novo da nuvem. Na Ágora, a rede cai no meio da sessão: os argumentos pré-aquecidos são usados, o fallback assume, nenhum turno toca a rede, o turno dura o tempo de pensar do oponente e toda fala da batalha e da exploração tem texto. Nenhum erro chega ao log. |
| `Game.Tests.EditMode.UI.UiTextTests.GeneratedUiText_HasALineForEverythingTheBattleNarrates` | Toda frase que a batalha narra existe em `ui_text.json`. Nada aparece como `#chave`. |
| `FallbackLibraryTests`, `DialogueLibraryTests`, `OpponentRosterTests` | Cada falácia tem ao menos 3 argumentos de fallback válidos, entre 40 e 300 caracteres. A biblioteca de diálogo gerada guarda cada fala do `dialogues.json`, e a regeneração recusa fala vazia. Todo o repertório de cada oponente está coberto pelo banco de fallback. |

A rede falsa fica em `Assets/_Game/Tests/Support` (`FlakyNetwork`, `FlakyIdTokenProvider`, `FakeFirebase`). Os testes de PlayMode a injetam na cena por `OnlineBackendOverride`.

---

## Preparação

### Build

```powershell
./tools/build.ps1 -BuildProfile "Windows Development"
```

A saída é `Build/WindowsDevelopment/GFT.exe`. Use a build **Development**: nela, qualquer exceção abre o console de desenvolvimento da Unity no canto da tela. Esse é o "popup de erro" que não pode aparecer.

Anote o commit da build (`git rev-parse --short HEAD`) na tabela de resultado.

### Duas configurações de rede

| Config | `Assets/_Game/Data/NetworkConfig.asset` | Para quê |
|---|---|---|
| **A — como está no repositório** | vazio | Itens 1 e 2. O jogo nunca abre conexão. |
| **B — com rede** | `projectId` e `webApiKey` de produção | Itens 3 e 4 e a repetição dos itens 1 e 2 com rede configurada. |

A config B é uma edição local de texto no `.asset`, **nunca versionada**. A chave web do Firebase não é segredo. A chave da OpenAI nunca entra ali. Gere uma build para cada config.

Sem projeto de produção, use os emuladores: `useEmulator: 1`, `projectId: demo-gft` e `firebase emulators:start` na raiz. O modo avião não corta o `127.0.0.1`. Com emuladores, "cortar a rede" é parar os emuladores com `Ctrl+C`, ou criar uma regra de saída no Firewall do Windows bloqueando `GFT.exe`.

### Pasta de dados

`%USERPROFILE%\AppData\LocalLow\DefaultCompany\GFT`

| Arquivo | Conteúdo |
|---|---|
| `saves\slot0.json` a `slot2.json` | saves locais; **só o slot 0 (o primeiro da lista) sincroniza com a nuvem** |
| `saves\active_slot.txt` | slot ativo |
| `auth.json` | `uid` da conta anônima (config B) |
| `argument_cache\` | argumentos já recebidos do servidor |
| `settings.json` | opções |
| `Player.log` | log da build |

Antes de cada rodada, renomeie a pasta para `GFT.bak-<data>` e comece do zero.

### Como cortar a rede

Modo avião do Windows (`Win+A` → Modo avião) ou Wi-Fi desligado **e** cabo desconectado. Confira em `ping 8.8.8.8` que não há resposta.

Para medir tempo, grave a tela com a Xbox Game Bar (`Win+Alt+R`) e meça quadro a quadro depois.

### Controles

| Ação | Teclado | Gamepad |
|---|---|---|
| Mover o cursor | setas / WASD | direcional / analógico |
| Confirmar | `Enter` / `Espaço` / clique | A |
| Cancelar / pausa | `Esc` / botão direito | B |

---

## Item 1 — tudo jogável com Wi-Fi desligado

Config A. Repita com a config B, sempre com o modo avião ligado **antes** de abrir o jogo.

- [ ] 1.1 Ligue o modo avião e abra `GFT.exe`. O menu principal aparece. Na config A é na hora. Na config B leva no máximo 2 s, o orçamento do boot online.
- [ ] 1.2 **NOVO JOGO** → primeiro slot. A Kleia começa na roda de oleiro da oficina do Cerâmico.
- [ ] 1.3 Saia da oficina para o pátio. A fala de gatilho da Kleia (`kerameikos_leaving_workshop`) aparece.
- [ ] 1.4 Fale com o vizinho no Cerâmico (`neighbor_kerameikos`).
- [ ] 1.5 Siga pela rua de terra até a saída para a Ágora. A Ágora carrega com a Kleia na chegada, e há save automático.
- [ ] 1.6 Fale com Eudoro e com Menexeno.
- [ ] 1.7 Provoque os seis duelos: Thrasybios, Óron, Phaidra, Kritolaos, Simo e Bátilo. Ao longo deles, use ao menos uma vez **CIRCULAR** (escolhendo a célula azul), **REFUTAR**, **MAIÊUTICA**, **CITAR**, **CONCEDER** e **RETIRAR-SE**. Vença ao menos um duelo e fuja de outro.
- [ ] 1.8 Depois de vencer, o oponente não provoca de novo. Depois de fugir, ele provoca de novo quando a Kleia sai do raio e volta.
- [ ] 1.9 `Esc` → **PAUSA**: **SALVAR** no primeiro slot (confirmar sobrescrita), **OPÇÕES** (troque a velocidade do texto), **COMPÊNDIO** (as falácias refutadas aparecem) e **MENU PRINCIPAL**.
- [ ] 1.10 **CONTINUAR** → primeiro slot. A Kleia volta à Ágora no lugar salvo, ou na célula livre mais próxima se um oponente voltou para lá, e o duelo vencido continua vencido.
- [ ] 1.11 Vá da Ágora ao Cerâmico e volte. A névoa já revelada continua revelada.
- [ ] 1.12 Feche pelo menu (**SAIR**) e abra de novo ainda em modo avião. **CONTINUAR** funciona.

**Passa se** nenhum passo ficar bloqueado por falta de rede.

## Item 2 — nenhum diálogo vazio, travamento ou popup de erro

Observe durante todo o item 1.

- [ ] 2.1 Toda caixa de diálogo tem texto. Não pode aparecer caixa vazia, `#alguma.chave` nem □ no lugar de letra. Preste atenção em `ç ã õ â ê ô á é í ó ú à` e no travessão.
- [ ] 2.2 Toda fala do oponente tem nome e retrato, e o texto faz sentido para a falácia (vem do fallback).
- [ ] 2.3 Nada congela: a animação da Kleia, do cursor e da névoa nunca para por mais de meio segundo, nem no boot, nem ao entrar em batalha, nem ao salvar.
- [ ] 2.4 O console de desenvolvimento da Unity nunca abre na tela, e nenhuma janela de erro aparece.
- [ ] 2.5 Ao terminar, procure erros no log:

```powershell
Select-String -Path "$env:USERPROFILE\AppData\LocalLow\DefaultCompany\GFT\Player.log" -Pattern "Exception","Error"
```

O resultado esperado é vazio. Na config B, o aviso `Online services unavailable, continuing offline` é esperado e não conta como erro.

## Item 3 — tempo de turno idêntico ao modo online

Config B. O oponente "pensa" por `SandboxBattleDirector.DefaultThinkSeconds` (0,6 s) antes de jogar. O argumento já está na memória, pré-aquecido ao entrar no mapa ou vindo do fallback, então a rede nunca entra no turno.

- [ ] 3.1 **Online**: pasta de dados limpa, rede ligada. Entre na Ágora, espere 5 s e provoque Thrasybios. Grave 5 turnos do oponente.
- [ ] 3.2 Meça cada turno do quadro em que a última página da fala anterior fecha até o quadro em que a fala do oponente começa a aparecer. Se o oponente andar, desconte a caminhada (250 ms por célula).
- [ ] 3.3 **Offline**: ligue o modo avião com o jogo aberto, sem recarregar a cena. Provoque outro oponente e grave mais 5 turnos. Depois, feche o jogo, abra de novo em modo avião e repita com um terceiro oponente.
- [ ] 3.4 Compare as médias. A diferença entre online e offline é menor que 0,2 s, e **nenhum turno passa de 2 s**, o timeout de rede.
- [ ] 3.5 Opcional, rede ruim: conecte num Wi-Fi sem internet (hotspot do celular com dados móveis desligados). O menu aparece em até 2 s e os turnos seguem iguais aos do 3.3.

## Item 4 — save funciona e sincroniza ao reconectar

Config B. Os saves são JSON legíveis. Compare sempre o campo `timestamp`, em milissegundos Unix.

Para ler o documento `saves/{uid}` da nuvem, pegue o `uid` em `auth.json`:

- **Produção:** console do Firebase → Firestore → `saves` → `{uid}` → campo `timestamp`.
- **Emuladores:**

```powershell
$uid = (Get-Content "$env:USERPROFILE\AppData\LocalLow\DefaultCompany\GFT\auth.json" | ConvertFrom-Json).uid
(Invoke-RestMethod -Headers @{ Authorization = "Bearer owner" } "http://127.0.0.1:8080/v1/projects/demo-gft/databases/(default)/documents/saves/$uid").fields.timestamp
```

- [ ] 4.1 **Online.** Pasta de dados limpa. **NOVO JOGO** no primeiro slot e ande até a Ágora; a chegada salva sozinha. O `timestamp` de `saves\slot0.json` é igual ao da nuvem. Copie `slot0.json` para `slot0.antigo.json`, fora da pasta `saves`.
- [ ] 4.2 **Queda no meio da sessão.** Com o jogo aberto, ligue o modo avião. Vença ou fuja de um duelo, o que dispara o save automático. O `timestamp` de `slot0.json` muda na hora, e a nuvem continua com o valor antigo. Nada trava e nenhum erro aparece.
- [ ] 4.3 Ainda offline, `Esc` → **SALVAR** no primeiro slot. O `slot0.json` muda de novo.
- [ ] 4.4 **Reconexão.** Desligue o modo avião e **não salve de novo**. Em até 30 s (`GameServices.DefaultPushRetrySeconds`), o `timestamp` da nuvem fica igual ao de `slot0.json`: a nuvem recebe o save do 4.3, o mais novo, e não o do 4.2.
- [ ] 4.5 **Boot offline.** Feche o jogo, ligue o modo avião e abra. **CONTINUAR** carrega o save local do 4.3, com a Kleia no lugar e os duelos preservados.
- [ ] 4.6 **O mais novo vence: nuvem mais nova.** Feche o jogo e desligue o modo avião. Troque `saves\slot0.json` pela cópia `slot0.antigo.json` do 4.1 e abra o jogo online. O boot baixa o save da nuvem: `slot0.json` volta ao `timestamp` do 4.4, e **CONTINUAR** põe a Kleia onde ela estava no 4.3.
- [ ] 4.7 **O mais novo vence: local mais novo.** Ligue o modo avião, jogue até um save automático e feche. Desligue o modo avião e abra. Logo no boot, a nuvem recebe o `timestamp` desse save offline.
- [ ] 4.8 **Slots locais.** Um save no segundo ou terceiro slot funciona offline e nunca aparece na nuvem.

> Um save antigo, carregado num boot offline, só é reconciliado no boot seguinte com rede, por `SaveSynchronizer.ReconcileAsync`. O reenvio automático a cada 30 s vale só para o save que a própria sessão gravou. Assim, um save antigo nunca sobrescreve um mais novo que esteja na nuvem.

---

## Registro

Copie a tabela para o card ou para o relatório da rodada.

| Data | Commit | Build | Config | Testador | Item | Resultado | Observação |
|---|---|---|---|---|---|---|---|
| | | Windows Development | A | | 1 | | |
| | | Windows Development | A | | 2 | | |
| | | Windows Development | B | | 1 | | |
| | | Windows Development | B | | 2 | | |
| | | Windows Development | B | | 3 | | |
| | | Windows Development | B | | 4 | | |
| | | Windows Release | A | | 1 ([ENT-01]) | | conteúdo final da entrega |
