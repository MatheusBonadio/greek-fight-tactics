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

O CLI também fica fixo. O CI baixa exatamente a `1.0.0-beta.8` por `tools/install-cli.ps1`, que confere o SHA-256 gravado no script. Atualizar é sempre deliberado: `unity self-update --target <versão>` na máquina, depois a versão e o hash novos em `tools/install-cli.ps1` e nesta tabela, e `unity skill install claude-code --local --force` para regravar a skill do CLI no projeto. No `SKILL.md` regravado, apague o `allowed-tools` do cabeçalho: ele pré-aprova o Bash em toda sessão com a skill ativa. O `unity skill refresh` não desfaz essa edição sem `--force`. O `winget` instala a versão mais recente, então não use `winget upgrade` no CLI.

## Preparar a máquina

```powershell
& ([scriptblock]::Create((irm https://public-cdn.cloud.unity3d.com/hub/prod/cli/install.ps1))) -Target 1.0.0-beta.8
unity auth login
unity install 6000.6.0f1
unity install-modules --editor-version 6000.6.0f1 --module webgl windows-il2cpp --accept-eula
unity editors default 6000.6.0f1
```

### Claude Code

A skill do CLI já vem no repositório em `.claude/skills/unity-cli`, gerada por `unity skill install claude-code --local`. O servidor MCP do Editor também: `.mcp.json` na raiz registra `unity-editor-mcp` (stdio, `unity mcp`) no escopo do projeto, sem segredo. Na primeira sessão do Claude Code no projeto, aprove o servidor quando ele perguntar e confira:

```powershell
claude mcp get unity-editor-mcp
```

Não rode `unity mcp configure claude-code`. Ele grava a mesma entrada no escopo do usuário, porque o CLI recusa `--local` para o Claude Code.

A skill é genérica e manda dirigir o Editor aberto em vez de editar cena e asset. Neste projeto vale o [CLAUDE.md](CLAUDE.md): cena, prefab e asset só saem das ferramentas de `Assets/_Game/Editor/`, e o Editor conectado serve para inspecionar e verificar.

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

O workflow `.github/workflows/ci.yml` roda em runner Windows. `functions-tests` usa sempre o `windows-latest`. Os jobs Unity usam o runner da variável `UNITY_RUNNER` (veja [Licença do Editor](#licença-do-editor)).

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
| `UNITY_SERIAL` | só no runner hospedado | serial de uma licença paga com assento (Pro ou equivalente) |

A conta de serviço é criada no Unity Cloud Dashboard, em **Administration → Service accounts**, com uma chave nova. Ela nunca é a sua conta pessoal. O CI valida a chave com `unity auth login --secret-from-stdin --no-store` e usa as variáveis de ambiente do mesmo nome, sem gravar credencial no runner.

### Licença do Editor

**A Unity não ativa licença do Editor para conta de serviço.** Com a conta de serviço, `unity license activate` e `--personal` falham de propósito. O passo de licença olha `unity license status` e segue um de dois caminhos:

- **Já existe licença ativa no runner:** a ativação é pulada e a licença fica onde está. O job não a devolve.
- **Não existe licença:** o job ativa `UNITY_SERIAL` e, no fim, devolve só esse assento, mesmo com falha ou cancelamento. Sem o segredo, o job falha dizendo isso.

Arquivo `.ulf` não serve no `windows-latest`. A Personal não tem ativação manual, e o `.ulf` fica preso ao hardware da máquina que o gerou, enquanto cada run ganha uma VM nova.

**Este projeto só tem a Personal**, então os jobs Unity rodam num runner self-hosted nesta máquina, que já tem a licença ativa:

1. Em **Settings → Actions → Runners → New self-hosted runner**, escolha Windows x64 e siga os comandos. No `config.cmd`, dê o rótulo `unity`.
2. Rode o runner com o seu usuário do Windows: `run.cmd` num terminal, ou o serviço com a sua conta em vez de `NETWORK SERVICE`. A Personal fica em `%LOCALAPPDATA%\Unity\licenses`, que é por usuário.
3. Em **Settings → Secrets and variables → Actions → Variables**, crie `UNITY_RUNNER` com `["self-hosted", "windows", "unity"]`. Sem a variável, os jobs Unity usam `"windows-latest"`.
4. Use runner self-hosted só com o repositório privado. Num público, o PR de um fork rodaria código nesta máquina.

Se a Personal vencer, o passo de licença falha. Renove na máquina com `unity license activate --personal --accept-eula`, logado com a sua conta.

**Nesse caminho, quem licencia o Editor é a sua conta pessoal.** A conta de serviço autentica só o CLI; a licença que o Editor valida é a Personal da sua conta, guardada pelo cliente de licença do seu usuário. O "nunca conta pessoal" do card [CLI-02] só se cumpre por inteiro com um assento pago em `UNITY_SERIAL` ou com um servidor de licença flutuante.

O que já foi conferido nesta máquina, com credenciais fictícias nas variáveis da conta de serviço:

- `unity --format json license status` continua listando a Personal, com `authMode` `service-account`, e sai com 0. O passo de licença, então, não tenta ativar nada.
- `unity test` passa ao Editor o `-hubSessionId`, mas não o `-accessToken`. O Editor registra `Access token is unavailable`, valida a Personal pelo cliente de licença e os testes rodam. Sem as variáveis, o CLI passa o token da sua sessão.

Com uma conta de serviço de verdade isso ainda não rodou. Com credenciais válidas, o CLI pode passar ao Editor um token da conta de serviço. O servidor de licença recusa esse token na ativação, e ninguém testou o que ele faz na validação da Personal. No primeiro run, procure as linhas `[Licensing::` no log do `unity-tests`. Se o Editor recusar a licença, o `unity test` sai com 6 e o job falha.

### Proteção da `main`

O ruleset é configuração do GitHub, não fica no repositório. Enquanto ninguém o criar, um check vermelho não bloqueia push nem merge na `main`.

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

Rulesets em repositório privado exigem GitHub Pro ou Team. O GitHub Pro vem de graça no GitHub Student Developer Pack. Cada mudança roda a suíte Unity duas vezes (branch `ci` e `main`). No runner self-hosted isso não gasta minutos do plano. No `windows-latest`, cada minuto conta em dobro.
