using System;
using UnityEngine;
using PacMan.Semantics;

namespace PacMan.XR
{
    /// <summary>
    /// ============================================================================
    /// PAC-MAN SEMÂNTICO (CIn-UFPE) - Deteção de Colisão e Interação Espacial XR
    /// ============================================================================
    /// Componente integrado no XR Origin / Jogador para gerir a deteção física e
    /// de gatilhos (triggers) com as gemas do labirinto procedural, comunicando
    /// os eventos de consumo diretamente ao MotorRaciocinioSemantico.
    /// ============================================================================
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("Pac-Man Semântico/XR/Interação XR Colisão")]
    public class InteracaoXRColisao : MonoBehaviour
    {
        [Header("Referências Semânticas")]
        [Tooltip("Referência ao motor central de raciocínio ontológico.")]
        public MotorRaciocinioSemantico motorSemantico;

        [Header("Configuração de Feedback")]
        [Tooltip("Efeito sonoro disparado ao consumir uma gema no labirinto.")]
        public AudioClip somConsumoGema;

        private AudioSource audioSource;

        private void Start()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.spatialBlend = 0f; // 2D som direto no headset
                audioSource.playOnAwake = false;
            }

            if (motorSemantico == null)
            {
                motorSemantico = FindFirstObjectByType<MotorRaciocinioSemantico>();
            }
        }

        // Suporte a colisões físicas do XR Origin / Character Controller e Ray Interactors
        private void OnTriggerEnter(Collider other)
        {
            if (other != null)
            {
                ProcessarInteracaoOntologica(other.gameObject);
            }
        }

        // Suporte alternativo para interações baseadas em XR Direct/Ray Interactors (trigger colliders)
        private void OnCollisionEnter(Collision collision)
        {
            if (collision != null && collision.gameObject != null)
            {
                ProcessarInteracaoOntologica(collision.gameObject);
            }
        }

        /// <summary>
        /// Processa a interação ontológica com a entidade colidida no labirinto procedural.
        /// </summary>
        public void ProcessarInteracaoOntologica(GameObject alvo)
        {
            if (alvo == null) return;

            // Verifica se o objeto tocado é uma Gema baseada no grafo ontológico
            if (alvo.CompareTag("GemaValida") || alvo.name.Contains("Gema") || alvo.name.Contains("Gem"))
            {
                Debug.Log($"[InteracaoXR] Gema ontológica '{alvo.name}' consumida via XR.");

                // Comunica com o MotorRaciocinioSemantico se for uma gema especial (Power Pellet)
                bool ehGemaEspecial = alvo.name.IndexOf("Special", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                      alvo.name.IndexOf("Power", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                      alvo.name.IndexOf("Pellet", StringComparison.OrdinalIgnoreCase) >= 0;

                if (motorSemantico != null && ehGemaEspecial)
                {
                    motorSemantico.AtivarPowerPellet(8.0f);
                }

                // Dispara feedback sonoro se disponível
                if (somConsumoGema != null && audioSource != null)
                {
                    audioSource.PlayOneShot(somConsumoGema);
                }

                // Efetua interação polimórfica (juice, pontuação) ou destrói visualmente o item no labirinto procedural
                IInstanciaSemantica inst = alvo.GetComponent<IInstanciaSemantica>();
                if (inst is InstanciaSemanticaBase instBase)
                {
                    instBase.Interagir(gameObject, ehRaycast: false);
                }
                else
                {
                    Destroy(alvo);
                }
            }
            else if (alvo.CompareTag("ObstaculoInvalido") || alvo.name.Contains("Parede"))
            {
                Debug.LogWarning($"[InteracaoXR] Colisão com barreira espacial detetada: {alvo.name}");
            }
        }
    }
}
