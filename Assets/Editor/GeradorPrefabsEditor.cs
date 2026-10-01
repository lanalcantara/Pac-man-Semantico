#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.IO;

/// <summary>
/// ============================================================================
/// PAC-MAN SEMÂNTICO (Semantic Pac-Man) - Realidade Virtual e Aumentada (RV/RA)
/// ============================================================================
/// Utilitário do Unity Editor para geração automática dos modelos 3D estilizados:
/// 1. Pac-Man 3D com mandíbulas animadas (Upper/Lower Jaw).
/// 2. Pílulas / Pac-Dots (Gemas Válidas com aura brilhante).
/// 3. Fantasma Blinky 3D (Obstáculos Inválidos com olhos móveis).
/// ============================================================================
/// </summary>
[InitializeOnLoad]
public static class GeradorPrefabsEditor
{
    private const string PastaPrefabs = "Assets/Prefabs";
    private const string PastaResourcesPrefabs = "Assets/Resources/Prefabs";

    private const string CaminhoGemaValida = PastaPrefabs + "/GemaValida.prefab";
    private const string CaminhoObstaculoInvalido = PastaPrefabs + "/ObstaculoInvalido.prefab";
    private const string CaminhoPacManJogador = PastaPrefabs + "/PacManJogador.prefab";

    private const string CaminhoGemaResources = PastaResourcesPrefabs + "/GemaValida.prefab";
    private const string CaminhoObstaculoResources = PastaResourcesPrefabs + "/ObstaculoInvalido.prefab";

    static GeradorPrefabsEditor()
    {
        EditorApplication.delayCall += VerificarECriarPrefabsAutomaticamente;
    }

    [MenuItem("Pac-Man Semântico/1. Configurar Rig XR e Pac-Man na Cena", false, 1)]
    public static void ConfigurarXROriginNaCena()
    {
        // 1. Limpa objetos antigos avulsos
        GameObject cameraAntiga = GameObject.Find("Main Camera");
        if (cameraAntiga != null && cameraAntiga.transform.parent == null)
        {
            Undo.DestroyObjectImmediate(cameraAntiga);
        }

        GameObject esferaAntiga = GameObject.Find("Sphere");
        if (esferaAntiga != null)
        {
            Undo.DestroyObjectImmediate(esferaAntiga);
        }

        // 2. Garante os Prefabs gerados
        CriarPrefabs(forcarRecriacao: true);

        // 3. Localiza ou cria o XR Origin
        GameObject xrOriginObj = GameObject.Find("XR Origin");
        if (xrOriginObj == null)
        {
            xrOriginObj = new GameObject("XR Origin");
            Undo.RegisterCreatedObjectUndo(xrOriginObj, "Criar XR Origin Pac-Man");
        }

        xrOriginObj.transform.position = new Vector3(0f, 0f, -6f);
        xrOriginObj.tag = "Player";

        CharacterController cc = xrOriginObj.GetComponent<CharacterController>();
        if (cc == null) cc = Undo.AddComponent<CharacterController>(xrOriginObj);
        cc.center = new Vector3(0f, 0.9f, 0f);
        cc.height = 1.8f;
        cc.radius = 0.35f;

        ControladorXRJogador controladorXR = xrOriginObj.GetComponent<ControladorXRJogador>();
        if (controladorXR == null) controladorXR = Undo.AddComponent<ControladorXRJogador>(xrOriginObj);
        controladorXR.characterController = cc;

        // 4. Camera Offset
        Transform cameraOffset = xrOriginObj.transform.Find("Camera Offset");
        if (cameraOffset == null)
        {
            GameObject offsetObj = new GameObject("Camera Offset");
            offsetObj.transform.SetParent(xrOriginObj.transform, false);
            offsetObj.transform.localPosition = new Vector3(0f, 1.36f, 0f);
            cameraOffset = offsetObj.transform;
            Undo.RegisterCreatedObjectUndo(offsetObj, "Criar Camera Offset");
        }
        controladorXR.cameraOffset = cameraOffset;

        // 5. Main Camera XR
        Transform mainCameraXR = cameraOffset.Find("Main Camera");
        Camera cam;
        if (mainCameraXR == null)
        {
            GameObject camObj = new GameObject("Main Camera");
            camObj.transform.SetParent(cameraOffset, false);
            camObj.transform.localPosition = Vector3.zero;
            camObj.tag = "MainCamera";

            cam = camObj.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 1000f;
            cam.fieldOfView = 60f;

            camObj.AddComponent<AudioListener>();

            SphereCollider sc = camObj.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = 0.2f;

            mainCameraXR = camObj.transform;
            Undo.RegisterCreatedObjectUndo(camObj, "Criar Main Camera XR");
        }
        else
        {
            cam = mainCameraXR.GetComponent<Camera>();
        }
        controladorXR.cameraPrincipalXR = cam;

        // 6. Modelo 3D Visual do Pac-Man anexado à visão do jogador
        Transform visualPacMan = mainCameraXR.Find("Visual_PacMan");
        if (visualPacMan == null)
        {
            GameObject pacManPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CaminhoPacManJogador);
            if (pacManPrefab != null)
            {
                GameObject pacObj = (GameObject)PrefabUtility.InstantiatePrefab(pacManPrefab, mainCameraXR);
                pacObj.name = "Visual_PacMan";
                pacObj.transform.localPosition = new Vector3(0f, -0.35f, 0.65f); // Posicionado à frente no campo de visão
                pacObj.transform.localScale = Vector3.one * 0.45f;
                Undo.RegisterCreatedObjectUndo(pacObj, "Instanciar Pac-Man Visual");
            }
        }

        // 7. Controladores Esquerdo e Direito
        Transform maoEsquerda = cameraOffset.Find("Left Hand Controller");
        if (maoEsquerda == null)
        {
            GameObject esqObj = new GameObject("Left Hand Controller");
            esqObj.transform.SetParent(cameraOffset, false);
            esqObj.transform.localPosition = new Vector3(-0.25f, -0.1f, 0.4f);
            esqObj.tag = "GameController";

            SphereCollider colEsq = esqObj.AddComponent<SphereCollider>();
            colEsq.isTrigger = true;
            colEsq.radius = 0.1f;

            InteracaoEspacialXR interacaoEsq = esqObj.AddComponent<InteracaoEspacialXR>();
            interacaoEsq.tipoMao = InteracaoEspacialXR.MaoXR.MaoEsquerda;

            maoEsquerda = esqObj.transform;
            Undo.RegisterCreatedObjectUndo(esqObj, "Criar Left Hand Controller");
        }
        controladorXR.controladorEsquerdo = maoEsquerda;

        Transform maoDireita = cameraOffset.Find("Right Hand Controller");
        if (maoDireita == null)
        {
            GameObject dirObj = new GameObject("Right Hand Controller");
            dirObj.transform.SetParent(cameraOffset, false);
            dirObj.transform.localPosition = new Vector3(0.25f, -0.1f, 0.4f);
            dirObj.tag = "GameController";

            SphereCollider colDir = dirObj.AddComponent<SphereCollider>();
            colDir.isTrigger = true;
            colDir.radius = 0.1f;

            InteracaoEspacialXR interacaoDir = dirObj.AddComponent<InteracaoEspacialXR>();
            interacaoDir.tipoMao = InteracaoEspacialXR.MaoXR.MaoDireita;

            maoDireita = dirObj.transform;
            Undo.RegisterCreatedObjectUndo(dirObj, "Criar Right Hand Controller");
        }
        controladorXR.controladorDireito = maoDireita;

        // 8. Gerenciador de Efeitos Juice (Partículas e Vibrações)
        GameObject juiceObj = GameObject.Find("GerenciadorEfeitosJuice");
        if (juiceObj == null)
        {
            juiceObj = new GameObject("GerenciadorEfeitosJuice");
            juiceObj.AddComponent<GerenciadorEfeitosJuice>();
            Undo.RegisterCreatedObjectUndo(juiceObj, "Criar Gerenciador Juice");
        }

        // 9. Gerenciador de Cenário Semântico
        GameObject cenarioObj = GameObject.Find("GerenciadorCenarioSemantico");
        if (cenarioObj == null)
        {
            cenarioObj = new GameObject("GerenciadorCenarioSemantico");
            Undo.RegisterCreatedObjectUndo(cenarioObj, "Criar Gerenciador Cenario");
        }
        GerenciadorCenarioSemantico gerCenario = cenarioObj.GetComponent<GerenciadorCenarioSemantico>();
        if (gerCenario == null)
        {
            gerCenario = Undo.AddComponent<GerenciadorCenarioSemantico>(cenarioObj);
        }
        // 10. Piso da Arena (Plane)
        GameObject plane = GameObject.Find("Plane");
        if (plane != null)
        {
            plane.transform.position = Vector3.zero;
            plane.transform.localScale = new Vector3(4f, 1f, 4f);
            gerCenario.planoChao = plane.transform;
        }
        gerCenario.assentarPerfeitamenteNoChao = true;
        gerCenario.alturaGemas = 0f;
        gerCenario.alturaObstaculos = 0f;
        gerCenario.CarregarPrefabsAutomaticamente();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("<color=#00FF99><b>[Pac-Man Semântico]</b></color> Configuração do Rig XR, Personagem Pac-Man e Gerenciadores concluída com sucesso!");
    }

    [MenuItem("Pac-Man Semântico/2. Gerar Todos os Prefabs 3D (Pac-Man, Pílulas, Fantasmas)", false, 2)]
    public static void MenuGerarPrefabs()
    {
        CriarPrefabs(forcarRecriacao: true);
    }

    [MenuItem("Pac-Man Semântico/3. Gerar Arquivo JSON Exemplo (C++ Bridge)", false, 3)]
    public static void MenuGerarExemploJson()
    {
        CenarioSemanticoDTO cenario = BackendOntologiaBridge.GerarMockOntologiaCpp(12, 6, 14f, 0.85f);
        cenario.nomeOntologia = "PacMan_ALCQ_Ontology";
        cenario.versaoAxiomas = "v2.0-Export";

        string json = BackendOntologiaBridge.ExportarParaJson(cenario, true);
        string caminhoArquivo = "Assets/OntologiaExemplo_DL.json";
        File.WriteAllText(caminhoArquivo, json);

        AssetDatabase.ImportAsset(caminhoArquivo);
        Debug.Log($"<color=#00DDFF><b>[Pac-Man Semântico]</b></color> Arquivo de exemplo JSON ontológico gerado em: {caminhoArquivo}");
    }

    public static void VerificarECriarPrefabsAutomaticamente()
    {
        if (!File.Exists(CaminhoGemaValida) || !File.Exists(CaminhoObstaculoInvalido) ||
            !File.Exists(CaminhoPacManJogador) || !File.Exists(CaminhoGemaResources) || !File.Exists(CaminhoObstaculoResources))
        {
            CriarPrefabs(forcarRecriacao: false);
        }
    }

    private static void CriarPrefabs(bool forcarRecriacao)
    {
        GarantirPastasExistentes();

        if (forcarRecriacao || !File.Exists(CaminhoGemaValida) || !File.Exists(CaminhoGemaResources))
        {
            CriarPrefabPilulaPacDot();
        }

        if (forcarRecriacao || !File.Exists(CaminhoObstaculoInvalido) || !File.Exists(CaminhoObstaculoResources))
        {
            CriarPrefabFantasmaBlinky();
        }

        if (forcarRecriacao || !File.Exists(CaminhoPacManJogador))
        {
            CriarPrefabPacManJogador();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void GarantirPastasExistentes()
    {
        if (!Directory.Exists(PastaPrefabs)) Directory.CreateDirectory(PastaPrefabs);
        if (!Directory.Exists(PastaResourcesPrefabs)) Directory.CreateDirectory(PastaResourcesPrefabs);
        if (!Directory.Exists("Assets/Materials")) Directory.CreateDirectory("Assets/Materials");
    }

    // =========================================================================
    // 1. MODELO 3D DA PÍLULA / PAC-DOT (GEMA VÁLIDA)
    // =========================================================================
    private static void CriarPrefabPilulaPacDot()
    {
        GameObject pilulaRaiz = new GameObject("GemaValida");

        // Núcleo Esférico Dourado/Esmeralda
        GameObject esfera = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        esfera.name = "Nucleo_PacDot";
        esfera.transform.SetParent(pilulaRaiz.transform, false);
        esfera.transform.localScale = new Vector3(0.55f, 0.55f, 0.55f);
        Object.DestroyImmediate(esfera.GetComponent<Collider>());

        Color corOuroNeon = new Color(1f, 0.88f, 0.2f);
        Color emissaoOuro = new Color(0.85f, 0.65f, 0.1f);
        Material matPilula = CriarOuObterMaterial("Mat_PacDot_Glow", corOuroNeon, emissaoOuro, 0.95f);

        Renderer rend = esfera.GetComponent<Renderer>();
        if (rend != null && matPilula != null)
        {
            rend.sharedMaterial = matPilula;
        }

        // Anel / Halo decorativo
        GameObject halo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        halo.name = "Halo_Aura";
        halo.transform.SetParent(pilulaRaiz.transform, false);
        halo.transform.localScale = new Vector3(0.7f, 0.02f, 0.7f);
        Object.DestroyImmediate(halo.GetComponent<Collider>());
        Renderer rendHalo = halo.GetComponent<Renderer>();
        if (rendHalo != null && matPilula != null)
        {
            rendHalo.sharedMaterial = matPilula;
        }

        // Colisor Trigger
        SphereCollider col = pilulaRaiz.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.4f;

        // Scripts Semânticos e de Animação
        pilulaRaiz.AddComponent<RotacaoFlutuante>();
        GerenciadorInstancia ger = pilulaRaiz.AddComponent<GerenciadorInstancia>();
        ger.ehValida = true;
        ger.valorPontuacao = 10;
        ger.classeOntologica = "PacDot_GemaValida";
        ger.expressaoDL = "PacDot ⊑ EntidadeConsistente ⊓ ∃temEnergia.Valida";
        ger.descricaoSemantica = "Pílula Pac-Dot Válida. Satisfaz a axiomatização ontológica!";

        PrefabUtility.SaveAsPrefabAsset(pilulaRaiz, CaminhoGemaValida);
        PrefabUtility.SaveAsPrefabAsset(pilulaRaiz, CaminhoGemaResources);
        Object.DestroyImmediate(pilulaRaiz);

        Debug.Log($"<color=#00FF99><b>[Pac-Man Semântico]</b></color> Prefab Pac-Dot estilizado criado em: {CaminhoGemaValida}");
    }

    // =========================================================================
    // 2. MODELO 3D DO FANTASMA BLINKY (OBSTÁCULO INVÁLIDO)
    // =========================================================================
    private static void CriarPrefabFantasmaBlinky()
    {
        GameObject fantasmaRaiz = new GameObject("ObstaculoInvalido");

        Color corFantasma = new Color(1f, 0.12f, 0.2f);
        Color emissaoFantasma = new Color(0.85f, 0.05f, 0.1f);
        Material matCorpo = CriarOuObterMaterial("Mat_Fantasma_Blinky", corFantasma, emissaoFantasma, 0.9f);

        Color corOlhoBranco = Color.white;
        Material matOlho = CriarOuObterMaterial("Mat_Olho_Branco", corOlhoBranco, Color.clear, 0.8f);

        Color corPupilaAzul = new Color(0.05f, 0.2f, 0.95f);
        Material matPupila = CriarOuObterMaterial("Mat_Pupila_Azul", corPupilaAzul, corPupilaAzul * 0.4f, 0.95f);

        // 1. Cabeça (Cúpula Esférica Superior)
        GameObject cabeca = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        cabeca.name = "Cabeca_Cupula";
        cabeca.transform.SetParent(fantasmaRaiz.transform, false);
        cabeca.transform.localPosition = new Vector3(0f, 0.35f, 0f);
        cabeca.transform.localScale = new Vector3(0.85f, 0.85f, 0.85f);
        Object.DestroyImmediate(cabeca.GetComponent<Collider>());
        cabeca.GetComponent<Renderer>().sharedMaterial = matCorpo;

        // 2. Tronco / Saia Cilíndrica
        GameObject corpo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        corpo.name = "Corpo_Saia";
        corpo.transform.SetParent(fantasmaRaiz.transform, false);
        corpo.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        corpo.transform.localScale = new Vector3(0.85f, 0.4f, 0.85f);
        Object.DestroyImmediate(corpo.GetComponent<Collider>());
        corpo.GetComponent<Renderer>().sharedMaterial = matCorpo;

        // 3. Franjas da Saia (3 esferas na base)
        for (int i = 0; i < 3; i++)
        {
            GameObject franja = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            franja.name = $"Franja_{i + 1}";
            franja.transform.SetParent(fantasmaRaiz.transform, false);
            float offsetX = (i - 1) * 0.28f;
            franja.transform.localPosition = new Vector3(offsetX, -0.3f, 0f);
            franja.transform.localScale = new Vector3(0.3f, 0.25f, 0.3f);
            Object.DestroyImmediate(franja.GetComponent<Collider>());
            franja.GetComponent<Renderer>().sharedMaterial = matCorpo;
        }

        // 4. Olho Esquerdo
        GameObject olhoEsq = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        olhoEsq.name = "OlhoEsquerdo";
        olhoEsq.transform.SetParent(fantasmaRaiz.transform, false);
        olhoEsq.transform.localPosition = new Vector3(-0.18f, 0.3f, 0.35f);
        olhoEsq.transform.localScale = new Vector3(0.22f, 0.28f, 0.15f);
        Object.DestroyImmediate(olhoEsq.GetComponent<Collider>());
        olhoEsq.GetComponent<Renderer>().sharedMaterial = matOlho;

        GameObject pupilaEsq = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        pupilaEsq.name = "PupilaEsquerda";
        pupilaEsq.transform.SetParent(olhoEsq.transform, false);
        pupilaEsq.transform.localPosition = new Vector3(0f, 0f, 0.04f);
        pupilaEsq.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
        Object.DestroyImmediate(pupilaEsq.GetComponent<Collider>());
        pupilaEsq.GetComponent<Renderer>().sharedMaterial = matPupila;

        // 5. Olho Direito
        GameObject olhoDir = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        olhoDir.name = "OlhoDireito";
        olhoDir.transform.SetParent(fantasmaRaiz.transform, false);
        olhoDir.transform.localPosition = new Vector3(0.18f, 0.3f, 0.35f);
        olhoDir.transform.localScale = new Vector3(0.22f, 0.28f, 0.15f);
        Object.DestroyImmediate(olhoDir.GetComponent<Collider>());
        olhoDir.GetComponent<Renderer>().sharedMaterial = matOlho;

        GameObject pupilaDir = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        pupilaDir.name = "PupilaDireita";
        pupilaDir.transform.SetParent(olhoDir.transform, false);
        pupilaDir.transform.localPosition = new Vector3(0f, 0f, 0.04f);
        pupilaDir.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
        Object.DestroyImmediate(pupilaDir.GetComponent<Collider>());
        pupilaDir.GetComponent<Renderer>().sharedMaterial = matPupila;

        // Colisor Trigger
        CapsuleCollider col = fantasmaRaiz.AddComponent<CapsuleCollider>();
        col.isTrigger = true;
        col.height = 1.4f;
        col.radius = 0.45f;
        col.center = new Vector3(0f, 0.15f, 0f);

        // Scripts de Animação e Ontologia
        AnimacaoFantasma anim = fantasmaRaiz.AddComponent<AnimacaoFantasma>();
        anim.pupilaEsquerda = pupilaEsq.transform;
        anim.pupilaDireita = pupilaDir.transform;

        GerenciadorInstancia ger = fantasmaRaiz.AddComponent<GerenciadorInstancia>();
        ger.ehValida = false;
        ger.valorPontuacao = -5;
        ger.classeOntologica = "Fantasma_Blinky";
        ger.expressaoDL = "Fantasma ⊑ Perigo ⊓ (A ⊓ ¬A ≡ ⊥)";
        ger.descricaoSemantica = "Fantasma Blinky. Violação ontológica detectada!";

        PrefabUtility.SaveAsPrefabAsset(fantasmaRaiz, CaminhoObstaculoInvalido);
        PrefabUtility.SaveAsPrefabAsset(fantasmaRaiz, CaminhoObstaculoResources);
        Object.DestroyImmediate(fantasmaRaiz);

        Debug.Log($"<color=#00FF99><b>[Pac-Man Semântico]</b></color> Prefab Fantasma Blinky 3D criado em: {CaminhoObstaculoInvalido}");
    }

    // =========================================================================
    // 3. MODELO 3D DO PERSONAGEM PAC-MAN JOGADOR COM BOCA ANIMADA
    // =========================================================================
    private static void CriarPrefabPacManJogador()
    {
        GameObject pacManRaiz = new GameObject("PacManJogador");

        Color corAmareloPacMan = new Color(1f, 0.92f, 0.05f);
        Color emissaoAmarelo = new Color(0.85f, 0.75f, 0.02f);
        Material matPacMan = CriarOuObterMaterial("Mat_PacMan_Amarelo", corAmareloPacMan, emissaoAmarelo, 0.95f);

        Color corPretoOlho = new Color(0.05f, 0.05f, 0.05f);
        Material matOlhoPreto = CriarOuObterMaterial("Mat_Olho_Preto", corPretoOlho, Color.clear, 0.9f);

        // Mandíbula Superior
        GameObject mandibulaSup = new GameObject("MandibulaSuperior");
        mandibulaSup.transform.SetParent(pacManRaiz.transform, false);

        GameObject hemiSup = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        hemiSup.name = "Malha_MandibulaSup";
        hemiSup.transform.SetParent(mandibulaSup.transform, false);
        hemiSup.transform.localPosition = new Vector3(0f, 0.15f, 0f);
        hemiSup.transform.localScale = new Vector3(0.9f, 0.6f, 0.9f);
        Object.DestroyImmediate(hemiSup.GetComponent<Collider>());
        hemiSup.GetComponent<Renderer>().sharedMaterial = matPacMan;

        // Olho Esquerdo Retrô
        GameObject olhoEsq = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        olhoEsq.name = "Olho_Esq";
        olhoEsq.transform.SetParent(mandibulaSup.transform, false);
        olhoEsq.transform.localPosition = new Vector3(-0.25f, 0.35f, 0.25f);
        olhoEsq.transform.localScale = new Vector3(0.12f, 0.2f, 0.12f);
        Object.DestroyImmediate(olhoEsq.GetComponent<Collider>());
        olhoEsq.GetComponent<Renderer>().sharedMaterial = matOlhoPreto;

        // Olho Direito Retrô
        GameObject olhoDir = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        olhoDir.name = "Olho_Dir";
        olhoDir.transform.SetParent(mandibulaSup.transform, false);
        olhoDir.transform.localPosition = new Vector3(0.25f, 0.35f, 0.25f);
        olhoDir.transform.localScale = new Vector3(0.12f, 0.2f, 0.12f);
        Object.DestroyImmediate(olhoDir.GetComponent<Collider>());
        olhoDir.GetComponent<Renderer>().sharedMaterial = matOlhoPreto;

        // Mandíbula Inferior
        GameObject mandibulaInf = new GameObject("MandibulaInferior");
        mandibulaInf.transform.SetParent(pacManRaiz.transform, false);

        GameObject hemiInf = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        hemiInf.name = "Malha_MandibulaInf";
        hemiInf.transform.SetParent(mandibulaInf.transform, false);
        hemiInf.transform.localPosition = new Vector3(0f, -0.15f, 0f);
        hemiInf.transform.localScale = new Vector3(0.9f, 0.6f, 0.9f);
        Object.DestroyImmediate(hemiInf.GetComponent<Collider>());
        hemiInf.GetComponent<Renderer>().sharedMaterial = matPacMan;

        // Script de Animação Chomp
        AnimacaoBocaPacMan anim = pacManRaiz.AddComponent<AnimacaoBocaPacMan>();
        anim.mandibulaSuperior = mandibulaSup.transform;
        anim.mandibulaInferior = mandibulaInf.transform;

        PrefabUtility.SaveAsPrefabAsset(pacManRaiz, CaminhoPacManJogador);
        Object.DestroyImmediate(pacManRaiz);

        Debug.Log($"<color=#00FF99><b>[Pac-Man Semântico]</b></color> Prefab Pac-Man Jogador com boca animada criado em: {CaminhoPacManJogador}");
    }

    private static Material CriarOuObterMaterial(string nome, Color corBase, Color corEmissao, float suavidade = 0.9f)
    {
        string caminhoMat = $"Assets/Materials/{nome}.mat";
        Material matExistente = AssetDatabase.LoadAssetAtPath<Material>(caminhoMat);
        if (matExistente != null)
        {
            matExistente.SetColor("_BaseColor", corBase);
            if (matExistente.HasProperty("_EmissionColor") && corEmissao != Color.clear)
            {
                matExistente.EnableKeyword("_EMISSION");
                matExistente.SetColor("_EmissionColor", corEmissao);
            }
            return matExistente;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Diffuse");
        Material mat = new Material(shader) { name = nome };

        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", corBase);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", corBase);

        if (corEmissao != Color.clear && mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", corEmissao);
        }

        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", suavidade);

        AssetDatabase.CreateAsset(mat, caminhoMat);
        return mat;
    }
}
#endif
