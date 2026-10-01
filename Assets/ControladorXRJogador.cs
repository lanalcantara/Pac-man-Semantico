using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// ============================================================================
/// PAC-MAN SEMÂNTICO (Semantic Pac-Man) - Realidade Virtual e Aumentada (RV/RA)
/// ============================================================================
/// Controlador de Locomoção e Rastreamento Espacial do Jogador no Pac-Man Semântico.
/// Suporta:
/// 1. Locomoção Contínua Suave (Thumbstick Esquerdo / Teclado WASD).
/// 2. Giro Rápido (Snap Turn 45°) ou Giro Contínuo (Thumbstick Direito / Teclas Q-E).
/// 3. Rastreamento Físico Roomscale da Cabeça (HMD) e Mãos (6DoF / Hand Tracking).
/// 4. Modo Simulador de Desktop/Editor (Mouse Look com Botão Direito + WASD)
///    para testes ágeis sem necessidade de óculos de RV conectados.
/// ============================================================================
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class ControladorXRJogador : MonoBehaviour
{
    [Header("Identidade do Sistema")]
    [Tooltip("Nome do módulo de controle.")]
    public string moduloControle = "Pac-Man Semântico XR Rig";

    [Header("Componentes Físicos")]
    [Tooltip("Referência ao CharacterController do jogador para movimentação e colisões.")]
    public CharacterController characterController;

    [Header("Hierarquia XR Origin")]
    [Tooltip("Transform do Offset de Câmera (ajuste de altura/chão).")]
    public Transform cameraOffset;

    [Tooltip("Câmera principal do jogador (Headset / HMD / Visão do Pac-Man).")]
    public Camera cameraPrincipalXR;

    [Tooltip("Objeto que representa a Mão/Controlador Esquerdo.")]
    public Transform controladorEsquerdo;

    [Tooltip("Objeto que representa a Mão/Controlador Direito.")]
    public Transform controladorDireito;

    [Header("Locomoção Contínua")]
    [Tooltip("Velocidade linear de movimento do Pac-Man no labirinto.")]
    public float velocidadeMovimento = 3.8f;

    [Tooltip("Aceleração de gravidade.")]
    public float gravidade = 9.81f;

    [Header("Configuração de Giro (Turn)")]
    [Tooltip("Se ativado, utiliza Snap Turn (giro instantâneo em passos angulares).")]
    public bool usarSnapTurn = true;

    [Tooltip("Ângulo em graus para cada passo do Snap Turn.")]
    public float anguloSnapTurn = 45f;

    [Tooltip("Velocidade em graus/segundo para giro suave contínuo.")]
    public float velocidadeGiroSuave = 75f;

    [Tooltip("Tempo de recarga entre giros Snap Turn.")]
    public float intervaloSnapTurn = 0.3f;

    [Header("Simulador de Desktop (Unity Editor)")]
    [Tooltip("Permite navegar com Teclado e Mouse no Editor sem necessidade de headset.")]
    public bool habilitarSimuladorDesktop = true;

    [Tooltip("Sensibilidade de rotação do mouse no modo simulador.")]
    public float sensibilidadeMouse = 2.5f;

    private float m_VelocidadeVertical = 0f;
    private float m_TimerSnapTurn = 0f;
    private float m_RotacaoMouseY = 0f;

    private UnityEngine.XR.InputDevice m_DispositivoMaoEsquerda;
    private UnityEngine.XR.InputDevice m_DispositivoMaoDireita;

    private void Awake()
    {
        // Desativa temporariamente o script para impedir qualquer chamada de movimento antes da prontidão total
        enabled = false;

        GarantirInicializacaoControlador();

        if (cameraOffset == null)
        {
            Transform offset = transform.Find("Camera Offset");
            if (offset != null) cameraOffset = offset;
        }

        if (cameraPrincipalXR == null)
        {
            cameraPrincipalXR = GetComponentInChildren<Camera>();
        }

        LocalizarControladoresSeNecessario();

        // Garante a reativação caso a chamada padrão de Start seja adiada pelo Unity
        Invoke(nameof(AtivarControladorNoStart), 0f);
    }

    private void AtivarControladorNoStart()
    {
        if (!enabled)
        {
            Start();
        }
    }

    private void OnEnable()
    {
        GarantirInicializacaoControlador();
    }

    private void Start()
    {
        GarantirInicializacaoControlador();
        AtualizarDispositivosEntrada();

        // Reativa o script somente agora, garantindo que o CharacterController e o XR Origin estão 100% instanciados e ativos
        enabled = true;
    }

    private void Update()
    {
        m_TimerSnapTurn -= Time.deltaTime;

        AtualizarRastreamentoControladores();
        ProcessarGiro();
        ProcessarMovimento();
        ProcessarSimuladorDesktop();
    }

    /// <summary>
    /// Garante que o CharacterController está referenciado, ativo e devidamente configurado.
    /// </summary>
    private void GarantirInicializacaoControlador()
    {
        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>() ?? GetComponentInChildren<CharacterController>();
        }

        if (characterController != null)
        {
            if (!characterController.enabled && gameObject.activeInHierarchy)
            {
                characterController.enabled = true;
            }

            characterController.center = new Vector3(0f, 0.9f, 0f);
            characterController.height = 1.8f;
            characterController.radius = 0.35f;
            characterController.minMoveDistance = 0.001f;
            characterController.skinWidth = 0.08f;
        }
    }

    private void LocalizarControladoresSeNecessario()
    {
        if (controladorEsquerdo == null)
        {
            Transform esq = transform.Find("Camera Offset/Left Hand Controller") ?? transform.Find("Left Hand Controller");
            controladorEsquerdo = esq;
        }

        if (controladorDireito == null)
        {
            Transform dir = transform.Find("Camera Offset/Right Hand Controller") ?? transform.Find("Right Hand Controller");
            controladorDireito = dir;
        }
    }

    private void AtualizarDispositivosEntrada()
    {
        List<UnityEngine.XR.InputDevice> dispositivosEsq = new List<UnityEngine.XR.InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Left | InputDeviceCharacteristics.Controller, dispositivosEsq);
        if (dispositivosEsq.Count > 0) m_DispositivoMaoEsquerda = dispositivosEsq[0];

        List<UnityEngine.XR.InputDevice> dispositivosDir = new List<UnityEngine.XR.InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller, dispositivosDir);
        if (dispositivosDir.Count > 0) m_DispositivoMaoDireita = dispositivosDir[0];
    }

    private void AtualizarRastreamentoControladores()
    {
        if (!m_DispositivoMaoEsquerda.isValid || !m_DispositivoMaoDireita.isValid)
        {
            AtualizarDispositivosEntrada();
        }

        if (m_DispositivoMaoEsquerda.isValid && controladorEsquerdo != null)
        {
            Vector3 pos;
            Quaternion rot;
            if (m_DispositivoMaoEsquerda.TryGetFeatureValue(CommonUsages.devicePosition, out pos))
                controladorEsquerdo.localPosition = pos;
            if (m_DispositivoMaoEsquerda.TryGetFeatureValue(CommonUsages.deviceRotation, out rot))
                controladorEsquerdo.localRotation = rot;
        }

        if (m_DispositivoMaoDireita.isValid && controladorDireito != null)
        {
            Vector3 pos;
            Quaternion rot;
            if (m_DispositivoMaoDireita.TryGetFeatureValue(CommonUsages.devicePosition, out pos))
                controladorDireito.localPosition = pos;
            if (m_DispositivoMaoDireita.TryGetFeatureValue(CommonUsages.deviceRotation, out rot))
                controladorDireito.localRotation = rot;
        }
    }

    private void ProcessarMovimento()
    {
        Vector2 entradaMove = Vector2.zero;

        // 1. Thumbstick XR Esquerdo
        if (m_DispositivoMaoEsquerda.isValid)
        {
            Vector2 eixoXR;
            if (m_DispositivoMaoEsquerda.TryGetFeatureValue(CommonUsages.primary2DAxis, out eixoXR))
            {
                entradaMove += eixoXR;
            }
        }

        // 2. Fallback Teclado WASD / Setas
        float inputHorizontal = Input.GetAxisRaw("Horizontal");
        float inputVertical = Input.GetAxisRaw("Vertical");
        if (Mathf.Abs(inputHorizontal) > 0.05f || Mathf.Abs(inputVertical) > 0.05f)
        {
            entradaMove += new Vector2(inputHorizontal, inputVertical);
        }

        entradaMove = Vector2.ClampMagnitude(entradaMove, 1f);

        Transform orientacaoReferencia = cameraPrincipalXR != null ? cameraPrincipalXR.transform : transform;
        Vector3 frente = orientacaoReferencia.forward;
        Vector3 direita = orientacaoReferencia.right;

        frente.y = 0f;
        direita.y = 0f;
        frente.Normalize();
        direita.Normalize();

        Vector3 direcaoDesejada = (frente * entradaMove.y) + (direita * entradaMove.x);

        // Guarda explícita e rigorosa de segurança antes de qualquer interação com o CharacterController
        if (characterController != null && characterController.enabled && characterController.gameObject.activeInHierarchy)
        {
            if (characterController.isGrounded)
            {
                m_VelocidadeVertical = -0.5f;
            }
            else
            {
                m_VelocidadeVertical -= gravidade * Time.deltaTime;
            }

            Vector3 movimentoFinal = (direcaoDesejada * velocidadeMovimento) + (Vector3.up * m_VelocidadeVertical);

            // Executa Move com dupla guarda de segurança
            if (characterController != null && characterController.enabled && characterController.gameObject.activeInHierarchy)
            {
                characterController.Move(movimentoFinal * Time.deltaTime);
            }

            // Roomscale Height Tracking
            if (cameraPrincipalXR != null && characterController != null && characterController.enabled && characterController.gameObject.activeInHierarchy)
            {
                float alturaHeadset = Mathf.Clamp(cameraPrincipalXR.transform.localPosition.y, 0.8f, 2.3f);
                characterController.height = alturaHeadset;
                characterController.center = new Vector3(
                    cameraPrincipalXR.transform.localPosition.x,
                    alturaHeadset / 2f,
                    cameraPrincipalXR.transform.localPosition.z
                );
            }
        }
        else
        {
            // Fallback de movimentação direta via Transform caso o CharacterController esteja inativo ou desativado
            Vector3 movimentoFinal = direcaoDesejada * velocidadeMovimento;
            if (movimentoFinal.sqrMagnitude > 0.0001f)
            {
                transform.Translate(movimentoFinal * Time.deltaTime, Space.World);
            }
        }
    }

    private void ProcessarGiro()
    {
        float inputGiro = 0f;

        if (m_DispositivoMaoDireita.isValid)
        {
            Vector2 eixoDir;
            if (m_DispositivoMaoDireita.TryGetFeatureValue(CommonUsages.primary2DAxis, out eixoDir))
            {
                inputGiro = eixoDir.x;
            }
        }

        if (Input.GetKey(KeyCode.Q)) inputGiro = -1f;
        if (Input.GetKey(KeyCode.E)) inputGiro = 1f;

        if (usarSnapTurn)
        {
            if (Mathf.Abs(inputGiro) > 0.6f && m_TimerSnapTurn <= 0f)
            {
                float angulo = Mathf.Sign(inputGiro) * anguloSnapTurn;
                transform.RotateAround(cameraPrincipalXR != null ? cameraPrincipalXR.transform.position : transform.position, Vector3.up, angulo);
                m_TimerSnapTurn = intervaloSnapTurn;
            }
        }
        else
        {
            if (Mathf.Abs(inputGiro) > 0.1f)
            {
                transform.Rotate(Vector3.up * inputGiro * velocidadeGiroSuave * Time.deltaTime, Space.World);
            }
        }
    }

    private void ProcessarSimuladorDesktop()
    {
        if (!habilitarSimuladorDesktop) return;

        if (Input.GetMouseButton(1))
        {
            float mouseX = Input.GetAxis("Mouse X") * sensibilidadeMouse;
            float mouseY = Input.GetAxis("Mouse Y") * sensibilidadeMouse;

            transform.Rotate(Vector3.up * mouseX, Space.World);

            if (cameraPrincipalXR != null)
            {
                m_RotacaoMouseY = Mathf.Clamp(m_RotacaoMouseY - mouseY, -85f, 85f);
                cameraPrincipalXR.transform.localRotation = Quaternion.Euler(m_RotacaoMouseY, 0f, 0f);
            }
        }
    }
}
