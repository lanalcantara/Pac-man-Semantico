using TMPro;
using UnityEngine;

/// <summary>
/// ============================================================================
/// INTERFACE ESPACIAL BIDIRECIONAL OWL - Placa de Sinalização Semântica (Wayfinding)
/// ============================================================================
/// Poste / Painel Holográfico posicionado nas bifurcações e entradas de corredores
/// do labirinto procedural para indicar explicitamente a hierarquia da TBox
/// (Superclasses e Subclasses OWL) de forma agnóstica de domínio.
/// ============================================================================
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class PlacaSinalizacaoSemantica : MonoBehaviour
{
    public const float AlturaCentroPainel = 1.28f;
    public const float LarguraPainelHolografico = 2.40f;
    public const float AlturaPainelHolografico = 0.68f;
    public const float EspessuraPainelHolografico = 0.06f;

    public const float LarguraRectTexto = 2.16f;
    public const float AlturaRectTexto = 0.54f;
    public const float OffsetZTexto = 0.042f;

    [Header("Identificação da Bifurcação e Hierarquia OWL")]
    public string idBifurcacao = "Bifurcacao_01";
    public string setaDirecao = "⬆";
    public string tipoHierarquia = "Superclasse";
    public string nomeClasseExibida = "Parasitas";

    [Header("Referências Visuais e TextMeshPro")]
    public TMP_Text textoFrontalTMP;
    public TMP_Text textoTraseiroTMP;
    public Renderer rendererPainelHolografico;
    public Renderer rendererMolduraNeon;
    public Renderer rendererPosteBase;

    // Aliases de compatibilidade direta com GerenciadorCenarioSemantico e GeradorPrefabsEditor
    public TMP_Text textoFrontal
    {
        get => textoFrontalTMP;
        set => textoFrontalTMP = value;
    }

    public TMP_Text textoTraseiro
    {
        get => textoTraseiroTMP;
        set => textoTraseiroTMP = value;
    }

    public Renderer rendererPainelFundo
    {
        get => rendererPainelHolografico;
        set => rendererPainelHolografico = value;
    }

    public Renderer[] renderersEmissivos;

    [Header("Estética Cibernética Dark Grid")]
    public Color corNeon = new Color(0.65f, 0.20f, 1.0f, 1.0f);
    [Range(0.5f, 2.0f)]
    public float intensidadeEmissaoHDR = 0.85f;

    private static TMP_FontAsset s_FontePadraoTMP;

    private void Awake()
    {
        SincronizarGeometriaEPainelPlaca();
        GarantirReferenciasTextMeshPro();
        GarantirTextoPadraoOffline();
        AtualizarVisualPlaca();
    }

    private void OnEnable()
    {
        SincronizarGeometriaEPainelPlaca();
        GarantirReferenciasTextMeshPro();
        GarantirTextoPadraoOffline();
        AtualizarVisualPlaca();
    }

    private void Start()
    {
        SincronizarGeometriaEPainelPlaca();
        GarantirReferenciasTextMeshPro();
        GarantirTextoPadraoOffline();
        AtualizarVisualPlaca();
    }

    /// <summary>
    /// Sobrecarga canônica exigida pela especificação:
    /// Inicializa a placa recebendo o nome da classe OWL e o tipo de relação/hierarquia
    /// (ex: ConfigurarPlaca("Parasitas", "Superclasse") ou ConfigurarPlaca("Nematoda", "Subclasse")).
    /// </summary>
    public void ConfigurarPlaca(string nomeClasse, string tipoRelacao = "Subclasse")
    {
        Color corAutomatica = DeterminarCorPorClasseOuRelacao(nomeClasse, tipoRelacao);
        string seta = !string.IsNullOrEmpty(setaDirecao) ? setaDirecao : "⬆";
        ConfigurarPlaca(seta, tipoRelacao, nomeClasse, corAutomatica);
    }

    /// <summary>
    /// Sobrecarga com cor customizada: ConfigurarPlaca("Nematoda", "Subclasse", corNeon).
    /// </summary>
    public void ConfigurarPlaca(string nomeClasse, string tipoRelacao, Color corCategoria)
    {
        string seta = !string.IsNullOrEmpty(setaDirecao) ? setaDirecao : "⬆";
        ConfigurarPlaca(seta, tipoRelacao, nomeClasse, corCategoria);
    }

    /// <summary>
    /// Atualiza dinamicamente o texto e o brilho da placa de sinalização
    /// a partir da taxonomia TBox recebida via WebSocket ou Grafo OWL local/offline.
    /// </summary>
    public void ConfigurarPlaca(string seta, string prefixoHierarquia, string nomeClasse, Color corCategoria)
    {
        setaDirecao = string.IsNullOrEmpty(seta) ? "⬆" : seta;
        tipoHierarquia = NormalizarTipoRelacao(prefixoHierarquia);
        nomeClasseExibida = string.IsNullOrEmpty(nomeClasse) ? ObterNomeClasseOfflinePadrao(tipoHierarquia) : nomeClasse;
        corNeon = corCategoria;

        SincronizarGeometriaEPainelPlaca();
        GarantirReferenciasTextMeshPro();
        AtualizarVisualPlaca();
    }

    private void GarantirTextoPadraoOffline()
    {
        if (string.IsNullOrEmpty(tipoHierarquia))
        {
            tipoHierarquia = "Superclasse";
        }
        if (string.IsNullOrEmpty(nomeClasseExibida))
        {
            nomeClasseExibida = ObterNomeClasseOfflinePadrao(tipoHierarquia);
        }
        if (string.IsNullOrEmpty(setaDirecao))
        {
            setaDirecao = "⬆";
        }
    }

    private static string NormalizarTipoRelacao(string prefixo)
    {
        if (string.IsNullOrEmpty(prefixo)) return "Superclasse";
        if (prefixo.IndexOf("Caminho", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            prefixo.IndexOf("Super", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "Superclasse";
        }
        if (prefixo.IndexOf("Sub", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "Subclasse";
        }
        return prefixo;
    }

    private string ObterNomeClasseOfflinePadrao(string relacao)
    {
        if (!string.IsNullOrEmpty(relacao) && relacao.IndexOf("Super", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "Parasitas";
        }
        return "Nematoda";
    }

    private Color DeterminarCorPorClasseOuRelacao(string nomeClasse, string tipoRelacao)
    {
        string combinado = $"{nomeClasse} {tipoRelacao}";
        if (combinado.IndexOf("Hosped", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            combinado.IndexOf("Host", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            combinado.IndexOf("Human", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            combinado.IndexOf("Intestine", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return new Color(0.05f, 0.55f, 1.0f, 1.0f); // Azul Neon
        }
        return new Color(0.68f, 0.20f, 1.0f, 1.0f); // Roxo Neon
    }

    /// <summary>
    /// Garante que o pedestal, o poste de suporte, o painel holográfico e a moldura superior
    /// estejam perfeitamente alinhados e dimensionados (inclusive em instâncias vindas de prefabs antigos).
    /// </summary>
    private void SincronizarGeometriaEPainelPlaca()
    {
        Transform trBase = transform.Find("Base_Pedestal");
        if (trBase != null)
        {
            trBase.localPosition = new Vector3(0f, 0.04f, 0f);
            trBase.localScale = new Vector3(0.45f, 0.04f, 0.45f);
        }

        Transform trPoste = transform.Find("Poste_Suporte");
        if (trPoste != null)
        {
            trPoste.localPosition = new Vector3(0f, 0.56f, 0f);
            trPoste.localScale = new Vector3(0.08f, 0.56f, 0.08f);
        }

        Transform trPainel = transform.Find("Painel_Holografico");
        if (trPainel != null)
        {
            trPainel.localPosition = new Vector3(0f, AlturaCentroPainel, 0f);
            trPainel.localRotation = Quaternion.identity;
            trPainel.localScale = new Vector3(LarguraPainelHolografico, AlturaPainelHolografico, EspessuraPainelHolografico);
            if (rendererPainelHolografico == null)
            {
                rendererPainelHolografico = trPainel.GetComponent<Renderer>();
            }
        }

        Transform trMoldura = transform.Find("Moldura_Neon_HDR");
        if (trMoldura != null)
        {
            float topoPainel = AlturaCentroPainel + (AlturaPainelHolografico * 0.5f) + 0.02f;
            trMoldura.localPosition = new Vector3(0f, topoPainel, 0f);
            trMoldura.localRotation = Quaternion.identity;
            trMoldura.localScale = new Vector3(LarguraPainelHolografico + 0.06f, 0.05f, EspessuraPainelHolografico + 0.02f);
            if (rendererMolduraNeon == null)
            {
                rendererMolduraNeon = trMoldura.GetComponent<Renderer>();
            }
        }
    }

    private void GarantirReferenciasTextMeshPro()
    {
        if (textoFrontalTMP == null)
        {
            Transform trFrente = transform.Find("TMP_Texto_Frontal");
            if (trFrente != null) textoFrontalTMP = trFrente.GetComponent<TMP_Text>();
        }
        if (textoTraseiroTMP == null)
        {
            Transform trVerso = transform.Find("TMP_Texto_Traseiro");
            if (trVerso != null) textoTraseiroTMP = trVerso.GetComponent<TMP_Text>();
        }

        if (textoFrontalTMP == null || textoTraseiroTMP == null)
        {
            TMP_Text[] textos = GetComponentsInChildren<TMP_Text>(true);
            if (textos != null && textos.Length > 0)
            {
                if (textoFrontalTMP == null) textoFrontalTMP = textos[0];
                if (textos.Length > 1 && textoTraseiroTMP == null) textoTraseiroTMP = textos[1];
            }
        }

        // Se ainda não houver componente TextMeshPro filho, cria automaticamente na frente e no verso da placa
        if (textoFrontalTMP == null)
        {
            GameObject txtFrenteObj = new GameObject("TMP_Texto_Frontal", typeof(RectTransform));
            txtFrenteObj.transform.SetParent(transform, false);
            textoFrontalTMP = txtFrenteObj.AddComponent<TextMeshPro>();
        }

        if (textoTraseiroTMP == null)
        {
            GameObject txtVersoObj = new GameObject("TMP_Texto_Traseiro", typeof(RectTransform));
            txtVersoObj.transform.SetParent(transform, false);
            textoTraseiroTMP = txtVersoObj.AddComponent<TextMeshPro>();
        }

        CentralizarRectTransformNoPainel(textoFrontalTMP, ehFaceTraseira: false);
        CentralizarRectTransformNoPainel(textoTraseiroTMP, ehFaceTraseira: true);
    }

    /// <summary>
    /// Força o RectTransform do TextMeshPro a ficar 100% centralizado no eixo (X=0, Y=AlturaCentroPainel)
    /// e contido dentro das bordas do Painel_Holografico, corrigindo qualquer desalinhamento do Prefab.
    /// </summary>
    public static void CentralizarRectTransformNoPainel(TMP_Text tmp, bool ehFaceTraseira)
    {
        if (tmp == null) return;

        RectTransform rt = tmp.rectTransform;
        if (rt == null) return;

        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(LarguraRectTexto, AlturaRectTexto);
        rt.localScale = Vector3.one;
        rt.localRotation = ehFaceTraseira ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;

        Vector3 posCentralizada = new Vector3(0f, AlturaCentroPainel, ehFaceTraseira ? OffsetZTexto : -OffsetZTexto);
        rt.localPosition = posCentralizada;
        rt.anchoredPosition3D = posCentralizada;
    }

    public void AtualizarVisualPlaca()
    {
        SincronizarGeometriaEPainelPlaca();
        CentralizarRectTransformNoPainel(textoFrontalTMP, ehFaceTraseira: false);
        CentralizarRectTransformNoPainel(textoTraseiroTMP, ehFaceTraseira: true);

        // Layout em 2 linhas centralizadas: linha superior com a direção/hierarquia e linha inferior com a Classe OWL em destaque
        // Isso impede que nomes como 'Nematoda' ou 'Platyhelminthes' escapem pelas laterais ou por baixo do painel!
        string conteudoFormatado = $"<size=74%>{setaDirecao} {tipoHierarquia}:</size>\n<b>{nomeClasseExibida}</b>";

        AplicarTextoBrancoContornoPretoTMP(textoFrontalTMP, conteudoFormatado);
        AplicarTextoBrancoContornoPretoTMP(textoTraseiroTMP, conteudoFormatado);
        AplicarMateriaisEmissivosHDR(corNeon);
    }

    private static TMP_FontAsset ObterFontePadraoTMP()
    {
        if (s_FontePadraoTMP != null) return s_FontePadraoTMP;

        if (TMP_Settings.defaultFontAsset != null)
        {
            s_FontePadraoTMP = TMP_Settings.defaultFontAsset;
            return s_FontePadraoTMP;
        }

        s_FontePadraoTMP = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF")
                           ?? Resources.Load<TMP_FontAsset>("LiberationSans SDF");
        return s_FontePadraoTMP;
    }

    private void AplicarTextoBrancoContornoPretoTMP(TMP_Text tmp, string texto)
    {
        if (tmp == null) return;

        TMP_FontAsset fonte = ObterFontePadraoTMP();
        if (tmp.font == null && fonte != null)
        {
            tmp.font = fonte;
        }

        tmp.text = texto;
        tmp.richText = true;

        // Auto-Sizing ativo garante que qualquer nome de classe caiba 100% dentro do painel holográfico sem ultrapassar as bordas
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 0.65f;
        tmp.fontSizeMax = 1.50f;
        tmp.fontSize = 1.45f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.lineSpacing = -6f;
        tmp.margin = new Vector4(0.08f, 0.04f, 0.08f, 0.04f);
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Ellipsis;

        // Branco Puro (#FFFFFF) com Contorno Preto (#000000)
        tmp.color = Color.white;
        tmp.outlineColor = new Color32(0, 0, 0, 255);
        tmp.outlineWidth = 0.22f;

        if (tmp.fontSharedMaterial != null)
        {
            Material matTexto = new Material(tmp.fontSharedMaterial);
            if (matTexto.HasProperty("_FaceColor"))
            {
                matTexto.SetColor("_FaceColor", Color.white);
            }
            if (matTexto.HasProperty("_OutlineColor"))
            {
                matTexto.SetColor("_OutlineColor", Color.black);
            }
            if (matTexto.HasProperty("_OutlineWidth"))
            {
                matTexto.SetFloat("_OutlineWidth", 0.22f);
            }
            tmp.fontMaterial = matTexto;
        }

        MeshRenderer mr = tmp.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.sortingOrder = 10;
        }

        tmp.ForceMeshUpdate();
    }

    private void AplicarMateriaisEmissivosHDR(Color corBase)
    {
        Shader shaderLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Diffuse");
        Color corEmissaoHDR = corBase * intensidadeEmissaoHDR;

        if (rendererMolduraNeon != null && shaderLit != null)
        {
            Material matMoldura = new Material(shaderLit);
            matMoldura.name = $"Mat_MolduraWayfinding_{nomeClasseExibida}";
            if (matMoldura.HasProperty("_BaseColor")) matMoldura.SetColor("_BaseColor", corBase);
            if (matMoldura.HasProperty("_Color")) matMoldura.SetColor("_Color", corBase);
            if (matMoldura.HasProperty("_EmissionColor"))
            {
                matMoldura.EnableKeyword("_EMISSION");
                matMoldura.SetColor("_EmissionColor", corEmissaoHDR);
            }
            rendererMolduraNeon.sharedMaterial = matMoldura;
        }

        if (renderersEmissivos != null && shaderLit != null)
        {
            Material matEmissivo = new Material(shaderLit);
            matEmissivo.name = $"Mat_EmissivoWayfinding_{nomeClasseExibida}";
            if (matEmissivo.HasProperty("_BaseColor")) matEmissivo.SetColor("_BaseColor", corBase);
            if (matEmissivo.HasProperty("_Color")) matEmissivo.SetColor("_Color", corBase);
            if (matEmissivo.HasProperty("_EmissionColor"))
            {
                matEmissivo.EnableKeyword("_EMISSION");
                matEmissivo.SetColor("_EmissionColor", corEmissaoHDR);
            }
            for (int i = 0; i < renderersEmissivos.Length; i++)
            {
                if (renderersEmissivos[i] != null)
                {
                    renderersEmissivos[i].sharedMaterial = matEmissivo;
                }
            }
        }

        // Fundo do painel holográfico: PRETO PROFUNDO SEM EMISSÃO para que o texto Branco Puro com contorno preto fique 100% legível!
        if (rendererPainelHolografico != null && shaderLit != null)
        {
            Material matPainel = new Material(shaderLit);
            matPainel.name = $"Mat_PainelWayfinding_Preto_{nomeClasseExibida}";
            Color corFundoPreto = new Color(0.01f, 0.015f, 0.03f, 1.0f);
            if (matPainel.HasProperty("_BaseColor")) matPainel.SetColor("_BaseColor", corFundoPreto);
            if (matPainel.HasProperty("_Color")) matPainel.SetColor("_Color", corFundoPreto);
            if (matPainel.HasProperty("_Smoothness")) matPainel.SetFloat("_Smoothness", 0.0f);
            if (matPainel.HasProperty("_SpecularHighlights")) matPainel.SetFloat("_SpecularHighlights", 0.0f);
            if (matPainel.HasProperty("_EnvironmentReflections")) matPainel.SetFloat("_EnvironmentReflections", 0.0f);
            matPainel.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            matPainel.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            rendererPainelHolografico.sharedMaterial = matPainel;
        }
    }
}
