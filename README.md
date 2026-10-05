# Greek Fight Tactics

Um jogo de estratégia por turnos em que as batalhas são vencidas com argumentos, não com espadas.

## A história

Atenas, 402 a.C. A cidade acabou de se livrar de um governo de tiranos e ainda desconfia de todo mundo.

Kleia tem dezessete anos e é filha de um oleiro do Cerâmico, o bairro dos ceramistas. Ela não pode votar, não pode estudar na Academia e não pode se defender num tribunal. Passou a vida pintando ânforas no chão da oficina enquanto os homens discutiam em volta dela, e aprendeu a discutir só de ouvir.

Ninguém proibiu uma garota de fazer uma pergunta.

## Como se joga

Você anda pela cidade num mapa de pixel art visto na diagonal. Os bairros vão aparecendo conforme você explora. As pessoas conversam, contam o que está acontecendo e, às vezes, dizem bobagens em público.

Quando alguém diz uma bobagem, Kleia pode desafiá-lo para um debate. O público se junta em volta, e o debate acontece ali mesmo, no meio da rua.

No debate, cada lado tem duas barras:

- **Coerência** é o quanto o raciocínio de cada um aguenta. Se a de Kleia chegar a zero, ela perde.
- **Plateia** mostra de que lado o público está. Se o público inteiro ficar com Kleia, ela vence.

A cada rodada, o adversário fala algo com um erro escondido, e você escolhe como responder:

- **Refutar** é apontar o erro de raciocínio, a falácia que ele cometeu.
- **Perguntar** (a maiêutica de Sócrates) é fazer a pergunta que mostra que uma das ideias em que ele se apoia é falsa.
- **Conceder** é aceitar o ponto para recuperar o fôlego.
- Também dá para andar pela arena, citar os filósofos antigos ou desistir.

Cada fala tem uma resposta certa. Se o erro está no raciocínio, só refutar funciona. Se a fala se apoia numa ideia falsa, só a pergunta funciona. Responder do jeito errado sai caro, então é preciso entender o argumento antes de reagir.

Nas opções há um **modo assistido**, que mostra qual é a resposta certa em cada rodada.

## A jornada

**Ato I: O Cerâmico e a Ágora.** Kleia descobre que sabe discutir. Ela enfrenta charlatães que vendem "a arte de vencer qualquer discussão", e cada um ensina um tipo de falácia. Um velho misterioso se oferece para ensiná-la a perguntar, de graça. A fama dela cresce, e com a fama vem o perigo: começam a chamá-la de ímpia, a mesma acusação que um dia vai condenar Sócrates. O ato termina num julgamento na escadaria da ágora.

**Ato II: A Academia e o Oráculo.** Kleia encontra filósofos de verdade, que apresentam paradoxos em vez de erros. Agora apontar falácias não basta, porque não há falácia nenhuma. A história termina num tribunal com 501 jurados, e a última jogada de Kleia decide qual dos três finais você vê. O Ato II ainda está em construção.

## Visual e som

Pixel art no estilo dos jogos de 16 bits, com a paleta limitada dos consoles da época, e trilha sonora original.

## Equipe

- Arte e game design: Matheus Bonadio (840474)
- Música e efeitos sonoros: Victor Vicentini (840763)

## Para rodar e compilar

O jogo é feito na Unity `6000.6.0f1`, e tudo roda pela linha de comando da Unity (Unity CLI):

```powershell
unity install 6000.6.0f1
./tools/test.ps1 -Mode All
./tools/build.ps1 -BuildProfile "Windows Development"
```

O build sai em `Build/WindowsDevelopment/GFT.exe`.

- [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md): guia técnico completo, com versões, preparação da máquina, ferramentas de arte e integração contínua.
- [docs/GDD.md](docs/GDD.md): documento de design, com a história completa e as regras.
- [CLAUDE.md](CLAUDE.md): arquitetura do código.
