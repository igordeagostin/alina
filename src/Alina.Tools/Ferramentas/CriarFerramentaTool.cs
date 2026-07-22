using System.ComponentModel;
using System.Text.Json;
using Alina.Core.Ferramentas;
using Alina.Core.Tools;
using Microsoft.Extensions.AI;

namespace Alina.Tools.Ferramentas;

/// <summary>
/// Cria (ou atualiza) uma ferramenta declarativa a partir de um JSON, persistindo-a na
/// pasta de dados. A ferramenta fica chamável no turno seguinte, sem novo build.
/// </summary>
public sealed class CriarFerramentaTool : ToolBase
{
    private static readonly JsonSerializerOptions OpcoesJson = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly IFerramentaStore _ferramentas;

    public CriarFerramentaTool(IConfirmationService confirmation, IFerramentaStore ferramentas) : base(confirmation)
        => _ferramentas = ferramentas;

    public override string Name => "criar_ferramenta";

    public override string Description =>
        "Cria uma nova ferramenta declarativa (um comando externo que você passará a poder chamar), a partir de " +
        "um JSON. Use quando o usuário pedir para você CRIAR/APRENDER uma ferramenta ou automatizar um comando " +
        "recorrente. A ferramenta fica disponível logo em seguida, sem reiniciar o app. " +
        "O JSON tem os campos: nome (snake_case), descricao, comando (executável), argumentos (array de strings, " +
        "podendo conter {placeholders}), exigeConfirmacao (bool; use false só para ações seguras, o que dispensa o " +
        "SIM/NÃO), e parametros (array de { nome, descricao, obrigatorio } — um por placeholder).";

    public override bool RequiresConfirmation => false;

    public override AIFunction AsAIFunction() => AIFunctionFactory.Create(RunAsync, Name, Description);

    [Description("Cria ou atualiza uma ferramenta declarativa a partir do JSON de definição.")]
    public async Task<string> RunAsync(
        [Description("JSON da definição da ferramenta (nome, descricao, comando, argumentos, exigeConfirmacao, parametros).")]
        string definicaoJson,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(definicaoJson))
        {
            return "Erro: informe o JSON da definição da ferramenta.";
        }

        DefinicaoFerramenta? definicao;
        try
        {
            definicao = JsonSerializer.Deserialize<DefinicaoFerramenta>(definicaoJson, OpcoesJson);
        }
        catch (JsonException ex)
        {
            return $"Erro: JSON inválido — {ex.Message}";
        }

        string? erro = Validar(definicao);
        if (erro is not null)
        {
            return $"Erro: {erro}";
        }

        bool existia = await _ferramentas.ExisteAsync(definicao!.Nome, cancellationToken);
        await _ferramentas.SalvarAsync(definicao, cancellationToken);

        string acao = existia ? "atualizada" : "criada";
        string confirma = definicao.ExigeConfirmacao ? " (pede confirmação ao executar)" : " (roda sem confirmação)";
        return $"Ferramenta '{definicao.Nome}' {acao}{confirma}. Já está disponível para uso.";
    }

    private static string? Validar(DefinicaoFerramenta? definicao)
    {
        if (definicao is null)
        {
            return "definição vazia.";
        }

        if (string.IsNullOrWhiteSpace(definicao.Nome))
        {
            return "o campo 'nome' é obrigatório.";
        }

        if (string.IsNullOrWhiteSpace(definicao.Comando))
        {
            return "o campo 'comando' é obrigatório.";
        }

        return null;
    }
}
