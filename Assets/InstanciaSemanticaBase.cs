using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ============================================================================
/// PAC-MAN SEMÂNTICO (Semantic Pac-Man) - Modelagem Orientada a Objetos (UML/C++)
/// ============================================================================
/// Classe base abstrata para todas as instâncias ontológicas no Pac-Man Semântico.
/// Implementa IInstanciaSemantica e gerencia colisões XR, propriedades DL,
/// feedbacks visuais/hápticos e sincronização com a pontuação lógica global.
/// ============================================================================
/// </summary>
[SelectionBase]
public abstract class InstanciaSemanticaBase : MonoBehaviour, IInstanciaSemantica
{
    [Header("Identidade Ontológica (Description Logic)")]
    [SerializeField] protected string m_Id = "inst_001";
    [SerializeField] protected string m_NomeClasse = "InstanciaBase";
    [SerializeField] protected bool m_EhValida = true;
    [SerializeField] protected string m_ExpressaoDL = "Classe ⊑ Valida";
    [SerializeField, TextArea(2, 4)] protected string m_DescricaoSemantica = "Descrição ontológica.";
    [SerializeField] protected int m_ValorPontuacao = 10;
    [SerializeField] protected Color m_CorDestaque = Color.green;

    [Header("Efeitos Visuais e Interativos")]
    [SerializeField] protected Color m_CorEmissaoBase = Color.clear;

    // Dicionário de propriedades ontológicas (Object/Data Properties)
    protected readonly Dictionary<string, string> m_PropriedadesDL = new Dictionary<string, string>();

    // Cache e estado
    protected Renderer m_Renderer;
    protected Material m_MaterialInstanciado;
    protected bool m_Coletada = false;
    protected bool m_EstaFocada = false;

    // Pontuação Lógica Global do Pac-Man Semântico
    public static int pontuacaoLogica = 0;
    public static int instanciasValidasRestantes = 0;
    public static int totalColetadas = 0;

    // Eventos estáticos de ciclo de vida semântico
    public static event Action<IInstanciaSemantica, bool> OnInstanciaInteragida;
    public static event Action<IInstanciaSemantica> OnInstanciaFocada;
    public static event Action<IInstanciaSemantica> OnInstanciaDesfocada;

    // =========================================================================
    // PROPRIEDADES DA INTERFACE IInstanciaSemantica
    // =========================================================================
    public string Id
    {
        get => m_Id;
        set => m_Id = value;
    }

    public string NomeClasse
    {
        get => m_NomeClasse;
        set => m_NomeClasse = value;
    }

    public bool EhValida
    {
        get => m_EhValida;
        set => m_EhValida = value;
    }

    public string ExpressaoDL
    {
        get => m_ExpressaoDL;
        set => m_ExpressaoDL = value;
    }

    public string DescricaoSemantica
    {
        get => m_DescricaoSemantica;
        set => m_DescricaoSemantica = value;
    }

    public int ValorPontuacao
    {
        get => m_ValorPontuacao;
        set => m_ValorPontuacao = value;
    }

    public Vector3 Posicao
    {
        get => transform.position;
        set => transform.position = value;
    }

    public Color CorDestaque
    {
        get => m_CorDestaque;
        set => m_CorDestaque = value;
    }

    public IReadOnlyDictionary<string, string> PropriedadesDL => m_PropriedadesDL;

    // =========================================================================
    // CICLO DE VIDA DO UNITY
    // =========================================================================
    protected virtual void Awake()
    {
        m_Renderer = GetComponent<Renderer>() ?? GetComponentInChildren<Renderer>();
        if (m_Renderer != null)
        {
            m_MaterialInstanciado = m_Renderer.material;
            if (m_MaterialInstanciado.HasProperty("_EmissionColor"))
            {
                m_CorEmissaoBase = m_MaterialInstanciado.GetColor("_EmissionColor");
            }
        }
    }

    protected virtual void Start()
    {
        if (m_EhValida)
        {
            instanciasValidasRestantes++;
        }
    }

    protected virtual void OnDestroy()
    {
        if (m_EhValida && !m_Coletada)
        {
            instanciasValidasRestantes = Mathf.Max(0, instanciasValidasRestantes - 1);
        }
    }

    // =========================================================================
    // MÉTODOS DA INTERFACE IInstanciaSemantica
    // =========================================================================
    public virtual void InicializarSemantica(DadoInstanciaSemantica dados)
    {
        if (dados == null) return;

        m_Id = dados.id;
        m_NomeClasse = dados.classeOntologica;
        m_EhValida = dados.ehValida;
        m_ExpressaoDL = dados.expressaoDL;
        m_DescricaoSemantica = dados.descricaoSemantica;
        m_ValorPontuacao = dados.valorPontuacao;
        m_CorDestaque = dados.corDestaque;

        m_PropriedadesDL.Clear();
        if (dados.propriedades != null)
        {
            foreach (var prop in dados.propriedades)
            {
                if (!string.IsNullOrEmpty(prop.chave))
                {
                    m_PropriedadesDL[prop.chave] = prop.valor;
                }
            }
        }

        AplicarCoresNoMaterial();
    }

    public virtual void Interagir(GameObject interator, bool ehRaycast = false)
    {
        if (m_Coletada) return;

        if (m_EhValida)
        {
            AoExecutarColeta(interator, ehRaycast);
        }
        else
        {
            AoDetectarViolacao(interator, ehRaycast);
        }
    }

    public virtual void DefinirFocoVisual(bool focado)
    {
        if (m_EstaFocada == focado || m_Coletada) return;
        m_EstaFocada = focado;

        if (m_MaterialInstanciado != null && m_MaterialInstanciado.HasProperty("_EmissionColor"))
        {
            if (focado)
            {
                Color corFoco = m_EhValida ? Color.cyan * 1.3f : Color.magenta * 1.3f;
                m_MaterialInstanciado.SetColor("_EmissionColor", corFoco);
                m_MaterialInstanciado.EnableKeyword("_EMISSION");
                OnInstanciaFocada?.Invoke(this);
            }
            else
            {
                m_MaterialInstanciado.SetColor("_EmissionColor", m_CorEmissaoBase);
                OnInstanciaDesfocada?.Invoke(this);
            }
        }
    }

    // =========================================================================
    // MÉTODOS ABSTRATOS POLIMÓRFICOS
    // =========================================================================
    protected abstract void AoExecutarColeta(GameObject interator, bool ehRaycast);
    protected abstract void AoDetectarViolacao(GameObject interator, bool ehRaycast);

    // =========================================================================
    // DETECÇÃO DE COLISÃO XR UNIVERSAL
    // =========================================================================
    protected virtual void OnTriggerEnter(Collider other)
    {
        if (m_Coletada) return;

        if (EhColisorDoJogador(other))
        {
            Interagir(other.gameObject, ehRaycast: false);
        }
    }

    public static bool EhColisorDoJogador(Collider col)
    {
        if (col == null) return false;

        if (col.CompareTag("Player") || col.CompareTag("MainCamera") || col.CompareTag("GameController"))
            return true;

        string nome = col.gameObject.name.ToLower();
        if (nome.Contains("player") || nome.Contains("pacman") || nome.Contains("pac-man") ||
            nome.Contains("sphere") || nome.Contains("hand") || nome.Contains("controller") ||
            nome.Contains("camera") || nome.Contains("origin"))
            return true;

        if (col.GetComponentInParent<ControladorXRJogador>() != null ||
            col.GetComponentInParent<InteracaoEspacialXR>() != null ||
            col.GetComponentInParent<MovimentoComeCome>() != null)
        {
            return true;
        }

        return false;
    }

    protected void NotificarInteracao(bool sucesso)
    {
        OnInstanciaInteragida?.Invoke(this, sucesso);
    }

    protected void EmitirFeedbackHaptico(GameObject interator, float amplitude, float duracao)
    {
        if (interator == null) return;
        InteracaoEspacialXR interacaoXR = interator.GetComponentInParent<InteracaoEspacialXR>();
        if (interacaoXR != null)
        {
            interacaoXR.DispararVibracaoHaptica(amplitude, duracao);
        }
    }

    protected void CriarEfeitoVisual(bool sucesso)
    {
        GameObject efeitoObj = new GameObject($"Efeito_DL_{m_NomeClasse}_{m_Id}");
        efeitoObj.transform.position = transform.position;

        Light luz = efeitoObj.AddComponent<Light>();
        luz.type = LightType.Point;
        luz.range = 3.5f;
        luz.intensity = sucesso ? 4.5f : 6.0f;
        luz.color = sucesso ? new Color(0.2f, 1f, 0.5f) : new Color(1f, 0.2f, 0.2f);

        Destroy(efeitoObj, 0.35f);
    }

    protected virtual void AplicarCoresNoMaterial()
    {
        if (m_MaterialInstanciado != null && m_CorDestaque != Color.clear)
        {
            if (m_MaterialInstanciado.HasProperty("_BaseColor"))
                m_MaterialInstanciado.SetColor("_BaseColor", m_CorDestaque);
            if (m_MaterialInstanciado.HasProperty("_Color"))
                m_MaterialInstanciado.SetColor("_Color", m_CorDestaque);
            if (m_MaterialInstanciado.HasProperty("_EmissionColor"))
            {
                m_MaterialInstanciado.EnableKeyword("_EMISSION");
                m_MaterialInstanciado.SetColor("_EmissionColor", m_CorDestaque * 0.7f);
                m_CorEmissaoBase = m_CorDestaque * 0.7f;
            }
        }
    }
}
