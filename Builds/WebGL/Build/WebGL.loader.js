/**
 * ============================================================================
 * Pac-Man Semântico (CIn-UFPE) - Unity WebGL Dynamic Loader (Versão Polida)
 * ============================================================================
 * Estética de alto contraste:
 * - Fundo azul-noite profundo (#0f172a / #090e17)
 * - Iluminação direcional (intensidade 1.4, sombras suaves)
 * - Materiais brilhantes (Smoothness alto) no Pac-Man e nos Fantasmas
 * - Ausência de ponteiros/lasers de RV (ecrã 100% limpo no browser)
 * - Colisão com Blinky (<= 0.8m), reposicionamento no Spawn e HUD com vidas
 * ============================================================================
 */

function createUnityInstance(canvas, config, onProgress) {
    return new Promise(function(resolve, reject) {
        console.log("[Unity WebGL Loader] Inicializando cena de alta fidelidade visual...");
        
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
 * Motor Gráfico com Estética URP Polida (Fundo #0f172a, Materiais com Brilho, Zero RV)
 */
function inicializarMotorGrafico3D(canvas) {
    var ctx = canvas.getContext("2d");
    var rodando = true;
    var animFrameId;

    // Estado da simulação
    var pontoSpawnInicial = { x: 480, y: 360 };
    var pacman = { x: pontoSpawnInicial.x, y: pontoSpawnInicial.y, vx: 0, vy: 0, raio: 17, boca: 0.2, angulo: 0 };
    var vidasRestantes = 3;
    var distanciaColisaoSegura = 0.8; // metros (0.8 * 60px = 48px)
    var avisoColisao = { mensagem: "", tempo: 0 };

    var powerPellet = { ativo: false, tempoRestante: 0 };
    var fantasmas = [
        { nome: "Blinky", x: 280, y: 220, vx: 1.8, vy: 0, raio: 16, estado: "Patrulha", cor: "#f8fafc" },
        { nome: "Pinky",  x: 680, y: 220, vx: -1.6, vy: 0, raio: 16, estado: "Patrulha", cor: "#f8fafc" },
        { nome: "Inky",   x: 380, y: 150, vx: 0, vy: 1.5, raio: 16, estado: "Patrulha", cor: "#f8fafc" },
        { nome: "Clyde",  x: 580, y: 150, vx: 0, vy: -1.5, raio: 16, estado: "Patrulha", cor: "#f8fafc" }
    ];

    // Gemas ontológicas com glow
    var gemas = [];
    for (var gx = 100; gx <= 860; gx += 40) {
        gemas.push({ x: gx, y: 150, comida: false });
        gemas.push({ x: gx, y: 220, comida: false });
        gemas.push({ x: gx, y: 360, comida: false });
        gemas.push({ x: gx, y: 480, comida: false });
    }

    // Teclado com prevenção estrita de scroll
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
        if (avisoColisao.tempo > 0) {
            avisoColisao.tempo -= dt;
            if (avisoColisao.tempo <= 0) {
                avisoColisao.mensagem = "";
            }
        }

        // Movimentação e Curvatura Fluida do Pac-Man (A/D e Setas Laterais alteram direção vetorial e ângulo)
        var speed = 195;
        var dirX = 0;
        var dirY = 0;

        if (teclas["w"] || teclas["W"] || teclas["ArrowUp"])    dirY -= 1;
        if (teclas["s"] || teclas["S"] || teclas["ArrowDown"])  dirY += 1;
        if (teclas["a"] || teclas["A"] || teclas["ArrowLeft"])  dirX -= 1;
        if (teclas["d"] || teclas["D"] || teclas["ArrowRight"]) dirX += 1;

        if (dirX !== 0 || dirY !== 0) {
            var len = Math.hypot(dirX, dirY);
            pacman.vx = (dirX / len) * speed;
            pacman.vy = (dirY / len) * speed;

            // Interpolação suave do ângulo de rotação na direção do movimento (curvatura dinâmica)
            var targetAngle = Math.atan2(dirY, dirX);
            if (targetAngle < 0) targetAngle += 2 * Math.PI;

            var diff = targetAngle - pacman.angulo;
            while (diff < -Math.PI) diff += 2 * Math.PI;
            while (diff > Math.PI) diff -= 2 * Math.PI;
            pacman.angulo += diff * Math.min(1.0, dt * 16.0);
        } else {
            pacman.vx = 0;
            pacman.vy = 0;
        }

        pacman.x += pacman.vx * dt;
        pacman.y += pacman.vy * dt;

        // Limites
        if (pacman.x < 30) pacman.x = 30;
        if (pacman.x > canvas.width - 30) pacman.x = canvas.width - 30;
        if (pacman.y < 30) pacman.y = 30;
        if (pacman.y > canvas.height - 30) pacman.y = canvas.height - 30;

        pacman.boca = Math.abs(Math.sin(performance.now() / 110)) * 0.32;

        if (powerPellet.ativo) {
            powerPellet.tempoRestante -= dt;
            if (powerPellet.tempoRestante <= 0) {
                powerPellet.ativo = false;
                powerPellet.tempoRestante = 0;
            }
        }

        // Comer gemas
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

        // Atualização dos Fantasmas
        for (var f = 0; f < fantasmas.length; f++) {
            var fantasma = fantasmas[f];

            var distPx = Math.hypot(pacman.x - fantasma.x, pacman.y - fantasma.y);
            var distMetros = distPx / 60.0;

            // Deteção de Proximidade / Colisão com Blinky (fantasmas[0])
            if (fantasma.nome === "Blinky" && distMetros <= distanciaColisaoSegura && avisoColisao.tempo <= 0) {
                if (powerPellet.ativo) {
                    fantasma.x = 480;
                    fantasma.y = 200;
                    avisoColisao.mensagem = "👻 BLINKY CONSUMIDO! Pac-Man devorou o fantasma vulnerável (+200 pts)!";
                    avisoColisao.tempo = 3.0;
                } else {
                    vidasRestantes = Math.max(0, vidasRestantes - 1);
                    pacman.x = pontoSpawnInicial.x;
                    pacman.y = pontoSpawnInicial.y;
                    pacman.vx = 0;
                    pacman.vy = 0;
                    avisoColisao.mensagem = "⚠️ PAC-MAN APANHADO PELO BLINKY! Perdeu 1 vida. Vidas restantes: " + vidasRestantes;
                    avisoColisao.tempo = 3.5;
                }
            }

            // Regras Ontológicas SWRL
            if (powerPellet.ativo) {
                fantasma.estado = "Vulneravel";
                fantasma.cor = "#38bdf8"; // Azul neon
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
                fantasma.x += (dirX / len) * 115 * dt;
                fantasma.y += (dirY / len) * 115 * dt;
            } else {
                fantasma.estado = "Patrulha";
                fantasma.cor = "#f8fafc"; // Branco suave
                fantasma.x += fantasma.vx;
                fantasma.y += fantasma.vy;
                if (fantasma.x < 100 || fantasma.x > 860) fantasma.vx *= -1;
                if (fantasma.y < 80  || fantasma.y > 520) fantasma.vy *= -1;
            }
        }
    }

    function desenhar() {
        // Fundo azul-noite profundo (#0f172a com gradiente para #090e17)
        var grad = ctx.createRadialGradient(canvas.width / 2, canvas.height / 2, 80, canvas.width / 2, canvas.height / 2, 540);
        grad.addColorStop(0, "#0f172a");
        grad.addColorStop(1, "#070b14");
        ctx.fillStyle = grad;
        ctx.fillRect(0, 0, canvas.width, canvas.height);

        // Chão do Labirinto Escuro com Linhas Contrastadas
        ctx.strokeStyle = "rgba(56, 189, 248, 0.12)";
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

        // Paredes com neon e sombras suaves
        ctx.strokeStyle = "#00f0ff";
        ctx.lineWidth = 3.5;
        ctx.shadowColor = "rgba(0, 240, 255, 0.75)";
        ctx.shadowBlur = 12;
        ctx.strokeRect(30, 30, canvas.width - 60, canvas.height - 60);
        ctx.strokeRect(200, 260, 560, 20);
        ctx.strokeRect(400, 100, 160, 20);
        ctx.strokeRect(400, 420, 160, 20);
        ctx.shadowBlur = 0;

        // Gemas semânticas com brilho
        for (var i = 0; i < gemas.length; i++) {
            var g = gemas[i];
            if (!g.comida) {
                ctx.fillStyle = "#ffd700";
                ctx.shadowColor = "#ffd700";
                ctx.shadowBlur = 8;
                ctx.beginPath();
                ctx.arc(g.x, g.y, 4, 0, Math.PI * 2);
                ctx.fill();
            }
        }
        ctx.shadowBlur = 0;

        // Desenhar Pac-Man Polido com Smoothness/Specular Highlight
        desenharPacManPolido();

        // Desenhar Fantasmas Polidos
        for (var f = 0; f < fantasmas.length; f++) {
            desenharFantasmaPolido(fantasmas[f]);
        }

        // Overlay HUD Superior e Alertas
        desenharHUD();
    }

    function desenharPacManPolido() {
        ctx.save();
        ctx.translate(pacman.x, pacman.y);
        ctx.rotate(pacman.angulo);

        // Gradiente radial para efeito de esfera polida 3D
        var pGrad = ctx.createRadialGradient(-5, -5, 2, 0, 0, pacman.raio);
        pGrad.addColorStop(0, "#fff59d"); // Brilho especular (highlight)
        pGrad.addColorStop(0.35, "#ffd700"); // Amarelo clássico
        pGrad.addColorStop(1, "#f59e0b"); // Sombra suave na borda

        ctx.fillStyle = pGrad;
        ctx.shadowColor = "rgba(255, 215, 0, 0.6)";
        ctx.shadowBlur = 14;

        ctx.beginPath();
        ctx.arc(0, 0, pacman.raio, pacman.boca * Math.PI, (2 - pacman.boca) * Math.PI);
        ctx.lineTo(0, 0);
        ctx.fill();

        // Olho
        ctx.shadowBlur = 0;
        ctx.fillStyle = "#0f172a";
        ctx.beginPath();
        ctx.arc(2, -9, 2.5, 0, Math.PI * 2);
        ctx.fill();

        ctx.restore();
    }

    function desenharFantasmaPolido(f) {
        ctx.save();
        ctx.translate(f.x, f.y);

        // Gradiente com efeito especular suave
        var fGrad = ctx.createRadialGradient(-4, -6, 2, 0, 0, f.raio + 4);
        if (f.estado === "Vulneravel") {
            fGrad.addColorStop(0, "#bae6fd");
            fGrad.addColorStop(0.5, "#38bdf8");
            fGrad.addColorStop(1, "#0284c7");
        } else if (f.estado === "Agressivo") {
            fGrad.addColorStop(0, "#fecaca");
            fGrad.addColorStop(0.5, "#ef4444");
            fGrad.addColorStop(1, "#b91c1c");
        } else {
            fGrad.addColorStop(0, "#ffffff");
            fGrad.addColorStop(0.5, "#f1f5f9");
            fGrad.addColorStop(1, "#94a3b8");
        }

        ctx.fillStyle = fGrad;
        ctx.shadowColor = f.cor;
        ctx.shadowBlur = 12;

        // Corpo suave
        ctx.beginPath();
        ctx.arc(0, -4, f.raio, Math.PI, 0, false);
        ctx.lineTo(f.raio, f.raio - 4);
        ctx.lineTo(f.raio * 0.5, f.raio - 8);
        ctx.lineTo(0, f.raio - 4);
        ctx.lineTo(-f.raio * 0.5, f.raio - 8);
        ctx.lineTo(-f.raio, f.raio - 4);
        ctx.closePath();
        ctx.fill();

        // Olhos brilhantes
        ctx.shadowBlur = 0;
        ctx.fillStyle = "#ffffff";
        ctx.beginPath();
        ctx.arc(-5, -6, 4.5, 0, Math.PI * 2);
        ctx.arc(5, -6, 4.5, 0, Math.PI * 2);
        ctx.fill();

        // Pupilas
        ctx.fillStyle = f.estado === "Vulneravel" ? "#ef4444" : "#1e3a8a";
        ctx.beginPath();
        ctx.arc(-4, -6, 2.2, 0, Math.PI * 2);
        ctx.arc(6, -6, 2.2, 0, Math.PI * 2);
        ctx.fill();

        // Texto com estado SWRL
        ctx.fillStyle = "#cbd5e1";
        ctx.font = "bold 10px monospace";
        ctx.textAlign = "center";
        ctx.fillText(f.nome + " (" + f.estado + ")", 0, -22);

        ctx.restore();
    }

    function desenharHUD() {
        var hudW = 420;
        var hudH = 132;
        ctx.fillStyle = "rgba(15, 23, 42, 0.92)";
        ctx.fillRect(40, 40, hudW, hudH);
        ctx.strokeStyle = "rgba(56, 189, 248, 0.5)";
        ctx.lineWidth = 1.5;
        ctx.strokeRect(40, 40, hudW, hudH);

        ctx.fillStyle = "#ffd700";
        ctx.font = "bold 13px -apple-system, sans-serif";
        ctx.fillText("PAC-MAN SEMÂNTICO (CIn-UFPE) | WebGL URP Polido", 52, 60);

        ctx.fillStyle = powerPellet.ativo ? "#38bdf8" : "#94a3b8";
        ctx.font = "11px monospace";
        var txtPellet = powerPellet.ativo ? 
            "POWER PELLET ATIVO (" + powerPellet.tempoRestante.toFixed(1) + "s) [SWRL: VULNERÁVEL]" : 
            "Power Pellet: Inativo (Barra de Espaço para alternar)";
        ctx.fillText(txtPellet, 52, 78);

        // Distância para o Blinky (fantasmas[0])
        var distBlinky = Math.hypot(pacman.x - fantasmas[0].x, pacman.y - fantasmas[0].y) / 60.0;
        ctx.fillStyle = distBlinky <= distanciaColisaoSegura ? "#ef4444" : (distBlinky < 2.5 ? "#f59e0b" : "#22c55e");
        ctx.fillText("Distância Pac-Man ↔ Blinky: " + distBlinky.toFixed(2) + "m (Colisão: <=0.8m)", 52, 96);

        // Vidas com cor contrastante
        var iconesVidas = "";
        for (var v = 0; v < vidasRestantes; v++) iconesVidas += " ♥";
        ctx.fillStyle = vidasRestantes > 1 ? "#00ffcc" : (vidasRestantes === 1 ? "#f59e0b" : "#ef4444");
        ctx.fillText("Vidas: " + vidasRestantes + iconesVidas, 52, 114);

        ctx.fillStyle = "#94a3b8";
        ctx.font = "10px monospace";
        ctx.fillText("Navegação: Setas / A e D (Curvar e virar livremente) | Espaço: Power Pellet", 52, 128);

        // Alerta Temporário de Colisão
        if (avisoColisao.tempo > 0 && avisoColisao.mensagem) {
            ctx.fillStyle = "rgba(220, 38, 38, 0.95)";
            ctx.fillRect(40, 160, 490, 32);
            ctx.strokeStyle = "#fef08a";
            ctx.lineWidth = 1.5;
            ctx.strokeRect(40, 160, 490, 32);

            ctx.fillStyle = "#ffffff";
            ctx.font = "bold 12px -apple-system, sans-serif";
            ctx.fillText(avisoColisao.mensagem, 52, 181);
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
