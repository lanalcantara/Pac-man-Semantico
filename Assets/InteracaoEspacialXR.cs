using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// ============================================================================
/// PAC-MAN SEMÂNTICO (Semantic Pac-Man) - Interação Espacial por Apontamento XR
/// ============================================================================
/// Módulo de Apontamento e Raycast Laser 3D para Controladores de Realidade Virtual.
/// Permite que o utilizador aponte o laser do comando de RV diretamente para gemas,
/// parasitas e fantasmas hostis, realizando a inspeção prévia de classes e instâncias
/// ontológicas (Ascaris lumbricoides, Taenia solium, etc.) antes da captura física.
/// 
/// Funcionalidades:
/// 1. Feixe Laser Volumétrico com Retículo Holográfico Inteligente.
/// 2. Detecção e Realce Visual com Cores Semânticas (Verde = Válida, Vermelho = Hostil/Inválida).
/// 3. Haptic Feedback tátil refinado ao focar espécimes biológicos no labirinto.
/// 4. Captura à distância por gatilho do controlador (Index Trigger / Primary Button).
/// ============================================================================
/// </summary>
[RequireComponent(typeof(SphereCollider))]
[AddComponentMenu("Pac-Man Semântico/XR/Interação Espacial XR")]
public class InteracaoEspacialXR : MonoBehaviour
{
    public enum MaoXR
    {
        MaoEsquerda,
        MaoDireita,
        CabecaHeadset
    }

    [Header("Configuração do Controlador")]
    [Tooltip("Identificador do controlador físico no XR Origin.")]
    public MaoXR tipoMao = MaoXR.MaoDireita;

    [Header("Feixe Laser (Raycast Pointer)")]
    [Tooltip("Habilita o feixe laser para inspeção ontológica à distância.")]
    public bool habilitarRaycast = true;

    [Tooltip("Alcance máximo do raio laser em metros.")]
    public float distanciaMaximaRaycast = 15f;

    [Tooltip("Camadas de colisão consideradas pelo raio laser.")]
    public LayerMask camadasAlvo = ~0;

    [Header("Cores Semânticas do Laser")]
    [Tooltip("Cor do feixe quando não está apontando para nenhum elemento semântico.")]
    public Color corLaserRepouso = new Color(0f, 0.85f, 1f, 0.55f);

    [Tooltip("Cor do feixe ao focar uma Gema Válida (Parasita Conforme).")]
    public Color corLaserGemaValida = new Color(0.1f, 1f, 0.45f, 0.95f);

    [Tooltip("Cor do feixe ao focar um Obstáculo Inválido ou Fantasma Hostil.")]
    public Color corLaserObstaculo = new Color(1f, 0.15f, 0.25f, 0.95f);

    [Header("Retículo Visual Holográfico")]
    [Tooltip("Exibir anel/retículo no ponto de impacto da mira.")]
    public bool mostrarReticulo = true;

    private LineRenderer m_LineRenderer;
    private SphereCollider m_ColisorProximidade;
    private IInstanciaSemantica m_InstanciaSobFoco;
    private GameObject m_ReticuloObj;

    private UnityEngine.XR.InputDevice m_DispositivoXR;

    private void Awake()
    {
        m_ColisorProximidade = GetComponent<SphereCollider>();
        if (m_ColisorProximidade != null)
        {
            m_ColisorProximidade.isTrigger = true;
            m_ColisorProximidade.radius = 0.12f;
        }

        InicializarLaser();
        InicializarReticulo();
    }

    private void Start()
    {
        AtualizarDispositivoXR();
    }

    private void Update()
    {
        if (habilitarRaycast)
        {
            ProcessarRaycastEspacial();
            VerificarEntradaInteracao();
        }
        else if (m_LineRenderer != null && m_LineRenderer.enabled)
        {
            m_LineRenderer.enabled = false;
        }
    }

    private void InicializarLaser()
    {
        m_LineRenderer = GetComponent<LineRenderer>();
        if (m_LineRenderer == null)
        {
            m_LineRenderer = gameObject.AddComponent<LineRenderer>();
        }

        m_LineRenderer.startWidth = 0.007f;
        m_LineRenderer.endWidth = 0.002f;
        m_LineRenderer.positionCount = 2;
        m_LineRenderer.useWorldSpace = true;

        Shader laserShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        Material matLaser = new Material(laserShader) { color = corLaserRepouso };

        m_LineRenderer.material = matLaser;
        m_LineRenderer.startColor = corLaserRepouso;
        m_LineRenderer.endColor = corLaserRepouso * 0.5f;
    }

    private void InicializarReticulo()
    {
        if (!mostrarReticulo) return;

        m_ReticuloObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        m_ReticuloObj.name = $"Reticulo_Holografico_{tipoMao}";
        m_ReticuloObj.transform.localScale = Vector3.one * 0.035f;

        Collider c = m_ReticuloObj.GetComponent<Collider>();
        if (c != null) Destroy(c);

        Renderer r = m_ReticuloObj.GetComponent<Renderer>();
        if (r != null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            Material m = new Material(sh) { color = corLaserRepouso };
            r.material = m;
        }

        m_ReticuloObj.SetActive(false);
    }

    /// <summary>
    /// Processa continuamente o raio laser e detecta entidades da ontologia de parasitologia sob o feixe.
    /// </summary>
    private void ProcessarRaycastEspacial()
    {
        Vector3 origem = transform.position;
        Vector3 direcao = transform.forward;
        Vector3 pontoFinal = origem + (direcao * distanciaMaximaRaycast);

        RaycastHit hit;
        bool colidiu = Physics.Raycast(origem, direcao, out hit, distanciaMaximaRaycast, camadasAlvo, QueryTriggerInteraction.Collide);

        IInstanciaSemantica instanciaDetectada = null;

        if (colidiu)
        {
            pontoFinal = hit.point;
            instanciaDetectada = hit.collider.GetComponentInParent<IInstanciaSemantica>() ?? hit.collider.GetComponent<IInstanciaSemantica>();
        }

        if (m_LineRenderer != null)
        {
            m_LineRenderer.enabled = true;
            m_LineRenderer.SetPosition(0, origem);
            m_LineRenderer.SetPosition(1, pontoFinal);
        }

        if (m_ReticuloObj != null && mostrarReticulo)
        {
            m_ReticuloObj.SetActive(colidiu);
            if (colidiu)
            {
                m_ReticuloObj.transform.position = pontoFinal;
            }
        }

        // Transição de foco semântico (Inspeção prévia)
        if (instanciaDetectada != m_InstanciaSobFoco)
        {
            if (m_InstanciaSobFoco != null)
            {
                m_InstanciaSobFoco.DefinirFocoVisual(false);
            }

            m_InstanciaSobFoco = instanciaDetectada;

            if (m_InstanciaSobFoco != null)
            {
                m_InstanciaSobFoco.DefinirFocoVisual(true);

                Color corFoco = m_InstanciaSobFoco.EhValida ? corLaserGemaValida : corLaserObstaculo;
                AjustarCorLaser(corFoco);

                // Emite micro-vibração tátil suave no comando ao focar uma nova entidade
                DispararVibracaoHaptica(0.18f, 0.04f);

                Debug.Log($"<color=#00E5FF><b>[Inspeção Laser XR]</b></color> Focando '{m_InstanciaSobFoco.NomeClasse}' ({m_InstanciaSobFoco.Id}) | DL: [{m_InstanciaSobFoco.ExpressaoDL}]");
            }
            else
            {
                AjustarCorLaser(corLaserRepouso);
            }
        }
    }

    /// <summary>
    /// Processa o gatilho de interação/captura à distância através do controle XR ou simulador.
    /// </summary>
    private void VerificarEntradaInteracao()
    {
        if (m_InstanciaSobFoco == null) return;

        bool acionado = false;

        // 1. Teclado / Mouse Fallback (Editor)
        if (Input.GetButtonDown("Fire1") || Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.E))
        {
            acionado = true;
        }

        // 2. Leitura Direta de Dispositivos XR (OpenXR / Touch / Knuckles)
        if (!m_DispositivoXR.isValid)
        {
            AtualizarDispositivoXR();
        }

        if (m_DispositivoXR.isValid)
        {
            // Gatilho Principal (Index Trigger)
            if (m_DispositivoXR.TryGetFeatureValue(CommonUsages.triggerButton, out bool triggerPressionado) && triggerPressionado)
            {
                acionado = true;
            }
            else if (m_DispositivoXR.TryGetFeatureValue(CommonUsages.trigger, out float triggerValue) && triggerValue > 0.65f)
            {
                acionado = true;
            }

            // Botão Primário (Botão A / X)
            if (m_DispositivoXR.TryGetFeatureValue(CommonUsages.primaryButton, out bool primaryButton) && primaryButton)
            {
                acionado = true;
            }
        }

        if (acionado)
        {
            Debug.Log($"<color=#00FF99><b>[Captura Laser XR]</b></color> Capturando espécime '{m_InstanciaSobFoco.NomeClasse}' ({m_InstanciaSobFoco.Id}) via Raycast.");
            DispararVibracaoHaptica(0.5f, 0.12f);

            var inst = m_InstanciaSobFoco;
            m_InstanciaSobFoco = null;
            AjustarCorLaser(corLaserRepouso);

            inst.Interagir(gameObject, ehRaycast: true);
        }
    }

    private void AjustarCorLaser(Color cor)
    {
        if (m_LineRenderer != null && m_LineRenderer.material != null)
        {
            m_LineRenderer.material.color = cor;
            m_LineRenderer.startColor = cor;
            m_LineRenderer.endColor = cor * 0.6f;
        }

        if (m_ReticuloObj != null)
        {
            Renderer r = m_ReticuloObj.GetComponent<Renderer>();
            if (r != null && r.material != null)
            {
                r.material.color = cor;
            }
        }
    }

    public void DispararVibracaoHaptica(float amplitude, float duracao)
    {
        if (!m_DispositivoXR.isValid)
        {
            AtualizarDispositivoXR();
        }

        if (m_DispositivoXR.isValid)
        {
            HapticCapabilities capabilities;
            if (m_DispositivoXR.TryGetHapticCapabilities(out capabilities) && capabilities.supportsImpulse)
            {
                m_DispositivoXR.SendHapticImpulse(0, Mathf.Clamp01(amplitude), Mathf.Clamp(duracao, 0.02f, 1f));
            }
        }
    }

    private void AtualizarDispositivoXR()
    {
        InputDeviceCharacteristics characteristics = InputDeviceCharacteristics.Controller;

        if (tipoMao == MaoXR.MaoEsquerda)
            characteristics |= InputDeviceCharacteristics.Left;
        else if (tipoMao == MaoXR.MaoDireita)
            characteristics |= InputDeviceCharacteristics.Right;
        else
            characteristics = InputDeviceCharacteristics.HeadMounted;

        List<UnityEngine.XR.InputDevice> devices = new List<UnityEngine.XR.InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(characteristics, devices);

        if (devices.Count > 0)
        {
            m_DispositivoXR = devices[0];
        }
    }

    private void OnDestroy()
    {
        if (m_ReticuloObj != null)
        {
            Destroy(m_ReticuloObj);
        }
    }
}
