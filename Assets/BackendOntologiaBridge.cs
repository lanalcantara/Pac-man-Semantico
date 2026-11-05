using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ============================================================================
/// INTERFACE ESPACIAL BIDIRECIONAL PARA ENGENHARIA DE ONTOLOGIAS OWL
/// ============================================================================
/// Ponte de comunicação e integração entre o Unity C# e o plugin nativo
/// Java/OSGi no Protégé (Servidor WebSocket porta 8085) e parser OWL/DL.
/// 
/// Nota Arquitetural:
/// O antigo backend legado em C++ (P/Invoke DLL) foi integralmente removido.
/// A comunicação em tempo real ocorre via ProtegeWebSocketBridge (ws://localhost:8085).
/// ============================================================================
/// </summary>
public static class BackendOntologiaBridge
{
    /// <summary>
    /// Gera o cenário ontológico fundamentado na Ontologia de Parasitologia (Protégé / OWL ALCQ(D)).
    /// Modela espécies, hospedeiros, habitats, vias de transmissão e restrições biológicas.
    /// </summary>
    public static CenarioSemanticoDTO GerarCenarioOntologiaPadrao(int qtdValidos, int qtdInvalidos, float raio, float altura)
    {
        CenarioSemanticoDTO cenario = new CenarioSemanticoDTO
        {
            nomeOntologia = "OntologiaParasitologia_Protege_OWL",
            versaoAxiomas = "ALCQ(D)-ProtegeWS-8085",
            ontologiaConsistente = true,
            tempoInferencaMs = 2.45f
        };

        var catalogoValidos = new (string classe, string dl, int pontos, string desc, Color cor, (string k, string v)[] props)[]
        {
            (
                "AscarisLumbricoides",
                "Nematoda ⊓ ∃has_host.Human ⊓ ∃has_habitat.SmallIntestine ⊓ ∃has_transmission.FecalOral",
                25,
                "Ascaris lumbricoides: Helminto nematódeo parasita do intestino delgado humano transmitido por via fecal-oral.",
                new Color(0.0f, 1.0f, 0.8f),
                new (string, string)[] { ("has_host", "Human"), ("has_habitat", "SmallIntestine"), ("has_transmission", "FecalOral"), ("has_morphology", "Cylindrical") }
            ),
            (
                "EnterobiusVermicularis",
                "Nematoda ⊓ ∃has_host.Human ⊓ ∃has_habitat.PerianalRegion ⊓ ∃has_transmission.Autoinfection",
                20,
                "Enterobius vermicularis (oxiúro): Helminto cilíndrico associado a prurido perianal e ciclo de autoinfecção.",
                new Color(0.2f, 0.75f, 1.0f),
                new (string, string)[] { ("has_host", "Human"), ("has_habitat", "PerianalRegion"), ("has_transmission", "Autoinfection"), ("has_morphology", "Cylindrical") }
            ),
            (
                "TaeniaSolium",
                "Platyhelminthes ⊓ ∃has_host.Human ⊓ ∃has_habitat.SmallIntestine ⊓ ∃has_transmission.UndercookedPork",
                30,
                "Taenia solium: Platelminto segmentado achatado do intestino delgado humano com transmissão suína.",
                new Color(1.0f, 0.84f, 0.0f),
                new (string, string)[] { ("has_host", "Human"), ("has_habitat", "SmallIntestine"), ("has_transmission", "UndercookedPork"), ("has_morphology", "FlatSegments") }
            ),
            (
                "AntiparasiticTreatment",
                "Treatment ⊓ ∃neutralizes.InfectionVector",
                50,
                "Tratamento antiparasitário específico. Aciona a Regra SWRL 1, tornando os vetores hostis vulneráveis para neutralização!",
                new Color(0.95f, 0.35f, 1.0f),
                new (string, string)[] { ("isPowerPellet", "true"), ("action", "VulnerabilizeVectors") }
            ),
            (
                "Nematoda",
                "Parasite ⊓ ∃has_morphology.Cylindrical",
                15,
                "Nematoda: Espécime de helminto cilíndrico com cutícula protetora e simetria bilateral.",
                new Color(0.65f, 0.55f, 0.98f),
                new (string, string)[] { ("has_morphology", "Cylindrical") }
            ),
            (
                "Platyhelminthes",
                "Parasite ⊓ ∃has_morphology.FlatSegments",
                15,
                "Platyhelminthes: Espécime de verme achatado dorsoventralmente com proglotes características.",
                new Color(0.95f, 0.45f, 0.71f),
                new (string, string)[] { ("has_morphology", "FlatSegments") }
            )
        };

        var catalogoInvalidos = new (string classe, string dl, int pontos, string desc, Color cor, (string k, string v)[] props)[]
        {
            (
                "InconsistentParasiteSpecimen",
                "Parasite ⊓ ∃has_morphology.(Cylindrical ⊓ FlatSegments) ≡ ⊥",
                -15,
                "Inconsistência ontológica taxonômica: Morfologia mutuamente exclusiva viola axiomas ALCQ(D).",
                new Color(1.0f, 0.15f, 0.2f),
                new (string, string)[] { ("violation", "MutuallyExclusiveMorphology") }
            ),
            (
                "HostMismatchViolation",
                "AscarisLumbricoides ⊓ ∃has_host.InvertebrateVector ⊑ Inconsistency",
                -10,
                "Incompatibilidade taxonômica de hospedeiro/vetor de transmissão detectada pelo raciocinador.",
                new Color(0.9f, 0.1f, 0.3f),
                new (string, string)[] { ("violation", "IncompatibleHost") }
            ),
            (
                "HabitatInconsistency",
                "EnterobiusVermicularis ⊓ ∃has_habitat.BloodStream ⊑ Inconsistency",
                -10,
                "Incompatibilidade anatômica de habitat do parasita (tecido sanguíneo em vez de perianal).",
                new Color(1.0f, 0.3f, 0.1f),
                new (string, string)[] { ("violation", "InvalidHabitat") }
            )
        };

        for (int i = 0; i < qtdValidos; i++)
        {
            Vector2 circulo = UnityEngine.Random.insideUnitCircle * raio;
            var item = catalogoValidos[i % catalogoValidos.Length];

            var dado = new DadoInstanciaSemantica
            {
                id = $"inst_parasite_{i + 1:00}",
                classeOntologica = item.classe,
                expressaoDL = item.dl,
                ehValida = true,
                posicao = new Vector3(circulo.x, altura, circulo.y),
                valorPontuacao = item.pontos,
                descricaoSemantica = item.desc,
                corDestaque = item.cor
            };

            foreach (var p in item.props)
            {
                dado.propriedades.Add(new PropriedadeOntologica(p.k, p.v));
            }

            cenario.instancias.Add(dado);
        }

        for (int i = 0; i < qtdInvalidos; i++)
        {
            Vector2 circulo = UnityEngine.Random.insideUnitCircle * raio;
            var item = catalogoInvalidos[i % catalogoInvalidos.Length];

            var dado = new DadoInstanciaSemantica
            {
                id = $"inst_violacao_{i + 1:00}",
                classeOntologica = item.classe,
                expressaoDL = item.dl,
                ehValida = false,
                posicao = new Vector3(circulo.x, altura, circulo.y),
                valorPontuacao = item.pontos,
                descricaoSemantica = item.desc,
                corDestaque = item.cor
            };

            foreach (var p in item.props)
            {
                dado.propriedades.Add(new PropriedadeOntologica(p.k, p.v));
            }

            cenario.instancias.Add(dado);
        }

        return cenario;
    }

    /// <summary>
    /// Carrega e converte dados de um arquivo .owl do Protégé diretamente em CenarioSemanticoDTO.
    /// </summary>
    public static CenarioSemanticoDTO CarregarDeOwl(string xmlContent)
    {
        VirtOnto.Model.Graph grafo = VirtOnto.Model.Graph.LoadFromOwlXml(xmlContent);
        return grafo.ToCenarioDTO();
    }

    public static CenarioSemanticoDTO CarregarDeJson(string json)
    {
        try
        {
            return JsonUtility.FromJson<CenarioSemanticoDTO>(json);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[OWL Bridge] Erro ao analisar JSON ontológico: {ex.Message}");
            return null;
        }
    }

    public static string ExportarParaJson(CenarioSemanticoDTO cenario, bool formatado = true)
    {
        return JsonUtility.ToJson(cenario, formatado);
    }
}
