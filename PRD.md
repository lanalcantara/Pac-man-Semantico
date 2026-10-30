# Documento de Requisitos do Produto (PRD)
## Pac-Man Semântico (VirtOnto) - CIn-UFPE

---

### 1. Visão Geral e Objetivos do Projeto
O **Pac-Man Semântico (VirtOnto)** é um jogo e ambiente interativo acadêmico desenvolvido no Centro de Informática da Universidade Federal de Pernambuco (CIn-UFPE) para a disciplina de **Realidade Virtual e Aumentada (RVA)**.

O objetivo central do projeto é unir a consagrada jogabilidade arcade do clássico **Pac-Man dos anos 80** com raciocínio semântico baseado em **Ontologias OWL** e regras **SWRL**, criando um ambiente lúdico e imersivo capaz de:
1. Ensino interativo de conceitos de Parasitologia médica (classes de helmintos, protozoários, ciclos biológicos, hospedeiros definitivos/intermediários).
2. Validação ontológica em tempo real: o ato do Pac-Man devorar itens ou enfrentar inimigos corresponde à validação e classificação formal de axiomas na Lógica de Descrição $\mathcal{ALCQ}(\mathcal{D})$.
3. Experiência de visualização híbrida: modo de desktop e WebGL em perspectiva isométrica **Pinball 3D** (estilo mesa pinball 3D retrô) e modo totalmente imersivo em óculos de realidade virtual **Meta Quest (OpenXR)**.

---

### 2. Personagens e Identidade Visual

#### 2.1 Pac-Man (Protagonista / Jogador)
- **Design Procedural 3D:** Esfera amarela clássica (`Mat_PacMan_Amarelo`, `#FFD700`) dividida em mandíbula superior (`MandibulaSuperior`) e inferior (`MandibulaInferior`), com olhos expressivos (`Olho_Esq`, `Olho_Dir`).
- **Animação Dinâmica ("Waka-Waka"):** Script [AnimacaoBocaPacMan.cs](file:///d:/VirtOnto-main/VirtOnto-main/Assets/AnimacaoBocaPacMan.cs) realiza a rotação periódica das mandíbulas, sincronizando a velocidade de mastigação e os efeitos sonoros procedurais retrô de "chomp" com a velocidade de deslocamento.
- **Escala Física:** $0.80$ em todos os eixos, garantindo passagem folgada e fluida pelas canaletas e curvas do labirinto sem atrito lateral.
- **Física e Travas:** Controlado via `Rigidbody` contínuo dinâmico com travamento de rotações e do eixo $Y$ (`FreezePositionY`), assentado perfeitamente no nível do solo ($y = 0.0\text{m}$ na base, $y = 0.40\text{m}$ no centro da esfera).

#### 2.2 Os 4 Fantasmas Canônicos
- **Modelagem:** Cápsulas 3D estilizadas com base recortada procedural e pares de olhos brancos com pupilas direcionais.
- **Cores Oficiais dos Anos 80:**
  1. **Blinky (Vermelho - `#FF0000`):** Perseguidor direto (Shadow/Chaser).
  2. **Pinky (Rosa - `#FFB8FF`):** Estrategista de emboscada (Speedy/Ambusher).
  3. **Inky (Ciano - `#00FFFF`):** Flanqueador tático (Bashful/Flanker).
  4. **Clyde (Laranja - `#FFB852`):** Patrulheiro tímido e dispersivo (Pokey/Disperser).
- **Inteligência Artificial e NavMesh:** Navegam exclusivamente através de malha de navegação (`NavMeshSurface`), com pontos de spawn na gaiola central do labirinto.
- **Estados Semânticos:**
  - *Patrol / Chase (Hostil):* Perseguem o Pac-Man. O contato direto resulta em perda de 1 vida e respawn seguro do jogador.
  - *Vulnerable (Azul Escuro Piscante):* Ativado após o Pac-Man devorar uma **Power Pellet**. Os fantasmas reduzem a velocidade, fogem do jogador e podem ser devorados, rendendo $+200$ pontos.

#### 2.3 Itens do Labirinto
- **Gemas Ontológicas (Pac-Dots):** Esferas douradas de tamanho reduzido ($\text{scale} \approx 0.20$) espalhadas por todas as vias do labirinto. Cada gema representa uma instância de um conceito de Parasitologia (ex.: *Ascaris lumbricoides*, *Enterobius vermicularis*, *Giardia lamblia*).
- **Power Pellets (Gemas Especiais):** Esferas luminosas de tamanho intermediário ($\text{scale} \approx 0.50$) posicionadas estrategicamente nos quatro cantos do labirinto. Possuem animação de pulsação suave em escala e emissão. Ao serem consumidas, ativam o estado de vulnerabilidade dos fantasmas por 8 segundos.
- **Cerejas / Frutas Bônus:** Modelos procedurais especiais que surgem no centro do mapa concedendo pontuação extra.

---

### 3. Labirinto e Arquitetura Espacial
- **Design:** Fiel à malha clássica simétrica arcade dos anos 80, com corredores horizontais, verticais, câmara central de renascimento dos fantasmas (Ghost House) e portais de teletransporte lateral (Wrap-around tunnels).
- **Materiais e Cores:**
  - Paredes: Tom azul-violáceo neon característico (`Mat_Parede_Labirinto_AzulNeon`, HSL compatível com a paleta do Protégé).
  - Piso: Superfície escura texturizada antirreflexiva (`Mat_ChaoLabirinto_Escuro`).
- **Dimensões e Proporções:** Largura de canaleta de $1.8\text{m}$, garantindo circulação simultânea sem travamentos físicos.

---

### 4. Sistema de Câmeras e Perspectivas

| Modo | Ângulo / FOV | Descrição |
|---|---|---|
| **Pinball 3D (Padrão)** | Pitch $54^\circ$, FOV $62^\circ$, Altura $15.5\text{m}$, Offset $Z = -11.5\text{m}$ | Visão inclinada dinâmica de cima para baixo com tracking suave em interpolação linear (`Lerp`), simulando a experiência das clássicas mesas de Pinball 3D. |
| **Mesa Completa** | Pitch $55^\circ$, FOV $65^\circ$, Altura $22.0\text{m}$, Offset $Z = -14.5\text{m}$ | Visão aérea panorâmica que enquadra todo o tabuleiro do labirinto simultaneamente. |
| **Terceira Pessoa Baixa** | Pitch $22^\circ$, FOV $65^\circ$, Altura $2.5\text{m}$, Offset $Z = -3.8\text{m}$ | Visão logo atrás do ombro do Pac-Man, acompanhando curvas em tempo real. |
| **Primeira Pessoa VR** | Alinhada ao HMD do Meta Quest, FOV Roomscale | Imersão completa em realidade virtual onde o usuário caminha e enxerga através dos olhos do Pac-Man. |

*Atalho:* Pressionar tecla **`C`** para alternar instantaneamente entre os modos de câmera.

---

### 5. Mecânicas de Jogabilidade, Telas e Interface (HUD)

#### 5.1 Tela Inicial de Entrada (Modal PLAY)
- Ao abrir o jogo, a execução inicia pausada (`Time.timeScale = 0.0f`).
- Um modal translúcido com moldura neon exibe o título **PAC-MAN SEMÂNTICO (VirtOnto)** e o botão de destaque **`[ ▶ INICIAR JOGO (PLAY) ]`**.
- O clique no botão aciona o `EventSystem` da cena, destravando o tempo (`Time.timeScale = 1.0f`) e iniciando a partida.

#### 5.2 Sistema de Vidas e Game Over
- O jogador inicia com **3 vidas**.
- As vidas restantes são exibidas no HUD por meio de ícones de **Pac-Mans pequenos amarelos**.
- Cada contato com fantasma hostil consome 1 vida, executa feedback sonoro e visual de colisão (Juice effect), repele o fantasma agressor e reposiciona o jogador no ponto de spawn inicial.
- Quando as vidas se esgotam ($0$), o jogo exibe a tela de **GAME OVER** com opção de reiniciar a partida via reload de cena.

#### 5.3 Painel Lateral Semântico (HUD Arcade Retrô)
- Canvas configurado com `Canvas Scaler` no modo *Scale With Screen Size* (1920x1080).
- Exibe em tempo real:
  - Pontuação acumulada.
  - Vidas restantes em ícones clássicos.
  - Nome da última instância parasitológica capturada (ex.: `Ascaris_lumbricoides_01`).
  - Classe taxonômica inferida na ontologia (ex.: `HelminthoIntestinal`).
  - Axioma em Lógica de Descrição ($\mathcal{DL}$) gerado.
- Botão **`[ X ]`** superior direito permite minimizar o painel lateral para não obstruir a visão da arena. Tecla de atalho: **`H`**.

---

### 6. Controles e Entradas

- **Teclado (Desktop / WebGL):**
  - Movimentação direcional: Teclas clássicas **`W`**, **`A`**, **`S`**, **`D`** e **Setas Direcionais** (`↑`, `↓`, `←`, `→`).
  - Alternar Câmera: **`C`**.
  - Minimizar/Restaurar HUD: **`H`**.
  - Simular Power Pellet (Debug): **`P`**.
- **Realidade Virtual (Meta Quest / OpenXR):**
  - Comando Esquerdo: Thumbstick analógico para locomoção suave (Smooth Locomotion).
  - Comando Direito: Thumbstick analógico para rotação angular rápida (Snap Turn de $45^\circ$) e acionamento por Grip/Trigger.
  - Feedback Háptico dual em ambos os controladores ao consumir gemas ou sofrer dano.

---

### 7. Arquitetura de Backend e Ontologias
- **Ontologia OWL:** Modelada em Protégé com formalismo $\mathcal{ALCQ}(\mathcal{D})$.
- **Fontes de Dados:**
  - [parasitology_ontology.json](file:///d:/VirtOnto-main/VirtOnto-main/Assets/Resources/parasitology_ontology.json): Mapeamento hierárquico de classes, subclasses e restrições.
  - [parasitology_spatial_graph_3d.json](file:///d:/VirtOnto-main/VirtOnto-main/Assets/Resources/parasitology_spatial_graph_3d.json): Grafo tridimensional de relações ontológicas.
- **Backend Java Spring Boot (Opcional):** Microsserviço REST responsável por persistência e inferências complexas de grafos via endpoints `/api/ontology/nodes`, `/api/ontology/edges` e `/api/ontology/graph`.
