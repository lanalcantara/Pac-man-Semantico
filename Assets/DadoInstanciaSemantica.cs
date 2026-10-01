using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ============================================================================
/// PAC-MAN SEMÂNTICO (Semantic Pac-Man) - Modelagem Orientada a Objetos (UML/C++)
/// ============================================================================
/// Estrutura para propriedades individuais de ontologia (Object e Data Properties).
/// ============================================================================
/// </summary>
[Serializable]
public struct PropriedadeOntologica
{
    [Tooltip("Nome/URI da propriedade (ex: temEnergia, nivelPerigo, pertenceACategoria).")]
    public string chave;

    [Tooltip("Valor da propriedade (ex: Pura, Alto, MineralMagico).")]
    public string valor;

    public PropriedadeOntologica(string chave, string valor)
    {
        this.chave = chave;
        this.valor = valor;
    }
}

/// <summary>
/// Estrutura de dados formal (DTO) que representa uma instância da ontologia.
/// Totalmente alinhada com as classes C++ e diagramas PlantUML.
/// </summary>
[Serializable]
public class DadoInstanciaSemantica
{
    [Tooltip("Identificador único da instância (ex: pac_gema_01, ind_obstaculo_02).")]
    public string id = Guid.NewGuid().ToString().Substring(0, 8);

    [Tooltip("Nome da classe ontológica na TBox (ex: GemaDiamante, ObstaculoCorrompido).")]
    public string classeOntologica = "InstanciaGenerica";

    [Tooltip("Expressão formal de Description Logic (DL) associada.")]
    public string expressaoDL = "⊤";

    [Tooltip("Indica se o raciocinador semântico (C++ reasoner) considerou esta instância VÁLIDA (satisfaz a TBox/ABox).")]
    public bool ehValida = true;

    [Tooltip("Posição 3D da gema/obstáculo no labirinto.")]
    public Vector3 posicao = Vector3.zero;

    [Tooltip("Rotação 3D da instância.")]
    public Quaternion rotacao = Quaternion.identity;

    [Tooltip("Escala 3D.")]
    public Vector3 escala = Vector3.one;

    [Tooltip("Pontos concedidos ou penalizados na coleta do Pac-Man.")]
    public int valorPontuacao = 10;

    [Tooltip("Descrição semântica detalhada para exibição em hologramas/HUD espacial.")]
    public string descricaoSemantica = "Instância ontológica do Pac-Man Semântico.";

    [Tooltip("Cor representativa da classe ontológica (RGBA).")]
    public Color corDestaque = Color.green;

    [Tooltip("Lista de propriedades ontológicas (Object/Data Properties).")]
    public List<PropriedadeOntologica> propriedades = new List<PropriedadeOntologica>();

    [Tooltip("Axiomas ou parâmetros adicionais de inferência.")]
    public List<string> axiomasRelacionados = new List<string>();
}

/// <summary>
/// Contêiner DTO para transferência completa do cenário ontológico
/// entre o Backend C++, arquivos JSON e o Unity.
/// </summary>
[Serializable]
public class CenarioSemanticoDTO
{
    [Tooltip("Nome da Ontologia (ex: OntologiaPacManSemantico, ALCQ_Maze).")]
    public string nomeOntologia = "OntologiaPacManSemantico";

    [Tooltip("Versão do esquema ou timestamp da inferência.")]
    public string versaoAxiomas = "v1.0.0-DL";

    [Tooltip("Tempo de processamento do raciocinador em milissegundos.")]
    public float tempoInferencaMs = 0f;

    [Tooltip("Indica se a ontologia é consistente (sem contradições lógicas).")]
    public bool ontologiaConsistente = true;

    [Tooltip("Lista de todas as instâncias a serem instanciadas no labirinto.")]
    public List<DadoInstanciaSemantica> instancias = new List<DadoInstanciaSemantica>();
}

/// <summary>
/// Interface para provedores de ontologia do Pac-Man Semântico.
/// </summary>
public interface IProvedorOntologia
{
    CenarioSemanticoDTO ObterCenario(int qtdValidos, int qtdInvalidos, float raio, float altura);
}
