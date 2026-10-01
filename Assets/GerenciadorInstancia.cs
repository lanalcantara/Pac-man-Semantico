using System;
using UnityEngine;

/// <summary>
/// ============================================================================
/// PAC-MAN SEMÂNTICO (Semantic Pac-Man) - Modelagem Orientada a Objetos (UML/C++)
/// ============================================================================
/// Implementação concreta e retrocompatível de InstanciaSemanticaBase.
/// Atua como fachada e manipulador versátil para Prefabs e instâncias dinâmicas
/// no Unity, com suporte integral a Description Logic (DL) e interação XR.
/// ============================================================================
/// </summary>
[SelectionBase]
public class GerenciadorInstancia : InstanciaSemanticaBase
{
    [Header("Configurações Semânticas")]
    [Tooltip("Define se este objeto cumpre ou não a regra lógica/restrição da ontologia.")]
    public bool ehValida = true;

    [Tooltip("Pontos concedidos ao coletar uma instância válida (ou penalidade se inválida).")]
    public int valorPontuacao = 10;

    [Tooltip("Nome da classe ontológica a qual esta instância pertence.")]
    public string classeOntologica = "InstanciaGenerica";

    [Tooltip("Fórmula ou axioma de Description Logic avaliado.")]
    public string expressaoDL = "Classe ⊑ Valida";

    [Tooltip("Descrição semântica para exibição em hologramas/HUD espacial.")]
    [TextArea(2, 4)]
    public string descricaoSemantica = "Instância ontológica gerada dinamicamente no Pac-Man Semântico.";

    [Tooltip("Tempo de intervalo antes de re-acionar alerta para obstáculos.")]
    public float tempoCooldownPenalidade = 1.0f;

    private float m_TimerCooldown = 0f;

    // Dados estruturados completos da ontologia
    [NonSerialized]
    public DadoInstanciaSemantica dadosCompletos;

    protected override void Awake()
    {
        base.Awake();
        SincronizarCamposPublicosComBase();
    }

    private void Update()
    {
        if (m_TimerCooldown > 0f)
        {
            m_TimerCooldown -= Time.deltaTime;
        }
    }

    private void SincronizarCamposPublicosComBase()
    {
        m_Id = string.IsNullOrEmpty(m_Id) ? Guid.NewGuid().ToString().Substring(0, 8) : m_Id;
        m_NomeClasse = classeOntologica;
        m_EhValida = ehValida;
        m_ExpressaoDL = expressaoDL;
        m_DescricaoSemantica = descricaoSemantica;
        m_ValorPontuacao = valorPontuacao;
    }

    /// <summary>
    /// Configura a instância a partir dos dados do Backend C++ ou JSON.
    /// </summary>
    public void ConfigurarComDados(DadoInstanciaSemantica dados)
    {
        dadosCompletos = dados;
        if (dados != null)
        {
            ehValida = dados.ehValida;
            valorPontuacao = dados.valorPontuacao;
            classeOntologica = dados.classeOntologica;
            expressaoDL = dados.expressaoDL;
            descricaoSemantica = dados.descricaoSemantica;

            InicializarSemantica(dados);
        }
    }

    public override void InicializarSemantica(DadoInstanciaSemantica dados)
    {
        base.InicializarSemantica(dados);
        if (dados != null)
        {
            ehValida = dados.ehValida;
            valorPontuacao = dados.valorPontuacao;
            classeOntologica = dados.classeOntologica;
            expressaoDL = dados.expressaoDL;
            descricaoSemantica = dados.descricaoSemantica;
        }
    }

    /// <summary>
    /// Executa a interação espacial (chamada tanto por proximidade física quanto por laser raycast XR).
    /// </summary>
    public void InteragirEspacialmente(GameObject interator, bool ehRaycast = false)
    {
        Interagir(interator, ehRaycast);
    }

    /// <summary>
    /// Destaca visualmente a gema quando apontada pelo feixe Laser XR.
    /// </summary>
    public void DefinirFocoRaycast(bool focado)
    {
        DefinirFocoVisual(focado);
    }

    protected override void AoExecutarColeta(GameObject interator, bool ehRaycast)
    {
        m_Coletada = true;
        totalColetadas++;
        pontuacaoLogica += valorPontuacao;
        instanciasValidasRestantes = Mathf.Max(0, instanciasValidasRestantes - 1);

        Debug.Log($"<color=#00FF66><b>[Pac-Man Semântico - GEMA VÁLIDA]</b></color> '{classeOntologica}' ({m_Id}) coletada com sucesso! " +
                  $"Axioma DL: [{expressaoDL}]. +{valorPontuacao} pts. Total: <b>{pontuacaoLogica}</b> " +
                  $"[Método: {(ehRaycast ? "Raycast Espacial" : "Toque Proximidade XR")}]");

        EmitirFeedbackHaptico(interator, 0.45f, 0.15f);
        NotificarInteracao(sucesso: true);
        CriarEfeitoVisual(sucesso: true);

        Destroy(gameObject, 0.05f);
    }

    protected override void AoDetectarViolacao(GameObject interator, bool ehRaycast)
    {
        if (m_TimerCooldown > 0f) return;
        m_TimerCooldown = tempoCooldownPenalidade;

        pontuacaoLogica += valorPontuacao;

        Debug.LogWarning($"<color=#FF3344><b>[Pac-Man Semântico - OBSTÁCULO INVÁLIDO]</b></color> '{classeOntologica}' ({m_Id}) detectado! " +
                         $"Violação DL: [{expressaoDL}]. Penalidade: {valorPontuacao} pts. Total: <b>{pontuacaoLogica}</b>.");

        EmitirFeedbackHaptico(interator, 0.85f, 0.35f);
        NotificarInteracao(sucesso: false);
        CriarEfeitoVisual(sucesso: false);
    }
}
