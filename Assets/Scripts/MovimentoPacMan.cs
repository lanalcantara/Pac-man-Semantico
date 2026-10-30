using UnityEngine;

namespace PacMan.Player
{
    /// <summary>
    /// ============================================================================
    /// PAC-MAN SEMÂNTICO (CIn-UFPE) - Controle de Movimento e Curvatura do Pac-Man
    /// ============================================================================
    /// Componente de navegação leve e fluida para o labirinto:
    /// - Teclas A/D e Setas Laterais (ArrowLeft / ArrowRight) giram suavemente o Pac-Man.
    /// - Teclas W/S e Setas Cima/Baixo (ArrowUp / ArrowDown) deslocam o Pac-Man.
    /// - Permite curvar livremente pelas esquinas e corredores sem travar em paredes.
    /// ============================================================================
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class MovimentoPacMan : MonoBehaviour
    {
        [Header("Velocidades")]
        [Tooltip("Velocidade de avanço linear (metros por segundo).")]
        public float velocidadeMovimento = 3.8f;

        [Tooltip("Velocidade de rotação ao usar as teclas A/D ou Setas Laterais (graus por segundo).")]
        public float velocidadeGiro = 110.0f;

        [Tooltip("Gravidade aplicada ao personagem.")]
        public float gravidade = 9.81f;

        private CharacterController m_CharacterController;
        private float m_VelocidadeVertical = 0f;

        private void Awake()
        {
            GarantirCharacterController();
        }

        private void GarantirCharacterController()
        {
            if (m_CharacterController == null)
            {
                m_CharacterController = GetComponent<CharacterController>() ?? gameObject.AddComponent<CharacterController>();
            }

            if (m_CharacterController != null)
            {
                m_CharacterController.radius = 0.45f;
                m_CharacterController.height = 1.8f;
                m_CharacterController.skinWidth = 0.05f;
                m_CharacterController.minMoveDistance = 0f;
                m_CharacterController.stepOffset = 0.15f;
            }
        }

        private void Update()
        {
            ProcessarGiro();
            ProcessarMovimento();
        }

        /// <summary>
        /// Aplica rotação suave via teclas laterais (A/D ou ArrowLeft/ArrowRight).
        /// </summary>
        public void ProcessarGiro()
        {
            float giro = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.Q)) giro -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.E)) giro += 1f;

            if (Mathf.Abs(giro) > 0.01f)
            {
                transform.Rotate(Vector3.up * giro * velocidadeGiro * Time.deltaTime, Space.World);
            }
        }

        /// <summary>
        /// Desloca o Pac-Man na direção em que está olhando, permitindo curvar em esquinas e deslizar nas paredes.
        /// </summary>
        public void ProcessarMovimento()
        {
            float vertical = 0f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) vertical += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) vertical -= 1f;

            Vector3 direcaoDesejada = transform.forward * vertical;

            if (m_CharacterController == null)
            {
                GarantirCharacterController();
            }

            if (m_CharacterController != null)
            {
                if (!m_CharacterController.enabled && gameObject.activeInHierarchy)
                {
                    m_CharacterController.enabled = true;
                }

                // Barreira preditiva contra penetração em paredes com deslizamento no plano do muro
                Vector3 direcaoHorizontal = new Vector3(direcaoDesejada.x, 0f, direcaoDesejada.z);
                if (direcaoHorizontal.sqrMagnitude > 0.001f)
                {
                    float distVerificacao = (velocidadeMovimento * Time.deltaTime) + m_CharacterController.radius + 0.08f;
                    Vector3 centroCapsula = transform.position + new Vector3(0f, m_CharacterController.height / 2f, 0f);

                    if (Physics.SphereCast(centroCapsula, m_CharacterController.radius * 0.95f, direcaoHorizontal.normalized, out RaycastHit hit, distVerificacao, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (hit.collider != null && !hit.collider.isTrigger)
                        {
                            Vector3 normalParede = hit.normal;
                            normalParede.y = 0f;
                            normalParede.Normalize();

                            direcaoHorizontal = Vector3.ProjectOnPlane(direcaoHorizontal, normalParede);
                            direcaoDesejada = direcaoHorizontal;
                        }
                    }
                }

                if (m_CharacterController.isGrounded)
                {
                    m_VelocidadeVertical = -4.0f;
                }
                else
                {
                    m_VelocidadeVertical -= gravidade * Time.deltaTime;
                }

                Vector3 movimentoFinal = (direcaoDesejada * velocidadeMovimento) + (Vector3.up * m_VelocidadeVertical);
                m_CharacterController.Move(movimentoFinal * Time.deltaTime);
            }
        }
    }
}
