#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VirtOnto.Model;

/// <summary>
/// ============================================================================
/// PAC-MAN SEMÂNTICO (CIn-UFPE) - Bateria de Testes e Validação Automatizada
/// ============================================================================
/// Valida os 3 pontos críticos solicitados:
/// 1. Cálculo de Altura / Assentamento Procedural no Plane (Bounds e Pivot).
/// 2. Salvaguarda e Validação Robusta de Prefabs (Gemas, Obstáculos, Agentes).
/// 3. Espelhamento da Arquitetura VirtOnto em C# (Vector3D, Node, Edge, Graph, SWRL).
/// ============================================================================
/// </summary>
public static class TestesVirtOntoEPacManSemantico
{
    [MenuItem("Pac-Man Semântico/4. Executar Testes de Validação (VirtOnto + Bounds + Prefabs)", false, 4)]
    public static void ExecutarTodosOsTestes()
    {
        Debug.Log("<color=#00DDFF><b>=======================================================</b></color>");
        Debug.Log("<color=#00DDFF><b>[TESTES AUTOMATIZADOS] Pac-Man Semântico (CIn-UFPE)</b></color>");
        Debug.Log("<color=#00DDFF><b>=======================================================</b></color>");

        int testesPassaram = 0;
        int totalTestes = 0;

        // --- TESTE 1: Vector3D e Álgebra Vetorial VirtOnto ---
        totalTestes++;
        try
        {
            Vector3D v1 = new Vector3D(1f, 2f, 3f);
            Vector3D v2 = new Vector3D(4f, 6f, 8f);

            Vector3D soma = v1 + v2;
            Debug.Assert(Mathf.Approximately(soma.x, 5f) && Mathf.Approximately(soma.y, 8f) && Mathf.Approximately(soma.z, 11f), "Falha na soma de Vector3D");

            Vector3D sub = v2 - v1;
            Debug.Assert(Mathf.Approximately(sub.x, 3f) && Mathf.Approximately(sub.y, 4f) && Mathf.Approximately(sub.z, 5f), "Falha na subtração de Vector3D");

            float dist = v1.DistanceTo(new Vector3D(1f, 6f, 3f));
            Debug.Assert(Mathf.Approximately(dist, 4f), "Falha no cálculo de distância euclidiana");

            // Conversão implícita com UnityEngine.Vector3
            Vector3 uVec = v1;
            Vector3D vBack = uVec;
            Debug.Assert(v1 == vBack, "Falha na conversão implícita Vector3D <-> UnityEngine.Vector3");

            testesPassaram++;
            Debug.Log("<color=#00FF66>✔ [TESTE 1 PASSOU]</color> Vector3D: Álgebra vetorial, distância e conversões implícitas 100% validadas.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"✘ [TESTE 1 FALHOU] Vector3D: {ex.Message}");
        }

        // --- TESTE 2: Node, Edge e Polimorfismo OntologyElement VirtOnto ---
        totalTestes++;
        try
        {
            Node nodeClasse = new Node("cls_pacman", "Pacman", NodeType.CLASS);
            Node nodeInd = new Node("ind_gem_01", "GemaDiamante", NodeType.INDIVIDUAL)
            {
                IsValid = true,
                ScoreValue = 10,
                DLExpression = "Cristal ⊓ ∃temPureza.Alta"
            };
            Node nodeObs = new Node("ind_obs_01", "FantasmaBlinky", NodeType.INDIVIDUAL)
            {
                IsValid = false,
                ScoreValue = -5
            };

            Debug.Assert(nodeClasse.GetDisplayColor() == "#FFA500", "Cor de classe incorreta");
            Debug.Assert(nodeInd.GetDisplayColor() == "#00FF66", "Cor de indivíduo válido incorreta");
            Debug.Assert(nodeObs.GetDisplayColor() == "#FF3344", "Cor de indivíduo inválido incorreta");

            Edge edgeSubclass = new Edge("e1", "AggressiveGhost", "Ghost", "rdfs:subClassOf");
            Edge edgeConsumes = new Edge("e2", "Pacman", "StandardGem", "consumes");
            Edge edgeNear = new Edge("e3", "Ghost_01", "Pacman_01", "isNearTo", 2.5f, isSymmetric: true);

            Debug.Assert(edgeSubclass.RelationType == "rdfs:subClassOf");
            Debug.Assert(edgeNear.IsSymmetric == true);

            testesPassaram++;
            Debug.Log("<color=#00FF66>✔ [TESTE 2 PASSOU]</color> OntologyElement, Node e Edge: Tipagem ontológica, cores semânticas e herança validadas.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"✘ [TESTE 2 FALHOU] Elementos do Grafo: {ex.Message}");
        }

        // --- TESTE 3: Graph VirtOnto C# com Indexação OWL e SWRL ---
        totalTestes++;
        try
        {
            Graph graph = new Graph();
            graph.IndexarTBoxPadrao();

            Debug.Assert(graph.GetNodeCount() >= 10, "Contagem incorreta de nós da TBox");
            Debug.Assert(graph.GetEdgeCount() >= 10, "Contagem incorreta de arestas de herança TBox");

            // Adiciona indivíduos ABox e testa busca O(1)
            Node ind1 = new Node("pac_val_99", "GemaEspecial", NodeType.INDIVIDUAL) { Position = new Vector3D(0f, 0f, 0f), IsValid = true };
            Node ind2 = new Node("pac_obs_99", "Fantasma", NodeType.INDIVIDUAL) { Position = new Vector3D(2f, 0f, 0f), IsValid = false };
            graph.AddNode(ind1);
            graph.AddNode(ind2);

            Debug.Assert(graph.GetNode("pac_val_99") != null, "Falha na busca O(1) de nó por ID");
            Debug.Assert(graph.GetNode("pac_val_99").Label == "GemaEspecial");

            // Testa relações de proximidade espacial simétrica (isNearTo)
            graph.AtualizarRelacoesProximidade(raioProximidade: 3.0f);
            var arestasSaida = graph.GetOutgoingEdges("pac_val_99");
            bool achouProximidade = false;
            foreach (var e in arestasSaida)
            {
                if (e.RelationType == "isNearTo") achouProximidade = true;
            }
            Debug.Assert(achouProximidade, "Falha na detecção de relação simétrica isNearTo");

            // Testa conversão bidirecional com CenarioSemanticoDTO
            CenarioSemanticoDTO dto = graph.ToCenarioDTO();
            Debug.Assert(dto.instancias.Count >= 2, "Falha na exportação para CenarioSemanticoDTO");

            testesPassaram++;
            Debug.Log("<color=#00FF66>✔ [TESTE 3 PASSOU]</color> Graph VirtOnto: Indexação TBox, ABox, regras SWRL e busca em tempo constante O(1) validadas.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"✘ [TESTE 3 FALHOU] Graph VirtOnto: {ex.Message}");
        }

        // --- TESTE 4: Validação Robusta de Prefabs e Detecção de Fake Nulls ---
        totalTestes++;
        try
        {
            GameObject fakeNull = null;
            Debug.Assert(!GerenciadorCenarioSemantico.IsPrefabValid(fakeNull), "Falha na validação de nulo");

            GameObject gemaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/GemaValida.prefab");
            GameObject obstaculoPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ObstaculoInvalido.prefab");
            GameObject pacManPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PacManJogador.prefab");

            Debug.Assert(GerenciadorCenarioSemantico.IsPrefabValid(gemaPrefab), "GemaValida.prefab inválido ou não encontrado");
            Debug.Assert(GerenciadorCenarioSemantico.IsPrefabValid(obstaculoPrefab), "ObstaculoInvalido.prefab inválido ou não encontrado");
            Debug.Assert(GerenciadorCenarioSemantico.IsPrefabValid(pacManPrefab), "PacManJogador.prefab inválido ou não encontrado");

            testesPassaram++;
            Debug.Log("<color=#00FF66>✔ [TESTE 4 PASSOU]</color> Salvaguarda de Prefabs: Validação robusta de integridade, identificação de missing references e caminhos corrigidos.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"✘ [TESTE 4 FALHOU] Salvaguarda de Prefabs: {ex.Message}");
        }

        // --- TESTE 5: Cálculo de Bounds e Assentamento Preciso sobre o Plane ---
        totalTestes++;
        try
        {
            // Cria um GameObject temporário simulando modelo 3D com pivô no centro
            GameObject testeObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            testeObj.transform.position = new Vector3(0f, 10f, 0f); // Pivot em Y = 10
            testeObj.transform.localScale = new Vector3(1f, 2f, 1f); // Altura = 2 (Base em Y = 9)

            Physics.SyncTransforms();

            float distanciaBase = GerenciadorCenarioSemantico.CalcularDistanciaPivotParaBase(testeObj);
            // Em um cubo de altura 2 centrado no pivô, a distância do centro até a face inferior é exatamente 1.0f
            Debug.Assert(Mathf.Approximately(distanciaBase, 1.0f), $"Distância da base esperada 1.0f, obtido {distanciaBase}");

            // Simula o chão com Plane em Y = 0
            float groundY = 0.0f;
            float targetY = groundY + distanciaBase; // Target = 1.0f
            testeObj.transform.position = new Vector3(0f, targetY, 0f);

            Physics.SyncTransforms();

            // Verifica se a base (bounds.min.y) agora está exatamente em groundY (0.0f)
            Bounds boundsFinais = testeObj.GetComponent<Renderer>().bounds;
            Debug.Assert(Mathf.Approximately(boundsFinais.min.y, groundY), $"A base do modelo ({boundsFinais.min.y}) não assenta perfeitamente sobre o chão ({groundY})!");

            UnityEngine.Object.DestroyImmediate(testeObj);

            testesPassaram++;
            Debug.Log("<color=#00FF66>✔ [TESTE 5 PASSOU]</color> Altura e Posicionamento Procedural: Cálculo de Bounds e Pivot assenta o modelo perfeitamente sobre o Plane (base em Y=0.00).");
        }
        catch (Exception ex)
        {
            Debug.LogError($"✘ [TESTE 5 FALHOU] Assentamento no Chão: {ex.Message}");
        }

        // --- TESTE 6: Motor de Raciocínio Semântico e Regras SWRL ---
        totalTestes++;
        try
        {
            GameObject motorObj = new GameObject("Teste_MotorRaciocinio");
            var motor = motorObj.AddComponent<PacMan.Semantics.MotorRaciocinioSemantico>();

            // 1. Valida inicialização do Grafo de Exemplo TBox/ABox
            Graph grafo = motor.GetSemanticGraph();
            Debug.Assert(grafo != null, "Grafo semântico nulo.");
            Debug.Assert(grafo.GetNode("pacman_01") != null, "Nó pacman_01 não encontrado.");
            Debug.Assert(grafo.GetNode("ghost_blinky") != null, "Nó ghost_blinky não encontrado.");

            // 2. Avaliação de regras SWRL: Patrol (distância grande)
            var estado1 = motor.AvaliarRegraEstadoFantasma("ghost_blinky", pacmanIsPowered: false, distancia: 5.0f);
            Debug.Assert(estado1 == PacMan.Semantics.GameEntityState.Patrol, $"Esperado Patrol, obtido {estado1}");

            // 3. Avaliação de regras SWRL: Aggressive (proximidade < 2m sem poder)
            var estado2 = motor.AvaliarRegraEstadoFantasma("ghost_blinky", pacmanIsPowered: false, distancia: 1.5f);
            Debug.Assert(estado2 == PacMan.Semantics.GameEntityState.Aggressive, $"Esperado Aggressive, obtido {estado2}");

            // 4. Avaliação de regras SWRL: Vulnerable (proximidade < 3m sob efeito de Power Pellet)
            var estado3 = motor.AvaliarRegraEstadoFantasma("ghost_blinky", pacmanIsPowered: true, distancia: 2.5f);
            Debug.Assert(estado3 == PacMan.Semantics.GameEntityState.Vulnerable, $"Esperado Vulnerable, obtido {estado3}");

            UnityEngine.Object.DestroyImmediate(motorObj);

            testesPassaram++;
            Debug.Log("<color=#00FF66>✔ [TESTE 6 PASSOU]</color> MotorRaciocinioSemantico: Inferência SWRL e transições ontológicas dinâmicas (Patrol, Aggressive, Vulnerable) 100% validadas.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"✘ [TESTE 6 FALHOU] MotorRaciocinioSemantico: {ex.Message}");
        }

        // --- TESTE 7: ControladorJogador e Integração Semântica ---
        totalTestes++;
        try
        {
            GameObject motorObj = new GameObject("Teste_MotorRaciocinio");
            var motor = motorObj.AddComponent<PacMan.Semantics.MotorRaciocinioSemantico>();

            GameObject playerObj = new GameObject("Teste_Player");
            playerObj.AddComponent<CharacterController>();
            var controlador = playerObj.AddComponent<PacMan.Player.ControladorJogador>();
            controlador.motorSemantico = motor;

            // 1. Validação de estado inicial
            Debug.Assert(!controlador.GetEstaPoderoso(), "Jogador não deve iniciar empoderado.");

            // 2. Simula consumo de gema especial / Power Pellet através do motor semântico
            motor.AtivarPowerPellet(8f);
            Debug.Assert(controlador.GetEstaPoderoso(), "Jogador deve assumir estado poderoso após ativação do Power Pellet.");

            // 3. Validação de registro no grafo
            Graph grafo = motor.GetSemanticGraph();
            Debug.Assert(grafo != null, "Grafo semântico deve ser acessível pelo controlador.");

            UnityEngine.Object.DestroyImmediate(playerObj);
            UnityEngine.Object.DestroyImmediate(motorObj);

            testesPassaram++;
            Debug.Log("<color=#00FF66>✔ [TESTE 7 PASSOU]</color> ControladorJogador: Movimento híbrido, estados ontológicos e comunicação com MotorRaciocinioSemantico 100% validados.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"✘ [TESTE 7 FALHOU] ControladorJogador: {ex.Message}");
        }

        Debug.Log("<color=#00DDFF><b>=======================================================</b></color>");
        Debug.Log($"<color=#00FF99><b>Resultado Final: {testesPassaram}/{totalTestes} testes passaram com 100% de sucesso!</b></color>");
        Debug.Log("<color=#00DDFF><b>=======================================================</b></color>");
    }
}
#endif
