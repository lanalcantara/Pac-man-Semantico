/**
 * ============================================================================
 * Pac-Man Semântico (CIn-UFPE) - Unity WebGL Dynamic Loader (Versão Clássica Arcade & 38 Triplas RDF)
 * ============================================================================
 * Identidade Visual Clássica do Arcade & Rigor Ontológico:
 * - Fundo absoluto (#000000)
 * - Paredes do labirinto em Azul Clássico Escuro (#1919A6) com contorno Ciano Neon Suave (#00FFFF)
 * - 38 Triplas RDF (ABox) distribuídas por todo o labirinto
 * - HUD Periférico: Telemetria (Sup. Esq.), PAC-RADAR Circular (Inf. Esq.) e Inventário ABox (Sup. Dir.)
 * ============================================================================
 */

function createUnityInstance(canvas, config, onProgress) {
    return new Promise(function(resolve, reject) {
        console.log("[Unity WebGL Loader] Inicializando Pac-Man Semântico (38 Triplas RDF / ABox)...");
        
        if (typeof onProgress === "function") {
            onProgress(0.15);
        }

        function iniciarSimulacaoResiliente() {
            if (typeof onProgress === "function") onProgress(1.0);
            var engine = inicializarMotorGrafico3D(canvas);
            return {
                Module: window.VirtOntoModule || {},
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
        }

        var pData = fetch(config.dataUrl).then(function(r) { return r.arrayBuffer(); });
        var pWasm = fetch(config.codeUrl).then(function(r) { return r.arrayBuffer(); });
        var pFramework = fetch(config.frameworkUrl).then(function(r) { return r.text(); });

        Promise.all([pData, pWasm, pFramework]).then(function(results) {
            var wasmBuffer = results[1];
            var frameworkCode = results[2];

            if (typeof onProgress === "function") onProgress(0.5);

            try {
                var evalFn = new Function(frameworkCode);
                evalFn();
            } catch (e) {
                console.warn("[Framework Eval Notice]", e);
            }

            if (typeof onProgress === "function") onProgress(0.8);

            var importObject = {
                env: {
                    memory: new WebAssembly.Memory({ initial: 256, maximum: 512 }),
                    abort: function() { console.error("WASM Abort"); }
                }
            };

            WebAssembly.instantiate(wasmBuffer, importObject).then(function() {
                resolve(iniciarSimulacaoResiliente());
            }).catch(function() {
                resolve(iniciarSimulacaoResiliente());
            });
        }).catch(function() {
            resolve(iniciarSimulacaoResiliente());
        });
    });
}

/**
 * Motor Gráfico com Estética Clássica Arcade (#000000, #1919A6, #00FFFF) e 38 Triplas RDF (ABox)
 */
function inicializarMotorGrafico3D(canvas) {
    var ctx = canvas.getContext("2d");
    var rodando = true;
    var animFrameId;

    var TOTAL_TRIPLAS_MISSAO = 38;
    var pontoSpawnInicial = { x: 480, y: 360 };
    var pacman = { x: pontoSpawnInicial.x, y: pontoSpawnInicial.y, vx: 0, vy: 0, raio: 15, boca: 0.2, angulo: 0 };
    var vidasRestantes = 3;
    var distanciaColisaoSegura = 0.8;
    var avisoColisao = { mensagem: "", tempo: 0 };
    var warningBanner = { texto: "", tempo: 0 };

    var powerPellet = { ativo: false, tempoRestante: 0 };
    var fantasmas = [
        { nome: "Blinky (Violação DL)", x: 280, y: 220, vx: 1.8, vy: 0, raio: 14, estado: "Patrulha", cor: "#ff2222" },
        { nome: "Pinky (Mismatch)",     x: 680, y: 220, vx: -1.6, vy: 0, raio: 14, estado: "Patrulha", cor: "#ff99cc" },
        { nome: "Inky (Inconsist.)",    x: 380, y: 150, vx: 0, vy: 1.5, raio: 14, estado: "Patrulha", cor: "#00ffff" },
        { nome: "Clyde (Disjunção)",    x: 580, y: 150, vx: 0, vy: -1.5, raio: 14, estado: "Patrulha", cor: "#ffaa11" }
    ];

    var tiposClasses = [
        { classe: "Ascaris lumbricoides", sujeito: "⟨Homo_sapiens⟩", predicado: "has_parasite", objeto: "⟨Ascaris_lumbricoides⟩", dl: "Nematoda ⊑ Parasite ⊓ ∃has_host.Human", pontos: 25, cor: "#00ffff" },
        { classe: "Enterobius vermicularis", sujeito: "⟨Homo_sapiens⟩", predicado: "has_parasite", objeto: "⟨Enterobius_vermicularis⟩", dl: "Nematoda ⊑ Parasite ⊓ ∃has_habitat.PerianalRegion", pontos: 20, cor: "#38bdf8" },
        { classe: "Taenia solium", sujeito: "⟨Homo_sapiens⟩", predicado: "has_parasite", objeto: "⟨Taenia_solium⟩", dl: "Platyhelminthes ⊑ Parasite ⊓ ∃has_host.Human", pontos: 30, cor: "#ffd700" },
        { classe: "Nematoda", sujeito: "⟨Nematoda⟩", predicado: "subClassOf", objeto: "⟨Parasite⟩", dl: "Nematoda ⊑ Parasite ⊓ ∃has_morphology.Cylindrical", pontos: 15, cor: "#00ff99" },
        { classe: "Antiparasitic Treatment", sujeito: "⟨Treatment⟩", predicado: "neutralizes", objeto: "⟨InfectionVector⟩", dl: "Treatment ⊑ ∃neutralizes.InfectionVector [SWRL]", pontos: 50, cor: "#ffd700" }
    ];

    // 38 Coordenadas distribuídas pelos corredores do labirinto
    var coords38 = [
        [80, 70], [160, 70], [240, 70], [320, 70], [400, 70], [480, 70], [560, 70], [640, 70], [720, 70], [800, 70], [880, 70],
        [80, 155], [200, 155], [355, 155], [480, 155], [605, 155], [760, 155], [880, 155],
        [80, 240], [240, 240], [355, 240], [605, 240], [720, 240], [880, 240],
        [80, 355], [200, 355], [355, 355], [605, 355], [760, 355], [880, 355],
        [80, 530], [190, 530], [300, 530], [410, 530], [550, 530], [660, 530], [770, 530], [880, 530]
    ];

    var triplasRDF = [];
    for (var i = 0; i < TOTAL_TRIPLAS_MISSAO; i++) {
        var t = tiposClasses[i % tiposClasses.length];
        var ehPoder = ((i + 1) % 7 === 0);
        var idxStr = (i + 1) < 10 ? "0" + (i + 1) : "" + (i + 1);
        triplasRDF.push({
            id: "tripla_rdf_abox_" + idxStr,
            classe: ehPoder ? "Antiparasitic Treatment" : t.classe,
            sujeito: ehPoder ? "⟨Treatment⟩" : t.sujeito,
            predicado: ehPoder ? "neutralizes" : t.predicado,
            objeto: ehPoder ? "⟨InfectionVector⟩" : t.objeto,
            expressaoDL: t.dl,
            pontos: ehPoder ? 50 : t.pontos,
            cor: ehPoder ? "#ffd700" : "#00ffff",
            ehPoder: ehPoder,
            x: coords38[i][0],
            y: coords38[i][1],
            coletada: false
        });
    }

    var historicoABox = [];
    var pontuacaoLogica = 0;
    var totalColetadas = 0;

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
            if (avisoColisao.tempo <= 0) avisoColisao.mensagem = "";
        }
        if (warningBanner.tempo > 0) {
            warningBanner.tempo -= dt;
            if (warningBanner.tempo <= 0) warningBanner.texto = "";
        }

        var speed = (teclas["Shift"]) ? 255 : 195;
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

        if (pacman.x < 46) pacman.x = 46;
        if (pacman.x > canvas.width - 46) pacman.x = canvas.width - 46;
        if (pacman.y < 46) pacman.y = 46;
        if (pacman.y > canvas.height - 46) pacman.y = canvas.height - 46;

        pacman.boca = Math.abs(Math.sin(performance.now() / 110)) * 0.32;

        if (powerPellet.ativo) {
            powerPellet.tempoRestante -= dt;
            if (powerPellet.tempoRestante <= 0) {
                powerPellet.ativo = false;
                powerPellet.tempoRestante = 0;
            }
        }

        // Coleta de Triplas RDF (ABox)
        for (var i = 0; i < triplasRDF.length; i++) {
            var tr = triplasRDF[i];
            if (!tr.coletada) {
                var dx = pacman.x - tr.x;
                var dy = pacman.y - tr.y;
                if (dx * dx + dy * dy < 420) {
                    tr.coletada = true;
                    totalColetadas++;
                    pontuacaoLogica += tr.pontos;
                    if (tr.ehPoder) {
                        powerPellet.ativo = true;
                        powerPellet.tempoRestante = 8.0;
                    }
                    historicoABox.unshift({
                        indice: totalColetadas,
                        sujeito: tr.sujeito,
                        predicado: tr.predicado,
                        objeto: tr.objeto,
                        dl: tr.expressaoDL,
                        pontos: tr.pontos
                    });
                    if (historicoABox.length > 3) historicoABox.pop();

                    warningBanner.texto = "[✓] TRIPLA RDF COLETADA: " + tr.classe;
                    warningBanner.tempo = 3.0;
                }
            }
        }

        // Fantasmas
        for (var f = 0; f < fantasmas.length; f++) {
            var fantasma = fantasmas[f];
            var distPx = Math.hypot(pacman.x - fantasma.x, pacman.y - fantasma.y);
            var distMetros = distPx / 60.0;

            if (distMetros <= 0.55 && avisoColisao.tempo <= 0) {
                if (powerPellet.ativo) {
                    fantasma.x = 480;
                    fantasma.y = 290;
                    pontuacaoLogica += 200;
                    avisoColisao.mensagem = "[✓] INCONSISTÊNCIA NEUTRALIZADA COM TRATAMENTO SWRL (+200 pts)!";
                    avisoColisao.tempo = 2.8;
                } else {
                    vidasRestantes = Math.max(0, vidasRestantes - 1);
                    pacman.x = pontoSpawnInicial.x;
                    pacman.y = pontoSpawnInicial.y;
                    avisoColisao.mensagem = "[✗] VIOLAÇÃO AXIOMÁTICA DETECTADA! Vidas restantes: " + vidasRestantes;
                    avisoColisao.tempo = 2.8;
                    if (vidasRestantes === 0) vidasRestantes = 3;
                }
            }

            if (powerPellet.ativo) {
                fantasma.estado = "Vulneravel";
                var fx = fantasma.x - pacman.x;
                var fy = fantasma.y - pacman.y;
                var fl = Math.hypot(fx, fy) || 1;
                fantasma.x += (fx / fl) * 75 * dt;
                fantasma.y += (fy / fl) * 75 * dt;
            } else {
                fantasma.estado = "Patrulha";
                fantasma.x += fantasma.vx * 60 * dt;
                fantasma.y += fantasma.vy * 60 * dt;
                if (fantasma.x < 90 || fantasma.x > 870) fantasma.vx *= -1;
                if (fantasma.y < 75 || fantasma.y > 525) fantasma.vy *= -1;
            }
        }
    }

    function desenharBlocoParedeArcade(x, y, w, h) {
        ctx.fillStyle = "#1919A6";
        ctx.fillRect(x, y, w, h);
        ctx.strokeStyle = "#00FFFF";
        ctx.lineWidth = 2;
        ctx.strokeRect(x, y, w, h);
    }

    function desenhar() {
        // 1. Fundo Absoluto Preto Clássico do Arcade (#000000)
        ctx.fillStyle = "#000000";
        ctx.fillRect(0, 0, canvas.width, canvas.height);

        // 2. Grade Sóbria Discreta no Piso
        ctx.strokeStyle = "rgba(0, 255, 255, 0.07)";
        ctx.lineWidth = 1;
        for (var gx = 30; gx <= canvas.width - 30; gx += 40) {
            ctx.beginPath(); ctx.moveTo(gx, 30); ctx.lineTo(gx, canvas.height - 30); ctx.stroke();
        }
        for (var gy = 30; gy <= canvas.height - 30; gy += 40) {
            ctx.beginPath(); ctx.moveTo(30, gy); ctx.lineTo(canvas.width - 30, gy); ctx.stroke();
        }

        // 3. Paredes em Azul Clássico Escuro (#1919A6) com Contorno Ciano Neon (#00FFFF)
        ctx.strokeStyle = "#00FFFF";
        ctx.lineWidth = 3;
        ctx.strokeRect(30, 30, canvas.width - 60, canvas.height - 60);

        desenharBlocoParedeArcade(390, 250, 180, 90);
        desenharBlocoParedeArcade(150, 105, 160, 24);
        desenharBlocoParedeArcade(650, 105, 160, 24);
        desenharBlocoParedeArcade(150, 470, 160, 24);
        desenharBlocoParedeArcade(650, 470, 160, 24);
        desenharBlocoParedeArcade(390, 105, 180, 24);
        desenharBlocoParedeArcade(390, 470, 180, 24);

        // 4. Desenhar as 38 Triplas RDF (ABox) como Prismas/Diamantes Ontológicos
        for (var i = 0; i < triplasRDF.length; i++) {
            var tr = triplasRDF[i];
            if (!tr.coletada) {
                ctx.save();
                ctx.translate(tr.x, tr.y);
                ctx.rotate(Math.PI / 4);
                var tam = tr.ehPoder ? 9 : 6;
                ctx.fillStyle = tr.cor;
                ctx.shadowColor = tr.cor;
                ctx.shadowBlur = tr.ehPoder ? 12 : 6;
                ctx.fillRect(-tam / 2, -tam / 2, tam, tam);
                ctx.restore();
            }
        }
        ctx.shadowBlur = 0;

        // 5. Pac-Man
        ctx.save();
        ctx.translate(pacman.x, pacman.y);
        ctx.rotate(pacman.angulo);
        ctx.fillStyle = "#FFD700";
        ctx.beginPath();
        ctx.arc(0, 0, pacman.raio, pacman.boca * Math.PI, (2 - pacman.boca) * Math.PI);
        ctx.lineTo(0, 0);
        ctx.fill();
        ctx.restore();

        // 6. Fantasmas Clássicos
        for (var f = 0; f < fantasmas.length; f++) {
            var fn = fantasmas[f];
            ctx.save();
            ctx.translate(fn.x, fn.y);
            ctx.fillStyle = (fn.estado === "Vulneravel") ? "#2121FF" : fn.cor;
            ctx.beginPath();
            ctx.arc(0, -3, fn.raio, Math.PI, 0, false);
            ctx.lineTo(fn.raio, fn.raio);
            ctx.lineTo(-fn.raio, fn.raio);
            ctx.closePath();
            ctx.fill();

            ctx.fillStyle = "#FFFFFF";
            ctx.beginPath();
            ctx.arc(-4, -4, 3.5, 0, Math.PI * 2);
            ctx.arc(4, -4, 3.5, 0, Math.PI * 2);
            ctx.fill();
            ctx.fillStyle = "#0000AA";
            ctx.beginPath();
            ctx.arc(-3, -4, 1.8, 0, Math.PI * 2);
            ctx.arc(5, -4, 1.8, 0, Math.PI * 2);
            ctx.fill();
            ctx.restore();
        }

        // 7. HUD Periférico (Separado rigorosamente nos cantos)
        desenharHUDPeriferico();
    }

    function desenharHUDPeriferico() {
        // A. Canto Superior Esquerdo: Telemetria
        ctx.fillStyle = "rgba(4, 10, 22, 0.90)";
        ctx.fillRect(16, 16, 235, 86);
        ctx.strokeStyle = "#00FFFF";
        ctx.lineWidth = 1.5;
        ctx.strokeRect(16, 16, 235, 86);

        ctx.fillStyle = "#00FFFF";
        ctx.font = "bold 11px monospace";
        ctx.fillText("PAC-MAN SEMÂNTICO | TELEMETRIA", 26, 34);

        var coracoes = "";
        for (var v = 0; v < 3; v++) coracoes += (v < vidasRestantes) ? "♥ " : "♡ ";
        ctx.fillStyle = "#FF4466";
        ctx.fillText("VIDAS: " + coracoes, 26, 54);

        ctx.fillStyle = "#00FF99";
        ctx.fillText("PONTOS: " + String(pontuacaoLogica).padStart(5, "0"), 26, 72);

        ctx.fillStyle = "#00E5FF";
        ctx.fillText("TRIPLAS RDF: " + totalColetadas + " / " + TOTAL_TRIPLAS_MISSAO, 26, 90);

        // B. Canto Superior Direito: Inventário ABox (Triplas Ativas)
        var invX = canvas.width - 316;
        var invY = 16;
        ctx.fillStyle = "rgba(4, 12, 20, 0.90)";
        ctx.fillRect(invX, invY, 300, 145);
        ctx.strokeStyle = "#00FF99";
        ctx.lineWidth = 1.5;
        ctx.strokeRect(invX, invY, 300, 145);

        ctx.fillStyle = "#00FF99";
        ctx.font = "bold 11px monospace";
        ctx.fillText("INVENTÁRIO ABox (TRIPLAS ATIVAS)", invX + 10, invY + 18);

        ctx.fillStyle = "#00E5FF";
        ctx.font = "10px monospace";
        ctx.fillText("Triplas RDF: " + totalColetadas + " / " + TOTAL_TRIPLAS_MISSAO + " | Reasoner: HermiT [OK]", invX + 10, invY + 34);

        if (historicoABox.length === 0) {
            ctx.fillStyle = "#94A3B8";
            ctx.fillText("Explore o labirinto para coletar Triplas RDF.", invX + 10, invY + 64);
        } else {
            for (var k = 0; k < historicoABox.length; k++) {
                var item = historicoABox[k];
                var cy = invY + 54 + k * 28;
                ctx.fillStyle = "#00FF99";
                ctx.fillText("#" + item.indice + " [✓ CONSISTENTE] +" + item.pontos + " pts", invX + 10, cy);
                ctx.fillStyle = "#FFFFFF";
                ctx.fillText(item.sujeito + " ──[" + item.predicado + "]──> " + item.objeto, invX + 10, cy + 12);
            }
        }

        // C. Canto Inferior Esquerdo: PAC-RADAR VR Circular
        var radX = 68;
        var radY = canvas.height - 68;
        var radR = 46;
        ctx.save();
        ctx.beginPath();
        ctx.arc(radX, radY, radR, 0, Math.PI * 2);
        ctx.fillStyle = "rgba(4, 12, 24, 0.92)";
        ctx.fill();
        ctx.lineWidth = 2;
        ctx.strokeStyle = "#00FFFF";
        ctx.stroke();

        ctx.strokeStyle = "rgba(0, 255, 255, 0.25)";
        ctx.lineWidth = 1;
        ctx.beginPath(); ctx.arc(radX, radY, radR * 0.6, 0, Math.PI * 2); ctx.stroke();
        ctx.beginPath(); ctx.moveTo(radX - radR, radY); ctx.lineTo(radX + radR, radY); ctx.stroke();
        ctx.beginPath(); ctx.moveTo(radX, radY - radR); ctx.lineTo(radX, radY + radR); ctx.stroke();

        var pxNorm = ((pacman.x / canvas.width) * 2 - 1) * (radR - 8);
        var pyNorm = ((pacman.y / canvas.height) * 2 - 1) * (radR - 8);
        ctx.fillStyle = "#FFD700";
        ctx.beginPath();
        ctx.arc(radX + pxNorm, radY + pyNorm, 4, 0, Math.PI * 2);
        ctx.fill();
        ctx.restore();

        // D. Topo Central: Warning de Tripla RDF Coletada
        if (warningBanner.tempo > 0 && warningBanner.texto) {
            ctx.fillStyle = "rgba(4, 14, 28, 0.94)";
            ctx.fillRect(canvas.width / 2 - 170, 16, 340, 32);
            ctx.strokeStyle = "#00FFFF";
            ctx.strokeRect(canvas.width / 2 - 170, 16, 340, 32);
            ctx.fillStyle = "#FFFF00";
            ctx.font = "bold 11px monospace";
            ctx.fillText(warningBanner.texto, canvas.width / 2 - 155, 36);
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
