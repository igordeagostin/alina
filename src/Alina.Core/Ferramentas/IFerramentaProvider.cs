using Alina.Core.Tools;

namespace Alina.Core.Ferramentas;

/// <summary>
/// Fonte viva das ferramentas declarativas. O <see cref="Tools.ToolRegistry"/> o
/// consulta a cada montagem das tools, então uma ferramenta recém-criada (pela UI,
/// pela Alina ou por edição manual do arquivo) fica disponível no mesmo turno,
/// sem reiniciar o app.
/// </summary>
public interface IFerramentaProvider
{
    /// <summary>Monta as ferramentas declarativas atuais como <see cref="ITool"/>.</summary>
    IReadOnlyList<ITool> ObterFerramentas();
}
