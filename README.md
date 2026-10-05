<h1 align="center">
    <img alt="Greek Fight Tactics" src="docs/images/banner.png" width="100%">
</h1>

<p align='center'>
  <img alt='Unity' src='https://img.shields.io/badge/Unity-000000?style=for-the-badge&logo=unity&logoColor=white' />
  <img alt='C#' src='https://img.shields.io/badge/C%23-512BD4?style=for-the-badge&logo=dotnet&logoColor=white' />
  <img alt='SQLite' src='https://img.shields.io/badge/SQLite-003B57?style=for-the-badge&logo=sqlite&logoColor=white' />
  <img alt='Python' src='https://img.shields.io/badge/Python-3776AB?style=for-the-badge&logo=python&logoColor=white' />
</p>

## 💻 Projeto

**Greek Fight Tactics** é um jogo de estratégia por turnos, em pixel art no estilo dos 16 bits, em que as batalhas são vencidas com argumentos, não com espadas.

Atenas, 402 a.C. Kleia tem dezessete anos e é filha de um oleiro. Ela não pode votar, não pode estudar na Academia e não pode se defender num tribunal, mas ninguém proibiu uma garota de fazer uma pergunta. Ela percorre a cidade desafiando charlatães e sofistas em debates públicos e expondo, diante da plateia, os erros de raciocínio de cada um.

<img src="docs/images/battle.png" width="100%">

### ✨ Principais Funcionalidades

- 🗣️ **Debates por turnos**: cada fala do adversário esconde um erro, e você escolhe refutar, perguntar, conceder, circular pela arena, citar os antigos ou se retirar
- 🎯 **Uma resposta certa por fala**: se o erro está no raciocínio, só a refutação funciona; se a fala se apoia numa ideia falsa, só a pergunta, a maiêutica de Sócrates
- 📊 **Coerência e Plateia**: vence quem zerar a Coerência do outro ou conquistar o público inteiro, e a plateia por perto fortalece ou enfraquece cada golpe
- 🗺️ **Exploração isométrica**: bairros de Atenas com relevo e uma névoa que se revela conforme Kleia anda
- 📖 **Compêndio de falácias**: cada falácia exposta entra num livro, com explicação e dica
- 🧭 **Modo assistido**: mostra qual é a resposta certa em cada rodada
- 🎭 **História com escolhas**: diálogos, cenas e decisões que mudam o caminho de Kleia até o julgamento na ágora
- 💾 **Saves locais**: três espaços de save guardados no próprio computador, sem precisar de internet
- 🎨 **Pixel art e trilha original**: paleta de 16 bits e música composta para o jogo

## 📦 Instalação e Execução

### Para jogar

1. Baixe o `GFT-windows-x64.zip` na página de [Releases](https://github.com/MatheusBonadio/greek-fight-tactics/releases).
2. Extraia o arquivo em qualquer pasta.
3. Abra o `GFT.exe`.

Requer Windows 10 ou 11 de 64 bits.

### Pré-requisitos para compilar

- Windows 10 ou 11 de 64 bits
- PowerShell 7
- Git com Git LFS
- Unity CLI `1.0.0-beta.8` e Unity Editor `6000.6.0f1`

### Passos de Instalação

```powershell
# Clone o repositório
git lfs install
git clone git@github.com:MatheusBonadio/greek-fight-tactics.git

# Entre no diretório do projeto
cd greek-fight-tactics

# Instale a Unity CLI e o editor na versão do projeto
& ([scriptblock]::Create((irm https://public-cdn.cloud.unity3d.com/hub/prod/cli/install.ps1))) -Target 1.0.0-beta.8
unity auth login
unity install 6000.6.0f1

# Rode os testes
./tools/test.ps1 -Mode All

# Gere o executável em Build/WindowsRelease/GFT.exe
./tools/build.ps1 -BuildProfile "Windows Release"
```

O guia técnico completo está em [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md), a história e as regras em [docs/GDD.md](docs/GDD.md) e a arquitetura do código em [CLAUDE.md](CLAUDE.md).

## 👥 Equipe

- **Matheus Bonadio** (840474): arte e game design
- **Victor Vicentini** (840763): música e efeitos sonoros

## 📄 Licença

Este projeto ainda não tem uma licença definida. As fontes e bibliotecas de terceiros seguem as próprias licenças: Ark Pixel Font e Liberation Sans (SIL Open Font License 1.1), Newtonsoft.Json, SQLite-net e SQLite3 Multiple Ciphers (MIT) e os pacotes da Unity (Unity Companion License).
