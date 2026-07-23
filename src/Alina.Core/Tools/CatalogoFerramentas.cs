using System.Text;
using System.Text.Json;

namespace Alina.Core.Tools;

/// <summary>
/// Descreve em texto as ferramentas atualmente disponíveis (nativas + declarativas),
/// para injetar no contexto das conversas que planejam habilidades e ferramentas.
/// Sem isso a Alina planeja às cegas e reinventa, como comando cru, o que já existe
/// como ferramenta chamável.
/// </summary>
public static class CatalogoFerramentas
{
    public static string? Descrever(IReadOnlyList<ITool> ferramentas)
    {
        if (ferramentas.Count == 0)
        {
            return null;
        }

        StringBuilder sb = new StringBuilder();
        foreach (ITool ferramenta in ferramentas)
        {
            sb.Append("- ").Append(ferramenta.Name);

            string parametros = DescreverParametros(ferramenta);
            if (parametros.Length > 0)
            {
                sb.Append('(').Append(parametros).Append(')');
            }

            sb.Append(": ").Append(ferramenta.Description);

            if (ferramenta.RequiresConfirmation)
            {
                sb.Append(" [pede confirmação ao usuário antes de executar]");
            }

            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    private static string DescreverParametros(ITool ferramenta)
    {
        try
        {
            JsonElement schema = ferramenta.AsAIFunction().JsonSchema;
            if (!schema.TryGetProperty("properties", out JsonElement propriedades)
                || propriedades.ValueKind != JsonValueKind.Object)
            {
                return string.Empty;
            }

            HashSet<string> obrigatorios = LerObrigatorios(schema);

            List<string> nomes = new List<string>();
            foreach (JsonProperty propriedade in propriedades.EnumerateObject())
            {
                nomes.Add(obrigatorios.Contains(propriedade.Name) ? propriedade.Name : propriedade.Name + "?");
            }

            return string.Join(", ", nomes);
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private static HashSet<string> LerObrigatorios(JsonElement schema)
    {
        HashSet<string> obrigatorios = new HashSet<string>(StringComparer.Ordinal);
        if (schema.TryGetProperty("required", out JsonElement lista) && lista.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in lista.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    obrigatorios.Add(item.GetString()!);
                }
            }
        }

        return obrigatorios;
    }
}
