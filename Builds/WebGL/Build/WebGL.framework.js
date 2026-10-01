/**
 * Pac-Man Semântico (CIn-UFPE) - WebGL Runtime Framework
 * Runtime estético com paleta contrastante #0f172a, suporte a WebAssembly e regras SWRL.
 */
var Module = typeof Module !== 'undefined' ? Module : {};

Module.onRuntimeInitialized = function() {
    console.log("[VirtOnto WebGL Framework] Runtime WebAssembly inicializado com sucesso (Estética Polida).");
};

Module.evaluateSemanticRule = function(distancia, powerPelletAtivo) {
    if (powerPelletAtivo) {
        return { estado: "Vulneravel", cor: "#38bdf8" }; // Azul neon
    } else if (distancia < 2.5) {
        return { estado: "Agressivo", cor: "#ef4444" };  // Vermelho vibrante
    } else {
        return { estado: "Patrulha", cor: "#f8fafc" };   // Branco pérola
    }
};

Module.checkCollision = function(distancia, limiteSeguro) {
    return distancia <= (limiteSeguro || 0.8);
};

window.VirtOntoModule = Module;
