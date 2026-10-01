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

        [Header("Configuração de Demonstração")]
        public bool exibirGuiWebGL = true;
        public float duracaoPowerPelletSimulado = 8.0f;

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

            Debug.Log("<color=#00FFFF><b>[Demo WebGL]</b></color> Pac-Man Semântico iniciado em modo demonstração browser.");
        }

        private void Update()
        {
            // Atalho de teclado para alternar Power Pellet via Barra de Espaço
            if (Input.GetKeyDown(KeyCode.Space))
            {
                AlternarPowerPellet();
            }
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

            float largura = 320f;
            float altura = 230f;
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

            // Distância
            float dist = (pacmanTransform != null && ghostTransform != null) ?
                Vector3.Distance(pacmanTransform.position, ghostTransform.position) : 
                (avaliadorRegras != null ? avaliadorRegras.distanciaAtual : 0f);
            GUILayout.Label($"<b>Distância Pac-Man ↔ Fantasma:</b> {dist:F2}m");

            // Power Pellet
            bool isPowered = motorSemantico != null && motorSemantico.pacmanIsPowered;
            string poderTxt = isPowered ? $"ATIVO ({motorSemantico.tempoPowerRestante:F1}s)" : "INATIVO";
            GUILayout.Label($"<b>Power Pellet:</b> {poderTxt}");

            GUILayout.Space(6);

            // Botão de Interação
            string btnTxt = isPowered ? "Desativar Power Pellet" : "⚡ Ativar Power Pellet (Regra Vulnerável)";
            if (GUILayout.Button(btnTxt, GUILayout.Height(28)))
            {
                AlternarPowerPellet();
            }

            GUILayout.Space(6);
            GUILayout.Label("<size=10><b>Controles:</b> [WASD/Setas] Mover | [Q/E ou Botão Direito] Olhar | [Espaço] Power Pellet</size>");

            GUILayout.EndArea();
        }
    }
}
