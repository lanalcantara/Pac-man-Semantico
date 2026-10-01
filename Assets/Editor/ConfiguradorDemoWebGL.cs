#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PacMan.Editor
{
    /// <summary>
    /// ============================================================================
    /// PAC-MAN SEMÂNTICO (CIn-UFPE) - Configurador de Exportação WebGL
    /// ============================================================================
    /// Ferramenta do Unity Editor para preparar o projeto para exportação WebGL
    /// destinada ao GitHub Pages, configurando Player Settings, resolução e cena demo.
    /// ============================================================================
    /// </summary>
    public static class ConfiguradorDemoWebGL
    {
        private const string CaminhoCenaDemo = "Assets/Scenes/DemoWeb/CenaDemoWeb.unity";

        [MenuItem("Pac-Man Semântico/6. Configurar Plataforma WebGL e Cena Demo", false, 6)]
        public static void ConfigurarPlataformaWebGL()
        {
            Debug.Log("<color=#00FFFF><b>[WebGL Setup]</b></color> Configurando Player Settings para WebGL / GitHub Pages...");

            // 1. Configurações de Memória e Carregamento WebGL
            PlayerSettings.defaultWebScreenWidth = 960;
            PlayerSettings.defaultWebScreenHeight = 600;
            PlayerSettings.WebGL.memorySize = 256;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;

            // 2. Altera a plataforma de Build para WebGL
            BuildTargetGroup targetGroup = BuildTargetGroup.WebGL;
            BuildTarget target = BuildTarget.WebGL;

            if (EditorUserBuildSettings.activeBuildTarget != target)
            {
                bool switched = EditorUserBuildSettings.SwitchActiveBuildTarget(targetGroup, target);
                Debug.Log($"<color=#00FF99><b>[WebGL Setup]</b></color> Plataforma ativa alternada para WebGL: {switched}");
            }

            // 3. Garante que CenaDemoWeb está na lista de cenas do Build
            ConfigurarCenasNoBuild();

            Debug.Log("<color=#00FF99><b>✔ [WebGL Setup Concluído]</b></color> Projeto 100% configurado para exportação WebGL (GitHub Pages)!");
        }

        private static void ConfigurarCenasNoBuild()
        {
            EditorBuildSettingsScene[] cenasAtuais = EditorBuildSettings.scenes;
            bool cenaDemoPresente = false;

            foreach (var scene in cenasAtuais)
            {
                if (scene.path == CaminhoCenaDemo)
                {
                    cenaDemoPresente = true;
                    break;
                }
            }

            if (!cenaDemoPresente)
            {
                EditorBuildSettingsScene[] novasCenas = new EditorBuildSettingsScene[cenasAtuais.Length + 1];
                novasCenas[0] = new EditorBuildSettingsScene(CaminhoCenaDemo, true);
                for (int i = 0; i < cenasAtuais.Length; i++)
                {
                    novasCenas[i + 1] = cenasAtuais[i];
                }
                EditorBuildSettings.scenes = novasCenas;
                Debug.Log($"<color=#00FF99><b>[WebGL Setup]</b></color> Cena '{CaminhoCenaDemo}' adicionada como cena principal de build.");
            }
        }
    }
}
#endif
