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
    /// 2. Detecção e consumo de gemas ontológicas via OnTriggerEnter sem empurrão físico.
    /// 3. Lógica limpa de dano e proximidade de fantasmas (distância <= 0.8m) com respawn seguro.
    /// 4. Comunicação bidirecional com o MotorRaciocinioSemantico e Grafo Ontológico.
    /// ============================================================================
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public class ControladorJogador : MonoBehaviour
    {
        [Header("Configurações de Movimento")]
        [Tooltip("Velocidade linear de deslocamento do jogador.")]
        public float velocidadeMovimento = 3.8f;

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

        [Header("Sistema de Vidas e Colisão com Fantasmas")]
        [Tooltip("Quantidade de vidas do Pac-Man.")]
        public int vidasRestantes = 3;

        [Tooltip("Distância limite para detecção limpa de proximidade/dano com Fantasmas agressivos.")]
        public float distanciaDanoFantasma = 0.8f;

        [Tooltip("Ponto inicial (Spawn) seguro para reposicionar o Pac-Man após sofrer dano.")]
        public Vector3 pontoSpawn = new Vector3(0f, 0.5f, -10f);

        [Tooltip("Tempo de invulnerabilidade após sofrer dano para evitar múltiplos acertos consecutivos.")]
        public float tempoInvulnerabilidade = 2.0f;

        private float m_TimerInvulnerabilidade = 0f;

        /// <summary>
        /// Evento disparado quando as vidas do Pac-Man são alteradas (dano ou cura).
        /// </summary>
        public static event Action<int> OnVidasAlteradas;

        private CharacterController characterController;
        private float m_VelocidadeVertical = 0f;

        private void Awake()
        {
            GarantirCharacterController();
        }

        private void GarantirCharacterController()
        {
            if (characterController == null)
            {
                characterController = GetComponent<CharacterController>() ?? gameObject.AddComponent<CharacterController>();
            }

            if (characterController != null)
            {
                characterController.radius = 0.45f;
                characterController.height = 1.8f;
                characterController.skinWidth = 0.05f;
                characterController.minMoveDistance = 0f;
                characterController.stepOffset = 0.15f;
            }
        }

        private void Start()
        {
            LocalizarComponentes();
            ConectarComMotorSemantico();
            pontoSpawn = transform.position;
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
            if (m_TimerInvulnerabilidade > 0f)
            {
                m_TimerInvulnerabilidade -= Time.deltaTime;
            }

            ProcessarMovimentoHibrido();
            VerificarProximidadeFantasmas();
        }

        /// <summary>
        /// Localiza automaticamente câmeras e referências no cenário com salvaguardas de nulidade.
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
            float moveX = 0f;
            float moveZ = 0f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) moveZ += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) moveZ -= 1f;

            float giro = 0f;
            if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.LeftArrow)) giro -= 1f;
            if (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.RightArrow)) giro += 1f;

            if (Input.GetKey(KeyCode.A)) moveX -= 1f;
            if (Input.GetKey(KeyCode.D)) moveX += 1f;

            // 2. Leitura nativa de Realidade Virtual (XR Input Devices)
            if (habilitarControlesXR)
            {
                InputDevice leftController = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
                if (leftController.isValid && leftController.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 axisLeft))
                {
                    if (axisLeft.sqrMagnitude > 0.04f)
                    {
                        moveX = axisLeft.x;
                        moveZ = axisLeft.y;
                    }
                }

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

            // Barreira preditiva física de colisão com paredes do labirinto
            Vector3 direcaoHorizontal = new Vector3(direcaoDesejada.x, 0f, direcaoDesejada.z);
            if (direcaoHorizontal.sqrMagnitude > 0.001f)
            {
                float distVerificacao = (velocidadeMovimento * Time.deltaTime) + characterController.radius + 0.08f;
                Vector3 centroCapsula = transform.position + new Vector3(0f, characterController.height / 2f, 0f);

                if (Physics.SphereCast(centroCapsula, characterController.radius * 0.95f, direcaoHorizontal.normalized, out RaycastHit hit, distVerificacao, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider != null && !hit.collider.isTrigger)
                    {
                        Vector3 normalParede = hit.normal;
                        normalParede.y = 0f;
                        normalParede.Normalize();

                        direcaoHorizontal = Vector3.ProjectOnPlane(direcaoHorizontal, normalParede);
                        direcaoDesejada = direcaoHorizontal;
                    }
                }
            }

            Vector3 deslocamento = direcaoDesejada * (velocidadeMovimento * Time.deltaTime);

            // 5. Aplicação de gravidade para assentamento correto no plano
            if (characterController.isGrounded)
            {
                m_VelocidadeVertical = -4.0f;
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
        /// Verificação limpa de distância baseada no script de proximidade (<= 0.8m).
        /// Ao encostar num fantasma agressivo, reduz exatamente 1 vida, executa o impulso háptico
        /// e faz o respawn seguro, sem empurrar o jogador fisicamente pelo mapa.
        /// </summary>
        private void VerificarProximidadeFantasmas()
        {
            if (m_TimerInvulnerabilidade > 0f) return;

            ObstaculoInvalidoSemantico[] obstaculos = FindObjectsByType<ObstaculoInvalidoSemantico>(FindObjectsSortMode.None);
            if (obstaculos != null)
            {
                for (int i = 0; i < obstaculos.Length; i++)
                {
                    if (obstaculos[i] == null || !obstaculos[i].gameObject.activeInHierarchy) continue;
                    float dist = Vector3.Distance(transform.position, obstaculos[i].transform.position);
                    if (dist <= distanciaDanoFantasma)
                    {
                        TratarEncontroFantasma(obstaculos[i].GetComponent<Collider>(), obstaculos[i]);
                        return;
                    }
                }
            }

            AnimacaoFantasma[] anims = FindObjectsByType<AnimacaoFantasma>(FindObjectsSortMode.None);
            if (anims != null)
            {
                for (int i = 0; i < anims.Length; i++)
                {
                    if (anims[i] == null || !anims[i].gameObject.activeInHierarchy) continue;
                    float dist = Vector3.Distance(transform.position, anims[i].transform.position);
                    if (dist <= distanciaDanoFantasma)
                    {
                        TratarEncontroFantasma(anims[i].GetComponent<Collider>(), anims[i].GetComponent<IInstanciaSemantica>());
                        return;
                    }
                }
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision == null || collision.collider == null) return;
            OnTriggerEnter(collision.collider);
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (hit == null || hit.collider == null) return;

            IInstanciaSemantica inst = hit.collider.GetComponent<IInstanciaSemantica>() ?? hit.collider.GetComponentInParent<IInstanciaSemantica>();

            bool ehTagGema = false;
            bool ehTagObstaculo = false;
            try { ehTagGema = hit.collider.CompareTag("GemaValida"); } catch { }
            try { ehTagObstaculo = hit.collider.CompareTag("ObstaculoInvalido"); } catch { }

            if (ehTagGema || hit.gameObject.name.Contains("Gema") || hit.gameObject.name.Contains("PacDot") || (inst != null && inst.EhValida))
            {
                TratarConsumoGema(hit.collider, inst);
            }
            else if (ehTagObstaculo || hit.gameObject.name.Contains("Obstaculo") || hit.gameObject.name.Contains("Fantasma") || hit.gameObject.name.Contains("Blinky") || (inst != null && !inst.EhValida))
            {
                TratarEncontroFantasma(hit.collider, inst);
            }
        }

        /// <summary>
        /// Detecção e processamento de colisões com gemas e fantasmas via Triggers.
        /// </summary>
        private void OnTriggerEnter(Collider other)
        {
            if (other == null) return;

            IInstanciaSemantica inst = other.GetComponent<IInstanciaSemantica>() ?? other.GetComponentInParent<IInstanciaSemantica>();

            bool ehTagGema = false;
            bool ehTagObstaculo = false;
            try { ehTagGema = other.CompareTag("GemaValida"); } catch { }
            try { ehTagObstaculo = other.CompareTag("ObstaculoInvalido"); } catch { }

            // 1. Consumo de gemas amarelas (Pac-Dots / Gemas Ontológicas)
            if (ehTagGema || other.name.Contains("Gema") || other.name.Contains("PacDot") || (inst != null && inst.EhValida))
            {
                TratarConsumoGema(other, inst);
            }
            // 2. Encontro com fantasmas e obstáculos (Agentes ABox)
            else if (ehTagObstaculo || other.name.Contains("Obstaculo") || other.name.Contains("Fantasma") || other.name.Contains("Blinky") || (inst != null && !inst.EhValida))
            {
                TratarEncontroFantasma(other, inst);
            }
        }

        /// <summary>
        /// Processa o consumo da gema: extrai IInstanciaSemantica, regista a instância e a classe exata
        /// no sistema ontológico (atualizando o HUD com a classe correta, ex: Ascaris lumbricoides ou Enterobius vermicularis),
        /// soma os pontos corretos e destrói/desativa a gema imediatamente (removendo qualquer empurrão físico).
        /// </summary>
        private void TratarConsumoGema(Collider other, IInstanciaSemantica inst)
        {
            GameObject gemaObj = (inst is Component comp && comp != null) ? comp.gameObject : (other != null ? other.gameObject : null);
            if (gemaObj == null) return;

            string idItem = inst != null && !string.IsNullOrEmpty(inst.Id) ? inst.Id : gemaObj.name;
            string nomeClasseOriginal = inst != null && !string.IsNullOrEmpty(inst.NomeClasse) ? inst.NomeClasse : gemaObj.name;
            string nomeClasseFormatado = InstanciaSemanticaBase.FormatarNomeClasse(nomeClasseOriginal);
            int pontos = inst != null ? inst.ValorPontuacao : 10;
            string expressaoDL = inst != null ? inst.ExpressaoDL : "";
            string descSemantica = inst != null ? inst.DescricaoSemantica : "";

            Debug.Log($"<color=#00FF99><b>[ControladorJogador]</b></color> Gema '{nomeClasseFormatado}' ({idItem}) consumida! Registando no sistema ontológico.");

            // Verifica se trata-se de uma gema especial (Power Pellet)
            bool ehGemaEspecial = (inst != null && (nomeClasseOriginal.IndexOf("Special", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                    nomeClasseOriginal.IndexOf("Power", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                    nomeClasseOriginal.IndexOf("Pellet", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                    (inst.PropriedadesDL != null && inst.PropriedadesDL.ContainsKey("isPowerPellet")))) ||
                                  gemaObj.name.IndexOf("Special", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  gemaObj.name.IndexOf("Power", StringComparison.OrdinalIgnoreCase) >= 0;

            if (ehGemaEspecial)
            {
                estaPoderoso = true;
                if (motorSemantico != null)
                {
                    motorSemantico.AtivarPowerPellet(8.0f);
                }
            }

            // Filtragem Ontológica Estrita (Protégé / parasites.owl)
            bool ontologicamenteValida = InstanciaSemanticaBase.ValidarTaxonomiaOntologicaEstrita(nomeClasseOriginal, out string motivo);
            if (!ontologicamenteValida)
            {
                int penalidade = -10;
                InstanciaSemanticaBase.pontuacaoLogica += penalidade;
                Debug.LogWarning($"<color=#FF0044><b>[VIOLAÇÃO ONTOLÓGICA]</b></color> Espécime '{nomeClasseFormatado}' recusado: {motivo}. Penalidade: {penalidade} pts.");
                DispararHapticosJogador(0.85f, 0.35f);

                Collider[] colsErr = gemaObj.GetComponentsInChildren<Collider>();
                if (colsErr != null) { for (int i = 0; i < colsErr.Length; i++) if (colsErr[i] != null) colsErr[i].enabled = false; }
                gemaObj.SetActive(false);
                Destroy(gemaObj);
                return;
            }

            // 1. Registra a instância e a relação semântica 'consumes' no Grafo Ontológico VirtOnto
            RegistrarRelacaoConsumoNoGrafo(idItem, nomeClasseFormatado);

            // 2. Atualiza os HUDs (WebGL e HUD Holográfico VR) com a classe correta
            InstanciaSemanticaBase.AtualizarHUDs(idItem, nomeClasseFormatado, pontos, expressaoDL, descSemantica);

            // 3. Atualiza pontuação lógica
            InstanciaSemanticaBase.pontuacaoLogica += pontos;
            InstanciaSemanticaBase.totalColetadas++;
            InstanciaSemanticaBase.instanciasValidasRestantes = Mathf.Max(0, InstanciaSemanticaBase.instanciasValidasRestantes - 1);

            // 4. Feedback Háptico e Juice
            DispararHapticosJogador(0.45f, 0.15f);
            if (GerenciadorEfeitosJuice.Instancia != null)
            {
                Color corGema = inst != null ? inst.CorDestaque : new Color(1f, 0.88f, 0.2f);
                GerenciadorEfeitosJuice.Instancia.CriarExplosaoBrilhoColeta(gemaObj.transform.position, corGema);
            }

            // 5. Destrói/desativa a gema IMEDIATAMENTE (removendo qualquer comportamento de empurrão físico)
            Collider[] cols = gemaObj.GetComponentsInChildren<Collider>();
            if (cols != null)
            {
                for (int i = 0; i < cols.Length; i++)
                {
                    if (cols[i] != null) cols[i].enabled = false;
                }
            }
            gemaObj.SetActive(false);
            Destroy(gemaObj);
        }

        /// <summary>
        /// Processa o encontro com fantasmas conforme as regras de inferência SWRL.
        /// </summary>
        private void TratarEncontroFantasma(Collider other, IInstanciaSemantica inst)
        {
            string idFantasma = inst != null && !string.IsNullOrEmpty(inst.Id) ? inst.Id : "ghost_blinky";
            GameEntityState estadoFantasma = motorSemantico != null ? motorSemantico.ObterEstadoEntidade(idFantasma) : GameEntityState.Patrol;

            // Se o Pac-Man estiver empoderado e o fantasma vulnerável, o Pac-Man consome o fantasma
            if (estaPoderoso && (estadoFantasma == GameEntityState.Vulnerable || (motorSemantico != null && motorSemantico.pacmanIsPowered)))
            {
                Debug.Log($"<color=#00E5FF><b>[ControladorJogador - SWRL]</b></color> Fantasma VULNERÁVEL '{idFantasma}' consumido pelo Pac-Man empoderado! (+200 pts)");
                InstanciaSemanticaBase.pontuacaoLogica += 200;

                RegistrarRelacaoConsumoNoGrafo(idFantasma, "VulnerableGhost");

                if (other != null)
                {
                    AnimacaoFantasma anim = other.GetComponent<AnimacaoFantasma>() ?? other.GetComponentInParent<AnimacaoFantasma>();
                    if (anim != null) anim.DispararSusto(1.0f);
                    other.gameObject.SetActive(false);
                }
            }
            else
            {
                // Fantasma Agressivo: aplica dano limpo sem empurrão físico bruto
                ExecutarDanoPacMan(other != null ? other.gameObject : null);
            }
        }

        /// <summary>
        /// Executa o dano limpo ao Pac-Man, reduzindo exatamente 1 vida, executando impulso háptico
        /// e efetuando o respawn seguro, sem empurrar o jogador fisicamente pelo mapa.
        /// </summary>
        public void ExecutarDanoPacMan(GameObject fantasma)
        {
            if (m_TimerInvulnerabilidade > 0f) return;

            m_TimerInvulnerabilidade = tempoInvulnerabilidade;
            vidasRestantes = Mathf.Max(0, vidasRestantes - 1);

            Debug.LogWarning($"<color=#FF0044><b>[DANO PAC-MAN]</b></color> Encontro com {fantasma?.name ?? "Fantasma Agressivo"}! " +
                             $"Vidas restantes: <b>{vidasRestantes}</b>. Efetuando respawn seguro para {pontoSpawn} sem empurrão físico.");

            // 1. Dispara impulso háptico
            DispararHapticosJogador(0.85f, 0.45f);

            // 2. Dispara Juice de impacto e tremor de câmera
            if (GerenciadorEfeitosJuice.Instancia != null)
            {
                GerenciadorEfeitosJuice.Instancia.CriarEfeitoStunImpacto(transform.position);
            }

            // 3. Notifica ouvintes de alteração de vida
            OnVidasAlteradas?.Invoke(vidasRestantes);

            // 4. Sincroniza com o ControladorDemoWeb e HUD Holográfico
            var demoWeb = FindFirstObjectByType<PacMan.Web.ControladorDemoWeb>();
            if (demoWeb != null)
            {
                demoWeb.vidasRestantes = vidasRestantes;
                demoWeb.ExibirAvisoColisao($"⚠️ O PAC-MAN FOI APANHADO! Perdeu 1 vida. Vidas restantes: {vidasRestantes}");
            }

            var hudVR = FindFirstObjectByType<HUDHolograficoXR>();
            if (hudVR != null)
            {
                hudVR.AtualizarTextosHUD();
            }

            // 5. Reposiciona o Pac-Man com segurança sem empurrão físico pelo mapa
            ReposicionarNoSpawn();
        }

        /// <summary>
        /// Dispara impulsos hápticos com salvaguarda de nulidade para controladores VR.
        /// </summary>
        private void DispararHapticosJogador(float amplitude, float duracao)
        {
            var ctrlXR = GetComponent<ControladorXRJogador>() ?? GetComponentInParent<ControladorXRJogador>() ?? FindFirstObjectByType<ControladorXRJogador>();
            if (ctrlXR != null)
            {
                ctrlXR.DispararHapticsDualController(amplitude, duracao);
            }

            var interacaoXR = GetComponentInChildren<InteracaoEspacialXR>() ?? FindFirstObjectByType<InteracaoEspacialXR>();
            if (interacaoXR != null)
            {
                interacaoXR.DispararVibracaoHaptica(amplitude, duracao);
            }
        }

        /// <summary>
        /// Reposiciona o Pac-Man no ponto de Spawn com segurança absoluta, anulando velocidades e CharacterController.
        /// </summary>
        public void ReposicionarNoSpawn()
        {
            if (characterController != null)
            {
                characterController.enabled = false;
                transform.position = pontoSpawn;
                characterController.enabled = true;
            }
            else
            {
                transform.position = pontoSpawn;
            }

            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            m_VelocidadeVertical = 0f;
        }

        /// <summary>
        /// Registra formalmente a aresta direcionada 'consumes' entre o Pac-Man e o elemento ontológico.
        /// </summary>
        private void RegistrarRelacaoConsumoNoGrafo(string targetId, string nomeClasse = "OntologyEntity")
        {
            Graph grafo = motorSemantico != null ? motorSemantico.GetSemanticGraph() : gerenciadorCenario?.GrafoOntologico;
            if (grafo != null && !string.IsNullOrEmpty(targetId))
            {
                if (grafo.GetNode(targetId) == null)
                {
                    grafo.AddNode(new Node(targetId, nomeClasse, NodeType.Individual));
                }

                string edgeId = $"edge_consumes_{targetId}_{Time.frameCount}";
                Edge arestaConsumo = new Edge(edgeId, "pacman_01", targetId, "consumes");
                grafo.AddEdge(arestaConsumo);
            }
        }

        public bool GetEstaPoderoso() => estaPoderoso;
        public void SetEstaPoderoso(bool valor) => estaPoderoso = valor;
    }
}