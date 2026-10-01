# VirtOnto - Microsserviço Spring Boot de Ontologia (Pac-Man Semântico)

Microsserviço REST em **Java 17 / Spring Boot** desenvolvido no âmbito da pesquisa de Mestrado em Ciência da Computação no **Centro de Informática da Universidade Federal de Pernambuco (CIn-UFPE)**.

---

## 1. Visão Geral

Este microsserviço atua como o backend central de raciocínio ontológico e persistência de grafos para a aplicação **Pac-Man Semântico**. Espelha fielmente a arquitetura da biblioteca **VirtOnto** implementada em C++ e C# (Unity 6).

### Modelos VirtOnto em Java:
- **`Vector3D`**: Vetor euclidiano 3D com métodos de álgebra e cálculo de distâncias espaciais (`distanceTo`).
- **`OntologyElement`**: Classe abstrata fundamental com identificador (`id`), rótulo (`label`) e propriedades dinâmicas (`properties`).
- **`Node`**: Nó ontológico estendendo `OntologyElement`, representando classes da TBox (`Class`), indivíduos da ABox (`Individual`) ou regras SWRL (`Rule`), com coordenadas espaciais (`position`) e cor de exibição (`displayColor`).
- **`Edge`**: Relação ontológica ou axioma direcionado entre nós (`sourceId`, `targetId`, `relationType`, `weight`).
- **`Graph`**: Gestor de ontologia com indexação em tempo constante $O(1)$, histórico de arestas e emulação de regras SWRL.

---

## 2. Endpoints da API REST (`/api/ontology`)

| Método | Endpoint | Descrição |
|---|---|---|
| `GET` | `/api/ontology/status` | Retorna o estado operacional e metadados da ontologia |
| `GET` | `/api/ontology/graph` | Retorna a estrutura completa do grafo (nós e arestas) |
| `GET` | `/api/ontology/nodes` | Retorna o dicionário de todos os nós indexados |
| `GET` | `/api/ontology/nodes/list` | Retorna a lista plana de nós (suporta filtro `?type=Individual`) |
| `GET` | `/api/ontology/nodes/{id}` | Busca um nó específico por ID |
| `POST` | `/api/ontology/nodes` | Cria ou atualiza um nó no grafo |
| `DELETE` | `/api/ontology/nodes/{id}` | Remove um nó e suas arestas vinculadas |
| `GET` | `/api/ontology/edges` | Retorna todas as arestas/relações ativas |
| `GET` | `/api/ontology/edges/{id}` | Retorna uma aresta específica por ID |
| `POST` | `/api/ontology/edges` | Cria ou atualiza uma relação ontológica |
| `DELETE` | `/api/ontology/edges/{id}` | Remove uma aresta por ID |
| `POST` | `/api/ontology/evaluate-rules` | Dispara a avaliação de regras SWRL sobre o grafo |
| `POST` | `/api/ontology/reset` | Reinicializa a ontologia canônica padrão |

---

## 3. Configuração de CORS

O microsserviço inclui uma classe de configuração dedicada (`com.virtonto.config.CorsConfig`) com anotação `@Configuration` e suporte total a origens cruzadas:
- Permite requisições originadas do **Unity Editor**, **WebGL builds**, **Aplicações Standalone (PC)** e dispositivos de **Realidade Virtual (Meta Quest / OpenXR)**.
- Suporta os métodos HTTP `GET`, `POST`, `PUT`, `DELETE` e `OPTIONS`.

---

## 4. Como Compilar e Executar

### Pré-requisitos:
- **Java JDK 17** (instalado e configurado no `JAVA_HOME`).
- **Apache Maven 3.9+** (localizado em `C:\Users\Lana\maven\apache-maven-3.9.9\bin\mvn.cmd`).

### Compilação:
```bash
cd "c:\Users\Lana\Come-come semântico\MotorJava"
& "C:\Users\Lana\maven\apache-maven-3.9.9\bin\mvn.cmd" clean compile
```

### Execução dos Testes Automatizados:
```bash
& "C:\Users\Lana\maven\apache-maven-3.9.9\bin\mvn.cmd" test
```

### Inicialização do Servidor (Porta 8080):
```bash
& "C:\Users\Lana\maven\apache-maven-3.9.9\bin\mvn.cmd" spring-boot:run
```

O servidor estará acessível em: `http://localhost:8080/api/ontology/status`.
