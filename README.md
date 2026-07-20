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

## Testes

```powershell
dotnet test Alina.slnx
```
