using Alina.Core.Orchestration;
using Alina.Core.Personalidade;
using Alina.Core.Tools;
using Alina.Infrastructure.Personalidade;

namespace Alina.Tests;

public sealed class PersonalidadeTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "alina-tests", Guid.NewGuid().ToString("n"));

    private string Arquivo => Path.Combine(_tempDir, "personalidade.json");

    [Fact]
    public void Prompt_com_iniciativa_minima_manda_parar_apos_o_pedido()
    {
        PerfilPersonalidade perfil = new PerfilPersonalidade { Proatividade = 1, Verbosidade = 1 };

        string prompt = SystemPromptBuilder.Build(Array.Empty<ITool>(), preferences: null, personalidade: perfil);

        Assert.Contains("Faça exatamente o que foi pedido e pare.", prompt);
        Assert.Contains("menor número de palavras possível", prompt);
        Assert.DoesNotContain("Seja bastante proativa", prompt);
    }

    [Fact]
    public void Prompt_com_iniciativa_maxima_pede_antecipacao()
    {
        PerfilPersonalidade perfil = new PerfilPersonalidade { Proatividade = 5 };

        string prompt = SystemPromptBuilder.Build(Array.Empty<ITool>(), preferences: null, personalidade: perfil);

        Assert.Contains("Seja bastante proativa", prompt);
        Assert.DoesNotContain("Faça exatamente o que foi pedido e pare.", prompt);
    }

    [Fact]
    public void Prompt_inclui_orientacoes_livres_do_usuario()
    {
        PerfilPersonalidade perfil = new PerfilPersonalidade { Instrucoes = "  Me chame de Igor.  " };

        string prompt = SystemPromptBuilder.Build(Array.Empty<ITool>(), preferences: null, personalidade: perfil);

        Assert.Contains("Orientações do usuário", prompt);
        Assert.Contains("Me chame de Igor.", prompt);
    }

    [Fact]
    public void Normalizado_limita_os_eixos_a_faixa_valida()
    {
        PerfilPersonalidade perfil = new PerfilPersonalidade
        {
            Verbosidade = 0,
            Proatividade = 9,
            Humor = -3,
            Formalidade = 5,
        }.Normalizado();

        Assert.Equal(PerfilPersonalidade.NivelMinimo, perfil.Verbosidade);
        Assert.Equal(PerfilPersonalidade.NivelMaximo, perfil.Proatividade);
        Assert.Equal(PerfilPersonalidade.NivelMinimo, perfil.Humor);
        Assert.Equal(5, perfil.Formalidade);
    }

    [Fact]
    public async Task Store_sem_arquivo_devolve_o_perfil_padrao()
    {
        FilePersonalidadeStore store = new FilePersonalidadeStore(Arquivo);

        PerfilPersonalidade perfil = await store.ObterAsync();

        Assert.Equal(new PerfilPersonalidade().Verbosidade, perfil.Verbosidade);
        Assert.Equal(string.Empty, perfil.Instrucoes);
    }

    [Fact]
    public async Task Store_persiste_e_reflete_alteracao_feita_no_disco()
    {
        FilePersonalidadeStore store = new FilePersonalidadeStore(Arquivo);

        await store.SalvarAsync(new PerfilPersonalidade { Humor = 1, Instrucoes = "Sem piadas." });

        PerfilPersonalidade salvo = await store.ObterAsync();
        Assert.Equal(1, salvo.Humor);
        Assert.Equal("Sem piadas.", salvo.Instrucoes);

        FilePersonalidadeStore outro = new FilePersonalidadeStore(Arquivo);
        await outro.SalvarAsync(new PerfilPersonalidade { Humor = 5, Instrucoes = "Pode brincar." });

        PerfilPersonalidade relido = await store.ObterAsync();
        Assert.Equal(5, relido.Humor);
        Assert.Equal("Pode brincar.", relido.Instrucoes);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}
