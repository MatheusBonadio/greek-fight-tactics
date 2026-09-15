# Greek Fight Tactics (GFT)

Tactics isométrico de dialética na Grécia Antiga. Unity 2D URP, pixel art 16-bit, batalha por turnos vencida no argumento.

## Versões fixadas

| Ferramenta | Versão |
|---|---|
| Unity Editor | `6000.6.0f1` (revisão `f7f8ed4d1e24`) |
| Unity CLI | `1.0.0-beta.8` |
| Template | Universal 2D (`com.unity.template.universal-2d` 7.0.0) |
| Git LFS | 3.6.1 |

O patch do editor está congelado em `ProjectSettings/ProjectVersion.txt` até a entrega acadêmica. Nenhum upgrade antes disso.

## Preparar a máquina

```powershell
winget install Unity.CLI
unity auth login
unity install 6000.6.0f1
unity install-modules --editor-version 6000.6.0f1 --module webgl windows-il2cpp --accept-eula
unity editors default 6000.6.0f1
```

## Clonar

```powershell
git lfs install
git clone <url> GFT
cd GFT
unity projects add .
unity vcs hooks install
unity vcs merge-setup
unity vcs doctor
```

## Testar e compilar

```powershell
unity test . --mode EditMode --output artifacts/editmode.xml -- -nographics
unity test . --mode PlayMode --output artifacts/playmode.xml
unity build . --profile "Windows Development" --output-path Build/Windows/GFT.exe
unity build . --profile "Web" --output-path Build/Web
```

O fluxo inteiro é por CLI, sem Editor. Arquitetura e convenções em [CLAUDE.md](CLAUDE.md), design em [docs/GDD.md](docs/GDD.md), tarefas em [docs/trello_board.json](docs/trello_board.json).
