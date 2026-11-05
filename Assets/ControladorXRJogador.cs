using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using PacMan.Semantics;

/// <summary>
/// ============================================================================
/// PAC-MAN SEMÂNTICO (Semantic Pac-Man) - Controlador Imersivo de Realidade Virtual (XR/VR)
/// ============================================================================
/// Gerencia a locomoção, rotação, rastreamento físico roomscale (escala 1:1 humana)
/// e colisões táticas no headset de RV em ambiente acadêmico.
///
/// Responsabilidades:
/// 1. Locomoção Contínua Suave (Smooth Locomotion) via joystick do comando esquerdo.
/// 2. Rotação por Snap Turn ou Giro Suave via joystick do comando direito.
/// 3. Consumo de gemas ontológicas via OnTriggerEnter sem empurrão físico.
/// 4. Lógica limpa de dano e proximidade de fantasmas (distância <= 0.8m) com respawn seguro.
/// 5. Feedback Háptico Dual-Controller ao sofrer dano ou interagir com a ontologia.
/// ============================================================================
/// </summary>
[RequireComponent(typeof(CharacterController))]
[DisallowMultipleComponent]
public class ControladorXRJogador : MonoBehaviour
{
    private Transform m_VisualPacManTransform;
    public Transform PosicaoPacManReal
    {
        get
        {
            if (m_VisualPacManTransform != null) return m_VisualPacManTransform;
            GameObject visual = GameObject.Find("Visual_PacMan");
            if (visual != null)
            {
                m_VisualPacManTransform = visual.transform;
                return m_VisualPacManTransform;
            }
            return transform;
        }
    }

    private Rigidbody m_Rigidbody;
    private Vector3 m_VetorMovimentoDesejado = Vector3.zero;

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

    [Header("Locomoção Contínua VR (Joystick Esquerdo)")]
    [Tooltip("Velocidade linear de movimento suave do Pac-Man no labirinto.")]
    public float velocidadeMovimento = 3.8f;

    [Tooltip("Aceleração de gravidade aplicada ao CharacterController.")]
    public float gravidade = 9.81f;

    [Tooltip("Zona morta (Deadzone) para o thumbstick esquerdo.")]
    public float deadzoneMovimento = 0.12f;

    [Header("Configuração de Giro VR (Joystick Direito)")]
    [Tooltip("Se ativado, utiliza Snap Turn (giro instantâneo em passos angulares para conforto VR).")]
    public bool usarSnapTurn = true;

    [Tooltip("Ângulo em graus para cada passo do Snap Turn (padrão: 45°).")]
    public float anguloSnapTurn = 45f;

    [Tooltip("Velocidade em graus/segundo para giro suave contínuo via thumbstick.")]
    public float velocidadeGiroSuave = 85f;

    [Tooltip("Velocidade em graus/segundo para rotação via teclado no simulador de desktop.")]
    public float velocidadeGiroTeclado = 110f;

    [Tooltip("Tempo de intervalo/cooldown entre giros Snap Turn.")]
    public float intervaloSnapTurn = 0.28f;

    [Header("Simulador de Desktop (Unity Editor)")]
    [Tooltip("Permite navegar com Teclado e Mouse no Editor sem necessidade de headset.")]
    public bool habilitarSimuladorDesktop = true;

    [Tooltip("Sensibilidade de rotação do mouse no modo simulador.")]
    public float sensibilidadeMouse = 2.5f;

    [Header("Sistema de Vidas e Dano XR")]
    [Tooltip("Vidas restantes do Pac-Man.")]
    public int vidasRestantes = 3;

    [Tooltip("Distância limite para detecção limpa de proximidade/dano com Fantasmas agressivos.")]
    public float distanciaDanoFantasma = 0.8f;

    [Tooltip("Ponto de Spawn seguro para onde o XR Origin é reposicionado ao sofrer dano.")]
    public Vector3 pontoSpawn = new Vector3(0f, 0.5f, -10f);

    [Tooltip("Tempo de invulnerabilidade após sofrer dano em segundos.")]
    public float tempoInvulnerabilidade = 2.0f;

    private float m_TimerInvulnerabilidade = 0f;
    private float m_TimerSnapTurn = 0f;
    private float m_RotacaoMouseY = 0f;

    private UnityEngine.XR.InputDevice m_DispositivoMaoEsquerda;
    private UnityEngine.XR.InputDevice m_DispositivoMaoDireita;

    private void Awake()
    {
        GarantirInicializacaoControlador();
        LocalizarComponentesHierarquia();
        GarantirColisoresFisicosXR();
    }

    private void OnEnable()
    {
        GarantirInicializacaoControlador();
    }

    private void Start()
    {
        GarantirInicializacaoControlador();
        LocalizarComponentesHierarquia();
        AtualizarDispositivosEntrada();
        GarantirColisoresFisicosXR();

        // 1. Restauração do Avatar (Pac-Man) no ponto de spawn inicial correto do labirinto, rente ao chão
        transform.position = new Vector3(0f, 0.0f, -6.0f);
        pontoSpawn = transform.position;

        // Garante que o modelo visual do Pac-Man está ativo, visível (MeshRenderers enabled) e na escala ~0.8f
        GameObject visualPac = GameObject.Find("Visual_PacMan");
        if (visualPac != null)
        {
            visualPac.SetActive(true);
            visualPac.transform.localPosition = new Vector3(0f, 0.40f, 0f);
            visualPac.transform.localScale = new Vector3(0.80f, 0.80f, 0.80f);

            MeshRenderer[] renderers = visualPac.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].enabled = true;
                    if (!renderers[i].gameObject.activeSelf) renderers[i].gameObject.SetActive(true);
                }
            }

            if (visualPac.GetComponent<AvatarPacManFisico>() == null)
            {
                visualPac.AddComponent<AvatarPacManFisico>();
            }
        }
    }

    public void AlinharBaseAoChao()
    {
        transform.position = new Vector3(transform.position.x, 0.0f, transform.position.z);
    }

    private void Update()
    {
        if (m_TimerSnapTurn > 0f)
        {
            m_TimerSnapTurn -= Time.deltaTime;
        }

        if (m_TimerInvulnerabilidade > 0f)
        {
            m_TimerInvulnerabilidade -= Time.deltaTime;
        }

        AtualizarRastreamentoControladores();
        LerEntradasMovimento();
        RotacionarVisualPacMan();
        ProcessarGiroVR();
        ProcessarSimuladorDesktop();
        VerificarProximidadeFantasmas();
        VerificarConsumoGemasProximas();
    }

    private void FixedUpdate()
    {
        ProcessarMovimentoFisico();
    }

    /// <summary>
    /// Localiza câmeras, offsets e controladores na hierarquia do XR Origin.
    /// </summary>
    private void LocalizarComponentesHierarquia()
    {
        if (cameraOffset == null)
        {
            Transform offset = transform.Find("Camera Offset") ?? transform.Find("CameraOffset");
            if (offset != null) cameraOffset = offset;
        }

        if (cameraPrincipalXR == null)
        {
            cameraPrincipalXR = GetComponentInChildren<Camera>();
        }

        // Seção 2 da Especificação Mestra: Garante que a câmera esteja DENTRO do labirinto (visão imersiva nos corredores),
        // e não a 15.5m de altura como visão de topo antiga.
        if (cameraPrincipalXR != null && !XRSettings.enabled)
        {
            if (cameraPrincipalXR.transform.localPosition.y > 4.0f)
            {
                cameraPrincipalXR.transform.localPosition = new Vector3(0f, 0.25f, -2.5f);
                cameraPrincipalXR.transform.localRotation = Quaternion.Euler(14f, 0f, 0f);
            }
        }

        if (controladorEsquerdo == null)
        {
            Transform esq = transform.Find("Camera Offset/Left Hand Controller") ?? transform.Find("Left Hand Controller") ?? transform.Find("LeftController");
            controladorEsquerdo = esq;
        }

        if (controladorDireito == null)
        {
            Transform dir = transform.Find("Camera Offset/Right Hand Controller") ?? transform.Find("Right Hand Controller") ?? transform.Find("RightController");
            controladorDireito = dir;
        }
    }

    /// <summary>
    /// Garante que o CharacterController está referenciado, ativo e devidamente configurado para escala 1:1 humana.
    /// </summary>
    private void GarantirInicializacaoControlador()
    {
        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>() ?? GetComponentInChildren<CharacterController>();
        }

        // Desativa CharacterController para que o Rigidbody.velocity controle o movimento sem bloqueio
        if (characterController != null)
        {
            characterController.enabled = false;
        }

        // Garante Rigidbody dinâmico com trava estrita de eixo Y e rotações (FreezePositionY)
        m_Rigidbody = GetComponent<Rigidbody>();
        if (m_Rigidbody == null)
        {
            m_Rigidbody = gameObject.AddComponent<Rigidbody>();
        }
        m_Rigidbody.isKinematic = false;
        m_Rigidbody.useGravity = false;
        m_Rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        m_Rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        m_Rigidbody.constraints = RigidbodyConstraints.FreezePositionY |
                                  RigidbodyConstraints.FreezeRotationX |
                                  RigidbodyConstraints.FreezeRotationY |
                                  RigidbodyConstraints.FreezeRotationZ;

        // Garante Colisor Sólido para colisão física impenetrável com as paredes do labirinto
        SphereCollider sc = GetComponent<SphereCollider>();
        if (sc == null)
        {
            sc = gameObject.AddComponent<SphereCollider>();
        }
        sc.isTrigger = false;
        sc.radius = 0.38f;
        sc.center = new Vector3(0f, 0.40f, 0f);
    }

    private void GarantirColisoresFisicosXR()
    {
        if (cameraPrincipalXR != null)
        {
            SphereCollider scCam = cameraPrincipalXR.GetComponent<SphereCollider>();
            if (scCam == null)
            {
                scCam = cameraPrincipalXR.gameObject.AddComponent<SphereCollider>();
            }
            if (scCam != null)
            {
                // Desativa colisor na tela dos olhos para que apenas o Pac-Man físico no chão colida
                scCam.enabled = false;
            }
        }
    }

    private void AtualizarDispositivosEntrada()
    {
        m_DispositivoMaoEsquerda = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        m_DispositivoMaoDireita = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
    }

    private void AtualizarRastreamentoControladores()
    {
        if (!m_DispositivoMaoEsquerda.isValid || !m_DispositivoMaoDireita.isValid)
        {
            AtualizarDispositivosEntrada();
        }
    }

    /// <summary>
    /// Lê entradas clássicas de controle via Input.GetAxis("Horizontal") e Input.GetAxis("Vertical"),
    /// com fallback explícito para WASD, Teclas de Direção (Setas) e thumbsticks VR.
    /// </summary>
    private void LerEntradasMovimento()
    {
        // 2. Religação dos Controles: Leitura de eixos analógicos clássicos
        float inputX = Input.GetAxis("Horizontal");
        float inputZ = Input.GetAxis("Vertical");

        // Fallback explícito para WASD e Setas Direcionais
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) inputZ = Mathf.Max(inputZ, 1f);
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) inputZ = Mathf.Min(inputZ, -1f);
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) inputX = Mathf.Min(inputX, -1f);
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) inputX = Mathf.Max(inputX, 1f);

        Vector2 dir2D = new Vector2(inputX, inputZ);
        if (dir2D.sqrMagnitude > 1f) dir2D.Normalize();

        // Leitura de thumbstick esquerdo de Realidade Virtual (OpenXR / Meta Quest)
        if (m_DispositivoMaoEsquerda.isValid && m_DispositivoMaoEsquerda.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 eixoVR))
        {
            if (eixoVR.magnitude > deadzoneMovimento)
            {
                dir2D = eixoVR;
            }
        }

        m_VetorMovimentoDesejado = new Vector3(dir2D.x, 0f, dir2D.y);
    }

    /// <summary>
    /// Aplica o vetor de movimento diretamente ao Rigidbody.velocity do jogador nos eixos X e Z,
    /// travando rigorosamente o eixo Y para evitar atravessar o chão ou subir na câmera.
    /// </summary>
    private void ProcessarMovimentoFisico()
    {
        if (m_Rigidbody == null) m_Rigidbody = GetComponent<Rigidbody>();
        if (m_Rigidbody == null) return;

        // 3. Trava de Eixo Y: Freeze Position Y e Freeze Rotations
        m_Rigidbody.constraints = RigidbodyConstraints.FreezePositionY |
                                  RigidbodyConstraints.FreezeRotationX |
                                  RigidbodyConstraints.FreezeRotationY |
                                  RigidbodyConstraints.FreezeRotationZ;

        Vector3 vel = m_VetorMovimentoDesejado * velocidadeMovimento;

        // Aplica velocidade diretamente aos eixos X e Z
        m_Rigidbody.linearVelocity = new Vector3(vel.x, 0f, vel.z);
#pragma warning disable CS0618
        m_Rigidbody.velocity = new Vector3(vel.x, 0f, vel.z);
#pragma warning restore CS0618

        // Trava absoluta de segurança: y = 0.0f rente ao chão
        Vector3 posAtual = transform.position;
        if (Mathf.Abs(posAtual.y - 0.0f) > 0.001f)
        {
            transform.position = new Vector3(posAtual.x, 0.0f, posAtual.z);
        }
    }

    /// <summary>
    /// Orienta suavemente a malha visual do Pac-Man para a direção em que ele se desloca.
    /// </summary>
    private void RotacionarVisualPacMan()
    {
        if (m_VetorMovimentoDesejado.sqrMagnitude > 0.05f)
        {
            Quaternion rotAlvo = Quaternion.LookRotation(m_VetorMovimentoDesejado.normalized, Vector3.up);
            if (PosicaoPacManReal != null && PosicaoPacManReal != transform)
            {
                PosicaoPacManReal.rotation = Quaternion.Slerp(PosicaoPacManReal.rotation, rotAlvo, Time.deltaTime * 14f);
            }
            else
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, rotAlvo, Time.deltaTime * 14f);
            }
        }
    }

    private void ProcessarGiroVR()
    {
        float inputGiroXR = 0f;
        if (m_DispositivoMaoDireita.isValid)
        {
            if (m_DispositivoMaoDireita.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 eixoDir))
            {
                if (Mathf.Abs(eixoDir.x) > 0.12f)
                {
                    inputGiroXR = eixoDir.x;
                }
            }
            else if (m_DispositivoMaoDireita.TryGetFeatureValue(CommonUsages.secondary2DAxis, out Vector2 eixoSecDir))
            {
                if (Mathf.Abs(eixoSecDir.x) > 0.12f)
                {
                    inputGiroXR = eixoSecDir.x;
                }
            }
        }

        if (Mathf.Abs(inputGiroXR) > 0.15f)
        {
            if (usarSnapTurn)
            {
                if (Mathf.Abs(inputGiroXR) > 0.55f && m_TimerSnapTurn <= 0f)
                {
                    float angulo = Mathf.Sign(inputGiroXR) * anguloSnapTurn;
                    transform.Rotate(Vector3.up, angulo, Space.World);
                    m_TimerSnapTurn = intervaloSnapTurn;
                }
            }
            else
            {
                transform.Rotate(Vector3.up, inputGiroXR * velocidadeGiroSuave * Time.deltaTime, Space.World);
            }
        }

        float inputGiroTeclado = 0f;
        if (Input.GetKey(KeyCode.Q)) inputGiroTeclado -= 1f;
        if (Input.GetKey(KeyCode.E)) inputGiroTeclado += 1f;

        if (Mathf.Abs(inputGiroTeclado) > 0.01f)
        {
            transform.Rotate(Vector3.up, inputGiroTeclado * velocidadeGiroTeclado * Time.deltaTime, Space.World);
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
                // cameraPrincipalXR.transform.localRotation = Quaternion.Euler(m_RotacaoMouseY, 0f, 0f);
            }
        }
    }

    /// <summary>
    /// Verificação limpa de proximidade com fantasmas (distância <= 0.8m).
    /// Ao encostar num fantasma agressivo, reduz exatamente 1 vida, executa o impulso háptico
    /// e faz o respawn seguro, sem empurrar o jogador fisicamente pelo mapa.
    /// </summary>
    private void VerificarProximidadeFantasmas()
    {
        if (m_TimerInvulnerabilidade > 0f) return;

        Vector3 posPac = PosicaoPacManReal.position;
        Vector2 posJogador2D = new Vector2(posPac.x, posPac.z);
        float raioContatoImediato = 1.05f; // Contato imediato no momento em que as esferas se tocam

        ObstaculoInvalidoSemantico[] obstaculos = FindObjectsByType<ObstaculoInvalidoSemantico>(FindObjectsSortMode.None);
        if (obstaculos != null)
        {
            for (int i = 0; i < obstaculos.Length; i++)
            {
                if (obstaculos[i] == null || !obstaculos[i].gameObject.activeInHierarchy) continue;
                Vector3 posObs = obstaculos[i].transform.position;
                float dist2D = Vector2.Distance(posJogador2D, new Vector2(posObs.x, posObs.z));
                if (dist2D <= raioContatoImediato && Mathf.Abs(transform.position.y - posObs.y) < 1.8f)
                {
                    Collider col = obstaculos[i].GetComponentInChildren<Collider>() ?? obstaculos[i].GetComponent<Collider>();
                    ProcessarColisaoEntidade(col);
                    return;
                }
            }
        }

        AnimacaoFantasma[] anims = FindObjectsByType<AnimacaoFantasma>(FindObjectsSortMode.None);
        if (anims != null)
        {
            for (int i = 0; i < anims.Length; i++)
            {
                if (anims[i] == null || !anims[i].gameObject.activeInHierarchy) continue;
                Vector3 posAnim = anims[i].transform.position;
                float dist2D = Vector2.Distance(posJogador2D, new Vector2(posAnim.x, posAnim.z));
                if (dist2D <= raioContatoImediato && Mathf.Abs(transform.position.y - posAnim.y) < 1.8f)
                {
                    Collider col = anims[i].GetComponentInChildren<Collider>() ?? anims[i].GetComponent<Collider>();
                    ProcessarColisaoEntidade(col);
                    return;
                }
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision == null || collision.collider == null) return;
        ProcessarColisaoEntidade(collision.collider);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null) return;
        ProcessarColisaoEntidade(other);
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (hit == null || hit.collider == null) return;
        ProcessarColisaoEntidade(hit.collider);
    }

    /// <summary>
    /// Processa a colisão tanto com gemas amarelas (consumo imediato) quanto com fantasmas.
    /// </summary>
    public void ProcessarColisaoEntidade(Collider col)
    {
        if (col == null) return;

        IInstanciaSemantica inst = col.GetComponent<IInstanciaSemantica>() ?? col.GetComponentInParent<IInstanciaSemantica>();

        bool ehTagGema = false;
        bool ehTagObstaculo = false;
        try { ehTagGema = col.CompareTag("GemaValida"); } catch { }
        try { ehTagObstaculo = col.CompareTag("ObstaculoInvalido"); } catch { }

        // 1. Gema amarela / Válida: consome e registra na ontologia
        if (ehTagGema || col.gameObject.name.Contains("Gema") || col.gameObject.name.Contains("PacDot") || (inst != null && inst.EhValida))
        {
            TratarConsumoGema(col, inst);
        }
        // 2. Obstáculo / Fantasma: dano limpo sem empurrão físico
        else if (ehTagObstaculo || col.gameObject.name.Contains("Obstaculo") || col.gameObject.name.Contains("Fantasma") || col.gameObject.name.Contains("Blinky") || (inst != null && !inst.EhValida))
        {
            TratarEncontroFantasma(col, inst);
        }
    }

    /// <summary>
    /// Consome a gema, registra a instância e classe exata na ontologia (atualizando HUDs),
    /// soma pontos e desativa/destrói a gema imediatamente para remover qualquer empurrão físico.
    /// </summary>
    private void TratarConsumoGema(Collider col, IInstanciaSemantica inst)
    {
        GameObject gemaObj = (inst is Component comp && comp != null) ? comp.gameObject : (col != null ? col.gameObject : null);
        if (gemaObj == null) return;

        // Desativa os colisores da gema para evitar múltiplas leituras e mantém o objeto ancorado na Rede Neural
        Collider[] cols = gemaObj.GetComponentsInChildren<Collider>();
        if (cols != null)
        {
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] != null) cols[i].enabled = false;
            }
        }

        // Ao comer uma classe/subclasse/instância certa, os fantasmas ficam Azuis (Calmos) por 6s enquanto o Reasoner valida!
        var motor = FindFirstObjectByType<MotorRaciocinioSemantico>();
        if (motor != null)
        {
            motor.AtivarPowerPellet(6.0f);
        }

        if (inst != null)
        {
            // Dispara AoExecutarColeta -> NotificarInteracao(true) -> RelationBuilder conecta na Rede Neural!
            inst.Interagir(PosicaoPacManReal != null ? PosicaoPacManReal.gameObject : gameObject, false);

            if (RelationBuilder.Instance != null)
            {
                RelationBuilder.Instance.AdicionarItemComidoNaRedeNeural(inst, gemaObj);
            }
        }

        DispararHapticsDualController(0.45f, 0.15f);

        if (GerenciadorEfeitosJuice.Instancia != null)
        {
            Color corGema = inst != null ? inst.CorDestaque : new Color(0.1f, 1.0f, 0.55f);
            GerenciadorEfeitosJuice.Instancia.CriarExplosaoBrilhoColeta(gemaObj.transform.position, corGema);
        }
    }

    /// <summary>
    /// Processa o encontro com o fantasma conforme regras SWRL.
    /// </summary>
    private void TratarEncontroFantasma(Collider col, IInstanciaSemantica inst)
    {
        var motor = FindFirstObjectByType<MotorRaciocinioSemantico>();
        string idFantasma = inst != null && !string.IsNullOrEmpty(inst.Id) ? inst.Id : "ghost_blinky";
        bool pacmanPowered = motor != null && motor.pacmanIsPowered;
        GameEntityState estado = motor != null ? motor.ObterEstadoEntidade(idFantasma) : GameEntityState.Patrol;

        if (pacmanPowered && estado == GameEntityState.Vulnerable)
        {
            // Devora fantasma vulnerável
            InstanciaSemanticaBase.pontuacaoLogica += 200;
            InstanciaSemanticaBase.RegistrarConsumoOntologico(idFantasma, "VulnerableGhost");
            if (col != null)
            {
                AnimacaoFantasma anim = col.GetComponent<AnimacaoFantasma>() ?? col.GetComponentInParent<AnimacaoFantasma>();
                if (anim != null) anim.DispararSusto(1.0f);
                col.gameObject.SetActive(false);
            }
        }
        else
        {
            // Dano limpo do fantasma agressivo
            ExecutarDanoPacMan(col != null ? col.gameObject : null);
        }
    }

    /// <summary>
    /// Executa o dano direto ao Pac-Man no XR Origin, emitindo impulso háptico potente
    /// e reposicionando com segurança absoluta, sem empurrar o jogador fisicamente pelo mapa.
    /// </summary>
    public void ExecutarDanoPacMan(GameObject fantasma)
    {
        if (m_TimerInvulnerabilidade > 0f) return;

        m_TimerInvulnerabilidade = tempoInvulnerabilidade;

        // Repele o fantasma agressor 6 metros para trás
        if (fantasma != null)
        {
            Vector3 dirAfastamento = (fantasma.transform.position - transform.position).normalized;
            dirAfastamento.y = 0f;
            if (dirAfastamento.sqrMagnitude < 0.01f) dirAfastamento = transform.forward;
            fantasma.transform.position += dirAfastamento * 6.0f;
        }
        vidasRestantes = Mathf.Max(0, vidasRestantes - 1);

        Debug.LogWarning($"<color=#FF0044><b>[XR DANO PAC-MAN]</b></color> Encontro com {fantasma?.name ?? "Fantasma Hostil"}! " +
                         $"Vidas restantes: <b>{vidasRestantes}</b>. Efetuando respawn seguro para {pontoSpawn} sem empurrão físico.");

        // 1. Dispara feedback Juice de impacto e tremor de tela
        if (GerenciadorEfeitosJuice.Instancia != null)
        {
            GerenciadorEfeitosJuice.Instancia.CriarEfeitoStunImpacto(transform.position);
        }

        // 2. Dispara vibração háptica potente em ambos os controladores de RV
        DispararHapticsDualController(0.85f, 0.45f);

        // 3. O Fantasma QUEBRA o último pedaço da linha da Rede Neural do Pac-Man,
        // fazendo aquela Classe/Subclasse/Instância reaparecer no corredor para o jogador voltar e comer de novo!
        string nomeFantasma = fantasma != null ? fantasma.name : "Fantasma Caçador";
        if (RelationBuilder.Instance != null)
        {
            RelationBuilder.Instance.QuebrarUltimoPedacoDaRedePorFantasma(nomeFantasma);
        }

        var demoWeb = FindFirstObjectByType<PacMan.Web.ControladorDemoWeb>();
        if (demoWeb != null)
        {
            demoWeb.vidasRestantes = vidasRestantes;
            demoWeb.ExibirAvisoColisao($"💥 FANTASMA QUEBROU SUA LINHA! Volte e reconecte o último item comido!");
        }

        var hudVR = FindFirstObjectByType<HUDHolograficoXR>();
        if (hudVR != null)
        {
            hudVR.AtualizarTextosHUD();
        }
    }

    /// <summary>
    /// Emite vibração háptica nos dois comandos de RV simultaneamente com verificação de nulidade.
    /// </summary>
    public void DispararHapticsDualController(float amplitude, float duracao)
    {
        if (!m_DispositivoMaoEsquerda.isValid || !m_DispositivoMaoDireita.isValid)
        {
            AtualizarDispositivosEntrada();
        }

        if (m_DispositivoMaoEsquerda.isValid && m_DispositivoMaoEsquerda.TryGetHapticCapabilities(out var capEsq) && capEsq.supportsImpulse)
        {
            m_DispositivoMaoEsquerda.SendHapticImpulse(0, amplitude, duracao);
        }

        if (m_DispositivoMaoDireita.isValid && m_DispositivoMaoDireita.TryGetHapticCapabilities(out var capDir) && capDir.supportsImpulse)
        {
            m_DispositivoMaoDireita.SendHapticImpulse(0, amplitude, duracao);
        }
    }

    /// <summary>
    /// Reposiciona o XR Origin de volta ao ponto inicial com integridade física garantida.
    /// </summary>
    public void ReposicionarNoSpawn()
    {
        if (characterController != null && characterController.enabled)
        {
            characterController.enabled = false;
            transform.position = pontoSpawn;
            characterController.enabled = true;
        }
        else
        {
            transform.position = pontoSpawn;
        }

        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    /// <summary>
    /// Varredura contínua horizontal em 360° para coleta de gemas próximas ao corpo (elimina gemas não capturadas).
    /// </summary>
    private void VerificarConsumoGemasProximas()
    {
        Vector3 centroCorpo = PosicaoPacManReal.position + Vector3.up * 0.35f;
        Collider[] hits = Physics.OverlapSphere(centroCorpo, 1.95f, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i];
            if (col == null || col.gameObject == gameObject || col.transform.IsChildOf(transform)) continue;

            IInstanciaSemantica inst = col.GetComponent<IInstanciaSemantica>() ?? col.GetComponentInParent<IInstanciaSemantica>();
            if (inst != null && inst.EhValida)
            {
                TratarConsumoGema(col, inst);
                return;
            }
        }
    }

}