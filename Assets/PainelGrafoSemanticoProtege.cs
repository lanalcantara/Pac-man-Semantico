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
/// Painel de Resultado Final & Relatório Completo de Triplas RDF (Vitória / Game Over)
/// ============================================================================
/// 1. Durante a partida, exibe apenas o banner inferior sutil ao coletar Triplas RDF.
/// 2. Ao final da partida (Vitória ou Violação Ontológica / Game Over):
///    - Pausa o jogo (Time.timeScale = 0f) e oculta HUDs periféricos (Radar, Telemetria, ABox).
///    - Ocupa 85% da viewport (1632x918 em CanvasScaler 1920x1080), fundo #050814 (95% alpha) e borda #00FFFF.
///    - Cabeçalho claro: ONTOLOGIA VALIDADA - CONSISTENTE (#00FF88) ou VIOLAÇÃO ONTOLÓGICA - GRAFO INCOMPLETO (#FF4D4D).
///    - Corpo Central com ScrollRect listando TODAS as Triplas RDF coletadas e as pendentes sem corte de texto.
///    - Rodapé com botões grandes JOGAR NOVAMENTE e FECHAR / MENU (Mouse, Teclado Enter/R e VR).
/// ============================================================================
/// </summary>
[DisallowMultipleComponent]
public class PainelGrafoSemanticoProtege : MonoBehaviour
{
    public static PainelGrafoSemanticoProtege Instancia { get; private set; }

    [Header("Configuração de Exibição")]
    public Vector2 tamanhoPainelFinal = new Vector2(1632f, 918f); // 85% da viewport 1920x1080

    private Canvas m_CanvasGrafo;
    private GameObject m_PainelBackdrop;
    private GameObject m_PainelRaiz;
    private Outline m_OutlinePainel;
    private TMP_Text m_TextoTituloPainel;
    private TMP_Text m_TextoMetricas;
    private TMP_Text m_TextoConceitoFoco;
    private TMP_Text m_TextoRelatorioTriplas;
    private ScrollRect m_ScrollRelatorio;
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
            }
        }
    }

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

        // 2. Backdrop escuro para isolar a leitura
        m_PainelBackdrop = new GameObject("Backdrop_Resultado_Final", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        m_PainelBackdrop.transform.SetParent(canvasObj.transform, false);
        RectTransform rtBack = m_PainelBackdrop.GetComponent<RectTransform>();
        rtBack.anchorMin = Vector2.zero;
        rtBack.anchorMax = Vector2.one;
        rtBack.sizeDelta = Vector2.zero;
        m_PainelBackdrop.GetComponent<Image>().color = new Color(0.01f, 0.02f, 0.05f, 0.78f);

        // 3. Painel Modal Principal (85% da viewport: 1632x918 em 1920x1080, fundo #050814 95% alpha, borda #00FFFF)
        m_PainelRaiz = new GameObject("Janela_Resultado_Final_85pct", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
        m_PainelRaiz.transform.SetParent(canvasObj.transform, false);

        RectTransform rtJanela = m_PainelRaiz.GetComponent<RectTransform>();
        rtJanela.anchorMin = new Vector2(0.5f, 0.5f);
        rtJanela.anchorMax = new Vector2(0.5f, 0.5f);
        rtJanela.pivot = new Vector2(0.5f, 0.5f);
        rtJanela.anchoredPosition = Vector2.zero;
        rtJanela.sizeDelta = tamanhoPainelFinal;

        Image imgFundo = m_PainelRaiz.GetComponent<Image>();
        imgFundo.color = new Color(0.020f, 0.031f, 0.078f, 0.95f); // #050814 com 95% de alpha

        m_OutlinePainel = m_PainelRaiz.GetComponent<Outline>();
        m_OutlinePainel.effectColor = new Color(0.0f, 1.0f, 1.0f, 0.95f); // #00FFFF
        m_OutlinePainel.effectDistance = new Vector2(2f, -2f);

        // =====================================================================
        // ÁREA 1: CABEÇALHO
        // =====================================================================
        m_TextoTituloPainel = CriarTextoTop(m_PainelRaiz.transform, "Titulo_Resultado",
            "ONTOLOGIA VALIDADA - CONSISTENTE",
            34f, FontStyles.Bold,
            new Vector2(0f, -28f), new Vector2(1540f, 48f),
            new Color(0.0f, 1.0f, 0.533f), TextAlignmentOptions.Center); // #00FF88

        m_TextoMetricas = CriarTextoTop(m_PainelRaiz.transform, "Resumo_Numerico",
            "Pontuação Total: 0 pts   |   Triplas Coletadas: 0 / 38   |   Reasoner HermiT: CONSISTENTE",
            22f, FontStyles.Bold,
            new Vector2(0f, -84f), new Vector2(1540f, 36f),
            new Color(0.886f, 0.910f, 0.941f), TextAlignmentOptions.Center); // #E2E8F0

        m_TextoConceitoFoco = CriarTextoTop(m_PainelRaiz.transform, "Subtitulo_Diagnostico",
            "Relatório completo das Triplas RDF (ABox) sintetizadas durante a sessão.",
            19f, FontStyles.Normal,
            new Vector2(0f, -124f), new Vector2(1540f, 32f),
            new Color(0.404f, 0.910f, 0.976f), TextAlignmentOptions.Center); // #67E8F9

        // Divisor superior
        CriarDivisorHorizontal(m_PainelRaiz.transform, new Vector2(0f, -166f), 1540f, new Color(0.0f, 1.0f, 1.0f, 0.45f));

        // =====================================================================
        // ÁREA 2: CORPO CENTRAL (ScrollRect com lista completa de Triplas RDF)
        // =====================================================================
        GameObject scrollArea = new GameObject("ScrollRect_Relatorio_Triplas", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(ScrollRect), typeof(Outline));
        scrollArea.transform.SetParent(m_PainelRaiz.transform, false);

        RectTransform rtScroll = scrollArea.GetComponent<RectTransform>();
        rtScroll.anchorMin = new Vector2(0.5f, 0.5f);
        rtScroll.anchorMax = new Vector2(0.5f, 0.5f);
        rtScroll.pivot = new Vector2(0.5f, 0.5f);
        rtScroll.anchoredPosition = new Vector2(0f, -14f);
        rtScroll.sizeDelta = new Vector2(1540f, 570f);

        scrollArea.GetComponent<Image>().color = new Color(0.012f, 0.020f, 0.050f, 0.92f);
        Outline outlineScroll = scrollArea.GetComponent<Outline>();
        outlineScroll.effectColor = new Color(0.0f, 1.0f, 1.0f, 0.30f);
        outlineScroll.effectDistance = new Vector2(1f, -1f);

        // Viewport com RectMask2D
        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        viewport.transform.SetParent(scrollArea.transform, false);
        RectTransform rtViewport = viewport.GetComponent<RectTransform>();
        rtViewport.anchorMin = Vector2.zero;
        rtViewport.anchorMax = Vector2.one;
        rtViewport.offsetMin = new Vector2(16f, 14f);
        rtViewport.offsetMax = new Vector2(-32f, -14f);

        // Content expansível verticalmente
        GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        RectTransform rtContent = content.GetComponent<RectTransform>();
        rtContent.anchorMin = new Vector2(0f, 1f);
        rtContent.anchorMax = new Vector2(1f, 1f);
        rtContent.pivot = new Vector2(0.5f, 1f);
        rtContent.anchoredPosition = Vector2.zero;
        rtContent.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup vlg = content.GetComponent<VerticalLayoutGroup>();
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.padding = new RectOffset(8, 8, 8, 16);

        ContentSizeFitter csf = content.GetComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Texto principal do relatório (fonte confortável 20px, lineSpacing 12, Word Wrapping ativado)
        GameObject txtRelObj = new GameObject("Texto_Relatorio_Triplas", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtRelObj.transform.SetParent(content.transform, false);
        m_TextoRelatorioTriplas = txtRelObj.GetComponent<TextMeshProUGUI>();
        m_TextoRelatorioTriplas.fontSize = 20f;
        m_TextoRelatorioTriplas.lineSpacing = 12f;
        m_TextoRelatorioTriplas.color = new Color(0.886f, 0.910f, 0.941f); // #E2E8F0
        m_TextoRelatorioTriplas.alignment = TextAlignmentOptions.TopLeft;
        m_TextoRelatorioTriplas.textWrappingMode = TextWrappingModes.Normal;
        m_TextoRelatorioTriplas.overflowMode = TextOverflowModes.Overflow;
        m_TextoRelatorioTriplas.richText = true;
        m_TextoRelatorioTriplas.raycastTarget = false;

        // Scrollbar vertical
        GameObject scrollbarObj = new GameObject("Scrollbar_Vertical", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Scrollbar));
        scrollbarObj.transform.SetParent(scrollArea.transform, false);
        RectTransform rtSb = scrollbarObj.GetComponent<RectTransform>();
        rtSb.anchorMin = new Vector2(1f, 0f);
        rtSb.anchorMax = new Vector2(1f, 1f);
        rtSb.pivot = new Vector2(1f, 0.5f);
        rtSb.anchoredPosition = new Vector2(-6f, 0f);
        rtSb.sizeDelta = new Vector2(14f, -20f);
        scrollbarObj.GetComponent<Image>().color = new Color(0.08f, 0.12f, 0.22f, 0.8f);

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
        m_ScrollRelatorio.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;

        // =====================================================================
        // ÁREA 3: RODAPÉ (Botões grandes JOGAR NOVAMENTE e FECHAR / MENU)
        // =====================================================================
        CriarDivisorHorizontal(m_PainelRaiz.transform, new Vector2(0f, -772f), 1540f, new Color(0.0f, 1.0f, 1.0f, 0.35f));

        m_BotaoJogarNovamente = CriarBotaoRodape(
            m_PainelRaiz.transform, "Botao_Jogar_Novamente",
            "JOGAR NOVAMENTE",
            new Vector2(-220f, 64f), new Vector2(360f, 62f),
            new Color(0.05f, 0.45f, 0.25f, 0.95f),
            new Color(0.290f, 0.871f, 0.502f, 1f), // #4ADE80
            AcaoJogarNovamente);

        m_BotaoVoltarMenu = CriarBotaoRodape(
            m_PainelRaiz.transform, "Botao_Fechar_Menu",
            "FECHAR / MENU",
            new Vector2(220f, 64f), new Vector2(360f, 62f),
            new Color(0.06f, 0.22f, 0.42f, 0.95f),
            new Color(0.404f, 0.910f, 0.976f, 1f), // #67E8F9
            AcaoVoltarMenuPrincipal);

        CriarTextoBottom(m_PainelRaiz.transform, "Txt_Atalhos_Rodape",
            "[Clique] Selecionar   •   [R] Jogar Novamente   •   [ENTER / ESC / A] Fechar / Menu   •   Use a barra de rolagem para ler todas as triplas",
            18f, FontStyles.Normal,
            new Vector2(0f, 18f), new Vector2(1540f, 28f),
            new Color(0.886f, 0.910f, 0.941f, 0.85f));

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
            "<b>✦ TRIPLA RDF SINTETIZADA (ABox) ✦</b>", 13f, FontStyles.Bold,
            new Vector2(0f, 11f), new Vector2(530f, 20f), new Color(0.290f, 0.871f, 0.502f));

        m_TextoBannerDetalhes = CriarTextoCentro(m_BannerOrbeColetada.transform, "Texto_Banner_Detalhes",
            "Instância ontológica conectada ao reasoner.", 13f, FontStyles.Normal,
            new Vector2(0f, -10f), new Vector2(530f, 20f), Color.white);

        m_BannerOrbeColetada.SetActive(false);
    }

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
            string tipo = ehOrbeDePoder ? "✦ TRIPLA RDF DE PODER (ABox) ✦" : "✦ TRIPLA RDF SINTETIZADA (ABox) ✦";
            string cor = ehOrbeDePoder ? "#FACC15" : "#4ADE80";
            m_TextoBannerTitulo.text = $"<color={cor}><b>{tipo}</b></color>";
        }

        if (m_TextoBannerDetalhes != null)
        {
            m_TextoBannerDetalhes.text = $"<color=#67E8F9><b>{nomeClasse}</b></color>  |  <color=#E2E8F0>{expressaoDL}</color>";
        }

        m_BannerOrbeColetada.SetActive(true);
        m_TimerBanner = 2.5f;
    }

    /// <summary>
    /// Apresenta o relatório completo de Vitória ou Violação Ontológica (Game Over),
    /// pausando o jogo (Time.timeScale = 0) e ocultando os HUDs periféricos.
    /// </summary>
    public void ExibirGrafoFinal(bool vitoria, int orbesColetadas, int totalOrbes, int pontuacao)
    {
        m_ExibindoTelaFinal = true;
        m_OrbesColetadas = orbesColetadas;
        m_TotalOrbesMissao = totalOrbes > 0 ? totalOrbes : 38;

        if (m_PainelRaiz == null)
        {
            ConstruirInterfaceGrafo();
        }

        // 1. Pausa o jogo e oculta HUDs periféricos para leitura limpa
        Time.timeScale = 0f;
        if (m_BannerOrbeColetada != null) m_BannerOrbeColetada.SetActive(false);
        if (MinimapaHUDVR.Instancia != null) MinimapaHUDVR.Instancia.DefinirAtivacaoHUD(false);
        if (PainelInventarioABoxVR.Instancia != null) PainelInventarioABoxVR.Instancia.DefinirVisibilidadePainel(false);

        if (m_PainelBackdrop != null) m_PainelBackdrop.SetActive(true);
        m_PainelRaiz.SetActive(true);

        // 2. Cabeçalho em alto contraste
        if (vitoria)
        {
            if (m_OutlinePainel != null) m_OutlinePainel.effectColor = new Color(0.0f, 1.0f, 0.533f, 0.95f); // #00FF88
            if (m_TextoTituloPainel != null)
            {
                m_TextoTituloPainel.text = "<color=#00FF88>ONTOLOGIA VALIDADA - CONSISTENTE</color>";
            }
            if (m_TextoMetricas != null)
            {
                m_TextoMetricas.text = $"<color=#FFFFFF>Pontuação Total: <b>{pontuacao} pts</b></color>   |   " +
                                       $"<color=#4ADE80>Triplas Coletadas: <b>{m_TotalOrbesMissao} / {m_TotalOrbesMissao} (100%)</b></color>   |   " +
                                       $"<color=#67E8F9>Reasoner HermiT: <b>CONSISTENTE</b></color>";
            }
            if (m_TextoConceitoFoco != null)
            {
                m_TextoConceitoFoco.text = "<color=#E2E8F0>Todas as Triplas RDF da ABox foram sintetizadas e validadas sem violações axiomáticas.</color>";
            }
        }
        else
        {
            if (m_OutlinePainel != null) m_OutlinePainel.effectColor = new Color(1.0f, 0.302f, 0.302f, 0.95f); // #FF4D4D
            if (m_TextoTituloPainel != null)
            {
                m_TextoTituloPainel.text = "<color=#FF4D4D>VIOLAÇÃO ONTOLÓGICA - GRAFO INCOMPLETO</color>";
            }

            int faltam = Mathf.Max(0, m_TotalOrbesMissao - orbesColetadas);
            if (m_TextoMetricas != null)
            {
                m_TextoMetricas.text = $"<color=#FFFFFF>Pontuação Total: <b>{pontuacao} pts</b></color>   |   " +
                                       $"<color=#FACC15>Triplas Coletadas: <b>{orbesColetadas} / {m_TotalOrbesMissao}</b> (Faltaram: {faltam})</color>   |   " +
                                       $"<color=#FF4D4D>Reasoner HermiT: <b>INCOMPLETO / VIOLAÇÃO</b></color>";
            }
            if (m_TextoConceitoFoco != null)
            {
                m_TextoConceitoFoco.text = "<color=#E2E8F0>Abaixo estão todas as Triplas RDF registradas na ABox e as classes pendentes para fechar o grafo.</color>";
            }
        }

        // 3. Corpo Central: Relatório completo dentro do ScrollRect
        PreencherRelatorioTriplas(vitoria);

        if (m_ScrollRelatorio != null)
        {
            Canvas.ForceUpdateCanvases();
            m_ScrollRelatorio.verticalNormalizedPosition = 1f;
        }
    }

    private void PreencherRelatorioTriplas(bool vitoria)
    {
        if (m_TextoRelatorioTriplas == null) return;

        var sb = new StringBuilder();
        var historico = PainelInventarioABoxVR.Instancia != null ? PainelInventarioABoxVR.Instancia.HistoricoTriplas : null;

        sb.AppendLine("<color=#67E8F9><b>═══ RELATÓRIO DE TRIPLAS RDF SINTETIZADAS (ABox) ═══</b></color>");
        sb.AppendLine();

        if (historico != null && historico.Count > 0)
        {
            for (int i = 0; i < historico.Count; i++)
            {
                var t = historico[i];
                string badge = t.consistente
                    ? "<color=#4ADE80><b>[✓ CONSISTENTE]</b></color>"
                    : "<color=#FF4D4D><b>[✗ INCONSISTENTE]</b></color>";
                string ptsStr = t.pontos >= 0 ? $"+{t.pontos}" : $"{t.pontos}";

                sb.AppendLine(
                    $"<color=#FFFFFF><b>#{t.indice:D2}</b></color>  {badge}  <color=#FACC15>({ptsStr} pts)</color>   " +
                    $"<color=#FFFFFF><b>{t.sujeito}</b></color> <color=#67E8F9>──[{t.predicado}]→</color> <color=#FFFFFF><b>{t.objeto}</b></color>");
                sb.AppendLine($"      <color=#E2E8F0><size=18>Axioma DL: {t.axiomaDL}</size></color>");
            }
        }
        else
        {
            sb.AppendLine("<color=#FACC15>Nenhuma Tripla RDF foi coletada antes do encerramento da partida.</color>");
        }

        if (!vitoria && GerenciadorCenarioSemantico.Instancia != null)
        {
            List<string> faltantes = GerenciadorCenarioSemantico.Instancia.ObterClassesTriplasFaltantes();
            if (faltantes != null && faltantes.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"<color=#FACC15><b>═══ TRIPLAS / CLASSES PENDENTES ({faltantes.Count}) ═══</b></color>");
                sb.AppendLine();
                for (int i = 0; i < faltantes.Count; i++)
                {
                    sb.AppendLine($"<color=#FACC15>• [PENDENTE]</color> <color=#E2E8F0>{faltantes[i]}</color>");
                }
            }
        }

        m_TextoRelatorioTriplas.text = sb.ToString();
    }

    public void AcaoJogarNovamente()
    {
        Time.timeScale = 1.0f;
        m_ExibindoTelaFinal = false;
        if (m_PainelRaiz != null) m_PainelRaiz.SetActive(false);
        if (m_PainelBackdrop != null) m_PainelBackdrop.SetActive(false);

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
    }

    // =========================================================================
    // UTILITÁRIOS DE CONSTRUÇÃO DE UI
    // =========================================================================

    private static void CriarDivisorHorizontal(Transform pai, Vector2 posTop, float largura, Color cor)
    {
        GameObject div = new GameObject("Divisor", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        div.transform.SetParent(pai, false);
        RectTransform rt = div.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = posTop;
        rt.sizeDelta = new Vector2(largura, 2f);
        div.GetComponent<Image>().color = cor;
    }

    private static TMP_Text CriarTextoTop(Transform pai, string nome, string conteudo, float tamanhoFonte,
        FontStyles estilo, Vector2 posTop, Vector2 dimensao, Color cor, TextAlignmentOptions alinhamento)
    {
        GameObject go = new GameObject(nome, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(pai, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = posTop;
        rt.sizeDelta = dimensao;

        TMP_Text texto = go.GetComponent<TextMeshProUGUI>();
        texto.text = conteudo;
        texto.fontSize = tamanhoFonte;
        texto.fontStyle = estilo;
        texto.color = cor;
        texto.alignment = alinhamento;
        texto.textWrappingMode = TextWrappingModes.Normal;
        texto.overflowMode = TextOverflowModes.Overflow;
        texto.raycastTarget = false;
        return texto;
    }

    private static TMP_Text CriarTextoBottom(Transform pai, string nome, string conteudo, float tamanhoFonte,
        FontStyles estilo, Vector2 posBottom, Vector2 dimensao, Color cor)
    {
        GameObject go = new GameObject(nome, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(pai, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = posBottom;
        rt.sizeDelta = dimensao;

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

    private static Button CriarBotaoRodape(Transform pai, string nome, string rotulo, Vector2 posBottom, Vector2 tamanho,
        Color corFundo, Color corBorda, UnityEngine.Events.UnityAction acao)
    {
        GameObject btnGO = new GameObject(nome, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(Outline));
        btnGO.transform.SetParent(pai, false);

        RectTransform rtBtn = btnGO.GetComponent<RectTransform>();
        rtBtn.anchorMin = new Vector2(0.5f, 0f);
        rtBtn.anchorMax = new Vector2(0.5f, 0f);
        rtBtn.pivot = new Vector2(0.5f, 0f);
        rtBtn.anchoredPosition = posBottom;
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
