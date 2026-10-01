/**
 * Pac-Man Semântico (CIn-UFPE) - WebGL Runtime Framework
 * Framework auxiliar para suporte a WebAssembly, gráficos WebGL e eventos do browser.
 */
var Module = typeof Module !== 'undefined' ? Module : {};

Module.onRuntimeInitialized = function() {
    console.log("[VirtOnto WebGL Framework] Runtime WebAssembly inicializado com sucesso.");
};

Module.evaluateSemanticRule = function(distancia, powerPelletAtivo) {
    if (powerPelletAtivo) {
        return { estado: "Vulneravel", cor: "#38bdf8" }; // Azul
    } else if (distancia < 2.5) {
        return { estado: "Agressivo", cor: "#ef4444" };  // Vermelho
    } else {
        return { estado: "Patrulha", cor: "#f8fafc" };   // Branco
    }
};

Module.checkCollision = function(distancia, limiteSeguro) {
    return distancia <= (limiteSeguro || 0.8);
};

window.VirtOntoModule = Module;
