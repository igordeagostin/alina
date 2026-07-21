# Identidade visual da Alina

> **Fonte da verdade do visual.** Toda mudança de UI/estilo do projeto — app,
> console, documentação, artefatos — deve partir desta paleta. Sempre que houver
> pedido de ajuste visual, esta é a identidade a seguir.

## Cor oficial: Âmbar `#DFA53A`

O âmbar é uma resina fossilizada que preserva memórias do passado para o futuro:
símbolo de **preservação do conhecimento**, calor humano, raridade e sofisticação.
É a metáfora exata da Alina — ela **aprende, preserva, evolui e ilumina caminhos**.

## Paleta

| Papel | Nome | Hex |
|---|---|---|
| Fundo principal | Preto Grafite | `#171717` |
| Cor de destaque | Âmbar | `#DFA53A` |
| Destaque (hover) | Âmbar claro | `#E8B84A` |
| Texto / superfícies claras | Branco Gelo | `#F7F7F2` |
| Painéis / superfícies elevadas | Cinza | `#303030` |

### Tons derivados (para gradientes, glow e estados)

Estes são derivados da paleta base para dar profundidade sem fugir da identidade:

| Token | Hex | Uso |
|---|---|---|
| Âmbar pressionado | `#C48F2E` | botão pressionado, bordas de destaque |
| Âmbar brilhante | `#F2C965` | ponto alto do orbe, brilho |
| Glow âmbar | `rgba(223, 165, 58, 0.55)` | sombra/halo do orbe |
| Glow âmbar suave | `rgba(223, 165, 58, 0.26)` | brilho de fundo, foco de input |
| Fundo elevado | `#1F1F1F` | cabeçalho, rodapé (entre o fundo e o painel) |
| Borda | `#3A3A3A` | divisórias |
| Borda suave | `#2A2A2A` | divisórias discretas |
| Texto secundário | `#9A9A92` | dicas, status, legendas |
| Balão do usuário | `#2A2620` | fundo de mensagem do usuário (grafite morno) |

## Princípios

- **Âmbar é acento, não fundo.** Usar em orbe, foco, botões primários e estados
  ativos — não em grandes áreas.
- **Fundo escuro e sóbrio** (grafite) para o âmbar brilhar e transmitir calor.
- **Contraste legível:** texto em Branco Gelo sobre grafite; texto escuro
  (`#171717`) sobre botões âmbar.
- **Glow morno** em vez de neon frio — coerente com "resina quente e acolhedora".

## Onde está aplicada

- App desktop: variáveis CSS em [`src/Alina.App/wwwroot/app.css`](../src/Alina.App/wwwroot/app.css)
  e o `Background` da janela em [`src/Alina.App/MainWindow.xaml`](../src/Alina.App/MainWindow.xaml).
