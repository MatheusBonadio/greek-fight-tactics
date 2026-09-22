# Estilo-alvo: referência isométrica 16-bit

Referência enviada pelo usuário em 21/09/2026: uma cena isométrica de tactics no estilo SNES (masmorra azul-petróleo, herói com plaqueta de nome, menu de comandos flutuante, HUD de aventura nos cantos). A imagem é 448×448, ou seja, um quadro nativo de **224×224 ampliado 2×**, a mesma resolução de referência da nossa Pixel Perfect Camera. Quando o arquivo estiver no repositório, ele fica em `art/reference/target_style.png`.

Reproduzimos o **estilo** (proporções, bordas, cores, tipografia, composição). Não copiamos personagens, ícones nem a assinatura do autor da imagem.

Todas as medidas abaixo estão em pixels nativos (quadro de 224), com precisão de ±1 px.

## Decisões do usuário (21/09/2026)

- **Tela:** preencher a tela 16:9, mantendo a escala da referência. Nada de barras laterais; o HUD fica nos cantos da tela.
- **Mapas:** mistos. Batalhas e interiores são salas que cabem inteiras na tela, com largura + altura de até 14 células (ex.: 8×6, porque uma sala isométrica ocupa `(largura + altura) × 16` px de largura). A exploração da Ágora pode ser maior, com a câmera seguindo a Kleia.
- **HUD:** o layout e o estilo são os da referência, adaptados ao significado do GFT.
- **HUD na batalha (22/09/2026):** tudo fica numa faixa embaixo da tela (um quarto da altura): menu de ações, painéis da Kleia (azul egeu, à esquerda) e do desafiante (vermelho, à direita) com nome, rosto e Coerência em número, Plateia entre os dois e a caixa de texto larga. Nada cobre a parte de cima, onde ficam o duelo e o público. O texto fica um pouco menor que o da Press Start 2P e ocupa mais largura: o canvas da UI tem referência de 640×360.

## Grade e escala

- **Tile de chão:** losango de 32×16, igual ao nosso atual. A escala do mundo não muda.
- **Sala da referência:** cerca de 8×6 células. Ela ocupa quase toda a largura do quadro e fica centralizada sobre fundo **preto puro**.
- **Paredes:** blocos altos, de uns 32 px de altura visível, com tijolos, cobrindo as bordas de trás da sala. As portas são recortes escuros na parede.
- **Personagens:** cerca de 16×24 px de corpo visível, dentro da célula 24×32 atual, com contorno escuro de 1 px e 3 a 4 tons por material.

## Cores

- Paleta SNES (RGB555, canais múltiplos de 8; `art/palette/snes.gpl`), com saturação alta e sombras frias.
- **Fundo:** preto `#000000` fora do mapa, não o azul-marinho atual.
- **Chão da referência:** azul-petróleo em 3 tons, com linha de junta escura. No GFT, cada mapa mantém o próprio material (mármore, terra, argila) em 3 tons, e a junta é sutil: um tom abaixo do tom dominante do material, para os tiles se emendarem e só uma linha leve marcar a grade (decisão do usuário, 22/09/2026). Paredes vizinhas também se emendam, com a junta no tom mais escuro do material.
- **Alcance de movimento:** losangos preenchidos de verde-amarelado em 2 tons, com as juntas visíveis. As células de ataque e de maiêutica seguem o mesmo esquema, com a própria cor.
- **Interface:** branco `#F8F8F8` sobre preto; dourado `#F8D830` com contorno marrom-escuro para a plaqueta de nome; azul `#3868D8` para as bordas das caixas de slot e de itens.

## Tipografia

- Fonte bitmap **estreita**, em caixa alta: avanço de cerca de 5 px por letra, maiúsculas de 7 px e linha de 8 px no menu. A Press Start 2P, com 8 px de avanço, é larga demais para esse visual.
- Precisa cobrir as maiúsculas acentuadas do português (Á À Â Ã É Ê Í Ó Ô Õ Ú Ç), com o acento acima da linha das maiúsculas. A fonte escolhida, a Ark Pixel Font 10px Proportional, já desenha assim, com linha de 14 px nos textos corridos e de 12 px nas listas.
- Licença OFL ou CC0 obrigatória, com o crédito em `credits.body`.

## Menu de comandos (referência: MOVE / ATTACK / MAGIC / ITEM / DEFEND / RUN)

- Caixa preta com borda branca de 1 px e 3 px de margem interna. O tamanho vem do conteúdo: cerca de 44×55 para 6 linhas.
- Aparece **perto da unidade ativa**, acima e à direita dela, sem cobri-la. Não fica fixo num canto.
- Seta branca ▸ à esquerda do item selecionado; uma linha de 8 px por item.
- No GFT, os itens são CIRCULAR, REFUTAR, MAIÊUTICA, CITAR, CONCEDER e RETIRAR-SE. Os submenus (falácias, premissas) seguem o mesmo estilo e abrem ao lado.

## Plaqueta de nome

- Retângulo dourado com contorno escuro e o nome em letras escuras (cerca de 22×8 para 4 letras), com uma seta dourada apontando para baixo logo abaixo (cerca de 7×4).
- Fica sobre a cabeça da unidade ativa, no turno dela.

## HUD de exploração e batalha (adaptado ao GFT)

| Referência | GFT |
|---|---|
| 3 corações no canto superior esquerdo (cerca de 7×7, com 1 px de espaço) | Coerência da Kleia em ícones com frações |
| Barra vertical verde com moldura (cerca de 6×18) | Plateia |
| Contadores com ícone, "×100" | Dracmas, usos de citação e falácias do compêndio |
| Dois slots com borda azul arredondada e letra (cerca de 12×20) | Citação equipada e habilidade da ramificação Damaris (Inversão ou Escuta) |
| Caixa de itens com borda azul dupla no canto superior direito (cerca de 56×22) | Últimas falácias reveladas ou atalho do compêndio |
| Pergaminho de mapa no canto inferior direito | Opcional: minimapa com a névoa revelada |

- A Coerência do oponente aparece na plaqueta dele, ou numa barra curta ao lado, só durante a batalha.
- A caixa de diálogo não aparece na referência. Ela segue o mesmo estilo: preta, borda branca de 1 px, a fonte nova e o retrato à esquerda.
