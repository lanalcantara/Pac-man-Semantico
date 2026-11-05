using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PacMan.Network;

/// <summary>
/// ============================================================================
/// INTERFACE ESPACIAL BIDIRECIONAL PARA ENGENHARIA DE ONTOLOGIAS OWL (Unity 6)
/// ============================================================================
/// Gerenciador Científico de HUD (SemanticHUDManager) utilizando TextMeshPro:
/// 1. Substitui "Score" por "Tamanho da ABox: [X]".
/// 2. Substitui "Vidas" por "Status do Reasoner":
///    - Padrão: "CONSISTENTE" (Verde #00FF88).
///    - Erro (exception do HermiT): "INCONSISTENTE" (Vermelho #FF2233, piscando).
/// 3. Substitui "Última Captura" por "Foco Atual: [Nome da Classe]".
/// 4. Componente ScrollRect (Terminal de Logs / Feed de Triplas RDF) para exibir feed em tempo real
///    (ex: "[+] researcher_01 instanciou AscarisLumbricoides").
/// ============================================================================
/// </summary>
public class SemanticHUDManager : MonoBehaviour
{
    private static SemanticHUDManager s_Instancia;
    public static SemanticHUDManager Instancia => s_Instancia;

    [Header("Componentes Principais (Canvas)")]
    public Canvas canvasPrincipal;
    public CanvasScaler canvasScaler;
    public GraphicRaycaster graphicRaycaster;

    [Header("Modal de Início (Sessão de Engenharia OWL)")]
    public GameObject modalPlayCanvas;
    public Button botaoIniciarPlay;
    public TMP_Text textoTituloPlayTMP;
    public TMP_Text textoSubtituloPlayTMP;
    public TMP_Text textoDescricaoPlayTMP;

    [Header("Painel Superior do HUD (TextMeshPro)")]
    public GameObject painelHUD;
    [Tooltip("Exibe 'Axiomas Inseridos: [X] | Tamanho da ABox: [X]'")]
    public TMP_Text textoAxiomasABoxTMP;
    [Tooltip("Exibe 'Status do Reasoner: CONSISTENTE (Verde)' ou 'INCONSISTENTE (Vermelho piscando)'")]
    public TMP_Text textoStatusReasonerTMP;
    [Tooltip("Exibe 'Foco Atual: [Nome da Classe]'")]
    public TMP_Text textoFocoAtualTMP;

    [Header("Terminal / Log de Ações (ScrollRect + TextMeshPro)")]
    public GameObject painelTerminalLog;
    public ScrollRect scrollRectTerminal;
    public RectTransform conteudoScrollTerminal;
    public TMP_Text textoLogTerminalTMP;

    [Header("Compatibilidade Legacy UI (Opcional)")]
    public Text textoPontuacao;
    public Text textoVidas;
    public Text textoEntidadeOntologica;

    [Header("Estado Semântico e do Reasoner (HermiT)")]
    public int axiomasInseridos = 0;
    public bool reasonerConsistente = true;
    public string detalheErroReasoner = "";
    public string focoAtualClasse = "Nenhuma classe em foco";
    public bool painelAberto = true;

    // Propriedades de compatibilidade com scripts existentes
    public int pontuacaoAtual
    {
        get => axiomasInseridos;
        set { axiomasInseridos = value; AtualizarDisplay(); }
    }

    public int vidasRestantes
    {
        get => reasonerConsistente ? 3 : 0;
        set { /* Substituído pelo Status do Reasoner */ }
    }

    public string entidadeAtual
    {
        get => focoAtualClasse;
        set { focoAtualClasse = value; AtualizarDisplay(); }
    }

    private readonly List<string> m_LinhasLog = new List<string>();
    private const int MAX_LINHAS_LOG = 60;
    private float m_TimerPiscaInconsistente = 0f;
    private bool m_EstadoVisivelPisca = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void GarantirHUDAtivoNaCena()
    {
        RemoverCanvasArcadeLegado();

        SemanticHUDManager hud = FindFirstObjectByType<SemanticHUDManager>();
        if (hud != null && hud.GetComponent<RectTransform>() == null)
        {
            DestroyImmediate(hud.gameObject);
            hud = null;
        }

        if (hud == null)
        {
            GameObject canvasObj = new GameObject("Semantic_HUD_Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            hud = canvasObj.AddComponent<SemanticHUDManager>();
        }
        else if (hud.gameObject.name.IndexOf("Arcade", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            hud.gameObject.name = "Semantic_HUD_Canvas";
        }

        hud.DefinirPainelVisivel(true);
    }

    public static void RemoverCanvasArcadeLegado()
    {
        string[] nomesLegados = new string[]
        {
            "HUD_Canvas_Arcade",
            "Painel_Arcade_HUD",
            "Modal_Play_Canvas",
            "Canvas_Arcade"
        };

        for (int i = 0; i < nomesLegados.Length; i++)
        {
            GameObject objLegado = GameObject.Find(nomesLegados[i]);
            if (objLegado != null && objLegado.GetComponent<SemanticHUDManager>() == null)
            {
                DestroyImmediate(objLegado);
            }
        }

        // Varre todos os TMP_Text na cena e remove imediatamente qualquer objeto corrompido sem RectTransform
        TMP_Text[] todosTextos = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < todosTextos.Length; i++)
        {
            TMP_Text t = todosTextos[i];
            if (t != null && t is TextMeshProUGUI && t.GetComponent<RectTransform>() == null)
            {
                DestroyImmediate(t.gameObject);
            }
        }
    }

    private void Awake()
    {
        s_Instancia = this;
        if (gameObject.name.IndexOf("Arcade", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            gameObject.name = "Semantic_HUD_Canvas";
        }
        RemoverCanvasArcadeLegado();
        GarantirEventSystem();
        GarantirCanvasEComponentes();
        Time.timeScale = 1.0f;
    }

    private void OnEnable()
    {
        InstanciaSemanticaBase.OnInstanciaInteragida += AoInteragirInstancia;
        InstanciaSemanticaBase.OnInstanciaFocada += AoFocarInstancia;
        InstanciaSemanticaBase.OnInstanciaDesfocada += AoDesfocarInstancia;

        if (ProtegeWebSocketBridge.Instance != null)
        {
            InscreverEventosWebSocket(ProtegeWebSocketBridge.Instance);
        }
    }

    private void OnDisable()
    {
        InstanciaSemanticaBase.OnInstanciaInteragida -= AoInteragirInstancia;
        InstanciaSemanticaBase.OnInstanciaFocada -= AoFocarInstancia;
        InstanciaSemanticaBase.OnInstanciaDesfocada -= AoDesfocarInstancia;

        if (ProtegeWebSocketBridge.Instance != null)
        {
            DesinscreverEventosWebSocket(ProtegeWebSocketBridge.Instance);
        }
    }

    private void Start()
    {
        Time.timeScale = 1.0f;
        RemoverCanvasArcadeLegado();

        var ws = ProtegeWebSocketBridge.Instance ?? FindFirstObjectByType<ProtegeWebSocketBridge>();
        if (ws != null)
        {
            DesinscreverEventosWebSocket(ws);
            InscreverEventosWebSocket(ws);
        }

        if (m_LinhasLog.Count == 0)
        {
            AdicionarLinhaLog("[SYS] Interface Espacial Bidirecional OWL inicializada.");
            AdicionarLinhaLog("[WS:8085] Aguardando sincronização com plugin Protégé (HermiT Reasoner)...");
        }

        DefinirPainelVisivel(true);
        AtualizarDisplay();
    }

    private void Update()
    {
        // Efeito de alerta piscante quando o HermiT Reasoner detecta INCONSISTÊNCIA
        if (!reasonerConsistente && textoStatusReasonerTMP != null)
        {
            m_TimerPiscaInconsistente += Time.unscaledDeltaTime * 5.0f;
            bool visivel = Mathf.Sin(m_TimerPiscaInconsistente * Mathf.PI) >= 0f;
            if (visivel != m_EstadoVisivelPisca)
            {
                m_EstadoVisivelPisca = visivel;
                textoStatusReasonerTMP.text = m_EstadoVisivelPisca
                    ? "Status do Reasoner: <color=#FF2233><b>⚠ INCONSISTENTE</b></color>"
                    : "Status do Reasoner: <color=#660011><b>⚠ INCONSISTENTE</b></color>";
            }
        }
    }

    private void InscreverEventosWebSocket(ProtegeWebSocketBridge ws)
    {
        ws.OnActionLogged += AdicionarLinhaLog;
        ws.OnReasonerStatusChanged += DefinirStatusReasoner;
        ws.OnAxiomCountChanged += DefinirAxiomasInseridos;
    }

    private void DesinscreverEventosWebSocket(ProtegeWebSocketBridge ws)
    {
        ws.OnActionLogged -= AdicionarLinhaLog;
        ws.OnReasonerStatusChanged -= DefinirStatusReasoner;
        ws.OnAxiomCountChanged -= DefinirAxiomasInseridos;
    }

    public static void GarantirEventSystem()
    {
        if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();

            Type inputSystemModule = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputSystemModule != null)
            {
                esObj.AddComponent(inputSystemModule);
            }
            else
            {
                esObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }
        }
    }

    private void AoFocarInstancia(IInstanciaSemantica inst)
    {
        if (inst == null) return;
        focoAtualClasse = InstanciaSemanticaBase.FormatarNomeClasse(inst.NomeClasse);
        AtualizarDisplay();
    }

    private void AoDesfocarInstancia(IInstanciaSemantica inst)
    {
        // Mantém o último foco ou exibe estado neutro
        AtualizarDisplay();
    }

    private void AoInteragirInstancia(IInstanciaSemantica inst, bool sucesso)
    {
        if (inst != null)
        {
            string nomeClasse = InstanciaSemanticaBase.FormatarNomeClasse(inst.NomeClasse);
            focoAtualClasse = nomeClasse;

            // Se o ProtegeWebSocketBridge não estiver ativo na cena para incrementar por evento, atualiza localmente
            if (ProtegeWebSocketBridge.Instance == null)
            {
                axiomasInseridos++;
                AdicionarLinhaLog($"[+] researcher_01 instanciou {inst.NomeClasse}");
                if (!inst.EhValida)
                {
                    DefinirStatusReasoner(false, $"HermiT: Violação em {inst.NomeClasse}");
                }
            }
        }
        AtualizarDisplay();
    }

    public void DefinirAxiomasInseridos(int quantidade)
    {
        axiomasInseridos = Mathf.Max(0, quantidade);
        AtualizarDisplay();
    }

    public void DefinirStatusReasoner(bool consistente, string detalhe = "")
    {
        reasonerConsistente = consistente;
        detalheErroReasoner = detalhe;
        m_TimerPiscaInconsistente = 0f;
        m_EstadoVisivelPisca = true;
        AtualizarDisplay();
    }

    public void DefinirFocoAtual(string nomeClasse)
    {
        if (!string.IsNullOrEmpty(nomeClasse))
        {
            focoAtualClasse = nomeClasse;
            AtualizarDisplay();
        }
    }

    /// <summary>
    /// Adiciona uma entrada ao ScrollRect (Log/Terminal de Ações) no formato científico OWL.
    /// Exemplo: "[+] researcher_01 instanciou AscarisLumbricoides"
    /// </summary>
    public void AdicionarLinhaLog(string mensagem)
    {
        if (string.IsNullOrEmpty(mensagem)) return;

        m_LinhasLog.Add(mensagem);
        if (m_LinhasLog.Count > MAX_LINHAS_LOG)
        {
            m_LinhasLog.RemoveAt(0);
        }

        if (textoLogTerminalTMP != null)
        {
            textoLogTerminalTMP.text = string.Join("\n", m_LinhasLog);
        }

        if (conteudoScrollTerminal != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(conteudoScrollTerminal);
        }

        if (scrollRectTerminal != null)
        {
            Canvas.ForceUpdateCanvases();
            scrollRectTerminal.verticalNormalizedPosition = 0f;
        }
    }

    // Métodos legados de compatibilidade
    public void DefinirPontuacao(int pontos) => DefinirAxiomasInseridos(pontos);
    public void DefinirVidas(int vidas) => DefinirStatusReasoner(vidas > 0);
    public void DefinirEntidade(string nomeEntidade) => DefinirFocoAtual(nomeEntidade);

    public void AlternarVisibilidadePainel()
    {
        painelAberto = !painelAberto;
        if (painelHUD != null) painelHUD.SetActive(painelAberto);
        if (painelTerminalLog != null) painelTerminalLog.SetActive(painelAberto);
    }

    public void DefinirModalPlayVisivel(bool visivel)
    {
        if (modalPlayCanvas != null) modalPlayCanvas.SetActive(visivel);
    }

    public void DefinirPainelVisivel(bool visivel)
    {
        painelAberto = visivel;
        if (painelHUD != null) painelHUD.SetActive(visivel);
        if (painelTerminalLog != null) painelTerminalLog.SetActive(visivel);
    }

    public bool TemModalPlayAtivo()
    {
        return modalPlayCanvas != null && modalPlayCanvas.activeSelf;
    }

    public void AoClicarIniciarPlay()
    {
        DefinirModalPlayVisivel(false);
        Time.timeScale = 1.0f;
        DefinirPainelVisivel(true);

        var ctrl = FindFirstObjectByType<PacMan.Web.ControladorDemoWeb>();
        if (ctrl != null)
        {
            ctrl.IniciarJogoPlay();
        }
        AdicionarLinhaLog("[+] Sessão de Engenharia Espacial OWL iniciada.");
    }

    public void AtualizarDisplay()
    {
        string txtAxiomas = $"Tamanho da ABox: <color=#00E5FF><b>{axiomasInseridos}</b></color>";
        string txtReasoner = reasonerConsistente
            ? "Status do Reasoner: <color=#00FF88><b>CONSISTENTE</b></color>"
            : "Status do Reasoner: <color=#FF2233><b>⚠ INCONSISTENTE</b></color>";
        string txtFoco = $"Foco Atual: <color=#38BDF8><b>{focoAtualClasse}</b></color>";

        if (textoAxiomasABoxTMP != null) textoAxiomasABoxTMP.text = txtAxiomas;
        if (textoStatusReasonerTMP != null) textoStatusReasonerTMP.text = txtReasoner;
        if (textoFocoAtualTMP != null) textoFocoAtualTMP.text = txtFoco;

        // Sincroniza campos legados se existirem
        if (textoPontuacao != null) textoPontuacao.text = txtAxiomas;
        if (textoVidas != null) textoVidas.text = txtReasoner;
        if (textoEntidadeOntologica != null) textoEntidadeOntologica.text = txtFoco;
    }

    /// <summary>
    /// Utilitário seguro para obter ou criar um filho de UI garantindo que ele sempre nasça com RectTransform
    /// e seja filho de um Canvas/RectTransform antes de receber qualquer componente TextMeshProUGUI ou Image.
    /// </summary>
    private static RectTransform ObterOuCriarFilhoUI(Transform pai, string nome)
    {
        Transform existente = pai.Find(nome);
        if (existente != null)
        {
            RectTransform rtExistente = existente.GetComponent<RectTransform>();
            if (rtExistente != null)
            {
                return rtExistente;
            }
            DestroyImmediate(existente.gameObject);
        }

        GameObject novoObj = new GameObject(nome, typeof(RectTransform), typeof(CanvasRenderer));
        novoObj.transform.SetParent(pai, false);
        return novoObj.GetComponent<RectTransform>();
    }

    /// <summary>
    /// Aplica fonte Branca (#FFFFFF) com Outline Preto (#000000) em qualquer TextMeshProUGUI
    /// para nitidez absoluta contra o fundo Dark Grid sem ser lavado pelo Bloom.
    /// </summary>
    private static void AplicarNitidezTextoTMP(TextMeshProUGUI tmp, Color corBase)
    {
        if (tmp == null) return;
        tmp.color = corBase;
        tmp.outlineColor = new Color32(0, 0, 0, 255);
        tmp.outlineWidth = 0.16f;
    }

    /// <summary>
    /// Constrói a interface científica usando TextMeshProUGUI e ScrollRect para o Log/Terminal de Ações.
    /// </summary>
    private void GarantirCanvasEComponentes()
    {
        if (canvasPrincipal == null) canvasPrincipal = GetComponent<Canvas>();
        if (canvasPrincipal == null)
        {
            canvasPrincipal = gameObject.AddComponent<Canvas>();
        }
        canvasPrincipal.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasPrincipal.pixelPerfect = true;

        if (GetComponent<RectTransform>() == null)
        {
            gameObject.AddComponent<RectTransform>();
        }

        canvasScaler = GetComponent<CanvasScaler>();
        if (canvasScaler == null) canvasScaler = gameObject.AddComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1920, 1080);
        canvasScaler.matchWidthOrHeight = 0.5f;

        graphicRaycaster = GetComponent<GraphicRaycaster>();
        if (graphicRaycaster == null) graphicRaycaster = gameObject.AddComponent<GraphicRaycaster>();
        graphicRaycaster.enabled = true;

        // Remove qualquer modal de Play ou painel Arcade legado caso exista como filho do Canvas
        Transform modalTr = transform.Find("Modal_Play_Canvas");
        if (modalTr != null)
        {
            if (Application.isPlaying) Destroy(modalTr.gameObject);
            else DestroyImmediate(modalTr.gameObject);
        }
        Transform arcadeTr = transform.Find("Painel_Arcade_HUD");
        if (arcadeTr != null)
        {
            if (Application.isPlaying) Destroy(arcadeTr.gameObject);
            else DestroyImmediate(arcadeTr.gameObject);
        }
        modalPlayCanvas = null;

        // =====================================================================
        // 2. BARRA SUPERIOR CIENTÍFICA (AXIOMAS / FOCO ATUAL / STATUS DO REASONER)
        // =====================================================================
        RectTransform rtPainelHUD = ObterOuCriarFilhoUI(transform, "Painel_OWL_HUD");
        painelHUD = rtPainelHUD.gameObject;
        rtPainelHUD.anchorMin = new Vector2(0f, 1f);
        rtPainelHUD.anchorMax = new Vector2(1f, 1f);
        rtPainelHUD.pivot = new Vector2(0.5f, 1f);
        rtPainelHUD.anchoredPosition = Vector2.zero;
        rtPainelHUD.sizeDelta = new Vector2(0f, 56f);

        Image imgPainel = painelHUD.GetComponent<Image>();
        if (imgPainel == null) imgPainel = painelHUD.AddComponent<Image>();
        imgPainel.color = new Color(0.02f, 0.05f, 0.12f, 0.92f);

        // 2.1 Esquerda: Axiomas Inseridos / Tamanho da ABox
        if (textoAxiomasABoxTMP == null || textoAxiomasABoxTMP.GetComponent<RectTransform>() == null)
        {
            RectTransform rt = ObterOuCriarFilhoUI(rtPainelHUD, "Texto_AxiomasABox_TMP");
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(28f, 0f);
            rt.sizeDelta = new Vector2(560f, 44f);

            TextMeshProUGUI tmp = rt.gameObject.GetComponent<TextMeshProUGUI>();
            if (tmp == null) tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = 18;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.richText = true;
            AplicarNitidezTextoTMP(tmp, Color.white);
            textoAxiomasABoxTMP = tmp;
        }

        // 2.2 Centro: Foco Atual: [Nome da Classe]
        if (textoFocoAtualTMP == null || textoFocoAtualTMP.GetComponent<RectTransform>() == null)
        {
            RectTransform rt = ObterOuCriarFilhoUI(rtPainelHUD, "Texto_FocoAtual_TMP");
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(580f, 44f);

            TextMeshProUGUI tmp = rt.gameObject.GetComponent<TextMeshProUGUI>();
            if (tmp == null) tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = 18;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.richText = true;
            AplicarNitidezTextoTMP(tmp, Color.white);
            textoFocoAtualTMP = tmp;
        }

        // 2.3 Direita: Status do Reasoner (CONSISTENTE / INCONSISTENTE)
        if (textoStatusReasonerTMP == null || textoStatusReasonerTMP.GetComponent<RectTransform>() == null)
        {
            RectTransform rt = ObterOuCriarFilhoUI(rtPainelHUD, "Texto_StatusReasoner_TMP");
            rt.anchorMin = new Vector2(1f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(-65f, 0f);
            rt.sizeDelta = new Vector2(500f, 44f);

            TextMeshProUGUI tmp = rt.gameObject.GetComponent<TextMeshProUGUI>();
            if (tmp == null) tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = 18;
            tmp.alignment = TextAlignmentOptions.MidlineRight;
            tmp.richText = true;
            AplicarNitidezTextoTMP(tmp, Color.white);
            textoStatusReasonerTMP = tmp;
        }

        // Botão [ X ] de Ocultar Painel
        Transform btnTr = painelHUD.transform.Find("Botao_Fechar");
        if (btnTr == null)
        {
            RectTransform rt = ObterOuCriarFilhoUI(rtPainelHUD, "Botao_Fechar");
            GameObject btnObj = rt.gameObject;
            rt.anchorMin = new Vector2(1f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(-18f, 0f);
            rt.sizeDelta = new Vector2(30f, 28f);

            Image img = btnObj.GetComponent<Image>();
            if (img == null) img = btnObj.AddComponent<Image>();
            img.color = new Color(0.85f, 0.2f, 0.2f, 0.95f);

            Button btn = btnObj.GetComponent<Button>();
            if (btn == null) btn = btnObj.AddComponent<Button>();
            btn.onClick.AddListener(AlternarVisibilidadePainel);

            RectTransform rtTxt = ObterOuCriarFilhoUI(rt, "Text_TMP");
            rtTxt.anchorMin = Vector2.zero;
            rtTxt.anchorMax = Vector2.one;
            rtTxt.sizeDelta = Vector2.zero;

            TextMeshProUGUI txt = rtTxt.gameObject.GetComponent<TextMeshProUGUI>();
            if (txt == null) txt = rtTxt.gameObject.AddComponent<TextMeshProUGUI>();
            txt.fontSize = 15;
            txt.fontStyle = FontStyles.Bold;
            txt.text = "✕";
            txt.color = Color.white;
            txt.alignment = TextAlignmentOptions.Center;
        }

        // =====================================================================
        // 3. SCROLLRECT - TERMINAL / LOG DE AÇÕES ONTOLÓGICAS (INFERIOR ESQUERDO)
        // =====================================================================
        Transform termTr = transform.Find("Painel_Terminal_Log");
        if (termTr == null || termTr.GetComponent<RectTransform>() == null)
        {
            RectTransform rtTerm = ObterOuCriarFilhoUI(transform, "Painel_Terminal_Log");
            painelTerminalLog = rtTerm.gameObject;
            rtTerm.anchorMin = new Vector2(0f, 0f);
            rtTerm.anchorMax = new Vector2(0f, 0f);
            rtTerm.pivot = new Vector2(0f, 0f);
            rtTerm.anchoredPosition = new Vector2(24f, 24f);
            rtTerm.sizeDelta = new Vector2(680f, 185f);

            Image imgTerm = painelTerminalLog.GetComponent<Image>();
            if (imgTerm == null) imgTerm = painelTerminalLog.AddComponent<Image>();
            imgTerm.color = new Color(0.01f, 0.03f, 0.09f, 0.88f);

            // Cabeçalho do Terminal
            RectTransform rtHeader = ObterOuCriarFilhoUI(rtTerm, "Header_Terminal_TMP");
            rtHeader.anchorMin = new Vector2(0f, 1f);
            rtHeader.anchorMax = new Vector2(1f, 1f);
            rtHeader.pivot = new Vector2(0.5f, 1f);
            rtHeader.anchoredPosition = new Vector2(0f, -4f);
            rtHeader.sizeDelta = new Vector2(-20f, 24f);

            TextMeshProUGUI txtHeader = rtHeader.gameObject.GetComponent<TextMeshProUGUI>();
            if (txtHeader == null) txtHeader = rtHeader.gameObject.AddComponent<TextMeshProUGUI>();
            txtHeader.fontSize = 13;
            txtHeader.fontStyle = FontStyles.Bold;
            txtHeader.text = "TERMINAL DE LOGS (FEED DE TRIPLAS RDF / WEBSOCKET 8085)";
            txtHeader.color = new Color(0.0f, 0.85f, 1.0f);
            txtHeader.alignment = TextAlignmentOptions.MidlineLeft;

            // Viewport do ScrollRect
            RectTransform rtVp = ObterOuCriarFilhoUI(rtTerm, "Viewport");
            GameObject viewportObj = rtVp.gameObject;
            rtVp.anchorMin = new Vector2(0f, 0f);
            rtVp.anchorMax = new Vector2(1f, 1f);
            rtVp.offsetMin = new Vector2(10f, 8f);
            rtVp.offsetMax = new Vector2(-10f, -30f);

            Image imgVp = viewportObj.GetComponent<Image>();
            if (imgVp == null) imgVp = viewportObj.AddComponent<Image>();
            imgVp.color = new Color(0f, 0f, 0f, 0.01f);
            if (viewportObj.GetComponent<RectMask2D>() == null) viewportObj.AddComponent<RectMask2D>();

            // Content do ScrollRect
            conteudoScrollTerminal = ObterOuCriarFilhoUI(rtVp, "Content");
            GameObject contentObj = conteudoScrollTerminal.gameObject;
            conteudoScrollTerminal.anchorMin = new Vector2(0f, 1f);
            conteudoScrollTerminal.anchorMax = new Vector2(1f, 1f);
            conteudoScrollTerminal.pivot = new Vector2(0.5f, 1f);
            conteudoScrollTerminal.anchoredPosition = Vector2.zero;
            conteudoScrollTerminal.sizeDelta = new Vector2(0f, 140f);

            ContentSizeFitter csf = contentObj.GetComponent<ContentSizeFitter>();
            if (csf == null) csf = contentObj.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            TextMeshProUGUI tmpLog = contentObj.GetComponent<TextMeshProUGUI>();
            if (tmpLog == null) tmpLog = contentObj.AddComponent<TextMeshProUGUI>();
            tmpLog.fontSize = 14;
            tmpLog.alignment = TextAlignmentOptions.BottomLeft;
            tmpLog.richText = true;
            tmpLog.textWrappingMode = TextWrappingModes.Normal;
            AplicarNitidezTextoTMP(tmpLog, Color.white);
            textoLogTerminalTMP = tmpLog;

            scrollRectTerminal = painelTerminalLog.GetComponent<ScrollRect>();
            if (scrollRectTerminal == null) scrollRectTerminal = painelTerminalLog.AddComponent<ScrollRect>();
            scrollRectTerminal.viewport = rtVp;
            scrollRectTerminal.content = conteudoScrollTerminal;
            scrollRectTerminal.horizontal = false;
            scrollRectTerminal.vertical = true;
            scrollRectTerminal.movementType = ScrollRect.MovementType.Clamped;
            scrollRectTerminal.scrollSensitivity = 20f;
        }
        else
        {
            painelTerminalLog = termTr.gameObject;
            scrollRectTerminal = painelTerminalLog.GetComponent<ScrollRect>();
            if (scrollRectTerminal != null)
            {
                conteudoScrollTerminal = scrollRectTerminal.content;
                if (conteudoScrollTerminal != null && textoLogTerminalTMP == null)
                {
                    textoLogTerminalTMP = conteudoScrollTerminal.GetComponent<TMP_Text>();
                }
            }
        }

        if (painelHUD != null) painelHUD.SetActive(true);
        if (painelTerminalLog != null) painelTerminalLog.SetActive(true);
    }
}
