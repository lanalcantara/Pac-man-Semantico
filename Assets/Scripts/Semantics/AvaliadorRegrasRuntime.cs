using System;
using UnityEngine;
using VirtOnto.Model;

namespace PacMan.Semantics
{
    /// <summary>
    /// ============================================================================
    /// PAC-MAN SEMÂNTICO (CIn-UFPE) - Avaliador de Regras SWRL em Tempo de Execução
    /// ============================================================================
    /// Componente que avalia continuamente as distâncias espaciais euclidianas entre
    /// o Pac-Man (XR Origin) e os fantasmas, acionando o MotorRaciocinioSemantico
    /// para inferir estados ontológicos via regras SWRL e atualizar dinamicamente
    /// as propriedades visuais dos fantasmas (materiais e emissão).
    ///
    /// Também valida o posicionamento e assentamento do XR Origin no plano procedural
    /// para garantir uma experiência imersiva de Realidade Virtual correta.
    /// ============================================================================
    /// </summary>
    [AddComponentMenu("Pac-Man Semântico/Avaliador Regras Runtime")]
    public class AvaliadorRegrasRuntime : MonoBehaviour
    {
        [Header("Referências")]
        [Tooltip("Instância central do motor de raciocínio ontológico.")]
        public MotorRaciocinioSemantico motorSemantico;

        [Tooltip("Transform do Pac-Man (Jogador / XR Origin) para cálculo de distâncias.")]
        public Transform pacmanTransform;

        [Tooltip("Transform do Fantasma alvo (ex: Blinky) para avaliação e atualização visual.")]
        public Transform ghostTransform;

        [Header("Configurações de Regra SWRL")]
        [Tooltip("Distância limite para ativação de comportamento agressivo / perseguição (SWRL).")]
        public float distanciaAgressiva = 2.0f;

        [Tooltip("Distância limite para ativação de vulnerabilidade quando empoderado (SWRL).")]
        public float distanciaVulneravel = 3.0f;

        [Header("Estado do Jogador")]
        [Tooltip("Permite simular o estado empoderado (Power Pellet) no Inspector ou sincronizar com o motor.")]
        public bool simularPacmanEmpowered = true;

        [Header("Validação de Posicionamento XR Origin")]
        [Tooltip("Executa validação contínua da altura do XR Origin para evitar corte ou flutuação do chão.")]
        public bool validarChaoContinuo = true;

        [Tooltip("Indica se o XR Origin está posicionado e assentado corretamente sobre a superfície.")]
        public bool xrOriginPosicionadoCorretamente = false;

        [Tooltip("Última altura de superfície detectada do chão procedural.")]
        public float alturaChaoDetectada = 0f;

        [Header("Diagnóstico Runtime")]
        [Tooltip("Distância euclidiana atual calculada entre Pac-Man e o Fantasma.")]
        public float distanciaAtual = 0f;

        [Tooltip("Último estado ontológico inferido pelo motor de regras.")]
        public GameEntityState ultimoEstadoInferido = GameEntityState.Patrol;

        private void Start()
        {
            InicializarReferencias();
            ValidarPosicionamentoXROrigin();
        }

        private void Update()
        {
            if (motorSemantico == null || pacmanTransform == null || ghostTransform == null)
            {
                // Tenta resolver referências ausentes dinamicamente se ainda não localizadas
                InicializarReferencias();
                if (motorSemantico == null || pacmanTransform == null || ghostTransform == null) return;
            }

            // Valida continuamente a posição vertical do XR Origin se configurado
            if (validarChaoContinuo)
            {
                ValidarPosicionamentoXROrigin();
            }

            // Calcula a distância espacial em tempo de execução
            float distancia = Vector3.Distance(pacmanTransform.position, ghostTransform.position);
            distanciaAtual = distancia;

            // Obtém o estado empoderado do jogador (ex: se consumiu uma Power Pellet)
            bool pacmanEmpowered = (motorSemantico != null && motorSemantico.pacmanIsPowered) || simularPacmanEmpowered;

            // Sincroniza limiares de regras SWRL com o motor semântico
            if (motorSemantico != null)
            {
                motorSemantico.distanciaAgressao = distanciaAgressiva;
                motorSemantico.distanciaVulnerabilidade = distanciaVulneravel;
            }

            // Simula a inferência SWRL baseada em Description Logic / Regras Ontológicas
            GameEntityState estadoAtual = motorSemantico.AvaliarRegraEstadoFantasma("ghost_blinky", pacmanEmpowered, distancia);
            ultimoEstadoInferido = estadoAtual;

            // Aplica a mudança visual com base no estado inferido pelo grafo
            AplicarVisualConformeEstado(estadoAtual, ghostTransform.gameObject);
        }

        /// <summary>
        /// Aplica as alterações visuais de material na entidade fantasma com base no estado ontológico inferido.
        /// Azul: Vulnerável (sob efeito de Power Pellet).
        /// Vermelho: Agressivo (perseguição por proximidade).
        /// Branco/Padrão: Patrulha.
        /// </summary>
        public void AplicarVisualConformeEstado(GameEntityState estado, GameObject ghostObj)
        {
            if (ghostObj == null) return;

            Renderer rend = ghostObj.GetComponent<Renderer>();
            if (rend != null && rend.material != null)
            {
                switch (estado)
                {
                    case GameEntityState.Vulnerable:
                        rend.material.color = Color.blue; // Azul quando vulnerável (Power Pellet ativa)
                        break;
                    case GameEntityState.Aggressive:
                        rend.material.color = Color.red;  // Vermelho em modo de perseguição
                        break;
                    case GameEntityState.Patrol:
                    default:
                        rend.material.color = Color.white; // Padrão
                        break;
                }
            }

            // Garante aplicação em todos os sub-renderers caso o prefab utilize hierarquia de malhas
            Renderer[] renderers = ghostObj.GetComponentsInChildren<Renderer>();
            Color corAlvo = estado switch
            {
                GameEntityState.Vulnerable => Color.blue,
                GameEntityState.Aggressive => Color.red,
                _ => Color.white
            };

            foreach (var r in renderers)
            {
                if (r != null && r.material != null)
                {
                    if (r != rend)
                    {
                        r.material.color = corAlvo;
                    }

                    if (r.material.HasProperty("_BaseColor"))
                    {
                        r.material.SetColor("_BaseColor", corAlvo);
                    }

                    if (r.material.HasProperty("_EmissionColor"))
                    {
                        r.material.EnableKeyword("_EMISSION");
                        r.material.SetColor("_EmissionColor", corAlvo * (estado == GameEntityState.Aggressive ? 0.9f : 0.4f));
                    }
                }
            }
        }

        /// <summary>
        /// Valida o posicionamento do XR Origin na cena procedural para garantir uma experiência imersiva correta.
        /// Avalia a altura Y em relação à superfície superior do chão (Plane), evitando corte ou flutuação indevida.
        /// </summary>
        public bool ValidarPosicionamentoXROrigin()
        {
            if (pacmanTransform == null)
            {
                xrOriginPosicionadoCorretamente = false;
                return false;
            }

            float alturaChao = 0f;
            GerenciadorCenarioSemantico gerenciador = FindFirstObjectByType<GerenciadorCenarioSemantico>();

            if (gerenciador != null)
            {
                alturaChao = gerenciador.ObterAlturaSuperficieChao(pacmanTransform.position);
            }
            else
            {
                // Fallback via Raycast físico vertical
                Vector3 origem = new Vector3(pacmanTransform.position.x, 50f, pacmanTransform.position.z);
                if (Physics.Raycast(origem, Vector3.down, out RaycastHit hit, 100f, ~0, QueryTriggerInteraction.Ignore))
                {
                    alturaChao = hit.point.y;
                }
            }

            alturaChaoDetectada = alturaChao;

            // Se o XR Origin estiver afundado abaixo do piso, corrige o eixo Y
            if (pacmanTransform.position.y < alturaChao - 0.05f)
            {
                Debug.LogWarning($"[AvaliadorRegrasRuntime] XR Origin afundado ({pacmanTransform.position.y:F2} < piso {alturaChao:F2}). Corrigindo para a superfície.");
                pacmanTransform.position = new Vector3(pacmanTransform.position.x, alturaChao, pacmanTransform.position.z);
                xrOriginPosicionadoCorretamente = true;
                return false;
            }

            xrOriginPosicionadoCorretamente = true;
            return true;
        }

        /// <summary>
        /// Inicializa e autodetecta referências essenciais se não tiverem sido configuradas no Inspector.
        /// </summary>
        private void InicializarReferencias()
        {
            if (motorSemantico == null)
            {
                motorSemantico = FindFirstObjectByType<MotorRaciocinioSemantico>();
            }

            if (pacmanTransform == null)
            {
                GameObject player = GameObject.FindWithTag("Player");
                if (player != null)
                {
                    pacmanTransform = player.transform;
                }
                else
                {
                    ControladorXRJogador xrJogador = FindFirstObjectByType<ControladorXRJogador>();
                    if (xrJogador != null)
                    {
                        pacmanTransform = xrJogador.transform;
                    }
                    else if (Camera.main != null)
                    {
                        pacmanTransform = Camera.main.transform;
                    }
                }
            }

            if (ghostTransform == null && motorSemantico != null)
            {
                // Tenta localizar a instância do Blinky no grafo ou na hierarquia da cena
                Graph grafo = motorSemantico.GetSemanticGraph();
                if (grafo != null)
                {
                    Node ghostNode = grafo.GetNode("ghost_blinky");
                    if (ghostNode != null && ghostNode.GameObjectInstance != null)
                    {
                        ghostTransform = ghostNode.GameObjectInstance.transform;
                    }
                }

                if (ghostTransform == null)
                {
                    GameObject ghostObj = GameObject.Find("Fantasma") ?? GameObject.Find("Blinky") ?? GameObject.Find("ObstaculoInvalido");
                    if (ghostObj != null)
                    {
                        ghostTransform = ghostObj.transform;
                    }
                }
            }
        }
    }
}
