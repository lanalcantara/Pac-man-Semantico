using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace PacMan.Network
{
    /// <summary>
    /// ============================================================================
    /// INTERFACE ESPACIAL BIDIRECIONAL OWL - ProtegeWebSocketBridge (Porta 8085)
    /// ============================================================================
    /// Cliente WebSocket nativo assíncrono para comunicação bidirecional em tempo real
    /// com o plugin Java/OSGi do Protégé (servidor WebSocket na porta 8085).
    /// Substitui integralmente o antigo backend legado em C++.
    /// ============================================================================
    /// </summary>
    [DisallowMultipleComponent]
    public class ProtegeWebSocketBridge : MonoBehaviour
    {
        public static ProtegeWebSocketBridge Instance { get; private set; }

        [Header("Configuração WebSocket Protégé (Java/OSGi)")]
        [Tooltip("Endpoint WebSocket do plugin nativo no Protégé.")]
        public string serverUri = "ws://localhost:8085";

        [Tooltip("Conectar automaticamente ao servidor WebSocket no Start.")]
        public bool autoConnect = true;

        [Header("Estado da Conexão e Reasoner")]
        [SerializeField] private bool m_IsConnected;
        [SerializeField] private bool m_ReasonerConsistent = true;
        [SerializeField] private int m_AxiomsInsertedCount = 0;

        public bool IsConnected => m_IsConnected;
        public bool ReasonerConsistent => m_ReasonerConsistent;
        public int AxiomsInsertedCount => m_AxiomsInsertedCount;

        // Eventos para sincronizar HUD, IA dos Reasoner Agents e Topologia
        public event Action<string> OnActionLogged;
        public event Action<bool, string> OnReasonerStatusChanged;
        public event Action<int> OnAxiomCountChanged;
        public event Action<string> OnRawMessageReceived;

        private ClientWebSocket m_WebSocket;
        private CancellationTokenSource m_Cts;
        private readonly ConcurrentQueue<Action> m_MainThreadQueue = new ConcurrentQueue<Action>();

        [Serializable]
        public class OwlSocketMessage
        {
            public string action;         // Ex: "INSTANTIATE_NODE", "CREATE_OBJECT_PROPERTY", "UNDO_LAST_ACTION", "REASONER_STATUS"
            public string subjectId;
            public string subjectClass;
            public string propertyName;
            public string objectId;
            public string objectClass;
            public bool isConsistent = true;
            public string reasonerMessage;
            public long timestamp;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            if (autoConnect && Application.isPlaying)
            {
                ConectarAsync();
            }
        }

        private void Update()
        {
            while (m_MainThreadQueue.TryDequeue(out Action action))
            {
                action?.Invoke();
            }
        }

        private void OnDestroy()
        {
            DesconectarAsync();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public async void ConectarAsync()
        {
            if (m_WebSocket != null && (m_WebSocket.State == WebSocketState.Open || m_WebSocket.State == WebSocketState.Connecting))
                return;

            m_Cts = new CancellationTokenSource();
            m_WebSocket = new ClientWebSocket();

            try
            {
                Uri uri = new Uri(serverUri);
                await m_WebSocket.ConnectAsync(uri, m_Cts.Token);
                m_IsConnected = true;

                EnfileirarMainThread(() =>
                {
                    RegistrarLog($"[WS:8085] Conectado ao plugin Java/OSGi Protégé em {serverUri}");
                });

                _ = LoopRecebimentoAsync(m_Cts.Token);
            }
            catch (Exception ex)
            {
                m_IsConnected = false;
                EnfileirarMainThread(() =>
                {
                    Debug.Log($"<color=#00DDFF><b>[ProtegeWebSocketBridge]</b></color> Servidor WebSocket ({serverUri}) offline ({ex.Message}). Operando em modo de validação ontológica local sincronizável.");
                });
            }
        }

        public async void DesconectarAsync()
        {
            try
            {
                m_Cts?.Cancel();
                if (m_WebSocket != null && m_WebSocket.State == WebSocketState.Open)
                {
                    await m_WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Encerrando sessão Unity", CancellationToken.None);
                }
            }
            catch (Exception)
            {
                // Ignora exceções de fechamento durante teardown
            }
            finally
            {
                m_IsConnected = false;
                m_WebSocket?.Dispose();
                m_WebSocket = null;
            }
        }

        private async Task LoopRecebimentoAsync(CancellationToken token)
        {
            byte[] buffer = new byte[8192];
            while (m_WebSocket != null && m_WebSocket.State == WebSocketState.Open && !token.IsCancellationRequested)
            {
                try
                {
                    var segment = new ArraySegment<byte>(buffer);
                    WebSocketReceiveResult result = await m_WebSocket.ReceiveAsync(segment, token);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        m_IsConnected = false;
                        break;
                    }

                    string rawJson = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    EnfileirarMainThread(() => ProcessarMensagemRecebida(rawJson));
                }
                catch (Exception)
                {
                    m_IsConnected = false;
                    break;
                }
            }
        }

        private void ProcessarMensagemRecebida(string rawJson)
        {
            OnRawMessageReceived?.Invoke(rawJson);

            try
            {
                OwlSocketMessage msg = JsonUtility.FromJson<OwlSocketMessage>(rawJson);
                if (msg != null && !string.IsNullOrEmpty(msg.action))
                {
                    if (msg.action == "REASONER_STATUS" || msg.action == "HERMIT_EXCEPTION")
                    {
                        DefinirStatusReasoner(msg.isConsistent, msg.reasonerMessage);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ProtegeWebSocketBridge] Falha ao processar payload JSON do Protégé: {ex.Message}");
            }
        }

        /// <summary>
        /// Envia axioma de instanciação de indivíduo (ABox ClassAssertion) via WebSocket para o Protégé.
        /// </summary>
        public void EnviarInstanciacaoIndividuo(string nodeId, string owlClass, bool ehValida = true, string dlExpression = "")
        {
            m_AxiomsInsertedCount++;
            OnAxiomCountChanged?.Invoke(m_AxiomsInsertedCount);

            var payload = new OwlSocketMessage
            {
                action = "INSTANTIATE_NODE",
                subjectId = nodeId,
                subjectClass = owlClass,
                isConsistent = ehValida,
                reasonerMessage = dlExpression,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            EnviarJsonAsync(JsonUtility.ToJson(payload));
            RegistrarLog($"[+] researcher_01 instanciou {owlClass} ({nodeId})");

            if (!ehValida)
            {
                DefinirStatusReasoner(false, $"HermiT Reasoner Exception: Inconsistência em {owlClass} ({dlExpression})");
            }
        }

        /// <summary>
        /// Envia tripla de ObjectPropertyAssertion (Nodo A -> propriedade -> Nodo B) via WebSocket para o Protégé.
        /// </summary>
        public void EnviarRelacaoObjectProperty(string sourceId, string sourceClass, string propertyName, string targetId, string targetClass, bool consistente = true)
        {
            m_AxiomsInsertedCount++;
            OnAxiomCountChanged?.Invoke(m_AxiomsInsertedCount);

            var payload = new OwlSocketMessage
            {
                action = "CREATE_OBJECT_PROPERTY",
                subjectId = sourceId,
                subjectClass = sourceClass,
                propertyName = propertyName,
                objectId = targetId,
                objectClass = targetClass,
                isConsistent = consistente,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            EnviarJsonAsync(JsonUtility.ToJson(payload));
            RegistrarLog($"[+] Tripla OWL: {sourceClass}({sourceId}) -> {propertyName} -> {targetClass}({targetId})");

            if (!consistente)
            {
                DefinirStatusReasoner(false, $"HermiT Disjoint/Domain Violation: {sourceClass} -> {propertyName} -> {targetClass}");
            }
        }

        /// <summary>
        /// Desfaz a última ação que gerou inconsistência no Reasoner e restaura o estado CONSISTENTE.
        /// </summary>
        public void DesfazerUltimaAcaoInconsistente()
        {
            if (m_AxiomsInsertedCount > 0)
            {
                m_AxiomsInsertedCount--;
                OnAxiomCountChanged?.Invoke(m_AxiomsInsertedCount);
            }

            var payload = new OwlSocketMessage
            {
                action = "UNDO_LAST_ACTION",
                isConsistent = true,
                reasonerMessage = "Axioma inconsistente removido pelo usuário.",
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            EnviarJsonAsync(JsonUtility.ToJson(payload));
            RegistrarLog($"[-] Axioma inconsistente revertido. HermiT: ABox restaurada.");
            DefinirStatusReasoner(true, "CONSISTENTE");
        }

        public void DefinirStatusReasoner(bool consistente, string detalhe = "")
        {
            m_ReasonerConsistent = consistente;
            OnReasonerStatusChanged?.Invoke(consistente, detalhe);

            if (!consistente && !string.IsNullOrEmpty(detalhe))
            {
                RegistrarLog($"[!] ERRO HERMIT: {detalhe}");
            }
        }

        public void RegistrarLog(string mensagem)
        {
            Debug.Log($"<color=#00FFCC><b>[OWL Terminal]</b></color> {mensagem}");
            OnActionLogged?.Invoke(mensagem);
        }

        public async void EnviarJsonAsync(string json)
        {
            if (m_WebSocket == null || m_WebSocket.State != WebSocketState.Open)
                return;

            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(json);
                var segment = new ArraySegment<byte>(bytes);
                await m_WebSocket.SendAsync(segment, WebSocketMessageType.Text, true, m_Cts != null ? m_Cts.Token : CancellationToken.None);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ProtegeWebSocketBridge] Erro ao enviar pacote WebSocket: {ex.Message}");
            }
        }

        private void EnfileirarMainThread(Action action)
        {
            if (action != null)
            {
                m_MainThreadQueue.Enqueue(action);
            }
        }
    }
}

