# Greek Fight Tactics (GFT) — Arquitetura

Tactics isométrico de dialética (Atenas, 402 a.C.). Unity 6.6 (`6000.6.0f1`) 2D URP, pixel art 16-bit, batalha por turnos vencida no argumento. Backend Firebase, texto dos adversários gerado por LLM.

**Fluxo de trabalho por CLI, sem Editor.** Isso atravessa todo o resto: nada pode depender de clique no Inspector. Configuração de import, fatiamento de sprite, criação de asset e build são código versionado, não estado de projeto.

| Referência | Conteúdo |
|---|---|
| `docs/GDD.md` | História, personagens, especificação de tiles, animações, UI e prompt de arte |
| `docs/trello_board.json` | Tarefas, dependências e critérios de aceitação — sempre via skill `sprint-board` |
| `art/` | Paleta, mockup de referência e placeholders — regras na skill `pixel-art-pipeline` |

---

## Camadas

```
Game.UI ─┬─▶ Game.Battle ─────────────▶ Game.Grid ──▶ Game.Core
         │                                  ▲             ▲
         └─▶ Game.Net ──▶ Game.Content ─────┴─────────────┘
```

A dependência é estritamente unidirecional. `Game.Grid` não conhece `Game.Battle`; `Game.Battle` não conhece `Game.Net` nem `Game.UI`. Cada camada é um Assembly Definition com o mesmo nome.

| Camada | Conteúdo | Unity |
|---|---|---|
| `Game.Core` | `Coord`, enums, `Argument`, `ArgumentValidator`, `ArgumentTones`, `FallbackArgumentPicker`, `StoryCondition`, `ActGate` | No Engine References |
| `Game.Grid` | `GridModel`, `IsoMath`, `PathFinder`, `FogOfWar`, `DuelCrowdPlanner`, `TerrainSteps`, `TerrainSorting`, `TerrainPicking` | só `Vector2` |
| `Game.Battle` | resolver, estado e ações | No Engine References |
| `Game.Content` | ScriptableObjects e tabelas por id | `ScriptableObject` |
| `Game.Net` | JSON (Newtonsoft), fallback, cliente da Cloud Function | I/O |
| `Game.UI` | `MonoBehaviour`, views, input | tudo |

**Core, Grid e Battle não dependem da Unity** além de tipos matemáticos. Não é purismo: é o que torna a suíte inteira executável em EditMode headless, em segundos. Como tudo roda por CLI, um resolver que precisa de `MonoBehaviour` para ser testado é um resolver que não vai ser testado.

O contrato do argumento mora em `Game.Core` porque é a fronteira entre duas camadas que não se conhecem: `Game.Net` produz `Argument`, `Game.Battle` consome.

**Estado estático.** O Play Mode entra sem domain reload, então estático mutável sobrevive entre sessões. Grid, Content, Net e UI têm o analyzer de autostatics ligado por `<asmdef>.globalconfig`: todo campo estático precisa de `[AutoStaticsCleanup]` ou `[NoAutoStaticsCleanup]`, ou a compilação falha com UAL0010/UAL0013. Core e Battle não enxergam o atributo — ele vive em `UnityEngine.CoreModule` —, então lá o analyzer fica desligado e a regra é mais dura: estático só pode ser `const` ou `static readonly` de dado imutável.

---

## Invariantes

Cinco regras que, se quebradas, causam retrabalho estrutural ou risco real.

### 1. A chave da OpenAI nunca entra no cliente

```
Unity ──(Auth token + App Check)──▶ Cloud Function ──(Secret Manager)──▶ OpenAI
```

Nada de chave em `.asset`, `PlayerPrefs`, Remote Config ou string ofuscada. Build de Unity é descompilável e strings são recuperáveis mesmo com IL2CPP. A Unity chama o nosso endpoint e recebe um `Argument` já validado no servidor — ela não sabe que a OpenAI existe.

### 2. O LLM gera texto, a engine julga a regra

O modelo nunca decide vencedor, nunca calcula dano, nunca muta estado. Ele devolve um argumento etiquetado; o `BattleResolver` lê a etiqueta e decide tudo. Payload que não valida contra o catálogo de falácias é descartado e substituído pelo fallback local antes de chegar ao resolver.

### 3. `GridModel` é a fonte de verdade; o Tilemap é render

Nunca consulte `Tilemap` para decidir regra. Ocupação, custo, transponibilidade e revelação vivem em `TileData[,]`. O Tilemap desenha o que o modelo já decidiu.

### 4. O resolver é puro e a mutação acontece num ponto só

`BattleResolver.Resolve(play, state)` não altera nada — devolve um `Outcome`. Só `BattleState.Apply(outcome)` muta. A view apenas reproduz. É isso que permite simular mil batalhas em teste headless e reproduzir qualquer bug a partir de uma seed.

### 5. O jogo é completável offline

Cada entrada do catálogo tem no mínimo 3 argumentos escritos à mão em `Assets/_Game/Data/Fallback/{id}.json`, referenciados como `TextAsset` pelo `FallbackLibrarySO` — sem pasta `Resources/`. Timeout de 2 s, 1 retry, falha silenciosa — o jogador nunca espera nem vê erro. Modo avião faz parte do critério de aceite de todo card que toca rede, com o roteiro de `docs/qa/airplane-mode.md`.

---

## Tipos centrais

```csharp
namespace Game.Core
{
    public readonly struct Coord
    {
        public readonly int X;
        public readonly int Y;
    }

    public enum TerrainId { Marble, OchreMarble, Dirt, Grass, Stone, Water, Diagram, Snow, Rift }
    public enum Profile { Sophist, Philosopher }
    public enum ActionId { Circulate, Refute, Maieutics, Quote, Concede, Withdraw, Argue }
    public enum Act { One = 1, Two = 2 }
    public enum FallacyCategory { Informal, Formal, Paradox }

    public readonly struct Argument
    {
        public readonly string Speech;
        public readonly string FallacyId;
        public readonly IReadOnlyList<string> Premises;
        public readonly int WeakPremise;
        public readonly string Tone;

        public Argument(string speech, string fallacyId, IEnumerable<string> premises, int weakPremise, string tone);

        public bool IsRefutable => FallacyId != null;
    }

    public sealed class ArgumentValidator
    {
        public const int MinSpeechLength = 40;
        public const int MaxSpeechLength = 300;

        public ArgumentValidator(IEnumerable<string> refutableFallacyIds);
        public ArgumentViolation Validate(in Argument argument);
    }
}
```

```csharp
namespace Game.Grid
{
    public struct TileData
    {
        public const byte ImpassableCost = byte.MaxValue;
        public const int NoOccupant = 0;
        public const byte GroundLevel = 0;

        public static readonly TileData OutOfBounds;

        public TerrainId Terrain;
        public byte Cost;
        public bool Revealed;
        public int OccupantId;
        public byte Level;

        public readonly bool IsPassable => Cost != ImpassableCost;
        public readonly bool IsOccupied => OccupantId != NoOccupant;
    }

    public sealed class GridModel
    {
        public int Width { get; }
        public int Height { get; }
        public int MaxLevel { get; }
        public TileData this[Coord cell] { get; }
        public bool Contains(Coord cell);
        public bool TryGet(Coord cell, out TileData tile);
        public bool TrySet(Coord cell, TileData tile);
    }

    public static class IsoMath
    {
        public const float CellWidth = 1.0f;
        public const float CellHeight = 0.5f;
        public const int SortingStep = 10;
        public const int LevelPixels = 8;
        public const float LevelHeight = 0.5f;

        public static Vector2 GridToWorld(Coord cell) =>
            new((cell.X - cell.Y) * CellWidth, (cell.X + cell.Y) * CellHeight);

        public static Vector2 GridToSurface(Coord cell, float level) => GridToWorld(cell) + new Vector2(0f, Lift(level));
        public static float Lift(float level) => level * LevelHeight;

        public static Coord WorldToGrid(Vector2 world);

        public static int SortingOrder(Coord cell) => -(cell.X + cell.Y) * SortingStep;
    }

    public static class TerrainSteps
    {
        public const int MaxClimb = 1;
        public const int ClimbCost = 1;

        public static bool CanStep(in TileData from, in TileData to);
        public static int StepCost(in TileData from, in TileData to);
    }

    public sealed class PathFinder
    {
        public static bool AreBeside(Coord first, Coord second);
        public static bool AreBesideOnFoot(GridModel grid, Coord first, Coord second);
        public bool FindPath(GridModel grid, Coord from, Coord to, List<Coord> path);
        public bool FindPathBeside(GridModel grid, Coord from, Coord target, List<Coord> path);
        public void Reachable(GridModel grid, Coord from, int maxCost, List<Coord> cells);
    }

    public sealed class FogOfWar
    {
        public const int DefaultRevealRadius = 3;
        public FogOfWar(GridModel grid);
        public int Reveal(Coord center, int radius = DefaultRevealRadius);
        public byte[] ToBitmask();
        public bool TryLoadBitmask(byte[] bitmask);
        public void Hold(Coord cell);
        public bool Release(Coord cell);
    }
}
```

Tile 32×16 com PPU 16 é um losango de 2 × 1 unidades; `CellWidth`/`CellHeight` são o **meio passo** por eixo, e `GridToWorld` devolve o centro do losango. Na Unity o eixo Y cresce para cima, então a célula de maior `X + Y` está mais ao fundo e recebe sorting order menor. `SortingStep` deixa folga para sub-ordenar ator, prop e overlay dentro da mesma célula. O bitmask da névoa usa 1 bit por célula.

**Níveis de terreno.** `TileData.Level` é a altura da célula, de 0 a `TerrainSorting.MaxLevel` (3), em degraus de 8 px (`IsoMath.LevelPixels`; `LevelHeight` é 0,5 unidade, meia célula). `GridToWorld` continua sendo o chão; `GridToSurface(cell, level)` é o topo levantado.
- **Passo:** vizinhos com um nível de diferença são um degrau; mais que isso é penhasco, intransponível (`TerrainSteps.CanStep`). Subir custa 1 a mais (`TerrainSteps.ClimbCost`), descer não.
- **Quem respeita:** `PathFinder` (`FindPath`, `Reachable` e `FindPathBeside`), `GridWalker`, `DuelCrowdPlanner.StepsFrom` e, por eles, a CIRCULAR, o realce de alcance, as cenas de Timeline e o público do duelo.
- **Ao lado:** `PathFinder.AreBesideOnFoot` é o "ao lado" de conversa e desafio: vizinho a no máximo um degrau, nunca no pé de um penhasco.
- **Batalha:** `BattleRules` não muda. Altura não dá bônus; ela só pesa no custo do movimento.
- **Mapa:** o nível vem de `heights` no JSON do mapa (ver **Mapas**), e `GridSandboxController.Levels` o leva ao `GridModel`. O save não guarda altura.

**Alinhamento com o `Grid` da Unity** — validado na captura da GridSandbox, não mude sem capturar de novo:

| Ajuste | Valor | Por quê |
|---|---|---|
| `Grid.cellLayout` | `IsometricZAsY` | layout isométrico 2:1 |
| `Grid.cellSize` | `(2, 1, 1)` | com PPU 16 o tile mede 2 × 1 unidades; `(1, 0.5, 1)` só valeria com PPU 32 |
| posição do `Grid` | `(0, -0.5, 0)` | a Unity ancora a célula no vértice inferior; `IsoMath` usa o centro |
| `Tilemap.tileAnchor` | `(-0.5, 0.5, 0)` | faz o pivot *Bottom Left* dos sprites de tile cair no canto do losango |
| ordenação | `TilemapRenderer.Mode.Individual` + ator com `SpriteSortPoint.Pivot` na mesma camada e ordem dos blocos | o Transparency Sort Axis (0, 1, -1) decide pela altura do pé (o Z fica em 0 no chão plano; blocos, níveis levantados e o ator no meio do passo usam o Z só para ordenar), e o ator passa atrás de coluna e à frente de prop sem sorting order manual |
| tilemap `Blocks` | Transparency Sort Axis `(0, 1, -1)` e posição local `(0, 0, -BlockSortLift)`, com `BlockSortLift = 0.25` | o tile ordena pelo vértice de baixo do losango e o ator pelo centro da célula; sem o ajuste, o ator nas células da frente à esquerda e à direita empata com o bloco e aparece atrás dele. Com câmera ortográfica o Z não move o desenho, só o ponto de ordenação. Deslocar Y e compensar com a matriz do tile não funciona: a matriz também entra na ordenação |
| camadas por nível | cada camada é um `LevelTilemaps` com um tilemap filho por nível (`<Camada>_<nível>`), e `SetTile(cell, tile)` escolhe o filho pelo nível da célula | o nível 0 de chão, saídas, véu, alcance, cursor e névoa fica onde sempre esteve (ordens −20, −19, −18, −15, −10 e −5, posição zero); os níveis acima deles e todos os níveis de blocos e faces ordenam junto com os atores, na ordem 0 |
| nível levantado | posição local `(0, IsoMath.Lift(L), TerrainSorting.TilemapDepth(L, deslocamento))` | o filho sobe `Lift(L)` e anda o mesmo tanto em Z, então o eixo (0, 1, −1) continua ordenando pelo chão da célula. Só o desenho sobe |

```csharp
namespace Game.Battle
{
    public readonly struct Play
    {
        public readonly int ActorId;
        public readonly ActionId Action;
        public readonly int TargetId;
        public readonly Coord Destination;
        public readonly string ChosenFallacyId;
        public readonly int ChosenPremise;
        public readonly string QuoteId;
        public readonly Argument? PresentedArgument;

        public static Play Circulate(int actorId, Coord destination);
        public static Play Refute(int actorId, int targetId, string chosenFallacyId);
        public static Play Maieutics(int actorId, int targetId, int chosenPremise);
        public static Play Quote(int actorId, string quoteId);
        public static Play Concede(int actorId);
        public static Play Withdraw(int actorId);
        public static Play Argue(int actorId, int targetId, Argument argument);
    }

    public sealed class Outcome
    {
        public RejectionReason Rejection { get; }
        public bool Hit { get; }
        public bool Counterattack { get; }
        public int ActorCoherenceDelta { get; }
        public int TargetCoherenceDelta { get; }
        public int PlayerAudienceDelta { get; }
        public IReadOnlyList<Coord> Path { get; }
        public int TurnCost { get; }
    }

    public sealed class BattleResolver
    {
        public Outcome Resolve(in Play play, BattleState state);
    }

    public sealed class BattleState
    {
        public void Apply(Outcome outcome);
    }
}
```

`FallacyId == null` é o paradoxo do Ato II. `Argue` é a ação exclusiva do oponente: é assim que ele apresenta o `Argument` que o jogador responde com `Refute` ou `Maieutics`. Jogada inválida (fora de alcance, fora do turno, refutar paradoxo) volta como `Outcome` rejeitado — `Apply` registra no log e não gasta o turno.

A Plateia soma zero: `PlayerAudience` vai de 0 a 100 e a do oponente é o complemento. Todo número de balanceamento (dano, alcance, bônus posicional, limite de 50 turnos) mora em `BattleRules`, nunca espalhado pelo resolver.

**Duelo em fases.** `BattleRules.Phases` (um `PhasedDuel`) liga o duelo por fases; sem ele, nada muda. As regras puras ficam em `Game.Battle`:
- **`DuelPhase`:** id, ação que responde (`Counter`: REFUTAR, MAIÊUTICA ou CONCEDER), `UntilCoherence` (o piso da fase) e os argumentos da fase.
- **`PhasedDuel`:** a fase vem da Coerência do oponente (`IndexFor`, a primeira cujo piso está abaixo dela), então não há estado novo no `BattleState`. `Problems` recusa fase sem argumento, contra-ataque que não responde, piso que não desce, último piso diferente de 0 e REFUTAR contra paradoxo. `Rules()` devolve as regras com `AudienceVictory` desligado: a Plateia sozinha não pula fase.
- **`DuelPhaseRule.Apply`:** é a única linha nova do `BattleResolver.Resolve`, depois da penalidade de encurralado. A resposta da fase acerta (CONCEDER só com argumento na mesa) e leva o oponente até o piso, no máximo `PhaseCounterDamage` (20), com `PhaseCounterAudienceGain` (10) de Plateia. REFUTAR ou MAIÊUTICA certos na fase errada viram `Outcome.Deflected`: REFUTAR leva o contragolpe, MAIÊUTICA perde Plateia. Resposta errada continua sendo erro comum, e CONCEDER fora da fase é só conceder. Nenhum outro efeito passa o piso da fase.
- **`PhasedOpponent`:** argumenta sempre com os argumentos da fase atual, em rodízio, sem LLM.

---

## Fluxo: exploração

`GameStateMachine` vive na mesma cena do mapa. Batalha **não carrega cena** — é isso que dá a sensação de tactics dentro do mundo e evita perder o estado da névoa.

```
Exploration → (confirmar no desafiante) → BattleIntro → BattleTurn ⇄ BattleResolve → BattleOutro → Exploration
                                                                 │
                                                          Dialogue / Paused
```

Movimento: `PathFinder.FindPath` devolve células; o ator percorre a 250 ms por célula com interpolação **linear** entre centros, e o nível também é interpolado no degrau (`GridWalker.Level`). Easing faz o pixel tremer. `TileData.OccupantId` é atualizado na saída e na entrada de cada célula, nunca no meio.

**Altura e ordenação.** O ponto de ordenação é sempre o chão da célula; só o desenho sobe.
- **Valor de ordenação:** com o eixo (0, 1, −1), vale `y − z`, e o maior é desenhado primeiro. Um tile vale `y do chão + deslocamento da camada − TerrainSorting.LevelStep × nível` (`TerrainSorting.TileSortValue`).
- **Deslocamentos:** chão e faces 0,06, seta de saída 0,055, véu 0,05, alcance 0,04, cursor 0,03, névoa 0,02 e sombra do ator 0,01, todos logo atrás do ator da mesma célula. Bloco −0,25 (o antigo `BlockSortLift`) e véu do bloco −0,26.
- **`LevelStep` (0,07):** é maior que qualquer deslocamento. Na mesma profundidade, a célula mais alta é desenhada depois do ator mais baixo.
- **Ator:** `ActorView` põe o sprite em `GridWalker.SurfacePosition` (chão mais `Lift` do nível interpolado) com Z = `GridWalker.SortDepth`. No passo, o ator ordena como a mais da frente das duas células, com no máximo `StepSlack` (0,015) da interpolação, menos `LevelStep × nível`. Assim ele nunca afunda sob o chão em que entra ou de que sai, e um degrau mais alto ao lado do passo continua na frente dele. `GridWalker.WorldPosition` continua no chão, e é ele que a névoa (`FogTileRules.ShowsWhatStandsAt`) e a regra usam.
- **Testes:** `TerrainLevelTests` cobre os quatro casos difíceis nos níveis 0 e 2, o ator na frente e atrás de uma célula levantada, a caminhada sobre um platô, a subida de um degrau, o degrau mais alto ao lado do passo e a coluna em cima de um penhasco.
- **Ponteiro:** `GridCursor.TryHover` usa `TerrainPicking.CellAt`. Ele desce do nível mais alto até o chão, pixel a pixel, e fica com a primeira coluna que chega àquela altura sob o ponteiro, então o topo levantado, o penhasco e o que ele esconde resolvem certo. `GridCursor.TryPoint(cell)` aponta sem geometria: restauração do save, alvo da CIRCULAR e testes.

O `GameStateMachine` pertence ao `GridSandboxController`. O `SandboxBattleDirector` usa essa mesma instância, e a pausa a recebe no `Start`. A exploração só lê input com `ExplorationInputEnabled` ligado e a máquina em `Exploration`.

**Diálogo na exploração.** O `ExplorationDialogueDirector` fica em toda cena de mapa, gerado por `SandboxTools`.
- **Conversa:** Confirm chama `GridSandboxController.ConfirmCursor`, que pergunta primeiro ao `Interaction` do diretor.
  - Num NPC com `dialogue` ao lado, a Kleia e ele se viram um para o outro e a caixa abre. Num NPC distante, ela anda até a célula livre ao lado de caminho mais curto (`PathFinder.FindPathBeside`) e depois fala.
  - Num desafiante (ver **Desafio** na batalha da sandbox), a aproximação é a mesma: ela anda até a célula livre ao lado, os dois se viram um para o outro e o duelo começa. Ao lado dele, começa na hora. O diretor só conhece o desafio pela interface `IDuelChallenges` (`Challenges`), que o `SandboxBattleDirector` registra no `Awake`.
  - Num ator sem `dialogue` com um gatilho de célula pendente ao lado (o Menexeno da porta do Cerâmico), ela anda até o gatilho de caminho mais curto, e a cena dele toca ao pisar ali.
  - Célula sem NPC falante nem desafiante continua sendo andar. Oponente vencido não é desafiante: confirmar nele não faz nada, a menos que ele tenha `dialogue`, e aí ele conversa.
  - Thrasybios não tem `dialogue`: a resposta dele é a batalha.
- **Bloqueio:** a conversa abre o overlay `Dialogue` da máquina. Isso trava o movimento, os desafios e a pausa. Fechar volta a `Exploration` e faz save automático (`AutosavePolicy`).
- **Caixa:** `Assets/_Game/Prefabs/UI/ExplorationDialogue.prefab` é gerado por `BattleHudTools.BuildHud`, com o mesmo `DialogueBoxView` do HUD de batalha, em canvas próprio abaixo da pausa. Ela ocupa a faixa de baixo inteira (632×86), com 5 linhas de texto.
- **Dados:** `Assets/_Game/Data/Source/dialogues.json` vira o `DialogueLibrarySO` em `DataTools.Regenerate`.
  - `speakers` dá nome e retrato (id da folha do personagem) a cada falante. Linha sem `speaker` é narração.
  - Cada linha tem `conditions` (`flag`, com `equals`, `absent` ou `not`) e `effects` (`flag` e `value`, padrão `"true"`). `not` vale enquanto a flag não tem aquele valor, inclusive ausente (`StoryCondition.Not`), e é assim que se espera `damaris_branch` decidida (`"not": "undecided"`) ou um duelo ainda não vencido.
  - Os nomes seguem o `SaveFlags`: `act`, `damaris_branch`, `reputation`, `position` ou flag própria em snake case. A regeneração falha com flag inválida, falante desconhecido ou texto vazio.
- **Regras puras (Core):** `DialogueScript`, `ScriptLine`, `StoryCondition`, `StoryEffect` e `DialogueRun`.
  - As condições são julgadas quando a conversa começa. Por isso, a linha que marca `met_x` não revela na mesma conversa as linhas que exigem `met_x`.
  - Os efeitos entram no `SaveData` à medida que cada linha aparece.
  - O diretor carrega as flags do save ativo e reaplica os efeitos da sessão sobre o `CaptureSave` ao salvar. Save antigo sem `flags` continua válido e lê toda flag como ausente.
- **Gatilho de célula:** `triggers` no JSON do mapa (`x`, `y`, `dialogue` ou `cutscene`, `flag` e `conditions` opcionais, no mesmo formato das linhas).
  - Ao entrar na célula, pelo `CellEntered` do walker, a Kleia para nela e a cena toca se as condições valem (`DialogueTriggerLink.IsDue`). A `flag` é gravada no disparo, então a cena toca uma vez só. Vários gatilhos podem dividir a mesma flag. Se um NPC ao lado fala o mesmo diálogo, os dois se viram um para o outro.
  - Gatilho com `cutscene` toca a cena de Timeline do mapa em vez de um diálogo (`ExplorationDialogueDirector.TryPlayCutsceneOnce`).
  - `MapLayout.Validate` recusa gatilho fora do mapa, em célula bloqueada, sem diálogo nem cena (ou com os dois), com cena que o mapa não tem, com flag que não seja própria, com condição inválida ou na célula de outro gatilho, de uma saída ou de um ator.
- **Deixa de exploração:** `cues` no JSON do mapa (`on`, `dialogue`, `flag`, `conditions`) viram `StoryCueLink` no `StoryCueDirector`, criado só em mapa com deixas.
  - `on` é um `ExplorationCue` (Core): `MapStart` (mapa aberto sem vir por uma saída), `FogRevealed` (passo que revela névoa, pelo `GridSandboxController.FogRevealed`, que interrompe a caminhada), `WalkFinished` (fim de uma caminhada que não foi para conversar) e `Idle` (primeiro momento quieto com as condições cumpridas, inclusive ao chegar por uma saída ou voltar de uma batalha).
  - A Ágora usa `Idle` para mandar a Kleia de volta ao Cerâmico depois de vencer o Thrasybios (`agora_after_thrasybios`, flag `headed_home`), senão nada a leva à cena do Menexeno e a MAIÊUTICA fica trancada. Com `headed_home`, o Menexeno da Ágora troca o conselho de antes do duelo por um "vá para casa" e, depois de `maieutics_unlocked`, pelas falas de quem já ensinou.
  - Toca só com a exploração quieta: sem conversa, aproximação, gatilho pendente, batalha ou pausa. A `flag` é gravada no disparo (`ExplorationDialogueDirector.TryPlayOnce`), como no gatilho.
  - É assim que o Cerâmico ensina a andar, a névoa e a conversar, cada passo uma vez.
- **NPC condicional:** `conditions` num ator do mapa vira `ActorPresenceLink` no `GridSandboxController`. O ator fica desligado e sem walker até o save ativo cumprir as condições, e só é posto na célula quando o progresso carrega. O jogador não pode ter condições.
  - **Chega andando:** `enters` (`x`, `y`) num ator condicional é a célula por onde ele entra. Em vez de surgir no posto, ele nasce ali e caminha até a célula dele pelo `PathFinder`, então o jogador vê o Menexeno e a Damaris descerem a rua da Ágora e o Thrasybios do julgamento subir do mercado para a escadaria.
    - `MapLayout.Validate` recusa entrada fora do mapa, em célula bloqueada, na própria célula do ator, sobre outro ator, sem caminho até o posto ou num ator que não espera flag nenhuma.
    - `GridSandboxController.IsSettling` é verdadeiro enquanto alguém ainda caminha para o posto, e o `ExplorationDialogueDirector` segura o gatilho de célula pendente até todo mundo estar no lugar — a cena só começa com os dois em cena. O gatilho não se perde: ele espera.
    - Se o caminho estiver bloqueado (a Kleia parada no corredor), ele tenta de novo a cada quadro e não conta como chegando, então nada trava.
  - **Ao vivo:** `GridSandboxController.ApplyStory(story)` põe quem passou a cumprir as condições e tira quem deixou de cumprir. O `ExplorationDialogueDirector.RefreshMap` chama isso com a história da sessão em `Remember`, ao fechar uma conversa, ao fim de uma cena de Timeline e ao fim de um duelo, sempre com a máquina em `Exploration`. Se a célula de partida estiver ocupada, ele tenta de novo nos quadros seguintes.
  - Quem sai por `Leave` numa cena (`TryDismiss`) não volta na mesma sessão, mesmo que as condições ainda valham.
  - Teste PlayMode que usa o walker de um ator condicional (Thrasybios do mercado, Menexeno da Ágora) espera `ProgressLoaded`.
- **Bloco condicional:** `conditionalBlocks` no JSON do mapa (`x`, `y`, `conditions`) aponta um bloco que já está em `blocks` e vira `BlockPresenceLink` no `GridSandboxController`.
  - A cena é gerada com o bloco. Quando o progresso carrega, antes de restaurar a posição, o bloco cujas condições falham sai do render (`BlockTileAt`) e a célula volta ao custo do chão (`SandboxTools.FloorCost`, gravado no link). O `GridModel` decide, o tilemap só desenha.
  - Serve para os dois sentidos: `absent` tira um prop (a roda vendida), e uma condição presente faz um prop aparecer (a pilha de ânforas encalhadas).
  - `MapLayout.Validate` recusa bloco condicional fora do mapa, em célula sem bloco, repetido, sem condição ou com condição inválida.
- **Pressão (beat I.6):** `StoryPressure` (Core, puro) conta os duelos concluídos: oponentes distintos com `duel_<id>` gravado (`won`, `lost`, `withdrew` ou `stalemate`) ou com vitória em `SaveData.Duels`. Revanche conta uma vez, e fugir também conclui o duelo: o boato cresce porque a Kleia discute em público, não porque ganha.
  - Os estágios são flags derivadas, lidas por `SaveFlags.TryGet` como a vitória de save antigo e nunca gravadas: `pressure_rumor` com 2 duelos (o do Thrasybios e o primeiro de bairro), `pressure_deserted` com 3 e `pressure_tools` com 4. São nomes próprios, então condições usam presença ou `absent`.
  - Os números moram em `StoryPressure`. Diálogos, deixas, NPCs e blocos condicionais leem os estágios, sem código por nome.
- **Dias:** o jogo não tem relógio. `StoryDays` (Core) passa um dia a cada volta para casa: chegar por uma saída a um mapa com `"home": true` (o Cerâmico, `GridSandboxController.IsHome`) soma 1 em `day` (a partir de 1) e desconta 1 de toda flag `*_absent_days`, que some ao chegar a zero, e então `absent` passa a valer.
  - É o menor calendário honesto: a Kleia vai à Ágora de dia e dorme na oficina. Continuar um save ou abrir o jogo no Cerâmico não passa dia.
  - O controlador aplica o dia no save da chegada e na cópia usada pelos NPCs e blocos condicionais. O `ExplorationDialogueDirector` só carrega a história depois de `ProgressLoaded`, então as condições da cena já leem o dia novo.
- **Cena de Timeline:** `cutscenes` no JSON do mapa (`id` e `steps`) vira um `TimelineAsset` por cena em `Assets/_Game/Data/Cutscenes/<mapa>/<id>.playable`, gerado por `CutsceneTools` dentro de `SandboxTools.BuildSandbox`.
  - Cada passo tem `at` (segundos, estritamente crescente e maior que 0) e `action` (`CutsceneAction`, Core): `Face` (`actor` vira para `toward` ou para `x`/`y`), `Dialogue` (`dialogue`), `Walk` (`actor` anda até `x`/`y` pelo `PathFinder`) e `Leave` (`actor` sai do mapa e libera a célula por `GridSandboxController.TryDismiss`). Atores são citados pelo `character`, que precisa aparecer uma única vez no mapa; o jogador não sai.
  - Cada passo é um `CutsceneMarker` no marker track da timeline. O `CutsceneDirector` fica no mesmo objeto do `PlayableDirector`, recebe as notificações e executa um passo por vez: `Dialogue` e `Walk` pausam a timeline até a caixa fechar ou o ator parar. A timeline dura meio segundo além do último passo.
  - A cena abre o overlay `Cutscene` da máquina (trava exploração, desafios, deixas e pausa). Os diálogos abrem `Dialogue` por cima e, ao fechar, voltam para `Cutscene` sem save. No fim, a máquina volta a `Exploration` e o save automático grava de uma vez os efeitos da cena; sair no meio faz a cena tocar de novo.
- **Escolhas:** uma linha com `choices` (2 a 4 opções, texto de até 24 caracteres) para a conversa até a Kleia responder.
  - Cada opção tem `effects` e, opcionalmente, `lines` próprias, que tocam logo depois; em seguida a conversa continua após a linha da pergunta. As condições dessas linhas são julgadas quando a opção é escolhida.
  - Regras puras em `DialogueChoice` e `DialogueRun.Choose` (Core). `DialoguePlayer` (UI) conduz a `DialogueBoxView` na exploração e na batalha: as opções só aparecem depois que a pergunta termina de ser digitada, Navigate move o cursor, Confirm escolhe e o clique numa linha (`PanelRowPointer`) também. Clique fora das opções não escolhe nada.
  - O `DialogueEntry` guarda os ramos achatados em `branches`, por índice. Entrada antiga sem `choices` continua válida, e `DataTools.Regenerate` valida opções, efeitos e falantes dentro dos ramos.

---

## Fluxo: um turno de batalha

```
1. TurnQueue.Next()                          → Debater
2. AI?      OpponentBehaviour.Decide()       → Play
   Player?  BattleMenu collects input        → Play
3. BattleResolver.Resolve(play, state)       → Outcome   [pure]
4. BattleState.Apply(outcome)                            [single mutation]
5. BattleView.Replay(outcome)                            [animation only]
6. VictoryCheck.Evaluate(state)
```

Vitória: Coerência do oponente em 0, **ou** Plateia em 100%. Derrota: Coerência do jogador em 0.

**A virada do Ato II** mora inteiramente no passo 3: quando `Argument.IsRefutable == false`, `ActionId.Refute` é rejeitada pelo resolver e a UI desabilita o botão. Nenhuma outra camada precisa saber que o Ato II existe. Isso vale defender — é a tese do jogo virando mecânica num ponto só do código.

**Cada argumento se responde uma vez.** A MAIÊUTICA não pode ser a resposta a tudo, e o equilíbrio mora em três regras:
- **Argumento respondido:** uma REFUTAÇÃO certa ou qualquer MAIÊUTICA, certa ou errada, derruba o argumento da mesa (`BattleState.IsArgumentAnswered`, `HasOpenArgument`). Até o oponente argumentar de novo, REFUTAR e MAIÊUTICA voltam como `ArgumentAlreadyAnswered` ("Esse argumento já caiu."). Sem isso, a pergunta errada entregava a premissa certa na jogada seguinte, porque quase todo argumento tem duas premissas. A REFUTAÇÃO errada deixa o argumento na mesa: o tutorial e o erro do Thrasybios dependem disso.
- **Premissas embaralhadas:** `ShuffledPremiseSource` (Battle) embaralha, com a seed do duelo, a ordem das premissas de todo argumento dos duelos comuns, e o `WeakPremise` acompanha o texto. No banco de fallback a premissa fraca era a segunda em 72% dos casos, e o jogador aprendia a posição, não o argumento. O tutorial e o julgamento de rua ficam de fora, com a ordem escrita à mão.
- **Números (`BattleRules`):** a REFUTAÇÃO certa tira 25, e a MAIÊUTICA certa tira 30, mas custa dois turnos. A MAIÊUTICA errada custa 10 de Plateia e 8 de Coerência da Kleia (`MaieuticsMissCoherenceLoss`), que o tutorial zera, como zera o contra-ataque. Quem sabe a falácia refuta; quem não sabe arrisca a pergunta.

- **Painel de depuração da sandbox:** o `OnGUI` do `GridSandboxController` (descrição da célula, spawn e remoção de ator de teste, gizmos) começa oculto em `DebugPanelVisible`. F3 alterna o painel só em build de desenvolvimento e no Editor (`Debug.isDebugBuild`). Os métodos por trás dele continuam públicos para os testes. Com o painel visível, o ponteiro sobre ele (`IsOverDebugPanel`) não move o cursor do grid, e o clique nele não anda com a Kleia.
- **Pular a história (F4):** `StoryCheckpoints` (Core, puro) é a lista ordenada dos pontos do Ato I — o recado do pai, o duelo do mercado, a lição da porta, a pressão com quatro duelos, a oferta da Damaris recusada, a convocação do arauto, o julgamento vencido e o selo de bronze —, cada um com as flags que aquele trecho deixaria gravadas.
  - `ExplorationDialogueDirector.SkipToNextCheckpoint` aplica os efeitos do próximo ponto pelo `Remember`, então o mapa se atualiza na hora (NPC condicional entra andando, saída fecha ou abre) e o save automático grava. Ele recusa com diálogo, cena ou batalha aberta.
  - F4 chama isso em build de desenvolvimento e no Editor, e o painel do F3 mostra em que ponto o save está e qual é o próximo, com um botão. O `IStoryShortcut` é a costura: o diretor se registra no `GridSandboxController.StoryShortcut`, como faz com `Interaction`.
  - Serve para testar: um save pulado é indistinguível de um jogado, porque as flags são as mesmas que as cenas gravariam.
- **Ponteiro sobre a UI:** do mesmo jeito, o ponteiro sobre qualquer elemento de UI (`GridSandboxController.IsPointerOverUi`, que pergunta `EventSystem.current.IsPointerOverGameObject()`) não move o cursor e o clique não anda. Vale para a faixa do HUD, a caixa de diálogo e os menus sobre o mapa, inclusive na mira da CIRCULAR. Teclado e gamepad não mudam. `ExplorationInputPlayTests` clica com o mouse de verdade num painel sobre o mapa.
- **Input da exploração x menus:** exploração e menus leem o mesmo mapa `Grid`. Enquanto `PauseMenuController.CapturesInput` for verdadeiro (pausa aberta ou fechada neste quadro), o `GridSandboxController` ignora Confirm, Cancel, ponteiro e navegação. O quadro do fechamento conta porque o callback do menu roda antes do `Update`: sem isso, o Enter que escolhe "Voltar ao jogo" também mandaria a Kleia andar. Os testes de `ExplorationInputPlayTests` apertam teclas e o mouse de verdade pelo `InputTestFixture`. O `GridControls` é um asset compartilhado e guarda o estado de ações criado por testes anteriores, fora do mundo do fixture; por isso o teste re-resolve os bindings (`InputActionAsset.devices`) depois de carregar a cena e de novo depois do `TearDown`, senão as ações ficam sem controles.
- **Batalha jogável na sandbox:** um único `SandboxBattleDirector` por cena conduz o duelo contra qualquer `OpponentSO` do mapa, sem código por nome.
  - **Quem é oponente:** `SandboxTools` liga ao diretor, como `MapOpponent` (ator, `OpponentSO` e retrato), todo ator do mapa cujo `character` é um `id` de `opponents.json`. Mapa sem oponente (o Cerâmico) não ganha diretor.
  - **Desafio:** o duelo só começa quando o jogador confirma (clique, teclado ou gamepad) na célula de um desafiante. Andar ao lado ou passar por ele nunca começa nada.
    - `ChallengeRule` (Core) escolhe o desafiante da célula confirmada entre os `Challenger` do mapa: ator colocado, em célula revelada, ainda não vencido, e só no modo `Exploration`. Com diálogo, cena, pausa ou batalha aberta, ninguém aceita desafio. Desafiante sob a névoa não aparece, então também não pode ser desafiado.
    - O `SandboxBattleDirector` implementa `IDuelChallenges`: `ChallengerAt` responde ao `ExplorationDialogueDirector`, que faz a aproximação, e `Challenge` revalida e chama `StartBattle` quando a Kleia chega ao lado.
    - Depois de perder ou fugir, basta confirmar nele de novo. Oponente não conversa: ator com `dialogue` não pode ser oponente.
  - **Entrada na batalha:** desliga `GridSandboxController.ExplorationInputEnabled` e a pausa, mostra o `BattleHud`, chama o público, escurece o mundo fora da arena e centraliza a câmera no duelo, sem zoom.
    - **Câmera:** são duas `CinemachineCamera`. A da exploração segue a Kleia com o `CinemachineConfiner2D` do mapa. A `DuelCamera` segue o `DuelFocus`, não tem confiner, tem prioridade maior e fica desligada fora do duelo.
    - `ExplorationCamera.FrameDuel` põe o foco no ponto médio dos dois debatedores (`DuelArena.WorldCenter`, levantado pelo nível médio deles), descido `HudLayout.BottomStripFraction` (0,25) × o tamanho ortográfico, e liga a `DuelCamera`. Assim a arena fica no meio da área acima da faixa do HUD (`ExplorationCamera.FramingFor`), mesmo encostada na borda do mapa, e fora dele aparece o preto do fundo. O foco fica preso à grade de pixels da arte e acompanha o tamanho ortográfico real da câmera.
    - O `CinemachineBrain` desliza entre as duas com `DefaultBlend` EaseInOut de 0,6 s (`DefaultGlideSeconds`), e `IsGliding` é o blend em andamento. O `PixelSnappedCamera` segura a saída na grade de pixels durante o blend.
    - A câmera da exploração fica com `StandbyUpdate.Always`, então continua seguindo a Kleia presa aos limites durante o duelo. Na saída, `FrameExploration` desliga a `DuelCamera`, e o blend volta exatamente para onde o confiner a segura, sem salto.
    - **A grade de pixels é do `PixelSnappedCamera`, na câmera de saída.** As câmeras virtuais não levam `CinemachinePixelPerfect`: a extensão corrige a lente da câmera ao vivo a cada quadro e, contra uma câmera amortecida com dead zone, fica caçando um pixel — medido em 2,7% dos quadros de uma caminhada reta, contra 0% só com o `PixelSnappedCamera`. Ele arredonda a própria posição da câmera com histerese de `HysteresisPixels` (0,35), então só troca de pixel quando a posição real passa de 0,85 px do mostrado, e o assentamento no fim do passo não oscila.
    - O tamanho ortográfico nunca muda. Por isso, antes da troca, a câmera que entra recebe o tamanho que a tela já usa, e a da exploração volta ao tamanho de projeto quando o blend termina (`CinemachineCore.BlendFinishedEvent`).
    - `HudLayout` (UI) é o contrato com o HUD: a faixa de baixo ocupa `BottomStripFraction` da altura da tela.
    - **Arena:** `DuelCrowdPlanner.Plan` (Grid, puro e com seed) devolve a `DuelArena`: centro, lugares do público e raio iluminado. As regras ficam em `CrowdRules`: 8 a 12 lugares num anel de raio 3,5 ± 0,75 células em volta do centro. Só entram células livres, reveladas e a pelo menos duas células de cada debatedor, espalhadas por ângulo. Quando o anel não cabe, como num canto do mapa, os lugares chegam a até 5,5 de raio e formam uma segunda fila. Um teste garante de 8 a 12 espectadores em todo duelo da Ágora.
    - **Público chegando (`BattleCrowd`):** cada lugar tem uma entrada na borda do mapa, fora da arena iluminada, e um cidadão (`citizen`, clone do `SpectatorTemplate` que o `SandboxTools` gera) anda dali até o lugar, a 0,2 s por célula, sem cortar o miolo da arena. Enquanto está no escuro, ele usa o material do véu. Ao chegar, vira para o centro.
    - **Espera:** os turnos só começam quando todos chegam ou quando passam 3 s (`ArrivalCapSeconds`), e quem atrasou vai direto para o lugar. Nesse meio tempo, a fase do diretor é `Gathering`, e a fala de abertura já pode estar na caixa. Teste que quer pular a chegada zera `ArrivalCapSeconds`, e `SandboxBattleDirector.CrowdSeed` fixa a seed.
    - **Fora do mundo da exploração:** os espectadores andam em grades próprias, cópias do terreno, e nunca ocupam o grid da exploração. Por isso não são desafiantes, não conversam, não entram no save nem nos duelos gravados, e também não contam como plateia para o bônus de adjacência. No grid da batalha, `BattleSetup.Spectators` ocupa os lugares deles, então a CIRCULAR não os alcança.
    - **Escuro fora da arena (`ArenaVeilView`):** um xadrez de 50% de `void` cobre tudo o que fica fora do raio iluminado. É efeito de código, sem arte, feito pelo shader `Game/Arena Veil` (`Assets/_Game/Shaders/ArenaVeil.shader`), com o padrão preso à grade de pixels do mundo. O chão de fora ganha o tile `fog` no tilemap `ArenaVeil` do nível da célula, entre o chão e o `Range`. Os blocos de fora são copiados no tilemap `BlocksVeil`, 0,01 à frente dos `Blocks` na ordenação, e escurecem na altura inteira, não só no losango do chão. As faces de fora são copiadas do mesmo jeito no `FacesVeil`. Só entra bloco visível: bloco sob a névoa não está nos `Blocks`, e o que se revela durante o duelo é copiado na hora (`Tilemap.tilemapTileChanged`). Os atores de fora trocam de material. Os debatedores e o público ficam sempre na luz. A densidade sobe em degraus (Bayer 4×4) enquanto o público chega e some em 0,4 s na saída. É só render: não toca `FogOfWar` nem `TileData.Revealed`.
    - **Geração:** o `SandboxTools` só cria o `BattleCrowd` e os dois tilemaps do véu em mapa com oponente.
  - **Turnos:** o oponente pensa `ThinkSeconds` antes de jogar pela `OpponentBrain`; o jogador escolhe no menu. As falas e os resultados aparecem na caixa de diálogo. Ao refutar certo, o ícone da falácia sobe sobre o oponente da vez e ela entra no compêndio por `GameServices.Compendium.Unlock`. As falas da Kleia e do oponente mostram o retrato de cada um (`SandboxBattleDirector.ConfigurePlayerPortrait` e `MapOpponent.Portrait`); a narração fica sem retrato. Os mesmos retratos, o nome da Kleia (`battle.player_name`) e o `DisplayName` do oponente vão para os painéis do HUD por `BattleHud.Introduce`. `SandboxTools.PortraitFor` usa o retrato dedicado `art/placeholders/portraits/<personagem>_portrait.png` (32×32) quando ele existe e, senão, o quadro parado de frente (`<personagem>_se_0`) da folha do personagem.
  - **Saída:** ao fim, volta à exploração e faz save automático. Nada recomeça sozinho: a Kleia pode ficar ao lado do oponente sem novo duelo.
    - **Público indo embora:** seja qual for o resultado, `BattleCrowd.Disperse` manda cada espectador para a borda de um dos dois lados do mapa que ficam atrás dele, sorteado com seed. Assim o público se divide em lados opostos. Cada um parte com um pequeno atraso e some ao pisar na borda.
    - **Exploração de volta:** o input volta na hora. Como o público só existe nas grades próprias, a Kleia anda por cima de quem ainda está saindo. O véu some, e a câmera volta a seguir a Kleia, de novo presa aos limites do mapa.
  - **Resultado como flag:** todo duelo terminado grava `duel_<id>` (`DuelStoryFlags` no Core, `won`, `lost`, `withdrew` ou `stalemate` por `DuelStoryResults` no Battle) nas flags da história pelo `ExplorationDialogueDirector.Remember` e no save automático da saída. Diálogos, gatilhos e NPCs condicionais leem essa flag, como o Menexeno na porta da oficina depois de `duel_thrasybios=won`. Save de antes das flags continua valendo: sem a flag, `SaveFlags.TryGet` lê `duel_<id>=won` do duelo vencido em `SaveData.Duels`.
  - **Custo do duelo (card BTL-07):** a Coerência é da jornada, não do duelo.
    - `StoryStamina` (Core, puro) guarda o desgaste na flag `coherence_lost` da história, então nada muda no schema do save nem no documento do Firestore. `Carried(save, full)` é com quanto a Kleia entra no duelo (`BattleSetup.PlayerCoherence`), e o piso é `StoryStamina.Bruised` (20 de 60): derrota nenhuma a deixa sem chão para a próxima.
    - Ao fim de cada duelo, `SandboxBattleDirector.RememberTheToll` grava dois efeitos pelo `Remember`: o que sobrou dela (perder cai ao piso, fugir cobra `WithdrawToll`, vencer guarda o que restou) e a Reputação por `ReputationRules` (vencer +5, perder e fugir −10, entre 0 e 100). O `BattleRules.WithdrawReputationPenalty` lê o mesmo número do Core.
    - **Descanso:** chegar em casa (`"home": true`) passa o dia e devolve a Coerência inteira, no mesmo ponto do `GridSandboxController` em que `StoryDays.Pass` roda, no save da chegada e na cópia da história. É a única cura, e é isso que faz a volta ao Cerâmico valer a viagem.
    - O duelo do tutorial fica de fora: ele começa cheio e não cobra pedágio.
  - **Ação bloqueada:** `ActionUnlocks` (Core) liga ação a flag; hoje só a MAIÊUTICA, por `maieutics_unlocked`, que a cena do Menexeno grava. O diretor passa as ações bloqueadas em `BattleSetup.LockedActions` para o `BattleState`, o resolver devolve `RejectionReason.ActionLocked` para o jogador e o menu mostra `rejection.action_locked`. Teste que usa MAIÊUTICA pela sandbox grava a flag antes.
  - **Habilidades (beat I.7):** `AbilityUnlocks` (Core) lê as habilidades do save pelas flags `ability_<id>` do `DamarisOffer`. O diretor as passa em `BattleSetup.Abilities` para o `BattleState`, que guarda os usos da batalha (`BattleRules.InversionUsesPerBattle` e `ListeningUsesPerBattle`, 1 cada).
    - `AbilityRules` (Battle, puro) é a única linha nova do resolver: depois de resolver o `Argue` do oponente, marca `Outcome.TriggeredAbility`. Só `BattleState.Apply` gasta o uso, e o `BattleLogEntry` registra a habilidade.
    - **INVERSÃO:** o primeiro ataque do oponente vira Plateia para a Kleia, do mesmo tamanho: o deslocamento do sofista troca de lado, e o dano de Coerência do filósofo vira ganho de Plateia. O contra-ataque de uma refutação errada não é invertido.
    - **ESCUTA:** o primeiro argumento revela a premissa fraca (`BattleState.IsWeakPremiseRevealed`, até o próximo argumento). O submenu da MAIÊUTICA a pinta de dourado como o modo assistido (`ActionMenuModel.RevealsWeakPremise`), sem mudar o ataque.
    - Depois da fala do oponente, a caixa narra `battle.inversion` ou `battle.listening` (a premissa entra no `{0}`), por `SandboxBattleDirector.AnnounceAbility` e `BattleText.AbilityKey`.
  - **Vitória gravada:** vencer grava `DuelRecord(true, victory)` em `GridSandboxController.Duels` (`DuelProgress`), e `CaptureSave` leva isso a `SaveData.Duels` pela chave do `id`. `DuelVictoryRule` classifica pelo último golpe acertado (REFUTAR ou MAIÊUTICA); sem nenhum, conta como `Concede`. Ao carregar a cena, o controlador lê os duelos do save ativo em qualquer local, e oponente vencido nunca mais aceita desafio. Save antigo sem `duels` continua válido.
  - **Grid da batalha:** `BattleSession` roda o duelo numa cópia do grid da exploração, porque o `BattleState` exige células livres e os walkers já as ocupam. Os movimentos voltam para os walkers pelo `Outcome.Path`.
  - **CIRCULAR:** abre a fase `PlayerTargeting`.
    - O menu para de receber input, o alcance azul fica no chão e o cursor da exploração volta a responder a mouse, teclado e gamepad por `GridSandboxController.CursorInputEnabled`.
    - Confirmar numa célula azul move a Kleia: o destino vai em `BattleMenuChoice.Destination` e é validado por `CirculateTargeting`. Cancelar volta ao menu.
    - Uma escolha sem destino ainda cai em `BattleSession.CirculateDestination`, a célula alcançável mais próxima do oponente.
  - **Voz do oponente:** ator com `dialogue` não pode ser oponente, então a fala de um duelista vem do bloco `dialogue` dele em `opponents.json` (`intro`, `victory`, `defeat`), que vira `OpponentSO.Lines`. `OpponentLines` é o tipo dessas três falas; `OpponentPhases` o contém e delega, então o julgamento de rua continua igual e `OpponentSO.Lines` devolve as falas das fases quando o duelo é em fases e as próprias quando não é. O `SandboxBattleDirector` toca `Intro` no lugar da fala de abertura e `DialogueFor(resultado)` no lugar da linha de status — `victory` é a vitória da Kleia, `defeat` a derrota dela. O tutorial vence os dois: enquanto ele está pendente, as deixas dele mandam. `DataTools` recusa `dialogue` em oponente que já é tutorial ou duelo em fases, e cobra que cada id exista em `dialogues.json`.
  - **Dados:** o elenco vem de `Assets/_Game/Data/Source/opponents.json`, gerado como `OpponentSO` com `id` e folha (`<id>_sheet.png`), e as falas vêm de `battle.*` em `ui_text.json`. `initial_audience` é a Plateia inicial da Kleia; a tabela do GDD dá a do oponente, que é o complemento.
  - **Elenco do Ato I:** Thrasybios e os cinco duelistas de bairro do beat I.5: Óron (falso dilema), Phaidra (apelo à ignorância), Kritolaos (ad hominem), Simo (espantalho) e Bátilo (generalização apressada, perfil `Philosopher`). Cada pool tem a falácia da família e uma aparentada. Um teste simula duelos contra cada um, na célula em que ele está na Ágora e com a Kleia na célula ao lado a que ela chega andando da entrada, e exige vitória do jogador. O julgamento de rua entra na mesma simulação com o `PhasedOpponent` e um jogador que conhece a resposta de cada fase.
  - **Julgamento de rua (beat I.8):** `thrasybios_street_trial` é outro oponente, com a folha do Thrasybios (`sheet`), então o duelo do tutorial, o `duel_thrasybios` e os saves antigos continuam iguais.
    - **Fases:** o `phased_duel` de `opponents.json` vira `OpponentSO.Phases` (`OpponentPhases`): diálogos `intro`, `victory` e `defeat`, e três fases. Ataque pessoal (ad hominem, piso 40, CONCEDER), falso dilema (piso 20, REFUTAR) e medo político (apelo à emoção invocando os Trinta, piso 0, só MAIÊUTICA na premissa sobre o que ele mesmo fez sob a tirania). Cada fase tem `dialogue`, `music`, `posture`, `tint` opcional e seus `arguments` escritos à mão, validados pelo `ArgumentValidator` na regeneração. O julgamento inteiro roda offline e nunca chama o LLM.
    - **Diretor:** com `OpponentSO.IsPhased`, o `SandboxBattleDirector` usa `PhasedDuel.Rules()` e o `PhasedOpponent`, toca o `intro` no lugar da fala de abertura e, a cada fase que começa (`ContinueWithPhase`), toca o diálogo dela, aplica a postura e dispara `PhaseStarted` com um `DuelPhaseCue` (oponente, índice, fase, id da música e postura, também em `CurrentPhaseCue`). Vitória e derrota tocam `victory` e `defeat` no lugar da linha de status. Oponente em fases não é pré-aquecido (`SandboxBattleDirector.Prewarms`).
    - **Postura:** `ActorView.Posture` troca o estado parado por `action` (a coluna de ação da folha), e `ActorView.Tint` pinta o `SpriteRenderer`. Na fase 2 ele gesticula; na 3, empalidece (`#D0C8F8`). Tudo volta ao normal no fim do duelo.
    - **Música:** cada fase pede `MusicForPhase(MusicId)` (`street_trial_personal_attack`, `street_trial_false_dilemma`, `street_trial_political_fear`). Hoje as três são apelidos de `music_battle` em `audio.json`, então a trilha segue sem recomeçar. Uma gravação com o nome da fase passa a tocar sem mudar código.
    - **Textos:** `battle.concede_counter` narra o CONCEDER que acerta, e `battle.phase_deflected` a resposta certa na fase errada (`BattleText.OutcomeKey`).
    - **Derrota não é fim de jogo:** perder grava `duel_thrasybios_street_trial=lost` e, pelo `defeat`, `lost_street_trial`. Ele continua na escadaria, e o `intro` da nova tentativa tem outras falas.
  - **Tutorial do Thrasybios (beats I.2 e I.3):** o `tutorial` de `opponents.json` (`fallacy` do pool, `done_flag` e um diálogo por `TutorialCue`) vira `OpponentSO.Tutorial`. Enquanto a `done_flag` falta no save, o duelo é guiado.
    - `RefuteTutorial` (Battle, puro) decide a deixa: demonstração da ânfora ao começar, explicação do argumento no primeiro turno, dica ao abrir a lista de falácias (a cada turno), erro e lembrete depois de outra ação. A dica cita a falácia pelo `display_name` do catálogo, o mesmo rótulo da lista, e um teste garante isso.
    - O `TutorialOpponent` tira do `ScriptedArgumentSource` o primeiro argumento de fallback da falácia (na ordem do arquivo, o da ânfora) e o repete a cada turno, então o erro continua na mesa até ser refutado.
    - `RefuteTutorial.Rules(maxCoherence)` zera o contra-ataque e a penalidade de encurralado e faz uma refutação certa tirar toda a Coerência dele: uma falácia, uma refutação, vitória garantida (GDD I.2).
    - As deixas tocam na caixa do HUD pelo `DialoguePlayer`, com as flags do `ExplorationDialogueDirector` da cena (`SandboxBattleDirector.ConfigureStory`), e o autosave da saída passa pelo `CaptureSave` dele.
    - Vencer grava a `done_flag` e toca `agora_wrong_laughter` (I.3: a plateia ri dela) no lugar da mensagem de vitória. A cena termina na oferta de aulas pagas, que grava `thrasybios_lessons` = `accepted` ou `refused` para o epílogo.
    - Fugir deixa o tutorial pendente, mas a demonstração toca uma vez só (`saw_thrasybios_demo`). Com a `done_flag` gravada, os duelos seguintes contra ele são normais.
- **Realce de alvo:** `BattleTargetPreview.Apply` desenha o realce a partir da entrada selecionada no menu. REFUTAR indisponível limpa o realce, então o vermelho nunca aparece num paradoxo. A maiêutica usa `BattleResolver.MaieuticsRange`, que já inclui o bônus do Ato II. `BattleHud.BindPreview(highlighter)` liga isso ao `SelectionChanged` do menu, e a mensagem de indisponível é "não há erro a apontar". Na sandbox, o `RangeHighlightView` pinta essas células no tilemap `Range` (entre o chão e o cursor) com `hl_move`, `hl_refute` e `hl_maieutics`, só enquanto o jogador escolhe; o diretor limpa o realce no turno do oponente e ao sair da batalha.
- **Oferta de Damaris:** `DamarisOffer.Apply` grava a escolha no save uma única vez.
  - Aceitar dá a flag `ability_inversion`, soma 200 em `drachmas` e grava `menexeno_absent_days` = 3.
  - Recusar dá `ability_listening` e `menexeno_respect`.
  - O efeito `damaris_branch` de diálogo passa por ela: `SaveFlags.TrySet` chama `DamarisOffer.Apply` quando o ramo ainda está `undecided`. Reaplicar o mesmo ramo (o `CaptureSave` reaplica os efeitos da sessão) não faz nada, então as dracmas entram uma vez só. Trocar um ramo já decidido só muda o campo, para o `set_flag` de depuração.
  - O `damarisBranch` sobrevive ao JSON local, ao documento do Firestore e a saves antigos carregados por `save_load`. O boss do Thrasybios (beat I.8) só abre com o ramo decidido.
  - **Cena (beat I.7):** ver o Cerâmico em **Mapas**. `damaris_offer` termina na escolha "Aceitar a oferta" ou "Recusar", e `damaris_technique` narra a habilidade do ramo depois que ela vai embora.
- **Tribunal:** `TribunalScript` guarda as regras puras. Damaris trai no turno 3 se aceitou e defende se recusou. A ação final escolhe o desfecho: REFUTAR leva ao Sofista (340 absolvições), CONCEDER ao Filósofo (261 condenações) e MAIÊUTICA à Mestra (281). Menexeno vota pela absolvição em todos os finais, exceto o do Filósofo; na Mestra o GDD não define o voto dele.

---

## Fluxo: obtenção de argumento

O turno é síncrono (`IArgumentSource.TryNext`); a rede nunca entra nele. Montagem em `Game.Net`:

```csharp
var transport = new UnityWebRequestTransport();
var endpoints = FirebaseEndpoints.Production(webApiKey, projectId);
var auth = new FirebaseAnonymousAuth(transport, endpoints, new FileAuthSessionStore(Application.persistentDataPath));
var client = new ArgumentServiceClient(transport, auth, endpoints.ArgumentUrl, validator);
var source = new PrewarmedArgumentSource(client, diskCache, validator, new FallbackArgumentLibrary(fallbackLibrary, validator, seed), seed);

source.Register(opponent.Id, OpponentContext.From(opponentSO, topic));
await source.PrewarmAsync(opponent.Id, count, destroyCancellationToken);
```

- `ArgumentServiceClient.FetchAsync` nunca lança: devolve `ArgumentFetch` com `Delivered`, `Declined` (204), `Invalid`, `Unavailable` ou `Cancelled`. Timeout de 2 s, 1 retry com backoff para timeout, erro de rede, 429 e 5xx; 401 força renovar o ID token antes do retry; 204 e payload inválido não reintentam.
- `PrewarmedArgumentSource.PrewarmAsync` consulta o `ArgumentDiskCache` pela mesma chave do servidor, busca o que falta, grava no cache e enfileira. Para no primeiro `Unavailable`, `Declined` ou `Cancelled`. `TryNext` serve a fila e, vazia, cai no `FallbackArgumentLibrary` — silencioso para o jogador.
- A seed do pedido fica em `0..SeedVariants-1` (8) para o cache compartilhado do servidor ter acerto alto.
- `FirebaseAnonymousAuth` usa a REST do Identity Toolkit (`accounts:signUp`) e do Secure Token (`grant_type=refresh_token`), guarda uid e refresh token em `auth.json`, renova o ID token 5 min antes de expirar, cria conta nova se o refresh for recusado (4xx) e devolve `null` offline. `FirebaseEndpoints` recusa chave começando com `sk-`; `FirebaseEndpoints.Emulator("demo-gft")` aponta para os emuladores locais.
- Testes EditMode usam `IHttpTransport` e `IIdTokenProvider` falsos que completam na hora, então as `Task` terminam síncronas.
- **Modo avião ([QA-02]):** `Assets/_Game/Tests/Support` (assembly `Game.Tests.Support`) tem `FlakyNetwork`, `FlakyIdTokenProvider`, `FakeFirebase` e `LoopbackHttpServer`.
  - A rede cai com timeout, conexão recusada, 5xx, pedido travado ou host que não resolve, e volta com `Reconnect`.
  - No `Unresolvable`, o pedido sai pelo `UnityWebRequestTransport` de verdade para `https://gft-airplane-mode.invalid`, e o DNS da máquina falha de verdade. O `.invalid` é reservado e nunca resolve. Tem de ser HTTPS: fora de `127.0.0.1`, a Unity recusa HTTP antes do DNS e registra um aviso, que o `LogAssert.NoUnexpectedReceived` reprova.
  - `LoopbackHttpServer` atende o `FakeFirebase.ServedAt` em `127.0.0.1` por socket de verdade, então o `UnityWebRequestTransport` também entra no teste. Ele responde, trava (`Hang`) ou recusa conexão (`GoDown`), e as respostas saem na thread principal.
  - `AirplaneModeTests` (EditMode) e `AirplaneModePlayTests` (PlayMode) usam essas classes.
  - `AirplaneModePlaythroughPlayTests` joga, com a rede caída desde o menu, tudo o que existe: novo jogo, Cerâmico, Ágora, todas as conversas, os seis duelos com todas as ações, a pressão sobre a oficina, a oferta de Damaris, a ausência do Menexeno, um duelo com a habilidade ganha, o julgamento de rua perdido e depois vencido, o arquivo com a lista e o selo, a Academia, a pausa e o Continuar. Cada queda de rede responde a Damaris de um jeito (`Hanging` recusa, as outras aceitam), então os dois ramos rodam offline. Ao ganhar conteúdo novo, estenda esse teste.
  - Os testes rodam no Editor e não fecham o card sozinhos. O roteiro manual na build de Windows fica em `docs/qa/airplane-mode.md`, e cada rodada é anotada na tabela de registro dele.

**Pré-aquecimento:** ao entrar num mapa, chame `PrewarmAsync` para os adversários presentes. Quando o jogador chegar perto, os argumentos já estão na fila e a latência é zero.

**Boot:** `Game.UI.GameServices` é quem monta tudo isso numa cena.
- **No `Start`:** carrega o `ArgumentDiskCache` de `persistentDataPath/argument_cache` e cria o `LocalSaveStore`. Se `NetworkConfigSO.IsConfigured`, faz sign-in anônimo e roda `SaveSynchronizer.ReconcileAsync`.
- **Orçamento do boot:** o sign-in e a reconciliação têm `OnlineBootBudgetSeconds` (2 s). Passado o prazo, são cancelados, e o `IsBooted` libera o jogo com o save local, sem que o menu ou o mapa esperem timeout.
- **Backend:** vem de `OnlineBackend.FromConfig(network)` (transporte, endpoints do Firebase e do Firestore, espera de backoff). `OnlineBackendOverride.Set` troca o backend de todo `GameServices` que iniciar depois. É a costura dos testes de modo avião, e o teardown chama `Clear`.
- **Salvamento do cache:** em pause, quit e destroy, e depois de cada `GameServices.PrewarmAsync`.
- **Fonte de argumentos:** `CreateArgumentSource(seed)` devolve um `PrewarmedArgumentSource`. Sem configuração de rede, ele fica sem cliente (`IsOnline == false`) e serve só o fallback.
- **Configuração de rede:** fica em `Assets/_Game/Data/NetworkConfig.asset`, que `SandboxTools.BuildSandbox` cria vazio só se não existir. A chave web do Firebase não é segredo; a da OpenAI nunca entra ali. `useEmulator` aponta para os emuladores locais.

**Validação é do servidor.** A Cloud Function valida o payload antes de responder:

```json
{
  "speech": "Todo homem justo é temido pelos injustos. Sócrates era temido. Logo, Sócrates era justo.",
  "fallacy_id": "affirming_the_consequent",
  "premises": ["Todo justo é temido pelos injustos", "Sócrates era temido"],
  "weak_premise": 1,
  "tone": "arrogant"
}
```

`fallacy_id` é uma falácia refutável do catálogo (ou `null` num paradoxo), `weak_premise` é índice válido de `premises`, `speech` tem entre 40 e 300 caracteres e `tone` pertence a `ArgumentTones`. Chaves e valores de enum em inglês; `speech` e `premises` em português, porque são texto de jogo. O schema formal está em `docs/argument.schema.json`, e um teste EditMode garante que ele bate com o catálogo. O cliente revalida com `ArgumentValidator` por garantia, mas a fronteira de confiança é o servidor.

### Backend (`functions/`)

Firebase Functions v2 em TypeScript, região `southamerica-east1`, função HTTP `generateArgument`. A ordem dentro de `handleGenerateArgument` é o contrato: `POST` → ID token (401) → corpo (400) → kill switch `config/runtime.llm_enabled` (204) → rate limit por UID/hora em `rate_limits` (429) → `argument_cache/{hash}` (200, `X-Cache: hit`) → OpenAI com `json_schema` estrito e teto de tokens → validação → lista de bloqueio e moderação → grava cache (200, `X-Cache: miss`). `fallacy_id` inesperado tenta mais uma vez; qualquer outra falha vira 204 e o cliente usa o fallback.

- `handler.ts` não importa Firebase nem OpenAI: tudo entra por `Dependencies`, e os testes unitários usam dublês. Os adaptadores reais ficam em `firestoreAdapters.ts` e `openaiAdapters.ts`.
- A chave de cache é `sha256("opponent|fallacy|seed")` cortada em 32 hex, a mesma do `ArgumentDiskCache`.
- `src/generated/content.json` é gerado por `npm run sync-content` a partir de `fallacies.json`, do banco de fallback e de `docs/argument.schema.json`; um teste falha se estiver desatualizado. Os few-shot são os 2 primeiros argumentos de fallback de cada entrada.
- O schema enviado à OpenAI passa por `toStrictSchema`, que remove `minLength`, `minimum` e afins; os limites continuam valendo na validação do servidor.
- `OPENAI_API_KEY` é `defineSecret` (Secret Manager). Os demais parâmetros (`OPENAI_MODEL`, `MAX_COMPLETION_TOKENS`, `RATE_LIMIT_PER_HOUR`, `CACHE_TTL_DAYS`, `BLOCKED_TERMS`, preços por milhão de tokens) são `defineString`/`defineInt`. `usage/{yyyy-mm-dd}` agrega chamadas, cache hits, tokens e custo.
- `firestore.rules` libera só `saves/{uid}` e `progress/{uid}/duels/*` ao dono; `argument_cache`, `rate_limits`, `usage` e `config` são exclusivos do servidor. TTL de `expires_at` declarado em `firestore.indexes.json`.
- O projeto padrão é `demo-gft`, que só existe nos emuladores.

```powershell
cd functions
npm ci
npm run build
npm test
npm run test:emulators
npm run usage-report -- 2026-09-01 2026-09-15
```

- **`test:emulators`:** precisa de Java e sobe Firestore e Auth via `firebase emulators:exec`. No Windows, o CLI deixa o `java.exe` do emulador do Firestore órfão, segurando a porta 8080. O `scripts/run-emulator-tests.mjs` encerra, ao final, apenas os emuladores iniciados por aquela execução.
- **`usage-report`:** imprime o dashboard de `usage/{yyyy-mm-dd}`: chamadas por dia, chamadas à OpenAI, cache hits, taxa de acerto e custo diário e acumulado. O padrão são os últimos 30 dias. Contra produção, precisa de credenciais do Google e de `GCLOUD_PROJECT`. Com `FIRESTORE_EMULATOR_HOST`, lê o emulador. A taxa de acerto é `cache_hits / requests`, e `requests` conta cada chamada à OpenAI (inclusive o retry) mais cada hit, então ela subestima levemente o acerto real.

---

## Persistência

Offline-first, sempre. O jogador nunca espera Firestore.

```csharp
public enum DamarisChoice { Undecided, Accepted, Refused }

public sealed record SaveData(
    Act ActFlag,
    byte[] FogBitmask,
    DamarisChoice DamarisBranch,
    int Reputation,
    Coord Position,
    Dictionary<string, DuelResult> Duels,
    long Timestamp);
```

Escrita local primeiro, sempre por `SaveSynchronizer`:

- `SaveLocal` grava o arquivo com `LocalSaveStore` e marca push pendente, sem tocar a rede.
- `PushPendingAsync` envia em background com `FirestoreSaveClient`: um `:commit` REST com `saves/{uid}` e `progress/{uid}/duels/{id}`, autenticado pelo ID token. São até 3 tentativas com backoff de 2 s e 4 s, e 401 força renovar o token. Se um save mais novo chegar durante o envio, ele continua pendente.
- `ReconcileAsync` roda no boot e o `Timestamp` mais recente vence: remoto mais novo sobrescreve o arquivo, local mais novo é enviado, e offline mantém o local como pendente.
- `FirestoreDocuments` converte o JSON de `SaveJson` em valores tipados do Firestore (`integerValue` como string, `mapValue`, `arrayValue`). O corpo de commit de referência fica em `functions/test/fixtures/save_commit.json`: o teste EditMode compara com ele e `rest-contract.emulator.test.ts` o envia ao emulador, junto com os corpos de signUp e refresh da auth. Mudou o formato, atualize o fixture e rode as duas suítes.

Regras em `firestore.rules`: só o dono lê e escreve `saves/{uid}` e `progress/{uid}/duels/*`. `DamarisBranch` é lido no Tribunal do Ato II.

`ActFlag` só muda por efeito de diálogo (`{ "flag": "act", "value": "2" }`, hoje só no selo de bronze do arquivo). O `ExplorationSaveMapper` o preserva de save em save, e `ActGate` (Core) é a regra única de "o Ato II já começou".

**Slots.** São 3 slots locais (`saves/slot0.json` a `slot2.json`, `SaveSlots`).
- **Catálogo:** `SaveSlotCatalog` resume cada slot como vazio, ilegível ou usável, com ato, `Location` e `PlaytimeSeconds`, e guarda o slot ativo em `saves/active_slot.txt` para a escolha sobreviver à troca de cena.
- **GameServices:** `SelectSlot`, `SaveToSlot`, `StartNewGame` (grava um save vazio no slot escolhido) e `TryLoadActiveSave`. O tempo de jogo só avança com `Time.timeScale` > 0.
- **Nuvem:** só o slot 0 sincroniza com `saves/{uid}`; os outros são apenas locais.
- **Campos novos:** `location` e `playtimeSeconds` entraram no JSON e no documento do Firestore. Saves antigos sem eles continuam válidos, e `playtimeSeconds` negativo é rejeitado.
- **Compêndio:** as falácias desbloqueadas ficam em `SaveData.Compendium`.
  - No JSON é `compendium`, uma lista de ids em ordem ordinal, gravada só quando não está vazia. Por isso o fixture do commit não muda. No Firestore vira `arrayValue` de `stringValue`.
  - Saves antigos sem o campo continuam válidos, e id em branco é rejeitado.
  - `GameServices.Compendium` é o `FallacyCompendium` da sessão, com os ids do catálogo. O boot, `SelectSlot` e `SaveToSlot` o restauram do save do slot, junto com o tempo de jogo, e `StartNewGame` o esvazia. Depois do `ReconcileAsync`, as falácias do save remoto são somadas.
  - `GridSandboxController.CaptureSave` passa as desbloqueadas para `ExplorationSaveMapper.ToSave`, que soma com as do save anterior.
- **Restauração:** `ExplorationRestore` só aplica o save quando o `Location` bate com o mapa atual. Cada mapa usa o `GridSandboxController.LocationId` gravado na própria cena (`agora`, `kerameikos`). Se a célula salva estiver ocupada, como a de partida de um oponente que andou na batalha, a Kleia volta na célula livre mais próxima (`GridWalker.TryFindFreeCellNear`). Célula fora do mapa ou bloqueada não restaura nada.

O formato de `FogOfWar.ToBitmask` virou schema de banco. Mudá-lo invalida saves de playtest — se precisar, escreva migração.

---

## Dados

Conteúdo é ScriptableObject em `Assets/_Game/Data/`, nunca código. Se você está escrevendo `if (opponent.DisplayName == "Thrasybios")`, pare e mova para o SO.

| SO | Campos principais |
|---|---|
| `FallacySO` | `id`, `displayName`, `description`, `playerHint`, `icon`, `category` (`Informal` / `Formal` / `Paradox`) |
| `FallacyCatalogSO` | `Dictionary<string, FallacySO>` por id |
| `OpponentSO` | `displayName`, `sheet` (e `sheetId`, quando a folha é de outro personagem), `maxCoherence`, `initialAudience`, `fallacyPool`, `act`, `profile`, `tutorial`, `phases`, `lines` |
| `ArgumentSO` | espelha o JSON do argumento |
| `FallbackLibrarySO` | `Dictionary<string, TextAsset>`: id do catálogo → JSON com os argumentos escritos à mão |
| `TerrainCostTableSO` | `Dictionary<TerrainId, byte>` |
| `AbilitySO` | `Inversion` e `Listening` (ramificação Damaris) |
| `QuoteItemSO` | `displayName`, `effectDescription`, `coherenceDelta`, `audienceDelta`, `uses` |
| `AudioLibrarySO` | `Dictionary<string, AudioClip>` por id e `Dictionary<string, float>` com o tom da voz de cada falante |
| `DialogueLibrarySO` | `Dictionary<string, DialogueSpeaker>` (nome e retrato) e `Dictionary<string, DialogueEntry>` (linhas com condições e efeitos) por id |

Tabelas por id são `Dictionary` serializado com `[SerializeField]`, nativo no 6.6 — nada de listas paralelas. O analyzer de serialização (UAC1xxx) roda na compilação e não pode emitir aviso.

Texto exibido ao jogador (nomes, falas, descrições, dicas) é português e vive só nesses dados.

**Sem Editor, SOs são criados por script.** `Game.EditorTools.DataTools.Regenerate` lê `Assets/_Game/Data/Source/*.json` e `Assets/_Game/Data/Fallback/*.json` e (re)cria catálogo, ícones, tabela de custos e biblioteca de fallback. O conteúdo fica legível em diff, a regeneração é idempotente e falha com código diferente de zero se algum argumento de fallback não passar no `ArgumentValidator`.

**Mapas.** O layout de cada mapa fica em `Assets/_Game/Data/Maps/<id>.json` e é lido por `MapLayout`.
- **Tamanho:** vem do próprio JSON (largura da linha e número de linhas); `SandboxTools` monta grid, câmera e captura a partir dele. A Ágora tem 20×20 e o Cerâmico 48×48.
- **Chão e blocos:** `floor` e `blocks` são linhas de texto, e `floor[y][x]` é a célula `(x, y)`. A linha 0 é a borda da frente, a mais próxima da câmera.
- **Névoa inicial:** `fogRevealDepth` opcional revela de saída as células com `x + y` menor que ele (padrão `SandboxTools.FogRevealDepth`). No Cerâmico é 0: a oficina se revela conforme a Kleia anda.
- **Névoa no render:** o `GridModel` decide, e o render só lê `TileData.Revealed` (`FogTileRules`).
  - O tilemap `Fog` fica na camada `Default` com ordem `SandboxTools.FogSortingOrder` (-5): acima do chão, do véu do chão, do `Range` e do cursor, e abaixo de blocos e atores. Assim, bloco ou ator de célula revelada nunca é cortado pela névoa das células de trás.
  - Célula não revelada não mostra nada do que está nela. O bloco sai dos `Blocks`: a fonte fica em `GridSandboxController.BlockTiles` (gerado por `SandboxTools`), e o tilemap só recebe o bloco quando a célula é revelada. As faces e a seta de saída seguem a mesma regra (`FaceTiles` e `Exits`). O ator some por `ActorView.IsShown` (o `SpriteRenderer`), pela célula sob o pivô (`FogTileRules.ShowsWhatStandsAt`), então aparece e some no meio do passo. Os espectadores do `BattleCrowd` seguem a mesma regra.
  - A borda pontilhada da névoa é só do chão: a célula da borda ainda não foi revelada, então o bloco e o ator dela também ficam escondidos. Um bloco alto meio pontilhado flutuaria no escuro como antes, e ainda entregaria quem está ali.
  - `PaintFogPreview` aplica a mesma regra na cena gerada, e é isso que `CaptureReference` fotografa.
- **Legendas:** `floorLegend` e `blockLegend` levam cada símbolo ao nome do tile, e `-` marca a célula sem bloco.
- **Alturas:** `heights` opcional são linhas de dígitos paralelas a `floor`, com o nível de cada célula. Sem `heights`, o mapa é plano.
  - `MapLayout.Validate` recusa linhas de outro tamanho e dígito fora de 0 a `TerrainSorting.MaxLevel`.
  - `MapLayout.CanStep` é a regra do degrau nos testes de alcance e de "ao lado" dos mapas.
- **Espessura e penhascos:** toda célula pendura faces até o nível do vizinho da frente, e na borda da frente do mapa até `-SandboxTools.BaseDepthLevels` (2 níveis, 16 px), então o chão lê como uma laje e não como uma folha.
  - O vizinho SW é `x − 1` e o SE é `y − 1`. `SandboxTools.FaceSegmentsOf` gera um segmento de um nível (8 px) por nível descoberto, no tilemap `Faces` daquele nível.
  - O segmento de cima leva o friso (`lip`) e os outros, o corpo (`body`): `side_<earth|stone>_<lip|body>_<sw|se|sw_se>`.
  - A terra fica sob terra, terra batida, argila, palha e grama; a pedra, sob mármore, mármore ocre, mosaico, pedra e água (`SandboxTools.SideMaterialFor`).
  - O sprite de face tem 32×32 e pendura 16 px abaixo da célula, então o tilemap `Faces` desce `FaceSpriteDrop` (1 unidade) e compensa em Z.
  - Os limites da câmera (`MapOutline`) e a captura de referência incluem a base embaixo e o nível mais alto em cima.
- **Atores:** `actors` lista personagem, célula e `Facing`, e o primeiro é o jogador. O campo opcional `dialogue` dá ao NPC um id de `dialogues.json`, e `conditions` o faz esperar flags da história. O jogador não pode ter nenhum dos dois, e um teste garante que todo id citado nos mapas (atores, gatilhos e deixas) existe.
  - `sheet` opcional veste o ator com a folha de outro personagem (`MapActor.SheetId`), e o `character` continua sendo o id dele no elenco e nas cenas. É assim que o julgamento de rua usa a folha do Thrasybios, e o arquivista e o acadêmico usam a do `citizen`.
- **Blocos condicionais e casa:** `conditionalBlocks` mostra ou esconde blocos pelas flags da história, e `"home": true` marca o mapa onde a volta passa um dia (ver **Diálogo na exploração**).
- **Ato do mapa:** `act` opcional (padrão 1) diz a que ato o mapa pertence. Toda saída para um mapa do Ato II espera `ActFlag = 2` (ver **Portão** em **Saídas**).
- **Validação:** `MapLayout.Validate` recusa linhas de larguras diferentes, símbolo desconhecido, ator em bloco, em água ou em célula repetida, condição inválida e deixa sem diálogo ou sem flag própria.
- **GridSandbox:** a cena é montada a partir de `agora.json`, a Ágora do beat I.2, em quatro níveis. Um teste garante que toda célula andável é alcançável a partir da Kleia, degrau a degrau.
  - **Nível 0:** a estrada de terra que vem do Cerâmico, com a saída na ponta, e o mercado de pedra onde Thrasybios junta a plateia.
  - **Nível 1:** a praça em xadrez com borda de meandro, um degrau acima da estrada e do mercado, com a herma no canto. A fonte é um tanque rebaixado ao nível 0, com as paredes de pedra de trás à mostra.
  - **Stoa:** sobe em degraus da praça. A colunata fica no nível 2 e o passeio de trás, no 3. Diante do mercado, a plataforma cai dois níveis num muro de pedra.
  - **Morro das oliveiras:** a borda do caminho está no nível 2, com um muro de terra de dois níveis sobre a estrada, e o topo no 3. Sobe-se pela praça, e o topo cai dois níveis sobre o fundo da praça.
  - **Rua das casas de tijolo:** fica no nível 2, um degrau acima da praça.
  - **Duelistas de bairro:** Óron na ponta da stoa, junto à parede de mármore e ainda sob a névoa (10, 10), Phaidra no morro de oliveiras (1, 10), Kritolaos na rua de pedra diante das casas (6, 10), Simo na praça em xadrez (8, 8) e Bátilo junto à fonte (7, 6). Testes garantem que a Kleia consegue parar ao lado de cada desafiante e de cada NPC que conversa, e que nenhum ator bloqueia o único caminho para uma célula além do recanto logo atrás dele.
  - **A cobrança (beat I.6):** três deixas `Idle`, uma por estágio da pressão. Com `pressure_rumor`, Thrasybios sobe no caixote e acusa a Kleia de asebeia (`agora_asebeia_rumor`, flag `heard_asebeia`). Com `pressure_deserted`, a ágora repete a palavra sem ele (`agora_asebeia_spreads`, `saw_rumor_spread`). Com `pressure_tools`, a MAIÊUTICA aprendida e a oferta de Damaris em aberto, a Kleia decide voltar para ver o pai (`agora_pressure_home`, `worried_for_home`), que é onde Damaris espera.
  - O Menexeno e o Eudoro trocam de fala com o boato. O Menexeno ouve "asebeia" e lembra de um tribunal, sem dizer qual.
  - **Menexeno ausente:** o Menexeno da Ágora tem a condição `menexeno_absent_days` ausente, então some enquanto dura a ausência que a oferta aceita grava (três voltas para casa). Nesse tempo, `agora_menexeno_away` (`noticed_menexeno_gone`) aponta o lugar vazio junto à stoa. Ao voltar, ele comenta a bolsa; se a Kleia recusou, ele diz que soube (`menexeno_respect`).
  - **Julgamento de rua (beats I.8 a I.10):** tudo fica em blocos próprios no fim de `actors`, `exits` e `cues` da Ágora.
    - A deixa `Idle` `agora_street_trial_summons` espera `duel_thrasybios=won`, `maieutics_unlocked` e `damaris_branch` decidida (`not: undecided`, o contrato com a cena da Damaris do AT1-07) e grava `street_trial_called`.
    - Com essa flag, o Thrasybios do mercado sai (`street_trial_called` ausente) e o `thrasybios_street_trial` aparece em (10, 8), entre as colunas da stoa, no nível 2. Kleia o desafia da praça, um degrau abaixo, ou do passeio de trás, e ali não fica encurralada. Ele fica, entre as colunas, e perde Coerência a cada fala, mas nunca abaixo do piso da fase.
    - Menexeno fica na stoa enquanto `duel_thrasybios_street_trial` não é `won`. Vencer tira os dois do mapa ao voltar à exploração, e a deixa `agora_menexeno_gone` grava `searching_menexeno`.
    - `searching_menexeno` abre a porta do arquivo em (11, 4), bem atrás do lugar do Menexeno. A estrada da Academia sai em (7, 11), entre as casas de tijolo, e a deixa `agora_academy_road` a anuncia no Ato II (`saw_academy_road`).
- **Cerâmico:** `kerameikos.json` (48×48) gera `Assets/_Game/Scenes/Kerameikos.unity`, o bairro dos oleiros dos beats I.1, I.4, I.6 e I.7. É a casa (`"home": true`): chegar pela rua passa um dia.
  - **Gerador.** O JSON sai de `python tools/maps/kerameikos.py`, que pinta chão, blocos e alturas por retângulos, deriva os gatilhos de cena das células livres ao lado do Menexeno e da Damaris e grava também a chegada no `agora.json`. Ele **recusa gravar** quando algo não fecha: célula andável ilhada, ator sem vizinho livre, ator que tranca a única passagem, saída fora da rua ou da borda, gatilho em célula bloqueada, Damaris longe da chegada e os dois estrangulamentos da história — a porta do Menexeno e o canto da roda. O JSON continua sendo o que o jogo lê; o script é a ferramenta de autoria, como os de `tools/art/`.
  - **A oficina não mudou.** O quadrante x 0–15, y 0–15 é o mesmo quarteirão de antes: terra batida cercada de tijolo com uma única porta em (4, 7), a roda ao lado da qual a Kleia começa, Aristeu junto às ânforas do fundo, o pátio de argila seca com o forno e o canto de palha com oliveiras. Aristeu (folha placeholder `aristeu`) a manda levar a mercadoria à Ágora (`aristeu_workshop`, flag `aristeu_errand`), com o retrato dedicado `aristeu_portrait`, de mão erguida ao lado do rosto, dedos tortos e nós inchados. Toda célula que a história cita continua onde estava.
  - **O bairro em volta é novo.** São 2304 células, 54% andáveis e 924 blocos — contra 87% e 40 blocos da Ágora, que é por onde ela parecia vazia. Ruas de duas células cortam quarteirões cegos de tijolo em três alturas, com olarias abertas para a rua, o Eridanos correndo pela borda oeste com margem de grama, a necrópole em terraço de pedra atrás de recintos de mármore e o morro de oliveiras acima dela.
  - **Relevo.** A rua fica no nível 0 e a oficina inteira um degrau acima. Os quarteirões sobem a 2 e as torres a 3. A necrópole é um terraço de nível 2 que só se alcança pela rampa de (8–13, 30–31): o resto da borda dela cai dois níveis sobre a rua do vau e é penhasco. O morro tem o topo no nível 3.
  - **A rua da Ágora sai em (16, 0) e (17, 0)**, na ponta da rua da frente, a 19 passos da porta da oficina, e a chegada é (16, 2) — a ida e a volta continuam curtas. O bairro se explora pela alameda que sobe em x 16–17; a célula mais funda fica a 98 passos da porta.
  - Tutorial por deixas: `kerameikos_first_steps` ao abrir o jogo, `kerameikos_tutorial_fog` no primeiro passo que revela névoa e `kerameikos_tutorial_talk` no fim da caminhada seguinte, cada um com a própria flag `tutorial_*`. Sair pela porta toca `kerameikos_leaving_workshop` (`left_workshop`).
  - Menexeno (beat I.4) só aparece em (3, 6), diante da porta, com `duel_thrasybios=won` e sem `maieutics_unlocked`. Ele não tem `dialogue`: pisar numa das três células livres ao lado dele toca a cena de Timeline `menexeno_follows_home` (flag `menexeno_door_scene`).
  - A cena vira os dois um para o outro e toca `menexeno_door_offer` (a condição de nunca cobrar por uma pergunta, `menexeno_condition`). Ele olha para a rua e `menexeno_door_glance` planta a pista do passado (`menexeno_avoids_past`). Depois vem `menexeno_door_lesson`, ele desce a rua e sai do mapa, e `menexeno_door_learned` grava `maieutics_unlocked`, que libera a MAIÊUTICA.
  - **A cobrança (beat I.6):** dois fregueses (`citizen`, `customer_oil` e `customer_crater`) examinam as ânforas da porta em (5, 5) e (6, 5) enquanto falta `pressure_deserted`. Com o boato, eles hesitam; depois somem, e a deixa `Idle` `kerameikos_no_customers` (`saw_empty_workshop`) aponta a porta vazia.
    - Blocos condicionais: uma pilha de ânforas encalhadas aparece em (7, 6) com `pressure_deserted`, e a roda de oleiro em (3, 11) some com `pressure_tools`, liberando a célula. O gatilho `kerameikos_wheel_sold` (`saw_wheel_sold`), logo depois da porta, em (4, 8), mostra o canto vazio.
    - O Aristeu muda a cada estágio: o freguês que perguntou do boato, a oficina sem ninguém e os pincéis finos já vendidos ("a roda vai atrás"), a roda vendida e, depois da oferta, as dracmas que ele não quer usar ou a recusa que ele aprova. O vizinho da rua também se afasta e comenta a roda.
  - **Damaris (beat I.7):** espera na rua em (16, 4), a dois passos da chegada da Ágora, com `pressure_tools`, `maieutics_unlocked` e `damaris_branch=undecided`. Como o Menexeno da porta, ela não tem `dialogue`: pisar numa das quatro células ao lado dela toca a cena `damaris_seeks_kleia` (flag `damaris_offer_scene`).
    - A cena vira as duas uma para a outra e toca `damaris_offer`: ela é de Abdera, viu a roda descer a rua e oferece proteção, 200 dracmas e uma técnica, em troca de a Kleia argumentar por ela quando for chamada. A escolha grava `damaris_branch` por `DamarisOffer.Apply`.
    - Depois ela desce a rua, sai do mapa, e `damaris_technique` narra a INVERSÃO ou a ESCUTA. Com o ramo decidido, ela não volta.
  - Não há batalha: `SandboxTools` só cria o `SandboxBattleDirector` quando o mapa tem oponente.
- **Saídas:** `exits` no JSON do mapa liga uma célula a outro local, com célula de chegada e `Facing`.
  - `MapLayout.Validate` recusa saída fora do mapa, em célula bloqueada, sem destino ou repetida, e um teste garante que a chegada cai em célula livre, sem ator e sem outra saída — senão o jogador quicaria de volta.
  - **Seta:** toda saída mostra a seta dourada animada `overlay_exit_<ne|nw|se|sw>` (tile animado de 4 quadros que pulsa para a ponta), no tilemap `Exits` do nível da célula, entre o chão e o véu. Ela aponta para fora da borda em que a saída está (`MapLayout.HeadingOf`: x = 0 é SW, x máximo é NE, y = 0 é SE, y máximo é NW) ou para o `heading` do JSON. Sob a névoa, a seta não aparece.
  - **Nome:** `label` no JSON é o texto do jogador ("Para o Cerâmico", "Para a Ágora"). Com o cursor numa saída revelada e a exploração livre, o `ExitLabelView` mostra o nome num canvas próprio acima da seta (`GridSandboxController.HoveredExit`). O rótulo não recebe raycast, então nunca engole o clique no mapa. Label em branco é recusado.
  - Pisar na saída e parar dispara a troca: `SceneArrival` guarda a chegada, `LocationScenes.SceneFor` resolve a cena e o `GridSandboxController` da outra ponta teleporta o jogador no `Awake`, revela a névoa em volta e faz save automático.
  - A chegada pula a restauração do save, porque a posição dela vale mais que a gravada.
  - **Portão:** `conditions` numa saída, no mesmo formato das linhas, e o `act` do mapa de destino (`SandboxTools.ActOf`) viram `MapExitLink.Conditions` e `RequiredAct`. `ActGate.IsOpen` (Core) decide.
    - Fechada, a saída não tem seta nem nome, não leva a lugar nenhum e a célula fica intransponível e sob a névoa: `CellGate.Close` (Grid) segura a célula no `FogOfWar` (`Hold`), então nem o passo ao lado nem um save antigo a revelam.
    - O `GridSandboxController` fecha todo portão no `Awake` e o `ApplyStory` abre os que a história libera, com `CellGate.Open`: a célula volta ao custo do mapa e sai da névoa.
  - **Névoa:** o save guarda a névoa de cada mapa em `SaveData.FogByLocation` (`fogByLocation` no JSON, só gravado quando existe). Ao sair, a névoa do mapa de origem viaja no `PendingArrival` e entra no autosave da chegada; ao chegar, a névoa salva do destino é somada à revelada em volta da chegada (`ExplorationRestore.TryMergeFog`). `FogBitmask` continua sendo a do mapa atual, e saves antigos migram a névoa do `Location` gravado.
- **Arquivo (beats I.9 e I.10):** `archive.json` (8×8) gera `Assets/_Game/Scenes/Archive.unity`, o Metrôon, onde a cidade guarda leis e sentenças.
  - A Kleia chega pela porta da frente (saída para a Ágora) e a deixa `archive_entrance` descreve o lugar. O arquivista (`archive_archivist`, flag `asked_archivist`) conta que Menexeno leu a lista do Conselho do ano dos Trinta.
  - A mesa com a lista fica entre jarros, no fundo. Pisar diante dela toca a cena de Timeline `archive_list_scene` (flag `read_juror_list`).
  - A cena mostra `archive_juror_list`: o processo contra Sócrates sob os Trinta, por recusar a ordem de prender Leão de Salamina, com a morte pedida. A tirania caiu antes da sentença, e Menexeno está na coluna da condenação (`knows_menexeno_vote`). Assim a revelação não contradiz Sócrates vivo em 402 a.C.
  - O acadêmico que lia junto às colunas (só enquanto falta `bronze_seal`) anda até ela e entrega o selo de bronze (`archive_bronze_seal`, que grava `bronze_seal` e `act = 2`). Depois sai do mapa, e `archive_act_two` fecha o Ato I.
- **Academia:** `academy.json` (8×8, `act: 2`) gera `Assets/_Game/Scenes/Academy.unity`. É um esboço, só para a estrada ter destino: jardim com oliveiras e colunas dóricas no lugar do pórtico jônico, a deixa `academy_threshold` e a saída de volta. O mapa de verdade é o card AT2-01.
- **Menu:** Novo jogo começa em `LocationScenes.StartScene` (a oficina do Cerâmico) e Continuar abre a cena do `Location` gravado no save; save antigo de `sandbox` abre a Ágora.
- **Uma cena por mapa:** `SandboxTools.Maps` lista local, mapa e cena.
  - `BuildSandbox` monta todas as cenas, e `CaptureReference` fotografa a Ágora, o Cerâmico, o arquivo e a Academia (`artifacts/reference/grid_sandbox.png`, `kerameikos.png`, `archive.png` e `academy.png`).
  - Cada cena grava o próprio `GridSandboxController.LocationId` (`agora`, `kerameikos`, `archive`, `academy`). Esse id vai para o save e decide se `ExplorationRestore` aplica posição e névoa.
  - Os tiles do Cerâmico (`floor_packed_dirt`, `floor_dry_clay`, `floor_straw`, `block_kiln`, `prop_potters_wheel`, `prop_amphora_stack`) ficam em `art/placeholders/tiles`, como todo o cenário estático P1 dos dois mapas: arte do PixelLab, já na palette v2 (ver a skill `pixel-art-pipeline`).

---

## Pipeline de arte sem Editor

Resumo; o detalhe está na skill `pixel-art-pipeline`.

- A fonte da arte fica em `art/`. `Game.EditorTools.ArtTools.SliceSheets` copia `art/placeholders/` para `Assets/_Game/Art/Placeholder/`, força a reimportação, fatia as folhas e gera clipes e Animator Controllers. Idempotente.
- `PixelArtImporter` (`AssetPostprocessor`, porque Preset Manager exige Inspector) vale para tudo sob `/_Game/Art/`: Sprite, Point, sem compressão, PPU 16, sem mipmap, Full Rect. O modo é **sempre explícito** — `Multiple` só para `*_sheet.png`, `Single` para o resto —, porque o template 2D importa sprite novo como `Multiple` e aí o pivot definido no importer é ignorado. Tudo sob `/tiles/` recebe pivot `BottomLeft`.
- Retratos dedicados ficam em `art/placeholders/portraits/<personagem>_portrait.png`, 32×32 (o miolo da moldura de 40×40), importados como sprite `Single`. Um teste confere tamanho, paleta e o limite de 15 cores.
- Fundos de tela (`bg_`) têm o quadro nativo de 384×216 e até 64 cores da paleta, como uma camada de fundo do SNES com várias subpaletas; o resto das regras vale igual. O único hoje é `bg_title.png`, a tela de título, gerada no PixelLab, com os sete quadros da animação ao lado.
- Folhas 96×128 fatiadas em células 24×32 (`<personagem>_<se|sw|ne|nw>_<coluna>`) com pivot `(0.5, 0.0625)` por `ISpriteEditorDataProvider`, preservando o `spriteID` de cada célula entre regenerações.
- Um Animator Controller por personagem em `Assets/_Game/Art/Placeholder/Animations/`, com um estado por animação: `idle`, `walk` e `action` sempre, mais uma para cada tira `<personagem>_<animação>_<se|ne>.png` que existir em `art/placeholders/characters` (`ArtTools.StatesFor`). Cada estado é um blend tree 2D por `dirX`/`dirY` — quatro direções quando vem da folha, duas quando vem de tiras, porque SW e NW são o espelho. Os clipes só trocam sprite (curva de referência de objeto, sem interpolação); curvas numéricas, quando existirem, usam tangentes `Constant`.
- `Game.EditorTools.RenderSetupTools.Apply` gera `Assets/_Game/Prefabs/Camera/PixelPerfectCameraRig.prefab` (URP `PixelPerfectCamera`, referência 224×224, PPU 16, *Upscale Render Texture*, *Crop Frame* `None`) e põe o `Renderer2D.asset` em Transparency Sort *Custom Axis* (0, 1, -1). *Crop Frame* `Windowbox` recorta nos dois eixos para manter a área quadrada de 224×224: em tela cheia 16:9 isso corta parte do mapa e põe barras. Com `None`, a tela inteira é usada e as bordas mostram um pouco mais do mapa, sem quebrar o pixel perfect.
- Tile de chão contorna só as bordas de cima; a de baixo é desenhada pelo vizinho da frente, e contorno nas quatro bordas vira linha dupla na junção. A junta é sutil: um tom abaixo do tom dominante do próprio material, nunca `outline_blue`, para os tiles se emendarem e só uma linha leve marcar a grade. `ArtTools.RegenerateAll` roda `FloorTileSeams.ApplyToSourceFloors` antes de copiar, e um teste EditMode garante que nenhum chão em `art/` tem a cor da junta na borda de baixo do losango. O mesmo tom no miolo do tile é textura e fica.
- **Faces laterais:** `tools/art/side_faces.py` desenha `side_earth.png` e `side_stone.png` (32×32, dois níveis de face sob o losango: terra em estratos paralelos à borda, pedra em fiadas de cantaria). `SideFaceTiles.GenerateSources`, dentro de `SandboxTools.BuildSandbox`, corta delas os segmentos de um nível (`side_<material>_<lip|body>_<sw|se|sw_se>.png`). O corpo repete a cada 8 px e a cada célula, então faces vizinhas viram um muro contínuo, sem linha de grade.
- **Tile animado:** `<tile>_1.png`, `<tile>_2.png`… ao lado de `<tile>.png`, que é o quadro 0, viram um `AnimatedTile` do `com.unity.2d.tilemap.extras` em `TileAssets/Animated/<tile>.asset`, gerado por `SandboxTools`. Todos tocam a `SandboxTools.AnimatedTileFramesPerSecond` (6) com velocidade fixa e início 0, então todas as células trocam de quadro juntas. A água da Ágora usa isso com 8 quadros de água parada mexida pela brisa: a textura fica imóvel, riscos curtos de `aegean_light` balançam 1 px e somem aos poucos, e um brilho aparece num quadro só. Riscos e brilho são posicionados sobre a grade das células, então cada quadro emenda com os vizinhos. As juntas ficam paradas.
- Paredes (`wall_*`, `block_step`) usam o tom mais escuro do material como junta, para paredes vizinhas virarem um muro contínuo. O `BlockTileSeams` lê a cor da junta no pixel mais alto da coluna 0 de cada bloco e apaga a borda vertical direita nessa cor; bloco que não chega à coluna 0 usa `outline` ou `outline_blue`. Props e objetos soltos mantêm o contorno escuro.
- Trocar o modo de import de uma textura muda o fileID do sprite. Depois de mexer no importer, rode `ArtTools.SliceSheets` **e** `DataTools.Regenerate`, que religa os ícones dos `FallacySO`.
- **Arte nova antes da Unity (`tools/art/`, Python 3 + Pillow):**
  - `quantize.py` leva uma pasta de PNGs brutos para a `snes.gpl` sem dithering, redimensiona com Nearest Neighbor para a dimensão exata (`--size`, `--like art/placeholders` ou prefixo do nome) e deixa no máximo 15 cores.
  - `verify.py` imprime a contagem de cores por arquivo e sai com 1 quando um PNG tem mais de 15 cores + transparente, alfa parcial, cor fora do RGB555 ou da paleta, dimensão errada para o prefixo (`SIZE_RULES` em `sprite_rules.py`), pixel opaco fora do losango num chão, névoa ou realce (`FLOOR_FOOTPRINT`, o fundo que a IA esquece) ou chão que deixa vão ao repetir.
  - `tile_preview.py` cola 4 cópias do tile na grade e grava a prévia em `artifacts/art/tiling/`.
  - `side_faces.py` desenha as duas fontes das faces laterais (`side_earth`, `side_stone`) e `exit_arrows.py` desenha os 16 quadros das setas de saída. Os testes conferem que os PNGs versionados são o que os scripts desenham.
  - `title_frames.py` lê a tela de título e escreve os sete quadros da animação a partir dela: gira as faixas de água, faz o halo do sol crescer e recuar, engrossa e afina as nuvens pintadas, sobe um tom na luz das construções e balança o contorno da folhagem. Só recolore pixels da própria pintura, então os quadros ficam na paleta, e o quadro 0 é a arte intocada.
  - `idle_flex.py` redesenha as tiras de parado (`<personagem>_idle_<se|ne>.png` e `_idle_breath_`) a partir do quadro 0 delas: o corpo inteiro, da cabeça à barra da roupa, desce 1 px e a perna visível encurta na mesma linha, no ciclo `FLEX` (de pé, dobrado, dobrado, de pé). É joelho dobrando: os pés não saem do lugar, a roupa não muda de desenho nem de tamanho e nada anda para os lados. A fronteira entre corpo e perna é a última linha inteira antes de as pernas se separarem. A largura do arquivo diz quantos quadros o movimento tem, e `--check` falha quando a tira versionada não é o que o script desenha. Perna curta demais ou cabeça encostada no topo do quadro são recusadas, em vez de saírem tortas.
  - `shadow.py` desenha `overlay_shadow.png`, a elipse cheia de `outline` sob o ator, de raio `RADIUS_X`×`RADIUS_Y` (9×4,5, a proporção 2:1 da célula), inteira dentro do losango do chão. Ela é opaca no arquivo, e a transparência vem do `ActorView.ShadowOpacity` no `SpriteRenderer` — assim a sombra tem opacidade de verdade sem alfa parcial na arte, e `ActorView.Renderers` entrega corpo e sombra juntos para o véu da arena escurecer os dois. O sprite tem pivot *Bottom Left*, como todo tile, então o `ActorView` o desce meia célula para centrá-lo no pé e devolve esse meio passo em Z com `TerrainSorting.TileSortPoint`, do mesmo jeito que o `TilemapDepth` faz com as camadas: sem isso o ponto de ordenação cai meia célula à frente e a sombra é desenhada por cima do próprio dono.
  - `anchor.py` mede quanto a base de cada `prop_` e `block_` está fora do centro da célula (a linha mais larga e mais baixa da base vai para 8 linhas acima do fundo do canvas e x = 16), e `--fix` move o sprite. Todo objeto mais estreito que a célula passa por ele antes de entrar em `art/`.
  - `aseprite_palette.py` copia a `snes.gpl` para `palettes/default.gpl` na pasta do usuário do Aseprite e põe o `default.ase` antigo de lado, e o Aseprite a adota na próxima abertura. O `--check` confere o `default.ase` gravado.
  - Testes: `python -m unittest discover -s tools/art`. Hoje `verify.py art/placeholders` falha em 9 arquivos por pendências conhecidas, listadas na skill.

---

## Áudio

Som é dado, como a arte: a música é gravada, os efeitos saem de um script versionado, o Unity importa tudo por ferramenta de CLI, e o jogo chama cada som por id. Trocar o arquivo mantendo o nome é tudo o que a arte definitiva exige.

- **Música:** as trilhas gravadas ficam em `audio/music/<id>.wav` (WAV estéreo 24-bit a 44,1 kHz, o master sem perda): `music_title` (título), `music_kerameikos` (a casa da Kleia), `music_agora` e `music_battle`. Cada uma dura um número exato de compassos e é exportada como loop. Não converta para MP3: o Unity já comprime em Vorbis no import, MP3 sobre Vorbis perde qualidade duas vezes, e o preenchimento do encoder de MP3 abre uma pausa na emenda do loop.
- **Efeitos:** `tools/audio/placeholders.py` desenha os 11 clipes de `audio/placeholders/` em WAV mono 16-bit a 11025 Hz — ondas quadradas e triangulares em escala pentatônica, com envelope de ataque e decaimento. São 10 efeitos e o blip de voz. `--check` falha quando o WAV versionado não é o que o script desenha, como em `side_faces.py`, e um teste recusa `music_*` entre os placeholders, onde faria sombra a uma gravação.
- **Import:** `AudioTools.ImportAudio` espelha `audio/placeholders/` em `Assets/_Game/Audio/Placeholder/` e `audio/music/` em `Assets/_Game/Audio/Music/`, apaga o que sobrou e reimporta. `AudioTools.ClipPathFor` prefere a gravação ao placeholder. O `ProjectAudioImporter` (`AssetPostprocessor`, pelo mesmo motivo do `PixelArtImporter`) deixa a música em estéreo, *Streaming* e Vorbis (qualidade `MusicQuality`, 0,7). Os efeitos ficam mono, PCM e *Decompress On Load*, porque um blip de 40 ms não se transmite.
- **Ids (Core):** `AudioIds` é puro e guarda as constantes e as regras de escolha: `MusicFor(Profile)` separa o tema sofista do filósofo, `SoundFor(ActionId)` dá o som da jogada, e `MusicForLocation`/`MusicForPhase` derivam a trilha do mapa e da fase. `AudioBus` é `Music`, `Sfx` ou `Ui`. Nenhum `switch` de perfil ou de ação vive na `Game.UI`.
- **Biblioteca:** `AudioLibrarySO` sai de `Assets/_Game/Data/Source/audio.json` em `DataTools.Regenerate`, que **falha** se um id declarado não tiver clipe, se uma voz apontar para falante inexistente ou se um falante ficar sem tom.
  - **Apelidos:** `aliases` leva um id sem gravação própria ao clipe de outro. O arquivo toca a trilha da Ágora, a Academia a da casa, e os dois temas de duelo e as três fases do julgamento de rua tocam `music_battle`. O código continua pedindo o id específico, então uma gravação nova com o nome do apelido substitui o apelido: é só tirar a linha de `aliases`. A regeneração falha com apelido que também é clipe ou que aponta para um id que não é clipe.
- **Tocador:** `Game.UI.GameAudio` tem duas fontes de música, para o crossfade, e pools de 4 fontes para efeitos e para UI. A API é estática (`Play`, `PlayUi`, `Blip`, `PlayMusic`, `StopMusic`) e **não faz nada sem instância** — é o que permite aos testes montarem view crua sem áudio. O campo estático leva `[AutoStaticsCleanup]`, então a classe é `partial`, e `Shutdown()` existe para o teardown de PlayMode, senão o objeto com `DontDestroyOnLoad` vaza de um teste para o outro.
- **Transições:** `PlayMusic(id, segundos)` entra com a trilha nova de 0 enquanto a anterior sai na outra fonte, pelo tempo não escalado, então a pausa não congela o fade.
  - **Tempos:** `CrossfadeSeconds` (1,5 s) entre mapas e do título ao jogo. `BattleCrossfadeSeconds` (0,6 s) ao começar o duelo, junto com o deslize da câmera. No fim do duelo, a batalha sai em `StingFadeSeconds` (0,8 s) para o jingle de vitória ou derrota soar sozinho, e a trilha do mapa volta quando a exploração volta.
  - **Mesmo clipe não recomeça:** pedir um id cujo clipe já toca não faz nada. Por isso a Ágora e o arquivo emendam, e a troca de fase do julgamento não reinicia a batalha. Pedir a trilha que ainda está saindo a traz de volta do volume em que estava.
  - **Retomada:** `MusicResume` (Core, puro) guarda onde cada clipe parou. Voltar a ele em até `WindowSeconds` (5 min) retoma dali, então sair de um duelo não recomeça a música da Ágora. Depois disso, a trilha começa do início.
  - `Muted` para as duas fontes e esquece a trilha, então o próximo `PlayMusic` volta a tocar.
- **`AudioListener`:** existe num lugar só, no `RenderSetupTools.CreateCameraRigPrefab`. Toda cena instancia esse rig, então há exatamente um por cena. Dois viram aviso da Unity, e aviso reprova os `LogAssert.NoUnexpectedReceived()` do modo avião.
- **Voz:** há um `voice_blip` só, tocado com `pitch` por falante (`AudioLibrarySO.PitchFor`), dos 0,7 do Menexeno aos 1,15 da Kleia. O `DialogueBoxView` o dispara quando o typewriter revela um caractere novo, e `GameAudio.BlipCooldownSeconds` (45 ms) impede que vire metralhadora. `DialoguePlayer` passa o id do falante em `DialogueBoxView.VoiceId`.
- **Volume:** `GameSettings` tem `MasterVolume`, `MusicVolume` e `EffectsVolume`, nos mesmos passos de 10%. `GameServices.ApplySettings` continua escrevendo `AudioListener.volume` e leva os outros dois aos buses. Arquivo de settings antigo, com só `master_volume`, continua válido. Ainda **não há linha de música e efeitos no menu de opções** — o volume é aplicado, mas só o mestre é ajustável pela UI.
- **Não há `AudioMixer`.** Os buses são pools com multiplicador próprio. Criar um `.mixer` por CLI só é possível por reflexão sobre `UnityEditor.Audio.AudioMixerController`, que é interno; enquanto o Ato I não pedir ducking, snapshot ou filtro, o multiplicador entrega o mesmo e não depende de API interna.

---

## Unity CLI

O Unity CLI (`unity`) resolve a versão do editor pelo `ProjectVersion.txt`. O patch fica congelado em `6000.6.0f1` até a entrega acadêmica.

```bash
unity editors default 6000.6.0f1
unity projects require
unity projects verify

unity test . --mode EditMode --output artifacts/editmode.xml -- -nographics
unity test . --mode PlayMode --output artifacts/playmode.xml

unity build . --profile "Windows Development" --output-path Build/Windows/GFT.exe
unity build . --profile "Web" --output-path Build/Web

unity run . -- -nographics -executeMethod Game.EditorTools.DataTools.Regenerate -logFile -
unity run . -- -nographics -executeMethod Game.EditorTools.ArtTools.SliceSheets -logFile -
unity run . -- -nographics -executeMethod Game.EditorTools.AudioTools.ImportAudio -logFile -

unity run . -- -nographics -executeMethod Game.EditorTools.BuildProfileTools.CreateProfiles -logFile -
unity run . -- -nographics -executeMethod Game.EditorTools.BuildProfileTools.ConfigureProfiles -logFile -
unity run . -- -nographics -executeMethod Game.EditorTools.RenderSetupTools.Apply -logFile -
unity run . -- -nographics -executeMethod Game.EditorTools.SandboxTools.BuildSandbox -logFile -
unity run . -- -executeMethod Game.EditorTools.SandboxTools.CaptureReference -logFile -
unity run . -- -nographics -executeMethod Game.EditorTools.UiTools.ImportTextMeshProEssentials -logFile -
unity run . -- -nographics -executeMethod Game.EditorTools.FontTools.BuildPixelFont -logFile -
unity run . -- -nographics -executeMethod Game.EditorTools.BattleHudTools.BuildHud -logFile -
unity run . -- -nographics -executeMethod Game.EditorTools.SystemMenuTools.BuildMenus -logFile -

unity vcs doctor
```

**UI de batalha (uGUI + TextMesh Pro).**
- **`UiTools.ImportTextMeshProEssentials`:** extrai o `TMP Essential Resources.unitypackage` do pacote `com.unity.ugui` direto para `Assets/TextMesh Pro`, preservando os GUIDs. `AssetDatabase.ImportPackage` só agenda a importação, e o batch sai antes de ela rodar.
- **Fonte:** Ark Pixel Font 10px Proportional, variante latin (SIL OFL 1.1, de TakWolf, em `Assets/_Game/Fonts/ArkPixel` com o `OFL.txt` ao lado), gerada por `FontTools.BuildPixelFont`.
  - O Font Asset é raster (sem anti-aliasing), gravado no tamanho nativo de 10 px (`FontTools.NativePixelSize`), com atlas 512 em filtro Point. Ele é congelado como estático depois de gravar latim básico, acentos do português e o □, para a build não descartar o atlas. Falta de glifo no arquivo da fonte faz a ferramenta falhar.
  - A LiberationSans fica como fallback para caracteres fora do atlas; o □ de glifo ausente já vem da própria Ark.
  - A ferramenta define a fonte como `TMP_Settings.defaultFontAsset`, e todo label nasce dela. Rode antes de `BattleHudTools.BuildHud`.
  - Todo label usa o tamanho nativo (`BattleHudTools.FontSize` = 10), sem auto size: um pixel da fonte é um pixel do canvas, e o título da tela inicial usa o dobro (20). A fonte é proporcional (maiúsculas de 7 px, avanço de 4 a 6 px), então as larguras são medidas no texto real, nunca contadas em caracteres. `UiTextFitTests` confere que ações, falácias, motivos, entradas de menu, opções e linhas de slot cabem no label sem corte.
  - **Maiúsculas acentuadas:** a Ark desenha `Á À Â Ã É Ê Í Ó Ô Õ Ú` inteiras, com o acento acima da linha de capitular, então nada é recomposto no atlas. Os testes de `PixelFontTests` conferem isso no próprio atlas.
  - **Entrelinha:** o `lineHeight` do Font Asset é a altura real de todos os glifos gravados (do pé do descendente ao topo do acento), 14 px, então o acento de uma linha nunca encosta na de cima. As listas usam linhas de 12 px. A ferramenta refaz o atlas no mesmo asset, e GUID e sub-assets continuam os mesmos.
  - **Alinhamento vertical:** labels de uma linha usam `Left`/`Right` (centro pelas métricas da fonte), nunca `MidlineLeft`/`MidlineRight`: o *Midline* centraliza pela geometria do texto, e uma linha com acento ou cedilha sairia um pixel mais alta ou mais baixa que as vizinhas.
- **Canvas:** toda UI (HUD, diálogo da exploração, medidor de jurados, compêndio, pausa e `MainMenu`) usa Canvas *Screen Space – Overlay* com `CanvasScaler` de referência 640×360 (`UiBuilder.CanvasWidth` e `CanvasHeight`), casando a altura. Isso dá escala inteira em 720p (2×), 1080p (3×), 1440p (4×) e 4K (6×). Não use *Screen Space – Camera*: com a `PixelPerfectCamera` em *Upscale Render Texture*, a UI seria desenhada na render texture de 224×224 e o texto sairia ilegível depois de ampliado.
- **Estilo:** o da referência (`art/reference/target_style.md`). Caixas pretas com borda branca de 1 px e cantos cortados (`box_*` em 9-slice), cursor ▸ dourado e texto branco. O título de uma janela solta (pausa, opções, slots, créditos, compêndio) é uma aba sobre a borda de cima (`UiBuilder.CreateTab`). Dentro da faixa de baixo não há aba, porque ela subiria sobre a fileira de cima. O nome de quem fala é uma plaqueta dourada com letra escura. `UiPlate` ajusta a largura da aba ou plaqueta ao texto a cada troca. Os labels de uma linha não cortam texto (`TextOverflowModes.Overflow`), e `UiTextFitTests` garante que o texto de verdade cabe na largura.
- **`BattleHudTools.BuildHud`:** gera `Assets/_Game/Prefabs/UI/BattleHud.prefab`. Rode `DataTools.Regenerate` antes. Tudo fica numa faixa preta embaixo da tela, de altura `HudLayout.BottomStripFraction` do canvas (90 de 360), e nada cobre a parte de cima, onde estão o duelo e o público:
  - **Menu de ações:** coluna à esquerda (96×86), com as 6 ações em linhas de 12 px.
  - **Painéis dos debatedores (`DebaterPanelView`):** na fileira de cima da faixa, Kleia à esquerda em azul egeu e o desafiante à direita em vermelho, espelhado. Cada painel (156×28) tem o rosto recortado do retrato, a plaqueta com o nome na cor do lado, a legenda `hud.coherence` e a barra de Coerência com o valor, como `7/10`. A barra do desafiante esvazia para a direita. `BattleHud.Introduce(DebaterBadge, DebaterBadge)` recebe nome e retrato de cada um.
  - **Plateia:** entre os dois painéis, com a legenda `hud.audience`. A parte da Kleia enche em azul pela esquerda sobre o vermelho do desafiante, com as porcentagens de cada lado nas cores deles.
  - **Mensagens:** a fileira de baixo (538×56 em 16:9) é a área de texto. O diálogo ocupa toda ela, com retrato 40×40 e a plaqueta do nome embaixo dele à esquerda, e 3 linhas de texto. Sem diálogo aberto, a caixa de ajuda mostra o motivo de uma ação indisponível.
  - **Submenus:** falácias e premissas abrem numa caixa de 300×56 no começo da área de texto, com 4 linhas visíveis. O título ("Qual falácia?") aparece em dourado na área de texto à direita dela. Enquanto ela está aberta, `ActionMenuView.SubmenuToggled` faz o `BattleHud` empurrar o diálogo para o lado, então a dica do tutorial nunca cobre a lista.
- **Rótulos e mensagens:** rótulos das ações e motivos de ação indisponível vêm de `Assets/_Game/Data/Source/ui_text.json`, gerado como `UiTextSO`. As chaves são `action.<acao>` e `rejection.<motivo>` em snake case (`UiTextKeys`), e uma chave ausente aparece como `#chave`, nunca como texto vazio.
  - **Quem a fala nomeia:** toda linha `battle.*` de resultado recebe dois nomes, `{0}` para quem jogou e `{1}` para o outro debatedor. Quem perde o fio, aproveita o erro ou se contradiz é sempre `{1}`: o erro contrário fazia a caixa dizer que a Kleia perdia o fio quando ela acertava a refutação. Um teste EditMode formata cada linha com dois nomes e confere qual dos dois aparece.
- **Menu:** `BattleMenuController` é puro (raiz, submenu de falácia, submenu de premissa). As views só desenham. Teclado e gamepad entram por `BattleMenuInput`, e o mouse entra por `MenuRowPointer`. O clique do mouse na ação `Confirm` é ignorado, para não confirmar duas vezes.
  - **Vez de quem:** `ActionMenuView.Interactive` só é verdadeiro na fase `PlayerChoosing`, e o setter de `SandboxBattleDirector.Phase` o mantém em dia. Fora da vez da Kleia (oponente pensando, animação, diálogo, escolha de destino da CIRCULAR, chegada do público), o cursor some e o menu ignora teclado, gamepad e mouse. A caixa de ajuda também fica vazia: fora da vez, toda ação volta como `NotActorsTurn`, e sem isso ela repetia "Ainda não é a sua vez." enquanto o oponente pensava.
  - **Lista longa:** o submenu mostra 4 linhas e rola com o cursor (`WindowStart`). As setas `MoreAbove` e `MoreBelow` (o cursor girado 90°) aparecem quando há opções escondidas acima ou abaixo, e a rodinha do mouse rola a lista pelo `BattleMenuInput`. Sem as setas, a dica do tutorial citava o Equívoco, 9º da lista, e o jogador não o via.
- **Caixa de diálogo:** `DialogueBoxView` estica na largura da área de texto, com uma coluna à esquerda para o retrato 40×40 e, embaixo dele, a plaqueta com o nome do falante. `ConfigureMargins` reserva a margem do texto ao lado dessa coluna quando a linha tem retrato ou falante e, quando a linha tem escolhas, ao lado da caixa de opções. A narração usa a largura toda. A quebra de linha e a paginação usam `TextOverflowModes.Page` do TMP, então a largura real em pixels vale mais que a contagem de caracteres do `DialogueText`. O `Typewriter` revela a página atual. `Confirm` completa a página, depois avança, e na última fecha e dispara `Finished`.
- **Glifo ausente:** o TMP troca o caractere por `TMP_Settings.missingGlyphCharacter` ou, se ele for 0, por □ (U+25A1), e registra aviso. Um teste garante que a fonte padrão tem esse glifo e cobre `á é í ó ú â ê ô ã õ à ç`, todas as maiúsculas `Á À Â Ã É Ê Í Ó Ô Õ Ú Ç`, aspas, travessão e reticências. Outro lê o atlas e confere que cada maiúscula acentuada vem da própria fonte, não é o desenho da minúscula, mantém a letra base inteira e põe o acento acima da linha de capitular.
- **Compêndio:** `CompendiumView` mostra só as falácias desbloqueadas de `FallacyCompendium`, com nome, descrição, dica e ícone vindos do catálogo, e um contador vindo de `ui_text.json`. Sem nenhuma descoberta, mostra `compendium.empty`. `BattleHudTools.CreateCompendiumView` monta uma janela de 440×184 no centro, com a lista de 12 linhas à esquerda e os detalhes à direita, em dois lugares:
  - `Assets/_Game/Prefabs/UI/Compendium.prefab`, avulso, com `CompendiumInput` próprio;
  - o `PauseMenu.prefab`, sem input, porque quem o dirige é o `SystemMenuInput` da pausa.
- **Ícone de falácia revelada:** `FallacyRevealView.Attach(actor)` coloca o ícone de 16×16 sobre a cabeça do ator, 2,5 unidades acima do pivot, na mesma camada e com ordem +100.
- **Modo assistido:** `ActionMenuView.AssistedMode` pinta de dourado a premissa fraca no submenu de maiêutica.
- **Testes:** `TextMeshProUGUI` só gera malha dentro de um `Canvas`. Teste EditMode que lê `textInfo` precisa pôr o label sob um `Canvas`, senão `characterCount` fica 0.
- **Capturas da UI:** os testes PlayMode `[Explicit]` `Capture_BattleHudAndPauseMenu` e `Capture_MainMenu` gravam em `artifacts/reference/` a batalha com uma fala e com a lista de falácias, o diálogo da exploração, a pausa, o compêndio e a tela de título com slots e opções, a 1920×1080 (UI em 3×, mundo em 4×). Rode com `./tools/test.ps1 -Mode PlayMode -Filter "Capture_BattleHudAndPauseMenu;Capture_MainMenu"`. `UiCapture` troca os canvases para *Screen Space – Camera* e chama `Camera.Render`: em batch mode o `WaitForEndOfFrame` nunca volta.

**Menus de sistema.** A ordem de geração é: `DataTools.Regenerate`, depois `BattleHudTools.BuildHud`, depois `SystemMenuTools.BuildMenus`, depois `SandboxTools.BuildSandbox`. A sandbox instancia o prefab da pausa, então os menus têm de existir antes.
- **Cenas no build:** `SystemMenuTools.ApplyBuildScenes` define a ordem `MainMenu`, `GridSandbox`, `Kerameikos`, `Archive`, `Academy`, `SampleScene`, e as duas ferramentas usam o mesmo método.
- **Cena `MainMenu`:** tem câmera, EventSystem com `InputSystemUIInputModule`, `GameServices` e `MainMenuController`. "Continuar" só habilita quando algum slot é usável e abre `SaveSlotsView` para carregar. "Novo jogo" abre o mesmo painel para escolher o slot e pede confirmação antes de sobrescrever, com "Não" como padrão. Os dois carregam `GridSandbox` até existir o mapa do Ato I.
  - **Tela de título:** o fundo é `art/placeholders/ui/bg_title.png` (a Acrópole ao pôr do sol, 384×216), esticado por um `AspectRatioFitter` para cobrir a tela: 5× exato em 1080p.
  - **O quadro respira:** `bg_title_1.png` a `bg_title_7.png` (gerados por `tools/art/title_frames.py`) tocam em `SpriteFlipbook`, a `SpriteFlipbook.DefaultFramesPerSecond` (6), com `bg_title.png` como quadro 0. Em cada volta as ondas correm pela baía, o halo do sol cresce dois pixels e volta, as nuvens pintadas encorpam, a luz do poente avança um tom sobre os telhados e as árvores e os arbustos balançam. O relógio é o tempo não escalado, então a tela continua viva com o jogo pausado.
  - **Nuvens soltas:** `cloud_high`, `cloud_mid` e `cloud_low` (32×16, desenhadas no PixelLab) atravessam o céu por `DriftingSprite`, cada uma na sua altura e velocidade (`SystemMenuTools.TitleClouds`). Elas são filhas do fundo, medem e andam em pixels da pintura (o componente converte pela largura do `RectTransform` do fundo) e reaparecem do outro lado ao sair.
  - O título `system.game_title` fica no alto, em dourado no dobro do tamanho da fonte, com contorno escuro, e `system.game_subtitle` embaixo dele. O céu fica livre abaixo do título, e o Partenon não é coberto.
  - A lista do menu fica no canto de baixo à direita, sobre os telhados. Slots, opções, créditos e confirmação abrem no centro, no mesmo estilo de caixa.
- **Painéis:** `MenuListModel` pula entradas desabilitadas. `MenuListView`, `OptionsView`, `CreditsView` e `CompendiumView` implementam `IMenuPanel`. `SystemMenuInput` repassa teclado e gamepad ao painel ativo, e `PanelRowPointer` repassa o mouse.
- **Opções:** `GameSettings` (Core) guarda volume em passos de 10%, velocidade do texto (15, 30 ou 60 caracteres por segundo) e modo assistido, persistidos em `settings.json` por `LocalSettingsStore`, com JSON tolerante que volta ao padrão. `GameServices.UpdateSettings` grava, aplica em `AudioListener.volume` e dispara `SettingsChanged`. `BattleHud.ApplySettings` leva velocidade e modo assistido para o diálogo e o menu.
- **Save automático:** `AutosavePolicy.ShouldSave` é verdadeiro ao voltar à exploração depois de batalha ou diálogo. `GameServices.Autosave` grava local e, com rede configurada, empurra pelo `SaveSynchronizer`. Se o envio falhar, o `Update` reenvia o save da sessão a cada `PushRetrySeconds` (30 s) até ele subir. Um save pendente que a sessão não gravou, carregado num boot offline, espera a reconciliação do próximo boot, para nunca sobrescrever um save mais novo da nuvem. `ExplorationSaveMapper` monta o `SaveData` a partir do `ExplorationSnapshot`, preserva o progresso do save anterior e acrescenta as falácias desbloqueadas e os duelos vencidos na sessão (`DuelProgress`).
- **Pausa:** `PauseMenu.prefab` fica em toda cena de mapa, com a tela escurecida e a lista numa caixa central com o título `system.pause_title` na aba. Cancel fora da pausa abre o menu, `Time.timeScale` vai a 0 e o `GameStateMachine` entra em `Paused` quando houver um. O menu tem Voltar ao jogo, Salvar, Opções, Compêndio e Menu principal. Salvar só habilita com `SaveProvider` e serviços prontos e abre os slots com confirmação de sobrescrita. Menu principal salva no slot ativo por `SaveProvider` antes de trocar de cena. `GridSandboxController.CaptureSave` é o provedor da sandbox.
  - **Compêndio:** o painel vem dentro do prefab da pausa. No `Start`, o `PauseMenuController` chama `BindCompendium` com o `GameServices.Compendium` da cena, então a entrada fica habilitada em toda cena com `GameServices`. Sem painel ou sem serviços, ela fica desabilitada.
- **Créditos:** vêm de `credits.body` em `ui_text.json`, paginados. Ao adicionar fonte, biblioteca ou asset de terceiros, acrescente a licença ali.
- **Testes PlayMode:** nunca tocam o save nem as opções reais do jogador. `IsolatedDataDirectory.Enter()` aponta `GFT_DATA_DIRECTORY` para uma pasta temporária, que `GameServices.DataDirectory` respeita, e o `Dispose` do teardown apaga essa pasta.

**Medidor de 501 jurados (Tribunal).**
- **`TribunalVerdicts` (Core):** guarda os números de referência. São 501 jurados e maioria de 251. O final Sofista absolve por 340, o Filósofo condena por 261 e a Mestra absolve por 281.
- **`JurorCount` (Core):**
  - Embaralha os votos de forma determinística pela seed, mantém os totais exatos e reserva o último voto para Menexeno.
  - Revela a 60 votos/s e segura 1,5 s antes do voto dele; `SkipToEnd` pula a animação.
  - O construtor rejeita um voto de Menexeno impossível para os totais pedidos.
- **`JurorLayout`:** 1 pixel por jurado, em 3 fileiras (y 6, 8 e 10), com grupos de 10 colunas separados por 1 pixel. A absolvição entra pela esquerda e a condenação pela direita. Um teste garante que os dois lados nunca se cruzam dentro dos 200×16.
- **`JurorMeterView`:** pinta uma `Texture2D` 200×16 com filtro Point num `RawImage` e destaca Menexeno em dourado, com uma marca acima e abaixo da coluna dele. A absolvição usa o azul da Kleia e a condenação o vermelho do desafiante. O prefab é `Assets/_Game/Prefabs/UI/JurorMeter.prefab`, gerado por `BattleHudTools.BuildHud`, numa caixa da referência sobre a fileira de cima da faixa de baixo, onde fica a Plateia na batalha.

No dia a dia e no CI, use os scripts de `tools/`, que chamam o CLI com `--format json` e tratam o código de saída — `8` é teste falhando e não reintenta; `6` e `7` são falha de operação ou de serviço e reintentam uma vez. `-Format github` troca o formato para o CI transformar falha em anotação:

```powershell
./tools/test.ps1 -Mode All
./tools/build.ps1 -BuildProfile "Windows Development"
```

PlayMode não usa `-nographics`. `unity run` já passa `-batchmode` e `-quit` ao editor e recusa essas flags depois do `--`. Build Profiles ficam em `Assets/Settings/Build Profiles/`: `Windows Development`, `Windows Release` e `Web`.

Todo `-executeMethod` aponta para um `static void` em `Assets/_Game/Editor/`, namespace `Game.EditorTools`. Ele deve chamar `EditorApplication.Exit(code)` explicitamente ao final, com código diferente de zero em falha, ou o CI passa mesmo quebrado.

### CI (GitHub Actions)

`.github/workflows/ci.yml`, runner Windows. Detalhes, segredos e o passo a passo do ruleset estão no README.
- **Jobs:** `functions-tests` e `unity-tests` rodam em todo push e em PR para a `main`. São os checks que o ruleset da `main` deve exigir. O ruleset é configuração do GitHub, fora do repositório: enquanto ninguém o criar, um check vermelho não bloqueia nada. `windows-build` roda só em push ou disparo manual na `main`, depois dos dois: build, `tools/scan-secrets.ps1` e upload do player.
- **Setup:** `.github/actions/setup-unity` instala o CLI fixo por `tools/install-cli.ps1` (versão e SHA-256 no script), valida a conta de serviço com `unity auth login --secret-from-stdin --no-store`, instala o Editor do `ProjectVersion.txt` com `unity install`, ativa o serial quando o runner não tem licença e devolve a chave de `unity cache key` para o cache da `Library`.
- **Licença:** a Unity não ativa licença para conta de serviço.
  - Os jobs Unity rodam no runner da variável `UNITY_RUNNER` (JSON de rótulos, padrão `"windows-latest"`).
  - Com licença já ativa no runner, o job não ativa nem devolve nada. É o caminho do projeto, que só tem a Personal: runner self-hosted na máquina de desenvolvimento, rodando com o usuário dono da licença.
  - Nesse caminho, a licença do Editor é a Personal da conta pessoal, e a conta de serviço autentica só o CLI. O "nunca conta pessoal" do [CLI-02] só se cumpre por inteiro com assento pago (`UNITY_SERIAL`) ou servidor flutuante.
  - Testado só com credenciais fictícias na conta de serviço: `license status` ainda vê a Personal, o CLI não passa `-accessToken` ao Editor, e o Editor valida a licença pelo cliente de licença local. Com credenciais válidas, ainda não rodou.
  - Sem licença, o job ativa `UNITY_SERIAL` e marca `RUNNER_TEMP/unity-license-activated`. `.github/actions/return-unity-license` só devolve a licença quando essa marca existe, então a licença que já estava na máquina nunca é devolvida.
  - `.ulf` não entra: a Personal não tem ativação manual e o arquivo fica preso ao hardware que o gerou.
- **Push direto na `main`:** com o ruleset ativo, a `main` só aceita commit que já passou nos checks. Por isso o commit sobe antes para o branch `ci` e depois para a `main`.
- **Fim de linha:** `functions/src/generated/*.json` é `eol=lf` no `.gitattributes`. Sem isso, um checkout novo no Windows traz CRLF e o teste de sincronia do `content.json` falha.
- **Claude Code:**
  - **Skill:** a skill `unity-cli` fica em `.claude/skills/unity-cli`, gerada por `unity skill install claude-code --local`. O `allowed-tools` do cabeçalho foi tirado, porque pré-aprovaria o Bash em toda sessão com a skill ativa. Ao regravar a skill, tire de novo. O `unity skill refresh` não sobrescreve a cópia editada sem `--force`.
  - **Este arquivo vence as skills da Unity.** A skill manda dirigir o Editor aberto em vez de editar cena e asset. Aqui, cena, prefab e asset só saem das ferramentas de `Assets/_Game/Editor/` por `unity run ... -executeMethod`, nunca de `unity command` ou do MCP. O Editor conectado serve para inspecionar e verificar (inclusive os comandos `gft` de `Assets/_Game/Code/DevCommands`), e `unity command eval` fica só na máquina local.
  - **MCP:** o `unity-editor-mcp` (`unity mcp`) fica em `.mcp.json`, no escopo do projeto, e cada pessoa o aprova na primeira sessão. `unity mcp configure claude-code` gravaria a mesma entrada no escopo do usuário.

### Plano B: editor em batch mode

Se o CLI falhar, o editor direto faz o mesmo. Dois detalhes que costumam custar uma hora: **`-runTests` não combina com `-quit`** (o processo encerra antes de escrever o XML), e **`-logFile -`** manda o log para stdout — sem isso o erro fica num arquivo que ninguém lê.

```bash
UNITY="C:/Program Files/Unity/Hub/Editor/6000.6.0f1/Editor/Unity.exe"

"$UNITY" -batchmode -nographics -projectPath . \
  -runTests -testPlatform EditMode -testResults artifacts/editmode.xml -logFile -

"$UNITY" -batchmode -nographics -quit -projectPath . \
  -executeMethod Game.EditorTools.DataTools.Regenerate -logFile -

"$UNITY" -batchmode -nographics -quit -projectPath . \
  -executeMethod Game.EditorTools.BuildTools.BuildWindowsDevelopment -logFile -
```

`-runTests` sai com código 2 quando algum teste falha. `BuildTools` aceita `-buildOutput <caminho>`, o mesmo argumento que `unity build --execute-method` repassa. No PowerShell, `Unity.exe` é aplicativo de janela e não bloqueia: use `Start-Process -Wait -PassThru` para ler o código de saída.

---

## Armadilhas conhecidas

**Ordenação isométrica** é o bug mais caro deste tipo de projeto. Ao mexer em sorting, teste os quatro casos difíceis: prop que transborda a célula na frente e atrás de um ator, três atores em diagonal adjacente, ator atrás de coluna alta, bloco ocupando visualmente duas células.

**Acentuação em fonte bitmap** quebra em silêncio. Ao tocar em fonte ou texto, teste `ç Ç ã Ã õ Õ â ê ô á é í ó ú à` e o travessão `—`.

**Cor fora do RGB555.** Todos os canais são múltiplos de 8. Quantizar contra `art/palette/snes.gpl` com dithering desligado, máximo 15 cores + transparente por sprite.

**Custo de IA escala com jogadores.** Antes de adicionar chamada nova ao LLM, confira se o cache compartilhado cobre o caso. Existe kill switch no Remote Config que força o fallback.

---

## Convenções

**Código inteiro em inglês.** Identificadores, namespaces, nomes de arquivo e de asset, chaves de JSON e valores de enum. Português aparece só em texto exibido ao jogador, que vive nos dados (ScriptableObjects, fallback, catálogo), e na documentação.

**Sem comentários no código.** A intenção fica nos nomes: se um valor precisa de explicação, vira constante nomeada (`TileData.ImpassableCost`); se um bloco precisa de explicação, vira método com nome. Vale para C#, scripts de ferramenta e exemplos em documentação.

**Commits: Conventional Commits, sem escopo.** `type: description`, descrição em inglês, minúscula, no imperativo, sem ponto final. Só a linha de assunto: sem corpo e sem trailer `Co-Authored-By`.

```
feat: add grid model with bounds checking
fix: keep sorting order stable for overflowing props
refactor: extract fallback provider from argument client
test: cover world to grid round trip on diamond edges
docs: add README and LICENSE
chore: pin unity editor to 6000.6.0f1
ci: run editmode tests on pull requests
```

Tipos: `feat`, `fix`, `refactor`, `perf`, `test`, `docs`, `build`, `ci`, `chore`, `style`.

**Branches:** trabalho direto na `main`, sem branches `feature/*` (projeto solo). O hook de pre-commit do Unity demora minutos: rode o `git commit` em background.

**Definition of Done** (card [SET-03]): compila sem warning novo; passa EditMode e PlayMode por `unity test`; tem teste automatizado ou passo manual escrito no card; não quebrou save existente; se toca rede, testado em modo avião.
