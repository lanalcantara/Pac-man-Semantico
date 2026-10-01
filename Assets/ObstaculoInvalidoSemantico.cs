using UnityEngine;

/// <summary>
/// ============================================================================
/// PAC-MAN SEMÂNTICO (Semantic Pac-Man) - Modelagem Orientada a Objetos (UML/C++)
/// ============================================================================
/// Classe concreta que representa um Obstáculo Inválido (Violação Ontológica).
/// Representa indivíduos ou axiomas que violam restrições de Description Logic.
/// Penaliza a pontuação do Pac-Man e atua como barreira/perigo no labirinto.
/// ============================================================================
/// </summary>
public class ObstaculoInvalidoSemantico : InstanciaSemanticaBase
{
    [Header("Configurações do Obstáculo")]
    [Tooltip("Tempo de intervalo antes de disparar nova penalidade caso o jogador permaneça em contato.")]
    public float tempoCooldownPenalidade = 1.0f;

    private float m_TimerCooldown = 0f;

    protected override void Awake()
    {
        base.Awake();
        m_EhValida = false;
        m_ValorPontuacao = -5;
        if (m_CorDestaque == Color.green || m_CorDestaque == Color.clear)
        {
            m_CorDestaque = new Color(1f, 0.15f, 0.2f);
        }
    }

    private void Update()
    {
        if (m_TimerCooldown > 0f)
        {
            m_TimerCooldown -= Time.deltaTime;
        }
    }

    protected override void AoExecutarColeta(GameObject interator, bool ehRaycast)
    {
        // Obstáculos inválidos não são coletados como gemas
    }

    protected override void AoDetectarViolacao(GameObject interator, bool ehRaycast)
    {
        if (m_TimerCooldown > 0f) return;
        m_TimerCooldown = tempoCooldownPenalidade;

        pontuacaoLogica += m_ValorPontuacao;

        Debug.LogWarning($"<color=#FF3344><b>[Pac-Man Semântico - OBSTÁCULO INVÁLIDO]</b></color> '{m_NomeClasse}' ({m_Id}) detectado! " +
                         $"Violação DL: [{m_ExpressaoDL}]. Penalidade: {m_ValorPontuacao} pts. Total: <b>{pontuacaoLogica}</b>.");

        EmitirFeedbackHaptico(interator, 0.85f, 0.35f);
        NotificarInteracao(sucesso: false);
        CriarEfeitoVisual(sucesso: false);
    }
}
