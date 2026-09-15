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
    characters/  7 folhas 96x128 + 7 idles 24x32
    tiles/
      floor/     7 tiles 32x16
      blocks/    7 blocos e props 32x(16+altura)
      overlays/  névoa + 4 realces de alcance
    ui/          caixas, barras, cursor, moldura
```

`art/` é a fonte, fora do projeto Unity. O card [ART-01] copia `art/placeholders/` para `Assets/_Game/Art/Placeholder/`, onde o importer atua. Os placeholders já estão em RGB555 e nas dimensões finais: trocar pela arte definitiva, mantendo nome e tamanho, não quebra nenhuma referência.

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

Nomes próprios mantêm a grafia do GDD (`kleia`, `menexeno`, `thrasybios`); só a Pítia vira `pythia`.

---

## Paleta

33 cores RGB555 — todos os canais múltiplos de 8 — em `art/palette/snes.gpl`. Os nomes seguem `<material>_<tom>`, com tons `light`, `mid`, `shade`, `dark`:

| Grupo | Nomes | Uso |
|---|---|---|
| Contorno | `outline #080810`, `outline_blue #101828` | Props e personagens / tiles de chão |
| Mármore | `marble_light/mid/shade/dark` | Ágora, roupas brancas, flash de dano |
| Ocre | `ochre_light/mid/shade/dark` | Cerâmico, terra, madeira |
| Egeu | `aegean_light/mid/shade/dark` | Kleia, Coerência, água, sombra de neve |
| Oliva | `olive_light/mid/shade` | Vegetação |
| Vermelho | `red_light/mid/dark` | Damaris, Refutação |
| Roxo | `purple_light/mid/dark` | Thrasybios, Maiêutica, véu da Pítia |
| Ouro | `gold_light/mid/dark` | Plateia, cursor, bordas |
| Pele / cabelo | `skin_light/mid/shade`, `hair_dark/mid` | Personagens |
| UI | `ui_background #181828`, `ui_border #F8F8F0` | Caixas |

Regras duras, verificadas antes de qualquer arte entrar em `art/`:

1. Todo pixel opaco pertence à paleta. Sem exceção para "quase igual".
2. Máximo de 15 cores + transparente por sprite.
3. Contorno de tile de chão é `outline_blue`; de props e personagens, `outline`. O tile de chão só contorna as bordas de cima (NO e NE, até a linha das pontas); as de baixo usam a cor do corpo, porque o vizinho da frente desenha essa linha. Com contorno nas quatro bordas, a junção vira uma linha dupla. `FloorTileSeams` aplica a regra em `art/placeholders/tiles/floor` a cada `ArtTools.SliceSheets`.
4. Transparência parcial não existe: véu, vapor e névoa usam dithering 50% entre duas cores da paleta.

---

## Dimensões e pivots

| Tipo | Canvas | Pivot | Observação |
|---|---|---|---|
| Tile de chão | 32×16 losango 2:1 | Bottom Left | Encaixa nos 4 vizinhos |
| Bloco | 32×(16+altura) | Bottom Left | `wall_*` são 32×32 (16 de topo + 16 de parede); `block_doric_column` é 32×56 |
| Prop maior que a célula | até 48×64 | Bottom Left | Ocupa 1 célula e transborda |
| Personagem | 24×32 | Custom `(0.5, 0.0625)` | Pés na linha y = 30 a partir do topo |

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

O template de prompt, os negativos e a variação para tiles estão na seção 9 do GDD. O pós-processamento não é opcional — nenhum gerador entrega RGB555 limpo:

1. Quantizar contra `art/palette/snes.gpl` com dithering **desligado** (Aseprite: *Sprite → Color Mode → Indexed* com a paleta carregada).
2. Redimensionar para o tamanho exato com Nearest Neighbor.
3. Conferir o limite de 15 cores + transparente.
4. Tiles: colar 4 cópias lado a lado e conferir o encaixe.
5. Salvar com o mesmo nome e dimensão do placeholder que substitui.

## Fonte

Bitmap de 8 px com acentuação completa. Acentuação em fonte bitmap quebra em silêncio: ao tocar em fonte ou texto, teste `ç Ç ã Ã õ Õ â ê ô á é í ó ú à` e o travessão `—`.
