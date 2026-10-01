using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ============================================================================
/// PAC-MAN SEMÂNTICO (Semantic Pac-Man) - Modelagem Orientada a Objetos (UML/C++)
/// ============================================================================
/// Interface que define o contrato de uma Instância Ontológica (Indivíduo da ABox).
/// Alinhada com os diagramas de classes PlantUML e a estrutura do Backend C++.
/// ============================================================================
/// </summary>
public interface IInstanciaSemantica
{
    /// <summary>
    /// Identificador único do indivíduo na ontologia (ex: pac_gema_01, ind_obstaculo_02).
    /// </summary>
    string Id { get; set; }

    /// <summary>
    /// Nome do Conceito ou Classe Ontológica da TBox (ex: GemaDiamante, FantasmaCorrompido).
    /// </summary>
    string NomeClasse { get; set; }

    /// <summary>
    /// Avaliação lógica: indica se o indivíduo satisfaz a regra/axioma de Description Logic.
    /// </summary>
    bool EhValida { get; set; }

    /// <summary>
    /// Expressão formal em Description Logic (DL) (ex: Cristal ⊓ ∃temPureza.Alta).
    /// </summary>
    string ExpressaoDL { get; set; }

    /// <summary>
    /// Descrição textual amigável da regra para exibição na interface VR/AR.
    /// </summary>
    string DescricaoSemantica { get; set; }

    /// <summary>
    /// Valor de pontuação associado à coleta ou colisão.
    /// </summary>
    int ValorPontuacao { get; set; }

    /// <summary>
    /// Posição 3D da instância no espaço do labirinto.
    /// </summary>
    Vector3 Posicao { get; set; }

    /// <summary>
    /// Cor de destaque visual associada à classe semântica.
    /// </summary>
    Color CorDestaque { get; set; }

    /// <summary>
    /// Dicionário de propriedades ontológicas (Object Properties e Data Properties).
    /// </summary>
    IReadOnlyDictionary<string, string> PropriedadesDL { get; }

    /// <summary>
    /// Inicializa a instância a partir dos dados DTO fornecidos pelo backend C++ ou JSON.
    /// </summary>
    void InicializarSemantica(DadoInstanciaSemantica dados);

    /// <summary>
    /// Executa a interação espacial (coleta física por toque ou disparo de raycast XR).
    /// </summary>
    void Interagir(GameObject interator, bool ehRaycast = false);

    /// <summary>
    /// Altera o destaque visual quando a instância é focada pelo feixe de laser XR.
    /// </summary>
    void DefinirFocoVisual(bool focado);
}
