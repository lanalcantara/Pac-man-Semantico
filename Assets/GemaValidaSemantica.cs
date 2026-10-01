using UnityEngine;

/// <summary>
/// ============================================================================
/// PAC-MAN SEMÂNTICO (Semantic Pac-Man) - Modelagem Orientada a Objetos (UML/C++)
/// ============================================================================
/// Classe concreta que representa uma Gema Válida (Indivíduo Ontológico Conforme).
/// Satisfaz as regras e restrições de Description Logic (DL) na TBox/ABox.
/// Concede pontos positivos ao Pac-Man ao ser coletada e é destruída do labirinto.
/// ============================================================================
/// </summary>
public class GemaValidaSemantica : InstanciaSemanticaBase
{
    [Header("Configurações da Gema")]
    [Tooltip("Intensidade do brilho emissivo em RV/RA.")]
    public float intensidadeBrilho = 1.2f;

    protected override void Awake()
    {
        base.Awake();
        m_EhValida = true;
        m_ValorPontuacao = 10;
        if (m_CorDestaque == Color.green || m_CorDestaque == Color.clear)
        {
            m_CorDestaque = new Color(0.1f, 1f, 0.45f);
        }
    }

    protected override void AoExecutarColeta(GameObject interator, bool ehRaycast)
    {
        m_Coletada = true;
        totalColetadas++;
        pontuacaoLogica += m_ValorPontuacao;
        instanciasValidasRestantes = Mathf.Max(0, instanciasValidasRestantes - 1);

        Debug.Log($"<color=#00FF66><b>[Pac-Man Semântico - GEMA VÁLIDA]</b></color> '{m_NomeClasse}' ({m_Id}) coletada com sucesso! " +
                  $"Axioma DL: [{m_ExpressaoDL}]. +{m_ValorPontuacao} pts. Total: <b>{pontuacaoLogica}</b> " +
                  $"[Método: {(ehRaycast ? "Raycast Espacial" : "Toque Proximidade XR")}]");

        EmitirFeedbackHaptico(interator, 0.45f, 0.15f);
        NotificarInteracao(sucesso: true);
        CriarEfeitoVisual(sucesso: true);

        Destroy(gameObject, 0.05f);
    }

    protected override void AoDetectarViolacao(GameObject interator, bool ehRaycast)
    {
        // Gemas válidas não disparam violação
    }
}
