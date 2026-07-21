// Waveform reativa ao microfone. O C# empurra a amplitude (0–1) via
// alinaWaveform.push(nivel); um loop de animação desenha um equalizador
// espelhado em âmbar que decai suavemente quando a captura para.
window.alinaWaveform = (function () {
    const BARRAS = 40;
    const historico = new Array(BARRAS).fill(0);
    let nivel = 0;
    let raf = null;
    let canvas = null;
    let ctx = null;

    function garantirCanvas() {
        if (canvas && canvas.isConnected) {
            return true;
        }
        canvas = document.getElementById('waveform');
        ctx = canvas ? canvas.getContext('2d') : null;
        return !!ctx;
    }

    function corAmbar(alpha) {
        return `rgba(223, 165, 58, ${alpha})`;
    }

    function frame() {
        historico.push(nivel);
        historico.shift();
        nivel *= 0.82;

        if (garantirCanvas()) {
            desenhar();
        }

        const ativo = nivel > 0.004 || historico.some((v) => v > 0.004);
        raf = ativo ? requestAnimationFrame(frame) : null;
    }

    function desenhar() {
        const dpr = window.devicePixelRatio || 1;
        const largura = (canvas.width = canvas.clientWidth * dpr);
        const altura = (canvas.height = canvas.clientHeight * dpr);
        ctx.clearRect(0, 0, largura, altura);

        const espaco = largura / BARRAS;
        const barra = espaco * 0.5;
        const meio = altura / 2;
        const minimo = 2 * dpr;

        for (let i = 0; i < BARRAS; i++) {
            const v = historico[i];
            const alturaBarra = Math.max(minimo, v * altura * 0.9);
            const x = i * espaco + (espaco - barra) / 2;
            const y = meio - alturaBarra / 2;

            const grad = ctx.createLinearGradient(0, y, 0, y + alturaBarra);
            grad.addColorStop(0, corAmbar(0.35));
            grad.addColorStop(0.5, corAmbar(0.95));
            grad.addColorStop(1, corAmbar(0.35));
            ctx.fillStyle = grad;

            const r = barra / 2;
            ctx.beginPath();
            ctx.roundRect(x, y, barra, alturaBarra, r);
            ctx.fill();
        }
    }

    function push(v) {
        if (typeof v !== 'number' || isNaN(v)) {
            return;
        }
        nivel = Math.max(nivel, Math.min(1, Math.max(0, v)));
        if (!raf) {
            raf = requestAnimationFrame(frame);
        }
    }

    return { push };
})();
