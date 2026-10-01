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
    /// Simula as classes e restrições Description Logic (DL) que o backend C++ processa para o Pac-Man Semântico.
    /// </summary>
    public static CenarioSemanticoDTO GerarMockOntologiaCpp(int qtdValidos, int qtdInvalidos, float raio, float altura)
    {
        CenarioSemanticoDTO cenario = new CenarioSemanticoDTO
        {
            nomeOntologia = "PacMan_SemanticOntology_DL",
            versaoAxiomas = "ALCQ(D)",
            ontologiaConsistente = true,
            tempoInferencaMs = 3.5f
        };

        string[] classesValidas = new string[]
        {
            "GemaDiamante", "MineralMagico", "EntidadeConforme", "CristalPuro", "AxiomaConsistente"
        };
        string[] expressoesDLValidas = new string[]
        {
            "Cristal ⊓ ∃temEnergia.Pura ⊓ ≥1 valorPositivo",
            "GemaValida ⊑ EntidadeSegura ⊓ ∀interacao.Permitida",
            "Recurso ⊓ {valido} ⊓ ¬Perigoso",
            "InstanciaSatisfeita ⊑ ∃raciocinio.Aprovado"
        };

        string[] classesInvalidas = new string[]
        {
            "FantasmaCorrompido", "ObstaculoSemantico", "RestricaoViolada", "AntiMateria", "AxiomaInconsistente"
        };
        string[] expressoesDLInvalidas = new string[]
        {
            "Obstaculo ⊑ Perigo ⊓ ¬Valido",
            "EntidadeCorrompida ⊓ ≤0 compatibilidade",
            "Inconsistencia ⊑ A ⊓ ¬A (Bottom ⊥)",
            "ViolacaoCardinalidade ⊑ >5 nivelAmeaca"
        };

        for (int i = 0; i < qtdValidos; i++)
        {
            Vector2 circulo = UnityEngine.Random.insideUnitCircle * raio;
            string classe = classesValidas[i % classesValidas.Length];
            string dl = expressoesDLValidas[i % expressoesDLValidas.Length];

            cenario.instancias.Add(new DadoInstanciaSemantica
            {
                id = $"pac_val_{i + 1:00}",
                classeOntologica = classe,
                expressaoDL = dl,
                ehValida = true,
                posicao = new Vector3(circulo.x, altura, circulo.y),
                valorPontuacao = 10,
                descricaoSemantica = $"[DL VÁLIDA] {classe}: Satisfaz axioma '{dl}'. Coleta autorizada!",
                corDestaque = new Color(0.1f, 1f, 0.45f)
            });
        }

        for (int i = 0; i < qtdInvalidos; i++)
        {
            Vector2 circulo = UnityEngine.Random.insideUnitCircle * raio;
            string classe = classesInvalidas[i % classesInvalidas.Length];
            string dl = expressoesDLInvalidas[i % expressoesDLInvalidas.Length];

            cenario.instancias.Add(new DadoInstanciaSemantica
            {
                id = $"pac_obs_{i + 1:00}",
                classeOntologica = classe,
                expressaoDL = dl,
                ehValida = false,
                posicao = new Vector3(circulo.x, altura, circulo.y),
                valorPontuacao = -5,
                descricaoSemantica = $"[DL VIOLAÇÃO] {classe}: Viola restrição ontológica '{dl}'. Obstáculo ativo!",
                corDestaque = new Color(1f, 0.15f, 0.2f)
            });
        }

        return cenario;
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
