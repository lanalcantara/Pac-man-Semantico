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

        /// <summary>
        /// Abre e seleciona exclusivamente a cena WebGL leve (CenaDemoWeb.unity)
        /// garantindo que cenas de Realidade Virtual (como SampleScene) não estejam ativas.
        /// </summary>
        [MenuItem("Pac-Man Semântico/5. Selecionar e Abrir Cena Demo WebGL (CenaDemoWeb)", false, 5)]
        public static void AbrirCenaDemoWeb()
        {
            Debug.Log("<color=#00FFFF><b>[Cena WebGL]</b></color> Abrindo cena dedicada CenaDemoWeb.unity...");
            
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                var scene = EditorSceneManager.OpenScene(CaminhoCenaDemo, OpenSceneMode.Single);
                ConfigurarCenasNoBuild();
                Debug.Log($"<color=#00FF99><b>✔ [Cena WebGL Ativa]</b></color> '{scene.name}' aberta com sucesso. Apenas ela está configurada para build.");
            }
        }

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

            // 2. Altera a plataforma de Build para WebGL se suportada
            BuildTargetGroup targetGroup = BuildTargetGroup.WebGL;
            BuildTarget target = BuildTarget.WebGL;

            if (BuildPipeline.IsBuildTargetSupported(targetGroup, target))
            {
                if (EditorUserBuildSettings.activeBuildTarget != target)
                {
                    bool switched = EditorUserBuildSettings.SwitchActiveBuildTarget(targetGroup, target);
                    Debug.Log($"<color=#00FF99><b>[WebGL Setup]</b></color> Plataforma ativa alternada para WebGL: {switched}");
                }
            }
            else
            {
                Debug.LogWarning("<color=#FFCC00><b>[WebGL Setup]</b></color> Módulo WebGL não instalado no Unity Editor ativo. " +
                                 "Para compilar nativamente via Unity, instale o 'WebGL Build Support' via Unity Hub.");
            }

            // 3. Garante EXCLUSIVIDADE da CenaDemoWeb na lista de cenas do Build
            ConfigurarCenasNoBuild();

            Debug.Log("<color=#00FF99><b>✔ [WebGL Setup Concluído]</b></color> Projeto 100% configurado para exportação WebGL (GitHub Pages)!");
        }

        public static void ConfigurarCenasNoBuild()
        {
            // Define exclusivamente a CenaDemoWeb no índice 0
            EditorBuildSettingsScene[] cenasExclusivas = new EditorBuildSettingsScene[]
            {
                new EditorBuildSettingsScene(CaminhoCenaDemo, true)
            };

            EditorBuildSettings.scenes = cenasExclusivas;
            Debug.Log($"<color=#00FF99><b>[WebGL Setup]</b></color> Cena '{CaminhoCenaDemo}' definida como ÚNICA cena do Build (Cenas de RV desativadas).");
        }

        /// <summary>
        /// Executa o pipeline de build WebGL exportando exatamente para ./Builds/WebGL
        /// </summary>
        [MenuItem("Pac-Man Semântico/7. Executar Build WebGL para ./Builds/WebGL", false, 7)]
        public static void ExecutarBuildWebGL()
        {
            ConfigurarPlataformaWebGL();

            string outputFolder = "Builds/WebGL";
            string buildFolder = Path.Combine(outputFolder, "Build");
            if (!Directory.Exists(buildFolder))
            {
                Directory.CreateDirectory(buildFolder);
            }

            BuildTargetGroup targetGroup = BuildTargetGroup.WebGL;
            BuildTarget target = BuildTarget.WebGL;

            if (BuildPipeline.IsBuildTargetSupported(targetGroup, target))
            {
                BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
                {
                    scenes = new[] { CaminhoCenaDemo },
                    locationPathName = outputFolder,
                    target = target,
                    options = BuildOptions.None
                };

                Debug.Log($"<color=#00FFFF><b>[WebGL Build]</b></color> Compilando player WebGL nativo para {outputFolder}...");
                var report = BuildPipeline.BuildPlayer(buildPlayerOptions);
                Debug.Log($"<color=#00FF99><b>[WebGL Build]</b></color> Resultado do Build: {report.summary.result} ({report.summary.totalErrors} erros)");
            }
            else
            {
                Debug.LogWarning("<color=#FFCC00><b>[WebGL Build]</b></color> O editor ativo não possui o módulo WebGL Build Support instalado. " +
                                 "Certifique-se de que os ficheiros binários em Builds/WebGL/Build/ estão sincronizados para o deploy.");
            }
        }
    }
}
#endif
