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

            // Deteção de Proximidade / Colisão entre Pac-Man e Blinky
            VerificarColisaoPacmanBlinky();
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

            float largura = 340f;
            float altura = 270f;
            Rect painel = new Rect(15, 15, largura, altura);

            GUI.Box(painel, "🕹️ Pac-Man Semântico (CIn-UFPE) | WebGL Demo");

            GUILayout.BeginArea(new Rect(25, 45, largura - 20, altura - 40));

            // Estado Ontológico
            string estadoTxt = avaliadorRegras != null ? avaliadorRegras.ultimoEstadoInferido.ToString() : "Patrol";
            string corHex = estadoTxt switch
            {
                "Vulnerable" => "#3399FF",
                "Aggressive" => "#FF3333",
                _ => "#FFFFFF"
            };

            GUILayout.Label($"<b>Regra SWRL:</b> <color={corHex}><b>{estadoTxt.ToUpper()}</b></color>");

            // Distância Pac-Man <-> Blinky
            Transform alvoFantasma = ghostBlinkyTransform != null ? ghostBlinkyTransform : ghostTransform;
            float dist = (pacmanTransform != null && alvoFantasma != null) ?
                Vector3.Distance(pacmanTransform.position, alvoFantasma.position) : 
                (avaliadorRegras != null ? avaliadorRegras.distanciaAtual : 0f);
            GUILayout.Label($"<b>Distância Pac-Man ↔ Blinky:</b> {dist:F2}m (Colisão: &lt;={distanciaColisaoSegura:F1}m)");

            // Vidas do Jogador
            string iconesVidas = vidasRestantes > 0 ? new string('♥', vidasRestantes) : "ZERO (GAME OVER)";
            string corVidas = vidasRestantes > 1 ? "#00FF99" : (vidasRestantes == 1 ? "#FFCC00" : "#FF3333");
            GUILayout.Label($"<b>Vidas Pac-Man:</b> <color={corVidas}><b>{vidasRestantes} {iconesVidas}</b></color>");

            // Power Pellet
            bool isPowered = motorSemantico != null && motorSemantico.pacmanIsPowered;
            string poderTxt = isPowered ? $"ATIVO ({motorSemantico.tempoPowerRestante:F1}s)" : "INATIVO";
            GUILayout.Label($"<b>Power Pellet:</b> {poderTxt}");

            // Aviso de Colisão em Destaque
            if (!string.IsNullOrEmpty(mensagemAvisoColisao))
            {
                GUI.color = Color.yellow;
                GUILayout.Box($"<b>{mensagemAvisoColisao}</b>");
                GUI.color = Color.white;
            }

            GUILayout.Space(4);

            // Botão de Interação
            string btnTxt = isPowered ? "Desativar Power Pellet" : "⚡ Ativar Power Pellet (Regra Vulnerável)";
            if (GUILayout.Button(btnTxt, GUILayout.Height(26)))
            {
                AlternarPowerPellet();
            }

            GUILayout.Space(4);
            GUILayout.Label("<size=10><b>Controles:</b> [WASD/Setas] Mover | [Q/E ou Mouse] Olhar | [Espaço] Pellet</size>");

            GUILayout.EndArea();
        }
    }
}
