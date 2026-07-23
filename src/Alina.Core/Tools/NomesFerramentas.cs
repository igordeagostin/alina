namespace Alina.Core.Tools;

/// <summary>
/// Nomes de ferramentas que a composição precisa citar sem depender do assembly que
/// as implementa — hoje, as que disparam trabalho paralelo e portanto ficam de fora
/// dos orquestradores isolados.
/// </summary>
public static class NomesFerramentas
{
    public const string ExecutarEmParalelo = "executar_em_paralelo";

    public const string DelegarEmBackground = "delegar_em_background";
}
