using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// ============================================================================
/// PAC-MAN SEMÂNTICO (Semantic Pac-Man) - Modelagem Orientada a Objetos (UML/C++)
/// ============================================================================
/// Módulo de Interação Espacial 3D para Controladores e Hand Tracking no Pac-Man Semântico.
/// Opera diretamente sobre a interface IInstanciaSemantica.
/// 
/// Funcionalidades:
/// 1. Coleta Direta por Proximidade e Toque Tátil (Trigger Colliders nas mãos/boca do Pac-Man).
/// 2. Raycasting Espacial com Laser Visível (LineRenderer) para inspeção e coleta à distância.
/// 3. Feedback Háptico e Luminoso em tempo real ao interagir com gemas e obstáculos.
/// ============================================================================
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class InteracaoEspacialXR : MonoBehaviour
{
    public enum MaoXR
    {
        MaoEsquerda,
        MaoDireita,
        CabecaHeadset
    }

    [Header("Configuração do Dispositivo")]
    [Tooltip("Define se este controlador representa a mão esquerda, direita ou a visão do Pac-Man.")]
    public MaoXR tipoMao = MaoXR.MaoDireita;

    [Header("Raycast Espacial (Laser Pointer)")]
    [Tooltip("Habilita o feixe de laser para interações semânticas à distância.")]
    public bool habilitarRaycast = true;

    [Tooltip("Alcance máximo do raio laser em metros.")]
    public float distanciaMaximaRaycast = 12f;

    [Tooltip("Camadas de colisão consideradas pelo raio laser.")]
    public LayerMask camadasAlvo = ~0;

    [Tooltip("Cor do laser em estado de repouso.")]
    public Color corLaserRepouso = new Color(0f, 0.8f, 1f, 0.6f);

    [Tooltip("Cor do laser quando está mirando em uma gema ou obstáculo semântico.")]
    public Color corLaserFoco = new Color(0.2f, 1f, 0.4f, 0.9f);

    [Header("Retículo Visual")]
    [Tooltip("Exibir retículo luminoso no ponto de mira.")]
    public bool mostrarReticulo = true;

    private LineRenderer m_LineRenderer;
    private SphereCollider m_ColisorProximidade;
    private IInstanciaSemantica m_InstanciaSobFoco;
    private GameObject m_ReticuloObj;

    private UnityEngine.XR.InputDevice m_DispositivoXR;

    private void Awake()
    {
        m_ColisorProximidade = GetComponent<SphereCollider>();
        m_ColisorProximidade.isTrigger = true;
        m_ColisorProximidade.radius = 0.12f;

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

        m_LineRenderer.startWidth = 0.008f;
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
        m_ReticuloObj.name = $"Reticulo_PacMan_{tipoMao}";
        m_ReticuloObj.transform.localScale = Vector3.one * 0.04f;

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

    private void ProcessarRaycastEspacial()
    {
        Vector3 origem = transform.position;
        Vector3 direcao = transform.forward;
        Vector3 pontoFinal = origem + (direcao * distanciaMaximaRaycast);

        RaycastHit hit;
        bool colidiu = Physics.Raycast(origem, direcao, out hit, distanciaMaximaRaycast, camadasAlvo);

        IInstanciaSemantica instanciaDetectada = null;

        if (colidiu)
        {
            pontoFinal = hit.point;
            instanciaDetectada = hit.collider.GetComponentInParent<IInstanciaSemantica>();
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
                AjustarCorLaser(corLaserFoco);
                DispararVibracaoHaptica(0.15f, 0.05f);
            }
            else
            {
                AjustarCorLaser(corLaserRepouso);
            }
        }
    }

    private void VerificarEntradaInteracao()
    {
        if (m_InstanciaSobFoco == null) return;

        bool acionado = false;

        if (Input.GetButtonDown("Fire1") || Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
        {
            acionado = true;
        }

        if (m_DispositivoXR.isValid)
        {
            bool triggerPressionado = false;
            if (m_DispositivoXR.TryGetFeatureValue(CommonUsages.triggerButton, out triggerPressionado) && triggerPressionado)
            {
                acionado = true;
            }

            bool primaryButton = false;
            if (m_DispositivoXR.TryGetFeatureValue(CommonUsages.primaryButton, out primaryButton) && primaryButton)
            {
                acionado = true;
            }
        }

        if (acionado)
        {
            m_InstanciaSobFoco.Interagir(gameObject, ehRaycast: true);
            m_InstanciaSobFoco = null;
            AjustarCorLaser(corLaserRepouso);
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
