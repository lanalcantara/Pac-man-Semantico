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
        m_CharacterController = GetComponent<CharacterController>();
        m_ControladorXR = GetComponent<ControladorXRJogador>();
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

            if (m_CharacterController != null && m_CharacterController.enabled && m_CharacterController.gameObject.activeInHierarchy)
            {
                m_CharacterController.Move(direcao * velocidade * Time.deltaTime);
            }
            else
            {
                transform.Translate(direcao * velocidade * Time.deltaTime, Space.World);
            }

            Quaternion rotacaoAlvo = Quaternion.LookRotation(direcao, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotacaoAlvo, velocidadeRotacao * Time.deltaTime);
        }
    }
}
