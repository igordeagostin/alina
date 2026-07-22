namespace Alina.Core.Habilidades;

/// <summary>
/// Como a conversa com a Alina se posiciona diante de uma habilidade que já existe.
/// </summary>
public enum ModoConversaHabilidade
{
    /// <summary>Ajustar o documento a partir do que o usuário pedir.</summary>
    Edicao,

    /// <summary>
    /// Partir do resultado de execuções reais relatadas no histórico para corrigir
    /// o que falhou no documento.
    /// </summary>
    Treino,
}
