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
            m_CharacterController = GetComponent<CharacterController>();
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
        /// Desloca o Pac-Man na direção em que está olhando, permitindo curvar em esquinas.
        /// </summary>
        public void ProcessarMovimento()
        {
            float vertical = 0f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) vertical += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) vertical -= 1f;

            Vector3 direcaoDesejada = transform.forward * vertical;

            if (m_CharacterController != null && m_CharacterController.enabled)
            {
                if (m_CharacterController.isGrounded)
                {
                    m_VelocidadeVertical = -0.5f;
                }
                else
                {
                    m_VelocidadeVertical -= gravidade * Time.deltaTime;
                }

                Vector3 movimentoFinal = (direcaoDesejada * velocidadeMovimento) + (Vector3.up * m_VelocidadeVertical);
                m_CharacterController.Move(movimentoFinal * Time.deltaTime);
            }
            else
            {
                transform.Translate(direcaoDesejada * velocidadeMovimento * Time.deltaTime, Space.World);
            }
        }
    }
}
