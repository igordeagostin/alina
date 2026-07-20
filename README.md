# Alina

Assistente pessoal de desenvolvimento estilo "Jarvis" em .NET. A Alina entende
seus pedidos, decide qual ferramenta usar e executa ações (terminal, arquivos, e —
nas próximas fases — Claude Code, git, etc.), pedindo confirmação para operações
críticas.

> Estado atual: **Fases 1, 2, 3, 5 e 6 concluídas**. Chat por texto e por **voz** (push-to-talk),
> orquestrador com LLM e function-calling, histórico persistido, a tool do **Claude Code**
> (delega tarefas ao `claude` CLI em modo headless), ferramentas de **Git** (status, diff, log,
> commit, branch), **memória permanente que aprende**, **plugins** declarativos e **execução em
> background** (tarefas longas rodam sem travar a conversa).

## Arquitetura

```
Alina.slnx
  src/
    Alina.Core            # Orquestrador, Models, Memory (abstrações), ITool
    Alina.Tools           # Tools: Terminal, FileRead, ClaudeCode, Git (status/diff/log/commit/branch)
    Alina.Voice           # STT (Whisper), TTS (gpt-4o-mini-tts), áudio via NAudio
    Alina.Infrastructure  # IChatClient (Microsoft.Extensions.AI), config, stores JSON
    Alina.Console         # UI de console (REPL + confirmação)
  tests/
    Alina.Tests           # xUnit
```

O LLM é abstraído via **Microsoft.Extensions.AI** (`IChatClient`), com **OpenAI como
provider default** e o provider trocável por configuração. O loop de tool-calling é
resolvido pelo pipeline `UseFunctionInvocation()`.

## Pré-requisitos

- .NET SDK 10
- Uma chave de API de LLM (OpenAI por padrão)

## Configuração

A chave **não** deve ser commitada. Configure via user-secrets no projeto do console:

```powershell
cd src/Alina.Console
dotnet user-secrets set "Llm:ApiKey" "<sua-chave>"
# opcional: trocar o modelo
dotnet user-secrets set "Llm:Model" "gpt-4o-mini"
```

Também é possível configurar via `appsettings.json` (seções `Llm` e `Storage`) ou
variáveis de ambiente (ex: `Llm__ApiKey`).

Memória permanente (preferências, convenções de código) é lida de um arquivo Markdown.
Por padrão fica em `%APPDATA%/Alina/preferences.md` — veja
[`docs/preferences.example.md`](docs/preferences.example.md).

## Executando

```powershell
dotnet run --project src/Alina.Console
```

Comandos do REPL: `/voz`, `/memorias`, `/plugins`, `/tarefas`, `/tarefa <id>`, `/cancelar <id>`, `/nova`, `/historico`, `/ajuda`, `/sair`.

### Tarefas em background (não-bloqueante)

Peça algo como "faça X em background" e a Alina dispara a tarefa (delegada ao Claude Code) sem
travar a conversa — você continua interagindo e ela **avisa quando terminar**. Acompanhe com `/tarefas`,
veja o resultado com `/tarefa <id>` e interrompa com `/cancelar <id>`.

### Plugins (ferramentas externas sem recompilar)

Cada plugin é um arquivo `*.plugin.json` na pasta de plugins (default: `plugins/` ao lado do
executável; configurável em `Plugins:Directory`). Ele descreve um comando externo que vira uma
tool da Alina, com parâmetros `{placeholder}` preenchidos pelo LLM. Veja
[`src/Alina.Console/plugins/exemplo.plugin.json`](src/Alina.Console/plugins/exemplo.plugin.json).
Plugins com `requiresConfirmation: true` pedem SIM/NÃO antes de executar. Use `/plugins` para listar os carregados.

### Modo voz (push-to-talk)

Digite `/voz` para entrar no modo voz. Aperte **Enter** para começar a gravar e **Enter**
de novo para parar; a Alina transcreve (Whisper), processa e responde por voz
(gpt-4o-mini-tts). Digite `/texto` para voltar ao teclado. Requer microfone e alto-falante.
Configuração na seção `Voice` do appsettings (`Voice`, `SttModel`, `TtsModel`, `Language`, `Speed`).

## Testes

```powershell
dotnet test Alina.slnx
```

## Roadmap

- **Fase 1 (atual):** chat por texto, OpenAI, histórico, framework de tools.
- **Fase 2 (feito):** voz push-to-talk — STT (Whisper) e TTS (gpt-4o-mini-tts), áudio via NAudio.
- **Fase 3 (feito):** tool do **Claude Code** via CLI headless (`claude -p --output-format json`).
  Configurável na seção `ClaudeCode` do appsettings (`PermissionMode`, `TimeoutSeconds`,
  `MaxTurns`, `SkipPermissions`, `Executable`, `DefaultWorkingDirectory`).
- **Fase 4:** leitura/alteração de arquivos e execução automática de testes.
- **Fase 5 (feito):** ferramentas de Git — `git_status`, `git_diff`, `git_log`, `git_commit`
  (com confirmação), `git_branch`. Config na seção `Git` do appsettings.
- **Fase 6 (em progresso):**
  - ✅ **Memória permanente que aprende** — tools `lembrar`, `listar_memorias`, `esquecer`;
    fatos persistidos em `memory.json` e injetados no system prompt a cada turno.
  - ✅ **Plugins personalizados** — manifestos `*.plugin.json` viram tools (AIFunction de schema
    dinâmico); carregados no boot, com confirmação e substituição de parâmetros.
  - ✅ **Execução em background** — `IBackgroundTaskManager` roda delegações ao Claude Code sem
    bloquear a conversa; tools `delegar_em_background`/`listar_tarefas` e comandos `/tarefas`,
    `/tarefa <id>`, `/cancelar <id>`, com notificação ao concluir.
