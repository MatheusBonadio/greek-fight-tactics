---
name: pixel-art-pipeline
description: Regras de arte do Greek Fight Tactics (GFT) — placeholders, paleta RGB555, dimensões de tiles e sprites, fatiamento de folhas, import no Unity por AssetPostprocessor, animação com curvas Constant, ordenação isométrica e geração de arte por IA. Use SEMPRE que a tarefa tocar em sprite, tile, bloco, prop, overlay, UI em pixel art, paleta, Aseprite, import de textura, sprite sheet, pivot, Animator ou AnimationClip, sorting order, Pixel Perfect Camera, ou quando o usuário pedir para gerar, trocar, quantizar ou validar arte — mesmo que ele só diga "coloca o sprite da Kleia" ou "o pixel está tremendo".
---

# Pixel art pipeline

O projeto não usa o Editor da Unity. Tudo que normalmente seria clique no Inspector — import settings, fatiamento, pivot, clipes de animação — é código versionado em `Assets/_Game/Editor/`, executado por `unity run`. A consequência prática: se uma configuração de arte não está num script, ela não existe.

A especificação de conteúdo (fichas de personagem, tiles por mapa, tabela de animações, UI) está em `docs/GDD.md`, seções 5 a 9. Esta skill cobre o *como*.

---

## Onde a arte vive

```
art/
  palette/       snes.gpl  snes.png  snes.txt
  reference/     scene_mockup.png
  placeholders/
    characters/  13 folhas 96x128 + 13 idles 24x32
    portraits/   retratos dedicados 32x32 (hoje só aristeu_portrait)
    tiles/
      floor/     10 tiles 32x16
      blocks/    10 blocos e props 32x(16+altura)
      overlays/  névoa + 4 realces de alcance
    ui/          caixas, barras, cursor, moldura
  incoming/
    track_a/  track_b/   arte entregue que o jogo ainda não usa + generation_log.csv
```

`art/` é a fonte, fora do projeto Unity. O card [ART-01] copia `art/placeholders/` para `Assets/_Game/Art/Placeholder/`, onde o importer atua. Os placeholders já estão em RGB555 e nas dimensões finais: trocar pela arte definitiva, mantendo nome e tamanho, não quebra nenhuma referência.

O cenário estático P1 da Ágora e do Cerâmico (todo `tiles/floor` e `tiles/blocks`) já é a arte do PixelLab, trocada no lugar dos placeholders de mesmo nome e tamanho. Os chãos saíram do `create_isometric_tile` (thin tile, 32 px): o topo é recortado da linha 11, cortado no losango e levado à rampa do material por luminância, e o contorno das bordas de cima é copiado do placeholder. Paredes são `create_isometric_tile` (block). Blocos altos e props são `create_image_pixflux` isométrico com a rampa em `color_image`; os mais estreitos que a célula sobem `(32 − largura) / 4` px para a base ficar no centro do losango. `block_step` e as paredes altas são derivados das paredes, sem geração. Seed, id do PixelLab e tentativas descartadas de cada arquivo estão em `art/incoming/track_*/generation_log.csv`.

`art/reference/scene_mockup.png` mostra como tudo se monta: chão em xadrez de mármore, parede com altura, realces de alcance, névoa e o HUD com as duas barras e o menu de seis ações.

### Nomes de arquivo

Inglês, `snake_case`, com prefixo por categoria. O prefixo é o que o importer e os scripts de montagem usam para decidir o que fazer com o arquivo.

| Prefixo | Categoria | Exemplo |
|---|---|---|
| `floor_` | Tile de chão 32×16 | `floor_marble_ochre.png` |
| `block_` / `wall_` | Bloco com altura que ocupa a célula | `block_doric_column.png` |
| `prop_` | Objeto sobre a célula, pode transbordar | `prop_market_stall.png` |
| `overlay_` / `fog` | Camada acima do chão | `fog.png` |
| `hl_` | Realce de alcance | `hl_refute.png` |
| `bar_` / `box_` / `frame_` / `icon_` / `meter_` | UI | `bar_coherence.png` |
| `<personagem>_sheet` / `<personagem>_idle_se` | Personagem | `kleia_sheet.png` |
| `<personagem>_portrait` | Retrato 32×32 da caixa de diálogo | `aristeu_portrait.png` |
| `<tile>_<n>` | Quadro `n` (1, 2…) de um tile animado; `<tile>.png` é o quadro 0. Vira `AnimatedTile` em que todas as células tocam juntas | `floor_water_1.png` |

Tile animado que se repete no chão precisa emendar em todo quadro, não só no primeiro. Na água, cada quadro é a mesma textura deslocada sobre a grade das células (o loop soma um período da grade, (16, 8) px), com a crista também periódica na grade. Animação gerada por IA quadro a quadro não garante isso.

Nomes próprios mantêm a grafia do GDD (`kleia`, `menexeno`, `thrasybios`), sem acento (`oron`, `batilo`); só a Pítia vira `pythia`. Os cinco duelistas de bairro são trocas de paleta das folhas de `citizen`, `damaris` e `thrasybios`, e `aristeu` é a de `citizen` com quíton marrom e barba grisalha, até a arte definitiva. Retrato dedicado (`<personagem>_portrait.png`, 32×32, o miolo da moldura `frame_portrait` de 40×40) vence o quadro `<personagem>_se_0` da folha no diálogo e na batalha. O de Aristeu mostra a mão erguida ao lado do rosto, com os dedos tortos, desviados para o lado do mindinho, e os nós inchados: é o detalhe obrigatório do GDD, que a folha de 24×32 não tem espaço para mostrar.

---

## Paleta

A palette v2 tem 44 entradas RGB555 — todos os canais múltiplos de 8 — em `art/palette/snes.gpl`, e 43 cores, porque `ui_border` repete `marble_light`. As 11 últimas são os acréscimos da v2 (§4.2 de `docs/art/pixellab_spec.md`). Os nomes seguem `<material>_<tom>`, com tons `light`, `mid`, `shade`, `dark`:

| Grupo | Nomes | Uso |
|---|---|---|
| Contorno | `outline #080810`, `outline_blue #101828` | Props e personagens / objetos de mármore (coluna, herma) |
| Mármore | `marble_light/mid/shade/dark` | Ágora, roupas brancas, flash de dano |
| Mármore frio | `marble_cold_shade #A0A8B8`, `marble_cold_dark #687088` | Sombra e oclusão do mármore branco da arquitetura |
| Ocre | `ochre_light/mid/shade/dark` | Cerâmico, terra, tijolo cru, madeira |
| Argila | `clay_light #F8A870`, `clay_mid #D87848`, `clay_shade #A84830` | Terracota e argila crua dos props e do forno |
| Egeu | `aegean_light/mid/shade/dark` | Kleia, Coerência, água, sombra de neve |
| Oliva | `olive_light/mid/shade` | Vegetação |
| Vermelho | `red_light/mid/dark` | Damaris, Refutação |
| Roxo | `purple_light/mid/dark` | Thrasybios, Maiêutica, véu da Pítia |
| Ouro | `gold_light/mid/dark` | Plateia, cursor, bordas |
| Pele / cabelo | `skin_light/mid/shade`, `hair_dark/mid` | Personagens |
| UI atual | `ui_background #181828`, `ui_border #F8F8F0` | Caixas atuais |
| Vazio e UI nova | `void #000000`, `ui_white #F8F8F8`, `ui_gold #F8D830`, `ui_blue #3868D8` | Fundo fora do mapa, névoa e o HUD da referência |
| Alcance | `move_light #C8F058`, `move_mid #88C030` | Só o `hl_move` |

`void`, `ui_*` e `move_*` nunca entram em sprite do mundo.

Regras duras, verificadas por `python tools/art/verify.py <pasta>` antes de qualquer arte entrar em `art/`:

1. Todo pixel opaco pertence à paleta. Sem exceção para "quase igual".
2. Máximo de 15 cores + transparente por sprite.
3. A grade é marcada por uma linha sutil, nunca por contorno escuro. A junta do tile de chão é o tom da rampa logo abaixo do tom dominante do próprio material (mármore `marble_light` → `marble_mid`, grama `olive_light` → `olive_mid`, terra `ochre_mid` → `ochre_shade`), e a de parede e degrau é o tom mais escuro do material (`ochre_dark` no tijolo, `marble_cold_dark` no mármore), para os tiles se emendarem. Props e personagens levam `outline`, e objetos de mármore soltos, `outline_blue`. O tile de chão só marca as bordas de cima (NO e NE, até a linha das pontas); as de baixo usam a cor do corpo, porque o vizinho da frente desenha essa linha. Com junta nas quatro bordas, a junção vira uma linha dupla. `FloorTileSeams` aplica a regra em `art/placeholders/tiles/floor` a cada `ArtTools.SliceSheets`, olhando só a borda de baixo do losango, então o tom da junta pode aparecer como textura no miolo.
4. Transparência parcial não existe: véu, vapor e névoa usam dithering 50% entre duas cores da paleta.

O `verify.py` cobre as regras 1, 2 e 4, o RGB555, a dimensão pelo prefixo do nome e o encaixe na célula. Chão, névoa e realce (`floor_`, `fog*`, `hl_`) não podem ter pixel opaco fora do losango de 268 px dos placeholders (`FLOOR_FOOTPRINT` em `tools/art/tile_preview.py`), e o chão também não pode deixar vão ao se repetir. A regra 3 continua com `FloorTileSeams` e o teste EditMode. Os placeholders atuais ainda falham em quatro pontos, todos pendentes de decisão e não regressão:
- A sombra dos personagens é translúcida, em `#101020`: 16 px com alfa 90 nos `_idle_se` e 208 px em `#06060B` com alfa 32 nas folhas, que também saem do RGB555.
- Névoa (`fog*`) em `#101020`, a cor que o GDD 6.5 pede mas que não está na paleta.
- Realces `hl_*` a 55% de alfa, como o GDD 6.5 especifica, contra a regra 4.
- `box_dialogue` tem 160×40, e o GDD e o `DialogueBoxView` usam 208×48.

Não esconda essas falhas com `--skip`: ou a arte muda, ou a regra muda no GDD e aqui.

---

## Dimensões e pivots

| Tipo | Canvas | Pivot | Observação |
|---|---|---|---|
| Tile de chão | 32×16 losango 2:1 | Bottom Left | Encaixa nos 4 vizinhos |
| Bloco | 32×(16+altura) | Bottom Left | `wall_*` são 32×32 (16 de topo + 16 de parede); `block_doric_column` é 32×56 |
| Prop maior que a célula | até 48×64 | Bottom Left | Ocupa 1 célula e transborda |
| Personagem | 24×32 | Custom `(0.5, 0.0625)` | Pés na linha y = 30 a partir do topo |

**Centro da célula.** Todo prop ou bloco mais estreito que a célula fica com a base no centro do losango: a linha mais larga e mais baixa da base (entre as 10 últimas linhas do sprite) vai para 8 linhas acima do fundo do canvas, que é a linha mais larga do chão, e o meio dela vai para x = 16. Numa caixa (barraca, baú), essa linha é onde terminam as arestas verticais, ou seja, as pontas laterais da base. Base desenhada na frente da célula faz o objeto parecer afundado ou deslocado para baixo. `python tools/art/anchor.py <pasta>` mede o desvio de cada `prop_` e `block_`, e `--fix` move o sprite, crescendo o canvas para cima quando falta espaço. Pixel solto na base engana a medida: limpe antes. Bloco de largura cheia (32 px na base) já é a célula e não se move.

Com pivot na base, a altura extra de um bloco cresce para cima sozinha na Unity. Em coordenadas de pixel com y para baixo — no Aseprite ou no mockup — o topo do bloco sobe `altura − 16` px em relação ao chão.

### Folha de personagem

96×128, grade 24×32, **Sprite Mode = Multiple**:

| | col 0 | col 1 | col 2 | col 3 |
|---|---|---|---|---|
| **linha 0** | idle SE | passo A SE | passo B SE | ação SE |
| **linha 1** | idle SW | passo A SW | passo B SW | ação SW |
| **linha 2** | idle NE | passo A NE | passo B NE | ação NE |
| **linha 3** | idle NW | passo A NW | passo B NW | ação NW |

Os placeholders trazem as quatro direções. Na arte final, gere só SE e NE: SW e NW são espelhos resolvidos em código (`SpriteRenderer.flipX`), o que corta metade do trabalho.

As animações completas — 12 por debatedor, com frames e durações — estão na seção 7 do GDD.

---

## Import no Unity

Preset Manager exige Inspector. `AssetPostprocessor` roda em batch mode e é o único caminho:

```csharp
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    public sealed class PixelArtImporter : AssetPostprocessor
    {
        public const string ArtFolder = "/_Game/Art/";
        public const string TileFolder = "/tiles/";
        public const string SheetSuffix = "_sheet.png";
        public const int PixelsPerUnit = 16;

        private void OnPreprocessTexture()
        {
            if (!assetPath.Contains(ArtFolder))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = assetPath.EndsWith(SheetSuffix) ? SpriteImportMode.Multiple : SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.mipmapEnabled = false;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteGenerateFallbackPhysicsShape = false;
            if (assetPath.Contains(TileFolder))
            {
                settings.spriteAlignment = (int)SpriteAlignment.BottomLeft;
            }

            importer.SetTextureSettings(settings);
        }
    }
}
```

A fonte da verdade é `Assets/_Game/Editor/PixelArtImporter.cs`; o trecho acima o espelha. Três detalhes que já custaram uma rodada de depuração:

- `spriteMeshType`, `spriteAlignment` e `spriteGenerateFallbackPhysicsShape` só existem em `TextureImporterSettings`, não direto no `TextureImporter`.
- O template Universal 2D importa sprite novo como `Multiple`. Nesse modo o pivot vem do retângulo interno, e o `spriteAlignment` do importer é ignorado em silêncio. Por isso o modo é sempre definido explicitamente.
- Mudar o modo de import muda o fileID do sprite e quebra referências. Depois de mexer no importer, rode `ArtTools.SliceSheets` e `DataTools.Regenerate`.

### Fatiamento

Folhas `*_sheet.png` são fatiadas por `Game.EditorTools.ArtTools` usando `SpriteDataProviderFactories` e `ISpriteEditorDataProvider` (pacote `com.unity.2d.sprite`, asmdef `Unity.2D.Sprite.Editor`): células 24×32, pivot `(0.5, 0.0625)`, nomes `<personagem>_<se|sw|ne|nw>_<coluna>`. O `spriteID` de cada célula é reaproveitado entre execuções, então clipes e prefabs não perdem a referência. Invocação:

```bash
unity run . -- -nographics -executeMethod Game.EditorTools.ArtTools.SliceSheets -logFile -
```

O mesmo método copia `art/placeholders/` para `Assets/_Game/Art/Placeholder/`, força a reimportação e gera os Animator Controllers. É idempotente — rode depois de qualquer troca de arte. Termina com `EditorApplication.Exit` e código diferente de zero em falha. `Assets/_Game/Tests/EditMode/Art/ArtAssetsTests.cs` confere contagem, fatiamento, pivots e controllers.

### Animação

Clipe montado à mão nasce com tangentes `Linear` ou `Free` e o pixel treme. Crie os `AnimationClip` por script, sempre com keyframes de sprite:

```csharp
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    public static class SpriteClipFactory
    {
        public static AnimationClip Create(Sprite[] frames, float secondsPerFrame, bool loop)
        {
            var clip = new AnimationClip { frameRate = 1f / secondsPerFrame };
            var binding = EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite");
            var keyframes = frames
                .Select((sprite, index) => new ObjectReferenceKeyframe { time = index * secondsPerFrame, value = sprite })
                .Append(new ObjectReferenceKeyframe { time = frames.Length * secondsPerFrame, value = frames[^1] })
                .ToArray();

            AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            return clip;
        }
    }
}
```

Curva de referência de objeto é degrau por natureza: não existe interpolação entre sprites. Curvas numéricas que acompanham a animação — o bob de 1 px da caminhada, o recuo de 2 px do dano — usam `AnimationUtility.SetKeyLeftTangentMode` e `SetKeyRightTangentMode` com `TangentMode.Constant` em todos os keyframes.

Movimento na grade não é animação: é o ator percorrendo células a 250 ms cada, interpolação linear entre centros, sem easing.

---

## Grade e ordenação

Grid com **Isometric Z as Y** e Cell Size `(2, 1, 1)`: com tile 32×16 e PPU 16 o losango mede 2 × 1 unidades. `(1, 0.5, 1)` só valeria com PPU 32 e faz os tiles se sobreporem pela metade. O `Grid` fica em `(0, -0.5, 0)` porque a Unity ancora a célula no vértice inferior e `IsoMath` usa o centro, e cada `Tilemap` usa `tileAnchor = (-0.5, 0.5, 0)` para o pivot *Bottom Left* cair no canto do losango. A conversão é `IsoMath` em `Game.Grid` (ver `CLAUDE.md`).

`docs/reference/grid_sandbox.png` é a imagem de referência da GridSandbox, gerada por `Game.EditorTools.SandboxTools.CaptureReference` (sem `-nographics`, a 48 px por unidade). Depois de mexer em import, pivot, âncora ou sorting, capture de novo e compare antes de commitar.

Em pixel, com y para baixo, profundidade cresce com `x + y`. Na Unity, com y para cima, a célula de maior `x + y` está mais ao fundo e precisa de sorting order *menor* — por isso `IsoMath.SortingOrder` é negativo. `TilemapRenderer` em modo *Individual*.

Ao mexer em sorting, teste os quatro casos difíceis: prop que transborda a célula na frente e atrás de um ator, três atores em diagonal adjacente, ator atrás de `block_doric_column`, e bloco ocupando visualmente duas células.

---

## Câmera

`PixelPerfectCamera` com referência 224×224, PPU 16, *Upscale Render Texture* e *Crop Frame* X/Y ligados. Configurada em prefab versionado, nunca em cena solta. Escala final 2× Nearest Neighbor.

---

## Gerar arte por IA

O template de prompt, os negativos e a variação para tiles estão na seção 9 do GDD. O pós-processamento não é opcional — nenhum gerador entrega RGB555 limpo. Ele é feito pelos scripts de `tools/art/` (Python 3 + Pillow, `python -m pip install -r tools/art/requirements.txt`), sem Unity:

1. Tire o fundo antes: o quantizador só entende transparência pelo canal alfa (abaixo de 128 vira transparente, o resto opaco; `--alpha-threshold` muda o corte). Fundo esquecido é a falha mais comum. Num tile de 32×16 ele aparece como erro `footprint`, com a contagem de pixels opacos fora do losango. Só a contagem de vãos não pegaria esse caso, porque o retângulo opaco não deixa vão.
2. Quantize e redimensione de uma vez, com o mesmo nome do placeholder que a arte substitui:
   ```powershell
   python tools/art/quantize.py <pasta-de-pngs-brutos> --out <pasta-de-saida> --like art/placeholders
   ```
   - A dimensão vem de `--size WxH`, do PNG de mesmo nome na pasta de `--like` ou, na falta dele, do prefixo do nome.
   - O redimensionamento é Nearest Neighbor. Como cada pixel é mapeado sozinho, dá no mesmo que quantizar primeiro.
   - Cada pixel vai para a cor mais próxima da paleta pela distância RGB ponderada por luminância, `30·ΔR² + 59·ΔG² + 11·ΔB²` (`weighted_distance` em `tools/art/palette.py`), sem dithering. O peso multiplica a diferença já elevada ao quadrado: pesar o canal antes de elevar ao quadrado eleva o peso também, e aí o azul quase não conta e violeta vira marrom.
   - Se sobrarem mais de 15 cores, ficam as 15 mais usadas e o resto é remapeado entre elas (`--max-colors 0` desliga).
   - Sprite do mundo é quantizado contra a rampa do material (§4.6 da spec), passada em `--palette` como um `.gpl` com só aquelas entradas da `snes.gpl`. Com a paleta inteira, o branco quase puro cai em `ui_white` e o preto em `void`.
   - Ao final, o script já roda as mesmas checagens do `verify.py` sobre a saída.
3. Confira o lote com o relatório de cores por arquivo; ele sai com código 1 se algo quebrar as regras:
   ```powershell
   python tools/art/verify.py <pasta-de-saida> --colors
   ```
4. Tiles: `python tools/art/tile_preview.py <tile-ou-pasta>` cola 4 cópias em losango 2×2 e grava a prévia ampliada 4× sobre magenta em `artifacts/art/tiling/<tile>_tiling.png`. Para um tile de 32×16, ele também conta os pixels opacos fora do losango e os vãos numa grade 3×3, e sai com código 1 se houver algum. Olhe a prévia: vão aparece em magenta, e contorno duplo aparece na junção.
5. Props e blocos: `python tools/art/anchor.py <pasta-de-saida> --fix` centra a base de cada um na célula (ver **Centro da célula**). Confira sobre um losango de chão com o centro marcado.
6. Copie para `art/placeholders/` só o que passou, com o mesmo nome e dimensão, e rode `ArtTools.SliceSheets`.

Os testes dos scripts rodam com `python -m unittest discover -s tools/art`. A tabela de dimensões por prefixo que o `verify.py` e o `quantize.py` usam é `SIZE_RULES`, em `tools/art/sprite_rules.py`: categoria nova de arquivo entra lá.

### Aseprite

O Aseprite guarda a paleta padrão na pasta do usuário, fora do repositório: `palettes/default.ase` dentro de `%APPDATA%\Aseprite`, `~/Library/Application Support/Aseprite` ou `~/.config/aseprite`, ou de `$ASEPRITE_USER_FOLDER` quando ela existe. Por isso a configuração é feita uma vez por máquina:

1. Com o Aseprite fechado, rode `python tools/art/aseprite_palette.py`.
   - O script copia `art/palette/snes.gpl` para `palettes/default.gpl` e renomeia o `default.ase` antigo para `default.ase.bak`.
   - Na próxima abertura, o Aseprite não acha o `default.ase`, carrega o `default.gpl` como padrão e o grava como `default.ase` (`load_default_palette`, em `src/app/modules/palettes.cpp`).
   - Instalação portátil, com `aseprite.ini` ao lado do executável, usa a pasta do executável: passe `--user-folder <pasta>`.
2. Abra o Aseprite uma vez e rode `python tools/art/aseprite_palette.py --check`.
   - Ele sai com 0 só quando o `default.ase` gravado pelo Aseprite tem as 44 entradas da `snes.gpl`, na ordem.
   - Antes disso, sai com 1 e diz `pending`, `FAIL` ou qual entrada difere. *File → New* também deve abrir com as 44 entradas.
3. Pela interface, o caminho equivalente fica no menu de opções da paleta (botão ☰ acima das cores): *Load Palette* com a `snes.gpl` e depois *Save Palette as Default*.
4. Para converter um sprite à mão, use *Sprite → Color Mode → More Options…*, com modo *Indexed* e dithering *No Dithering*.

Os rótulos de menu e a migração do `default.gpl` foram conferidos no código-fonte do Aseprite (`data/gui.xml`, `data/strings/en.ini` e `palettes.cpp`). O `--check` lê os dois formatos de paleta do `.ase`: o bloco antigo `0x0004`, que o Aseprite 1.3.5 ou mais novo grava para paleta opaca de até 256 cores, e o `0x2019`. Os testes usam uma pasta de usuário temporária e arquivos `.ase` montados pela especificação do formato. Nenhum Aseprite instalado rodou os passos 1 e 2 ainda. Por isso o critério #1 do [ART-02] só fecha quando alguém os executar na própria máquina e o `--check` sair com 0.

O Aseprite serve para retoque. A conversão em lote é do `quantize.py`, e o que vale é o que passa no `verify.py`.

## Fonte

Bitmap de 8 px com acentuação completa. Acentuação em fonte bitmap quebra em silêncio: ao tocar em fonte ou texto, teste `ç Ç ã Ã õ Õ â ê ô á é í ó ú à` e o travessão `—`.
