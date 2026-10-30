# Registro de Sessão: Correção da Perseguição dos Fantasmas, Desacoplamento Tático e Evitação NavMesh

- **Data e Hora:** 2026-10-22 14:32:00 -03:00
- **Participantes:** Lana e Assistente Antigravity
- **Projeto:** Pac-Man Semântico (Semantic Pac-Man) - CIn-UFPE / Unity
- **Repositório Oficial:** `C:\Users\Lana\Come-come semântico` (`https://github.com/lanalcantara/Pac-man-Semantico.git`)

---

## 1. Solicitações da Usuária (Lana)
"Por favor, corrijam com urgência o comportamento de perseguição dos fantasmas (`MotorRaciocinioSemantico.cs`):

1. **Fim da Aglomeração sobre o Pac-Man:**
   - Todos os fantasmas colavam-se exatamente sobre as coordenadas do Pac-Man, bloqueando o jogador.
   - Cada fantasma deve ter um limite de aproximação estrito (ex: parar de perseguir diretamente quando estiverem a menos de 1,5 metros do corpo do Pac-Man) ou rodar em torno dele mantendo um raio de guarda, em vez de o soterrar.

2. **Desacoplamento Real de Destinos (Papéis Táticos Distintos):**
   - Papéis de IA gerando coordenadas de destino visualmente separadas no labirinto:
     - **Blinky:** Persegue diretamente o Pac-Man.
     - **Pinky:** Mira 4 metros à frente da direção atual do Pac-Man.
     - **Inky:** Mira num ponto lateral (flanco esquerdo/direito).
     - **Clyde:** Se estiver a menos de 5 metros, recua obrigatoriamente para o seu quadrante de patrulha.
   - Evitar que todos apontem para o mesmo vetor, eliminando fusão dos agentes.

3. **Ativação de Evitação de Obstáculos no NavMesh:**
   - Nos componentes `NavMeshAgent` de cada fantasma, descentralizar `avoidancePriority` com valores únicos por fantasma.
   - Definir `obstacleAvoidanceType` como `HighQualityObstacleAvoidance` para que se repilam mutuamente, abrindo caminhos livres para o jogador navegar pelo labirinto."

---

## 2. Ações e Implementações Realizadas

### 2.1. Refatoração do `Assets/Scripts/MotorRaciocinioSemantico.cs`
1. **Enum `PapelTaticoFantasma`:**
   - Criado enum formal com os 4 papéis táticos clássicos: `Blinky`, `Pinky`, `Inky`, `Clyde`.
2. **Método `ObterPapelTatico(GameObject ghostObj, string id)`:**
   - Identifica o papel através de substrings de nome/id/classe ontológica.
   - Em caso de clones genéricos (`ObstaculoInvalido`), aplica distribuição balanceada em fila (Round-Robin: 0 -> Blinky, 1 -> Pinky, 2 -> Inky, 3 -> Clyde).
3. **Cálculo de Destinos Desacoplados (`CalcularDestinoTatico`):**
   - **Blinky:** Mira diretamente na posição do Pac-Man (`posPacMan`).
   - **Pinky:** Projeta vetor à frente da direção de visão do Pac-Man: `posPacMan + (dirPacMan * 4.0f)`.
   - **Inky:** Projeta vetor no flanco lateral: `posPacMan + (dirFlanco * (4.0f * sinalLado))`.
   - **Clyde:** Verifica distância euclidiana no plano XZ: se `dist < 5.0f`, recua obrigatoriamente para o quadrante de patrulha (`quadrantePatrulhaClyde`); se `dist >= 5.0f`, avança cautelosamente.
4. **Fim da Aglomeração e Limite Estrito de Aproximação (1.5m):**
   - Criado campo `limiteAproximacao = 1.5f`.
   - Quando o fantasma está a $\le 1.5\text{m}$ do Pac-Man, o avanço direto contra o centro do corpo do jogador é interrompido.
   - No NavMesh, calcula ponto perimetral de guarda e utiliza `stoppingDistance = 1.5f`, desacelerando o fantasma sem congelar o agente.
   - Na navegação por corredor (fallback sem NavMesh), aplica translação tangencial suave de patrulha perimetral ou parada em vez de empurrar o jogador.
5. **Evitação de Obstáculos no NavMesh de Alta Qualidade:**
   - `obstacleAvoidanceType` configurado para `ObstacleAvoidanceType.HighQualityObstacleAvoidance`.
   - `avoidancePriority` descentralizada com valores exclusivos por fantasma via `ObterPrioridadeEvitacao(papel, instanceId)`:
     - Blinky: Base 20 (+ offset por instância).
     - Pinky: Base 35 (+ offset por instância).
     - Inky: Base 50 (+ offset por instância).
     - Clyde: Base 65 (+ offset por instância).
   - `radius = 0.45f` e `autoBraking = true`.

### 2.2. Atualização do `Assets/Editor/GeradorPrefabsEditor.cs`
- No método `CriarPrefabFantasmaBlinky()`, adicionada configuração direta do componente `NavMeshAgent` no prefab com:
  - `speed = 2.6f`
  - `stoppingDistance = 1.5f`
  - `radius = 0.45f`
  - `obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance`
  - `avoidancePriority = 20`

### 2.3. Criação do TESTE 21 em `Assets/Editor/TestesVirtOntoEPacManSemantico.cs`
- Implementado teste unitário automatizado validando:
  - Atribuição correta dos 4 papéis táticos (`Blinky`, `Pinky`, `Inky`, `Clyde`).
  - Pinky mirando 4 metros à frente.
  - Inky mirando no flanco lateral.
  - Clyde recuando para o quadrante quando $< 5\text{m}$ e avançando quando $\ge 5\text{m}$.
  - Limite estrito de aproximação ($1.5\text{m}$) ativando raio de guarda.
  - Conjunto de 4 prioridades `avoidancePriority` estritamente distintas (sem empate).
  - Configuração ativa de `HighQualityObstacleAvoidance`.

---

## 3. Validação e Compilação (Parte 1)
- Executado `dotnet build "C:\Users\Lana\Come-come semântico\Come-come semântico.slnx"`:
  - `Assembly-CSharp.dll`: Compilação com êxito (0 Erros, 0 Avisos).
  - `Assembly-CSharp-Editor.dll`: Compilação com êxito (0 Erros, 0 Avisos).

---

## 4. Nova Solicitação da Usuária (Lana): Labirinto Clássico 3D do Pac-Man (Unity 6 / XR)
"Por favor, atualize o gerador do cenário (GerenciadorCenarioSemantico.cs ou script de layout do labirinto) para substituir o labirinto atual por um layout estruturado fiel ao mapa clássico do Pac-Man, adaptado para o espaço tridimensional em Unity 6 / XR:

1. **Geometria Exata Baseada em Grid (Matriz 2D Clássica):**
   - Matriz 2D simétrica fiel ao design clássico do Pac-Man arcade (28 colunas x 30 linhas).
   - Bordas fechadas, 'Ghost House' central com portinha amarela, blocos em 'T', 'L' e barras horizontais/verticais.
   - Paredes com espessura estrita de 1,2 metros e altura de 4,0 metros, `isStatic = true` e `BoxCollider` maciço impedindo travessia.
2. **Túneis de Teletransporte Lateral (Wrap-around):**
   - Aberturas laterais esquerda e direita com gatilhos `Trigger`.
   - Reposicionamento instantâneo entre extremidades opostas (com cooldown anti-loop), compatível com `NavMeshAgent.Warp()`, `CharacterController` e `Rigidbody`.
3. **Distribuição Estratégica dos Itens e Power Pellets:**
   - 4 Power Pellets rigorosamente nos quatro cantos estratégicos do labirinto clássico.
   - Gemas amarelas válidas (espécimes ontológicos) e instâncias de violação distribuídas ao longo dos corredores em pontos pré-calculados de grade, sem sobreposição com paredes.
4. **Geração e Baking Automático do NavMesh:**
   - Execução automática de Bake do `NavMeshSurface` com coleta de colisores físicos para navegação precisa dos fantasmas sem colisões anômalas."

---

## 5. Implementações Realizadas (Labirinto Clássico 3D)

### 5.1. Matriz Clássica e Geometria 3D (`Assets/GerenciadorCenarioSemantico.cs`)
1. **Definição Canônica `LAYOUT_PACMAN_CLASSICO`:**
   - Matriz 28x30 com os caracteres clássicos:
     - `#`: Paredes sólidas.
     - `.`: Corredor transitável com pontos de grade para gemas válidas (espécimes ontológicos de parasitologia).
     - `P`: Power Pellets canônicas nos 4 cantos (`R3,C1`, `R3,C26`, `R22,C1`, `R22,C26`).
     - `G`: Ghost House interior (spawn dos 4 fantasmas).
     - `-`: Portinha amarela emissiva da Ghost House.
     - `T`: Túneis de teletransporte lateral (Wrap-around nas extremidades da linha 13).
     - `S`: Ponto canônico de spawn inicial do Pac-Man.
2. **Dimensões e Física Estrita:**
   - `alturaParedes = 4.0f`
   - `espessuraParedes = 1.2f`
   - `tamanhoCelulaGrid = 1.2f`
   - Cada bloco de parede é instanciado com `isStatic = true`, `BoxCollider` sólido (não-trigger) de $1.2\text{m} \times 4.0\text{m} \times 1.2\text{m}$ e acabamento visual com frizo néon de alto contraste (estética arcade XR).
3. **Ghost House com Portinha Amarela:**
   - A portinha amarela néon (`PortinhaAmarela_GhostHouse`) é posicionada no centro superior da casa dos fantasmas com material amarelo emissivo (`_EmissionColor = #FFE600`).
4. **Túneis de Teletransporte Wrap-around (`PacMan.Navigation.TunelTeletransporte`):**
   - Triggers `Tunel_WrapAround_Esquerdo` e `Tunel_WrapAround_Direito` posicionados nas aberturas laterais.
   - Script `TunelTeletransporte` acoplado aos dois gatilhos com referência cruzada (`transformSaidaDestino`), suporte a `NavMeshAgent.Warp()`, `CharacterController` e `Rigidbody`, além de cooldown de 0.8s prevenindo repetição em 'ping-pong'.
5. **Distribuição de Itens e Coleta Segura:**
   - As 4 Power Pellets são registradas e alocadas com prioridade máxima nos 4 cantos canônicos.
   - Corredores pré-calculados em grade (`m_PontosCorredoresNavegaveis`) garantem que gemas e instâncias ontológicas sejam instanciadas no centro exato dos corredores transitáveis, sem tocar nem penetrar nas paredes.
6. **Baking Automático do NavMesh (Unity 6 / XR):**
   - Método `ExecutarBakeNavMesh()` configura e executa `NavMeshSurface.BuildNavMesh()` com:
     - `collectObjects = CollectObjects.All`
     - `useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders`
     - `defaultArea = 0` (Walkable)
   - Disparado automaticamente após `ConstruirLabirinto3D()`.

### 5.2. Teste Automatizado TESTE 22 (`Assets/Editor/TestesVirtOntoEPacManSemantico.cs`)
- Adicionado TESTE 22 à suíte de testes com validações rigorosas:
  - Dimensão da matriz clássica: 30 linhas e 28 colunas exatas.
  - Parâmetros físicos: 4.0m de altura, 1.2m de espessura de parede e célula.
  - Presença de paredes estáticas com `BoxCollider` maciço.
  - Instanciação da `PortinhaAmarela_GhostHouse`.
  - Configuração recíproca dos túneis de teletransporte lateral (Wrap-around Triggers).
  - Presença de exatamente 4 Power Pellets nos cantos canônicos e >100 pontos navegáveis de corredor.
  - Execução bem-sucedida do Bake de NavMesh via `NavMeshSurface`.

---

## 6. Validação e Compilação (Parte 2)
- Executado `dotnet build "C:\Users\Lana\Come-come semântico\Come-come semântico.slnx"`:
  - `Assembly-CSharp.dll`: Compilação com êxito (0 Erros, 0 Avisos).
  - `Assembly-CSharp-Editor.dll`: Compilação com êxito (0 Erros, 0 Avisos).

---

## 7. Nova Solicitação da Usuária (Lana): Correção de Escala, Materiais Magenta e Alinhamento da Câmara XR
"Por favor, corrijam com urgência a distorção severa da geometria do labirinto visível no Unity (GerenciadorCenarioSemantico.cs):

1. **Correção de Escala e Proporção do Mapa Canônico (28x30):**
   - A matriz procedural estava a deformar os blocos e a criar paredes gigantescas que bloqueiam e distorcem a câmara.
   - Redefinir rigorosamente o espaçamento da grid (tamanhoCelulaGrid = 1.0f) para que o labirinto mantenha proporções simétricas, corredores uniformes e escala correta para o avatar do Pac-Man e dos fantasmas.
   - Paredes com espessura constante e altura padronizada (1.0m de espessura por 3.5m de altura), sem esticar geometrias.
2. **Eliminação Definitiva de Artefatos Cor-de-Rosa (Magenta):**
   - Shaders e materiais das paredes perderam a referência e ficaram magenta (erro de URP / Material nulo).
   - Garantir que todos os blocos gerados proceduralmente recebam um material URP válido (néon azul/ciano das paredes do labirinto) no momento da instanciação.
3. **Alinhamento Correto da Câmara XR / Visão do Jogador:**
   - Garantir que o ponto de spawn do jogador (XR Origin / CenaDemoWeb) nasce no corredor inicial correto da Ghost House, com a câmara voltada para o interior do labirinto e com a altura perfeitamente nivelada ao chão, evitando qualquer recorte ou oclusão incorreta pelas paredes recém-ajustadas."

---

## 8. Implementações Realizadas (Escala, Materiais URP e Câmara XR)

### 8.1. Padronização Proporcional 1:1 e Geometria Sem Distorções
1. **Padronização Físico-Métrica:**
   - `tamanhoCelulaGrid = 1.0f` (fixo em 1 metro por célula).
   - `espessuraParedes = 1.0f` (espessura constante e uniforme de 1 metro).
   - `alturaParedes = 3.5f` (altura padrão de 3.5 metros para visão desobstruída em XR).
2. **Eliminação de Deformações em Malhas:**
   - Blocos de parede instanciados com escala $1.0\text{m} \times 3.5\text{m} \times 1.0\text{m}$ e `BoxCollider` maciço (`col.size = Vector3.one`, `col.center = Vector3.zero`).
   - Removidos cubos secundários filhos de friso luminoso (`trimLED`) que geravam cubos duplicados, colisões parasitas e shaders magenta.
3. **Correção de Altura do Chão (`ObterAlturaSuperficieChao`):**
   - Corrigido problema crítico onde o raycast de chão atingia o topo das paredes de 3.5m/4.0m e considerava o topo da parede como a superfície do chão.
   - O `planoChao` agora é a autoridade geométrica absoluta (`colChao.bounds.max.y` ou `planoChao.position.y`), garantindo que o chão seja detectado estritamente em $y = 0.0\text{m}$.

### 8.2. Eliminação Definitiva de Shaders Magenta / Rosa (URP)
1. **Criação de Materiais URP Oficiais em Disco:**
   - [Mat_Parede_Labirinto_AzulNeon.mat](file:///C:/Users/Lana/Come-come%20semântico/Assets/Materials/Mat_Parede_Labirinto_AzulNeon.mat): Material URP Lit com shader `Universal Render Pipeline/Lit` (GUID `933532a4fcc9baf4fa0491de14d08ed7`), base azul néon profundo (`#0A1E52`), metálico 0.8, suavidade 0.85 e emissão néon ciano (`#00D4FF`).
   - [Mat_Porta_GhostHouse_Amarela.mat](file:///C:/Users/Lana/Come-come%20semântico/Assets/Materials/Mat_Porta_GhostHouse_Amarela.mat): Material URP Lit com shader `Universal Render Pipeline/Lit`, base amarela e emissão néon amarela (`#FFE600`).
2. **Resolução Robusta em Runtime e Batching SRP:**
   - Implementados métodos `ObterMaterialParedeValido()` e `ObterMaterialPortaGhostHouseValido()`.
   - Paredes recebem `rend.sharedMaterial = matParede`, compartilhando uma única instância na memória de vídeo, ativando o SRP Batcher do URP e eliminando qualquer chance de shader `Hidden/InternalErrorShader` (rosa/magenta).

### 8.3. Alinhamento da Câmara XR / Spawn do Jogador
1. **Spawn Canônico no Corredor da Ghost House:**
   - Em `LAYOUT_PACMAN_CLASSICO`, o ponto `'S'` foi alocado no corredor da Ghost House (Linha 16, Coluna 13 e 14), onde há 10 metros livres de largura sem qualquer parede bloqueando a visão.
   - Posição centralizada: $x = 0.0\text{m}$, $z = -1.5\text{m}$, $y = 0.0\text{m}$ (piso perfeitamente nivelado).
2. **Orientação e Configuração de Câmara:**
   - Rotação inicial definida para o Norte (`Quaternion.identity`), com a câmara encarando o interior do labirinto e a fachada da Ghost House.
   - `cameraPrincipalXR.nearClipPlane = 0.05f` para prevenir recorte visual ou oclusão próxima.
   - Sincronização direta com `ControladorXRJogador.pontoSpawn` e atualização de coordenadas iniciais no asset de cena `CenaDemoWeb.unity`.

---

## 9. Validação e Compilação Final de Produção
- Executado `dotnet build "C:\Users\Lana\Come-come semântico\Come-come semântico.slnx"`:
  - `Assembly-CSharp.dll`: Compilação com êxito (0 Erros, 0 Avisos).
  - `Assembly-CSharp-Editor.dll`: Compilação com êxito (0 Erros, 0 Avisos).
- Atualizado TESTE 22 em `Assets/Editor/TestesVirtOntoEPacManSemantico.cs` validando os parâmetros 1.0m/3.5m, materiais URP sem magenta e spawn no corredor da Ghost House.

