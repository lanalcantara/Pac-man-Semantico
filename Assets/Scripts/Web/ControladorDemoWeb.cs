using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using PacMan.Semantics;

namespace PacMan.Web
{
    /// <summary>
    /// ============================================================================
    /// PAC-MAN SEMÂNTICO (CIn-UFPE) - Controlador de Demonstração WebGL & Arcade
    /// ============================================================================
    /// Interface Canônica e Limpa (Conforme Solicitado pelo Usuário):
    /// - Escala de Referência 1920x1080 (Scale With Screen Size).
    /// - Fontes reduzidas, sem textos sobrepostos.
    /// - Textos estritamente limpos: Botão PLAY, Pontuação, Vidas e Entidade Ativa.
    /// - Sem textos inúteis de debug (sem 'PATROL', sem distâncias, sem lista de atalhos).
    /// - Botão [ X ] no canto superior direito para fechar/minimizar o painel.
    /// ============================================================================
    /// </summary>
    [AddComponentMenu("Pac-Man Semântico/Web/Controlador Demo Web")]
    public class ControladorDemoWeb : MonoBehaviour
    {
        public enum EstadoJogoDemo
        {
            MenuInicial,
            Jogando,
            GameOver,
            Vitoria
        }

        public enum ModoCamera
        {
            Pinball3D,             // Padrão: Câmera de cima inclinada estilo Space Cadet Pinball 3D
            MesaCompletaPinball,   // Visão panorâmica estática da mesa inteira
            TerceiraPessoaBaixa,   // 3ª pessoa clássica atrás do Pac-Man
            PrimeiraPessoaVR       // Visão em 1ª pessoa cockpit (Meta Quest)
        }

        [Header("Estado do Jogo")]
        public EstadoJogoDemo estadoAtual = EstadoJogoDemo.MenuInicial;

        [Header("Modo de Visualização da Câmera")]
        public ModoCamera modoCamera = ModoCamera.Pinball3D;

        [Header("Referências")]
        public MotorRaciocinioSemantico motorSemantico;
        public AvaliadorRegrasRuntime avaliadorRegras;
        public Transform pacmanTransform;
        public Transform ghostTransform;
        public Transform ghostBlinkyTransform;

        [Header("Configuração de Interface")]
        public bool exibirGuiWebGL = true;
        [Tooltip("Indica se o painel HUD está minimizado/fechado pelo usuário.")]
        public bool hudMinimizado = false;
        public float duracaoPowerPelletSimulado = 8.0f;

        [Header("Colisão e Vidas")]
        public float distanciaColisaoSegura = 1.0f;
        public Vector3 pontoSpawnInicial = new Vector3(0f, 0f, -6f);
        public int vidasRestantes = 3;
        private float temporizadorAvisoColisao = 0f;
        private string mensagemAvisoColisao = "";

        [Header("Feedback de Captura Semântica (Tempo Real)")]
        public string ultimaInstanciaCapturada = "Nenhuma";
        public string ultimaClasseCapturada = "Aguardando item...";
        public string ultimoAxiomaDL = "";
        public bool ultimoItemValido = true;
        public int pontuacaoAcumulada = 0;
        public int totalItensCapturados = 0;

        private CharacterController m_CharacterController;
        private Camera m_CameraPrincipal;
        private GameObject m_VisualPacMan;

        private void OnEnable()
        {
            InstanciaSemanticaBase.OnInstanciaInteragida += AoInteragirInstanciaSemantica;
            PacMan.Player.ControladorJogador.OnVidasAlteradas += AoAlterarVidasJogador;
        }

        private void OnDisable()
        {
            InstanciaSemanticaBase.OnInstanciaInteragida -= AoInteragirInstanciaSemantica;
            PacMan.Player.ControladorJogador.OnVidasAlteradas -= AoAlterarVidasJogador;
        }

        private void AoAlterarVidasJogador(int novasVidas)
        {
            vidasRestantes = novasVidas;
            if (vidasRestantes <= 0)
            {
                FinalizarJogoGameOver();
            }
        }

        private void Start()
        {
            if (motorSemantico == null) motorSemantico = FindFirstObjectByType<MotorRaciocinioSemantico>();
            if (avaliadorRegras == null) avaliadorRegras = FindFirstObjectByType<AvaliadorRegrasRuntime>();

            LocalizarElementosCena();
            ConfigurarVisualEPerspectivaCamera();
            GarantirCanvasArcade();

            estadoAtual = EstadoJogoDemo.MenuInicial;
            PausarEntidadesParaMenu(true);
            Time.timeScale = 0f;

            Debug.Log("<color=#00FFFF><b>[Demo WebGL]</b></color> Pac-Man Semântico pronto em perspectiva Pinball 3D. Clique em PLAY para iniciar!");
        }

        
        private void GarantirCanvasArcade()
        {
            HUDCanvasArcade.GarantirEventSystem();
            HUDCanvasArcade hud = FindFirstObjectByType<HUDCanvasArcade>();
            if (hud == null)
            {
                GameObject canvasObj = new GameObject("HUD_Canvas_Arcade");
                hud = canvasObj.AddComponent<HUDCanvasArcade>();
            }
            if (hud != null)
            {
                hud.DefinirModalPlayVisivel(estadoAtual == EstadoJogoDemo.MenuInicial);
                hud.DefinirPainelVisivel(estadoAtual == EstadoJogoDemo.Jogando && !hudMinimizado);
            }
        }

        private void LocalizarElementosCena()
        {
            if (pacmanTransform == null)
            {
                GameObject xrOrigin = GameObject.Find("XR Origin") ?? GameObject.FindWithTag("Player");
                if (xrOrigin != null) pacmanTransform = xrOrigin.transform;
            }

            if (pacmanTransform != null)
            {
                pacmanTransform.gameObject.SetActive(true);
                pontoSpawnInicial = new Vector3(0f, 0.0f, -6.0f);
                pacmanTransform.position = pontoSpawnInicial;

                m_CharacterController = pacmanTransform.GetComponent<CharacterController>();
                if (m_CharacterController != null)
                {
                    m_CharacterController.enabled = false;
                }

                Rigidbody rb = pacmanTransform.GetComponent<Rigidbody>();
                if (rb == null) rb = pacmanTransform.gameObject.AddComponent<Rigidbody>();
                rb.isKinematic = false;
                rb.useGravity = false;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.constraints = RigidbodyConstraints.FreezePositionY |
                                 RigidbodyConstraints.FreezeRotationX |
                                 RigidbodyConstraints.FreezeRotationY |
                                 RigidbodyConstraints.FreezeRotationZ;

                SphereCollider sc = pacmanTransform.GetComponent<SphereCollider>();
                if (sc == null) sc = pacmanTransform.gameObject.AddComponent<SphereCollider>();
                sc.isTrigger = false;
                sc.radius = 0.38f;
                sc.center = new Vector3(0f, 0.40f, 0f);
            }

            if (ghostBlinkyTransform == null)
            {
                GameObject blinky = GameObject.Find("Fantasma_Blinky") ?? GameObject.Find("Blinky") ?? GameObject.Find("Fantasma_0");
                if (blinky != null) ghostBlinkyTransform = blinky.transform;
            }

            if (ghostTransform == null && ghostBlinkyTransform != null)
            {
                ghostTransform = ghostBlinkyTransform;
            }

            m_CameraPrincipal = Camera.main;

            m_VisualPacMan = GameObject.Find("Visual_PacMan");
            if (m_VisualPacMan == null && pacmanTransform != null)
            {
                Transform tVis = pacmanTransform.Find("Visual_PacMan");
                if (tVis != null) m_VisualPacMan = tVis.gameObject;
            }
            if (m_VisualPacMan != null)
            {
                m_VisualPacMan.SetActive(true);
                m_VisualPacMan.transform.localPosition = new Vector3(0f, 0.40f, 0f);
                m_VisualPacMan.transform.localScale = new Vector3(0.80f, 0.80f, 0.80f);

                MeshRenderer[] renderers = m_VisualPacMan.GetComponentsInChildren<MeshRenderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] != null)
                    {
                        renderers[i].enabled = true;
                        if (!renderers[i].gameObject.activeSelf) renderers[i].gameObject.SetActive(true);
                    }
                }
            }
        }

        public void ConfigurarVisualEPerspectivaCamera()
        {
            if (m_CameraPrincipal == null) m_CameraPrincipal = Camera.main;
            AplicarModoCamera(modoCamera);
        }

        public void AlternarModoCamera()
        {
            modoCamera = (ModoCamera)(((int)modoCamera + 1) % 4);
            AplicarModoCamera(modoCamera);
        }

        public void AplicarModoCamera(ModoCamera modo)
        {
            if (m_CameraPrincipal == null) m_CameraPrincipal = Camera.main;
            if (m_CameraPrincipal == null) return;

            Transform camTransform = m_CameraPrincipal.transform;

            switch (modo)
            {
                case ModoCamera.Pinball3D:
                    m_CameraPrincipal.fieldOfView = 62f;
                    if (pacmanTransform != null)
                    {
                        camTransform.position = pacmanTransform.position + new Vector3(0f, 15.5f, -11.5f);
                    }
                    else
                    {
                        camTransform.position = new Vector3(0f, 15.5f, -11.5f);
                    }
                    camTransform.rotation = Quaternion.Euler(54f, 0f, 0f);
                    if (m_VisualPacMan != null) m_VisualPacMan.SetActive(true);
                    break;

                case ModoCamera.MesaCompletaPinball:
                    m_CameraPrincipal.fieldOfView = 65f;
                    camTransform.position = new Vector3(0f, 22.0f, -14.5f);
                    camTransform.rotation = Quaternion.Euler(55f, 0f, 0f);
                    if (m_VisualPacMan != null) m_VisualPacMan.SetActive(true);
                    break;

                case ModoCamera.TerceiraPessoaBaixa:
                    m_CameraPrincipal.fieldOfView = 65f;
                    if (pacmanTransform != null)
                    {
                        camTransform.position = pacmanTransform.position + new Vector3(0f, 2.5f, -3.8f);
                    }
                    camTransform.rotation = Quaternion.Euler(22f, 0f, 0f);
                    if (m_VisualPacMan != null) m_VisualPacMan.SetActive(true);
                    break;

                case ModoCamera.PrimeiraPessoaVR:
                    m_CameraPrincipal.fieldOfView = 75f;
                    if (pacmanTransform != null)
                    {
                        camTransform.position = pacmanTransform.position + new Vector3(0f, 1.25f, 0f);
                        camTransform.rotation = pacmanTransform.rotation;
                    }
                    if (m_VisualPacMan != null) m_VisualPacMan.SetActive(false);
                    break;
            }
        }

        private void LateUpdate()
        {
            if (m_CameraPrincipal == null || pacmanTransform == null) return;

            Transform camTransform = m_CameraPrincipal.transform;

            if (modoCamera == ModoCamera.Pinball3D)
            {
                Vector3 targetPos = pacmanTransform.position + new Vector3(0f, 15.5f, -11.5f);
                camTransform.position = Vector3.Lerp(camTransform.position, targetPos, Time.deltaTime * 8f);
                camTransform.rotation = Quaternion.Euler(54f, 0f, 0f);
            }
            else if (modoCamera == ModoCamera.MesaCompletaPinball)
            {
                camTransform.position = Vector3.Lerp(camTransform.position, new Vector3(0f, 22.0f, -14.5f), Time.deltaTime * 6f);
                camTransform.rotation = Quaternion.Euler(55f, 0f, 0f);
            }
            else if (modoCamera == ModoCamera.TerceiraPessoaBaixa)
            {
                Vector3 targetPos = pacmanTransform.position + new Vector3(0f, 2.5f, -3.8f);
                camTransform.position = Vector3.Lerp(camTransform.position, targetPos, Time.deltaTime * 10f);
                camTransform.rotation = Quaternion.Euler(22f, 0f, 0f);
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.C)) AlternarModoCamera();
            if (Input.GetKeyDown(KeyCode.H)) hudMinimizado = !hudMinimizado;
            if (Input.GetKeyDown(KeyCode.P)) AlternarPowerPellet();

            if (temporizadorAvisoColisao > 0f) temporizadorAvisoColisao -= Time.deltaTime;

            if (estadoAtual == EstadoJogoDemo.Jogando)
            {
                VerificarColisaoPacmanBlinky();
            }
        }

        public void IniciarJogoPlay()
        {
            estadoAtual = EstadoJogoDemo.Jogando;
            PausarEntidadesParaMenu(false);
            if (HUDCanvasArcade.Instancia != null)
            {
                HUDCanvasArcade.Instancia.DefinirModalPlayVisivel(false);
                HUDCanvasArcade.Instancia.DefinirPainelVisivel(!hudMinimizado);
            }
            Debug.Log("<color=#00FF99><b>[Jogo Iniciado]</b></color> Corrida semântica iniciada!");
        }

        public void ReiniciarJogoPartida()
        {
            Time.timeScale = 1.0f;
            Time.timeScale = 1.0f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        public void FinalizarJogoGameOver()
        {
            estadoAtual = EstadoJogoDemo.GameOver;
            PausarEntidadesParaMenu(true);
            Debug.Log("<color=#FF3333><b>[GAME OVER]</b></color> Fim de jogo!");
        }

        private void PausarEntidadesParaMenu(bool pausar)
        {
            if (pacmanTransform != null)
            {
                Rigidbody rb = pacmanTransform.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
#pragma warning disable CS0618
                    rb.velocity = Vector3.zero;
#pragma warning restore CS0618
                }
            }

            var agents = FindObjectsByType<UnityEngine.AI.NavMeshAgent>(FindObjectsSortMode.None);
            if (agents != null)
            {
                foreach (var agent in agents)
                {
                    if (agent != null && agent.isOnNavMesh) agent.isStopped = pausar;
                }
            }
        }

        public void ExecutarResetColisao(float tempoEspera = 0.5f)
        {
            vidasRestantes--;
            ExibirAvisoColisao("Colisão com Fantasma! -1 Vida", 2.5f);

            if (vidasRestantes <= 0)
            {
                FinalizarJogoGameOver();
            }
            else
            {
                ResetarPosicaoPacman();
            }
        }

        public void VerificarColisaoPacmanBlinky()
        {
            Transform alvoFantasma = ghostBlinkyTransform != null ? ghostBlinkyTransform : ghostTransform;
            if (pacmanTransform == null || alvoFantasma == null) return;

            float distancia = Vector3.Distance(pacmanTransform.position, alvoFantasma.position);

            if (distancia <= distanciaColisaoSegura)
            {
                bool isVulnerable = (avaliadorRegras != null &&
                    avaliadorRegras.ultimoEstadoInferido == GameEntityState.Vulnerable) ||
                    (motorSemantico != null && motorSemantico.pacmanIsPowered);

                if (isVulnerable)
                {
                    alvoFantasma.position = new Vector3(0f, 0f, 0f);
                    pontuacaoAcumulada += 200;
                    ExibirAvisoColisao("Fantasma Capturado! +200 pts", 2.5f);
                }
                else
                {
                    ExecutarResetColisao(0.5f);
                }
            }
        }

        public void ExibirAvisoColisao(string msg, float tempo = 3.0f)
        {
            mensagemAvisoColisao = msg;
            temporizadorAvisoColisao = tempo;
        }

        public void ResetarPosicaoPacman()
        {
            if (pacmanTransform != null)
            {
                pacmanTransform.position = pontoSpawnInicial;
                Rigidbody rb = pacmanTransform.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
#pragma warning disable CS0618
                    rb.velocity = Vector3.zero;
#pragma warning restore CS0618
                }
            }
        }

        public void RegistrarCapturaItem(string id, string nomeClasse, bool valida, int pontos, string dl = "")
        {
            ultimaInstanciaCapturada = id;
            ultimaClasseCapturada = nomeClasse;
            ultimoAxiomaDL = dl;
            ultimoItemValido = valida;
            totalItensCapturados++;
            pontuacaoAcumulada += pontos;
        }

        private void AoInteragirInstanciaSemantica(IInstanciaSemantica instancia, bool sucesso)
        {
            if (instancia == null) return;

            ultimaInstanciaCapturada = instancia.Id;
            ultimaClasseCapturada = instancia.NomeClasse;
            ultimoAxiomaDL = instancia.ExpressaoDL;
            ultimoItemValido = instancia.EhValida;
            totalItensCapturados++;

            if (instancia.ValorPontuacao > 0)
            {
                pontuacaoAcumulada += instancia.ValorPontuacao;
            }
            else
            {
                pontuacaoAcumulada += instancia.EhValida ? 50 : 10;
            }
        }

        public void AlternarPowerPellet()
        {
            if (motorSemantico != null)
            {
                bool novoEstado = !motorSemantico.pacmanIsPowered;
                if (novoEstado)
                {
                    motorSemantico.AtivarPowerPellet(duracaoPowerPelletSimulado);
                }
                else
                {
                    motorSemantico.pacmanIsPowered = false;
                }
            }
        }

        // =========================================================================
        // INTERFACE CANÔNICA, LIMPA E ESCALADA (1920x1080)
        // MANTÉM APENAS: Botão PLAY, Pontuação, Vidas e Entidade Ativa.
        // Botão [ X ] para fechar/minimizar.
        // =========================================================================
        private void OnGUI()
        {
            if (!exibirGuiWebGL) return;

            // Escala proporcional com resolução de referência 1920x1080 (Scale With Screen Size)
            float refWidth = 1920f;
            float refHeight = 1080f;
            Vector3 escalaGUI = new Vector3(Screen.width / refWidth, Screen.height / refHeight, 1.0f);
            Matrix4x4 matrizOriginal = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, escalaGUI);

            Color corFundoOriginal = GUI.backgroundColor;
            Color corTextoOriginal = GUI.color;

            // 1. TELA INICIAL (APENAS BOTÃO PLAY E TÍTULO LIMPO)
            // Sem textos de instruções longos, sem debug e SEM sobreposição com o HUD!
            if (HUDCanvasArcade.Instancia != null && HUDCanvasArcade.Instancia.TemModalPlayAtivo())
            {
                // O Modal amplo em Canvas UI já está sendo renderizado e gerenciado pelo HUDCanvasArcade
            }
            else if (estadoAtual == EstadoJogoDemo.MenuInicial)
            {
                DesenharMenuInicialPlay();
            }
            // 2. TELA DE GAME OVER
            else if (estadoAtual == EstadoJogoDemo.GameOver)
            {
                DesenharGameOver();
            }
            // 3. HUD DURANTE O JOGO (APENAS VIDAS, PONTUAÇÃO E ENTIDADE ATIVA)
            else if (estadoAtual == EstadoJogoDemo.Jogando)
            {
                if (!hudMinimizado)
                {
                    DesenharHUDCanonica();
                }
                else
                {
                    DesenharBotaoReabrirHUD();
                }
            }

            GUI.backgroundColor = corFundoOriginal;
            GUI.color = corTextoOriginal;
            GUI.matrix = matrizOriginal;
        }

        /// <summary>
        /// Menu Inicial Limpo: Contém estritamente o título e o Botão PLAY.
        /// </summary>
        private void DesenharMenuInicialPlay()
        {
            float w = 850f;
            float h = 480f;
            Rect modalRect = new Rect((1920f - w) / 2f, (1080f - h) / 2f, w, h);

            GUI.backgroundColor = new Color(0.02f, 0.05f, 0.16f, 0.96f);
            GUI.Box(modalRect, GUIContent.none);

            GUILayout.BeginArea(new Rect(modalRect.x + 36f, modalRect.y + 32f, w - 72f, h - 64f));

            GUILayout.Label("<size=32><b><color=#FFFF00>🕹️  PAC-MAN SEMÂNTICO (CIn-UFPE)</color></b></size>", new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });
            GUILayout.Space(8f);
            GUILayout.Label("<size=20><color=#38BDF8>Ontologia Parasitologia • Raciocínio DL (Protégé / OWL)</color></size>", new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });

            GUILayout.Space(26f);
            GUILayout.Label("<size=17><color=#F1F5F9>• Devore as <b>Gemas Ontológicas (Classes)</b> pelo labirinto.</color></size>", new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });
            GUILayout.Label("<size=17><color=#F1F5F9>• Cuidado com os <b>4 Fantasmas</b> que partem da gaiola central!</color></size>", new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });
            GUILayout.Label("<size=17><color=#F1F5F9>• Colete as <b>Cerejas</b> e <b>Power Pellets</b> para virar o jogo.</color></size>", new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });

            GUILayout.Space(36f);

            GUI.backgroundColor = new Color(0.06f, 0.73f, 0.51f, 1f);
            if (GUILayout.Button("<size=24><b>▶   I N I C I A R   J O G O   ( P L A Y )</b></size>", GUILayout.Height(75f)))
            {
                IniciarJogoPlay();
            }

            GUILayout.EndArea();
        }

        /// <summary>
        /// HUD Canônica Limpa: Mantém APENAS Vidas, Pontuação e Entidade Ativa.
        /// Botão [ X ] vermelho no canto superior direito para fechar/minimizar.
        /// </summary>
        private void DesenharHUDCanonica()
        {
            float largura = 320f;
            float altura = 145f;
            Rect painel = new Rect(35f, 35f, largura, altura);

            GUI.backgroundColor = new Color(0.02f, 0.05f, 0.14f, 0.90f);
            GUI.Box(painel, "🕹️ Pac-Man Semântico");

            // Botão [ X ] vermelho para fechar/minimizar
            Rect rectFechar = new Rect(painel.x + largura - 30f, painel.y + 5f, 24f, 20f);
            GUI.backgroundColor = new Color(0.85f, 0.2f, 0.2f, 0.95f);
            if (GUI.Button(rectFechar, "<b>✕</b>"))
            {
                hudMinimizado = true;
            }
            GUI.backgroundColor = new Color(0.02f, 0.05f, 0.14f, 0.90f);

            GUILayout.BeginArea(new Rect(painel.x + 16f, painel.y + 32f, largura - 32f, altura - 40f));

            // 1. Vidas
            string iconesVidas = "";
            for (int v = 0; v < vidasRestantes; v++) iconesVidas += "ᗧ ";
            if (vidasRestantes <= 0) iconesVidas = "ZERO";
            GUILayout.Label($"<size=13><b>Vidas:</b> <color=#00FF99><b>{vidasRestantes}  {iconesVidas}</b></color></size>");

            // 2. Pontuação
            GUILayout.Label($"<size=13><b>Pontuação:</b> <color=#FFFF00><b>{pontuacaoAcumulada:D5} pts</b></color></size>");

            // 3. Entidade Ativa
            string entidadeExibida = !string.IsNullOrEmpty(ultimaClasseCapturada) ? ultimaClasseCapturada : "Iniciando...";
            GUILayout.Label($"<size=12><b>Entidade Ativa:</b> <color=#38BDF8><b>{entidadeExibida}</b></color></size>");

            if (temporizadorAvisoColisao > 0f && !string.IsNullOrEmpty(mensagemAvisoColisao))
            {
                GUILayout.Space(2f);
                GUI.color = Color.yellow;
                GUILayout.Label($"<size=11>⚠️ {mensagemAvisoColisao}</size>");
                GUI.color = Color.white;
            }

            GUILayout.EndArea();
        }

        /// <summary>
        /// Botão discreto para reabrir a janela de informações quando minimizada.
        /// </summary>
        private void DesenharBotaoReabrirHUD()
        {
            float w = 220f;
            float h = 32f;
            Rect miniRect = new Rect(35f, 35f, w, h);

            GUI.backgroundColor = new Color(0.02f, 0.05f, 0.14f, 0.92f);

            string iconesVidas = "";
            for (int v = 0; v < vidasRestantes; v++) iconesVidas += "ᗧ ";
            if (vidasRestantes <= 0) iconesVidas = "ZERO";

            if (GUI.Button(miniRect, $"<size=12><b>🕹️ Vidas: {vidasRestantes} {iconesVidas} [ + ]</b></size>"))
            {
                hudMinimizado = false;
            }
        }

        /// <summary>
        /// Tela de Game Over.
        /// </summary>
        private void DesenharGameOver()
        {
            float w = 400f;
            float h = 180f;
            Rect modalRect = new Rect((1920f - w) / 2f, (1080f - h) / 2f, w, h);

            GUI.backgroundColor = new Color(0.18f, 0.02f, 0.02f, 0.96f);
            GUI.Box(modalRect, GUIContent.none);

            GUILayout.BeginArea(new Rect(modalRect.x + 20f, modalRect.y + 16f, w - 40f, h - 32f));

            GUILayout.Label("<size=20><b><color=#FF3333>💀 G A M E   O V E R 💀</color></b></size>", new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });
            GUILayout.Space(10f);
            GUILayout.Label($"<size=14><b>Pontuação Final:</b> <color=#FFFF00>{pontuacaoAcumulada:D5} pts</color></size>", new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });
            GUILayout.Space(16f);

            GUI.backgroundColor = new Color(0.15f, 0.65f, 1f, 1f);
            if (GUILayout.Button("<size=14><b>🔄  J O G A R   N O V A M E N T E</b></size>", GUILayout.Height(42f)))
            {
                ReiniciarJogoPartida();
            }

            GUILayout.EndArea();
        }
    }
}
