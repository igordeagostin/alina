# Alina

> ⚠️ **Projeto em desenvolvimento.** APIs, estrutura e comportamento ainda mudam.

Assistente pessoal de desenvolvimento estilo "Jarvis" em **.NET 10**. A Alina entende
seus pedidos, decide qual ferramenta usar e executa ações (terminal, arquivos, Claude Code,
git, voz), pedindo confirmação para operações críticas.

O LLM é abstraído via **Microsoft.Extensions.AI** (`IChatClient`), com OpenAI como provider
padrão e trocável por configuração.

## Estrutura

```
Alina.slnx
  src/
    Alina.Core            # Orquestrador, Models, Memory (abstrações), ITool
    Alina.Tools           # Tools: Terminal, FileRead, ClaudeCode, Git, Memory, Plugins, Background
    Alina.Voice           # STT (Whisper), TTS, áudio via NAudio
    Alina.Infrastructure  # IChatClient, configuração, stores JSON
    Alina.Console         # UI de console (REPL + confirmação)
    Alina.App             # UI desktop (Blazor Hybrid / WPF)
  tests/
    Alina.Tests           # xUnit
```

## Rodando

Requer .NET SDK 10 e uma chave de API de LLM (OpenAI por padrão). A chave **nunca** é
commitada — configure via user-secrets:

```powershell
cd src/Alina.Console
dotnet user-secrets set "Llm:ApiKey" "<sua-chave>"
dotnet run
```

## Voz e palavra de ativação ("Alina")

O `Alina.App` pode ser acionado por voz de duas formas: pela **hotkey global**
(`Ctrl+Espaço`) ou **chamando pelo nome "Alina"** (palavra de ativação). As duas
convivem e disparam o mesmo fluxo.

A detecção do nome roda **localmente e offline** via [Vosk](https://alphacephei.com/vosk/),
sem enviar áudio para a nuvem e sem exigir contas ou chaves. Ela depende de um
**modelo de voz em português** que **não é versionado** no repositório (~50 MB).

Para habilitar em um novo clone:

1. Baixe o modelo **`vosk-model-small-pt-0.3`** em
   [alphacephei.com/vosk/models](https://alphacephei.com/vosk/models).
2. Extraia de modo que os arquivos fiquem em
   `src/Alina.App/Modelos/vosk-model-small-pt-0.3/` (essa pasta é ignorada pelo git).
   O build copia o modelo para a saída automaticamente.
3. No app, abra as configurações (⚙) e marque **"Atender quando eu chamar 'Alina'"**.
   O mesmo painel traz atalhos para **baixar o modelo** e **abrir a pasta** quando ele
   ainda não está presente.

> O Vosk não precisa de treino: é um reconhecedor de fala genérico e a ativação
> acontece quando "alina" aparece na transcrição. Como é um nome próprio, ele pode
> ser transcrito de forma aproximada; nesse caso, ajuste `Voice:PalavrasAtivacao`
> no `appsettings.json` com variações (ex.: `"aline"`, `"alinha"`, `"a lina"`) —
> sem recompilar.

Para usar outro modelo (outro idioma ou tamanho), aponte o caminho por configuração:

```powershell
cd src/Alina.App
dotnet user-secrets set "Voice:CaminhoModeloVosk" "C:\caminho\do\modelo"
```

## Testes

```powershell
dotnet test Alina.slnx
```
