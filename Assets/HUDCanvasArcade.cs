using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ============================================================================
/// PAC-MAN SEMÂNTICO (Semantic Pac-Man) - HUD Arcade Canvas & Modal de PLAY
/// ============================================================================
/// 1. Canvas Scaler configurado para Scale With Screen Size (1920x1080).
/// 2. EventSystem e GraphicRaycaster garantidos para cliques 100% responsivos.
/// 3. Pausa Real: Time.timeScale = 0f ao iniciar; Time.timeScale = 1f ao clicar em PLAY.
/// 4. Modal Inicial de PLAY amplo (850x480), tipografia legível e botão de 620x75.
/// 5. HUD In-Game limpo no topo: Score, Vidas (ᗧ), Entidade Ativa e botão [ ✕ ].
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

    [Header("Modal de Início (Play) Canvas")]
    public GameObject modalPlayCanvas;
    public Button botaoIniciarPlay;
    public Text textoTituloPlay;
    public Text textoSubtituloPlay;
    public Text textoDescricaoPlay;

    [Header("Painel Superior do HUD")]
    public GameObject painelHUD;
    public Text textoPontuacao;
    public Text textoVidas;
    public Text textoEntidadeOntologica;

    [Header("Estado Visual")]
    public int pontuacaoAtual = 0;
    public int vidasRestantes = 3;
    public string entidadeAtual = "Aguardando item...";
    public bool painelAberto = true;

    private void Awake()
    {
        s_Instancia = this;
        GarantirEventSystem();
        GarantirCanvasEComponentes();
        // PAUSA REAL: Jogo inicia totalmente paralisado enquanto o modal de Play está aberto
        Time.timeScale = 0f;
    }

    private void OnEnable()
    {
        InstanciaSemanticaBase.OnInstanciaInteragida += AoInteragirInstancia;
        InstanciaSemanticaBase.OnInstanciaFocada += AoFocarInstancia;
        PacMan.Player.ControladorJogador.OnVidasAlteradas += AoAlterarVidas;
    }

    private void OnDisable()
    {
        InstanciaSemanticaBase.OnInstanciaInteragida -= AoInteragirInstancia;
        InstanciaSemanticaBase.OnInstanciaFocada -= AoFocarInstancia;
        PacMan.Player.ControladorJogador.OnVidasAlteradas -= AoAlterarVidas;
    }

    private void Start()
    {
        Time.timeScale = 0f;
        AtualizarDisplay();
    }

    /// <summary>
    /// Garante que exista um EventSystem na cena com StandaloneInputModule ou InputSystemUIInputModule,
    /// permitindo que cliques de mouse, touch ou teclado funcionem imediatamente sobre os botões do Canvas.
    /// </summary>
    public static void GarantirEventSystem()
    {
        if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();

            // Suporte compatível tanto para o novo Input System quanto para o Legacy Standalone
            Type inputSystemModule = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputSystemModule != null)
            {
                esObj.AddComponent(inputSystemModule);
            }
            else
            {
                esObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            Debug.Log("<color=#00FF99><b>[EventSystem]</b></color> Objeto EventSystem criado dinamicamente na cena.");
        }
    }

    private void AoAlterarVidas(int novasVidas)
    {
        vidasRestantes = Mathf.Max(0, novasVidas);
        AtualizarDisplay();
    }

    private void AoFocarInstancia(IInstanciaSemantica inst)
    {
        if (inst == null) return;
        string nomeFormatado = InstanciaSemanticaBase.FormatarNomeClasse(inst.NomeClasse);
        entidadeAtual = $"{nomeFormatado}";
        AtualizarDisplay();
    }

    private void AoInteragirInstancia(IInstanciaSemantica inst, bool sucesso)
    {
        if (inst != null)
        {
            string nomeFormatado = InstanciaSemanticaBase.FormatarNomeClasse(inst.NomeClasse);
            entidadeAtual = $"{nomeFormatado}";
            pontuacaoAtual += inst.ValorPontuacao;
        }
        AtualizarDisplay();
    }

    public void DefinirPontuacao(int pontos)
    {
        pontuacaoAtual = pontos;
        AtualizarDisplay();
    }

    public void DefinirVidas(int vidas)
    {
        vidasRestantes = Mathf.Max(0, vidas);
        AtualizarDisplay();
    }

    public void DefinirEntidade(string nomeEntidade)
    {
        if (!string.IsNullOrEmpty(nomeEntidade))
        {
            entidadeAtual = nomeEntidade;
            AtualizarDisplay();
        }
    }

    public void AlternarVisibilidadePainel()
    {
        painelAberto = !painelAberto;
        if (painelHUD != null)
        {
            painelHUD.SetActive(painelAberto);
        }
    }

    public void DefinirModalPlayVisivel(bool visivel)
    {
        if (modalPlayCanvas != null)
        {
            modalPlayCanvas.SetActive(visivel);
        }
    }

    public void DefinirPainelVisivel(bool visivel)
    {
        painelAberto = visivel;
        if (painelHUD != null)
        {
            painelHUD.SetActive(visivel);
        }
    }

    public bool TemModalPlayAtivo()
    {
        return modalPlayCanvas != null && modalPlayCanvas.activeSelf;
    }

    /// <summary>
    /// Ação do Clique: O botão verde [ INICIAR JOGO (PLAY) ] desativa o modal inicial
    /// e devolve o tempo ao normal (Time.timeScale = 1f).
    /// </summary>
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
        Debug.Log("<color=#00FF99><b>[PLAY]</b></color> Jogo destravado! Time.timeScale = 1.0f.");
    }

    public void AtualizarDisplay()
    {
        if (textoPontuacao != null)
        {
            textoPontuacao.text = $"SCORE: <color=#FFD700>{pontuacaoAtual:D5}</color>";
        }

        if (textoVidas != null)
        {
            string icones = "";
            for (int i = 0; i < vidasRestantes; i++)
            {
                icones += "<color=#FFEB04>ᗧ</color> ";
            }
            if (vidasRestantes <= 0) icones = "<color=#FF3344>ZERO</color>";
            textoVidas.text = $"VIDAS: {icones}";
        }

        if (textoEntidadeOntologica != null)
        {
            textoEntidadeOntologica.text = $"ENTIDADE ATIVA: <color=#00E5FF>{entidadeAtual}</color>";
        }
    }

    /// <summary>
    /// Configura o Canvas com Scale With Screen Size (1920x1080), GraphicRaycaster, Modal de PLAY amplo e HUD limpo.
    /// </summary>
    private void GarantirCanvasEComponentes()
    {
        if (canvasPrincipal == null)
        {
            canvasPrincipal = GetComponent<Canvas>();
        }

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

        Font arcadeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

        // =====================================================================
        // 1. MODAL DE PLAY AMPLO (CENTRALIZADO, 850x480, FONTES GRANDES E BOTÃO CLICÁVEL)
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
            rtModal.sizeDelta = new Vector2(850f, 480f);

            Image imgModal = modalPlayCanvas.AddComponent<Image>();
            imgModal.color = new Color(0.02f, 0.05f, 0.16f, 0.96f);

            // Título Principal
            GameObject tTitleObj = new GameObject("Texto_Titulo");
            tTitleObj.transform.SetParent(modalPlayCanvas.transform, false);
            RectTransform rtTitle = tTitleObj.AddComponent<RectTransform>();
            rtTitle.anchorMin = new Vector2(0.5f, 1f);
            rtTitle.anchorMax = new Vector2(0.5f, 1f);
            rtTitle.pivot = new Vector2(0.5f, 1f);
            rtTitle.anchoredPosition = new Vector2(0f, -32f);
            rtTitle.sizeDelta = new Vector2(800f, 50f);

            textoTituloPlay = tTitleObj.AddComponent<Text>();
            textoTituloPlay.font = arcadeFont;
            textoTituloPlay.fontSize = 32;
            textoTituloPlay.fontStyle = FontStyle.Bold;
            textoTituloPlay.text = "🕹️  PAC-MAN SEMÂNTICO (CIn-UFPE)";
            textoTituloPlay.color = new Color(1.0f, 0.88f, 0.0f);
            textoTituloPlay.alignment = TextAnchor.MiddleCenter;

            // Subtítulo
            GameObject tSubObj = new GameObject("Texto_Subtitulo");
            tSubObj.transform.SetParent(modalPlayCanvas.transform, false);
            RectTransform rtSub = tSubObj.AddComponent<RectTransform>();
            rtSub.anchorMin = new Vector2(0.5f, 1f);
            rtSub.anchorMax = new Vector2(0.5f, 1f);
            rtSub.pivot = new Vector2(0.5f, 1f);
            rtSub.anchoredPosition = new Vector2(0f, -86f);
            rtSub.sizeDelta = new Vector2(800f, 34f);

            textoSubtituloPlay = tSubObj.AddComponent<Text>();
            textoSubtituloPlay.font = arcadeFont;
            textoSubtituloPlay.fontSize = 20;
            textoSubtituloPlay.fontStyle = FontStyle.Bold;
            textoSubtituloPlay.text = "Ontologia Parasitologia • Raciocínio DL (Protégé / OWL)";
            textoSubtituloPlay.color = new Color(0.22f, 0.74f, 0.97f);
            textoSubtituloPlay.alignment = TextAnchor.MiddleCenter;

            // Descrição e Instruções
            GameObject tDescObj = new GameObject("Texto_Descricao");
            tDescObj.transform.SetParent(modalPlayCanvas.transform, false);
            RectTransform rtDesc = tDescObj.AddComponent<RectTransform>();
            rtDesc.anchorMin = new Vector2(0.5f, 1f);
            rtDesc.anchorMax = new Vector2(0.5f, 1f);
            rtDesc.pivot = new Vector2(0.5f, 1f);
            rtDesc.anchoredPosition = new Vector2(0f, -145f);
            rtDesc.sizeDelta = new Vector2(760f, 180f);

            textoDescricaoPlay = tDescObj.AddComponent<Text>();
            textoDescricaoPlay.font = arcadeFont;
            textoDescricaoPlay.fontSize = 17;
            textoDescricaoPlay.lineSpacing = 1.35f;
            textoDescricaoPlay.supportRichText = true;
            textoDescricaoPlay.text = @"• Devore as <color=#FFFF00><b>Gemas Ontológicas (Classes)</b></color> navegando pelo labirinto.
• Cuidado com os <color=#FF4444><b>4 Fantasmas</b></color> que partem da gaiola central!
• Colete as <color=#FF2255><b>Cerejas</b></color> e <color=#FFD700><b>Power Pellets</b></color> para virar o jogo.

<color=#94A3B8>Controles: Setas / WASD • C: Alternar Câmera • H: Ocultar HUD</color>";
            textoDescricaoPlay.color = new Color(0.95f, 0.96f, 0.98f);
            textoDescricaoPlay.alignment = TextAnchor.MiddleCenter;

            // Botão PLAY
            GameObject btnPlayObj = new GameObject("Botao_Iniciar_Play");
            btnPlayObj.transform.SetParent(modalPlayCanvas.transform, false);
            RectTransform rtBtn = btnPlayObj.AddComponent<RectTransform>();
            rtBtn.anchorMin = new Vector2(0.5f, 0f);
            rtBtn.anchorMax = new Vector2(0.5f, 0f);
            rtBtn.pivot = new Vector2(0.5f, 0f);
            rtBtn.anchoredPosition = new Vector2(0f, 38f);
            rtBtn.sizeDelta = new Vector2(620f, 75f);

            Image imgBtn = btnPlayObj.AddComponent<Image>();
            imgBtn.color = new Color(0.06f, 0.73f, 0.51f); // Esmeralda vibrante

            botaoIniciarPlay = btnPlayObj.AddComponent<Button>();
            ColorBlock cb = botaoIniciarPlay.colors;
            cb.normalColor = new Color(0.06f, 0.73f, 0.51f);
            cb.highlightedColor = new Color(0.20f, 0.83f, 0.60f);
            cb.pressedColor = new Color(0.04f, 0.55f, 0.38f);
            botaoIniciarPlay.colors = cb;
            botaoIniciarPlay.onClick.AddListener(AoClicarIniciarPlay);

            GameObject txtBtnObj = new GameObject("Text");
            txtBtnObj.transform.SetParent(btnPlayObj.transform, false);
            RectTransform rtTxtBtn = txtBtnObj.AddComponent<RectTransform>();
            rtTxtBtn.anchorMin = Vector2.zero;
            rtTxtBtn.anchorMax = Vector2.one;
            rtTxtBtn.sizeDelta = Vector2.zero;

            Text txtBtn = txtBtnObj.AddComponent<Text>();
            txtBtn.font = arcadeFont;
            txtBtn.fontSize = 24;
            txtBtn.fontStyle = FontStyle.Bold;
            txtBtn.text = "▶   I N I C I A R   J O G O   ( P L A Y )";
            txtBtn.color = Color.white;
            txtBtn.alignment = TextAnchor.MiddleCenter;
        }
        else
        {
            modalPlayCanvas = modalTr.gameObject;
        }

        // =====================================================================
        // 2. PAINEL SUPERIOR COMPACTO (HUD IN-GAME)
        // =====================================================================
        Transform painelTr = transform.Find("Painel_Arcade_HUD");
        if (painelTr == null)
        {
            painelHUD = new GameObject("Painel_Arcade_HUD");
            painelHUD.transform.SetParent(transform, false);
            RectTransform rt = painelHUD.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(0f, 52f);

            Image img = painelHUD.AddComponent<Image>();
            img.color = new Color(0.02f, 0.05f, 0.12f, 0.88f);
        }
        else
        {
            painelHUD = painelTr.gameObject;
        }

        // Texto de Pontuação (Score) - Superior Esquerdo
        if (textoPontuacao == null)
        {
            Transform tTr = painelHUD.transform.Find("Texto_Score");
            GameObject tObj = tTr != null ? tTr.gameObject : new GameObject("Texto_Score");
            tObj.transform.SetParent(painelHUD.transform, false);
            RectTransform rt = tObj.GetComponent<RectTransform>() ?? tObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(30f, 0f);
            rt.sizeDelta = new Vector2(240f, 40f);

            textoPontuacao = tObj.GetComponent<Text>() ?? tObj.AddComponent<Text>();
            textoPontuacao.font = arcadeFont;
            textoPontuacao.fontSize = 15;
            textoPontuacao.fontStyle = FontStyle.Bold;
            textoPontuacao.alignment = TextAnchor.MiddleLeft;
            textoPontuacao.supportRichText = true;
        }

        // Texto de Entidade Ontológica - Centro
        if (textoEntidadeOntologica == null)
        {
            Transform tTr = painelHUD.transform.Find("Texto_EntidadeOntologica");
            GameObject tObj = tTr != null ? tTr.gameObject : new GameObject("Texto_EntidadeOntologica");
            tObj.transform.SetParent(painelHUD.transform, false);
            RectTransform rt = tObj.GetComponent<RectTransform>() ?? tObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(500f, 40f);

            textoEntidadeOntologica = tObj.GetComponent<Text>() ?? tObj.AddComponent<Text>();
            textoEntidadeOntologica.font = arcadeFont;
            textoEntidadeOntologica.fontSize = 14;
            textoEntidadeOntologica.fontStyle = FontStyle.Bold;
            textoEntidadeOntologica.alignment = TextAnchor.MiddleCenter;
            textoEntidadeOntologica.supportRichText = true;
        }

        // Texto de Vidas - Direita
        if (textoVidas == null)
        {
            Transform tTr = painelHUD.transform.Find("Texto_Vidas");
            GameObject tObj = tTr != null ? tTr.gameObject : new GameObject("Texto_Vidas");
            tObj.transform.SetParent(painelHUD.transform, false);
            RectTransform rt = tObj.GetComponent<RectTransform>() ?? tObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(-75f, 0f);
            rt.sizeDelta = new Vector2(180f, 40f);

            textoVidas = tObj.GetComponent<Text>() ?? tObj.AddComponent<Text>();
            textoVidas.font = arcadeFont;
            textoVidas.fontSize = 15;
            textoVidas.fontStyle = FontStyle.Bold;
            textoVidas.alignment = TextAnchor.MiddleRight;
            textoVidas.supportRichText = true;
        }

        // Botão [ X ] de Fechar HUD
        Transform btnTr = painelHUD.transform.Find("Botao_Fechar");
        if (btnTr == null)
        {
            GameObject btnObj = new GameObject("Botao_Fechar");
            btnObj.transform.SetParent(painelHUD.transform, false);
            RectTransform rt = btnObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(-15f, 0f);
            rt.sizeDelta = new Vector2(28f, 26f);

            Image img = btnObj.AddComponent<Image>();
            img.color = new Color(0.85f, 0.2f, 0.2f, 0.95f);

            Button btn = btnObj.AddComponent<Button>();
            btn.onClick.AddListener(AlternarVisibilidadePainel);

            GameObject txtObj = new GameObject("Text");
            txtObj.transform.SetParent(btnObj.transform, false);
            RectTransform rtTxt = txtObj.AddComponent<RectTransform>();
            rtTxt.anchorMin = Vector2.zero;
            rtTxt.anchorMax = Vector2.one;
            rtTxt.sizeDelta = Vector2.zero;

            Text txt = txtObj.AddComponent<Text>();
            txt.font = arcadeFont;
            txt.fontSize = 14;
            txt.fontStyle = FontStyle.Bold;
            txt.text = "✕";
            txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;
        }

        // Estado inicial: Mostra o modal de Play, oculta o HUD superior
        if (modalPlayCanvas != null) modalPlayCanvas.SetActive(true);
        if (painelHUD != null) painelHUD.SetActive(false);
    }
}
