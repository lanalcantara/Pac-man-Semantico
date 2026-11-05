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
/// HUD Científico de Engenharia de Ontologias (utilizando TextMeshPro):
/// 1. Substitui "Score" por "Axiomas Inseridos: [X]" / "Tamanho da ABox: [X]".
/// 2. Substitui "Vidas" por "Status do Reasoner":
///    - Padrão: "CONSISTENTE" (Verde #00FF88).
///    - Erro (exception do HermiT): "INCONSISTENTE" (Vermelho #FF2233, piscando).
/// 3. Substitui "Última Captura" por "Foco Atual: [Nome da Classe]".
/// 4. Componente ScrollRect (Log/Terminal de Ações) para exibir feed em tempo real
///    (ex: "[+] researcher_01 instanciou AscarisLumbricoides").
/// ============================================================================
/// </summary>
public class HUDCanvasArcade : MonoBehaviour
{
    private static HUDCanvasArcade s_Instancia;
    public static HUDCanvasArcade Instancia => s_Instancia;

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

    private void Awake()
    {
        s_Instancia = this;
        GarantirEventSystem();
        GarantirCanvasEComponentes();
        Time.timeScale = 0f;
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
        Time.timeScale = 0f;

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
        string txtAxiomas = $"Axiomas Inseridos: <color=#00E5FF><b>{axiomasInseridos}</b></color> <color=#64748B>|</color> Tamanho da ABox: <color=#00E5FF><b>{axiomasInseridos}</b></color>";
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
    /// Constrói a interface científica usando TextMeshProUGUI e ScrollRect para o Log/Terminal de Ações.
    /// </summary>
    private void GarantirCanvasEComponentes()
    {
        if (canvasPrincipal == null) canvasPrincipal = GetComponent<Canvas>();
        if (canvasPrincipal == null)
        {
            canvasPrincipal = gameObject.AddComponent<Canvas>();
            canvasPrincipal.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasPrincipal.pixelPerfect = true;
        }

        canvasScaler = GetComponent<CanvasScaler>() ?? gameObject.AddComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1920, 1080);
        canvasScaler.matchWidthOrHeight = 0.5f;

        graphicRaycaster = GetComponent<GraphicRaycaster>() ?? gameObject.AddComponent<GraphicRaycaster>();
        graphicRaycaster.enabled = true;

        // =====================================================================
        // 1. MODAL DE INÍCIO (INTERFACE ESPACIAL BIDIRECIONAL OWL)
        // =====================================================================
        Transform modalTr = transform.Find("Modal_Play_Canvas");
        if (modalTr == null)
        {
            modalPlayCanvas = new GameObject("Modal_Play_Canvas");
            modalPlayCanvas.transform.SetParent(transform, false);
            RectTransform rtModal = modalPlayCanvas.AddComponent<RectTransform>();
            rtModal.anchorMin = new Vector2(0.5f, 0.5f);
            rtModal.anchorMax = new Vector2(0.5f, 0.5f);
            rtModal.pivot = new Vector2(0.5f, 0.5f);
            rtModal.anchoredPosition = Vector2.zero;
            rtModal.sizeDelta = new Vector2(880f, 480f);

            Image imgModal = modalPlayCanvas.AddComponent<Image>();
            imgModal.color = new Color(0.03f, 0.06f, 0.14f, 0.96f);

            // Título TMP
            GameObject tTitleObj = new GameObject("Texto_Titulo_TMP");
            tTitleObj.transform.SetParent(modalPlayCanvas.transform, false);
            RectTransform rtTitle = tTitleObj.AddComponent<RectTransform>();
            rtTitle.anchorMin = new Vector2(0.5f, 1f);
            rtTitle.anchorMax = new Vector2(0.5f, 1f);
            rtTitle.pivot = new Vector2(0.5f, 1f);
            rtTitle.anchoredPosition = new Vector2(0f, -32f);
            rtTitle.sizeDelta = new Vector2(820f, 50f);

            textoTituloPlayTMP = tTitleObj.AddComponent<TextMeshProUGUI>();
            textoTituloPlayTMP.fontSize = 28;
            textoTituloPlayTMP.fontStyle = FontStyles.Bold;
            textoTituloPlayTMP.text = "INTERFACE ESPACIAL BIDIRECIONAL PARA ENGENHARIA OWL";
            textoTituloPlayTMP.color = new Color(0.0f, 0.90f, 1.0f);
            textoTituloPlayTMP.alignment = TextAlignmentOptions.Center;

            // Subtítulo TMP
            GameObject tSubObj = new GameObject("Texto_Subtitulo_TMP");
            tSubObj.transform.SetParent(modalPlayCanvas.transform, false);
            RectTransform rtSub = tSubObj.AddComponent<RectTransform>();
            rtSub.anchorMin = new Vector2(0.5f, 1f);
            rtSub.anchorMax = new Vector2(0.5f, 1f);
            rtSub.pivot = new Vector2(0.5f, 1f);
            rtSub.anchoredPosition = new Vector2(0f, -86f);
            rtSub.sizeDelta = new Vector2(820f, 34f);

            textoSubtituloPlayTMP = tSubObj.AddComponent<TextMeshProUGUI>();
            textoSubtituloPlayTMP.fontSize = 18;
            textoSubtituloPlayTMP.text = "Protégé OSGi WebSocket (Porta 8085) • Reasoner HermiT • ALCQ(D)";
            textoSubtituloPlayTMP.color = new Color(0.58f, 0.64f, 0.72f);
            textoSubtituloPlayTMP.alignment = TextAlignmentOptions.Center;

            // Descrição TMP
            GameObject tDescObj = new GameObject("Texto_Descricao_TMP");
            tDescObj.transform.SetParent(modalPlayCanvas.transform, false);
            RectTransform rtDesc = tDescObj.AddComponent<RectTransform>();
            rtDesc.anchorMin = new Vector2(0.5f, 1f);
            rtDesc.anchorMax = new Vector2(0.5f, 1f);
            rtDesc.pivot = new Vector2(0.5f, 1f);
            rtDesc.anchoredPosition = new Vector2(0f, -140f);
            rtDesc.sizeDelta = new Vector2(780f, 190f);

            textoDescricaoPlayTMP = tDescObj.AddComponent<TextMeshProUGUI>();
            textoDescricaoPlayTMP.fontSize = 16;
            textoDescricaoPlayTMP.text =
                "• Interaja com os <color=#00E5FF><b>Nodos de Dados (Cubos/Prismas)</b></color> para instanciar indivíduos na ABox.\n" +
                "• Conecte Nodos A e B para asserir <color=#A855F7><b>Object Properties (ex: has_host)</b></color> via WebSocket.\n" +
                "• Monitore o <color=#00FF88><b>Status do Reasoner (HermiT)</b></color> e os Agentes de Inferência.\n\n" +
                "<color=#94A3B8>Navegação: WASD / Setas • C: Alternar Câmera • H: Alternar HUD • Z: Desfazer Inconsistência</color>";
            textoDescricaoPlayTMP.color = new Color(0.95f, 0.96f, 0.98f);
            textoDescricaoPlayTMP.alignment = TextAlignmentOptions.Center;

            // Botão Iniciar Sessão
            GameObject btnPlayObj = new GameObject("Botao_Iniciar_Play");
            btnPlayObj.transform.SetParent(modalPlayCanvas.transform, false);
            RectTransform rtBtn = btnPlayObj.AddComponent<RectTransform>();
            rtBtn.anchorMin = new Vector2(0.5f, 0f);
            rtBtn.anchorMax = new Vector2(0.5f, 0f);
            rtBtn.pivot = new Vector2(0.5f, 0f);
            rtBtn.anchoredPosition = new Vector2(0f, 38f);
            rtBtn.sizeDelta = new Vector2(620f, 70f);

            Image imgBtn = btnPlayObj.AddComponent<Image>();
            imgBtn.color = new Color(0.05f, 0.65f, 0.55f);

            botaoIniciarPlay = btnPlayObj.AddComponent<Button>();
            botaoIniciarPlay.onClick.AddListener(AoClicarIniciarPlay);

            GameObject txtBtnObj = new GameObject("Text_TMP");
            txtBtnObj.transform.SetParent(btnPlayObj.transform, false);
            RectTransform rtTxtBtn = txtBtnObj.AddComponent<RectTransform>();
            rtTxtBtn.anchorMin = Vector2.zero;
            rtTxtBtn.anchorMax = Vector2.one;
            rtTxtBtn.sizeDelta = Vector2.zero;

            TextMeshProUGUI txtBtn = txtBtnObj.AddComponent<TextMeshProUGUI>();
            txtBtn.fontSize = 22;
            txtBtn.fontStyle = FontStyles.Bold;
            txtBtn.text = "INICIAR AMBIENTE DE ENGENHARIA OWL";
            txtBtn.color = Color.white;
            txtBtn.alignment = TextAlignmentOptions.Center;
        }
        else
        {
            modalPlayCanvas = modalTr.gameObject;
        }

        // =====================================================================
        // 2. BARRA SUPERIOR CIENTÍFICA (AXIOMAS / FOCO ATUAL / STATUS DO REASONER)
        // =====================================================================
        Transform painelTr = transform.Find("Painel_OWL_HUD");
        if (painelTr == null)
        {
            painelHUD = new GameObject("Painel_OWL_HUD");
            painelHUD.transform.SetParent(transform, false);
            RectTransform rt = painelHUD.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, 56f);

            Image img = painelHUD.AddComponent<Image>();
            img.color = new Color(0.02f, 0.05f, 0.12f, 0.92f);
        }
        else
        {
            painelHUD = painelTr.gameObject;
        }

        // 2.1 Esquerda: Axiomas Inseridos / Tamanho da ABox
        if (textoAxiomasABoxTMP == null)
        {
            Transform tTr = painelHUD.transform.Find("Texto_AxiomasABox_TMP");
            GameObject tObj = tTr != null ? tTr.gameObject : new GameObject("Texto_AxiomasABox_TMP");
            tObj.transform.SetParent(painelHUD.transform, false);
            RectTransform rt = tObj.GetComponent<RectTransform>() ?? tObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(28f, 0f);
            rt.sizeDelta = new Vector2(560f, 44f);

            textoAxiomasABoxTMP = tObj.GetComponent<TextMeshProUGUI>() ?? tObj.AddComponent<TextMeshProUGUI>();
            textoAxiomasABoxTMP.fontSize = 18;
            textoAxiomasABoxTMP.alignment = TextAlignmentOptions.MidlineLeft;
            textoAxiomasABoxTMP.richText = true;
        }

        // 2.2 Centro: Foco Atual: [Nome da Classe]
        if (textoFocoAtualTMP == null)
        {
            Transform tTr = painelHUD.transform.Find("Texto_FocoAtual_TMP");
            GameObject tObj = tTr != null ? tTr.gameObject : new GameObject("Texto_FocoAtual_TMP");
            tObj.transform.SetParent(painelHUD.transform, false);
            RectTransform rt = tObj.GetComponent<RectTransform>() ?? tObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(580f, 44f);

            textoFocoAtualTMP = tObj.GetComponent<TextMeshProUGUI>() ?? tObj.AddComponent<TextMeshProUGUI>();
            textoFocoAtualTMP.fontSize = 18;
            textoFocoAtualTMP.alignment = TextAlignmentOptions.Center;
            textoFocoAtualTMP.richText = true;
        }

        // 2.3 Direita: Status do Reasoner (CONSISTENTE / INCONSISTENTE)
        if (textoStatusReasonerTMP == null)
        {
            Transform tTr = painelHUD.transform.Find("Texto_StatusReasoner_TMP");
            GameObject tObj = tTr != null ? tTr.gameObject : new GameObject("Texto_StatusReasoner_TMP");
            tObj.transform.SetParent(painelHUD.transform, false);
            RectTransform rt = tObj.GetComponent<RectTransform>() ?? tObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(-65f, 0f);
            rt.sizeDelta = new Vector2(500f, 44f);

            textoStatusReasonerTMP = tObj.GetComponent<TextMeshProUGUI>() ?? tObj.AddComponent<TextMeshProUGUI>();
            textoStatusReasonerTMP.fontSize = 18;
            textoStatusReasonerTMP.alignment = TextAlignmentOptions.MidlineRight;
            textoStatusReasonerTMP.richText = true;
        }

        // Botão [ X ] de Ocultar Painel
        Transform btnTr = painelHUD.transform.Find("Botao_Fechar");
        if (btnTr == null)
        {
            GameObject btnObj = new GameObject("Botao_Fechar");
            btnObj.transform.SetParent(painelHUD.transform, false);
            RectTransform rt = btnObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(-18f, 0f);
            rt.sizeDelta = new Vector2(30f, 28f);

            Image img = btnObj.AddComponent<Image>();
            img.color = new Color(0.85f, 0.2f, 0.2f, 0.95f);

            Button btn = btnObj.AddComponent<Button>();
            btn.onClick.AddListener(AlternarVisibilidadePainel);

            GameObject txtObj = new GameObject("Text_TMP");
            txtObj.transform.SetParent(btnObj.transform, false);
            RectTransform rtTxt = txtObj.AddComponent<RectTransform>();
            rtTxt.anchorMin = Vector2.zero;
            rtTxt.anchorMax = Vector2.one;
            rtTxt.sizeDelta = Vector2.zero;

            TextMeshProUGUI txt = txtObj.AddComponent<TextMeshProUGUI>();
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
        if (termTr == null)
        {
            painelTerminalLog = new GameObject("Painel_Terminal_Log");
            painelTerminalLog.transform.SetParent(transform, false);
            RectTransform rtTerm = painelTerminalLog.AddComponent<RectTransform>();
            rtTerm.anchorMin = new Vector2(0f, 0f);
            rtTerm.anchorMax = new Vector2(0f, 0f);
            rtTerm.pivot = new Vector2(0f, 0f);
            rtTerm.anchoredPosition = new Vector2(24f, 24f);
            rtTerm.sizeDelta = new Vector2(680f, 185f);

            Image imgTerm = painelTerminalLog.AddComponent<Image>();
            imgTerm.color = new Color(0.01f, 0.03f, 0.09f, 0.88f);

            // Cabeçalho do Terminal
            GameObject headerObj = new GameObject("Header_Terminal_TMP");
            headerObj.transform.SetParent(painelTerminalLog.transform, false);
            RectTransform rtHeader = headerObj.AddComponent<RectTransform>();
            rtHeader.anchorMin = new Vector2(0f, 1f);
            rtHeader.anchorMax = new Vector2(1f, 1f);
            rtHeader.pivot = new Vector2(0.5f, 1f);
            rtHeader.anchoredPosition = new Vector2(0f, -4f);
            rtHeader.sizeDelta = new Vector2(-20f, 24f);

            TextMeshProUGUI txtHeader = headerObj.AddComponent<TextMeshProUGUI>();
            txtHeader.fontSize = 13;
            txtHeader.fontStyle = FontStyles.Bold;
            txtHeader.text = "TERMINAL DE AXIOMAS OWL / PROTÉGÉ WEBSOCKET (8085)";
            txtHeader.color = new Color(0.0f, 0.85f, 1.0f);
            txtHeader.alignment = TextAlignmentOptions.MidlineLeft;

            // Viewport do ScrollRect
            GameObject viewportObj = new GameObject("Viewport");
            viewportObj.transform.SetParent(painelTerminalLog.transform, false);
            RectTransform rtVp = viewportObj.AddComponent<RectTransform>();
            rtVp.anchorMin = new Vector2(0f, 0f);
            rtVp.anchorMax = new Vector2(1f, 1f);
            rtVp.offsetMin = new Vector2(10f, 8f);
            rtVp.offsetMax = new Vector2(-10f, -30f);

            Image imgVp = viewportObj.AddComponent<Image>();
            imgVp.color = new Color(0f, 0f, 0f, 0.01f);
            RectMask2D mask = viewportObj.AddComponent<RectMask2D>();

            // Content do ScrollRect
            GameObject contentObj = new GameObject("Content");
            contentObj.transform.SetParent(viewportObj.transform, false);
            conteudoScrollTerminal = contentObj.AddComponent<RectTransform>();
            conteudoScrollTerminal.anchorMin = new Vector2(0f, 1f);
            conteudoScrollTerminal.anchorMax = new Vector2(1f, 1f);
            conteudoScrollTerminal.pivot = new Vector2(0.5f, 1f);
            conteudoScrollTerminal.anchoredPosition = Vector2.zero;
            conteudoScrollTerminal.sizeDelta = new Vector2(0f, 140f);

            ContentSizeFitter csf = contentObj.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            textoLogTerminalTMP = contentObj.AddComponent<TextMeshProUGUI>();
            textoLogTerminalTMP.fontSize = 14;
            textoLogTerminalTMP.color = new Color(0.82f, 0.96f, 0.88f);
            textoLogTerminalTMP.alignment = TextAlignmentOptions.BottomLeft;
            textoLogTerminalTMP.richText = true;
            textoLogTerminalTMP.textWrappingMode = TextWrappingModes.Normal;

            scrollRectTerminal = painelTerminalLog.AddComponent<ScrollRect>();
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
        }

        if (modalPlayCanvas != null) modalPlayCanvas.SetActive(true);
        if (painelHUD != null) painelHUD.SetActive(false);
        if (painelTerminalLog != null) painelTerminalLog.SetActive(false);
    }
}
