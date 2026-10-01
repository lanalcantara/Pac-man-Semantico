using UnityEngine;

/// <summary>
/// ============================================================================
/// PAC-MAN SEMÂNTICO (Semantic Pac-Man) - Animações e Feedback Visual (Juice)
/// ============================================================================
/// Controla o rastreamento ocular e animações dos Fantasmas (Obstáculos Semânticos, ex: Blinky).
/// 
/// Funcionalidades:
/// 1. Suporte a assentamento preciso no chão do Plane sem flutuação indesejada.
/// 2. Orientação dinâmica dos olhos e pupilas em direção ao Pac-Man (jogador).
/// 3. Reação de susto/vibração quando ocorre colisão ou infração ontológica.
/// ============================================================================
/// </summary>
public class AnimacaoFantasma : MonoBehaviour
{
    [Header("Flutuação e Movimento")]
    [Tooltip("Habilita flutuação vertical (padrão desabilitado para manter a base assentada no Plane).")]
    public bool permitirFlutuacao = false;

    [Tooltip("Distância de oscilação vertical.")]
    public float amplitudeFlutuacao = 0.08f;

    [Tooltip("Velocidade da oscilação vertical.")]
    public float frequenciaFlutuacao = 2.8f;

    [Tooltip("Ângulo de balanço lateral (roll).")]
    public float inclinacaoLateral = 0f;

    [Tooltip("Garante que a oscilação não penetre o plano do chão.")]
    public bool oscilarApenasParaCima = true;

    [Header("Olhos e Pupilas")]
    [Tooltip("Transform da pupila esquerda.")]
    public Transform pupilaEsquerda;

    [Tooltip("Transform da pupila direita.")]
    public Transform pupilaDireita;

    [Tooltip("Deslocamento máximo das pupilas ao olhar para o alvo.")]
    public float deslocamentoPupila = 0.035f;

    [Header("Alvo de Rastreamento")]
    [Tooltip("Transform do Pac-Man / Jogador.")]
    public Transform alvoJogador;

    private Vector3 m_PosicaoInicial;
    private float m_DesvioFase;
    private float m_TimerSusto = 0f;
    private bool m_BaseDefinida = false;

    private void Start()
    {
        if (!m_BaseDefinida)
        {
            m_PosicaoInicial = transform.localPosition;
            m_BaseDefinida = true;
        }
        m_DesvioFase = Random.Range(0f, Mathf.PI * 2f);

        LocalizarAlvoEOlhos();
    }

    /// <summary>
    /// Sincroniza a posição base com a posição calculada para assentar no chão.
    /// </summary>
    public void AtualizarPosicaoBase(Vector3 novaPosicaoLocal)
    {
        m_PosicaoInicial = novaPosicaoLocal;
        m_BaseDefinida = true;
    }

    private void Update()
    {
        ProcessarFlutuacao();
        ProcessarOlharPupilas();
        ProcessarReacaoSusto();
    }

    private void LocalizarAlvoEOlhos()
    {
        if (alvoJogador == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null) alvoJogador = player.transform;
            else if (Camera.main != null) alvoJogador = Camera.main.transform;
        }

        if (pupilaEsquerda == null)
        {
            pupilaEsquerda = transform.Find("OlhoEsquerdo/PupilaEsquerda") ?? transform.Find("Pupila_Esq");
        }

        if (pupilaDireita == null)
        {
            pupilaDireita = transform.Find("OlhoDireito/PupilaDireita") ?? transform.Find("Pupila_Dir");
        }
    }

    private void ProcessarFlutuacao()
    {
        if (!permitirFlutuacao && amplitudeFlutuacao <= 0.001f)
        {
            return;
        }

        float tempo = (Time.time * frequenciaFlutuacao) + m_DesvioFase;
        float seno = Mathf.Sin(tempo);
        float offsetVertical = oscilarApenasParaCima ? Mathf.Abs(seno) * amplitudeFlutuacao : seno * amplitudeFlutuacao;
        transform.localPosition = new Vector3(m_PosicaoInicial.x, m_PosicaoInicial.y + offsetVertical, m_PosicaoInicial.z);

        if (inclinacaoLateral > 0.01f)
        {
            float balancoZ = Mathf.Sin(tempo * 0.7f) * inclinacaoLateral;
            transform.localRotation = Quaternion.Euler(0f, transform.localEulerAngles.y, balancoZ);
        }
    }

    private void ProcessarOlharPupilas()
    {
        if (alvoJogador == null) return;

        Vector3 direcaoParaJogador = (alvoJogador.position - transform.position).normalized;
        Vector3 direcaoLocal = transform.InverseTransformDirection(direcaoParaJogador);

        Vector3 offsetOlhar = new Vector3(
            Mathf.Clamp(direcaoLocal.x, -1f, 1f) * deslocamentoPupila,
            Mathf.Clamp(direcaoLocal.y, -1f, 1f) * (deslocamentoPupila * 0.6f),
            0f
        );

        if (pupilaEsquerda != null)
        {
            pupilaEsquerda.localPosition = new Vector3(0f, 0f, 0.04f) + offsetOlhar;
        }

        if (pupilaDireita != null)
        {
            pupilaDireita.localPosition = new Vector3(0f, 0f, 0.04f) + offsetOlhar;
        }
    }

    /// <summary>
    /// Dispara uma reação de tremor visual quando o Pac-Man toca no fantasma.
    /// </summary>
    public void DispararSusto(float duracao = 0.4f)
    {
        m_TimerSusto = duracao;
    }

    private void ProcessarReacaoSusto()
    {
        if (m_TimerSusto > 0f)
        {
            m_TimerSusto -= Time.deltaTime;
            float tremorX = Random.Range(-0.04f, 0.04f);
            float tremorZ = Random.Range(-0.04f, 0.04f);
            transform.localPosition += new Vector3(tremorX, 0f, tremorZ);
        }
    }
}
