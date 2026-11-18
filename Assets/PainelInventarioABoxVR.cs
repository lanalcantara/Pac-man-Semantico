using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ============================================================================
/// PAC-MAN SEMÂNTICO (CIn-UFPE)
/// PAINEL DE INVENTÁRIO E HISTÓRICO DE INSTÂNCIAS (ABox / Triplas Ativas)
/// ============================================================================
/// Interface holográfica periférica WorldSpace no campo de visão direito do Meta Quest 3.
/// 
/// Funcionalidades Ontológicas:
/// 1. Conexão Direta com a ABox (Indivíduos e Instanciação):
///    - Cada gema/item coletado no labirinto é uma Instância real da ontologia (ex: Ascaris lumbricoides).
///    - Exibe imediatamente a tripla semântica recém-formada no formato:
///      ⟨Sujeito⟩ ──[predicado]──> ⟨Objeto⟩
///      Ex: ⟨Homo_sapiens⟩ ──[has_parasite]──> ⟨Ascaris_lumbricoides⟩
/// 
/// 2. Validação Lógica pelo Reasoner (HermiT):
///    - Se a captura for consistente: Badge [✓ CONSISTENTE] em neon verde (#00FF99).
///    - Se for detectada inconsistência/violação: Badge [✗ INCONSISTÊNCIA] em neon vermelho (#FF3344).
///    - Apresenta o axioma formal de Description Logic (DL) correspondente.
/// 
/// 3. Ergonomia e Conforto VR (Quest 3 Pancake Lenses):
///    - Posicionamento WorldSpace no quadrante periférico direito (simétrico ao Minimapa TBox).
///    - Amortecimento inercial suave (Damping 14f) para conforto visual.
///    - Ocultamento automático durante Menu Hub, Vitória ou GameOver.
/// ============================================================================
/// </summary>
[DisallowMultipleComponent]
public class PainelInventarioABoxVR : MonoBehaviour
{
    public static PainelInventarioABoxVR Instancia { get; private set; }

    [Header("Configurações Visuais e Ergonomia VR")]
    public bool exibirInventario = true;
    public Vector2 tamanhoPainel = new Vector2(460f, 365f);
    public float distanciaVisaoVR = 1.35f;
    public Vector2 offsetPerifericoVR = new Vector2(0.82f, -0.30f); // Rigorosamente na periferia inferior direita

    // Dados estruturados de uma tripla semântica coletada
    public class EntradaTriplaABox
    {
        public int indice;
        public string idInstancia;
        public string nomeClasse;
        public string sujeito;
        public string predicado;
        public string objeto;
        public string axiomaDL;
        public int pontos;
        public bool consistente;
        public float timestamp;
    }

    // Estrutura de UI para cada Card do Feed
    private class CardTriplaUI
    {
        public GameObject objCard;
        public Image imgFundo;
        public Outline outlineBorda;
        public TMP_Text txtLinha1Status;
        public TMP_Text txtLinha2Tripla;
        public TMP_Text txtLinha3Axioma;
    }

    private Canvas m_CanvasInventario;
    private GameObject m_PainelRaiz;
    private TMP_Text m_TextoTitulo;
    private TMP_Text m_TextoMetricasABox;
    private TMP_Text m_TextoRodape;

    private readonly List<CardTriplaUI> m_CardsUI = new List<CardTriplaUI>();
    private readonly List<EntradaTriplaABox> m_HistoricoTriplas = new List<EntradaTriplaABox>();
    private const int MAX_CARDS_EXIBIDOS = 3;

    public IReadOnlyList<EntradaTriplaABox> HistoricoTriplas => m_HistoricoTriplas;

    public void DefinirVisibilidadePainel(bool visivel)
    {
        if (m_PainelRaiz != null && m_PainelRaiz.activeSelf != visivel)
        {
            m_PainelRaiz.SetActive(visivel);
        }
    }

    private ControladorXRJogador m_ControladorJogador;
    private float m_TimerFlashCardRecente = 0f;

    private void Awake()
    {
        if (Instancia != null && Instancia != this)
        {
            Destroy(gameObject);
            return;
        }
        Instancia = this;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void GarantirNaCena()
    {
        if (Instancia == null)
        {
            var existente = FindAnyObjectByType<PainelInventarioABoxVR>();
            if (existente == null)
            {
                GameObject obj = new GameObject("Painel_Inventario_ABox_VR");
                obj.AddComponent<PainelInventarioABoxVR>();
                DontDestroyOnLoad(obj);
            }
        }
    }

    private void Start()
    {
        ConstruirInterfaceInventario();
        LocalizarReferencias();
    }

    private void Update()
    {
        if (!exibirInventario || m_PainelRaiz == null) return;

        // Oculta durante Hub Inicial, GameOver, Vitória ou Tela de Resultado Final
        bool telaFinalAberta = PainelGrafoSemanticoProtege.Instancia != null && PainelGrafoSemanticoProtege.Instancia.ExibindoTelaFinal;
        if (telaFinalAberta || (HubInicialAmbiente.Instancia != null &&
            (HubInicialAmbiente.Instancia.hubAberto ||
             HubInicialAmbiente.Instancia.GameOverAberto ||
             HubInicialAmbiente.Instancia.VitoriaAberta ||
             !HubInicialAmbiente.Instancia.partidaEmAndamento)))
        {
            if (m_PainelRaiz.activeSelf) m_PainelRaiz.SetActive(false);
            return;
        }

        if (!m_PainelRaiz.activeSelf) m_PainelRaiz.SetActive(true);

        LocalizarReferencias();
        AtualizarAnimacaoFlashCardRecente();
    }

    private void LateUpdate()
    {
        AtualizarAcompanhamentoOlharVR();
    }

    /// <summary>
    /// Amortece o acompanhamento inercial do olhar em VR, fixando o painel no quadrante direito.
    /// </summary>
    private void AtualizarAcompanhamentoOlharVR()
    {
        if (m_CanvasInventario == null || !exibirInventario) return;

        bool modoPC = Application.isEditor || !UnityEngine.XR.XRSettings.isDeviceActive;

        if (modoPC)
        {
            // No Desktop / Editor PC: ScreenSpaceOverlay ancorado exclusivamente no canto direito
            if (m_CanvasInventario.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                m_CanvasInventario.renderMode = RenderMode.ScreenSpaceOverlay;
                m_CanvasInventario.transform.localScale = Vector3.one;
            }
            if (m_PainelRaiz != null)
            {
                RectTransform rt = m_PainelRaiz.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(1f, 1f);
                    rt.anchorMax = new Vector2(1f, 1f);
                    rt.pivot = new Vector2(1f, 1f);
                    rt.sizeDelta = tamanhoPainel;
                    rt.anchoredPosition = new Vector2(-24f, -24f);
                }
            }
        }
        else
        {
            if (m_PainelRaiz == null || !m_PainelRaiz.activeSelf) return;

            Camera cam = Camera.main;
            if (m_ControladorJogador != null && m_ControladorJogador.cameraPrincipalXR != null)
            {
                cam = m_ControladorJogador.cameraPrincipalXR;
            }
            if (cam == null) return;

            if (m_CanvasInventario.renderMode != RenderMode.WorldSpace)
            {
                m_CanvasInventario.renderMode = RenderMode.WorldSpace;
            }
            m_CanvasInventario.transform.localScale = Vector3.one * 0.00075f;

            if (m_PainelRaiz != null)
            {
                RectTransform rt = m_PainelRaiz.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0.5f, 0.5f);
                    rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.sizeDelta = tamanhoPainel;
                    rt.anchoredPosition = Vector2.zero;
                }
            }

            Vector3 camPos = cam.transform.position;
            Vector3 camFwd = cam.transform.forward;
            Vector3 camUp = cam.transform.up;
            Vector3 camRight = cam.transform.right;

            // Posicionado suavemente no campo de visão periférico direito (fora da frente do Pac-Man)
            Vector3 posAlvo = camPos + camFwd * distanciaVisaoVR + camRight * offsetPerifericoVR.x + camUp * offsetPerifericoVR.y;

            // Canting esférico voltado para os olhos do usuário (cockpit HUD periférico)
            Vector3 dirOlhar = (posAlvo - camPos).normalized;
            Quaternion rotAlvo = Quaternion.LookRotation(dirOlhar, camUp);

            m_CanvasInventario.transform.position = Vector3.Lerp(m_CanvasInventario.transform.position, posAlvo, Time.unscaledDeltaTime * 14f);
            m_CanvasInventario.transform.rotation = Quaternion.Slerp(m_CanvasInventario.transform.rotation, rotAlvo, Time.unscaledDeltaTime * 14f);
        }
    }

    private void LocalizarReferencias()
    {
        if (m_ControladorJogador == null)
        {
            m_ControladorJogador = FindAnyObjectByType<ControladorXRJogador>();
        }
    }

    private void AtualizarAnimacaoFlashCardRecente()
    {
        if (m_TimerFlashCardRecente > 0f && m_CardsUI.Count > 0)
        {
            m_TimerFlashCardRecente -= Time.deltaTime * 3.5f;
            float pulso = Mathf.Clamp01(m_TimerFlashCardRecente);
            if (m_CardsUI[0].outlineBorda != null)
            {
                Color cBase = m_HistoricoTriplas.Count > 0 && !m_HistoricoTriplas[m_HistoricoTriplas.Count - 1].consistente
                    ? new Color(1.0f, 0.20f, 0.25f)
                    : new Color(0.0f, 1.0f, 0.65f);
                m_CardsUI[0].outlineBorda.effectColor = Color.Lerp(cBase * 0.60f, Color.white, pulso);
            }
        }
    }

    // =========================================================================
    // API PÚBLICA DE TRIPLAS ABox
    // =========================================================================

    /// <summary>
    /// Registra uma nova instância coletada pelo Pac-Man ou uma inconsistência detectada pelo Reasoner.
    /// Formata a tripla semântica e atualiza os cards do HUD.
    /// </summary>
    public void RegistrarInstanciaColetada(string idInstancia, string nomeClasse, string expressaoDL, int pontos, bool consistente)
    {
        var tripla = FormatarTriplaSemantica(nomeClasse, expressaoDL, consistente);

        EntradaTriplaABox entrada = new EntradaTriplaABox
        {
            indice = m_HistoricoTriplas.Count + 1,
            idInstancia = idInstancia,
            nomeClasse = nomeClasse,
            sujeito = tripla.sujeito,
            predicado = tripla.predicado,
            objeto = tripla.objeto,
            axiomaDL = tripla.axioma,
            pontos = pontos,
            consistente = consistente,
            timestamp = Time.time
        };

        m_HistoricoTriplas.Add(entrada);
        m_TimerFlashCardRecente = 1.0f;

        AtualizarCardsUI();
        AtualizarCabecalhoERodape();
    }

    /// <summary>
    /// Reseta todo o inventário de ABox para um estado inicial limpo (nova partida).
    /// </summary>
    public void LimparInventario()
    {
        m_HistoricoTriplas.Clear();
        m_TimerFlashCardRecente = 0f;

        for (int i = 0; i < m_CardsUI.Count; i++)
        {
            var card = m_CardsUI[i];
            if (card == null || card.objCard == null) continue;
            if (i == 0)
            {
                card.objCard.SetActive(true);
                if (card.imgFundo != null) card.imgFundo.color = new Color(0.027f, 0.063f, 0.094f, 0.92f);
                if (card.outlineBorda != null) card.outlineBorda.effectColor = new Color(0.20f, 0.35f, 0.50f, 0.40f);
                if (card.txtLinha1Status != null) card.txtLinha1Status.text = "<color=#64748B><b>[ABox EM ESPERA]</b></color>";
                if (card.txtLinha2Tripla != null) card.txtLinha2Tripla.text = "<color=#94A3B8><i>Explore o labirinto para coletar Triplas RDF (ABox).</i></color>";
                if (card.txtLinha3Axioma != null) card.txtLinha3Axioma.text = "";
            }
            else
            {
                card.objCard.SetActive(false);
            }
        }

        AtualizarCabecalhoERodape();
    }

    private void AtualizarCardsUI()
    {
        int total = m_HistoricoTriplas.Count;

        for (int i = 0; i < MAX_CARDS_EXIBIDOS; i++)
        {
            CardTriplaUI card = m_CardsUI[i];
            int idxTripla = total - 1 - i;

            if (idxTripla >= 0 && idxTripla < total)
            {
                card.objCard.SetActive(true);
                EntradaTriplaABox t = m_HistoricoTriplas[idxTripla];

                if (t.consistente)
                {
                    card.imgFundo.color = new Color(0.027f, 0.063f, 0.094f, 0.92f); // #071018 92% alpha
                    card.outlineBorda.effectColor = new Color(0.0f, 1.0f, 0.60f, (i == 0) ? 0.95f : 0.55f);

                    string ptsStr = (t.pontos >= 0) ? $"+{t.pontos}" : $"{t.pontos}";
                    card.txtLinha1Status.text = $"#{t.indice:D2}  <color=#4ADE80><b>[OK CONSISTENTE]</b></color>   <color=#FACC15><b>{ptsStr} pts</b></color>";
                    card.txtLinha2Tripla.text = $"<color=#FFFFFF><b>{t.sujeito}</b></color> <color=#67E8F9>--[{t.predicado}]-></color> <color=#FFFFFF><b>{t.objeto}</b></color>";
                    if (card.txtLinha3Axioma != null) card.txtLinha3Axioma.text = "";
                }
                else
                {
                    card.imgFundo.color = new Color(0.027f, 0.063f, 0.094f, 0.92f); // #071018 92% alpha
                    card.outlineBorda.effectColor = new Color(1.0f, 0.25f, 0.30f, (i == 0) ? 0.95f : 0.55f);

                    card.txtLinha1Status.text = $"#{t.indice:D2}  <color=#FF4D4D><b>[ERRO INCONSISTÊNCIA]</b></color>   <color=#FFAAAA><b>{t.pontos} pts</b></color>";
                    card.txtLinha2Tripla.text = $"<color=#FFFFFF><b>{t.sujeito}</b></color> <color=#FF6B81>--[{t.predicado}]-></color> <color=#FFFFFF><b>{t.objeto}</b></color>";
                    if (card.txtLinha3Axioma != null) card.txtLinha3Axioma.text = "";
                }
            }
            else
            {
                if (total == 0 && i == 0)
                {
                    card.objCard.SetActive(true);
                }
                else
                {
                    card.objCard.SetActive(false);
                }
            }
        }
    }

    private void AtualizarCabecalhoERodape()
    {
        int coletadas = m_HistoricoTriplas.Count;
        int totalMissao = 38;

        if (GerenciadorCenarioSemantico.Instancia != null)
        {
            // Pega o total real de Triplas RDF da missão
            totalMissao = GerenciadorCenarioSemantico.Instancia.TotalTriplasMissao;
        }

        bool temInconsistencia = false;
        for (int i = 0; i < m_HistoricoTriplas.Count; i++)
        {
            if (!m_HistoricoTriplas[i].consistente) { temInconsistencia = true; break; }
        }

        string reasonerStatus = temInconsistencia
            ? "<color=#FF4D4D><b>HermiT [VIOLAÇÃO]</b></color>"
            : "<color=#4ADE80><b>HermiT [OK]</b></color>";

        if (m_TextoMetricasABox != null)
        {
            m_TextoMetricasABox.text = $"Triplas RDF: <color=#67E8F9><b>{coletadas} / {totalMissao}</b></color>   |   Reasoner: {reasonerStatus}";
        }

        if (m_TextoRodape != null)
        {
            m_TextoRodape.text = $"ABox Ativa: <b>{coletadas} Triplas RDF</b>  •  OWL 2 DL  •  CIn-UFPE";
        }
    }

    /// <summary>
    /// Gera a tripla ontológica formal (Sujeito, Predicado, Objeto e Axioma DL) baseada no conceito.
    /// </summary>
    public static (string sujeito, string predicado, string objeto, string axioma) FormatarTriplaSemantica(string nomeClasse, string expressaoDL, bool consistente)
    {
        string c = (nomeClasse ?? "").ToLower();

        if (c.Contains("ascaris"))
        {
            return (
                "[Homo_sapiens]",
                "has_parasite",
                "[Ascaris_lumbricoides]",
                "Nematoda SUBCLASS_OF Parasite AND SOME has_host.Human AND SOME has_habitat.SmallIntestine"
            );
        }
        else if (c.Contains("enterobius"))
        {
            return (
                "[Homo_sapiens]",
                "has_parasite",
                "[Enterobius_vermicularis]",
                "Nematoda SUBCLASS_OF Parasite AND SOME has_host.Human AND SOME has_habitat.PerianalRegion"
            );
        }
        else if (c.Contains("taenia"))
        {
            return (
                "[Homo_sapiens]",
                "has_parasite",
                "[Taenia_solium]",
                "Platyhelminthes SUBCLASS_OF Parasite AND SOME has_host.Human AND SOME has_transmission.UndercookedPork"
            );
        }
        else if (c.Contains("treatment") || c.Contains("tratamento") || c.Contains("pellet"))
        {
            return (
                "[AntiparasiticTreatment]",
                "neutralizes",
                "[InfectionVector]",
                "Treatment SUBCLASS_OF SOME neutralizes.InfectionVector [SWRL Regra 1]"
            );
        }
        else if (c.Contains("nematoda"))
        {
            return (
                "[Nematoda]",
                "subClassOf",
                "[Parasite]",
                "Nematoda SUBCLASS_OF Parasite AND SOME has_morphology.Cylindrical"
            );
        }
        else if (c.Contains("platyhelm"))
        {
            return (
                "[Platyhelminthes]",
                "subClassOf",
                "[Parasite]",
                "Platyhelminthes SUBCLASS_OF Parasite AND SOME has_morphology.FlatSegments"
            );
        }
        else if (c.Contains("inconsistent") || c.Contains("specimen"))
        {
            return (
                "[Specimen_Invalido]",
                "has_morphology",
                "[Cylindrical AND FlatSegments]",
                "Inconsistencia ALCQ(D): MorfologiaMutuamenteExclusiva == FALSE"
            );
        }
        else if (c.Contains("hostmismatch") || c.Contains("mismatch"))
        {
            return (
                "[Ascaris_lumbricoides]",
                "has_host",
                "[InvertebrateVector]",
                "Violacao de Dominio: IncompatibleHostRestriction SUBCLASS_OF FALSE"
            );
        }
        else if (c.Contains("habitat"))
        {
            return (
                "[Enterobius_vermicularis]",
                "has_habitat",
                "[BloodStream]",
                "Violacao Anatomica: InvalidHabitatForNematoda SUBCLASS_OF FALSE"
            );
        }

        // Fallback genérico DL
        string s = "[Pesquisador]";
        string p = consistente ? "synthesizes" : "violates_axiom";
        string o = $"[{nomeClasse}]";
        string ax = string.IsNullOrEmpty(expressaoDL) ? "ConceptAssertion(C, a)" : expressaoDL;

        return (s, p, o, ax);
    }

    // =========================================================================
    // CONSTRUÇÃO PROCEDURAL DA INTERFACE WORLDSPACE VR
    // =========================================================================

    private void ConstruirInterfaceInventario()
    {
        // 1. Canvas com suporte híbrido PC (ScreenSpaceOverlay) e VR (WorldSpace)
        GameObject canvasObj = new GameObject("Inventario_Canvas_VR", typeof(Canvas), typeof(CanvasScaler));
        canvasObj.transform.SetParent(transform, false);

        m_CanvasInventario = canvasObj.GetComponent<Canvas>();
        m_CanvasInventario.sortingOrder = 25;

        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        bool modoPC = Application.isEditor || !UnityEngine.XR.XRSettings.isDeviceActive;
        if (modoPC)
        {
            m_CanvasInventario.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.transform.localScale = Vector3.one;
        }
        else
        {
            m_CanvasInventario.renderMode = RenderMode.WorldSpace;
            canvasObj.transform.localScale = Vector3.one * 0.00075f;
        }

        // 2. Painel Raiz (Dark Grid Frame)
        m_PainelRaiz = new GameObject("Painel_Inventario_Raiz", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
        m_PainelRaiz.transform.SetParent(canvasObj.transform, false);

        tamanhoPainel = new Vector2(460f, 365f);
        RectTransform rtRaiz = m_PainelRaiz.GetComponent<RectTransform>();
        rtRaiz.sizeDelta = tamanhoPainel;
        if (modoPC)
        {
            rtRaiz.anchorMin = new Vector2(1f, 1f);
            rtRaiz.anchorMax = new Vector2(1f, 1f);
            rtRaiz.pivot = new Vector2(1f, 1f);
            rtRaiz.anchoredPosition = new Vector2(-24f, -24f);
        }
        else
        {
            rtRaiz.anchorMin = new Vector2(0.5f, 0.5f);
            rtRaiz.anchorMax = new Vector2(0.5f, 0.5f);
            rtRaiz.pivot = new Vector2(0.5f, 0.5f);
            rtRaiz.anchoredPosition = Vector2.zero;
        }

        Image imgFundo = m_PainelRaiz.GetComponent<Image>();
        imgFundo.color = new Color(0.020f, 0.035f, 0.055f, 0.95f); // Fundo escuro de alta opacidade

        Outline outline = m_PainelRaiz.GetComponent<Outline>();
        outline.effectColor = new Color(0.0f, 1.0f, 0.65f, 0.85f); // Verde esmeralda neon
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        // 3. Cabeçalho
        m_TextoTitulo = CriarTexto(m_PainelRaiz.transform, "Titulo_Inventario",
            "<b>INVENTÁRIO ABox (TRIPLAS ATIVAS)</b>", 14, FontStyles.Bold,
            new Vector2(14f, -12f), new Vector2(432f, 22f),
            new Color(0.0f, 1.0f, 0.65f), TextAlignmentOptions.Left);

        m_TextoMetricasABox = CriarTexto(m_PainelRaiz.transform, "Texto_Metricas",
            "Triplas RDF: <color=#67E8F9><b>0 / 38</b></color>   |   Reasoner: <color=#4ADE80><b>HermiT [OK]</b></color>", 12.5f, FontStyles.Normal,
            new Vector2(14f, -36f), new Vector2(432f, 20f),
            new Color(0.85f, 0.92f, 1.0f), TextAlignmentOptions.Left);

        // Linha divisória
        CriarLinhaDivisoria(m_PainelRaiz.transform, new Vector2(12f, -60f), 436f);

        // 4. Feed de 3 Cards de Triplas
        float yInicial = -68f;
        float alturaCard = 74f;
        float espacamento = 8f;

        for (int i = 0; i < MAX_CARDS_EXIBIDOS; i++)
        {
            GameObject cardObj = new GameObject($"Card_Tripla_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
            cardObj.transform.SetParent(m_PainelRaiz.transform, false);

            RectTransform rtCard = cardObj.GetComponent<RectTransform>();
            rtCard.anchorMin = new Vector2(0.5f, 1f);
            rtCard.anchorMax = new Vector2(0.5f, 1f);
            rtCard.pivot = new Vector2(0.5f, 1f);
            rtCard.anchoredPosition = new Vector2(0f, yInicial - i * (alturaCard + espacamento));
            rtCard.sizeDelta = new Vector2(436f, alturaCard);

            Image imgCard = cardObj.GetComponent<Image>();
            imgCard.color = new Color(0.027f, 0.063f, 0.094f, 0.92f); // #071018 92% alpha

            Outline outlineCard = cardObj.GetComponent<Outline>();
            outlineCard.effectDistance = new Vector2(1.5f, -1.5f);
            outlineCard.effectColor = new Color(0.20f, 0.35f, 0.50f, 0.40f);

            TMP_Text txtL1 = CriarTexto(cardObj.transform, "Linha1_Status",
                "<color=#64748B><b>[ABox EM ESPERA]</b></color>", 13.5f, FontStyles.Bold,
                new Vector2(12f, -8f), new Vector2(412f, 22f), Color.white, TextAlignmentOptions.Left);

            TMP_Text txtL2 = CriarTexto(cardObj.transform, "Linha2_Tripla",
                "<color=#94A3B8><i>Explore o labirinto para coletar Triplas RDF (ABox).</i></color>", 16.5f, FontStyles.Bold,
                new Vector2(12f, -32f), new Vector2(412f, 38f), Color.white, TextAlignmentOptions.Left);

            TMP_Text txtL3 = CriarTexto(cardObj.transform, "Linha3_Axioma",
                "", 9f, FontStyles.Normal,
                new Vector2(12f, -54f), new Vector2(412f, 16f), Color.white, TextAlignmentOptions.Left);
            txtL3.gameObject.SetActive(false); // Oculto no HUD em tempo real para eliminar corte e poluição

            CardTriplaUI cardUI = new CardTriplaUI
            {
                objCard = cardObj,
                imgFundo = imgCard,
                outlineBorda = outlineCard,
                txtLinha1Status = txtL1,
                txtLinha2Tripla = txtL2,
                txtLinha3Axioma = txtL3
            };

            if (i > 0) cardObj.SetActive(false);
            m_CardsUI.Add(cardUI);
        }

        // 5. Rodapé
        CriarLinhaDivisoria(m_PainelRaiz.transform, new Vector2(12f, -318f), 436f);

        m_TextoRodape = CriarTexto(m_PainelRaiz.transform, "Texto_Rodape",
            "ABox Ativa: <b>0 Triplas RDF</b>  •  OWL 2 DL  •  CIn-UFPE", 11.5f, FontStyles.Normal,
            new Vector2(14f, -328f), new Vector2(432f, 22f),
            new Color(0.65f, 0.75f, 0.90f), TextAlignmentOptions.Center);
    }

    private static void CriarLinhaDivisoria(Transform pai, Vector2 pos, float largura)
    {
        GameObject linha = new GameObject("Linha_Divisoria", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        linha.transform.SetParent(pai, false);

        RectTransform rt = linha.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(largura, 1.2f);

        Image img = linha.GetComponent<Image>();
        img.color = new Color(0.0f, 1.0f, 0.65f, 0.40f);
    }

    private static TMP_Text CriarTexto(Transform pai, string nome, string conteudo, float tamanhoFonte,
        FontStyles estilo, Vector2 posicao, Vector2 dimensao, Color cor,
        TextAlignmentOptions alinhamento = TextAlignmentOptions.Left)
    {
        GameObject go = new GameObject(nome, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(pai, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = posicao;
        rt.sizeDelta = dimensao;

        TMP_Text texto = go.GetComponent<TextMeshProUGUI>();
        texto.text = conteudo;
        texto.fontSize = tamanhoFonte;
        texto.fontStyle = estilo;
        texto.color = cor;
        texto.alignment = alinhamento;
        texto.raycastTarget = false;
        texto.textWrappingMode = TextWrappingModes.Normal;
        texto.overflowMode = TextOverflowModes.Truncate;

        return texto;
    }
}
