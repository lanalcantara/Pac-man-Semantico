using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// ============================================================================
/// PAC-MAN SEMÂNTICO (CIn-UFPE)
/// MINIMAPA HUD CIRCULAR VR & AVISOS DE CAPTURA (ESTILO GTA / COCKPIT VR)
/// ============================================================================
/// 1. Mapeia a posição 3D do Pac-Man e da Orbe ativa para um painel circular no canto
///    inferior esquerdo da visão (estilo minimapa tático / GTA).
/// 2. Fundo com textura de grade wireframe neon estilizada do labirinto.
/// 3. Ícone do Pac-Man com rotação contínua atrelada ao ângulo Yaw do jogador/câmera.
/// 4. Warning dinâmico flutuante no visor disparado na captura de gemas/indivíduos.
/// 5. Fixação Head-Locked com amortecimento inercial suave (Lerp/Slerp a 14f) em LateUpdate.
/// 6. Visível estritamente durante a partida ativa (oculto no Hub, GameOver e Vitória).
/// ============================================================================
/// </summary>
[DisallowMultipleComponent]
public class MinimapaHUDVR : MonoBehaviour
{
    public static MinimapaHUDVR Instancia { get; private set; }

    [Header("Referências de UI")]
    public GameObject painelMinimapaRoot; // O Canvas ou GameObject pai do minimapa
    public RectTransform iconePacMan;     // Ícone do Pac-Man no mapa
    public RectTransform iconeOrbeAlvo;   // Ícone da Orbe atual
    public TextMeshProUGUI textoWarning;  // Texto de aviso de captura (ex: "Orbe Capturada!")

    [Header("Configurações do Labirinto (Mundo para UI)")]
    public Transform playerTransform;     // Referência ao XR Origin / Pac-Man no mundo 3D
    public Transform orbeAtualTransform;  // Referência à orbe ativa na cena
    public float mundoMinX = -10.5f;
    public float mundoMaxX = 10.5f;
    public float mundoMinZ = -10.5f;
    public float mundoMaxZ = 10.5f;
    public float mapaUIRadius = 75f;      // Raio máximo em pixels dentro do HUD circular

    [Header("Ergonomia VR & Acompanhamento Suave")]
    public float distanciaVisaoVR = 1.35f;
    public Vector2 offsetPerifericoVR = new Vector2(-0.75f, -0.40f); // Canto inferior esquerdo da visão
    public float velocidadeDamping = 14f;

    private float warningTimer = 0f;
    private Canvas m_CanvasHUD;
    private GameObject m_BannerWarningRoot;
    private Camera m_CameraPrincipal;
    private float m_PulsoOrbeTimer = 0f;
    private static Texture2D s_TexGridRadarCircular;

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
    public static void GarantirNaCena()
    {
        if (Instancia == null)
        {
            var existente = FindAnyObjectByType<MinimapaHUDVR>();
            if (existente == null)
            {
                GameObject obj = new GameObject("Minimapa_HUD_Circular_VR");
                obj.AddComponent<MinimapaHUDVR>();
                if (Application.isPlaying) DontDestroyOnLoad(obj);
            }
        }
    }

    private void Start()
    {
        LocalizarReferencias();
        if (painelMinimapaRoot == null)
        {
            ConstruirMinimapaCircularProcedural();
        }
        EsconderWarning();
        AtualizarAcompanhamentoVisaoVR();
    }

    private void Update()
    {
        LocalizarReferencias();

        // 0. Controle de Ativação do HUD (apenas dentro da partida ativa)
        bool partidaAtiva = true;
        if (HubInicialAmbiente.Instancia != null)
        {
            partidaAtiva = HubInicialAmbiente.Instancia.partidaEmAndamento &&
                          !HubInicialAmbiente.Instancia.hubAberto &&
                          !HubInicialAmbiente.Instancia.GameOverAberto &&
                          !HubInicialAmbiente.Instancia.VitoriaAberta;
        }

        if (painelMinimapaRoot != null && painelMinimapaRoot.activeSelf != partidaAtiva)
        {
            painelMinimapaRoot.SetActive(partidaAtiva);
        }

        if (!partidaAtiva) return;

        // 1. Atualiza a posição do Pac-Man no minimapa em tempo real
        if (playerTransform != null && iconePacMan != null)
        {
            Vector2 posicaoNormalizada = NormalizarCoordenadas(playerTransform.position.x, playerTransform.position.z);
            iconePacMan.anchoredPosition = posicaoNormalizada * mapaUIRadius;

            // Rotaciona o ícone com base na rotação da câmera ou corpo do jogador
            float yRot = (m_CameraPrincipal != null) ? m_CameraPrincipal.transform.eulerAngles.y : playerTransform.eulerAngles.y;
            iconePacMan.rotation = Quaternion.Euler(0, 0, -yRot);
        }

        // 2. Atualiza a posição da Orbe Alvo no minimapa
        if (orbeAtualTransform != null && iconeOrbeAlvo != null)
        {
            if (!iconeOrbeAlvo.gameObject.activeSelf) iconeOrbeAlvo.gameObject.SetActive(true);
            Vector2 posOrbeNorm = NormalizarCoordenadas(orbeAtualTransform.position.x, orbeAtualTransform.position.z);
            iconeOrbeAlvo.anchoredPosition = posOrbeNorm * mapaUIRadius;

            // Efeito visual pulsante no ícone da orbe alvo
            m_PulsoOrbeTimer += Time.deltaTime * 5.0f;
            float escalaPulso = 1.0f + Mathf.Sin(m_PulsoOrbeTimer) * 0.25f;
            iconeOrbeAlvo.localScale = new Vector3(escalaPulso, escalaPulso, 1f);
        }
        else if (iconeOrbeAlvo != null && iconeOrbeAlvo.gameObject.activeSelf)
        {
            iconeOrbeAlvo.gameObject.SetActive(false);
        }

        // 3. Controla o tempo de sumir o aviso de captura (Warning)
        if (warningTimer > 0)
        {
            warningTimer -= Time.deltaTime;
            if (warningTimer <= 0)
            {
                EsconderWarning();
            }
        }
    }

    private void LateUpdate()
    {
        AtualizarAcompanhamentoVisaoVR();
    }

    /// <summary>
    /// Amortece o acompanhamento inercial do olhar em VR (LateUpdate), posicionando o HUD
    /// suavemente no canto inferior esquerdo periférico, virado para os olhos do usuário.
    /// </summary>
    private void AtualizarAcompanhamentoVisaoVR()
    {
        if (m_CanvasHUD == null) return;

        bool modoPC = Application.isEditor || !UnityEngine.XR.XRSettings.isDeviceActive;

        if (modoPC)
        {
            // No Desktop / Editor PC: ScreenSpaceOverlay ancorado exclusivamente no canto inferior esquerdo
            if (m_CanvasHUD.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                m_CanvasHUD.renderMode = RenderMode.ScreenSpaceOverlay;
                m_CanvasHUD.transform.localScale = Vector3.one;
            }
            if (painelMinimapaRoot != null)
            {
                RectTransform rtRoot = painelMinimapaRoot.GetComponent<RectTransform>();
                if (rtRoot != null)
                {
                    rtRoot.anchorMin = new Vector2(0f, 0f);
                    rtRoot.anchorMax = new Vector2(0f, 0f);
                    rtRoot.pivot = new Vector2(0f, 0f);
                    rtRoot.anchoredPosition = new Vector2(24f, 24f);
                }
            }
            if (m_BannerWarningRoot != null)
            {
                RectTransform rtBanner = m_BannerWarningRoot.GetComponent<RectTransform>();
                if (rtBanner != null)
                {
                    rtBanner.anchorMin = new Vector2(0.5f, 1f);
                    rtBanner.anchorMax = new Vector2(0.5f, 1f);
                    rtBanner.pivot = new Vector2(0.5f, 1f);
                    rtBanner.anchoredPosition = new Vector2(0f, -28f);
                }
            }
        }
        else
        {
            if (painelMinimapaRoot != null && !painelMinimapaRoot.activeSelf) return;

            Camera cam = m_CameraPrincipal != null ? m_CameraPrincipal : Camera.main;
            if (cam == null) return;

            if (m_CanvasHUD.renderMode != RenderMode.WorldSpace)
            {
                m_CanvasHUD.renderMode = RenderMode.WorldSpace;
            }
            m_CanvasHUD.transform.localScale = Vector3.one * 0.00085f;

            if (painelMinimapaRoot != null)
            {
                RectTransform rtRoot = painelMinimapaRoot.GetComponent<RectTransform>();
                if (rtRoot != null)
                {
                    rtRoot.anchorMin = new Vector2(0.5f, 0.5f);
                    rtRoot.anchorMax = new Vector2(0.5f, 0.5f);
                    rtRoot.pivot = new Vector2(0.5f, 0.5f);
                    rtRoot.anchoredPosition = Vector2.zero;
                }
            }

            Vector3 camPos = cam.transform.position;
            Vector3 camFwd = cam.transform.forward;
            Vector3 camUp = cam.transform.up;
            Vector3 camRight = cam.transform.right;

            // Ancoragem periférica no quadrante inferior esquerdo
            Vector3 posAlvo = camPos + camFwd * distanciaVisaoVR + camRight * offsetPerifericoVR.x + camUp * offsetPerifericoVR.y;

            // Canta a tela levemente em direção aos olhos (cockpit feeling)
            Vector3 dirParaOlhos = (posAlvo - camPos).normalized;
            Quaternion rotAlvo = Quaternion.LookRotation(dirParaOlhos, camUp);

            m_CanvasHUD.transform.position = Vector3.Lerp(m_CanvasHUD.transform.position, posAlvo, Time.unscaledDeltaTime * velocidadeDamping);
            m_CanvasHUD.transform.rotation = Quaternion.Slerp(m_CanvasHUD.transform.rotation, rotAlvo, Time.unscaledDeltaTime * velocidadeDamping);
        }
    }

    private Vector2 NormalizarCoordenadas(float mundoX, float mundoZ)
    {
        // Mapeia os limites do mundo 3D (-10.5 a +10.5) para o intervalo de -1 a 1 na UI circular
        float normX = Mathf.InverseLerp(mundoMinX, mundoMaxX, mundoX) * 2f - 1f;
        float normZ = Mathf.InverseLerp(mundoMinZ, mundoMaxZ, mundoZ) * 2f - 1f;

        Vector2 vec = new Vector2(normX, normZ);
        if (vec.magnitude > 1.0f)
        {
            vec.Normalize();
        }
        return vec;
    }

    /// <summary>
    /// Dispara o aviso flutuante de Tripla RDF coletada com contagem regressiva de 3.5s.
    /// </summary>
    public void DispararWarningCaptura(string nomeOrbe)
    {
        if (m_BannerWarningRoot != null)
        {
            m_BannerWarningRoot.SetActive(true);
        }

        if (textoWarning != null)
        {
            textoWarning.gameObject.SetActive(true);
            textoWarning.text = $"<color=#00FFFF>[OK] TRIPLA RDF COLETADA:</color>\n<size=16><color=#FFFF00><b>{nomeOrbe}</b></color></size>";
            warningTimer = 3.5f; // Fica visível por 3.5 segundos
        }
    }

    private void EsconderWarning()
    {
        if (textoWarning != null)
        {
            textoWarning.gameObject.SetActive(false);
        }
        if (m_BannerWarningRoot != null)
        {
            m_BannerWarningRoot.SetActive(false);
        }
    }

    /// <summary>
    /// Permite definir a orbe ativa diretamente a partir do Gerenciador de Cenário.
    /// </summary>
    public void DefinirAlvoOrbe(Transform orbeTransform)
    {
        orbeAtualTransform = orbeTransform;
    }

    /// <summary>
    /// Chamado pelo Gerenciador quando a partida inicia ou termina.
    /// </summary>
    public void DefinirAtivacaoHUD(bool ativo)
    {
        if (painelMinimapaRoot != null)
        {
            painelMinimapaRoot.SetActive(ativo);
        }
    }

    private void LocalizarReferencias()
    {
        if (playerTransform == null)
        {
            var ctrl = FindAnyObjectByType<ControladorXRJogador>();
            if (ctrl != null)
            {
                playerTransform = ctrl.transform;
                if (ctrl.cameraPrincipalXR != null)
                {
                    m_CameraPrincipal = ctrl.cameraPrincipalXR;
                }
            }
            if (playerTransform == null && Camera.main != null)
            {
                playerTransform = Camera.main.transform;
                m_CameraPrincipal = Camera.main;
            }
        }
        else if (m_CameraPrincipal == null)
        {
            m_CameraPrincipal = Camera.main;
        }

        // Sempre rastreia a Tripla RDF ativa mais próxima do Pac-Man no labirinto
        if (orbeAtualTransform == null || !orbeAtualTransform.gameObject.activeInHierarchy)
        {
            var gemas = FindObjectsByType<GemaValidaSemantica>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            Transform melhor = null;
            float menorDistSqr = float.MaxValue;
            Vector3 posRef = playerTransform != null ? playerTransform.position : Vector3.zero;
            for (int i = 0; i < gemas.Length; i++)
            {
                if (gemas[i] != null && gemas[i].gameObject.activeInHierarchy)
                {
                    Collider col = gemas[i].GetComponent<Collider>();
                    if (col != null && !col.enabled) continue;
                    float d = (gemas[i].transform.position - posRef).sqrMagnitude;
                    if (d < menorDistSqr)
                    {
                        menorDistSqr = d;
                        melhor = gemas[i].transform;
                    }
                }
            }
            orbeAtualTransform = melhor;
        }
    }

    /// <summary>
    /// Constrói a interface procedural completa do minimapa circular com estilo retro arcade
    /// e do banner superior de warning flutuante.
    /// </summary>
    private void ConstruirMinimapaCircularProcedural()
    {
        // 1. Canvas com suporte híbrido PC (ScreenSpaceOverlay) e VR (WorldSpace)
        GameObject canvasObj = new GameObject("Canvas_MinimapaHUD_Circular", typeof(Canvas), typeof(CanvasScaler));
        canvasObj.transform.SetParent(transform, false);

        m_CanvasHUD = canvasObj.GetComponent<Canvas>();
        m_CanvasHUD.sortingOrder = 25;

        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        bool modoPC = Application.isEditor || !UnityEngine.XR.XRSettings.isDeviceActive;
        if (modoPC)
        {
            m_CanvasHUD.renderMode = RenderMode.ScreenSpaceOverlay;
            m_CanvasHUD.transform.localScale = Vector3.one;
        }
        else
        {
            m_CanvasHUD.renderMode = RenderMode.WorldSpace;
            m_CanvasHUD.transform.localScale = Vector3.one * 0.00085f;
        }

        // 2. Painel Raiz (ancorado no canto inferior esquerdo no PC)
        painelMinimapaRoot = new GameObject("Painel_Minimapa_Root", typeof(RectTransform));
        painelMinimapaRoot.transform.SetParent(canvasObj.transform, false);

        RectTransform rtRoot = painelMinimapaRoot.GetComponent<RectTransform>();
        rtRoot.sizeDelta = new Vector2(210f, 210f);
        if (modoPC)
        {
            rtRoot.anchorMin = new Vector2(0f, 0f);
            rtRoot.anchorMax = new Vector2(0f, 0f);
            rtRoot.pivot = new Vector2(0f, 0f);
            rtRoot.anchoredPosition = new Vector2(24f, 24f);
        }
        else
        {
            rtRoot.anchorMin = new Vector2(0.5f, 0.5f);
            rtRoot.anchorMax = new Vector2(0.5f, 0.5f);
            rtRoot.pivot = new Vector2(0.5f, 0.5f);
            rtRoot.anchoredPosition = Vector2.zero;
        }

        // 3. Fundo Circular do Minimapa com textura de grade wireframe
        GameObject bgObj = new GameObject("Radar_Circular_BG", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
        bgObj.transform.SetParent(painelMinimapaRoot.transform, false);

        RectTransform rtBg = bgObj.GetComponent<RectTransform>();
        rtBg.anchorMin = new Vector2(0.5f, 0.5f);
        rtBg.anchorMax = new Vector2(0.5f, 0.5f);
        rtBg.pivot = new Vector2(0.5f, 0.5f);
        rtBg.sizeDelta = new Vector2(170f, 170f);
        rtBg.anchoredPosition = new Vector2(0f, 8f);

        Image imgBg = bgObj.GetComponent<Image>();
        imgBg.color = new Color(0.015f, 0.035f, 0.08f, 0.92f);
        imgBg.sprite = CriarSpriteCircularSuave();

        Outline outline = bgObj.GetComponent<Outline>();
        outline.effectColor = new Color(0.0f, 1.0f, 1.0f, 0.75f); // Ciano Neon Suave (#00FFFF)
        outline.effectDistance = new Vector2(2f, -2f);

        // Grade interna wireframe
        GameObject gridObj = new GameObject("Grid_Wireframe_Interna", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        gridObj.transform.SetParent(bgObj.transform, false);
        RectTransform rtGrid = gridObj.GetComponent<RectTransform>();
        rtGrid.sizeDelta = new Vector2(150f, 150f);
        rtGrid.anchoredPosition = Vector2.zero;
        RawImage rawGrid = gridObj.GetComponent<RawImage>();
        rawGrid.texture = ObterTexturaGridRadarCircular();
        rawGrid.color = new Color(0.0f, 1.0f, 1.0f, 0.25f);

        // 4. Ícone da Orbe Alvo (Losango pulsante ciano)
        GameObject orbeObj = new GameObject("Icone_Orbe_Alvo", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        orbeObj.transform.SetParent(bgObj.transform, false);
        iconeOrbeAlvo = orbeObj.GetComponent<RectTransform>();
        iconeOrbeAlvo.sizeDelta = new Vector2(14f, 14f);
        iconeOrbeAlvo.anchoredPosition = Vector2.zero;
        iconeOrbeAlvo.rotation = Quaternion.Euler(0, 0, 45f); // Losango

        Image imgOrbe = orbeObj.GetComponent<Image>();
        imgOrbe.color = new Color(0.0f, 1.0f, 1.0f, 1f); // Ciano neon
        orbeObj.SetActive(false);

        // 5. Ícone do Pac-Man (Amarelo clássico com seta de orientação)
        GameObject pacObj = new GameObject("Icone_PacMan", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        pacObj.transform.SetParent(bgObj.transform, false);
        iconePacMan = pacObj.GetComponent<RectTransform>();
        iconePacMan.sizeDelta = new Vector2(16f, 16f);
        iconePacMan.anchoredPosition = Vector2.zero;

        Image imgPac = pacObj.GetComponent<Image>();
        imgPac.color = new Color(1.0f, 0.85f, 0.05f, 1f); // Amarelo Pac-Man
        imgPac.sprite = CriarSpriteTrianguloDirecao();

        // 6. Rótulo inferior com estilo Arcade
        GameObject labelObj = new GameObject("Label_Minimapa", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObj.transform.SetParent(painelMinimapaRoot.transform, false);
        RectTransform rtLabel = labelObj.GetComponent<RectTransform>();
        rtLabel.anchorMin = new Vector2(0.5f, 0f);
        rtLabel.anchorMax = new Vector2(0.5f, 0f);
        rtLabel.pivot = new Vector2(0.5f, 0f);
        rtLabel.sizeDelta = new Vector2(180f, 22f);
        rtLabel.anchoredPosition = new Vector2(0f, 0f);
        TextMeshProUGUI txtLabel = labelObj.GetComponent<TextMeshProUGUI>();
        txtLabel.text = "<color=#00FFFF><b>PAC-RADAR VR</b></color>";
        txtLabel.fontSize = 11f;
        txtLabel.alignment = TextAlignmentOptions.Center;
        txtLabel.overflowMode = TextOverflowModes.Truncate;

        // 7. Banner de Aviso Dinâmico de Captura (posicionado no topo central da visão)
        m_BannerWarningRoot = new GameObject("Banner_Warning_Captura", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
        m_BannerWarningRoot.transform.SetParent(canvasObj.transform, false);

        RectTransform rtBanner = m_BannerWarningRoot.GetComponent<RectTransform>();
        rtBanner.sizeDelta = new Vector2(360f, 54f);
        if (modoPC)
        {
            rtBanner.anchorMin = new Vector2(0.5f, 1f);
            rtBanner.anchorMax = new Vector2(0.5f, 1f);
            rtBanner.pivot = new Vector2(0.5f, 1f);
            rtBanner.anchoredPosition = new Vector2(0f, -28f);
        }
        else
        {
            rtBanner.anchoredPosition = new Vector2(420f, 360f);
        }

        Image imgBanner = m_BannerWarningRoot.GetComponent<Image>();
        imgBanner.color = new Color(0.02f, 0.05f, 0.12f, 0.94f);

        Outline outlineBanner = m_BannerWarningRoot.GetComponent<Outline>();
        outlineBanner.effectColor = new Color(0.0f, 1.0f, 1.0f, 0.85f);
        outlineBanner.effectDistance = new Vector2(1.5f, -1.5f);

        GameObject txtWarnObj = new GameObject("Texto_Warning", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtWarnObj.transform.SetParent(m_BannerWarningRoot.transform, false);
        RectTransform rtTxt = txtWarnObj.GetComponent<RectTransform>();
        rtTxt.sizeDelta = new Vector2(320f, 48f);
        rtTxt.anchoredPosition = Vector2.zero;

        textoWarning = txtWarnObj.GetComponent<TextMeshProUGUI>();
        textoWarning.text = "[OK] TRIPLA RDF COLETADA!";
        textoWarning.fontSize = 13f;
        textoWarning.alignment = TextAlignmentOptions.Center;
        textoWarning.color = Color.white;
        textoWarning.overflowMode = TextOverflowModes.Truncate;

        m_BannerWarningRoot.SetActive(false);
    }

    private static Sprite CriarSpriteCircularSuave()
    {
        const int TAM = 128;
        Texture2D tex = new Texture2D(TAM, TAM, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[TAM * TAM];
        Vector2 centro = new Vector2(TAM * 0.5f, TAM * 0.5f);
        float raioMax = TAM * 0.48f;

        for (int y = 0; y < TAM; y++)
        {
            for (int x = 0; x < TAM; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), centro);
                float a = Mathf.Clamp01((raioMax - dist) * 1.5f);
                pixels[y * TAM + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, TAM, TAM), new Vector2(0.5f, 0.5f));
    }

    private static Sprite CriarSpriteTrianguloDirecao()
    {
        const int TAM = 64;
        Texture2D tex = new Texture2D(TAM, TAM, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[TAM * TAM];

        for (int y = 0; y < TAM; y++)
        {
            for (int x = 0; x < TAM; x++)
            {
                // Triângulo apontando para cima (Norte = frente)
                float normY = (float)y / TAM;
                float meiaLargura = (1.0f - normY) * 0.45f;
                float normX = (float)x / TAM - 0.5f;

                bool dentro = Mathf.Abs(normX) <= meiaLargura && normY >= 0.15f;
                pixels[y * TAM + x] = dentro ? Color.white : Color.clear;
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, TAM, TAM), new Vector2(0.5f, 0.5f));
    }

    private static Texture2D ObterTexturaGridRadarCircular()
    {
        if (s_TexGridRadarCircular != null) return s_TexGridRadarCircular;

        const int TAM = 128;
        s_TexGridRadarCircular = new Texture2D(TAM, TAM, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[TAM * TAM];
        Vector2 centro = new Vector2(TAM * 0.5f, TAM * 0.5f);

        for (int y = 0; y < TAM; y++)
        {
            for (int x = 0; x < TAM; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), centro);
                bool anelExterno = Mathf.Abs(dist - 56f) < 1.2f;
                bool anelMedio = Mathf.Abs(dist - 36f) < 1.0f;
                bool anelInterno = Mathf.Abs(dist - 16f) < 1.0f;
                bool eixoX = Mathf.Abs(y - 64) < 1.0f;
                bool eixoY = Mathf.Abs(x - 64) < 1.0f;

                if (dist <= 62f && (anelExterno || anelMedio || anelInterno || eixoX || eixoY))
                {
                    pixels[y * TAM + x] = Color.white;
                }
                else
                {
                    pixels[y * TAM + x] = Color.clear;
                }
            }
        }
        s_TexGridRadarCircular.SetPixels(pixels);
        s_TexGridRadarCircular.Apply();
        return s_TexGridRadarCircular;
    }
}
