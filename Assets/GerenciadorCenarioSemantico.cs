using System;
using System.Collections.Generic;
using UnityEngine;
using VirtOnto.Model;

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
public class GerenciadorCenarioSemantico : MonoBehaviour
{
    public enum OrigemDadosOntologia
    {
        ProceduralInterno,
        BackendCppNativo,
        ArquivoJson,
        GrafoOntologicoVirtOnto,
        InjecaoExternaDinamica
    }

    [Header("Identidade do Projeto")]
    [Tooltip("Nome oficial do sistema.")]
    public string nomeProjeto = "Pac-Man Semântico (Semantic Pac-Man) - CIn-UFPE";

    [Header("Origem dos Dados Ontológicos")]
    [Tooltip("Define a fonte das classes e instâncias da ontologia.")]
    public OrigemDadosOntologia origemDados = OrigemDadosOntologia.ProceduralInterno;

    [Tooltip("Arquivo JSON de ontologia exportada (utilizado quando origem for ArquivoJson).")]
    public TextAsset arquivoOntologiaJson;

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

    [Tooltip("Mapeamentos personalizados entre classes da ontologia C++/OWL e modelos 3D específicos.")]
    public List<MapeamentoClassePrefab> mapeamentoClassesCustomizadas = new List<MapeamentoClassePrefab>();

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

    private void Awake()
    {
        CarregarPrefabsAutomaticamente();
        LocalizarPlanoChao();
    }

    private void Start()
    {
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
        if (planoChao != null) return;

        GameObject planeObj = GameObject.Find("Plane") ?? GameObject.Find("Floor") ?? GameObject.Find("Chao") ?? GameObject.Find("Piso");
        if (planeObj != null)
        {
            planoChao = planeObj.transform;
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

        CenarioSemanticoDTO cenario = null;

        switch (origemDados)
        {
            case OrigemDadosOntologia.GrafoOntologicoVirtOnto:
                Debug.Log("<color=#00DDFF><b>[Pac-Man Semântico - VirtOnto C#]</b></color> Construindo e consumindo Grafo Ontológico VirtOnto...");
                GrafoOntologico = ConstruirGrafoVirtOntoPadrao();
                cenario = GrafoOntologico.ToCenarioDTO();
                break;

            case OrigemDadosOntologia.BackendCppNativo:
                Debug.Log("<color=#00DDFF><b>[Pac-Man Semântico - Backend C++]</b></color> Solicitando inferência de Description Logic à DLL nativa...");
                cenario = BackendOntologiaBridge.ConsultarBackendCpp(quantidadeValidos, quantidadeInvalidos, raioEspalhamento, alturaGemas);
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
                    cenario = BackendOntologiaBridge.GerarMockOntologiaCpp(quantidadeValidos, quantidadeInvalidos, raioEspalhamento, alturaGemas);
                }
                break;

            case OrigemDadosOntologia.InjecaoExternaDinamica:
                Debug.Log("[Pac-Man Semântico] Modo de Injeção Dinâmica Ativo. Aguardando chamada de CarregarCenarioDeDados().");
                return;

            case OrigemDadosOntologia.ProceduralInterno:
            default:
                Debug.Log("<color=#00FF99><b>[Pac-Man Semântico - Procedural]</b></color> Gerando instâncias ontológicas Description Logic...");
                cenario = BackendOntologiaBridge.GerarMockOntologiaCpp(quantidadeValidos, quantidadeInvalidos, raioEspalhamento, alturaGemas);
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

        m_TotalInstanciasInstanciadas = m_ObjetosInstanciados.Count;

        // 3. Avaliação inicial de regras SWRL sobre o grafo instanciado
        GrafoOntologico.AvaliarRegrasSWRL(msg => Debug.Log($"<color=#9B59B6><b>[VirtOnto SWRL]</b></color> {msg}"));

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

        // 2. Determina a coordenada horizontal (X, Z)
        Vector3 posicaoPlana = dado.posicao;
        if (posicaoPlana == Vector3.zero || m_PosicoesOcupadas.Count == 0)
        {
            posicaoPlana = ObterPosicaoValidaNoChao(dado.ehValida);
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
        float alturaOffset = dado.ehValida ? alturaGemas : alturaObstaculos;
        float yFinal = chaoY + distanciaPivotParaBase + (assentarPerfeitamenteNoChao ? 0f : alturaOffset);

        Vector3 posFinalAssentada = new Vector3(posicaoPlana.x, yFinal, posicaoPlana.z);
        instanciaObj.transform.position = posFinalAssentada;

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

        if (Physics.Raycast(origemRaio, Vector3.down, out RaycastHit hit, 100f, ~0, QueryTriggerInteraction.Ignore))
        {
            // Ignora se o colisor for um objeto gerado ou trigger
            if (hit.collider != null && !hit.collider.isTrigger)
            {
                return hit.point.y;
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
        if (renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                if (renderers[i].enabled)
                {
                    b.Encapsulate(renderers[i].bounds);
                }
            }
            return obj.transform.position.y - b.min.y;
        }

        Collider[] colliders = obj.GetComponentsInChildren<Collider>();
        if (colliders.Length > 0)
        {
            Bounds b = colliders[0].bounds;
            for (int i = 1; i < colliders.Length; i++)
            {
                b.Encapsulate(colliders[i].bounds);
            }
            return obj.transform.position.y - b.min.y;
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

    private Vector3 ObterPosicaoValidaNoChao(bool ehGemaValida)
    {
        float altura = transform.position.y + (ehGemaValida ? alturaGemas : alturaObstaculos);
        Vector3 posicaoCandidata = Vector3.zero;
        int tentativasMaximas = 40;

        for (int i = 0; i < tentativasMaximas; i++)
        {
            Vector2 circulo = UnityEngine.Random.insideUnitCircle * raioEspalhamento;

            if (circulo.magnitude < 2.5f)
            {
                circulo = circulo.normalized * 2.8f;
            }

            posicaoCandidata = new Vector3(
                transform.position.x + circulo.x,
                altura,
                transform.position.z + circulo.y
            );

            bool posicaoLivre = true;
            foreach (var pos in m_PosicoesOcupadas)
            {
                if (Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(posicaoCandidata.x, posicaoCandidata.z)) < distanciaMinimaEntreInstancias)
                {
                    posicaoLivre = false;
                    break;
                }
            }

            if (posicaoLivre)
            {
                return posicaoCandidata;
            }
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
        string[] classesGemas = { "GemaDiamante", "MineralMagico", "EntidadeConforme", "CristalPuro", "StandardGem", "SpecialGem", "PacDot" };
        string[] classesFantasmas = { "FantasmaCorrompido", "ObstaculoSemantico", "RestricaoViolada", "AntiMateria", "AxiomaInconsistente", "AggressiveGhost", "PatrolGhost", "VulnerableGhost", "Ghost", "Obstacle", "Fantasma_Blinky" };

        foreach (var cls in classesGemas)
        {
            RegistrarMapeamentoSeAusente(cls, prefabGemaValida);
        }

        foreach (var cls in classesFantasmas)
        {
            RegistrarMapeamentoSeAusente(cls, prefabObstaculoInvalido);
        }

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
        // Garante a presença de colisor Trigger para interação espacial XR e física
        Collider[] colisores = obj.GetComponentsInChildren<Collider>();
        if (colisores.Length == 0)
        {
            SphereCollider sc = obj.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = 0.5f;
        }
        else
        {
            foreach (var c in colisores)
            {
                c.isTrigger = true;
            }
        }

        // Vincula a implementação de IInstanciaSemantica
        IInstanciaSemantica instSemantica = obj.GetComponent<IInstanciaSemantica>();
        if (instSemantica == null)
        {
            GerenciadorInstancia ger = obj.AddComponent<GerenciadorInstancia>();
            instSemantica = ger;
        }

        instSemantica.InicializarSemantica(dado);

        if (dado.ehValida && obj.GetComponent<RotacaoFlutuante>() == null)
        {
            RotacaoFlutuante rot = obj.AddComponent<RotacaoFlutuante>();
            rot.permitirFlutuacao = false;
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
        GameObject pilula = new GameObject("PacDot_Procedural");
        pilula.transform.position = posicao;
        pilula.transform.SetParent(transform);

        Color corBase = (dado != null && dado.corDestaque != Color.clear) ? dado.corDestaque : new Color(1f, 0.88f, 0.2f);
        Material matGema = CriarMaterialDinamico(corBase, corBase * 0.75f, 0.95f);

        // Núcleo Esférico Brilhante
        GameObject esfera = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        esfera.name = "Nucleo_PacDot";
        esfera.transform.SetParent(pilula.transform, false);
        esfera.transform.localScale = new Vector3(0.55f, 0.55f, 0.55f);
        Destroy(esfera.GetComponent<Collider>());
        esfera.GetComponent<Renderer>().material = matGema;

        // Halo decorativo
        GameObject halo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        halo.name = "Halo_Aura";
        halo.transform.SetParent(pilula.transform, false);
        halo.transform.localScale = new Vector3(0.7f, 0.02f, 0.7f);
        Destroy(halo.GetComponent<Collider>());
        halo.GetComponent<Renderer>().material = matGema;

        RotacaoFlutuante rot = pilula.AddComponent<RotacaoFlutuante>();
        rot.permitirFlutuacao = false;

        return pilula;
    }

    private GameObject CriarObstaculoProcedural(Vector3 posicao, DadoInstanciaSemantica dado = null)
    {
        GameObject fantasma = new GameObject("Fantasma_Procedural");
        fantasma.transform.position = posicao;
        fantasma.transform.SetParent(transform);

        Color corFantasma = (dado != null && dado.corDestaque != Color.clear) ? dado.corDestaque : new Color(1f, 0.12f, 0.2f);
        Material matCorpo = CriarMaterialDinamico(corFantasma, corFantasma * 0.6f, 0.9f);

        // Cabeça (Cúpula Superior)
        GameObject cabeca = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        cabeca.name = "Cabeca_Cupula";
        cabeca.transform.SetParent(fantasma.transform, false);
        cabeca.transform.localPosition = new Vector3(0f, 0.35f, 0f);
        cabeca.transform.localScale = new Vector3(0.85f, 0.85f, 0.85f);
        Destroy(cabeca.GetComponent<Collider>());
        cabeca.GetComponent<Renderer>().material = matCorpo;

        // Tronco (Saia Cilíndrica)
        GameObject corpo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        corpo.name = "Corpo_Saia";
        corpo.transform.SetParent(fantasma.transform, false);
        corpo.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        corpo.transform.localScale = new Vector3(0.85f, 0.4f, 0.85f);
        Destroy(corpo.GetComponent<Collider>());
        corpo.GetComponent<Renderer>().material = matCorpo;

        // Franjas onduladas da saia
        for (int i = 0; i < 3; i++)
        {
            GameObject franja = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            franja.name = $"Franja_{i + 1}";
            franja.transform.SetParent(fantasma.transform, false);
            float offsetX = (i - 1) * 0.28f;
            franja.transform.localPosition = new Vector3(offsetX, -0.15f, 0f);
            franja.transform.localScale = new Vector3(0.3f, 0.25f, 0.3f);
            Destroy(franja.GetComponent<Collider>());
            franja.GetComponent<Renderer>().material = matCorpo;
        }

        // Olhos Expressivos
        Shader shaderUnlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        Material matOlho = new Material(shaderUnlit) { color = Color.white };
        Material matPupila = new Material(shaderUnlit) { color = new Color(0.05f, 0.2f, 0.95f) };

        // Olho Esquerdo
        GameObject olhoEsq = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        olhoEsq.name = "OlhoEsquerdo";
        olhoEsq.transform.SetParent(fantasma.transform, false);
        olhoEsq.transform.localPosition = new Vector3(-0.18f, 0.3f, 0.35f);
        olhoEsq.transform.localScale = new Vector3(0.22f, 0.28f, 0.15f);
        Destroy(olhoEsq.GetComponent<Collider>());
        olhoEsq.GetComponent<Renderer>().material = matOlho;

        GameObject pupilaEsq = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        pupilaEsq.name = "PupilaEsquerda";
        pupilaEsq.transform.SetParent(olhoEsq.transform, false);
        pupilaEsq.transform.localPosition = new Vector3(0f, 0f, 0.04f);
        pupilaEsq.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
        Destroy(pupilaEsq.GetComponent<Collider>());
        pupilaEsq.GetComponent<Renderer>().material = matPupila;

        // Olho Direito
        GameObject olhoDir = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        olhoDir.name = "OlhoDireito";
        olhoDir.transform.SetParent(fantasma.transform, false);
        olhoDir.transform.localPosition = new Vector3(0.18f, 0.3f, 0.35f);
        olhoDir.transform.localScale = new Vector3(0.22f, 0.28f, 0.15f);
        Destroy(olhoDir.GetComponent<Collider>());
        olhoDir.GetComponent<Renderer>().material = matOlho;

        GameObject pupilaDir = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        pupilaDir.name = "PupilaDireita";
        pupilaDir.transform.SetParent(olhoDir.transform, false);
        pupilaDir.transform.localPosition = new Vector3(0f, 0f, 0.04f);
        pupilaDir.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
        Destroy(pupilaDir.GetComponent<Collider>());
        pupilaDir.GetComponent<Renderer>().material = matPupila;

        AnimacaoFantasma anim = fantasma.AddComponent<AnimacaoFantasma>();
        anim.pupilaEsquerda = pupilaEsq.transform;
        anim.pupilaDireita = pupilaDir.transform;
        anim.permitirFlutuacao = false;

        return fantasma;
    }

    private GameObject CriarPacManProcedural(Vector3 posicao, DadoInstanciaSemantica dado = null)
    {
        GameObject pacObj = new GameObject("PacMan_Procedural");
        pacObj.transform.position = posicao;
        pacObj.transform.SetParent(transform);

        Color corAmarelo = new Color(1f, 0.92f, 0.05f);
        Material matPac = CriarMaterialDinamico(corAmarelo, corAmarelo * 0.7f, 0.95f);

        GameObject corpo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        corpo.name = "Corpo_PacMan";
        corpo.transform.SetParent(pacObj.transform, false);
        corpo.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);
        Destroy(corpo.GetComponent<Collider>());
        corpo.GetComponent<Renderer>().material = matPac;

        return pacObj;
    }

    private void PrepararMateriaisProcedurais()
    {
        if (m_MaterialGemaProcedural == null)
        {
            m_MaterialGemaProcedural = CriarMaterialDinamico(
                corBase: new Color(1f, 0.88f, 0.2f),
                corEmissao: new Color(0.85f, 0.65f, 0.1f),
                suavidade: 0.95f
            );
        }

        if (m_MaterialObstaculoProcedural == null)
        {
            m_MaterialObstaculoProcedural = CriarMaterialDinamico(
                corBase: new Color(1f, 0.12f, 0.2f),
                corEmissao: new Color(0.85f, 0.05f, 0.1f),
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

    // =========================================================================
    // 5. INTEGRAÇÃO COM VIRTONTO C# GRAPH (OWL / SWRL)
    // =========================================================================

    /// <summary>
    /// Gera o Grafo VirtOnto canônico com classes TBox, indivíduos ABox e axiomas ALCQ(D).
    /// </summary>
    public Graph ConstruirGrafoVirtOntoPadrao()
    {
        Graph graph = new Graph
        {
            NomeOntologia = "OntologiaPacManSemantico_VirtOnto_CIn",
            VersaoAxiomas = "v2.0-VirtOnto-ALCQ"
        };

        graph.IndexarTBoxPadrao();

        CenarioSemanticoDTO mock = BackendOntologiaBridge.GerarMockOntologiaCpp(quantidadeValidos, quantidadeInvalidos, raioEspalhamento, alturaGemas);
        graph.BuildFromCenarioDTO(mock);

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
    public void LimparCenarioExistente()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform filho = transform.GetChild(i);
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
    }
}
