using PacMan.XR;
using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ============================================================================
/// PAC-MAN SEMÂNTICO (CIn-UFPE)
/// Painel de Resultado Final & Relatório Semântico Agrupado (Vitória / Violação)
/// ============================================================================
/// 1. Modal proporcional ocupando 85% x 85% da viewport (AnchorMin 0.075 / AnchorMax 0.925).
/// 2. Estrutura sem sobreposição via VerticalLayoutGroup (Fundo #050814 95% alpha, borda ciano #00FFFF):
///    - Cabeçalho: Status final, 4 Indicadores Objetivos (HermiT, Triplas, Relações Únicas, Pontuação) e subtítulo.
///    - Corpo Central: Grade de 2 colunas (GridLayoutGroup) com cards das Relações Únicas e Seção de Pendências.
///    - Rodapé: Botões JOGAR NOVAMENTE e FECHAR / MENU com linha dedicada de atalhos.
/// 3. Ocultação total e imediata de todos os HUDs e notificações periféricas durante a tela final.
/// 4. Tipografia 100% compatível com LiberationSans SDF (zero quadradinhos Unicode).
/// ============================================================================
/// </summary>
[DisallowMultipleComponent]
public class PainelGrafoSemanticoProtege : MonoBehaviour
{
    public static PainelGrafoSemanticoProtege Instancia { get; private set; }

    [Header("Configuração de Exibição")]
    public Vector2 tamanhoPainelVR = new Vector2(1632f, 918f);

    private Canvas m_CanvasGrafo;
    private GameObject m_PainelBackdrop;
    private GameObject m_PainelRaiz;
    private Outline m_OutlinePainel;

    // Cabeçalho e Indicadores
    private TMP_Text m_TextoTituloPainel;
    private TMP_Text m_TextoConceitoFoco;
    private TMP_Text m_TextoMetricaHermiTValor;
    private TMP_Text m_TextoMetricaTriplasValor;
    private TMP_Text m_TextoMetricaRelacoesValor;
    private TMP_Text m_TextoMetricaPontosValor;

    // Corpo Central e Cards
    private ScrollRect m_ScrollRelatorio;
    private GameObject m_ContainerGridCards;
    private TMP_Text m_TextoVazio;
    private GameObject m_ContainerSecaoPendentes;
    private GameObject m_ContainerGridPendentes;
    private TMP_Text m_TextoContadorPendentes;

    // Rodapé
    private Button m_BotaoJogarNovamente;
    private Button m_BotaoVoltarMenu;

    // Banner de notificação inferior durante a partida
    private GameObject m_BannerOrbeColetada;
    private TMP_Text m_TextoBannerTitulo;
    private TMP_Text m_TextoBannerDetalhes;
    private float m_TimerBanner = 0f;

    // Dados da partida
    private int m_OrbesColetadas = 0;
    private int m_TotalOrbesMissao = 38;
    private bool m_ExibindoTelaFinal = false;

    public bool ExibindoTelaFinal => m_ExibindoTelaFinal;

    private readonly HashSet<string> m_NosDesatados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    // Pool de Cards para Reuso Eficiente
    private readonly List<CardTriplaRelatorioUI> m_PoolCardsColetados = new List<CardTriplaRelatorioUI>();
    private readonly List<CardTriplaRelatorioUI> m_PoolCardsPendentes = new List<CardTriplaRelatorioUI>();

    // =========================================================================
    // ESTRUTURAS DE DADOS PARA AGRUPAMENTO SEMÂNTICO
    // =========================================================================

    public class TriplaAgrupada
    {
        public string chave;
        public string sujeito;
        public string predicado;
        public string objeto;
        public string axiomaDL;
        public int contagem;
        public int pontosTotais;
        public bool todosConsistentes;
    }

    private class CardTriplaRelatorioUI
    {
        public GameObject cardObj;
        public Image imgFundo;
        public Outline outline;
        public TMP_Text txtLinha1Status;
        public TMP_Text txtLinha2Tripla;
        public TMP_Text txtLinha3Axioma;
    }

    // =========================================================================
    // CICLO DE VIDA DO UNITY
    // =========================================================================

    private void Awake()
    {
        if (Instancia != null && Instancia != this)
        {
            Destroy(gameObject);
            return;
        }
        Instancia = this;
        if (m_PainelRaiz != null) m_PainelRaiz.SetActive(false);
        if (m_PainelBackdrop != null) m_PainelBackdrop.SetActive(false);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void GarantirPainelNaCena()
    {
        if (Instancia == null)
        {
            var painel = FindAnyObjectByType<PainelGrafoSemanticoProtege>();
            if (painel == null)
            {
                GameObject obj = new GameObject("PainelGrafoSemanticoProtege");
                obj.AddComponent<PainelGrafoSemanticoProtege>();
            }
        }
    }

    private void Start()
    {
        ConstruirInterfaceGrafo();

        if (m_PainelRaiz != null) m_PainelRaiz.SetActive(false);
        if (m_PainelBackdrop != null) m_PainelBackdrop.SetActive(false);
    }

    private void Update()
    {
        // 1. Contador do banner inferior sutil durante a partida
        if (m_TimerBanner > 0f)
        {
            m_TimerBanner -= Time.deltaTime;
            if (m_TimerBanner <= 0f && m_BannerOrbeColetada != null)
            {
                m_BannerOrbeColetada.SetActive(false);
            }
        }

        // 2. Atalhos na Tela Final (Mouse, Teclado Enter/R/Esc e Controle VR)
        if (m_ExibindoTelaFinal && m_PainelRaiz != null && m_PainelRaiz.activeSelf)
        {
            bool jogarNovamente = Input.GetKeyDown(KeyCode.R);
            bool voltarMenu = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
                              Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.M) ||
                              Input.GetKeyDown(KeyCode.JoystickButton0) || Input.GetKeyDown(KeyCode.JoystickButton6);

#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb.rKey.wasPressedThisFrame) jogarNovamente = true;
                if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame || kb.mKey.wasPressedThisFrame)
                    voltarMenu = true;
            }
#endif

            var provedorXR = FindAnyObjectByType<MonoBehaviour>() as IProvedorEntradaXR;
            if (provedorXR != null)
            {
                var dados = provedorXR.LerEntradas(0.1f);
                if (dados.Interagir || dados.AlternarMenuHub)
                {
                    voltarMenu = true;
                }
            }

            if (jogarNovamente)
            {
                AcaoJogarNovamente();
            }
            else if (voltarMenu)
            {
                AcaoVoltarMenuPrincipal();
            }
        }
    }

    private void LateUpdate()
    {
        AtualizarAcompanhamentoOlharVR();
    }

    private void AtualizarAcompanhamentoOlharVR()
    {
        if (m_CanvasGrafo == null) return;
        bool visivel = (m_ExibindoTelaFinal && m_PainelRaiz != null && m_PainelRaiz.activeSelf) ||
                       (m_BannerOrbeColetada != null && m_BannerOrbeColetada.activeSelf);

        if (!visivel) return;

        bool emVR = !Application.isEditor && (UnityEngine.XR.XRSettings.isDeviceActive ||
                    (UnityEngine.XR.Management.XRGeneralSettings.Instance != null &&
                     UnityEngine.XR.Management.XRGeneralSettings.Instance.Manager != null &&
                     UnityEngine.XR.Management.XRGeneralSettings.Instance.Manager.activeLoader != null));

        if (emVR)
        {
            Camera cam = Camera.main;
            var ctrl = FindAnyObjectByType<ControladorXRJogador>();
            if (ctrl != null && ctrl.cameraPrincipalXR != null) cam = ctrl.cameraPrincipalXR;
            if (cam == null) return;

            if (m_CanvasGrafo.renderMode != RenderMode.WorldSpace)
            {
                m_CanvasGrafo.renderMode = RenderMode.WorldSpace;
                m_CanvasGrafo.transform.localScale = Vector3.one * 0.00145f;
                if (m_PainelRaiz != null)
                {
                    RectTransform rtJ = m_PainelRaiz.GetComponent<RectTransform>();
                    rtJ.anchorMin = new Vector2(0.5f, 0.5f);
                    rtJ.anchorMax = new Vector2(0.5f, 0.5f);
                    rtJ.pivot = new Vector2(0.5f, 0.5f);
                    rtJ.sizeDelta = tamanhoPainelVR;
                    rtJ.anchoredPosition = Vector2.zero;
                }
            }

            Vector3 posAlvo = cam.transform.position + cam.transform.forward * 2.05f;
            Quaternion rotAlvo = Quaternion.LookRotation(cam.transform.forward, cam.transform.up);

            m_CanvasGrafo.transform.position = Vector3.Lerp(m_CanvasGrafo.transform.position, posAlvo, Time.unscaledDeltaTime * 14f);
            m_CanvasGrafo.transform.rotation = Quaternion.Slerp(m_CanvasGrafo.transform.rotation, rotAlvo, Time.unscaledDeltaTime * 14f);
        }
        else
        {
            if (m_CanvasGrafo.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                m_CanvasGrafo.renderMode = RenderMode.ScreenSpaceOverlay;
                m_CanvasGrafo.transform.localScale = Vector3.one;
                if (m_PainelRaiz != null)
                {
                    RectTransform rtJ = m_PainelRaiz.GetComponent<RectTransform>();
                    rtJ.anchorMin = new Vector2(0.075f, 0.075f);
                    rtJ.anchorMax = new Vector2(0.925f, 0.925f);
                    rtJ.pivot = new Vector2(0.5f, 0.5f);
                    rtJ.offsetMin = Vector2.zero;
                    rtJ.offsetMax = Vector2.zero;
                }
            }
        }
    }

    // =========================================================================
    // CONSTRUÇÃO PROCEDURAL DA INTERFACE MODAL (85% VIEWPORT)
    // =========================================================================

    private void ConstruirInterfaceGrafo()
    {
        // 1. Canvas com CanvasScaler 1920x1080 (Scale With Screen Size)
        GameObject canvasObj = new GameObject("GrafoSemantico_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObj.transform.SetParent(transform, false);

        m_CanvasGrafo = canvasObj.GetComponent<Canvas>();
        m_CanvasGrafo.sortingOrder = 100;

        bool emVR = !Application.isEditor && UnityEngine.XR.XRSettings.isDeviceActive;
        if (emVR)
        {
            m_CanvasGrafo.renderMode = RenderMode.WorldSpace;
            m_CanvasGrafo.transform.localScale = Vector3.one * 0.00145f;
        }
        else
        {
            m_CanvasGrafo.renderMode = RenderMode.ScreenSpaceOverlay;
            m_CanvasGrafo.transform.localScale = Vector3.one;
        }

        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        // 2. Backdrop escuro semi-transparente
        m_PainelBackdrop = new GameObject("Backdrop_Resultado_Final", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        m_PainelBackdrop.transform.SetParent(canvasObj.transform, false);
        RectTransform rtBack = m_PainelBackdrop.GetComponent<RectTransform>();
        rtBack.anchorMin = Vector2.zero;
        rtBack.anchorMax = Vector2.one;
        rtBack.sizeDelta = Vector2.zero;
        m_PainelBackdrop.GetComponent<Image>().color = new Color(0.01f, 0.02f, 0.05f, 0.85f);

        // 3. Painel Modal Principal: 85% x 85% via âncoras proporcionais (0.075 a 0.925), padding 20px
        m_PainelRaiz = new GameObject("Janela_Resultado_Final_85pct", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline), typeof(VerticalLayoutGroup));
        m_PainelRaiz.transform.SetParent(canvasObj.transform, false);

        RectTransform rtJanela = m_PainelRaiz.GetComponent<RectTransform>();
        if (emVR)
        {
            rtJanela.anchorMin = new Vector2(0.5f, 0.5f);
            rtJanela.anchorMax = new Vector2(0.5f, 0.5f);
            rtJanela.pivot = new Vector2(0.5f, 0.5f);
            rtJanela.sizeDelta = tamanhoPainelVR;
        }
        else
        {
            rtJanela.anchorMin = new Vector2(0.075f, 0.075f);
            rtJanela.anchorMax = new Vector2(0.925f, 0.925f);
            rtJanela.pivot = new Vector2(0.5f, 0.5f);
            rtJanela.offsetMin = Vector2.zero;
            rtJanela.offsetMax = Vector2.zero;
        }

        Image imgFundo = m_PainelRaiz.GetComponent<Image>();
        imgFundo.color = new Color(0.020f, 0.031f, 0.078f, 0.95f); // #050814 com 95% de alpha

        m_OutlinePainel = m_PainelRaiz.GetComponent<Outline>();
        m_OutlinePainel.effectColor = new Color(0.0f, 1.0f, 1.0f, 0.95f); // #00FFFF
        m_OutlinePainel.effectDistance = new Vector2(2f, -2f);

        VerticalLayoutGroup vlgModal = m_PainelRaiz.GetComponent<VerticalLayoutGroup>();
        vlgModal.padding = new RectOffset(20, 20, 16, 16);
        vlgModal.spacing = 12f;
        vlgModal.childAlignment = TextAnchor.UpperCenter;
        vlgModal.childControlWidth = true;
        vlgModal.childControlHeight = true;
        vlgModal.childForceExpandWidth = true;
        vlgModal.childForceExpandHeight = false;

        // =====================================================================
        // FAIXA 1: CABEÇALHO (~18% da altura) COM 4 INDICADORES OBJETIVOS
        // =====================================================================
        GameObject headerObj = new GameObject("Header_Resultado", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        headerObj.transform.SetParent(m_PainelRaiz.transform, false);

        VerticalLayoutGroup vlgHeader = headerObj.GetComponent<VerticalLayoutGroup>();
        vlgHeader.spacing = 8f;
        vlgHeader.padding = new RectOffset(6, 6, 2, 6);
        vlgHeader.childAlignment = TextAnchor.MiddleCenter;
        vlgHeader.childControlWidth = true;
        vlgHeader.childControlHeight = true;
        vlgHeader.childForceExpandWidth = true;
        vlgHeader.childForceExpandHeight = false;

        LayoutElement leHeader = headerObj.GetComponent<LayoutElement>();
        leHeader.minHeight = 152f;
        leHeader.preferredHeight = 160f;
        leHeader.flexibleHeight = 0f;

        // Título Principal
        m_TextoTituloPainel = CriarTextoLayout(headerObj.transform, "Titulo_Resultado",
            "ONTOLOGIA VALIDADA - CONSISTENTE",
            30f, FontStyles.Bold, new Color(0.0f, 1.0f, 0.533f), 38f);

        // Linha com os 4 Indicadores Objetivos (Cards/Pills Lado a Lado)
        GameObject rowMetricas = new GameObject("Linha_4_Metricas", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        rowMetricas.transform.SetParent(headerObj.transform, false);

        HorizontalLayoutGroup hlgMetricas = rowMetricas.GetComponent<HorizontalLayoutGroup>();
        hlgMetricas.spacing = 14f;
        hlgMetricas.childAlignment = TextAnchor.MiddleCenter;
        hlgMetricas.childControlWidth = true;
        hlgMetricas.childControlHeight = true;
        hlgMetricas.childForceExpandWidth = true;
        hlgMetricas.childForceExpandHeight = false;

        LayoutElement leMetricas = rowMetricas.GetComponent<LayoutElement>();
        leMetricas.minHeight = 52f;
        leMetricas.preferredHeight = 54f;
        leMetricas.flexibleHeight = 0f;

        // Card 1: Status Reasoner HermiT
        var card1 = CriarBoxMetrica(rowMetricas.transform, "Box_HermiT", "REASONER HERMIT", "100% CONSISTENTE", new Color(0.290f, 0.871f, 0.502f));
        m_TextoMetricaHermiTValor = card1.txtValor;

        // Card 2: Triplas Coletadas
        var card2 = CriarBoxMetrica(rowMetricas.transform, "Box_Triplas", "TRIPLAS COLETADAS", "38 / 38 (100%)", new Color(0.404f, 0.910f, 0.976f));
        m_TextoMetricaTriplasValor = card2.txtValor;

        // Card 3: Relações Únicas Validadas
        var card3 = CriarBoxMetrica(rowMetricas.transform, "Box_Relacoes", "RELACOES UNICAS", "6 Relacoes", new Color(0.980f, 0.800f, 0.082f));
        m_TextoMetricaRelacoesValor = card3.txtValor;

        // Card 4: Pontuação Total
        var card4 = CriarBoxMetrica(rowMetricas.transform, "Box_Pontuacao", "PONTUACAO TOTAL", "0 pts", Color.white);
        m_TextoMetricaPontosValor = card4.txtValor;

        // Subtítulo descritivo
        m_TextoConceitoFoco = CriarTextoLayout(headerObj.transform, "Subtitulo_Diagnostico",
            "Relatorio estruturado das relacoes ontologicas unicas sintetizadas durante a sessao.",
            15f, FontStyles.Normal, new Color(0.886f, 0.910f, 0.941f), 24f);

        CriarDivisorLayout(headerObj.transform, new Color(0.0f, 1.0f, 1.0f, 0.45f));

        // =====================================================================
        // FAIXA 2: CORPO CENTRAL (ScrollRect com Grade de 2 Colunas e Cards)
        // =====================================================================
        GameObject scrollArea = new GameObject("ScrollRect_Relatorio_Triplas", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(ScrollRect), typeof(Outline), typeof(LayoutElement));
        scrollArea.transform.SetParent(m_PainelRaiz.transform, false);

        LayoutElement leScroll = scrollArea.GetComponent<LayoutElement>();
        leScroll.minHeight = 280f;
        leScroll.flexibleHeight = 1f;

        scrollArea.GetComponent<Image>().color = new Color(0.012f, 0.020f, 0.050f, 0.92f);
        Outline outlineScroll = scrollArea.GetComponent<Outline>();
        outlineScroll.effectColor = new Color(0.0f, 1.0f, 1.0f, 0.35f);
        outlineScroll.effectDistance = new Vector2(1f, -1f);

        // Viewport com RectMask2D
        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        viewport.transform.SetParent(scrollArea.transform, false);
        RectTransform rtViewport = viewport.GetComponent<RectTransform>();
        rtViewport.anchorMin = Vector2.zero;
        rtViewport.anchorMax = Vector2.one;
        rtViewport.offsetMin = new Vector2(4f, 4f);
        rtViewport.offsetMax = new Vector2(-28f, -4f);

        // Content
        GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        RectTransform rtContent = content.GetComponent<RectTransform>();
        rtContent.anchorMin = new Vector2(0f, 1f);
        rtContent.anchorMax = new Vector2(1f, 1f);
        rtContent.pivot = new Vector2(0.5f, 1f);
        rtContent.anchoredPosition = Vector2.zero;
        rtContent.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup vlgContent = content.GetComponent<VerticalLayoutGroup>();
        vlgContent.childControlWidth = true;
        vlgContent.childControlHeight = true;
        vlgContent.childForceExpandWidth = true;
        vlgContent.childForceExpandHeight = false;
        vlgContent.padding = new RectOffset(16, 16, 12, 16);
        vlgContent.spacing = 12f;

        ContentSizeFitter csf = content.GetComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Título da Seção de Relações Únicas
        CriarTextoLayout(content.transform, "Titulo_Secao_Coletadas",
            "RELACOES ONTOLOGICAS UNICAS SINTETIZADAS (ABox)",
            16.5f, FontStyles.Bold, new Color(0.404f, 0.910f, 0.976f), 26f);

        // Container do GridLayoutGroup de 2 Colunas para os Cards
        m_ContainerGridCards = new GameObject("Container_Grid_Cards", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
        m_ContainerGridCards.transform.SetParent(content.transform, false);

        GridLayoutGroup glg = m_ContainerGridCards.GetComponent<GridLayoutGroup>();
        glg.cellSize = new Vector2(755f, 96f);
        glg.spacing = new Vector2(16f, 10f);
        glg.startCorner = GridLayoutGroup.Corner.UpperLeft;
        glg.startAxis = GridLayoutGroup.Axis.Horizontal;
        glg.childAlignment = TextAnchor.UpperLeft;
        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = 2;

        ContentSizeFitter csfGrid = m_ContainerGridCards.GetComponent<ContentSizeFitter>();
        csfGrid.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csfGrid.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Texto informativo caso nenhuma tripla tenha sido coletada
        GameObject txtVazioObj = new GameObject("Texto_Vazio", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        txtVazioObj.transform.SetParent(content.transform, false);
        m_TextoVazio = txtVazioObj.GetComponent<TextMeshProUGUI>();
        m_TextoVazio.fontSize = 18f;
        m_TextoVazio.alignment = TextAlignmentOptions.Center;
        m_TextoVazio.color = new Color(0.980f, 0.800f, 0.082f);
        m_TextoVazio.text = "Nenhuma Tripla RDF foi sintetizada antes do encerramento da partida.";
        txtVazioObj.SetActive(false);

        // Seção Separada de Pendentes (Violação Ontológica)
        m_ContainerSecaoPendentes = new GameObject("Secao_Pendentes_Incompletas", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        m_ContainerSecaoPendentes.transform.SetParent(content.transform, false);

        VerticalLayoutGroup vlgPend = m_ContainerSecaoPendentes.GetComponent<VerticalLayoutGroup>();
        vlgPend.childControlWidth = true;
        vlgPend.childControlHeight = true;
        vlgPend.childForceExpandWidth = true;
        vlgPend.childForceExpandHeight = false;
        vlgPend.padding = new RectOffset(0, 0, 10, 8);
        vlgPend.spacing = 10f;

        ContentSizeFitter csfPend = m_ContainerSecaoPendentes.GetComponent<ContentSizeFitter>();
        csfPend.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csfPend.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        CriarDivisorLayout(m_ContainerSecaoPendentes.transform, new Color(0.980f, 0.800f, 0.082f, 0.55f));

        CriarTextoLayout(m_ContainerSecaoPendentes.transform, "Titulo_Secao_Pendentes",
            "=== TRIPLAS / CLASSES PENDENTES (GRAFO INCOMPLETO) ===",
            17f, FontStyles.Bold, new Color(0.980f, 0.800f, 0.082f), 26f);

        m_TextoContadorPendentes = CriarTextoLayout(m_ContainerSecaoPendentes.transform, "Subtitulo_Pendentes",
            "As seguintes classes e relacoes da ontologia ficaram pendentes para o fechamento consistente:",
            14.5f, FontStyles.Normal, new Color(0.886f, 0.910f, 0.941f), 22f);

        m_ContainerGridPendentes = new GameObject("Container_Grid_Pendentes", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
        m_ContainerGridPendentes.transform.SetParent(m_ContainerSecaoPendentes.transform, false);

        GridLayoutGroup glgPend = m_ContainerGridPendentes.GetComponent<GridLayoutGroup>();
        glgPend.cellSize = new Vector2(755f, 88f);
        glgPend.spacing = new Vector2(16f, 10f);
        glgPend.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        glgPend.constraintCount = 2;

        ContentSizeFitter csfGridPend = m_ContainerGridPendentes.GetComponent<ContentSizeFitter>();
        csfGridPend.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csfGridPend.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        m_ContainerSecaoPendentes.SetActive(false);

        // Scrollbar vertical
        GameObject scrollbarObj = new GameObject("Scrollbar_Vertical", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Scrollbar));
        scrollbarObj.transform.SetParent(scrollArea.transform, false);
        RectTransform rtSb = scrollbarObj.GetComponent<RectTransform>();
        rtSb.anchorMin = new Vector2(1f, 0f);
        rtSb.anchorMax = new Vector2(1f, 1f);
        rtSb.pivot = new Vector2(1f, 0.5f);
        rtSb.anchoredPosition = new Vector2(-8f, 0f);
        rtSb.sizeDelta = new Vector2(14f, -16f);
        scrollbarObj.GetComponent<Image>().color = new Color(0.08f, 0.12f, 0.22f, 0.85f);

        GameObject handleArea = new GameObject("Sliding Area", typeof(RectTransform));
        handleArea.transform.SetParent(scrollbarObj.transform, false);
        RectTransform rtHa = handleArea.GetComponent<RectTransform>();
        rtHa.anchorMin = Vector2.zero;
        rtHa.anchorMax = Vector2.one;
        rtHa.sizeDelta = Vector2.zero;

        GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        handle.transform.SetParent(handleArea.transform, false);
        RectTransform rtHandle = handle.GetComponent<RectTransform>();
        rtHandle.sizeDelta = Vector2.zero;
        Image imgHandle = handle.GetComponent<Image>();
        imgHandle.color = new Color(0.404f, 0.910f, 0.976f, 0.85f); // #67E8F9

        Scrollbar sb = scrollbarObj.GetComponent<Scrollbar>();
        sb.handleRect = rtHandle;
        sb.targetGraphic = imgHandle;
        sb.direction = Scrollbar.Direction.BottomToTop;

        m_ScrollRelatorio = scrollArea.GetComponent<ScrollRect>();
        m_ScrollRelatorio.viewport = rtViewport;
        m_ScrollRelatorio.content = rtContent;
        m_ScrollRelatorio.horizontal = false;
        m_ScrollRelatorio.vertical = true;
        m_ScrollRelatorio.scrollSensitivity = 35f;
        m_ScrollRelatorio.movementType = ScrollRect.MovementType.Clamped;
        m_ScrollRelatorio.verticalScrollbar = sb;
        m_ScrollRelatorio.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

        // =====================================================================
        // FAIXA 3: RODAPÉ (~18% da altura - Botões + Linha de Atalhos)
        // =====================================================================
        GameObject footerObj = new GameObject("Footer_Resultado", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        footerObj.transform.SetParent(m_PainelRaiz.transform, false);

        VerticalLayoutGroup vlgFooter = footerObj.GetComponent<VerticalLayoutGroup>();
        vlgFooter.spacing = 10f;
        vlgFooter.padding = new RectOffset(8, 8, 6, 2);
        vlgFooter.childAlignment = TextAnchor.MiddleCenter;
        vlgFooter.childControlWidth = true;
        vlgFooter.childControlHeight = true;
        vlgFooter.childForceExpandWidth = true;
        vlgFooter.childForceExpandHeight = false;

        LayoutElement leFooter = footerObj.GetComponent<LayoutElement>();
        leFooter.minHeight = 122f;
        leFooter.preferredHeight = 128f;
        leFooter.flexibleHeight = 0f;

        CriarDivisorLayout(footerObj.transform, new Color(0.0f, 1.0f, 1.0f, 0.35f));

        GameObject botoesRow = new GameObject("Linha_Botoes_Rodape", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        botoesRow.transform.SetParent(footerObj.transform, false);

        HorizontalLayoutGroup hlgBotoes = botoesRow.GetComponent<HorizontalLayoutGroup>();
        hlgBotoes.spacing = 40f;
        hlgBotoes.childAlignment = TextAnchor.MiddleCenter;
        hlgBotoes.childControlWidth = false;
        hlgBotoes.childControlHeight = false;
        hlgBotoes.childForceExpandWidth = false;
        hlgBotoes.childForceExpandHeight = false;

        LayoutElement leRow = botoesRow.GetComponent<LayoutElement>();
        leRow.minHeight = 58f;
        leRow.preferredHeight = 60f;

        m_BotaoJogarNovamente = CriarBotaoRodapeLayout(
            botoesRow.transform, "Botao_Jogar_Novamente",
            "JOGAR NOVAMENTE",
            new Vector2(360f, 58f),
            new Color(0.05f, 0.45f, 0.25f, 0.95f),
            new Color(0.290f, 0.871f, 0.502f, 1f), // #4ADE80
            AcaoJogarNovamente);

        m_BotaoVoltarMenu = CriarBotaoRodapeLayout(
            botoesRow.transform, "Botao_Fechar_Menu",
            "FECHAR / MENU",
            new Vector2(360f, 58f),
            new Color(0.06f, 0.22f, 0.42f, 0.95f),
            new Color(0.404f, 0.910f, 0.976f, 1f), // #67E8F9
            AcaoVoltarMenuPrincipal);

        CriarTextoLayout(footerObj.transform, "Txt_Atalhos_Rodape",
            "[Clique] Selecionar   |   [R] Jogar Novamente   |   [ENTER / ESC / A] Fechar / Menu   |   Role para inspecionar",
            17f, FontStyles.Normal,
            new Color(0.886f, 0.910f, 0.941f, 0.85f), 26f);

        // Banner inferior sutil durante gameplay
        ConstruirBannerInferior(canvasObj.transform);
    }

    private void ConstruirBannerInferior(Transform paiCanvas)
    {
        m_BannerOrbeColetada = new GameObject("Banner_Notificacao_Orbe", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
        m_BannerOrbeColetada.transform.SetParent(paiCanvas, false);

        RectTransform rtBanner = m_BannerOrbeColetada.GetComponent<RectTransform>();
        rtBanner.anchorMin = new Vector2(0.5f, 0f);
        rtBanner.anchorMax = new Vector2(0.5f, 0f);
        rtBanner.pivot = new Vector2(0.5f, 0f);
        rtBanner.anchoredPosition = new Vector2(0f, 32f);
        rtBanner.sizeDelta = new Vector2(560f, 52f);

        Image imgBanner = m_BannerOrbeColetada.GetComponent<Image>();
        imgBanner.color = new Color(0.02f, 0.03f, 0.08f, 0.92f);

        Outline outline = m_BannerOrbeColetada.GetComponent<Outline>();
        outline.effectColor = new Color(0.0f, 1.0f, 1.0f, 0.75f);

        m_TextoBannerTitulo = CriarTextoCentro(m_BannerOrbeColetada.transform, "Texto_Banner_Titulo",
            "<b>* TRIPLA RDF SINTETIZADA (ABox) *</b>", 13f, FontStyles.Bold,
            new Vector2(0f, 11f), new Vector2(530f, 20f), new Color(0.290f, 0.871f, 0.502f));

        m_TextoBannerDetalhes = CriarTextoCentro(m_BannerOrbeColetada.transform, "Texto_Banner_Detalhes",
            "Instancia ontologica conectada ao reasoner.", 13f, FontStyles.Normal,
            new Vector2(0f, -10f), new Vector2(530f, 20f), Color.white);

        m_BannerOrbeColetada.SetActive(false);
    }

    // =========================================================================
    // EXIBIÇÃO E PREENCHIMENTO DO RELATÓRIO FINAL
    // =========================================================================

    public void ExibirGrafoFinal(bool vitoria, int orbesColetadas, int totalOrbes, int pontuacao)
    {
        m_ExibindoTelaFinal = true;
        m_OrbesColetadas = orbesColetadas;
        m_TotalOrbesMissao = totalOrbes > 0 ? totalOrbes : 38;

        if (m_PainelRaiz == null)
        {
            ConstruirInterfaceGrafo();
        }

        // 1. Pausa o jogo
        Time.timeScale = 0f;

        // 2. Ocultação total e imediata de todos os HUDs e avisos periféricos
        OcultarTodosHUDsGameplay();

        if (m_PainelBackdrop != null) m_PainelBackdrop.SetActive(true);
        if (m_PainelRaiz != null) m_PainelRaiz.SetActive(true);

        // 3. Preenche relatório agrupado em cards e métricas
        PreencherRelatorioTriplas(vitoria);

        if (m_ScrollRelatorio != null)
        {
            Canvas.ForceUpdateCanvases();
            m_ScrollRelatorio.verticalNormalizedPosition = 1f;
        }
    }

    private void PreencherRelatorioTriplas(bool vitoria)
    {
        var historico = PainelInventarioABoxVR.Instancia != null ? PainelInventarioABoxVR.Instancia.HistoricoTriplas : null;
        var triplasAgrupadas = new Dictionary<string, TriplaAgrupada>();

        if (historico != null)
        {
            for (int i = 0; i < historico.Count; i++)
            {
                var t = historico[i];
                string chave = $"{t.sujeito}|{t.predicado}|{t.objeto}";
                if (!triplasAgrupadas.TryGetValue(chave, out var agrupada))
                {
                    agrupada = new TriplaAgrupada
                    {
                        chave = chave,
                        sujeito = t.sujeito,
                        predicado = t.predicado,
                        objeto = t.objeto,
                        axiomaDL = t.axiomaDL,
                        contagem = 0,
                        pontosTotais = 0,
                        todosConsistentes = true
                    };
                    triplasAgrupadas[chave] = agrupada;
                }
                agrupada.contagem++;
                agrupada.pontosTotais += t.pontos;
                if (!t.consistente) agrupada.todosConsistentes = false;
            }
        }

        // 1. Popula ou recicla cards no Grid de Coletadas
        int idxCard = 0;
        foreach (var par in triplasAgrupadas)
        {
            CardTriplaRelatorioUI cardUI;
            if (idxCard < m_PoolCardsColetados.Count)
            {
                cardUI = m_PoolCardsColetados[idxCard];
            }
            else
            {
                cardUI = CriarCardTripla(m_ContainerGridCards.transform, false);
                m_PoolCardsColetados.Add(cardUI);
            }

            ConfigurarCardColetado(cardUI, par.Value);
            idxCard++;
        }

        // Desativa cards excedentes no pool
        for (int i = idxCard; i < m_PoolCardsColetados.Count; i++)
        {
            m_PoolCardsColetados[i].cardObj.SetActive(false);
        }

        if (m_TextoVazio != null)
        {
            m_TextoVazio.gameObject.SetActive(triplasAgrupadas.Count == 0);
        }

        // 2. Seção de Pendentes (em caso de Violação Ontológica)
        if (!vitoria)
        {
            m_ContainerSecaoPendentes.SetActive(true);

            List<string> faltantes = GerenciadorCenarioSemantico.Instancia != null
                ? GerenciadorCenarioSemantico.Instancia.ObterClassesTriplasFaltantes()
                : null;

            var pendentesAgrupados = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (faltantes != null && faltantes.Count > 0)
            {
                for (int i = 0; i < faltantes.Count; i++)
                {
                    string nomeClasse = ExtrairNomeClassePendente(faltantes[i]);
                    if (!pendentesAgrupados.ContainsKey(nomeClasse))
                    {
                        pendentesAgrupados[nomeClasse] = 0;
                    }
                    pendentesAgrupados[nomeClasse]++;
                }
            }

            int idxPendente = 0;
            foreach (var par in pendentesAgrupados)
            {
                CardTriplaRelatorioUI cardUI;
                if (idxPendente < m_PoolCardsPendentes.Count)
                {
                    cardUI = m_PoolCardsPendentes[idxPendente];
                }
                else
                {
                    cardUI = CriarCardTripla(m_ContainerGridPendentes.transform, true);
                    m_PoolCardsPendentes.Add(cardUI);
                }

                ConfigurarCardPendente(cardUI, par.Key, par.Value);
                idxPendente++;
            }

            for (int i = idxPendente; i < m_PoolCardsPendentes.Count; i++)
            {
                m_PoolCardsPendentes[i].cardObj.SetActive(false);
            }

            if (m_TextoContadorPendentes != null)
            {
                int totalFaltam = faltantes != null ? faltantes.Count : 0;
                m_TextoContadorPendentes.text = $"<color=#FACC15>Foram identificadas <b>{pendentesAgrupados.Count} classes unicas pendentes</b> (totalizando {totalFaltam} instancias nao sintetizadas):</color>";
            }
        }
        else
        {
            m_ContainerSecaoPendentes.SetActive(false);
        }

        // 3. Atualizar Indicadores do Cabeçalho
        AtualizarIndicadoresCabecalho(vitoria, m_OrbesColetadas, m_TotalOrbesMissao, InstanciaSemanticaBase.pontuacaoLogica, triplasAgrupadas.Count);
    }

    private void AtualizarIndicadoresCabecalho(bool vitoria, int orbesColetadas, int totalOrbes, int pontuacao, int relacoesUnicas)
    {
        if (vitoria)
        {
            if (m_OutlinePainel != null)
                m_OutlinePainel.effectColor = new Color(0.0f, 1.0f, 0.533f, 0.95f); // #00FF88

            if (m_TextoTituloPainel != null)
                m_TextoTituloPainel.text = "<color=#00FF88>ONTOLOGIA VALIDADA - CONSISTENTE</color>";

            if (m_TextoMetricaHermiTValor != null)
                m_TextoMetricaHermiTValor.text = "<color=#4ADE80><b>100% CONSISTENTE</b></color>";

            if (m_TextoMetricaTriplasValor != null)
                m_TextoMetricaTriplasValor.text = $"<color=#67E8F9><b>{totalOrbes} / {totalOrbes} (100%)</b></color>";

            if (m_TextoMetricaRelacoesValor != null)
                m_TextoMetricaRelacoesValor.text = $"<color=#FACC15><b>{relacoesUnicas} Relacoes</b></color>";

            if (m_TextoMetricaPontosValor != null)
                m_TextoMetricaPontosValor.text = $"<color=#FFFFFF><b>{pontuacao} pts</b></color>";

            if (m_TextoConceitoFoco != null)
                m_TextoConceitoFoco.text = "<color=#E2E8F0>Todas as Triplas RDF da ABox foram sintetizadas e validadas sem violacoes axiomaticas pelo Reasoner HermiT.</color>";
        }
        else
        {
            if (m_OutlinePainel != null)
                m_OutlinePainel.effectColor = new Color(1.0f, 0.302f, 0.302f, 0.95f); // #FF4D4D

            if (m_TextoTituloPainel != null)
                m_TextoTituloPainel.text = "<color=#FF4D4D>VIOLACAO ONTOLOGICA - GRAFO INCOMPLETO</color>";

            if (m_TextoMetricaHermiTValor != null)
                m_TextoMetricaHermiTValor.text = "<color=#FF4D4D><b>VIOLACAO / INCOMPLETO</b></color>";

            float pct = totalOrbes > 0 ? (orbesColetadas * 100f / totalOrbes) : 0f;
            if (m_TextoMetricaTriplasValor != null)
                m_TextoMetricaTriplasValor.text = $"<color=#FACC15><b>{orbesColetadas} / {totalOrbes} ({pct:F0}%)</b></color>";

            if (m_TextoMetricaRelacoesValor != null)
                m_TextoMetricaRelacoesValor.text = $"<color=#FACC15><b>{relacoesUnicas} Relacoes</b></color>";

            if (m_TextoMetricaPontosValor != null)
                m_TextoMetricaPontosValor.text = $"<color=#FFFFFF><b>{pontuacao} pts</b></color>";

            if (m_TextoConceitoFoco != null)
                m_TextoConceitoFoco.text = "<color=#E2E8F0>Abaixo estao as relacoes unicas registradas na ABox e as classes pendentes para fechar o grafo.</color>";
        }
    }

    private CardTriplaRelatorioUI CriarCardTripla(Transform pai, bool ehPendente)
    {
        GameObject cardObj = new GameObject(ehPendente ? "Card_Tripla_Pendente" : "Card_Tripla_Coletada",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline), typeof(VerticalLayoutGroup));
        cardObj.transform.SetParent(pai, false);

        RectTransform rt = cardObj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(755f, ehPendente ? 88f : 96f);

        Image img = cardObj.GetComponent<Image>();
        img.color = new Color(0.043f, 0.086f, 0.133f, 0.96f); // #0B1622 (96% alpha)

        Outline outline = cardObj.GetComponent<Outline>();
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        outline.effectColor = ehPendente
            ? new Color(0.980f, 0.800f, 0.082f, 0.75f)
            : new Color(0.0f, 1.0f, 0.533f, 0.75f);

        VerticalLayoutGroup vlg = cardObj.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(14, 14, 9, 9);
        vlg.spacing = 3f;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childAlignment = TextAnchor.UpperLeft;

        // Linha 1 (Topo)
        GameObject l1 = new GameObject("Linha1_Status", typeof(RectTransform), typeof(TextMeshProUGUI));
        l1.transform.SetParent(cardObj.transform, false);
        RectTransform rt1 = l1.GetComponent<RectTransform>();
        rt1.sizeDelta = new Vector2(0f, 20f);
        TMP_Text txt1 = l1.GetComponent<TextMeshProUGUI>();
        txt1.fontSize = 13.5f;
        txt1.fontStyle = FontStyles.Bold;
        txt1.alignment = TextAlignmentOptions.Left;
        txt1.textWrappingMode = TextWrappingModes.NoWrap;
        txt1.overflowMode = TextOverflowModes.Ellipsis;
        txt1.raycastTarget = false;

        // Linha 2 (Destaque Tripla)
        GameObject l2 = new GameObject("Linha2_Tripla", typeof(RectTransform), typeof(TextMeshProUGUI));
        l2.transform.SetParent(cardObj.transform, false);
        RectTransform rt2 = l2.GetComponent<RectTransform>();
        rt2.sizeDelta = new Vector2(0f, 25f);
        TMP_Text txt2 = l2.GetComponent<TextMeshProUGUI>();
        txt2.fontSize = 16.5f;
        txt2.fontStyle = FontStyles.Bold;
        txt2.alignment = TextAlignmentOptions.Left;
        txt2.textWrappingMode = TextWrappingModes.Normal;
        txt2.overflowMode = TextOverflowModes.Ellipsis;
        txt2.raycastTarget = false;

        // Linha 3 (Axioma DL)
        GameObject l3 = new GameObject("Linha3_Axioma", typeof(RectTransform), typeof(TextMeshProUGUI));
        l3.transform.SetParent(cardObj.transform, false);
        RectTransform rt3 = l3.GetComponent<RectTransform>();
        rt3.sizeDelta = new Vector2(0f, 20f);
        TMP_Text txt3 = l3.GetComponent<TextMeshProUGUI>();
        txt3.fontSize = 12.5f;
        txt3.fontStyle = FontStyles.Normal;
        txt3.color = new Color(0.580f, 0.639f, 0.722f); // #94A3B8
        txt3.alignment = TextAlignmentOptions.Left;
        txt3.textWrappingMode = TextWrappingModes.Normal;
        txt3.overflowMode = TextOverflowModes.Ellipsis;
        txt3.raycastTarget = false;

        return new CardTriplaRelatorioUI
        {
            cardObj = cardObj,
            imgFundo = img,
            outline = outline,
            txtLinha1Status = txt1,
            txtLinha2Tripla = txt2,
            txtLinha3Axioma = txt3
        };
    }

    private void ConfigurarCardColetado(CardTriplaRelatorioUI cardUI, TriplaAgrupada agrupada)
    {
        cardUI.cardObj.SetActive(true);
        bool ok = agrupada.todosConsistentes;
        cardUI.outline.effectColor = ok
            ? new Color(0.0f, 1.0f, 0.533f, 0.85f)
            : new Color(1.0f, 0.302f, 0.302f, 0.85f);

        string statusBadge = ok
            ? "<color=#4ADE80><b>[OK CONSISTENTE]</b></color>"
            : "<color=#FF4D4D><b>[ERRO INCONSISTENTE]</b></color>";
        string instanciasBadge = $"<color=#FACC15><b>(x{agrupada.contagem} instâncias)</b></color>";
        string ptsBadge = agrupada.pontosTotais >= 0
            ? $"<color=#94A3B8>+{agrupada.pontosTotais} pts</color>"
            : $"<color=#FF4D4D>{agrupada.pontosTotais} pts</color>";

        cardUI.txtLinha1Status.text = $"{statusBadge}   {instanciasBadge}   {ptsBadge}";

        string suj = SanitizarTextoAsciiTMP(agrupada.sujeito.Trim('[', ']'));
        string pred = SanitizarTextoAsciiTMP(agrupada.predicado.Trim('[', ']'));
        string obj = SanitizarTextoAsciiTMP(agrupada.objeto.Trim('[', ']'));

        cardUI.txtLinha2Tripla.text = $"<color=#FFFFFF><b>{suj}</b></color> <color=#67E8F9>--[{pred}]-></color> <color=#FFFFFF><b>{obj}</b></color>";

        string ax = SanitizarTextoAsciiTMP(agrupada.axiomaDL);
        cardUI.txtLinha3Axioma.text = $"<color=#94A3B8>Axioma DL: {ax}</color>";
    }

    private void ConfigurarCardPendente(CardTriplaRelatorioUI cardUI, string nomeClasse, int quantidadePendentes)
    {
        cardUI.cardObj.SetActive(true);
        cardUI.outline.effectColor = new Color(0.980f, 0.800f, 0.082f, 0.85f); // #FACC15

        var tripla = PainelInventarioABoxVR.FormatarTriplaSemantica(nomeClasse, "", false);

        string badgeStatus = "<color=#FACC15><b>[PENDENTE]</b></color>";
        string badgeInstancias = $"<color=#E2E8F0><b>(x{quantidadePendentes} instâncias pendentes)</b></color>";
        cardUI.txtLinha1Status.text = $"{badgeStatus}   {badgeInstancias}";

        string suj = SanitizarTextoAsciiTMP(tripla.sujeito.Trim('[', ']'));
        string pred = SanitizarTextoAsciiTMP(tripla.predicado.Trim('[', ']'));
        string obj = SanitizarTextoAsciiTMP(tripla.objeto.Trim('[', ']'));

        cardUI.txtLinha2Tripla.text = $"<color=#FFFFFF><b>{suj}</b></color> <color=#67E8F9>--[{pred}]-></color> <color=#FFFFFF><b>{obj}</b></color>";

        string ax = SanitizarTextoAsciiTMP(tripla.axioma);
        cardUI.txtLinha3Axioma.text = $"<color=#94A3B8>Axioma DL: {ax}</color>";
    }

    private static string ExtrairNomeClassePendente(string item)
    {
        if (string.IsNullOrEmpty(item)) return "Desconhecido";
        string s = item.Trim();
        int idxEspaco = s.IndexOf(' ');
        if (idxEspaco >= 0 && idxEspaco < s.Length - 1)
        {
            s = s.Substring(idxEspaco + 1).Trim();
        }
        int idxParentese = s.IndexOf('(');
        if (idxParentese >= 0)
        {
            s = s.Substring(0, idxParentese).Trim();
        }
        return s;
    }

    // =========================================================================
    // OCULTAÇÃO E RESTAURAÇÃO DE HUDS PERIFÉRICOS
    // =========================================================================

    private void OcultarTodosHUDsGameplay()
    {
        if (m_BannerOrbeColetada != null) m_BannerOrbeColetada.SetActive(false);

        if (MinimapaHUDVR.Instancia != null)
        {
            MinimapaHUDVR.Instancia.DefinirAtivacaoHUD(false);
            MinimapaHUDVR.Instancia.EsconderWarning();
        }

        if (PainelInventarioABoxVR.Instancia != null)
        {
            PainelInventarioABoxVR.Instancia.DefinirVisibilidadePainel(false);
        }

        if (HUDGameplayPeriferico.Instancia != null)
        {
            HUDGameplayPeriferico.Instancia.exibirHUD = false;
            if (HUDGameplayPeriferico.Instancia.gameObject != null)
            {
                HUDGameplayPeriferico.Instancia.gameObject.SetActive(false);
            }
        }

        SemanticHUDManager.exibirHUDGeral = false;
        if (SemanticHUDManager.Instancia != null)
        {
            if (SemanticHUDManager.Instancia.painelHUD != null) SemanticHUDManager.Instancia.painelHUD.SetActive(false);
            if (SemanticHUDManager.Instancia.painelTerminalLog != null) SemanticHUDManager.Instancia.painelTerminalLog.SetActive(false);
            if (SemanticHUDManager.Instancia.painelExplicativoJogo != null) SemanticHUDManager.Instancia.painelExplicativoJogo.SetActive(false);
        }

        var hudHolo = FindAnyObjectByType<HUDHolograficoXR>();
        if (hudHolo != null && hudHolo.gameObject != null)
        {
            hudHolo.gameObject.SetActive(false);
        }
    }

    private void RestaurarHUDsGameplay()
    {
        if (HUDGameplayPeriferico.Instancia != null)
        {
            HUDGameplayPeriferico.Instancia.exibirHUD = true;
            if (HUDGameplayPeriferico.Instancia.gameObject != null)
            {
                HUDGameplayPeriferico.Instancia.gameObject.SetActive(true);
            }
        }
        if (MinimapaHUDVR.Instancia != null)
        {
            MinimapaHUDVR.Instancia.DefinirAtivacaoHUD(true);
        }
        if (PainelInventarioABoxVR.Instancia != null)
        {
            PainelInventarioABoxVR.Instancia.DefinirVisibilidadePainel(true);
        }
    }

    // =========================================================================
    // API PÚBLICA E CONTROLES
    // =========================================================================

    public void NotificarConsumoOntologico(string idInstancia, string nomeClasse)
    {
        if (!string.IsNullOrEmpty(nomeClasse)) m_NosDesatados.Add(nomeClasse.Trim());
        if (!string.IsNullOrEmpty(idInstancia)) m_NosDesatados.Add(idInstancia.Trim());
    }

    public void ExibirBannerOrbeColetada(string nomeClasse, string idInstancia, string expressaoDL, bool ehOrbeDePoder, int indiceOrbe = 0, int totalOrbes = 38)
    {
        if (m_BannerOrbeColetada == null) return;

        if (m_TextoBannerTitulo != null)
        {
            string tipo = ehOrbeDePoder ? "* TRIPLA RDF DE PODER (ABox) *" : "* TRIPLA RDF SINTETIZADA (ABox) *";
            string cor = ehOrbeDePoder ? "#FACC15" : "#4ADE80";
            m_TextoBannerTitulo.text = $"<color={cor}><b>{tipo}</b></color>";
        }

        if (m_TextoBannerDetalhes != null)
        {
            string dlLimpo = SanitizarTextoAsciiTMP(expressaoDL);
            m_TextoBannerDetalhes.text = $"<color=#67E8F9><b>{nomeClasse}</b></color>  |  <color=#E2E8F0>{dlLimpo}</color>";
        }

        m_BannerOrbeColetada.SetActive(true);
        m_TimerBanner = 2.5f;
    }

    public static string SanitizarTextoAsciiTMP(string texto)
    {
        if (string.IsNullOrEmpty(texto)) return "";
        return texto
            .Replace("\u2713", "OK")            // ✓
            .Replace("\u2717", "ERRO")          // ✗
            .Replace("\u27E8", "<")             // ⟨
            .Replace("\u27E9", ">")             // ⟩
            .Replace("\u2293", " AND ")         // ⊓
            .Replace("\u2203", "SOME ")         // ∃
            .Replace("\u2291", " SUBCLASS_OF ") // ⊑
            .Replace("\u2261", " == ")          // ≡
            .Replace("\u22A5", "FALSE")         // ⊥
            .Replace("\u2192", "->")            // →
            .Replace("\u2500", "-")             // ─
            .Replace("\u2726", "*");            // ✦
    }

    public void AcaoJogarNovamente()
    {
        Time.timeScale = 1.0f;
        m_ExibindoTelaFinal = false;
        if (m_PainelRaiz != null) m_PainelRaiz.SetActive(false);
        if (m_PainelBackdrop != null) m_PainelBackdrop.SetActive(false);

        RestaurarHUDsGameplay();

        if (HubInicialAmbiente.Instancia != null)
        {
            HubInicialAmbiente.Instancia.AcaoPlayIniciarPartida();
        }
    }

    public void AcaoVoltarMenuPrincipal()
    {
        Time.timeScale = 1.0f;
        m_ExibindoTelaFinal = false;
        if (m_PainelRaiz != null) m_PainelRaiz.SetActive(false);
        if (m_PainelBackdrop != null) m_PainelBackdrop.SetActive(false);

        RestaurarHUDsGameplay();

        if (HubInicialAmbiente.Instancia != null)
        {
            HubInicialAmbiente.Instancia.AcaoIrParaMenuPrincipal();
        }
    }

    public void ExibirFluxogramaCompletoVitoria()
    {
        ExibirGrafoFinal(true, m_TotalOrbesMissao, m_TotalOrbesMissao, InstanciaSemanticaBase.pontuacaoLogica);
    }

    public void DefinirAlvoOrbe(Transform transformOrbe, int indice, int total, bool ehOrbeDePoder, string nomeOrbe)
    {
        m_OrbesColetadas = Mathf.Max(0, indice - 1);
        m_TotalOrbesMissao = total;
    }

    public void ReiniciarMetricas()
    {
        Time.timeScale = 1.0f;
        m_ExibindoTelaFinal = false;
        m_OrbesColetadas = 0;
        m_NosDesatados.Clear();

        if (m_PainelRaiz != null) m_PainelRaiz.SetActive(false);
        if (m_PainelBackdrop != null) m_PainelBackdrop.SetActive(false);

        RestaurarHUDsGameplay();
    }

    // =========================================================================
    // UTILITÁRIOS DE CONSTRUÇÃO DE UI
    // =========================================================================

    private static (GameObject container, TMP_Text txtTitulo, TMP_Text txtValor) CriarBoxMetrica(Transform pai, string nome, string titulo, string valorInicial, Color corValor)
    {
        GameObject box = new GameObject(nome, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        box.transform.SetParent(pai, false);

        LayoutElement le = box.GetComponent<LayoutElement>();
        le.minHeight = 50f;
        le.preferredHeight = 52f;
        le.flexibleHeight = 1f;
        le.minWidth = 175f;
        le.flexibleWidth = 1f;

        Image img = box.GetComponent<Image>();
        img.color = new Color(0.027f, 0.063f, 0.094f, 0.95f); // #071018

        Outline outline = box.GetComponent<Outline>();
        outline.effectColor = new Color(0.118f, 0.227f, 0.373f, 0.85f); // #1E3A5F
        outline.effectDistance = new Vector2(1f, -1f);

        VerticalLayoutGroup vlg = box.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(6, 6, 5, 5);
        vlg.spacing = 2f;
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        GameObject titObj = new GameObject("Txt_Titulo", typeof(RectTransform), typeof(TextMeshProUGUI));
        titObj.transform.SetParent(box.transform, false);
        RectTransform rtT = titObj.GetComponent<RectTransform>();
        rtT.sizeDelta = new Vector2(0f, 16f);
        TMP_Text txtT = titObj.GetComponent<TextMeshProUGUI>();
        txtT.text = titulo;
        txtT.fontSize = 11.5f;
        txtT.fontStyle = FontStyles.Normal;
        txtT.color = new Color(0.580f, 0.639f, 0.722f); // #94A3B8
        txtT.alignment = TextAlignmentOptions.Center;
        txtT.raycastTarget = false;

        GameObject valObj = new GameObject("Txt_Valor", typeof(RectTransform), typeof(TextMeshProUGUI));
        valObj.transform.SetParent(box.transform, false);
        RectTransform rtV = valObj.GetComponent<RectTransform>();
        rtV.sizeDelta = new Vector2(0f, 22f);
        TMP_Text txtV = valObj.GetComponent<TextMeshProUGUI>();
        txtV.text = valorInicial;
        txtV.fontSize = 16f;
        txtV.fontStyle = FontStyles.Bold;
        txtV.color = corValor;
        txtV.alignment = TextAlignmentOptions.Center;
        txtV.raycastTarget = false;

        return (box, txtT, txtV);
    }

    private static void CriarDivisorLayout(Transform pai, Color cor)
    {
        GameObject div = new GameObject("Divisor", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        div.transform.SetParent(pai, false);
        div.GetComponent<Image>().color = cor;
        LayoutElement le = div.GetComponent<LayoutElement>();
        le.minHeight = 2f;
        le.preferredHeight = 2f;
        le.flexibleHeight = 0f;
    }

    private static TMP_Text CriarTextoLayout(Transform pai, string nome, string conteudo, float tamanhoFonte,
        FontStyles estilo, Color cor, float alturaPreferida)
    {
        GameObject go = new GameObject(nome, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        go.transform.SetParent(pai, false);

        LayoutElement le = go.GetComponent<LayoutElement>();
        le.minHeight = alturaPreferida;
        le.preferredHeight = alturaPreferida;
        le.flexibleHeight = 0f;

        TMP_Text texto = go.GetComponent<TextMeshProUGUI>();
        texto.text = conteudo;
        texto.fontSize = tamanhoFonte;
        texto.fontStyle = estilo;
        texto.color = cor;
        texto.alignment = TextAlignmentOptions.Center;
        texto.textWrappingMode = TextWrappingModes.Normal;
        texto.overflowMode = TextOverflowModes.Overflow;
        texto.raycastTarget = false;
        return texto;
    }

    private static TMP_Text CriarTextoCentro(Transform pai, string nome, string conteudo, float tamanhoFonte,
        FontStyles estilo, Vector2 pos, Vector2 dimensao, Color cor)
    {
        GameObject go = new GameObject(nome, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(pai, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = dimensao;

        TMP_Text texto = go.GetComponent<TextMeshProUGUI>();
        texto.text = conteudo;
        texto.fontSize = tamanhoFonte;
        texto.fontStyle = estilo;
        texto.color = cor;
        texto.alignment = TextAlignmentOptions.Center;
        texto.textWrappingMode = TextWrappingModes.Normal;
        texto.raycastTarget = false;
        return texto;
    }

    private static Button CriarBotaoRodapeLayout(Transform pai, string nome, string rotulo, Vector2 tamanho,
        Color corFundo, Color corBorda, UnityEngine.Events.UnityAction acao)
    {
        GameObject btnGO = new GameObject(nome, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(Outline));
        btnGO.transform.SetParent(pai, false);

        RectTransform rtBtn = btnGO.GetComponent<RectTransform>();
        rtBtn.sizeDelta = tamanho;

        Image imgBtn = btnGO.GetComponent<Image>();
        imgBtn.color = corFundo;

        Outline outline = btnGO.GetComponent<Outline>();
        outline.effectColor = corBorda;
        outline.effectDistance = new Vector2(2f, -2f);

        Button btn = btnGO.GetComponent<Button>();
        btn.onClick.AddListener(acao);

        CriarTextoCentro(btnGO.transform, "Txt_" + nome, $"<b>{rotulo}</b>", 22f, FontStyles.Bold,
            Vector2.zero, new Vector2(tamanho.x - 24f, tamanho.y - 10f), Color.white);

        return btn;
    }
}
