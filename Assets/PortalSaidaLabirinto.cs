using System;
using UnityEngine;
using PacMan.Semantics;

/// <summary>
/// ============================================================================
/// PAC-MAN SEMÂNTICO (CIn-UFPE) - Portal de Saída e Escape do Labirinto
/// ============================================================================
/// Componente que governa a saída / portal de escape do labirinto ontológico.
/// Transforma o labirinto infinito em uma missão clara com objetivo e vitória:
/// 1. Quarentena Ativa (Bloqueado): Inicialmente com escudo holográfico de energia
///    exigindo que o pesquisador biomédico colete todos os espécimes válidos (gemas).
/// 2. Desbloqueio e Transição (Aberto): Assim que todos os espécimes são analisados
///    e coletados, o portal ativa feixes de luz esmeralda, destranca o campo e emite
///    alerta visual e sonoro de fuga iminente.
/// 3. Vitória da Missão: Ao entrar no portal desbloqueado, dispara o evento de Vitória,
///    apresentando o relatório final, telemetria e pontuação.
/// ============================================================================
/// </summary>
[DisallowMultipleComponent]
public class PortalSaidaLabirinto : MonoBehaviour
{
    [Header("Estado do Portal")]
    [Tooltip("Indica se o portal está desbloqueado para fuga.")]
    public bool portalDesbloqueado = false;

    [Tooltip("Indica se a vitória já foi alcançada.")]
    public bool vitoriaAlcancada = false;

    [Header("Objetivo de Espécimes")]
    [Tooltip("Quantidade de espécimes válidos restantes para abrir o portal.")]
    public int especimesRestantes = 0;

    [Tooltip("Quantidade total de espécimes que precisavam ser coletados.")]
    public int totalEspecimesInicial = 0;

    [Header("Elementos Visuais Procedurais")]
    public GameObject escudoEnergia;
    public GameObject arcoPortal;
    public Light luzPortal;
    public Material materialEscudo;

    public static event Action OnPortalAberto;
    public static event Action OnVitoriaCompletada;

    private static PortalSaidaLabirinto s_Instancia;
    public static PortalSaidaLabirinto Instancia => s_Instancia;

    private float m_RotacaoAnel = 0f;

    private void Awake()
    {
        s_Instancia = this;
    }

    private void Start()
    {
        AtualizarEstadoPortal();
    }

    private void Update()
    {
        if (vitoriaAlcancada) return;

        // Monitora quantidade de espécimes restantes
        especimesRestantes = InstanciaSemanticaBase.instanciasValidasRestantes;

        if (!portalDesbloqueado && especimesRestantes <= 0 && InstanciaSemanticaBase.totalColetadas > 0)
        {
            DesbloquearPortal();
        }

        // Animação de anel de contenção holográfico
        if (arcoPortal != null)
        {
            m_RotacaoAnel += (portalDesbloqueado ? 90f : 30f) * Time.deltaTime;
            arcoPortal.transform.Rotate(Vector3.forward, (portalDesbloqueado ? 90f : 30f) * Time.deltaTime, Space.Self);
        }

        // Detecção de proximidade com o jogador para vitória
        VerificarEntradaJogador();
    }

    /// <summary>
    /// Desbloqueia o portal de saída quando todos os espécimes válidos foram consumidos.
    /// </summary>
    public void DesbloquearPortal()
    {
        if (portalDesbloqueado) return;
        portalDesbloqueado = true;

        if (escudoEnergia != null)
        {
            // O escudo de quarentena se desativa
            escudoEnergia.SetActive(false);
        }

        if (luzPortal != null)
        {
            luzPortal.color = new Color(0.1f, 1f, 0.45f);
            luzPortal.intensity = 4.5f;
            luzPortal.range = 10f;
        }

        Debug.Log("<color=#00FF99><b>[PORTAL DE SAÍDA DESBLOQUEADO]</b></color> Todos os espécimes foram coletados! A quarentena foi desativada. Fuja pelo portal para vencer!");

        // Feedback Juice de abertura do portal
        if (GerenciadorEfeitosJuice.Instancia != null)
        {
            GerenciadorEfeitosJuice.Instancia.CriarExplosaoBrilhoColeta(transform.position + Vector3.up * 1.5f, new Color(0.1f, 1f, 0.45f));
        }

        OnPortalAberto?.Invoke();
    }

    private void VerificarEntradaJogador()
    {
        if (!portalDesbloqueado || vitoriaAlcancada) return;

        GameObject player = GameObject.FindWithTag("Player");
        Transform trPlayer = player != null ? player.transform : (Camera.main != null ? Camera.main.transform : null);
        if (trPlayer == null) return;

        Vector2 posPortal2D = new Vector2(transform.position.x, transform.position.z);
        Vector2 posPlayer2D = new Vector2(trPlayer.position.x, trPlayer.position.z);
        float distHorizontal = Vector2.Distance(posPortal2D, posPlayer2D);

        if (distHorizontal <= 2.2f && Mathf.Abs(transform.position.y - trPlayer.position.y) < 3.5f)
        {
            ExecutarVitoria(trPlayer.gameObject);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!portalDesbloqueado || vitoriaAlcancada) return;
        if (InstanciaSemanticaBase.EhColisorDoJogador(other))
        {
            ExecutarVitoria(other.gameObject);
        }
    }

    public void ExecutarVitoria(GameObject jogador = null)
    {
        if (vitoriaAlcancada) return;
        vitoriaAlcancada = true;

        Debug.Log("<color=#00FFFF><b>=======================================================</b></color>\n" +
                  "<color=#00FF99><b>[VITÓRIA - PAC-MAN SEMÂNTICO] MISSÃO CONCLUÍDA COM SUCESSO!</b></color>\n" +
                  $"Pontuação Final: {InstanciaSemanticaBase.pontuacaoLogica} pts | Espécimes: {InstanciaSemanticaBase.totalColetadas} | Quarentena Superada!\n" +
                  "<color=#00FFFF><b>=======================================================</b></color>");

        // Haptics no VR
        if (jogador != null)
        {
            ControladorXRJogador ctrlXR = jogador.GetComponentInParent<ControladorXRJogador>();
            if (ctrlXR != null)
            {
                ctrlXR.DispararHapticsDualController(0.85f, 0.6f);
            }
        }

        // Feedback Juice comemorativo
        if (GerenciadorEfeitosJuice.Instancia != null)
        {
            for (int i = 0; i < 4; i++)
            {
                Vector3 offset = UnityEngine.Random.insideUnitSphere * 1.5f;
                GerenciadorEfeitosJuice.Instancia.CriarExplosaoBrilhoColeta(transform.position + offset + Vector3.up * 1.2f, new Color(0.2f, 1f, 0.8f));
            }
        }

        // Notifica evento global de vitória
        OnVitoriaCompletada?.Invoke();
    }

    private void AtualizarEstadoPortal()
    {
        if (escudoEnergia != null)
        {
            escudoEnergia.SetActive(!portalDesbloqueado);
        }

        if (luzPortal != null)
        {
            luzPortal.color = portalDesbloqueado ? new Color(0.1f, 1f, 0.45f) : new Color(1f, 0.35f, 0.1f);
            luzPortal.intensity = 2.5f;
        }
    }
}
