using System;
using System.Collections.Generic;
using UnityEngine;
using VirtOnto.Model;

namespace PacMan.Semantics
{
    /// <summary>
    /// Estados ontológicos formais dos agentes no Pac-Man Semântico.
    /// </summary>
    public enum GameEntityState
    {
        Patrol,       // Modo Patrulha padrão (navegação sem ameaça imediata)
        Aggressive,   // Modo Agressivo / Perseguição (proximidade ao Pac-Man)
        Vulnerable,   // Modo Vulnerável (inferência SWRL: Pac-Man sob efeito de SpecialGem)
        Consumed      // Entidade consumida ou desativada logicamente
    }

    /// <summary>
    /// ============================================================================
    /// PAC-MAN SEMÂNTICO (CIn-UFPE) - Motor de Raciocínio Semântico
    /// ============================================================================
    /// Componente que governa os estados lógicos dos agentes (Pac-Man e Fantasmas)
    /// através de inferência dinâmica inspirada em regras SWRL e Lógica de Descrição.
    /// 
    /// Integrações:
    /// 1. Utiliza a estrutura do grafo VirtOnto (Graph, Node, Edge, Vector3D).
    /// 2. Conecta-se ao GerenciadorCenarioSemantico para sincronizar instâncias ativas.
    /// 3. Avalia distâncias euclidianas e propriedades de itens consumidos para
    ///    atualizar dinamicamente comportamentos (Patrol, Aggressive, Vulnerable).
    /// ============================================================================
    /// </summary>
    [DisallowMultipleComponent]
    public class MotorRaciocinioSemantico : MonoBehaviour
    {
        [Header("Grafo Semântico e Conexões")]
        [Tooltip("Referência ao Gerenciador Central de Cenário Semântico.")]
        public GerenciadorCenarioSemantico gerenciadorCenario;

        [Tooltip("Transform do Pac-Man (Jogador) para cálculo geométrico de distâncias.")]
        public Transform alvoPacMan;

        [Header("Estado de Poder do Pac-Man (SWRL Premise)")]
        [Tooltip("Indica se o Pac-Man está sob efeito de uma gema especial (Power Pellet).")]
        public bool pacmanIsPowered = false;

        [Tooltip("Tempo restante do estado de poder do Pac-Man em segundos.")]
        public float tempoPowerRestante = 0f;

        [Tooltip("Duração padrão do efeito de Power Pellet em segundos.")]
        public float duracaoPowerPellet = 8.0f;

        [Header("Limiares de Proximidade (Axiomas Espaciais SWRL)")]
        [Tooltip("Distância máxima para ativar regra SWRL de vulnerabilidade do fantasma.")]
        public float distanciaVulnerabilidade = 3.0f;

        [Tooltip("Distância de detecção para ativar comportamento agressivo.")]
        public float distanciaAgressao = 2.0f;

        [Header("Comportamentos Dinâmicos dos Fantasmas (Runtime)")]
        [Tooltip("Habilita atualização dinâmica de movimentação e reações em tempo de execução.")]
        public bool moverFantasmasDinamicamente = true;

        [Tooltip("Velocidade do fantasma ao perseguir o jogador no modo agressivo.")]
        public float velocidadeAgressao = 1.4f;

        [Tooltip("Velocidade de fuga do fantasma no modo vulnerável.")]
        public float velocidadeVulneravel = 0.9f;

        [Header("Cores dos Estados Ontológicos (Feedback Visual)")]
        public Color corPatrulha = new Color(1f, 0.2f, 0.2f);
        public Color corAgressivo = new Color(1f, 0.0f, 0.0f);
        public Color corVulneravel = new Color(0.15f, 0.45f, 1f);

        // Instância central do grafo ontológico
        private Graph semanticGraph;

        // Cache de estados ativos de cada entidade fantasma (Id -> Estado)
        private readonly Dictionary<string, GameEntityState> m_EstadosEntidades = new Dictionary<string, GameEntityState>(StringComparer.OrdinalIgnoreCase);

        // Eventos semânticos
        public event Action<string, GameEntityState> OnEstadoEntidadeAlterado;
        public event Action<bool> OnPacManPowerStateChanged;

        private void Awake()
        {
            semanticGraph = new Graph();
            InicializarTBoxABoxExemplo();

            if (gerenciadorCenario == null)
            {
                gerenciadorCenario = GetComponent<GerenciadorCenarioSemantico>() ?? FindAnyObjectByType<GerenciadorCenarioSemantico>();
            }
        }

        private void OnEnable()
        {
            InstanciaSemanticaBase.OnInstanciaInteragida += TratarInteracaoInstancia;
        }

        private void OnDisable()
        {
            InstanciaSemanticaBase.OnInstanciaInteragida -= TratarInteracaoInstancia;
        }

        private void Start()
        {
            LocalizarAlvoPacMan();

            // Se o gerenciador possuir um grafo preenchido, sincroniza com ele
            if (gerenciadorCenario != null && gerenciadorCenario.GrafoOntologico != null && gerenciadorCenario.GrafoOntologico.GetNodeCount() > 0)
            {
                SetSemanticGraph(gerenciadorCenario.GrafoOntologico);
            }
        }

        private void Update()
        {
            AtualizarTemporizadorPowerPellet();
            LocalizarAlvoPacMan();
            ProcessarRaciocinioFantasmas();
        }

        /// <summary>
        /// Localiza dinamicamente o alvo do Pac-Man (Jogador) na cena.
        /// </summary>
        private void LocalizarAlvoPacMan()
        {
            if (alvoPacMan != null) return;

            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                alvoPacMan = player.transform;
            }
            else if (Camera.main != null)
            {
                alvoPacMan = Camera.main.transform;
            }
        }

        /// <summary>
        /// Trata eventos de interação com itens do cenário (ex: coleta de gemas especiais).
        /// Regra SWRL: Consumir(PacMan, SpecialGem) -> HasState(PacMan, Empowered).
        /// </summary>
        private void TratarInteracaoInstancia(IInstanciaSemantica inst, bool sucesso)
        {
            if (sucesso && inst != null)
            {
                bool ehGemaEspecial = inst.NomeClasse.IndexOf("Special", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                      inst.NomeClasse.IndexOf("Power", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                      inst.NomeClasse.IndexOf("Pellet", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                      (inst.PropriedadesDL != null && inst.PropriedadesDL.ContainsKey("isPowerPellet"));

                if (ehGemaEspecial)
                {
                    AtivarPowerPellet(duracaoPowerPellet);
                }
            }
        }

        /// <summary>
        /// Atualiza o temporizador regressivo do estado Empowered do Pac-Man.
        /// </summary>
        private void AtualizarTemporizadorPowerPellet()
        {
            if (tempoPowerRestante > 0f)
            {
                tempoPowerRestante -= Time.deltaTime;
                if (tempoPowerRestante <= 0f)
                {
                    tempoPowerRestante = 0f;
                    pacmanIsPowered = false;
                    OnPacManPowerStateChanged?.Invoke(false);
                    Debug.Log("<color=#FF9900><b>[Motor Semântico]</b></color> Efeito do Power Pellet expirou. Pac-Man retornou ao estado normal.");
                }
            }
        }

        /// <summary>
        /// TBox e ABox inicial (Exemplo Pac-Man Semântico).
        /// </summary>
        private void InicializarTBoxABoxExemplo()
        {
            // TBox e ABox inicial (Exemplo Pac-Man Semântico)
            Node pacman = new Node("pacman_01", "PacMan", NodeType.Individual);
            pacman.SetDisplayColor("#FFFF00");

            Node ghost = new Node("ghost_blinky", "Blinky", NodeType.Individual);
            ghost.SetDisplayColor("#FF0000");

            semanticGraph.AddNode(pacman);
            semanticGraph.AddNode(ghost);

            // Adiciona aresta representando relação inicial (ex: isNearTo / hasState)
            Edge relacao = new Edge("edge_01", "pacman_01", "ghost_blinky", "isNearTo");
            semanticGraph.AddEdge(relacao);
        }

        /// <summary>
        /// Emula o motor SWRL: Avalia axiomas e transições de estado dinâmicas
        /// Regra SWRL Exemplo: IsNearTo(p, g) ^ HasState(p, Empowered) -> HasState(g, Vulnerable)
        /// </summary>
        public GameEntityState AvaliarRegraEstadoFantasma(string ghostId, bool pacmanIsPowered, float distancia)
        {
            Node ghostNode = semanticGraph != null ? semanticGraph.GetNode(ghostId) : null;
            if (ghostNode == null) return GameEntityState.Patrol;

            // Condicional baseada em Description Logic / SWRL
            if (distancia < distanciaVulnerabilidade && pacmanIsPowered)
            {
                Debug.Log($"[SWRL Engine] Regra disparada: Fantasma {ghostNode.GetLabel()} assumiu estado VULNERÁVEL.");
                return GameEntityState.Vulnerable;
            }
            else if (distancia < distanciaAgressao)
            {
                return GameEntityState.Aggressive;
            }

            return GameEntityState.Patrol;
        }

        /// <summary>
        /// Processa continuamente o raciocínio ontológico para todas as instâncias de fantasmas ativas no grafo/cenário.
        /// </summary>
        private void ProcessarRaciocinioFantasmas()
        {
            if (semanticGraph == null || alvoPacMan == null) return;

            Vector3 posPacMan = alvoPacMan.position;

            // Obtém todos os nós individuais do grafo
            var individuos = semanticGraph.GetNodesByType(NodeType.INDIVIDUAL);
            foreach (var node in individuos)
            {
                if (node == null || node.IsValid) continue; // Fantasmas/Obstáculos possuem IsValid == false

                // Calcula a distância atual entre Pac-Man e o Fantasma
                Vector3 posFantasma = node.GameObjectInstance != null ? node.GameObjectInstance.transform.position : (Vector3)node.Position;
                float distancia = Vector3.Distance(posPacMan, posFantasma);

                // Avalia o novo estado ontológico conforme as regras SWRL
                GameEntityState estadoAtual = m_EstadosEntidades.TryGetValue(node.Id, out var est) ? est : GameEntityState.Patrol;
                GameEntityState novoEstado = AvaliarRegraEstadoFantasma(node.Id, pacmanIsPowered, distancia);

                if (novoEstado != estadoAtual)
                {
                    m_EstadosEntidades[node.Id] = novoEstado;
                    AplicarEstadoNoGameObject(node, novoEstado);
                    OnEstadoEntidadeAlterado?.Invoke(node.Id, novoEstado);
                }

                // Atualiza dinamicamente o comportamento em tempo de execução
                AtualizarComportamentoFisicoFantasma(node, novoEstado, posPacMan, posFantasma);
            }
        }

        /// <summary>
        /// Atualiza dinamicamente a movimentação e reações do fantasma conforme o estado ontológico atual.
        /// </summary>
        private void AtualizarComportamentoFisicoFantasma(Node node, GameEntityState estado, Vector3 posPacMan, Vector3 posFantasma)
        {
            if (!moverFantasmasDinamicamente || node.GameObjectInstance == null) return;

            Transform tr = node.GameObjectInstance.transform;
            Vector3 posAtual = tr.position;

            if (estado == GameEntityState.Aggressive)
            {
                // Perseguição: move-se na direção horizontal do Pac-Man mantendo altura do chão
                Vector3 dir = (posPacMan - posAtual);
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.4f)
                {
                    tr.position += dir.normalized * (velocidadeAgressao * Time.deltaTime);
                    tr.rotation = Quaternion.Slerp(tr.rotation, Quaternion.LookRotation(dir.normalized), Time.deltaTime * 6f);
                }
            }
            else if (estado == GameEntityState.Vulnerable)
            {
                // Fuga: afasta-se na direção oposta ao Pac-Man mantendo a altura do chão
                Vector3 dir = (posAtual - posPacMan);
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.05f)
                {
                    tr.position += dir.normalized * (velocidadeVulneravel * Time.deltaTime);
                    tr.rotation = Quaternion.Slerp(tr.rotation, Quaternion.LookRotation(dir.normalized), Time.deltaTime * 6f);
                }
            }
            else if (estado == GameEntityState.Patrol)
            {
                // Patrulha / vigília suave
                tr.Rotate(Vector3.up, 25f * Time.deltaTime);
            }
        }

        /// <summary>
        /// Atualiza visualmente e logicamente a instância física do fantasma ao mudar de estado ontológico.
        /// </summary>
        private void AplicarEstadoNoGameObject(Node node, GameEntityState novoEstado)
        {
            if (node.GameObjectInstance == null) return;

            Renderer[] renderers = node.GameObjectInstance.GetComponentsInChildren<Renderer>();
            Color corAlvo = novoEstado switch
            {
                GameEntityState.Vulnerable => corVulneravel,
                GameEntityState.Aggressive => corAgressivo,
                _ => corPatrulha
            };

            foreach (var rend in renderers)
            {
                if (rend != null && rend.material != null)
                {
                    if (rend.material.HasProperty("_BaseColor")) rend.material.SetColor("_BaseColor", corAlvo);
                    if (rend.material.HasProperty("_Color")) rend.material.SetColor("_Color", corAlvo);
                    if (rend.material.HasProperty("_EmissionColor"))
                    {
                        rend.material.EnableKeyword("_EMISSION");
                        rend.material.SetColor("_EmissionColor", corAlvo * (novoEstado == GameEntityState.Aggressive ? 0.9f : 0.5f));
                    }
                }
            }

            // Atualiza componentes semânticos e de animação
            AnimacaoFantasma anim = node.GameObjectInstance.GetComponent<AnimacaoFantasma>();
            if (anim != null)
            {
                if (novoEstado == GameEntityState.Vulnerable)
                {
                    anim.DispararSusto(0.5f);
                }
            }
        }

        /// <summary>
        /// Ativa o estado de poder do Pac-Man (consumo de SpecialGem / Power Pellet),
        /// disparando imediatamente a regra SWRL de vulnerabilidade.
        /// </summary>
        public void AtivarPowerPellet(float duracao = 8.0f)
        {
            pacmanIsPowered = true;
            tempoPowerRestante = duracao;
            OnPacManPowerStateChanged?.Invoke(true);

            Debug.Log($"<color=#FFD700><b>[Motor Semântico - SpecialGem]</b></color> Pac-Man consumiu Power Pellet! " +
                      $"Estado Empowered ativo por {duracao:F1}s. Raciocinador avaliando vulnerabilidade dos fantasmas...");

            // Força avaliação imediata das regras
            ProcessarRaciocinioFantasmas();
        }

        /// <summary>
        /// Retorna o estado ontológico atual de uma entidade pelo ID.
        /// </summary>
        public GameEntityState ObterEstadoEntidade(string id)
        {
            return m_EstadosEntidades.TryGetValue(id, out var estado) ? estado : GameEntityState.Patrol;
        }

        /// <summary>
        /// Reseta os estados internos das entidades no motor de inferência.
        /// </summary>
        public void ResetarEstados()
        {
            m_EstadosEntidades.Clear();
            pacmanIsPowered = false;
            tempoPowerRestante = 0f;
            OnPacManPowerStateChanged?.Invoke(false);
        }

        public Graph GetSemanticGraph() => semanticGraph;

        public void SetSemanticGraph(Graph graph)
        {
            if (graph != null)
            {
                semanticGraph = graph;
                m_EstadosEntidades.Clear();
            }
        }
    }
}
