using Alina.Core.Geracao;

namespace Alina.Tests;

public sealed class LeitorMensagemParcialTests
{
    [Fact]
    public void Acrescentar_entrega_a_mensagem_enquanto_ela_chega()
    {
        LeitorMensagemParcial leitor = new LeitorMensagemParcial();

        Assert.Null(leitor.Acrescentar("{\"mensa"));
        Assert.Null(leitor.Acrescentar("gem\": \""));

        ProgressoGeracao? passo = leitor.Acrescentar("Montei o doc");

        Assert.Equal(FaseGeracao.Escrevendo, passo!.Fase);
        Assert.Equal("Montei o doc", passo.Mensagem);
    }

    [Fact]
    public void Acrescentar_muda_para_rascunho_quando_a_fala_fecha()
    {
        LeitorMensagemParcial leitor = new LeitorMensagemParcial();

        leitor.Acrescentar("{\"mensagem\":\"Pronto");
        ProgressoGeracao? passo = leitor.Acrescentar("\",\"pronto\":true,\"conteudo\":\"# Spotify");

        Assert.Equal(FaseGeracao.Rascunho, passo!.Fase);
        Assert.Equal("Pronto", passo.Mensagem);
    }

    [Fact]
    public void Acrescentar_mantem_a_fala_depois_de_fechada()
    {
        LeitorMensagemParcial leitor = new LeitorMensagemParcial();

        leitor.Acrescentar("{\"mensagem\":\"Pronto\",\"conteudo\":\"# Passo 1");
        ProgressoGeracao? passo = leitor.Acrescentar("\\n## Passo 2\"}");

        Assert.Equal(FaseGeracao.Rascunho, passo!.Fase);
        Assert.Equal("Pronto", passo.Mensagem);
    }

    [Fact]
    public void Acrescentar_desescapa_o_que_ja_chegou()
    {
        LeitorMensagemParcial leitor = new LeitorMensagemParcial();

        leitor.Acrescentar("{\"mensagem\":\"Criei a ferramenta ");
        ProgressoGeracao? passo = leitor.Acrescentar("\\\"spotify\\\" e o\\ndocumento");

        Assert.Equal("Criei a ferramenta \"spotify\" e o\ndocumento", passo!.Mensagem);
    }

    [Fact]
    public void Acrescentar_ignora_escape_cortado_no_meio()
    {
        LeitorMensagemParcial leitor = new LeitorMensagemParcial();

        leitor.Acrescentar("{\"mensagem\":\"linha um");
        ProgressoGeracao? comBarraSolta = leitor.Acrescentar("\\");
        ProgressoGeracao? comUnicodeParcial = leitor.Acrescentar("u00e");

        Assert.Null(comBarraSolta);
        Assert.Null(comUnicodeParcial);

        ProgressoGeracao? completo = leitor.Acrescentar("7 final");

        Assert.Equal("linha umç final", completo!.Mensagem);
    }

    [Fact]
    public void Acrescentar_aspas_escapadas_nao_fecham_a_mensagem()
    {
        LeitorMensagemParcial leitor = new LeitorMensagemParcial();

        ProgressoGeracao? passo = leitor.Acrescentar("{\"mensagem\":\"diga \\\"oi\\\" e siga");

        Assert.Equal(FaseGeracao.Escrevendo, passo!.Fase);
        Assert.Equal("diga \"oi\" e siga", passo.Mensagem);
    }

    [Fact]
    public void Acrescentar_nao_reporta_enquanto_nao_ha_texto_util()
    {
        LeitorMensagemParcial leitor = new LeitorMensagemParcial();

        Assert.Null(leitor.Acrescentar("```json\n{\n  \"pronto\": true,\n"));
    }
}
