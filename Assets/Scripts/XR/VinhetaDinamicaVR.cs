using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

/// <summary>
/// ============================================================================
/// PAC-MAN SEMÂNTICO (CIn-UFPE)
/// VINHETA DINÂMICA DE CONFORTO VR (Tunneling / Prevenção de Cinetose)
/// ============================================================================
/// Implementa a técnica padrão ouro da indústria para eliminação de enjoo de movimento
/// (Motion Sickness / Cinetose) em Realidade Virtual (Meta Quest 3/3S).
/// 
/// Mecânica de Funcionamento:
/// 1. Durante a rotação rápida (Yaw) via Analógico Direito (ou Mouse/Teclado), o campo
///    visual periférico é suavemente fechado por uma máscara de vinheta escura circular.
/// 2. Isso reduz o fluxo óptico na retina periférica — a principal causa de conflito
///    vestíbulo-ocular — mantendo a visão central 100% nítida e transparente.
/// 3. Ao cessar o giro ou aceleração rápida, a vinheta reabre de forma imperceptível
///    com interpolação suave (Mathf.MoveTowards / Lerp a 9.0f).
/// 4. Zero Draw-Calls quando inativa: a imagem é desabilitada quando o alpha atinge zero.
/// ============================================================================
/// </summary>
[DisallowMultipleComponent]
public class VinhetaDinamicaVR : MonoBehaviour
{
    public static VinhetaDinamicaVR Instancia { get; private set; }

    [Header("1. Configurações de Conforto VR")]
    [Tooltip("Desativada por padrao para evitar escurecimento/oscilacao ao girar a camera.")]
    public bool habilitarVinheta = false;

    [Range(0.2f, 0.95f)]
    [Tooltip("Opacidade máxima da vinheta nos giros mais rápidos (recomendado Quest 3: 0.70).")]
    public float intensidadeMaxima = 0.70f;

    [Tooltip("Velocidade com que a vinheta abre e fecha (suavidade de transição).")]
    public float velocidadeTransicao = 9.0f;

    [Tooltip("Limiar mínimo de velocidade angular em graus/segundo para acionar a vinheta.")]
    public float limiarGiroAngular = 12.0f;

    [Tooltip("Também fecha levemente a vinheta ao acelerar bruscamente para frente.")]
    public bool ativarAoAcelerar = false;

    private Camera m_CameraAlvo;
    private Canvas m_CanvasVinheta;
    private RawImage m_ImagemVinheta;
    private Texture2D m_TexturaVinheta;

    private float m_AlphaAtual = 0f;
    private Quaternion m_UltimaRotacaoCamera;
    private ControladorXRJogador m_ControladorJogador;

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
            var existente = FindAnyObjectByType<VinhetaDinamicaVR>();
            if (existente == null)
            {
                Camera cam = Camera.main;
                GameObject go = new GameObject("Vinheta_Dinamica_VR_Comfort");
                if (cam != null)
                {
                    go.transform.SetParent(cam.transform, false);
                }
                go.AddComponent<VinhetaDinamicaVR>();
            }
        }
    }

    private void Start()
    {
        LocalizarReferencias();
        ConstruirOverlayVinheta();
        m_UltimaRotacaoCamera = (m_CameraAlvo != null) ? m_CameraAlvo.transform.rotation : transform.rotation;
    }

    private void LocalizarReferencias()
    {
        if (m_CameraAlvo == null)
        {
            m_CameraAlvo = GetComponentInParent<Camera>() ?? Camera.main;
        }
        if (m_ControladorJogador == null)
        {
            m_ControladorJogador = FindAnyObjectByType<ControladorXRJogador>();
        }
    }

    private void LateUpdate()
    {
        if (m_ImagemVinheta == null) return;

        // Desativa completamente fora do modo VR ou quando a vinheta estiver desabilitada, evitando escurecimento ao girar
        if (!habilitarVinheta || Application.isEditor || !XRSettings.isDeviceActive)
        {
            m_AlphaAtual = 0f;
            if (m_ImagemVinheta.gameObject.activeSelf)
            {
                m_ImagemVinheta.color = Color.clear;
                m_ImagemVinheta.gameObject.SetActive(false);
            }
            return;
        }

        LocalizarReferencias();
        if (m_CameraAlvo == null) return;

        // 1. Calcula a velocidade angular real da câmera no frame atual
        float deltaTempo = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
        float deltaAngulo = Quaternion.Angle(m_CameraAlvo.transform.rotation, m_UltimaRotacaoCamera) / deltaTempo;
        m_UltimaRotacaoCamera = m_CameraAlvo.transform.rotation;

        // 2. Lê entradas diretas do analógico direito e gatilho acelerador se disponíveis
        float entradaGiroYaw = 0f;
        float aceleracaoGatilho = 0f;

        if (m_ControladorJogador != null)
        {
            var dados = m_ControladorJogador.ObterEntradaAtual();
            entradaGiroYaw = Mathf.Abs(dados.GiroCameraYaw);
            aceleracaoGatilho = dados.AceleradorGatilho;
        }

        // 3. Determina a intensidade alvo da vinheta
        float alvoAlpha = 0f;

        if (entradaGiroYaw > 0.02f || deltaAngulo > limiarGiroAngular)
        {
            float fatorGiro = Mathf.Clamp01(Mathf.Max(entradaGiroYaw, deltaAngulo / 90f));
            alvoAlpha = fatorGiro * intensidadeMaxima;
        }
        else if (ativarAoAcelerar && aceleracaoGatilho > 0.15f)
        {
            // Leve contração periférica em aceleração rápida (30% da intensidade máxima)
            alvoAlpha = aceleracaoGatilho * (intensidadeMaxima * 0.40f);
        }

        // 4. Interpolação suave
        m_AlphaAtual = Mathf.MoveTowards(m_AlphaAtual, alvoAlpha, deltaTempo * velocidadeTransicao);

        // 5. Aplicação no shader/material da imagem
        if (m_AlphaAtual > 0.002f)
        {
            if (!m_ImagemVinheta.gameObject.activeSelf)
            {
                m_ImagemVinheta.gameObject.SetActive(true);
            }
            m_ImagemVinheta.color = new Color(0f, 0f, 0f, m_AlphaAtual);
        }
        else
        {
            if (m_ImagemVinheta.gameObject.activeSelf)
            {
                m_ImagemVinheta.color = Color.clear;
                m_ImagemVinheta.gameObject.SetActive(false); // Zero draw calls em repouso
            }
        }
    }

    /// <summary>
    /// Constrói a tela de vinheta procedural posicionada no plano de corte da câmera.
    /// </summary>
    private void ConstruirOverlayVinheta()
    {
        if (m_CameraAlvo == null) return;

        // 1. Gera textura circular radial invertida (Preto nas bordas, Transparente no centro)
        GerarTexturaVinhetaRadial();

        // 2. Canvas ScreenSpaceCamera anexado à câmera principal
        GameObject canvasObj = new GameObject("Canvas_Overlay_Vinheta", typeof(Canvas));
        canvasObj.transform.SetParent(m_CameraAlvo.transform, false);

        m_CanvasVinheta = canvasObj.GetComponent<Canvas>();
        m_CanvasVinheta.renderMode = RenderMode.ScreenSpaceCamera;
        m_CanvasVinheta.worldCamera = m_CameraAlvo;
        m_CanvasVinheta.planeDistance = 0.22f; // Imediatamente à frente da visão
        m_CanvasVinheta.sortingOrder = 999;     // Acima da geometria, abaixo de avisos críticos

        // 3. RawImage cobrindo todo o campo visual
        GameObject imgObj = new GameObject("Imagem_Vinheta_Radial", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        imgObj.transform.SetParent(canvasObj.transform, false);

        RectTransform rt = imgObj.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;

        m_ImagemVinheta = imgObj.GetComponent<RawImage>();
        m_ImagemVinheta.texture = m_TexturaVinheta;
        m_ImagemVinheta.color = Color.clear;
        m_ImagemVinheta.raycastTarget = false;

        imgObj.SetActive(false);
    }

    /// <summary>
    /// Cria uma textura procedural com falloff suave em anel circular (Zero dependência de PNG externo).
    /// </summary>
    private void GerarTexturaVinhetaRadial()
    {
        const int TAMANHO = 256;
        m_TexturaVinheta = new Texture2D(TAMANHO, TAMANHO, TextureFormat.RGBA32, false);
        m_TexturaVinheta.name = "Tex_Vinheta_Radial_Procedural";
        m_TexturaVinheta.wrapMode = TextureWrapMode.Clamp;
        m_TexturaVinheta.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[TAMANHO * TAMANHO];
        Vector2 centro = new Vector2(0.5f, 0.5f);

        for (int y = 0; y < TAMANHO; y++)
        {
            float v = (float)y / (TAMANHO - 1);
            for (int x = 0; x < TAMANHO; x++)
            {
                float u = (float)x / (TAMANHO - 1);
                float distCentro = Vector2.Distance(new Vector2(u, v), centro) * 2.0f; // 0 no centro, 1 nas bordas

                // Centro totalmente límpido até 48% do raio; transição suave até 96%
                float alpha = Mathf.SmoothStep(0.48f, 0.96f, distCentro);
                pixels[y * TAMANHO + x] = new Color(0f, 0f, 0f, alpha);
            }
        }

        m_TexturaVinheta.SetPixels(pixels);
        m_TexturaVinheta.Apply();
    }

    private void OnDestroy()
    {
        if (m_TexturaVinheta != null)
        {
            Destroy(m_TexturaVinheta);
        }
    }
}
