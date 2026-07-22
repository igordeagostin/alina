namespace Alina.Core.Ferramentas;

/// <summary>Entrada leve do índice de ferramentas (nome, descrição e se pede confirmação).</summary>
public sealed record FerramentaResumo(string Nome, string Descricao, bool ExigeConfirmacao);
