using System.Text;
using System.Text.Json;

namespace Alina.Core.Geracao;

/// <summary>
/// Lê o campo "mensagem" de um JSON que ainda está chegando, pedaço a pedaço. Os geradores
/// pedem ao modelo um objeto JSON em que a fala vem primeiro e o rascunho depois; assim a
/// UI já mostra a frase enquanto o documento inteiro ainda está sendo escrito.
/// </summary>
public sealed class LeitorMensagemParcial
{
    private const string Campo = "\"mensagem\"";

    private readonly StringBuilder _bruto = new StringBuilder();
    private string _mensagem = string.Empty;
    private bool _fechada;

    /// <summary>
    /// Acrescenta um pedaço recém-chegado e devolve o estado atual, ou <c>null</c> quando
    /// nada visível mudou (evita repintar a tela à toa).
    /// </summary>
    public ProgressoGeracao? Acrescentar(string pedaco)
    {
        if (string.IsNullOrEmpty(pedaco))
        {
            return null;
        }

        _bruto.Append(pedaco);

        if (_fechada)
        {
            return new ProgressoGeracao(FaseGeracao.Rascunho, _mensagem);
        }

        string texto = _bruto.ToString();
        int inicio = InicioDoValor(texto);
        if (inicio < 0)
        {
            return null;
        }

        int fim = FimDoValor(texto, inicio);
        string cru = fim < 0 ? texto[inicio..] : texto[inicio..fim];
        string mensagem = Desescapar(cru);

        if (fim >= 0)
        {
            _fechada = true;
            _mensagem = mensagem;
            return new ProgressoGeracao(FaseGeracao.Rascunho, mensagem);
        }

        if (mensagem == _mensagem)
        {
            return null;
        }

        _mensagem = mensagem;
        return new ProgressoGeracao(FaseGeracao.Escrevendo, mensagem);
    }

    private static int InicioDoValor(string texto)
    {
        int campo = texto.IndexOf(Campo, StringComparison.OrdinalIgnoreCase);
        if (campo < 0)
        {
            return -1;
        }

        int i = campo + Campo.Length;
        while (i < texto.Length && (texto[i] == ' ' || texto[i] == ':' || texto[i] == '\n' || texto[i] == '\r'))
        {
            i++;
        }

        return i < texto.Length && texto[i] == '"' ? i + 1 : -1;
    }

    private static int FimDoValor(string texto, int inicio)
    {
        for (int i = inicio; i < texto.Length; i++)
        {
            if (texto[i] == '"' && !EscapadoEm(texto, i))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool EscapadoEm(string texto, int indice)
    {
        int barras = 0;
        for (int i = indice - 1; i >= 0 && texto[i] == '\\'; i--)
        {
            barras++;
        }

        return barras % 2 == 1;
    }

    /// <summary>
    /// Converte o trecho cru em texto legível. Como o corte pode cair no meio de uma
    /// sequência de escape, o rabo incompleto é descartado antes de entregar ao parser.
    /// </summary>
    private static string Desescapar(string cru)
    {
        string seguro = SemEscapeIncompleto(cru);
        if (seguro.Length == 0)
        {
            return string.Empty;
        }

        try
        {
            return JsonSerializer.Deserialize<string>($"\"{seguro}\"") ?? string.Empty;
        }
        catch (JsonException)
        {
            return seguro;
        }
    }

    private static string SemEscapeIncompleto(string cru)
    {
        int unicode = cru.LastIndexOf("\\u", StringComparison.Ordinal);
        if (unicode >= 0 && cru.Length - unicode < 6 && !EscapadoEm(cru, unicode))
        {
            return cru[..unicode];
        }

        int barras = 0;
        for (int i = cru.Length - 1; i >= 0 && cru[i] == '\\'; i--)
        {
            barras++;
        }

        return barras % 2 == 1 ? cru[..^1] : cru;
    }
}
