# Plano — A "cara" da Alina (app desktop)

> Documento de planejamento. Nenhuma linha de código foi escrita ainda — este é o
> desenho para revisão antes da implementação.

## 1. Objetivo

Dar à Alina uma interface gráfica que:

- **Inicia junto com o Windows** e fica pronta para receber comandos (por voz e texto).
- Tem **dois modos de tela**:
  - **Modo compacto** — janela pequena, sempre visível, mostrando só o estado
    (parada / ouvindo / pensando / executando / falando).
  - **Modo detalhado** — janela maior com o texto das respostas, histórico da
    conversa, tools em execução e confirmações.
- Visual **estilo Jarvis**: orbe animado com glow, waveform reagindo à voz,
  transparência e transições suaves.

O `Alina.Console` atual **continua existindo intacto**. O app gráfico é um
*head* paralelo que reaproveita todas as camadas já prontas.

## 2. Decisão de tecnologia (já tomada)

**App desktop em Blazor Hybrid** — shell **WPF** hospedando a UI em Razor/HTML/CSS
via `BlazorWebView`.

Por quê desktop e não web:

| Fator | Conclusão |
|---|---|
| Reuso do código .NET | Head desktop referencia `Core`/`Tools`/`Voice` no mesmo processo, sem HTTP/serialização. |
| Voz (NAudio) | Captura/reprodução usam APIs nativas de áudio, inexistentes no navegador. |
| Push-to-talk global | Só um app desktop registra hotkey global no Windows. |
| Sempre ligada + tray | Ícone de bandeja e widget flutuante são naturais no desktop. |
| Iniciar com o Windows | Task Scheduler / registro — trivial no desktop. |

Por quê Blazor Hybrid (e não WPF puro ou Avalonia): o alvo "Jarvis desde já" põe
90% do esforço na camada visual, e HTML/CSS/Canvas/WebGL é o ambiente mais
produtivo para glow, waveform e animações. O shell WPF entrega janela transparente,
tray, hotkey e autostart de forma sólida.

**Ponto de atenção conhecido:** transparência do `WebView2` (fundo transparente +
click-through no modo compacto) exige `DefaultBackgroundColor = Transparent` e
ajuste da janela WPF (`AllowsTransparency`, `WindowStyle=None`). Isso é resolvido
uma vez na Fase C.

## 3. Arquitetura

```
Alina.Core          (inalterado, + IAssistantStatus)
Alina.Tools         (inalterado)
Alina.Infrastructure(inalterado)
Alina.Voice         (inalterado)
Alina.Console       (inalterado — head de terminal)
Alina.App    <====  NOVO head gráfico (WPF + BlazorWebView)
```

O novo projeto **reaproveita a mesma composição de DI** do
[Program.cs do console](../src/Alina.Console/Program.cs), trocando apenas as
implementações que dependem de UI:

- `ConsoleConfirmationService` → **`GuiConfirmationService`**.
- A voz deixa de ser dirigida por `Enter` no terminal e passa a ser dirigida por
  **hotkey global**, reusando `IAudioRecorder`/`ISpeechToText`/`ITextToSpeech`/`IAudioPlayer`.

### 3.1. Única adição no Core: eventos de status

Hoje o [IOrchestrator](../src/Alina.Core/Orchestration/IOrchestrator.cs) só
expõe `SendAsync` (texto → texto). O visual precisa reagir a estados. Adição
mínima e agnóstica de UI:

```csharp
// Alina.Core/Orchestration/AssistantState.cs
public enum AssistantState
{
    Idle,       // parada, aguardando
    Listening,  // captando áudio do microfone
    Thinking,   // LLM processando
    Executing,  // rodando uma tool
    Speaking    // reproduzindo TTS
}

// Alina.Core/Orchestration/IAssistantStatus.cs
public interface IAssistantStatus
{
    AssistantState Current { get; }
    event EventHandler<AssistantState>? Changed;
    void Set(AssistantState state); // usado pelos heads
}
```

Implementação: `AssistantStatus` (singleton, thread-safe, dispara `Changed`).

Quem seta cada estado:

- `Listening` / `Speaking` — o **app**, ao redor de STT e TTS.
- `Thinking` — o **app**, ao redor de `orchestrator.SendAsync(...)`.
- `Executing` — um **decorator `StatusTrackingTool : ITool`** que envolve cada
  tool e marca `Executing` durante `ExecuteAsync` (registrado na DI por cima das
  tools concretas). Alternativa mais simples para a v1: derivar "executando" do
  `GuiConfirmationService` (quando há uma confirmação pendente). Decisão fica para
  a Fase B.

Esta é a **única** mudança em projeto existente; o console não é afetado (ele
simplesmente não assina o evento).

## 4. Estrutura do projeto `Alina.App`

Target: `net10.0-windows`, `<UseWPF>true</UseWPF>`.

```
src/Alina.App/
  Alina.App.csproj
  App.xaml / App.xaml.cs         -> monta o Host/DI (copiado do console) e o tray
  Composition.cs                 -> AddAlina + tools + voz (extraído do Program.cs)
  Services/
    GuiConfirmationService.cs    -> IConfirmationService via diálogo/overlay
    VoiceController.cs           -> orquestra hotkey -> STT -> SendAsync -> TTS
    GlobalHotkey.cs              -> RegisterHotKey (Win32) para push-to-talk
    TrayIcon.cs                  -> NotifyIcon: modos, mostrar/esconder, sair
    Autostart.cs                 -> registra/remove no Task Scheduler
    UiState.cs                   -> ponte observável (status + mensagens) p/ Blazor
  Windows/
    CompactWindow.xaml           -> janela pequena, borderless, always-on-top
    DetailWindow.xaml            -> janela grande, chat + detalhes
  Components/                    -> UI Razor (Blazor)
    Orb.razor                    -> orbe animado (glow/pulso por estado)
    Waveform.razor               -> canvas alimentado pela amplitude do mic
    ChatLog.razor                -> histórico + resposta em texto
    ConfirmDialog.razor          -> overlay de confirmação (SIM/NÃO)
  wwwroot/
    app.css                      -> tema, glow, transições
    orb.js / waveform.js         -> interop de animação/canvas
  appsettings.json               -> mesmas seções do console (Llm, Voice, etc.)
```

Dependências NuGet novas:

- `Microsoft.AspNetCore.Components.WebView.Wpf`
- `Microsoft.Web.WebView2` (runtime já presente no Windows 11)
- `Hardcodet.NotifyIcon.Wpf` (tray) — ou `System.Windows.Forms.NotifyIcon`
- `TaskScheduler` (Microsoft.Win32.TaskScheduler) para o autostart — ou usar a
  chave `Run` do registro (mais simples, sem "iniciar minimizado" limpo).

## 5. Os dois modos de tela

### 5.1. Modo compacto
- Janela ~120×120 px, sem borda, cantos arredondados, fundo transparente.
- `Topmost=true`, arrastável, fixada num canto (posição lembrada).
- Conteúdo: **orbe** que muda cor/animação por `AssistantState`:
  - Idle → respiração lenta, tom frio.
  - Listening → pulso + **waveform** reagindo ao microfone.
  - Thinking → rotação/shimmer.
  - Executing → anel de progresso.
  - Speaking → pulso sincronizado com o áudio.
- Clique → alterna para o modo detalhado. Clique direito → menu (mesmo do tray).

### 5.2. Modo detalhado
- Janela ~420×640 px, redimensionável.
- Topo: orbe menor + estado atual em texto.
- Centro: **ChatLog** — mensagens do usuário e da Alina, texto das respostas,
  linhas de "executando tool X…".
- Overlays de **confirmação** aparecem aqui (e trazem a janela ao foco).
- Rodapé: campo de texto (fallback sem voz) + botão de microfone.

Alternância: as duas janelas compartilham o mesmo `UiState`; trocar de modo é
mostrar uma e esconder a outra, com fade.

## 6. Voz mãos-livres

- **Hotkey global** (ex.: `Ctrl+Espaço` segurar-para-falar) via `RegisterHotKey`.
- Fluxo: segurar → `Set(Listening)` + grava (`IAudioRecorder`) → soltar → STT
  (`ISpeechToText`) → `Set(Thinking)` + `SendAsync` → `Set(Speaking)` + TTS
  (`ITextToSpeech` + `IAudioPlayer`) → `Set(Idle)`.
- Reaproveita os componentes de baixo nível do `Alina.Voice`; **não** reusa o
  `VoiceChat` do console (que tem UX de terminal embutida).
- Amplitude do microfone é exposta ao Blazor (interop) para alimentar a waveform.

## 7. Iniciar com o Windows

- **Task Scheduler** com gatilho *At log on* do usuário, ação = executável do app,
  iniciando **minimizado na bandeja** (argumento `--tray`).
- Toggle "Iniciar com o Windows" nas configurações liga/desliga a tarefa.
- Alternativa mais simples se preferir: chave
  `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` (sem controle fino de
  "minimizado", mas trivial).

## 8. Fases de entrega e critérios de aceite

| Fase | Entrega | Aceite |
|---|---|---|
| **A** ✅ | Projeto `Alina.App` + DI reusada + confirmação gráfica + 1 janela simples (texto → `SendAsync` → resposta). | Digito no app e recebo resposta da Alina rodando dentro do shell WPF. |
| **B** ✅ | `IAssistantStatus` no Core (+ `StatusTrackingFunction` marcando `Executing` por tool) + modo compacto e detalhado + alternância + confirmação como overlay na janela. | Orbe reflete os estados; alterno entre os dois modos; confirmações aparecem no app. |
| **C** | Tray + hotkey global de push-to-talk + autostart. | Falo segurando a hotkey de qualquer app; ícone na bandeja; inicia com o Windows. |
| **D** | Capricho visual: glow, waveform reativa, transições. | Visual "Jarvis" — orbe glowando e waveform reagindo à voz. |

> **Status:** Fases A e B concluídas. Na Fase B o modo de voz do app usa
> push-to-talk **por clique** no orbe; a hotkey global mãos-livres vem na Fase C.
> A confirmação passou de `MessageBox` nativo para overlay Blazor dentro da janela.

## 9. Riscos e mitigações

- **Transparência do WebView2** (modo compacto) — quirk conhecido; mitigado com
  `DefaultBackgroundColor=Transparent` + janela WPF transparente. Se travar,
  fallback: orbe do modo compacto desenhado em WPF/SkiaSharp nativo e só o modo
  detalhado em Blazor.
- **Status `Executing` por tool** — se o decorator de `ITool` ficar intrusivo na
  v1, começar derivando o estado só do fluxo voz/LLM e refinar depois.
- **Runtime WebView2 ausente** em máquina limpa — presente por padrão no Win11;
  documentar dependência.
- **Streaming das respostas** — hoje `SendAsync` devolve o texto só no fim. Para a
  v1 mostramos a resposta completa; streaming token-a-token fica como melhoria
  futura (exigiria `GetStreamingResponseAsync` no orquestrador).

## 10. Fora de escopo (v1)

- Streaming token-a-token da resposta.
- Empacotamento/instalador (MSIX). Roda via build local.
- Temas claros/customização visual além do tema Jarvis.
- Multi-monitor avançado além de lembrar a posição.
