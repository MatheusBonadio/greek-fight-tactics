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
| `Game.Core` | `Coord`, enums, `Argument`, `ArgumentValidator`, `ArgumentTones`, `FallbackArgumentPicker` | No Engine References |
| `Game.Grid` | `GridModel`, `IsoMath`, `PathFinder`, `FogOfWar` | só `Vector2` |
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

Cada entrada do catálogo tem no mínimo 3 argumentos escritos à mão em `Assets/_Game/Data/Fallback/{id}.json`, referenciados como `TextAsset` pelo `FallbackLibrarySO` — sem pasta `Resources/`. Timeout de 2 s, 1 retry, falha silenciosa — o jogador nunca espera nem vê erro. Modo avião faz parte do critério de aceite de todo card que toca rede.

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

        public static readonly TileData OutOfBounds;

        public TerrainId Terrain;
        public byte Cost;
        public bool Revealed;
        public int OccupantId;

        public readonly bool IsPassable => Cost != ImpassableCost;
        public readonly bool IsOccupied => OccupantId != NoOccupant;
    }

    public sealed class GridModel
    {
        public int Width { get; }
        public int Height { get; }
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

        public static Vector2 GridToWorld(Coord cell) =>
            new((cell.X - cell.Y) * CellWidth, (cell.X + cell.Y) * CellHeight);

        public static Coord WorldToGrid(Vector2 world);

        public static int SortingOrder(Coord cell) => -(cell.X + cell.Y) * SortingStep;
    }

    public sealed class PathFinder
    {
        public bool FindPath(GridModel grid, Coord from, Coord to, List<Coord> path);
        public void Reachable(GridModel grid, Coord from, int maxCost, List<Coord> cells);
    }

    public sealed class FogOfWar
    {
        public const int DefaultRevealRadius = 3;
        public FogOfWar(GridModel grid);
        public int Reveal(Coord center, int radius = DefaultRevealRadius);
        public byte[] ToBitmask();
        public bool TryLoadBitmask(byte[] bitmask);
    }
}
```

Tile 32×16 com PPU 16 é um losango de 2 × 1 unidades; `CellWidth`/`CellHeight` são o **meio passo** por eixo, e `GridToWorld` devolve o centro do losango. Na Unity o eixo Y cresce para cima, então a célula de maior `X + Y` está mais ao fundo e recebe sorting order menor. `SortingStep` deixa folga para sub-ordenar ator, prop e overlay dentro da mesma célula. O bitmask da névoa usa 1 bit por célula.

**Alinhamento com o `Grid` da Unity** — validado na captura da GridSandbox, não mude sem capturar de novo:

| Ajuste | Valor | Por quê |
|---|---|---|
| `Grid.cellLayout` | `IsometricZAsY` | layout isométrico 2:1 |
| `Grid.cellSize` | `(2, 1, 1)` | com PPU 16 o tile mede 2 × 1 unidades; `(1, 0.5, 1)` só valeria com PPU 32 |
| posição do `Grid` | `(0, -0.5, 0)` | a Unity ancora a célula no vértice inferior; `IsoMath` usa o centro |
| `Tilemap.tileAnchor` | `(-0.5, 0.5, 0)` | faz o pivot *Bottom Left* dos sprites de tile cair no canto do losango |
| ordenação | `TilemapRenderer.Mode.Individual` + ator com `SpriteSortPoint.Pivot` na mesma camada e ordem dos blocos | o Transparency Sort Axis (0, 1, 0) decide pela altura do pé, e o ator passa atrás de coluna e à frente de prop sem sorting order manual |

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

---

## Fluxo: exploração

`GameStateMachine` vive na mesma cena do mapa. Batalha **não carrega cena** — é isso que dá a sensação de tactics dentro do mundo e evita perder o estado da névoa.

```
Exploration → (aggro radius) → BattleIntro → BattleTurn ⇄ BattleResolve → BattleOutro → Exploration
                                                                 │
                                                          Dialogue / Paused
```

Movimento: `PathFinder.FindPath` devolve células; o ator percorre a 250 ms por célula com interpolação **linear** entre centros. Easing faz o pixel tremer. `TileData.OccupantId` é atualizado na saída e na entrada de cada célula, nunca no meio.

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

**Pré-aquecimento:** ao entrar num mapa, chame `PrewarmAsync` para os adversários presentes. Quando o jogador chegar perto, os argumentos já estão na fila e a latência é zero.

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
```

`test:emulators` precisa de Java e sobe Firestore e Auth via `firebase emulators:exec`.

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

O formato de `FogOfWar.ToBitmask` virou schema de banco. Mudá-lo invalida saves de playtest — se precisar, escreva migração.

---

## Dados

Conteúdo é ScriptableObject em `Assets/_Game/Data/`, nunca código. Se você está escrevendo `if (opponent.DisplayName == "Thrasybios")`, pare e mova para o SO.

| SO | Campos principais |
|---|---|
| `FallacySO` | `id`, `displayName`, `description`, `playerHint`, `icon`, `category` (`Informal` / `Formal` / `Paradox`) |
| `FallacyCatalogSO` | `Dictionary<string, FallacySO>` por id |
| `OpponentSO` | `displayName`, `sheet`, `maxCoherence`, `initialAudience`, `fallacyPool`, `act`, `profile` |
| `ArgumentSO` | espelha o JSON do argumento |
| `FallbackLibrarySO` | `Dictionary<string, TextAsset>`: id do catálogo → JSON com os argumentos escritos à mão |
| `TerrainCostTableSO` | `Dictionary<TerrainId, byte>` |
| `AbilitySO` | `Inversion` e `Listening` (ramificação Damaris) |
| `QuoteItemSO` | `displayName`, `effectDescription`, `coherenceDelta`, `audienceDelta`, `uses` |

Tabelas por id são `Dictionary` serializado com `[SerializeField]`, nativo no 6.6 — nada de listas paralelas. O analyzer de serialização (UAC1xxx) roda na compilação e não pode emitir aviso.

Texto exibido ao jogador (nomes, falas, descrições, dicas) é português e vive só nesses dados.

**Sem Editor, SOs são criados por script.** `Game.EditorTools.DataTools.Regenerate` lê `Assets/_Game/Data/Source/*.json` e `Assets/_Game/Data/Fallback/*.json` e (re)cria catálogo, ícones, tabela de custos e biblioteca de fallback. O conteúdo fica legível em diff, a regeneração é idempotente e falha com código diferente de zero se algum argumento de fallback não passar no `ArgumentValidator`.

---

## Pipeline de arte sem Editor

Resumo; o detalhe está na skill `pixel-art-pipeline`.

- A fonte da arte fica em `art/`. `Game.EditorTools.ArtTools.SliceSheets` copia `art/placeholders/` para `Assets/_Game/Art/Placeholder/`, força a reimportação, fatia as folhas e gera clipes e Animator Controllers. Idempotente.
- `PixelArtImporter` (`AssetPostprocessor`, porque Preset Manager exige Inspector) vale para tudo sob `/_Game/Art/`: Sprite, Point, sem compressão, PPU 16, sem mipmap, Full Rect. O modo é **sempre explícito** — `Multiple` só para `*_sheet.png`, `Single` para o resto —, porque o template 2D importa sprite novo como `Multiple` e aí o pivot definido no importer é ignorado. Tudo sob `/tiles/` recebe pivot `BottomLeft`.
- Folhas 96×128 fatiadas em células 24×32 (`<personagem>_<se|sw|ne|nw>_<coluna>`) com pivot `(0.5, 0.0625)` por `ISpriteEditorDataProvider`, preservando o `spriteID` de cada célula entre regenerações.
- Um Animator Controller por personagem em `Assets/_Game/Art/Placeholder/Animations/`, com estados `idle`, `walk` e `action`, cada um um blend tree 2D por `dirX`/`dirY`. Os clipes só trocam sprite (curva de referência de objeto, sem interpolação); curvas numéricas, quando existirem, usam tangentes `Constant`.
- `Game.EditorTools.RenderSetupTools.Apply` gera `Assets/_Game/Prefabs/Camera/PixelPerfectCameraRig.prefab` (URP `PixelPerfectCamera`, referência 224×224, PPU 16, *Upscale Render Texture*, *Crop Frame* X/Y) e põe o `Renderer2D.asset` em Transparency Sort *Custom Axis* (0, 1, 0).
- Trocar o modo de import de uma textura muda o fileID do sprite. Depois de mexer no importer, rode `ArtTools.SliceSheets` **e** `DataTools.Regenerate`, que religa os ícones dos `FallacySO`.

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

unity run . -- -nographics -executeMethod Game.EditorTools.BuildProfileTools.CreateProfiles -logFile -
unity run . -- -nographics -executeMethod Game.EditorTools.BuildProfileTools.ConfigureProfiles -logFile -
unity run . -- -nographics -executeMethod Game.EditorTools.RenderSetupTools.Apply -logFile -
unity run . -- -nographics -executeMethod Game.EditorTools.SandboxTools.BuildSandbox -logFile -
unity run . -- -executeMethod Game.EditorTools.SandboxTools.CaptureReference -logFile -

unity vcs doctor
```

No dia a dia e no CI, use os scripts de `tools/`, que chamam o CLI com `--format json` e tratam o código de saída — `8` é teste falhando e não reintenta; `6` e `7` são falha de operação ou de serviço e reintentam uma vez:

```powershell
./tools/test.ps1 -Mode All
./tools/build.ps1 -BuildProfile "Windows Development"
```

PlayMode não usa `-nographics`. `unity run` já passa `-batchmode` e `-quit` ao editor e recusa essas flags depois do `--`. Build Profiles ficam em `Assets/Settings/Build Profiles/`: `Windows Development`, `Windows Release` e `Web`.

Todo `-executeMethod` aponta para um `static void` em `Assets/_Game/Editor/`, namespace `Game.EditorTools`. Ele deve chamar `EditorApplication.Exit(code)` explicitamente ao final, com código diferente de zero em falha, ou o CI passa mesmo quebrado.

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

**Commits: Conventional Commits, sem escopo.** `type: description`, descrição em inglês, minúscula, no imperativo, sem ponto final.

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

**Branches:** `feature/SET-01-project-setup` — sempre com o ID do card e descrição em inglês. `main` protegida.

**Definition of Done** (card [SET-03]): compila sem warning novo; passa EditMode e PlayMode por `unity test`; tem teste automatizado ou passo manual escrito no card; não quebrou save existente; se toca rede, testado em modo avião.
