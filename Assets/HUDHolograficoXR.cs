using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ============================================================================
/// PAC-MAN SEMÂNTICO (Semantic Pac-Man) - Modelagem Orientada a Objetos (UML/C++)
/// ============================================================================
/// Interface Espacial Holográfica (World-Space HUD) do Pac-Man Semântico.
/// Conecta-se à interface IInstanciaSemantica e eventos polimórficos
/// para exibir em tempo real a pontuação, status DL e axiomas.
/// ============================================================================
/// </summary>
public class HUDHolograficoXR : MonoBehaviour
{
    [Header("Identidade")]
    [Tooltip("Título do painel.")]
    public string tituloHUD = "PAC-MAN SEMÂNTICO - HUD VR";

    [Header("Referências Visuais")]
    [Tooltip("Transform da Câmera XR para o HUD seguir suavemente no campo de visão.")]
    public Transform cameraXR;

    [Tooltip("Distância à frente da câmera.")]
    public float distanciaFrente = 1.8f;

    [Tooltip("Altura em relação aos olhos.")]
    public float offsetAltura = -0.15f;

    [Tooltip("Velocidade de acompanhamento suave.")]
    public float velocidadeAcompanhamento = 4f;

    [Header("Campos de Texto Clássicos (UI)")]
    public Text textoPontuacao;
    public Text textoGemasRestantes;
    public Text textoClasseFocada;
    public Text textoFormulaDL;
    public Text textoDescricao;

    [Header("Campos TextMeshPro (Opcional)")]
    public TMP_Text textoPontuacaoTMP;
    public TMP_Text textoGemasRestantesTMP;
    public TMP_Text textoClasseFocadaTMP;
    public TMP_Text textoFormulaDLTMP;

    [Header("Modo de Fixação")]
    [Tooltip("Se verdadeiro, o HUD flutua suavemente no campo de visão do usuário.")]
    public bool acompanharCabeca = true;

    private void OnEnable()
    {
        InstanciaSemanticaBase.OnInstanciaInteragida += AoInteragirInstancia;
        InstanciaSemanticaBase.OnInstanciaFocada += AoFocarInstancia;
        InstanciaSemanticaBase.OnInstanciaDesfocada += AoDesfocarInstancia;
    }

    private void OnDisable()
    {
        InstanciaSemanticaBase.OnInstanciaInteragida -= AoInteragirInstancia;
        InstanciaSemanticaBase.OnInstanciaFocada -= AoFocarInstancia;
        InstanciaSemanticaBase.OnInstanciaDesfocada -= AoDesfocarInstancia;
    }

    private void Start()
    {
        if (cameraXR == null && Camera.main != null)
        {
            cameraXR = Camera.main.transform;
        }

        AtualizarTextosHUD();
    }

    private void LateUpdate()
    {
        if (acompanharCabeca && cameraXR != null)
        {
            Vector3 posicaoAlvo = cameraXR.position + (cameraXR.forward * distanciaFrente) + (cameraXR.up * offsetAltura);
            transform.position = Vector3.Lerp(transform.position, posicaoAlvo, Time.deltaTime * velocidadeAcompanhamento);

            Quaternion rotacaoAlvo = Quaternion.LookRotation(transform.position - cameraXR.position);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotacaoAlvo, Time.deltaTime * velocidadeAcompanhamento);
        }

        AtualizarTextosHUD();
    }

    private void AtualizarTextosHUD()
    {
        string scoreStr = $"Pac-Man Score: {InstanciaSemanticaBase.pontuacaoLogica}";
        string restantesStr = $"Gemas Válidas: {InstanciaSemanticaBase.instanciasValidasRestantes}";

        if (textoPontuacao != null) textoPontuacao.text = scoreStr;
        if (textoPontuacaoTMP != null) textoPontuacaoTMP.text = scoreStr;

        if (textoGemasRestantes != null) textoGemasRestantes.text = restantesStr;
        if (textoGemasRestantesTMP != null) textoGemasRestantesTMP.text = restantesStr;
    }

    private void AoFocarInstancia(IInstanciaSemantica inst)
    {
        if (inst == null) return;

        string classe = $"Classe: {inst.NomeClasse} [{(inst.EhValida ? "<color=#00FF66>VÁLIDA</color>" : "<color=#FF3333>INVÁLIDA</color>")}]";
        string formula = $"DL: {inst.ExpressaoDL}";
        string desc = inst.DescricaoSemantica;

        if (textoClasseFocada != null) textoClasseFocada.text = classe;
        if (textoClasseFocadaTMP != null) textoClasseFocadaTMP.text = classe;

        if (textoFormulaDL != null) textoFormulaDL.text = formula;
        if (textoFormulaDLTMP != null) textoFormulaDLTMP.text = formula;

        if (textoDescricao != null) textoDescricao.text = desc;
    }

    private void AoDesfocarInstancia(IInstanciaSemantica inst)
    {
        string padrao = "Aponte o feixe laser para inspecionar axiomas";
        if (textoClasseFocada != null) textoClasseFocada.text = padrao;
        if (textoClasseFocadaTMP != null) textoClasseFocadaTMP.text = padrao;
        if (textoFormulaDL != null) textoFormulaDL.text = "";
        if (textoFormulaDLTMP != null) textoFormulaDLTMP.text = "";
    }

    private void AoInteragirInstancia(IInstanciaSemantica inst, bool sucesso)
    {
        AtualizarTextosHUD();
    }
}
