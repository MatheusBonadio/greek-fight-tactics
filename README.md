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

O CLI também fica fixo. O CI baixa exatamente a `1.0.0-beta.8` por `tools/install-cli.ps1`, que confere o SHA-256 gravado no script. Atualizar é sempre deliberado: `unity self-update --target <versão>` na máquina, depois a versão e o hash novos em `tools/install-cli.ps1` e nesta tabela, e `unity skill install claude-code --local --force` para regravar a skill do CLI no projeto. O `winget` instala a versão mais recente, então não use `winget upgrade` no CLI.

## Preparar a máquina

```powershell
& ([scriptblock]::Create((irm https://public-cdn.cloud.unity3d.com/hub/prod/cli/install.ps1))) -Target 1.0.0-beta.8
unity auth login
unity install 6000.6.0f1
unity install-modules --editor-version 6000.6.0f1 --module webgl windows-il2cpp --accept-eula
unity editors default 6000.6.0f1
```

### Claude Code

A skill do CLI já vem no repositório em `.claude/skills/unity-cli`, gerada por `unity skill install claude-code --local`. O servidor MCP do Editor é registrado uma vez por máquina, no escopo do usuário, porque o CLI grava a entrada com `claude mcp add --scope user`:

```powershell
unity mcp configure claude-code
claude mcp list
```

A entrada se chama `unity-editor-mcp` e roda `unity mcp`. Não precisa de segredo.

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
./tools/test.ps1 -Mode All
./tools/build.ps1 -BuildProfile "Windows Development"
```

Os scripts chamam `unity test` e `unity build` com `--format json` e tratam o código de saída. Com `-Format github`, a falha vira anotação no GitHub Actions. Os comandos diretos continuam valendo:

```powershell
unity test . --mode EditMode --output artifacts/editmode.xml -- -nographics
unity test . --mode PlayMode --output artifacts/playmode.xml
unity build . --profile "Windows Development" --output-path Build/Windows/GFT.exe
unity build . --profile "Web" --output-path Build/Web
```

O fluxo inteiro é por CLI, sem Editor. Arquitetura e convenções em [CLAUDE.md](CLAUDE.md), design em [docs/GDD.md](docs/GDD.md), tarefas em [docs/trello_board.json](docs/trello_board.json).

## Integração contínua

O workflow `.github/workflows/ci.yml` roda em runner Windows.

| Job | Quando | O que faz |
|---|---|---|
| `functions-tests` | todo push e todo PR para a `main` | `npm ci`, `npm run typecheck` e `npm test` em `functions/` |
| `unity-tests` | todo push e todo PR para a `main` | instala o CLI fixo e o Editor do `ProjectVersion.txt` com `unity install`, restaura a `Library` pela chave de `unity cache key` e roda `tools/test.ps1` em EditMode e PlayMode com `-Format github` |
| `windows-build` | push ou disparo manual na `main`, depois dos dois jobs acima | `tools/build.ps1` no perfil Windows Development, `tools/scan-secrets.ps1` no player e upload do artefato `GFT-WindowsDevelopment-<sha>` (14 dias) |

O ambiente fixa `UNITY_NON_INTERACTIVE`, `UNITY_NO_UPDATE_CHECK`, `UNITY_BUILD_TIMEOUT` e `UNITY_TEST_TIMEOUT` (45 min), e cada job tem `timeout-minutes`, para nada ficar pendurado num prompt.

### Segredos

Em **Settings → Secrets and variables → Actions → New repository secret**:

| Segredo | Obrigatório | Conteúdo |
|---|---|---|
| `UNITY_SERVICE_ACCOUNT_ID` | sim | ID da chave da conta de serviço |
| `UNITY_SERVICE_ACCOUNT_SECRET` | sim | segredo da chave da conta de serviço |
| `UNITY_SERIAL` | um dos dois | serial de uma licença Unity com assento (Pro, Plus ou equivalente) |
| `UNITY_LICENSE` | um dos dois | conteúdo de um arquivo de licença `.ulf` |

A conta de serviço é criada no Unity Cloud Dashboard, em **Administration → Service accounts**, com uma chave nova. Ela nunca é a sua conta pessoal. O CI valida a chave com `unity auth login --secret-from-stdin --no-store` e usa as variáveis de ambiente do mesmo nome, sem gravar credencial no runner.

**A Unity não ativa licença do Editor para conta de serviço.** Com a conta de serviço, `unity license activate` e `--personal` falham de propósito. Por isso o Editor precisa de `UNITY_SERIAL` ou `UNITY_LICENSE`. Sem nenhum dos dois, o job falha com essa mensagem. A licença Personal desta máquina não serve, porque só ativa com login de usuário. Se não houver serial nem `.ulf`, a alternativa é registrar esta máquina como runner self-hosted e trocar `runs-on` dos jobs Unity: o passo de licença pula a ativação quando já existe licença ativa. Ao fim de cada job, `unity license return --yes` devolve o assento.

### Proteção da `main`

Em **Settings → Rules → Rulesets → New branch ruleset**:

1. Enforcement status **Active**, alvo **Include default branch**.
2. Ligue **Require status checks to pass** e adicione `unity-tests` e `functions-tests`. Eles só aparecem na busca depois da primeira execução do workflow.
3. Mantenha **Block force pushes**.
4. Não coloque ninguém em **Bypass list**.

O ruleset vale também para push direto, não só para PR: a `main` só aceita um commit que já passou nos checks. Como o trabalho é direto na `main`, o commit passa antes por um branch de CI e só depois sobe para a `main`:

```powershell
git push origin HEAD:refs/heads/ci --force
git push origin HEAD:main
```

Rode o segundo comando quando o run do branch `ci` ficar verde na aba **Actions**. Se ele estiver vermelho, o GitHub recusa o push na `main`.

Rulesets em repositório privado exigem GitHub Pro ou Team. O GitHub Pro vem de graça no GitHub Student Developer Pack. Runner Windows conta em dobro nos minutos do plano, e cada mudança roda a suíte Unity duas vezes (branch `ci` e `main`).
