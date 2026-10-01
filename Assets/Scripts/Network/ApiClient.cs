using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using VirtOnto.Model;

namespace PacMan.Network
{
    /// <summary>
    /// ============================================================================
    /// PAC-MAN SEMÂNTICO (CIn-UFPE) - Cliente de Rede para API Spring Boot
    /// ============================================================================
    /// Responsável pela comunicação assíncrona com o microsserviço Java Spring Boot
    /// (VirtOnto REST API), sincronizando o grafo ontológico (/nodes, /edges, /graph)
    /// com o GerenciadorCenarioSemantico para posicionamento procedural.
    /// ============================================================================
    /// </summary>
    [DisallowMultipleComponent]
    public class ApiClient : MonoBehaviour
    {
        [Header("Configurações do Microsserviço Java")]
        [Tooltip("URL base da API REST Spring Boot.")]
        public string baseUrl = "http://localhost:8080/api/ontology";

        [Tooltip("Testa a conectividade com o backend automaticamente no Start.")]
        public bool testarConexaoNoStart = true;

        [Tooltip("Sincroniza o grafo do backend Java automaticamente ao iniciar.")]
        public bool sincronizarNoStart = false;

        [Header("Integração com o Cenário")]
        [Tooltip("Referência ao Gerenciador Central de Cenário Semântico.")]
        public GerenciadorCenarioSemantico gerenciadorCenario;

        // =========================================================================
        // DTOs DE SERIALIZAÇÃO JSON
        // =========================================================================

        [Serializable]
        public class Vector3Dto
        {
            public float x;
            public float y;
            public float z;

            public Vector3 ToVector3() => new Vector3(x, y, z);
        }

        [Serializable]
        public class NodeDto
        {
            public string id;
            public string label;
            public string type;
            public string displayColor;
            public string conceptClass;
            public bool valid;
            public Vector3Dto position;
        }

        [Serializable]
        public class EdgeDto
        {
            public string id;
            public string label;
            public string sourceId;
            public string targetId;
            public string relationType;
            public float weight;
        }

        [Serializable]
        public class StatusResponseDto
        {
            public string service;
            public string status;
            public string institution;
            public string version;
            public int totalNodes;
            public int totalEdges;
        }

        [Serializable]
        private class NodeListWrapper
        {
            public List<NodeDto> items = new List<NodeDto>();
        }

        [Serializable]
        private class EdgeListWrapper
        {
            public List<EdgeDto> items = new List<EdgeDto>();
        }

        // =========================================================================
        // CICLO DE VIDA UNITY
        // =========================================================================

        private void Awake()
        {
            if (gerenciadorCenario == null)
            {
                gerenciadorCenario = GetComponent<GerenciadorCenarioSemantico>() ?? FindAnyObjectByType<GerenciadorCenarioSemantico>();
            }
        }

        private void Start()
        {
            if (testarConexaoNoStart)
            {
                StartCoroutine(GetGraphStatus());
            }

            if (sincronizarNoStart)
            {
                SincronizarCenarioComBackend();
            }
        }

        // =========================================================================
        // 1. VERIFICAÇÃO DE STATUS (/status)
        // =========================================================================

        /// <summary>
        /// Corrotina para testar a ligação com a API Java Spring Boot.
        /// </summary>
        [ContextMenu("Testar Conexão com API Java (/status)")]
        public void MenuTestarConexao()
        {
            StartCoroutine(GetGraphStatus());
        }

        public IEnumerator GetGraphStatus(Action<StatusResponseDto> onComplete = null)
        {
            string url = baseUrl + "/status";
            Debug.Log($"<color=#00DDFF><b>[ApiClient]</b></color> Consultando status da API Java em '{url}'...");

            using (UnityWebRequest www = UnityWebRequest.Get(url))
            {
                www.timeout = 5;
                yield return www.SendWebRequest();

                if (www.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError("[ApiClient] Erro ao comunicar com a API Java: " + www.error + " (URL: " + url + ")");
                    onComplete?.Invoke(null);
                }
                else
                {
                    string jsonResponse = www.downloadHandler.text;
                    Debug.Log("<color=#00FF99><b>[ApiClient]</b></color> Conexão bem-sucedida com a API Java! Resposta: " + jsonResponse);

                    StatusResponseDto statusDto = null;
                    try
                    {
                        statusDto = JsonUtility.FromJson<StatusResponseDto>(jsonResponse);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("[ApiClient] Não foi possível deserializar DTO de status: " + ex.Message);
                    }

                    onComplete?.Invoke(statusDto);
                }
            }
        }

        // =========================================================================
        // 2. SINCRONIZAÇÃO COMPLETA DO GRAFO ONTOLÓGICO (/nodes e /edges)
        // =========================================================================

        /// <summary>
        /// Ponto de integração principal: Busca nós e arestas da API Spring Boot
        /// e solicita a reconstrução do labirinto ao GerenciadorCenarioSemantico.
        /// </summary>
        [ContextMenu("Sincronizar Cenário com Backend Java")]
        public void SincronizarCenarioComBackend()
        {
            if (gerenciadorCenario == null)
            {
                gerenciadorCenario = GetComponent<GerenciadorCenarioSemantico>() ?? FindAnyObjectByType<GerenciadorCenarioSemantico>();
            }

            StartCoroutine(SincronizarGrafoCorrotina(grafoRecebido =>
            {
                if (grafoRecebido != null && grafoRecebido.GetNodeCount() > 0)
                {
                    Debug.Log($"<color=#00FF99><b>[ApiClient -> Gerenciador]</b></color> {grafoRecebido.GetNodeCount()} nós e {grafoRecebido.GetEdgeCount()} arestas recebidos da API Java. Reconstruindo cenário procedural...");
                    if (gerenciadorCenario != null)
                    {
                        gerenciadorCenario.CarregarCenarioDeDados(grafoRecebido.ToCenarioDTO());
                    }
                }
                else
                {
                    Debug.LogWarning("[ApiClient] Grafo retornado pelo backend Java está vazio ou nulo.");
                }
            }));
        }

        /// <summary>
        /// Corrotina que busca nós e arestas via REST e instancia um objeto Graph VirtOnto.
        /// </summary>
        public IEnumerator SincronizarGrafoCorrotina(Action<Graph> onComplete)
        {
            string urlNodes = baseUrl + "/nodes/list";
            string urlEdges = baseUrl + "/edges";

            Graph graph = new Graph
            {
                NomeOntologia = "PacMan_JavaSpringBoot_VirtOnto",
                VersaoAxiomas = "v2.0-SpringBoot"
            };

            // 1. Requisição dos Nós
            using (UnityWebRequest reqNodes = UnityWebRequest.Get(urlNodes))
            {
                reqNodes.timeout = 7;
                yield return reqNodes.SendWebRequest();

                if (reqNodes.result == UnityWebRequest.Result.Success)
                {
                    string rawJson = reqNodes.downloadHandler.text;
                    List<NodeDto> listaNodes = DeserializarLista<NodeDto>(rawJson);

                    foreach (var dto in listaNodes)
                    {
                        NodeType t = (dto.type != null && dto.type.Equals("Class", StringComparison.OrdinalIgnoreCase))
                            ? NodeType.CLASS
                            : NodeType.INDIVIDUAL;

                        Vector3D pos = dto.position != null ? new Vector3D(dto.position.x, dto.position.y, dto.position.z) : new Vector3D();

                        Node node = new Node(dto.id, dto.label, t)
                        {
                            ConceptClass = string.IsNullOrEmpty(dto.conceptClass) ? dto.label : dto.conceptClass,
                            DisplayColor = string.IsNullOrEmpty(dto.displayColor) ? "#FFFFFF" : dto.displayColor,
                            IsValid = dto.valid,
                            Position = pos
                        };

                        graph.AddNode(node);
                    }
                }
                else
                {
                    Debug.LogError("[ApiClient] Falha ao obter nós da API Java: " + reqNodes.error);
                }
            }

            // 2. Requisição das Arestas
            using (UnityWebRequest reqEdges = UnityWebRequest.Get(urlEdges))
            {
                reqEdges.timeout = 7;
                yield return reqEdges.SendWebRequest();

                if (reqEdges.result == UnityWebRequest.Result.Success)
                {
                    string rawJson = reqEdges.downloadHandler.text;
                    List<EdgeDto> listaEdges = DeserializarLista<EdgeDto>(rawJson);

                    foreach (var dto in listaEdges)
                    {
                        Edge edge = new Edge(dto.id, dto.sourceId, dto.targetId, dto.relationType, dto.weight > 0 ? dto.weight : 1.0f);
                        graph.AddEdge(edge);
                    }
                }
                else
                {
                    Debug.LogWarning("[ApiClient] Falha ao obter arestas da API Java: " + reqEdges.error);
                }
            }

            onComplete?.Invoke(graph);
        }

        // =========================================================================
        // 3. ENVIO DE NÓS E ARESTAS PARA O BACKEND (/nodes e /edges via POST)
        // =========================================================================

        /// <summary>
        /// Envia um novo nó para ser persistido no grafo da API Java.
        /// </summary>
        public IEnumerator PostNode(NodeDto node, Action<bool> onComplete = null)
        {
            string url = baseUrl + "/nodes";
            string jsonBody = JsonUtility.ToJson(node);

            using (UnityWebRequest www = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
                www.uploadHandler = new UploadHandlerRaw(bodyRaw);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");

                yield return www.SendWebRequest();

                bool sucesso = www.result == UnityWebRequest.Result.Success;
                if (!sucesso)
                {
                    Debug.LogError("[ApiClient] Erro ao postar nó na API Java: " + www.error);
                }
                onComplete?.Invoke(sucesso);
            }
        }

        /// <summary>
        /// Envia uma nova aresta de relação ontológica (ex: consumes) para a API Java.
        /// </summary>
        public IEnumerator PostEdge(EdgeDto edge, Action<bool> onComplete = null)
        {
            string url = baseUrl + "/edges";
            string jsonBody = JsonUtility.ToJson(edge);

            using (UnityWebRequest www = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
                www.uploadHandler = new UploadHandlerRaw(bodyRaw);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");

                yield return www.SendWebRequest();

                bool sucesso = www.result == UnityWebRequest.Result.Success;
                if (!sucesso)
                {
                    Debug.LogError("[ApiClient] Erro ao postar aresta na API Java: " + www.error);
                }
                onComplete?.Invoke(sucesso);
            }
        }

        // =========================================================================
        // UTILITÁRIO DE DESSERIALIZAÇÃO JSON
        // =========================================================================

        private List<T> DeserializarLista<T>(string rawJson)
        {
            if (string.IsNullOrEmpty(rawJson) || rawJson == "[]")
            {
                return new List<T>();
            }

            // Envelopa o array JSON raiz com { "items": [...] } para compatibilidade com o JsonUtility do Unity
            string wrapped = "{\"items\":" + rawJson + "}";
            try
            {
                if (typeof(T) == typeof(NodeDto))
                {
                    NodeListWrapper w = JsonUtility.FromJson<NodeListWrapper>(wrapped);
                    return w != null && w.items != null ? (List<T>)(object)w.items : new List<T>();
                }
                else if (typeof(T) == typeof(EdgeDto))
                {
                    EdgeListWrapper w = JsonUtility.FromJson<EdgeListWrapper>(wrapped);
                    return w != null && w.items != null ? (List<T>)(object)w.items : new List<T>();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ApiClient] Erro ao deserializar lista de {typeof(T).Name}: {ex.Message}");
            }

            return new List<T>();
        }
    }
}
