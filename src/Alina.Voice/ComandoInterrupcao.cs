namespace Alina.Voice;

/// <summary>
/// Reconhece as falas cujo único conteúdo é mandar parar ("chega", "esquece", "cancela").
/// Numa conversa fluida falar não interrompe mais nada por si só — a Alina segue tocando
/// o que já começou enquanto ouve. Só um pedido explícito como estes cancela o trabalho
/// em andamento, e por isso a checagem é feita no texto transcrito, bem mais confiável
/// que o reconhecedor local.
/// </summary>
public static class ComandoInterrupcao
{
    private const int MaximoPalavras = 4;

    public static bool EhPedidoDeParar(string texto, IEnumerable<string> palavras)
    {
        string frase = TextoVoz.Normalizar(texto ?? string.Empty).Trim();
        if (frase.Length == 0)
        {
            return false;
        }

        string[] partes = frase.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (partes.Length > MaximoPalavras)
        {
            return false;
        }

        return TextoVoz.ContemAlgum(frase, palavras);
    }
}
