using System.Collections;
using UnityEngine;

/// <summary>
/// ============================================================================
/// PAC-MAN SEMÂNTICO (Semantic Pac-Man) - Efeitos Visuais e Sonoros (Game Juice)
/// ============================================================================
/// Gerenciador central de partículas, brilhos, vibrações de câmera (screen shake)
/// e feedbacks imersivos em RV/RA.
/// 
/// Funcionalidades:
/// 1. Explosão de partículas de brilho (Sparkles & Shimmer) na coleta de Pac-Dots/Gemas.
/// 2. Efeito de Stun/Alerta visual e tremor de impacto ao tocar em Fantasmas.
/// 3. Efeitos sonoros procedurais gerados em tempo de execução sem dependências.
/// ============================================================================
/// </summary>
public class GerenciadorEfeitosJuice : MonoBehaviour
{
    public static GerenciadorEfeitosJuice Instancia { get; private set; }

    [Header("Configuração de Partículas")]
    [Tooltip("Material para partículas de brilho (gerado automaticamente se nulo).")]
    public Material materialParticulaGlow;

    [Header("Impacto de Stun (Fantasmas)")]
    [Tooltip("Intensidade do tremor de câmera no impacto.")]
    public float forcaTremerCamera = 0.18f;

    [Tooltip("Duração do tremor de câmera em segundos.")]
    public float duracaoTremerCamera = 0.25f;

    private AudioSource m_AudioSource;
    private AudioClip m_SomColetaValida;
    private AudioClip m_SomAlertaFantasma;

    private void Awake()
    {
        if (Instancia != null && Instancia != this)
        {
            Destroy(gameObject);
            return;
        }
        Instancia = this;

        m_AudioSource = GetComponent<AudioSource>();
        if (m_AudioSource == null)
        {
            m_AudioSource = gameObject.AddComponent<AudioSource>();
            m_AudioSource.spatialBlend = 0.3f;
            m_AudioSource.volume = 0.6f;
        }

        GerarSonsProcedurais();
        PrepararMaterialParticulas();
    }

    private void OnEnable()
    {
        InstanciaSemanticaBase.OnInstanciaInteragida += AoInteragirComInstancia;
    }

    private void OnDisable()
    {
        InstanciaSemanticaBase.OnInstanciaInteragida -= AoInteragirComInstancia;
    }

    private void AoInteragirComInstancia(IInstanciaSemantica instancia, bool sucesso)
    {
        if (instancia == null) return;

        if (sucesso)
        {
            CriarExplosaoBrilhoColeta(instancia.Posicao, instancia.CorDestaque);
        }
        else
        {
            CriarEfeitoStunImpacto(instancia.Posicao);

            if (instancia is Component comp)
            {
                AnimacaoFantasma animFantasma = comp.GetComponentInChildren<AnimacaoFantasma>();
                if (animFantasma != null)
                {
                    animFantasma.DispararSusto(0.45f);
                }
            }
        }
    }

    /// <summary>
    /// Dispara uma explosão radiante de partículas douradas/esmeralda com rastro na coleta.
    /// </summary>
    public void CriarExplosaoBrilhoColeta(Vector3 posicao, Color cor)
    {
        GameObject burstObj = new GameObject("Burst_PacDot");
        burstObj.transform.position = posicao;

        // 1. Sistema de Partículas Cibernéticas (Feixe de Dados Holográfico)
        ParticleSystem ps = burstObj.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = false;
        main.duration = 0.55f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.30f, 0.65f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.8f, 4.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.18f);
        Color corCyber = cor != Color.clear ? new Color(cor.r, cor.g, cor.b, 0.9f) : new Color(0.0f, 0.90f, 1.0f, 0.9f);
        main.startColor = corCyber;
        main.gravityModifier = -0.45f; // Partículas de dados ascendem como telemetria espacial
        main.stopAction = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 24) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(0.35f, 0.35f, 0.35f);

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(corCyber, 0f), new GradientColorKey(new Color(0.0f, 0.95f, 1.0f), 0.4f), new GradientColorKey(corCyber, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) }
        );
        colorOverLifetime.color = grad;

        var renderer = burstObj.GetComponent<ParticleSystemRenderer>();
        if (materialParticulaGlow != null)
        {
            renderer.material = materialParticulaGlow;
        }

        // 2. Pulso de Luz Cibernético (Sem som de arcade!)
        GameObject luzObj = new GameObject("Luz_Flash_Cyber");
        luzObj.transform.position = posicao;
        Light luz = luzObj.AddComponent<Light>();
        luz.type = LightType.Point;
        luz.color = new Color(0.0f, 0.92f, 1.0f);
        luz.range = 4.0f;
        luz.intensity = 4.5f;
        Destroy(luzObj, 0.28f);

        ps.Play();
        Destroy(burstObj, 1.0f);
    }

    /// <summary>
    /// Dispara efeito de Stun / Alerta vermelho ao colidir com um Fantasma (Obstáculo).
    /// </summary>
    public void CriarEfeitoStunImpacto(Vector3 posicao)
    {
        // 1. Partículas de Alerta Elétrico
        GameObject stunObj = new GameObject("Impacto_Fantasma");
        stunObj.transform.position = posicao;

        ParticleSystem ps = stunObj.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = false;
        main.duration = 0.5f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
        main.startColor = new Color(1f, 0.1f, 0.25f);
        main.stopAction = ParticleSystemStopAction.Destroy;

        var rend = stunObj.GetComponent<ParticleSystemRenderer>();
        if (rend != null && materialParticulaGlow != null)
        {
            rend.material = materialParticulaGlow;
        }

        var emission = ps.emission;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 35) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Donut;
        shape.radius = 0.4f;

        ps.Play();
        Destroy(stunObj, 0.8f);

        // 2. Flash de Luz Vermelha de Exceção do Reasoner (Sem som de arcade!)
        GameObject luzObj = new GameObject("Luz_Alerta_Reasoner");
        luzObj.transform.position = posicao;
        Light luz = luzObj.AddComponent<Light>();
        luz.type = LightType.Point;
        luz.color = new Color(1f, 0.1f, 0.2f);
        luz.range = 4.5f;
        luz.intensity = 7.0f;
        Destroy(luzObj, 0.3f);

        // 3. Tremor de Câmera (Screen Shake)
        if (Camera.main != null)
        {
            StartCoroutine(ExecutarTremorCamera(Camera.main.transform, forcaTremerCamera, duracaoTremerCamera));
        }
    }

    private IEnumerator ExecutarTremorCamera(Transform cameraTransform, float forca, float duracao)
    {
        Vector3 posOriginal = cameraTransform.localPosition;
        float decorrido = 0f;

        while (decorrido < duracao)
        {
            float progresso = 1f - (decorrido / duracao);
            float offsetX = Random.Range(-1f, 1f) * forca * progresso;
            float offsetY = Random.Range(-1f, 1f) * forca * progresso;

            cameraTransform.localPosition = posOriginal + new Vector3(offsetX, offsetY, 0f);
            decorrido += Time.deltaTime;
            yield return null;
        }

        cameraTransform.localPosition = posOriginal;
    }

    private void PrepararMaterialParticulas()
    {
        if (materialParticulaGlow == null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default");
            materialParticulaGlow = new Material(sh);
        }
    }

    private void GerarSonsProcedurais()
    {
        // 1. Som de Gema/Pac-Dot Válida (Chime ascendente)
        int sampleRate = 44100;
        float duracao = 0.12f;
        int totalSamples = (int)(sampleRate * duracao);
        float[] samplesValidos = new float[totalSamples];

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / sampleRate;
            float freq = Mathf.Lerp(520f, 1040f, t / duracao);
            float env = Mathf.Sin(t / duracao * Mathf.PI);
            samplesValidos[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.5f;
        }
        m_SomColetaValida = AudioClip.Create("PacMan_ChimeValido", totalSamples, 1, sampleRate, false);
        m_SomColetaValida.SetData(samplesValidos, 0);

        // 2. Som de Alerta Fantasma (Buzzer descendente)
        float duracaoAlerta = 0.22f;
        int totalSamplesAlerta = (int)(sampleRate * duracaoAlerta);
        float[] samplesAlerta = new float[totalSamplesAlerta];

        for (int i = 0; i < totalSamplesAlerta; i++)
        {
            float t = (float)i / sampleRate;
            float freq = Mathf.Lerp(320f, 110f, t / duracaoAlerta);
            float env = 1f - (t / duracaoAlerta);
            float wave = Mathf.Sin(2f * Mathf.PI * freq * t) > 0f ? 1f : -1f; // Onda quadrada retrô
            samplesAlerta[i] = wave * env * 0.35f;
        }
        m_SomAlertaFantasma = AudioClip.Create("PacMan_AlertaFantasma", totalSamplesAlerta, 1, sampleRate, false);
        m_SomAlertaFantasma.SetData(samplesAlerta, 0);
    }
}
