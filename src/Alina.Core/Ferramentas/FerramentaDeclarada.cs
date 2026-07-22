using Alina.Core.Tools;
using Microsoft.Extensions.AI;

namespace Alina.Core.Ferramentas;

/// <summary>
/// Adapta uma <see cref="DefinicaoFerramenta"/> à interface <see cref="ITool"/>,
/// para que uma ferramenta declarada em JSON seja indistinguível, para o
/// orquestrador, de uma tool escrita em C#.
/// </summary>
public sealed class FerramentaDeclarada : ITool
{
    private readonly DefinicaoFerramenta _definicao;
    private readonly IConfirmationService _confirmation;

    public FerramentaDeclarada(DefinicaoFerramenta definicao, IConfirmationService confirmation)
    {
        _definicao = definicao;
        _confirmation = confirmation;
    }

    public string Name => _definicao.Nome;

    public string Description => _definicao.Descricao;

    public bool RequiresConfirmation => _definicao.ExigeConfirmacao;

    public AIFunction AsAIFunction() => new FuncaoFerramenta(_definicao, _confirmation);
}
