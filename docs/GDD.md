# GREEK FIGHT TACTICS (GFT)
### Tactics isométrico de dialética na Grécia Antiga

**Motor:** Unity 6.6 2D URP · **Resolução lógica:** 224×224 (ou 256×224) · **PPU:** 16
**Perspectiva:** isométrica 2:1 · **Paleta:** RGB555 (SNES) · **Escala final:** 2× Nearest Neighbor

---

## 1. PREMISSA

Atenas, 402 a.C. Os Trinta Tiranos caíram há um ano. A democracia voltou, desconfiada e vingativa. Ninguém sabe ainda que dentro de três anos um velho chamado Sócrates vai beber cicuta por "corromper a juventude".

**Kleia**, dezessete anos, filha de um oleiro do Cerâmico, aprendeu dialética do pior jeito possível: ouvindo. Sentada no chão da oficina, pintando ânforas enquanto homens discutiam sobre ela como se fosse mobília. Ela não pode falar na Assembleia. Não pode entrar na Academia. Não pode processar ninguém.

Mas ninguém proibiu uma garota de fazer uma pergunta.

---

## 2. LOOP DE JOGO

**Exploração** → mapa isométrico coberto por névoa, revelado ao caminhar. Áreas trancadas por `ActFlag` narrativo.
**Encontro** → um adversário te nota dentro do raio de aggro. A câmera fecha, a plateia se aproxima, o turno começa.
**Batalha** → debate por turnos em grade. Duas barras, seis ações.
**Consequência** → reputação, diálogo e portas mudam conforme *como* você venceu, não apenas *se* venceu.

### 2.1 As duas barras

| Barra | Cor | O que é | Você perde quando |
|---|---|---|---|
| **Coerência** | Azul-egeu `#48C8E0` | Sua integridade lógica | Chega a 0 → você se contradisse em público |
| **Plateia** | Dourado `#F8E058` | Quem a multidão apoia | — (é recurso de vitória, não de derrota) |

**Vitória:** zerar a Coerência do oponente **ou** levar a Plateia a 100% do seu lado.
**Derrota:** sua Coerência chega a 0.

Sofistas: Plateia alta, Coerência baixa. Filósofos: o inverso. Isso dá inimigos mecanicamente distintos sem tocar no sistema.

### 2.2 As seis ações (o menu do mockup)

| Menu | Ação | Efeito |
|---|---|---|
| MOVE | **CIRCULAR** | Reposiciona na grade. Ficar perto da plateia dá bônus de persuasão; ficar encurralado dá penalidade. |
| ATTACK | **REFUTAR** | Aponta a falácia cometida. Acerto: dano em Coerência + ganho de Plateia. Erro: contragolpe. |
| MAGIC | **MAIÊUTICA** | Faz a pergunta que força o oponente a se contradizer sozinho. Custo alto de turno, dano enorme se a premissa escolhida for a certa. |
| ITEM | **CITAR** | Pergaminhos com efeito fixo (Heráclito, Parmênides, Homero). Consumível. |
| DEFEND | **CONCEDER** | Aceita o ponto. Recupera Coerência, perde Plateia. Às vezes é a única jogada certa. |
| RUN | **RETIRAR-SE** | Foge. Reputação cai, o adversário fica mais forte no reencontro. |

### 2.3 A regra de ouro da IA embarcada

**O LLM gera. A engine julga.** O modelo nunca decide quem venceu.

O LLM devolve o argumento do adversário já etiquetado:

```json
{
  "speech": "Todo homem justo é temido pelos injustos. Sócrates era temido. Logo, Sócrates era justo.",
  "fallacy_id": "affirming_the_consequent",
  "premises": ["Todo justo é temido pelos injustos", "Sócrates era temido"],
  "weak_premise": 1,
  "tone": "arrogant"
}
```

Chaves e valores de enum em inglês; `speech` e `premises` em português, porque são texto exibido ao jogador.

O jogador não digita: ele **aponta**. Qual premissa é frágil, qual falácia foi cometida. Verificável em código, funciona offline com conteúdo pré-gerado, e o texto nunca se repete.

**Obrigatório:** cache por seed, fallback escrito à mão para cada `fallacy_id`, timeout de 2s. Se a API cair, o jogo continua.

---

## 3. ATO I — "O CERÂMICO E A ÁGORA"
### *Quem tem o direito de perguntar?*

**Arco:** Kleia descobre que sabe argumentar. Descobre também que isso tem preço, e que quem paga é o pai dela.
**Adversários:** sofistas e charlatães. Todos cometem **falácias formais e informais** — sempre há um erro a apontar.
**Duração-alvo:** 3–4 horas.

### Beats

**I.1 — A Oficina (tutorial)**
Kleia pinta uma ânfora de figuras vermelhas. Tutorial de movimento em grade e de névoa: a oficina se revela conforme ela anda. Aristeu, o pai, pede que ela leve a mercadoria à ágora — ele está com as mãos ruins demais para carregar.

**I.2 — A Ágora (primeira batalha)**
A névoa se abre sobre o mercado. **Thrasybios** vende "a arte de vencer qualquer discussão" por três dracmas. Demonstra o produto humilhando um freguês: prova que a ânfora que o homem acabou de comprar de Kleia *não é uma ânfora* (equivocação entre "vaso" e "ânfora ritual").

Kleia interrompe. Batalha tutorial: uma falácia, uma refutação, vitória garantida.

**I.3 — O riso errado**
Ela vence. A plateia ri — dela, não com ela. Uma garota do Cerâmico venceu um mestre; isso é piada, não feito. Thrasybios não se abala: publicidade é publicidade. Oferece aulas pagas. *Escolha registrada.*

**I.4 — Menexeno**
Um velho de manto sujo a segue até em casa. Assistiu tudo. Oferece ensinar de graça, com uma condição: **que ela nunca aceite dinheiro por uma pergunta.** Não explica por quê.

**I.5 — Mundo aberto (cinco duelos)**
A névoa abre cinco bairros. Cada adversário ensina uma família de falácia:

| Adversário | Local | Falácia | Perfil |
|---|---|---|---|
| **Óron, o cambista** | Stoa | Falso dilema | Coerência 40 / Plateia 60 |
| **Phaidra, a adivinha** | Templo de Hefesto | Apelo à ignorância | Coerência 30 / Plateia 70 |
| **Kritolaos, o político menor** | Pnyx | Ad hominem | Coerência 50 / Plateia 80 |
| **Simo, o ator** | Teatro | Espantalho | Coerência 45 / Plateia 65 |
| **Bátilo, o pescador** | Porto | Generalização apressada | Coerência 60 / Plateia 30 |

Bátilo é o único simpático — e o mais difícil de vencer sem humilhar. Serve de teste moral: dá para vencê-lo com Maiêutica em vez de Refutação.

**I.6 — A cobrança**
Thrasybios não esquece. Espalha pela ágora que Kleia é *asebés* — ímpia, que ensina a duvidar dos deuses. É exatamente a acusação que mataria Sócrates três anos depois. Os clientes somem. Aristeu começa a vender as ferramentas.

**I.7 — Damaris**
Uma sofista estrangeira, de Abdera (a terra de Protágoras), procura Kleia. Não é inimiga. Oferece proteção, dinheiro e uma técnica nova, em troca de que Kleia argumente por ela quando for chamada.

> **Escolha de ramificação (define o Ato II):**
> **Aceitar** → habilidade *Inversão* (transforma o ataque do oponente em ganho de Plateia) + 200 dracmas. Menexeno para de aparecer por três dias.
> **Recusar** → habilidade *Escuta* (revela a premissa fraca uma vez por batalha) + respeito de Menexeno.

**I.8 — Clímax: o julgamento de rua**
Thrasybios força um debate público na escadaria do Portique. Se Kleia perder, o pai é arruinado e o nome dela vira sinônimo de impiedade.

**Boss em três fases:**
1. *Ataque pessoal* — ad hominem sobre a origem dela. Contra-ataque: Conceder (tirar a arma dele).
2. *Falso dilema* — "ou você respeita os deuses, ou você é como Sócrates." Contra-ataque: Refutar, apontando a terceira opção.
3. *Apelo ao medo político* — invoca os Trinta Tiranos. Contra-ataque: só Maiêutica funciona; é preciso fazê-lo admitir que ele próprio lucrou sob a tirania.

**I.9 — O twist**
Kleia vence. Menexeno some sem explicação. Buscando-o, ela chega ao arquivo do tribunal e encontra uma lista: **Menexeno foi um dos jurados no julgamento de um velho filósofo, anos antes. Votou pela condenação.**

Ele não estava ensinando por bondade. Estava pagando uma dívida.

**I.10 — Portão do Ato II**
Um acadêmico que assistiu ao debate entrega a Kleia um **selo de bronze**. Não é admissão — é um convite para visitar. `ActFlag = 2`. A estrada para a Academia sai da névoa.

---

## 4. ATO II — "A ACADEMIA E O ORÁCULO"
### *Toda pergunta tem resposta?*

**Arco:** Kleia descobre que existem perguntas sem falácia e sem resposta. E que a habilidade que a salvou pode matá-la.
**Adversários:** filósofos legítimos que apresentam **paradoxos genuínos**, não erros.
**Duração-alvo:** 4–5 horas.

### A virada mecânica

No Ato II, **REFUTAR falha mesmo quando você está certo** — porque não há falácia para apontar. Um paradoxo não é um erro. A interface avisa isso visualmente: quando o adversário não tem `fallacy_id`, o realce vermelho de Refutação simplesmente não acende.

Isso obriga o jogador a usar **CONCEDER** e **MAIÊUTICA**, que até então pareciam ações fracas. O jogo ensina pela mecânica que refutar não é a mesma coisa que pensar.

### Beats

**II.1 — A Academia**
Kleia entra como *convidada*, não aluna — pode assistir, não pode inscrever-se. **Eudoro**, geômetra de vinte anos, a trata como curiosidade de feira. A primeira batalha contra ele é uma humilhação: ela tenta refutar e não consegue.

**II.2 — O confronto com Menexeno**
Ele reaparece no jardim de oliveiras. Kleia o acusa. Ele não nega:

> "Eu votei pela morte porque ele me fez sentir estúpido diante do meu filho. Não porque estava errado. Porque estava certo, e eu não suportei. É esse o preço de uma boa pergunta, menina. Alguém sempre paga."

**II.3 — Os duelos de paradoxo**

| Adversário | Paradoxo | Solução mecânica |
|---|---|---|
| **Zenão de Élea** | Aquiles e a tartaruga | Conceder duas vezes, depois Maiêutica na definição de "infinito" |
| **Eubúlides** | O Mentiroso | Impossível vencer por Refutação; só Conceder mantém a Coerência |
| **Aglaofonte** | O Sorites (o monte de areia) | Maiêutica na premissa da vagueza |
| **Irmãos Dídimos** | O Navio de Teseu | Batalha dupla: dois alvos com condições opostas |
| **Heráclito & Parmênides** *(espectros)* | Tudo flui × Nada muda | Você precisa **perder** contra um para vencer o outro |

**II.4 — A tese de Damaris**
Ela volta com o argumento forte, e o argumento é bom: *se todo argumento pode ser refutado, a verdade não existe — resta apenas a persuasão.*

E ela demonstra. Esta batalha é **inperdível por design**: Kleia perde independentemente do que o jogador faça. Mas *como* ela perde (Coerência zerada por contradição, ou por Conceder até o fim) muda o diálogo seguinte e o final disponível.

**II.5 — Delfos**
Viagem por montanha. Névoa densa, tiles de rocha e neve. A **Pítia** não debate — ela reflete. Mecanicamente: **cada Refutação lançada volta contra a própria Coerência de Kleia.** A única jogada vencedora é CONCEDER no momento exato, dizendo em voz alta: *não sei*.

É a única batalha do jogo vencida por admitir ignorância.

**II.6 — Clímax: o Tribunal**
Kleia é formalmente acusada de impiedade. A barra de Plateia vira **501 jurados** explícitos, cada ponto um voto contado.

- **Thrasybios** é o acusador oficial.
- **Damaris** se oferece como defensora. *Se você aceitou a oferta dela no Ato I, ela trai você no terceiro turno.* Se recusou, ela defende de verdade — e perde reputação por isso.
- **Menexeno está entre os jurados.** Seu voto é o último a ser contado.

**II.7 — Os três finais**

Determinados pela ação usada no turno final:

**REFUTAR → O Sofista.**
Kleia vence humilhando Thrasybios em público. Absolvida por 340 votos. Abre uma escola paga, fica famosa e rica. Menexeno vota pela absolvição e vai embora sem olhar para ela. Última cena: uma aluna pergunta algo sincero e Kleia cobra pela resposta.

**CONCEDER → O Filósofo.**
Kleia recusa se defender com truques. Diz apenas o que acredita. Condenada ao exílio por 261 votos. Menexeno vota pela condenação — e depois larga tudo e vai com ela para a estrada. Última cena: os dois caminhando, discutindo.

**MAIÊUTICA → A Mestra.** *(final verdadeiro)*
Kleia não se defende: faz perguntas aos jurados até que eles cheguem sozinhos à absolvição. Vence por 281 votos e ninguém lembra do nome dela — porque eles acham que se convenceram sozinhos. Volta ao Cerâmico e abre uma escola gratuita, aberta a mulheres e escravos. Última cena: uma menina sentada no chão, pintando uma ânfora, ouvindo.

---

## 5. PERSONAGENS

Cada ficha traz **físico** (para o gerador de sprite), **psicológico** e **assinatura visual** — o elemento que torna a silhueta reconhecível em 24 px.

---

### KLEIA DE CERÂMICO — protagonista

**Idade:** 17. **Altura no sprite:** 30 px (padrão).
**Físico:** magra, ombros levemente curvados de quem passa o dia inclinada sobre cerâmica. Cabelo castanho-escuro, curto na altura do queixo, mal preso com um cordão. Antebraços e mãos permanentemente manchados de óxido de ferro (vermelho-terra) e argila. Pés descalços, sujos. Cicatriz pequena no polegar direito (torno).
**Vestimenta:** quíton simples de lã tingida de azul-egeu, desbotado, com remendo visível no ombro direito. Sem joias. Um cordão de couro no pulso.
**Assinatura visual:** **tabuinha de cera** ocre presa ao cinto + estilete de bronze. É o "arma" dela na silhueta.
**Psicológico:** atenta a ponto de parecer rude. Não interrompe — espera a pessoa terminar de errar. Sua arrogância é defensiva: ela ataca antes de ser desprezada. Arco do jogo é aprender a fazer perguntas sem querer vencer.
**Paleta:** `aegean_light #48C8E0` · `aegean_mid #2890C0` · `hair_dark #382828` · `skin_light #F8D0A8` · `ochre_mid #D89038`

---

### MENEXENO — mentor

**Idade:** 68. **Altura no sprite:** 28 px (curvado).
**Físico:** alto mas encurvado, magro de negligência e não de disciplina. Barba branca desigual, cortada por ele mesmo. Cabelo ralo. Mãos grandes de quem já foi ferreiro. Anda apoiado num bastão de oliveira. Olhos com catarata inicial no esquerdo — pintar a íris esquerda um tom mais claro.
**Vestimenta:** manto de lã crua, outrora branco, hoje cinza de sujeira. Sandálias gastas amarradas com corda errada.
**Assinatura visual:** **bastão de oliveira** nodoso, mais alto que ele. Na versão de Ato II, um pequeno selo de bronze pendurado nele — o do júri.
**Psicológico:** paciente por penitência, não por natureza. Nunca dá respostas — não por método socrático puro, mas porque perdeu a confiança nas próprias. Fala pouco, e quando fala é seco.
**Paleta:** `marble_light #F8F8F0` · `marble_shade #A8A8A0` · `marble_mid #D8D8C8` (barba) · `ochre_shade #A05828` (bastão)

---

### THRASYBIOS — antagonista do Ato I

**Idade:** 41. **Altura no sprite:** 32 px (o mais alto do elenco).
**Físico:** robusto, bem alimentado, postura de orador treinado — peito aberto, queixo levantado. Cabelo castanho ondulado e oleado, barba aparada com precisão de barbeiro caro. Dentes perfeitos (raro e proposital). Gesticula com a mão direita sempre aberta.
**Vestimenta:** himátion de lã tingida de **púrpura** — corante caríssimo, exibicionismo puro. Borda em grega dourada. Sandálias novas. Anel de sinete no indicador.
**Assinatura visual:** **rolo de papiro** com fita dourada, que ele nunca abre. É adereço, não fonte.
**Psicológico:** não é burro e não é mau. É um profissional. Acredita sinceramente que verdade é um produto e que quem paga merece vencer. A única coisa que o fere é ser tratado como irrelevante — não como errado.
**Paleta:** `purple_light #C078E0` · `purple_mid #8838A0` · `purple_dark #501868` · `gold_light #F8E058` · `hair_mid #604030`

---

### DAMARIS DE ABDERA — antagonista do Ato II

**Idade:** 35. **Altura no sprite:** 30 px.
**Físico:** compacta, ereta, imóvel quando fala — o oposto de Thrasybios. Pele mais escura (trácia). Cabelo preto preso em trança enrolada na nuca, sem um fio solto. Olhos muito fixos: nos retratos, pintar sem piscar (sem frame de blink).
**Vestimenta:** peplos vermelho-terracota de tecido caro mas corte severo, sem bordado. Um único broche de prata. Sandálias práticas de viagem.
**Assinatura visual:** **sem adereço nas mãos** — ela é a única personagem que nunca segura nada. As mãos vazias são a assinatura.
**Psicológico:** a mais honesta do jogo. Não mente: ela realmente acredita que a verdade não é acessível e que fingir o contrário é crueldade. Trata Kleia com respeito genuíno, o que torna a traição (ramo A) pior.
**Paleta:** `red_light #F87858` · `red_mid #C03828` · `red_dark #781820` · `skin_shade #A87048` · `marble_light #F8F8F0` (broche)

---

### EUDORO — rival da Academia

**Idade:** 20. **Altura no sprite:** 30 px.
**Físico:** magro, alto, desengonçado. Cabelo preto cortado curto demais. Postura de quem foi ensinado a se portar mas não praticou. Sempre com tinta nos dedos.
**Vestimenta:** quíton branco impecável com borda dourada — dinheiro de família. Sandálias caras que ele arrasta.
**Assinatura visual:** **compasso e tabuinha** — a geometria como identidade.
**Psicológico:** repete Platão sem ter entendido. Desprezo por Kleia é herdado, não pensado; por isso desmonta no Ato II quando ela o supera. Vira aliado no ato final.
**Paleta:** `gold_light #F8E058` · `gold_mid #D8A818` · `marble_light #F8F8F0` · `hair_dark #382828`

---

### A PÍTIA — Delfos

**Idade:** indeterminada (o sprite não deve deixar claro).
**Físico:** sentada durante toda a cena, sobre a trípode. Nunca em pé, nunca caminhando — **não precisa de animação de walk**. Rosto parcialmente coberto por véu; olhos não visíveis.
**Vestimenta:** manto branco com sobreposição translúcida roxa (usar dithering 50% entre `marble_light` e `purple_mid` para simular transparência — o SNES não tem alfa real).
**Assinatura visual:** **vapor** subindo da fenda no chão, animação contínua de 6 frames.
**Paleta:** `marble_light #F8F8F0` · `purple_mid #8838A0` · `aegean_light #48C8E0` (vapor)

---

### ARISTEU — pai de Kleia

**Idade:** 52. Oleiro. Mãos deformadas por artrite — **detalhe obrigatório no retrato**, os dedos tortos são o motivo pelo qual Kleia precisa ir à ágora. Quíton marrom manchado. Barba grisalha curta. Nunca aparece em batalha; apenas em cenas na oficina. Paleta `ochre_shade` / `ochre_dark` / `marble_dark`.

---

### CIDADÃO (NPC genérico de plateia)

Sprite modular: **um corpo, quatro cabeças, três cores de manto** = 12 variantes com 1 sheet. Manto em tons neutros (`marble_mid`, `marble_shade`, `ochre_shade`) para nunca competir visualmente com os debatedores. Carregam ânfora, cesto ou nada.

---

## 6. ESPECIFICAÇÃO DE TILES

**Grid:** losango 32 × 16 px (2:1). Blocos: 32 × (16 + altura).
**Pivot:** canto inferior-esquerdo do bounding box. **Sorting:** profundidade `gx + gy` crescente em pixel (y para baixo); na Unity, com Y para cima, `IsoMath.SortingOrder` é negativo.
**Regra dura:** o contorno de todo tile de chão é `outline_blue #101828`; o de props é `outline #080810`.

### 6.1 Cerâmico (Ato I, início)

| Tile | Dimensão | Descrição |
|---|---|---|
| `floor_packed_dirt` | 32×16 | Terra ocre escura, 3 tons, pequenas pedras em `ochre_dark`. Ruído de 4–6 px isolados, nunca agrupados. |
| `floor_dry_clay` | 32×16 | Ocre claro rachado. Trincas como linhas de 1 px em `ochre_dark`, 2 por tile no máximo. |
| `floor_straw` | 32×16 | Palha espalhada, riscos diagonais de 3 px em `gold_mid` sobre terra. |
| `block_kiln` | 32×40 | Forno de olaria em tijolo. Abertura frontal com brasa animada (2 frames). |
| `prop_potters_wheel` | 32×28 | Roda de oleiro. Animação de giro: 4 frames. |
| `prop_amphora_stack` | 32×32 | Pilha de 5 ânforas. Três variantes de arranjo. |
| `wall_brick` | 32×32 | Tijolo cru, juntas visíveis de 1 px. |

### 6.2 Ágora (Ato I, principal)

| Tile | Dimensão | Descrição |
|---|---|---|
| `floor_marble` | 32×16 | Mármore branco polido. Veio sutil: 1 linha curva de 1 px em `marble_shade`, em 1 a cada 4 tiles. |
| `floor_marble_ochre` | 32×16 | Mesmo tile em ocre. Alternar em xadrez cria o padrão do mockup. |
| `floor_mosaic_meander` | 32×16 | Grega (meandro) em `aegean_mid` sobre mármore. **Deve encaixar com os 4 vizinhos** — desenhar como conjunto de 4, não isolado. |
| `floor_worn_stone` | 32×16 | Pedra cinza irregular, para becos. |
| `block_doric_column` | 32×56 | Fuste canelado: 5 caneluras verticais de 1 px, capitel simples. Sem base (dórica). |
| `block_broken_column` | 32×28 | A mesma, partida ao meio, topo irregular. |
| `block_step` | 32×24 | Degrau de 8 px de altura. Encaixável em série de 3. |
| `prop_market_stall` | 32×28 | Banca de madeira com toldo listrado `red_mid`/`marble_light`. |
| `prop_herm_statue` | 32×48 | Herma: pilar retangular com cabeça barbada no topo. |
| `prop_olive_tree` | 48×56 | **Maior que o tile** — ocupa 1 célula mas transborda. Copa em 3 tons de oliva, tronco retorcido. Animação de vento: 3 frames, 400 ms cada. |
| `prop_fountain` | 32×36 | Fonte pública. Água animada em 4 frames. |

### 6.3 Academia (Ato II)

| Tile | Dimensão | Descrição |
|---|---|---|
| `floor_garden_grass` | 32×16 | Grama oliva, tufos de 2 px. |
| `floor_gravel_path` | 32×16 | Cascalho claro, ruído denso de 1 px. |
| `floor_marble_blue` | 32×16 | Mármore com veio azul-egeu. Interiores da escola. |
| `floor_diagram` | 32×16 | **Tile especial:** figura geométrica riscada no chão (círculo, triângulo, quadrado). 3 variantes. Marca as salas de geometria. |
| `block_scroll_shelf` | 32×40 | Nichos com rolos de papiro. Ocre e branco. |
| `prop_ancient_olive_tree` | 48×64 | Versão maior e mais retorcida da oliveira. |
| `block_portico` | 32×48 | Coluna jônica (com volutas no capitel) — diferenciar da dórica da ágora. |

### 6.4 Delfos (Ato II, final)

| Tile | Dimensão | Descrição |
|---|---|---|
| `floor_mountain_rock` | 32×16 | Rocha cinza-azulada, alto contraste. |
| `floor_snow` | 32×16 | Neve com sombra azul-fria. Usar `aegean_dark` na sombra, nunca cinza. |
| `floor_rift` | 32×16 | Fenda escura com brilho interno. Vapor emerge daqui. |
| `block_delphic_treasury` | 32×48 | Pequeno templo-cofre em mármore, frontão triangular. |
| `prop_tripod` | 32×32 | Trípode de bronze, assento da Pítia. |
| `prop_omphalos` | 32×24 | A pedra-umbigo: ovoide com rede esculpida. |
| `overlay_vapor` | 32×32 | Vapor translúcido por dithering. 6 frames, 150 ms. |

### 6.5 Overlays de sistema

| Tile | Cor | Uso |
|---|---|---|
| `fog` | `#101020` | Névoa de guerra. Borda em dithering 50% para transição. |
| `fog_edge_ne/nw/se/sw` | — | 4 variantes de canto para bordas suaves. |
| `hl_move` | `#48C8E0` @ 55% | Alcance de CIRCULAR |
| `hl_refute` | `#F87858` @ 55% | Alcance de REFUTAR |
| `hl_maieutics` | `#C078E0` @ 55% | Alcance de MAIÊUTICA |
| `hl_select` | `#F8E058` @ 70% | Cursor. Pulso de 2 frames, 500 ms. |

---

## 7. ESPECIFICAÇÃO DE ANIMAÇÕES

**Canvas do personagem:** 24 × 32 px. Pés na linha y = 30. Pivot: (12, 30).
**Direções:** SE, SW, NE, NW. **SW e NW são espelhos** de SE e NE — gere apenas 2 direções e espelhe em código, economiza metade do trabalho.

| Animação | Frames | Duração/frame | Loop | Quem usa |
|---|---|---|---|---|
| `idle` | 2 | 600 ms | sim | todos |
| `idle_breath` | 4 | 400 ms | sim | apenas debatedores em batalha (peito sobe 1 px) |
| `walk` | 4 | 150 ms | sim | todos exceto Pítia |
| `talk` | 3 | 200 ms | sim | todos (boca + mão) |
| `refute` | 5 | 120 ms | não | debatedores — braço avança, dedo aponta, recuo |
| `maieutics` | 6 | 180 ms | não | debatedores — ambas as mãos abrem, cabeça inclina |
| `concede` | 4 | 250 ms | não | debatedores — ombros caem, palmas para cima |
| `quote` | 4 | 200 ms | não | quem tem rolo/tabuinha |
| `coherence_damage` | 3 | 100 ms | não | todos — recuo de 2 px + flash branco no frame 1 |
| `defeat` | 6 | 220 ms | não | todos — joelhos dobram, cabeça baixa, pose final mantida |
| `victory` | 4 | 300 ms | não | apenas Kleia e bosses |
| `think` | 3 | 500 ms | sim | Menexeno, Eudoro (ociosos no cenário) |

### Notas de animação

- **Flash de dano:** substituir toda a paleta do sprite por `marble_light` por 1 frame. É o efeito 16-bit clássico e custa nada.
- **Sem interpolação.** Toda animação em Unity deve usar curvas *Constant*, não *Linear*. Caso contrário o pixel art treme.
- **Movimento na grade:** 250 ms por célula, com `walk` rodando. Movimento linear entre centros de célula, sem easing.
- **Balanço vertical (bob):** 1 px para cima nos frames 1 e 3 da caminhada. Nunca 2 px — vira pulo.
- **A Pítia não caminha.** Só `idle`, `talk` e uma animação especial `trance` de 8 frames.

---

## 8. ESPECIFICAÇÃO DE UI

| Elemento | Dimensão | Notas |
|---|---|---|
| `box_menu` | 96×88 | Borda dupla branca sobre `ui_background #181828`. Cantos vazados 1 px (estilo do mockup). |
| `box_dialogue` | 208×48 | Ocupa a largura quase toda, base da tela. |
| `bar_coherence` | 64×8 | Preenchimento `aegean_mid`, brilho de topo `aegean_light`. |
| `bar_audience` | 64×8 | Preenchimento `gold_mid`, brilho `gold_light`. |
| `frame_portrait` | 40×40 | Borda dourada dupla. |
| `cursor` | 8×8 | Triângulo branco. Pisca a 500 ms. |
| `icon_fallacy` | 16×16 | Um ícone por falácia (20 no total). Aparece sobre a cabeça do oponente quando revelado. |
| `meter_jurors` | 200×16 | Exclusivo do Tribunal: 501 pixels-voto, 1 px por jurado. |

**Fonte:** pixel bitmap, altura 8 px, com acentuação completa em português (ã õ ç á é í ó ú â ê ô à). Testar `Ç` e `õ` antes de fechar a fonte — são os que quebram.

---

## 9. TEMPLATE DE PROMPT PARA GERADOR DE SPRITE

Use em inglês; os modelos respondem melhor. Substitua os campos entre colchetes.

```
16-bit SNES pixel art sprite, isometric 2:1 perspective, [24x32] pixels,
character: [physical description + clothing from the character sheet],
holding [signature prop],
facing [south-east / north-east],
front-facing 3/4 view, full body, standing idle pose,
palette: strictly limited to 15 colors, RGB555 (all channel values multiples of 8),
dominant colors [#48C8E0, #2890C0, #F8D0A8, #382828, #D89038],
hard black outline #080810, no anti-aliasing, no gradients, no dithering on skin,
2 shading tones maximum per material, high contrast lighting from upper-left,
transparent background, centered, feet at bottom edge,
crisp pixel edges, nearest neighbor, single sprite, no text, no border, no frame
```

**Negativos obrigatórios:**
```
anti-aliasing, blur, gradient, soft shading, 3d render, painterly,
drop shadow, glow, outline glow, watermark, text, multiple characters,
modern clothing, photorealistic, smooth edges
```

**Para tiles**, trocar as duas primeiras linhas por:
```
16-bit SNES pixel art isometric floor tile, diamond shape, exactly 32x16 pixels,
2:1 isometric projection, seamlessly tileable on all four edges,
```

### Pós-processamento obrigatório

Nenhum gerador entrega RGB555 limpo. Sempre rodar depois:

1. **Quantizar** para a paleta `art/palette/snes.gpl` (versionada no repositório) — no Aseprite: *Sprite → Color Mode → Indexed*, com a paleta carregada e dithering **desligado**.
2. **Redimensionar** para o tamanho exato com Nearest Neighbor.
3. **Verificar contagem de cores** — máximo 15 + transparente por sprite.
4. **Verificar encaixe** de tiles colando 4 cópias lado a lado.

---

## 10. CHECKLIST TÉCNICO UNITY

- [ ] Pixel Perfect Camera: referência 224×224 (ou 256×224), PPU 16, *Upscale Render Texture* ligado, *Crop Frame* X e Y
- [ ] Todo sprite: *Filter Mode* = Point, *Compression* = None, *Pixels Per Unit* = 16
- [ ] Grid: *Isometric Z as Y*, Cell Size (1, 0.5, 1)
- [ ] `TileData[,]` como fonte de verdade — Tilemap é só render
- [ ] Sorting por profundidade `gx + gy` (na Unity, com Y para cima, `IsoMath.SortingOrder` é negativo), com `TilemapRenderer` em modo *Individual*
- [ ] Animator com curvas *Constant* em todos os clipes
- [ ] `GameStateMachine`: Exploration ↔ Battle na **mesma cena**, sem load
- [ ] Névoa: Tilemap sobreposto + bitmask serializado no save
- [ ] `ActFlag` (1 ou 2) controla portas **e** o pool de adversários gerados
- [ ] Cliente LLM: timeout 2 s, cache por seed, fallback local por `fallacy_id`
- [ ] Nenhuma decisão de regra vem do LLM — apenas texto e etiqueta
