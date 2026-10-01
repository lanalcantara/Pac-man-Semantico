using System;
using UnityEngine;
using UnityEngine.XR;
using VirtOnto.Model;
using PacMan.Semantics;

namespace PacMan.Player
{
    /// <summary>
    /// ============================================================================
    /// PAC-MAN SEMÂNTICO (CIn-UFPE) - Controlador do Jogador (Teclado + XR Origin)
    /// ============================================================================
    /// Gerencia a locomoção do Pac-Man tanto em ambiente de testes no Unity Editor
    /// (WASD / Setas) quanto em Realidade Virtual imersiva (XR Origin / XR Interaction Toolkit).
    /// 
    /// Responsabilidades:
    /// 1. Suporte a controles híbridos: Teclado/Mouse e Thumbsticks 2D de RV (OpenXR).
    /// 2. Detecção de colisões ontológicas (OnTriggerEnter) para consumo de itens e gemas.
    /// 3. Comunicação bidirecional com o MotorRaciocinioSemantico (estados do jogador
    ///    e inferência SWRL que torna os fantasmas vulneráveis).
    /// ============================================================================
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public class ControladorJogador : MonoBehaviour
    {
        [Header("Configurações de Movimento")]
        [Tooltip("Velocidade linear de deslocamento do jogador.")]
        public float velocidadeMovimento = 5.0f;

        [Tooltip("Velocidade de rotação angular (giro horizontal suave ou snap turn).")]
        public float velocidadeGiro = 90.0f;

        [Tooltip("Aceleração gravitacional aplicada ao CharacterController.")]
        public float gravidade = 9.81f;

        [Header("Referências Semânticas")]
        [Tooltip("Instância ativa do Motor de Raciocínio Semântico.")]
        public MotorRaciocinioSemantico motorSemantico;

        [Tooltip("Referência opcional ao Gerenciador Central de Cenário.")]
        public GerenciadorCenarioSemantico gerenciadorCenario;

        [Header("Integração com XR Origin (RV)")]
        [Tooltip("Câmera principal do jogador (Headset / HMD / XR Camera). Se nula, busca Camera.main.")]
        public Transform cameraXR;

        [Tooltip("Habilita leitura direta de Thumbsticks e controladores XR.")]
        public bool habilitarControlesXR = true;

        [Header("Estado Ontológico do Jogador")]
        [SerializeField]
        [Tooltip("Indica se o jogador está com o estado Empowered ativo (após coletar Power Pellet).")]
        private bool estaPoderoso = false;

        private CharacterController characterController;
        private float m_VelocidadeVertical = 0f;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
        }

        private void Start()
        {
            LocalizarComponentes();
            ConectarComMotorSemantico();
        }

        private void OnEnable()
        {
            if (motorSemantico != null)
            {
                motorSemantico.OnPacManPowerStateChanged += TratarMudancaEstadoPoder;
            }
        }

        private void OnDisable()
        {
            if (motorSemantico != null)
            {
                motorSemantico.OnPacManPowerStateChanged -= TratarMudancaEstadoPoder;
            }
        }

        private void Update()
        {
            ProcessarMovimentoHibrido();
        }

        /// <summary>
        /// Localiza automaticamente câmeras e referências no cenário.
        /// </summary>
        private void LocalizarComponentes()
        {
            if (cameraXR == null)
            {
                if (Camera.main != null)
                {
                    cameraXR = Camera.main.transform;
                }
                else
                {
                    Camera cam = GetComponentInChildren<Camera>();
                    if (cam != null) cameraXR = cam.transform;
                }
            }

            if (motorSemantico == null)
            {
                motorSemantico = FindFirstObjectByType<MotorRaciocinioSemantico>() ?? FindAnyObjectByType<MotorRaciocinioSemantico>();
            }

            if (gerenciadorCenario == null)
            {
                gerenciadorCenario = FindFirstObjectByType<GerenciadorCenarioSemantico>() ?? FindAnyObjectByType<GerenciadorCenarioSemantico>();
            }
        }

        /// <summary>
        /// Estabelece a ligação com o Motor de Raciocínio Semântico.
        /// </summary>
        private void ConectarComMotorSemantico()
        {
            if (motorSemantico != null)
            {
                motorSemantico.alvoPacMan = transform;
                motorSemantico.OnPacManPowerStateChanged += TratarMudancaEstadoPoder;
                estaPoderoso = motorSemantico.pacmanIsPowered;
            }
        }

        /// <summary>
        /// Atualiza o estado de poder em resposta ao temporizador ontológico do motor SWRL.
        /// </summary>
        private void TratarMudancaEstadoPoder(bool powered)
        {
            estaPoderoso = powered;
            Debug.Log($"<color=#FFD700><b>[ControladorJogador]</b></color> Estado Empowered do Pac-Man atualizado para: <b>{estaPoderoso}</b>.");
        }

        /// <summary>
        /// Processa movimentação tanto por teclado (WASD / Setas) quanto por thumbstick do XR Origin.
        /// </summary>
        private void ProcessarMovimentoHibrido()
        {
            if (characterController == null || !characterController.enabled) return;

            // 1. Leitura do Teclado (Editor / Desktop fallback)
            float moveX = Input.GetAxis("Horizontal");
            float moveZ = Input.GetAxis("Vertical");
            float giro = 0f;

            if (Input.GetKey(KeyCode.Q)) giro -= 1f;
            if (Input.GetKey(KeyCode.E)) giro += 1f;

            // 2. Leitura nativa de Realidade Virtual (XR Input Devices)
            if (habilitarControlesXR)
            {
                // Mão Esquerda: Movimento linear (Thumbstick / Touchpad)
                InputDevice leftController = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
                if (leftController.isValid && leftController.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 axisLeft))
                {
                    if (axisLeft.sqrMagnitude > 0.04f)
                    {
                        moveX = axisLeft.x;
                        moveZ = axisLeft.y;
                    }
                }

                // Mão Direita: Giro horizontal / Snap Turn
                InputDevice rightController = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
                if (rightController.isValid && rightController.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 axisRight))
                {
                    if (Mathf.Abs(axisRight.x) > 0.2f)
                    {
                        giro = axisRight.x;
                    }
                }
            }

            // 3. Aplicação do Giro Angular
            if (Mathf.Abs(giro) > 0.01f)
            {
                transform.Rotate(Vector3.up, giro * velocidadeGiro * Time.deltaTime);
            }

            // 4. Cálculo do vetor de direção orientado pela visão do jogador (Câmera XR)
            Transform orientacao = cameraXR != null ? cameraXR : transform;
            Vector3 forward = orientacao.forward;
            Vector3 right = orientacao.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            Vector3 direcaoDesejada = (right * moveX + forward * moveZ);
            if (direcaoDesejada.sqrMagnitude > 1f)
            {
                direcaoDesejada.Normalize();
            }

            Vector3 deslocamento = direcaoDesejada * (velocidadeMovimento * Time.deltaTime);

            // 5. Aplicação de gravidade para assentamento correto no plano
            if (characterController.isGrounded)
            {
                m_VelocidadeVertical = -0.5f;
            }
            else
            {
                m_VelocidadeVertical -= gravidade * Time.deltaTime;
            }
            deslocamento.y = m_VelocidadeVertical * Time.deltaTime;

            characterController.Move(deslocamento);

            // 6. Rotação suave do modelo para a direção do movimento quando deslocando
            Vector3 direcaoPlana = new Vector3(deslocamento.x, 0f, deslocamento.z);
            if (direcaoPlana.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direcaoPlana.normalized), Time.deltaTime * 10f);
            }
        }

        /// <summary>
        /// Detecção e processamento de colisões físicas e semânticas com itens e agentes do cenário.
        /// </summary>
        private void OnTriggerEnter(Collider other)
        {
            if (other == null) return;

            IInstanciaSemantica inst = other.GetComponent<IInstanciaSemantica>() ?? other.GetComponentInParent<IInstanciaSemantica>();

            // 1. Detecção de itens e gemas válidas (Pac-Dots / Gemas Ontológicas)
            if (other.CompareTag("GemaValida") || other.name.Contains("Gema") || other.name.Contains("PacDot") || (inst != null && inst.EhValida))
            {
                TratarConsumoGema(other, inst);
            }
            // 2. Detecção de obstáculos e fantasmas (Agentes ABox)
            else if (other.CompareTag("ObstaculoInvalido") || other.name.Contains("Obstaculo") || other.name.Contains("Fantasma") || other.name.Contains("Blinky") || (inst != null && !inst.EhValida))
            {
                TratarEncontroFantasma(other, inst);
            }
        }

        /// <summary>
        /// Processa o consumo de gemas e dispara a relação semântica 'consumes' no Grafo.
        /// </summary>
        private void TratarConsumoGema(Collider other, IInstanciaSemantica inst)
        {
            string idItem = inst != null ? inst.Id : other.name;
            string nomeClasse = inst != null ? inst.NomeClasse : other.name;

            Debug.Log($"<color=#00FF99><b>[ControladorJogador]</b></color> Gema '{nomeClasse}' ({idItem}) consumida! Disparando relação semântica 'consumes'.");

            // Verifica se trata-se de uma gema especial (Power Pellet)
            bool ehGemaEspecial = (inst != null && (inst.NomeClasse.IndexOf("Special", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                    inst.NomeClasse.IndexOf("Power", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                    inst.NomeClasse.IndexOf("Pellet", StringComparison.OrdinalIgnoreCase) >= 0)) ||
                                  other.name.IndexOf("Special", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  other.name.IndexOf("Power", StringComparison.OrdinalIgnoreCase) >= 0;

            if (ehGemaEspecial)
            {
                estaPoderoso = true;
                if (motorSemantico != null)
                {
                    motorSemantico.AtivarPowerPellet(8.0f);
                }
            }

            // Registra a aresta de relação 'consumes' no Grafo Ontológico VirtOnto
            RegistrarRelacaoConsumoNoGrafo(idItem);

            // Efetua a interação polimórfica para disparar juice, pontuação e destruição controlada
            if (inst is InstanciaSemanticaBase instBase)
            {
                instBase.Interagir(gameObject, ehRaycast: false);
            }
            else
            {
                Destroy(other.gameObject);
            }
        }

        /// <summary>
        /// Processa o encontro com obstáculos ou fantasmas conforme as regras de inferência SWRL.
        /// </summary>
        private void TratarEncontroFantasma(Collider other, IInstanciaSemantica inst)
        {
            string idFantasma = inst != null ? inst.Id : "ghost_blinky";
            GameEntityState estadoFantasma = motorSemantico != null ? motorSemantico.ObterEstadoEntidade(idFantasma) : GameEntityState.Patrol;

            // Se o Pac-Man estiver empoderado e o fantasma vulnerável, o Pac-Man consome o fantasma
            if (estaPoderoso && (estadoFantasma == GameEntityState.Vulnerable || (motorSemantico != null && motorSemantico.pacmanIsPowered)))
            {
                Debug.Log($"<color=#00E5FF><b>[ControladorJogador - SWRL]</b></color> Fantasma VULNERÁVEL '{idFantasma}' consumido pelo Pac-Man empoderado! (+200 pts)");
                InstanciaSemanticaBase.pontuacaoLogica += 200;

                RegistrarRelacaoConsumoNoGrafo(idFantasma);

                AnimacaoFantasma anim = other.GetComponent<AnimacaoFantasma>() ?? other.GetComponentInParent<AnimacaoFantasma>();
                if (anim != null) anim.DispararSusto(1.0f);

                // Desativa temporariamente o fantasma consumido
                other.gameObject.SetActive(false);
            }
            else
            {
                Debug.LogWarning($"<color=#FF3333><b>[ControladorJogador]</b></color> Colisão com obstáculo/fantasma hostil detectada! Estado: {estadoFantasma}.");
                if (inst is InstanciaSemanticaBase instBase)
                {
                    instBase.Interagir(gameObject, ehRaycast: false);
                }
            }
        }

        /// <summary>
        /// Registra formalmente a aresta direcionada 'consumes' entre o Pac-Man e o elemento ontológico.
        /// </summary>
        private void RegistrarRelacaoConsumoNoGrafo(string targetId)
        {
            Graph grafo = motorSemantico != null ? motorSemantico.GetSemanticGraph() : gerenciadorCenario?.GrafoOntologico;
            if (grafo != null)
            {
                string edgeId = $"edge_consumes_{targetId}_{Time.frameCount}";
                Edge arestaConsumo = new Edge(edgeId, "pacman_01", targetId, "consumes");
                grafo.AddEdge(arestaConsumo);
            }
        }

        public bool GetEstaPoderoso() => estaPoderoso;
        public void SetEstaPoderoso(bool valor) => estaPoderoso = valor;
    }
}
