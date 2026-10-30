using UnityEngine;

/// <summary>
/// ============================================================================
/// PAC-MAN SEMÂNTICO (Semantic Pac-Man) - Realidade Virtual e Aumentada (RV/RA)
/// ============================================================================
/// Controla a movimentação e orientação clássica do Pac-Man no plano 3D.
/// Faz o personagem virar suavemente na direção do movimento no labirinto
/// e integra-se harmoniosamente com o ControladorXRJogador quando em RV/RA.
/// ============================================================================
/// </summary>
public class MovimentoComeCome : MonoBehaviour
{
    [Header("Configurações de Movimento")]
    [Tooltip("Velocidade linear de movimento do Pac-Man.")]
    public float velocidade = 5f;

    [Tooltip("Velocidade de rotação ao mudar de direção.")]
    public float velocidadeRotacao = 12f;

    [Header("Modo de Operação")]
    [Tooltip("Se ativado, redireciona o controle para o ControladorXRJogador caso exista na cena.")]
    public bool integrarComXR = true;

    private CharacterController m_CharacterController;
    private ControladorXRJogador m_ControladorXR;

    private void Awake()
    {
        GarantirCharacterController();
        m_ControladorXR = GetComponent<ControladorXRJogador>();
    }

    private void GarantirCharacterController()
    {
        if (m_CharacterController == null)
        {
            m_CharacterController = GetComponent<CharacterController>() ?? gameObject.AddComponent<CharacterController>();
        }

        if (m_CharacterController != null)
        {
            m_CharacterController.radius = 0.42f;
            m_CharacterController.height = 1.8f;
            m_CharacterController.skinWidth = 0.08f;
            m_CharacterController.minMoveDistance = 0.0001f;
            m_CharacterController.stepOffset = 0.15f;
        }
    }

    private void Update()
    {
        if (integrarComXR && m_ControladorXR != null && m_ControladorXR.enabled)
        {
            return;
        }

        ProcessarMovimentoClassico();
    }

    private void ProcessarMovimentoClassico()
    {
        float entradaX = Input.GetAxis("Horizontal");
        float entradaZ = Input.GetAxis("Vertical");

        Vector3 direcao = new Vector3(entradaX, 0f, entradaZ);

        if (direcao.magnitude > 0.01f)
        {
            direcao.Normalize();

            if (m_CharacterController == null)
            {
                GarantirCharacterController();
            }

            if (m_CharacterController != null)
            {
                if (!m_CharacterController.enabled && gameObject.activeInHierarchy)
                {
                    m_CharacterController.enabled = true;
                }

                // Barreira preditiva de colisão com paredes do labirinto
                float distVerificacao = (velocidade * Time.deltaTime) + m_CharacterController.radius + 0.08f;
                Vector3 centroCapsula = transform.position + new Vector3(0f, m_CharacterController.height / 2f, 0f);

                if (Physics.SphereCast(centroCapsula, m_CharacterController.radius * 0.95f, direcao.normalized, out RaycastHit hit, distVerificacao, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider != null && !hit.collider.isTrigger)
                    {
                        Vector3 normalParede = hit.normal;
                        normalParede.y = 0f;
                        normalParede.Normalize();

                        direcao = Vector3.ProjectOnPlane(direcao, normalParede);
                    }
                }

                float grav = 9.81f;
                float velY = m_CharacterController.isGrounded ? -4.0f : -grav;
                Vector3 movFinal = (direcao * velocidade) + (Vector3.up * velY);
                m_CharacterController.Move(movFinal * Time.deltaTime);

                if (direcao.sqrMagnitude > 0.001f)
                {
                    Quaternion rotacaoAlvo = Quaternion.LookRotation(direcao, Vector3.up);
                    transform.rotation = Quaternion.Slerp(transform.rotation, rotacaoAlvo, velocidadeRotacao * Time.deltaTime);
                }
            }
        }
    }
}
