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

        // 1. Sistema de Partículas Explosivas
        ParticleSystem ps = burstObj.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 0.6f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 5.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
        main.startColor = cor != Color.clear ? cor : new Color(1f, 0.9f, 0.2f);
        main.gravityModifier = -0.3f; // Partículas flutuam levemente para cima
        main.stopAction = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 28) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.15f;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(main.startColor.color, 0f), new GradientColorKey(Color.white, 0.3f), new GradientColorKey(main.startColor.color, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.5f), new GradientAlphaKey(0f, 1f) }
        );
        colorOverLifetime.color = grad;

        var renderer = burstObj.GetComponent<ParticleSystemRenderer>();
        if (materialParticulaGlow != null)
        {
            renderer.material = materialParticulaGlow;
        }

        // 2. Pulso de Luz
        GameObject luzObj = new GameObject("Luz_Flash");
        luzObj.transform.position = posicao;
        Light luz = luzObj.AddComponent<Light>();
        luz.type = LightType.Point;
        luz.color = main.startColor.color;
        luz.range = 3.5f;
        luz.intensity = 5.0f;
        Destroy(luzObj, 0.25f);

        // 3. Efeito Sonoro de Sucesso
        if (m_AudioSource != null && m_SomColetaValida != null)
        {
            m_AudioSource.pitch = Random.Range(1.0f, 1.25f);
            m_AudioSource.PlayOneShot(m_SomColetaValida, 0.7f);
        }

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
        var main = ps.main;
        main.duration = 0.5f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
        main.startColor = new Color(1f, 0.1f, 0.25f);
        main.stopAction = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 35) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Donut;
        shape.radius = 0.4f;

        ps.Play();
        Destroy(stunObj, 0.8f);

        // 2. Flash de Luz Vermelha de Alerta
        GameObject luzObj = new GameObject("Luz_Alerta");
        luzObj.transform.position = posicao;
        Light luz = luzObj.AddComponent<Light>();
        luz.type = LightType.Point;
        luz.color = new Color(1f, 0.1f, 0.2f);
        luz.range = 4.5f;
        luz.intensity = 7.0f;
        Destroy(luzObj, 0.3f);

        // 3. Som de Alerta/Perigo
        if (m_AudioSource != null && m_SomAlertaFantasma != null)
        {
            m_AudioSource.pitch = Random.Range(0.85f, 1.05f);
            m_AudioSource.PlayOneShot(m_SomAlertaFantasma, 0.9f);
        }

        // 4. Tremor de Câmera (Screen Shake)
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
