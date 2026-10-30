using UnityEngine;

/// <summary>
/// ============================================================================
/// PAC-MAN SEMÂNTICO (Semantic Pac-Man) - Modelagem Orientada a Objetos (UML/C++)
/// ============================================================================
/// Classe concreta que representa uma Gema Válida (Indivíduo Ontológico Conforme).
/// Satisfaz as regras e restrições de Description Logic (DL) na TBox/ABox.
/// Concede pontos positivos ao Pac-Man ao ser coletada e é destruída do labirinto imediatamente.
///
/// Inclui comportamento de fuga dinâmica (Evasão Tática) quando o jogador se aproxima (< 3m),
/// com velocidade estritamente ajustada para metade da velocidade do jogador (50%),
/// permitindo que o jogador a encurrale estrategicamente nos corredores.
/// ============================================================================
/// </summary>
public class GemaValidaSemantica : InstanciaSemanticaBase
{
    [Header("Configurações da Gema")]
    [Tooltip("Intensidade do brilho emissivo em RV/RA.")]
    public float intensidadeBrilho = 1.2f;

    [Header("Comportamento de Fuga Dinâmica (Parasita Vivo)")]
    [Tooltip("Habilita o comportamento tático de afastamento quando o Pac-Man se aproxima.")]
    public new bool habilitarFuga = false;

    [Tooltip("Distância limite para iniciar a evasão tática (estritamente 3.0 metros).")]
    public new float raioDeteccaoFuga = 2.8f;

    [Tooltip("Velocidade com que a gema se afasta pelos corredores (estritamente 50% da velocidade do jogador = 1.9 m/s).")]
    public new float velocidadeFuga = 1.6f;

    [Tooltip("Raio do colisor Trigger para captura tática facilitada.")]
    public float raioCaptura = 1.8f;

    protected override void Awake()
    {
        base.Awake();
        m_EhValida = true;
        m_ValorPontuacao = 10;
        base.raioDeteccaoFuga = raioDeteccaoFuga;
        base.velocidadeFuga = velocidadeFuga;
        base.habilitarFuga = habilitarFuga;

        // Remoção imediata de NavMeshAgent ou script de fuga para garantir Pac-Dots 100% estáticos
        UnityEngine.AI.NavMeshAgent nav = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (nav != null) Destroy(nav);
        GemaFugitiva fuga = GetComponent<GemaFugitiva>();
        if (fuga != null) Destroy(fuga);
        if (m_CorDestaque == Color.green || m_CorDestaque == Color.clear)
        {
            m_CorDestaque = new Color(0.1f, 1f, 0.45f);
        }

        // Garante que TODOS os colisores sejam Triggers (isTrigger = true) para eliminar empurrão físico
        Collider[] todosColisores = GetComponentsInChildren<Collider>();
        if (todosColisores != null)
        {
            for (int i = 0; i < todosColisores.Length; i++)
            {
                if (todosColisores[i] != null) todosColisores[i].isTrigger = true;
            }
        }

        SphereCollider sc = GetComponent<SphereCollider>();
        if (sc == null)
        {
            sc = gameObject.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = raioCaptura;
        }
        else
        {
            sc.isTrigger = true;
            sc.radius = Mathf.Max(sc.radius, raioCaptura);
        }

        // Garante que a física seja cinemática (sem forças ou empurrões físicos)
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero;
        }
    }

    protected override void Update()
    {
        base.Update();

        // Coleta automática por proximidade absoluta vinculada EXCLUSIVAMENTE ao corpo do Pac-Man
        if (!m_Coletada)
        {
            GameObject pacmanCorpo = GameObject.Find("Visual_PacMan");
            if (pacmanCorpo != null)
            {
                Vector2 posGema2D = new Vector2(transform.position.x, transform.position.z);
                Vector2 posPac2D = new Vector2(pacmanCorpo.transform.position.x, pacmanCorpo.transform.position.z);
                float distHorizontal = Vector2.Distance(posGema2D, posPac2D);
                float diffVertical = Mathf.Abs(transform.position.y - pacmanCorpo.transform.position.y);
                if (distHorizontal <= 1.45f && diffVertical < 1.8f)
                {
                    AoExecutarColeta(pacmanCorpo, ehRaycast: false);
                }
            }
        }
    }

    protected override void AoExecutarColeta(GameObject interator, bool ehRaycast)
    {
        // REJEIÇÃO ABSOLUTA: Câmera ou tela NÃO coletam gemas
        if (!ehRaycast && interator != null)
        {
            string nomeI = interator.name.ToLower();
            if (interator.CompareTag("MainCamera") || nomeI.Contains("camera") || nomeI.Contains("screen") || 
                nomeI.Contains("offset") || nomeI.Contains("hmd") || interator.GetComponentInParent<Camera>() != null)
            {
                return;
            }
        }
        if (m_Coletada) return;
        m_Coletada = true;
        totalColetadas++;
        pontuacaoLogica += m_ValorPontuacao;
        instanciasValidasRestantes = Mathf.Max(0, instanciasValidasRestantes - 1);

        string nomeClasseFormatado = FormatarNomeClasse(m_NomeClasse);

        Debug.Log($"<color=#00FF66><b>[Pac-Man Semântico - GEMA VÁLIDA]</b></color> '{nomeClasseFormatado}' ({m_Id}) capturada! " +
                  $"Axioma DL: [{m_ExpressaoDL}]. +{m_ValorPontuacao} pts. Total: <b>{pontuacaoLogica}</b> " +
                  $"[Método: {(ehRaycast ? "Raycast Espacial" : "Toque Proximidade XR")}]");

        // 1. Registra a instância e a classe exata no sistema ontológico (Grafo VirtOnto)
        RegistrarConsumoOntologico(m_Id, nomeClasseFormatado);

        // 2. Atualiza o HUD em tempo real com a classe correta (ex: Ascaris lumbricoides ou Enterobius vermicularis)
        AtualizarHUDs(m_Id, nomeClasseFormatado, m_ValorPontuacao, m_ExpressaoDL, m_DescricaoSemantica);

        // 3. Emite impulsos hápticos e efeitos visuais
        EmitirFeedbackHaptico(interator, 0.45f, 0.15f);
        NotificarInteracao(sucesso: true);
        CriarEfeitoVisual(sucesso: true);

        // 4. Desativa e destrói IMEDIATAMENTE a gema, anulando qualquer empurrão físico residual
        Collider[] cols = GetComponentsInChildren<Collider>();
        if (cols != null)
        {
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] != null) cols[i].enabled = false;
            }
        }
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    protected override void AoDetectarViolacao(GameObject interator, bool ehRaycast)
    {
        // Gemas válidas não disparam violação
    }
}
