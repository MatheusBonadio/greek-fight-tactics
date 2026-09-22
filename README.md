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

## Ferramentas de arte

Scripts Python 3 com Pillow em `tools/art/`. Não abrem a Unity; as regras que aplicam estão na skill `pixel-art-pipeline`.

```powershell
python -m pip install -r tools/art/requirements.txt
python tools/art/quantize.py <pasta-de-pngs-brutos> --out <pasta-de-saida> --like art/placeholders
python tools/art/verify.py art/placeholders
python tools/art/tile_preview.py art/placeholders/tiles/floor
python tools/art/aseprite_palette.py
python tools/art/aseprite_palette.py --check
python -m unittest discover -s tools/art
```

- `quantize.py` redimensiona cada PNG com Nearest Neighbor para a dimensão exata e o leva à paleta `art/palette/snes.gpl` sem dithering, com no máximo 15 cores. A dimensão vem de `--size`, do placeholder de mesmo nome (`--like`) ou do prefixo do arquivo.
- `verify.py` imprime a contagem de cores de cada arquivo e sai com código 1 quando algum PNG quebra uma regra: mais de 15 cores + transparente, alfa parcial, canal fora do RGB555, cor fora da paleta, dimensão errada para o prefixo, chão, névoa ou realce com pixel opaco fora do losango, ou chão que deixa vão ao se repetir. `--colors` lista as cores com o nome na paleta, e `--skip <regra>` ignora uma regra.
- `tile_preview.py` cola 4 cópias do tile na grade isométrica, exporta a prévia ampliada em `artifacts/art/tiling/` e sai com código 1 se um tile de 32×16 tiver pixel opaco fora do losango ou deixar vão.
- `aseprite_palette.py` põe a `snes.gpl` como paleta padrão do Aseprite na pasta do usuário. O `--check` sai com 0 só quando o Aseprite já a gravou como `default.ase`.

O fluxo inteiro é por CLI, sem Editor. Arquitetura e convenções em [CLAUDE.md](CLAUDE.md), design em [docs/GDD.md](docs/GDD.md), tarefas em [docs/trello_board.json](docs/trello_board.json).
