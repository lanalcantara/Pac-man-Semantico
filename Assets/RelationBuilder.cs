using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using PacMan.Network;

/// <summary>
/// ============================================================================
/// INTERFACE ESPACIAL BIDIRECIONAL OWL - RelationBuilder (Object Properties)
/// ============================================================================
/// Substitui a mecânica arcade das antigas "Cerejas" por engenharia espacial
/// de Object Properties OWL usando LineRenderer e TextMeshPro 3D:
/// 1. O LineRenderer fica OCULTO/DESATIVADO por padrão durante toda a navegação.
/// 2. Somente quando o usuário INICIAR EXPLICITAMENTE a ação de conectar dois Nodos
///    (Clique esquerdo do mouse apontando para um Nodo, Gatilho VR ou Tecla C),
///    ativa o "Modo de Conexão" e estica a linha do Nodo A até o Player.
/// 3. Ao clicar/acionar o segundo Nodo (Nodo B), fixa a linha entre o Nodo A e o Nodo B,
///    instancia o TextMeshPro flutuante com a propriedade (ex: "has_host") e envia via WebSocket.
/// 4. Botão Direito do Mouse ou ESC cancela e oculta imediatamente o LineRenderer ativo.
/// ============================================================================
/// </summary>
[DisallowMultipleComponent]
public class RelationBuilder : MonoBehaviour
{
    public static RelationBuilder Instance { get; private set; }

    [Header("Configuração de Object Property OWL")]
    [Tooltip("Rótulo padrão da propriedade de objeto a ser asserida (ex: has_host, has_habitat, has_transmission).")]
    public string propriedadeAtiva = "has_host";

    [Tooltip("Catálogo de propriedades OWL alternáveis (Tecla Tab).")]
    public string[] catalogoPropriedades = new string[]
    {
        "has_host",
        "has_habitat",
        "has_transmission",
        "has_morphology"
    };

    [Header("Estado do Modo de Conexão")]
    public bool emModoConexao = false;
    public string nodoOrigemId;
    public string nodoOrigemClasse;
    public Vector3 nodoOrigemPosicao;

    [Header("Configuração Visual do LineRenderer")]
    public float larguraLinha = 0.055f;
    public Color corLinhaEmConstrucao = new Color(0.0f, 0.95f, 1.0f, 0.9f);
    public Color corLinhaFixada = new Color(0.66f, 0.33f, 0.97f, 0.95f);
    public float distanciaMaximaSelecaoNodo = 18.0f;

    private LineRenderer m_LinhaAtiva;
    private Material m_MaterialLinha;
    private int m_IndicePropriedade = 0;
    private IInstanciaSemantica m_NodoEmFoco;

    [Serializable]
    public class RelacaoCriada
    {
        public string sourceId;
        public string sourceClass;
        public string propertyName;
        public string targetId;
        public string targetClass;
        public GameObject lineObject;
        public GameObject labelObject;
    }

    private readonly List<RelacaoCriada> m_RelacoesFixadas = new List<RelacaoCriada>();
    public IReadOnlyList<RelacaoCriada> RelacoesFixadas => m_RelacoesFixadas;

    private void Awake()
    {
        Instance = this;
        emModoConexao = false;
        PrepararMaterialLinha();
        LimparLinhasTemporariasOrfas();
    }

    private void OnEnable()
    {
        InstanciaSemanticaBase.OnInstanciaFocada += AoFocarNodo;
        InstanciaSemanticaBase.OnInstanciaDesfocada += AoDesfocarNodo;
        OcultarLinhaEmConstrucao();
    }

    private void OnDisable()
    {
        InstanciaSemanticaBase.OnInstanciaFocada -= AoFocarNodo;
        InstanciaSemanticaBase.OnInstanciaDesfocada -= AoDesfocarNodo;
        CancelarModoConexao(silencioso: true);
    }

    private void LimparLinhasTemporariasOrfas()
    {
        // Remove qualquer objeto Linha_Conexao_Ativa_* residual para nunca atravessar a tela no início
        LineRenderer[] todasLinhas = FindObjectsByType<LineRenderer>(FindObjectsSortMode.None);
        for (int i = 0; i < todasLinhas.Length; i++)
        {
            if (todasLinhas[i] != null && todasLinhas[i].gameObject.name.StartsWith("Linha_Conexao_Ativa_", StringComparison.OrdinalIgnoreCase))
            {
                todasLinhas[i].enabled = false;
                if (Application.isPlaying) Destroy(todasLinhas[i].gameObject);
                else DestroyImmediate(todasLinhas[i].gameObject);
            }
        }
    }

    private void AoFocarNodo(IInstanciaSemantica instancia)
    {
        m_NodoEmFoco = instancia;
    }

    private void AoDesfocarNodo(IInstanciaSemantica instancia)
    {
        if (m_NodoEmFoco == instancia)
        {
            m_NodoEmFoco = null;
        }
    }

    private void Update()
    {
        // 1. Permite cancelar o Modo de Conexão a qualquer momento com Botão Direito ou ESC
        if (emModoConexao && (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace)))
        {
            CancelarModoConexao(silencioso: false);
            return;
        }

        // 2. Permite alternar a Object Property ativa com Tab
        if (Input.GetKeyDown(KeyCode.Tab) && catalogoPropriedades != null && catalogoPropriedades.Length > 0)
        {
            m_IndicePropriedade = (m_IndicePropriedade + 1) % catalogoPropriedades.Length;
            propriedadeAtiva = catalogoPropriedades[m_IndicePropriedade];
            if (SemanticHUDManager.Instancia != null)
            {
                SemanticHUDManager.Instancia.AdicionarLinhaLog($"[OWL] Object Property selecionada: {propriedadeAtiva}");
            }
        }

        // 3. Modo Criação (Do Zero In-Game) - Tecla N instancia um novo Nodo Ontológico no espaço (sem ativar linha sozinha)
        if (Input.GetKeyDown(KeyCode.N))
        {
            InstanciarNodoCriacaoInGame();
        }

        // 4. Ação EXPLÍCITA do usuário para iniciar ou concluir a conexão entre dois Nodos (Clique Esquerdo ou Tecla C)
        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.C))
        {
            TentarAcionarConexaoExplicita();
        }

        // 5. Atualiza a ponta final da linha ativa SOMENTE enquanto emModoConexao for true; caso contrário mantém oculta
        if (emModoConexao && m_LinhaAtiva != null)
        {
            if (!m_LinhaAtiva.gameObject.activeSelf) m_LinhaAtiva.gameObject.SetActive(true);
            m_LinhaAtiva.enabled = true;

            Vector3 posDestino = ObterPosicaoPontaConexao();
            m_LinhaAtiva.SetPosition(0, nodoOrigemPosicao);
            m_LinhaAtiva.SetPosition(1, posDestino);
        }
        else
        {
            OcultarLinhaEmConstrucao();
        }

        // 6. Faz os rótulos TextMeshPro 3D das relações já fixadas olharem para a câmera principal (Billboard legível)
        if (Camera.main != null && m_RelacoesFixadas.Count > 0)
        {
            Transform camTr = Camera.main.transform;
            for (int i = 0; i < m_RelacoesFixadas.Count; i++)
            {
                var rel = m_RelacoesFixadas[i];
                if (rel != null && rel.labelObject != null)
                {
                    Vector3 dir = rel.labelObject.transform.position - camTr.position;
                    if (dir.sqrMagnitude > 0.001f)
                    {
                        rel.labelObject.transform.rotation = Quaternion.LookRotation(dir);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Executa Raycast a partir do cursor do mouse (ou centro da câmera/foco XR) para detectar
    /// o Nodo Ontológico que o usuário clicou explicitamente para conectar.
    /// </summary>
    private void TentarAcionarConexaoExplicita()
    {
        IInstanciaSemantica nodoAlvo = DetectarNodoSobCliqueOuMira();
        if (nodoAlvo != null)
        {
            RegistrarInteracaoNodo(
                nodoAlvo.Id,
                nodoAlvo.NomeClasse,
                nodoAlvo.Posicao + Vector3.up * 0.45f,
                nodoAlvo.EhValida);
        }
        else if (emModoConexao && Input.GetMouseButtonDown(0))
        {
            // Se clicou no vazio enquanto estava em modo de conexão, cancela e esconde a linha para não poluir a tela
            CancelarModoConexao(silencioso: false);
        }
    }

    private IInstanciaSemantica DetectarNodoSobCliqueOuMira()
    {
        Camera cam = Camera.main;
        if (cam != null)
        {
            // 1. Tenta Raycast pela posição do cursor do Mouse na tela
            Ray raioMouse = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(raioMouse, out RaycastHit hitMouse, distanciaMaximaSelecaoNodo, ~0, QueryTriggerInteraction.Collide))
            {
                IInstanciaSemantica instMouse = hitMouse.collider.GetComponentInParent<IInstanciaSemantica>()
                                                ?? hitMouse.collider.GetComponent<IInstanciaSemantica>();
                if (instMouse != null) return instMouse;
            }

            // 2. Se acionado por tecla C, tenta Raycast pelo centro da câmera
            if (Input.GetKeyDown(KeyCode.C))
            {
                Ray raioCentro = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                if (Physics.Raycast(raioCentro, out RaycastHit hitCentro, distanciaMaximaSelecaoNodo, ~0, QueryTriggerInteraction.Collide))
                {
                    IInstanciaSemantica instCentro = hitCentro.collider.GetComponentInParent<IInstanciaSemantica>()
                                                     ?? hitCentro.collider.GetComponent<IInstanciaSemantica>();
                    if (instCentro != null) return instCentro;
                }
            }
        }

        // 3. Fallback: Nodo atualmente sob foco do apontador XR (somente quando acionado por clique/gatilho explícito)
        if (m_NodoEmFoco != null)
        {
            return m_NodoEmFoco;
        }

        return null;
    }

    private Vector3 ObterPosicaoPontaConexao()
    {
        Camera cam = Camera.main;
        if (cam != null)
        {
            Ray raioMouse = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(raioMouse, out RaycastHit hit, distanciaMaximaSelecaoNodo, ~0, QueryTriggerInteraction.Collide))
            {
                IInstanciaSemantica inst = hit.collider.GetComponentInParent<IInstanciaSemantica>()
                                           ?? hit.collider.GetComponent<IInstanciaSemantica>();
                if (inst != null)
                {
                    return inst.Posicao + Vector3.up * 0.45f;
                }
            }
        }

        return transform.position + Vector3.up * 0.45f;
    }

    /// <summary>
    /// Processa a ação explícita de conexão com um Nodo A (inicia Modo de Conexão)
    /// ou Nodo B (fixa a linha, cria rótulo TMP e envia tripla via WebSocket).
    /// </summary>
    public void RegistrarInteracaoNodo(string nodeId, string nodeClass, Vector3 nodePos, bool ehValido = true)
    {
        if (string.IsNullOrEmpty(nodeId)) return;

        if (!emModoConexao)
        {
            IniciarModoConexao(nodeId, nodeClass, nodePos);
        }
        else
        {
            // Se clicar no mesmo nodo de origem novamente, cancela e oculta a linha
            if (string.Equals(nodoOrigemId, nodeId, StringComparison.OrdinalIgnoreCase))
            {
                CancelarModoConexao(silencioso: false);
                return;
            }

            FinalizarConexao(nodeId, nodeClass, nodePos, ehValido);
        }
    }

    public void IniciarModoConexao(string sourceId, string sourceClass, Vector3 sourcePos)
    {
        emModoConexao = true;
        nodoOrigemId = sourceId;
        nodoOrigemClasse = sourceClass;
        nodoOrigemPosicao = sourcePos;

        if (m_LinhaAtiva == null)
        {
            GameObject lineObj = new GameObject($"Linha_Conexao_Ativa_{sourceId}", typeof(LineRenderer));
            m_LinhaAtiva = ConfigurarLineRenderer(lineObj, corLinhaEmConstrucao);
        }

        m_LinhaAtiva.gameObject.name = $"Linha_Conexao_Ativa_{sourceId}";
        m_LinhaAtiva.gameObject.SetActive(true);
        m_LinhaAtiva.enabled = true;
        m_LinhaAtiva.positionCount = 2;
        m_LinhaAtiva.SetPosition(0, nodoOrigemPosicao);
        m_LinhaAtiva.SetPosition(1, ObterPosicaoPontaConexao());

        if (SemanticHUDManager.Instancia != null)
        {
            SemanticHUDManager.Instancia.AdicionarLinhaLog($"[CONEXÃO OWL] Origem '{sourceClass}' selecionada. Clique no 2º Nodo para '{propriedadeAtiva}' (Botão Direito cancela).");
        }

        Debug.Log($"<color=#00E5FF><b>[RelationBuilder]</b></color> Modo de Conexão ATIVO: Origem '{sourceClass}' ({sourceId}) -> aguardando clique no Nodo B para propriedade '{propriedadeAtiva}'...");
    }

    public void CancelarModoConexao(bool silencioso = false)
    {
        bool estavaAtivo = emModoConexao;
        emModoConexao = false;
        nodoOrigemId = null;
        nodoOrigemClasse = null;

        OcultarLinhaEmConstrucao();

        if (estavaAtivo && !silencioso && SemanticHUDManager.Instancia != null)
        {
            SemanticHUDManager.Instancia.AdicionarLinhaLog("[CONEXÃO OWL] Modo de conexão cancelado.");
        }
    }

    private void OcultarLinhaEmConstrucao()
    {
        if (m_LinhaAtiva != null)
        {
            m_LinhaAtiva.enabled = false;
            if (m_LinhaAtiva.gameObject.activeSelf)
            {
                m_LinhaAtiva.gameObject.SetActive(false);
            }
        }
    }

    public void FinalizarConexao(string targetId, string targetClass, Vector3 targetPos, bool consistente = true)
    {
        if (!emModoConexao) return;

        emModoConexao = false;

        // 1. Cria um novo objeto dedicado para a linha fixada e oculta o LineRenderer de construção
        OcultarLinhaEmConstrucao();

        GameObject linhaFixaObj = new GameObject($"ObjectProperty_{nodoOrigemId}_{propriedadeAtiva}_{targetId}", typeof(LineRenderer));
        LineRenderer lrFixo = ConfigurarLineRenderer(linhaFixaObj, corLinhaFixada);
        lrFixo.enabled = true;

        Color corFinal = consistente ? corLinhaFixada : new Color(1.0f, 0.15f, 0.25f, 0.95f);
        lrFixo.startColor = corFinal;
        lrFixo.endColor = corFinal;
        if (lrFixo.material != null && lrFixo.material.HasProperty("_BaseColor"))
        {
            lrFixo.material.SetColor("_BaseColor", corFinal);
        }
        if (lrFixo.material != null && lrFixo.material.HasProperty("_Color"))
        {
            lrFixo.material.SetColor("_Color", corFinal);
        }

        lrFixo.positionCount = 2;
        lrFixo.SetPosition(0, nodoOrigemPosicao);
        lrFixo.SetPosition(1, targetPos);

        // 2. Instancia um TextMeshPro flutuante no meio exato da linha com o rótulo da propriedade (ex: has_host)
        Vector3 pontoMedio = (nodoOrigemPosicao + targetPos) * 0.5f + Vector3.up * 0.32f;
        GameObject labelObj = InstanciarRotuloFlutuanteTMP(linhaFixaObj.transform, pontoMedio, propriedadeAtiva, corFinal);

        m_RelacoesFixadas.Add(new RelacaoCriada
        {
            sourceId = nodoOrigemId,
            sourceClass = nodoOrigemClasse,
            propertyName = propriedadeAtiva,
            targetId = targetId,
            targetClass = targetClass,
            lineObject = linhaFixaObj,
            labelObject = labelObj
        });

        // 3. Envia a tripla (Nodo A -> has_host -> Nodo B) via WebSocket (porta 8085)
        var ws = ProtegeWebSocketBridge.Instance ?? FindFirstObjectByType<ProtegeWebSocketBridge>();
        if (ws != null)
        {
            ws.EnviarRelacaoObjectProperty(nodoOrigemId, nodoOrigemClasse, propriedadeAtiva, targetId, targetClass, consistente);
        }
        else if (SemanticHUDManager.Instancia != null)
        {
            SemanticHUDManager.Instancia.DefinirAxiomasInseridos(SemanticHUDManager.Instancia.axiomasInseridos + 1);
            SemanticHUDManager.Instancia.AdicionarLinhaLog($"[+] Tripla OWL: {nodoOrigemClasse} -> {propriedadeAtiva} -> {targetClass}");
        }

        // 4. Registra também a aresta no Grafo Ontológico em memória
        var ger = FindFirstObjectByType<GerenciadorCenarioSemantico>();
        if (ger != null && ger.GrafoOntologico != null)
        {
            string edgeId = $"edge_{nodoOrigemId}_{propriedadeAtiva}_{targetId}_{Time.frameCount}";
            ger.GrafoOntologico.AddEdge(new VirtOnto.Model.Edge(edgeId, nodoOrigemId, targetId, propriedadeAtiva));
        }

        Debug.Log($"<color=#A855F7><b>[RelationBuilder - Tripla OWL Fixada]</b></color> ({nodoOrigemClasse} [{nodoOrigemId}]) --[{propriedadeAtiva}]--> ({targetClass} [{targetId}]) enviada via WebSocket!");
    }

    private LineRenderer ConfigurarLineRenderer(GameObject host, Color cor)
    {
        LineRenderer lr = host.GetComponent<LineRenderer>();
        if (lr == null)
        {
            lr = host.AddComponent<LineRenderer>();
        }
        lr.useWorldSpace = true;
        lr.startWidth = larguraLinha;
        lr.endWidth = larguraLinha;
        lr.numCapVertices = 4;
        lr.positionCount = 2;
        lr.enabled = false;

        PrepararMaterialLinha();
        Material matInst = new Material(m_MaterialLinha);
        if (matInst.HasProperty("_BaseColor")) matInst.SetColor("_BaseColor", cor);
        if (matInst.HasProperty("_Color")) matInst.SetColor("_Color", cor);
        if (matInst.HasProperty("_EmissionColor"))
        {
            matInst.EnableKeyword("_EMISSION");
            matInst.SetColor("_EmissionColor", cor * 1.4f);
        }

        lr.material = matInst;
        lr.startColor = cor;
        lr.endColor = cor;
        return lr;
    }

    private GameObject InstanciarRotuloFlutuanteTMP(Transform pai, Vector3 posicaoMundo, string textoPropriedade, Color corDestaque)
    {
        GameObject labelObj = new GameObject($"TMP_Label_{textoPropriedade}", typeof(RectTransform));
        labelObj.transform.SetParent(pai, true);
        labelObj.transform.position = posicaoMundo;

        TextMeshPro tmp = labelObj.AddComponent<TextMeshPro>();
        tmp.text = textoPropriedade;
        tmp.fontSize = 2.6f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;
        tmp.outlineColor = new Color32(0, 0, 0, 255);
        tmp.outlineWidth = 0.20f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;

        if (tmp.fontSharedMaterial != null)
        {
            Material matTexto = new Material(tmp.fontSharedMaterial);
            if (matTexto.HasProperty("_FaceColor")) matTexto.SetColor("_FaceColor", Color.white);
            if (matTexto.HasProperty("_OutlineColor")) matTexto.SetColor("_OutlineColor", Color.black);
            if (matTexto.HasProperty("_OutlineWidth")) matTexto.SetFloat("_OutlineWidth", 0.20f);
            tmp.fontMaterial = matTexto;
        }

        RectTransform rt = labelObj.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(3.5f, 0.8f);
        }

        return labelObj;
    }

    private int m_ContadorNodosCriadosInGame = 0;

    /// <summary>
    /// Seção 2 da Especificação Mestra: Modo Criação (Do Zero In-Game).
    /// Permite ao usuário soltar um novo Nodo Ontológico (Classe/Indivíduo) no espaço à sua frente.
    /// A linha de conexão só será ativada quando o usuário clicar explicitamente no nodo.
    /// </summary>
    public GameObject InstanciarNodoCriacaoInGame(string nomeClasseCustom = null)
    {
        m_ContadorNodosCriadosInGame++;
        string[] classesSugestao = { "Parasite", "Host", "Nematoda", "Platyhelminthes", "Human", "SmallIntestine" };
        string nomeClasse = !string.IsNullOrEmpty(nomeClasseCustom)
            ? nomeClasseCustom
            : classesSugestao[(m_ContadorNodosCriadosInGame - 1) % classesSugestao.Length];
        string idNodo = $"node_ingame_{nomeClasse}_{m_ContadorNodosCriadosInGame:D2}";

        Vector3 dirFrente = transform.forward.sqrMagnitude > 0.01f ? transform.forward.normalized : Vector3.forward;
        Vector3 posSpawn = transform.position + dirFrente * 1.6f;
        posSpawn.y = 0.45f;

        GameObject nodoObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        nodoObj.name = $"OntologyNode_{idNodo}";
        nodoObj.transform.position = posSpawn;
        nodoObj.transform.localScale = new Vector3(0.42f, 0.42f, 0.42f);
        nodoObj.transform.rotation = Quaternion.Euler(35f, 45f, 0f);

        var ger = FindFirstObjectByType<GerenciadorCenarioSemantico>();
        Renderer rend = nodoObj.GetComponent<Renderer>();
        if (rend != null && ger != null)
        {
            rend.material = ger.CriarMaterialTranslúcidoNodo(new Color(0.0f, 0.85f, 1.0f, 1f), 0.65f);
        }

        GerenciadorCenarioSemantico.AnexarLuzPontualDestaque(nodoObj, new Color(0.0f, 0.85f, 1.0f, 1f), 2.5f, 1.25f);

        BoxCollider boxCol = nodoObj.GetComponent<BoxCollider>();
        if (boxCol != null) boxCol.isTrigger = true;

        GerenciadorInstancia compNodo = nodoObj.AddComponent<GerenciadorInstancia>();
        compNodo.ConfigurarComDados(new DadoInstanciaSemantica
        {
            id = idNodo,
            classeOntologica = nomeClasse,
            expressaoDL = $"{nomeClasse}({idNodo})",
            ehValida = true,
            posicao = posSpawn,
            valorPontuacao = 50,
            descricaoSemantica = $"Nodo ontológico criado in-game ({nomeClasse})"
        });

        if (SemanticHUDManager.Instancia != null)
        {
            SemanticHUDManager.Instancia.AdicionarLinhaLog($"[CRIAÇÃO IN-GAME] Nodo '{nomeClasse}' ({idNodo}) posicionado. Clique nele para conectar.");
        }

        return nodoObj;
    }

    private void PrepararMaterialLinha()
    {
        if (m_MaterialLinha == null)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            m_MaterialLinha = new Material(s);
        }
    }
}
