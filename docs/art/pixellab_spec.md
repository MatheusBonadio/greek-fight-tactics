# Especificação de arte para o PixelLab

Greek Fight Tactics · versão de 21/09/2026, revisão 3 · substitui os placeholders de `art/placeholders/`

Este documento diz exatamente o que gerar no [PixelLab](https://www.pixellab.ai), com qual configuração, em que tamanho, com que nome e em que ordem. Ele divide o trabalho em duas trilhas sem nenhum arquivo em comum, para duas pessoas gerarem ao mesmo tempo. A lista completa, um arquivo por linha, está em [`pixellab_assets.csv`](pixellab_assets.csv). Os ids e os prompts do CSV são os mesmos deste documento.

Fontes: `docs/GDD.md` (§5 a §9), `art/reference/target_style.md` (a referência visual escolhida em 21/09/2026), as decisões do usuário de 21/09/2026 (§12.1), a skill `pixel-art-pipeline`, `art/palette/snes.gpl`, os 59 placeholders, os mapas `agora.json` e `kerameikos.json`, `opponents.json`, `dialogues.json`, os cards ART-03 a ART-08 e AT2-05 e a documentação da API v2 e da MCP do PixelLab, lida em 21/09/2026.

**O que mudou na revisão 3** (decisões do usuário de 21/09/2026, §12.1):
- **Fonte da UI:** Ark Pixel Font 10px Proportional (latin, OFL 1.1) no lugar da proposta monogram. A amostra `font_ui_specimen.png` passa a 384×96 e mostra o menu em linhas de 12 px (§11).
- **Paleta v2:** `snes.gpl` ganha o preto do vazio, as três cores da UI da referência, a rampa verde-amarelada do alcance, a rampa da argila e as sombras frias do mármore (§4.2). Nada mais é trocado pela vizinha. A névoa passa a `void` `#000000`.
- **Alcance de movimento:** `hl_move` em verde-amarelado de 2 tons, como na referência; REFUTAR e MAIÊUTICA mantêm as cores próprias (§4.8).
- **Tela:** quadro nativo de 384×216, ampliado 5× em 1080p. O mundo não muda de escala: losango de 32×16 e célula de 32×32 (§4.1).
- **Fora da arena:** o mundo é escurecido por um xadrez de 50% de `void`, feito em código (§11). Nenhum arquivo dependia de outra solução, então nada saiu da lista.
- **Prompts:** a terracota e a argila crua (ânforas, potes, torno, forno, clepsidra) passam a "terracotta-orange", e a arquitetura de mármore branco ganha "cool blue-grey shadows" (§4.6, §4.9). Os ids e a contagem de arquivos não mudaram.
- **Confirmados:** retrato de 32×32, parede alta só nas bordas de trás, cidadão modular por troca de cor em código e as alturas da §4.7.

**Revisão 2**, para quem vem de antes: prompts com assunto primeiro, estilo curto e sem negação (§4.9 a §4.11); ação em arco de movimento com a linha **Seleção**; movimentos de 1 px à mão; elenco criado a partir de um sprite sul no estilo da Kleia (§3.2); cidadão modular, paredes altas e portas, molduras de ícone e a fonte da UI; ids renumerados (use sempre o `file_name` para falar de um arquivo).

## Resumo

| Trilha | Arquivos P1 | Arquivos P2 | Arquivos P3 | Arquivos | Quadros | Quadros no PixelLab | Quadros de personagem | Pontos |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| **A** | 106 | 105 | 41 | 252 | 555 | 415 | 439 | 585 |
| **B** | 111 | 97 | 36 | 244 | 456 | 402 | 353 | 582 |
| **Total** | 217 | 202 | 77 | 496 | 1011 | 817 | 792 | 1167 |

- **Pontos** são uma unidade relativa de esforço, usada só para equilibrar as trilhas.
  - Uma tira gerada por texto vale 1 + 0,5 por quadro, com +1 nas ações feitas sob medida. A caminhada por template vale 3. A pose de 1 quadro vale 1,5.
  - A tira idle SE vale 1,5 (a rotação sai da criação, o quadro 2 é à mão) e a NE vale 1. Criar um personagem novo soma 4 (2 nas variantes de cidadão). Tira feita à mão a partir de quadros aprovados vale 1 + 0,2 por quadro.
  - Retrato neutro vale 2 e cada expressão por inpaint vale 1,5.
  - Chão vale 2, bloco ou prop vale 3 (4 se for alto ou largo) e ícone vale 1,5. Tile ou prop animado por texto vale 1 + 0,5 por quadro; por ciclo de paleta, 1 + 0,3 por quadro (o vapor vale 4).
  - Troca de paleta vale 0,5 + 0,1 por quadro. Parede alta e porta valem 1. Peça de UI desenhada à mão vale 2 (1 + 0,3 por quadro se tiver até 16 px, e 6 no logotipo).
- **Quadros** somam todos os quadros entregues, incluindo tiles e UI de 1 quadro.
  - "No PixelLab" conta só os quadros que saem de uma geração: a rotação, a tira animada, o tile, o retrato. Exclui o que é desenhado, deslocado ou recolorido à mão no Aseprite.
  - "De personagem" conta só as tiras de animação, incluindo as variantes por troca de paleta.
- **Conta PixelLab:**
  - As chamadas de custo conhecido (rotação `standard`, template de caminhada, `animate-with-text-v3` a 32 px, tiles, `map-objects`, `bitforge`, `pixen`) são cerca de 350, perto de 1 generation cada; com 2 a 3 tentativas, 700 a 1.050 generations para as duas trilhas. O custo exato de cada uma aparece na resposta.
  - As cerca de 70 chamadas Pro (22 sprites sul, 21 retratos, 23 expressões e a edição do `paint`) não têm preço na documentação. O piloto anota o custo real no log (campo `usage` da resposta; `GET /pro-flash/cost` estima o Pro Flash) e refaz esta conta antes do volume.
  - Uma assinatura Tier 1 para cada pessoa (2.000 generations por mês, 12 por mês na tabela de preços do site, sem moeda indicada) cobre as chamadas de custo conhecido num mês; o preço do Pro decide se falta um segundo mês. As ferramentas Pro, o `create-tiles-pro` e o `skeleton-v3` exigem Tier 1.

---

## 1. Como usar este documento

1. **Leia as seções 2 a 8 inteiras.** Elas valem para as duas pessoas. Depois, leia só a sua trilha: a seção 9 é a Trilha A, e a seção 10 é a Trilha B.
2. **Faça o piloto antes de qualquer volume** (§3.5). Ninguém gera o resto enquanto os pilotos das duas trilhas não estiverem aprovados pelos dois.
3. **Siga a ordem dos ids.** Na sua trilha, pegue o próximo arquivo com status `todo` no CSV. Os ids já estão em ordem de prioridade: todo o P1, depois o P2, depois o P3.
4. **Monte o prompt expandindo as referências.** A ordem é sempre **assunto primeiro, depois o bloco de tipo, depois o estilo curto**. Um prompt como `[THRASYBIOS] facing south in front view, tall orator stance (...), [CHARACTER] [STYLE_CHAR]` é montado assim:
   - `[THRASYBIOS]` e os outros descritores ficam na ficha do personagem, dentro da trilha dele;
   - `[CHARACTER]`, `[FLOOR]` e os outros blocos de tipo vêm da §4.10;
   - `[STYLE_CHAR]` e `[STYLE_ENV]` vêm da §4.9.

   Os prompts de animação não têm referência nenhuma: são o texto do campo `action`, já com a direção no começo. Os marcadores `[MANUAL]`, `[SWAP]`, `[EDIT]`, `[PORTRAIT]` e `[FACE]` não vão ao PixelLab como texto: são roteiros (§4.10). A tabela da §4.12 diz em qual campo de cada ferramenta cada parte entra.
5. **Escolha os quadros pela linha "Seleção".** Abaixo de cada prompt de animação, a linha **Seleção** diz, em português, quais quadros da saída ficam na tira e em que ordem. Ela substitui a descrição quadro a quadro que o modelo não consegue seguir. Na idle SE, a linha **Quadros** diz de onde sai cada quadro.
6. **Pós-processe e confira.** Todo arquivo passa pelo pós-processamento da §5.8 e pelo checklist da §6 antes de entrar no repositório.
7. **Entregue e marque.** Salve em `art/incoming/<trilha>/<categoria>/<file_name>` (§5.1), anote a geração no log (§5.7) e mude o status no CSV.

**Status do CSV:**

| Status | Significa |
|---|---|
| `todo` | ainda não começou |
| `wip` | em geração ou limpeza |
| `review` | entregue em `art/incoming`, esperando a revisão da outra pessoa |
| `approved` | revisado e aceito, pronto para o dev integrar |
| `integrated` | já está no jogo |

**Quadros:** são contados de 1 a N, da esquerda para a direita, na ordem em que tocam. É a mesma numeração das linhas "Seleção" e dos roteiros `[EDIT]` ("frame 1", "frame 2").

---

## 2. Decisões fixadas por este documento

| Decisão | Valor | Origem |
|---|---|---|
| Tamanho de personagem | Célula de **32×32** por quadro, pés 2 px acima da base, pivot `(0,5; 0,0625)` | Pedido do usuário; o pivot é o atual do `ArtTools` |
| Altura da figura | Cerca de 24 px para a Kleia, com a tabela da §4.7 para o resto | `target_style.md` (corpo de ~16×24); confirmada pelo usuário em 21/09/2026 |
| Escala da tela | Quadro nativo de **384×216**, ampliado **5×** em 1080p com Nearest Neighbor. O mundo mantém o losango de 32×16 e a célula de 32×32 de personagem e objeto | Decisão do usuário (21/09/2026) |
| Direções geradas | Só **SE** e **NE**. SW e NW são o espelho em código (`ActorView` já usa `flipX`) | GDD §7, ART-05 #2 |
| Formato de animação | Uma **tira horizontal** por animação e direção: `<personagem>_<animação>_<se\|ne>.png` | Este documento |
| Conjunto da Kleia | **Completo**: uma animação por ação do menu, incluindo RETIRAR-SE (`withdraw`, que faltava no GDD), mais os estados de apoio | Pedido do usuário |
| Demais personagens | **Conjunto reduzido** conforme o papel: chefe, duelista, NPC, espectro ou Pítia (§8.2) | Pedido do usuário |
| Criação do elenco | A Kleia é criada do texto e vira a âncora. Todo o resto nasce de um **sprite sul no estilo da Kleia** (`generate-with-style-v2` ou `create-character-pro-flash`), que é passado à rotação como `directions.south` | A ferramenta de 8 direções não aceita imagem de estilo (§3.2) |
| Movimento de 1 px | Quadro 2 da idle, `idle_breath`, bob da caminhada e os loops de brasa, água, fenda e vapor são **feitos à mão** no Aseprite, com zero generations | O `action` do v3 é texto livre: deslocamento em pixel não se controla |
| Estrutura do prompt | Assunto primeiro, depois o bloco de tipo, depois o estilo curto (§4.9). Cor em palavras comuns. Sem negação. Os parâmetros (outline, shading, detail, view, `color_image`) levam o resto | Revisão 2 |
| Retratos | 32×32, na janela da moldura `frame_portrait` de 40×40 (`BattleHudTools.PortraitInner = 32`). O neutro sai do sprite com `portrait-character-pro`; as outras expressões são inpaint no rosto do neutro. O ART-08 #1 pedia retrato de 40×40 | Código atual; confirmado pelo usuário em 21/09/2026. A ferramenta de retrato não recebe texto |
| Cidadão modular | **4 figuras geradas × 3 mantos = 12 variantes.** As 8 combinações que não são arquivo saem por troca exata de 3 cores feita por código (§8.2, §11). Em cada figura, a rampa do manto é exclusiva | GDD §5, ART-05 #1; troca por código confirmada pelo usuário em 21/09/2026 |
| Salas | Parede de trás **alta** (`wall_*_tall`, 32×48, com 32 px de face) **só nas bordas de trás**, e portas como recortes escuros, como na referência. Na frente da sala, parede baixa de 32×32 ou nenhuma (§12.2) | `target_style.md` (Grade e escala); parede alta só atrás confirmada pelo usuário em 21/09/2026 |
| Fora da arena | Em batalha, o mundo fora da arena é escurecido por um xadrez de 50% de `void`. É efeito de código, não arte: nenhum arquivo desta lista o desenha | Decisão do usuário (21/09/2026) |
| Paleta | **Palette v2** de `art/palette/snes.gpl`, ainda RGB555: acrescenta `void` `#000000`, `ui_white` `#F8F8F8`, `ui_gold` `#F8D830`, `ui_blue` `#3868D8`, a rampa `move_*` do alcance, a rampa `clay_*` da argila e as sombras frias `marble_cold_*` (§4.2). Cor fora da paleta entra no arquivo; nada é trocado pela vizinha | Decisão do usuário (21/09/2026) |
| Alcance de movimento | Losango opaco verde-amarelado em 2 tons (`move_light` com junta `move_mid`), como na referência. REFUTAR e MAIÊUTICA mantêm vermelho e púrpura (§4.8) | Decisão do usuário (21/09/2026), `target_style.md` (Cores) |
| Fonte da UI | **Ark Pixel Font 10px Proportional**, variante latin, licença SIL OFL 1.1 ([TakWolf/ark-pixel-font](https://github.com/TakWolf/ark-pixel-font)). Avanço de cerca de 6 px nas maiúsculas, maiúsculas de 7 px, linhas de 12 px no menu. A Trilha A entrega o arquivo, a licença e a amostra `font_ui_specimen.png` | Decisão do usuário (21/09/2026), no lugar da proposta monogram |
| Estilo | Tons de 3 a 4 por material, contorno escuro seletivo de 1 px, sem dithering de sombreado. Isso substitui "2 tons" e "contorno preto duro" do GDD §9 | `target_style.md`, que vence o GDD |
| Entrada no repositório | `art/incoming/track_a/` e `art/incoming/track_b/`, **nunca** `art/placeholders/`, que é copiado para a Unity e contado pelo teste | `ArtTools`, `ArtAssetsTests` |
| Nomes | Os ids dos placeholders continuam valendo (`floor_mosaic`, `floor_stone`, `prop_statue_base`, `prop_olive_trunk`), mesmo quando o GDD usa outro nome | Os mapas referenciam esses ids |

As decisões do usuário de 21/09/2026 já estão aplicadas no documento inteiro e registradas na §12.1. O que ainda depende dele está na §12.2.

---

## 3. Configurações globais do PixelLab

### 3.1 Contas

- Cada pessoa usa **a própria conta**. Personagens, objetos e tilesets ficam guardados na conta de quem os gerou, e o `style_character_id` só funciona dentro da mesma conta.
- Entre contas, a consistência vem das **imagens de referência**, que são PNGs versionados no repositório (§3.4).

### 3.2 Parâmetros fixos por tipo de asset

Os nomes são os da API v2 (`api.pixellab.ai/v2`) e, na caminhada, os da MCP (`animate_character`). Na interface web e no plugin do Aseprite, os mesmos controles aparecem com rótulos equivalentes. A `view` é **uma só para o projeto inteiro**: o piloto escolhe entre `low top-down` e `high top-down`, e o valor escolhido vale para personagens, retratos, `map-objects`, `bitforge` e `pixen`.

| Tipo | Ferramenta e parâmetros | Canvas |
|---|---|---|
| Kleia, a âncora (A-001, A-002) | `create-character-with-8-directions`, `mode` = `standard` (1 generation). `view` e `isometric` do piloto, `outline` = `single color black outline`, `shading` = `basic shading`, `detail` = `medium detail`, `template_id` = `mannequin`, `proportions` = `default` (o piloto compara com `stylized`), `color_image` = chave da Kleia com `force_colors`, `no_background`. O piloto compara com `create-character-pro-flash` (`image_size` 32×32, tamanho nativo). Guarde `south-east` e `north-east` | 32×32 |
| Elenco, passo 1: sprite sul | `generate-with-style-v2`: `style_images` = quadro 1 de A-001 (depois do piloto, também o quadro 1 de B-003), `description` = o prompt da tira idle SE, `style_description` vazio, `no_background`. Com uma imagem de estilo de 32 px, ela devolve 64 candidatos na mesma escala: escolha um de frente, na altura da §4.7. Alternativa: `create-character-pro-flash` com `style_image` = quadro 1 de A-001 e `image_size` 32×32 | 32×32 |
| Elenco, passo 2: rotação | `create-character-with-8-directions` (`standard`) com `directions` = {`south`: o sprite do passo 1} e o mesmo `description`, com a `view`, o `outline`, o `shading`, o `detail`, o `template_id` e a `color_image` + `force_colors` da Kleia. Alternativa: `create-character-v3` com `reference_image` = o sprite do passo 1. Guarde `south-east` e `north-east`: são o quadro 1 da idle SE e NE. O personagem fica salvo na conta, pronto para o template de caminhada | 32×32 |
| Sentados (Pítia, aluna) | `generate-with-style-v2` com `style_images` = quadro 1 de A-001 e a pose sentada já virada para SE. Não há rotação: esses personagens são só SE | 32×32 |
| `paint` da Kleia | `edit-image-pro-flash`, `method` = `text`, `use_color_palette_correction` ligado, `image` = quadro 1 de A-001. O canvas não cresce | 32×32 |
| Caminhada | `animate_character` (MCP) ou "Animate" na web, modo `template`, `template_animation_id` = `walking-4-frames`, `directions` = [a direção do arquivo], `ai_freedom` = 0, `action_description` = o prompt. 1 generation por direção; o esqueleto mantém os pés plantados. O bob é à mão (§5.3) | herdado |
| Animação por texto | `animate-with-text-v3`: `first_frame` = quadro 1 aprovado daquela direção; `last_frame` = o mesmo quadro nos códigos `animar loop` e `animar volta`, e vazio em `animar` e `pose`; `action` = o prompt (até 1.000 caracteres); `frame_count` par de 4 a 16 (§8.3); `drift_threshold` = 0 (é número: 0 corrige a cor de todos os quadros); `no_background` ligado; seed anotada. Na MCP é `animate_character` com `mode` = `v3`, `custom_start_frame` e `end_frame`, uma direção por chamada | 32×32 na entrada; a saída pode crescer e é recortada de volta |
| Quadros à mão | Aseprite, zero generations: quadro 2 da idle, `idle_breath`, `talk` e `paint` dos sentados, bob da caminhada. Copie o quadro aprovado e desloque linhas inteiras (§5.3) | 32×32 |
| Retrato neutro | `portrait-character-pro`, `direction` = `character_to_portrait`, `result_size` = 32, `view` do piloto, `image` = quadro 1 da idle SE (ou o sprite sul). A ferramenta não recebe texto. Espelhe na horizontal se o busto olhar para a esquerda | 32×32 |
| Expressão | `inpaint-v3` sobre o retrato neutro aprovado (32×32 é o mínimo da ferramenta), `mask_image` branca só no rosto, `description` = a expressão. Alternativa: `edit-image-pro-flash`, `method` = `text`, `use_color_palette_correction` ligado | 32×32 |
| Chão | `create-isometric-tile`, `isometric_tile_shape` = `thin tile`, `isometric_tile_size` = 32, `image_size` 32×32, `outline` = `lineless` (o contorno `outline_blue` é refeito à mão), `shading` = `basic shading`, `detail` = `low detail`, `init_image` = placeholder (força 300 a 400; nos chãos sem placeholder, o losango de `floor_marble.png` pintado com a cor base do material), `color_image` = rampa do material (§4.6). Alternativa: `create-tiles-pro` com `style_images` = placeholder ou chão aprovado | Gerar em 32×32, entregar 32×16 recortado pelo alfa do placeholder |
| Degrau, bema e banco (32×24) | `create-isometric-tile`, `isometric_tile_shape` = `thick tile` (cerca de 25% da tela, 8 px de lateral), `isometric_tile_size` = 32, `outline` = `single color outline`. Nunca `block` para uma peça de 8 px | Gerar em 32×32 e cortar as linhas transparentes de cima até 32×24 |
| Parede 32×32 | `create-isometric-tile`, `isometric_tile_shape` = `block` (cerca de 50% da tela, 16 px de face), `isometric_tile_size` = 32, `outline` = `single color outline`, `init_image` = placeholder | 32×32, recortado pelo alfa de `wall_brick.png` |
| Parede alta e porta | Aseprite (edição) da parede de 32×32 aprovada: a face ganha mais 16 px de fiadas; a porta é um recorte escuro na face | 32×48 |
| Bloco mais alto que 32, prop mais largo que 32 e props altos de pedra ou mármore (herma, fonte, kleroterion) | `create-image-bitforge`: `isometric` ligado, `view` do piloto, `init_image` = silhueta do placeholder ou blocagem à mão (força 400 a 600), `style_image` = parede ou chão aprovado do mesmo mapa (`style_strength` perto de 50), `color_image` = rampa, `negative_description` = §4.11, `no_background` | Tamanho final (área máxima 200×200) |
| Os outros props de uma célula | `map-objects`: `view` do piloto, `init_image` = silhueta do placeholder (já em 2:1) ou blocagem à mão, força 300 a 450, `color_image` = rampa, `outline` = `single color outline`, `shading` = `basic shading`. Alternativa: `create-image-bitforge` com `isometric` e `style_image`. Nunca `create-1-direction-object`: ele só tem vista `top-down` ou `sidescroller` e custa 20 a 40 generations | 32×32 (o mínimo da ferramenta); corte o topo transparente |
| Torno e vento | `animate-with-text-v3`, `first_frame` = `last_frame` = estático aprovado, `drift_threshold` = 0 | Tamanho do estático |
| Brasa, água, fenda e vapor | Ciclo de paleta à mão no Aseprite: 2 a 4 quadros exatos (o vapor tem 6, pelo AT2-05), e o tile continua encaixando nos vizinhos | Tamanho do estático |
| Moldura de ícone | Aseprite: 3 moldes (`icon_badge_informal`, `_formal`, `_paradox`), desenhados uma vez | 16×16 |
| Glifo de falácia | O primeiro glifo à mão ou com `create-image-pixen` 16×16 (quadrado é permitido abaixo de 32), `outline` = `single color black outline`, `detail` = `low detail`, `no_background`. Os outros com `generate-with-style-v2`, `style_images` = o primeiro glifo aprovado (64 candidatos a 16 px). Composição sobre a moldura no Aseprite. Nunca `create-image-pixflux`: a área mínima dele é 32×32 | Glifo de até 10×10 num canvas 16×16 |
| Ícone de item | `create-image-pixen` 16×16 para o primeiro; depois, `generate-with-style-v2` com ele como `style_images` | 16×16 |
| Efeito | Quadro-chave com `create-image-pixen` 16×16 (`outline` = `lineless`); os outros quadros à mão | 16×16 |

Em todas as ferramentas:
- **Fundo transparente:** `no_background` ou "Remove background" ligado. Não escreva "transparent background" no prompt.
- **Sombra:** nunca aceite a sombra projetada que o gerador desenhar; ela é apagada no pós-processamento.
- **Reprodução:** anote a seed e guarde as configurações da geração, para a outra pessoa conseguir continuar.

Onde os controles são "soft guidance" (outline, shading, detail), o modelo pode não obedecer: quem garante o resultado é o pós-processamento. `generate-with-style-v2`, `animate-with-text-v3` e `portrait-character-pro` não têm `color_image`: a cor delas é corrigida na quantização (§5.8).

### 3.3 Códigos de ferramenta usados nas trilhas

| Código | O que usar |
|---|---|
| `criar (âncora)` | Só a Kleia (A-001). Crie o personagem do texto (§3.2, primeira linha) e guarde SE e NE. O quadro 1 da idle é a rotação; o quadro 2 é à mão |
| `criar` | O resto do elenco em pé. Passo 1: sprite sul no estilo da Kleia. Passo 2: rotação a partir dele. Guarde SE e NE; o quadro 2 da idle é à mão |
| `rotação + mão` | Tira idle NE: a rotação NE do mesmo personagem mais o quadro 2 à mão. Nenhuma generation extra |
| `sentado: base` | Pítia e aluna: uma imagem SE sentada no estilo da Kleia, sem rotação; o quadro 2 da idle é à mão |
| `sentada: edição + mão` | `paint` da Kleia: edição do quadro 1 de A-001 para a pose sentada; o quadro 2 é à mão |
| `template walk + mão` | Template `walking-4-frames` e depois o bob de 1 px à mão |
| `animar loop` | `animate-with-text-v3` com `first_frame` = `last_frame` = quadro 1; fique com N quadros, sem repetir o primeiro no fim |
| `animar volta` | O mesmo, para ações que terminam de volta no idle |
| `animar` | `animate-with-text-v3` só com `first_frame`: a ação termina numa pose segurada |
| `pose` | Pose de 1 quadro: `animate-with-text-v3` com 4 quadros a partir do quadro 1, e fique só com o quadro-chave. Alternativa: `edit-image-pro-flash` (`method` = `text`) sobre o quadro 1 |
| `mão (Aseprite)` | Tira feita no Aseprite a partir de quadros aprovados; o prompt `[EDIT]` é o roteiro |
| `retrato` | `portrait-character-pro` a partir do quadro 1 da idle SE; o prompt `[PORTRAIT]` diz a fonte e o que conferir |
| `expressão` | `inpaint-v3` no rosto do retrato neutro; o prompt `[FACE]` diz a máscara e a descrição |
| `iso-tile thin` / `iso-tile thick` / `iso-tile block` | `create-isometric-tile` com essa forma e `isometric_tile_size` = 32 |
| `map-object+init` | `map-objects` com a silhueta do placeholder (ou uma blocagem à mão, quando não há placeholder) como `init_image` |
| `bitforge+init` | `create-image-bitforge` com `isometric`, a silhueta como `init_image` e a `style_image` do mapa |
| `animar tile` | `animate-with-text-v3` com `first_frame` = `last_frame` = o estático aprovado |
| `ciclo de paleta (Aseprite)` | Troca de cores em ciclo sobre o estático aprovado; o prompt `[EDIT]` diz quais |
| `glifo + moldura` | Glifo de até 10×10, gerado ou desenhado, composto sobre `icon_badge_<categoria>.png` |
| `ícone de item` | `create-image-pixen` 16×16 e depois `generate-with-style-v2` |
| `pixen 16x16 + mão` | Quadro-chave com `create-image-pixen`, os outros quadros à mão |
| `manual (Aseprite)` | Desenho à mão. O prompt `[MANUAL]` é o roteiro |
| `Aseprite (troca de paleta)` | Recolorir um arquivo aprovado. O prompt `[SWAP]` diz o que muda |
| `Aseprite (edição)` | Acrescentar ou estender um arquivo aprovado, sem mexer no resto. O prompt `[EDIT]` diz o que entra |

**Blocagem à mão:** quando um prop ou bloco não tem placeholder, desenhe em 2 minutos, no Aseprite, a silhueta dele em 2 tons chapados, com a pegada dentro do losango da base (§5.5). Ela já está no ângulo 2:1 e serve de `init_image`.

### 3.4 Como manter o elenco consistente entre duas pessoas

1. **Âncora de estilo única.** O quadro 1 aprovado de `kleia_idle_se.png` (A-001) é a referência de estilo de **todo** personagem, nas duas trilhas.
   - Ele entra como `style_images` do `generate-with-style-v2`, ou como `style_image` do `create-character-pro-flash`, no passo 1 do `criar`. Depois do piloto, o quadro 1 de B-003 (Thrasybios) entra como segunda imagem de estilo.
   - O sprite sul escolhido vira `directions.south` (ou `reference_image`) na rotação. A identidade e o estilo passam para SE e NE pela imagem, sem depender do texto.
   - Na conta da Trilha A, o `create-character-pro` com `style_character_id` = Kleia também serve, mas custa de 20 a 40 generations por chamada.
2. **Animação sempre a partir do quadro aprovado.** Toda tira começa do quadro 1 aprovado daquela direção, e os loops e as ações que voltam ao idle terminam nele (`last_frame`). Nunca anime só a partir de texto.
3. **Uma chave de cor por personagem.**
   - A chave é a lista da ficha. Monte um PNG de trabalho com uma faixa de 1 px por cor e use-o como `color_image` com `force_colors` onde houver esse campo (criação, tiles, `map-objects`, `bitforge`).
   - Onde não houver (`generate-with-style-v2`, `animate-with-text-v3`, `portrait-character-pro`, `inpaint-v3`), quantize a saída contra a mesma chave (§5.8).
   - Todas as tiras e os retratos do personagem usam a mesma chave: uma animação nunca traz cor nova.
4. **Configuração congelada.**
   - Depois do piloto, os valores escolhidos (`view`, `proportions`, `isometric`, canvas de geração, `frame_count` por tamanho de tira) são escritos na tabela da §3.2 e na §8.3, no lugar das alternativas.
   - Mudar qualquer um exige acordo dos dois e vale para o elenco inteiro.
5. **Revisão cruzada.** Todo arquivo de P1 é revisado pela outra pessoa numa cena de teste: o sprite sobre `floor_marble` e sobre `floor_packed_dirt`, ao lado da Kleia aprovada, em 1× e em 5× (a escala do jogo em 1080p).
6. **Referências de tile.** Os chãos aprovados no piloto (`floor_marble` e `floor_marble_ochre`) entram como `style_images` em todo chão feito com `create-tiles-pro`, das duas trilhas. Isso trava a densidade de pixel e o desenho da junta.

### 3.5 Piloto obrigatório (ART-03 #3)

Antes de começar, as duas pessoas montam as chaves de cor de trabalho com a **palette v2** (§4.2). O piloto já usa cores novas: o veio frio do `floor_marble` e a argila da `prop_amphora_stack`. Se o `snes.gpl` ainda não tiver sido atualizado (§11), use os hex da tabela da §4.2.

A ordem é esta:
1. A Trilha A faz **A-001 e A-002** (Kleia idle SE e NE). Ao mesmo tempo, a Trilha B faz **B-001 e B-002** (`floor_marble` e `floor_marble_ochre`, em xadrez).
2. Os dois aprovam juntos.
3. A Trilha B faz **B-003 e B-004** (Thrasybios idle), pelo caminho `criar` (sprite sul no estilo da Kleia e rotação). Ao mesmo tempo, a Trilha A faz **A-003** (`prop_amphora_stack`, terracota com figuras vermelhas, ART-03 #1), com `map-objects` e o placeholder como `init_image`.
4. Nova aprovação conjunta. Depois dela, cada um segue a sua trilha.

O piloto mede o que a documentação do PixelLab não garante:
- **Altura real da figura num canvas de 32.** A documentação fala em cerca de 60% da tela, uns 19 px. Se sair baixa demais, aumente o canvas de geração (40 ou 48) até a figura medir a altura da §4.7. Depois recorte para 32×32 **sem reescalar**, com os pés na linha 29. No `generate-with-style-v2`, confira se os 64 candidatos saem na escala da Kleia.
- **A `view` única:** qual de `low top-down` ou `high top-down`, com `isometric` ligado ou desligado, faz o pé do personagem e a base da ânfora assentarem no losango 2:1 do chão.
- **Quanto a tela cresce na animação**, e se o recorte de volta a 32×32 corta braço ou adereço.
- **Quantos quadros o `animate-with-text-v3` devolve** para `frame_count` = 4, com e sem `last_frame`, e se o `first_frame` vem incluído na saída. O resultado vai para a tabela da §8.3.
- **Geometria do `thin tile`:** se o losango sai com exatamente 32×16, com a ponta na linha 8, ou se precisa de recorte pelo placeholder.
- **Template:** quantos quadros o `walking-4-frames` devolve e quais são os de passagem (pernas juntas), onde entra o bob.

### 3.6 Exportação

- **Baixe o ZIP do personagem** (quadros soltos em `animations/<tipo>/<direção>/frame_000.png` e o `metadata.json`), e não o spritesheet. O spritesheet do PixelLab centraliza cada quadro na célula e desalinha os pés.
- **Nas ferramentas avulsas** (`animate-with-text-v3`, `generate-with-style-v2`, `inpaint-v3`), baixe cada imagem da resposta, na ordem.
- **Monte a tira você mesmo,** alinhando pelos pés (§5.3).
- **Guarde a saída bruta fora do repositório,** numa pasta local ou num drive compartilhado. No repositório entra só o arquivo final.

---

## 4. Bíblia de estilo e cor

### 4.1 Visual-alvo e precedência

- **Referência:** tactics isométrico de SNES. A imagem de referência é um quadro nativo de 224×224 ampliado 2×. Contraste alto; a sala fica centralizada sobre preto puro.
- **Tela do jogo:** quadro nativo de **384×216**, ampliado **5×** com Nearest Neighbor para 1920×1080. A escala do mundo é a da referência: losango de chão de 32×16 e célula de 32×32 para personagem e objeto. Uma sala de até 14 células (largura + altura) ocupa 224 px dos 384, e o HUD fica nos cantos, como pede o `target_style.md`. A arte é sempre entregue em 1×; a ampliação é da câmera (§11).
- **Sombras:** frias (azul-acinzentadas) no mármore, na pedra, na rocha, na neve e na água. O mármore branco da arquitetura usa as sombras frias da palette v2 (`marble_cold_shade`, `marble_cold_dark`). Nos materiais quentes (pele, terracota, terra, tijolo, madeira), a sombra é o tom escuro do próprio material (§4.6). A saturação vem da chave de cor de cada arquivo, não do prompt.
- **Hierarquia de contraste:** debatedores > NPCs > props > chão.
  - O chão tem tons próximos em valor e junta sutil, um tom abaixo do tom dominante do material: os tiles se emendam e só uma linha leve marca a grade.
  - Os debatedores têm contorno fechado e as cores mais saturadas.
  - Os cidadãos usam só famílias neutras.
- **Fundo preto:** o preto `void` `#000000` é a cor de limpeza da câmera e a da névoa (`fog.png`). Todo sprite sai com fundo **transparente**. Nunca peça "black background": esses pixels viram contorno opaco na quantização.
- **Fora da arena:** na batalha, o mundo fora da arena fica escurecido por um xadrez regular de 50% de `void` por cima dos tiles e dos atores. É efeito de código (§11), não arte: nenhum tile tem versão escurecida, e nenhum overlay desenhado faz esse papel.
- **Bordas do mapa:** o tom mais escuro de cada material não pode descer abaixo do nível `*_dark`, senão o contorno de props na borda (copa de oliveira, parede do fundo) some contra o preto e contra o xadrez de `void`.
- **Precedência em caso de conflito:**
  1. `art/reference/target_style.md`;
  2. `art/palette/snes.gpl`, com os acréscimos da palette v2 (§4.2);
  3. as regras da skill `pixel-art-pipeline`;
  4. GDD §5 a §9;
  5. cards ART-03 e ART-04.

### 4.2 Paleta mestra (`art/palette/snes.gpl`, palette v2, RGB555)

Todos os canais são múltiplos de 8. O arquivo atual tem 33 entradas e 32 cores únicas, porque `ui_border` repete `marble_light`. A **palette v2** (decisão do usuário de 21/09/2026) acrescenta 11 entradas, listadas na segunda tabela: são 44 entradas e 43 cores únicas. A última coluna é a palavra que vai no prompt: o modelo não conhece os nomes dos tokens, e "marble light glyph" pede um glifo de mármore. O valor exato é imposto pela `color_image` e pela quantização, nunca pelo texto.

| Nome | Hex | Papel | No prompt |
|---|---|---|---|
| `outline` | `#080810` | Contorno de personagens, props e ícones; olhos | near-black |
| `outline_blue` | `#101828` | Contorno dos objetos soltos de mármore e pedra (coluna, herma); sombra no chão sob o personagem (proposta). Não é junta de chão nem de parede | dark blue outline |
| `marble_light` | `#F8F8F0` | Mármore iluminado, roupa branca, brilho, papiro | off-white |
| `marble_mid` | `#D8D8C8` | Mármore base, linho, barba branca na sombra, remendo | pale grey-beige |
| `marble_shade` | `#A8A8A0` | Pedra cinza, lã crua, prata, sombra de tecido branco e de papiro | light grey |
| `marble_dark` | `#787880` | Oclusão e juntas frias da pedra, lã suja, grisalho escuro | cool dark grey |
| `ochre_light` | `#F8C070` | Mármore ocre, argila seca, reflexo de terracota | light sand-ochre |
| `ochre_mid` | `#D89038` | Terracota, cera, madeira iluminada, terra da estrada | warm orange-ochre |
| `ochre_shade` | `#A05828` | Terra batida, madeira, sombra de terracota, sandálias | rust brown |
| `ochre_dark` | `#683018` | Pedrinhas, trincas, juntas de tijolo, oclusão quente | dark brown |
| `aegean_light` | `#48C8E0` | Quíton da Kleia, brilho da Coerência, reflexo de água, vapor | bright cyan, sky-blue |
| `aegean_mid` | `#2890C0` | Sombra do quíton da Kleia, barra de Coerência, água, meandro | sea blue |
| `aegean_shade` | `#186088` | Água funda, exômide do Bátilo, sombra de neve | deep sea blue |
| `aegean_dark` | `#103050` | Borda de água, sombra funda de neve | navy blue |
| `olive_light` | `#A8C858` | Folhagem iluminada, tufos, murta, louro | yellow-green |
| `olive_mid` | `#789838` | Grama base, copa | olive green |
| `olive_shade` | `#486820` | Sombra de folhagem | dark olive green |
| `red_light` | `#F87858` | Luz do peplos da Damaris, alcance de REFUTAR, brasa | coral red |
| `red_mid` | `#C03828` | Damaris, óxido nas mãos da Kleia, toldo, pigmento, ícone de falácia informal | brick red |
| `red_dark` | `#781820` | Sombra do peplos da Damaris e da moldura de falácia informal | dark maroon |
| `purple_light` | `#C078E0` | Luz do himátion do Thrasybios, alcance de MAIÊUTICA | lilac |
| `purple_mid` | `#8838A0` | Thrasybios, véu da Pítia, ícone de falácia formal | royal purple |
| `purple_dark` | `#501868` | Sombra do himátion e do véu | dark purple |
| `gold_light` | `#F8E058` | Brilho da Plateia, cursor, borda grega, moedas | bright yellow |
| `gold_mid` | `#D8A818` | Plateia, bronze iluminado, palha, açafrão | golden yellow |
| `gold_dark` | `#906008` | Bronze, sombra do ouro | dark bronze |
| `skin_light` | `#F8D0A8` | Pele clara (base) e luz das outras peles | fair skin |
| `skin_mid` | `#D8A078` | Sombra da pele clara, base da pele de sol | sun-tanned skin |
| `skin_shade` | `#A87048` | Oclusão da pele clara, base da pele trácia | brown skin |
| `hair_dark` | `#382828` | Cabelo escuro, verniz negro da cerâmica | near-black brown |
| `hair_mid` | `#604030` | Cabelo castanho, couro, tronco, xale escuro | dark brown |
| `ui_background` | `#181828` | Fundo das caixas atuais; a UI nova usa `void` | — |
| `ui_border` | `#F8F8F0` | Borda das caixas atuais (igual a `marble_light`); a UI nova usa `ui_white` | — |

**Acréscimos da palette v2** (decisão do usuário de 21/09/2026). As cores que a referência pede e a paleta não tinha **entram no arquivo**, em vez de serem trocadas pela vizinha. Os hex de `void` e das três cores da UI são os da referência (`target_style.md`). Os das rampas novas são propostos aqui, já em RGB555, e ficam valendo até o `snes.gpl` ser reescrito (§11, §12.2):

| Nome | Hex | Papel | No prompt |
|---|---|---|---|
| `void` | `#000000` | Vazio fora do mapa (cor de limpeza da câmera), névoa (`fog.png`), fundo das caixas da UI e o xadrez de 50% fora da arena, que é código | — |
| `ui_white` | `#F8F8F8` | Texto e borda de 1 px das caixas, cursor do menu | — |
| `ui_gold` | `#F8D830` | Plaqueta de nome e a seta dela, com contorno e letras em `ochre_dark` | — |
| `ui_blue` | `#3868D8` | Borda dos slots (`frame_slot`) e da caixa de itens (`box_items`) | — |
| `move_light` | `#C8F058` (proposta) | Preenchimento do alcance de CIRCULAR (`hl_move`) | — |
| `move_mid` | `#88C030` (proposta) | Junta do alcance de CIRCULAR | — |
| `clay_light` | `#F8A870` (proposta) | Luz da terracota e da argila crua | light terracotta |
| `clay_mid` | `#D87848` (proposta) | Terracota das ânforas, do forno e da clepsidra; argila do torno e do monte | terracotta-orange |
| `clay_shade` | `#A84830` (proposta) | Sombra da terracota, tijolo cozido do forno | deep terracotta |
| `marble_cold_shade` | `#A0A8B8` (proposta) | Sombra do mármore branco da arquitetura: face direita das paredes, colunas, degraus, herma, veio do `floor_marble` | pale blue-grey |
| `marble_cold_dark` | `#687088` (proposta) | Oclusão do mármore branco da arquitetura | slate blue-grey |

- **Sai de uso:** `#101020`, a cor da névoa e da sombra dos placeholders, não entra na palette v2. A névoa passa a `void`, e a sombra no chão continua em `outline_blue` (§5.3).
- **Cores só da UI e do alcance:** `void`, `ui_white`, `ui_gold`, `ui_blue`, `move_light` e `move_mid` nunca vão em sprite do mundo nem em prompt do PixelLab. As peças que as usam são `[MANUAL]`.
- **Contagem:** as cores novas entram no limite de 15 + transparente por arquivo, como as outras (§4.5).

Nos roteiros `[MANUAL]`, `[SWAP]` e `[EDIT]`, que são para o Aseprite, os tokens aparecem com o nome exato (`red_mid`), porque ali a precisão é o que importa.

### 4.3 Luz: superior esquerda (ART-03 #2)

- **Blocos, paredes e props:** o topo é a face mais clara (light). A face esquerda leva o mid, a face direita leva o shade, e juntas, base e oclusão levam o dark.
- **Chão:** tudo é topo. A base é o tom claro do material, a textura usa o mid, os detalhes (veio, trinca, pedrinha) usam shade ou dark, e a junta é `outline_blue`.
- **Personagens:** o brilho fica no alto da cabeça, no ombro esquerdo e no lado esquerdo do rosto e do tecido. A sombra fica sob o queixo, sob os braços, no lado direito e na barra da roupa. Metal leva no máximo 1 px de brilho especular.
- **Espelho:** SW e NW são SE e NE espelhados, o que leva a luz para cima e à direita nessas direções. A troca é aceita, como nos tactics da época. Para disfarçar, concentre o contraste no eixo vertical (topo claro, base escura) e limite a assimetria esquerda/direita a uma coluna de pixels.
- **Proibido:** rim light, glow, bloom, luz colorida e sombra projetada. A única sombra é a elipse fixa sob o personagem (§5.3).

### 4.4 Contorno: 1 px escuro e seletivo

- **Espessura e silhueta:** sempre 1 px, nunca 2, com a silhueta externa **sempre fechada**.
- **Onde usar `outline`:** embaixo (pés), no lado da sombra (direita), em volta da cabeça e onde a figura encosta no chão. Nas bordas iluminadas (topo e esquerda), o contorno pode usar o tom mais escuro do material vizinho: é o "selective outline".
- **Linhas internas** (braço contra tronco, dobra, cinto) usam o tom escuro do próprio material, nunca preto. As exceções são olhos, boca e mão sobre roupa da mesma cor.
- **Quem leva `outline #080810`:** personagens, retratos, ícones, props de madeira, terracota, tijolo, bronze e vegetação.
- **Quem leva `outline_blue #101828`:** os objetos soltos de mármore e pedra: colunas, herma, tesouro de Delfos.
- **Junta sutil, sem contorno escuro:**
  - Os tiles de chão, **só nas duas bordas de cima** (NO e NE), no tom da rampa logo abaixo do tom dominante do material (`marble_light` → `marble_mid`, `olive_light` → `olive_mid`, `ochre_mid` → `ochre_shade`). As de baixo usam a cor do corpo, porque o vizinho da frente desenha essa linha.
  - As paredes e degraus (`wall_*`, `block_step`), no tom mais escuro do material (`ochre_dark` no tijolo, `marble_cold_dark` no mármore), para paredes vizinhas virarem um muro contínuo.
- **Ponta esquerda do chão:** o pixel `x = 0` da linha 8 (a nona a partir do topo) precisa ter a cor exata da junta. O `FloorTileSeams` usa esse pixel para achar a junta e só a procura na borda de baixo do losango.
- **Blocos:** desenhe a junta completa. O `BlockTileSeams` lê a cor dela no pixel mais alto da coluna 0 e apaga sozinho a borda vertical direita.
- **Sem contorno:** vapor, névoa, realces de alcance, fogo e efeitos.

### 4.5 Tons, dithering, limite de cores e transparência

- **Tons:** 3 a 4 por material (light, mid, shade, dark), em blocos chapados com degrau duro. As famílias de 3 tons (oliva, vermelho, roxo, ouro, pele) usam o contorno ou um dark vizinho como quarto nível.
- **Dithering:** nunca em sombreado. A única exceção é o **xadrez regular de 50%** para translucidez: o véu da Pítia (`marble_light` × `purple_mid`), o `overlay_vapor`, a borda da névoa e a metade de baixo dos espectros. O xadrez é desenhado à mão, depois da geração. O escurecimento fora da arena também é um xadrez de 50%, de `void`, mas é aplicado pelo código em cima do mapa, nunca desenhado. A quantização roda sempre com dithering **desligado**.
- **Limite de cores:** no máximo **15 cores + transparente por arquivo**. O contorno e a sombra no chão entram na conta. Todas as tiras de um personagem usam a mesma chave de cor. O limite é conferido na quantização: o modelo não sabe contar cores, então isso não vai no prompt.
- **Transparência binária:** alfa 0 ou 255, nenhum pixel semitransparente.
- **Flash de dano:** não se desenha. É um shader que troca a paleta inteira por `marble_light` (ART-07 #5).

### 4.6 Rampas por material (luz → escuro)

| Material | Luz / topo | Base | Sombra | Escuro / junta | Regra |
|---|---|---|---|---|---|
| Mármore branco | marble_light | marble_mid | marble_cold_shade | marble_cold_dark | Contorno `outline_blue`. Veio curvo de 1 px em marble_cold_shade. Coluna dórica com 5 caneluras de 1 px. Sombras frias da palette v2 |
| Mármore ocre | ochre_light | ochre_mid | ochre_shade | outline_blue | Alterna com o branco em xadrez |
| Mármore azul (Academia) | marble_light | marble_mid | aegean_mid (veio) | outline_blue | |
| Meandro (`floor_mosaic`) | marble_light | marble_mid | aegean_mid (grega) | outline_blue | Continua no vizinho sem emenda |
| Pedra cinza | marble_mid | marble_shade | marble_dark | outline_blue | Sombra fria |
| Rocha de Delfos | marble_shade | marble_dark | aegean_shade | aegean_dark / outline_blue | Cinza-azulada, alto contraste |
| Neve | marble_light | marble_mid | aegean_shade | aegean_dark | Sombra sempre azul |
| Terracota e figuras vermelhas | clay_light | clay_mid | clay_shade | ochre_dark | Rampa da argila (palette v2). Verniz negro em hair_dark com núcleo outline. Figuras vermelhas são argila reservada: 2 a 3 px de clay_mid sobre a faixa negra |
| Argila crua (torno, monte, potes) | clay_light | clay_mid | clay_shade | ochre_dark | A mesma rampa da terracota |
| Tijolo cru (`wall_brick`) | ochre_light (topo) | ochre_mid (face esquerda) | ochre_shade (face direita) | ochre_dark (juntas de 1 px) | Contorno outline |
| Forno (`block_kiln`) | clay_mid | clay_shade | ochre_shade | ochre_dark | Tijolo cozido na rampa da argila, mais quente que o tijolo cru. Brasa com núcleo gold_light e borda red_light |
| Terra da estrada (`floor_dirt`) | ochre_mid | ochre_shade (sulcos) | ochre_dark (pedrinhas) | outline_blue | |
| Terra batida (`floor_packed_dirt`) | ochre_mid (grãos) | ochre_shade | ochre_dark (pedras) | outline_blue | Mais escura que a estrada |
| Argila seca (`floor_dry_clay`) | ochre_light | ochre_mid (manchas) | ochre_dark (até 2 trincas) | outline_blue | A mais clara das três terras |
| Palha (`floor_straw`) | gold_light (1 a 2 px) | ochre_shade (terra) | gold_mid (riscos de 3 px) | outline_blue | |
| Grama | olive_light (tufos de 2 px) | olive_mid | olive_shade | outline_blue | |
| Oliveira | olive_light + marble_shade (verso prateado) | olive_mid | olive_shade | Tronco em hair_mid, hair_dark e ochre_dark | Tronco retorcido |
| Água | aegean_light (reflexo) | aegean_mid | aegean_shade | aegean_dark (borda) | Espuma de 1 px em marble_light |
| Madeira | ochre_mid | ochre_shade | ochre_dark | outline | O bastão de oliveira leva nós em hair_mid |
| Toldo listrado | marble_light | red_mid | red_dark | outline | Listras de 2 px |
| Pele clara (A) | skin_light | skin_mid | skin_shade | outline | |
| Pele de sol (B) | skin_light (1 px de luz) | skin_mid | skin_shade | outline | |
| Pele trácia (C) | skin_mid | skin_shade | hair_mid | outline | |
| Cabelo escuro | hair_mid | hair_dark | outline | — | |
| Cabelo castanho | ochre_shade | hair_mid | hair_dark | outline | |
| Cabelo grisalho | marble_mid | marble_shade | marble_dark | outline | |
| Cabelo branco | marble_light | marble_mid | marble_shade | outline | |
| Tecido azul-egeu (Kleia) | aegean_light | aegean_mid | aegean_shade | outline | |
| Tecido azul-marinho (Bátilo) | aegean_mid | aegean_shade | aegean_dark | outline | |
| Tecido púrpura (Thrasybios) | purple_light | purple_mid | purple_dark | outline | |
| Tecido terracota (Damaris) | red_light | red_mid | red_dark | outline | |
| Tecido açafrão (Simo) | gold_light | gold_mid | gold_dark | outline | |
| Tecido mostarda (Óron) | ochre_light | ochre_mid | ochre_shade | outline | |
| Tecido branco limpo | marble_light | marble_mid | marble_shade | outline | Manto `linen` do cidadão (§8.2) |
| Lã crua suja | marble_mid | marble_shade | marble_dark | outline | Manto `grey` do cidadão |
| Tecido marrom de trabalho | ochre_mid | ochre_shade | ochre_dark | outline | Manto `brown` do cidadão |
| Xale escuro | ochre_shade | hair_mid | hair_dark | outline | |
| Bronze | gold_light (1 px) | gold_mid | gold_dark | ochre_dark | Mais escuro que o ouro |
| Ouro | gold_light | gold_mid | gold_dark | — | Numa figura de 24 px, a grega vira linha tracejada gold_light/gold_mid |
| Prata | marble_light | marble_shade | marble_dark | — | |
| Papiro | marble_light | marble_mid | marble_shade | — | Fita em gold_light |
| Cera (tabuinha) | ochre_mid | ochre_shade (moldura) | ochre_dark (riscos) | — | |

- **Sombras frias do mármore:** valem para o mármore branco da arquitetura e das peças de pedra lavrada (chão, paredes, colunas, degraus, herma, fonte, ônfalo, tesouro de Delfos, parede do tribunal). O tecido branco, o papiro, a prata e o cabelo grisalho continuam com `marble_shade` e `marble_dark`, que são mais quentes.
- **Rampa da argila:** vale para a terracota e a argila crua dos props e do forno. Os chãos de terra e de argila seca do Cerâmico continuam em ocre, mais apagados, para a ânfora se destacar do chão (hierarquia da §4.1). A ânfora que um personagem carrega ou pinta (Kleia no `paint`, a aluna, o cidadão jovem) fica na chave de cor dele, em ocre.

### 4.7 Escala e alturas no canvas de 32×32

- **Corpo:** cerca de 16 px de largura. Um adereço pode ir até a borda da célula.
- **Cabeça:** 7 a 8 px, cerca de 3 cabeças de altura.
- **Pés:** a última linha dos pés é a **linha 29** (contando a partir de 0 no topo). As linhas 30 e 31 são da sombra (§5.3).

| Personagem | id | Altura | Trilha |
|---|---|---|---|
| Kleia de Cerâmico | `kleia` | 24 px (cabeça ~8 px) | A |
| Aristeu | `aristeu` | 23 px | A |
| Menexeno | `menexeno` | 22 px curvado; o bastão chega a 27 px | A |
| Cidadão (base) | `citizen` | 24 px | A |
| Damaris de Abdera | `damaris` | 24 px | A |
| Eudoro | `eudoro` | 25 px, esguio | A |
| A Pítia | `pythia` | cerca de 26 px com a trípode (sentada) | A |
| Acadêmico do selo (I.10) | `academic` | 24 px | A |
| Irmão Dídimos A | `didymos_a` | 24 px | A |
| Cidadão idoso (variante) | `citizen_elder` | 23 px | A |
| Cidadã (variante) | `citizen_woman` | 23 px | A |
| Cidadão jovem (variante) | `citizen_youth` | 25 px | A |
| Thrasybios | `thrasybios` | 26 px (o mais alto) | B |
| Óron, o cambista | `oron` | 22 px, baixo e largo | B |
| Phaidra, a adivinha | `phaidra` | 23 px, curvada | B |
| Kritolaos, o político menor | `kritolaos` | 25 px | B |
| Simo, o ator | `simo` | 25 px com o coturno | B |
| Bátilo, o pescador | `batilo` | 25 px, o mais largo | B |
| Zenão de Eleia | `zeno` | 25 px | B |
| Eubúlides | `eubulides` | 24 px | B |
| Aglaofonte | `aglaophon` | 24 px | B |
| Heráclito (espectro) | `heraclitus` | 25 px, flutuando 2 px acima do chão | B |
| Parmênides (espectro) | `parmenides` | 25 px, flutuando 2 px acima do chão | B |
| A aluna (finais) | `student` | cerca de 15 px sentada | B |

### 4.8 Cores reservadas e códigos do sistema

| Família | Reservada para |
|---|---|
| aegean | Kleia, Coerência |
| move (palette v2) | Só o alcance de CIRCULAR (`hl_move`). Nenhum sprite usa |
| gold | Plateia, cursor, UI |
| red | Damaris, REFUTAR |
| purple | Thrasybios, MAIÊUTICA, Pítia |
| void e ui (palette v2) | Só névoa, UI e efeitos de código (§4.2). Nenhum sprite do mundo usa |

- **Cidadãos e NPCs genéricos** não usam nenhuma dessas famílias.
- **Oponentes:** podem usar pequenos acentos dessas famílias (a balança de bronze do Óron, as contas roxas da Phaidra, o açafrão do Simo), nunca como cor dominante.
- **Chão:** o tom base do figurino não pode ser o material do chão onde o personagem fica parado.

| Elemento | Cores |
|---|---|
| Coerência | aegean_mid, com brilho aegean_light |
| Plateia | gold_mid, com brilho gold_light |
| Alcance de CIRCULAR | move_light com junta move_mid: verde-amarelado em dois tons opacos, como na referência (decisão de 21/09/2026). Não disputa o azul com o quíton da Kleia |
| Alcance de REFUTAR / MAIÊUTICA | red_light com junta red_mid / purple_light com junta purple_mid, no mesmo esquema de dois tons opacos |
| Interface | Caixas em `void` com borda e texto em `ui_white`; plaqueta de nome em `ui_gold` com contorno e letras em `ochre_dark`; slots e caixa de itens com borda `ui_blue` |
| Ícone de falácia | Moldura `icon_badge_*` em red_mid (informal), purple_mid (formal) ou aegean_mid (paradoxo), com glifo marble_light de até 10×10 e contorno outline, como no `FallacyIconPainter` |

### 4.9 Estilo curto e ordem do prompt

Todo prompt gerado segue esta ordem: **`<assunto e ação>, <bloco de tipo>, <estilo curto>`**. O assunto vem primeiro porque é o que o modelo mais pesa; o estilo vem por último e curto, porque quem impõe o estilo de verdade são os parâmetros (`outline`, `shading`, `detail`, `view`), a `color_image` e as imagens de referência.

Há dois estilos curtos, cada um com menos de 25 palavras.

`[STYLE_CHAR]`, para personagens:
```text
16-bit SNES tactics RPG sprite, crisp hard pixels, 3-4 flat tones per material, light from upper left, 1-pixel dark outline, Classical Athens 400 BC
```

`[STYLE_ENV]`, para chão, blocos, props, ícones e efeitos:
```text
16-bit SNES tactics RPG pixel art, crisp hard pixels, 3-4 flat tones per material, light from upper left
```

- **Sem assunto no estilo.** O `[STYLE_ENV]` não fala de Atenas, de roupa, de cerâmica nem de arquitetura: essas palavras puxariam colunas e ânforas para dentro de um tile de grama. O contorno fica no bloco de tipo, porque o chão é `lineless` e o prop tem contorno fechado.
- **Nada que o parâmetro já faz.** Não escreva "transparent background" (é o `no_background`), nem um número de cores (o modelo não conta cores; o limite é da quantização).
- **Sombra fria só onde ela existe.** "cool blue-grey shadows" aparece só nos prompts de pedra, rocha, neve e, desde a palette v2, da arquitetura de mármore branco: `block_broken_column`, `block_cliff_rock`, `block_delphic_treasury`, `block_doric_column`, `block_portico`, `block_step`, `floor_court_stone`, `floor_mountain_rock`, `floor_rift`, `floor_snow`, `floor_stone`, `prop_fountain`, `prop_omphalos`, `prop_statue_base`, `wall_court`, `wall_marble`. Em pele, argila, terra e madeira, o modelo pintaria azul na sombra, e com `force_colors` numa chave sem egeu esses pixels cairiam em `marble_dark` ou no contorno. Nos props de pedra cinza lisa (altar, bema, clerotério) a sombra continua sendo a da rampa da pedra, sem a frase.
- **Cor em palavras comuns** (tabela da §4.2). "brick red", nunca "red mid".

### 4.10 Blocos por tipo de asset

Todo bloco que descreve um objeto no mundo usa a mesma frase de câmera, `2:1 isometric pixel-art camera, 30 degree elevation`, e a `view` é a mesma em todas as ferramentas (§3.2). Assim, o disco do torno e a bacia da fonte saem no mesmo ângulo do losango do chão.

`[CHARACTER]`:
```text
single full-body character, 32x32 pixel sprite, resting on a flat ground line 2 pixels above the bottom edge, body centered, head about 8 pixels tall, 2:1 isometric pixel-art camera, 30 degree elevation,
```

A direção não fica no bloco. Os prompts de criação trazem `facing south in front view` (o sprite sul, que depois é rodado) ou já a pose sentada virada para SE. Os prompts de animação começam pela frase de direção do arquivo:
- `_se` → `Facing south-east in 3/4 front view:`
- `_ne` → `Facing north-east in 3/4 back view, back turned to the viewer, face hidden:`

Por isso a tira SE e a tira NE de uma mesma animação têm prompts diferentes. O texto NE nunca cita boca, olhos, sobrancelhas, sorriso ou olhar: o gesto é de cabeça, ombro, mão ou adereço (§5.4).

`[FLOOR]`:
```text
flat isometric floor tile, top face only, a 2:1 diamond 32 pixels wide and 16 pixels tall, seamless with its four neighbours, a subtle 1-pixel joint line one shade darker than the surface along the two upper edges, blending into its neighbours, 2:1 isometric pixel-art camera, 30 degree elevation,
```

`[BLOCK]`:
```text
single isometric block on one 32x16 floor cell, its base diamond on the bottom edge of the canvas, straight vertical faces, lightest top face, mid-tone left face, darkest right face, closed 1-pixel outline, 2:1 isometric pixel-art camera, 30 degree elevation,
```

`[PROP]`:
```text
single isometric object standing on one 32x16 floor cell, its footprint inside the base diamond at the bottom of the canvas, free to rise above the cell, closed 1-pixel outline, 2:1 isometric pixel-art camera, 30 degree elevation,
```

`[GLYPH]`, para o glifo que vai sobre a moldura do ícone de falácia:
```text
single flat symbolic glyph, one bold off-white shape at most 10 by 10 pixels centered on a 16x16 canvas, 1-pixel dark outline, readable at 1x,
```

`[ITEM]`:
```text
single small object icon filling a 16x16 canvas, readable at 1x, 1-pixel dark outline,
```

`[FX]`:
```text
small flat visual effect sprite on a 16x16 canvas, lineless flat shapes in 2 to 4 colors, bold readable silhouette over any floor,
```

Marcadores que **não vão ao PixelLab como texto**:

| Marcador | Significa |
|---|---|
| `[MANUAL]` | Desenho à mão no Aseprite. O texto é o roteiro: siga tamanhos e cores pixel a pixel |
| `[SWAP]` | Troca de paleta de um arquivo aprovado: mude só o que está listado, todo o resto fica idêntico |
| `[EDIT]` | Edição de um arquivo aprovado no Aseprite (deslocar linhas, estender uma parede, recortar uma porta, ciclar cores), com zero generations |
| `[PORTRAIT]` | Receita do `portrait-character-pro`: qual imagem entra e o que conferir no resultado. O texto depois de "Inpaint fix", quando houver, é o `description` de um `inpaint-v3` de correção |
| `[FACE]` | Receita do `inpaint-v3`: qual retrato, onde vai a máscara. O texto depois de "Description:" é o `description` |

### 4.11 Negativos

Nenhum prompt deste documento usa negação. As ferramentas `v3` e Pro não têm campo de negativo, e uma negação no texto positivo costuma **acrescentar** o objeto: "No weapon" desenha uma arma. Por isso:
- o que se quer é escrito em forma positiva: "empty open hand with the index finger extended", "bare empty hands at her sides", "plain and unadorned", "blue-white snow";
- o que o pós-processamento já remove (sombra projetada, flash de dano, fundo) não aparece no prompt;
- os limites históricos vão dentro dos descritores, também em forma positiva: "knee-length chiton, bare legs, leather thong sandals, rectangular wool himation draped over the left shoulder", "ankle-length peplos pinned at the shoulders".

O campo `negative_description` só existe, entre as ferramentas usadas aqui, no `create-image-bitforge` (o do `create-image-pixflux` está descontinuado, e as ferramentas antigas `inpaint` e `animate-with-text` não são usadas). A lista abaixo vai **só** nesse campo, nas linhas `bitforge+init`:

```text
anti-aliasing, blur, smooth edges, soft shading, gradient, airbrush, dithering, noise texture, painterly, watercolor, 3d render, photorealistic, glow, bloom, rim light, lens flare, drop shadow, cast shadow, semi-transparent pixels, jpeg artifacts, pastel, sepia, black background, background scenery, border, frame, text, letters, numbers, watermark, signature, logo, user interface, duplicate, cropped, modern building, concrete, glass, Roman arch, medieval, fantasy
```

REFUTAR é dedo apontado, nunca golpe: não use nenhum template de ataque com arma.

### 4.12 Onde colar cada parte

| Ferramenta | Campo | O que colar |
|---|---|---|
| Criar a Kleia (`create-character-with-8-directions`) | `description` | O prompt de A-001 expandido: descritor + pose + `[CHARACTER]` + `[STYLE_CHAR]` |
| Sprite sul do elenco (`generate-with-style-v2`, `create-character-pro-flash`) | `description` | O prompt da tira idle SE, expandido |
| Rotação (`create-character-with-8-directions` com `directions.south`, `create-character-v3`) | `description` | O mesmo texto do sprite sul |
| Base sentada (`generate-with-style-v2`) | `description` | O prompt da tira idle SE, expandido |
| Edição (`edit-image-pro-flash`) | `description` | O prompt inteiro (`paint` da Kleia) |
| Animar (`animate-with-text-v3`; na MCP, `animate_character` v3) | `action` / `action_description` | O prompt inteiro da tira: ele já começa pela direção |
| Caminhada (`animate_character`, modo `template`) | `action_description` | O prompt inteiro da tira |
| Retrato neutro (`portrait-character-pro`) | — | Nenhum texto. A linha `[PORTRAIT]` diz a imagem e o que conferir; a correção, se houver, vai no `description` de um `inpaint-v3` |
| Expressão (`inpaint-v3`, `edit-image-pro-flash`) | `description` | Só o texto depois de "Description:" |
| Chão, bloco, prop, glifo, ícone de item, efeito | `description` | O prompt inteiro, expandido |
| `create-image-bitforge` | `negative_description` | A lista da §4.11 |
| Tile animado (`animate-with-text-v3`) | `action` | O prompt inteiro |
| `[MANUAL]`, `[SWAP]`, `[EDIT]` | — | Não vão ao PixelLab: são o roteiro do trabalho no Aseprite |

Os limites de tamanho são 2.000 caracteres no `description` e 1.000 no `action`; os prompts expandidos cabem nos dois. Se precisar cortar, corte o estilo curto: nunca o descritor nem a ação.

---

## 5. Convenções de entrega

### 5.1 Pastas

```text
art/incoming/
  track_a/                 Trilha A
    characters/  portraits/  floor/  blocks/  props/  overlays/  fx/  ui/  icons/
    generation_log.csv
  track_b/                 Trilha B
    characters/  portraits/  floor/  blocks/  props/  fx/  ui/  icons/
    generation_log.csv
```

- **Caminho:** o caminho de cada arquivo é `art/incoming/<track_a|track_b>/<category>/<file_name>`, com `category` e `file_name` tirados do CSV. A coluna `track` do CSV (`A` ou `B`) diz a pasta: `A` vai em `track_a`, `B` vai em `track_b`.
- **Exemplo:** a linha A-012 (`track` = `A`, `category` = `characters`, `file_name` = `kleia_refute_se.png`) é entregue em `art/incoming/track_a/characters/kleia_refute_se.png`.
- **Fonte da UI:** o arquivo da Ark Pixel Font 10px Proportional, variante latin, tirado da release oficial de [TakWolf/ark-pixel-font](https://github.com/TakWolf/ark-pixel-font), e o texto da licença SIL OFL 1.1 vão em `art/incoming/track_a/fonts/`. A amostra `font_ui_specimen.png` vai em `ui/` (§12.1).
- **Nada em `art/placeholders/`.** O `ArtTools` copia essa pasta para a Unity, e o `ArtAssetsTests` conta exatamente 59 PNGs nela: um arquivo a mais quebra o teste. Quem move a arte aprovada para o lugar definitivo é o dev (§11).
- **Saída bruta do PixelLab** (ZIP, quadros soltos, tentativas descartadas) e as chaves de cor de trabalho ficam fora do repositório.

### 5.2 Nomes de arquivo

Nomes em inglês e `snake_case`, sem acento. Nomes próprios seguem os ids do jogo: `kleia`, `oron`, `batilo`, `pythia`.

| Tipo | Padrão | Exemplo |
|---|---|---|
| Tira de personagem | `<personagem>_<animação>_<se\|ne>.png` | `kleia_refute_se.png` |
| Retrato | `portrait_<personagem>_<expressão>.png` | `portrait_kleia_shaken.png` |
| Chão | `floor_<material>.png` | `floor_packed_dirt.png` |
| Parede e bloco | `wall_<material>.png`, `block_<nome>.png` | `block_doric_column.png` |
| Parede alta e porta | `wall_<material>_tall.png`, `wall_<material>_tall_door_<left\|right>.png` | `wall_brick_tall_door_left.png` |
| Prop | `prop_<nome>.png` | `prop_amphora_stack.png` |
| Tira de tile ou prop animado | `<id do estático>_<animação>.png` | `block_kiln_glow.png` |
| Overlay | `fog.png`, `hl_<ação>.png`, `overlay_<nome>.png` | `hl_refute.png` |
| Efeito | `fx_<nome>.png` | `fx_refute_hit.png` |
| Moldura de ícone | `icon_badge_<informal\|formal\|paradox>.png` | `icon_badge_formal.png` |
| Ícone de falácia | `icon_fallacy_<id do catálogo>.png` | `icon_fallacy_straw_man.png` |
| Ícone de item e HUD | `icon_<nome>.png` | `icon_quote_homer.png` |
| UI | `box_`, `bar_`, `frame_`, `meter_`, `cursor`, `font_ui_specimen` | `box_dialogue.png` |

Use o nome **exato** da coluna `file_name`. Um nome diferente não é integrado.

### 5.3 Tiras de animação de personagem

**Formato:**
- PNG RGBA de 8 bits, fundo transparente, na resolução nativa (1×).
- Células de **32×32** lado a lado na horizontal, sem espaço entre elas e sem margem.
- N quadros viram uma tira de **32·N × 32**. O quadro 1 fica à esquerda.

| Exemplo | Quadros | Tamanho |
|---|---|---|
| `kleia_idle_se.png` | 2 | 64×32 |
| `kleia_refute_se.png` | 5 | 160×32 |
| `kleia_maieutics_se.png` | 6 | 192×32 |
| `pythia_trance_se.png` | 8 | 256×32 |
| `oron_defeat_se.png` | 1 | 32×32 |

**Alinhamento** (o mesmo em todos os quadros de todas as tiras do personagem):
- **Pés na linha 29** (contando a partir de 0 no topo), a terceira de baixo para cima. É o pivot `(0,5; 0,0625)`, 2 px acima da base. O pé de apoio nunca sai dessa linha: nem no bob da caminhada, nem no recuo do dano, nem na derrota (aí são os joelhos que ficam na linha 29).
- **Eixo vertical do corpo** entre as colunas 15 e 16, com ±1 px nas poses paradas. Nas ações, o braço pode ir até a borda da célula, mas nada pode ser cortado: se cortar, refaça com uma pose mais fechada.
- **Sombra no chão:** uma elipse fixa em `outline_blue`, igual em todos os quadros. Ela tem 10 px na linha 30 (colunas 11 a 20) e 6 px na linha 31 (colunas 13 a 18). O placeholder usa `#101020`, que não entra na palette v2 (§4.2). Se a sombra vira sprite separado continua em aberto (§12.2).
- **Movimento de 1 px, sempre à mão** (o gerador não controla pixel):
  - Quadro 2 da idle: copie o quadro 1 e suba 1 px as linhas da cabeça, dos ombros e do peito (o roteiro de cada personagem está na linha "Quadros" da idle SE e no `[EDIT]` da idle NE).
  - `idle_breath`: quadros 1 e 4 no lugar, quadro 2 com o peito 1 px acima, quadro 3 com peito e cabeça 1 px acima (roteiro na tira).
  - Caminhada: a cabeça e o tronco sobem 1 px nos dois quadros de passagem do template, os de pernas juntas. O GDD chama esses quadros de "1 e 3" porque conta a partir de 0.
  - Depois da limpeza do `coherence_damage` de 3 quadros, o tronco fica 2 px à esquerda no quadro 2; ajuste à mão se o gerador deslocar mais ou menos.
  - O Animator não usa curvas numéricas (o `ArtAssetsTests` proíbe), então nada disso é feito em código.
- **Loop sem salto:** o último quadro de uma tira em loop leva de volta ao primeiro sem repeti-lo. Por isso os loops são gerados com `last_frame` = quadro 1 e o quadro final repetido é descartado.
- **Exceções:**
  - Os espectros flutuam com o ponto mais baixo na linha 27; a sombra continua nas linhas 30 e 31.
  - A Pítia assenta os pés da trípode na linha 29.
  - A aluna, sentada, assenta o corpo na linha 29.

### 5.4 Direções

- **Gere só SE e NE.** O `ActorView` espelha SW e NW com `flipX` (`FacingRules.IsMirrored`) e toca sempre a linha SE ou NE.
- **O espelho troca de lado os detalhes assimétricos:** o remendo no ombro da Kleia, a mão aberta do Thrasybios, a catarata do Menexeno, o bastão na mão direita. A troca é aceita. O que identifica o personagem fica no eixo central, como a tabuinha no centro do cinto da Kleia.
- **Em NE a figura está de costas (3/4).** O rosto não aparece. O prompt NE começa por `back turned to the viewer, face hidden` e descreve só gestos de cabeça, ombro, mão e adereço: o `talk` NE é um aceno de cabeça com o gesto da mão, o `think` NE é a cabeça que inclina e a mão que sobe ao queixo, o `coherence_damage` NE é a cabeça que sacode para trás com os ombros encolhidos. Se o modelo virar o rosto para a câmera, descarte.
- **A Pítia, o `paint` da Kleia e a aluna são só SE.** Posicione-os no mapa virados para a câmera.

### 5.5 Tiles, blocos, props e overlays

| Tipo | Tamanho | Regra |
|---|---|---|
| Chão | 32×16 | Recorte pelo **alfa do placeholder** (`art/placeholders/tiles/floor/floor_marble.png` é a máscara do losango). A ponta está na linha 8. O contorno `outline_blue` fica só nas duas bordas de cima |
| Parede | 32×32 | 16 px de topo mais 16 px de face. Use o alfa de `wall_brick.png` como máscara |
| Parede alta | 32×48 | 16 px de topo mais 32 px de face, como na referência. Vai **só nas bordas de trás** da sala: a linha de Y máximo (a face direita é a que dá para a sala) e a coluna de X máximo (a face esquerda dá para a sala). Na frente, parede de 32×32 ou nenhuma, para não esconder a sala |
| Porta | 32×48 | Recorte escuro de cerca de 8×20 px na face que dá para a sala: `_door_right` na parede de Y máximo, `_door_left` na de X máximo. É bloco, intransponível (§11) |
| Degrau, bema e banco | 32×24 | 16 px de topo mais 8 px de lateral (`thick tile`) |
| Bloco | 32×(16 + altura) | O losango da base encosta nas últimas linhas. Com pivot Bottom Left, a altura extra cresce para cima |
| Prop | 32×H, até 48×64 | A pegada fica dentro do losango da base. Acima de 32 px de largura, exige código (§11) |
| Overlay | 32×16 | Mesma máscara do chão, opaco, sem contorno |
| Tile ou prop animado | Estático `<id>.png` + tira `<id>_<animação>.png` | O quadro 1 da tira é idêntico ao estático. Hoje o jogo usa só o estático (§11) |

As bordas `fog_edge_ne/nw/se/sw` são **geradas** pelo `SandboxTools` a partir de `fog.png`, que é `void` puro. Não as desenhe. O escurecimento fora da arena de batalha também não é arte: é o código que aplica o xadrez de 50% de `void` sobre as células de fora (§11).

### 5.6 Retratos, ícones, UI e efeitos

- **Retrato:** 32×32, busto em 3/4 virado para a direita, fundo transparente. Ele entra na janela de 32×32 da moldura `frame_portrait` (40×40). O ART-08 #1 pedia retrato de 40×40; o usuário confirmou o de 32×32 dentro da moldura em 21/09/2026.
- **Como se faz:** o neutro sai do quadro 1 da idle SE com `portrait-character-pro` (espelhado se olhar para a esquerda). As outras expressões são inpaint só no rosto do neutro aprovado, o que mantém cabelo, roupa e contorno idênticos.
- **Expressões:** `neutral` e `shaken` para cada personagem. A Pítia usa `neutral` e `trance`. A Kleia soma `determined` e `doubtful` no P3.
- **Mesma chave de cor do sprite.** O retrato não traz cor nova.
- **Ícone de falácia:** 16×16, legível em 1×, sem texto. É a moldura da categoria (`icon_badge_*`, desenhada uma vez) com um glifo de um motivo só, de até 10×10, composto por cima. É desenhado sobre a cabeça do oponente (`FallacyRevealView`) e no compêndio.
- **Ícone de item:** 16×16, um objeto só, até a borda do canvas menos 1 px de contorno.
- **UI:** no tamanho exato da coluna `cell_size`. Caixas e barras são `[MANUAL]`, porque a geometria é exata ao pixel. As cores são as da referência, já na palette v2: fundo `void`, texto e borda `ui_white`, plaqueta `ui_gold`, slots `ui_blue` (§4.8).
- **Texto na UI:** a fonte é a Ark Pixel Font 10px Proportional em 1× (um pixel da fonte = um pixel do quadro de 384×216). As maiúsculas têm 7 px e avanço de cerca de 6 px, e cada linha do menu tem 12 px, contra 8 na referência. As peças que carregam texto (`box_menu`, `box_nameplate`, `frame_slot`, contadores) têm o tamanho final medido na amostra `font_ui_specimen.png`.
- **Efeito:** tira horizontal de 16×16 ou 8×8 por quadro, sem contorno.

### 5.7 Registro de geração

Cada trilha mantém um `generation_log.csv` na própria pasta, com uma linha por geração aproveitada:

```text
id,file_name,date,author,tool,mode,seed,canvas,params,pixellab_id,generations_spent,notes
```

- `params` resume view, outline, shading, detail, `first_frame`/`last_frame` e o que foi usado como referência.
- `pixellab_id` é o `character_id`, `object_id` ou `background_job_id` na conta.

Com isso, a outra pessoa consegue reproduzir ou continuar qualquer asset.

### 5.8 Pós-processamento obrigatório (RGB555)

Nenhum gerador entrega RGB555 limpo, nem com `force_colors`. A ordem é esta (no Aseprite, até existir um validador automático):

1. **Resolução nativa.** Se o export veio ampliado, reduza por fator inteiro com Nearest Neighbor, ou use a ferramenta `unzoom` do PixelLab.
2. **Alfa binário.** Alfa ≥ 128 vira opaco; o resto vira transparente.
3. **Quantização contra a chave do personagem ou a rampa do material,** com dithering **desligado** (*Sprite → Color Mode → Indexed*, com a paleta carregada). O `reduce-colors` do PixelLab com `palette_image` quantiza todos os quadros de uma animação juntos. É obrigatória nas saídas das ferramentas sem `color_image` (§3.4).
4. **Contagem.** No máximo 15 cores + transparente. Se passar, funda as menos usadas na vizinha da chave.
5. **Contorno.**
   - Personagens e props: silhueta fechada, com `#080810` exato.
   - Chão: junta sutil (um tom abaixo do dominante do material) só nas bordas de cima, com o pixel da ponta esquerda na cor da junta.
   - Paredes e degraus: junta no tom mais escuro do material.
6. **Sombra.** Apague a do gerador e cole a elipse padrão (§5.3).
7. **Canvas.** Recorte para o tamanho da coluna `cell_size`, alinhando pelos pés ou pela base, sem reescalar.
8. **Quadros à mão.** Faça os deslocamentos de 1 px (§5.3) e descarte o quadro final repetido dos loops.
9. **Translucidez.** Aplique o xadrez de 50% onde a ficha pede (véu, vapor, espectros).
10. **Tiles.** Monte um 2×2 e um 4×1 e confira o encaixe.

---

## 6. Checklist de aceite por arquivo

Todo arquivo:
- [ ] Nome idêntico ao `file_name` do CSV e salvo na pasta da trilha e categoria certas.
- [ ] Tamanho exato. Nas tiras, a largura é 32 × o número de quadros da coluna `frames` e a altura é 32.
- [ ] Fundo transparente, com alfa só 0 ou 255.
- [ ] Todo pixel opaco está na palette v2 (`art/palette/snes.gpl`, com os acréscimos da §4.2).
- [ ] No máximo 15 cores + transparente, contando contorno e sombra.
- [ ] Contorno de 1 px fechado: `outline` em personagem e prop, `outline_blue` em chão e arquitetura de mármore e pedra.
- [ ] Luz de cima e da esquerda, 3 a 4 tons por material, sem dithering de sombreado. Sombra fria só em mármore da arquitetura, pedra, rocha, neve e água.
- [ ] Silhueta legível em 1×, sobre o chão onde o ator fica e sobre `void`.
- [ ] Nenhuma cor só da UI ou do alcance (`void`, `ui_*`, `move_*`) num sprite do mundo.
- [ ] Cores reservadas respeitadas (§4.8).
- [ ] Sem texto, assinatura, moldura ou fundo.

Personagem:
- [ ] Mesma chave de cor em todas as tiras e nos retratos.
- [ ] Pés na linha 29 em **todos** os quadros, e eixo do corpo entre as colunas 15 e 16 nas poses paradas.
- [ ] Elipse da sombra idêntica em todos os quadros (linhas 30 e 31).
- [ ] Os quadros são os da linha "Seleção", na ordem dela.
- [ ] O quadro 1 de cada tira de ação parte da pose do idle, e o último volta para perto dela (exceto `defeat`, `victory`, `concede`, `withdraw`, `listen`, `invert` e o `argue` dos duelistas, que terminam numa pose segurada).
- [ ] O número de quadros bate com a coluna `frames`, e os quadros estão na ordem de reprodução.
- [ ] Nenhum braço, adereço ou cabelo cortado na borda da célula.
- [ ] A animação em loop não "pula" na volta do último quadro para o primeiro, e o primeiro não se repete no fim.
- [ ] Em NE, nenhum rosto: nem boca, nem olhos, nem a cabeça virada para a câmera.
- [ ] Movimentos de 1 px feitos à mão, sem deslocar os pés.
- [ ] Sem flash branco desenhado.
- [ ] Cidadão: as 3 cores do manto aparecem só no manto (§8.2).

Retrato:
- [ ] Busto virado para a direita, mesma chave do sprite.
- [ ] Expressão: só o rosto mudou em relação ao neutro; cabelo, roupa e contorno idênticos pixel a pixel.

Tile, bloco e prop:
- [ ] Chão: o alfa é idêntico ao do placeholder (losango 32×16), a ponta esquerda `x = 0` na linha 8 está na cor do contorno, e o contorno não aparece na metade de baixo.
- [ ] Encaixe conferido em 2×2 e 4×1.
- [ ] Bloco e prop: a base encosta nas últimas linhas, a pegada fica dentro do losango da célula, e o ângulo bate com o do chão (disco e bacia em elipse 2:1, não em círculo).
- [ ] Tile animado: o quadro 1 da tira é idêntico ao estático, pixel a pixel.

Ícone:
- [ ] Moldura idêntica à `icon_badge_*` da categoria, pixel a pixel.
- [ ] Glifo de um motivo só, dentro do centro de 10×10.

---

## 7. Divisão do trabalho: Trilha A e Trilha B

### 7.1 Princípio

- **Dono único.** Cada arquivo tem exatamente um dono, e **nenhum arquivo aparece nas duas trilhas**.
- **Personagem inteiro com uma pessoa só,** incluindo tiras, retratos e variantes por troca de paleta.
- **O resto é dividido por família de material,** para cada pessoa manter um estilo coerente:
  - a **Trilha A** fica com a Kleia e o círculo dela (Aristeu, Menexeno, cidadãos, Eudoro, Damaris), a Pítia, o acadêmico do selo e os irmãos Dídimos, o Cerâmico e Delfos (terra, tijolo, terracota, rocha, neve) e a interface do sistema (overlays, UI, fonte, os indicadores de 8×8 feitos à mão e os ícones de falácia com as molduras);
  - a **Trilha B** fica com o Thrasybios, os cinco duelistas do Ato I, os filósofos do Ato II (Zenão, Eubúlides, Aglaofonte e os dois espectros), a aluna, a Ágora, a Academia e o Tribunal (mármore, pedra, grama, água, oliveira), os efeitos de 16×16, os ícones de item e habilidade (a mesma técnica dos efeitos: `pixen` em 16×16), a moldura do medidor de jurados e o logotipo.
- **Por que os Dídimos estão na A:** eles não são do círculo da Kleia. Ficam com a A só para as duas trilhas terminarem o P2 juntas. O Dídimos B é troca de paleta do A, então o par inteiro tem um dono só.

| | Trilha A | Trilha B |
|---|---|---|
| **Personagens** (tiras e retratos) | Kleia de Cerâmico; Aristeu; Menexeno; Cidadão (base); Damaris de Abdera; Eudoro; A Pítia; Acadêmico do selo (I.10); Irmão Dídimos A; Irmão Dídimos B; Menexeno do Ato II (selo do júri); Cidadão idoso (variante); Cidadã (variante); Cidadão jovem (variante) | Thrasybios; Óron, o cambista; Phaidra, a adivinha; Kritolaos, o político menor; Simo, o ator; Bátilo, o pescador; Zenão de Eleia; Eubúlides; Aglaofonte; Heráclito (espectro); Parmênides (espectro); A aluna (finais) |
| **Cenário** (arquivos) | Cerâmico (18); Delfos (9) | Ágora (30); Academia (9); Tribunal (10) |
| **Sistema** (arquivos) | Overlays (7); Interface (20); Ícones (23); Efeitos (3) | Ícones (6); Interface (2); Efeitos (4) |

- **Compartilhados.** O asset compartilhado entre mapas tem um dono só:
  - `floor_dirt`, `wall_brick` e `wall_brick_tall` são da Trilha A e também entram na Ágora;
  - `prop_olive_trunk` é da Trilha B e também entra no Cerâmico;
  - `block_scroll_shelf` é da Trilha B e também serve ao arquivo do tribunal (I.9);
  - o Tribunal reaproveita `block_doric_column`, `block_step` e `prop_bema_stone`, todos da Trilha B.

### 7.2 Esforço por trilha e prioridade

| Prioridade | A: arquivos | A: quadros | A: pontos | B: arquivos | B: quadros | B: pontos | Diferença |
|---|---:|---:|---:|---:|---:|---:|---:|
| P1 | 106 | 250 | 278 | 111 | 229 | 273 | 2% |
| P2 | 105 | 214 | 211 | 97 | 169 | 215 | 2% |
| P3 | 41 | 91 | 97 | 36 | 58 | 94 | 3% |
| **Total** | 252 | 555 | 585 | 244 | 456 | 582 | 1% |

- **Equilíbrio:** a última coluna é a diferença de pontos entre as trilhas. As duas terminam o P1 juntas, e depois o P2 e o P3, sem uma esperar a outra.
- **Prioridades:**
  - **P1:** fatia vertical do Ato I (Cerâmico, Ágora, Thrasybios, os cinco duelistas), a interface que o jogo já usa, as molduras de ícone e as paredes altas das salas.
  - **P2:** Ato II (Academia, Delfos, elenco novo), o HUD da referência e a fonte, que dependem de código, e as portas.
  - **P3:** Tribunal e acabamento.
- **Ordem de trabalho:** cada pessoa termina o P1 inteiro antes de abrir o P2.

### 7.3 Dependências entre as trilhas

| Quem espera | O quê | De quem |
|---|---|---|
| As duas trilhas, desde o piloto | Palette v2 (§4.2) no `snes.gpl`; até lá, as chaves de trabalho usam os hex da §4.2 | Dev (§11) |
| B-003 em diante (todo personagem da B) | Quadro 1 aprovado de `kleia_idle_se.png` (A-001), a âncora de estilo | Trilha A |
| Todo personagem da A depois do piloto | Quadro 1 aprovado de `thrasybios_idle_se.png` (B-003), a segunda imagem de estilo | Trilha B |
| Todo chão da A | `floor_marble` e `floor_marble_ochre` aprovados (B-001 e B-002), como referência de densidade e de junta, e como `style_images` quando o chão for feito com `create-tiles-pro` | Trilha B |
| Ágora (mapa da B) | `floor_dirt`, `wall_brick` e `wall_brick_tall` da A, compartilhados | Trilha A |
| Cerâmico (mapa da A) | `prop_olive_trunk` da B, compartilhado | Trilha B |
| `prop_ancient_olive_tree`, `floor_garden_grass` | `prop_olive_trunk` e `floor_grass` aprovados, como estilo | A própria Trilha B |
| Portas | A parede alta aprovada do mesmo material | A própria trilha |
| Dídimos B | Dídimos A aprovado | A própria Trilha A |

### 7.4 Ritual

- **Piloto conjunto:** §3.5.
- **Sincronização semanal:** cada pessoa mostra os arquivos em `review`, e a outra aprova ou devolve com o item do checklist que falhou.
- **Mudança de configuração:** qualquer mudança de parâmetro (§3.2), de chave de cor ou de altura é combinada e escrita neste documento antes de valer.

---

## 8. Catálogo de animações

### 8.1 Todas as animações

| Animação | ms por quadro | Loop | Uso | Como se faz | Quem tem (quadros) |
|---|---:|---|---|---|---|
| `idle` | 600 | sim | parado na exploração e fora do turno | rotação do personagem + quadro 2 à mão | kleia (2), aristeu (2), menexeno (2), citizen (2), damaris (2), eudoro (2), pythia (2), academic (2), didymos_a (2), citizen_elder (2), citizen_woman (2), citizen_youth (2), thrasybios (2), oron (2), phaidra (2), kritolaos (2), simo (2), batilo (2), zeno (2), eubulides (2), aglaophon (2), heraclitus (2), parmenides (2), student (2) |
| `walk` | 150 | sim | exploração e CIRCULAR; bob de 1 px nos quadros de passagem | template `walking-4-frames` + bob à mão | kleia (4), menexeno (4), citizen (4), damaris (4), eudoro (4), didymos_a (4), citizen_elder (4), citizen_woman (4), citizen_youth (4), thrasybios (4), oron (4), phaidra (4), kritolaos (4), simo (4), batilo (4), zeno (4), eubulides (4), aglaophon (4) |
| `talk` | 200 | sim | falas na caixa de diálogo | animate v3 com `last_frame` = quadro 1 (sentados: à mão) | kleia (3), aristeu (3), menexeno (3), citizen (3), damaris (3), eudoro (3), pythia (3), academic (3), didymos_a (3), citizen_elder (3), citizen_woman (3), citizen_youth (3), thrasybios (3), oron (3), phaidra (3), kritolaos (3), simo (3), batilo (3), zeno (3), eubulides (3), aglaophon (3), heraclitus (3), parmenides (3), student (3) |
| `idle_breath` | 400 | sim | batalha, esperando o turno do oponente (peito sobe 1 px) | à mão, a partir do quadro 1 da idle | kleia (4) |
| `think` | 500 | sim | Kleia com o menu aberto; Menexeno e Eudoro ociosos no cenário | animate v3 com `last_frame` = quadro 1 | kleia (3), menexeno (3), eudoro (3) |
| `argue` | 200 | não | oponente apresenta o Argument (ActionId.Argue) | animate v3; chefes com `last_frame` = quadro 1 | damaris (4), eudoro (3), didymos_a (3), thrasybios (4), oron (3), phaidra (3), kritolaos (3), simo (3), batilo (3), zeno (3), eubulides (3), aglaophon (3), heraclitus (3), parmenides (3) |
| `refute` | 120 | não | REFUTAR: braço avança, dedo aponta, recuo | animate v3 com `last_frame` = quadro 1 | kleia (5) |
| `maieutics` | 180 | não | MAIÊUTICA: mãos abrem, cabeça inclina | animate v3 com `last_frame` = quadro 1 | kleia (6) |
| `quote` | 200 | não | CITAR: ergue e lê a tabuinha | animate v3 com `last_frame` = quadro 1 | kleia (4) |
| `concede` | 250 | não | CONCEDER: ombros caem, palmas para cima | animate v3 sem `last_frame`; na pose de 1 quadro, só o quadro-chave | kleia (4), damaris (1), eudoro (1), didymos_a (1), thrasybios (1), oron (1), phaidra (1), kritolaos (1), simo (1), batilo (1), zeno (1), eubulides (1), aglaophon (1), heraclitus (1), parmenides (1) |
| `withdraw` | 150 | não | RETIRAR-SE (nova): abraça a tabuinha, vira e recua; depois sai com walk | animate v3 sem `last_frame` | kleia (4) |
| `coherence_damage` | 100 | não | recebe golpe; o flash branco é shader, não desenho | animate v3 com `last_frame` = quadro 1; na pose de 1 quadro, sem `last_frame` e só o quadro-chave | kleia (3), damaris (3), eudoro (1), didymos_a (1), thrasybios (3), oron (1), phaidra (1), kritolaos (1), simo (1), batilo (1), zeno (1), eubulides (1), aglaophon (1), heraclitus (1), parmenides (1) |
| `defeat` | 220 | não, segura o último quadro | derrota; pose final mantida | animate v3 sem `last_frame`; na pose de 1 quadro, só o quadro-chave | kleia (6), damaris (6), eudoro (1), didymos_a (1), thrasybios (6), oron (1), phaidra (1), kritolaos (1), simo (1), batilo (1), zeno (1), eubulides (1), aglaophon (1), heraclitus (1), parmenides (1) |
| `victory` | 300 | não, segura o último quadro | vitória (Kleia e chefes) | animate v3 sem `last_frame` | kleia (4), damaris (4), thrasybios (4) |
| `float` | 300 | sim | espectros: substitui walk | animate v3 com `last_frame` = quadro 1 | heraclitus (2), parmenides (2) |
| `trance` | 200 | sim | Pítia (AT2-05) | animate v3 com `first_frame` = `last_frame` = base sentada | pythia (8) |
| `listen` | 250 | não | habilidade Escuta (ramo Recusar) | animate v3 sem `last_frame` | kleia (3) |
| `invert` | 200 | não | habilidade Inversão (ramo Aceitar) | animate v3 sem `last_frame` | kleia (4) |
| `paint` | 400 | sim | sentada pintando a ânfora (abertura do I.1, cena final) | base sentada + quadro 2 à mão | kleia (2), student (2) |
| `cheer` | 250 | sim | plateia reagindo | animate v3 com `last_frame` = quadro 1 | citizen (2), citizen_elder (2), citizen_woman (2), citizen_youth (2) |

A Kleia tem uma animação para cada ação do menu de batalha:

| Ação do menu | Animação |
|---|---|
| CIRCULAR | `walk` |
| REFUTAR | `refute` |
| MAIÊUTICA | `maieutics` |
| CITAR | `quote` |
| CONCEDER | `concede` |
| RETIRAR-SE | `withdraw` |

Ela também tem os estados de apoio da batalha:
- `idle_breath` enquanto espera o turno;
- `think` com o menu aberto;
- `talk` nas falas;
- `coherence_damage` quando recebe golpe;
- `defeat` e `victory` no fim.

As habilidades da ramificação Damaris (`listen` e `invert`) entram no P2.

### 8.2 Conjuntos por personagem

| Personagem | Trilha | Conjunto | Animações (quadros) | Quadros por direção | Direções | Quadros entregues |
|---|---|---|---|---:|---|---:|
| Kleia de Cerâmico | A | COMPLETO | idle 2, walk 4, talk 3, idle_breath 4, think 3, refute 5, maieutics 6, quote 4, concede 4, withdraw 4, coherence_damage 3, defeat 6, victory 4, listen 3, invert 4, paint 2 | 61 | SE, NE (`paint` só SE) | 120 |
| Aristeu | A | NPC mínimo | idle 2, talk 3 | 5 | SE, NE | 10 |
| Menexeno | A | NPC + think | idle 2, walk 4, talk 3, think 3 | 12 | SE, NE | 24 |
| Cidadão (base) | A | NPC | idle 2, walk 4, talk 3, cheer 2 | 11 | SE, NE | 22 |
| Damaris de Abdera | A | CHEFE | idle 2, walk 4, talk 3, argue 4, concede 1, coherence_damage 3, defeat 6, victory 4 | 27 | SE, NE | 54 |
| Eudoro | A | DUELISTA + think | idle 2, walk 4, talk 3, think 3, argue 3, concede 1, coherence_damage 1, defeat 1 | 18 | SE, NE | 36 |
| A Pítia | A | PÍTIA | idle 2, talk 3, trance 8 | 13 | SE | 13 |
| Acadêmico do selo (I.10) | A | NPC mínimo | idle 2, talk 3 | 5 | SE, NE | 10 |
| Irmão Dídimos A | A | DUELISTA | idle 2, walk 4, talk 3, argue 3, concede 1, coherence_damage 1, defeat 1 | 15 | SE, NE | 30 |
| Irmão Dídimos B | A | troca de paleta de `didymos_a` | igual a `didymos_a` | — | SE, NE | 30 |
| Menexeno do Ato II (selo do júri) | A | edição de `menexeno` | igual a `menexeno` | — | SE, NE | 24 |
| Cidadão idoso (variante) | A | NPC variante | idle 2, walk 4, talk 3, cheer 2 | 11 | SE, NE | 22 |
| Cidadã (variante) | A | NPC variante | idle 2, walk 4, talk 3, cheer 2 | 11 | SE, NE | 22 |
| Cidadão jovem (variante) | A | NPC variante | idle 2, walk 4, talk 3, cheer 2 | 11 | SE, NE | 22 |
| Thrasybios | B | CHEFE | idle 2, walk 4, talk 3, argue 4, concede 1, coherence_damage 3, defeat 6, victory 4 | 27 | SE, NE | 54 |
| Óron, o cambista | B | DUELISTA | idle 2, walk 4, talk 3, argue 3, concede 1, coherence_damage 1, defeat 1 | 15 | SE, NE | 30 |
| Phaidra, a adivinha | B | DUELISTA | idle 2, walk 4, talk 3, argue 3, concede 1, coherence_damage 1, defeat 1 | 15 | SE, NE | 30 |
| Kritolaos, o político menor | B | DUELISTA | idle 2, walk 4, talk 3, argue 3, concede 1, coherence_damage 1, defeat 1 | 15 | SE, NE | 30 |
| Simo, o ator | B | DUELISTA | idle 2, walk 4, talk 3, argue 3, concede 1, coherence_damage 1, defeat 1 | 15 | SE, NE | 30 |
| Bátilo, o pescador | B | DUELISTA | idle 2, walk 4, talk 3, argue 3, concede 1, coherence_damage 1, defeat 1 | 15 | SE, NE | 30 |
| Zenão de Eleia | B | DUELISTA | idle 2, walk 4, talk 3, argue 3, concede 1, coherence_damage 1, defeat 1 | 15 | SE, NE | 30 |
| Eubúlides | B | DUELISTA | idle 2, walk 4, talk 3, argue 3, concede 1, coherence_damage 1, defeat 1 | 15 | SE, NE | 30 |
| Aglaofonte | B | DUELISTA | idle 2, walk 4, talk 3, argue 3, concede 1, coherence_damage 1, defeat 1 | 15 | SE, NE | 30 |
| Heráclito (espectro) | B | ESPECTRO | idle 2, float 2, talk 3, argue 3, concede 1, coherence_damage 1, defeat 1 | 13 | SE, NE | 26 |
| Parmênides (espectro) | B | ESPECTRO | idle 2, float 2, talk 3, argue 3, concede 1, coherence_damage 1, defeat 1 | 13 | SE, NE | 26 |
| A aluna (finais) | B | NPC sentada | idle 2, talk 3, paint 2 | 7 | SE | 7 |

- **Oponentes:** têm `walk` e `concede` porque a `OpponentBrain` também joga CIRCULAR e CONCEDER, e `argue` porque é assim que apresentam o `Argument`.
- **Poses de 1 quadro** (`concede`, `coherence_damage`, `defeat` dos duelistas) seguram por 300 ms, ou até o fim da batalha, no caso de `defeat`.

**Cidadão modular (GDD §5, ART-05 #1).** O GDD pede um corpo, quatro cabeças e três cores de manto: 12 variantes. As quatro figuras da tabela acima são as quatro cabeças, cada uma gerada com o manto numa rampa **exclusiva** (as 3 cores do manto não aparecem em mais nenhum lugar da figura). As 8 combinações que faltam não são arquivo: saem por troca exata das 3 cores do manto, feita por código (§11), sobre as tiras aprovadas.

| Rampa do manto | Luz | Base | Sombra |
|---|---|---|---|
| `linen` (linho claro) | marble_light | marble_mid | marble_shade |
| `grey` (lã cinza) | marble_mid | marble_shade | marble_dark |
| `brown` (lã marrom) | ochre_mid | ochre_shade | ochre_dark |

| Cabeça (arquivo gerado) | Manto gerado | `linen` | `grey` | `brown` |
|---|---|---|---|---|
| `citizen` (castanho, barba curta) | `linen` | o arquivo | `citizen_grey` | `citizen_brown` |
| `citizen_elder` (careca, barba grisalha) | `brown` | `citizen_elder_linen` | `citizen_elder_grey` | o arquivo |
| `citizen_woman` (véu claro, cesto) | `brown` | `citizen_woman_linen` | `citizen_woman_grey` | o arquivo |
| `citizen_youth` (pele escura, ânfora) | `linen` | o arquivo | `citizen_youth_grey` | `citizen_youth_brown` |

O cidadão idoso e a cidadã são gerados com o manto marrom porque a barba grisalha e o véu claro usam os tons de mármore: com o manto em `linen`, a troca de cor levaria a barba e o véu junto.

### 8.3 Como chegar ao número de quadros no PixelLab

O `animate-with-text-v3` só aceita `frame_count` par, de 4 a 16. A documentação não diz se o `first_frame` vem incluído na saída; na MCP, o `animate_character` em modo `v3` guarda o quadro de referência como quadro 0 quando `keep_first_frame` está ligado (o padrão), então pedir 4 devolve 5. O piloto mede o comportamento da ferramenta avulsa e corrige esta tabela (§3.5).

| Quadros na tira | Código | Como gerar | O que manter |
|---|---|---|---|
| 1 (pose) | `pose` | `frame_count` 4, só `first_frame` | O quadro-chave, em geral o último |
| 2 (idle, `paint`) | `criar`, `rotação + mão`, `sentado: base`, `sentada: edição + mão`, `mão (Aseprite)` | Nenhuma geração além da rotação, da base ou da edição | Quadro 1 + quadro 2 à mão |
| 2 (`cheer`, `float`) | `animar loop` | `frame_count` 4, `first_frame` = `last_frame` = quadro 1 | Quadro 1 + o pico |
| 3 (`talk`, `think`) | `animar loop` | `frame_count` 4, `first_frame` = `last_frame` = quadro 1 | Quadro 1 + os dois do meio; descarte o repetido do fim |
| 3 (`argue` dos duelistas, `listen`) | `animar` | `frame_count` 4, só `first_frame` | As três batidas do gesto |
| 3 (`coherence_damage`) | `animar volta` | `frame_count` 4, `first_frame` = `last_frame` = quadro 1 | Recuo, pico e meio caminho de volta |
| 4 (`walk`) | `template walk + mão` | Template `walking-4-frames` | Os 4 quadros do template, com o bob à mão |
| 4 (`idle_breath`, `talk` sentado) | `mão (Aseprite)` | Nenhuma | Roteiro `[EDIT]` |
| 4 (ações) | `animar` ou `animar volta` | `frame_count` 4 ou 6 | As 4 batidas da linha "Seleção" |
| 5 (`refute`) | `animar volta` | `frame_count` 6, `first_frame` = `last_frame` = quadro 1 | Antecipação, avanço, pico, segura e recolhe |
| 6 (`maieutics`, `defeat`) | `animar volta` / `animar` | `frame_count` 6 ou 8 | As 6 batidas da linha "Seleção" |
| 8 (`trance`) | `animar loop` | `frame_count` 8 ou 10, `first_frame` = `last_frame` = base sentada | 8 quadros; descarte o repetido do fim |

Se a identidade derivar entre quadros, tente `animate-with-skeleton-v3` (beta, Tier 1, 2 a 4 generations por direção). Evite o modo `pro` de animação, que custa de 20 a 40 generations por direção. Se a saída não trouxer uma batida da linha "Seleção", gere de novo com outra seed antes de desenhar à mão.

---

## 9. Trilha A

252 arquivos, de A-001 a A-252. Os ids seguem a ordem de trabalho. Dentro de cada prioridade, primeiro os personagens (tiras e retratos), depois o cenário e o sistema.

### 9.1 P1 — Ato I, fatia vertical

106 arquivos (A-001 a A-106), 250 quadros.

**Piloto (§3.5):** A-001 `kleia_idle_se.png`, A-002 `kleia_idle_ne.png`, A-003 `prop_amphora_stack.png`. Os pilotos vêm antes de todo o resto.

#### Kleia de Cerâmico · `kleia` · COMPLETO

- **Altura no canvas:** 24 px (cabeça ~8 px).
- **Onde fica:** todos os mapas; referência sobre `floor_marble` e `floor_packed_dirt`.
- **Direções:** SE e NE; view fixada no piloto (§3.2), a mesma para o elenco inteiro.
- **Arquivos:** 31 tiras (120 quadros) e 4 retratos, em P1, P2, P3.
- **Chave de cor:** outline, outline_blue, skin_light, skin_mid, skin_shade, hair_dark, hair_mid, aegean_light, aegean_mid, aegean_shade, marble_mid (remendo 2×2 no ombro direito), red_mid (óxido nas mãos e antebraços), ochre_mid, ochre_shade (tabuinha e moldura), gold_mid (estilete, 1 a 2 px) — 15 cores.
- **Silhueta e atuação:** Magra, ombros curvados para a frente, cabeça que inclina quando escuta. Cabelo escuro na altura do queixo, preso de mau jeito. Descalça. A tabuinha ocre no centro do cinto é a maior mancha quente e funciona como a "arma" da silhueta: desenhe-a no eixo central, para o espelho SW/NW não trocá-la de lado. Parada e atenta, sem gesticular à toa; no REFUTAR, arrogância defensiva (braço esticado, queixo erguido).

Descritor `[KLEIA]`:

```text
Kleia, a 17-year-old ancient Greek potter's daughter, about 24 pixels tall, slim with slightly hunched shoulders, chin-length dark brown hair loosely tied back with a cord, knee-length faded sky-blue wool chiton belted at the waist, bare arms and bare legs, a pale grey-beige mended patch on the right shoulder, hands and forearms stained brick red with iron-oxide clay, bare dusty feet, an orange-ochre wax writing tablet hanging at the center of her belt, a small golden bronze stylus, a leather cord around one wrist,
```

- **A-001** · `kleia_idle_se.png` · `idle` · 2 quadros (tira 64×32) · 600 ms por quadro, em loop · `criar (âncora)`
  ```text
  [KLEIA] standing attentive and slightly hunched, bare empty hands loose at her sides, the wax tablet on the belt, [CHARACTER] [STYLE_CHAR]
  ```
  **Quadros:** Quadro 1 = a rotação SE do personagem criado com este prompt (§3.3, `criar (âncora)`). Quadro 2 à mão: suba 1 px as linhas do peito e incline a cabeça 1 px para o ombro direito, como quem escuta, com pés e sombra idênticos.
- **A-002** · `kleia_idle_ne.png` · `idle` · 2 quadros (tira 64×32) · 600 ms por quadro, em loop · `rotação + mão`
  ```text
  [EDIT] Frame 1 is the north-east rotation of the character created for kleia_idle_se.png, with zero extra generations. Frame 2 copies frame 1 with the chest rows moved up 1 px and the head tilted 1 px toward the right shoulder, as if listening, while the feet and the shadow stay identical.
  ```
- **A-004** · `kleia_walk_se.png` · `walk` · 4 quadros (tira 128×32) · 150 ms por quadro, em loop · `template walk + mão`
  ```text
  Facing south-east in 3/4 front view: walking barefoot at an easy pace, the arms swinging slightly and the wax tablet steady on the belt.
  ```
  **Seleção:** Template `walking-4-frames`, `directions` = ["south-east"], este texto em `action_description`. Confira os pés plantados na linha 29; depois, à mão, suba 1 px a cabeça e o tronco nos dois quadros de passagem (pernas juntas).
- **A-005** · `kleia_walk_ne.png` · `walk` · 4 quadros (tira 128×32) · 150 ms por quadro, em loop · `template walk + mão`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: walking barefoot at an easy pace, the arms swinging slightly and the wax tablet steady on the belt.
  ```
  **Seleção:** Template `walking-4-frames`, `directions` = ["north-east"], este texto em `action_description`. Confira os pés plantados na linha 29; depois, à mão, suba 1 px a cabeça e o tronco nos dois quadros de passagem (pernas juntas).
- **A-006** · `kleia_talk_se.png` · `talk` · 3 quadros (tira 96×32) · 200 ms por quadro, em loop · `animar loop`
  ```text
  Facing south-east in 3/4 front view: talks in place, the mouth opening and closing while the right hand makes a small open gesture at chest height, then settles back to the idle pose.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle SE. Fique com 3, em ordem: quadro 1 da idle (boca fechada) · boca aberta com o gesto no auge · boca entreaberta, mão voltando. Descarte o último quadro gerado se ele repetir o primeiro.
- **A-007** · `kleia_talk_ne.png` · `talk` · 3 quadros (tira 96×32) · 200 ms por quadro, em loop · `animar loop`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: talks in place with small nods of the head while the right hand makes a small open gesture at chest height, then settles back to the idle pose.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle NE. Fique com 3, em ordem: quadro 1 da idle · aceno de cabeça com o gesto no auge · mão voltando. Descarte o último quadro gerado se ele repetir o primeiro.
- **A-008** · `kleia_idle_breath_se.png` · `idle_breath` · 4 quadros (tira 128×32) · 400 ms por quadro, em loop · `mão (Aseprite)`
  ```text
  [EDIT] Built from frame 1 of kleia_idle_se.png, zero generations: frame 1 is that frame; frame 2 moves the chest and shoulder rows up 1 px; frame 3 moves the head, chest and shoulder rows up 1 px; frame 4 repeats frame 2. The feet and the shadow stay identical in all four frames.
  ```
- **A-009** · `kleia_idle_breath_ne.png` · `idle_breath` · 4 quadros (tira 128×32) · 400 ms por quadro, em loop · `mão (Aseprite)`
  ```text
  [EDIT] Built from frame 1 of kleia_idle_ne.png, zero generations: frame 1 is that frame; frame 2 moves the chest and shoulder rows up 1 px; frame 3 moves the head, chest and shoulder rows up 1 px; frame 4 repeats frame 2. The feet and the shadow stay identical in all four frames.
  ```
- **A-010** · `kleia_think_se.png` · `think` · 3 quadros (tira 96×32) · 500 ms por quadro, em loop · `animar loop`
  ```text
  Facing south-east in 3/4 front view: thinks while the menu is open: lowers the chin and lifts the bronze stylus to her lips, glances to the side, then lowers the stylus back to the idle pose.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle SE. Fique com 3, em ordem: quadro 1 da idle · estilete subindo, queixo baixo · estilete nos lábios, olhar de lado. Descarte o último quadro gerado se ele repetir o primeiro.
- **A-011** · `kleia_think_ne.png` · `think` · 3 quadros (tira 96×32) · 500 ms por quadro, em loop · `animar loop`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: thinks while the menu is open: the head tilts down and the right hand lifts the stylus to the chin, holds, then lowers it back to the idle pose.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle NE. Fique com 3, em ordem: quadro 1 da idle · cabeça inclina para baixo · estilete no queixo. Descarte o último quadro gerado se ele repetir o primeiro.
- **A-012** · `kleia_refute_se.png` · `refute` · 5 quadros (tira 160×32) · 120 ms por quadro, sem loop · `animar volta`
  ```text
  Facing south-east in 3/4 front view: refutes with an accusing point: shifts her weight back, snaps the right arm forward and points the index finger of the open hand straight at the opponent with the chin raised, holds the point, then pulls the arm back to the idle pose.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle SE. Fique com 5: antecipação (peso para trás) · braço avançando · braço esticado, indicador apontado, queixo erguido · segura · braço recolhendo, perto do idle.
- **A-013** · `kleia_refute_ne.png` · `refute` · 5 quadros (tira 160×32) · 120 ms por quadro, sem loop · `animar volta`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: refutes with an accusing point: shifts her weight back, snaps the right arm forward and points the index finger of the open hand straight ahead with the head lifted, holds the point, then pulls the arm back to the idle pose.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle NE. Fique com 5: antecipação (peso para trás) · braço avançando · braço esticado, indicador apontado, cabeça erguida · segura · braço recolhendo, perto do idle.
- **A-014** · `kleia_maieutics_se.png` · `maieutics` · 6 quadros (tira 192×32) · 180 ms por quadro, sem loop · `animar volta`
  ```text
  Facing south-east in 3/4 front view: asks a maieutic question: both empty hands slowly open outward with the palms up at waist height, the head tilts to one side with the eyebrows raised, holds the open questioning gesture, then the hands return to the idle pose.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle SE. Fique com 6: quadro 1 da idle · mãos abrindo · palmas para cima na cintura · cabeça inclinada, sobrancelhas erguidas · segura o gesto · mãos voltando.
- **A-015** · `kleia_maieutics_ne.png` · `maieutics` · 6 quadros (tira 192×32) · 180 ms por quadro, sem loop · `animar volta`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: asks a maieutic question: both empty hands slowly open outward with the palms up at waist height, the head tilts to one side, holds the open questioning gesture, then the hands return to the idle pose.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle NE. Fique com 6: quadro 1 da idle · mãos abrindo · palmas para cima na cintura · cabeça inclinada · segura o gesto · mãos voltando.
- **A-016** · `kleia_quote_se.png` · `quote` · 4 quadros (tira 128×32) · 200 ms por quadro, sem loop · `animar volta`
  ```text
  Facing south-east in 3/4 front view: quotes from her notes: takes the wax tablet from her belt, raises it to chest height and reads from it with the head lowered, then hangs it back on the belt.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle SE. Fique com 4: tira a tabuinha do cinto · ergue à altura do peito · lê com a cabeça baixa · devolve ao cinto.
- **A-017** · `kleia_quote_ne.png` · `quote` · 4 quadros (tira 128×32) · 200 ms por quadro, sem loop · `animar volta`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: quotes from her notes: takes the wax tablet from her belt, raises it to chest height with the head bowed over it, then hangs it back on the belt.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle NE. Fique com 4: tira a tabuinha do cinto · ergue à altura do peito · lê com a cabeça baixa · devolve ao cinto.
- **A-018** · `kleia_concede_se.png` · `concede` · 4 quadros (tira 128×32) · 250 ms por quadro, sem loop · `animar`
  ```text
  Facing south-east in 3/4 front view: concedes calmly: the shoulders drop, the head lowers slightly and both palms turn up at hip height, then she holds the open-palm pose with a small breath.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle SE, sem `last_frame`. Fique com 4: ombros caem · cabeça baixa · palmas para cima na altura do quadril · respiro segurando a pose (sem voltar ao idle).
- **A-019** · `kleia_concede_ne.png` · `concede` · 4 quadros (tira 128×32) · 250 ms por quadro, sem loop · `animar`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: concedes calmly: the shoulders drop, the head lowers slightly and both palms turn up at hip height, then she holds the open-palm pose with a small breath.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle NE, sem `last_frame`. Fique com 4: ombros caem · cabeça baixa · palmas para cima na altura do quadril · respiro segurando a pose (sem voltar ao idle).
- **A-020** · `kleia_withdraw_se.png` · `withdraw` · 4 quadros (tira 128×32) · 150 ms por quadro, sem loop · `animar`
  ```text
  Facing south-east in 3/4 front view: withdraws from the debate: hugs the wax tablet to her chest, turns her shoulders away and takes one step back with the feet on the ground line, ending half turned away, ready to leave.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle SE, sem `last_frame`. Fique com 4: abraça a tabuinha · vira os ombros · um passo atrás, pés na linha 29 · meio de costas, pronta para sair.
- **A-021** · `kleia_withdraw_ne.png` · `withdraw` · 4 quadros (tira 128×32) · 150 ms por quadro, sem loop · `animar`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: withdraws from the debate: hugs the wax tablet to her chest, turns her shoulders away and takes one step back with the feet on the ground line, ending half turned away, ready to leave.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle NE, sem `last_frame`. Fique com 4: abraça a tabuinha · vira os ombros · um passo atrás, pés na linha 29 · meio de costas, pronta para sair.
- **A-022** · `kleia_coherence_damage_se.png` · `coherence_damage` · 3 quadros (tira 96×32) · 100 ms por quadro, sem loop · `animar volta`
  ```text
  Facing south-east in 3/4 front view: reels from a hit: flinches with the eyes shut as the head snaps back, the upper body leans back to the left with the feet planted, then recovers toward the idle pose.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle SE. Fique com 3: recuo com os olhos fechados e a cabeça para trás · tronco 2 px para a esquerda, pés plantados · meio caminho de volta ao idle.
- **A-023** · `kleia_coherence_damage_ne.png` · `coherence_damage` · 3 quadros (tira 96×32) · 100 ms por quadro, sem loop · `animar volta`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: reels from a hit: the head jerks back and the shoulders hunch, the upper body leans back to the left with the feet planted, then recovers toward the idle pose.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle NE. Fique com 3: cabeça sacode para trás e ombros encolhem · tronco 2 px para a esquerda, pés plantados · meio caminho de volta ao idle.
- **A-024** · `kleia_defeat_se.png` · `defeat` · 6 quadros (tira 192×32) · 220 ms por quadro, sem loop, segura o último · `animar`
  ```text
  Facing south-east in 3/4 front view: is defeated: staggers, the knees bend and she drops to both knees, the head bows and the hands fall to the ground, ending in a kneeling pose with the head down.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle SE, sem `last_frame`. Fique com 6: cambaleia · joelhos dobram · de joelhos · cabeça baixa · mãos no chão · pose final (segura).
- **A-025** · `kleia_defeat_ne.png` · `defeat` · 6 quadros (tira 192×32) · 220 ms por quadro, sem loop, segura o último · `animar`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: is defeated: staggers, the knees bend and she drops to both knees, the head bows and the hands fall to the ground, ending in a kneeling pose with the head down.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle NE, sem `last_frame`. Fique com 6: cambaleia · joelhos dobram · de joelhos · cabeça baixa · mãos no chão · pose final (segura).
- **A-026** · `kleia_victory_se.png` · `victory` · 4 quadros (tira 128×32) · 300 ms por quadro, sem loop, segura o último · `animar`
  ```text
  Facing south-east in 3/4 front view: wins with quiet pride: straightens up and lifts the chin, raises the wax tablet overhead in one hand, then lowers the arm into a proud upright stance.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle SE, sem `last_frame`. Fique com 4: endireita-se · ergue o queixo · tabuinha acima da cabeça · pose final ereta (segura).
- **A-027** · `kleia_victory_ne.png` · `victory` · 4 quadros (tira 128×32) · 300 ms por quadro, sem loop, segura o último · `animar`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: wins with quiet pride: straightens up and lifts the head, raises the wax tablet overhead in one hand, then lowers the arm into a proud upright stance.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle NE, sem `last_frame`. Fique com 4: endireita-se · ergue a cabeça · tabuinha acima da cabeça · pose final ereta (segura).
- **A-028** · `portrait_kleia_neutral.png` · retrato 32×32 · `retrato`
  ```text
  [PORTRAIT] Source: frame 1 of kleia_idle_se.png. Check: calm attentive gaze, mouth closed, the chin slightly raised.
  ```
- **A-029** · `portrait_kleia_shaken.png` · retrato 32×32 · `expressão`
  ```text
  [FACE] Source: portrait_kleia_neutral.png, mask on the eyes, brows and mouth. Description: eyes wide open, brows raised and drawn together, mouth slightly open, a bead of sweat on the temple.
  ```

#### Aristeu · `aristeu` · NPC mínimo

- **Altura no canvas:** 23 px.
- **Onde fica:** `floor_packed_dirt` (oficina).
- **Direções:** SE e NE; view fixada no piloto (§3.2), a mesma para o elenco inteiro.
- **Arquivos:** 4 tiras (10 quadros) e 2 retratos, em P1.
- **Chave de cor:** outline, outline_blue, skin_light, skin_mid, skin_shade, marble_shade, marble_dark (barba grisalha), ochre_mid, ochre_shade, ochre_dark (quíton marrom), ochre_light (respingos de argila de 1 px), red_mid (óxido) — 12 cores.
- **Silhueta e atuação:** Robusto mas cansado, ombros caídos. As mãos ficam junto ao peito, com dedos tortos e nós em `skin_shade`: é o motivo de a Kleia ir à ágora. Não há placeholder nem ator no mapa ainda; o sprite é necessário para o I.1.

Descritor `[ARISTEU]`:

```text
Aristeu, a 52-year-old ancient Greek potter, about 23 pixels tall, sturdy but tired with dropped shoulders, short grey beard, sun-tanned skin, knee-length stained rust-brown work chiton spattered with pale clay, bare legs, leather thong sandals, arthritic hands with crooked swollen fingers held close to the chest,
```

- **A-030** · `aristeu_idle_se.png` · `idle` · 2 quadros (tira 64×32) · 600 ms por quadro, em loop · `criar`
  ```text
  [ARISTEU] facing south in front view, tired with the shoulders dropped, the crooked arthritic hands held close to the chest, [CHARACTER] [STYLE_CHAR]
  ```
  **Quadros:** Quadro 1 = a rotação SE do personagem criado a partir do sprite sul deste prompt (§3.3, `criar`). Quadro 2 à mão: suba 1 px as linhas da cabeça, dos ombros e do peito, com pés e sombra idênticos.
- **A-031** · `aristeu_idle_ne.png` · `idle` · 2 quadros (tira 64×32) · 600 ms por quadro, em loop · `rotação + mão`
  ```text
  [EDIT] Frame 1 is the north-east rotation of the character created for aristeu_idle_se.png, with zero extra generations. Frame 2 copies frame 1 with the head, shoulder and chest rows moved up 1 px, while the feet and the shadow stay identical.
  ```
- **A-032** · `aristeu_talk_se.png` · `talk` · 3 quadros (tira 96×32) · 200 ms por quadro, em loop · `animar loop`
  ```text
  Facing south-east in 3/4 front view: talks in place, the mouth opening and closing while one crooked hand lifts weakly toward the listener, then settles back to the idle pose.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle SE. Fique com 3, em ordem: quadro 1 da idle (boca fechada) · boca aberta com o gesto no auge · boca entreaberta, mão voltando. Descarte o último quadro gerado se ele repetir o primeiro.
- **A-033** · `aristeu_talk_ne.png` · `talk` · 3 quadros (tira 96×32) · 200 ms por quadro, em loop · `animar loop`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: talks in place with small nods of the head while one crooked hand lifts weakly toward the listener, then settles back to the idle pose.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle NE. Fique com 3, em ordem: quadro 1 da idle · aceno de cabeça com o gesto no auge · mão voltando. Descarte o último quadro gerado se ele repetir o primeiro.
- **A-034** · `portrait_aristeu_neutral.png` · retrato 32×32 · `retrato`
  ```text
  [PORTRAIT] Source: frame 1 of aristeu_idle_se.png. Check: tired kind eyes, both crooked arthritic hands raised in front of the chest and clearly visible. Inpaint fix for the lower third, if needed: both crooked arthritic hands raised in front of the chest.
  ```
- **A-035** · `portrait_aristeu_shaken.png` · retrato 32×32 · `expressão`
  ```text
  [FACE] Source: portrait_aristeu_neutral.png, mask on the eyes, brows and mouth. Description: brows raised and drawn together in worry, mouth open.
  ```

#### Menexeno · `menexeno` · NPC + think

- **Altura no canvas:** 22 px curvado; o bastão chega a 27 px.
- **Onde fica:** `floor_marble` (Ágora, célula 10,4).
- **Direções:** SE e NE; view fixada no piloto (§3.2), a mesma para o elenco inteiro.
- **Arquivos:** 8 tiras (24 quadros) e 2 retratos, em P1.
- **Chave de cor:** outline, outline_blue, skin_light, skin_mid, skin_shade, marble_light, marble_mid (barba e cabelo), marble_shade, marble_dark (manto de lã crua encardido), ochre_shade, ochre_dark, hair_mid (bastão nodoso e corda das sandálias) — 12 cores; a versão do Ato II soma gold_mid e gold_dark (selo) — 14.
- **Silhueta e atuação:** Alto mas encurvado, costas em arco, cabeça projetada à frente, mãos grandes de ex-ferreiro. O bastão passa da altura da cabeça e fica na mão direita. No retrato, a íris esquerda leva 1 px de `marble_mid` (catarata). Econômico e seco.

Descritor `[MENEXENO]`:

```text
Menexeno, a 68-year-old ancient Greek man, about 22 pixels tall because he is stooped, gaunt, uneven self-cut white beard, thinning white hair, large former-blacksmith hands, a rectangular undyed wool himation gone grimy light grey, wrapped around the body and draped over the left shoulder, bare shins, worn leather sandals tied with rope, leaning on a gnarled rust-brown olive-wood staff that rises above his head,
```

- **A-036** · `menexeno_idle_se.png` · `idle` · 2 quadros (tira 64×32) · 600 ms por quadro, em loop · `criar`
  ```text
  [MENEXENO] facing south in front view, stooped, leaning on the gnarled staff held in the right hand, the head pushed forward, [CHARACTER] [STYLE_CHAR]
  ```
  **Quadros:** Quadro 1 = a rotação SE do personagem criado a partir do sprite sul deste prompt (§3.3, `criar`). Quadro 2 à mão: suba 1 px as linhas da cabeça, dos ombros e do peito, com pés, bastão e sombra idênticos.
- **A-037** · `menexeno_idle_ne.png` · `idle` · 2 quadros (tira 64×32) · 600 ms por quadro, em loop · `rotação + mão`
  ```text
  [EDIT] Frame 1 is the north-east rotation of the character created for menexeno_idle_se.png, with zero extra generations. Frame 2 copies frame 1 with the head, shoulder and chest rows moved up 1 px, while the feet, the staff and the shadow stay identical.
  ```
- **A-038** · `menexeno_walk_se.png` · `walk` · 4 quadros (tira 128×32) · 150 ms por quadro, em loop · `template walk + mão`
  ```text
  Facing south-east in 3/4 front view: walking with a slow stooped shuffle, leaning on the gnarled staff.
  ```
  **Seleção:** Template `walking-4-frames`, `directions` = ["south-east"], este texto em `action_description`. Confira os pés plantados na linha 29; depois, à mão, suba 1 px a cabeça e o tronco nos dois quadros de passagem (pernas juntas).
- **A-039** · `menexeno_walk_ne.png` · `walk` · 4 quadros (tira 128×32) · 150 ms por quadro, em loop · `template walk + mão`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: walking with a slow stooped shuffle, leaning on the gnarled staff.
  ```
  **Seleção:** Template `walking-4-frames`, `directions` = ["north-east"], este texto em `action_description`. Confira os pés plantados na linha 29; depois, à mão, suba 1 px a cabeça e o tronco nos dois quadros de passagem (pernas juntas).
- **A-040** · `menexeno_talk_se.png` · `talk` · 3 quadros (tira 96×32) · 200 ms por quadro, em loop · `animar loop`
  ```text
  Facing south-east in 3/4 front view: talks in place, the mouth opening and closing while the free left hand lifts slightly in a dry, minimal gesture, then settles back to the idle pose.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle SE. Fique com 3, em ordem: quadro 1 da idle (boca fechada) · boca aberta com o gesto no auge · boca entreaberta, mão voltando. Descarte o último quadro gerado se ele repetir o primeiro.
- **A-041** · `menexeno_talk_ne.png` · `talk` · 3 quadros (tira 96×32) · 200 ms por quadro, em loop · `animar loop`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: talks in place with small nods of the head while the free left hand lifts slightly in a dry, minimal gesture, then settles back to the idle pose.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle NE. Fique com 3, em ordem: quadro 1 da idle · aceno de cabeça com o gesto no auge · mão voltando. Descarte o último quadro gerado se ele repetir o primeiro.
- **A-042** · `menexeno_think_se.png` · `think` · 3 quadros (tira 96×32) · 500 ms por quadro, em loop · `animar loop`
  ```text
  Facing south-east in 3/4 front view: thinks, idle in the scenery: strokes the beard with the free hand, closes the eyes, then opens them with the gaze down, the staff held still.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle SE. Fique com 3, em ordem: quadro 1 da idle · mão alisa a barba · olhos fechados. Descarte o último quadro gerado se ele repetir o primeiro.
- **A-043** · `menexeno_think_ne.png` · `think` · 3 quadros (tira 96×32) · 500 ms por quadro, em loop · `animar loop`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: thinks, idle in the scenery: the head tilts down and the free hand rises to the beard, then lowers back to the idle pose, the staff held still.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle NE. Fique com 3, em ordem: quadro 1 da idle · cabeça inclina · mão na barba. Descarte o último quadro gerado se ele repetir o primeiro.
- **A-044** · `portrait_menexeno_neutral.png` · retrato 32×32 · `retrato`
  ```text
  [PORTRAIT] Source: frame 1 of menexeno_idle_se.png. Check: dry unreadable look; the left iris gets 1 px of marble_mid by hand (cataract).
  ```
- **A-045** · `portrait_menexeno_shaken.png` · retrato 32×32 · `expressão`
  ```text
  [FACE] Source: portrait_menexeno_neutral.png, mask on the eyes, brows and mouth. Description: eyes shut tight in pain, jaw clenched.
  ```

#### Cidadão (base) · `citizen` · NPC

- **Altura no canvas:** 24 px.
- **Onde fica:** Ágora (plateia) e Cerâmico (vizinho, `neighbor_kerameikos`).
- **Direções:** SE e NE; view fixada no piloto (§3.2), a mesma para o elenco inteiro.
- **Arquivos:** 8 tiras (22 quadros) e 2 retratos, em P1, P3.
- **Chave de cor:** outline, outline_blue, skin_light, skin_mid, skin_shade, hair_mid, hair_dark, marble_light, marble_mid, marble_shade (quíton e himátion de linho: rampa do manto, exclusiva), ochre_shade (sandálias) — 11 cores.
- **Silhueta e atuação:** Corpo base do cidadão modular: cabeça castanha (cabelo e barba curtos), roupa de linho claro. Só famílias neutras: aegean, purple, red e gold são proibidos em cidadão. A rampa do manto é **exclusiva**: `marble_light`, `marble_mid` e `marble_shade` só aparecem na roupa, para a troca de manto por código funcionar (§8.2).

Descritor `[CITIZEN]`:

```text
an ordinary ancient Athenian citizen, about 24 pixels tall, short brown hair and a short beard, a knee-length off-white linen chiton under a rectangular off-white linen himation draped over the left shoulder, bare legs, rust-brown leather thong sandals, plain and unadorned, muted neutral colors,
```

- **A-046** · `citizen_idle_se.png` · `idle` · 2 quadros (tira 64×32) · 600 ms por quadro, em loop · `criar`
  ```text
  [CITIZEN] facing south in front view, plain relaxed stance, the arms at the sides, [CHARACTER] [STYLE_CHAR]
  ```
  **Quadros:** Quadro 1 = a rotação SE do personagem criado a partir do sprite sul deste prompt (§3.3, `criar`). Quadro 2 à mão: suba 1 px as linhas da cabeça, dos ombros e do peito, com pés e sombra idênticos.
- **A-047** · `citizen_idle_ne.png` · `idle` · 2 quadros (tira 64×32) · 600 ms por quadro, em loop · `rotação + mão`
  ```text
  [EDIT] Frame 1 is the north-east rotation of the character created for citizen_idle_se.png, with zero extra generations. Frame 2 copies frame 1 with the head, shoulder and chest rows moved up 1 px, while the feet and the shadow stay identical.
  ```
- **A-048** · `citizen_walk_se.png` · `walk` · 4 quadros (tira 128×32) · 150 ms por quadro, em loop · `template walk + mão`
  ```text
  Facing south-east in 3/4 front view: walking at an ordinary unhurried pace.
  ```
  **Seleção:** Template `walking-4-frames`, `directions` = ["south-east"], este texto em `action_description`. Confira os pés plantados na linha 29; depois, à mão, suba 1 px a cabeça e o tronco nos dois quadros de passagem (pernas juntas).
- **A-049** · `citizen_walk_ne.png` · `walk` · 4 quadros (tira 128×32) · 150 ms por quadro, em loop · `template walk + mão`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: walking at an ordinary unhurried pace.
  ```
  **Seleção:** Template `walking-4-frames`, `directions` = ["north-east"], este texto em `action_description`. Confira os pés plantados na linha 29; depois, à mão, suba 1 px a cabeça e o tronco nos dois quadros de passagem (pernas juntas).
- **A-050** · `citizen_talk_se.png` · `talk` · 3 quadros (tira 96×32) · 200 ms por quadro, em loop · `animar loop`
  ```text
  Facing south-east in 3/4 front view: talks in place, the mouth opening and closing while one hand opens at waist height, then settles back to the idle pose.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle SE. Fique com 3, em ordem: quadro 1 da idle (boca fechada) · boca aberta com o gesto no auge · boca entreaberta, mão voltando. Descarte o último quadro gerado se ele repetir o primeiro.
- **A-051** · `citizen_talk_ne.png` · `talk` · 3 quadros (tira 96×32) · 200 ms por quadro, em loop · `animar loop`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: talks in place with small nods of the head while one hand opens at waist height, then settles back to the idle pose.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle NE. Fique com 3, em ordem: quadro 1 da idle · aceno de cabeça com o gesto no auge · mão voltando. Descarte o último quadro gerado se ele repetir o primeiro.
- **A-052** · `portrait_citizen_neutral.png` · retrato 32×32 · `retrato`
  ```text
  [PORTRAIT] Source: frame 1 of citizen_idle_se.png. Check: friendly ordinary face.
  ```
- **A-053** · `portrait_citizen_shaken.png` · retrato 32×32 · `expressão`
  ```text
  [FACE] Source: portrait_citizen_neutral.png, mask on the eyes, brows and mouth. Description: startled, eyebrows raised, eyes wide, mouth open.
  ```

#### Damaris de Abdera · `damaris` · CHEFE

- **Altura no canvas:** 24 px.
- **Onde fica:** Ágora (I.7), Academia e Tribunal.
- **Direções:** SE e NE; view fixada no piloto (§3.2), a mesma para o elenco inteiro.
- **Arquivos:** 16 tiras (54 quadros) e 2 retratos, em P1, P2.
- **Chave de cor:** outline, outline_blue, skin_mid, skin_shade, hair_mid (pele trácia), hair_dark (trança), red_light, red_mid, red_dark (peplos terracota), marble_light, marble_shade (broche de prata), ochre_shade, ochre_dark (sandálias) — 13 cores.
- **Silhueta e atuação:** Compacta, ereta, simétrica e imóvel: o oposto do Thrasybios. Nunca segura nada; as mãos vazias e visíveis são a assinatura. Olhar fixo, sem quadro de piscar, nem no retrato abalado. No talk só a boca e um gesto mínimo se mexem.

Descritor `[DAMARIS]`:

```text
Damaris of Abdera, a 35-year-old Thracian woman sophist, about 24 pixels tall, compact and very upright, perfectly still, darker brown skin, black hair in a tight smooth braid coiled at the nape, a severe ankle-length plain brick-red peplos pinned at the shoulders, a single small silver brooch, practical leather travel sandals, bare empty hands held visibly at her sides, a fixed wide-open stare,
```

- **A-054** · `damaris_idle_se.png` · `idle` · 2 quadros (tira 64×32) · 600 ms por quadro, em loop · `criar`
  ```text
  [DAMARIS] facing south in front view, perfectly upright and still, the arms straight at her sides, the bare empty hands visible, [CHARACTER] [STYLE_CHAR]
  ```
  **Quadros:** Quadro 1 = a rotação SE do personagem criado a partir do sprite sul deste prompt (§3.3, `criar`). Quadro 2 à mão: suba 1 px só as linhas do peito, com cabeça, pés e sombra idênticos.
- **A-055** · `damaris_idle_ne.png` · `idle` · 2 quadros (tira 64×32) · 600 ms por quadro, em loop · `rotação + mão`
  ```text
  [EDIT] Frame 1 is the north-east rotation of the character created for damaris_idle_se.png, with zero extra generations. Frame 2 copies frame 1 with only the chest rows moved up 1 px, while the head, the feet and the shadow stay identical.
  ```
- **A-056** · `damaris_walk_se.png` · `walk` · 4 quadros (tira 128×32) · 150 ms por quadro, em loop · `template walk + mão`
  ```text
  Facing south-east in 3/4 front view: walking with a measured upright gait, the arms almost still and the bare hands empty.
  ```
  **Seleção:** Template `walking-4-frames`, `directions` = ["south-east"], este texto em `action_description`. Confira os pés plantados na linha 29; depois, à mão, suba 1 px a cabeça e o tronco nos dois quadros de passagem (pernas juntas).
- **A-057** · `damaris_walk_ne.png` · `walk` · 4 quadros (tira 128×32) · 150 ms por quadro, em loop · `template walk + mão`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: walking with a measured upright gait, the arms almost still and the bare hands empty.
  ```
  **Seleção:** Template `walking-4-frames`, `directions` = ["north-east"], este texto em `action_description`. Confira os pés plantados na linha 29; depois, à mão, suba 1 px a cabeça e o tronco nos dois quadros de passagem (pernas juntas).
- **A-058** · `damaris_talk_se.png` · `talk` · 3 quadros (tira 96×32) · 200 ms por quadro, em loop · `animar loop`
  ```text
  Facing south-east in 3/4 front view: talks in place, the mouth opening and closing while the right hand lifts slightly with the index finger raised while the body stays still, then settles back to the idle pose.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle SE. Fique com 3, em ordem: quadro 1 da idle (boca fechada) · boca aberta com o gesto no auge · boca entreaberta, mão voltando. Descarte o último quadro gerado se ele repetir o primeiro.
- **A-059** · `damaris_talk_ne.png` · `talk` · 3 quadros (tira 96×32) · 200 ms por quadro, em loop · `animar loop`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: talks in place with small nods of the head while the right hand lifts slightly with the index finger raised while the body stays still, then settles back to the idle pose.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle NE. Fique com 3, em ordem: quadro 1 da idle · aceno de cabeça com o gesto no auge · mão voltando. Descarte o último quadro gerado se ele repetir o primeiro.
- **A-060** · `portrait_damaris_neutral.png` · retrato 32×32 · `retrato`
  ```text
  [PORTRAIT] Source: frame 1 of damaris_idle_se.png. Check: fixed wide-open stare, mouth closed, perfectly composed.
  ```
- **A-061** · `portrait_damaris_shaken.png` · retrato 32×32 · `expressão`
  ```text
  [FACE] Source: portrait_damaris_neutral.png, mask on the eyes, brows and mouth. Description: the same fixed wide-open stare, lips pressed into a thin line.
  ```

#### Eudoro · `eudoro` · DUELISTA + think

- **Altura no canvas:** 25 px, esguio.
- **Onde fica:** `floor_marble_ochre` (Ágora, célula 4,7).
- **Direções:** SE e NE; view fixada no piloto (§3.2), a mesma para o elenco inteiro.
- **Arquivos:** 16 tiras (36 quadros) e 2 retratos, em P1, P2.
- **Chave de cor:** outline, outline_blue, skin_light, skin_mid, skin_shade, hair_dark (cabelo e tinta nos dedos), marble_light, marble_mid, marble_shade (quíton branco), gold_light, gold_mid (borda), gold_dark (compasso de bronze), ochre_mid, ochre_shade (tabuinha e sandálias) — 14 cores.
- **Silhueta e atuação:** Magro, alto e desengonçado, membros compridos e postura rígida; arrasta os pés. Compasso aberto em V na mão direita, tabuinha na esquerda. Muda em relação ao placeholder: quíton branco com borda dourada (GDD), não túnica dourada.

Descritor `[EUDORO]`:

```text
Eudoro, a 20-year-old student of Plato's Academy, about 25 pixels tall, thin, tall and gangly, black hair cropped very short, a spotless knee-length off-white chiton with a golden-yellow border, bare legs, expensive leather sandals, ink-stained fingers, an open bronze drawing compass in the right hand and a small wax tablet in the left, stiff haughty posture with the nose up,
```

- **A-062** · `eudoro_idle_se.png` · `idle` · 2 quadros (tira 64×32) · 600 ms por quadro, em loop · `criar`
  ```text
  [EUDORO] facing south in front view, stiff haughty posture with the nose up, the open compass in the right hand and the wax tablet in the left, [CHARACTER] [STYLE_CHAR]
  ```
  **Quadros:** Quadro 1 = a rotação SE do personagem criado a partir do sprite sul deste prompt (§3.3, `criar`). Quadro 2 à mão: suba 1 px as linhas da cabeça, dos ombros e do peito, com pés e sombra idênticos.
- **A-063** · `eudoro_idle_ne.png` · `idle` · 2 quadros (tira 64×32) · 600 ms por quadro, em loop · `rotação + mão`
  ```text
  [EDIT] Frame 1 is the north-east rotation of the character created for eudoro_idle_se.png, with zero extra generations. Frame 2 copies frame 1 with the head, shoulder and chest rows moved up 1 px, while the feet and the shadow stay identical.
  ```
- **A-064** · `eudoro_walk_se.png` · `walk` · 4 quadros (tira 128×32) · 150 ms por quadro, em loop · `template walk + mão`
  ```text
  Facing south-east in 3/4 front view: walking with a gangly stride, stiff elbows and dragging sandals.
  ```
  **Seleção:** Template `walking-4-frames`, `directions` = ["south-east"], este texto em `action_description`. Confira os pés plantados na linha 29; depois, à mão, suba 1 px a cabeça e o tronco nos dois quadros de passagem (pernas juntas).
- **A-065** · `eudoro_walk_ne.png` · `walk` · 4 quadros (tira 128×32) · 150 ms por quadro, em loop · `template walk + mão`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: walking with a gangly stride, stiff elbows and dragging sandals.
  ```
  **Seleção:** Template `walking-4-frames`, `directions` = ["north-east"], este texto em `action_description`. Confira os pés plantados na linha 29; depois, à mão, suba 1 px a cabeça e o tronco nos dois quadros de passagem (pernas juntas).
- **A-066** · `eudoro_talk_se.png` · `talk` · 3 quadros (tira 96×32) · 200 ms por quadro, em loop · `animar loop`
  ```text
  Facing south-east in 3/4 front view: talks in place, the mouth opening and closing while the compass hand lifts and wags once, then settles back to the idle pose.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle SE. Fique com 3, em ordem: quadro 1 da idle (boca fechada) · boca aberta com o gesto no auge · boca entreaberta, mão voltando. Descarte o último quadro gerado se ele repetir o primeiro.
- **A-067** · `eudoro_talk_ne.png` · `talk` · 3 quadros (tira 96×32) · 200 ms por quadro, em loop · `animar loop`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: talks in place with small nods of the head while the compass hand lifts and wags once, then settles back to the idle pose.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle NE. Fique com 3, em ordem: quadro 1 da idle · aceno de cabeça com o gesto no auge · mão voltando. Descarte o último quadro gerado se ele repetir o primeiro.
- **A-068** · `eudoro_think_se.png` · `think` · 3 quadros (tira 96×32) · 500 ms por quadro, em loop · `animar loop`
  ```text
  Facing south-east in 3/4 front view: thinks, idle in the scenery: touches his chin with the compass tip, rolls the eyes upward, then taps the wax tablet with the compass.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle SE. Fique com 3, em ordem: quadro 1 da idle · ponta do compasso no queixo, olhos para cima · compasso bate na tabuinha. Descarte o último quadro gerado se ele repetir o primeiro.
- **A-069** · `eudoro_think_ne.png` · `think` · 3 quadros (tira 96×32) · 500 ms por quadro, em loop · `animar loop`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: thinks, idle in the scenery: the head tilts down and the compass tip rises to the chin, then taps the wax tablet.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle NE. Fique com 3, em ordem: quadro 1 da idle · cabeça inclina, compasso no queixo · compasso bate na tabuinha. Descarte o último quadro gerado se ele repetir o primeiro.
- **A-070** · `portrait_eudoro_neutral.png` · retrato 32×32 · `retrato`
  ```text
  [PORTRAIT] Source: frame 1 of eudoro_idle_se.png. Check: haughty look with the nose up.
  ```
- **A-071** · `portrait_eudoro_shaken.png` · retrato 32×32 · `expressão`
  ```text
  [FACE] Source: portrait_eudoro_neutral.png, mask on the eyes, brows and mouth. Description: flustered, eyes darting to the side, mouth open mid-stammer.
  ```

#### Cerâmico (`kerameikos.json`)

- **A-003** · `prop_amphora_stack.png` · 32×32 · `map-object+init` — Piloto do ART-03 #3 (terracota e figuras vermelhas). `init_image` = o placeholder. Argila na rampa `clay_*` (palette v2), para a ânfora se destacar do chão ocre. Verniz negro = `hair_dark` com núcleo `outline`.
  ```text
  A stack of five terracotta amphorae, three lying at the bottom and two leaning on top, terracotta-orange clay with near-black glaze bands and tiny red-figure details left in bare clay color, [PROP] [STYLE_ENV]
  ```
- **A-072** · `floor_dirt.png` · 32×16 · `iso-tile thin` — Compartilhado: estrada do Cerâmico e entrada da Ágora. Dono único: Trilha A.
  ```text
  Dusty dirt road, warm orange-ochre base with shallow rust-brown ruts and 4 to 6 small isolated dark brown pebbles, [FLOOR] [STYLE_ENV]
  ```
- **A-073** · `floor_dry_clay.png` · 32×16 · `iso-tile thin` — A mais clara das três terras.
  ```text
  Sun-dried pale clay courtyard floor, light sand-ochre base with a few warm orange-ochre blotches and at most two thin 1-pixel dark brown cracks, [FLOOR] [STYLE_ENV]
  ```
- **A-074** · `floor_packed_dirt.png` · 32×16 · `iso-tile thin` — Mais escura que `floor_dirt` e mais que a argila.
  ```text
  Packed dark earth floor of a potter's workshop, rust-brown base with scattered warm orange-ochre grains and 4 to 6 tiny isolated dark brown pebbles, darker than the road, [FLOOR] [STYLE_ENV]
  ```
- **A-075** · `floor_straw.png` · 32×16 · `iso-tile thin` — Riscos diagonais de 3 px (GDD §6.1).
  ```text
  Loose golden straw scattered over packed earth, rust-brown earth with short diagonal 3-pixel golden-yellow straw strokes and 1 to 2 bright yellow highlights, [FLOOR] [STYLE_ENV]
  ```
- **A-076** · `block_kiln.png` · 32×40 · `bitforge+init` — Quadro estático. Corpo na rampa da argila (`clay_mid`, `clay_shade`, palette v2); a brasa: núcleo `gold_light`, borda `red_light`.
  ```text
  A potter's kiln of fired clay brick with a low domed top and a dark arched opening on the left face glowing with embers, terracotta-orange and deep terracotta brick with rust-brown soot, [BLOCK] [STYLE_ENV]
  ```
- **A-077** · `block_kiln_glow.png` · 32×40 · 2 quadros (tira 64×40) · `ciclo de paleta (Aseprite)` — 300 ms por quadro, loop.
  ```text
  [EDIT] 2 frames by palette cycling on block_kiln.png, zero generations: frame 1 is identical to the static file; in frame 2, inside the arched opening only, red_light becomes gold_light and red_mid becomes red_light, so the embers brighten. Every other pixel stays identical.
  ```
- **A-078** · `wall_brick.png` · 32×32 · `iso-tile block` — Compartilhado (oficina e casas da Ágora). Contorno completo; o `BlockTileSeams` apaga a borda vertical direita.
  ```text
  Sun-dried mud-brick wall block, a 16-pixel diamond top over 16 pixels of wall, brick courses with 1-pixel dark brown mortar joints, light sand-ochre top, warm orange-ochre left face, rust-brown right face, [BLOCK] [STYLE_ENV]
  ```
- **A-079** · `wall_brick_tall.png` · 32×48 · `Aseprite (edição)` — Parede alta da referência (~32 px de face), só nas bordas de trás das salas (§5.5). Compartilhada, como a `wall_brick`.
  ```text
  [EDIT] From the approved wall_brick.png, zero generations: keep the 16-px diamond top and extend both faces down to 32 px of wall by repeating the brick courses and the 1-px ochre_dark mortar joints, same colors and outline; the base diamond ends on the bottom row of the 32x48 canvas.
  ```
- **A-080** · `prop_potters_wheel.png` · 32×28 · `map-object+init` — Onde a Kleia começa. `init_image` = o placeholder. Gere em 32×32 e corte as 4 linhas transparentes de cima.
  ```text
  A low wooden potter's kick wheel with a lump of wet terracotta-orange clay on the top disc and spokes on the lower flywheel, warm orange-ochre and rust-brown wood, [PROP] [STYLE_ENV]
  ```
- **A-081** · `prop_potters_wheel_spin.png` · 32×28 · 4 quadros (tira 128×28) · `animar tile` — 120 ms por quadro, loop. `first_frame` = `last_frame` = o estático aprovado; fique com 4 quadros distintos, o 1 igual ao estático. Se os raios saírem tortos, desenhe o giro à mão (4 posições dos raios).
  ```text
  The potter's wheel turns steadily: the spokes and a notch in the clay rotate through one full cycle and come back to the starting position.
  ```

#### Overlays de sistema

- **A-082** · `fog.png` · 32×16 · `manual (Aseprite)` — Cor: `void` `#000000` (palette v2, §4.2), a mesma do fundo: a névoa some no vazio fora do mapa. Substitui o `#101020` dos placeholders. As bordas `fog_edge_*` são geradas pelo `SandboxTools` a partir deste arquivo: não desenhe.
  ```text
  [MANUAL] 32x16 canvas. Solid fog diamond covering exactly one floor cell (use the alpha of the placeholder as the mask), filled with void, lineless and flat.
  ```
- **A-083** · `hl_maieutics.png` · 32×16 · `manual (Aseprite)` — Mesmo esquema do `hl_move`, com a cor própria da MAIÊUTICA (§4.8).
  ```text
  [MANUAL] 32x16 canvas. Maieutics range highlight: an opaque filled diamond in two tones, purple_light fill with a 1-pixel purple_mid joint on the two upper edges, fully opaque.
  ```
- **A-084** · `hl_move.png` · 32×16 · `manual (Aseprite)` — Verde-amarelado em 2 tons com a junta visível, como na referência (decisão de 21/09/2026). A rampa `move_*` (palette v2) é exclusiva deste realce (§4.8).
  ```text
  [MANUAL] 32x16 canvas. Movement range highlight: an opaque filled diamond in two tones, move_light fill with a 1-pixel move_mid joint on the two upper edges, fully opaque.
  ```
- **A-085** · `hl_refute.png` · 32×16 · `manual (Aseprite)` — Mesmo esquema do `hl_move`, com a cor própria do REFUTAR (§4.8).
  ```text
  [MANUAL] 32x16 canvas. Refute range highlight: an opaque filled diamond in two tones, red_light fill with a 1-pixel red_mid joint on the two upper edges, fully opaque.
  ```
- **A-086** · `hl_select.png` · 32×16 · `manual (Aseprite)` — O pulso de 500 ms hoje alterna com vazio no código.
  ```text
  [MANUAL] 32x16 canvas. Cursor cell highlight: a 1-pixel gold_light diamond outline with gold_mid corner ticks, empty inside so the floor stays visible.
  ```

#### Interface

- **A-087** · `bar_audience.png` · 64×8 · `manual (Aseprite)`
  ```text
  [MANUAL] 64x8 canvas. Audience bar: a 1-pixel outline frame, gold_mid fill with a 1-pixel gold_light highlight on the top row.
  ```
- **A-088** · `bar_coherence.png` · 64×8 · `manual (Aseprite)`
  ```text
  [MANUAL] 64x8 canvas. Coherence bar: a 1-pixel outline frame, aegean_mid fill with a 1-pixel aegean_light highlight on the top row.
  ```
- **A-089** · `box_dialogue.png` · 208×48 · `manual (Aseprite)` — O placeholder mede 160×40, mas o código já desenha em 208×48. Por dentro da borda e da margem cabem 3 linhas de 12 px da Ark Pixel.
  ```text
  [MANUAL] 208x48 canvas. Dialogue box: same style as box_menu, void fill, a 1-pixel ui_white border, 3 pixels of inner margin; the portrait frame sits at the left.
  ```
- **A-090** · `box_menu.png` · 96×88 · `manual (Aseprite)` — Tamanho atual do código. A referência pede caixa do tamanho do conteúdo: vira 9-slice (§11). Com a Ark Pixel, as 6 linhas de 12 px somam 72 px de texto e, com a borda e a margem, a caixa mede cerca de 80 px de altura; a largura sai do item mais largo na amostra.
  ```text
  [MANUAL] 96x88 canvas. Command menu box: void fill, a 1-pixel ui_white border, 3 pixels of inner margin, square corners with the corner pixel cut.
  ```
- **A-091** · `cursor.png` · 8×8 · `manual (Aseprite)` — A piscada é código.
  ```text
  [MANUAL] 8x8 canvas. Menu cursor: a small right-pointing triangle arrow in ui_white, at most 7 pixels tall, vertically centred on a 12-pixel menu row.
  ```
- **A-092** · `frame_portrait.png` · 40×40 · `manual (Aseprite)` — A janela de 32×32 casa com `BattleHudTools.PortraitInner = 32`.
  ```text
  [MANUAL] 40x40 canvas. Portrait frame: a double gold border (gold_light outer line, gold_dark inner line) around a transparent 32x32 window centered in the frame.
  ```

#### Ícones de falácia (16×16)

- **A-093** · `icon_badge_formal.png` · 16×16 · `manual (Aseprite)` — Moldura dos ícones formais.
  ```text
  [MANUAL] 16x16 canvas. Badge template for formal fallacies: the same shape as icon_badge_informal, purple_mid fill, purple_light on the upper-left inner edge and purple_dark on the lower-right inner edge, a clear 10x10 center.
  ```
- **A-094** · `icon_badge_informal.png` · 16×16 · `manual (Aseprite)` — Moldura única de todos os ícones informais: o glifo de cada falácia é composto por cima (§3.2).
  ```text
  [MANUAL] 16x16 canvas. Badge template for informal fallacies: a rounded 16x16 badge with a 1-pixel outline border, red_mid fill, a 1-pixel red_light line on the upper-left inner edge and red_dark on the lower-right inner edge, leaving a clear 10x10 center for the glyph.
  ```
- **A-095** · `icon_badge_paradox.png` · 16×16 · `manual (Aseprite)` — Moldura dos paradoxos.
  ```text
  [MANUAL] 16x16 canvas. Badge template for paradoxes: the same shape as icon_badge_informal, aegean_mid fill, aegean_light on the upper-left inner edge and aegean_shade on the lower-right inner edge, a clear 10x10 center.
  ```
- **A-096** · `icon_fallacy_ad_hominem.png` · 16×16 · `glifo + moldura` — Ad hominem (Informal) · moldura `icon_badge_informal.png`
  ```text
  A hand pointing with the index finger extended, [GLYPH] [STYLE_ENV]
  ```
  **Primeiro glifo:** desenhe à mão ou gere com `create-image-pixen` em 16×16; depois de aprovado, ele é a `style_images` de todos os outros glifos (`generate-with-style-v2`, 64 candidatos a 16 px).
- **A-097** · `icon_fallacy_appeal_to_authority.png` · 16×16 · `glifo + moldura` — Apelo à autoridade (Informal) · moldura `icon_badge_informal.png`
  ```text
  A laurel crown, [GLYPH] [STYLE_ENV]
  ```
- **A-098** · `icon_fallacy_appeal_to_emotion.png` · 16×16 · `glifo + moldura` — Apelo à emoção (Informal) · moldura `icon_badge_informal.png`
  ```text
  A weeping tragic theatre mask, [GLYPH] [STYLE_ENV]
  ```
- **A-099** · `icon_fallacy_appeal_to_ignorance.png` · 16×16 · `glifo + moldura` — Apelo à ignorância (Informal) · moldura `icon_badge_informal.png`
  ```text
  A closed eye with a blindfold band across it, [GLYPH] [STYLE_ENV]
  ```
- **A-100** · `icon_fallacy_equivocation.png` · 16×16 · `glifo + moldura` — Equívoco (Informal) · moldura `icon_badge_informal.png`
  ```text
  One amphora split vertically into an off-white half and a light grey half, [GLYPH] [STYLE_ENV]
  ```
- **A-101** · `icon_fallacy_false_dilemma.png` · 16×16 · `glifo + moldura` — Falso dilema (Informal) · moldura `icon_badge_informal.png`
  ```text
  A two-pan balance scale, [GLYPH] [STYLE_ENV]
  ```
- **A-102** · `icon_fallacy_hasty_generalization.png` · 16×16 · `glifo + moldura` — Generalização apressada (Informal) · moldura `icon_badge_informal.png`
  ```text
  A single small fish, [GLYPH] [STYLE_ENV]
  ```
- **A-103** · `icon_fallacy_invalid_disjunctive_syllogism.png` · 16×16 · `glifo + moldura` — Silogismo disjuntivo inválido (Formal) · moldura `icon_badge_formal.png`
  ```text
  A Y-shaped fork with both branch tips highlighted, [GLYPH] [STYLE_ENV]
  ```
- **A-104** · `icon_fallacy_slippery_slope.png` · 16×16 · `glifo + moldura` — Ladeira escorregadia (Informal) · moldura `icon_badge_informal.png`
  ```text
  A ball at the top of a steep slope, [GLYPH] [STYLE_ENV]
  ```
- **A-105** · `icon_fallacy_straw_man.png` · 16×16 · `glifo + moldura` — Espantalho (Informal) · moldura `icon_badge_informal.png`
  ```text
  A straw scarecrow figure, [GLYPH] [STYLE_ENV]
  ```
- **A-106** · `icon_fallacy_undistributed_middle.png` · 16×16 · `glifo + moldura` — Termo médio não distribuído (Formal) · moldura `icon_badge_formal.png`
  ```text
  Two small separate rings side by side with a dot between them, [GLYPH] [STYLE_ENV]
  ```

### 9.2 P2 — Ato II e HUD da referência

105 arquivos (A-107 a A-211), 214 quadros.

#### Kleia de Cerâmico · `kleia` · COMPLETO

Ficha, chave de cor e descritor em §9.1.

- **A-107** · `kleia_listen_se.png` · `listen` · 3 quadros (tira 96×32) · 250 ms por quadro, sem loop · `animar`
  ```text
  Facing south-east in 3/4 front view: listens closely: cups the right hand behind her ear and leans the head toward the opponent, then narrows the eyes, focused.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle SE, sem `last_frame`. Fique com 3: quadro 1 da idle · mão em concha atrás da orelha, cabeça inclinada · olhos apertados, focada.
- **A-108** · `kleia_listen_ne.png` · `listen` · 3 quadros (tira 96×32) · 250 ms por quadro, sem loop · `animar`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: listens closely: cups the right hand behind her ear and leans the head toward the opponent, then holds the focused pose.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle NE, sem `last_frame`. Fique com 3: quadro 1 da idle · mão em concha atrás da orelha, cabeça inclinada · segura a pose.
- **A-109** · `kleia_invert_se.png` · `invert` · 4 quadros (tira 128×32) · 200 ms por quadro, sem loop · `animar`
  ```text
  Facing south-east in 3/4 front view: turns the attack around: the open right hand reaches toward the opponent, sweeps back over her shoulder as if turning the argument toward the crowd, and ends open at her side with a confident half smile.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle SE, sem `last_frame`. Fique com 4: quadro 1 da idle · mão aberta estendida para o oponente · varre por cima do ombro · mão aberta ao lado, meio sorriso.
- **A-110** · `kleia_invert_ne.png` · `invert` · 4 quadros (tira 128×32) · 200 ms por quadro, sem loop · `animar`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: turns the attack around: the open right hand reaches toward the opponent, sweeps back over her shoulder as if turning the argument toward the crowd, and ends open at her side with the head lifted.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle NE, sem `last_frame`. Fique com 4: quadro 1 da idle · mão aberta estendida para o oponente · varre por cima do ombro · mão aberta ao lado, cabeça erguida.

#### Damaris de Abdera · `damaris` · CHEFE

Ficha, chave de cor e descritor em §9.1.

- **A-111** · `damaris_argue_se.png` · `argue` · 4 quadros (tira 128×32) · 200 ms por quadro, sem loop · `animar volta`
  ```text
  Facing south-east in 3/4 front view: weighs two ideas with her bare empty hands: both hands rise to chest height with the palms facing each other, tilt like a balance, then lower back to her sides while the gaze stays fixed.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle SE. Fique com 4: parada · mãos vazias sobem, palmas frente a frente · inclinam como balança · voltam ao lado do corpo.
- **A-112** · `damaris_argue_ne.png` · `argue` · 4 quadros (tira 128×32) · 200 ms por quadro, sem loop · `animar volta`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: weighs two ideas with her bare empty hands: both hands rise to chest height with the palms facing each other, tilt like a balance, then lower back to her sides while the head stays still.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle NE. Fique com 4: parada · mãos vazias sobem, palmas frente a frente · inclinam como balança · voltam ao lado do corpo.
- **A-113** · `damaris_concede_se.png` · `concede` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing south-east in 3/4 front view: shifts into a composed concession: a small respectful nod with the back straight and the bare hands clasped in front of the waist.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle SE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: aceno contido, costas retas, mãos cruzadas na cintura.
- **A-114** · `damaris_concede_ne.png` · `concede` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: shifts into a composed concession: a small respectful nod with the back straight and the bare hands clasped in front of the waist.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle NE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: aceno contido, costas retas, mãos cruzadas na cintura.
- **A-115** · `damaris_coherence_damage_se.png` · `coherence_damage` · 3 quadros (tira 96×32) · 100 ms por quadro, sem loop · `animar volta`
  ```text
  Facing south-east in 3/4 front view: reels from a hit: flinches with the eyes shut as the head snaps back, the upper body leans back to the left with the feet planted, then recovers toward the idle pose.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle SE. Fique com 3: recuo com os olhos fechados e a cabeça para trás · tronco 2 px para a esquerda, pés plantados · meio caminho de volta ao idle.
- **A-116** · `damaris_coherence_damage_ne.png` · `coherence_damage` · 3 quadros (tira 96×32) · 100 ms por quadro, sem loop · `animar volta`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: reels from a hit: the head jerks back and the shoulders hunch, the upper body leans back to the left with the feet planted, then recovers toward the idle pose.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle NE. Fique com 3: cabeça sacode para trás e ombros encolhem · tronco 2 px para a esquerda, pés plantados · meio caminho de volta ao idle.
- **A-117** · `damaris_defeat_se.png` · `defeat` · 6 quadros (tira 192×32) · 220 ms por quadro, sem loop, segura o último · `animar`
  ```text
  Facing south-east in 3/4 front view: is defeated with dignity: takes a small step back, lowers the head and kneels on one knee with a straight back, then on both knees with the hands resting on them and the head bowed.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle SE, sem `last_frame`. Fique com 6: passo atrás · cabeça baixa · um joelho, costas retas · dois joelhos · mãos nos joelhos · pose final (segura).
- **A-118** · `damaris_defeat_ne.png` · `defeat` · 6 quadros (tira 192×32) · 220 ms por quadro, sem loop, segura o último · `animar`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: is defeated with dignity: takes a small step back, lowers the head and kneels on one knee with a straight back, then on both knees with the hands resting on them and the head bowed.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle NE, sem `last_frame`. Fique com 6: passo atrás · cabeça baixa · um joelho, costas retas · dois joelhos · mãos nos joelhos · pose final (segura).
- **A-119** · `damaris_victory_se.png` · `victory` · 4 quadros (tira 128×32) · 300 ms por quadro, sem loop, segura o último · `animar`
  ```text
  Facing south-east in 3/4 front view: wins calmly: gives a slow single nod, then folds the bare hands in front of the waist in a calm final pose.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle SE, sem `last_frame`. Fique com 4: parada · um aceno lento · mãos cruzadas na cintura · pose final calma (segura).
- **A-120** · `damaris_victory_ne.png` · `victory` · 4 quadros (tira 128×32) · 300 ms por quadro, sem loop, segura o último · `animar`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: wins calmly: gives a slow single nod, then folds the bare hands in front of the waist in a calm final pose.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle NE, sem `last_frame`. Fique com 4: parada · um aceno lento · mãos cruzadas na cintura · pose final calma (segura).

#### Eudoro · `eudoro` · DUELISTA + think

Ficha, chave de cor e descritor em §9.1.

- **A-121** · `eudoro_argue_se.png` · `argue` · 3 quadros (tira 96×32) · 200 ms por quadro, sem loop · `animar`
  ```text
  Facing south-east in 3/4 front view: presents an argument with a signature gesture: raises the open compass, draws a line in the air with it, then points the compass tip at the listener with the nose up.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle SE, sem `last_frame`. Fique com 3: início do gesto · auge do gesto · pose final segurada (a assinatura do prompt).
- **A-122** · `eudoro_argue_ne.png` · `argue` · 3 quadros (tira 96×32) · 200 ms por quadro, sem loop · `animar`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: presents an argument with a signature gesture: raises the open compass, draws a line in the air with it, then points the compass tip forward with the head lifted.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle NE, sem `last_frame`. Fique com 3: início do gesto · auge do gesto · pose final segurada (a assinatura do prompt).
- **A-123** · `eudoro_concede_se.png` · `concede` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing south-east in 3/4 front view: shifts into a held reluctant concession: the shoulders lowered, the head slightly down, one palm turned up, the compass lowered, awkward and embarrassed.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle SE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: ombros baixos, cabeça baixa, uma palma para cima.
- **A-124** · `eudoro_concede_ne.png` · `concede` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: shifts into a held reluctant concession: the shoulders lowered, the head slightly down, one palm turned up, the compass lowered, awkward and embarrassed.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle NE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: ombros baixos, cabeça baixa, uma palma para cima.
- **A-125** · `eudoro_coherence_damage_se.png` · `coherence_damage` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing south-east in 3/4 front view: shifts into a held pose, struck by an argument: flinching with the eyes shut, the head and upper body leaning back, one hand raised defensively, the feet planted on the ground line.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle SE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: recuo com os olhos fechados, uma mão erguida, pés plantados.
- **A-126** · `eudoro_coherence_damage_ne.png` · `coherence_damage` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: shifts into a held pose, struck by an argument: the head jerks back and the shoulders hunch, one hand raised defensively, the feet planted on the ground line.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle NE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: cabeça para trás, ombros encolhidos, uma mão erguida, pés plantados.
- **A-127** · `eudoro_defeat_se.png` · `defeat` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing south-east in 3/4 front view: shifts into a held pose, defeated: kneeling on one knee, the head bowed, the arms hanging.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle SE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: um joelho no chão, cabeça baixa, braços caídos.
- **A-128** · `eudoro_defeat_ne.png` · `defeat` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: shifts into a held pose, defeated: kneeling on one knee, the head bowed, the arms hanging.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle NE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: um joelho no chão, cabeça baixa, braços caídos.

#### A Pítia · `pythia` · PÍTIA

- **Altura no canvas:** cerca de 26 px com a trípode (sentada).
- **Onde fica:** `floor_rift` ao lado (Delfos).
- **Direções:** só SE; view fixada no piloto (§3.2), a mesma para o elenco inteiro.
- **Arquivos:** 3 tiras (13 quadros) e 2 retratos, em P2.
- **Chave de cor:** outline, outline_blue, marble_light, marble_mid, marble_shade (manto branco), purple_light, purple_mid, purple_dark (véu), skin_mid, skin_shade (mãos e queixo), gold_mid, gold_dark, ochre_dark (trípode de bronze), aegean_light (vapor, se entrar) — 14 cores.
- **Silhueta e atuação:** Sempre sentada na trípode, que faz parte do sprite (ator não pode ocupar célula com bloco). Massa triangular de véu; os olhos não aparecem e a idade é indeterminada. O véu translúcido é xadrez de 50% entre `purple_mid` e `marble_light`, desenhado à mão depois da geração. Só SE: o espelho dá SW; posicione-a no mapa virada para a entrada da Kleia.

Descritor `[PYTHIA]`:

```text
the Pythia, oracle priestess of Delphi of indeterminate age, always seated on a tall golden bronze tripod that is part of the sprite, about 26 pixels tall including the tripod, a long off-white robe under a royal-purple veil that covers her eyes and upper face, hands resting on her knees, still and trance-like,
```

- **A-129** · `pythia_idle_se.png` · `idle` · 2 quadros (tira 64×32) · 600 ms por quadro, em loop · `sentado: base`
  ```text
  [PYTHIA] seated on the tall bronze tripod facing south-east in 3/4 front view, the hands resting on her knees, the veil still, [CHARACTER] [STYLE_CHAR]
  ```
  **Quadros:** Quadro 1 = a base sentada SE gerada com este prompt (§3.3, `sentado: base`). Quadro 2 à mão: desloque 1 px a barra do véu, com trípode, corpo e sombra idênticos.
- **A-130** · `pythia_talk_se.png` · `talk` · 3 quadros (tira 96×32) · 200 ms por quadro, em loop · `mão (Aseprite)`
  ```text
  [EDIT] Built from frame 1 of pythia_idle_se.png, zero generations: frame 1 is that frame with the lips closed; frame 2 opens the lips below the veil by 1 px and lifts the right hand 1 px from the knee; frame 3 half opens the lips and returns the hand. The tripod, the veil edge and the shadow stay identical.
  ```
- **A-131** · `pythia_trance_se.png` · `trance` · 8 quadros (tira 256×32) · 200 ms por quadro, em loop · `animar loop`
  ```text
  Facing south-east in 3/4 front view: falls into an oracle trance: the head rolls slowly back, both arms rise with the palms up while the veil trembles, then everything settles back to the seated pose.
  ```
  **Seleção:** `first_frame` = `last_frame` = base sentada SE. Fique com 8, em ordem: base sentada · 2 e 3: a cabeça rola para trás · 4 e 5: braços sobem com as palmas para cima, o véu treme · 6: auge · 7 e 8: tudo volta à base. Descarte o último quadro gerado se ele repetir o primeiro.
- **A-132** · `portrait_pythia_neutral.png` · retrato 32×32 · `retrato`
  ```text
  [PORTRAIT] Source: frame 1 of pythia_idle_se.png. Check: the veil over the eyes, calm closed lips.
  ```
- **A-133** · `portrait_pythia_trance.png` · retrato 32×32 · `expressão`
  ```text
  [FACE] Source: portrait_pythia_neutral.png, mask on the mouth and the lower edge of the veil. Description: lips parted in a trance below the veil.
  ```

#### Acadêmico do selo (I.10) · `academic` · NPC mínimo

- **Altura no canvas:** 24 px.
- **Onde fica:** Ágora (fim do Ato I).
- **Direções:** SE e NE; view fixada no piloto (§3.2), a mesma para o elenco inteiro.
- **Arquivos:** 4 tiras (10 quadros) e 2 retratos, em P2.
- **Chave de cor:** outline, outline_blue, skin_light, skin_mid, skin_shade, marble_shade, marble_dark (cabelo grisalho), marble_light, marble_mid (himátion branco), aegean_shade (borda), gold_mid, gold_dark (selo de bronze), ochre_shade (sandálias) — 13 cores.
- **Silhueta e atuação:** Sem ficha no GDD (proposta). Digno, parado, o selo de bronze na mão direita. Não confundir com o Eudoro: mais velho, grisalho, borda azul-escura em vez de dourada.

Descritor `[ACADEMIC]`:

```text
a 50-year-old scholar of Plato's Academy, about 24 pixels tall, short grey hair and beard, a knee-length off-white chiton under a rectangular off-white himation with a deep sea-blue border draped over the left shoulder, leather sandals, a small golden bronze seal in the right hand, dignified and calm,
```

- **A-134** · `academic_idle_se.png` · `idle` · 2 quadros (tira 64×32) · 600 ms por quadro, em loop · `criar`
  ```text
  [ACADEMIC] facing south in front view, dignified and calm, the bronze seal held in the right hand, [CHARACTER] [STYLE_CHAR]
  ```
  **Quadros:** Quadro 1 = a rotação SE do personagem criado a partir do sprite sul deste prompt (§3.3, `criar`). Quadro 2 à mão: suba 1 px as linhas da cabeça, dos ombros e do peito, com pés e sombra idênticos.
- **A-135** · `academic_idle_ne.png` · `idle` · 2 quadros (tira 64×32) · 600 ms por quadro, em loop · `rotação + mão`
  ```text
  [EDIT] Frame 1 is the north-east rotation of the character created for academic_idle_se.png, with zero extra generations. Frame 2 copies frame 1 with the head, shoulder and chest rows moved up 1 px, while the feet and the shadow stay identical.
  ```
- **A-136** · `academic_talk_se.png` · `talk` · 3 quadros (tira 96×32) · 200 ms por quadro, em loop · `animar loop`
  ```text
  Facing south-east in 3/4 front view: talks in place, the mouth opening and closing while offers the bronze seal forward with the right hand, then settles back to the idle pose.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle SE. Fique com 3, em ordem: quadro 1 da idle (boca fechada) · boca aberta com o gesto no auge · boca entreaberta, mão voltando. Descarte o último quadro gerado se ele repetir o primeiro.
- **A-137** · `academic_talk_ne.png` · `talk` · 3 quadros (tira 96×32) · 200 ms por quadro, em loop · `animar loop`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: talks in place with small nods of the head while offers the bronze seal forward with the right hand, then settles back to the idle pose.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle NE. Fique com 3, em ordem: quadro 1 da idle · aceno de cabeça com o gesto no auge · mão voltando. Descarte o último quadro gerado se ele repetir o primeiro.
- **A-138** · `portrait_academic_neutral.png` · retrato 32×32 · `retrato`
  ```text
  [PORTRAIT] Source: frame 1 of academic_idle_se.png. Check: calm dignified look.
  ```
- **A-139** · `portrait_academic_shaken.png` · retrato 32×32 · `expressão`
  ```text
  [FACE] Source: portrait_academic_neutral.png, mask on the eyes, brows and mouth. Description: surprised, brows raised, eyes wide.
  ```

#### Irmão Dídimos A · `didymos_a` · DUELISTA

- **Altura no canvas:** 24 px.
- **Onde fica:** Ato II (batalha dupla).
- **Direções:** SE e NE; view fixada no piloto (§3.2), a mesma para o elenco inteiro.
- **Arquivos:** 14 tiras (30 quadros) e 2 retratos, em P2.
- **Chave de cor:** outline, outline_blue, skin_light, skin_mid, skin_shade, hair_dark, hair_mid, marble_mid, marble_shade (túnica), aegean_shade (faixa), ochre_mid, ochre_shade, ochre_dark (maquete de navio), marble_light (vela) — 14 cores.
- **Silhueta e atuação:** Sem ficha no GDD (proposta). Gêmeos marinheiros de 25 anos, cada um com uma maquete de navio: o paradoxo do Navio de Teseu. O B é a mesma folha com a faixa em `red_dark` (troca de paleta, sem gerar).

Descritor `[DIDYMOS_A]`:

```text
one of the twin brothers Didymos, a 25-year-old sailor, about 24 pixels tall, short curly black hair, a short pale grey-beige sailor tunic with a deep sea-blue sash, bare legs and bare feet, holding a small wooden model ship with an off-white sail in both hands, cheerful,
```

- **A-140** · `didymos_a_idle_se.png` · `idle` · 2 quadros (tira 64×32) · 600 ms por quadro, em loop · `criar`
  ```text
  [DIDYMOS_A] facing south in front view, holding the model ship in both hands at chest height, [CHARACTER] [STYLE_CHAR]
  ```
  **Quadros:** Quadro 1 = a rotação SE do personagem criado a partir do sprite sul deste prompt (§3.3, `criar`). Quadro 2 à mão: suba 1 px as linhas da cabeça, dos ombros e do peito, com pés e sombra idênticos.
- **A-141** · `didymos_a_idle_ne.png` · `idle` · 2 quadros (tira 64×32) · 600 ms por quadro, em loop · `rotação + mão`
  ```text
  [EDIT] Frame 1 is the north-east rotation of the character created for didymos_a_idle_se.png, with zero extra generations. Frame 2 copies frame 1 with the head, shoulder and chest rows moved up 1 px, while the feet and the shadow stay identical.
  ```
- **A-142** · `didymos_a_walk_se.png` · `walk` · 4 quadros (tira 128×32) · 150 ms por quadro, em loop · `template walk + mão`
  ```text
  Facing south-east in 3/4 front view: walking with a rolling sailor's gait, the ship held steady.
  ```
  **Seleção:** Template `walking-4-frames`, `directions` = ["south-east"], este texto em `action_description`. Confira os pés plantados na linha 29; depois, à mão, suba 1 px a cabeça e o tronco nos dois quadros de passagem (pernas juntas).
- **A-143** · `didymos_a_walk_ne.png` · `walk` · 4 quadros (tira 128×32) · 150 ms por quadro, em loop · `template walk + mão`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: walking with a rolling sailor's gait, the ship held steady.
  ```
  **Seleção:** Template `walking-4-frames`, `directions` = ["north-east"], este texto em `action_description`. Confira os pés plantados na linha 29; depois, à mão, suba 1 px a cabeça e o tronco nos dois quadros de passagem (pernas juntas).
- **A-144** · `didymos_a_talk_se.png` · `talk` · 3 quadros (tira 96×32) · 200 ms por quadro, em loop · `animar loop`
  ```text
  Facing south-east in 3/4 front view: talks in place, the mouth opening and closing while one hand leaves the ship and gestures, then settles back to the idle pose.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle SE. Fique com 3, em ordem: quadro 1 da idle (boca fechada) · boca aberta com o gesto no auge · boca entreaberta, mão voltando. Descarte o último quadro gerado se ele repetir o primeiro.
- **A-145** · `didymos_a_talk_ne.png` · `talk` · 3 quadros (tira 96×32) · 200 ms por quadro, em loop · `animar loop`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: talks in place with small nods of the head while one hand leaves the ship and gestures, then settles back to the idle pose.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle NE. Fique com 3, em ordem: quadro 1 da idle · aceno de cabeça com o gesto no auge · mão voltando. Descarte o último quadro gerado se ele repetir o primeiro.
- **A-146** · `didymos_a_argue_se.png` · `argue` · 3 quadros (tira 96×32) · 200 ms por quadro, sem loop · `animar`
  ```text
  Facing south-east in 3/4 front view: presents an argument with a signature gesture: raises the model ship, turns it to show one plank of a different color, then holds it out to the listener with a questioning grin.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle SE, sem `last_frame`. Fique com 3: início do gesto · auge do gesto · pose final segurada (a assinatura do prompt).
- **A-147** · `didymos_a_argue_ne.png` · `argue` · 3 quadros (tira 96×32) · 200 ms por quadro, sem loop · `animar`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: presents an argument with a signature gesture: raises the model ship, turns it to show one plank of a different color, then holds it out to the listener.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle NE, sem `last_frame`. Fique com 3: início do gesto · auge do gesto · pose final segurada (a assinatura do prompt).
- **A-148** · `didymos_a_concede_se.png` · `concede` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing south-east in 3/4 front view: shifts into a held reluctant concession: the shoulders lowered, the head slightly down, one palm turned up, the ship lowered to his waist.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle SE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: ombros baixos, cabeça baixa, uma palma para cima.
- **A-149** · `didymos_a_concede_ne.png` · `concede` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: shifts into a held reluctant concession: the shoulders lowered, the head slightly down, one palm turned up, the ship lowered to his waist.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle NE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: ombros baixos, cabeça baixa, uma palma para cima.
- **A-150** · `didymos_a_coherence_damage_se.png` · `coherence_damage` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing south-east in 3/4 front view: shifts into a held pose, struck by an argument: flinching with the eyes shut, the head and upper body leaning back, one hand raised defensively, the feet planted on the ground line.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle SE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: recuo com os olhos fechados, uma mão erguida, pés plantados.
- **A-151** · `didymos_a_coherence_damage_ne.png` · `coherence_damage` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: shifts into a held pose, struck by an argument: the head jerks back and the shoulders hunch, one hand raised defensively, the feet planted on the ground line.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle NE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: cabeça para trás, ombros encolhidos, uma mão erguida, pés plantados.
- **A-152** · `didymos_a_defeat_se.png` · `defeat` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing south-east in 3/4 front view: shifts into a held pose, defeated: kneeling on one knee, the head bowed, the arms hanging.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle SE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: um joelho no chão, cabeça baixa, braços caídos.
- **A-153** · `didymos_a_defeat_ne.png` · `defeat` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: shifts into a held pose, defeated: kneeling on one knee, the head bowed, the arms hanging.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle NE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: um joelho no chão, cabeça baixa, braços caídos.
- **A-154** · `portrait_didymos_a_neutral.png` · retrato 32×32 · `retrato`
  ```text
  [PORTRAIT] Source: frame 1 of didymos_a_idle_se.png. Check: cheerful grin.
  ```
- **A-155** · `portrait_didymos_a_shaken.png` · retrato 32×32 · `expressão`
  ```text
  [FACE] Source: portrait_didymos_a_neutral.png, mask on the eyes, brows and mouth. Description: confused frown, brows drawn together, mouth pulled to one side.
  ```

#### Irmão Dídimos B · `didymos_b` · troca de paleta de `didymos_a`

- **Método:** Mesma folha do Dídimos A, com a faixa `aegean_shade` trocada por `red_dark`. Nenhuma geração no PixelLab: é recolorir no Aseprite as tiras e os retratos aprovados do A.

- **A-156** · `didymos_b_idle_se.png` · `idle` · 2 quadros (tira 64×32) · 600 ms por quadro, em loop · `Aseprite (troca de paleta)`
  ```text
  [SWAP] Recolor of didymos_a_idle_se.png: replace aegean_shade (the sash) with red_dark; every other pixel stays identical. Same size and frame order.
  ```
- **A-157** · `didymos_b_idle_ne.png` · `idle` · 2 quadros (tira 64×32) · 600 ms por quadro, em loop · `Aseprite (troca de paleta)`
  ```text
  [SWAP] Recolor of didymos_a_idle_ne.png: replace aegean_shade (the sash) with red_dark; every other pixel stays identical. Same size and frame order.
  ```
- **A-158** · `didymos_b_walk_se.png` · `walk` · 4 quadros (tira 128×32) · 150 ms por quadro, em loop · `Aseprite (troca de paleta)`
  ```text
  [SWAP] Recolor of didymos_a_walk_se.png: replace aegean_shade (the sash) with red_dark; every other pixel stays identical. Same size and frame order.
  ```
- **A-159** · `didymos_b_walk_ne.png` · `walk` · 4 quadros (tira 128×32) · 150 ms por quadro, em loop · `Aseprite (troca de paleta)`
  ```text
  [SWAP] Recolor of didymos_a_walk_ne.png: replace aegean_shade (the sash) with red_dark; every other pixel stays identical. Same size and frame order.
  ```
- **A-160** · `didymos_b_talk_se.png` · `talk` · 3 quadros (tira 96×32) · 200 ms por quadro, em loop · `Aseprite (troca de paleta)`
  ```text
  [SWAP] Recolor of didymos_a_talk_se.png: replace aegean_shade (the sash) with red_dark; every other pixel stays identical. Same size and frame order.
  ```
- **A-161** · `didymos_b_talk_ne.png` · `talk` · 3 quadros (tira 96×32) · 200 ms por quadro, em loop · `Aseprite (troca de paleta)`
  ```text
  [SWAP] Recolor of didymos_a_talk_ne.png: replace aegean_shade (the sash) with red_dark; every other pixel stays identical. Same size and frame order.
  ```
- **A-162** · `didymos_b_argue_se.png` · `argue` · 3 quadros (tira 96×32) · 200 ms por quadro, sem loop · `Aseprite (troca de paleta)`
  ```text
  [SWAP] Recolor of didymos_a_argue_se.png: replace aegean_shade (the sash) with red_dark; every other pixel stays identical. Same size and frame order.
  ```
- **A-163** · `didymos_b_argue_ne.png` · `argue` · 3 quadros (tira 96×32) · 200 ms por quadro, sem loop · `Aseprite (troca de paleta)`
  ```text
  [SWAP] Recolor of didymos_a_argue_ne.png: replace aegean_shade (the sash) with red_dark; every other pixel stays identical. Same size and frame order.
  ```
- **A-164** · `didymos_b_concede_se.png` · `concede` · 1 quadro (tira 32×32) · pose de 1 quadro · `Aseprite (troca de paleta)`
  ```text
  [SWAP] Recolor of didymos_a_concede_se.png: replace aegean_shade (the sash) with red_dark; every other pixel stays identical. Same size and frame order.
  ```
- **A-165** · `didymos_b_concede_ne.png` · `concede` · 1 quadro (tira 32×32) · pose de 1 quadro · `Aseprite (troca de paleta)`
  ```text
  [SWAP] Recolor of didymos_a_concede_ne.png: replace aegean_shade (the sash) with red_dark; every other pixel stays identical. Same size and frame order.
  ```
- **A-166** · `didymos_b_coherence_damage_se.png` · `coherence_damage` · 1 quadro (tira 32×32) · pose de 1 quadro · `Aseprite (troca de paleta)`
  ```text
  [SWAP] Recolor of didymos_a_coherence_damage_se.png: replace aegean_shade (the sash) with red_dark; every other pixel stays identical. Same size and frame order.
  ```
- **A-167** · `didymos_b_coherence_damage_ne.png` · `coherence_damage` · 1 quadro (tira 32×32) · pose de 1 quadro · `Aseprite (troca de paleta)`
  ```text
  [SWAP] Recolor of didymos_a_coherence_damage_ne.png: replace aegean_shade (the sash) with red_dark; every other pixel stays identical. Same size and frame order.
  ```
- **A-168** · `didymos_b_defeat_se.png` · `defeat` · 1 quadro (tira 32×32) · pose de 1 quadro · `Aseprite (troca de paleta)`
  ```text
  [SWAP] Recolor of didymos_a_defeat_se.png: replace aegean_shade (the sash) with red_dark; every other pixel stays identical. Same size and frame order.
  ```
- **A-169** · `didymos_b_defeat_ne.png` · `defeat` · 1 quadro (tira 32×32) · pose de 1 quadro · `Aseprite (troca de paleta)`
  ```text
  [SWAP] Recolor of didymos_a_defeat_ne.png: replace aegean_shade (the sash) with red_dark; every other pixel stays identical. Same size and frame order.
  ```
- **A-170** · `portrait_didymos_b_neutral.png` · retrato 32×32 · `Aseprite (troca de paleta)`
  ```text
  [SWAP] Recolor of portrait_didymos_a_neutral.png: replace aegean_shade (the sash) with red_dark. Same size.
  ```
- **A-171** · `portrait_didymos_b_shaken.png` · retrato 32×32 · `Aseprite (troca de paleta)`
  ```text
  [SWAP] Recolor of portrait_didymos_a_shaken.png: replace aegean_shade (the sash) with red_dark. Same size.
  ```

#### Menexeno do Ato II (selo do júri) · `menexeno_seal` · edição de `menexeno`

- **Método:** As oito tiras aprovadas do Menexeno (quatro animações, em SE e NE) com um selo de bronze do júri, de 2×3 px (`gold_mid` com borda `gold_dark`), pendurado no alto do bastão em todos os quadros, acompanhando o bastão. É a versão do Ato II (GDD §5). Nenhuma geração no PixelLab: é edição no Aseprite (`[EDIT]`).

- **A-172** · `menexeno_seal_idle_se.png` · `idle` · 2 quadros (tira 64×32) · 600 ms por quadro, em loop · `Aseprite (edição)`
  ```text
  [EDIT] Edit of menexeno_idle_se.png: add a 2x3 px bronze juror's token (gold_mid with a gold_dark edge) hanging from the top of the staff in every frame, following the staff; every other pixel stays identical. Same size and frame order.
  ```
- **A-173** · `menexeno_seal_idle_ne.png` · `idle` · 2 quadros (tira 64×32) · 600 ms por quadro, em loop · `Aseprite (edição)`
  ```text
  [EDIT] Edit of menexeno_idle_ne.png: add a 2x3 px bronze juror's token (gold_mid with a gold_dark edge) hanging from the top of the staff in every frame, following the staff; every other pixel stays identical. Same size and frame order.
  ```
- **A-174** · `menexeno_seal_walk_se.png` · `walk` · 4 quadros (tira 128×32) · 150 ms por quadro, em loop · `Aseprite (edição)`
  ```text
  [EDIT] Edit of menexeno_walk_se.png: add a 2x3 px bronze juror's token (gold_mid with a gold_dark edge) hanging from the top of the staff in every frame, following the staff; every other pixel stays identical. Same size and frame order.
  ```
- **A-175** · `menexeno_seal_walk_ne.png` · `walk` · 4 quadros (tira 128×32) · 150 ms por quadro, em loop · `Aseprite (edição)`
  ```text
  [EDIT] Edit of menexeno_walk_ne.png: add a 2x3 px bronze juror's token (gold_mid with a gold_dark edge) hanging from the top of the staff in every frame, following the staff; every other pixel stays identical. Same size and frame order.
  ```
- **A-176** · `menexeno_seal_talk_se.png` · `talk` · 3 quadros (tira 96×32) · 200 ms por quadro, em loop · `Aseprite (edição)`
  ```text
  [EDIT] Edit of menexeno_talk_se.png: add a 2x3 px bronze juror's token (gold_mid with a gold_dark edge) hanging from the top of the staff in every frame, following the staff; every other pixel stays identical. Same size and frame order.
  ```
- **A-177** · `menexeno_seal_talk_ne.png` · `talk` · 3 quadros (tira 96×32) · 200 ms por quadro, em loop · `Aseprite (edição)`
  ```text
  [EDIT] Edit of menexeno_talk_ne.png: add a 2x3 px bronze juror's token (gold_mid with a gold_dark edge) hanging from the top of the staff in every frame, following the staff; every other pixel stays identical. Same size and frame order.
  ```
- **A-178** · `menexeno_seal_think_se.png` · `think` · 3 quadros (tira 96×32) · 500 ms por quadro, em loop · `Aseprite (edição)`
  ```text
  [EDIT] Edit of menexeno_think_se.png: add a 2x3 px bronze juror's token (gold_mid with a gold_dark edge) hanging from the top of the staff in every frame, following the staff; every other pixel stays identical. Same size and frame order.
  ```
- **A-179** · `menexeno_seal_think_ne.png` · `think` · 3 quadros (tira 96×32) · 500 ms por quadro, em loop · `Aseprite (edição)`
  ```text
  [EDIT] Edit of menexeno_think_ne.png: add a 2x3 px bronze juror's token (gold_mid with a gold_dark edge) hanging from the top of the staff in every frame, following the staff; every other pixel stays identical. Same size and frame order.
  ```

#### Cerâmico (`kerameikos.json`)

- **A-180** · `wall_brick_tall_door_left.png` · 32×48 · `Aseprite (edição)` — Porta na face esquerda: vai na parede de trás ao longo da borda NE da sala (coluna de X máximo). É bloco (intransponível): porta decorativa ou saída fora do mapa (§11).
  ```text
  [EDIT] From the approved wall_brick_tall.png, zero generations: cut a doorway into the left face, about 8 px wide and 20 px tall, centred on the face and drawn as a parallelogram that follows the 2:1 slope of the face; fill it with outline and give it a 1-px ochre_dark lintel and jambs. The top, the other face and the outline stay identical.
  ```
- **A-181** · `wall_brick_tall_door_right.png` · 32×48 · `Aseprite (edição)` — Porta na face direita: vai na parede de trás ao longo da borda NO da sala (linha de Y máximo). É bloco (intransponível): porta decorativa ou saída fora do mapa (§11).
  ```text
  [EDIT] From the approved wall_brick_tall.png, zero generations: cut a doorway into the right face, about 8 px wide and 20 px tall, centred on the face and drawn as a parallelogram that follows the 2:1 slope of the face; fill it with outline and give it a 1-px ochre_dark lintel and jambs. The top, the other face and the outline stay identical.
  ```

#### Delfos (mapa ainda não existe)

- **A-182** · `floor_mountain_rock.png` · 32×16 · `iso-tile thin` — Terreno `Stone`.
  ```text
  Cold mountain rock floor with angular cracks, light grey base with cool dark grey cracks and deep sea-blue accents, cool blue-grey shadows, high contrast, [FLOOR] [STYLE_ENV]
  ```
- **A-183** · `floor_rift.png` · 32×16 · `iso-tile thin` — Terreno `Rift`, intransponível. O vapor sai daqui.
  ```text
  A dark narrow fissure crossing a rock floor with a faint bright cyan glow deep inside, the rock around it light grey and cool dark grey, cool blue-grey shadows, [FLOOR] [STYLE_ENV]
  ```
- **A-184** · `floor_snow.png` · 32×16 · `iso-tile thin` — Sombra azul-fria (GDD §6.4). Terreno `Snow`.
  ```text
  Fresh blue-white snow floor, off-white base with soft pale grey-beige dips and deep sea-blue hollows, cool blue-grey shadows, [FLOOR] [STYLE_ENV]
  ```
- **A-185** · `block_cliff_rock.png` · 32×40 · `bitforge+init` — Novo, proposto: borda da montanha (não está no GDD). Sem placeholder: silhueta de bloco 32×40 (topo de 16 px e 24 px de face) em 2 tons, à mão.
  ```text
  A raised rock outcrop at the mountain edge with angular faces, light grey top, cool dark grey left face, deep sea-blue right face, cool blue-grey shadows, [BLOCK] [STYLE_ENV]
  ```
- **A-186** · `block_delphic_treasury.png` · 32×48 · `bitforge+init` — Mármore: contorno `outline_blue`, como a arquitetura da Ágora. Sem placeholder: a silhueta de `init_image` é um bloco de 2 tons desenhado à mão.
  ```text
  A small Delphic treasury on one cell, a white marble temple-vault with two columns in front and a triangular pediment, off-white marble with cool blue-grey shadows, [BLOCK] [STYLE_ENV]
  ```
- **A-187** · `prop_omphalos.png` · 32×24 · `map-object+init` — Gere em 32×32 e corte o topo. Sem placeholder: silhueta à mão.
  ```text
  The omphalos, an egg-shaped off-white marble stone carved with a knotted net pattern, on a low base, cool blue-grey shadows, [PROP] [STYLE_ENV]
  ```
- **A-188** · `prop_tripod.png` · 32×32 · `map-object+init` — Decorativa e vazia; a Pítia traz a própria trípode no sprite. Sem placeholder: silhueta à mão.
  ```text
  An empty tall bronze tripod with a shallow basin on top and three thin legs, golden yellow bronze with dark bronze shading and a single bright yellow glint, [PROP] [STYLE_ENV]
  ```
- **A-189** · `overlay_vapor.png` · 32×32 · 6 quadros (tira 192×32) · `manual (Aseprite)` — 150 ms, loop. Seis quadros, como pede o AT2-05 #2. Arquivo único de 6 quadros (192×32).
  ```text
  [MANUAL] 32x32 canvas per frame, 6 frames. Animation "rise": Translucent vapor wisps rising from the rift in a seamless 6-frame loop, drawn as a 50 percent checkerboard of aegean_light and marble_light over transparency, lineless; the wisp shapes rise 2 px per frame and wrap around, so frame 6 flows into frame 1. Palette cycling of the checker bands is the quickest way.
  ```

#### Interface

- **A-190** · `box_items.png` · 56×22 · `manual (Aseprite)` — Mesmo azul do `frame_slot` (`ui_blue`, palette v2).
  ```text
  [MANUAL] 56x22 canvas. Items box: double ui_blue border (two 1-pixel ui_blue lines with 1 pixel of void between them), void fill, for the last revealed fallacies.
  ```
- **A-191** · `box_nameplate.png` · 24×8 · `manual (Aseprite)` — HUD da referência. Fonte de 9-slice: o tamanho final sai da amostra. A referência mede ~22×8 para 4 letras; com a Ark Pixel (maiúsculas de 7 px e avanço de ~6 px), KLEIA dá cerca de 30 px de texto, e a altura precisa caber o acento de ÓRON e BÁTILO. Exige código.
  ```text
  [MANUAL] 24x8 canvas. Name plate for the active unit: a ui_gold rectangle with a 1-pixel ochre_dark outline, drawn as a 9-slice source with 2-pixel borders; the name is lettered in ochre_dark.
  ```
- **A-192** · `font_ui_specimen.png` · 384×96 · `manual (Aseprite)` — Aprovação da fonte decidida (Ark Pixel Font 10px Proportional, §12.1). Não entra no jogo. A largura é a do quadro nativo (384 px), para ver as peças lado a lado na escala real. Entrega junto, em `art/incoming/track_a/fonts/`, o arquivo da variante 10px Proportional latin da release oficial e o texto da licença OFL 1.1. Os tamanhos de `box_menu`, `box_nameplate`, `frame_slot` e dos contadores saem desta amostra, assim como a conferência das maiúsculas acentuadas (§11).
  ```text
  [MANUAL] 384x96 canvas. Specimen of Ark Pixel Font 10px Proportional (latin) rendered at 1x on void, no anti-aliasing, one font pixel per canvas pixel: at the left, the six commands CIRCULAR, REFUTAR, MAIEUTICA (with the circumflex), CITAR, CONCEDER, RETIRAR-SE in a box_menu-style box with 12-pixel rows and the cursor arrow; in the middle, a 208x48 box_dialogue-style box with lines that use the lowercase accents and every accented capital (A-acute, A-grave, A-circumflex, A-tilde, E-acute, E-circumflex, I-acute, O-acute, O-circumflex, O-tilde, U-acute, C-cedilla), curly quotes, the em dash and the ellipsis; at the right, the KLEIA, ORON (O-acute) and THRASYBIOS name plates and a x100 counter.
  ```
- **A-193** · `frame_slot.png` · 12×20 · `manual (Aseprite)` — Borda em `ui_blue` `#3868D8`, a cor da referência (palette v2). A letra dentro do slot é texto na fonte da UI.
  ```text
  [MANUAL] 12x20 canvas. Equipment slot frame: rounded 1-pixel ui_blue border with an aegean_dark inner line, transparent inside.
  ```
- **A-194** · `icon_coherence_empty.png` · 7×7 · `manual (Aseprite)`
  ```text
  [MANUAL] 7x7 canvas. Coherence point, empty: the same tiny column as a broken stump in marble_dark.
  ```
- **A-195** · `icon_coherence_full.png` · 7×7 · `manual (Aseprite)` — Substitui os corações da referência.
  ```text
  [MANUAL] 7x7 canvas. Coherence point, full: a tiny intact white marble column in marble_light with an outline.
  ```
- **A-196** · `icon_coherence_half.png` · 7×7 · `manual (Aseprite)`
  ```text
  [MANUAL] 7x7 canvas. Coherence point, half: the same tiny column cracked in the middle.
  ```
- **A-197** · `icon_compendium.png` · 8×8 · `manual (Aseprite)` — Contador de falácias do compêndio.
  ```text
  [MANUAL] 8x8 canvas. Tiny closed wax tablet diptych, ochre_mid with an ochre_shade frame, outline.
  ```
- **A-198** · `icon_drachma.png` · 8×8 · `manual (Aseprite)` — Contador de dracmas. O "×100" ao lado é texto na fonte da UI.
  ```text
  [MANUAL] 8x8 canvas. Tiny silver drachma coin with an owl mark, marble_light and marble_shade, outline.
  ```
- **A-199** · `icon_nameplate_arrow.png` · 7×4 · `manual (Aseprite)`
  ```text
  [MANUAL] 7x4 canvas. Small ui_gold arrow pointing down with an ochre_dark outline, placed under the name plate.
  ```
- **A-200** · `icon_quote.png` · 8×8 · `manual (Aseprite)` — Contador de usos de citação.
  ```text
  [MANUAL] 8x8 canvas. Tiny rolled scroll, marble_light with a gold_mid ribbon, outline.
  ```
- **A-201** · `meter_audience_fill.png` · 6×18 · `manual (Aseprite)`
  ```text
  [MANUAL] 6x18 canvas. Vertical audience meter fill: gold_mid with a 1-pixel gold_light column on the left, filling the inside of the frame.
  ```
- **A-202** · `meter_audience_frame.png` · 6×18 · `manual (Aseprite)`
  ```text
  [MANUAL] 6x18 canvas. Vertical audience meter frame, 1-pixel outline with a ui_white inner line, transparent inside.
  ```

#### Ícones de falácia (16×16)

- **A-203** · `icon_fallacy_affirming_the_consequent.png` · 16×16 · `glifo + moldura` — Afirmação do consequente (Formal) · moldura `icon_badge_formal.png`
  ```text
  A U-turn arrow pointing back, [GLYPH] [STYLE_ENV]
  ```
- **A-204** · `icon_fallacy_begging_the_question.png` · 16×16 · `glifo + moldura` — Petição de princípio (Informal) · moldura `icon_badge_informal.png`
  ```text
  A circular arrow chasing its own tail, [GLYPH] [STYLE_ENV]
  ```
- **A-205** · `icon_fallacy_denying_the_antecedent.png` · 16×16 · `glifo + moldura` — Negação do antecedente (Formal) · moldura `icon_badge_formal.png`
  ```text
  A broken arrow snapped in the middle, [GLYPH] [STYLE_ENV]
  ```
- **A-206** · `icon_fallacy_flux_vs_permanence.png` · 16×16 · `glifo + moldura` — Tudo flui e nada muda (Paradox) · moldura `icon_badge_paradox.png`
  ```text
  A disc with a wavy left half and a solid right half, [GLYPH] [STYLE_ENV]
  ```
- **A-207** · `icon_fallacy_four_terms.png` · 16×16 · `glifo + moldura` — Quatro termos (Formal) · moldura `icon_badge_formal.png`
  ```text
  Four dots arranged in a square, [GLYPH] [STYLE_ENV]
  ```
- **A-208** · `icon_fallacy_liar.png` · 16×16 · `glifo + moldura` — O Mentiroso (Paradox) · moldura `icon_badge_paradox.png`
  ```text
  A round hand mirror, [GLYPH] [STYLE_ENV]
  ```
- **A-209** · `icon_fallacy_ship_of_theseus.png` · 16×16 · `glifo + moldura` — O navio de Teseu (Paradox) · moldura `icon_badge_paradox.png`
  ```text
  A small ship with one plank of a different shade, [GLYPH] [STYLE_ENV]
  ```
- **A-210** · `icon_fallacy_sorites.png` · 16×16 · `glifo + moldura` — O monte de areia (Paradox) · moldura `icon_badge_paradox.png`
  ```text
  A small heap of sand with one falling grain, [GLYPH] [STYLE_ENV]
  ```
- **A-211** · `icon_fallacy_zeno_tortoise.png` · 16×16 · `glifo + moldura` — Aquiles e a tartaruga (Paradox) · moldura `icon_badge_paradox.png`
  ```text
  A small tortoise, [GLYPH] [STYLE_ENV]
  ```

### 9.3 P3 — Tribunal e acabamento

41 arquivos (A-212 a A-252), 91 quadros.

#### Kleia de Cerâmico · `kleia` · COMPLETO

Ficha, chave de cor e descritor em §9.1.

- **A-212** · `kleia_paint_se.png` · `paint` · 2 quadros (tira 64×32) · 400 ms por quadro, em loop · `sentada: edição + mão`
  ```text
  The same girl now sitting cross-legged on the ground facing south-east, painting a small orange-ochre amphora with near-black bands in front of her with a thin brush, same clothes and colors.
  ```
  **Quadros:** Quadro 1: `edit-image-pro-flash` (`method` = `text`, `use_color_palette_correction` ligado) sobre o quadro 1 de `kleia_idle_se.png`, com este prompt. Quadro 2 à mão: só a mão do pincel anda 1 px.
- **A-213** · `portrait_kleia_determined.png` · retrato 32×32 · `expressão`
  ```text
  [FACE] Source: portrait_kleia_neutral.png, mask on the eyes, brows and mouth. Description: narrowed eyes, brows lowered, jaw set, lips pressed together.
  ```
- **A-214** · `portrait_kleia_doubtful.png` · retrato 32×32 · `expressão`
  ```text
  [FACE] Source: portrait_kleia_neutral.png, mask on the eyes, brows and mouth. Description: eyes lowered to the side, lips pressed together, brows slightly raised in doubt.
  ```

#### Cidadão (base) · `citizen` · NPC

Ficha, chave de cor e descritor em §9.1.

- **A-215** · `citizen_cheer_se.png` · `cheer` · 2 quadros (tira 64×32) · 250 ms por quadro, em loop · `animar loop`
  ```text
  Facing south-east in 3/4 front view: cheers with the crowd: raises one fist high with the mouth open, then lowers it back to the idle pose.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle SE. Fique com 2, em ordem: quadro 1 da idle · punho no alto, boca aberta. Descarte o último quadro gerado se ele repetir o primeiro.
- **A-216** · `citizen_cheer_ne.png` · `cheer` · 2 quadros (tira 64×32) · 250 ms por quadro, em loop · `animar loop`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: cheers with the crowd: raises one fist high, then lowers it back to the idle pose.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle NE. Fique com 2, em ordem: quadro 1 da idle · punho no alto. Descarte o último quadro gerado se ele repetir o primeiro.

#### Cidadão idoso (variante) · `citizen_elder` · NPC variante

- **Altura no canvas:** 23 px.
- **Onde fica:** Ágora (plateia).
- **Direções:** SE e NE; view fixada no piloto (§3.2), a mesma para o elenco inteiro.
- **Arquivos:** 8 tiras (22 quadros) e 0 retratos, em P3.
- **Chave de cor:** outline, outline_blue, skin_light, skin_mid, skin_shade (pele de sol), marble_mid, marble_shade, marble_dark (barba grisalha), ochre_mid, ochre_shade, ochre_dark (himátion de lã marrom: rampa do manto, exclusiva), hair_mid (sandálias) — 12 cores.
- **Silhueta e atuação:** Cabeça careca de barba grisalha, levemente curvado, himátion de lã marrom. Mesma proporção do cidadão base. A rampa do manto (`ochre_mid`, `ochre_shade`, `ochre_dark`) é exclusiva: as sandálias usam `hair_mid`, e a barba grisalha fica fora da troca (§8.2).

Descritor `[CITIZEN_ELDER]`:

```text
an elderly ordinary Athenian citizen, about 23 pixels tall, bald with a short grey beard, slightly bent, a rectangular plain brown wool himation wrapped around the body and draped over the left shoulder, bare shins, dark brown leather thong sandals, plain and unadorned, muted neutral colors,
```

- **A-217** · `citizen_elder_idle_se.png` · `idle` · 2 quadros (tira 64×32) · 600 ms por quadro, em loop · `criar`
  ```text
  [CITIZEN_ELDER] facing south in front view, slightly bent, the hands clasped behind the back, [CHARACTER] [STYLE_CHAR]
  ```
  **Quadros:** Quadro 1 = a rotação SE do personagem criado a partir do sprite sul deste prompt (§3.3, `criar`). Quadro 2 à mão: suba 1 px as linhas da cabeça, dos ombros e do peito, com pés e sombra idênticos.
- **A-218** · `citizen_elder_idle_ne.png` · `idle` · 2 quadros (tira 64×32) · 600 ms por quadro, em loop · `rotação + mão`
  ```text
  [EDIT] Frame 1 is the north-east rotation of the character created for citizen_elder_idle_se.png, with zero extra generations. Frame 2 copies frame 1 with the head, shoulder and chest rows moved up 1 px, while the feet and the shadow stay identical.
  ```
- **A-219** · `citizen_elder_walk_se.png` · `walk` · 4 quadros (tira 128×32) · 150 ms por quadro, em loop · `template walk + mão`
  ```text
  Facing south-east in 3/4 front view: walking with slow careful steps.
  ```
  **Seleção:** Template `walking-4-frames`, `directions` = ["south-east"], este texto em `action_description`. Confira os pés plantados na linha 29; depois, à mão, suba 1 px a cabeça e o tronco nos dois quadros de passagem (pernas juntas).
- **A-220** · `citizen_elder_walk_ne.png` · `walk` · 4 quadros (tira 128×32) · 150 ms por quadro, em loop · `template walk + mão`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: walking with slow careful steps.
  ```
  **Seleção:** Template `walking-4-frames`, `directions` = ["north-east"], este texto em `action_description`. Confira os pés plantados na linha 29; depois, à mão, suba 1 px a cabeça e o tronco nos dois quadros de passagem (pernas juntas).
- **A-221** · `citizen_elder_talk_se.png` · `talk` · 3 quadros (tira 96×32) · 200 ms por quadro, em loop · `animar loop`
  ```text
  Facing south-east in 3/4 front view: talks in place, the mouth opening and closing while one hand opens at waist height, then settles back to the idle pose.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle SE. Fique com 3, em ordem: quadro 1 da idle (boca fechada) · boca aberta com o gesto no auge · boca entreaberta, mão voltando. Descarte o último quadro gerado se ele repetir o primeiro.
- **A-222** · `citizen_elder_talk_ne.png` · `talk` · 3 quadros (tira 96×32) · 200 ms por quadro, em loop · `animar loop`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: talks in place with small nods of the head while one hand opens at waist height, then settles back to the idle pose.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle NE. Fique com 3, em ordem: quadro 1 da idle · aceno de cabeça com o gesto no auge · mão voltando. Descarte o último quadro gerado se ele repetir o primeiro.
- **A-223** · `citizen_elder_cheer_se.png` · `cheer` · 2 quadros (tira 64×32) · 250 ms por quadro, em loop · `animar loop`
  ```text
  Facing south-east in 3/4 front view: cheers with the crowd: raises one fist high with the mouth open, then lowers it back to the idle pose.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle SE. Fique com 2, em ordem: quadro 1 da idle · punho no alto, boca aberta. Descarte o último quadro gerado se ele repetir o primeiro.
- **A-224** · `citizen_elder_cheer_ne.png` · `cheer` · 2 quadros (tira 64×32) · 250 ms por quadro, em loop · `animar loop`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: cheers with the crowd: raises one fist high, then lowers it back to the idle pose.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle NE. Fique com 2, em ordem: quadro 1 da idle · punho no alto. Descarte o último quadro gerado se ele repetir o primeiro.

#### Cidadã (variante) · `citizen_woman` · NPC variante

- **Altura no canvas:** 23 px.
- **Onde fica:** Ágora (plateia).
- **Direções:** SE e NE; view fixada no piloto (§3.2), a mesma para o elenco inteiro.
- **Arquivos:** 8 tiras (22 quadros) e 0 retratos, em P3.
- **Chave de cor:** outline, outline_blue, skin_light, skin_mid, skin_shade, hair_dark, marble_light, marble_mid (véu), ochre_mid, ochre_shade, ochre_dark (peplos de lã marrom: rampa do manto, exclusiva), ochre_light, hair_mid (cesto de vime e sandálias) — 13 cores.
- **Silhueta e atuação:** Cabelo preso sob véu claro, peplos marrom, cesto de vime no quadril. O cesto usa `ochre_light` e `hair_mid`, fora da rampa do manto, e o véu fica fora da troca (§8.2).

Descritor `[CITIZEN_WOMAN]`:

```text
an ordinary Athenian woman, about 23 pixels tall, dark hair tied up under a plain off-white veil, an ankle-length plain brown wool peplos pinned at both shoulders and belted, dark brown leather sandals, a pale wicker basket on her hip, plain and unadorned, muted neutral colors,
```

- **A-225** · `citizen_woman_idle_se.png` · `idle` · 2 quadros (tira 64×32) · 600 ms por quadro, em loop · `criar`
  ```text
  [CITIZEN_WOMAN] facing south in front view, standing with the wicker basket on her hip, [CHARACTER] [STYLE_CHAR]
  ```
  **Quadros:** Quadro 1 = a rotação SE do personagem criado a partir do sprite sul deste prompt (§3.3, `criar`). Quadro 2 à mão: suba 1 px as linhas da cabeça, dos ombros e do peito, com pés e sombra idênticos.
- **A-226** · `citizen_woman_idle_ne.png` · `idle` · 2 quadros (tira 64×32) · 600 ms por quadro, em loop · `rotação + mão`
  ```text
  [EDIT] Frame 1 is the north-east rotation of the character created for citizen_woman_idle_se.png, with zero extra generations. Frame 2 copies frame 1 with the head, shoulder and chest rows moved up 1 px, while the feet and the shadow stay identical.
  ```
- **A-227** · `citizen_woman_walk_se.png` · `walk` · 4 quadros (tira 128×32) · 150 ms por quadro, em loop · `template walk + mão`
  ```text
  Facing south-east in 3/4 front view: walking at a steady pace, the basket held on the hip.
  ```
  **Seleção:** Template `walking-4-frames`, `directions` = ["south-east"], este texto em `action_description`. Confira os pés plantados na linha 29; depois, à mão, suba 1 px a cabeça e o tronco nos dois quadros de passagem (pernas juntas).
- **A-228** · `citizen_woman_walk_ne.png` · `walk` · 4 quadros (tira 128×32) · 150 ms por quadro, em loop · `template walk + mão`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: walking at a steady pace, the basket held on the hip.
  ```
  **Seleção:** Template `walking-4-frames`, `directions` = ["north-east"], este texto em `action_description`. Confira os pés plantados na linha 29; depois, à mão, suba 1 px a cabeça e o tronco nos dois quadros de passagem (pernas juntas).
- **A-229** · `citizen_woman_talk_se.png` · `talk` · 3 quadros (tira 96×32) · 200 ms por quadro, em loop · `animar loop`
  ```text
  Facing south-east in 3/4 front view: talks in place, the mouth opening and closing while the free hand opens at chest height, then settles back to the idle pose.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle SE. Fique com 3, em ordem: quadro 1 da idle (boca fechada) · boca aberta com o gesto no auge · boca entreaberta, mão voltando. Descarte o último quadro gerado se ele repetir o primeiro.
- **A-230** · `citizen_woman_talk_ne.png` · `talk` · 3 quadros (tira 96×32) · 200 ms por quadro, em loop · `animar loop`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: talks in place with small nods of the head while the free hand opens at chest height, then settles back to the idle pose.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle NE. Fique com 3, em ordem: quadro 1 da idle · aceno de cabeça com o gesto no auge · mão voltando. Descarte o último quadro gerado se ele repetir o primeiro.
- **A-231** · `citizen_woman_cheer_se.png` · `cheer` · 2 quadros (tira 64×32) · 250 ms por quadro, em loop · `animar loop`
  ```text
  Facing south-east in 3/4 front view: cheers with the crowd: raises one fist high with the mouth open, then lowers it back to the idle pose.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle SE. Fique com 2, em ordem: quadro 1 da idle · punho no alto, boca aberta. Descarte o último quadro gerado se ele repetir o primeiro.
- **A-232** · `citizen_woman_cheer_ne.png` · `cheer` · 2 quadros (tira 64×32) · 250 ms por quadro, em loop · `animar loop`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: cheers with the crowd: raises one fist high, then lowers it back to the idle pose.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle NE. Fique com 2, em ordem: quadro 1 da idle · punho no alto. Descarte o último quadro gerado se ele repetir o primeiro.

#### Cidadão jovem (variante) · `citizen_youth` · NPC variante

- **Altura no canvas:** 25 px.
- **Onde fica:** Ágora (plateia) e porto.
- **Direções:** SE e NE; view fixada no piloto (§3.2), a mesma para o elenco inteiro.
- **Arquivos:** 8 tiras (22 quadros) e 0 retratos, em P3.
- **Chave de cor:** outline, outline_blue, skin_mid, skin_shade, hair_mid (pele C e sandálias), hair_dark, marble_light, marble_mid, marble_shade (quíton de linho: rampa do manto, exclusiva), ochre_mid, ochre_shade, ochre_dark (ânfora) — 12 cores.
- **Silhueta e atuação:** Jovem sem barba, pele escura, ânfora no ombro. O quíton de linho é a rampa do manto, exclusiva (§8.2).

Descritor `[CITIZEN_YOUTH]`:

```text
a young clean-shaven Athenian man with dark brown skin, about 25 pixels tall, short black hair, a knee-length plain off-white linen chiton belted at the waist, bare arms and legs, dark brown leather sandals, a terracotta amphora carried on his shoulder, plain and unadorned, muted neutral colors,
```

- **A-233** · `citizen_youth_idle_se.png` · `idle` · 2 quadros (tira 64×32) · 600 ms por quadro, em loop · `criar`
  ```text
  [CITIZEN_YOUTH] facing south in front view, standing, one hand steadying the amphora on his shoulder, [CHARACTER] [STYLE_CHAR]
  ```
  **Quadros:** Quadro 1 = a rotação SE do personagem criado a partir do sprite sul deste prompt (§3.3, `criar`). Quadro 2 à mão: suba 1 px as linhas da cabeça, dos ombros e do peito, com pés e sombra idênticos.
- **A-234** · `citizen_youth_idle_ne.png` · `idle` · 2 quadros (tira 64×32) · 600 ms por quadro, em loop · `rotação + mão`
  ```text
  [EDIT] Frame 1 is the north-east rotation of the character created for citizen_youth_idle_se.png, with zero extra generations. Frame 2 copies frame 1 with the head, shoulder and chest rows moved up 1 px, while the feet and the shadow stay identical.
  ```
- **A-235** · `citizen_youth_walk_se.png` · `walk` · 4 quadros (tira 128×32) · 150 ms por quadro, em loop · `template walk + mão`
  ```text
  Facing south-east in 3/4 front view: walking with an easy gait, the amphora steady on the shoulder.
  ```
  **Seleção:** Template `walking-4-frames`, `directions` = ["south-east"], este texto em `action_description`. Confira os pés plantados na linha 29; depois, à mão, suba 1 px a cabeça e o tronco nos dois quadros de passagem (pernas juntas).
- **A-236** · `citizen_youth_walk_ne.png` · `walk` · 4 quadros (tira 128×32) · 150 ms por quadro, em loop · `template walk + mão`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: walking with an easy gait, the amphora steady on the shoulder.
  ```
  **Seleção:** Template `walking-4-frames`, `directions` = ["north-east"], este texto em `action_description`. Confira os pés plantados na linha 29; depois, à mão, suba 1 px a cabeça e o tronco nos dois quadros de passagem (pernas juntas).
- **A-237** · `citizen_youth_talk_se.png` · `talk` · 3 quadros (tira 96×32) · 200 ms por quadro, em loop · `animar loop`
  ```text
  Facing south-east in 3/4 front view: talks in place, the mouth opening and closing while the free hand opens at waist height, then settles back to the idle pose.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle SE. Fique com 3, em ordem: quadro 1 da idle (boca fechada) · boca aberta com o gesto no auge · boca entreaberta, mão voltando. Descarte o último quadro gerado se ele repetir o primeiro.
- **A-238** · `citizen_youth_talk_ne.png` · `talk` · 3 quadros (tira 96×32) · 200 ms por quadro, em loop · `animar loop`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: talks in place with small nods of the head while the free hand opens at waist height, then settles back to the idle pose.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle NE. Fique com 3, em ordem: quadro 1 da idle · aceno de cabeça com o gesto no auge · mão voltando. Descarte o último quadro gerado se ele repetir o primeiro.
- **A-239** · `citizen_youth_cheer_se.png` · `cheer` · 2 quadros (tira 64×32) · 250 ms por quadro, em loop · `animar loop`
  ```text
  Facing south-east in 3/4 front view: cheers with the crowd: raises one fist high with the mouth open, then lowers it back to the idle pose.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle SE. Fique com 2, em ordem: quadro 1 da idle · punho no alto, boca aberta. Descarte o último quadro gerado se ele repetir o primeiro.
- **A-240** · `citizen_youth_cheer_ne.png` · `cheer` · 2 quadros (tira 64×32) · 250 ms por quadro, em loop · `animar loop`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: cheers with the crowd: raises one fist high, then lowers it back to the idle pose.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle NE. Fique com 2, em ordem: quadro 1 da idle · punho no alto. Descarte o último quadro gerado se ele repetir o primeiro.

#### Cerâmico (`kerameikos.json`)

- **A-241** · `prop_amphora_painted.png` · 32×32 · `map-object+init` — A ânfora que a Kleia pinta no I.1.
  ```text
  A single tall red-figure amphora on a small stand, a near-black glaze body with a band of dancers left in bare terracotta-orange clay, [PROP] [STYLE_ENV]
  ```
- **A-242** · `prop_amphora_stack_b.png` · 32×32 · `map-object+init` — Variante de arranjo (GDD §6.1). `init_image` = o placeholder de `prop_amphora_stack`, `style_image` = A-003 aprovado.
  ```text
  A different arrangement of five terracotta amphorae, two standing upright at the back and three lying in front, terracotta-orange clay with near-black glaze bands, [PROP] [STYLE_ENV]
  ```
- **A-243** · `prop_amphora_stack_c.png` · 32×32 · `map-object+init` — Variante de arranjo.
  ```text
  A third arrangement of terracotta amphorae, a pyramid of four with one broken amphora beside it, terracotta-orange clay with near-black glaze bands, [PROP] [STYLE_ENV]
  ```
- **A-244** · `prop_clay_heap.png` · 32×24 · `map-object+init`
  ```text
  A heap of raw terracotta-orange clay with a wooden shovel stuck in it, [PROP] [STYLE_ENV]
  ```
- **A-245** · `prop_paint_pots.png` · 32×24 · `map-object+init`
  ```text
  A cluster of small terracotta-orange clay pigment pots holding rust-brown and brick-red pigment, with a bundle of thin brushes on the ground, [PROP] [STYLE_ENV]
  ```

#### Delfos (mapa ainda não existe)

- **A-246** · `floor_rift_glow.png` · 32×16 · 2 quadros (tira 64×16) · `ciclo de paleta (Aseprite)` — Opcional.
  ```text
  [EDIT] 2 frames by palette cycling on floor_rift.png, zero generations: frame 1 is identical to the static file; in frame 2 the glow pixels inside the fissure swap aegean_shade and aegean_light, so the fissure brightens toward its center. The diamond edges stay identical, so the tile still joins its neighbours.
  ```
- **A-247** · `prop_laurel_bush.png` · 32×32 · `map-object+init`
  ```text
  A small sacred laurel bush with dense leaves, olive green and dark olive green with yellow-green highlights, [PROP] [STYLE_ENV]
  ```

#### Overlays de sistema

- **A-248** · `hl_select_b.png` · 32×16 · `manual (Aseprite)` — Só entra se o pulso deixar de alternar com vazio.
  ```text
  [MANUAL] 32x16 canvas. Second pulse frame of hl_select: the same diamond outline in gold_mid with the corner ticks 1 pixel shorter.
  ```

#### Interface

- **A-249** · `frame_minimap.png` · 48×48 · `manual (Aseprite)` — Opcional (referência: pergaminho de mapa).
  ```text
  [MANUAL] 48x48 canvas. Optional minimap frame styled as an unrolled papyrus with rolled ends, transparent center.
  ```

#### Efeitos

- **A-250** · `fx_alert.png` · 8×8 · 2 quadros (tira 16×8) · `manual (Aseprite)` — Sobre o oponente quando a Kleia entra no raio.
  ```text
  [MANUAL] 8x8 canvas per frame, 2 frames. Animation "blink": Aggro alert: a tiny gold_light exclamation mark with an outline, frame 2 1 pixel higher.
  ```
- **A-251** · `fx_dust.png` · 8×8 · 3 quadros (tira 24×8) · `manual (Aseprite)`
  ```text
  [MANUAL] 8x8 canvas per frame, 3 frames. Animation "puff": Footstep dust puff in ochre_light and ochre_mid, growing and fading over 3 frames, lineless.
  ```
- **A-252** · `fx_talk_hint.png` · 8×8 · 2 quadros (tira 16×8) · `manual (Aseprite)` — Sobre NPC com `dialogue`.
  ```text
  [MANUAL] 8x8 canvas per frame, 2 frames. Animation "blink": Talk hint: a tiny marble_light speech balloon with an outline, frame 2 1 pixel higher.
  ```

---

## 10. Trilha B

244 arquivos, de B-001 a B-244. Os ids seguem a ordem de trabalho. Dentro de cada prioridade, primeiro os personagens (tiras e retratos), depois o cenário e o sistema.

### 10.1 P1 — Ato I, fatia vertical

111 arquivos (B-001 a B-111), 229 quadros.

**Piloto (§3.5):** B-001 `floor_marble.png`, B-002 `floor_marble_ochre.png`, B-003 `thrasybios_idle_se.png`, B-004 `thrasybios_idle_ne.png`. Os pilotos vêm antes de todo o resto.

#### Thrasybios · `thrasybios` · CHEFE

- **Altura no canvas:** 26 px (o mais alto).
- **Onde fica:** `floor_stone` (mercado, célula 7,1).
- **Direções:** SE e NE; view fixada no piloto (§3.2), a mesma para o elenco inteiro.
- **Arquivos:** 16 tiras (54 quadros) e 2 retratos, em P1.
- **Chave de cor:** outline, outline_blue, skin_light, skin_mid, skin_shade, hair_mid, hair_dark (brilho de óleo = 1 px marble_light), purple_light, purple_mid, purple_dark (himátion), gold_light, gold_mid (grega, fita, anel), marble_light, marble_mid (papiro), ochre_shade (sandálias novas) — 15 cores.
- **Silhueta e atuação:** O mais alto, robusto, peito aberto, queixo erguido. A mão direita sempre aberta com a palma para cima; o rolo fechado sob o braço esquerdo, nunca aberto. Vendedor profissional: gestos largos, sem desleixo. Com 26 px, a borda grega vira uma linha tracejada `gold_light`/`gold_mid` na barra.

Descritor `[THRASYBIOS]`:

```text
Thrasybios, a 41-year-old ancient Greek sophist and salesman, about 26 pixels tall, the tallest of the cast, robust and well fed, chest out and chin raised like a trained orator, oiled wavy brown hair and a precisely trimmed beard, a knee-length chiton under a rich royal-purple wool himation with a golden-yellow meander border draped over the left shoulder, new leather sandals, a gold signet ring, the right hand always open in a presenting gesture, a closed off-white papyrus scroll tied with a golden-yellow ribbon under the left arm,
```

- **B-003** · `thrasybios_idle_se.png` · `idle` · 2 quadros (tira 64×32) · 600 ms por quadro, em loop · `criar`
  ```text
  [THRASYBIOS] facing south in front view, tall orator stance with the chest out and the chin raised, the closed scroll under the left arm, the right hand open with the palm up, [CHARACTER] [STYLE_CHAR]
  ```
  **Quadros:** Quadro 1 = a rotação SE do personagem criado a partir do sprite sul deste prompt (§3.3, `criar`). Quadro 2 à mão: suba 1 px as linhas da cabeça, dos ombros e do peito, com pés e sombra idênticos.
- **B-004** · `thrasybios_idle_ne.png` · `idle` · 2 quadros (tira 64×32) · 600 ms por quadro, em loop · `rotação + mão`
  ```text
  [EDIT] Frame 1 is the north-east rotation of the character created for thrasybios_idle_se.png, with zero extra generations. Frame 2 copies frame 1 with the head, shoulder and chest rows moved up 1 px, while the feet and the shadow stay identical.
  ```
- **B-005** · `thrasybios_walk_se.png` · `walk` · 4 quadros (tira 128×32) · 150 ms por quadro, em loop · `template walk + mão`
  ```text
  Facing south-east in 3/4 front view: walking with a confident orator stride, the open right hand swinging and the scroll kept under the left arm.
  ```
  **Seleção:** Template `walking-4-frames`, `directions` = ["south-east"], este texto em `action_description`. Confira os pés plantados na linha 29; depois, à mão, suba 1 px a cabeça e o tronco nos dois quadros de passagem (pernas juntas).
- **B-006** · `thrasybios_walk_ne.png` · `walk` · 4 quadros (tira 128×32) · 150 ms por quadro, em loop · `template walk + mão`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: walking with a confident orator stride, the open right hand swinging and the scroll kept under the left arm.
  ```
  **Seleção:** Template `walking-4-frames`, `directions` = ["north-east"], este texto em `action_description`. Confira os pés plantados na linha 29; depois, à mão, suba 1 px a cabeça e o tronco nos dois quadros de passagem (pernas juntas).
- **B-007** · `thrasybios_talk_se.png` · `talk` · 3 quadros (tira 96×32) · 200 ms por quadro, em loop · `animar loop`
  ```text
  Facing south-east in 3/4 front view: talks in place, the mouth opening and closing while the open right hand sweeps outward in a wide presenting gesture, then settles back to the idle pose.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle SE. Fique com 3, em ordem: quadro 1 da idle (boca fechada) · boca aberta com o gesto no auge · boca entreaberta, mão voltando. Descarte o último quadro gerado se ele repetir o primeiro.
- **B-008** · `thrasybios_talk_ne.png` · `talk` · 3 quadros (tira 96×32) · 200 ms por quadro, em loop · `animar loop`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: talks in place with small nods of the head while the open right hand sweeps outward in a wide presenting gesture, then settles back to the idle pose.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle NE. Fique com 3, em ordem: quadro 1 da idle · aceno de cabeça com o gesto no auge · mão voltando. Descarte o último quadro gerado se ele repetir o primeiro.
- **B-009** · `thrasybios_argue_se.png` · `argue` · 4 quadros (tira 128×32) · 200 ms por quadro, sem loop · `animar volta`
  ```text
  Facing south-east in 3/4 front view: sells the argument like a product: lifts the closed papyrus scroll high, taps it with the ringed right hand, points it at the listener with a smug smile, then settles back into the orator stance.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle SE. Fique com 4: ergue o rolo · bate no rolo com a mão do anel · aponta o rolo para o ouvinte, sorriso · volta à pose de orador.
- **B-010** · `thrasybios_argue_ne.png` · `argue` · 4 quadros (tira 128×32) · 200 ms por quadro, sem loop · `animar volta`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: sells the argument like a product: lifts the closed papyrus scroll high, taps it with the ringed right hand, points it forward, then settles back into the orator stance.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle NE. Fique com 4: ergue o rolo · bate no rolo com a mão do anel · aponta o rolo para a frente · volta à pose de orador.
- **B-011** · `thrasybios_concede_se.png` · `concede` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing south-east in 3/4 front view: shifts into a held reluctant concession: the shoulders lowered, the head slightly down, one palm turned up, the scroll hanging at his side and the jaw tight.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle SE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: ombros baixos, cabeça baixa, uma palma para cima, rolo pendurado.
- **B-012** · `thrasybios_concede_ne.png` · `concede` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: shifts into a held reluctant concession: the shoulders lowered, the head slightly down, one palm turned up, the scroll hanging at his side.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle NE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: ombros baixos, cabeça baixa, uma palma para cima, rolo pendurado.
- **B-013** · `thrasybios_coherence_damage_se.png` · `coherence_damage` · 3 quadros (tira 96×32) · 100 ms por quadro, sem loop · `animar volta`
  ```text
  Facing south-east in 3/4 front view: reels from a hit: flinches with the eyes shut as the head snaps back, the upper body leans back to the left with the feet planted, then recovers toward the idle pose.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle SE. Fique com 3: recuo com os olhos fechados e a cabeça para trás · tronco 2 px para a esquerda, pés plantados · meio caminho de volta ao idle.
- **B-014** · `thrasybios_coherence_damage_ne.png` · `coherence_damage` · 3 quadros (tira 96×32) · 100 ms por quadro, sem loop · `animar volta`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: reels from a hit: the head jerks back and the shoulders hunch, the upper body leans back to the left with the feet planted, then recovers toward the idle pose.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle NE. Fique com 3: cabeça sacode para trás e ombros encolhem · tronco 2 px para a esquerda, pés plantados · meio caminho de volta ao idle.
- **B-015** · `thrasybios_defeat_se.png` · `defeat` · 6 quadros (tira 192×32) · 220 ms por quadro, sem loop, segura o último · `animar`
  ```text
  Facing south-east in 3/4 front view: is defeated: staggers back, the scroll slips from his hand, he sinks to one knee with one hand on the ground and the head bowed, the scroll lying beside him.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle SE, sem `last_frame`. Fique com 6: cambaleia para trás · o rolo escorrega · um joelho no chão · uma mão no chão · cabeça baixa · pose final com o rolo no chão (segura).
- **B-016** · `thrasybios_defeat_ne.png` · `defeat` · 6 quadros (tira 192×32) · 220 ms por quadro, sem loop, segura o último · `animar`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: is defeated: staggers back, the scroll slips from his hand, he sinks to one knee with one hand on the ground and the head bowed, the scroll lying beside him.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle NE, sem `last_frame`. Fique com 6: cambaleia para trás · o rolo escorrega · um joelho no chão · uma mão no chão · cabeça baixa · pose final com o rolo no chão (segura).
- **B-017** · `thrasybios_victory_se.png` · `victory` · 4 quadros (tira 128×32) · 300 ms por quadro, sem loop, segura o último · `animar`
  ```text
  Facing south-east in 3/4 front view: plays to the crowd: spreads both arms to the audience, raises the scroll overhead, gives a theatrical little bow, then holds a proud final pose with the chin high.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle SE, sem `last_frame`. Fique com 4: abre os braços para a plateia · rolo acima da cabeça · reverência teatral · pose final de queixo alto (segura).
- **B-018** · `thrasybios_victory_ne.png` · `victory` · 4 quadros (tira 128×32) · 300 ms por quadro, sem loop, segura o último · `animar`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: plays to the crowd: spreads both arms to the audience, raises the scroll overhead, gives a theatrical little bow, then holds a proud final pose with the head high.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle NE, sem `last_frame`. Fique com 4: abre os braços para a plateia · rolo acima da cabeça · reverência teatral · pose final de queixo alto (segura).
- **B-019** · `portrait_thrasybios_neutral.png` · retrato 32×32 · `retrato`
  ```text
  [PORTRAIT] Source: frame 1 of thrasybios_idle_se.png. Check: confident salesman smile showing perfect teeth, chin raised.
  ```
- **B-020** · `portrait_thrasybios_shaken.png` · retrato 32×32 · `expressão`
  ```text
  [FACE] Source: portrait_thrasybios_neutral.png, mask on the eyes, brows and mouth. Description: forced smile cracking, one eyebrow raised higher than the other, a bead of sweat on the temple.
  ```

#### Óron, o cambista · `oron` · DUELISTA

- **Altura no canvas:** 22 px, baixo e largo.
- **Onde fica:** `floor_marble` (ponta da stoa, célula 10,10).
- **Direções:** SE e NE; view fixada no piloto (§3.2), a mesma para o elenco inteiro.
- **Arquivos:** 14 tiras (30 quadros) e 2 retratos, em P1.
- **Chave de cor:** outline, outline_blue, skin_light, skin_mid, skin_shade, hair_mid, hair_dark, ochre_light, ochre_mid, ochre_shade (himátion mostarda), marble_mid (quíton), gold_mid, gold_dark (balança), gold_light (moedas) — 14 cores.
- **Silhueta e atuação:** Baixo e barrigudo, a balança de dois pratos erguida: "só existem estas duas opções" (falso dilema). Bolsa de moedas no cinto. Sorriso comercial.

Descritor `[ORON]`:

```text
Oron, an ancient Greek money changer, about 22 pixels tall, short and pot-bellied, curly brown hair and a short beard, a mustard-ochre himation over a knee-length off-white chiton, bare legs, leather sandals, a coin purse at the belt, holding up a small golden bronze two-pan balance scale in the right hand, a salesman's smile,
```

- **B-021** · `oron_idle_se.png` · `idle` · 2 quadros (tira 64×32) · 600 ms por quadro, em loop · `criar`
  ```text
  [ORON] facing south in front view, short and pot-bellied, the balance scale held up in the right hand, [CHARACTER] [STYLE_CHAR]
  ```
  **Quadros:** Quadro 1 = a rotação SE do personagem criado a partir do sprite sul deste prompt (§3.3, `criar`). Quadro 2 à mão: suba 1 px as linhas da cabeça, dos ombros e do peito, com pés e sombra idênticos.
- **B-022** · `oron_idle_ne.png` · `idle` · 2 quadros (tira 64×32) · 600 ms por quadro, em loop · `rotação + mão`
  ```text
  [EDIT] Frame 1 is the north-east rotation of the character created for oron_idle_se.png, with zero extra generations. Frame 2 copies frame 1 with the head, shoulder and chest rows moved up 1 px, while the feet and the shadow stay identical.
  ```
- **B-023** · `oron_walk_se.png` · `walk` · 4 quadros (tira 128×32) · 150 ms por quadro, em loop · `template walk + mão`
  ```text
  Facing south-east in 3/4 front view: walking with waddling steps, the scale swinging a little.
  ```
  **Seleção:** Template `walking-4-frames`, `directions` = ["south-east"], este texto em `action_description`. Confira os pés plantados na linha 29; depois, à mão, suba 1 px a cabeça e o tronco nos dois quadros de passagem (pernas juntas).
- **B-024** · `oron_walk_ne.png` · `walk` · 4 quadros (tira 128×32) · 150 ms por quadro, em loop · `template walk + mão`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: walking with waddling steps, the scale swinging a little.
  ```
  **Seleção:** Template `walking-4-frames`, `directions` = ["north-east"], este texto em `action_description`. Confira os pés plantados na linha 29; depois, à mão, suba 1 px a cabeça e o tronco nos dois quadros de passagem (pernas juntas).
- **B-025** · `oron_talk_se.png` · `talk` · 3 quadros (tira 96×32) · 200 ms por quadro, em loop · `animar loop`
  ```text
  Facing south-east in 3/4 front view: talks in place, the mouth opening and closing while he rubs the thumb and fingers of the free hand together, then settles back to the idle pose.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle SE. Fique com 3, em ordem: quadro 1 da idle (boca fechada) · boca aberta com o gesto no auge · boca entreaberta, mão voltando. Descarte o último quadro gerado se ele repetir o primeiro.
- **B-026** · `oron_talk_ne.png` · `talk` · 3 quadros (tira 96×32) · 200 ms por quadro, em loop · `animar loop`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: talks in place with small nods of the head while he rubs the thumb and fingers of the free hand together, then settles back to the idle pose.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle NE. Fique com 3, em ordem: quadro 1 da idle · aceno de cabeça com o gesto no auge · mão voltando. Descarte o último quadro gerado se ele repetir o primeiro.
- **B-027** · `oron_argue_se.png` · `argue` · 3 quadros (tira 96×32) · 200 ms por quadro, sem loop · `animar`
  ```text
  Facing south-east in 3/4 front view: presents an argument with a signature gesture: holds the balance scale level, tips the left pan down, then tips the right pan down with a salesman's smile, as if only two options existed.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle SE, sem `last_frame`. Fique com 3: início do gesto · auge do gesto · pose final segurada (a assinatura do prompt).
- **B-028** · `oron_argue_ne.png` · `argue` · 3 quadros (tira 96×32) · 200 ms por quadro, sem loop · `animar`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: presents an argument with a signature gesture: holds the balance scale level, tips the left pan down, then tips the right pan down, as if only two options existed.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle NE, sem `last_frame`. Fique com 3: início do gesto · auge do gesto · pose final segurada (a assinatura do prompt).
- **B-029** · `oron_concede_se.png` · `concede` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing south-east in 3/4 front view: shifts into a held reluctant concession: the shoulders lowered, the head slightly down, one palm turned up, the scale lowered to his belly.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle SE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: ombros baixos, cabeça baixa, uma palma para cima.
- **B-030** · `oron_concede_ne.png` · `concede` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: shifts into a held reluctant concession: the shoulders lowered, the head slightly down, one palm turned up, the scale lowered to his belly.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle NE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: ombros baixos, cabeça baixa, uma palma para cima.
- **B-031** · `oron_coherence_damage_se.png` · `coherence_damage` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing south-east in 3/4 front view: shifts into a held pose, struck by an argument: flinching with the eyes shut, the head and upper body leaning back, one hand raised defensively, the feet planted on the ground line.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle SE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: recuo com os olhos fechados, uma mão erguida, pés plantados.
- **B-032** · `oron_coherence_damage_ne.png` · `coherence_damage` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: shifts into a held pose, struck by an argument: the head jerks back and the shoulders hunch, one hand raised defensively, the feet planted on the ground line.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle NE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: cabeça para trás, ombros encolhidos, uma mão erguida, pés plantados.
- **B-033** · `oron_defeat_se.png` · `defeat` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing south-east in 3/4 front view: shifts into a held pose, defeated: kneeling on one knee, the head bowed, the arms hanging.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle SE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: um joelho no chão, cabeça baixa, braços caídos.
- **B-034** · `oron_defeat_ne.png` · `defeat` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: shifts into a held pose, defeated: kneeling on one knee, the head bowed, the arms hanging.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle NE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: um joelho no chão, cabeça baixa, braços caídos.
- **B-035** · `portrait_oron_neutral.png` · retrato 32×32 · `retrato`
  ```text
  [PORTRAIT] Source: frame 1 of oron_idle_se.png. Check: salesman's smile.
  ```
- **B-036** · `portrait_oron_shaken.png` · retrato 32×32 · `expressão`
  ```text
  [FACE] Source: portrait_oron_neutral.png, mask on the eyes, brows and mouth. Description: eyes wide, brows raised, mouth in a tight nervous grin.
  ```

#### Phaidra, a adivinha · `phaidra` · DUELISTA

- **Altura no canvas:** 23 px, curvada.
- **Onde fica:** `floor_grass` (morro das oliveiras, célula 1,10).
- **Direções:** SE e NE; view fixada no piloto (§3.2), a mesma para o elenco inteiro.
- **Arquivos:** 14 tiras (30 quadros) e 2 retratos, em P1.
- **Chave de cor:** outline, outline_blue, skin_light, skin_mid, skin_shade, ochre_shade, hair_mid, hair_dark (xale escuro), marble_mid, marble_shade (vestido de linho), olive_light, olive_mid (ramo de louro), purple_light (contas), marble_light (astrágalos) — 14 cores.
- **Silhueta e atuação:** O xale como capuz é silhueta única no Ato I. Curvada à frente, espiando um presságio, o ramo de louro erguido. Muda em relação ao placeholder (todo oliva): oliva sobre a grama não se lê, então o xale é escuro.

Descritor `[PHAIDRA]`:

```text
Phaidra, an ancient Greek fortune-teller, about 23 pixels tall, slightly hunched, a dark brown wool shawl draped over her head and shoulders like a hood, an ankle-length pale grey-beige linen dress, a string of lilac amulet beads, a sprig of laurel held in her raised left hand, peering sideways,
```

- **B-037** · `phaidra_idle_se.png` · `idle` · 2 quadros (tira 64×32) · 600 ms por quadro, em loop · `criar`
  ```text
  [PHAIDRA] facing south in front view, slightly hunched, the laurel sprig raised in the left hand, [CHARACTER] [STYLE_CHAR]
  ```
  **Quadros:** Quadro 1 = a rotação SE do personagem criado a partir do sprite sul deste prompt (§3.3, `criar`). Quadro 2 à mão: suba 1 px as linhas da cabeça, dos ombros e do peito, com pés e sombra idênticos.
- **B-038** · `phaidra_idle_ne.png` · `idle` · 2 quadros (tira 64×32) · 600 ms por quadro, em loop · `rotação + mão`
  ```text
  [EDIT] Frame 1 is the north-east rotation of the character created for phaidra_idle_se.png, with zero extra generations. Frame 2 copies frame 1 with the head, shoulder and chest rows moved up 1 px, while the feet and the shadow stay identical.
  ```
- **B-039** · `phaidra_walk_se.png` · `walk` · 4 quadros (tira 128×32) · 150 ms por quadro, em loop · `template walk + mão`
  ```text
  Facing south-east in 3/4 front view: walking with creeping steps, the shawl swaying.
  ```
  **Seleção:** Template `walking-4-frames`, `directions` = ["south-east"], este texto em `action_description`. Confira os pés plantados na linha 29; depois, à mão, suba 1 px a cabeça e o tronco nos dois quadros de passagem (pernas juntas).
- **B-040** · `phaidra_walk_ne.png` · `walk` · 4 quadros (tira 128×32) · 150 ms por quadro, em loop · `template walk + mão`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: walking with creeping steps, the shawl swaying.
  ```
  **Seleção:** Template `walking-4-frames`, `directions` = ["north-east"], este texto em `action_description`. Confira os pés plantados na linha 29; depois, à mão, suba 1 px a cabeça e o tronco nos dois quadros de passagem (pernas juntas).
- **B-041** · `phaidra_talk_se.png` · `talk` · 3 quadros (tira 96×32) · 200 ms por quadro, em loop · `animar loop`
  ```text
  Facing south-east in 3/4 front view: talks in place, the mouth opening and closing while the laurel sprig wags once, then settles back to the idle pose.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle SE. Fique com 3, em ordem: quadro 1 da idle (boca fechada) · boca aberta com o gesto no auge · boca entreaberta, mão voltando. Descarte o último quadro gerado se ele repetir o primeiro.
- **B-042** · `phaidra_talk_ne.png` · `talk` · 3 quadros (tira 96×32) · 200 ms por quadro, em loop · `animar loop`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: talks in place with small nods of the head while the laurel sprig wags once, then settles back to the idle pose.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle NE. Fique com 3, em ordem: quadro 1 da idle · aceno de cabeça com o gesto no auge · mão voltando. Descarte o último quadro gerado se ele repetir o primeiro.
- **B-043** · `phaidra_argue_se.png` · `argue` · 3 quadros (tira 96×32) · 200 ms por quadro, sem loop · `animar`
  ```text
  Facing south-east in 3/4 front view: presents an argument with a signature gesture: raises the laurel sprig, shakes it as if reading an omen, then leans in, peering sideways with a knowing look.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle SE, sem `last_frame`. Fique com 3: início do gesto · auge do gesto · pose final segurada (a assinatura do prompt).
- **B-044** · `phaidra_argue_ne.png` · `argue` · 3 quadros (tira 96×32) · 200 ms por quadro, sem loop · `animar`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: presents an argument with a signature gesture: raises the laurel sprig, shakes it as if reading an omen, then leans in with the head turned to one side.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle NE, sem `last_frame`. Fique com 3: início do gesto · auge do gesto · pose final segurada (a assinatura do prompt).
- **B-045** · `phaidra_concede_se.png` · `concede` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing south-east in 3/4 front view: shifts into a held reluctant concession: the shoulders lowered, the head slightly down, one palm turned up.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle SE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: ombros baixos, cabeça baixa, uma palma para cima.
- **B-046** · `phaidra_concede_ne.png` · `concede` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: shifts into a held reluctant concession: the shoulders lowered, the head slightly down, one palm turned up.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle NE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: ombros baixos, cabeça baixa, uma palma para cima.
- **B-047** · `phaidra_coherence_damage_se.png` · `coherence_damage` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing south-east in 3/4 front view: shifts into a held pose, struck by an argument: flinching with the eyes shut, the head and upper body leaning back, one hand raised defensively, the feet planted on the ground line.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle SE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: recuo com os olhos fechados, uma mão erguida, pés plantados.
- **B-048** · `phaidra_coherence_damage_ne.png` · `coherence_damage` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: shifts into a held pose, struck by an argument: the head jerks back and the shoulders hunch, one hand raised defensively, the feet planted on the ground line.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle NE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: cabeça para trás, ombros encolhidos, uma mão erguida, pés plantados.
- **B-049** · `phaidra_defeat_se.png` · `defeat` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing south-east in 3/4 front view: shifts into a held pose, defeated: kneeling on one knee, the head bowed, the arms hanging.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle SE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: um joelho no chão, cabeça baixa, braços caídos.
- **B-050** · `phaidra_defeat_ne.png` · `defeat` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: shifts into a held pose, defeated: kneeling on one knee, the head bowed, the arms hanging.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle NE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: um joelho no chão, cabeça baixa, braços caídos.
- **B-051** · `portrait_phaidra_neutral.png` · retrato 32×32 · `retrato`
  ```text
  [PORTRAIT] Source: frame 1 of phaidra_idle_se.png. Check: knowing sideways glance from under the shawl.
  ```
- **B-052** · `portrait_phaidra_shaken.png` · retrato 32×32 · `expressão`
  ```text
  [FACE] Source: portrait_phaidra_neutral.png, mask on the eyes, brows and mouth. Description: eyes wide under the shawl, mouth agape.
  ```

#### Kritolaos, o político menor · `kritolaos` · DUELISTA

- **Altura no canvas:** 25 px.
- **Onde fica:** `floor_stone` (praça diante das casas, célula 6,10).
- **Direções:** SE e NE; view fixada no piloto (§3.2), a mesma para o elenco inteiro.
- **Arquivos:** 14 tiras (30 quadros) e 2 retratos, em P1.
- **Chave de cor:** outline, outline_blue, skin_light, skin_mid, skin_shade, marble_shade, marble_dark (cabelo e barba grisalhos), marble_light, marble_mid (himátion), olive_light, olive_mid (coroa de murta), gold_light (anel), ochre_shade (sandálias) — 13 cores.
- **Silhueta e atuação:** Himátion drapeado no ombro esquerdo e o braço direito esticado com o indicador acusador: o ad hominem aponta o homem. Mais velho que o Eudoro, grisalho, de coroa e manto longo.

Descritor `[KRITOLAOS]`:

```text
Kritolaos, a minor Athenian politician, about 25 pixels tall, middle-aged, grey hair and beard, a green myrtle wreath worn by Assembly speakers, a long off-white himation draped over the left shoulder, leather sandals, a gold ring, an indignant orator pose with the right arm ready to point,
```

- **B-053** · `kritolaos_idle_se.png` · `idle` · 2 quadros (tira 64×32) · 600 ms por quadro, em loop · `criar`
  ```text
  [KRITOLAOS] facing south in front view, indignant orator pose, the himation over the left shoulder, the right arm half raised, [CHARACTER] [STYLE_CHAR]
  ```
  **Quadros:** Quadro 1 = a rotação SE do personagem criado a partir do sprite sul deste prompt (§3.3, `criar`). Quadro 2 à mão: suba 1 px as linhas da cabeça, dos ombros e do peito, com pés e sombra idênticos.
- **B-054** · `kritolaos_idle_ne.png` · `idle` · 2 quadros (tira 64×32) · 600 ms por quadro, em loop · `rotação + mão`
  ```text
  [EDIT] Frame 1 is the north-east rotation of the character created for kritolaos_idle_se.png, with zero extra generations. Frame 2 copies frame 1 with the head, shoulder and chest rows moved up 1 px, while the feet and the shadow stay identical.
  ```
- **B-055** · `kritolaos_walk_se.png` · `walk` · 4 quadros (tira 128×32) · 150 ms por quadro, em loop · `template walk + mão`
  ```text
  Facing south-east in 3/4 front view: walking with a pompous stride, the head held high.
  ```
  **Seleção:** Template `walking-4-frames`, `directions` = ["south-east"], este texto em `action_description`. Confira os pés plantados na linha 29; depois, à mão, suba 1 px a cabeça e o tronco nos dois quadros de passagem (pernas juntas).
- **B-056** · `kritolaos_walk_ne.png` · `walk` · 4 quadros (tira 128×32) · 150 ms por quadro, em loop · `template walk + mão`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: walking with a pompous stride, the head held high.
  ```
  **Seleção:** Template `walking-4-frames`, `directions` = ["north-east"], este texto em `action_description`. Confira os pés plantados na linha 29; depois, à mão, suba 1 px a cabeça e o tronco nos dois quadros de passagem (pernas juntas).
- **B-057** · `kritolaos_talk_se.png` · `talk` · 3 quadros (tira 96×32) · 200 ms por quadro, em loop · `animar loop`
  ```text
  Facing south-east in 3/4 front view: talks in place, the mouth opening and closing while the right index finger jabs the air, then settles back to the idle pose.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle SE. Fique com 3, em ordem: quadro 1 da idle (boca fechada) · boca aberta com o gesto no auge · boca entreaberta, mão voltando. Descarte o último quadro gerado se ele repetir o primeiro.
- **B-058** · `kritolaos_talk_ne.png` · `talk` · 3 quadros (tira 96×32) · 200 ms por quadro, em loop · `animar loop`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: talks in place with small nods of the head while the right index finger jabs the air, then settles back to the idle pose.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle NE. Fique com 3, em ordem: quadro 1 da idle · aceno de cabeça com o gesto no auge · mão voltando. Descarte o último quadro gerado se ele repetir o primeiro.
- **B-059** · `kritolaos_argue_se.png` · `argue` · 3 quadros (tira 96×32) · 200 ms por quadro, sem loop · `animar`
  ```text
  Facing south-east in 3/4 front view: presents an argument with a signature gesture: inhales with the chest out, thrusts the right arm out into an accusing point with the index finger, then holds the accusation with the other hand on his chest.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle SE, sem `last_frame`. Fique com 3: início do gesto · auge do gesto · pose final segurada (a assinatura do prompt).
- **B-060** · `kritolaos_argue_ne.png` · `argue` · 3 quadros (tira 96×32) · 200 ms por quadro, sem loop · `animar`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: presents an argument with a signature gesture: inhales with the chest out, thrusts the right arm out into an accusing point with the index finger, then holds the accusation with the other hand on his chest.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle NE, sem `last_frame`. Fique com 3: início do gesto · auge do gesto · pose final segurada (a assinatura do prompt).
- **B-061** · `kritolaos_concede_se.png` · `concede` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing south-east in 3/4 front view: shifts into a held reluctant concession: the shoulders lowered, the head slightly down, one palm turned up.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle SE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: ombros baixos, cabeça baixa, uma palma para cima.
- **B-062** · `kritolaos_concede_ne.png` · `concede` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: shifts into a held reluctant concession: the shoulders lowered, the head slightly down, one palm turned up.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle NE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: ombros baixos, cabeça baixa, uma palma para cima.
- **B-063** · `kritolaos_coherence_damage_se.png` · `coherence_damage` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing south-east in 3/4 front view: shifts into a held pose, struck by an argument: flinching with the eyes shut, the head and upper body leaning back, one hand raised defensively, the feet planted on the ground line.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle SE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: recuo com os olhos fechados, uma mão erguida, pés plantados.
- **B-064** · `kritolaos_coherence_damage_ne.png` · `coherence_damage` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: shifts into a held pose, struck by an argument: the head jerks back and the shoulders hunch, one hand raised defensively, the feet planted on the ground line.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle NE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: cabeça para trás, ombros encolhidos, uma mão erguida, pés plantados.
- **B-065** · `kritolaos_defeat_se.png` · `defeat` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing south-east in 3/4 front view: shifts into a held pose, defeated: kneeling on one knee, the head bowed, the arms hanging.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle SE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: um joelho no chão, cabeça baixa, braços caídos.
- **B-066** · `kritolaos_defeat_ne.png` · `defeat` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: shifts into a held pose, defeated: kneeling on one knee, the head bowed, the arms hanging.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle NE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: um joelho no chão, cabeça baixa, braços caídos.
- **B-067** · `portrait_kritolaos_neutral.png` · retrato 32×32 · `retrato`
  ```text
  [PORTRAIT] Source: frame 1 of kritolaos_idle_se.png. Check: indignant raised brows, the myrtle wreath on the head.
  ```
- **B-068** · `portrait_kritolaos_shaken.png` · retrato 32×32 · `expressão`
  ```text
  [FACE] Source: portrait_kritolaos_neutral.png, mask on the eyes, brows and mouth. Description: sputtering, brows drawn down, mouth wide open.
  ```

#### Simo, o ator · `simo` · DUELISTA

- **Altura no canvas:** 25 px com o coturno.
- **Onde fica:** `floor_marble` (praça em xadrez, célula 8,8).
- **Direções:** SE e NE; view fixada no piloto (§3.2), a mesma para o elenco inteiro.
- **Arquivos:** 14 tiras (30 quadros) e 2 retratos, em P1.
- **Chave de cor:** outline, outline_blue, skin_light, skin_mid, skin_shade, hair_dark, gold_light, gold_mid, gold_dark (krokotos açafrão), red_mid (barra e boca da máscara), marble_light, marble_mid (máscara), ochre_shade (coturnos) — 13 cores.
- **Silhueta e atuação:** Pose teatral, a máscara sorridente erguida ao lado do rosto. No argue ele põe a máscara na frente do rosto: o espantalho veste a cara do outro. Muda em relação ao placeholder (vermelho): açafrão, para não disputar o vermelho com a Damaris.

Descritor `[SIMO]`:

```text
Simo, an ancient Greek stage actor, about 25 pixels tall including high-soled theatre boots, curly black hair, a flowing ankle-length saffron-yellow robe with a brick-red hem, an off-white theatre mask with an exaggerated grin held beside his face, a flamboyant pose,
```

- **B-069** · `simo_idle_se.png` · `idle` · 2 quadros (tira 64×32) · 600 ms por quadro, em loop · `criar`
  ```text
  [SIMO] facing south in front view, flamboyant actor pose, the grinning mask held beside his face in the raised right hand, [CHARACTER] [STYLE_CHAR]
  ```
  **Quadros:** Quadro 1 = a rotação SE do personagem criado a partir do sprite sul deste prompt (§3.3, `criar`). Quadro 2 à mão: suba 1 px as linhas da cabeça, dos ombros e do peito, com pés e sombra idênticos.
- **B-070** · `simo_idle_ne.png` · `idle` · 2 quadros (tira 64×32) · 600 ms por quadro, em loop · `rotação + mão`
  ```text
  [EDIT] Frame 1 is the north-east rotation of the character created for simo_idle_se.png, with zero extra generations. Frame 2 copies frame 1 with the head, shoulder and chest rows moved up 1 px, while the feet and the shadow stay identical.
  ```
- **B-071** · `simo_walk_se.png` · `walk` · 4 quadros (tira 128×32) · 150 ms por quadro, em loop · `template walk + mão`
  ```text
  Facing south-east in 3/4 front view: walking with light theatrical steps on high-soled boots.
  ```
  **Seleção:** Template `walking-4-frames`, `directions` = ["south-east"], este texto em `action_description`. Confira os pés plantados na linha 29; depois, à mão, suba 1 px a cabeça e o tronco nos dois quadros de passagem (pernas juntas).
- **B-072** · `simo_walk_ne.png` · `walk` · 4 quadros (tira 128×32) · 150 ms por quadro, em loop · `template walk + mão`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: walking with light theatrical steps on high-soled boots.
  ```
  **Seleção:** Template `walking-4-frames`, `directions` = ["north-east"], este texto em `action_description`. Confira os pés plantados na linha 29; depois, à mão, suba 1 px a cabeça e o tronco nos dois quadros de passagem (pernas juntas).
- **B-073** · `simo_talk_se.png` · `talk` · 3 quadros (tira 96×32) · 200 ms por quadro, em loop · `animar loop`
  ```text
  Facing south-east in 3/4 front view: talks in place, the mouth opening and closing while the free hand makes a flourish, then settles back to the idle pose.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle SE. Fique com 3, em ordem: quadro 1 da idle (boca fechada) · boca aberta com o gesto no auge · boca entreaberta, mão voltando. Descarte o último quadro gerado se ele repetir o primeiro.
- **B-074** · `simo_talk_ne.png` · `talk` · 3 quadros (tira 96×32) · 200 ms por quadro, em loop · `animar loop`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: talks in place with small nods of the head while the free hand makes a flourish, then settles back to the idle pose.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle NE. Fique com 3, em ordem: quadro 1 da idle · aceno de cabeça com o gesto no auge · mão voltando. Descarte o último quadro gerado se ele repetir o primeiro.
- **B-075** · `simo_argue_se.png` · `argue` · 3 quadros (tira 96×32) · 200 ms por quadro, sem loop · `animar`
  ```text
  Facing south-east in 3/4 front view: presents an argument with a signature gesture: lifts the grinning mask, holds it in front of his own face, then lowers it with a mocking little bow.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle SE, sem `last_frame`. Fique com 3: início do gesto · auge do gesto · pose final segurada (a assinatura do prompt).
- **B-076** · `simo_argue_ne.png` · `argue` · 3 quadros (tira 96×32) · 200 ms por quadro, sem loop · `animar`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: presents an argument with a signature gesture: lifts the theatre mask up to his head, holds it there, then lowers it with a mocking little bow.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle NE, sem `last_frame`. Fique com 3: início do gesto · auge do gesto · pose final segurada (a assinatura do prompt).
- **B-077** · `simo_concede_se.png` · `concede` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing south-east in 3/4 front view: shifts into a held reluctant concession: the shoulders lowered, the head slightly down, one palm turned up.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle SE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: ombros baixos, cabeça baixa, uma palma para cima.
- **B-078** · `simo_concede_ne.png` · `concede` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: shifts into a held reluctant concession: the shoulders lowered, the head slightly down, one palm turned up.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle NE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: ombros baixos, cabeça baixa, uma palma para cima.
- **B-079** · `simo_coherence_damage_se.png` · `coherence_damage` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing south-east in 3/4 front view: shifts into a held pose, struck by an argument: flinching with the eyes shut, the head and upper body leaning back, one hand raised defensively, the feet planted on the ground line.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle SE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: recuo com os olhos fechados, uma mão erguida, pés plantados.
- **B-080** · `simo_coherence_damage_ne.png` · `coherence_damage` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: shifts into a held pose, struck by an argument: the head jerks back and the shoulders hunch, one hand raised defensively, the feet planted on the ground line.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle NE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: cabeça para trás, ombros encolhidos, uma mão erguida, pés plantados.
- **B-081** · `simo_defeat_se.png` · `defeat` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing south-east in 3/4 front view: shifts into a held pose, defeated: kneeling on one knee, the head bowed, the arms hanging.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle SE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: um joelho no chão, cabeça baixa, braços caídos.
- **B-082** · `simo_defeat_ne.png` · `defeat` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: shifts into a held pose, defeated: kneeling on one knee, the head bowed, the arms hanging.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle NE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: um joelho no chão, cabeça baixa, braços caídos.
- **B-083** · `portrait_simo_neutral.png` · retrato 32×32 · `retrato`
  ```text
  [PORTRAIT] Source: frame 1 of simo_idle_se.png. Check: theatrical grin.
  ```
- **B-084** · `portrait_simo_shaken.png` · retrato 32×32 · `expressão`
  ```text
  [FACE] Source: portrait_simo_neutral.png, mask on the eyes, brows and mouth. Description: the theatrical grin frozen, eyes wide in panic.
  ```

#### Bátilo, o pescador · `batilo` · DUELISTA

- **Altura no canvas:** 25 px, o mais largo.
- **Onde fica:** `floor_marble_ochre` junto à fonte (célula 7,6).
- **Direções:** SE e NE; view fixada no piloto (§3.2), a mesma para o elenco inteiro.
- **Arquivos:** 14 tiras (30 quadros) e 2 retratos, em P1.
- **Chave de cor:** outline, outline_blue, skin_light, skin_mid, skin_shade, hair_mid, hair_dark, aegean_mid, aegean_shade, aegean_dark (exômide), ochre_shade, ochre_dark (gorro de feltro), ochre_light, ochre_mid (rede), marble_shade (peixe prateado) — 15 cores.
- **Silhueta e atuação:** Largo e forte, gorro cônico inconfundível, rede enrolada no ombro esquerdo, ombro direito nu. O único simpático, gestos calmos. O azul-marinho é parente do azul da Kleia (os dois trabalham), mas em outro valor.

Descritor `[BATILO]`:

```text
Batilo, a friendly ancient Greek fisherman, about 25 pixels tall and the broadest of the cast, sun-tanned, brown hair and beard, a conical brown felt pilos cap, a short navy-blue exomis tunic pinned on the left shoulder with the right shoulder bare, bare legs and bare feet, a rope fishing net coiled over the left shoulder, a small silver fish in the right hand, a relaxed open smile,
```

- **B-085** · `batilo_idle_se.png` · `idle` · 2 quadros (tira 64×32) · 600 ms por quadro, em loop · `criar`
  ```text
  [BATILO] facing south in front view, broad relaxed stance, the net over the left shoulder, the small fish in the right hand, [CHARACTER] [STYLE_CHAR]
  ```
  **Quadros:** Quadro 1 = a rotação SE do personagem criado a partir do sprite sul deste prompt (§3.3, `criar`). Quadro 2 à mão: suba 1 px as linhas da cabeça, dos ombros e do peito, com pés e sombra idênticos.
- **B-086** · `batilo_idle_ne.png` · `idle` · 2 quadros (tira 64×32) · 600 ms por quadro, em loop · `rotação + mão`
  ```text
  [EDIT] Frame 1 is the north-east rotation of the character created for batilo_idle_se.png, with zero extra generations. Frame 2 copies frame 1 with the head, shoulder and chest rows moved up 1 px, while the feet and the shadow stay identical.
  ```
- **B-087** · `batilo_walk_se.png` · `walk` · 4 quadros (tira 128×32) · 150 ms por quadro, em loop · `template walk + mão`
  ```text
  Facing south-east in 3/4 front view: walking with heavy easy steps.
  ```
  **Seleção:** Template `walking-4-frames`, `directions` = ["south-east"], este texto em `action_description`. Confira os pés plantados na linha 29; depois, à mão, suba 1 px a cabeça e o tronco nos dois quadros de passagem (pernas juntas).
- **B-088** · `batilo_walk_ne.png` · `walk` · 4 quadros (tira 128×32) · 150 ms por quadro, em loop · `template walk + mão`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: walking with heavy easy steps.
  ```
  **Seleção:** Template `walking-4-frames`, `directions` = ["north-east"], este texto em `action_description`. Confira os pés plantados na linha 29; depois, à mão, suba 1 px a cabeça e o tronco nos dois quadros de passagem (pernas juntas).
- **B-089** · `batilo_talk_se.png` · `talk` · 3 quadros (tira 96×32) · 200 ms por quadro, em loop · `animar loop`
  ```text
  Facing south-east in 3/4 front view: talks in place, the mouth opening and closing while the open palm turns up, calm, then settles back to the idle pose.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle SE. Fique com 3, em ordem: quadro 1 da idle (boca fechada) · boca aberta com o gesto no auge · boca entreaberta, mão voltando. Descarte o último quadro gerado se ele repetir o primeiro.
- **B-090** · `batilo_talk_ne.png` · `talk` · 3 quadros (tira 96×32) · 200 ms por quadro, em loop · `animar loop`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: talks in place with small nods of the head while the open palm turns up, calm, then settles back to the idle pose.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle NE. Fique com 3, em ordem: quadro 1 da idle · aceno de cabeça com o gesto no auge · mão voltando. Descarte o último quadro gerado se ele repetir o primeiro.
- **B-091** · `batilo_argue_se.png` · `argue` · 3 quadros (tira 96×32) · 200 ms por quadro, sem loop · `animar`
  ```text
  Facing south-east in 3/4 front view: presents an argument with a signature gesture: lifts the single fish by the tail, shows it proudly as if one fish proved everything, then shrugs with a friendly smile.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle SE, sem `last_frame`. Fique com 3: início do gesto · auge do gesto · pose final segurada (a assinatura do prompt).
- **B-092** · `batilo_argue_ne.png` · `argue` · 3 quadros (tira 96×32) · 200 ms por quadro, sem loop · `animar`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: presents an argument with a signature gesture: lifts the single fish by the tail, shows it proudly as if one fish proved everything, then shrugs.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle NE, sem `last_frame`. Fique com 3: início do gesto · auge do gesto · pose final segurada (a assinatura do prompt).
- **B-093** · `batilo_concede_se.png` · `concede` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing south-east in 3/4 front view: shifts into a held reluctant concession: the shoulders lowered, the head slightly down, one palm turned up.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle SE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: ombros baixos, cabeça baixa, uma palma para cima.
- **B-094** · `batilo_concede_ne.png` · `concede` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: shifts into a held reluctant concession: the shoulders lowered, the head slightly down, one palm turned up.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle NE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: ombros baixos, cabeça baixa, uma palma para cima.
- **B-095** · `batilo_coherence_damage_se.png` · `coherence_damage` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing south-east in 3/4 front view: shifts into a held pose, struck by an argument: flinching with the eyes shut, the head and upper body leaning back, one hand raised defensively, the feet planted on the ground line.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle SE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: recuo com os olhos fechados, uma mão erguida, pés plantados.
- **B-096** · `batilo_coherence_damage_ne.png` · `coherence_damage` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: shifts into a held pose, struck by an argument: the head jerks back and the shoulders hunch, one hand raised defensively, the feet planted on the ground line.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle NE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: cabeça para trás, ombros encolhidos, uma mão erguida, pés plantados.
- **B-097** · `batilo_defeat_se.png` · `defeat` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing south-east in 3/4 front view: shifts into a held pose, defeated: kneeling on one knee, the head bowed, the arms hanging.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle SE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: um joelho no chão, cabeça baixa, braços caídos.
- **B-098** · `batilo_defeat_ne.png` · `defeat` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: shifts into a held pose, defeated: kneeling on one knee, the head bowed, the arms hanging.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle NE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: um joelho no chão, cabeça baixa, braços caídos.
- **B-099** · `portrait_batilo_neutral.png` · retrato 32×32 · `retrato`
  ```text
  [PORTRAIT] Source: frame 1 of batilo_idle_se.png. Check: warm open smile under the felt cap.
  ```
- **B-100** · `portrait_batilo_shaken.png` · retrato 32×32 · `expressão`
  ```text
  [FACE] Source: portrait_batilo_neutral.png, mask on the eyes, brows and mouth. Description: puzzled frown, one brow raised, mouth pulled to one side.
  ```

#### Ágora (`agora.json`)

- **B-001** · `floor_marble.png` · 32×16 · `iso-tile thin` — Piloto do ART-03 #3, junto com o ocre. O veio aparece em 1 de cada 4 tiles no GDD; aqui é um arquivo só, com veio discreto. O veio usa a sombra fria do mármore (`marble_cold_shade`, palette v2).
  ```text
  Polished white marble floor, off-white base with pale grey-beige texture and one subtle curved 1-pixel pale blue-grey vein, [FLOOR] [STYLE_ENV]
  ```
- **B-002** · `floor_marble_ochre.png` · 32×16 · `iso-tile thin` — Piloto em xadrez com `floor_marble`.
  ```text
  Polished marble floor in warm ochre, light sand-ochre base with warm orange-ochre texture and one rust-brown vein, made to alternate with a white marble tile in a checkerboard, [FLOOR] [STYLE_ENV]
  ```
- **B-101** · `floor_grass.png` · 32×16 · `iso-tile thin`
  ```text
  Olive-green grass, olive green base with dark olive green patches and 2-pixel yellow-green tufts, [FLOOR] [STYLE_ENV]
  ```
- **B-102** · `floor_mosaic.png` · 32×16 · `iso-tile thin` — O GDD chama de `floor_mosaic_meander` e pede um conjunto de 4; o mapa usa um id só na borda inteira da praça (linhas 2 e 9, colunas 2 e 9, e os 4 cantos). P1 = um tile simétrico que serve em qualquer posição. O conjunto de 4 orientado está no P3 (`floor_mosaic_straight` e os 3 cantos).
  ```text
  White marble tile with a square Greek key meander motif in sea blue centred on the tile, symmetric on both diagonal axes of the diamond and touching the middle of all four edges, so the pattern continues into mosaic neighbours on every side, corners of the border included, [FLOOR] [STYLE_ENV]
  ```
- **B-103** · `floor_stone.png` · 32×16 · `iso-tile thin` — O GDD chama de `floor_worn_stone`.
  ```text
  Irregular worn grey paving stones, pale grey-beige stones with light grey wear and cool dark grey joints between the stones, cool blue-grey shadows, [FLOOR] [STYLE_ENV]
  ```
- **B-104** · `floor_water.png` · 32×16 · `iso-tile thin` — A fonte do mapa são 2×2 células de água.
  ```text
  Fountain basin water, sea-blue body with deep sea-blue depth, one bright cyan reflection, a 1-pixel off-white foam highlight and a navy-blue rim, [FLOOR] [STYLE_ENV]
  ```
- **B-105** · `block_doric_column.png` · 32×56 · `bitforge+init` — Contorno `outline_blue`. Caso difícil de ordenação: ator atrás da coluna.
  ```text
  A Doric marble column on one cell, a fluted off-white shaft with five 1-pixel vertical flutes rising straight from the floor, a simple cushion capital and a square abacus, cool blue-grey shadows, [BLOCK] [STYLE_ENV]
  ```
- **B-106** · `block_step.png` · 32×24 · `iso-tile thick` — Escadaria do Pórtico (I.8). `thick tile` em 32×32 dá cerca de 8 px de lateral; corte as linhas transparentes de cima até 32×24.
  ```text
  A single marble stair step with an 8-pixel riser, off-white top, pale grey-beige riser and cool blue-grey shadows, stackable in a row of three, [BLOCK] [STYLE_ENV]
  ```
- **B-107** · `wall_marble.png` · 32×32 · `iso-tile block` — Contorno `outline_blue` (arquitetura de mármore).
  ```text
  White marble wall block, a 16-pixel diamond top over 16 pixels of smooth ashlar wall with faint joints, off-white top, pale grey-beige left face, pale blue-grey right face, cool blue-grey shadows, [BLOCK] [STYLE_ENV]
  ```
- **B-108** · `wall_marble_tall.png` · 32×48 · `Aseprite (edição)` — Parede alta da referência, só nas bordas de trás das salas (§5.5). Serve à Ágora e às salas da Academia.
  ```text
  [EDIT] From the approved wall_marble.png, zero generations: keep the 16-px diamond top and extend both faces down to 32 px of wall by repeating the ashlar courses and their faint joints, same colors and outline_blue outline; the base diamond ends on the bottom row of the 32x48 canvas.
  ```
- **B-109** · `prop_market_stall.png` · 32×28 · `map-object+init` — `init_image` = o placeholder. Gere em 32×32 e corte o topo.
  ```text
  A small wooden market stall with a striped awning of 2-pixel brick-red and off-white stripes over a table of pots and fruit, [PROP] [STYLE_ENV]
  ```
- **B-110** · `prop_olive_trunk.png` · 32×40 · `map-object+init` — Compartilhado (3 na Ágora, 1 no Cerâmico). Dono único: Trilha B. `init_image` = o placeholder. Largura 32 no P1 (ver §11 sobre 48 px).
  ```text
  An old olive tree with a twisted dark brown trunk and a compact three-tone olive-green canopy with 2 to 3 silvery light grey leaf undersides, the whole tree within one cell width, [PROP] [STYLE_ENV]
  ```
- **B-111** · `prop_statue_base.png` · 32×48 · `bitforge+init` — O GDD chama de `prop_herm_statue` (32×48). O placeholder mede 32×36; com pivot Bottom Left o arquivo só cresce para cima. Recapture a referência.
  ```text
  A herm on one cell, a square white marble pillar with a bearded male head carved on top, off-white marble with cool blue-grey shadows, [PROP] [STYLE_ENV]
  ```

### 10.2 P2 — Ato II e HUD da referência

97 arquivos (B-112 a B-208), 169 quadros.

#### Zenão de Eleia · `zeno` · DUELISTA

- **Altura no canvas:** 25 px.
- **Onde fica:** Ato II.
- **Direções:** SE e NE; view fixada no piloto (§3.2), a mesma para o elenco inteiro.
- **Arquivos:** 14 tiras (30 quadros) e 2 retratos, em P2.
- **Chave de cor:** outline, outline_blue, skin_light, skin_mid, skin_shade, marble_light, marble_mid (himátion), marble_shade, marble_dark (barba e cabelo grisalhos), aegean_shade (barra), olive_mid, olive_shade (tartaruga), ochre_shade (sandálias) — 13 cores.
- **Silhueta e atuação:** Sem ficha no GDD (proposta). Esguio, barba grisalha pontuda, uma tartaruga pequena na palma: Aquiles e a tartaruga.

Descritor `[ZENO]`:

```text
Zeno of Elea, a 55-year-old philosopher, about 25 pixels tall, lean and upright, a pointed grey beard, grey hair, an off-white linen himation with a deep sea-blue hem draped over the left shoulder, leather sandals, a small olive-green tortoise resting on his left palm, a patient faint smile,
```

- **B-112** · `zeno_idle_se.png` · `idle` · 2 quadros (tira 64×32) · 600 ms por quadro, em loop · `criar`
  ```text
  [ZENO] facing south in front view, lean and upright, the tortoise resting on the left palm, [CHARACTER] [STYLE_CHAR]
  ```
  **Quadros:** Quadro 1 = a rotação SE do personagem criado a partir do sprite sul deste prompt (§3.3, `criar`). Quadro 2 à mão: suba 1 px as linhas da cabeça, dos ombros e do peito, com pés e sombra idênticos.
- **B-113** · `zeno_idle_ne.png` · `idle` · 2 quadros (tira 64×32) · 600 ms por quadro, em loop · `rotação + mão`
  ```text
  [EDIT] Frame 1 is the north-east rotation of the character created for zeno_idle_se.png, with zero extra generations. Frame 2 copies frame 1 with the head, shoulder and chest rows moved up 1 px, while the feet and the shadow stay identical.
  ```
- **B-114** · `zeno_walk_se.png` · `walk` · 4 quadros (tira 128×32) · 150 ms por quadro, em loop · `template walk + mão`
  ```text
  Facing south-east in 3/4 front view: walking with unhurried measured steps.
  ```
  **Seleção:** Template `walking-4-frames`, `directions` = ["south-east"], este texto em `action_description`. Confira os pés plantados na linha 29; depois, à mão, suba 1 px a cabeça e o tronco nos dois quadros de passagem (pernas juntas).
- **B-115** · `zeno_walk_ne.png` · `walk` · 4 quadros (tira 128×32) · 150 ms por quadro, em loop · `template walk + mão`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: walking with unhurried measured steps.
  ```
  **Seleção:** Template `walking-4-frames`, `directions` = ["north-east"], este texto em `action_description`. Confira os pés plantados na linha 29; depois, à mão, suba 1 px a cabeça e o tronco nos dois quadros de passagem (pernas juntas).
- **B-116** · `zeno_talk_se.png` · `talk` · 3 quadros (tira 96×32) · 200 ms por quadro, em loop · `animar loop`
  ```text
  Facing south-east in 3/4 front view: talks in place, the mouth opening and closing while the right index finger lifts, then settles back to the idle pose.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle SE. Fique com 3, em ordem: quadro 1 da idle (boca fechada) · boca aberta com o gesto no auge · boca entreaberta, mão voltando. Descarte o último quadro gerado se ele repetir o primeiro.
- **B-117** · `zeno_talk_ne.png` · `talk` · 3 quadros (tira 96×32) · 200 ms por quadro, em loop · `animar loop`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: talks in place with small nods of the head while the right index finger lifts, then settles back to the idle pose.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle NE. Fique com 3, em ordem: quadro 1 da idle · aceno de cabeça com o gesto no auge · mão voltando. Descarte o último quadro gerado se ele repetir o primeiro.
- **B-118** · `zeno_argue_se.png` · `argue` · 3 quadros (tira 96×32) · 200 ms por quadro, sem loop · `animar`
  ```text
  Facing south-east in 3/4 front view: presents an argument with a signature gesture: holds the tortoise forward on his palm, raises the right index finger, then gives a patient knowing smile.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle SE, sem `last_frame`. Fique com 3: início do gesto · auge do gesto · pose final segurada (a assinatura do prompt).
- **B-119** · `zeno_argue_ne.png` · `argue` · 3 quadros (tira 96×32) · 200 ms por quadro, sem loop · `animar`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: presents an argument with a signature gesture: holds the tortoise forward on his palm, raises the right index finger and holds the pose.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle NE, sem `last_frame`. Fique com 3: início do gesto · auge do gesto · pose final segurada (a assinatura do prompt).
- **B-120** · `zeno_concede_se.png` · `concede` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing south-east in 3/4 front view: shifts into a held reluctant concession: the shoulders lowered, the head slightly down, one palm turned up.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle SE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: ombros baixos, cabeça baixa, uma palma para cima.
- **B-121** · `zeno_concede_ne.png` · `concede` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: shifts into a held reluctant concession: the shoulders lowered, the head slightly down, one palm turned up.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle NE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: ombros baixos, cabeça baixa, uma palma para cima.
- **B-122** · `zeno_coherence_damage_se.png` · `coherence_damage` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing south-east in 3/4 front view: shifts into a held pose, struck by an argument: flinching with the eyes shut, the head and upper body leaning back, one hand raised defensively, the feet planted on the ground line.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle SE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: recuo com os olhos fechados, uma mão erguida, pés plantados.
- **B-123** · `zeno_coherence_damage_ne.png` · `coherence_damage` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: shifts into a held pose, struck by an argument: the head jerks back and the shoulders hunch, one hand raised defensively, the feet planted on the ground line.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle NE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: cabeça para trás, ombros encolhidos, uma mão erguida, pés plantados.
- **B-124** · `zeno_defeat_se.png` · `defeat` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing south-east in 3/4 front view: shifts into a held pose, defeated: kneeling on one knee, the head bowed, the arms hanging.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle SE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: um joelho no chão, cabeça baixa, braços caídos.
- **B-125** · `zeno_defeat_ne.png` · `defeat` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: shifts into a held pose, defeated: kneeling on one knee, the head bowed, the arms hanging.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle NE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: um joelho no chão, cabeça baixa, braços caídos.
- **B-126** · `portrait_zeno_neutral.png` · retrato 32×32 · `retrato`
  ```text
  [PORTRAIT] Source: frame 1 of zeno_idle_se.png. Check: patient faint smile.
  ```
- **B-127** · `portrait_zeno_shaken.png` · retrato 32×32 · `expressão`
  ```text
  [FACE] Source: portrait_zeno_neutral.png, mask on the eyes, brows and mouth. Description: brows knit, lips pressed together.
  ```

#### Eubúlides · `eubulides` · DUELISTA

- **Altura no canvas:** 24 px.
- **Onde fica:** Ato II.
- **Direções:** SE e NE; view fixada no piloto (§3.2), a mesma para o elenco inteiro.
- **Arquivos:** 14 tiras (30 quadros) e 2 retratos, em P2.
- **Chave de cor:** outline, outline_blue, skin_light, skin_mid, skin_shade, hair_mid, hair_dark, marble_shade, marble_dark (capa), olive_shade (forro), gold_mid, gold_dark (espelho de bronze), gold_light (1 px de brilho), ochre_shade (sandálias) — 14 cores.
- **Silhueta e atuação:** Sem ficha no GDD (proposta). Sorriso de canto, capa escura, um espelho de bronze: o Mentiroso.

Descritor `[EUBULIDES]`:

```text
Eubulides of Miletus, a 45-year-old philosopher, about 24 pixels tall, brown hair and a short beard, a sly half smile, a knee-length chiton under a dark grey cloak lined with dark olive green, leather sandals, a small round golden bronze hand mirror held at the hip,
```

- **B-128** · `eubulides_idle_se.png` · `idle` · 2 quadros (tira 64×32) · 600 ms por quadro, em loop · `criar`
  ```text
  [EUBULIDES] facing south in front view, the bronze mirror held at the hip, [CHARACTER] [STYLE_CHAR]
  ```
  **Quadros:** Quadro 1 = a rotação SE do personagem criado a partir do sprite sul deste prompt (§3.3, `criar`). Quadro 2 à mão: suba 1 px as linhas da cabeça, dos ombros e do peito, com pés e sombra idênticos.
- **B-129** · `eubulides_idle_ne.png` · `idle` · 2 quadros (tira 64×32) · 600 ms por quadro, em loop · `rotação + mão`
  ```text
  [EDIT] Frame 1 is the north-east rotation of the character created for eubulides_idle_se.png, with zero extra generations. Frame 2 copies frame 1 with the head, shoulder and chest rows moved up 1 px, while the feet and the shadow stay identical.
  ```
- **B-130** · `eubulides_walk_se.png` · `walk` · 4 quadros (tira 128×32) · 150 ms por quadro, em loop · `template walk + mão`
  ```text
  Facing south-east in 3/4 front view: walking with light sly steps.
  ```
  **Seleção:** Template `walking-4-frames`, `directions` = ["south-east"], este texto em `action_description`. Confira os pés plantados na linha 29; depois, à mão, suba 1 px a cabeça e o tronco nos dois quadros de passagem (pernas juntas).
- **B-131** · `eubulides_walk_ne.png` · `walk` · 4 quadros (tira 128×32) · 150 ms por quadro, em loop · `template walk + mão`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: walking with light sly steps.
  ```
  **Seleção:** Template `walking-4-frames`, `directions` = ["north-east"], este texto em `action_description`. Confira os pés plantados na linha 29; depois, à mão, suba 1 px a cabeça e o tronco nos dois quadros de passagem (pernas juntas).
- **B-132** · `eubulides_talk_se.png` · `talk` · 3 quadros (tira 96×32) · 200 ms por quadro, em loop · `animar loop`
  ```text
  Facing south-east in 3/4 front view: talks in place, the mouth opening and closing while the mirror hand tilts, then settles back to the idle pose.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle SE. Fique com 3, em ordem: quadro 1 da idle (boca fechada) · boca aberta com o gesto no auge · boca entreaberta, mão voltando. Descarte o último quadro gerado se ele repetir o primeiro.
- **B-133** · `eubulides_talk_ne.png` · `talk` · 3 quadros (tira 96×32) · 200 ms por quadro, em loop · `animar loop`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: talks in place with small nods of the head while the mirror hand tilts, then settles back to the idle pose.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle NE. Fique com 3, em ordem: quadro 1 da idle · aceno de cabeça com o gesto no auge · mão voltando. Descarte o último quadro gerado se ele repetir o primeiro.
- **B-134** · `eubulides_argue_se.png` · `argue` · 3 quadros (tira 96×32) · 200 ms por quadro, sem loop · `animar`
  ```text
  Facing south-east in 3/4 front view: presents an argument with a signature gesture: lifts the bronze mirror, turns it toward the listener, then grins from behind it.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle SE, sem `last_frame`. Fique com 3: início do gesto · auge do gesto · pose final segurada (a assinatura do prompt).
- **B-135** · `eubulides_argue_ne.png` · `argue` · 3 quadros (tira 96×32) · 200 ms por quadro, sem loop · `animar`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: presents an argument with a signature gesture: lifts the bronze mirror and turns it toward the listener, holding it out.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle NE, sem `last_frame`. Fique com 3: início do gesto · auge do gesto · pose final segurada (a assinatura do prompt).
- **B-136** · `eubulides_concede_se.png` · `concede` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing south-east in 3/4 front view: shifts into a held reluctant concession: the shoulders lowered, the head slightly down, one palm turned up.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle SE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: ombros baixos, cabeça baixa, uma palma para cima.
- **B-137** · `eubulides_concede_ne.png` · `concede` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: shifts into a held reluctant concession: the shoulders lowered, the head slightly down, one palm turned up.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle NE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: ombros baixos, cabeça baixa, uma palma para cima.
- **B-138** · `eubulides_coherence_damage_se.png` · `coherence_damage` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing south-east in 3/4 front view: shifts into a held pose, struck by an argument: flinching with the eyes shut, the head and upper body leaning back, one hand raised defensively, the feet planted on the ground line.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle SE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: recuo com os olhos fechados, uma mão erguida, pés plantados.
- **B-139** · `eubulides_coherence_damage_ne.png` · `coherence_damage` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: shifts into a held pose, struck by an argument: the head jerks back and the shoulders hunch, one hand raised defensively, the feet planted on the ground line.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle NE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: cabeça para trás, ombros encolhidos, uma mão erguida, pés plantados.
- **B-140** · `eubulides_defeat_se.png` · `defeat` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing south-east in 3/4 front view: shifts into a held pose, defeated: kneeling on one knee, the head bowed, the arms hanging.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle SE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: um joelho no chão, cabeça baixa, braços caídos.
- **B-141** · `eubulides_defeat_ne.png` · `defeat` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: shifts into a held pose, defeated: kneeling on one knee, the head bowed, the arms hanging.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle NE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: um joelho no chão, cabeça baixa, braços caídos.
- **B-142** · `portrait_eubulides_neutral.png` · retrato 32×32 · `retrato`
  ```text
  [PORTRAIT] Source: frame 1 of eubulides_idle_se.png. Check: sly half smile.
  ```
- **B-143** · `portrait_eubulides_shaken.png` · retrato 32×32 · `expressão`
  ```text
  [FACE] Source: portrait_eubulides_neutral.png, mask on the eyes, brows and mouth. Description: eyes narrowed, mouth set in a flat line.
  ```

#### Aglaofonte · `aglaophon` · DUELISTA

- **Altura no canvas:** 24 px.
- **Onde fica:** Ato II.
- **Direções:** SE e NE; view fixada no piloto (§3.2), a mesma para o elenco inteiro.
- **Arquivos:** 14 tiras (30 quadros) e 2 retratos, em P2.
- **Chave de cor:** outline, outline_blue, skin_light, skin_mid, skin_shade, hair_dark, hair_mid, ochre_light, ochre_mid, ochre_shade (túnica cor de areia), ochre_dark (saco), marble_mid (cinto) — 12 cores.
- **Silhueta e atuação:** Sem ficha no GDD (proposta). Túnica cor de areia, deixa areia escorrer da mão fechada: o Sorites.

Descritor `[AGLAOPHON]`:

```text
Aglaophon, a 30-year-old philosopher, about 24 pixels tall, sun-tanned, short black hair, a knee-length sand-colored tunic, bare legs, leather sandals, a small leather sand bag at the belt, the right fist closed around a handful of sand, calm and curious,
```

- **B-144** · `aglaophon_idle_se.png` · `idle` · 2 quadros (tira 64×32) · 600 ms por quadro, em loop · `criar`
  ```text
  [AGLAOPHON] facing south in front view, calm, the right fist closed around the sand, [CHARACTER] [STYLE_CHAR]
  ```
  **Quadros:** Quadro 1 = a rotação SE do personagem criado a partir do sprite sul deste prompt (§3.3, `criar`). Quadro 2 à mão: suba 1 px as linhas da cabeça, dos ombros e do peito, com pés e sombra idênticos.
- **B-145** · `aglaophon_idle_ne.png` · `idle` · 2 quadros (tira 64×32) · 600 ms por quadro, em loop · `rotação + mão`
  ```text
  [EDIT] Frame 1 is the north-east rotation of the character created for aglaophon_idle_se.png, with zero extra generations. Frame 2 copies frame 1 with the head, shoulder and chest rows moved up 1 px, while the feet and the shadow stay identical.
  ```
- **B-146** · `aglaophon_walk_se.png` · `walk` · 4 quadros (tira 128×32) · 150 ms por quadro, em loop · `template walk + mão`
  ```text
  Facing south-east in 3/4 front view: walking with relaxed steps.
  ```
  **Seleção:** Template `walking-4-frames`, `directions` = ["south-east"], este texto em `action_description`. Confira os pés plantados na linha 29; depois, à mão, suba 1 px a cabeça e o tronco nos dois quadros de passagem (pernas juntas).
- **B-147** · `aglaophon_walk_ne.png` · `walk` · 4 quadros (tira 128×32) · 150 ms por quadro, em loop · `template walk + mão`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: walking with relaxed steps.
  ```
  **Seleção:** Template `walking-4-frames`, `directions` = ["north-east"], este texto em `action_description`. Confira os pés plantados na linha 29; depois, à mão, suba 1 px a cabeça e o tronco nos dois quadros de passagem (pernas juntas).
- **B-148** · `aglaophon_talk_se.png` · `talk` · 3 quadros (tira 96×32) · 200 ms por quadro, em loop · `animar loop`
  ```text
  Facing south-east in 3/4 front view: talks in place, the mouth opening and closing while the closed fist lifts slightly, then settles back to the idle pose.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle SE. Fique com 3, em ordem: quadro 1 da idle (boca fechada) · boca aberta com o gesto no auge · boca entreaberta, mão voltando. Descarte o último quadro gerado se ele repetir o primeiro.
- **B-149** · `aglaophon_talk_ne.png` · `talk` · 3 quadros (tira 96×32) · 200 ms por quadro, em loop · `animar loop`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: talks in place with small nods of the head while the closed fist lifts slightly, then settles back to the idle pose.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle NE. Fique com 3, em ordem: quadro 1 da idle · aceno de cabeça com o gesto no auge · mão voltando. Descarte o último quadro gerado se ele repetir o primeiro.
- **B-150** · `aglaophon_argue_se.png` · `argue` · 3 quadros (tira 96×32) · 200 ms por quadro, sem loop · `animar`
  ```text
  Facing south-east in 3/4 front view: presents an argument with a signature gesture: raises the closed fist, opens it slightly so a thin stream of sand falls, then looks at the tiny heap as the last grains fall.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle SE, sem `last_frame`. Fique com 3: início do gesto · auge do gesto · pose final segurada (a assinatura do prompt).
- **B-151** · `aglaophon_argue_ne.png` · `argue` · 3 quadros (tira 96×32) · 200 ms por quadro, sem loop · `animar`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: presents an argument with a signature gesture: raises the closed fist and opens it slightly so a thin stream of sand falls into a tiny heap.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle NE, sem `last_frame`. Fique com 3: início do gesto · auge do gesto · pose final segurada (a assinatura do prompt).
- **B-152** · `aglaophon_concede_se.png` · `concede` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing south-east in 3/4 front view: shifts into a held reluctant concession: the shoulders lowered, the head slightly down, one palm turned up.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle SE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: ombros baixos, cabeça baixa, uma palma para cima.
- **B-153** · `aglaophon_concede_ne.png` · `concede` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: shifts into a held reluctant concession: the shoulders lowered, the head slightly down, one palm turned up.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle NE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: ombros baixos, cabeça baixa, uma palma para cima.
- **B-154** · `aglaophon_coherence_damage_se.png` · `coherence_damage` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing south-east in 3/4 front view: shifts into a held pose, struck by an argument: flinching with the eyes shut, the head and upper body leaning back, one hand raised defensively, the feet planted on the ground line.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle SE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: recuo com os olhos fechados, uma mão erguida, pés plantados.
- **B-155** · `aglaophon_coherence_damage_ne.png` · `coherence_damage` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: shifts into a held pose, struck by an argument: the head jerks back and the shoulders hunch, one hand raised defensively, the feet planted on the ground line.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle NE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: cabeça para trás, ombros encolhidos, uma mão erguida, pés plantados.
- **B-156** · `aglaophon_defeat_se.png` · `defeat` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing south-east in 3/4 front view: shifts into a held pose, defeated: kneeling on one knee, the head bowed, the arms hanging.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle SE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: um joelho no chão, cabeça baixa, braços caídos.
- **B-157** · `aglaophon_defeat_ne.png` · `defeat` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: shifts into a held pose, defeated: kneeling on one knee, the head bowed, the arms hanging.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle NE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: um joelho no chão, cabeça baixa, braços caídos.
- **B-158** · `portrait_aglaophon_neutral.png` · retrato 32×32 · `retrato`
  ```text
  [PORTRAIT] Source: frame 1 of aglaophon_idle_se.png. Check: calm curious look.
  ```
- **B-159** · `portrait_aglaophon_shaken.png` · retrato 32×32 · `expressão`
  ```text
  [FACE] Source: portrait_aglaophon_neutral.png, mask on the eyes, brows and mouth. Description: startled, eyes wide, brows raised, mouth open.
  ```

#### Heráclito (espectro) · `heraclitus` · ESPECTRO

- **Altura no canvas:** 25 px, flutuando 2 px acima do chão.
- **Onde fica:** Ato II (duelo duplo).
- **Direções:** SE e NE; view fixada no piloto (§3.2), a mesma para o elenco inteiro.
- **Arquivos:** 14 tiras (26 quadros) e 2 retratos, em P2.
- **Chave de cor:** outline, outline_blue, skin_mid, skin_shade, marble_light, marble_mid, marble_shade, marble_dark (barba e manto), red_light, red_mid, gold_light, gold_mid, ochre_light (chama) — 13 cores.
- **Silhueta e atuação:** Barba desgrenhada e chama na palma. A metade de baixo se dissolve em xadrez de 50% com transparente (desenhado à mão, alfa binário). Flutua: os pés ficam 2 px acima da linha de base; a sombra no chão continua nas linhas 30 e 31.

Descritor `[HERACLITUS]`:

```text
the ghost of Heraclitus, an old philosopher, about 25 pixels tall, floating just above the ground, a wild untamed grey beard and hair, a pale grey robe whose lower half dissolves into the air, a small coral-red and golden-yellow flame burning in his raised right palm, an intense stare,
```

- **B-160** · `heraclitus_idle_se.png` · `idle` · 2 quadros (tira 64×32) · 600 ms por quadro, em loop · `criar`
  ```text
  [HERACLITUS] facing south in front view, floating just above the ground, the flame burning in the raised right palm, [CHARACTER] [STYLE_CHAR]
  ```
  **Quadros:** Quadro 1 = a rotação SE do personagem criado a partir do sprite sul deste prompt (§3.3, `criar`). Quadro 2 à mão: suba a figura inteira 1 px e desloque 1 px para o lado a barra que se dissolve, com a sombra idêntica.
- **B-161** · `heraclitus_idle_ne.png` · `idle` · 2 quadros (tira 64×32) · 600 ms por quadro, em loop · `rotação + mão`
  ```text
  [EDIT] Frame 1 is the north-east rotation of the character created for heraclitus_idle_se.png, with zero extra generations. Frame 2 copies frame 1 with the whole figure moved up 1 px and the dissolving hem shifted 1 px sideways, while the shadow stays identical.
  ```
- **B-162** · `heraclitus_talk_se.png` · `talk` · 3 quadros (tira 96×32) · 200 ms por quadro, em loop · `animar loop`
  ```text
  Facing south-east in 3/4 front view: talks in place, the mouth opening and closing while the flame hand lifts, then settles back to the idle pose, still floating just above the ground.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle SE. Fique com 3, em ordem: quadro 1 da idle (boca fechada) · boca aberta com o gesto no auge · boca entreaberta, mão voltando. Descarte o último quadro gerado se ele repetir o primeiro.
- **B-163** · `heraclitus_talk_ne.png` · `talk` · 3 quadros (tira 96×32) · 200 ms por quadro, em loop · `animar loop`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: talks in place with small nods of the head while the flame hand lifts, then settles back to the idle pose, still floating just above the ground.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle NE. Fique com 3, em ordem: quadro 1 da idle · aceno de cabeça com o gesto no auge · mão voltando. Descarte o último quadro gerado se ele repetir o primeiro.
- **B-164** · `heraclitus_argue_se.png` · `argue` · 3 quadros (tira 96×32) · 200 ms por quadro, sem loop · `animar`
  ```text
  Facing south-east in 3/4 front view: presents an argument with a signature gesture: the flame in his palm flickers, he thrusts the flaming palm forward, then the flame flares higher.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle SE, sem `last_frame`. Fique com 3: início do gesto · auge do gesto · pose final segurada (a assinatura do prompt).
- **B-165** · `heraclitus_argue_ne.png` · `argue` · 3 quadros (tira 96×32) · 200 ms por quadro, sem loop · `animar`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: presents an argument with a signature gesture: the flame in his palm flickers, he thrusts the flaming palm forward, then the flame flares higher.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle NE, sem `last_frame`. Fique com 3: início do gesto · auge do gesto · pose final segurada (a assinatura do prompt).
- **B-166** · `heraclitus_concede_se.png` · `concede` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing south-east in 3/4 front view: shifts into a held reluctant concession: the head lowered, the free palm turned up, still floating.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle SE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: cabeça baixa, palma para cima, ainda flutuando.
- **B-167** · `heraclitus_concede_ne.png` · `concede` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: shifts into a held reluctant concession: the head lowered, the free palm turned up, still floating.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle NE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: cabeça baixa, palma para cima, ainda flutuando.
- **B-168** · `heraclitus_coherence_damage_se.png` · `coherence_damage` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing south-east in 3/4 front view: shifts into a held pose, struck by an argument: the figure recoils with the eyes shut and the dissolving lower half flickers wider, still floating.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle SE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: recuo com os olhos fechados, metade de baixo mais larga.
- **B-169** · `heraclitus_coherence_damage_ne.png` · `coherence_damage` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: shifts into a held pose, struck by an argument: the figure recoils with the head thrown back and the dissolving lower half flickers wider, still floating.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle NE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: recuo com a cabeça para trás, metade de baixo mais larga.
- **B-170** · `heraclitus_defeat_se.png` · `defeat` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing south-east in 3/4 front view: shifts into a held pose, defeated: the figure sinks until it almost touches the ground, the head bowed and the arms hanging, fading.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle SE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: a figura desce até 1 px do chão, cabeça baixa, braços caídos.
- **B-171** · `heraclitus_defeat_ne.png` · `defeat` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: shifts into a held pose, defeated: the figure sinks until it almost touches the ground, the head bowed and the arms hanging, fading.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle NE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: a figura desce até 1 px do chão, cabeça baixa, braços caídos.
- **B-172** · `heraclitus_float_se.png` · `float` · 2 quadros (tira 64×32) · 300 ms por quadro, em loop · `animar loop`
  ```text
  Facing south-east in 3/4 front view: drifts forward instead of walking, leaning slightly forward while floating just above the ground, the dissolving robe hem trailing behind, then settles back to the idle pose.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle SE. Fique com 2, em ordem: quadro 1 da idle · figura inclinada, 1 px mais alta, com a barra do manto para trás. Descarte o último quadro gerado se ele repetir o primeiro.
- **B-173** · `heraclitus_float_ne.png` · `float` · 2 quadros (tira 64×32) · 300 ms por quadro, em loop · `animar loop`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: drifts forward instead of walking, leaning slightly forward while floating just above the ground, the dissolving robe hem trailing behind, then settles back to the idle pose.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle NE. Fique com 2, em ordem: quadro 1 da idle · figura inclinada, 1 px mais alta, com a barra do manto para trás. Descarte o último quadro gerado se ele repetir o primeiro.
- **B-174** · `portrait_heraclitus_neutral.png` · retrato 32×32 · `retrato`
  ```text
  [PORTRAIT] Source: frame 1 of heraclitus_idle_se.png. Check: intense burning stare.
  ```
- **B-175** · `portrait_heraclitus_shaken.png` · retrato 32×32 · `expressão`
  ```text
  [FACE] Source: portrait_heraclitus_neutral.png, mask on the eyes, brows and mouth. Description: furious scowl, brows drawn down hard, teeth bared.
  ```

#### Parmênides (espectro) · `parmenides` · ESPECTRO

- **Altura no canvas:** 25 px, flutuando 2 px acima do chão.
- **Onde fica:** Ato II (duelo duplo).
- **Direções:** SE e NE; view fixada no piloto (§3.2), a mesma para o elenco inteiro.
- **Arquivos:** 14 tiras (26 quadros) e 2 retratos, em P2.
- **Chave de cor:** outline, outline_blue, skin_light, skin_mid, skin_shade, marble_light, marble_mid, marble_shade, marble_dark (barba e manto), aegean_light, aegean_mid, aegean_shade (esfera e brilho espectral) — 12 cores.
- **Silhueta e atuação:** Barba longa e composta, imóvel, esfera de mármore nas mãos. Mesmo xadrez de 50% do Heráclito na metade de baixo.

Descritor `[PARMENIDES]`:

```text
the ghost of Parmenides, an old philosopher, about 25 pixels tall, floating just above the ground, a long neatly combed white beard, a pale off-white robe whose lower half dissolves into the air, a smooth pale marble sphere with a faint sea-blue sheen held at chest height, perfectly still and serene,
```

- **B-176** · `parmenides_idle_se.png` · `idle` · 2 quadros (tira 64×32) · 600 ms por quadro, em loop · `criar`
  ```text
  [PARMENIDES] facing south in front view, floating just above the ground, perfectly still, the marble sphere at chest height, [CHARACTER] [STYLE_CHAR]
  ```
  **Quadros:** Quadro 1 = a rotação SE do personagem criado a partir do sprite sul deste prompt (§3.3, `criar`). Quadro 2 à mão: suba a figura inteira 1 px e desloque 1 px para o lado a barra que se dissolve, com a sombra idêntica.
- **B-177** · `parmenides_idle_ne.png` · `idle` · 2 quadros (tira 64×32) · 600 ms por quadro, em loop · `rotação + mão`
  ```text
  [EDIT] Frame 1 is the north-east rotation of the character created for parmenides_idle_se.png, with zero extra generations. Frame 2 copies frame 1 with the whole figure moved up 1 px and the dissolving hem shifted 1 px sideways, while the shadow stays identical.
  ```
- **B-178** · `parmenides_talk_se.png` · `talk` · 3 quadros (tira 96×32) · 200 ms por quadro, em loop · `animar loop`
  ```text
  Facing south-east in 3/4 front view: talks in place while only the mouth moves and the sphere stays still at chest height, then settles back to the idle pose, still floating just above the ground.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle SE. Fique com 3, em ordem: quadro 1 da idle (boca fechada) · boca aberta com o gesto no auge · boca entreaberta, mão voltando. Descarte o último quadro gerado se ele repetir o primeiro.
- **B-179** · `parmenides_talk_ne.png` · `talk` · 3 quadros (tira 96×32) · 200 ms por quadro, em loop · `animar loop`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: talks with a single slow nod of the head while the sphere stays still at chest height, then settles back to the idle pose, still floating just above the ground.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle NE. Fique com 3, em ordem: quadro 1 da idle · aceno de cabeça com o gesto no auge · mão voltando. Descarte o último quadro gerado se ele repetir o primeiro.
- **B-180** · `parmenides_argue_se.png` · `argue` · 3 quadros (tira 96×32) · 200 ms por quadro, sem loop · `animar`
  ```text
  Facing south-east in 3/4 front view: presents an argument with a signature gesture: holds the marble sphere still, closes the eyes, then the sphere rises slightly while the face stays calm and unmoved.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle SE, sem `last_frame`. Fique com 3: início do gesto · auge do gesto · pose final segurada (a assinatura do prompt).
- **B-181** · `parmenides_argue_ne.png` · `argue` · 3 quadros (tira 96×32) · 200 ms por quadro, sem loop · `animar`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: presents an argument with a signature gesture: holds the marble sphere still at chest height, then the sphere rises slightly while the body stays calm and unmoved.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle NE, sem `last_frame`. Fique com 3: início do gesto · auge do gesto · pose final segurada (a assinatura do prompt).
- **B-182** · `parmenides_concede_se.png` · `concede` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing south-east in 3/4 front view: shifts into a held reluctant concession: the head lowered, the free palm turned up, still floating.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle SE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: cabeça baixa, palma para cima, ainda flutuando.
- **B-183** · `parmenides_concede_ne.png` · `concede` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: shifts into a held reluctant concession: the head lowered, the free palm turned up, still floating.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle NE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: cabeça baixa, palma para cima, ainda flutuando.
- **B-184** · `parmenides_coherence_damage_se.png` · `coherence_damage` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing south-east in 3/4 front view: shifts into a held pose, struck by an argument: the figure recoils with the eyes shut and the dissolving lower half flickers wider, still floating.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle SE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: recuo com os olhos fechados, metade de baixo mais larga.
- **B-185** · `parmenides_coherence_damage_ne.png` · `coherence_damage` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: shifts into a held pose, struck by an argument: the figure recoils with the head thrown back and the dissolving lower half flickers wider, still floating.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle NE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: recuo com a cabeça para trás, metade de baixo mais larga.
- **B-186** · `parmenides_defeat_se.png` · `defeat` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing south-east in 3/4 front view: shifts into a held pose, defeated: the figure sinks until it almost touches the ground, the head bowed and the arms hanging, fading.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle SE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: a figura desce até 1 px do chão, cabeça baixa, braços caídos.
- **B-187** · `parmenides_defeat_ne.png` · `defeat` · 1 quadro (tira 32×32) · pose de 1 quadro · `pose`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: shifts into a held pose, defeated: the figure sinks until it almost touches the ground, the head bowed and the arms hanging, fading.
  ```
  **Seleção:** `first_frame` = quadro 1 da idle NE, sem `last_frame`. Fique só com o quadro-chave, em geral o último: a figura desce até 1 px do chão, cabeça baixa, braços caídos.
- **B-188** · `parmenides_float_se.png` · `float` · 2 quadros (tira 64×32) · 300 ms por quadro, em loop · `animar loop`
  ```text
  Facing south-east in 3/4 front view: drifts forward instead of walking, leaning slightly forward while floating just above the ground, the dissolving robe hem trailing behind, then settles back to the idle pose.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle SE. Fique com 2, em ordem: quadro 1 da idle · figura inclinada, 1 px mais alta, com a barra do manto para trás. Descarte o último quadro gerado se ele repetir o primeiro.
- **B-189** · `parmenides_float_ne.png` · `float` · 2 quadros (tira 64×32) · 300 ms por quadro, em loop · `animar loop`
  ```text
  Facing north-east in 3/4 back view, back turned to the viewer, face hidden: drifts forward instead of walking, leaning slightly forward while floating just above the ground, the dissolving robe hem trailing behind, then settles back to the idle pose.
  ```
  **Seleção:** `first_frame` = `last_frame` = quadro 1 da idle NE. Fique com 2, em ordem: quadro 1 da idle · figura inclinada, 1 px mais alta, com a barra do manto para trás. Descarte o último quadro gerado se ele repetir o primeiro.
- **B-190** · `portrait_parmenides_neutral.png` · retrato 32×32 · `retrato`
  ```text
  [PORTRAIT] Source: frame 1 of parmenides_idle_se.png. Check: serene closed-mouth calm.
  ```
- **B-191** · `portrait_parmenides_shaken.png` · retrato 32×32 · `expressão`
  ```text
  [FACE] Source: portrait_parmenides_neutral.png, mask on the eyes, brows and mouth. Description: a single crease between the brows, the rest of the face calm and still.
  ```

#### Ágora (`agora.json`)

- **B-192** · `wall_marble_tall_door_left.png` · 32×48 · `Aseprite (edição)` — Porta na face esquerda: vai na parede de trás ao longo da borda NE da sala (coluna de X máximo). É bloco (intransponível): porta decorativa ou saída fora do mapa (§11).
  ```text
  [EDIT] From the approved wall_marble_tall.png, zero generations: cut a doorway into the left face, about 8 px wide and 20 px tall, centred on the face and drawn as a parallelogram that follows the 2:1 slope of the face; fill it with outline and give it a 1-px marble_dark lintel and jambs. The top, the other face and the outline stay identical.
  ```
- **B-193** · `wall_marble_tall_door_right.png` · 32×48 · `Aseprite (edição)` — Porta na face direita: vai na parede de trás ao longo da borda NO da sala (linha de Y máximo). É bloco (intransponível): porta decorativa ou saída fora do mapa (§11).
  ```text
  [EDIT] From the approved wall_marble_tall.png, zero generations: cut a doorway into the right face, about 8 px wide and 20 px tall, centred on the face and drawn as a parallelogram that follows the 2:1 slope of the face; fill it with outline and give it a 1-px marble_dark lintel and jambs. The top, the other face and the outline stay identical.
  ```

#### Academia (mapa ainda não existe)

- **B-194** · `floor_diagram_circle.png` · 32×16 · `iso-tile thin` — `TerrainId.Diagram`, variante do círculo (as 3 variantes são círculo, quadrado e triângulo).
  ```text
  Marble floor tile with a geometric circle scratched into it with a compass, thin cool dark grey lines drawn in isometric perspective, [FLOOR] [STYLE_ENV]
  ```
- **B-195** · `floor_diagram_square.png` · 32×16 · `iso-tile thin` — `TerrainId.Diagram`, variante do quadrado.
  ```text
  Marble floor tile with a geometric square and its diagonal scratched into it, thin cool dark grey lines drawn in isometric perspective, [FLOOR] [STYLE_ENV]
  ```
- **B-196** · `floor_diagram_triangle.png` · 32×16 · `iso-tile thin` — `TerrainId.Diagram`, variante do triângulo.
  ```text
  Marble floor tile with a geometric triangle scratched into it, thin cool dark grey lines drawn in isometric perspective, [FLOOR] [STYLE_ENV]
  ```
- **B-197** · `floor_garden_grass.png` · 32×16 · `iso-tile thin` — Use `floor_grass` aprovado como imagem de estilo.
  ```text
  Well-kept garden grass of the Academy, olive green with 2-pixel yellow-green tufts, slightly lighter and tidier than the Agora grass, [FLOOR] [STYLE_ENV]
  ```
- **B-198** · `floor_gravel_path.png` · 32×16 · `iso-tile thin`
  ```text
  Light gravel path, pale grey-beige base with dense 1-pixel off-white and light grey gravel, [FLOOR] [STYLE_ENV]
  ```
- **B-199** · `floor_marble_blue.png` · 32×16 · `iso-tile thin`
  ```text
  Polished white marble floor with a curved sea-blue vein, for the school interiors, [FLOOR] [STYLE_ENV]
  ```
- **B-200** · `block_portico.png` · 32×48 · `bitforge+init` — Contorno `outline_blue`. `init_image` = a silhueta da `block_doric_column`, recortada para 32×48.
  ```text
  An Ionic marble column on one cell with volutes on the capital and a round base, off-white marble with cool blue-grey shadows, slimmer and more ornate than a Doric column, [BLOCK] [STYLE_ENV]
  ```
- **B-201** · `block_scroll_shelf.png` · 32×40 · `bitforge+init` — Também serve ao arquivo do tribunal (I.9). Sem placeholder: silhueta à mão.
  ```text
  A wooden wall shelf block with square niches full of rolled papyrus scrolls, warm orange-ochre wood and off-white scrolls, [BLOCK] [STYLE_ENV]
  ```
- **B-202** · `prop_ancient_olive_tree.png` · 48×64 · `bitforge+init` — 48×64 exige pivot próprio no importer (§11). `style_image` = `prop_olive_trunk` aprovado.
  ```text
  An ancient, larger and more twisted olive tree with a wide three-tone olive-green canopy, the trunk footprint inside one cell and the canopy overflowing it, [PROP] [STYLE_ENV]
  ```

#### Ícones de item e habilidade (16×16)

- **B-203** · `icon_ability_inversion.png` · 16×16 · `ícone de item` — Habilidade Inversão (ramo Aceitar).
  ```text
  Two curved golden-yellow arrows chasing each other in a circle, [ITEM] [STYLE_ENV]
  ```
  **Primeiro ícone:** `create-image-pixen` em 16×16; depois de aprovado, ele é a `style_images` dos outros ícones de item (`generate-with-style-v2`).
- **B-204** · `icon_ability_listening.png` · 16×16 · `ícone de item` — Habilidade Escuta (ramo Recusar).
  ```text
  A skin-toned ear with three small curved off-white sound waves beside it, [ITEM] [STYLE_ENV]
  ```
- **B-205** · `icon_item_bronze_seal.png` · 16×16 · `ícone de item` — O convite para a Academia.
  ```text
  A round golden bronze seal stamped with a small dark bronze owl, [ITEM] [STYLE_ENV]
  ```
- **B-206** · `icon_quote_heraclitus.png` · 16×16 · `ícone de item` — Citação de Heráclito.
  ```text
  A rolled off-white papyrus scroll tied with a golden-yellow ribbon and sealed with a small coral-red flame, [ITEM] [STYLE_ENV]
  ```
- **B-207** · `icon_quote_homer.png` · 16×16 · `ícone de item` — Citação de Homero.
  ```text
  A rolled off-white papyrus scroll tied with a golden-yellow ribbon and sealed with a small golden-yellow lyre, [ITEM] [STYLE_ENV]
  ```
- **B-208** · `icon_quote_parmenides.png` · 16×16 · `ícone de item` — Citação de Parmênides.
  ```text
  A rolled off-white papyrus scroll tied with a golden-yellow ribbon and sealed with a small light grey sphere, [ITEM] [STYLE_ENV]
  ```

### 10.3 P3 — Tribunal e acabamento

36 arquivos (B-209 a B-244), 58 quadros.

#### A aluna (finais) · `student` · NPC sentada

- **Altura no canvas:** cerca de 15 px sentada.
- **Onde fica:** Cerâmico (final da Mestra) e escola do final do Sofista.
- **Direções:** só SE; view fixada no piloto (§3.2), a mesma para o elenco inteiro.
- **Arquivos:** 3 tiras (7 quadros) e 2 retratos, em P3.
- **Chave de cor:** outline, outline_blue, skin_light, skin_mid, skin_shade, hair_dark, hair_mid, marble_mid, marble_shade (quíton simples), ochre_light, ochre_mid, ochre_shade (ânfora; figuras vermelhas em ochre_mid sobre faixa hair_dark) — 12 cores.
- **Silhueta e atuação:** Sem ficha no GDD (proposta). Menina de uns 10 anos, sentada no chão de pernas cruzadas, pintando uma ânfora pequena: é o eco da Kleia do I.1. Só SE.

Descritor `[STUDENT]`:

```text
a 10-year-old Athenian girl, about 15 pixels tall while seated, dark hair tied back, a plain pale grey-beige chiton, bare feet, a small orange-ochre amphora with near-black bands in front of her,
```

- **B-209** · `student_idle_se.png` · `idle` · 2 quadros (tira 64×32) · 600 ms por quadro, em loop · `sentado: base`
  ```text
  [STUDENT] sitting cross-legged on the ground facing south-east in 3/4 front view, holding a thin brush over the small amphora, [CHARACTER] [STYLE_CHAR]
  ```
  **Quadros:** Quadro 1 = a base sentada SE gerada com este prompt (§3.3, `sentado: base`). Quadro 2 à mão: incline a cabeça 1 px, com corpo, ânfora e sombra idênticos.
- **B-210** · `student_talk_se.png` · `talk` · 3 quadros (tira 96×32) · 200 ms por quadro, em loop · `mão (Aseprite)`
  ```text
  [EDIT] Built from frame 1 of student_idle_se.png, zero generations: frame 1 is that frame with the mouth closed; frame 2 lifts the head 1 px, opens the mouth 1 px and raises the free hand 2 px; frame 3 half opens the mouth and returns the hand. The body, the amphora and the shadow stay identical.
  ```
- **B-211** · `student_paint_se.png` · `paint` · 2 quadros (tira 64×32) · 400 ms por quadro, em loop · `mão (Aseprite)`
  ```text
  [EDIT] Built from frame 1 of student_idle_se.png, zero generations: frame 1 is that frame; frame 2 moves the brush hand 1 px along the amphora. Every other pixel stays identical.
  ```
- **B-212** · `portrait_student_neutral.png` · retrato 32×32 · `retrato`
  ```text
  [PORTRAIT] Source: frame 1 of student_idle_se.png. Check: bright curious face, dark hair tied back.
  ```
- **B-213** · `portrait_student_shaken.png` · retrato 32×32 · `expressão`
  ```text
  [FACE] Source: portrait_student_neutral.png, mask on the eyes, brows and mouth. Description: wide worried eyes, brows raised, mouth slightly open.
  ```

#### Ágora (`agora.json`)

- **B-214** · `floor_water_ripple.png` · 32×16 · 4 quadros (tira 128×16) · `ciclo de paleta (Aseprite)` — 200 ms, loop. Precisa de suporte a tile animado.
  ```text
  [EDIT] 4 frames by palette cycling on floor_water.png, zero generations: frame 1 is identical to the static file; the ripple pixels of the surface step through aegean_shade, aegean_mid, aegean_light and aegean_mid, offset by one diagonal per frame, so the ripples appear to travel. The rim, the foam and the edge pixels stay identical, so the tile stays seamless.
  ```
- **B-215** · `floor_mosaic_straight.png` · 32×16 · `iso-tile thin` — Peça 1 de 4 do meandro do GDD. Serve as linhas 2 e 9 da praça; espelhada com `flipX`, serve as colunas 2 e 9. Exige suporte no `MapLayout` (símbolo por peça e espelho) antes de entrar no mapa.
  ```text
  White marble tile crossed by a sea-blue Greek key meander band about 6 pixels wide that enters through the middle of the lower-left edge and leaves through the middle of the upper-right edge, parallel to the other two edges, so a row of these tiles along the grid X axis forms one continuous border, both side corners of the diamond in the joint color, [FLOOR] [STYLE_ENV]
  ```
- **B-216** · `floor_mosaic_corner_back.png` · 32×16 · `iso-tile thin` — Peça 3 de 4: o canto do fundo, célula (9, 9).
  ```text
  White marble corner tile of the same sea-blue Greek key border, the band enters through the middle of the lower-left edge, turns near the top corner of the diamond and leaves through the middle of the lower-right edge, joining floor_mosaic_straight and its mirror seamlessly, [FLOOR] [STYLE_ENV]
  ```
- **B-217** · `floor_mosaic_corner_front.png` · 32×16 · `iso-tile thin` — Peça 2 de 4: o canto da frente da praça, célula (2, 2).
  ```text
  White marble corner tile of the same sea-blue Greek key border, the band enters through the middle of the upper-right edge, turns near the bottom corner of the diamond and leaves through the middle of the upper-left edge, joining floor_mosaic_straight and its mirror seamlessly, [FLOOR] [STYLE_ENV]
  ```
- **B-218** · `floor_mosaic_corner_side.png` · 32×16 · `iso-tile thin` — Peça 4 de 4: o canto direito, célula (9, 2); espelhada com `flipX`, é o canto esquerdo, célula (2, 9).
  ```text
  White marble corner tile of the same sea-blue Greek key border, the band enters through the middle of the lower-left edge, turns near the right corner of the diamond and leaves through the middle of the upper-left edge, joining floor_mosaic_straight and its mirror seamlessly, [FLOOR] [STYLE_ENV]
  ```
- **B-219** · `block_broken_column.png` · 32×28 · `bitforge+init` — `init_image` = `block_doric_column` aprovado, recortado.
  ```text
  The Doric marble column broken in half, an irregular broken top and a few fallen fragments at its foot, off-white marble with cool blue-grey shadows, [BLOCK] [STYLE_ENV]
  ```
- **B-220** · `prop_bema_stone.png` · 32×24 · `iso-tile thick` — Ambienta o Kritolaos (Pnyx) e serve de tribuna no Tribunal. `thick tile` em 32×32, cortado para 32×24.
  ```text
  A low light grey stone speaker's platform, the bema, with a single step, [BLOCK] [STYLE_ENV]
  ```
- **B-221** · `prop_fish_baskets.png` · 32×24 · `map-object+init` — Ambienta o Bátilo.
  ```text
  Two pale wicker baskets of silver fish and a coiled rope net, [PROP] [STYLE_ENV]
  ```
- **B-222** · `prop_fountain.png` · 32×36 · `bitforge+init` — Hoje a fonte do mapa é `floor_water`. Sem placeholder: silhueta à mão.
  ```text
  A small public fountain, an off-white marble basin with cool blue-grey shadows and a lion-head spout pouring sea-blue water, [PROP] [STYLE_ENV]
  ```
- **B-223** · `prop_fountain_flow.png` · 32×36 · 4 quadros (tira 128×36) · `ciclo de paleta (Aseprite)` — 200 ms, loop.
  ```text
  [EDIT] 4 frames by palette cycling on prop_fountain.png, zero generations: frame 1 is identical to the static file; the pixels of the water stream and the basin ripples step through aegean_light, aegean_mid, aegean_shade and aegean_mid, offset by one row per frame, so the stream appears to fall. The marble stays identical.
  ```
- **B-224** · `prop_mask_stand.png` · 32×32 · `map-object+init` — Ambienta o Simo.
  ```text
  A wooden stand holding two off-white theatre masks, one comic and one tragic, on warm orange-ochre wood, [PROP] [STYLE_ENV]
  ```
- **B-225** · `prop_money_table.png` · 32×24 · `map-object+init` — Ambienta o Óron (stoa).
  ```text
  A money changer's small wooden table with stacks of silver coins and a tiny golden bronze balance, [PROP] [STYLE_ENV]
  ```
- **B-226** · `prop_olive_tree.png` · 48×56 · `bitforge+init` — Versão do GDD (48×56). Exige pivot próprio no importer. `style_image` = `prop_olive_trunk` aprovado.
  ```text
  A full olive tree larger than its cell, a twisted trunk inside one cell and a wide three-tone olive-green canopy overflowing it, [PROP] [STYLE_ENV]
  ```
- **B-227** · `prop_olive_trunk_wind.png` · 32×40 · 3 quadros (tira 96×40) · `animar tile` — 400 ms, loop (GDD §6.2). `first_frame` = `last_frame` = o estático aprovado; fique com 3 quadros, o 1 igual ao estático.
  ```text
  Wind moves through the canopy: the leaves and their silvery undersides sway slightly and settle back, while the trunk stays still.
  ```
- **B-228** · `prop_small_altar.png` · 32×24 · `map-object+init` — Ambienta a Phaidra.
  ```text
  A small light grey stone altar with a laurel branch and scattered knucklebones on it, [PROP] [STYLE_ENV]
  ```

#### Tribunal (mapa ainda não existe)

- **B-229** · `floor_court_stone.png` · 32×16 · `iso-tile thin` — Tribunal (proposta; o GDD não especifica).
  ```text
  Worn grey court floor of large square stone slabs, pale grey-beige slabs with cool dark grey joints, cool blue-grey shadows, [FLOOR] [STYLE_ENV]
  ```
- **B-230** · `block_juror_bench.png` · 32×24 · `iso-tile thick` — `thick tile` em 32×32, cortado para 32×24.
  ```text
  A long empty wooden juror bench along the cell, warm orange-ochre seat and rust-brown legs, [BLOCK] [STYLE_ENV]
  ```
- **B-231** · `block_juror_bench_full.png` · 32×32 · `bitforge+init` — Os 501 jurados não são atores: ficam pintados no bloco. `init_image` = `block_juror_bench` aprovado com duas silhuetas à mão.
  ```text
  A wooden bench with two seated Athenian jurors in plain grey and brown cloaks, shoulder to shoulder, facing the viewer, [BLOCK] [STYLE_ENV]
  ```
- **B-232** · `wall_court.png` · 32×32 · `iso-tile block` — Alternativa: reaproveitar `wall_marble`.
  ```text
  Plain whitewashed stone wall block of the People's Court, a 16-pixel diamond top over 16 pixels of wall, off-white top, pale grey-beige left face, pale blue-grey right face, cool blue-grey shadows, [BLOCK] [STYLE_ENV]
  ```
- **B-233** · `wall_court_tall.png` · 32×48 · `Aseprite (edição)` — Parede alta do fundo da sala do tribunal.
  ```text
  [EDIT] From the approved wall_court.png, zero generations: keep the 16-px diamond top and extend both faces down to 32 px of wall, repeating the whitewash texture, same colors and outline_blue outline; the base diamond ends on the bottom row of the 32x48 canvas.
  ```
- **B-234** · `wall_court_tall_door_left.png` · 32×48 · `Aseprite (edição)` — Porta na face esquerda: vai na parede de trás ao longo da borda NE da sala (coluna de X máximo). É bloco (intransponível): porta decorativa ou saída fora do mapa (§11).
  ```text
  [EDIT] From the approved wall_court_tall.png, zero generations: cut a doorway into the left face, about 8 px wide and 20 px tall, centred on the face and drawn as a parallelogram that follows the 2:1 slope of the face; fill it with outline and give it a 1-px marble_dark lintel and jambs. The top, the other face and the outline stay identical.
  ```
- **B-235** · `wall_court_tall_door_right.png` · 32×48 · `Aseprite (edição)` — Porta na face direita: vai na parede de trás ao longo da borda NO da sala (linha de Y máximo). É bloco (intransponível): porta decorativa ou saída fora do mapa (§11).
  ```text
  [EDIT] From the approved wall_court_tall.png, zero generations: cut a doorway into the right face, about 8 px wide and 20 px tall, centred on the face and drawn as a parallelogram that follows the 2:1 slope of the face; fill it with outline and give it a 1-px marble_dark lintel and jambs. The top, the other face and the outline stay identical.
  ```
- **B-236** · `prop_klepsydra.png` · 32×32 · `map-object+init`
  ```text
  A klepsydra water clock, a terracotta-orange clay jar on a wooden stand with a small bronze spout dripping into a lower jar, [PROP] [STYLE_ENV]
  ```
- **B-237** · `prop_kleroterion.png` · 32×40 · `bitforge+init` — Opcional.
  ```text
  A kleroterion allotment machine, a tall light grey stone slab with rows of small slots and a golden bronze tube along its side, [PROP] [STYLE_ENV]
  ```
- **B-238** · `prop_voting_urns.png` · 32×24 · `map-object+init`
  ```text
  Two voting urns side by side, one of golden bronze and one of rust-brown wood, [PROP] [STYLE_ENV]
  ```
- **B-239** · `frame_jurors.png` · 204×20 · `manual (Aseprite)` — O `meter_jurors` é procedural; só a moldura é arte.
  ```text
  [MANUAL] 204x20 canvas. Frame around the 200x16 juror meter: a 1-pixel outline with a thin gold_dark inner line, transparent inside.
  ```

#### Interface

- **B-240** · `title_logo.png` · 160×48 · `manual (Aseprite)` — Texto em pixel não sai do PixelLab: letreiro à mão. A moldura grega pode vir de geração.
  ```text
  [MANUAL] 160x48 canvas. Game title logo 'GREEK FIGHT TACTICS' in chunky pixel capitals, marble_light letters with a gold meander underline and an outline, hand-lettered.
  ```

#### Efeitos

- **B-241** · `fx_audience.png` · 16×16 · 3 quadros (tira 48×16) · `pixen 16x16 + mão`
  ```text
  A cluster of small golden sparkles of crowd approval, bright yellow and golden yellow, [FX] [STYLE_ENV]
  ```
  **Seleção:** 3 quadros à mão: as faíscas acendem e apagam em ordem.
- **B-242** · `fx_maieutics.png` · 16×16 · 4 quadros (tira 64×16) · `pixen 16x16 + mão`
  ```text
  A small royal-purple question mark with a lilac highlight, [FX] [STYLE_ENV]
  ```
  **Seleção:** 4 quadros à mão: dois ou três pontos de interrogação sobem em espiral, um passo por quadro.
- **B-243** · `fx_quote.png` · 16×16 · 4 quadros (tira 64×16) · `pixen 16x16 + mão`
  ```text
  Tiny glowing abstract glyphs rising from a rolled scroll, off-white and bright yellow, [FX] [STYLE_ENV]
  ```
  **Seleção:** 4 quadros à mão: os sinais sobem 2 px por quadro e somem no topo.
- **B-244** · `fx_refute_hit.png` · 16×16 · 3 quadros (tira 48×16) · `pixen 16x16 + mão` — 80 ms por quadro.
  ```text
  A sharp star-shaped spark burst of an accusation landing, coral-red core with brick-red spikes, [FX] [STYLE_ENV]
  ```
  **Seleção:** 3 quadros à mão a partir do gerado: estrela pequena · explosão grande com as pontas · faíscas sumindo.

---

## 11. Notas de integração (lado do dev)

Nada disto está feito, e este documento não muda código. É a lista do que precisa mudar para a arte de 32×32 entrar no jogo sem quebrar os testes. O melhor é um card de código aberto antes de a primeira arte chegar.

### Célula e fatiamento

1. **`ArtTools.CellWidth`:** passa de 24 para 32, com a folha em 128×128. `CellHeight` (32) e `CharacterPivot` `(0,5; 0,0625)` continuam iguais, e os pés continuam 2 px acima da base.
2. **Tiras:** o `ArtTools` precisa ler as tiras `<personagem>_<animação>_<se|ne>.png` e fatiá-las em células de 32×32, nomeadas `<personagem>_<animação>_<dir>_<i>`, com o mesmo pivot. Hoje ele só conhece `<personagem>_sheet.png` com 4×4 células e 3 estados.
3. **`spriteID`:** preserve-o entre regenerações, como já é feito na folha.
4. **`PixelArtImporter`:** hoje põe `Multiple` só em `*_sheet.png`. As tiras precisam de `Multiple` explícito (por pasta ou por padrão de nome), senão o template importa com o pivot errado. Retratos e ícones continuam `Single`.
5. **Colisão de nome:** os placeholders `<personagem>_idle_se.png` (24×32, 1 quadro, sem uso no código) têm o mesmo nome da nova tira `idle`. Apague o placeholder quando a tira entrar.

### Animator

6. **`ArtTools.StateNames`:** passa de `idle`/`walk`/`action` para o catálogo da §8, com duração e loop por animação e o último quadro segurado em `defeat`, `victory` e nas poses de 1 quadro.
7. **Blend tree:** o `ActorView` já manda `dirX = 1` e resolve SW e NW com `flipX`, então basta um filho SE e um NE por estado. Hoje são 4.
8. **Estados por personagem:** cada personagem tem só os estados do seu conjunto (§8.2).
9. **Testes a ajustar em `ArtAssetsTests`:**
   - os 3 estados exatos;
   - os 4 filhos do blend tree;
   - `SpritesPerSheet = 16`;
   - `CellWidth`;
   - `PlaceholderPngCount = 59`.
10. **Curvas:** o teste que proíbe curvas numéricas continua valendo, porque bob e recuo estão desenhados.

### Cidadão modular

11. **Troca de manto por código:** um passo do `ArtTools` gera, na importação, as 8 variantes da tabela da §8.2 (`citizen_grey`, `citizen_elder_linen` e as outras), trocando exatamente as 3 cores da rampa de origem pelas 3 da rampa de destino em todas as tiras aprovadas. As rampas ficam num dado versionado (por exemplo `art/palette/citizen_cloaks.json`), não no código. Alternativa: um material de troca de paleta no `SpriteRenderer`, que dispensa textura nova mas exige shader.
12. **Teste:** um EditMode confere que, em cada figura de cidadão, as 3 cores do manto não aparecem fora do manto (senão a troca pinta barba ou véu), e que cada variante sai com o mesmo alfa da origem.
13. **Mapa:** o NPC escolhe a variante por um campo opcional no JSON do mapa (por exemplo `"variant": "citizen_elder_grey"`), validado pelo `MapLayout`.

### Batalha e diálogo

14. **Estados usados:** o `ActorView` só toca `idle` e `walk`. A reprodução da batalha precisa disparar os outros:
    - `refute`, `maieutics`, `quote`, `concede`, `withdraw` e `argue` a partir do `Outcome`;
    - `coherence_damage` no alvo;
    - `idle_breath` fora do turno;
    - `think` com o menu aberto;
    - `defeat` e `victory` no fim;
    - `talk` enquanto a caixa de diálogo digita.
15. **Flash de dano:** é Shader Graph no material do sprite (ART-07 #5), não arte.
16. **Retratos:** o `SandboxTools.PortraitFor` usa o quadro `<personagem>_se_0` da folha. Ele passa a carregar `portrait_<personagem>_neutral.png`.
17. **Expressão abalada:** a troca para `shaken` depois de dano, ou com Coerência baixa, exige código no diálogo e na batalha. `speakers.portrait` em `dialogues.json` continua sendo o id do personagem.

### Tiles, salas e props

18. **Máscara do losango:** o `FloorTileSeams` e o `BlockTileSeams` rodam sobre `art/placeholders/tiles/`. Se a arte final ganhar outra pasta de origem, aponte os dois para ela. O `FloorTileSeams` depende do pixel da ponta esquerda (linha 8) na cor do contorno.
19. **Paredes altas:** `wall_brick_tall`, `wall_marble_tall` e `wall_court_tall` (32×48) entram como símbolos novos na `blockLegend` dos mapas, só nas bordas de trás (a linha de Y máximo e a coluna de X máximo da sala). Na oficina do Cerâmico são a linha y = 11 e a coluna x = 7; a parede da frente (linha y = 4 e coluna x = 0) continua baixa ou sai. Recapture a referência e confira a ordenação do ator encostado na parede alta.
20. **Portas:** os `_door_left` e `_door_right` são blocos, intransponíveis como qualquer parede: servem para portas decorativas e para saídas que ficam fora do mapa. Uma porta por onde a Kleia passa exige um tipo de bloco transponível no `MapLayout` e no `TileData` (ou a porta desenhada como overlay sobre uma célula de chão), e isso é código.
21. **Tiles animados:** não há suporte hoje (nenhum `AnimatedTile`). Forno, torno, água, fonte, vento e fenda precisam de `AnimatedTile` (pacote 2D Tilemap Extras) ou de um componente que troque o sprite. Até lá, entra só o estático.
22. **Props com mais de 32 px de largura** (`prop_ancient_olive_tree` 48×64, `prop_olive_tree` 48×56): precisam de pivot próprio no `PixelArtImporter`, `(8/48; 0)`, para o tronco cair na célula. O teste `Tiles_UseBottomLeftPivot` também precisa aceitá-lo.
23. **Herma:** `prop_statue_base` passa de 32×36 para 32×48. Com pivot Bottom Left ela só cresce para cima, mas a captura de referência (`SandboxTools.CaptureReference`) tem de ser refeita.
24. **Ordenação:** com a célula de personagem mais larga, recapture a GridSandbox e confira os quatro casos difíceis da skill `pixel-art-pipeline`.
25. **Meandro em 4 peças (P3):** `floor_mosaic_straight` e os três cantos só entram no mapa quando o `MapLayout` aceitar um símbolo por peça, inclusive as duas peças espelhadas (a reta do eixo Y e o canto esquerdo). O espelho deve sair por script na importação, como um sprite `_mirrored` gerado pelo `ArtTools`, e não pela matriz do tile: com pivot Bottom Left, a escala X = -1 gira o losango em torno do canto e o desloca uma célula, e a matriz também entra na ordenação. Até lá, a borda inteira usa o `floor_mosaic` simétrico do P1. O `FloorTileSeams` continua valendo, porque as duas bordas de cima continuam em cima depois do espelho.

### UI, fonte, ícones e cor

26. **Caixa de diálogo:** `box_dialogue` passa de 160×40 para 208×48, o tamanho que o código já desenha.
27. **9-slice:** `box_menu`, `box_nameplate` e `box_items` como caixas do tamanho do conteúdo, como pede a referência, exigem borda de 9-slice no sprite e layout por conteúdo.
28. **Fonte da UI (Ark Pixel Font 10px Proportional, decidida em 21/09/2026):**
    - `FontTools.BuildPixelFont` passa a gerar o Font Asset da Ark Pixel Font 10px Proportional, variante latin, no lugar da Press Start 2P, com as mesmas regras de hoje: raster, atlas com filtro Point, congelado como estático depois de gravar latim básico e acentos, LiberationSans como fallback do □.
    - O tamanho de fonte dos labels passa a ser o tamanho nativo, 10 (um pixel da fonte = um pixel do quadro de 384×216, ampliado 5× junto com o resto), e o auto size de 4 a 6 da `BattleHudTools.FontSize` deixa de valer. A fonte é proporcional: o layout mede a largura real do texto, nunca conta caracteres.
    - `RaisedAccentGlyphs`: a amostra `font_ui_specimen.png` confere se as maiúsculas acentuadas da Ark Pixel (Á À Â Ã É Ê Í Ó Ô Õ Ú) já vêm inteiras, com o acento acima da linha de capitular. As que vierem inteiras só são conferidas; a que vier espremida continua sendo recomposta. Os testes de glifo (cobertura e "a maiúscula acentuada vem da própria fonte e mantém o topo da letra base") continuam valendo. O arquivo de licença do repositório, lido em 21/09/2026, não declara Reserved Font Name, então um atlas recomposto pode manter o nome da fonte.
    - O `lineHeight` passa a cobrir do descendente ao topo do acento. As linhas do menu têm 12 px (a referência usa 8): o menu de 6 itens mede cerca de 80 px de altura, e a caixa de diálogo de 208×48 comporta 3 linhas. O menu, a plaqueta e os slots são medidos na amostra.
    - O crédito entra em `credits.body` (`ui_text.json`): Ark Pixel Font, Copyright (c) 2021, TakWolf, sob a SIL Open Font License 1.1. Se a Press Start 2P sair do jogo, o crédito dela sai junto.
29. **Ícones de falácia:** o `FallacyIconPainter`, dentro de `DataTools.Regenerate`, pinta os 20 ícones por código. Ele precisa ler `icon_fallacy_<id>.png` quando o arquivo existir (o ícone já composto com a moldura).
30. **HUD novo:** os ícones de Coerência, os contadores, os slots e o medidor vertical da Plateia exigem views novas.
31. **Paleta, névoa e realces:**
    - escreva a palette v2 da §4.2 em `art/palette/snes.gpl` (e em `snes.txt` e `snes.png`), com os nomes da tabela e os hex das rampas novas confirmados (§12.2); até lá, as chaves de trabalho usam os hex propostos;
    - troque a cor de fundo da câmera em `RenderSetupTools` (`#101020`) por `void` `#000000`, como pede a referência;
    - a névoa passa a `void`: o `fog.png` novo já vem assim, e qualquer `#101020` fixo no código da névoa ou das bordas `fog_edge_*` do `SandboxTools` passa a `#000000`;
    - os `hl_*` passam de alfa 140 para losangos opacos de 2 tons, e o `hl_move` passa do azul ao verde-amarelado (`move_*`).
32. **Escala da tela (5× em 1080p):**
    - `RenderSetupTools.ReferenceResolution` é hoje um valor só (224), usado nos dois eixos da `PixelPerfectCamera`. Ele vira largura 384 e altura 216 (5× em 1920×1080), com o mesmo PPU 16;
    - `BattleHudTools.CanvasReference` repete esse valor, e o `UiBuilder` o usa nos dois eixos do `CanvasScaler` de todos os prefabs de UI (HUD, pausa, compêndio, diálogo da exploração, medidor de jurados) e do `MainMenu`. Eles passam a (384, 216), e o HUD vai para os cantos do quadro 16:9, como no `target_style.md`;
    - recapture as referências (`SandboxTools.CaptureReference`) e confira que uma sala de largura + altura 14 cabe inteira na tela.
33. **Escurecimento fora da arena:** na batalha, tudo o que fica fora da arena (tiles, props e atores) recebe por cima um xadrez regular de 50% de `void`, feito em código. Pode ser um tilemap de overlay procedural ou um material; o padrão precisa estar preso à grade de pixels do quadro nativo, para não tremer quando a câmera anda. Não há arte para isso, e nenhum tile ganha versão escura. O que conta como arena é definido pelo código da batalha.

### Entrada da arte

34. **Promoção da arte aprovada:** um método de editor (por exemplo, `ArtTools.PromoteIncoming`) faria o caminho de `art/incoming/` até o lugar definitivo. Ele valida tamanho, paleta (a palette v2), contagem de cores e alfa com o checklist da §6 e falha com código diferente de zero.
35. **Atualizar os documentos:** `docs/GDD.md` §7 e §9 (24×32 → 32×32, 2 tons → 3 a 4, contorno seletivo, alcance de movimento verde-amarelado), a tabela de dimensões e a paleta da skill `pixel-art-pipeline`, e as passagens do `CLAUDE.md` sobre a câmera de 224×224 e a Press Start 2P.

---

## 12. Pendências e perguntas abertas

### 12.1 Decididas pelo usuário (21/09/2026)

O usuário fechou em 21/09/2026 seis das dezoito perguntas da revisão 2 e parte de uma sétima, e decidiu também a escala da tela e o escurecimento fora da arena. O documento já aplica tudo isto.

| Tema | Decisão | Onde entra |
|---|---|---|
| Fonte da UI (antiga questão 16) | **Ark Pixel Font 10px Proportional**, variante latin, SIL OFL 1.1 ([TakWolf/ark-pixel-font](https://github.com/TakWolf/ark-pixel-font)), no lugar da proposta monogram. Avanço de cerca de 6 px nas maiúsculas, maiúsculas de 7 px, linhas de 12 px no menu | §2, §5.1, §5.6, `font_ui_specimen`, §11 (28) |
| Cores fora da paleta (antiga 4) | **Palette v2** de `snes.gpl`, ainda RGB555: acrescenta `void` `#000000`, `ui_white` `#F8F8F8`, `ui_gold` `#F8D830` com contorno escuro, `ui_blue` `#3868D8`, a rampa verde-amarelada do alcance, a rampa da argila e sombras frias do mármore. As cores são acrescentadas, nunca trocadas pela vizinha. A névoa fica em `void`, não em `#101020` | §4.2, §4.6, §4.8, `fog`, peças de UI, §11 (31) |
| Cor do alcance de movimento (antiga 3) | **Verde-amarelado em 2 tons**, como na referência, no lugar do azul-egeu. REFUTAR e MAIÊUTICA mantêm as cores próprias | `hl_move`, §4.8 |
| Escala da tela | **5× em 1080p**, quadro nativo de 384×216. O mundo continua com losango de chão de 32×16 e célula de 32×32 para personagem e objeto | §2, §4.1, §11 (32) |
| Fora da arena | O mundo fora da arena de batalha é escurecido por um **xadrez de 50% de `void`**, feito em código. Nenhum arquivo da lista dependia de outra solução, então nada saiu | §4.1, §4.5, §5.5, §11 (33) |
| Altura da figura (antiga 1) | Vale a tabela da §4.7 (Kleia com 24 px, Thrasybios com 26). O GDD dava 28 a 32, e um Thrasybios de 32 não cabe na célula com os pés na linha 29 | §4.7 |
| Tamanho do retrato (antiga 14) | **32×32** na janela da moldura `frame_portrait` de 40×40. O ART-08 #1 pedia 40×40; o card precisa ser revisto no board | §2, §5.6 |
| Paredes altas (parte da antiga 17) | Parede alta **só nas bordas de trás** da sala | §2, §5.5, §11 (19) |
| Cidadão modular (antiga 18) | As 8 combinações de cabeça × manto que faltam saem por **troca de cor em código** | §8.2, §11 (11 a 13) |

### 12.2 Ainda em aberto

Precisam de decisão do usuário. Enquanto não houver decisão, vale o que está entre parênteses.

1. **Pipeline de 32×32.** A arte nova só entra no jogo depois do card de código da §11 (a arte fica em `art/incoming` até lá).
2. **Sombra no chão.** Vai desenhada no sprite, em `outline_blue`, ou vira um sprite separado que não acompanha o bob? (Desenhada no sprite.)
3. **Troca de cor em relação aos placeholders.** Confirmar três mudanças: Phaidra de oliva para xale escuro, Simo de vermelho para açafrão e Eudoro de túnica dourada para branco com borda dourada, como no GDD. (Trocadas.)
4. **Animações novas.** Confirmar as que não estão no GDD: `withdraw`, `argue`, `think` da Kleia no menu, `listen` e `invert` (habilidades do Ato II), as poses de 1 quadro dos duelistas, `float` dos espectros, `cheer` da plateia, `paint` da Kleia e da aluna, e os 200 ms por quadro do `trance`, que o GDD e o AT2-05 não definem. (Todas propostas como aqui.)
5. **Personagens sem ficha no GDD.** Confirmar os ganchos visuais propostos para os 5 duelistas, o elenco do Ato II, o acadêmico do selo, a aluna e as 3 variantes de cidadão. (Fichas das seções 9 e 10.)
6. **Meandro.** O GDD e o ART-06 #2 pedem um conjunto de 4 tiles, mas o mapa usa um id só (`floor_mosaic`) na borda inteira da praça. (No P1, um tile simétrico que serve em qualquer posição. No P3, o conjunto de 4 da Trilha B: uma peça reta e três cantos, com `flipX` para as outras orientações, o que exige suporte no `MapLayout`.)
7. **Mapas sem especificação.** Academia, Delfos e Tribunal não têm mapa, e o Tribunal nem tem tiles no GDD. Os tiles do Tribunal e o `block_cliff_rock` de Delfos são propostas.
8. **Critério do ART-04 #1** ("testar 3 geradores"). O padrão agora é o PixelLab; o critério precisa ser revisto no board.
9. **Quem é quem.** Qual das duas pessoas fica com a Trilha A e qual fica com a Trilha B, e se cada uma assina o próprio Tier 1.
10. **ART-07 × conjunto reduzido.** O card pede as doze animações do GDD §7 para todo debatedor. O pedido para esta spec é o conjunto completo só para a Kleia e um conjunto reduzido para o resto. (Vale o pedido; o card precisa ser revisto no board.)
11. **ART-05 #5 × tiras novas.** O card pede substituir mantendo o mesmo nome, para zero referência quebrada. As folhas de 96×128 viram tiras por animação, com nomes novos. (Os nomes dos tiles, props e UI continuam os mesmos; os personagens mudam de formato e dependem do card de código da §11.)
12. **Parede da frente e portas** (o que sobrou da antiga questão 17). A parede alta fica só nas bordas de trás, e os mapas atuais usam parede de 32×32 nas quatro bordas da oficina. A parede da frente fica baixa ou sai do mapa? As portas por onde a Kleia passa ficam como vão aberto (como hoje) ou viram porta desenhada, o que exige código (§11)? (Parede da frente baixa, vão aberto nas saídas.)
13. **Valores das rampas novas da palette v2** (nova, aberta pela decisão da paleta). A decisão acrescenta a rampa do alcance, a da argila e as sombras frias do mármore, mas não fixa os hex. A §4.2 propõe `move_light`, `move_mid`, `clay_light`, `clay_mid`, `clay_shade`, `marble_cold_shade` e `marble_cold_dark` em RGB555. Os pilotos B-001 (`floor_marble`) e A-003 (`prop_amphora_stack`) já usam algumas delas, então é o momento de conferir. (Os hex da §4.2, confirmados no piloto.)

---

## 13. Tabela de progresso

O CSV é a fonte da verdade. Atualize esta tabela a cada sincronização semanal. A contagem sai deste comando, na raiz do repositório:

```powershell
Import-Csv docs/art/pixellab_assets.csv | Group-Object track, priority, status | Sort-Object Name | Format-Table Count, Name
```

| Trilha | Prioridade | Arquivos | Quadros | todo | wip | review | approved | integrated | % aprovado |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|
| A | P1 | 106 | 250 | 97 | 0 | 1 | 0 | 8 | 8% |
| A | P2 | 105 | 214 | 105 | 0 | 0 | 0 | 0 | 0% |
| A | P3 | 41 | 91 | 41 | 0 | 0 | 0 | 0 | 0% |
| B | P1 | 111 | 229 | 98 | 0 | 1 | 0 | 12 | 11% |
| B | P2 | 97 | 169 | 97 | 0 | 0 | 0 | 0 | 0% |
| B | P3 | 36 | 58 | 36 | 0 | 0 | 0 | 0 | 0% |
| **Total** | | 496 | 1011 | 474 | 0 | 2 | 0 | 20 | 4% |

Por categoria:

| Categoria | Trilha A | Trilha B | Total | Feitos |
|---|---:|---:|---:|---:|
| Tiras de personagem (`characters`) | 150 | 159 | 309 | 0 |
| Retratos (`portraits`) | 22 | 24 | 46 | 0 |
| Chão (`floor`) | 8 | 18 | 26 | 10 |
| Paredes e blocos (`blocks`) | 8 | 15 | 23 | 5 |
| Props (`props`) | 11 | 16 | 27 | 5 |
| Overlays (`overlays`) | 7 | 0 | 7 | 0 |
| Efeitos (`fx`) | 3 | 4 | 7 | 0 |
| Interface (`ui`) | 20 | 2 | 22 | 0 |
| Ícones (`icons`) | 23 | 6 | 29 | 0 |
