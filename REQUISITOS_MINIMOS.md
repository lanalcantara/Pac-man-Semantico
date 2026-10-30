# Requisitos Mínimos do Sistema e Guia de Instalação
## Pac-Man Semântico (VirtOnto) - CIn-UFPE

Este documento descreve todos os requisitos de hardware, software, dependências e o procedimento passo a passo para clonar, configurar e executar o projeto em uma nova máquina de desenvolvimento.

---

### 1. Requisitos de Hardware

#### 1.1 Ambiente de Desenvolvimento (Desktop / Editor Unity)
| Componente | Requisito Mínimo | Requisito Recomendado |
|---|---|---|
| **Processador (CPU)** | Intel Core i5 8ª Geração / AMD Ryzen 5 2600 (4 núcleos / 8 threads) | Intel Core i7 12ª Geração / AMD Ryzen 7 5700X (8+ núcleos) |
| **Memória RAM** | 16 GB DDR4 | 32 GB DDR4/DDR5 |
| **Placa de Vídeo (GPU)** | NVIDIA GeForce GTX 1060 (6 GB) / AMD Radeon RX 580 (8 GB) compatível com DirectX 11 / Vulkan | NVIDIA GeForce RTX 3060 (12 GB) ou superior |
| **Armazenamento** | 25 GB livres em SSD (SATA III) | 50 GB livres em SSD NVMe M.2 (armazenamento rápido acelera a compilação de shaders e scripts Unity) |
| **Sistema Operacional** | Windows 10 (64-bit, versão 21H2+) ou Windows 11 | Windows 11 (64-bit) |

#### 1.2 Dispositivo de Realidade Virtual (Opcional - Modo VR)
- **Headset Homologado:** Meta Quest 2, Meta Quest 3 ou Meta Quest Pro.
- **Conexão:** Cabo Meta Quest Link (USB 3.2 Gen 1 de alta velocidade) ou Air Link via rede Wi-Fi 6 (5 GHz).

---

### 2. Requisitos de Software e Ferramentas

1. **Unity Editor:**
   - **Versão Exata:** `Unity 6000.3.24f1` (Unity 6 LTS).
   - **Módulos Obrigatórios (via Unity Hub):**
     - *Microsoft Visual Studio / VS Code Support*
     - *WebGL Build Support* (para builds e testes no navegador)
     - *Android Build Support* (incluindo Android SDK & NDK Tools e OpenJDK) para builds nativas no Meta Quest
     - *OpenXR Plugin*
2. **Ambiente .NET / C#:**
   - [.NET 8.0 SDK ou .NET 9.0 SDK](https://dotnet.microsoft.com/download) instalado e configurado nas variáveis de ambiente do sistema (`PATH`).
3. **Controle de Versão:**
   - [Git](https://git-scm.com/) (versão 2.40+) com suporte a [Git LFS](https://git-lfs.com/) instalado:
     ```bash
     git lfs install
     ```
4. **Assistente de IA / IDE:**
   - **Antigravity IDE** instalado no novo computador.
5. **Dependências de Ontologia e Backend (Opcional):**
   - [Java Development Kit (JDK) 17 ou 21](https://adoptium.net/).
   - [Protégé 5.5+](https://protege.stanford.edu/) (para inspeção e edição de ontologias OWL).

---

### 3. Procedimento de Instalação em Nova Máquina

#### Passo 1: Clonar o Repositório Privado
Abra o terminal (PowerShell ou Bash) no diretório desejado (preferencialmente em unidade secundária rápida como `D:\` para evitar encher o disco `C:\`):
```bash
git clone https://github.com/lanalcantara/Pac-man-Semantico.git
cd Pac-man-Semantico
```

#### Passo 2: Configurar o Arquivo de Ambiente (.env)
O arquivo `.env` já está versionado no repositório com todos os parâmetros padrão necessários. Verifique se ele se encontra na raiz do projeto. Caso necessite alterar URLs de backend, edite:
```env
VIRTONTO_BACKEND_URL="http://localhost:8080/api/ontology"
INITIAL_LIVES=3
PACMAN_MOVE_SPEED=4.5
```

#### Passo 3: Adicionar o Projeto no Unity Hub
1. Abra o **Unity Hub**.
2. Clique no botão **`Add`** (ou *Adicionar projeto existente*).
3. Selecione a pasta raiz clonada do projeto.
4. Garanta que a versão do Editor selecionada seja **`6000.3.24f1`**.
5. Clique para abrir o projeto. Na primeira execução, o Unity importará todos os assets e compilará os scripts C# automaticamente (isso pode levar alguns minutos).

#### Passo 4: Abrir a Cena Principal
1. Na janela de pastas do Unity (`Project`), navegue até:
   `Assets` > `Scenes` > `DemoWeb` > **`CenaDemoWeb.unity`**
2. Dê duplo clique para abrir a cena.
3. Observe que o cenário completo do labirinto, as luzes, o Canvas de HUD e o rig do jogador (`XR Origin`) serão carregados.

#### Passo 5: Executar o Jogo (Play Mode)
1. Clique no botão **`Play`** (`▶`) na barra superior do Unity Editor.
2. O jogo iniciará pausado no modo clássico **Pinball 3D**, apresentando o modal:
   `[ ▶ INICIAR JOGO (PLAY) ]`
3. Clique no botão com o mouse para iniciar.
4. Navegue com o Pac-Man pelo labirinto utilizando as teclas **`W`**, **`A`**, **`S`**, **`D`** ou as **Setas Direcionais**.
5. Pressione **`C`** para alternar as câmeras e **`H`** para recolher/expandir o painel lateral.

---

### 4. Como Contextualizar o Antigravity IDE no Novo Computador

Se você estiver abrindo o projeto no novo computador utilizando o **Antigravity IDE**:
1. Abra o workspace apontando para a pasta do projeto.
2. A pasta **`chats/`** contém todo o histórico cronológico de desenvolvimento e o índice mestre:
   - Abra o arquivo [chats/00_INDICE_CONVERSAS.md](file:///d:/VirtOnto-main/VirtOnto-main/chats/00_INDICE_CONVERSAS.md).
   - O documento [STATUS_DESENVOLVIMENTO.md](file:///d:/VirtOnto-main/VirtOnto-main/STATUS_DESENVOLVIMENTO.md) resume exatamente onde o projeto parou, quais decisões técnicas foram tomadas e quais são os próximos passos.
3. Ao enviar a primeira mensagem ao assistente, basta instruir:
   > *"Estou no novo computador. Leia o STATUS_DESENVOLVIMENTO.md e o último chat na pasta chats/ para retomar o contexto de onde paramos."*
   O agente lerá esses arquivos instantaneamente e estará 100% alinhado com o estado do projeto!
