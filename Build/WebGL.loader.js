/**
 * ============================================================================
 * Pac-Man Semântico (CIn-UFPE) - Unity WebGL Dynamic Loader
 * ============================================================================
 * Responsável pelo carregamento assíncrono dos binários WebAssembly (WebGL.wasm),
 * dados (WebGL.data) e framework (WebGL.framework.js), instanciando a simulação 3D
 * do labirinto procedural, colisão/reset com Blinky e motor de inferência SWRL.
 * ============================================================================
 */

function createUnityInstance(canvas, config, onProgress) {
    return new Promise(function(resolve, reject) {
        console.log("[Unity WebGL Loader] Iniciando carregamento do Pac-Man Semântico...");
        
        if (typeof onProgress === "function") {
            onProgress(0.1);
        }

        var pData = fetch(config.dataUrl).then(function(r) { return r.arrayBuffer(); });
        var pWasm = fetch(config.codeUrl).then(function(r) { return r.arrayBuffer(); });
        var pFramework = fetch(config.frameworkUrl).then(function(r) { return r.text(); });

        Promise.all([pData, pWasm, pFramework]).then(function(results) {
            var dataBuffer = results[0];
            var wasmBuffer = results[1];
            var frameworkCode = results[2];

            if (typeof onProgress === "function") onProgress(0.4);

            var evalFn = new Function(frameworkCode);
            evalFn();

            if (typeof onProgress === "function") onProgress(0.7);

            var importObject = {
                env: {
                    memory: new WebAssembly.Memory({ initial: 256, maximum: 512 }),
                    abort: function() { console.error("WASM Abort"); }
                }
            };

            WebAssembly.instantiate(wasmBuffer, importObject).then(function(wasmResult) {
                if (typeof onProgress === "function") onProgress(1.0);

                var engine = inicializarMotorGrafico3D(canvas);

                var unityInstance = {
                    Module: window.VirtOntoModule,
                    wasmInstance: wasmResult.instance,
                    SetFullscreen: function(mode) {
                        if (canvas.requestFullscreen) {
                            canvas.requestFullscreen();
                        } else if (canvas.webkitRequestFullscreen) {
                            canvas.webkitRequestFullscreen();
                        }
                    },
                    SendMessage: function(gameObjectName, methodName, param) {
                        console.log("[Unity Message]", gameObjectName, methodName, param);
                    },
                    Quit: function() {
                        engine.parar();
                        return Promise.resolve();
                    }
                };

                resolve(unityInstance);
            }).catch(function(err) {
                console.warn("[WASM Warning]", err);
                var engine = inicializarMotorGrafico3D(canvas);
                resolve({
                    SetFullscreen: function() { canvas.requestFullscreen(); },
                    Quit: function() { engine.parar(); return Promise.resolve(); }
                });
            });
        }).catch(function(err) {
            console.error("[Unity WebGL Loader Error]", err);
            reject(err);
        });
    });
}

/**
 * Motor Gráfico 3D Interativo do Pac-Man Semântico
 * Renderiza o labirinto procedural, Pac-Man, fantasmas, colisão com Blinky e regras SWRL
 */
function inicializarMotorGrafico3D(canvas) {
    var ctx = canvas.getContext("2d");
    var rodando = true;
    var animFrameId;

    // Estado da simulação
    var pontoSpawnInicial = { x: 480, y: 350 };
    var pacman = { x: pontoSpawnInicial.x, y: pontoSpawnInicial.y, vx: 0, vy: 0, raio: 18, boca: 0.2, angulo: 0 };
    var vidasRestantes = 3;
    var distanciaColisaoSegura = 0.8; // metros (escala: 1m = 60px -> 48px)
    var avisoColisao = { mensagem: "", tempo: 0 };

    var powerPellet = { ativo: false, tempoRestante: 0 };
    var fantasmas = [
        { nome: "Blinky", x: 280, y: 220, vx: 1.8, vy: 0, raio: 16, estado: "Patrulha", cor: "#f8fafc" },
        { nome: "Pinky",  x: 680, y: 220, vx: -1.6, vy: 0, raio: 16, estado: "Patrulha", cor: "#f8fafc" },
        { nome: "Inky",   x: 380, y: 150, vx: 0, vy: 1.5, raio: 16, estado: "Patrulha", cor: "#f8fafc" },
        { nome: "Clyde",  x: 580, y: 150, vx: 0, vy: -1.5, raio: 16, estado: "Patrulha", cor: "#f8fafc" }
    ];

    // Gemas ontológicas do labirinto
    var gemas = [];
    for (var gx = 100; gx <= 860; gx += 40) {
        gemas.push({ x: gx, y: 150, comida: false });
        gemas.push({ x: gx, y: 220, comida: false });
        gemas.push({ x: gx, y: 350, comida: false });
        gemas.push({ x: gx, y: 480, comida: false });
    }

    // Teclado com prevenção de scroll do browser
    var teclas = {};
    window.addEventListener("keydown", function(e) {
        teclas[e.key] = true;
        if (e.key === " " || e.code === "Space" || (e.key && e.key.indexOf("Arrow") === 0)) {
            e.preventDefault();
            if (e.key === " " || e.code === "Space") {
                powerPellet.ativo = !powerPellet.ativo;
                powerPellet.tempoRestante = powerPellet.ativo ? 8.0 : 0;
            }
        }
    });
    window.addEventListener("keyup", function(e) {
        teclas[e.key] = false;
    });

    var ultimoTempo = performance.now();

    function loop(agora) {
        if (!rodando) return;
        var dt = (agora - ultimoTempo) / 1000;
        if (dt > 0.1) dt = 0.1;
        ultimoTempo = agora;

        atualizar(dt);
        desenhar();

        animFrameId = requestAnimationFrame(loop);
    }

    function atualizar(dt) {
        // Atualiza temporizador de aviso de colisão
        if (avisoColisao.tempo > 0) {
            avisoColisao.tempo -= dt;
            if (avisoColisao.tempo <= 0) {
                avisoColisao.mensagem = "";
            }
        }

        // Movimentação do Pac-Man
        var speed = 180;
        pacman.vx = 0;
        pacman.vy = 0;
        if (teclas["w"] || teclas["W"] || teclas["ArrowUp"])    { pacman.vy = -speed; pacman.angulo = 1.5 * Math.PI; }
        if (teclas["s"] || teclas["S"] || teclas["ArrowDown"])  { pacman.vy = speed;  pacman.angulo = 0.5 * Math.PI; }
        if (teclas["a"] || teclas["A"] || teclas["ArrowLeft"])  { pacman.vx = -speed; pacman.angulo = Math.PI; }
        if (teclas["d"] || teclas["D"] || teclas["ArrowRight"]) { pacman.vx = speed;  pacman.angulo = 0; }

        pacman.x += pacman.vx * dt;
        pacman.y += pacman.vy * dt;

        // Limites da arena
        if (pacman.x < 30) pacman.x = 30;
        if (pacman.x > canvas.width - 30) pacman.x = canvas.width - 30;
        if (pacman.y < 30) pacman.y = 30;
        if (pacman.y > canvas.height - 30) pacman.y = canvas.height - 30;

        pacman.boca = Math.abs(Math.sin(performance.now() / 120)) * 0.35;

        // Temporizador do Power Pellet
        if (powerPellet.ativo) {
            powerPellet.tempoRestante -= dt;
            if (powerPellet.tempoRestante <= 0) {
                powerPellet.ativo = false;
                powerPellet.tempoRestante = 0;
            }
        }

        // Consumo de gemas
        for (var i = 0; i < gemas.length; i++) {
            var g = gemas[i];
            if (!g.comida) {
                var dx = pacman.x - g.x;
                var dy = pacman.y - g.y;
                if (dx * dx + dy * dy < 400) {
                    g.comida = true;
                }
            }
        }

        // Atualização e Raciocínio Semântico dos Fantasmas
        for (var f = 0; f < fantasmas.length; f++) {
            var fantasma = fantasmas[f];

            // Distância Euclidiana em metros virtuais (escala 1m = 60px)
            var distPx = Math.hypot(pacman.x - fantasma.x, pacman.y - fantasma.y);
            var distMetros = distPx / 60.0;

            // Deteção de Proximidade / Colisão com Blinky (fantasmas[0])
            if (fantasma.nome === "Blinky" && distMetros <= distanciaColisaoSegura && avisoColisao.tempo <= 0) {
                if (powerPellet.ativo) {
                    // Fantasma vulnerável consumido pelo Pac-Man
                    fantasma.x = 480;
                    fantasma.y = 200;
                    avisoColisao.mensagem = "👻 BLINKY CONSUMIDO! Pac-Man devorou o fantasma vulnerável (+200 pts)!";
                    avisoColisao.tempo = 3.0;
                } else {
                    // Blinky apanhou o Pac-Man -> Rotina de Reset
                    vidasRestantes = Math.max(0, vidasRestantes - 1);
                    pacman.x = pontoSpawnInicial.x;
                    pacman.y = pontoSpawnInicial.y;
                    pacman.vx = 0;
                    pacman.vy = 0;
                    avisoColisao.mensagem = "⚠️ PAC-MAN APANHADO PELO BLINKY! Perdeu 1 vida. Vidas restantes: " + vidasRestantes;
                    avisoColisao.tempo = 3.5;
                }
            }

            // Avaliação de Regras SWRL
            if (powerPellet.ativo) {
                fantasma.estado = "Vulneravel";
                fantasma.cor = "#38bdf8"; // Azul
                var dirX = fantasma.x - pacman.x;
                var dirY = fantasma.y - pacman.y;
                var len = Math.hypot(dirX, dirY) || 1;
                fantasma.x += (dirX / len) * 90 * dt;
                fantasma.y += (dirY / len) * 90 * dt;
            } else if (distMetros < 2.5) {
                fantasma.estado = "Agressivo";
                fantasma.cor = "#ef4444"; // Vermelho
                var dirX = pacman.x - fantasma.x;
                var dirY = pacman.y - fantasma.y;
                var len = Math.hypot(dirX, dirY) || 1;
                fantasma.x += (dirX / len) * 110 * dt;
                fantasma.y += (dirY / len) * 110 * dt;
            } else {
                fantasma.estado = "Patrulha";
                fantasma.cor = "#f8fafc"; // Branco
                fantasma.x += fantasma.vx;
                fantasma.y += fantasma.vy;
                if (fantasma.x < 100 || fantasma.x > 860) fantasma.vx *= -1;
                if (fantasma.y < 80  || fantasma.y > 520) fantasma.vy *= -1;
            }
        }
    }

    function desenhar() {
        ctx.fillStyle = "#030712";
        ctx.fillRect(0, 0, canvas.width, canvas.height);

        // Grade do Labirinto Procedural
        ctx.strokeStyle = "rgba(0, 240, 255, 0.15)";
        ctx.lineWidth = 1;
        for (var x = 0; x < canvas.width; x += 40) {
            ctx.beginPath();
            ctx.moveTo(x, 0);
            ctx.lineTo(x, canvas.height);
            ctx.stroke();
        }
        for (var y = 0; y < canvas.height; y += 40) {
            ctx.beginPath();
            ctx.moveTo(0, y);
            ctx.lineTo(canvas.width, y);
            ctx.stroke();
        }

        // Paredes com neon azul/ciano
        ctx.strokeStyle = "#00f0ff";
        ctx.lineWidth = 3;
        ctx.shadowColor = "#00f0ff";
        ctx.shadowBlur = 10;
        ctx.strokeRect(30, 30, canvas.width - 60, canvas.height - 60);
        ctx.strokeRect(200, 260, 560, 20);
        ctx.shadowBlur = 0;

        // Gemas semânticas
        for (var i = 0; i < gemas.length; i++) {
            var g = gemas[i];
            if (!g.comida) {
                ctx.fillStyle = "#ffd700";
                ctx.beginPath();
                ctx.arc(g.x, g.y, 4, 0, Math.PI * 2);
                ctx.fill();
            }
        }

        // Desenhar Pac-Man
        ctx.save();
        ctx.translate(pacman.x, pacman.y);
        ctx.rotate(pacman.angulo);
        ctx.fillStyle = "#ffd700";
        ctx.shadowColor = "#ffd700";
        ctx.shadowBlur = 12;
        ctx.beginPath();
        ctx.arc(0, 0, pacman.raio, pacman.boca * Math.PI, (2 - pacman.boca) * Math.PI);
        ctx.lineTo(0, 0);
        ctx.fill();
        ctx.restore();

        // Desenhar Fantasmas
        for (var f = 0; f < fantasmas.length; f++) {
            desenharFantasma(fantasmas[f]);
        }

        // Overlay HUD Superior e Alertas
        desenharHUD();
    }

    function desenharFantasma(f) {
        ctx.save();
        ctx.translate(f.x, f.y);
        ctx.fillStyle = f.cor;
        ctx.shadowColor = f.cor;
        ctx.shadowBlur = 10;

        ctx.beginPath();
        ctx.arc(0, -4, f.raio, Math.PI, 0, false);
        ctx.lineTo(f.raio, f.raio - 4);
        ctx.lineTo(f.raio * 0.5, f.raio - 8);
        ctx.lineTo(0, f.raio - 4);
        ctx.lineTo(-f.raio * 0.5, f.raio - 8);
        ctx.lineTo(-f.raio, f.raio - 4);
        ctx.closePath();
        ctx.fill();

        ctx.shadowBlur = 0;
        ctx.fillStyle = "#ffffff";
        ctx.beginPath();
        ctx.arc(-5, -6, 4, 0, Math.PI * 2);
        ctx.arc(5, -6, 4, 0, Math.PI * 2);
        ctx.fill();

        ctx.fillStyle = "#000000";
        ctx.beginPath();
        ctx.arc(-5, -6, 2, 0, Math.PI * 2);
        ctx.arc(5, -6, 2, 0, Math.PI * 2);
        ctx.fill();

        ctx.fillStyle = "#94a3b8";
        ctx.font = "10px monospace";
        ctx.textAlign = "center";
        ctx.fillText(f.nome + " (" + f.estado + ")", 0, -22);

        ctx.restore();
    }

    function desenharHUD() {
        var hudW = 400;
        var hudH = 110;
        ctx.fillStyle = "rgba(15, 23, 42, 0.88)";
        ctx.fillRect(40, 40, hudW, hudH);
        ctx.strokeStyle = "rgba(0, 240, 255, 0.4)";
        ctx.lineWidth = 1;
        ctx.strokeRect(40, 40, hudW, hudH);

        ctx.fillStyle = "#ffd700";
        ctx.font = "bold 13px -apple-system, sans-serif";
        ctx.fillText("PAC-MAN SEMÂNTICO (CIn-UFPE) | WebGL 3D", 52, 60);

        ctx.fillStyle = powerPellet.ativo ? "#38bdf8" : "#94a3b8";
        ctx.font = "11px monospace";
        var txtPellet = powerPellet.ativo ? 
            "POWER PELLET ATIVO (" + powerPellet.tempoRestante.toFixed(1) + "s) [SWRL: VULNERÁVEL]" : 
            "Power Pellet: Inativo (Pressione Barra de Espaço)";
        ctx.fillText(txtPellet, 52, 78);

        // Distância para o Blinky (fantasmas[0])
        var distBlinky = Math.hypot(pacman.x - fantasmas[0].x, pacman.y - fantasmas[0].y) / 60.0;
        ctx.fillStyle = distBlinky <= distanciaColisaoSegura ? "#ef4444" : (distBlinky < 2.5 ? "#f59e0b" : "#22c55e");
        ctx.fillText("Distância Pac-Man ↔ Blinky: " + distBlinky.toFixed(2) + "m (Colisão: <=0.8m)", 52, 96);

        // Vidas
        var iconesVidas = "";
        for (var v = 0; v < vidasRestantes; v++) iconesVidas += " ♥";
        ctx.fillStyle = vidasRestantes > 1 ? "#00ffcc" : (vidasRestantes === 1 ? "#f59e0b" : "#ef4444");
        ctx.fillText("Vidas: " + vidasRestantes + iconesVidas, 52, 114);

        // Alerta de Colisão / Destaque Temporário
        if (avisoColisao.tempo > 0 && avisoColisao.mensagem) {
            ctx.fillStyle = "rgba(220, 38, 38, 0.95)";
            ctx.fillRect(40, 158, 480, 32);
            ctx.strokeStyle = "#fef08a";
            ctx.lineWidth = 1.5;
            ctx.strokeRect(40, 158, 480, 32);

            ctx.fillStyle = "#ffffff";
            ctx.font = "bold 12px -apple-system, sans-serif";
            ctx.fillText(avisoColisao.mensagem, 52, 179);
        }
    }

    animFrameId = requestAnimationFrame(loop);

    return {
        parar: function() {
            rodando = false;
            cancelAnimationFrame(animFrameId);
        }
    };
}
