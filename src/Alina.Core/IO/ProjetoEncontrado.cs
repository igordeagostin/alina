namespace Alina.Core.IO;

/// <summary>Um projeto candidato encontrado no disco pela busca por nome.</summary>
/// <param name="Nome">Nome da pasta.</param>
/// <param name="Caminho">Caminho absoluto da pasta.</param>
/// <param name="Pontuacao">Quão bem o nome casa com o termo buscado (maior é melhor).</param>
/// <param name="Marcador">Arquivo que identifica a pasta como raiz de projeto (.git, .sln, .csproj…), se houver.</param>
public sealed record ProjetoEncontrado(string Nome, string Caminho, int Pontuacao, string? Marcador);
