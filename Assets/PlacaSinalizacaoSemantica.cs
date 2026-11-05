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
[DisallowMultipleComponent]
public class PlacaSinalizacaoSemantica : MonoBehaviour
{
    [Header("Identificação da Bifurcação")]
    public string idBifurcacao = "Bifurcacao_01";
    public string setaDirecao = "⬆";
    public string tipoHierarquia = "Caminho";
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

    [Header("Estética Cibernética HDR (Quest 3 Bloom)")]
    public Color corNeon = new Color(0.65f, 0.20f, 1.0f, 1.0f);
    [Range(1.0f, 3.0f)]
    public float intensidadeEmissaoHDR = 1.45f;

    /// <summary>
    /// Atualiza dinamicamente o texto e o brilho emissivo HDR da placa de sinalização
    /// a partir da taxonomia TBox recebida via WebSocket ou Grafo OWL.
    /// </summary>
    public void ConfigurarPlaca(string seta, string prefixoHierarquia, string nomeClasse, Color corCategoria)
    {
        setaDirecao = string.IsNullOrEmpty(seta) ? "⬆" : seta;
        tipoHierarquia = string.IsNullOrEmpty(prefixoHierarquia) ? "Caminho" : prefixoHierarquia;
        nomeClasseExibida = string.IsNullOrEmpty(nomeClasse) ? "owl:Thing" : nomeClasse;
        corNeon = corCategoria;

        string hexCor = ColorUtility.ToHtmlStringRGB(Color.Lerp(corCategoria, Color.white, 0.35f));
        string conteudoFormatado = $"{setaDirecao} <color=#94A3B8>{tipoHierarquia}:</color>\n<color=#{hexCor}><b>{nomeClasseExibida}</b></color>";

        if (textoFrontalTMP == null || textoTraseiroTMP == null)
        {
            TMP_Text[] textos = GetComponentsInChildren<TMP_Text>(true);
            if (textos != null && textos.Length > 0)
            {
                textoFrontalTMP = textos[0];
                if (textos.Length > 1) textoTraseiroTMP = textos[1];
            }
        }

        AplicarTextoEmissivoTMP(textoFrontalTMP, conteudoFormatado, corCategoria);
        AplicarTextoEmissivoTMP(textoTraseiroTMP, conteudoFormatado, corCategoria);
        AplicarMateriaisEmissivosHDR(corCategoria);
    }

    private void AplicarTextoEmissivoTMP(TMP_Text tmp, string texto, Color corBase)
    {
        if (tmp == null) return;

        tmp.text = texto;
        tmp.richText = true;
        tmp.alignment = TextAlignmentOptions.Center;

        // Brilho sutil e legível para o texto 3D sem borrar as letras no Bloom
        Color corBrilhoTexto = Color.Lerp(corBase, Color.white, 0.55f) * 1.15f;
        corBrilhoTexto.a = 1.0f;
        tmp.color = corBrilhoTexto;

        if (tmp.fontSharedMaterial != null)
        {
            Material matTexto = new Material(tmp.fontSharedMaterial);
            if (matTexto.HasProperty("_FaceColor"))
            {
                matTexto.SetColor("_FaceColor", corBrilhoTexto);
            }
            if (matTexto.HasProperty("_OutlineColor"))
            {
                matTexto.SetColor("_OutlineColor", corBase * intensidadeEmissaoHDR);
            }
            if (matTexto.HasProperty("_OutlineWidth"))
            {
                matTexto.SetFloat("_OutlineWidth", 0.14f);
            }
            tmp.fontMaterial = matTexto;
        }
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
            rendererMolduraNeon.material = matMoldura;
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
                    renderersEmissivos[i].material = matEmissivo;
                }
            }
        }

        if (rendererPainelHolografico != null && shaderLit != null)
        {
            Material matPainel = new Material(shaderLit);
            matPainel.name = $"Mat_PainelWayfinding_{nomeClasseExibida}";
            Color corFundoEscuro = new Color(0.02f, 0.04f, 0.09f, 0.94f);
            if (matPainel.HasProperty("_BaseColor")) matPainel.SetColor("_BaseColor", corFundoEscuro);
            if (matPainel.HasProperty("_Color")) matPainel.SetColor("_Color", corFundoEscuro);
            if (matPainel.HasProperty("_Smoothness")) matPainel.SetFloat("_Smoothness", 0.75f);
            if (matPainel.HasProperty("_EmissionColor"))
            {
                matPainel.EnableKeyword("_EMISSION");
                matPainel.SetColor("_EmissionColor", corBase * 0.45f);
            }
            rendererPainelHolografico.material = matPainel;
        }
    }
}

