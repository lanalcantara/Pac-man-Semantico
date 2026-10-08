using System;
using UnityEngine;
using PacMan.Semantics;

namespace PacMan.Web
{
    /// <summary>
    /// ============================================================================
    /// PAC-MAN SEMÂNTICO (CIn-UFPE) - Controlador de Demonstração WebGL
    /// ============================================================================
    /// Componente central da cena Demo WebGL (CenaDemoWeb.unity) voltada para o
    /// GitHub Pages. Executa a simulação ontológica totalmente autocontida e
    /// provê interface gráfica imediata no navegador (HUD, métricas e controles).
    /// ============================================================================
    /// </summary>
    [AddComponentMenu("Pac-Man Semântico/Web/Controlador Demo Web")]
    public class ControladorDemoWeb : MonoBehaviour
    {
        [Header("Referências")]
        public MotorRaciocinioSemantico motorSemantico;
        public AvaliadorRegrasRuntime avaliadorRegras;
        public Transform pacmanTransform;
        public Transform ghostTransform;
        [Tooltip("Referência direta ao fantasma Blinky para verificação de colisão")]
        public Transform ghostBlinkyTransform;

        [Header("Configuração de Demonstração")]
        public bool exibirGuiWebGL = true;
        public float duracaoPowerPelletSimulado = 8.0f;

        [Header("Colisão e Reset")]
        [Tooltip("Distância limite para detecção de toque entre Pac-Man e Blinky (padrão: 0.8m)")]
        public float distanciaColisaoSegura = 0.8f;
        [Tooltip("Ponto inicial (Spawn) para onde o Pac-Man é reposicionado ao colidir")]
        public Vector3 pontoSpawnInicial = Vector3.zero;
        public int vidasRestantes = 3;
        public float tempoAvisoColisao = 3.0f;
        private float temporizadorAvisoColisao = 0f;
        private string mensagemAvisoColisao = "";

        [Header("Feedback de Captura Semântica (Tempo Real)")]
        [Tooltip("Nome da última instância ontológica capturada/colidida.")]
        public string ultimaInstanciaCapturada = "Nenhuma";
        [Tooltip("Nome da classe ontológica correspondente.")]
        public string ultimaClasseCapturada = "Nenhuma";
        [Tooltip("Expressão Description Logic da última instância.")]
        public string ultimoAxiomaDL = "";
        public bool ultimoItemValido = true;
        public int pontuacaoAcumulada = 0;
        public int totalItensCapturados = 0;
        public float tempoDestaqueCaptura = 3.5f;
        private float temporizadorDestaqueCaptura = 0f;

        private void OnEnable()
        {
            InstanciaSemanticaBase.OnInstanciaInteragida += AoInteragirInstanciaSemantica;
        }

        private void OnDisable()
        {
            InstanciaSemanticaBase.OnInstanciaInteragida -= AoInteragirInstanciaSemantica;
        }

        private void Start()
        {
            if (motorSemantico == null)
            {
                motorSemantico = FindFirstObjectByType<MotorRaciocinioSemantico>();
            }

            if (avaliadorRegras == null)
            {
                avaliadorRegras = FindFirstObjectByType<AvaliadorRegrasRuntime>();
            }

            if (pacmanTransform == null)
            {
                GameObject player = GameObject.FindWithTag("Player");
                if (player != null) pacmanTransform = player.transform;
                else if (Camera.main != null) pacmanTransform = Camera.main.transform;
            }

            if (ghostTransform == null && avaliadorRegras != null)
            {
                ghostTransform = avaliadorRegras.ghostTransform;
            }

            if (ghostBlinkyTransform == null)
            {
                ghostBlinkyTransform = ghostTransform != null ? ghostTransform : (avaliadorRegras != null ? avaliadorRegras.ghostTransform : null);
            }

            if (pacmanTransform != null)
            {
                pontoSpawnInicial = pacmanTransform.position;
            }

            Debug.Log("<color=#00FFFF><b>[Demo WebGL]</b></color> Pac-Man Semântico iniciado em modo demonstração browser.");
        }

        private void Update()
        {
            // Atalho de teclado para alternar Power Pellet via Barra de Espaço
            if (Input.GetKeyDown(KeyCode.Space))
            {
                AlternarPowerPellet();
            }

            // Atualiza temporizador do aviso na tela
            if (temporizadorAvisoColisao > 0f)
            {
                temporizadorAvisoColisao -= Time.deltaTime;
                if (temporizadorAvisoColisao <= 0f)
                {
                    mensagemAvisoColisao = "";
                }
            }

            // Atualiza temporizador do destaque de captura ontológica
            if (temporizadorDestaqueCaptura > 0f)
            {
                temporizadorDestaqueCaptura -= Time.deltaTime;
            }

            // Deteção de Proximidade / Colisão entre Pac-Man e Blinky
            VerificarColisaoPacmanBlinky();
        }

        /// <summary>
        /// Callback acionado automaticamente ao colidir/interagir com qualquer instância semântica (Gemas, Obstáculos, etc.)
        /// </summary>
        private void AoInteragirInstanciaSemantica(IInstanciaSemantica inst, bool sucesso)
        {
            if (inst == null) return;
            RegistrarCapturaItem(inst.Id, inst.NomeClasse, inst.EhValida, inst.ValorPontuacao, inst.ExpressaoDL);
        }

        /// <summary>
        /// Registra a captura/colisão ontológica de um item e atualiza o painel HUD em tempo real.
        /// </summary>
        public void RegistrarCapturaItem(string idInstancia, string nomeClasse, bool ehValida, int pontos, string expressaoDL = "")
        {
            ultimaInstanciaCapturada = string.IsNullOrEmpty(idInstancia) ? "Instancia_Desconhecida" : idInstancia;
            ultimaClasseCapturada = string.IsNullOrEmpty(nomeClasse) ? "ClasseOntologica" : nomeClasse;
            ultimoItemValido = ehValida;
            ultimoAxiomaDL = expressaoDL;
            totalItensCapturados++;
            pontuacaoAcumulada += pontos;
            temporizadorDestaqueCaptura = tempoDestaqueCaptura;

            string status = ehValida ? "<color=#00FF66>VÁLIDA</color>" : "<color=#FF3333>INVÁLIDA</color>";
            string pontosStr = pontos >= 0 ? $"+{pontos}" : $"{pontos}";
            Debug.Log($"<color=#00FFCC><b>[HUD WebGL - Captura Ontológica]</b></color> Instância: <b>{ultimaInstanciaCapturada}</b> | Classe: <b>{ultimaClasseCapturada}</b> [{status}] | DL: [{expressaoDL}] | Pontos: {pontosStr} (Total: {pontuacaoAcumulada})");
        }

        /// <summary>
        /// Deteção de Proximidade / Colisão: No Update(), calcula a distância entre Pac-Man e Blinky
        /// </summary>
        public void VerificarColisaoPacmanBlinky()
        {
            if (pacmanTransform == null) return;
            Transform alvoFantasma = ghostBlinkyTransform != null ? ghostBlinkyTransform : ghostTransform;
            if (alvoFantasma == null) return;

            // Calcula a distância euclidiana entre o Pac-Man e o Blinky
            float distancia = Vector3.Distance(pacmanTransform.position, alvoFantasma.position);

            // Ação ao Colidir: quando a distância for menor ou igual ao limite seguro (0.8m)
            if (distancia <= distanciaColisaoSegura && temporizadorAvisoColisao <= 0f)
            {
                // Se o Pac-Man estiver com Power Pellet ativo (fantasma vulnerável)
                if (motorSemantico != null && motorSemantico.pacmanIsPowered)
                {
                    RegistrarCapturaItem("ghost_blinky", "AggressiveGhost ⊓ Vulnerable", true, 200, "Ghost ⊑ Vulnerable");
                    ExibirAvisoColisao("👻 FANTASMA DEVORADO! Pac-Man consumiu o fantasma vulnerável (+200 pts)!");
                    // Reposiciona o fantasma no centro da arena
                    alvoFantasma.position = new Vector3(0, alvoFantasma.position.y, 0);
                    Debug.Log($"<color=#00FF99><b>[Colisão WebGL]</b></color> Fantasma consumido a {distancia:F2}m!");
                }
                else
                {
                    // Fantasma apanhou o Pac-Man -> Ativa a rotina de reset
                    ExecutarResetColisao(distancia);
                }
            }
        }

        /// <summary>
        /// Rotina de reset executada quando o fantasma toca no Pac-Man:
        /// 1. Reposiciona o Pac-Man de volta ao ponto inicial (Spawn)
        /// 2. Atualiza o HUD na tela indicando que o Pac-Man foi apanhado e perdeu uma vida
        /// </summary>
        public void ExecutarResetColisao(float distanciaDetectada = 0f)
        {
            vidasRestantes = Mathf.Max(0, vidasRestantes - 1);

            // Reposiciona o Pac-Man de volta ao ponto inicial (Spawn)
            if (pacmanTransform != null)
            {
                pacmanTransform.position = pontoSpawnInicial;

                // Anula velocidades físicas se houver Rigidbody
                var rb = pacmanTransform.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }

            // Atualiza o HUD e exibe aviso temporário
            string msg = $"⚠️ O PAC-MAN FOI APANHADO! Perdeu 1 vida. Vidas restantes: {vidasRestantes}";
            ExibirAvisoColisao(msg);

            Debug.LogWarning($"<color=#FF3366><b>[Colisão WebGL]</b></color> Pac-Man apanhado pelo Blinky a {distanciaDetectada:F2}m (limite: {distanciaColisaoSegura:F2}m)! " +
                             $"Reposicionado para Spawn {pontoSpawnInicial}. Vidas: {vidasRestantes}.");
        }

        public void ExibirAvisoColisao(string msg)
        {
            mensagemAvisoColisao = msg;
            temporizadorAvisoColisao = tempoAvisoColisao;
        }

        public void AlternarPowerPellet()
        {
            if (motorSemantico != null)
            {
                if (motorSemantico.pacmanIsPowered)
                {
                    motorSemantico.ResetarEstados();
                }
                else
                {
                    motorSemantico.AtivarPowerPellet(duracaoPowerPelletSimulado);
                }
            }
        }

        private void OnGUI()
        {
            if (!exibirGuiWebGL) return;

            GUI.skin.box.fontSize = 12;
            GUI.skin.button.fontSize = 12;

            float largura = 360f;
            float altura = 345f;
            Rect painel = new Rect(15, 15, largura, altura);

            GUI.Box(painel, "🕹️ Pac-Man Semântico (CIn-UFPE) | WebGL Demo");

            GUILayout.BeginArea(new Rect(25, 42, largura - 20, altura - 48));

            // Estado Ontológico
            string estadoTxt = avaliadorRegras != null ? avaliadorRegras.ultimoEstadoInferido.ToString() : "Patrol";
            string corHex = estadoTxt switch
            {
                "Vulnerable" => "#3399FF",
                "Aggressive" => "#FF3333",
                _ => "#FFFFFF"
            };

            GUILayout.Label($"<b>Regra SWRL:</b> <color={corHex}><b>{estadoTxt.ToUpper()}</b></color>");

            // Distância Pac-Man <-> Blinky (sem códigos de escape HTML)
            Transform alvoFantasma = ghostBlinkyTransform != null ? ghostBlinkyTransform : ghostTransform;
            float dist = (pacmanTransform != null && alvoFantasma != null) ?
                Vector3.Distance(pacmanTransform.position, alvoFantasma.position) : 
                (avaliadorRegras != null ? avaliadorRegras.distanciaAtual : 0f);
            GUILayout.Label($"<b>Distância Pac-Man ↔ Blinky:</b> {dist:F2}m (Colisão: <={distanciaColisaoSegura:F1}m)");

            // Vidas do Jogador
            string iconesVidas = vidasRestantes > 0 ? new string('♥', vidasRestantes) : "ZERO (GAME OVER)";
            string corVidas = vidasRestantes > 1 ? "#00FF99" : (vidasRestantes == 1 ? "#FFCC00" : "#FF3333");
            GUILayout.Label($"<b>Vidas Pac-Man:</b> <color={corVidas}><b>{vidasRestantes} {iconesVidas}</b></color>");

            // Power Pellet
            bool isPowered = motorSemantico != null && motorSemantico.pacmanIsPowered;
            string poderTxt = isPowered ? $"ATIVO ({motorSemantico.tempoPowerRestante:F1}s)" : "INATIVO";
            GUILayout.Label($"<b>Power Pellet:</b> {poderTxt}");

            // FEEDBACK VISUAL DE CAPTURA EM TEMPO REAL (Instâncias & Classes Ontológicas)
            GUILayout.Space(2);
            if (totalItensCapturados > 0 && !string.IsNullOrEmpty(ultimaInstanciaCapturada) && ultimaInstanciaCapturada != "Nenhuma")
            {
                string corDestaque = temporizadorDestaqueCaptura > 0f ? (ultimoItemValido ? "#00FFCC" : "#FF6688") : "#CBD5E1";
                string statusValid = ultimoItemValido ? "<color=#00FF66>VÁLIDA</color>" : "<color=#FF3333>INVÁLIDA</color>";
                
                GUILayout.Label($"<b>Última Instância:</b> <color={corDestaque}><b>{ultimaInstanciaCapturada}</b></color>");
                GUILayout.Label($"<b>Classe Ontológica:</b> <color=#38BDF8><b>{ultimaClasseCapturada}</b></color> [{statusValid}]");
                
                if (!string.IsNullOrEmpty(ultimoAxiomaDL))
                {
                    GUILayout.Label($"<size=10><b>Axioma DL:</b> {ultimoAxiomaDL}</size>");
                }
                GUILayout.Label($"<b>Pontuação Lógica:</b> <color=#FFD700><b>{pontuacaoAcumulada} pts</b></color> ({totalItensCapturados} itens)");
            }
            else
            {
                GUILayout.Label("<b>Última Instância:</b> <color=#94A3B8><i>Nenhuma captura ainda</i></color>");
                GUILayout.Label("<b>Classe Ontológica:</b> <color=#94A3B8><i>Aguardando colisão com item</i></color>");
            }

            // Aviso de Colisão em Destaque
            if (!string.IsNullOrEmpty(mensagemAvisoColisao))
            {
                GUI.color = Color.yellow;
                GUILayout.Box($"<b>{mensagemAvisoColisao}</b>");
                GUI.color = Color.white;
            }

            GUILayout.Space(2);

            // Botão de Interação
            string btnTxt = isPowered ? "Desativar Power Pellet" : "⚡ Ativar Power Pellet (Regra Vulnerável)";
            if (GUILayout.Button(btnTxt, GUILayout.Height(24)))
            {
                AlternarPowerPellet();
            }

            GUILayout.Space(2);
            GUILayout.Label("<size=10><b>Controles:</b> [WASD/Setas] Mover | [Q/E ou Mouse] Olhar | [Espaço] Pellet</size>");

            GUILayout.EndArea();
        }
    }
}
