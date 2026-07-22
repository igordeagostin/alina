# Alina

Assistente pessoal de desenvolvimento estilo "Jarvis" em .NET 10. Orquestra um LLM
(OpenAI por padrão, via `Microsoft.Extensions.AI`/`IChatClient`) com function-calling,
memória permanente, voz push-to-talk, ferramentas declarativas e execução em background.

## Idioma

- **Português é o padrão do projeto.** Todo código gerado — identificadores, nomes de
  métodos, variáveis, mensagens, textos de UI, XML docs e documentação — deve ser em
  português, com acentuação e ortografia corretas.
- Termos técnicos e identificadores de bibliotecas externas permanecem na forma original.

## Estilo de código

- **Sem comentários**, salvo quando forem altamente necessários — ou seja, quando o código
  seria incompreensível sem eles. Prefira nomes claros e código autoexplicativo a comentários.
- Siga o estilo do código ao redor: mesma indentação, convenções de nomenclatura e idioma.
- C# moderno: `nullable` e `implicit usings` habilitados, `sealed` por padrão, expressões
  concisas. Um arquivo por tipo, namespace file-scoped.
- **Nunca use `var`.** Sempre declare o tipo explícito, mesmo quando ele for aparente pelo
  lado direito da atribuição. A regra é reforçada pelo `.editorconfig` (IDE0008 como erro).
- **Classes CSS sempre com traço simples.** Use `-` como separador em nomes de classe
  (ex.: `.cfg-abas`, `.config-grupo`). Nada de sintaxe BEM: nem duplo underscore
  (`.cfg__abas`) nem duplo traço para modificadores (`.config-item--ferramenta`). Um
  modificador é só mais uma palavra no nome: `.config-item-ferramenta`, `.cfg-aba-ativa`,
  `.msg-pending`. O duplo traço fica reservado às variáveis CSS (`--accent`, `--text`).

## Segredos e chaves de API

- **Nunca** inclua chaves de API explícitas ou rastreáveis no código, `appsettings.json`,
  testes ou qualquer arquivo versionado.
- Chaves são configuradas via **user-secrets** (`dotnet user-secrets set "Llm:ApiKey" ...`)
  ou variáveis de ambiente (`Llm__ApiKey`). O `appsettings.json` mantém `ApiKey` vazio.
- `.gitignore` já cobre `secrets.json`, `appsettings.*.local.json` e a pasta `data/`.
  Antes de commitar, confirme que nenhum segredo vazou.

## Estrutura

```
Alina.slnx
  src/
    Alina.Core            # Orquestrador, Models, Memory (abstrações), ITool
    Alina.Tools           # Tools: Terminal, FileRead, ClaudeCode, Git, Memory, Ferramentas, Background
    Alina.Voice           # STT (Whisper), TTS, áudio via NAudio, palavra de ativação (Vosk)
    Alina.Infrastructure  # IChatClient, configuração, stores JSON
    Alina.Console         # UI de console (REPL + confirmação)
    Alina.App             # UI WPF/Blazor
  tests/
    Alina.Tests           # xUnit
```

## Comandos

```powershell
dotnet build Alina.slnx
dotnet test Alina.slnx
dotnet run --project src/Alina.Console
```

## Commits

- **Não adicione trailer de co-autoria** (`Co-Authored-By: Claude ...`) nem qualquer
  outra menção ao assistente nas mensagens de commit. Os commits ficam apenas na
  autoria do usuário, para não registrar colaborador extra no repositório.
- Mensagens de commit em português.

## Convenções

- Ferramentas expostas ao LLM herdam de `ToolBase`/`ITool`; operações críticas passam por
  `IConfirmationService` antes de executar.
- Novas tools devem ser registradas na injeção de dependência (`ServiceCollectionExtensions`).
- Prefira as abstrações de `Alina.Core` (`IMemoryStore`, `IConversationStore`, `IChatClient`)
  em vez de acoplar a implementações concretas.
