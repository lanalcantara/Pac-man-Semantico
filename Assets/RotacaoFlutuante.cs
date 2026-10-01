using UnityEngine;

/// <summary>
/// ============================================================================
/// PAC-MAN SEMÂNTICO (Semantic Pac-Man) - Realidade Virtual e Aumentada (RV/RA)
/// ============================================================================
/// Adiciona uma rotação suave contínua às gemas colecionáveis do Pac-Man Semântico.
/// Permite controle rigoroso da base para evitar flutuação indesejada ou corte
/// no plano do piso (Plane).
/// ============================================================================
/// </summary>
public class RotacaoFlutuante : MonoBehaviour
{
    [Header("Configurações de Rotação")]
    [Tooltip("Velocidade e eixos de rotação por segundo (padrão no eixo vertical Y para evitar inclinações no chão).")]
    public Vector3 velocidadeRotacao = new Vector3(0f, 60f, 0f);

    [Header("Configurações de Flutuação")]
    [Tooltip("Habilita flutuação/oscilação vertical (padrão desligado para manter a base perfeitamente assentada no chão).")]
    public bool permitirFlutuacao = false;

    [Tooltip("Distância máxima de oscilação vertical.")]
    public float amplitude = 0.08f;

    [Tooltip("Frequência/velocidade da oscilação.")]
    public float frequencia = 2.5f;

    [Tooltip("Garante que qualquer oscilação ocorra apenas acima da base do chão, sem nunca penetrar o plano.")]
    public bool oscilarApenasParaCima = true;

    private Vector3 m_PosicaoInicial;
    private float m_OffsetTempo;
    private bool m_BaseDefinida = false;

    private void Start()
    {
        if (!m_BaseDefinida)
        {
            m_PosicaoInicial = transform.localPosition;
            m_BaseDefinida = true;
        }
        m_OffsetTempo = Random.Range(0f, Mathf.PI * 2f);
    }

    /// <summary>
    /// Sincroniza a posição base com a posição exata assentada sobre o chão calculada pelo gerenciador de cenário.
    /// </summary>
    public void AtualizarPosicaoBase(Vector3 novaPosicaoLocal)
    {
        m_PosicaoInicial = novaPosicaoLocal;
        m_BaseDefinida = true;
    }

    private void Update()
    {
        transform.Rotate(velocidadeRotacao * Time.deltaTime, Space.World);

        if (permitirFlutuacao && amplitude > 0.001f)
        {
            float seno = Mathf.Sin((Time.time * frequencia) + m_OffsetTempo);
            float offsetVertical = oscilarApenasParaCima ? Mathf.Abs(seno) * amplitude : seno * amplitude;
            transform.localPosition = new Vector3(m_PosicaoInicial.x, m_PosicaoInicial.y + offsetVertical, m_PosicaoInicial.z);
        }
    }
}
