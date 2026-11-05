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
/// 1. Ao interagir com um Nodo A, ativa o "Modo de Conexão" e desenha uma linha
///    física no espaço do Nodo A até o Player usando LineRenderer.
/// 2. Ao interagir com um Nodo B, fixa a linha entre o Nodo A e o Nodo B.
/// 3. Instancia um TextMeshPro flutuante no meio da linha com o rótulo da
///    propriedade (ex: "has_host").
/// 4. Envia a tripla (Nodo A -> has_host -> Nodo B) via WebSocket (porta 8085).
/// ============================================================================
/// </summary>
[DisallowMultipleComponent]
public class RelationBuilder : MonoBehaviour
{
    public static RelationBuilder Instance { get; private set; }

    [Header("Configuração de Object Property OWL")]
    [Tooltip("Rótulo padrão da propriedade de objeto a ser asserida (ex: has_host, has_habitat, has_transmission).")]
    public string propriedadeAtiva = "has_host";

    [Tooltip("Catálogo de propriedades OWL alternáveis (Tecla Tab ou R).")]
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
    public float larguraLinha = 0.06f;
    public Color corLinhaEmConstrucao = new Color(0.0f, 0.95f, 1.0f, 0.9f);
    public Color corLinhaFixada = new Color(0.66f, 0.33f, 0.97f, 0.95f);

    private LineRenderer m_LinhaAtiva;
    private Material m_MaterialLinha;
    private int m_IndicePropriedade = 0;

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
        PrepararMaterialLinha();
    }

    private void OnEnable()
    {
        InstanciaSemanticaBase.OnInstanciaInteragida += AoInteragirComNodo;
    }

    private void OnDisable()
    {
        InstanciaSemanticaBase.OnInstanciaInteragida -= AoInteragirComNodo;
    }

    private void Update()
    {
        // Permite alternar a Object Property ativa com Tab
        if (Input.GetKeyDown(KeyCode.Tab) && catalogoPropriedades != null && catalogoPropriedades.Length > 0)
        {
            m_IndicePropriedade = (m_IndicePropriedade + 1) % catalogoPropriedades.Length;
            propriedadeAtiva = catalogoPropriedades[m_IndicePropriedade];
            if (SemanticHUDManager.Instancia != null)
            {
                SemanticHUDManager.Instancia.AdicionarLinhaLog($"[OWL] Object Property selecionada: {propriedadeAtiva}");
            }
        }

        // Seção 2 da Especificação Mestra: Modo Criação (Do Zero In-Game) - Tecla N instancia um novo Nodo Ontológico no espaço
        if (Input.GetKeyDown(KeyCode.N))
        {
            InstanciarNodoCriacaoInGame();
        }

        // Atualiza a ponta final da linha ativa até a posição atual do Player enquanto em Modo de Conexão
        if (emModoConexao && m_LinhaAtiva != null)
        {
            Vector3 posPlayer = transform.position + Vector3.up * 0.45f;
            m_LinhaAtiva.SetPosition(0, nodoOrigemPosicao);
            m_LinhaAtiva.SetPosition(1, posPlayer);
        }

        // Faz os rótulos TextMeshPro 3D olharem para a câmera principal (Billboard legível)
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

    private void AoInteragirComNodo(IInstanciaSemantica instancia, bool sucesso)
    {
        if (instancia == null) return;
        RegistrarInteracaoNodo(instancia.Id, instancia.NomeClasse, instancia.Posicao + Vector3.up * 0.45f, instancia.EhValida);
    }

    /// <summary>
    /// Processa a interação com um Nodo A (inicia Modo de Conexão) ou Nodo B (fixa a linha, cria rótulo TMP e envia tripla via WebSocket).
    /// </summary>
    public void RegistrarInteracaoNodo(string nodeId, string nodeClass, Vector3 nodePos, bool ehValido = true)
    {
        if (string.IsNullOrEmpty(nodeId)) return;

        if (!emModoConexao)
        {
            // 1. Inicia Modo de Conexão a partir do Nodo A
            IniciarModoConexao(nodeId, nodeClass, nodePos);
        }
        else
        {
            // Evita conectar o mesmo nodo a si mesmo imediatamente
            if (string.Equals(nodoOrigemId, nodeId, StringComparison.OrdinalIgnoreCase))
                return;

            // 2. Fixa a conexão no Nodo B, instancia TextMeshPro e envia tripla via WebSocket
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
            GameObject lineObj = new GameObject($"Linha_Conexao_Ativa_{sourceId}");
            m_LinhaAtiva = ConfigurarLineRenderer(lineObj, corLinhaEmConstrucao);
        }

        m_LinhaAtiva.positionCount = 2;
        m_LinhaAtiva.SetPosition(0, nodoOrigemPosicao);
        m_LinhaAtiva.SetPosition(1, transform.position + Vector3.up * 0.45f);

        Debug.Log($"<color=#00E5FF><b>[RelationBuilder]</b></color> Modo de Conexão ATIVO: Origem '{sourceClass}' ({sourceId}) -> aguardando Nodo B para propriedade '{propriedadeAtiva}'...");
    }

    public void FinalizarConexao(string targetId, string targetClass, Vector3 targetPos, bool consistente = true)
    {
        if (!emModoConexao) return;

        emModoConexao = false;

        // 1. Fixa a linha física no espaço entre o Nodo A e o Nodo B
        GameObject linhaFixaObj;
        LineRenderer lrFixo;

        if (m_LinhaAtiva != null)
        {
            linhaFixaObj = m_LinhaAtiva.gameObject;
            linhaFixaObj.name = $"ObjectProperty_{nodoOrigemId}_{propriedadeAtiva}_{targetId}";
            lrFixo = m_LinhaAtiva;
            m_LinhaAtiva = null;
        }
        else
        {
            linhaFixaObj = new GameObject($"ObjectProperty_{nodoOrigemId}_{propriedadeAtiva}_{targetId}");
            lrFixo = ConfigurarLineRenderer(linhaFixaObj, corLinhaFixada);
        }

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
        LineRenderer lr = host.GetComponent<LineRenderer>() ?? host.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.startWidth = larguraLinha;
        lr.endWidth = larguraLinha;
        lr.numCapVertices = 4;
        lr.positionCount = 2;

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
        GameObject labelObj = new GameObject($"TMP_Label_{textoPropriedade}");
        labelObj.transform.SetParent(pai, true);
        labelObj.transform.position = posicaoMundo;

        TextMeshPro tmp = labelObj.AddComponent<TextMeshPro>();
        tmp.text = textoPropriedade;
        tmp.fontSize = 3.2f;
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
            rt.sizeDelta = new Vector2(4.0f, 1.0f);
        }

        return labelObj;
    }

    private int m_ContadorNodosCriadosInGame = 0;

    /// <summary>
    /// Seção 2 da Especificação Mestra: Modo Criação (Do Zero In-Game).
    /// Permite ao usuário soltar um novo Nodo Ontológico (Classe/Indivíduo) no espaço à sua frente
    /// e conectá-lo via laser (LineRenderer) para construir a ontologia de dentro do jogo.
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

        if (ger != null)
        {
            ger.AnexarLuzPontualDestaque(nodoObj, new Color(0.0f, 0.85f, 1.0f, 1f), 2.5f, 1.25f);
        }

        BoxCollider boxCol = nodoObj.GetComponent<BoxCollider>();
        if (boxCol != null) boxCol.isTrigger = true;

        GemaInstanciaValida compNodo = nodoObj.AddComponent<GemaInstanciaValida>();
        compNodo.Inicializar(idNodo, nomeClasse, $"{nomeClasse}({idNodo})", true);

        if (SemanticHUDManager.Instancia != null)
        {
            SemanticHUDManager.Instancia.AdicionarLinhaLog($"[CRIAÇÃO IN-GAME] Nodo '{nomeClasse}' ({idNodo}) posicionado em {posSpawn:F1}");
        }

        RegistrarInteracaoNodo(idNodo, nomeClasse, posSpawn, true);
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

