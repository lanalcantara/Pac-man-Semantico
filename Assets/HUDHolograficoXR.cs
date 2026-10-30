using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

/// <summary>
/// ============================================================================
/// PAC-MAN SEMÂNTICO (Semantic Pac-Man) - HUD Holográfico Espacial VR Antienjoo
/// ============================================================================
/// Interface Espacial Holográfica desenvolvida para Realidade Virtual imersiva (XR Origin).
/// Resolve o problema de enjoo de movimento (Motion Sickness) através de:
/// 
/// 1. Ancoragem ao Pulso / Mão (Modo Relógio / Terminal Biomédico Holográfico):
///    O HUD fica fixado ao pulso do jogador, surgindo como um display holográfico
///    apenas quando o utilizador ergue a mão e olha diretamente para ela.
/// 
/// 2. Ativação Automática por Gaze (Detecção de Campo de Visão / Postura):
///    Evita qualquer elemento estático preso aos olhos na visão periférica.
/// 
/// 3. Modo Flutuante Espacial com Amortecimento Inercial (Comfort Damping):
///    Caso configurado no espaço à frente, amortece giros rápidos da cabeça.
/// 
/// 4. Inspeção Semântica em Tempo Real (Ontologia de Parasitologia Protégé):
///    Exibe conceitos ontológicos, axiomas de Lógica de Descrição (DL),
///    taxonomia biológica e dados de telemetria médica.
/// ============================================================================
/// </summary>
[DisallowMultipleComponent]
public class HUDHolograficoXR : MonoBehaviour
{
    public enum ModoAncoragemHUD
    {
        PulsoEsquerdo,      // Ancorado ao pulso esquerdo (Smartwatch/Terminal Biomédico)
        PulsoDireito,       // Ancorado ao pulso direito
        FlutuanteAmortecido // Flutuando no mundo à frente da visão com amortecimento inercial
    }

    [Header("Configuração de Ancoragem e Conforto VR")]
    [Tooltip("Modo de posicionamento do HUD no espaço de Realidade Virtual.")]
    public ModoAncoragemHUD modoAncoragem = ModoAncoragemHUD.PulsoEsquerdo;

    [Tooltip("Ativa automaticamente o HUD somente quando o usuário olha diretamente para o pulso/mão.")]
    public bool ativarPorOlharParaMao = true;

    [Tooltip("Distância máxima entre o headset e o pulso para ativar a tela holográfica (em metros).")]
    public float distanciaMaxAtivacaoPulso = 0.75f;

    [Tooltip("Ângulo de visão limite (Dot Product) para ativar a tela holográfica (0.7 ~ 45°).")]
    public float limiarAnguloVisao = 0.68f;

    [Tooltip("Velocidade de transição suave de opacidade (Fade In / Fade Out).")]
    public float velocidadeFade = 6.0f;

    [Header("Offsets de Posição no Pulso")]
    public Vector3 offsetPosicaoPulso = new Vector3(0.06f, 0.08f, -0.04f);
    public Vector3 offsetRotacaoPulso = new Vector3(35f, -90f, 0f);
    public Vector3 escalaPulso = new Vector3(0.0012f, 0.0012f, 0.0012f);

    [Header("Referências Visuais VR")]
    [Tooltip("Transform da Câmera XR (Headset / Olhos). Se nula, busca Camera.main.")]
    public Transform cameraXR;

    [Tooltip("Transform do Controlador/Mão Esquerda para ancoragem do pulso.")]
    public Transform maoEsquerda;

    [Tooltip("Transform do Controlador/Mão Direita para ancoragem alternativa.")]
    public Transform maoDireita;

    [Header("Campos de Texto Clássicos (UI)")]
    public Text textoPontuacao;
    public Text textoGemasRestantes;
    public Text textoClasseFocada;
    public Text textoFormulaDL;
    public Text textoDescricao;

    [Header("Campos TextMeshPro (Recomendado)")]
    public TMP_Text textoPontuacaoTMP;
    public TMP_Text textoGemasRestantesTMP;
    public TMP_Text textoClasseFocadaTMP;
    public TMP_Text textoFormulaDLTMP;
    public TMP_Text textoDescricaoTMP;

    [Header("Visibilidade e Forçamento")]
    [Tooltip("Permite fixar o HUD sempre visível (por exemplo, ao pressionar o botão de menu do controle).")]
    public bool fixarSempreVisivel = false;

    private CanvasGroup m_CanvasGroup;
    private Canvas m_Canvas;
    private float m_AlphaAlvo = 0f;
    private Vector3 m_VelocidadePosAmortecida;

    private void Awake()
    {
        m_CanvasGroup = GetComponent<CanvasGroup>();
        if (m_CanvasGroup == null)
        {
            m_CanvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        m_Canvas = GetComponent<Canvas>();
        if (m_Canvas != null)
        {
            m_Canvas.renderMode = RenderMode.WorldSpace;
        }
    }

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
        LocalizarReferenciasVR();
        ConfigurarAncoragemInicial();
        AtualizarTextosHUD();
    }

    private void Update()
    {
        VerificarEntradaBotoesVR();
        ProcessarVisibilidadeAntienjoo();
    }

    private void LateUpdate()
    {
        ProcessarPosicionamentoEspacial();
    }

    /// <summary>
    /// Localiza automaticamente os componentes XR Origin, Câmera e Controladores.
    /// </summary>
    public void LocalizarReferenciasVR()
    {
        if (cameraXR == null)
        {
            if (Camera.main != null) cameraXR = Camera.main.transform;
            else
            {
                var cam = GetComponentInParent<Camera>() ?? FindFirstObjectByType<Camera>();
                if (cam != null) cameraXR = cam.transform;
            }
        }

        if (maoEsquerda == null)
        {
            GameObject esq = GameObject.Find("Left Hand Controller") ?? GameObject.Find("LeftController") ?? GameObject.Find("LeftHand");
            if (esq != null) maoEsquerda = esq.transform;
        }

        if (maoDireita == null)
        {
            GameObject dir = GameObject.Find("Right Hand Controller") ?? GameObject.Find("RightController") ?? GameObject.Find("RightHand");
            if (dir != null) maoDireita = dir.transform;
        }
    }

    /// <summary>
    /// Configura a ancoragem física no pulso ou modo flutuante.
    /// </summary>
    public void ConfigurarAncoragemInicial()
    {
        Transform maoAlvo = (modoAncoragem == ModoAncoragemHUD.PulsoDireito) ? maoDireita : maoEsquerda;

        if ((modoAncoragem == ModoAncoragemHUD.PulsoEsquerdo || modoAncoragem == ModoAncoragemHUD.PulsoDireito) && maoAlvo != null)
        {
            transform.SetParent(maoAlvo, false);
            transform.localPosition = offsetPosicaoPulso;
            transform.localRotation = Quaternion.Euler(offsetRotacaoPulso);
            transform.localScale = escalaPulso;
        }
    }

    /// <summary>
    /// Avalia a postura e o olhar do jogador em tempo real para ativar/ocultar suavemente o HUD.
    /// </summary>
    private void ProcessarVisibilidadeAntienjoo()
    {
        if (fixarSempreVisivel)
        {
            m_AlphaAlvo = 0.95f;
        }
        else if (modoAncoragem == ModoAncoragemHUD.PulsoEsquerdo || modoAncoragem == ModoAncoragemHUD.PulsoDireito)
        {
            if (cameraXR != null && ativarPorOlharParaMao)
            {
                Vector3 dirAoPulso = (transform.position - cameraXR.position).normalized;
                float dotGaze = Vector3.Dot(cameraXR.forward, dirAoPulso);
                float dist = Vector3.Distance(transform.position, cameraXR.position);

                // O HUD surge apenas se o jogador estiver olhando para o pulso e com a mão próxima ao campo de visão
                bool olhandoParaPulso = (dotGaze >= limiarAnguloVisao) && (dist <= distanciaMaxAtivacaoPulso);
                m_AlphaAlvo = olhandoParaPulso ? 0.95f : 0f;
            }
            else
            {
                m_AlphaAlvo = 0.95f;
            }
        }
        else // Flutuante amortecido
        {
            m_AlphaAlvo = 0.90f;
        }

        if (m_CanvasGroup != null)
        {
            m_CanvasGroup.alpha = Mathf.MoveTowards(m_CanvasGroup.alpha, m_AlphaAlvo, Time.deltaTime * velocidadeFade);
            m_CanvasGroup.interactable = m_CanvasGroup.alpha > 0.3f;
            m_CanvasGroup.blocksRaycasts = m_CanvasGroup.alpha > 0.3f;
        }
    }

    /// <summary>
    /// Processa o acompanhamento espacial caso esteja no modo flutuante com amortecimento.
    /// </summary>
    private void ProcessarPosicionamentoEspacial()
    {
        if (modoAncoragem == ModoAncoragemHUD.FlutuanteAmortecido && cameraXR != null)
        {
            Vector3 posDesejada = cameraXR.position + (cameraXR.forward * 1.35f) + (cameraXR.up * -0.15f);
            transform.position = Vector3.SmoothDamp(transform.position, posDesejada, ref m_VelocidadePosAmortecida, 0.25f);

            Quaternion rotDesejada = Quaternion.LookRotation(transform.position - cameraXR.position);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotDesejada, Time.deltaTime * 3.5f);
        }

        AtualizarTextosHUD();
    }

    /// <summary>
    /// Permite alternar visibilidade e fixação através de botões dos controladores XR ou teclado.
    /// </summary>
    private void VerificarEntradaBotoesVR()
    {
        // Teclado fallback (H para alternar, M para fixar)
        if (Input.GetKeyDown(KeyCode.H))
        {
            AlternarVisibilidadeHUD();
        }
        if (Input.GetKeyDown(KeyCode.M))
        {
            fixarSempreVisivel = !fixarSempreVisivel;
        }

        // Leitura de botões de controle VR (Secondary Button / Menu Button no comando esquerdo)
        InputDevice leftDev = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        if (leftDev.isValid)
        {
            if (leftDev.TryGetFeatureValue(CommonUsages.secondaryButton, out bool secBtn) && secBtn)
            {
                fixarSempreVisivel = !fixarSempreVisivel;
            }
            if (leftDev.TryGetFeatureValue(CommonUsages.menuButton, out bool menuBtn) && menuBtn)
            {
                AlternarVisibilidadeHUD();
            }
        }
    }

    public void AlternarVisibilidadeHUD()
    {
        fixarSempreVisivel = !fixarSempreVisivel;
        if (fixarSempreVisivel) m_AlphaAlvo = 0.95f;
    }

    /// <summary>
    /// Atualiza as métricas ontológicas, vidas e pontuação lógica no HUD.
    /// </summary>
    public void AtualizarTextosHUD()
    {
        int vidas = 3;
        var ctrlJogador = FindFirstObjectByType<PacMan.Player.ControladorJogador>();
        if (ctrlJogador != null) vidas = ctrlJogador.vidasRestantes;
        else
        {
            var ctrlXR = FindFirstObjectByType<ControladorXRJogador>();
            if (ctrlXR != null) vidas = ctrlXR.vidasRestantes;
        }

        string coracoes = vidas > 0 ? new string('♥', vidas) : "0";
        string scoreStr = $"Score: {InstanciaSemanticaBase.pontuacaoLogica} | Vidas: {coracoes}";
        string restantesStr = $"Gemas Válidas: {InstanciaSemanticaBase.instanciasValidasRestantes}";

        if (textoPontuacao != null) textoPontuacao.text = scoreStr;
        if (textoPontuacaoTMP != null) textoPontuacaoTMP.text = scoreStr;

        if (textoGemasRestantes != null) textoGemasRestantes.text = restantesStr;
        if (textoGemasRestantesTMP != null) textoGemasRestantesTMP.text = restantesStr;
    }

    /// <summary>
    /// Exibe os axiomas formais e propriedades ontológicas da entidade focada pelo laser do VR.
    /// </summary>
        public void AoFocarInstancia(IInstanciaSemantica inst)
    {
        if (inst == null) return;

        string statusCor = inst.EhValida ? "<color=#00FF88>VÁLIDA (+10)</color>" : "<color=#FF3344>INVÁLIDA (-5)</color>";
        string nomeFormatado = InstanciaSemanticaBase.FormatarNomeClasse(inst.NomeClasse);
        string classe = $"🔬 {nomeFormatado} [{statusCor}]";
        string formula = $"DL: {inst.ExpressaoDL}";
        string desc = string.IsNullOrEmpty(inst.DescricaoSemantica) ? "Espécime ontológico em análise..." : inst.DescricaoSemantica;

        if (textoClasseFocada != null) textoClasseFocada.text = classe;
        if (textoClasseFocadaTMP != null) textoClasseFocadaTMP.text = classe;

        if (textoFormulaDL != null) textoFormulaDL.text = formula;
        if (textoFormulaDLTMP != null) textoFormulaDLTMP.text = formula;

        if (textoDescricao != null) textoDescricao.text = desc;
        if (textoDescricaoTMP != null) textoDescricaoTMP.text = desc;
    }

    public void AoDesfocarInstancia(IInstanciaSemantica inst)
    {
        string padrao = "🔬 Aponte o laser do comando para inspecionar parasitas";
        if (textoClasseFocada != null) textoClasseFocada.text = padrao;
        if (textoClasseFocadaTMP != null) textoClasseFocadaTMP.text = padrao;
        if (textoFormulaDL != null) textoFormulaDL.text = "";
        if (textoFormulaDLTMP != null) textoFormulaDLTMP.text = "";
        if (textoDescricao != null) textoDescricao.text = "";
        if (textoDescricaoTMP != null) textoDescricaoTMP.text = "";
    }

    public void AoInteragirInstancia(IInstanciaSemantica inst, bool sucesso)
    {
        if (inst != null)
        {
            string status = sucesso ? $"<color=#00FF88>COLETADA (+{inst.ValorPontuacao})</color>" : $"<color=#FF3344>VIOLAÇÃO ({inst.ValorPontuacao})</color>";
            string nomeFormatado = InstanciaSemanticaBase.FormatarNomeClasse(inst.NomeClasse);
            string infoCaptura = $"Captura: {inst.Id} | {nomeFormatado} [{status}]";
            if (textoClasseFocada != null) textoClasseFocada.text = infoCaptura;
            if (textoClasseFocadaTMP != null) textoClasseFocadaTMP.text = infoCaptura;
            if (textoFormulaDL != null) textoFormulaDL.text = $"DL: {inst.ExpressaoDL}";
            if (textoFormulaDLTMP != null) textoFormulaDLTMP.text = $"DL: {inst.ExpressaoDL}";
            if (textoDescricao != null) textoDescricao.text = inst.DescricaoSemantica;
            if (textoDescricaoTMP != null) textoDescricaoTMP.text = inst.DescricaoSemantica;
        }
        AtualizarTextosHUD();
    }

    public void AoInteragirInstanciaManual(string id, string nomeClasse, int pontos, string expressaoDL, string desc, bool sucesso)
    {
        string status = sucesso ? $"<color=#00FF88>COLETADA (+{pontos})</color>" : $"<color=#FF3344>VIOLAÇÃO ({pontos})</color>";
        string nomeFormatado = InstanciaSemanticaBase.FormatarNomeClasse(nomeClasse);
        string infoCaptura = $"Captura: {id} | {nomeFormatado} [{status}]";
        if (textoClasseFocada != null) textoClasseFocada.text = infoCaptura;
        if (textoClasseFocadaTMP != null) textoClasseFocadaTMP.text = infoCaptura;
        if (textoFormulaDL != null) textoFormulaDL.text = $"DL: {expressaoDL}";
        if (textoFormulaDLTMP != null) textoFormulaDLTMP.text = $"DL: {expressaoDL}";
        if (textoDescricao != null) textoDescricao.text = desc;
        if (textoDescricaoTMP != null) textoDescricaoTMP.text = desc;
        AtualizarTextosHUD();
    }
}