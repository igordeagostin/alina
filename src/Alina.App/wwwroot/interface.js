// Ajustes visuais controlados pela tela de Aparência. O C# define a escala da
// fonte (multiplicador aplicado ao tamanho-base da raiz); todos os tamanhos em
// rem acompanham proporcionalmente.
window.alinaInterface = (function () {
    function definirEscalaFonte(escala) {
        const v = Math.max(0.5, Math.min(2, Number(escala) || 1));
        document.documentElement.style.setProperty('--escala-fonte', v);
    }

    return { definirEscalaFonte };
})();
