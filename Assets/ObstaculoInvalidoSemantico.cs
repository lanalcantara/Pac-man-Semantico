using UnityEngine;

/// <summary>
/// ============================================================================
/// PAC-MAN SEMÂNTICO (Semantic Pac-Man) - Modelagem Orientada a Objetos (UML/C++)
/// ============================================================================
/// Classe concreta que representa um Obstáculo Inválido (Violação Ontológica / Fantasma).
/// Representa indivíduos ou axiomas que violam restrições de Description Logic.
/// Penaliza a pontuação do Pac-Man e navega de forma autônoma pelo NavMesh.
///
/// Configurado estritamente com colisor Trigger (isTrigger = true) e física cinemática,
/// eliminando empurrão físico bruto contra o jogador.
/// ============================================================================
/// </summary>
public class ObstaculoInvalidoSemantico : InstanciaSemanticaBase
{
    [Header("Configurações do Obstáculo")]
    [Tooltip("Tempo de intervalo antes de disparar nova penalidade caso o jogador permaneça em contato.")]
    public float tempoCooldownPenalidade = 1.0f;

    [Header("Inteligência Artificial (NavMesh)")]
    [Tooltip("Habilita a perseguição dinâmica ao Pac-Man via malha NavMesh.")]
    public bool habilitarIA = true;

    [Header("Estado do Reasoner Agent (FSM NavMesh)")]
    [Tooltip("Quando o Reasoner está CONSISTENTE (Patrol), o agente vaga proceduralmente e ignora o jogador. Quando INCONSISTENTE (Chase), persegue o jogador.")]
    public bool emModoChaseInconsistente = false;

    private float m_TimerCooldown = 0f;
    private UnityEngine.AI.NavMeshAgent m_NavAgent;
    private Transform m_AlvoPacMan;
    private float m_TimerAtualizacaoIA = 0f;
    private Vector3 m_AlvoPatrulhaProcedural = Vector3.zero;

    protected override void Awake()
    {
        base.Awake();
        m_EhValida = false;
        m_ValorPontuacao = -5;
        if (m_CorDestaque == Color.green || m_CorDestaque == Color.clear)
        {
            m_CorDestaque = new Color(1f, 0.15f, 0.2f);
        }

        // Garante que TODOS os colisores sejam Triggers (isTrigger = true) para eliminar empurrão físico bruto
        Collider[] cols = GetComponentsInChildren<Collider>();
        if (cols != null)
        {
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] != null) cols[i].isTrigger = true;
            }
        }

        // Rigidbody cinemático sem forças físicas passivas
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero;
        }

        m_NavAgent = GetComponent<UnityEngine.AI.NavMeshAgent>();
    }

    protected override void Update()
    {
        base.Update();
        if (m_TimerCooldown > 0f)
        {
            m_TimerCooldown -= Time.deltaTime;
        }

        if (habilitarIA)
        {
            ProcessarNavegacaoIA();
        }
    }

    private void ProcessarNavegacaoIA()
    {
        if (m_NavAgent == null)
        {
            m_NavAgent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        }

        if (m_NavAgent == null || !m_NavAgent.isOnNavMesh) return;

        // Sincroniza estado com o MotorRaciocinioSemantico e ProtegeWebSocketBridge
        var ws = PacMan.Network.ProtegeWebSocketBridge.Instance;
        var motor = FindFirstObjectByType<PacMan.Semantics.MotorRaciocinioSemantico>();
        bool consistente = (ws != null) ? ws.ReasonerConsistent : (motor == null || motor.reasonerConsistente);
        emModoChaseInconsistente = !consistente;

        // Se o MotorRaciocinioSemantico estiver gerenciando a navegação centralmente, delega a ele
        if (motor != null && motor.moverFantasmasDinamicamente) return;

        m_TimerAtualizacaoIA -= Time.deltaTime;
        if (m_TimerAtualizacaoIA <= 0f)
        {
            m_TimerAtualizacaoIA = 0.45f;

            if (emModoChaseInconsistente)
            {
                // CHASE (INCONSISTENTE): Persegue o jogador
                if (m_AlvoPacMan == null)
                {
                    GameObject pac = GameObject.Find("Visual_PacMan") ?? GameObject.FindWithTag("Player");
                    if (pac != null) m_AlvoPacMan = pac.transform;
                }

                if (m_AlvoPacMan != null)
                {
                    m_NavAgent.speed = 2.6f;
                    m_NavAgent.SetDestination(m_AlvoPacMan.position);
                }
            }
            else
            {
                // PATROL (CONSISTENTE): Ignora o jogador e vaga proceduralmente pelo NavMesh
                m_NavAgent.speed = 1.9f;
                if (m_AlvoPatrulhaProcedural == Vector3.zero || Vector3.Distance(transform.position, m_AlvoPatrulhaProcedural) < 1.8f)
                {
                    Vector2 rand = Random.insideUnitCircle * 9.5f;
                    Vector3 candidato = new Vector3(rand.x, transform.position.y, rand.y);
                    if (UnityEngine.AI.NavMesh.SamplePosition(candidato, out UnityEngine.AI.NavMeshHit hit, 4.0f, UnityEngine.AI.NavMesh.AllAreas))
                    {
                        m_AlvoPatrulhaProcedural = hit.position;
                    }
                }
                if (m_AlvoPatrulhaProcedural != Vector3.zero)
                {
                    m_NavAgent.SetDestination(m_AlvoPatrulhaProcedural);
                }
            }
        }
    }

    protected override void AoExecutarColeta(GameObject interator, bool ehRaycast)
    {
        // Obstáculos inválidos não são coletados como gemas
    }

    protected override void AoDetectarViolacao(GameObject interator, bool ehRaycast)
    {
        if (m_TimerCooldown > 0f) return;

        // REJEIÇÃO ABSOLUTA DA CÂMARA/TELA/HMD: NUNCA pontua nem retira vida ao bater na tela!
        if (!ehRaycast && interator != null)
        {
            string nomeInter = interator.name.ToLower();
            if (interator.CompareTag("MainCamera") || nomeInter.Contains("camera") || nomeInter.Contains("screen") || 
                nomeInter.Contains("offset") || nomeInter.Contains("hmd") || interator.GetComponentInParent<Camera>() != null)
            {
                return; // Bloqueio total na câmera!
            }

            // O contato DEVE ser estritamente no corpo físico do Pac-Man no piso
            bool ehPacMan = interator.GetComponent<AvatarPacManFisico>() != null || interator.GetComponentInParent<AvatarPacManFisico>() != null || nomeInter.Contains("pacman");
            if (!ehPacMan)
            {
                GameObject vis = GameObject.Find("Visual_PacMan");
                if (vis != null && (interator == vis || interator.transform.IsChildOf(vis.transform)))
                {
                    ehPacMan = true;
                }
            }
            if (!ehPacMan) return;
        }

        m_TimerCooldown = tempoCooldownPenalidade;

        // Ao tocar em um nodo/agente que representa inconsistência lógica, aciona o estado INCONSISTENTE no Reasoner
        // (Os Reasoner Agents entram em modo Chase - Material Vermelho Sólido - até que o jogador desfaça a ação com Z/U)
        RegistrarConsumoOntologico(m_Id, m_NomeClasse, false, m_ExpressaoDL);

        var motor = FindFirstObjectByType<PacMan.Semantics.MotorRaciocinioSemantico>();
        if (motor != null)
        {
            motor.reasonerConsistente = false;
        }

        string nomeClasseFormatado = FormatarNomeClasse(m_NomeClasse);

        Debug.LogWarning($"<color=#FF3344><b>[Pac-Man Semântico - OBSTÁCULO INVÁLIDO]</b></color> '{nomeClasseFormatado}' ({m_Id}) colidiu com o Pac-Man! " +
                         $"Violação DL: [{m_ExpressaoDL}]. Penalidade: {m_ValorPontuacao} pts. Total: <b>{pontuacaoLogica}</b>.");

        EmitirFeedbackHaptico(interator, 0.85f, 0.35f);
        NotificarInteracao(sucesso: false);
        CriarEfeitoVisual(sucesso: false);

        // Dano imediato e perda de vida vinculados estritamente ao corpo do Pac-Man
        var ctrlXR = FindFirstObjectByType<ControladorXRJogador>();
        if (ctrlXR != null)
        {
            ctrlXR.ExecutarDanoPacMan(gameObject);
        }
        else
        {
            var web = FindFirstObjectByType<PacMan.Web.ControladorDemoWeb>();
            if (web != null) web.ExecutarResetColisao(0.5f);
        }

        // Repele o fantasma 6.0 metros para trás do Pac-Man com salvaguarda no NavMesh
        GameObject visPac = GameObject.Find("Visual_PacMan");
        Vector3 posPac = (visPac != null) ? visPac.transform.position : transform.position;
        Vector3 dirRecuo = (transform.position - posPac).normalized;
        dirRecuo.y = 0f;
        if (dirRecuo.sqrMagnitude < 0.01f) dirRecuo = -transform.forward;

        Vector3 novaPos = transform.position + dirRecuo * 6.0f;
        if (m_NavAgent != null && m_NavAgent.isOnNavMesh)
        {
            if (UnityEngine.AI.NavMesh.SamplePosition(novaPos, out UnityEngine.AI.NavMeshHit hit, 3.0f, UnityEngine.AI.NavMesh.AllAreas))
            {
                m_NavAgent.Warp(hit.position);
            }
            else
            {
                m_NavAgent.Warp(transform.position);
            }
        }
        else
        {
            transform.position = novaPos;
        }
    }
}
