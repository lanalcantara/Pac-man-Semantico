using UnityEngine;

/// <summary>
/// ============================================================================
/// PAC-MAN SEMÂNTICO (Semantic Pac-Man) - IA Autônoma de Evasão das Gemas
/// ============================================================================
/// Componente de comportamento dinâmico que faz as gemas amarelas (espécimes válidos)
/// fugirem ativamente do Pac-Man sempre que ele entrar no raio de detecção (3.0 metros).
///
/// Características:
/// 1. Elimina empurrão físico bruto (isKinematic = true no Rigidbody e colisores como Trigger).
/// 2. Velocidade de fuga equilibrada e inferior à do jogador (50% da velocidade do Pac-Man = 1.9 m/s).
/// 3. Raio de detecção estritamente fixado em 3 metros para permitir encurralamento estratégico nos corredores.
/// 4. Varredura multidirecional (raycasts) com salvaguardas de nulidade para evitar congelamento de IA.
/// ============================================================================
/// </summary>
[DisallowMultipleComponent]
public class GemaFugitiva : MonoBehaviour
{
    [Header("Configurações de Evasão Tática")]
    [Tooltip("Habilita o comportamento de fuga dinâmica.")]
    public bool habilitarFuga = true;

    [Tooltip("Raio de proximidade em metros para acionar a fuga ativa do Pac-Man (estritamente 3.0m).")]
    public float raioDeteccaoFuga = 2.8f;

    [Tooltip("Velocidade linear de fuga pelos corredores (estritamente 50% da velocidade do jogador = 1.9 m/s).")]
    public float velocidadeFuga = 1.6f;

    [Tooltip("Velocidade de rotação ao fugir.")]
    public float velocidadeRotacao = 240.0f;

    [Tooltip("Raio do colisor Trigger para captura tática facilitada.")]
    public float raioCaptura = 1.3f;

    [Header("Referências")]
    [Tooltip("Transform do Pac-Man (Jogador). Se nulo, localiza automaticamente.")]
    public Transform alvoPacMan;

    private Rigidbody m_Rigidbody;
    private CharacterController m_CharacterController;
    private float m_AlturaPiso = 0f;
    private bool m_AlturaPisoDefinida = false;

    private void Awake()
    {
        // 1. Desativa física bruta passiva para que a gema não seja empurrada como um bloco inerte
        m_Rigidbody = GetComponent<Rigidbody>();
        if (m_Rigidbody != null)
        {
            m_Rigidbody.isKinematic = true;
            m_Rigidbody.useGravity = false;
            m_Rigidbody.linearVelocity = Vector3.zero;
        }

        m_CharacterController = GetComponent<CharacterController>();

        // 2. Garante que TODOS os colisores sejam Triggers (isTrigger = true) para eliminar empurrão físico
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
    }

    private void Start()
    {
        LocalizarPacMan();
        DefinirAlturaPisoInicial();
    }

    private void Update()
    {
        if (!habilitarFuga) return;

        if (alvoPacMan == null)
        {
            LocalizarPacMan();
            if (alvoPacMan == null) return;
        }

        float dist = Vector3.Distance(transform.position, alvoPacMan.position);
        // Coleta tática imediata ao contato horizontal
        Vector2 posGema2D = new Vector2(transform.position.x, transform.position.z);
        Vector2 posPacMan2D = new Vector2(alvoPacMan.position.x, alvoPacMan.position.z);
        if (Vector2.Distance(posGema2D, posPacMan2D) <= 1.95f && Mathf.Abs(transform.position.y - alvoPacMan.position.y) < 3.0f)
        {
            var gemaBase = GetComponent<InstanciaSemanticaBase>();
            if (gemaBase != null) gemaBase.Interagir(alvoPacMan.gameObject, false);
            return;
        }

        if (dist < raioDeteccaoFuga && dist > 0.05f)
        {
            ExecutarFuga(dist);
        }
    }

    private void DefinirAlturaPisoInicial()
    {
        if (!m_AlturaPisoDefinida)
        {
            m_AlturaPiso = transform.position.y;
            m_AlturaPisoDefinida = true;
        }
    }

    /// <summary>
    /// Localiza o Transform do jogador com salvaguardas múltiplas (Tag, XR Origin, Controladores e Câmera).
    /// </summary>
    public void LocalizarPacMan()
    {
        if (alvoPacMan != null) return;

        GameObject p = GameObject.FindWithTag("Player");
        if (p != null)
        {
            alvoPacMan = p.transform;
            return;
        }

        var ctrlXR = FindFirstObjectByType<ControladorXRJogador>();
        if (ctrlXR != null)
        {
            alvoPacMan = ctrlXR.transform;
            return;
        }

        var ctrl = FindFirstObjectByType<PacMan.Player.ControladorJogador>();
        if (ctrl != null)
        {
            alvoPacMan = ctrl.transform;
            return;
        }

        var comeCome = FindFirstObjectByType<MovimentoComeCome>();
        if (comeCome != null)
        {
            alvoPacMan = comeCome.transform;
            return;
        }

        if (Camera.main != null)
        {
            alvoPacMan = Camera.main.transform;
        }
    }

    /// <summary>
    /// Executa o cálculo dinâmico de fuga da gema em direção aos corredores abertos.
    /// Velocidade limitada estritamente a 50% da velocidade do jogador (1.9 m/s).
    /// </summary>
    private void ExecutarFuga(float distAoJogador)
    {
        if (alvoPacMan == null) return;

        Vector3 posPacMan = alvoPacMan.position;
        Vector3 dirFuga = (transform.position - posPacMan);
        dirFuga.y = 0f;

        if (dirFuga.sqrMagnitude < 0.0001f)
        {
            dirFuga = transform.forward;
        }
        dirFuga.Normalize();

        Vector3 melhorDirecao = Vector3.zero;

        // 1. Testa direção diretamente oposta
        if (ChecarCorredorLivre(dirFuga, 0.6f))
        {
            melhorDirecao = dirFuga;
        }
        else
        {
            // 2. Varredura angular para encontrar o corredor livre mais distante do Pac-Man
            float[] angulos = new float[] { 30f, -30f, 60f, -60f, 90f, -90f, 120f, -120f, 150f, -150f };
            float maiorDistanciaProjetada = float.MinValue;

            foreach (float ang in angulos)
            {
                Vector3 cand = Quaternion.Euler(0f, ang, 0f) * dirFuga;
                cand.y = 0f;
                cand.Normalize();

                if (ChecarCorredorLivre(cand, 0.5f))
                {
                    Vector3 posProjetada = transform.position + (cand * 1.5f);
                    float distFutura = Vector3.Distance(new Vector3(posProjetada.x, 0f, posProjetada.z), new Vector3(posPacMan.x, 0f, posPacMan.z));
                    if (distFutura > maiorDistanciaProjetada)
                    {
                        maiorDistanciaProjetada = distFutura;
                        melhorDirecao = cand;
                    }
                }
            }
        }

        if (melhorDirecao.sqrMagnitude > 0.01f)
        {
            melhorDirecao.Normalize();
            // A velocidade é mantida estritamente inferior à do jogador (50% do Pac-Man)
            // Quando o jogador está muito próximo (< 1.2m) ou encurrala a gema, desacelera para captura fiável
            float velFinal = (distAoJogador <= 1.2f) ? Mathf.Min(velocidadeFuga, 1.0f) : velocidadeFuga;
            Vector3 deslocamento = melhorDirecao * (velFinal * Time.deltaTime);

            if (ChecarCorredorLivre(melhorDirecao, deslocamento.magnitude + 0.12f))
            {
                if (m_CharacterController != null && m_CharacterController.enabled)
                {
                    m_CharacterController.Move(deslocamento);
                }
                else
                {
                    Vector3 novaPos = transform.position + deslocamento;
                    transform.position = new Vector3(novaPos.x, m_AlturaPiso, novaPos.z);
                }

                transform.Rotate(Vector3.up, velocidadeRotacao * Time.deltaTime, Space.World);
            }
        }
    }

    /// <summary>
    /// Verifica se há barreiras físicas (paredes) na direção do deslocamento.
    /// </summary>
    private bool ChecarCorredorLivre(Vector3 direcao, float distancia)
    {
        Vector3 origem = transform.position + Vector3.up * 0.35f;

        if (Physics.Raycast(origem, direcao.normalized, out RaycastHit hit, distancia, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider != null && !hit.collider.isTrigger)
            {
                if (hit.collider.gameObject != gameObject && !hit.collider.transform.IsChildOf(transform))
                {
                    return false;
                }
            }
        }

        return true;
    }
}