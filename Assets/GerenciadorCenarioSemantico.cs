using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using VirtOnto.Model;
using PacMan.Semantics;
using PacMan.Network;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// ============================================================================
/// PAC-MAN SEMÂNTICO (Semantic Pac-Man) - Modelagem Orientada a Objetos (CIn-UFPE)
/// ============================================================================
/// Gerenciador Central de Cenário Semântico do Pac-Man Semântico.
/// 
/// Responsabilidades Principais:
/// 1. Posicionamento Procedural Preciso: Ajusta o eixo Y com base no cálculo dinâmico
///    de bounds e pivô de cada modelo 3D, garantindo assentamento exato sobre a
///    superfície do Plane (eliminando flutuação indesejada ou corte no chão).
/// 2. Salvaguarda e Validação Robusta de Prefabs: Recupera automaticamente referências
///    perdidas/missing (Gemas Válidas, Obstáculos Inválidos, Agentes Pac-Man e Fantasmas),
///    com fallback procedural inteligente para que nenhuma categoria falhe.
/// 3. Espelhamento de Grafo Ontológico (VirtOnto em C#): Indexa nós e arestas da
///    ontologia OWL/SWRL (TBox, ABox, ObjectProperties e regras de inferência DL),
///    permitindo consultas estruturadas de alto desempenho em tempo real.
/// ============================================================================
/// </summary>
[ExecuteAlways]
public class GerenciadorCenarioSemantico : MonoBehaviour
{
    public enum OrigemDadosOntologia
    {
        ProceduralInterno,
        ProtegeWebSocket8085,
        ArquivoJson,
        ArquivoOwl,
        GrafoOntologicoVirtOnto,
        InjecaoExternaDinamica,
        BackendJavaSpringBoot
    }

    [Header("Identidade do Projeto")]
    [Tooltip("Nome oficial do sistema.")]
    public string nomeProjeto = "Pac-Man Semântico (Semantic Pac-Man) - CIn-UFPE";

    [Header("Origem dos Dados Ontológicos")]
    [Tooltip("Define a fonte das classes e instâncias da ontologia.")]
    public OrigemDadosOntologia origemDados = OrigemDadosOntologia.ProceduralInterno;

    [Tooltip("Arquivo JSON de ontologia exportada (utilizado quando origem for ArquivoJson).")]
    public TextAsset arquivoOntologiaJson;

    [Tooltip("Arquivo OWL (RDF/XML) exportado diretamente do Protégé (utilizado quando origem for ArquivoOwl).")]
    public TextAsset arquivoOntologiaOwl;

    [Header("Superfície e Assentamento no Chão (Plane)")]
    [Tooltip("Referência explícita ao GameObject do Plane/Piso. Se nulo, localiza automaticamente na cena.")]
    public Transform planoChao;

    [Tooltip("Garante que a base visual/física do modelo assente perfeitamente sobre a superfície do Plane, eliminando flutuação indesejada.")]
    public bool assentarPerfeitamenteNoChao = true;

    [Tooltip("Altura adicional das Gemas Válidas acima da superfície (0 = base perfeitamente tangenciando o chão).")]
    public float alturaGemas = 0.0f;

    [Tooltip("Altura adicional dos Obstáculos acima da superfície (0 = base perfeitamente apoiada no chão).")]
    public float alturaObstaculos = 0.0f;

    [Tooltip("Distância mínima entre instâncias para evitar sobreposição.")]
    public float distanciaMinimaEntreInstancias = 1.8f;

    [Header("Geração Procedural")]
    [Tooltip("Quantidade de instâncias válidas (Gemas que satisfazem regras de Description Logic).")]
    [Range(1, 100)]
    public int quantidadeValidos = 12;

    [Tooltip("Quantidade de instâncias inválidas (Obstáculos / Violações Semânticas).")]
    [Range(0, 50)]
    public int quantidadeInvalidos = 6;

    [Tooltip("Raio de distribuição dos elementos a partir do centro da arena.")]
    public float raioEspalhamento = 14f;

    [Header("Prefabs Semânticos (Carregamento e Validação Robusta)")]
    [Tooltip("Prefab do modelo de gema válida (ex: GemaValida / PacDot).")]
    public GameObject prefabGemaValida;

    [Tooltip("Prefab do modelo de obstáculo inválido (ex: ObstaculoInvalido / Fantasma Blinky).")]
    public GameObject prefabObstaculoInvalido;

    [Tooltip("Prefab opcional do agente jogador (Pac-Man).")]
    public GameObject prefabAgentePacMan;

    [Tooltip("Prefab opcional para agentes fantasmas específicos.")]
    public GameObject prefabAgenteFantasma;

    [Tooltip("Prefab opcional para gemas especiais (Power Pellet).")]
    public GameObject prefabGemaEspecial;

    [System.Serializable]
    public struct MapeamentoClassePrefab
    {
        public string nomeClasseOntologica;
        public GameObject prefabCorrespondente;

        public MapeamentoClassePrefab(string nome, GameObject prefab)
        {
            nomeClasseOntologica = nome;
            prefabCorrespondente = prefab;
        }
    }

    [Tooltip("Mapeamentos personalizados entre classes da ontologia OWL e modelos 3D específicos.")]
    public List<MapeamentoClassePrefab> mapeamentoClassesCustomizadas = new List<MapeamentoClassePrefab>();

    [Header("Raciocínio Ontológico (SWRL Engine)")]
    [Tooltip("Motor de Raciocínio Semântico que avalia regras SWRL e gere comportamentos dos agentes.")]
    public MotorRaciocinioSemantico motorRaciocinio;

    [Header("Conexão com Backend Java / Protégé (WebSocket 8085 & REST)")]
    [Tooltip("Cliente WebSocket nativo para o plugin Java/OSGi no Protégé (porta 8085).")]
    public ProtegeWebSocketBridge protegeWebSocket;

    [Tooltip("Cliente de rede REST opcional.")]
    public ApiClient apiClient;

    [Header("Labirinto 3D (Paredes Físicas e Delimitação)")]
    [Tooltip("Gera as paredes físicas perimetrais e internas do labirinto.")]
    public bool instanciarLabirinto3D = true;

    [Tooltip("Altura das paredes 3D do labirinto.")]
    public float alturaParedes = 1.8f;
    // Spawns dos 4 Fantasmas estritamente dentro da Gaiola Central do Labirinto (Y = 0.0f Snap no Chao)
    private int m_ContadorSpawnFantasmas = 0;
    private int m_ContadorFantasmasConfigurados = 0;
    private static readonly Vector3[] s_SpawnsGaiolaCentral = new Vector3[]
    {
        new Vector3(-1.0f, 0.0f, 0.0f),   // Blinky (Vermelho) - Gaiola Central
        new Vector3(1.0f, 0.0f, 0.0f),    // Pinky (Rosa) - Gaiola Central
        new Vector3(-1.0f, 0.0f, -1.2f),  // Inky (Azul) - Gaiola Central
        new Vector3(1.0f, 0.0f, -1.2f)    // Clyde (Laranja) - Gaiola Central
    };

    // Cores canônicas vibrantes dos 4 Fantasmas clássicos (Vermelho, Rosa, Azul, Laranja)
    public static readonly Color[] CoresClassicasFantasmas = new Color[]
    {
        new Color(1.0f, 0.10f, 0.12f, 1f), // Blinky - Vermelho
        new Color(1.0f, 0.45f, 0.75f, 1f), // Pinky - Rosa
        new Color(0.0f, 0.85f, 1.0f, 1f),  // Inky - Ciano
        new Color(1.0f, 0.55f, 0.05f, 1f)  // Clyde - Laranja
    };

    public static readonly string[] NomesFantasmasClassicos = new string[]
    {
        "Blinky_Vermelho",
        "Pinky_Rosa",
        "Inky_Azul",
        "Clyde_Laranja"
    };


    [Tooltip("Espessura das paredes.")]
    public float espessuraParedes = 1.2f;

    [Tooltip("Dimensão total do labirinto em metros (X e Z).")]
    public float tamanhoLabirinto = 28f;

    [Tooltip("Material customizado para as paredes (se nulo, aplica Topologia Semântica por Superclasse).")]
    public Material materialParedes;

    [System.Serializable]
    public struct MapeamentoCorSuperclasse
    {
        public string nomeSuperclasse;
        public Color corMaterialRegiao;

        public MapeamentoCorSuperclasse(string superclasse, Color cor)
        {
            nomeSuperclasse = superclasse;
            corMaterialRegiao = cor;
        }
    }

    [Header("Topologia Semântica (Cores do Labirinto por Superclasse OWL)")]
    [Tooltip("Associa cores de materiais às paredes/regiões da NavMesh Surface conforme a Superclasse ontológica.")]
    public bool usarTopologiaSemanticaPorSuperclasse = true;

    [Tooltip("Cor da região da Superclasse 'Parasitas' (Nematoda / Platyhelminthes / Protozoa) -> Roxo.")]
    public Color corSuperclasseParasitas = new Color(0.55f, 0.16f, 0.90f, 1f);

    [Tooltip("Cor da região da Superclasse 'Hospedeiros' (Host / Human / Vertebrate) -> Azul.")]
    public Color corSuperclasseHospedeiros = new Color(0.10f, 0.45f, 0.95f, 1f);

    [Tooltip("Tabela extensível de mapeamento Superclasse OWL -> Cor de Material da Região.")]
    public List<MapeamentoCorSuperclasse> mapeamentoCoresSuperclasse = new List<MapeamentoCorSuperclasse>()
    {
        new MapeamentoCorSuperclasse("Parasitas", new Color(0.55f, 0.16f, 0.90f, 1f)),
        new MapeamentoCorSuperclasse("Hospedeiros", new Color(0.10f, 0.45f, 0.95f, 1f))
    };

    private GameObject m_ContainerParedes;

    [Header("Status do Grafo Ontológico (VirtOnto C#)")]
    [SerializeField] private string m_NomeOntologiaAtiva = "Pac-Man Semantic Ontology";
    [SerializeField] private string m_VersaoAxiomas = "ALCQ(D)";
    [SerializeField] private int m_TotalInstanciasInstanciadas = 0;
    [SerializeField] private int m_TotalNosGrafo = 0;
    [SerializeField] private int m_TotalArestasGrafo = 0;

    // Coleções ativas do labirinto
    private readonly List<IInstanciaSemantica> m_InstanciasSemanticas = new List<IInstanciaSemantica>();
    private readonly List<GameObject> m_ObjetosInstanciados = new List<GameObject>();
    private readonly List<Vector3> m_PosicoesOcupadas = new List<Vector3>();

    // Grafo Ontológico VirtOnto em C#
    public Graph GrafoOntologico { get; private set; } = new Graph();

    // Cache interno de materiais procedurais de contingência
    private Material m_MaterialGemaProcedural;
    private Material m_MaterialObstaculoProcedural;

    /// <summary>
    /// Lista pública somente-leitura de todas as instâncias ontológicas ativas no labirinto.
    /// </summary>
    public IReadOnlyList<IInstanciaSemantica> InstanciasAtivas => m_InstanciasSemanticas;

    public NavMeshSurface GarantirNavMeshSurface()
    {
        NavMeshSurface surface = GetComponent<NavMeshSurface>();
        if (surface == null)
        {
            surface = gameObject.AddComponent<NavMeshSurface>();
        }
        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        return surface;
    }

    private void Awake()
    {
        CarregarPrefabsAutomaticamente();
        LocalizarPlanoChao();
        GarantirNavMeshSurface();
        InicializarMotorRaciocinio();
        InicializarApiClient();
    }

    public void InicializarMotorRaciocinio()
    {
        if (motorRaciocinio == null)
        {
            motorRaciocinio = GetComponent<MotorRaciocinioSemantico>() ?? FindAnyObjectByType<MotorRaciocinioSemantico>();
            if (motorRaciocinio == null)
            {
                motorRaciocinio = gameObject.AddComponent<MotorRaciocinioSemantico>();
            }
        }

        if (motorRaciocinio != null)
        {
            motorRaciocinio.gerenciadorCenario = this;
        }
    }

    public void InicializarApiClient()
    {
        if (apiClient == null)
        {
            apiClient = GetComponent<ApiClient>() ?? FindAnyObjectByType<ApiClient>();
            if (apiClient == null)
            {
                apiClient = gameObject.AddComponent<ApiClient>();
            }
        }

        if (apiClient != null)
        {
            apiClient.gerenciadorCenario = this;
        }
    }

    private void Start()
    {
        if (Application.isPlaying)
        {
            SemanticHUDManager.GarantirHUDAtivoNaCena();
        }
        GerarInstanciasOntologicas();
    }

#if UNITY_EDITOR



    private void OnValidate()
    {
        CarregarPrefabsAutomaticamente();
        if (planoChao == null)
        {
            LocalizarPlanoChao();
        }
    }
#endif

    public void LocalizarPlanoChao()
    {
        if (planoChao == null)
        {
            GameObject planeObj = GameObject.Find("Plane") ?? GameObject.Find("Floor") ?? GameObject.Find("Chao") ?? GameObject.Find("Piso");
            if (planeObj != null)
            {
                planoChao = planeObj.transform;
            }
        }

        if (planoChao != null)
        {
            AplicarMaterialChaoLaboratorio();
        }
    }

    /// <summary>
    /// Aplica o material texturizado de laboratório limpo e reflexivo de alta fidelidade ao chão.
    /// </summary>
    public void AplicarMaterialChaoLaboratorio()
    {
        if (planoChao == null) return;

        Renderer rendChao = planoChao.GetComponent<Renderer>();
        if (rendChao != null)
        {
            Material matChao = CriarMaterialChaoLaboratorio();
            rendChao.material = matChao;
            rendChao.receiveShadows = true;
        }
    }

    // =========================================================================
    // 1. GERAÇÃO E CONSUMO DO CENÁRIO ONTOLÓGICO
    // =========================================================================

    /// <summary>
    /// Ponto de entrada principal: Gera o labirinto semântico conforme a origem de dados configurada.
    /// </summary>
    [ContextMenu("Pac-Man Semântico/Gerar Cenário Ontológico")]
    public void GerarInstanciasOntologicas()
    {
        LimparCenarioExistente();
        CarregarPrefabsAutomaticamente();
        LocalizarPlanoChao();
        GarantirNavMeshSurface();

        // 0. GARANTIA ABSOLUTA DE INSTANCIAÇÃO DAS PAREDES DO LABIRINTO 3D
        if (instanciarLabirinto3D)
        {
            ConstruirLabirinto3D();
        }

        CenarioSemanticoDTO cenario = null;

        switch (origemDados)
        {
            case OrigemDadosOntologia.GrafoOntologicoVirtOnto:
                Debug.Log("<color=#00DDFF><b>[Pac-Man Semântico - VirtOnto C#]</b></color> Construindo e consumindo Grafo Ontológico VirtOnto...");
                GrafoOntologico = ConstruirGrafoVirtOntoPadrao();
                cenario = GrafoOntologico.ToCenarioDTO();
                break;

            case OrigemDadosOntologia.ProtegeWebSocket8085:
                Debug.Log("<color=#00DDFF><b>[Interface Espacial OWL - Protégé WS:8085]</b></color> Conectando ao servidor WebSocket Java/OSGi no Protégé...");
                if (protegeWebSocket == null)
                {
                    protegeWebSocket = GetComponent<ProtegeWebSocketBridge>() ?? FindFirstObjectByType<ProtegeWebSocketBridge>();
                    if (protegeWebSocket == null)
                    {
                        protegeWebSocket = gameObject.AddComponent<ProtegeWebSocketBridge>();
                    }
                }
                cenario = BackendOntologiaBridge.GerarCenarioOntologiaPadrao(quantidadeValidos, quantidadeInvalidos, raioEspalhamento, alturaGemas);
                break;

            case OrigemDadosOntologia.ArquivoJson:
                if (arquivoOntologiaJson != null)
                {
                    Debug.Log($"<color=#00DDFF><b>[Pac-Man Semântico - JSON]</b></color> Carregando ontologia do arquivo '{arquivoOntologiaJson.name}'...");
                    cenario = BackendOntologiaBridge.CarregarDeJson(arquivoOntologiaJson.text);
                }
                else
                {
                    Debug.LogWarning("[Pac-Man Semântico] Nenhum arquivo JSON atribuído. Ativando gerador procedural de contingência.");
                    cenario = BackendOntologiaBridge.GerarCenarioOntologiaPadrao(quantidadeValidos, quantidadeInvalidos, raioEspalhamento, alturaGemas);
                }
                break;

            case OrigemDadosOntologia.ArquivoOwl:
                if (arquivoOntologiaOwl != null)
                {
                    Debug.Log($"<color=#00DDFF><b>[Pac-Man Semântico - OWL]</b></color> Carregando ontologia Protégé do arquivo '{arquivoOntologiaOwl.name}'...");
                    cenario = BackendOntologiaBridge.CarregarDeOwl(arquivoOntologiaOwl.text);
                }
                else
                {
                    TextAsset resOwl = Resources.Load<TextAsset>("parasites.owl") ?? Resources.Load<TextAsset>("parasites");
                    if (resOwl != null)
                    {
                        Debug.Log($"<color=#00DDFF><b>[Pac-Man Semântico - OWL]</b></color> Carregando ontologia Protégé de Resources/parasites.owl...");
                        cenario = BackendOntologiaBridge.CarregarDeOwl(resOwl.text);
                    }
                    else
                    {
                        cenario = BackendOntologiaBridge.GerarCenarioOntologiaPadrao(quantidadeValidos, quantidadeInvalidos, raioEspalhamento, alturaGemas);
                    }
                }
                break;

            case OrigemDadosOntologia.InjecaoExternaDinamica:
                Debug.Log("[Pac-Man Semântico] Modo de Injeção Dinâmica Ativo. Aguardando chamada de CarregarCenarioDeDados().");
                return;

            case OrigemDadosOntologia.BackendJavaSpringBoot:
                Debug.Log("<color=#00DDFF><b>[Pac-Man Semântico - Backend Java Spring Boot]</b></color> Consultando microsserviço REST em " + (apiClient != null ? apiClient.baseUrl : "http://localhost:8080/api/ontology") + "...");
                InicializarApiClient();
                if (apiClient != null)
                {
                    apiClient.SincronizarCenarioComBackend();
                }
                return;

            case OrigemDadosOntologia.ProceduralInterno:
            default:
                Debug.Log("<color=#00FF99><b>[Pac-Man Semântico - Procedural]</b></color> Gerando instâncias ontológicas Description Logic...");
                cenario = BackendOntologiaBridge.GerarCenarioOntologiaPadrao(quantidadeValidos, quantidadeInvalidos, raioEspalhamento, alturaGemas);
                break;
        }

        if (cenario != null)
        {
            CarregarCenarioDeDados(cenario);
        }
    }

    /// <summary>
    /// API Modular orientada a objetos: Constrói o Grafo VirtOnto e instancia o cenário físico no Unity.
    /// </summary>
    public void CarregarCenarioDeDados(CenarioSemanticoDTO cenario)
    {
        if (cenario == null)
        {
            Debug.LogError("[Pac-Man Semântico] Erro: Tentativa de carregar cenário ontológico nulo.");
            return;
        }

        LimparCenarioExistente();

        m_NomeOntologiaAtiva = string.IsNullOrEmpty(cenario.nomeOntologia) ? "Pac-Man Ontology" : cenario.nomeOntologia;
        m_VersaoAxiomas = string.IsNullOrEmpty(cenario.versaoAxiomas) ? "v1.0" : cenario.versaoAxiomas;

        PrepararMateriaisProcedurais();
        LocalizarPlanoChao();
        ConfigurarIluminacaoSciFi();

        // 0. Instanciação física do Labirinto 3D (Paredes delimitadoras)
        if (instanciarLabirinto3D)
        {
            ConstruirLabirinto3D();
        }

        // 0.1 BAKE DINÂMICO DO NAVMESH EM TEMPO DE EXECUÇÃO
        // Chamado exatamente após todas as paredes e chão serem instanciados, mas antes de instanciar ou habilitar os NavMeshAgents (Fantasmas).
        NavMeshSurface navSurface = GetComponent<NavMeshSurface>();
        if (navSurface == null)
        {
            navSurface = gameObject.AddComponent<NavMeshSurface>();
            navSurface.collectObjects = CollectObjects.All;
        navSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        }
        navSurface.BuildNavMesh();
        Debug.Log("<color=#00FF99><b>[NavMesh Dinâmico]</b></color> Malha de navegação gerada em tempo de execução com BuildNavMesh() após instanciação de paredes e chão.");

        // 1. Espelhamento formal: Constrói e indexa o Grafo VirtOnto a partir do DTO
        GrafoOntologico.BuildFromCenarioDTO(cenario);
        m_TotalNosGrafo = GrafoOntologico.GetNodeCount();
        m_TotalArestasGrafo = GrafoOntologico.GetEdgeCount();

        int contadorValidos = 0;
        int contadorInvalidos = 0;

        // 2. Instanciação física robusta no labirinto gerenciada a partir dos nós do Grafo VirtOnto
        var nosIndividuos = GrafoOntologico.GetNodesByType(NodeType.INDIVIDUAL);
        foreach (var node in nosIndividuos)
        {
            if (node == null) continue;

            DadoInstanciaSemantica dado = new DadoInstanciaSemantica
            {
                id = node.Id,
                classeOntologica = string.IsNullOrEmpty(node.ConceptClass) ? node.Label : node.ConceptClass,
                expressaoDL = node.DLExpression,
                ehValida = node.IsValid,
                posicao = node.Position,
                valorPontuacao = node.ScoreValue,
                descricaoSemantica = node.SemanticDescription,
                corDestaque = node.HighlightColor
            };

            GameObject objInstanciado = InstanciarObjetoSemantico(dado);
            if (objInstanciado != null)
            {
                m_ObjetosInstanciados.Add(objInstanciado);
                node.GameObjectInstance = objInstanciado;
                node.Position = objInstanciado.transform.position;

                IInstanciaSemantica instSemantica = objInstanciado.GetComponent<IInstanciaSemantica>();
                if (instSemantica != null)
                {
                    m_InstanciasSemanticas.Add(instSemantica);
                }

                if (node.IsValid) contadorValidos++;
                else contadorInvalidos++;
            }
        }

        InstanciarCerejasEPowerPellets();
        m_TotalInstanciasInstanciadas = m_ObjetosInstanciados.Count;

        // 2.1 Garante ponte WebSocket ativa com o plugin Java/OSGi do Protégé (porta 8085)
        if (protegeWebSocket == null)
        {
            protegeWebSocket = GetComponent<ProtegeWebSocketBridge>() ?? FindFirstObjectByType<ProtegeWebSocketBridge>();
            if (protegeWebSocket == null && Application.isPlaying)
            {
                protegeWebSocket = gameObject.AddComponent<ProtegeWebSocketBridge>();
            }
        }

        // 3. Avaliação inicial de regras SWRL sobre o grafo instanciado
        GrafoOntologico.AvaliarRegrasSWRL(msg => Debug.Log($"<color=#9B59B6><b>[VirtOnto SWRL]</b></color> {msg}"));

        // 4. Conecta e sincroniza o Grafo Ontológico com o Motor de Raciocínio Semântico
        if (motorRaciocinio != null)
        {
            motorRaciocinio.SetSemanticGraph(GrafoOntologico);
        }

        Debug.Log($"<color=#00FF99><b>[Pac-Man Semântico CIn-UFPE]</b></color> Cenário '<b>{m_NomeOntologiaAtiva}</b>' ({m_VersaoAxiomas}) instanciado com sucesso!\n" +
                  $"• Instâncias Físicas: {m_TotalInstanciasInstanciadas} ({contadorValidos} Gemas Válidas / {contadorInvalidos} Obstáculos/Fantasmas)\n" +
                  $"• Grafo VirtOnto: {m_TotalNosGrafo} Nós (Classes TBox + Indivíduos ABox) / {m_TotalArestasGrafo} Arestas (OWL/SWRL).");
    }

    // =========================================================================
    // 2. CÁLCULO DE SPAWN E ASSENTAMENTO PERFEITO SOBRE O CHÃO (PLANE)
    // =========================================================================

    private GameObject InstanciarObjetoSemantico(DadoInstanciaSemantica dado)
    {
        // 1. Obtém o prefab validado ou determina o tipo para fallback
        GameObject prefabEscolhido = ObterPrefabParaClasse(dado.classeOntologica, dado.ehValida);

        // 2. Determina a coordenada horizontal (X, Z) garantindo que esteja estritamente livre de paredes
        bool ehFantasma = !dado.ehValida ||
                          dado.classeOntologica.IndexOf("fantasma", StringComparison.OrdinalIgnoreCase) >= 0 ||
                          dado.classeOntologica.IndexOf("ghost", StringComparison.OrdinalIgnoreCase) >= 0 ||
                          dado.classeOntologica.IndexOf("invalido", StringComparison.OrdinalIgnoreCase) >= 0 ||
                          dado.classeOntologica.IndexOf("violacao", StringComparison.OrdinalIgnoreCase) >= 0;

        Vector3 posicaoPlana;
        if (ehFantasma)
        {
            // FANTASMAS (Obstáculos Inválidos): Nascem ESTRITAMENTE no interior da gaiola central do labirinto
            posicaoPlana = ObterPosicaoValidaNoChao(false);
            posicaoPlana.x = Mathf.Clamp(posicaoPlana.x, -2.2f, 2.2f);
            posicaoPlana.z = Mathf.Clamp(posicaoPlana.z, -2.0f, 1.5f);
        }
        else if (dado.posicao == Vector3.zero || !VerificarPosicaoLivreDeParedes(dado.posicao) || m_PosicoesOcupadas.Count == 0)
        {
            posicaoPlana = ObterPosicaoValidaNoChao(true);
        }
        else
        {
            posicaoPlana = dado.posicao;
        }

        m_PosicoesOcupadas.Add(posicaoPlana);

        // Orientação inicial vertical sem inclinações que causem corte no plano
        Quaternion rotacaoFinal = dado.rotacao != Quaternion.identity ? dado.rotacao : Quaternion.identity;

        // 3. Instanciação com salvaguarda contra referências quebradas
        GameObject instanciaObj = null;
        if (IsPrefabValid(prefabEscolhido))
        {
            try
            {
                instanciaObj = Instantiate(prefabEscolhido, posicaoPlana, rotacaoFinal, transform);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Pac-Man Semântico] Erro ao instanciar prefab para '{dado.classeOntologica}': {ex.Message}. Ativando fallback procedural.");
                instanciaObj = CriarProceduralPorClasse(posicaoPlana, dado);
            }
        }
        else
        {
            instanciaObj = CriarProceduralPorClasse(posicaoPlana, dado);
        }

        instanciaObj.name = $"Instancia_{dado.classeOntologica}_{dado.id}";

        if (dado.escala != Vector3.one && dado.escala != Vector3.zero)
        {
            instanciaObj.transform.localScale = dado.escala;
        }

        ConfigurarComponentesSemanticos(instanciaObj, dado);

        // 4. CÁLCULO DE ASSENTAMENTO PERFEITO NO PLANO (PLANE)
        // Detecta a superfície física real do Plane via Raycast ou referência explícita
        float chaoY = ObterAlturaSuperficieChao(posicaoPlana);

        // Sincroniza transformações para que o cálculo de Bounds seja perfeitamente exato
        Physics.SyncTransforms();

        float distanciaPivotParaBase = CalcularDistanciaPivotParaBase(instanciaObj);

        // Aplica o Y final: superfície do chão + distância até a base do modelo + offset opcional
        float offsetVerticalInstancia = dado.ehValida ? alturaGemas : alturaObstaculos;
        // Para fantasmas (NavMeshAgent), a coordenada Y DEVE ser exatamente a altura do chão (chaoY) para snap automático no NavMesh
        float yFinal = dado.ehValida ? (chaoY + distanciaPivotParaBase + (assentarPerfeitamenteNoChao ? 0f : offsetVerticalInstancia)) : chaoY;

        Vector3 posFinalAssentada = new Vector3(posicaoPlana.x, yFinal, posicaoPlana.z);
        instanciaObj.transform.position = posFinalAssentada;

        // Snap seguro do NavMeshAgent na malha de navegação para Fantasmas (Snap to Ground sem erro de Resume)
        if (ehFantasma)
        {
            UnityEngine.AI.NavMeshAgent agent = instanciaObj.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null)
            {
                agent.baseOffset = 0f;
                agent.enabled = false;

                // Amostragem restrita de 1.2m para evitar snap em corredores externos do outro lado da parede da gaiola
                if (UnityEngine.AI.NavMesh.SamplePosition(posFinalAssentada, out UnityEngine.AI.NavMeshHit hit, 1.2f, UnityEngine.AI.NavMesh.AllAreas)
                    && Mathf.Abs(hit.position.x) <= 2.5f && hit.position.z >= -2.2f && hit.position.z <= 2.0f)
                {
                    instanciaObj.transform.position = hit.position;
                    agent.enabled = true;
                    agent.Warp(hit.position);
                }
                else
                {
                    instanciaObj.transform.position = posFinalAssentada;
                    agent.enabled = true;
                    agent.Warp(posFinalAssentada);
                }

                if (agent.enabled && agent.isOnNavMesh)
                {
                    agent.isStopped = false;
                }
            }
        }

        // 5. Atualiza os componentes de animação para respeitar o ponto de apoio sem flutuação
        AjustarComportamentosDeFlutuacao(instanciaObj);

        return instanciaObj;
    }

    /// <summary>
    /// Calcula a altura da superfície superior do chão (Plane) na coordenada XZ especificada.
    /// Utiliza Raycast físico de cima para baixo com contingência inteligente para o Transform do Plane.
    /// </summary>
    public float ObterAlturaSuperficieChao(Vector3 posXZ)
    {
        Vector3 origemRaio = new Vector3(posXZ.x, 50f, posXZ.z);
        RaycastHit[] hits = Physics.RaycastAll(origemRaio, Vector3.down, 100f, ~0, QueryTriggerInteraction.Ignore);
        if (hits != null && hits.Length > 0)
        {
            // Prioriza o chão do labirinto (Plane / Floor / Piso)
            for (int i = 0; i < hits.Length; i++)
            {
                var h = hits[i];
                if (h.collider == null || h.collider.isTrigger) continue;
                string nome = h.collider.name.ToLower();
                if (nome.Contains("plane") || nome.Contains("chao") || nome.Contains("floor") || nome.Contains("piso") || nome.Contains("ground"))
                {
                    return h.point.y;
                }
            }

            // Segundo filtro: qualquer superfície sólida que não seja o Player ou Fantasma
            for (int i = 0; i < hits.Length; i++)
            {
                var h = hits[i];
                if (h.collider == null || h.collider.isTrigger) continue;
                if (h.collider.CompareTag("Player") || h.collider.name.Contains("XR") || h.collider.name.Contains("PacMan") || h.collider.name.Contains("Fantasma") || h.collider.name.Contains("Blinky"))
                {
                    continue;
                }
                return h.point.y;
            }
        }

        // Fallback: se o Plane foi identificado, utiliza a superfície superior do seu colisor ou posição
        if (planoChao != null)
        {
            Collider colChao = planoChao.GetComponent<Collider>();
            if (colChao != null)
            {
                return colChao.bounds.max.y;
            }
            return planoChao.position.y;
        }

        return 0f;
    }

    /// <summary>
    /// Calcula dinamicamente a distância vertical entre o Pivot (transform.position)
    /// e a base mais baixa (bounds.min.y) de todas as malhas ou colisores do modelo.
    /// </summary>
    public static float CalcularDistanciaPivotParaBase(GameObject obj)
    {
        if (obj == null) return 0f;

        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        if (renderers != null && renderers.Length > 0)
        {
            bool boundsIniciado = false;
            Bounds b = default;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].enabled)
                {
                    if (!boundsIniciado)
                    {
                        b = renderers[i].bounds;
                        boundsIniciado = true;
                    }
                    else
                    {
                        b.Encapsulate(renderers[i].bounds);
                    }
                }
            }
            if (boundsIniciado)
            {
                return obj.transform.position.y - b.min.y;
            }
        }

        Collider[] colliders = obj.GetComponentsInChildren<Collider>();
        if (colliders != null && colliders.Length > 0)
        {
            bool boundsIniciado = false;
            Bounds b = default;
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null && colliders[i].enabled)
                {
                    if (!boundsIniciado)
                    {
                        b = colliders[i].bounds;
                        boundsIniciado = true;
                    }
                    else
                    {
                        b.Encapsulate(colliders[i].bounds);
                    }
                }
            }
            if (boundsIniciado)
            {
                return obj.transform.position.y - b.min.y;
            }
        }

        return 0f;
    }

    private void AjustarComportamentosDeFlutuacao(GameObject instanciaObj)
    {
        // Gema: Mantém rotação contínua elegante no eixo Y, eliminando oscilações que penetrem o chão
        RotacaoFlutuante rot = instanciaObj.GetComponent<RotacaoFlutuante>();
        if (rot != null)
        {
            rot.permitirFlutuacao = !assentarPerfeitamenteNoChao;
            rot.velocidadeRotacao = new Vector3(0f, 60f, 0f);
            rot.AtualizarPosicaoBase(instanciaObj.transform.localPosition);
        }

        // Fantasma: Desativa oscilação vertical invasiva que faça o corpo cortar o piso
        AnimacaoFantasma anim = instanciaObj.GetComponent<AnimacaoFantasma>();
        if (anim != null)
        {
            anim.permitirFlutuacao = !assentarPerfeitamenteNoChao;
            anim.inclinacaoLateral = 0f;
            anim.AtualizarPosicaoBase(instanciaObj.transform.localPosition);
        }
    }

    /// <summary>
    /// Verifica se uma coordenada 3D está totalmente livre de colisões com paredes do labirinto.
    /// Garante que nenhuma entidade (Gema ou Fantasma) nasça ou seja posicionada sobre paredes.
    /// </summary>
    public bool VerificarPosicaoLivreDeParedes(Vector3 pos, float raioLivre = 0.65f)
    {
        // Limites do labirinto perimetral
        float limite = (tamanhoLabirinto / 2f) - 0.9f;
        if (Mathf.Abs(pos.x - transform.position.x) > limite || Mathf.Abs(pos.z - transform.position.z) > limite)
        {
            return false;
        }

        // Teste de sobreposição com BoxColliders de paredes instanciadas
        if (m_ContainerParedes != null)
        {
            Collider[] hits = Physics.OverlapSphere(pos + Vector3.up * 0.45f, raioLivre, ~0, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                if (hit != null && (hit.transform.IsChildOf(m_ContainerParedes.transform) || hit.name.StartsWith("Parede_")))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private Vector3 ObterPosicaoValidaNoChao(bool ehGemaValida)
    {
        // Se for Fantasma (Obstaculo Invalido), spawn estritamente dentro da Gaiola Central no nivel exato do chao
        float chaoNivel = ObterAlturaSuperficieChao(Vector3.zero);
        if (!ehGemaValida)
        {
            Vector3 posGaiola = s_SpawnsGaiolaCentral[m_ContadorSpawnFantasmas % s_SpawnsGaiolaCentral.Length];
            m_ContadorSpawnFantasmas++;
            return new Vector3(posGaiola.x, chaoNivel, posGaiola.z);
        }

        float altura = transform.position.y + (ehGemaValida ? alturaGemas : alturaObstaculos);
        Vector3 posicaoCandidata = Vector3.zero;

        // Waypoints principais de corredores do labirinto clássico (100% navegáveis)
        Vector2[] corredoresSeguros = new Vector2[]
        {
            new Vector2(0f, 3.5f),
            new Vector2(0f, -5.5f),
            new Vector2(0f, 7.0f),
            new Vector2(0f, -7.0f),
            new Vector2(-5.5f, 0f),
            new Vector2(5.5f, 0f),
            new Vector2(-5.5f, 3.5f),
            new Vector2(5.5f, 3.5f),
            new Vector2(-5.5f, -4.0f),
            new Vector2(5.5f, -4.0f),
            new Vector2(-8.5f, 3.5f),
            new Vector2(8.5f, 3.5f),
            new Vector2(-8.5f, -3.5f),
            new Vector2(8.5f, -3.5f),
            new Vector2(-3.5f, 8.5f),
            new Vector2(3.5f, 8.5f),
            new Vector2(-3.5f, -8.5f),
            new Vector2(3.5f, -8.5f)
        };

        // 1. Tenta posicionamento ancorado nos corredores seguros navegáveis
        for (int i = 0; i < corredoresSeguros.Length; i++)
        {
            Vector2 baseCorredor = corredoresSeguros[(i + m_PosicoesOcupadas.Count) % corredoresSeguros.Length];
            Vector2 jitter = UnityEngine.Random.insideUnitCircle * 0.7f;
            Vector3 cand = new Vector3(transform.position.x + baseCorredor.x + jitter.x, altura, transform.position.z + baseCorredor.y + jitter.y);

            if (VerificarPosicaoLivreDeParedes(cand, 0.65f))
            {
                bool livre = true;
                foreach (var pos in m_PosicoesOcupadas)
                {
                    if (Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(cand.x, cand.z)) < distanciaMinimaEntreInstancias)
                    {
                        livre = false;
                        break;
                    }
                }
                if (livre) return cand;
            }
        }

        // 2. Fallback procedural com validação rigorosa de paredes
        for (int i = 0; i < 60; i++)
        {
            Vector2 circulo = UnityEngine.Random.insideUnitCircle * raioEspalhamento;
            if (circulo.magnitude < 2.0f) circulo = circulo.normalized * 2.5f;

            posicaoCandidata = new Vector3(transform.position.x + circulo.x, altura, transform.position.z + circulo.y);

            if (!VerificarPosicaoLivreDeParedes(posicaoCandidata, 0.65f)) continue;

            bool posicaoLivre = true;
            foreach (var pos in m_PosicoesOcupadas)
            {
                if (Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(posicaoCandidata.x, posicaoCandidata.z)) < (distanciaMinimaEntreInstancias * 0.75f))
                {
                    posicaoLivre = false;
                    break;
                }
            }

            if (posicaoLivre) return posicaoCandidata;
        }

        return posicaoCandidata;
    }

    // =========================================================================
    // 3. SALVAGUARDA E RESOLUÇÃO ROBUSTA DE PREFABS
    // =========================================================================

    public static bool IsPrefabValid(GameObject prefab)
    {
        if (prefab == null) return false;
        if (ReferenceEquals(prefab, null)) return false;
        try
        {
            // Testa se a referência nativa interna do Unity está viva e válida
            return !string.IsNullOrEmpty(prefab.name) && prefab.transform != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Validação e salvaguarda robusta de referências a prefabs.
    /// Recupera automaticamente qualquer prefab marcado como Missing ou nulo no Inspector.
    /// </summary>
    public void CarregarPrefabsAutomaticamente()
    {
        if (!IsPrefabValid(prefabGemaValida))
        {
            prefabGemaValida = LoadPrefabRobust("GemaValida");
        }

        if (!IsPrefabValid(prefabObstaculoInvalido))
        {
            prefabObstaculoInvalido = LoadPrefabRobust("ObstaculoInvalido");
        }

        if (!IsPrefabValid(prefabAgentePacMan))
        {
            prefabAgentePacMan = LoadPrefabRobust("PacManJogador");
        }

        if (!IsPrefabValid(prefabAgenteFantasma))
        {
            prefabAgenteFantasma = LoadPrefabRobust("ObstaculoInvalido");
        }

        if (!IsPrefabValid(prefabGemaEspecial))
        {
            prefabGemaEspecial = LoadPrefabRobust("GemaValida");
        }

        // Garante mapeamentos canônicos das classes ontológicas OWL para modelos 3D
        GarantirMapeamentosCanonicos();
    }

    private void GarantirMapeamentosCanonicos()
    {
        string[] classesGemas = {
            "AscarisLumbricoides", "EnterobiusVermicularis", "TaeniaSolium",
            "Nematoda", "Platyhelminthes", "Parasite", "Host", "Human", "Pig",
            "SmallIntestine", "LargeIntestine", "PerianalRegion",
            "Cylindrical", "FlatSegments", "FecalOral", "UndercookedPork", "Autoinfection",
            "ConformingParasiteSpecimen", "AntiparasiticTreatment"
        };
        string[] classesFantasmas = {
            "InfectionVector", "TriatomineVector", "BiomphalariaVector", "AedesVector",
            "InconsistentParasiteSpecimen", "HostMismatchViolation", "ContaminatedHostVector",
            "ViolatedTaxon", "InconsistentSpecimen"
        };

        foreach (var cls in classesGemas)
        {
            RegistrarMapeamentoSeAusente(cls, prefabGemaValida);
        }

        foreach (var cls in classesFantasmas)
        {
            RegistrarMapeamentoSeAusente(cls, prefabObstaculoInvalido);
        }

        RegistrarMapeamentoSeAusente("BiomedicalResearcher", prefabAgentePacMan);
        RegistrarMapeamentoSeAusente("Pacman", prefabAgentePacMan);
        RegistrarMapeamentoSeAusente("Jogador", prefabAgentePacMan);
    }

    private void RegistrarMapeamentoSeAusente(string nomeClasse, GameObject prefabPadrao)
    {
        int idx = mapeamentoClassesCustomizadas.FindIndex(m => string.Equals(m.nomeClasseOntologica, nomeClasse, StringComparison.OrdinalIgnoreCase));
        if (idx >= 0)
        {
            var m = mapeamentoClassesCustomizadas[idx];
            if (!IsPrefabValid(m.prefabCorrespondente) && IsPrefabValid(prefabPadrao))
            {
                m.prefabCorrespondente = prefabPadrao;
                mapeamentoClassesCustomizadas[idx] = m;
            }
        }
        else if (IsPrefabValid(prefabPadrao))
        {
            mapeamentoClassesCustomizadas.Add(new MapeamentoClassePrefab(nomeClasse, prefabPadrao));
        }
    }

    private GameObject LoadPrefabRobust(string prefabName)
    {
        // 1. Tenta carregar da pasta Resources
        GameObject loaded = Resources.Load<GameObject>($"Prefabs/{prefabName}") ?? Resources.Load<GameObject>(prefabName);

#if UNITY_EDITOR
        // 2. Se falhar, busca no AssetDatabase durante o editor
        if (loaded == null)
        {
            string[] possiblePaths = new string[]
            {
                $"Assets/Prefabs/{prefabName}.prefab",
                $"Assets/Resources/Prefabs/{prefabName}.prefab",
                $"Assets/{prefabName}.prefab"
            };

            foreach (var path in possiblePaths)
            {
                loaded = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (loaded != null) break;
            }
        }
#endif
        return loaded;
    }

    private GameObject ObterPrefabParaClasse(string nomeClasse, bool ehValida)
    {
        if (!string.IsNullOrEmpty(nomeClasse))
        {
            foreach (var m in mapeamentoClassesCustomizadas)
            {
                if (string.Equals(m.nomeClasseOntologica, nomeClasse, StringComparison.OrdinalIgnoreCase) && IsPrefabValid(m.prefabCorrespondente))
                {
                    return m.prefabCorrespondente;
                }
            }

            // Heurística de classificação ontológica pelo nome
            string nomeLower = nomeClasse.ToLower();
            if (nomeLower.Contains("pacman") || nomeLower.Contains("jogador"))
            {
                if (IsPrefabValid(prefabAgentePacMan)) return prefabAgentePacMan;
            }

            if (nomeLower.Contains("ghost") || nomeLower.Contains("fantasma") || nomeLower.Contains("blinky") || nomeLower.Contains("obstaculo"))
            {
                if (IsPrefabValid(prefabAgenteFantasma)) return prefabAgenteFantasma;
                if (IsPrefabValid(prefabObstaculoInvalido)) return prefabObstaculoInvalido;
            }

            if (nomeLower.Contains("special") || nomeLower.Contains("power"))
            {
                if (IsPrefabValid(prefabGemaEspecial)) return prefabGemaEspecial;
            }
        }

        return ehValida ? (IsPrefabValid(prefabGemaValida) ? prefabGemaValida : null)
                        : (IsPrefabValid(prefabObstaculoInvalido) ? prefabObstaculoInvalido : null);
    }

    private void ConfigurarComponentesSemanticos(GameObject obj, DadoInstanciaSemantica dado)
    {
        // Garante a presenca de colisor Trigger ampliado para interacao espacial
        Collider[] colisores = obj.GetComponentsInChildren<Collider>();
        if (colisores.Length == 0)
        {
            SphereCollider sc = obj.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = dado.ehValida ? 0.65f : 0.45f;
        }
        else
        {
            foreach (var c in colisores)
            {
                c.isTrigger = true;
                if (dado.ehValida && c is SphereCollider sc)
                {
                    sc.radius = Mathf.Max(sc.radius, 0.65f);
                }
            }
        }

        // Rigidbody cinematico para objetos de cenario
        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // Vincula a implementacao de IInstanciaSemantica
        IInstanciaSemantica instSemantica = obj.GetComponent<IInstanciaSemantica>();
        if (instSemantica == null)
        {
            if (dado.ehValida)
            {
                GemaValidaSemantica gema = obj.AddComponent<GemaValidaSemantica>();
                instSemantica = gema;
            }
            else
            {
                ObstaculoInvalidoSemantico obs = obj.AddComponent<ObstaculoInvalidoSemantico>();
                instSemantica = obs;
            }
        }

        instSemantica.InicializarSemantica(dado);

        if (dado.ehValida)
        {
            // NODOS DE DADOS OWL (Cubos/Prismas Translúcidos): Remoção de NavMeshAgent ou GemaFugitiva
            UnityEngine.AI.NavMeshAgent nav = obj.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (nav != null) Destroy(nav);

            GemaFugitiva fuga = obj.GetComponent<GemaFugitiva>();
            if (fuga != null) Destroy(fuga);

            SphereCollider sc = obj.GetComponent<SphereCollider>();
            if (sc == null) sc = obj.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = 0.45f;

            if (obj.GetComponent<RotacaoFlutuante>() == null)
            {
                RotacaoFlutuante rot = obj.AddComponent<RotacaoFlutuante>();
                rot.permitirFlutuacao = false;
            }

            // Converte o MeshFilter do núcleo para Cubo/Prisma (Nodo de Dados) e aplica material translúcido
            Transform nucleo = obj.transform.Find("Nucleo_PacDot");
            if (nucleo != null)
            {
                nucleo.localScale = new Vector3(0.32f, 0.46f, 0.32f);
                nucleo.localRotation = Quaternion.Euler(35f, 45f, 0f);
                MeshFilter mf = nucleo.GetComponent<MeshFilter>();
                if (mf != null)
                {
                    GameObject tempCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    mf.sharedMesh = tempCube.GetComponent<MeshFilter>().sharedMesh;
                    if (Application.isPlaying) Destroy(tempCube);
                    else DestroyImmediate(tempCube);
                }
            }
            Transform halo = obj.transform.Find("Halo_Aura");
            if (halo != null)
            {
                halo.localScale = new Vector3(0.42f, 0.015f, 0.42f);
            }

            Color corNodoBase = dado.corDestaque != Color.clear ? dado.corDestaque : new Color(0.0f, 0.90f, 1.0f, 1f);
            Material matNodoTranslucido = CriarMaterialTranslúcidoNodo(corNodoBase, 0.58f);
            Renderer[] rendsNodo = obj.GetComponentsInChildren<Renderer>();
            foreach (var r in rendsNodo)
            {
                r.material = matNodoTranslucido;
            }
        }
        else
        {
            // REASONER AGENTS (Antigos Fantasmas):
            // Estado inicial Patrol (CONSISTENTE): Material Azul Translúcido.
            // Ao detectar erro lógico (INCONSISTENTE), o MotorRaciocinioSemantico altera para Vermelho Sólido (Chase).
            int ghostIdx = (m_ContadorFantasmasConfigurados) % CoresClassicasFantasmas.Length;
            string nomeGhost = NomesFantasmasClassicos[ghostIdx];
            obj.name = $"ReasonerAgent_{nomeGhost}_{dado.id}";
            obj.transform.localScale = new Vector3(0.80f, 0.80f, 0.80f);

            Color corAzulPatrol = new Color(0.10f, 0.55f, 1.0f, 1f);
            Material matAgentPatrol = CriarMaterialTranslúcidoNodo(corAzulPatrol, 0.52f);
            Renderer[] rendsFantasma = obj.GetComponentsInChildren<Renderer>();
            foreach (var r in rendsFantasma)
            {
                string rName = r.name.ToLower();
                if (!rName.Contains("olho") && !rName.Contains("pupila") && !rName.Contains("eye"))
                {
                    r.material = matAgentPatrol;
                }
            }

            // NavMeshAgent com Avoidance Priority diferenciada (30, 40, 50, 60)
            UnityEngine.AI.NavMeshAgent agent = obj.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent == null)
            {
                agent = obj.AddComponent<UnityEngine.AI.NavMeshAgent>();
            }
            agent.speed = 2.0f;
            agent.acceleration = 12f;
            agent.radius = 0.28f;
            agent.height = 1.0f;
            agent.stoppingDistance = 0.2f;
            agent.autoBraking = false;
            agent.baseOffset = 0f;
            if (agent.isOnNavMesh)
            {
                agent.isStopped = false;
            }
            
            int prioridade = 30 + ((m_ContadorFantasmasConfigurados++) % 4) * 10;
            agent.avoidancePriority = prioridade;
        }
    }




    // =========================================================================
    // 4. MODELAGEM PROCEDURAL DE CONTINGÊNCIA (FALLBACK INTELIGENTE)
    // =========================================================================

    private GameObject CriarProceduralPorClasse(Vector3 posicao, DadoInstanciaSemantica dado)
    {
        string nomeLower = dado.classeOntologica.ToLower();
        if (nomeLower.Contains("pacman") || nomeLower.Contains("jogador"))
        {
            // Vincula o nó ontológico diretamente ao jogador real (XR Origin) sem criar esfera dummy estática no chão
            GameObject realPlayer = GameObject.FindWithTag("Player");
            if (realPlayer == null)
            {
                var ctrlXR = FindFirstObjectByType<ControladorXRJogador>();
                if (ctrlXR != null) realPlayer = ctrlXR.gameObject;
            }
            if (realPlayer != null) return realPlayer;
            return CriarPacManProcedural(posicao, dado);
        }

        if (dado.ehValida)
        {
            return CriarGemaProcedural(posicao, dado);
        }
        else
        {
            return CriarObstaculoProcedural(posicao, dado);
        }
    }

    private GameObject CriarGemaProcedural(Vector3 posicao, DadoInstanciaSemantica dado = null)
    {
        GameObject nodoObj = new GameObject("NodoDados_Procedural");
        nodoObj.transform.position = posicao;
        nodoObj.transform.SetParent(transform);

        Color corBase = (dado != null && dado.corDestaque != Color.clear) ? dado.corDestaque : new Color(0.0f, 0.90f, 1.0f, 1f);
        Material matNodo = CriarMaterialTranslúcidoNodo(corBase, 0.58f);

        // Cubo / Prisma rotacionado (Nodo de Dados OWL)
        GameObject cuboPrisma = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cuboPrisma.name = "Nucleo_PacDot";
        cuboPrisma.transform.SetParent(nodoObj.transform, false);
        cuboPrisma.transform.localScale = new Vector3(0.32f, 0.46f, 0.32f);
        cuboPrisma.transform.localRotation = Quaternion.Euler(35f, 45f, 0f);
        Destroy(cuboPrisma.GetComponent<Collider>());
        cuboPrisma.GetComponent<Renderer>().material = matNodo;

        // Halo cibernético de base
        GameObject halo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        halo.name = "Halo_Aura";
        halo.transform.SetParent(nodoObj.transform, false);
        halo.transform.localScale = new Vector3(0.42f, 0.015f, 0.42f);
        Destroy(halo.GetComponent<Collider>());
        halo.GetComponent<Renderer>().material = matNodo;

        SphereCollider sc = nodoObj.AddComponent<SphereCollider>();
        sc.isTrigger = true;
        sc.radius = 0.45f;

        nodoObj.AddComponent<GemaValidaSemantica>();
        RotacaoFlutuante rot = nodoObj.AddComponent<RotacaoFlutuante>();
        rot.permitirFlutuacao = false;

        return nodoObj;
    }

    /// <summary>
    /// Cria um material translúcido cibernético para os Nodos de Dados OWL e Reasoner Agents.
    /// </summary>
    public Material CriarMaterialTranslúcidoNodo(Color corBase, float alpha = 0.58f)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Diffuse");
        Material mat = new Material(shader);
        Color corTranslucida = new Color(corBase.r, corBase.g, corBase.b, alpha);

        // Configuração de Transparência para URP e Standard
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f); // 1 = Transparent no URP
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
        if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", 0);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", corTranslucida);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", corTranslucida);

        if (mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(corBase.r, corBase.g, corBase.b, 1f) * 0.65f);
        }

        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.92f);
        return mat;
    }

        private GameObject CriarObstaculoProcedural(Vector3 posicao, DadoInstanciaSemantica dado = null)
    {
        // Trava matemática de segurança: nascimento rigoroso no interior da gaiola central
        posicao.x = Mathf.Clamp(posicao.x, -2.2f, 2.2f);
        posicao.z = Mathf.Clamp(posicao.z, -2.0f, 1.5f);

        int ghostIdx = (m_ContadorFantasmasConfigurados) % CoresClassicasFantasmas.Length;
        string nomeGhost = NomesFantasmasClassicos[ghostIdx];

        GameObject fantasma = new GameObject($"ReasonerAgent_{nomeGhost}");
        fantasma.transform.position = posicao;
        fantasma.transform.SetParent(transform);

        // Personagem em escala adequada ao corredor: 0.80f
        fantasma.transform.localScale = new Vector3(0.80f, 0.80f, 0.80f);

        Material matCorpo = CriarMaterialTranslúcidoNodo(new Color(0.10f, 0.55f, 1.0f, 1f), 0.52f);

        // 1. CORPO EM FORMA DE CÁPSULA (CAPSULE) CONFORME EXIGÊNCIA ESTRITA
        GameObject corpoCapsula = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        corpoCapsula.name = "Corpo_Capsula";
        corpoCapsula.transform.SetParent(fantasma.transform, false);
        corpoCapsula.transform.localPosition = new Vector3(0f, 0.52f, 0f);
        corpoCapsula.transform.localScale = new Vector3(0.85f, 0.65f, 0.85f);
        Destroy(corpoCapsula.GetComponent<Collider>());
        corpoCapsula.GetComponent<Renderer>().material = matCorpo;

        // 2. DOIS OLHOS BRANCOS MENORES COM PUPILAS AZUIS (LOOK DIRECIONAL)
        Shader shaderUnlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
        Material matOlho = new Material(shaderUnlit) { color = Color.white };
        Material matPupila = new Material(shaderUnlit) { color = new Color(0.05f, 0.20f, 0.95f) };

        // Olho Esquerdo (Esfera Branca Menor)
        GameObject olhoEsq = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        olhoEsq.name = "OlhoEsquerdo";
        olhoEsq.transform.SetParent(fantasma.transform, false);
        olhoEsq.transform.localPosition = new Vector3(-0.20f, 0.62f, 0.32f);
        olhoEsq.transform.localScale = new Vector3(0.24f, 0.28f, 0.20f);
        Destroy(olhoEsq.GetComponent<Collider>());
        olhoEsq.GetComponent<Renderer>().material = matOlho;

        GameObject pupilaEsq = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        pupilaEsq.name = "PupilaEsquerda";
        pupilaEsq.transform.SetParent(olhoEsq.transform, false);
        pupilaEsq.transform.localPosition = new Vector3(0f, 0f, 0.35f);
        pupilaEsq.transform.localScale = new Vector3(0.50f, 0.50f, 0.50f);
        Destroy(pupilaEsq.GetComponent<Collider>());
        pupilaEsq.GetComponent<Renderer>().material = matPupila;

        // Olho Direito (Esfera Branca Menor)
        GameObject olhoDir = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        olhoDir.name = "OlhoDireito";
        olhoDir.transform.SetParent(fantasma.transform, false);
        olhoDir.transform.localPosition = new Vector3(0.20f, 0.62f, 0.32f);
        olhoDir.transform.localScale = new Vector3(0.24f, 0.28f, 0.20f);
        Destroy(olhoDir.GetComponent<Collider>());
        olhoDir.GetComponent<Renderer>().material = matOlho;

        GameObject pupilaDir = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        pupilaDir.name = "PupilaDireita";
        pupilaDir.transform.SetParent(olhoDir.transform, false);
        pupilaDir.transform.localPosition = new Vector3(0f, 0f, 0.35f);
        pupilaDir.transform.localScale = new Vector3(0.50f, 0.50f, 0.50f);
        Destroy(pupilaDir.GetComponent<Collider>());
        pupilaDir.GetComponent<Renderer>().material = matPupila;

        AnimacaoFantasma anim = fantasma.AddComponent<AnimacaoFantasma>();
        anim.pupilaEsquerda = pupilaEsq.transform;
        anim.pupilaDireita = pupilaDir.transform;
        anim.permitirFlutuacao = false;

        // NavMeshAgent para locomoção suave pelos corredores
        UnityEngine.AI.NavMeshAgent agent = fantasma.AddComponent<UnityEngine.AI.NavMeshAgent>();
        agent.speed = 2.6f;
        agent.acceleration = 12f;
        agent.radius = 0.28f;
        agent.height = 1.0f;
        agent.stoppingDistance = 0.2f;
        agent.autoBraking = false;
        agent.baseOffset = 0f;
        agent.enabled = false;

        if (UnityEngine.AI.NavMesh.SamplePosition(posicao, out UnityEngine.AI.NavMeshHit hitFantasma, 1.2f, UnityEngine.AI.NavMesh.AllAreas))
        {
            fantasma.transform.position = hitFantasma.position;
            agent.enabled = true;
            agent.Warp(hitFantasma.position);
        }
        else
        {
            fantasma.transform.position = posicao;
            agent.enabled = true;
            agent.Warp(posicao);
        }

        m_ContadorFantasmasConfigurados++;
        return fantasma;
    }

        private GameObject CriarPacManProcedural(Vector3 posicao, DadoInstanciaSemantica dado = null)
    {
        // 1. Tenta carregar o prefab clássico PacManJogador se presente
        GameObject prefabPac = LoadPrefabRobust("PacManJogador");
        if (prefabPac != null)
        {
            GameObject pacInst = Instantiate(prefabPac, posicao, Quaternion.identity, transform);
            pacInst.name = "PacMan_Jogador";
            pacInst.transform.localScale = new Vector3(0.80f, 0.80f, 0.80f);
            return pacInst;
        }

        // 2. Modelo Procedural autêntico do Pac-Man com mandíbulas articuladas, olhos e material Amarelo Brilhante
        GameObject pacObj = new GameObject("PacMan_Procedural");
        pacObj.transform.position = posicao;
        pacObj.transform.SetParent(transform);
        pacObj.transform.localScale = new Vector3(0.80f, 0.80f, 0.80f);

        // Material Amarelo Brilhante Clássico
        Material matPac = Resources.Load<Material>("Mat_PacMan_Amarelo");
#if UNITY_EDITOR
        if (matPac == null)
        {
            matPac = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_PacMan_Amarelo.mat");
        }
#endif
        if (matPac == null)
        {
            Color amarelo = new Color(1.0f, 0.92f, 0.0f);
            matPac = CriarMaterialDinamico(amarelo, amarelo * 1.5f, 0.95f);
        }

        // Mandíbula Superior
        GameObject mandSup = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        mandSup.name = "MandibulaSuperior";
        mandSup.transform.SetParent(pacObj.transform, false);
        mandSup.transform.localPosition = new Vector3(0f, 0.12f, 0f);
        mandSup.transform.localScale = new Vector3(0.9f, 0.55f, 0.9f);
        Destroy(mandSup.GetComponent<Collider>());
        mandSup.GetComponent<Renderer>().material = matPac;

        // Mandíbula Inferior
        GameObject mandInf = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        mandInf.name = "MandibulaInferior";
        mandInf.transform.SetParent(pacObj.transform, false);
        mandInf.transform.localPosition = new Vector3(0f, -0.12f, 0f);
        mandInf.transform.localScale = new Vector3(0.9f, 0.55f, 0.9f);
        Destroy(mandInf.GetComponent<Collider>());
        mandInf.GetComponent<Renderer>().material = matPac;

        // Olhos expressivos do Pac-Man
        Material matOlho = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard")) { color = Color.black };
        GameObject olhoEsq = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        olhoEsq.name = "Olho_Esq";
        olhoEsq.transform.SetParent(mandSup.transform, false);
        olhoEsq.transform.localPosition = new Vector3(-0.25f, 0.35f, 0.25f);
        olhoEsq.transform.localScale = new Vector3(0.12f, 0.20f, 0.12f);
        Destroy(olhoEsq.GetComponent<Collider>());
        olhoEsq.GetComponent<Renderer>().material = matOlho;

        GameObject olhoDir = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        olhoDir.name = "Olho_Dir";
        olhoDir.transform.SetParent(mandSup.transform, false);
        olhoDir.transform.localPosition = new Vector3(0.25f, 0.35f, 0.25f);
        olhoDir.transform.localScale = new Vector3(0.12f, 0.20f, 0.12f);
        Destroy(olhoDir.GetComponent<Collider>());
        olhoDir.GetComponent<Renderer>().material = matOlho;

        AnimacaoBocaPacMan anim = pacObj.AddComponent<AnimacaoBocaPacMan>();
        anim.mandibulaSuperior = mandSup.transform;
        anim.mandibulaInferior = mandInf.transform;

        return pacObj;
    }

    private Texture2D m_TexturaParedeBiomedica;
    private Texture2D m_NormalMapParede;
    private Texture2D m_TexturaChaoLaboratorio;
    private Texture2D m_NormalMapChao;

    private void PrepararMateriaisProcedurais()
    {
        if (m_MaterialGemaProcedural == null)
        {
            m_MaterialGemaProcedural = CriarMaterialDinamico(
                corBase: new Color(0.1f, 1f, 0.45f),
                corEmissao: new Color(0f, 0.9f, 0.4f) * 1.5f,
                suavidade: 0.95f
            );
        }

        if (m_MaterialObstaculoProcedural == null)
        {
            m_MaterialObstaculoProcedural = CriarMaterialDinamico(
                corBase: new Color(1f, 0.12f, 0.2f),
                corEmissao: new Color(0.95f, 0.05f, 0.1f) * 1.4f,
                suavidade: 0.9f
            );
        }
    }

    private Material CriarMaterialDinamico(Color corBase, Color corEmissao, float suavidade)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Diffuse");
        Material mat = new Material(shader);

        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", corBase);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", corBase);

        if (mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", corEmissao);
        }

        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", suavidade);
        return mat;
    }

    /// <summary>
    /// Determina a Superclasse OWL associada à região espacial do labirinto (Topologia Semântica).
    /// Hemisfério Norte / NW / NE / Leste = "Parasitas" (Roxo);
    /// Hemisfério Sul / SW / SE / Oeste = "Hospedeiros" (Azul).
    /// </summary>
    public string DeterminarSuperclassePorRegiao(Vector3 posicao, string nomeBloco = "")
    {
        if (!string.IsNullOrEmpty(nomeBloco))
        {
            if (nomeBloco.Contains("Norte") || nomeBloco.Contains("NW") || nomeBloco.Contains("NE") || nomeBloco.Contains("Leste"))
            {
                return "Parasitas";
            }
            if (nomeBloco.Contains("Sul") || nomeBloco.Contains("SW") || nomeBloco.Contains("SE") || nomeBloco.Contains("Oeste"))
            {
                return "Hospedeiros";
            }
        }

        return (posicao.z >= 0f && posicao.x >= 0f) || posicao.z > 1.0f ? "Parasitas" : "Hospedeiros";
    }

    /// <summary>
    /// Retorna a Cor de Material associada à Superclasse da região gerada:
    /// - "Parasitas" (Nematoda / Platyhelminthes / Protozoa) = Roxo
    /// - "Hospedeiros" (Host / Human / Pig) = Azul
    /// </summary>
    public Color ObterCorTopologiaSuperclasse(string superclasse)
    {
        if (!string.IsNullOrEmpty(superclasse))
        {
            if (mapeamentoCoresSuperclasse != null)
            {
                for (int i = 0; i < mapeamentoCoresSuperclasse.Count; i++)
                {
                    if (string.Equals(mapeamentoCoresSuperclasse[i].nomeSuperclasse, superclasse, StringComparison.OrdinalIgnoreCase))
                    {
                        return mapeamentoCoresSuperclasse[i].corMaterialRegiao;
                    }
                }
            }

            if (superclasse.IndexOf("Parasit", StringComparison.OrdinalIgnoreCase) >= 0 ||
                superclasse.IndexOf("Nematod", StringComparison.OrdinalIgnoreCase) >= 0 ||
                superclasse.IndexOf("Platyhelmint", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return corSuperclasseParasitas; // Roxo
            }

            if (superclasse.IndexOf("Hosped", StringComparison.OrdinalIgnoreCase) >= 0 ||
                superclasse.IndexOf("Host", StringComparison.OrdinalIgnoreCase) >= 0 ||
                superclasse.IndexOf("Human", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return corSuperclasseHospedeiros; // Azul
            }
        }

        return corSuperclasseHospedeiros;
    }

    /// <summary>
    /// Cria o material de parede parametrizado pela Superclasse OWL da região (ex: "Parasitas" = Roxo; "Hospedeiros" = Azul).
    /// </summary>
    public Material CriarMaterialParedePorSuperclasse(string superclasse)
    {
        Color corSuperclasse = ObterCorTopologiaSuperclasse(superclasse);
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Diffuse");
        Material mat = new Material(shader);
        mat.name = $"Mat_Parede_Superclasse_{superclasse}";

        // Tonalidade profunda de laboratório tingida pela cor semântica da Superclasse (Roxo vs Azul)
        Color corParedeBase = Color.Lerp(new Color(0.06f, 0.06f, 0.14f, 1f), corSuperclasse, 0.58f);
        corParedeBase.a = 1f;

        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", corParedeBase);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", corParedeBase);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.12f);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.38f);

        if (mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", corSuperclasse * 0.25f);
        }

        return mat;
    }

    /// <summary>
    /// Cria o material avançado Sci-Fi / Laboratório Biomédico para paredes com texturas compostas e bump maps.
    /// </summary>
    public Material CriarMaterialParedeBiomedicaSciFi()
    {
        return CriarMaterialParedePorSuperclasse("Parasitas");
    }

    /// <summary>
    /// Cria o material néon luminoso (LED Trim) para frisos e sinalizações de corredores com a cor da Superclasse.
    /// </summary>
    public Material CriarMaterialNeonLED(Color corLED)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Diffuse");
        Material mat = new Material(shader);
        mat.name = "Mat_NeonLED_Trim";

        Color corFriso = new Color(corLED.r, corLED.g, corLED.b, 1f);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", corFriso);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", corFriso);

        if (mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", corFriso * 0.65f);
        }

        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.6f);
        return mat;
    }

    /// <summary>
    /// Cria o material reflexivo e texturizado de laboratório estéril com grid metálico para o chão.
    /// </summary>
    public Material CriarMaterialChaoLaboratorio()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Diffuse");
        Material mat = new Material(shader);
        mat.name = "Mat_Chao_Preto_Absoluto";

        // Preto absoluto (#000000) estilo arcade clássico
        Color corPreta = Color.black;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", corPreta);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", corPreta);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0f);

        if (mat.HasProperty("_EmissionColor"))
        {
            mat.DisableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", Color.black);
        }

        return mat;
    }




    private Texture2D GerarTexturaPainelBiomedico(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;

        Color darkTitanium = new Color(0.06f, 0.09f, 0.15f, 1f);
        Color slatePanel = new Color(0.12f, 0.17f, 0.26f, 1f);
        Color borderBevel = new Color(0.20f, 0.28f, 0.40f, 1f);
        Color circuitNeon = new Color(0.0f, 0.8f, 1.0f, 1f);

        Color[] pixels = new Color[w * h];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int px = x % (w / 2);
                int py = y;

                bool ehBordaExterna = (px < 4 || px >= (w / 2) - 4 || py < 4 || py >= h - 4);
                bool ehChanfro = (px >= 4 && px < 8) || (px >= (w / 2) - 8 && px < (w / 2) - 4) ||
                                 (py >= 4 && py < 8) || (py >= h - 8 && py < h - 4);
                bool ehCircuito = (px == 18 && py > 20 && py < h - 20) || (px == (w / 2) - 18 && py > 20 && py < h - 20);

                if (ehCircuito)
                {
                    pixels[y * w + x] = circuitNeon;
                }
                else if (ehBordaExterna)
                {
                    pixels[y * w + x] = darkTitanium;
                }
                else if (ehChanfro)
                {
                    pixels[y * w + x] = borderBevel;
                }
                else
                {
                    float noise = Mathf.PerlinNoise(x * 0.15f, y * 0.05f) * 0.08f;
                    pixels[y * w + x] = slatePanel + new Color(noise, noise, noise, 0f);
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private Texture2D GerarTexturaChaoGrid(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Trilinear;

        Color tileColor = new Color(0.04f, 0.06f, 0.10f, 1f);
        Color gridBorder = new Color(0.15f, 0.22f, 0.35f, 1f);
        Color guideTrack = new Color(0.0f, 0.85f, 0.95f, 1f);

        Color[] pixels = new Color[w * h];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                bool ehGridLinha = (x < 3 || x >= w - 3 || y < 3 || y >= h - 3);
                bool ehCentroGuide = (x >= w / 2 - 2 && x <= w / 2 + 2) && (y >= h / 2 - 2 && y <= h / 2 + 2);

                if (ehCentroGuide)
                {
                    pixels[y * w + x] = guideTrack;
                }
                else if (ehGridLinha)
                {
                    pixels[y * w + x] = gridBorder;
                }
                else
                {
                    float noise = Mathf.PerlinNoise(x * 0.2f, y * 0.2f) * 0.03f;
                    pixels[y * w + x] = tileColor + new Color(noise, noise, noise * 1.5f, 0f);
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private Texture2D GerarNormalMapDeTextura(Texture2D baseTex, float forca)
    {
        int w = baseTex.width;
        int h = baseTex.height;
        Texture2D normalMap = new Texture2D(w, h, TextureFormat.RGBA32, true);
        normalMap.wrapMode = TextureWrapMode.Repeat;

        Color[] basePixels = baseTex.GetPixels();
        Color[] normalPixels = new Color[w * h];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int xEsq = (x - 1 + w) % w;
                int xDir = (x + 1) % w;
                int yBaixo = (y - 1 + h) % h;
                int yCima = (y + 1) % h;

                float lEsq = basePixels[y * w + xEsq].grayscale;
                float lDir = basePixels[y * w + xDir].grayscale;
                float lBaixo = basePixels[yBaixo * w + x].grayscale;
                float lCima = basePixels[yCima * w + x].grayscale;

                float dX = (lDir - lEsq) * forca;
                float dY = (lCima - lBaixo) * forca;

                Vector3 normal = new Vector3(-dX, -dY, 1f).normalized;

                normalPixels[y * w + x] = new Color(
                    normal.x * 0.5f + 0.5f,
                    normal.y * 0.5f + 0.5f,
                    normal.z * 0.5f + 0.5f,
                    1f
                );
            }
        }

        normalMap.SetPixels(normalPixels);
        normalMap.Apply();
        return normalMap;
    }

    /// <summary>
    /// Configura iluminação global Sci-Fi, sombras suaves e luzes de corredor néon imersivas.
    /// </summary>
    public void ConfigurarIluminacaoSciFi()
    {
        Light dirLight = null;
        Light[] allLights = FindObjectsByType<Light>(FindObjectsSortMode.None);
        foreach (var l in allLights)
        {
            if (l.type == LightType.Directional)
            {
                dirLight = l;
                break;
            }
        }

        if (dirLight != null)
        {
            dirLight.intensity = 1.35f;
            dirLight.color = new Color(0.95f, 0.97f, 1f);
            dirLight.shadows = LightShadows.Soft;
            dirLight.shadowStrength = 0.85f;
            dirLight.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
        }

        GameObject containerLuzes = GameObject.Find("Iluminacao_Corredores_SciFi");
        if (containerLuzes == null)
        {
            containerLuzes = new GameObject("Iluminacao_Corredores_SciFi");
            containerLuzes.transform.SetParent(transform);
            containerLuzes.transform.localPosition = Vector3.zero;

            Vector3[] posLuzes = new Vector3[]
            {
                new Vector3(0f, 2.2f, 0f),       // Centro da Arena
                new Vector3(-7f, 2.2f, 7f),     // Quadrante NW
                new Vector3(7f, 2.2f, 7f),      // Quadrante NE
                new Vector3(-7f, 2.2f, -7f),    // Quadrante SW
                new Vector3(7f, 2.2f, -7f),     // Quadrante SE
                new Vector3(0f, 2.2f, 8.5f),    // Corredor Norte
                new Vector3(0f, 2.2f, -8.5f)    // Corredor Sul
            };

            Color[] coresLuzes = new Color[]
            {
                new Color(0f, 0.9f, 1f),        // Ciano Néon Centro
                new Color(0f, 1f, 0.65f),       // Esmeralda NW
                new Color(1f, 0.75f, 0.1f),     // Âmbar NE
                new Color(1f, 0.75f, 0.1f),     // Âmbar SW
                new Color(0f, 0.9f, 1f),        // Ciano SE
                new Color(0f, 0.9f, 1f),        // Ciano Norte
                new Color(1f, 0.2f, 0.4f)       // Rubi Alerta Sul
            };

            for (int i = 0; i < posLuzes.Length; i++)
            {
                GameObject pointLightObj = new GameObject($"Luz_Corredor_{i + 1}");
                pointLightObj.transform.SetParent(containerLuzes.transform);
                pointLightObj.transform.position = posLuzes[i];

                Light pLight = pointLightObj.AddComponent<Light>();
                pLight.type = LightType.Point;
                pLight.color = coresLuzes[i];
                pLight.range = 8.5f;
                pLight.intensity = 2.2f;
                pLight.shadows = LightShadows.Soft;
            }
        }

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientSkyColor = new Color(0.04f, 0.08f, 0.16f);
        RenderSettings.subtractiveShadowColor = new Color(0.02f, 0.04f, 0.08f);
    }

    /// <summary>
    /// Instancia as paredes perimetrais e corredores internos do labirinto 3D com BoxColliders e materiais polidos.
    /// </summary>
    
    /// <summary>
    /// Instancia Nodos Âncora de Relações (Object Properties) e Indivíduos Nomeados nos 4 cantos.
    /// (As antigas "Cerejas" foram substituídas pela mecânica espacial de LineRenderer em RelationBuilder.cs).
    /// </summary>
    public void InstanciarCerejasEPowerPellets()
    {
        // Garante que o Player possua o componente RelationBuilder ativo para traçar Object Properties (LineRenderer)
        GameObject playerObj = GameObject.FindWithTag("Player") ?? GameObject.Find("Visual_PacMan");
        if (playerObj != null && playerObj.GetComponent<RelationBuilder>() == null)
        {
            playerObj.AddComponent<RelationBuilder>();
        }

        // 2. POWER PELLETS / NAMED INDIVIDUALS: 4 Nodos prismáticos nos cantos do labirinto
        Vector3[] cantosPellets = new Vector3[]
        {
            new Vector3(-8.5f, 0.35f, 8.5f),
            new Vector3(8.5f, 0.35f, 8.5f),
            new Vector3(-8.5f, 0.35f, -8.5f),
            new Vector3(8.5f, 0.35f, -8.5f)
        };

        string[] nomesIndividuos = new string[]
        {
            "Human_Host_01",
            "SmallIntestine_Habitat_02",
            "FecalOral_Transmission_03",
            "AntiparasiticTreatment_04"
        };

        for (int i = 0; i < cantosPellets.Length; i++)
        {
            GameObject pelletObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pelletObj.name = $"NamedIndividual_Node_{i + 1}";
            pelletObj.transform.position = new Vector3(cantosPellets[i].x, 0.65f, cantosPellets[i].z);
            pelletObj.transform.localScale = new Vector3(0.42f, 0.58f, 0.42f);
            pelletObj.transform.localRotation = Quaternion.Euler(35f, 45f, 0f);
            pelletObj.transform.SetParent(transform);

            Renderer rendP = pelletObj.GetComponent<Renderer>();
            if (rendP != null)
            {
                rendP.material = CriarMaterialTranslúcidoNodo(new Color(0.65f, 0.35f, 1.0f, 1f), 0.62f);
            }

            BoxCollider boxCol = pelletObj.GetComponent<BoxCollider>();
            if (boxCol != null) Destroy(boxCol);
            SphereCollider colP = pelletObj.AddComponent<SphereCollider>();
            colP.isTrigger = true;
            colP.radius = 0.85f;

            var scriptP = pelletObj.AddComponent<PowerPelletIndividualSemantico>();
            scriptP.nomeIndividuo = nomesIndividuos[i];

            m_ObjetosInstanciados.Add(pelletObj);
        }

        Debug.Log("<color=#A855F7><b>[Mapeamento Ontológico]</b></color> RelationBuilder (LineRenderer) ativo e 4 Nodos de Indivíduos Nomeados instanciados no espaço.");
    }

    public void ConstruirLabirinto3D()
    {
        // Limpa com segurança qualquer container anterior de paredes
        if (m_ContainerParedes != null)
        {
            if (Application.isPlaying) Destroy(m_ContainerParedes);
            else DestroyImmediate(m_ContainerParedes);
            m_ContainerParedes = null;
        }

        Transform tAntigo = transform.Find("Labirinto_Paredes_3D");
        if (tAntigo != null)
        {
            if (Application.isPlaying) Destroy(tAntigo.gameObject);
            else DestroyImmediate(tAntigo.gameObject);
        }

        m_ContainerParedes = new GameObject("Labirinto_Paredes_3D");
        m_ContainerParedes.transform.SetParent(transform);
        m_ContainerParedes.transform.localPosition = Vector3.zero;

        Material matParede = materialParedes;
        if (matParede == null)
        {
            matParede = Resources.Load<Material>("Mat_Parede_Labirinto_AzulNeon");
#if UNITY_EDITOR
            if (matParede == null)
            {
                matParede = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_Parede_Labirinto_AzulNeon.mat");
            }
#endif
            if (matParede == null)
            {
                matParede = CriarMaterialParedeBiomedicaSciFi();
            }
        }

        float metade = tamanhoLabirinto / 2f;
        float yCentro = transform.position.y + (alturaParedes / 2f);

        // 1. Paredes Perimetrais Externas (Fecham o labirinto)
                // Parede Norte com Abertura para a Saída / Portal de Escape Ontológico
        float larguraAsa = (tamanhoLabirinto - 4.4f) / 2f;
        float offsetAsa = (metade + 2.2f) / 2f;
        CriarBlocoParede(m_ContainerParedes.transform, new Vector3(-offsetAsa, yCentro, metade), new Vector3(larguraAsa + espessuraParedes, alturaParedes, espessuraParedes), matParede, "Parede_Norte_Oeste");
        CriarBlocoParede(m_ContainerParedes.transform, new Vector3(offsetAsa, yCentro, metade), new Vector3(larguraAsa + espessuraParedes, alturaParedes, espessuraParedes), matParede, "Parede_Norte_Leste");

        // Constrói o Portal de Saída / Câmara de Escape Ontológico
        ConstruirPortalSaidaProcedural(m_ContainerParedes.transform, new Vector3(0f, yCentro, metade), matParede);
        CriarBlocoParede(m_ContainerParedes.transform, new Vector3(0f, yCentro, -metade), new Vector3(tamanhoLabirinto + espessuraParedes, alturaParedes, espessuraParedes), matParede, "Parede_Sul");
        CriarBlocoParede(m_ContainerParedes.transform, new Vector3(metade, yCentro, 0f), new Vector3(espessuraParedes, alturaParedes, tamanhoLabirinto), matParede, "Parede_Leste");
        CriarBlocoParede(m_ContainerParedes.transform, new Vector3(-metade, yCentro, 0f), new Vector3(espessuraParedes, alturaParedes, tamanhoLabirinto), matParede, "Parede_Oeste");

        // 2. Corredores Internos Clássicos do Pac-Man
        CriarBlocoParede(m_ContainerParedes.transform, new Vector3(-3.5f, yCentro, 0f), new Vector3(espessuraParedes, alturaParedes, 5.5f), matParede, "Parede_Centro_Esq");
        CriarBlocoParede(m_ContainerParedes.transform, new Vector3(3.5f, yCentro, 0f), new Vector3(espessuraParedes, alturaParedes, 5.5f), matParede, "Parede_Centro_Dir");
        CriarBlocoParede(m_ContainerParedes.transform, new Vector3(0f, yCentro, -2.75f), new Vector3(7f, alturaParedes, espessuraParedes), matParede, "Parede_Centro_Sul");

        // Blocos em L nos quadrantes
        CriarBlocoParede(m_ContainerParedes.transform, new Vector3(-7f, yCentro, 6f), new Vector3(6f, alturaParedes, espessuraParedes), matParede, "Parede_NW_H");
        CriarBlocoParede(m_ContainerParedes.transform, new Vector3(-7f, yCentro, 8.5f), new Vector3(espessuraParedes, alturaParedes, 5f), matParede, "Parede_NW_V");

        CriarBlocoParede(m_ContainerParedes.transform, new Vector3(7f, yCentro, 6f), new Vector3(6f, alturaParedes, espessuraParedes), matParede, "Parede_NE_H");
        CriarBlocoParede(m_ContainerParedes.transform, new Vector3(7f, yCentro, 8.5f), new Vector3(espessuraParedes, alturaParedes, 5f), matParede, "Parede_NE_V");

        CriarBlocoParede(m_ContainerParedes.transform, new Vector3(-7f, yCentro, -6f), new Vector3(6f, alturaParedes, espessuraParedes), matParede, "Parede_SW_H");
        CriarBlocoParede(m_ContainerParedes.transform, new Vector3(-7f, yCentro, -8.5f), new Vector3(espessuraParedes, alturaParedes, 5f), matParede, "Parede_SW_V");

        CriarBlocoParede(m_ContainerParedes.transform, new Vector3(7f, yCentro, -6f), new Vector3(6f, alturaParedes, espessuraParedes), matParede, "Parede_SE_H");
        CriarBlocoParede(m_ContainerParedes.transform, new Vector3(7f, yCentro, -8.5f), new Vector3(espessuraParedes, alturaParedes, 5f), matParede, "Parede_SE_V");

        // Divisórias dos corredores centrais Norte e Sul
        CriarBlocoParede(m_ContainerParedes.transform, new Vector3(0f, yCentro, 8.5f), new Vector3(6f, alturaParedes, espessuraParedes), matParede, "Parede_Norte_T");
        CriarBlocoParede(m_ContainerParedes.transform, new Vector3(0f, yCentro, -8.5f), new Vector3(6f, alturaParedes, espessuraParedes), matParede, "Parede_Sul_T");

        Debug.Log($"<color=#A855F7><b>[Topologia Semântica - Labirinto 3D]</b></color> {m_ContainerParedes.transform.childCount} blocos de parede instanciados e coloridos por Superclasse OWL (Parasitas = Roxo | Hospedeiros = Azul).");
    }

    private GameObject CriarBlocoParede(Transform pai, Vector3 pos, Vector3 escala, Material mat, string nome)
    {
        GameObject parede = GameObject.CreatePrimitive(PrimitiveType.Cube);
        parede.name = nome;
        parede.transform.SetParent(pai);
        parede.transform.position = pos;
        parede.transform.localScale = escala;
        parede.layer = LayerMask.NameToLayer("Default");

        // Topologia Semântica: determina a Superclasse OWL da região ("Parasitas" = Roxo; "Hospedeiros" = Azul)
        string superclasseRegiao = DeterminarSuperclassePorRegiao(pos, nome);
        Color corSuperclasse = ObterCorTopologiaSuperclasse(superclasseRegiao);
        Material matRegiao = usarTopologiaSemanticaPorSuperclasse
            ? CriarMaterialParedePorSuperclasse(superclasseRegiao)
            : mat;

        Renderer rend = parede.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.material = matRegiao;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            rend.receiveShadows = true;
        }

        parede.isStatic = true;
        // Colisor sólido estritamente não-trigger para bloquear 100% a travessia de jogadores e agentes
        BoxCollider col = parede.GetComponent<BoxCollider>();
        if (col != null)
        {
            col.isTrigger = false;
            col.size = Vector3.one;
            col.center = Vector3.zero;
        }

        // Adiciona friso superior luminoso (LED Trim Bar) com a cor da Superclasse da região
        GameObject trimLED = GameObject.CreatePrimitive(PrimitiveType.Cube);
        trimLED.name = $"{nome}_LEDTrim_{superclasseRegiao}";
        trimLED.transform.SetParent(parede.transform);
        trimLED.transform.localPosition = new Vector3(0f, 0.49f, 0f);
        trimLED.transform.localScale = new Vector3(1.02f, 0.04f, 1.02f);
        Collider colTrim = trimLED.GetComponent<Collider>();
        if (colTrim != null)
        {
            if (Application.isPlaying) Destroy(colTrim);
            else DestroyImmediate(colTrim);
        }

        Renderer rendLED = trimLED.GetComponent<Renderer>();
        if (rendLED != null)
        {
            rendLED.material = CriarMaterialNeonLED(corSuperclasse);
            rendLED.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rendLED.receiveShadows = false;
        }

        return parede;
    }

    // =========================================================================
    // 5. INTEGRAÇÃO COM VIRTONTO C# GRAPH (OWL / SWRL)
    // =========================================================================

    /// <summary>
    /// Gera o Grafo VirtOnto canônico com classes TBox, indivíduos ABox e axiomas ALCQ(D).
    /// </summary>
    public Graph ConstruirGrafoVirtOntoPadrao()
    {
        Graph graph;

        TextAsset owlAsset = arquivoOntologiaOwl ?? Resources.Load<TextAsset>("parasites.owl") ?? Resources.Load<TextAsset>("parasites");
        if (owlAsset != null && !string.IsNullOrEmpty(owlAsset.text))
        {
            graph = Graph.LoadFromOwlXml(owlAsset.text);
        }
        else
        {
            graph = new Graph();
            graph.IndexarTBoxPadrao();
        }

        graph.NomeOntologia = "ParasitologyOntology_VirtOnto_CIn";
        graph.VersaoAxiomas = "v2.0-Parasites-ALCQ";

        CenarioSemanticoDTO padrao = BackendOntologiaBridge.GerarCenarioOntologiaPadrao(quantidadeValidos, quantidadeInvalidos, raioEspalhamento, alturaGemas);
        graph.BuildFromCenarioDTO(padrao);

        return graph;
    }

    [ContextMenu("Pac-Man Semântico/Construir e Indexar Grafo VirtOnto (OWL-SWRL)")]
    public void MenuConstruirGrafoVirtOnto()
    {
        GrafoOntologico = ConstruirGrafoVirtOntoPadrao();
        m_TotalNosGrafo = GrafoOntologico.GetNodeCount();
        m_TotalArestasGrafo = GrafoOntologico.GetEdgeCount();

        Debug.Log($"<color=#00DDFF><b>[VirtOnto C#]</b></color> Grafo construído com sucesso! " +
                  $"Nós: {m_TotalNosGrafo}, Arestas: {m_TotalArestasGrafo}.");
    }

    [ContextMenu("Pac-Man Semântico/Avaliar Regras SWRL no Grafo")]
    public void MenuAvaliarRegrasSWRL()
    {
        if (GrafoOntologico == null || GrafoOntologico.GetNodeCount() == 0)
        {
            MenuConstruirGrafoVirtOnto();
        }

        GrafoOntologico.AvaliarRegrasSWRL(msg => Debug.Log($"<color=#9B59B6><b>[VirtOnto SWRL]</b></color> {msg}"));
    }

    [ContextMenu("Pac-Man Semântico/Sincronizar com Backend Java (Spring Boot)")]
    public void MenuSincronizarComBackendJava()
    {
        InicializarApiClient();
        if (apiClient != null)
        {
            apiClient.SincronizarCenarioComBackend();
        }
    }

    /// <summary>
    /// Localiza uma instância ativa por seu identificador único de ontologia,
    /// consultando prioritariamente o Grafo VirtOnto O(1) e depois a lista de instâncias.
    /// </summary>
    public IInstanciaSemantica ObterInstanciaPorId(string id)
    {
        Node node = GrafoOntologico != null ? GrafoOntologico.GetNode(id) : null;
        if (node != null && node.GameObjectInstance != null)
        {
            var inst = node.GameObjectInstance.GetComponent<IInstanciaSemantica>();
            if (inst != null) return inst;
        }
        return m_InstanciasSemanticas.Find(i => i.Id == id);
    }

    /// <summary>
    /// Retorna o nó ontológico correspondente no Grafo VirtOnto O(1).
    /// </summary>
    public Node ObterNoDoGrafo(string id) => GrafoOntologico?.GetNode(id);

    /// <summary>
    /// Retorna todos os nós individuais ativos indexados no Grafo VirtOnto.
    /// </summary>
    public IReadOnlyList<Node> ObterNosIndividuos() => GrafoOntologico?.GetNodesByType(NodeType.INDIVIDUAL);

    /// <summary>
    /// Retorna todas as relações (arestas) que partem do nó especificado.
    /// </summary>
    public IReadOnlyList<Edge> ObterRelacoesDoNo(string id) => GrafoOntologico?.GetOutgoingEdges(id);

    /// <summary>
    /// Limpa todas as instâncias ontológicas ativas no labirinto.
    /// </summary>
    [ContextMenu("Pac-Man Semântico/Limpar Cenário")]
    public void LimparCenarioExistente(bool destruirParedes = false)
    {
        if (destruirParedes && m_ContainerParedes != null)
        {
            if (Application.isPlaying) Destroy(m_ContainerParedes);
            else DestroyImmediate(m_ContainerParedes);
            m_ContainerParedes = null;
        }

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform filho = transform.GetChild(i);
            // PROTEÇÃO DO LABIRINTO: Nunca destrói as paredes do labirinto ao limpar gemas/fantasmas
            if (!destruirParedes && (filho.name == "Labirinto_Paredes_3D" || filho.gameObject == m_ContainerParedes))
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(filho.gameObject);
            }
            else
            {
                DestroyImmediate(filho.gameObject);
            }
        }

        m_ObjetosInstanciados.Clear();
        m_InstanciasSemanticas.Clear();
        m_PosicoesOcupadas.Clear();
        m_TotalInstanciasInstanciadas = 0;
        InstanciaSemanticaBase.instanciasValidasRestantes = 0;
        motorRaciocinio?.ResetarEstados();
    }

    private GameObject ConstruirPortalSaidaProcedural(Transform pai, Vector3 pos, Material matParede)
    {
        GameObject portalObj = new GameObject("Portal_Saida_Labirinto");
        portalObj.transform.SetParent(pai);
        portalObj.transform.position = pos;

        // 1. Pilares laterais luminosos
        GameObject pilarEsq = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pilarEsq.name = "Pilar_Esquerdo";
        pilarEsq.transform.SetParent(portalObj.transform, false);
        pilarEsq.transform.localPosition = new Vector3(-2.2f, 0f, 0f);
        pilarEsq.transform.localScale = new Vector3(0.6f, alturaParedes, 0.8f);
        if (matParede != null) pilarEsq.GetComponent<Renderer>().material = matParede;

        GameObject pilarDir = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pilarDir.name = "Pilar_Direito";
        pilarDir.transform.SetParent(portalObj.transform, false);
        pilarDir.transform.localPosition = new Vector3(2.2f, 0f, 0f);
        pilarDir.transform.localScale = new Vector3(0.6f, alturaParedes, 0.8f);
        if (matParede != null) pilarDir.GetComponent<Renderer>().material = matParede;

        // 2. Viga superior / Dintel do Portal com LED Trim
        GameObject vigaTopo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        vigaTopo.name = "Viga_Topo";
        vigaTopo.transform.SetParent(portalObj.transform, false);
        vigaTopo.transform.localPosition = new Vector3(0f, alturaParedes * 0.42f, 0f);
        vigaTopo.transform.localScale = new Vector3(4.8f, 0.8f, 0.8f);
        if (matParede != null) vigaTopo.GetComponent<Renderer>().material = matParede;

        // Trim LED luminoso de portal
        GameObject trimTopo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        trimTopo.name = "Trim_Neon_Portal";
        trimTopo.transform.SetParent(vigaTopo.transform, false);
        trimTopo.transform.localPosition = new Vector3(0f, -0.45f, 0f);
        trimTopo.transform.localScale = new Vector3(1.05f, 0.15f, 1.05f);
        Destroy(trimTopo.GetComponent<Collider>());
        trimTopo.GetComponent<Renderer>().material = CriarMaterialNeonLED(new Color(0f, 1f, 0.6f));

        // 3. Anel Holografico de Contencao (gira suavemente)
        GameObject anelContencao = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        anelContencao.name = "Anel_Contencao_Holografico";
        anelContencao.transform.SetParent(portalObj.transform, false);
        anelContencao.transform.localPosition = new Vector3(0f, 0f, 0f);
        anelContencao.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        anelContencao.transform.localScale = new Vector3(2.8f, 0.05f, 2.8f);
        Destroy(anelContencao.GetComponent<Collider>());
        anelContencao.GetComponent<Renderer>().material = CriarMaterialNeonLED(new Color(0.1f, 0.9f, 1f));

        // 4. Escudo de Quarentena / Barreira de Energia
        GameObject escudo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        escudo.name = "Escudo_Quarentena_Energia";
        escudo.transform.SetParent(portalObj.transform, false);
        escudo.transform.localPosition = new Vector3(0f, -0.4f, 0f);
        escudo.transform.localScale = new Vector3(3.6f, alturaParedes * 0.8f, 0.4f);
        BoxCollider colEscudo = escudo.GetComponent<BoxCollider>();
        if (colEscudo != null) colEscudo.isTrigger = true;
        Material matEscudo = CriarMaterialDinamico(new Color(1f, 0.35f, 0.05f, 0.5f), new Color(1f, 0.35f, 0.05f) * 1.5f, 0.9f);
        escudo.GetComponent<Renderer>().material = matEscudo;

        // 5. Luz indicadora do portal
        GameObject luzObj = new GameObject("Luz_Portal");
        luzObj.transform.SetParent(portalObj.transform, false);
        luzObj.transform.localPosition = new Vector3(0f, 0f, -0.5f);
        Light luzP = luzObj.AddComponent<Light>();
        luzP.type = LightType.Point;
        luzP.color = new Color(1f, 0.4f, 0.1f);
        luzP.range = 8.5f;
        luzP.intensity = 3.0f;

        // 6. Placa Sinalizadora Superior
        GameObject placa = GameObject.CreatePrimitive(PrimitiveType.Cube);
        placa.name = "Placa_Sinalizadora_SAIDA";
        placa.transform.SetParent(portalObj.transform, false);
        placa.transform.localPosition = new Vector3(0f, alturaParedes * 0.55f, -0.1f);
        placa.transform.localScale = new Vector3(3.5f, 0.6f, 0.15f);
        Destroy(placa.GetComponent<Collider>());
        placa.GetComponent<Renderer>().material = CriarMaterialNeonLED(new Color(0.05f, 1f, 0.5f));

        // 7. Componente PortalSaidaLabirinto
        PortalSaidaLabirinto portalComp = portalObj.AddComponent<PortalSaidaLabirinto>();
        portalComp.escudoEnergia = escudo;
        portalComp.arcoPortal = anelContencao;
        portalComp.luzPortal = luzP;
        portalComp.materialEscudo = matEscudo;

        BoxCollider triggerGeral = portalObj.AddComponent<BoxCollider>();
        triggerGeral.isTrigger = true;
        triggerGeral.size = new Vector3(3.8f, alturaParedes, 1.5f);
        triggerGeral.center = Vector3.zero;

        return portalObj;
    }

}
