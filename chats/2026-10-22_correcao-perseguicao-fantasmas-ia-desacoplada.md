# Registro de Conversa - Antigravity
**ID da Conversa:** `ccf09517-a259-4c9a-9454-089caf48c213`  
**Data/Hora:** 2026-10-22 15:34:14  
**Título / Tema:** 2026-10-22_correcao-perseguicao-fantasmas-ia-desacoplada  
**Total de Interações:** 3  

---

## Interação 1
*Data/Hora:* 2026-10-22T17:17:53Z

### 👤 Usuária (Lana):
"Por favor, corrijam com urgência o comportamento de perseguição dos fantasmas (MotorRaciocinioSemantico.cs):

Fim da Aglomeração sobre o Pac-Man:

Atualmente, todos os fantasmas colam-se exatamente sobre as coordenadas do Pac-Man, bloqueando o jogador. Cada fantasma deve ter um limite de aproximação estrito (ex: parar de perseguir diretamente quando estiverem a menos de 1,5 metros do corpo do Pac-Man) ou rodar em torno dele mantindo um raio de guarda, em vez de o soterrar.

Desacoplamento Real de Destinos (Papéis Táticos Distintos):

Certifique-se de que os papéis de IA estão a gerar coordenadas de destino visualmente separadas no labirinto:

Blinky: Persegue diretamente o Pac-Man.

Pinky: Mira 4 metros à frente da direção atual do Pac-Man.

Inky: Mira num ponto lateral (flanco esquerdo/direito).

Clyde: Se estiver a menos de 5 metros, recua obrigatoriamente para o seu quadrante de patrulha.

Se todos apontarem para o mesmo vetor, os fantasmas continuam a fundir-se.

Ativação de Evitação de Obstáculos no NavMesh:

Nos componentes NavMeshAgent de cada fantasma, garanta que a propriedade avoidancePriority está descentralizada (valores únicos por fantasma) e que o obstacleAvoidanceType está definido como High Quality para que eles repelam-se mutuamente ao tentar aproximar-se do jogador, abrindo caminhos livres para o utilizador transitar pelo labirinto."


<USER_SETTINGS_CHANGE>
The user changed setting `Model Selection` from None to Gemini 3.8 Flash (High). No need to comment on this change if the user doesn't ask about it. If reporting what model you are, please use a human readable name instead of the exact string.
</USER_SETTINGS_CHANGE>

### 🤖 Assistente (Antigravity):
As correções solicitadas para o comportamento de perseguição dos fantasmas em [MotorRaciocinioSemantico.cs](file:///C:/Users/Lana/Come-come%20semântico/Assets/Scripts/MotorRaciocinioSemantico.cs) foram implementadas e validadas com sucesso:

---

### 1. Fim da Aglomeração sobre o Pac-Man (Limite Estrito de 1,5m)
* **Limite Estrito de Aproximação (`limiteAproximacao = 1.5f`):** Adicionado limite estrito de aproximação ao corpo do Pac-Man. Quando a distância física no plano $XZ$ for $\le 1,5\text{m}$, o avanço frontal contra o centro do jogador é imediatamente interrompido.
* **Raio de Guarda Perimetral:**
  * **NavMesh:** Configurado `navAgent.stoppingDistance = 1.5f` e `navAgent.autoBraking = true`. Se o fantasma atingir o limiar, o destino é reorientado para o perímetro de guarda tangencial em torno do jogador, desacelerando o agente sem bloquear o loop com `isStopped`.
  * **Navegação por Corredores (Fallback):** Caso esteja sem NavMesh, o fantasma calcula vetores tangenciais ortogonais ao vetor Pac-Man $\to$ Fantasma, orbitando suavemente em vigilância ou cessando o deslocamento frontal em vez de soterrar o jogador.

---

### 2. Desacoplamento Real de Destinos (Papéis Táticos Distintos)
Criado o enum formal [PapelTaticoFantasma](file:///C:/Users/Lana/Come-come%20semântico/Assets/Scripts/MotorRaciocinioSemantico.cs) e a rotina [CalcularDestinoTatico](file:///C:/Users/Lana/Come-come%20semântico/Assets/Scripts/MotorRaciocinioSemantico.cs), com resolução individualizada via [ObterPapelTatico](file:///C:/Users/Lana/Come-come%20semântico/Assets/Scripts/MotorRaciocinioSemantico.cs) (reconhece nomes/classes ontológicas e aplica distribuição balanceada *Round-Robin* para clones procedurais):

* 🔴 **Blinky (`Blinky`):** Persegue diretamente a posição do Pac-Man (`posPacMan`), respeitando o raio de parada de 1,5m.
* 🌸 **Pinky (`Pinky`):** Emboscada frontal projetando 4 metros à frente da direção atual do Pac-Man:
  $$\vec{Destino}_{\text{Pinky}} = \vec{pos}_{\text{PacMan}} + (\vec
<truncated 495 bytes>
drante de patrulha (`quadrantePatrulhaClyde`), desobstruindo o corredor.
  * Se a distância for **$\ge 5.0\text{m}$**, avança cautelosamente em perseguição.

---

### 3. Ativação de Evitação de Obstáculos no NavMesh (High Quality)
* **`obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance`:** Ativado o algoritmo contínuo de evitação recíproca nos componentes `NavMeshAgent` dos fantasmas.
* **Prioridades Descentralizadas (`avoidancePriority`):** Atribuídas prioridades únicas por fantasma através de [ObterPrioridadeEvitacao](file:///C:/Users/Lana/Come-come%20semântico/Assets/Scripts/MotorRaciocinioSemantico.cs), garantindo que nunca haja empates ou travamentos mútuos em corredores estreitos:
  * **Blinky:** Prioridade base `20` (trilha de perseguição direta prioritária).
  * **Pinky:** Prioridade base `35` (desvia lateralmente para interceptação).
  * **Inky:** Prioridade base `50` (abre espaço contornando pelos cantos).
  * **Clyde:** Prioridade base `65` (cede passagem a todos durante recuo).
  * Inclui dispersão única baseada no `InstanceID` para garantir unicidade em qualquer quantidade de fantasmas.
* Atualizado também o gerador de prefabs em [GeradorPrefabsEditor.cs](file:///C:/Users/Lana/Come-come%20semântico/Assets/Editor/GeradorPrefabsEditor.cs#L444-L451) com as propriedades de NavMesh pré-configuradas.

---

### 4. Validação e Registro
* **Teste Unitário Automatizado (TESTE 21):** Adicionado ao conjunto de testes em [TestesVirtOntoEPacManSemantico.cs](file:///C:/Users/Lana/Come-come%20semântico/Assets/Editor/TestesVirtOntoEPacManSemantico.cs) validando o cálculo de destinos desacoplados dos 4 agentes, o recuo de Clyde aos 5m, o limite de 1,5m e a unicidade das prioridades de evitação.
* **Compilação:** `dotnet build` executado na solução `Come-come semântico.slnx` com **0 Avisos e 0 Erros**.
* **Memória de Sessão:** Histórico completo registrado em `C:\Users\Lana\Desktop\conversas\2026-10-22_correcao-perseguicao-fantasmas-ia-desacoplada.md`.

**Ações executadas:**
- Ação de ferramenta
- Edição de código

---

## Interação 2
*Data/Hora:* 2026-10-22T17:42:32Z

### 👤 Usuária (Lana):
Por favor, atualize o gerador do cenário (GerenciadorCenarioSemantico.cs ou script de layout do labirinto) para substituir o labirinto atual por um layout estruturado fiel ao mapa clássico do Pac-Man, adaptado para o espaço tridimensional em Unity 6 / XR:

Geometria Exata Baseada em Grid (Matriz 2D Clássica):

Implemente uma matriz ou definição de paredes baseada no design clássico simétrico do Pac-Man (com bordas fechadas, a 'Ghost House' — a caixa central retangular dos fantasmas com a portinha amarela — no centro exato, e os blocos de obstáculo em forma de 'T', 'L' e barras horizontais/verticais).

Garanta que as paredes tenham a espessura de 1,2 metros e altura de 4,0 metros (com isStatic = true e BoxCollider maciço), impedindo qualquer travessia.

Túneis de Teletransporte Lateral (Wrap-around):

Nas aberturas laterais esquerda e direita do labirinto (onde ficam os túneis no jogo original), implemente gatilhos de teletransporte (Trigger). Quando o Pac-Man ou um fantasma atravessar a borda esquerda, são instantaneamente reposicionados na borda direita (e vice-versa), mantendo a fluidez clássica do mapa.

Distribuição Estratégica dos Itens e Power Pellets:

Posicione as 4 Power Pellets (gemas especiais) rigorosamente nos quatro cantos estratégicos do labirinto, tal como no design original.

Distribua as gemas amarelas válidas (espécimes ontológicos de parasitologia) e as instâncias de violação ao longo dos corredores em pontos de grade pré-calculados, garantindo que fiquem acessíveis para a coleta do jogador sem ficarem presas dentro das paredes.

Geração e Baking Automático do NavMesh:

Certifique-se de que, após instanciar os blocos do labirinto clássico, o sistema execute o Bake do NavMesh (ou utilize uma superfície de navegação estática compatível) para que os fantasmas (Blinky, Pinky, Inky e Clyde) possam navegar com precisão pelos corredores estreitos sem colisões anómalas.

### 🤖 Assistente (Antigravity):
O gerador de cenário foi atualizado com sucesso em [GerenciadorCenarioSemantico.cs](file:///C:/Users/Lana/Come-come%20semântico/Assets/GerenciadorCenarioSemantico.cs) para substituir o labirinto anterior pela **geometria estruturada canônica do mapa clássico do Pac-Man**, adaptado para o espaço tridimensional em **Unity 6 / XR**.

---

### 1. Geometria Exata Baseada em Grid (Matriz 2D Clássica 28x30)
* **Matriz Canônica `LAYOUT_PACMAN_CLASSICO`:**
  * Resolução original de **28 colunas $\times$ 30 linhas**.
  * Reproduz fielmente os perímetros externos, a **Ghost House** central com a portinha amarela, os blocos em formato de **"T"**, **"L"** e as barras divisórias longitudinais e transversais.
* **Dimensões e Física Estrita:**
  * **Espessura:** $1,2\text{ m}$ (`espessuraParedes = 1.2f`, `tamanhoCelulaGrid = 1.2f`).
  * **Altura:** $4,0\text{ m}$ (`alturaParedes = 4.0f`).
  * **Física e Oclusão:** Cada bloco recebe `isStatic = true` e um `BoxCollider` maciço (`isTrigger = false`, tamanho $1,2 \times 4,0 \times 1,2\text{ m}$), impedindo qualquer travessia indesejada tanto pelo jogador em XR quanto pelos fantasmas.
  * **Ghost House:** Caixa retangular central com paredes estáticas e a **portinha amarela** (`PortinhaAmarela_GhostHouse`) posicionada no topo com material emissivo néon (`#FFE600`).

---

### 2. Túneis de Teletransporte Lateral (Wrap-around)
* **Gatilhos Laterais:**
  * Implementados nas aberturas da linha 13 da matriz (extremidade esquerda em $C=0$ e direita em $C=27$): `Tunel_WrapAround_Esquerdo` e `Tunel_WrapAround_Direito`.
  * Equipados com `BoxCollider` em modo **Trigger**.
* **Mecanismo de Teletransporte ([TunelTeletransporte](file:///C:/Users/Lana/Come-come%20semântico/Assets/GerenciadorCenarioSemantico.cs#L2079-L2179)):**
  * **Fantasmas (`NavMeshAgent`):** Utiliza `navAgent.Warp(posicaoDestino)` amostrada na malha de navegação (`NavMesh.SamplePosition`), garantindo que o agente não perca o caminho ou colida de forma anômala.
  * **Jogador XR (`CharacterController`):**
<truncated 772 bytes>
egaveis`):**
  * O gerador mapeia automaticamente os pontos navegáveis livres de colisão (onde o caractere da matriz é `.` ou `' '`).
  * As gemas amarelas (espécimes ontológicos de parasitologia) e instâncias de violação são distribuídas estritamente nesses waypoints centrais, garantindo que fiquem **100% acessíveis e desobstruídas das paredes**.

---

### 4. Geração e Baking Automático do NavMesh (Unity 6 / XR)
* **Integração com `NavMeshSurface`:**
  * Método `ExecutarBakeNavMesh()` configurado com:
    * `collectObjects = CollectObjects.All`
    * `useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders`
    * `defaultArea = 0` (Walkable)
  * Disparado automaticamente ao término de `ConstruirLabirinto3D()`, gerando a malha de navegação perfeitamente ajustada aos corredores de $1,2\text{ m}$ para os 4 papéis táticos (Blinky, Pinky, Inky e Clyde).

---

### 5. Validação Automatizada e Compilação
* **Novo Teste Adicionado:** [TESTE 22 em TestesVirtOntoEPacManSemantico.cs](file:///C:/Users/Lana/Come-come%20semântico/Assets/Editor/TestesVirtOntoEPacManSemantico.cs#L883-L968), cobrindo:
  1. Integridade da matriz 28x30.
  2. Dimensões estritas das paredes ($1,2\text{ m} \times 4,0\text{ m}$).
  3. Existência de colisores maciços estáticos.
  4. Presença da portinha amarela da Ghost House.
  5. Acoplamento recíproco dos triggers de teletransporte.
  6. Alocação das 4 Power Pellets e pontos livres de grade.
  7. Configuração e execução do Bake do `NavMeshSurface`.
* **Compilação de Produção:**
  ```bash
  dotnet build "C:\Users\Lana\Come-come semântico\Come-come semântico.slnx"
  # Assembly-CSharp -> Compilação com êxito (0 Avisos, 0 Erros)
  # Assembly-CSharp-Editor -> Compilação com êxito (0 Avisos, 0 Erros)
  ```
* **Memória da Sessão Atualizada:** O histórico completo foi registrado em [2026-10-22_correcao-perseguicao-fantasmas-ia-desacoplada.md](file:///C:/Users/Lana/Desktop/conversas/2026-10-22_correcao-perseguicao-fantasmas-ia-desacoplada.md).

**Ações executadas:**
- Ação de ferramenta
- Edição de código

---

## Interação 3
*Data/Hora:* 2026-10-22T18:15:36Z

### 👤 Usuária (Lana):
"Por favor, corrijam com urgência a distorção severa da geometria do labirinto visível no Unity (GerenciadorCenarioSemantico.cs):

Correção de Escala e Proporção do Mapa Canônico (28x30):

A matriz procedural está a deformar os blocos e a criar paredes gigantescas que bloqueiam e distorcem a câmara.

Redefina rigorosamente o espaçamento da grid (tamanhoCelulaGrid = 1.0f ou valor proporcional fixo) para que o labirinto mantenha proporções simétricas, corredores uniformes e a escala correta para o avatar do Pac-Man e dos fantasmas. As paredes devem ter exatamente espessura constante e altura padronizada (ex: 1.0m de espessura por 3.5m de altura), sem esticar geometrias.

Eliminação Definitiva de Artefatos Cor-de-Rosa (Magenta):

Os shaders e materiais das paredes ou efeitos perderam a referência e ficaram magenta (erro de URP / Material nulo). Certifique-se de que todos os blocos gerados proceduralmente recebem um material URP válido (ex: o material padrão néon azul/ciano das paredes do labirinto) no exato momento da instanciação.

Alinhamento Correto da Câmara XR / Visão do Jogador:

Garanta que o ponto de spawn do jogador (XR Origin / CenaDemoWeb) nasce no corredor inicial correto da Ghost House, com a câmara voltada para o interior do labirinto e com a altura perfeitamente nivelada ao chão, evitando qualquer recorte ou oclusão incorreta pelas paredes recém-ajustadas."

### 🤖 Assistente (Antigravity):
As distorções na geometria, os materiais magenta de URP e o posicionamento da câmara XR foram corrigidos com êxito em [GerenciadorCenarioSemantico.cs](file:///C:/Users/Lana/Come-come%20semântico/Assets/GerenciadorCenarioSemantico.cs) e na cena [CenaDemoWeb.unity](file:///C:/Users/Lana/Come-come%20semântico/Assets/Scenes/DemoWeb/CenaDemoWeb.unity).

---

### 1. Correção de Escala e Proporção do Mapa Canônico (28x30)
* **Grid 1:1 e Proporções Simétricas:**
  * **Espaçamento da Célula:** Redefinido para `tamanhoCelulaGrid = 1.0f` (1 metro cúbico por bloco).
  * **Dimensões das Paredes:** Espessura padronizada em $1,0\text{ m}$ (`espessuraParedes = 1.0f`) e altura unificada em $3,5\text{ m}$ (`alturaParedes = 3.5f`), eliminando deformações ou paredes que estiquem ou obstruam desproporcionalmente o campo de visão.
  * **Colisores Sólidos e Geometria Limpa:**
    * Cada bloco é instanciado com escala local exata de $(1.0, 3.5, 1.0)$ e `BoxCollider` maciço (`size = Vector3.one`, `isTrigger = false`).
    * **Remoção de frisos secundários com cubos filhos:** Eliminamos o cubo filho adicional (`trimLED`), que gerava centenas de objetos primitivos aninhados, duplicando draw calls, gerando colisões parasitas e exibindo cubos magenta no topo das paredes.
* **Correção no Cálculo de Altura do Chão ([`ObterAlturaSuperficieChao`](file:///C:/Users/Lana/Come-come%20semântico/Assets/GerenciadorCenarioSemantico.cs#L604-L640)):**
  * O `planoChao` agora é a **autoridade geométrica absoluta**.
  * Corrigido o bug onde o raycast físico atingia o topo das paredes ($y = 3,5\text{m}$) e considerava o topo da parede como sendo o "chão", o que fazia entidades e o jogador serem empurrados para cima das paredes. O chão agora é detectado com exatidão na superfície do plano ($y = 0,0\text{ m}$).

---

### 2. Eliminação Definitiva de Artefatos Cor-de-Rosa (Magenta / URP)
* **Criação de Materiais URP Nativos em Disco:**
  * [Mat_Parede_Labirinto_AzulNeon.mat](file:///C:/Users/Lana/Come-come%20semântic
<truncated 1472 bytes>
st House (onde há 10 metros de largura livre, sem nenhuma parede frontal ou lateral para colidir).
  * Coordenada de spawn simétrica: $x = 0,0\text{ m}$, $z = -1,5\text{ m}$, $y = 0,0\text{ m}$ (assentado no chão).
* **Orientação e Prevenção de Recorte:**
  * A rotação inicial é travada em `Quaternion.identity` (encarando o Norte), posicionando o jogador de frente para a Ghost House e com vista ampla para os corredores internos.
  * O plano de corte da câmara (`cameraPrincipalXR.nearClipPlane`) é configurado para $0,05\text{ m}$, impedindo que a aproximação de paredes gere oclusões anômalas ou recortes indesejados.
  * A posição inicial de repouso do `XR Origin` na cena [CenaDemoWeb.unity](file:///C:/Users/Lana/Come-come%20semântico/Assets/Scenes/DemoWeb/CenaDemoWeb.unity) foi sincronizada para $(0, 0, -1.5)$, garantindo visão alinhada tanto no Editor quanto no Play Mode.

---

### 4. Validação e Compilação
* **Suíte de Testes Automatizados:** O [TESTE 22 em TestesVirtOntoEPacManSemantico.cs](file:///C:/Users/Lana/Come-come%20semântico/Assets/Editor/TestesVirtOntoEPacManSemantico.cs#L883-L968) foi atualizado para validar:
  1. Integridade da matriz 28x30 com 28 colunas exatas em todas as 30 linhas.
  2. Dimensões estritas das paredes: $1,0\text{m}$ de espessura, $3,5\text{m}$ de altura e $1,0\text{m}$ de célula.
  3. Ausência de shaders magenta ou materiais de erro nas paredes e na porta.
  4. Mapeamento do spawn no corredor da Ghost House.
  5. Bake de NavMesh concluído com sucesso.
* **Resultado da Compilação:**
  ```bash
  dotnet build "C:\Users\Lana\Come-come semântico\Come-come semântico.slnx"
  # Assembly-CSharp -> Compilação com êxito (0 Avisos, 0 Erros)
  # Assembly-CSharp-Editor -> Compilação com êxito (0 Avisos, 0 Erros)
  ```
* **Memória da Sessão Atualizada:** O histórico completo foi registrado em [2026-10-22_correcao-perseguicao-fantasmas-ia-desacoplada.md](file:///C:/Users/Lana/Desktop/conversas/2026-10-22_correcao-perseguicao-fantasmas-ia-desacoplada.md).

**Ações executadas:**
- Ação de ferramenta
- Edição de código

---
