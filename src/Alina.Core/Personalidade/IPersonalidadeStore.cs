namespace Alina.Core.Personalidade;

/// <summary>
/// Guarda o <see cref="PerfilPersonalidade"/> escolhido pelo usuário. A leitura é
/// feita a cada turno, então mudanças valem sem reiniciar a Alina.
/// </summary>
public interface IPersonalidadeStore
{
    Task<PerfilPersonalidade> ObterAsync(CancellationToken cancellationToken = default);

    Task SalvarAsync(PerfilPersonalidade perfil, CancellationToken cancellationToken = default);
}
