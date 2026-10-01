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
        /// Garante automaticamente que CenaDemoWeb.unity é a cena ativa e de início ao dar Play no Unity Editor,
        /// descarregando qualquer cena residual como SampleScene.
        /// </summary>
        [InitializeOnLoadMethod]
        public static void GarantirCenaDemoWebAoIniciar()
        {
            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(CaminhoCenaDemo);
            if (sceneAsset != null)
            {
                EditorSceneManager.playModeStartScene = sceneAsset;
            }

            EditorApplication.delayCall += () =>
            {
                for (int i = 0; i < EditorSceneManager.sceneCount; i++)
                {
                    var sc = EditorSceneManager.GetSceneAt(i);
                    if (sc.name.Contains("SampleScene") && sc.isLoaded)
                    {
                        EditorSceneManager.CloseScene(sc, true);
                    }
                }

                var activeScene = EditorSceneManager.GetActiveScene();
                if (activeScene.path != CaminhoCenaDemo && !Application.isPlaying)
                {
                    EditorSceneManager.OpenScene(CaminhoCenaDemo, OpenSceneMode.Single);
                }
            };
        }

        /// <summary>
        /// Fecha e descarrega explicitamente a SampleScene e abre exclusivamente a CenaDemoWeb.unity.
        /// </summary>
        [MenuItem("Pac-Man Semântico/5. Selecionar e Abrir Cena Demo WebGL (CenaDemoWeb)", false, 5)]
        public static void AbrirCenaDemoWeb()
        {
            Debug.Log("<color=#00FFFF><b>[Cena WebGL]</b></color> Fechando SampleScene e abrindo exclusivamente CenaDemoWeb.unity...");

            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                var sc = EditorSceneManager.GetSceneAt(i);
                if (sc.name.Contains("SampleScene") && sc.isLoaded)
                {
                    EditorSceneManager.CloseScene(sc, true);
                }
            }
            
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                var scene = EditorSceneManager.OpenScene(CaminhoCenaDemo, OpenSceneMode.Single);
                var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(CaminhoCenaDemo);
                if (sceneAsset != null)
                {
                    EditorSceneManager.playModeStartScene = sceneAsset;
                }
                ConfigurarCenasNoBuild();
                Debug.Log($"<color=#00FF99><b>✔ [Cena WebGL Ativa]</b></color> '{scene.name}' aberta com sucesso. SampleScene fechada e apenas CenaDemoWeb está no Build.");
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
            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(CaminhoCenaDemo);
            if (sceneAsset != null)
            {
                EditorSceneManager.playModeStartScene = sceneAsset;
            }
            Debug.Log($"<color=#00FF99><b>[WebGL Setup]</b></color> Cena '{CaminhoCenaDemo}' definida como ÚNICA cena do Build e PlayModeStartScene.");
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

        /// <summary>
        /// Aplica os refinamentos visuais na CenaDemoWeb:
        /// - Ajusta câmera: FOV 65, Near 0.05, Pac-Man afastado da lente.
        /// - Iluminação: Directional Light com intensidade 1.4, rotação (55, -35, 0), sombras suaves.
        /// - Ambiente: Azul-noite profundo (#0f172a).
        /// - Oculta ponteiros/lasers de RV.
        /// - Material escuro de alto contraste no chão do labirinto.
        /// </summary>
        [MenuItem("Pac-Man Semântico/8. Polir Visual da Cena Demo WebGL (Luz, FOV, Materiais)", false, 8)]
        public static void PolirVisualCenaDemoWeb()
        {
            Debug.Log("<color=#00FFFF><b>[Polimento Visual WebGL]</b></color> Aplicando polimento estético na CenaDemoWeb...");

            var scene = EditorSceneManager.OpenScene(CaminhoCenaDemo, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError("Não foi possível abrir CenaDemoWeb.unity para polimento.");
                return;
            }

            // 1. Câmera Principal
            Camera cam = Camera.main;
            if (cam != null)
            {
                cam.fieldOfView = 65f;
                cam.nearClipPlane = 0.05f;
                cam.backgroundColor = new Color(0.059f, 0.090f, 0.165f, 1f); // #0f172a
                Debug.Log("<color=#00FF99>✔ Câmera ajustada:</color> FOV=65, NearClip=0.05, Background=#0f172a.");
            }

            // 2. Afastamento do Pac-Man da lente da câmera
            GameObject pacmanVisual = GameObject.Find("Visual_PacMan");
            if (pacmanVisual != null)
            {
                pacmanVisual.transform.localPosition = new Vector3(0f, -0.55f, 1.25f);
                pacmanVisual.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
                Debug.Log("<color=#00FF99>✔ Modelo Pac-Man reposicionado:</color> afastado da lente para não tapar o campo de visão.");
            }

            // 3. Directional Light
            Light[] lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (var l in lights)
            {
                if (l.type == LightType.Directional)
                {
                    l.intensity = 1.4f;
                    l.color = new Color(1f, 0.96f, 0.90f, 1f);
                    l.shadows = LightShadows.Soft;
                    l.shadowStrength = 0.85f;
                    l.transform.localEulerAngles = new Vector3(55f, -35f, 0f);
                    Debug.Log("<color=#00FF99>✔ Directional Light ajustada:</color> Intensidade=1.4, Rotação=(55, -35, 0), Sombras Suaves.");
                }
            }

            // 4. Ocultação de elementos de RV (Left/Right Hand Controller)
            GameObject leftHand = GameObject.Find("Left Hand Controller");
            if (leftHand != null) leftHand.SetActive(false);
            GameObject rightHand = GameObject.Find("Right Hand Controller");
            if (rightHand != null) rightHand.SetActive(false);
            Debug.Log("<color=#00FF99>✔ Elementos de RV ocultados:</color> Lasers e ponteiros desativados na cena WebGL.");

            // 5. Chão do labirinto escuro
            GameObject plane = GameObject.Find("Plane");
            if (plane != null)
            {
                Material matChao = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_ChaoLabirinto_Escuro.mat");
                if (matChao != null)
                {
                    var renderer = plane.GetComponent<MeshRenderer>();
                    if (renderer != null) renderer.sharedMaterial = matChao;
                    Debug.Log("<color=#00FF99>✔ Chão do labirinto atualizado:</color> Mat_ChaoLabirinto_Escuro aplicado.");
                }
            }

            // 6. Ambiente / Iluminação
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientSkyColor = new Color(0.059f, 0.090f, 0.165f, 1f); // #0f172a
            RenderSettings.subtractiveShadowColor = new Color(0.05f, 0.08f, 0.15f, 1f);
            RenderSettings.skybox = null;

            EditorSceneManager.SaveScene(scene);
            Debug.Log("<color=#00FF99><b>✔ [Polimento Concluído com Sucesso]</b></color> CenaDemoWeb.unity salva com estética premium.");
        }
    }
}
#endif
