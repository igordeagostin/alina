using System.Text.Json;
using System.Text.Json.Serialization;
using Alina.Core.Permissoes;
using Alina.Infrastructure.Configuration;

namespace Alina.Infrastructure.Permissoes;

/// <summary>
/// Implementação da política de permissões persistida num único JSON (opções + regras aprendidas)
/// na pasta de dados. Regras de escopo de sessão ficam apenas em memória. O acesso é serializado,
/// pois a avaliação é chamada pelo servidor de permissão (threads do Kestrel).
/// </summary>
public sealed class PoliticaPermissao : IPoliticaPermissao
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _arquivo;
    private readonly object _gate = new();
    private readonly List<RegraPermissao> _regrasSessao = [];
    private Estado _estado = new();

    public PoliticaPermissao(string arquivo)
    {
        _arquivo = arquivo;
        string? dir = Path.GetDirectoryName(_arquivo);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        Carregar();
    }

    public PoliticaPermissao(StorageOptions options)
        : this(Path.Combine(options.ResolveDataDirectory(), "permissoes.json")) { }

    public PoliticaPermissaoOptions Opcoes
    {
        get { lock (_gate) { return _estado.Opcoes; } }
    }

    public IReadOnlyList<RegraPermissao> Regras
    {
        get { lock (_gate) { return _estado.Regras.ToList(); } }
    }

    public DecisaoPermissao Avaliar(PedidoPermissao pedido)
    {
        lock (_gate)
        {
            List<RegraPermissao> todas = _estado.Regras.Concat(_regrasSessao).ToList();
            return AvaliadorPermissao.Avaliar(pedido, _estado.Opcoes, todas);
        }
    }

    public void Aprender(PedidoPermissao pedido, RespostaConfirmacaoPermissao resposta)
    {
        if (resposta.Escopo == EscopoPermissao.UmaVez)
        {
            return;
        }

        RegraPermissao regra = CriarRegra(pedido, resposta);

        lock (_gate)
        {
            if (resposta.Escopo == EscopoPermissao.Sessao)
            {
                _regrasSessao.Add(regra);
                return;
            }

            _estado.Regras.Add(regra);
            Salvar();
        }
    }

    public void AtualizarOpcoes(PoliticaPermissaoOptions opcoes)
    {
        lock (_gate)
        {
            _estado.Opcoes = opcoes;
            Salvar();
        }
    }

    public void RemoverRegra(string id)
    {
        lock (_gate)
        {
            bool removeu = _estado.Regras.RemoveAll(r => r.Id == id) > 0;
            if (removeu)
            {
                Salvar();
            }
        }
    }

    private static RegraPermissao CriarRegra(PedidoPermissao pedido, RespostaConfirmacaoPermissao resposta)
    {
        string? prefixoComando = string.IsNullOrWhiteSpace(pedido.Comando) ? null : ExtrairPrefixoComando(pedido.Comando);
        string? diretorio = resposta.Escopo == EscopoPermissao.SempreNesteDiretorio
            ? pedido.DiretorioTrabalho
            : null;

        return new RegraPermissao
        {
            Ferramenta = pedido.Ferramenta,
            PrefixoComando = prefixoComando,
            Diretorio = diretorio,
            Permitir = resposta.Permitido,
            Rotulo = MontarRotulo(pedido, prefixoComando, diretorio),
        };
    }

    private static string ExtrairPrefixoComando(string comando)
    {
        string[] tokens = comando.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return tokens.Length <= 2 ? string.Join(' ', tokens) : $"{tokens[0]} {tokens[1]}";
    }

    private static string MontarRotulo(PedidoPermissao pedido, string? prefixoComando, string? diretorio)
    {
        string alvo = prefixoComando is not null ? $"{prefixoComando}*" : pedido.Ferramenta;
        return diretorio is not null ? $"{alvo} em {diretorio}" : alvo;
    }

    private void Carregar()
    {
        try
        {
            if (File.Exists(_arquivo))
            {
                string json = File.ReadAllText(_arquivo);
                _estado = JsonSerializer.Deserialize<Estado>(json, JsonOptions) ?? new Estado();
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            _estado = new Estado();
        }
    }

    private void Salvar()
    {
        try
        {
            File.WriteAllText(_arquivo, JsonSerializer.Serialize(_estado, JsonOptions));
        }
        catch (IOException)
        {
        }
    }

    private sealed class Estado
    {
        public PoliticaPermissaoOptions Opcoes { get; set; } = new();
        public List<RegraPermissao> Regras { get; set; } = [];
    }
}
