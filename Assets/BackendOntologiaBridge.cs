using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// ============================================================================
/// PAC-MAN SEMÂNTICO (Semantic Pac-Man) - Realidade Virtual e Aumentada (RV/RA)
/// ============================================================================
/// Ponte de comunicação e integração entre o Unity C# e o Backend em C++
/// de Ontologias e Description Logic do Pac-Man Semântico.
/// 
/// Métodos de Integração Suportados:
/// 1. P/Invoke Nativo com DLL C++ (OntologiaBackendCpp.dll).
/// 2. Simulador de Contingência (Mock Reasoner ALCQ(D)).
/// 3. Desserialização e Exportação JSON.
/// ============================================================================
/// </summary>
public static class BackendOntologiaBridge
{
    // =========================================================================
    // 1. DEFINIÇÃO DE ESTRUTURA PARA INTEROPERABILIDADE NATIVA (C++ P/Invoke)
    // =========================================================================
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct InstanciaCppStruct
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string id;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string classeOntologica;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string expressaoDL;

        [MarshalAs(UnmanagedType.I1)]
        public bool ehValida;

        public float posX;
        public float posY;
        public float posZ;
        public int valorPontos;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string descricao;
    }

    // =========================================================================
    // 2. DECLARAÇÃO DE FUNÇÃO NATIVA C++ (DLL NATIVA)
    // =========================================================================
    [DllImport("OntologiaBackendCpp", CallingConvention = CallingConvention.Cdecl, EntryPoint = "RaciocinarOntologia")]
    private static extern int RaciocinarOntologiaNativo(
        int qtdValidos,
        int qtdInvalidos,
        float raio,
        float altura,
        [Out] InstanciaCppStruct[] outInstancias,
        int maxInstancias
    );

    /// <summary>
    /// Consulta o backend em C++ (DLL nativa) para gerar e classificar o labirinto do Pac-Man Semântico.
    /// Caso a DLL nativa não esteja no diretório de execução, ativa automaticamente o simulador de contingência.
    /// </summary>
    public static CenarioSemanticoDTO ConsultarBackendCpp(int qtdValidos, int qtdInvalidos, float raio, float altura)
    {
        CenarioSemanticoDTO cenario = new CenarioSemanticoDTO
        {
            nomeOntologia = "PacMan_CppBackend_ALCQ",
            versaoAxiomas = "v2.0-CppBridge",
            ontologiaConsistente = true
        };

        int totalEsperado = qtdValidos + qtdInvalidos;
        InstanciaCppStruct[] bufferNativo = new InstanciaCppStruct[totalEsperado];
        bool carregouDllNativa = false;

        try
        {
            int totalGerado = RaciocinarOntologiaNativo(qtdValidos, qtdInvalidos, raio, altura, bufferNativo, totalEsperado);
            if (totalGerado > 0)
            {
                carregouDllNativa = true;
                for (int i = 0; i < totalGerado; i++)
                {
                    InstanciaCppStruct s = bufferNativo[i];
                    cenario.instancias.Add(new DadoInstanciaSemantica
                    {
                        id = s.id,
                        classeOntologica = s.classeOntologica,
                        expressaoDL = s.expressaoDL,
                        ehValida = s.ehValida,
                        posicao = new Vector3(s.posX, s.posY, s.posZ),
                        valorPontuacao = s.valorPontos,
                        descricaoSemantica = s.descricao,
                        corDestaque = s.ehValida ? new Color(0.1f, 1f, 0.45f) : new Color(1f, 0.15f, 0.2f)
                    });
                }
                Debug.Log($"<color=#00DDFF><b>[Pac-Man Semântico - Backend C++]</b></color> {totalGerado} instâncias ontológicas inferidas com sucesso pela DLL nativa.");
            }
        }
        catch (DllNotFoundException)
        {
            Debug.Log("[Pac-Man Semântico - Backend C++] DLL nativa 'OntologiaBackendCpp.dll' não encontrada no diretório raiz. Utilizando motor de inferência de contingência (Mock DL).");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Pac-Man Semântico - Backend C++] Aviso durante chamada nativa: {ex.Message}. Utilizando motor de contingência.");
        }

        if (!carregouDllNativa)
        {
            cenario = GerarMockOntologiaCpp(qtdValidos, qtdInvalidos, raio, altura);
        }

        return cenario;
    }

    /// <summary>
    /// Gera o cenário ontológico rigorosamente fundamentado na Ontologia de Parasitologia (Protégé / OWL ALCQ(D)).
    /// Modela fielmente espécies, hospedeiros, habitats, vias de transmissão e restrições biológicas.
    /// </summary>
    public static CenarioSemanticoDTO GerarMockOntologiaCpp(int qtdValidos, int qtdInvalidos, float raio, float altura)
    {
        CenarioSemanticoDTO cenario = new CenarioSemanticoDTO
        {
            nomeOntologia = "OntologiaParasitologia_Protege_ALCQ",
            versaoAxiomas = "ALCQ(D)-Parasitology-v2.0",
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
            Debug.LogError($"[Pac-Man Semântico - Backend] Erro ao analisar JSON ontológico: {ex.Message}");
            return null;
        }
    }

    public static string ExportarParaJson(CenarioSemanticoDTO cenario, bool formatado = true)
    {
        return JsonUtility.ToJson(cenario, formatado);
    }
}
