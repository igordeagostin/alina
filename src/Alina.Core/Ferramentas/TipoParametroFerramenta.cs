using System.Text.Json.Serialization;

namespace Alina.Core.Ferramentas;

/// <summary>
/// Natureza do valor que o LLM preenche num parâmetro. Define a validação feita antes
/// de disparar o processo — sem ela, um caminho inventado vira comando executado às
/// cegas (o VS Code, por exemplo, abre uma aba vazia e devolve exit 0).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<TipoParametroFerramenta>))]
public enum TipoParametroFerramenta
{
    /// <summary>Infere pelo nome do parâmetro: "caminho", "pasta", "projeto"… viram diretório.</summary>
    Automatico,

    /// <summary>Texto livre — nenhuma validação. Use para desligar a inferência.</summary>
    Texto,

    /// <summary>Diretório que precisa existir no disco no momento da chamada.</summary>
    Diretorio,

    /// <summary>Arquivo que precisa existir no disco no momento da chamada.</summary>
    Arquivo,
}
