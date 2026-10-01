using System;
using System.Collections.Generic;
using UnityEngine;
using VirtOnto.Model;

namespace PacMan.Integration
{
    [System.Serializable]
    public struct CoordenadaPontoSaidaDto
    {
        public string id;
        public float x;
        public float y;
        public float z;
    }

    /// <summary>
    /// ============================================================================
    /// PAC-MAN SEMÂNTICO (CIn-UFPE) - Leitor de Coordenadas e Bridge C++
    /// ============================================================================
    /// Componente adaptador responsável por receber e converter as coordenadas
    /// de saída geradas pelo módulo em C++ em nós individuais do grafo ontológico
    /// VirtOnto (Node, Vector3D) e sincronizá-las com o Gerenciador de Cenário.
    /// ============================================================================
    /// </summary>
    [AddComponentMenu("Pac-Man Semântico/Integration/Leitor Coordenadas C++")]
    public class LeitorCoordenadasCpp : MonoBehaviour
    {
        [Header("Configuração de Coordenadas C++")]
        [Tooltip("Lista de coordenadas de saída recebidas do módulo C++")]
        public List<CoordenadaPontoSaidaDto> pontosSaidaCpp = new List<CoordenadaPontoSaidaDto>();

        [Header("Integração com Cenário Semântico")]
        [Tooltip("Referência ao Gerenciador de Cenário Semântico.")]
        public GerenciadorCenarioSemantico gerenciadorCenario;

        private void Start()
        {
            if (gerenciadorCenario == null)
            {
                gerenciadorCenario = FindFirstObjectByType<GerenciadorCenarioSemantico>();
            }

            if (gerenciadorCenario != null && gerenciadorCenario.GrafoOntologico != null)
            {
                ProcessarPontosSaida(gerenciadorCenario.GrafoOntologico);
            }
        }

        /// <summary>
        /// Injeta os pontos de saída do C++ diretamente no Grafo Ontológico do Unity
        /// </summary>
        public void ProcessarPontosSaida(Graph grafoAlvo)
        {
            if (grafoAlvo == null) return;

            foreach (var ponto in pontosSaidaCpp)
            {
                Node nodePontoSaida = new Node(ponto.id, $"Saida_{ponto.id}", NodeType.Individual);
                nodePontoSaida.SetPosition(new Vector3D(ponto.x, ponto.y, ponto.z));
                nodePontoSaida.SetDisplayColor("#00FFFF"); // Ciano para pontos de saída C++

                grafoAlvo.AddNode(nodePontoSaida);
                
                Debug.Log($"[C++ Bridge] Ponto de saída '{ponto.id}' importado com sucesso na posição ({ponto.x}, {ponto.y}, {ponto.z}).");
            }
        }

        /// <summary>
        /// Context menu para sincronizar manualmente as coordenadas com o grafo ativo.
        /// </summary>
        [ContextMenu("Injetar Pontos C++ no Grafo")]
        public void InjetarPontosNoGrafoAtual()
        {
            if (gerenciadorCenario == null)
            {
                gerenciadorCenario = FindFirstObjectByType<GerenciadorCenarioSemantico>();
            }

            if (gerenciadorCenario != null && gerenciadorCenario.GrafoOntologico != null)
            {
                ProcessarPontosSaida(gerenciadorCenario.GrafoOntologico);
            }
            else
            {
                Debug.LogWarning("[C++ Bridge] GerenciadorCenarioSemantico ou GrafoOntologico não encontrado.");
            }
        }
    }
}
