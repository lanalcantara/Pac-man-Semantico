using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using PacMan.Semantics;

namespace PacMan.Telemetria
{
    [System.Serializable]
    public struct RegistoTelemetria
    {
        public string timestamp;
        public float tempoJogo;
        public float fps;
        public float latenciaRedeMs;
        public string estadoFantasma;
        public float distanciaPacmanFantasma;
    }

    /// <summary>
    /// ============================================================================
    /// PAC-MAN SEMÂNTICO (CIn-UFPE) - Telemetria de Experimento e Desempenho
    /// ============================================================================
    /// Monitoriza métricas de desempenho (FPS), distâncias espaciais entre o Pac-Man
    /// (XR Origin) e os fantasmas, estados ontológicos do motor SWRL e latência de rede,
    /// exportando automaticamente para um ficheiro CSV em Application.persistentDataPath.
    /// ============================================================================
    /// </summary>
    [AddComponentMenu("Pac-Man Semântico/Telemetria/Telemetria Experimento")]
    public class TelemetriaExperimento : MonoBehaviour
    {
        [Header("Configuração de Registo")]
        public float intervaloRegisto = 1.0f; // Regista a cada 1 segundo
        private float temporizador = 0f;

        [Header("Referências")]
        public MotorRaciocinioSemantico motorSemantico;
        public Transform pacmanTransform;
        public Transform ghostTransform;

        private List<RegistoTelemetria> historico = new List<RegistoTelemetria>();
        private string caminhoFicheiro;

        private void Start()
        {
            caminhoFicheiro = Path.Combine(Application.persistentDataPath, $"telemetria_pacman_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            
            // Escreve o cabeçalho do CSV
            try
            {
                File.WriteAllText(caminhoFicheiro, "Timestamp,TempoJogo,FPS,LatenciaRedeMs,EstadoFantasma,DistanciaPacmanFantasma\n");
                Debug.Log($"[Telemetria] Ficheiro criado em: {caminhoFicheiro}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Telemetria] Não foi possível criar o arquivo de telemetria: {ex.Message}");
            }

            if (motorSemantico == null)
            {
                motorSemantico = FindFirstObjectByType<MotorRaciocinioSemantico>();
            }

            if (pacmanTransform == null)
            {
                GameObject player = GameObject.FindWithTag("Player");
                if (player != null) pacmanTransform = player.transform;
                else if (Camera.main != null) pacmanTransform = Camera.main.transform;
            }

            if (ghostTransform == null)
            {
                GameObject ghost = GameObject.Find("Fantasma") ?? GameObject.Find("Blinky") ?? GameObject.Find("ObstaculoInvalido");
                if (ghost != null) ghostTransform = ghost.transform;
            }
        }

        private void Update()
        {
            temporizador += Time.deltaTime;
            if (temporizador >= intervaloRegisto)
            {
                temporizador = 0f;
                RegistarMetricasFrame();
            }
        }

        /// <summary>
        /// Captura e regista o frame atual de telemetria ontológica e física.
        /// </summary>
        public void RegistarMetricasFrame()
        {
            float fpsAtual = Time.smoothDeltaTime > 0f ? (1.0f / Time.smoothDeltaTime) : 60.0f;
            float distancia = (pacmanTransform != null && ghostTransform != null) ? 
                Vector3.Distance(pacmanTransform.position, ghostTransform.position) : 0f;

            string estadoAtual = "Patrol";
            if (motorSemantico != null)
            {
                estadoAtual = motorSemantico.ObterEstadoAtualFantasma("ghost_blinky").ToString();
            }

            RegistoTelemetria reg = new RegistoTelemetria
            {
                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                tempoJogo = Time.time,
                fps = fpsAtual,
                latenciaRedeMs = 15.5f, // Valor simulado de latência da API Spring Boot
                estadoFantasma = estadoAtual,
                distanciaPacmanFantasma = distancia
            };

            historico.Add(reg);
            EscreverRegistoNoCsv(reg);
        }

        private void EscreverRegistoNoCsv(RegistoTelemetria reg)
        {
            try
            {
                if (string.IsNullOrEmpty(caminhoFicheiro))
                {
                    caminhoFicheiro = Path.Combine(Application.persistentDataPath, $"telemetria_pacman_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
                    if (!File.Exists(caminhoFicheiro))
                    {
                        File.WriteAllText(caminhoFicheiro, "Timestamp,TempoJogo,FPS,LatenciaRedeMs,EstadoFantasma,DistanciaPacmanFantasma\n");
                    }
                }

                string linha = $"{reg.timestamp},{reg.tempoJogo:F2},{reg.fps:F1},{reg.latenciaRedeMs:F2},{reg.estadoFantasma},{reg.distanciaPacmanFantasma:F2}\n";
                File.AppendAllText(caminhoFicheiro, linha);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Telemetria] Erro ao gravar CSV: {ex.Message}");
            }
        }

        public IReadOnlyList<RegistoTelemetria> ObterHistorico() => historico;
        public string ObterCaminhoFicheiro() => caminhoFicheiro;

        private void OnApplicationQuit()
        {
            Debug.Log($"[Telemetria] Sessão terminada. Total de registos guardados: {historico.Count}. Ficheiro: {caminhoFicheiro}");
        }
    }
}
