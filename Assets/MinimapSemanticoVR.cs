using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ============================================================================
/// PAC-MAN SEMÂNTICO (CIn-UFPE)
/// MINIMAPA SEMÂNTICO VR (TBox & Radar Espacial 2D)
/// ============================================================================
/// Painel holográfico flutuante WorldSpace no campo de visão esquerdo do Meta Quest 3.
/// 
/// Funcionalidades Ontológicas e Espaciais:
/// 1. Grafo Taxonômico Dinâmico (TBox):
///    - Representa a árvore de classes do Protégé (owl:Thing -> Parasitas/Hospedeiros -> Subclasses).
///    - Nós e arestas iniciam translúcidos/apagados e se iluminam em neon vívido (#00E5FF, #B388FF, #00FF99)
///      conforme o Pac-Man caminha pelas zonas do labirinto ou coleta instâncias daquela classe.
///    - Dá o feedback visual contínuo de que o pesquisador está sintetizando a ontologia em tempo real.
/// 
/// 2. Radar Espacial 2D do Labirinto:
///    - Mapa tático em grade exibindo as coordenadas (-10.5 a +10.5).
///    - Marcador do Pac-Man (● amarelo) com rastreamento contínuo em tempo real.
///    - Marcador da Orbe Alvo ativa (◆ ciano pulsante) indicando o próximo conceito a coletar.
///    - Marcadores dos Fantasmas (▲ vermelho) para orientação tática contra inconsistências.
/// 
/// 3. Ergonomia e Conforto VR (Quest 3 Pancake Lenses):
///    - Posicionamento WorldSpace no quadrante periférico esquerdo.
///    - Amortecimento inercial suave (Damping / Slerp 14f) para eliminação de motion sickness.
///    - Ocultamento automático durante o Menu Hub, Vitória ou GameOver.
/// ============================================================================
/// </summary>
[DisallowMultipleComponent]
public class MinimapSemanticoVR : MonoBehaviour
{
    public static MinimapSemanticoVR Instancia { get; private set; }

    [Header("Configurações Visuais e Ergonomia VR")]
    public bool exibirMinimapa = true;
    public Vector2 tamanhoPainel = new Vector2(280f, 280f);
    public float distanciaVisaoVR = 1.35f;
    public Vector2 offsetPerifericoVR = new Vector2(-0.82f, -0.30f); // Rigorosamente na periferia inferior esquerda

    // Representação interna de Nó da TBox
    private class NoTaxonomicoUI
    {
        public string id;
        public string rotulo;
        public Vector2 posicaoLocal;
        public Color corPadrao;
        public bool descoberto;
        public Image imgFundo;
        public Image imgBorda;
        public TMP_Text txtRotulo;
        public GameObject objRaiz;
    }

    // Representação interna de Aresta da TBox
    private class ArestaTaxonomicaUI
    {
        public string idOrigem;
        public string idDestino;
        public RectTransform rtLinha;
        public Image imgLinha;
        public bool ativa;
    }

    private Canvas m_CanvasMinimapa;
    private GameObject m_PainelRaiz;
    private TMP_Text m_TextoTitulo;
    private TMP_Text m_TextoZonaAtual;

    // Área do Radar 2D
    private RectTransform m_RtAreaRadar;
    private RectTransform m_RtMarcadorPacMan;
    private RectTransform m_RtMarcadorOrbe;
    private Image m_ImgMarcadorOrbe;
    private readonly List<RectTransform> m_MarcadoresFantasmas = new List<RectTransform>();

    // Grafo Taxonômico
    private readonly Dictionary<string, NoTaxonomicoUI> m_NosTaxonomia = new Dictionary<string, NoTaxonomicoUI>();
    private readonly List<ArestaTaxonomicaUI> m_ArestasTaxonomia = new List<ArestaTaxonomicaUI>();

    // Cache e rastreamento
    private ControladorXRJogador m_ControladorJogador;
    private Vector3 m_PosicaoOrbeAtiva = Vector3.zero;
    private bool m_TemOrbeAtiva = false;
    private string m_UltimaZona = "";
    private float m_TimerPulso = 0f;

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
            var existente = FindAnyObjectByType<MinimapSemanticoVR>();
            if (existente == null)
            {
                GameObject obj = new GameObject("Minimap_Semantico_VR");
                obj.AddComponent<MinimapSemanticoVR>();
                if (Application.isPlaying) DontDestroyOnLoad(obj);
            }
        }
    }

    private void Start()
    {
        ConstruirInterfaceMinimapa();
        InicializarGrafoTaxonomicoTBox();
        LocalizarReferencias();
    }

    private void Update()
    {
        if (!exibirMinimapa || m_PainelRaiz == null) return;

        // Oculta durante Hub Inicial, GameOver ou Vitória
        if (HubInicialAmbiente.Instancia != null &&
            (HubInicialAmbiente.Instancia.hubAberto ||
             HubInicialAmbiente.Instancia.GameOverAberto ||
             HubInicialAmbiente.Instancia.VitoriaAberta ||
             !HubInicialAmbiente.Instancia.partidaEmAndamento))
        {
            if (m_PainelRaiz.activeSelf) m_PainelRaiz.SetActive(false);
            return;
        }

        if (!m_PainelRaiz.activeSelf) m_PainelRaiz.SetActive(true);

        LocalizarReferencias();
        AtualizarDeteccaoZonaEPacMan();
        AtualizarRadarEspacial2D();
    }

    private void LateUpdate()
    {
        AtualizarAcompanhamentoOlharVR();
    }

    /// <summary>
    /// Amortece o acompanhamento inercial do olhar em VR, fixando o painel no quadrante esquerdo.
    /// </summary>
    private void AtualizarAcompanhamentoOlharVR()
    {
        if (m_CanvasMinimapa == null || !exibirMinimapa) return;
        if (m_PainelRaiz == null || !m_PainelRaiz.activeSelf) return;

        Camera cam = Camera.main;
        if (m_ControladorJogador != null && m_ControladorJogador.cameraPrincipalXR != null)
        {
            cam = m_ControladorJogador.cameraPrincipalXR;
        }
        if (cam == null) return;

        bool emVR = UnityEngine.XR.XRSettings.isDeviceActive || (UnityEngine.XR.XRSettings.enabled && !string.IsNullOrEmpty(UnityEngine.XR.XRSettings.loadedDeviceName)) ||
                    (UnityEngine.XR.Management.XRGeneralSettings.Instance != null && UnityEngine.XR.Management.XRGeneralSettings.Instance.Manager != null && UnityEngine.XR.Management.XRGeneralSettings.Instance.Manager.activeLoader != null) ||
                    Application.platform == RuntimePlatform.Android;

        if (emVR)
        {
            if (m_CanvasMinimapa.renderMode != RenderMode.WorldSpace)
            {
                m_CanvasMinimapa.renderMode = RenderMode.WorldSpace;
            }
            m_CanvasMinimapa.transform.localScale = Vector3.one * 0.00075f;

            Vector3 camPos = cam.transform.position;
            Vector3 camFwd = cam.transform.forward;
            Vector3 camUp = cam.transform.up;
            Vector3 camRight = cam.transform.right;

            // Posicionado suavemente no campo de visão periférico esquerdo (fora da frente do Pac-Man)
            Vector3 posAlvo = camPos + camFwd * distanciaVisaoVR + camRight * offsetPerifericoVR.x + camUp * offsetPerifericoVR.y;
            
            // Canting esférico voltado para os olhos do usuário (cockpit HUD periférico)
            Vector3 dirOlhar = (posAlvo - camPos).normalized;
            Quaternion rotAlvo = Quaternion.LookRotation(dirOlhar, camUp);

            m_CanvasMinimapa.transform.position = Vector3.Lerp(m_CanvasMinimapa.transform.position, posAlvo, Time.unscaledDeltaTime * 14f);
            m_CanvasMinimapa.transform.rotation = Quaternion.Slerp(m_CanvasMinimapa.transform.rotation, rotAlvo, Time.unscaledDeltaTime * 14f);
        }
        else
        {
            // No Desktop / PC: ScreenSpaceOverlay
            if (m_CanvasMinimapa.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                m_CanvasMinimapa.renderMode = RenderMode.ScreenSpaceOverlay;
                m_CanvasMinimapa.transform.localScale = Vector3.one;
            }
            if (m_PainelRaiz != null)
            {
                RectTransform rt = m_PainelRaiz.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0f, 0.45f);
                    rt.anchorMax = new Vector2(0f, 0.45f);
                    rt.pivot = new Vector2(0f, 0.5f);
                    rt.anchoredPosition = new Vector2(25f, 0f);
                }
            }
        }
    }

    private void LocalizarReferencias()
    {
        if (m_ControladorJogador == null)
        {
            m_ControladorJogador = FindAnyObjectByType<ControladorXRJogador>();
        }
    }

    /// <summary>
    /// Detecta em tempo real a zona/hemisfério em que o Pac-Man está navegando e acende os nós da TBox.
    /// </summary>
    private void AtualizarDeteccaoZonaEPacMan()
    {
        Vector3 posPac = ObterPosicaoPacMan();
        string zonaNome = "Centro (Hub Ontológico)";

        if (posPac.z > 1.2f)
        {
            // Hemisfério Norte: Parasitas
            IluminarNodo("Parasite");
            IluminarAresta("owl:Thing", "Parasite");

            if (posPac.x < -1.2f)
            {
                zonaNome = "Norte-Oeste (Nematoda)";
                IluminarNodo("Nematoda");
                IluminarAresta("Parasite", "Nematoda");
            }
            else if (posPac.x > 1.2f)
            {
                zonaNome = "Norte-Leste (Platyhelminthes)";
                IluminarNodo("Platyhelminthes");
                IluminarAresta("Parasite", "Platyhelminthes");
            }
            else
            {
                zonaNome = "Corredor Norte (Parasitas)";
            }
        }
        else if (posPac.z < -1.2f)
        {
            // Hemisfério Sul: Hospedeiros
            IluminarNodo("Host");
            IluminarAresta("owl:Thing", "Host");

            if (posPac.x < -1.2f)
            {
                zonaNome = "Sul-Oeste (Hospedeiro Humano)";
                IluminarNodo("Human");
                IluminarAresta("Host", "Human");
            }
            else if (posPac.x > 1.2f)
            {
                zonaNome = "Sul-Leste (Hospedeiro Suíno)";
                IluminarNodo("Pig");
                IluminarAresta("Host", "Pig");
            }
            else
            {
                zonaNome = "Corredor Sul (Hospedeiros)";
            }
        }

        if (zonaNome != m_UltimaZona && m_TextoZonaAtual != null)
        {
            m_UltimaZona = zonaNome;
            string corZona = posPac.z > 1.2f ? "#B388FF" : (posPac.z < -1.2f ? "#00E5FF" : "#FFD700");
            m_TextoZonaAtual.text = $"ZONA ATUAL: <color={corZona}><b>{zonaNome}</b></color>";
        }
    }

    /// <summary>
    /// Rastreia posições 2D no radar do labirinto.
    /// </summary>
    private void AtualizarRadarEspacial2D()
    {
        if (m_RtAreaRadar == null) return;

        Vector2 tamRadar = m_RtAreaRadar.sizeDelta;
        float halfW = tamRadar.x * 0.45f;
        float halfH = tamRadar.y * 0.44f;
        const float RAIO_LABIRINTO = 10.5f;

        // 1. Pac-Man
        Vector3 posPac = ObterPosicaoPacMan();
        if (m_RtMarcadorPacMan != null)
        {
            float nx = Mathf.Clamp(posPac.x / RAIO_LABIRINTO, -1f, 1f);
            float nz = Mathf.Clamp(posPac.z / RAIO_LABIRINTO, -1f, 1f);
            m_RtMarcadorPacMan.anchoredPosition = new Vector2(nx * halfW, nz * halfH);
        }

        // 2. Orbe Alvo Ativa (Pulsante)
        m_TimerPulso += Time.deltaTime * 6f;
        if (m_RtMarcadorOrbe != null)
        {
            if (m_TemOrbeAtiva)
            {
                if (!m_RtMarcadorOrbe.gameObject.activeSelf) m_RtMarcadorOrbe.gameObject.SetActive(true);

                float nx = Mathf.Clamp(m_PosicaoOrbeAtiva.x / RAIO_LABIRINTO, -1f, 1f);
                float nz = Mathf.Clamp(m_PosicaoOrbeAtiva.z / RAIO_LABIRINTO, -1f, 1f);
                m_RtMarcadorOrbe.anchoredPosition = new Vector2(nx * halfW, nz * halfH);

                float escalaPulso = 1.0f + 0.30f * Mathf.Sin(m_TimerPulso);
                m_RtMarcadorOrbe.localScale = Vector3.one * escalaPulso;
            }
            else
            {
                if (m_RtMarcadorOrbe.gameObject.activeSelf) m_RtMarcadorOrbe.gameObject.SetActive(false);
            }
        }

        // 3. Fantasmas
        var fantasmas = ObstaculoInvalidoSemantico.FantasmasAtivos;
        for (int i = 0; i < m_MarcadoresFantasmas.Count; i++)
        {
            if (i < fantasmas.Count && fantasmas[i] != null)
            {
                m_MarcadoresFantasmas[i].gameObject.SetActive(true);
                Vector3 p = fantasmas[i].transform.position;
                float nx = Mathf.Clamp(p.x / RAIO_LABIRINTO, -1f, 1f);
                float nz = Mathf.Clamp(p.z / RAIO_LABIRINTO, -1f, 1f);
                m_MarcadoresFantasmas[i].anchoredPosition = new Vector2(nx * halfW, nz * halfH);
            }
            else
            {
                m_MarcadoresFantasmas[i].gameObject.SetActive(false);
            }
        }
    }

    private Vector3 ObterPosicaoPacMan()
    {
        if (m_ControladorJogador != null)
        {
            return m_ControladorJogador.transform.position;
        }
        GameObject vis = GameObject.Find("Visual_PacMan");
        if (vis != null) return vis.transform.position;
        return Vector3.zero;
    }

    // =========================================================================
    // API PÚBLICA DE ATUALIZAÇÃO ONTOLÓGICA
    // =========================================================================

    /// <summary>
    /// Notifica que uma nova instância ABox foi coletada: ilumina o nó e aresta da TBox.
    /// </summary>
    public void NotificarInstanciaColetada(string nomeClasse, Vector3 pos)
    {
        string c = (nomeClasse ?? "").ToLower();
        if (c.Contains("ascaris") || c.Contains("enterobius") || c.Contains("nematoda"))
        {
            IluminarNodo("Nematoda");
            IluminarNodo("Parasite");
            IluminarAresta("Parasite", "Nematoda");
            IluminarAresta("owl:Thing", "Parasite");
        }
        else if (c.Contains("taenia") || c.Contains("schistosoma") || c.Contains("platyhelm"))
        {
            IluminarNodo("Platyhelminthes");
            IluminarNodo("Parasite");
            IluminarAresta("Parasite", "Platyhelminthes");
            IluminarAresta("owl:Thing", "Parasite");
        }
        else if (c.Contains("treatment") || c.Contains("tratamento") || c.Contains("pellet"))
        {
            IluminarNodo("Treatment");
            IluminarAresta("Parasite", "Treatment");
        }
        else if (c.Contains("human") || c.Contains("hospedeiro"))
        {
            IluminarNodo("Human");
            IluminarNodo("Host");
            IluminarAresta("Host", "Human");
            IluminarAresta("owl:Thing", "Host");
        }
        else if (c.Contains("pig") || c.Contains("suino"))
        {
            IluminarNodo("Pig");
            IluminarNodo("Host");
            IluminarAresta("Host", "Pig");
            IluminarAresta("owl:Thing", "Host");
        }
    }

    /// <summary>
    /// Define a coordenada da orbe ativa para radar em tempo real.
    /// </summary>
    public void DefinirAlvoOrbeAtiva(Vector3 pos, int indice, int total)
    {
        m_PosicaoOrbeAtiva = pos;
        m_TemOrbeAtiva = true;
    }

    /// <summary>
    /// Reseta o minimapa para o estado inicial limpo (nova partida).
    /// </summary>
    public void ReiniciarMinimapa()
    {
        m_TemOrbeAtiva = false;
        m_UltimaZona = "";

        foreach (var kvp in m_NosTaxonomia)
        {
            var no = kvp.Value;
            if (no == null) continue;

            if (no.id == "owl:Thing")
            {
                no.descoberto = true;
                no.imgFundo.color = new Color(0.20f, 0.25f, 0.35f, 0.95f);
                no.imgBorda.color = Color.white;
                no.txtRotulo.color = Color.white;
            }
            else
            {
                no.descoberto = false;
                no.imgFundo.color = new Color(0.06f, 0.08f, 0.12f, 0.75f);
                no.imgBorda.color = new Color(0.25f, 0.30f, 0.40f, 0.50f);
                no.txtRotulo.color = new Color(0.60f, 0.65f, 0.75f, 0.75f);
            }
        }

        for (int i = 0; i < m_ArestasTaxonomia.Count; i++)
        {
            var aresta = m_ArestasTaxonomia[i];
            if (aresta != null)
            {
                aresta.ativa = false;
                aresta.imgLinha.color = new Color(0.18f, 0.22f, 0.30f, 0.40f);
                aresta.rtLinha.sizeDelta = new Vector2(aresta.rtLinha.sizeDelta.x, 1.4f);
            }
        }
    }

    private void IluminarNodo(string id)
    {
        if (m_NosTaxonomia.TryGetValue(id, out NoTaxonomicoUI no))
        {
            if (!no.descoberto)
            {
                no.descoberto = true;
                no.imgFundo.color = new Color(no.corPadrao.r * 0.35f, no.corPadrao.g * 0.35f, no.corPadrao.b * 0.35f, 0.95f);
                no.imgBorda.color = no.corPadrao;
                no.txtRotulo.color = Color.white;
            }
        }
    }

    private void IluminarAresta(string idOrigem, string idDestino)
    {
        for (int i = 0; i < m_ArestasTaxonomia.Count; i++)
        {
            var a = m_ArestasTaxonomia[i];
            if (a.idOrigem == idOrigem && a.idDestino == idDestino)
            {
                if (!a.ativa)
                {
                    a.ativa = true;
                    a.imgLinha.color = new Color(0.0f, 1.0f, 0.65f, 0.95f); // Neon verde-esmeralda
                    a.rtLinha.sizeDelta = new Vector2(a.rtLinha.sizeDelta.x, 2.5f);
                }
                break;
            }
        }
    }

    // =========================================================================
    // CONSTRUÇÃO PROCEDURAL DA INTERFACE WORLDSPACE VR
    // =========================================================================

    private void ConstruirInterfaceMinimapa()
    {
        // 1. Canvas WorldSpace
        GameObject canvasObj = new GameObject("Minimap_Canvas_VR", typeof(Canvas), typeof(CanvasScaler));
        canvasObj.transform.SetParent(transform, false);

        m_CanvasMinimapa = canvasObj.GetComponent<Canvas>();
        m_CanvasMinimapa.renderMode = RenderMode.WorldSpace;
        m_CanvasMinimapa.sortingOrder = 90;
        canvasObj.transform.localScale = Vector3.one * 0.0011f;

        // 2. Painel Raiz (Dark Grid Frame)
        m_PainelRaiz = new GameObject("Painel_Minimapa_Raiz", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
        m_PainelRaiz.transform.SetParent(canvasObj.transform, false);

        RectTransform rtRaiz = m_PainelRaiz.GetComponent<RectTransform>();
        rtRaiz.sizeDelta = tamanhoPainel;
        rtRaiz.anchoredPosition = Vector2.zero;

        Image imgFundo = m_PainelRaiz.GetComponent<Image>();
        imgFundo.color = new Color(0.03f, 0.05f, 0.09f, 0.92f); // Dark slate

        Outline outline = m_PainelRaiz.GetComponent<Outline>();
        outline.effectColor = new Color(0.0f, 0.85f, 1.0f, 0.80f); // Ciano neon
        outline.effectDistance = new Vector2(2f, -2f);

        // 3. Cabeçalho
        m_TextoTitulo = CriarTexto(m_PainelRaiz.transform, "Titulo_Minimapa",
            "<b>MINIMAPA SEMÂNTICO (TBox)</b>", 12, FontStyles.Bold,
            new Vector2(10f, -10f), new Vector2(310f, 20f),
            new Color(0.0f, 0.95f, 1.0f), TextAlignmentOptions.Left);

        m_TextoZonaAtual = CriarTexto(m_PainelRaiz.transform, "Texto_ZonaAtual",
            "ZONA: <color=#FFD700><b>Centro (Hub Ontológico)</b></color>", 10, FontStyles.Normal,
            new Vector2(10f, -30f), new Vector2(310f, 18f),
            new Color(0.80f, 0.90f, 1.0f), TextAlignmentOptions.Left);

        // Linha divisória
        CriarLinhaDivisoria(m_PainelRaiz.transform, new Vector2(10f, -48f), 310f);

        // 4. Container do Grafo Taxonômico (Metade Superior)
        GameObject containerTBox = new GameObject("Container_TBox", typeof(RectTransform));
        containerTBox.transform.SetParent(m_PainelRaiz.transform, false);
        RectTransform rtTBox = containerTBox.GetComponent<RectTransform>();
        rtTBox.anchorMin = new Vector2(0.5f, 1f);
        rtTBox.anchorMax = new Vector2(0.5f, 1f);
        rtTBox.pivot = new Vector2(0.5f, 1f);
        rtTBox.anchoredPosition = new Vector2(0f, -52f);
        rtTBox.sizeDelta = new Vector2(310f, 145f);

        // 5. Container do Radar Espacial 2D (Metade Inferior)
        CriarLinhaDivisoria(m_PainelRaiz.transform, new Vector2(10f, -200f), 310f);

        CriarTexto(m_PainelRaiz.transform, "Rotulo_Radar",
            "<color=#00E5FF>RADAR TÁTICO</color>  <size=9><color=#88AACC>(● Pac-Man  ◆ Orbe  ▲ Fantasma)</color></size>",
            10, FontStyles.Bold, new Vector2(10f, -204f), new Vector2(310f, 16f),
            Color.white, TextAlignmentOptions.Left);

        GameObject containerRadar = new GameObject("Container_Radar_2D", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
        containerRadar.transform.SetParent(m_PainelRaiz.transform, false);
        m_RtAreaRadar = containerRadar.GetComponent<RectTransform>();
        m_RtAreaRadar.anchorMin = new Vector2(0.5f, 0f);
        m_RtAreaRadar.anchorMax = new Vector2(0.5f, 0f);
        m_RtAreaRadar.pivot = new Vector2(0.5f, 0f);
        m_RtAreaRadar.anchoredPosition = new Vector2(0f, 10f);
        m_RtAreaRadar.sizeDelta = new Vector2(300f, 115f);

        Image imgRadar = containerRadar.GetComponent<Image>();
        imgRadar.color = new Color(0.01f, 0.02f, 0.05f, 0.95f);

        Outline outlineRadar = containerRadar.GetComponent<Outline>();
        outlineRadar.effectColor = new Color(0.0f, 0.50f, 0.85f, 0.50f);
        outlineRadar.effectDistance = new Vector2(1f, -1f);

        // Grade de mira no radar
        CriarLinhaDivisoria(containerRadar.transform, new Vector2(0f, 0f), 300f, true); // Centro horizontal
        CriarEixoVertical(containerRadar.transform, new Vector2(0f, 0f), 115f);          // Centro vertical

        // Marcador do Pac-Man (Amarelo)
        GameObject goPac = new GameObject("Marcador_PacMan", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        goPac.transform.SetParent(containerRadar.transform, false);
        m_RtMarcadorPacMan = goPac.GetComponent<RectTransform>();
        m_RtMarcadorPacMan.sizeDelta = new Vector2(9f, 9f);
        Image imgPac = goPac.GetComponent<Image>();
        imgPac.color = new Color(1.0f, 0.85f, 0.0f, 1.0f); // Amarelo clássico

        // Marcador da Orbe (Ciano pulsante)
        GameObject goOrbe = new GameObject("Marcador_Orbe", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        goOrbe.transform.SetParent(containerRadar.transform, false);
        m_RtMarcadorOrbe = goOrbe.GetComponent<RectTransform>();
        m_RtMarcadorOrbe.sizeDelta = new Vector2(9f, 9f);
        m_RtMarcadorOrbe.localRotation = Quaternion.Euler(0f, 0f, 45f); // Diamante
        m_ImgMarcadorOrbe = goOrbe.GetComponent<Image>();
        m_ImgMarcadorOrbe.color = new Color(0.0f, 0.95f, 1.0f, 1.0f);

        // Marcadores dos 4 Fantasmas (Vermelho)
        for (int i = 0; i < 4; i++)
        {
            GameObject goF = new GameObject($"Marcador_Fantasma_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            goF.transform.SetParent(containerRadar.transform, false);
            RectTransform rtF = goF.GetComponent<RectTransform>();
            rtF.sizeDelta = new Vector2(7f, 7f);
            Image imgF = goF.GetComponent<Image>();
            imgF.color = new Color(1.0f, 0.20f, 0.30f, 0.90f);
            goF.SetActive(false);
            m_MarcadoresFantasmas.Add(rtF);
        }
    }

    /// <summary>
    /// Inicializa a topologia hierárquica formal da TBox dentro do container de 310x145.
    /// </summary>
    private void InicializarGrafoTaxonomicoTBox()
    {
        Transform paiTBox = m_PainelRaiz.transform.Find("Container_TBox");
        if (paiTBox == null) return;

        // 1. Nós da TBox
        // Root
        AdicionarNoTBox(paiTBox, "owl:Thing", "owl:Thing", new Vector2(0f, -14f), new Vector2(74f, 18f), Color.white, descoberto: true);

        // Nível 1: Parasitas (Norte) vs Hospedeiros (Sul)
        AdicionarNoTBox(paiTBox, "Parasite", "Parasitas", new Vector2(-70f, -48f), new Vector2(78f, 20f), new Color(0.70f, 0.30f, 1.0f));
        AdicionarNoTBox(paiTBox, "Host", "Hospedeiros", new Vector2(70f, -48f), new Vector2(78f, 20f), new Color(0.0f, 0.85f, 1.0f));

        // Nível 2: Subclasses
        AdicionarNoTBox(paiTBox, "Nematoda", "Nematoda", new Vector2(-114f, -88f), new Vector2(66f, 18f), new Color(0.0f, 1.0f, 0.85f));
        AdicionarNoTBox(paiTBox, "Platyhelminthes", "Platyhelm.", new Vector2(-40f, -88f), new Vector2(66f, 18f), new Color(1.0f, 0.35f, 0.65f));
        AdicionarNoTBox(paiTBox, "Human", "Human", new Vector2(40f, -88f), new Vector2(64f, 18f), new Color(0.20f, 0.65f, 1.0f));
        AdicionarNoTBox(paiTBox, "Pig", "Pig", new Vector2(114f, -88f), new Vector2(64f, 18f), new Color(1.0f, 0.45f, 0.75f));

        // Nível 3: Intervenção / Vetores
        AdicionarNoTBox(paiTBox, "Treatment", "Tratamento", new Vector2(-65f, -125f), new Vector2(72f, 18f), new Color(1.0f, 0.85f, 0.0f));
        AdicionarNoTBox(paiTBox, "Vetores", "Vetores", new Vector2(65f, -125f), new Vector2(72f, 18f), new Color(0.15f, 0.95f, 0.45f));

        // 2. Arestas Hierárquicas
        ConectarArestaTBox(paiTBox, "owl:Thing", "Parasite");
        ConectarArestaTBox(paiTBox, "owl:Thing", "Host");
        ConectarArestaTBox(paiTBox, "Parasite", "Nematoda");
        ConectarArestaTBox(paiTBox, "Parasite", "Platyhelminthes");
        ConectarArestaTBox(paiTBox, "Host", "Human");
        ConectarArestaTBox(paiTBox, "Host", "Pig");
        ConectarArestaTBox(paiTBox, "Parasite", "Treatment");
        ConectarArestaTBox(paiTBox, "Host", "Vetores");
    }

    private void AdicionarNoTBox(Transform pai, string id, string rotulo, Vector2 pos, Vector2 dim, Color cor, bool descoberto = false)
    {
        GameObject noObj = new GameObject("No_" + id, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
        noObj.transform.SetParent(pai, false);

        RectTransform rtNo = noObj.GetComponent<RectTransform>();
        rtNo.anchoredPosition = pos;
        rtNo.sizeDelta = dim;

        Image imgFundo = noObj.GetComponent<Image>();
        Outline outline = noObj.GetComponent<Outline>();
        outline.effectDistance = new Vector2(1f, -1f);

        TMP_Text txt = CriarTexto(noObj.transform, "Txt_Rotulo", rotulo, 9, FontStyles.Bold,
            Vector2.zero, dim, Color.white, TextAlignmentOptions.Center);

        if (descoberto)
        {
            imgFundo.color = new Color(cor.r * 0.35f, cor.g * 0.35f, cor.b * 0.35f, 0.95f);
            outline.effectColor = cor;
            txt.color = Color.white;
        }
        else
        {
            imgFundo.color = new Color(0.06f, 0.08f, 0.12f, 0.75f);
            outline.effectColor = new Color(0.25f, 0.30f, 0.40f, 0.50f);
            txt.color = new Color(0.60f, 0.65f, 0.75f, 0.75f);
        }

        NoTaxonomicoUI no = new NoTaxonomicoUI
        {
            id = id,
            rotulo = rotulo,
            posicaoLocal = pos,
            corPadrao = cor,
            descoberto = descoberto,
            imgFundo = imgFundo,
            imgBorda = null,
            txtRotulo = txt,
            objRaiz = noObj
        };

        m_NosTaxonomia[id] = no;
    }

    private void ConectarArestaTBox(Transform pai, string idOrigem, string idDestino)
    {
        if (!m_NosTaxonomia.TryGetValue(idOrigem, out NoTaxonomicoUI noOrigem) ||
            !m_NosTaxonomia.TryGetValue(idDestino, out NoTaxonomicoUI noDestino))
        {
            return;
        }

        GameObject linhaObj = new GameObject($"Linha_{idOrigem}_{idDestino}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        linhaObj.transform.SetParent(pai, false);
        linhaObj.transform.SetAsFirstSibling(); // Fica atrás dos nós

        RectTransform rtLinha = linhaObj.GetComponent<RectTransform>();
        Vector2 p1 = noOrigem.posicaoLocal;
        Vector2 p2 = noDestino.posicaoLocal;
        Vector2 dir = p2 - p1;
        float dist = dir.magnitude;
        float angulo = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        rtLinha.anchorMin = new Vector2(0.5f, 0.5f);
        rtLinha.anchorMax = new Vector2(0.5f, 0.5f);
        rtLinha.pivot = new Vector2(0f, 0.5f);
        rtLinha.anchoredPosition = p1;
        rtLinha.sizeDelta = new Vector2(dist, 1.4f);
        rtLinha.localRotation = Quaternion.Euler(0f, 0f, angulo);

        Image imgLinha = linhaObj.GetComponent<Image>();
        imgLinha.color = new Color(0.18f, 0.22f, 0.30f, 0.40f);

        ArestaTaxonomicaUI aresta = new ArestaTaxonomicaUI
        {
            idOrigem = idOrigem,
            idDestino = idDestino,
            rtLinha = rtLinha,
            imgLinha = imgLinha,
            ativa = false
        };

        m_ArestasTaxonomia.Add(aresta);
    }

    // =========================================================================
    // UTILITÁRIOS DE CRIAÇÃO UI
    // =========================================================================

    private static void CriarLinhaDivisoria(Transform pai, Vector2 pos, float largura, bool centralizada = false)
    {
        GameObject linha = new GameObject("Linha_Divisoria", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        linha.transform.SetParent(pai, false);

        RectTransform rt = linha.GetComponent<RectTransform>();
        if (centralizada)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
        }
        else
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
        }
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(largura, 1.2f);

        Image img = linha.GetComponent<Image>();
        img.color = new Color(0.0f, 0.85f, 1.0f, 0.40f);
    }

    private static void CriarEixoVertical(Transform pai, Vector2 pos, float altura)
    {
        GameObject linha = new GameObject("Eixo_Vertical", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        linha.transform.SetParent(pai, false);

        RectTransform rt = linha.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(1.2f, altura);

        Image img = linha.GetComponent<Image>();
        img.color = new Color(0.0f, 0.85f, 1.0f, 0.25f);
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
        texto.overflowMode = TextOverflowModes.Truncate;

        return texto;
    }
}
