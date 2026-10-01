using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace VirtOnto.Model
{
    // =========================================================================
    // 1. ESTRUTURA VECTOR3D
    // =========================================================================

    /// <summary>
    /// Representação vetorial tridimensional matemática compatível com a arquitetura VirtOnto (C++).
    /// Oferece operadores algébricos completos (+, -, *, /), distância euclidiana (DistanceTo)
    /// e conversão bidirecional implícita com UnityEngine.Vector3.
    /// </summary>
    [Serializable]
    public struct Vector3D : IEquatable<Vector3D>
    {
        public float x;
        public float y;
        public float z;

        public Vector3D(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public static Vector3D Zero => new Vector3D(0f, 0f, 0f);
        public static Vector3D One => new Vector3D(1f, 1f, 1f);
        public static Vector3D Up => new Vector3D(0f, 1f, 0f);
        public static Vector3D Forward => new Vector3D(0f, 0f, 1f);
        public static Vector3D Right => new Vector3D(1f, 0f, 0f);

        /// <summary>
        /// Calcula a distância euclidiana até outro ponto tridimensional.
        /// </summary>
        public float DistanceTo(Vector3D other)
        {
            float dx = x - other.x;
            float dy = y - other.y;
            float dz = z - other.z;
            return Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        public float Magnitude => Mathf.Sqrt(x * x + y * y + z * z);
        public float SqrMagnitude => x * x + y * y + z * z;

        public Vector3D Normalized
        {
            get
            {
                float mag = Magnitude;
                return mag > 1e-5f ? new Vector3D(x / mag, y / mag, z / mag) : Zero;
            }
        }

        // Operadores algébricos completos
        public static Vector3D operator +(Vector3D a, Vector3D b) => new Vector3D(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3D operator -(Vector3D a, Vector3D b) => new Vector3D(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3D operator *(Vector3D a, float scalar) => new Vector3D(a.x * scalar, a.y * scalar, a.z * scalar);
        public static Vector3D operator *(float scalar, Vector3D a) => new Vector3D(a.x * scalar, a.y * scalar, a.z * scalar);
        public static Vector3D operator /(Vector3D a, float scalar) => new Vector3D(a.x / scalar, a.y / scalar, a.z / scalar);
        public static Vector3D operator *(Vector3D a, Vector3D b) => new Vector3D(a.x * b.x, a.y * b.y, a.z * b.z);
        public static Vector3D operator -(Vector3D a) => new Vector3D(-a.x, -a.y, -a.z);

        public static bool operator ==(Vector3D a, Vector3D b) =>
            Mathf.Approximately(a.x, b.x) && Mathf.Approximately(a.y, b.y) && Mathf.Approximately(a.z, b.z);

        public static bool operator !=(Vector3D a, Vector3D b) => !(a == b);

        // Conversão implícita ergonômica com UnityEngine.Vector3
        public static implicit operator Vector3(Vector3D v) => new Vector3(v.x, v.y, v.z);
        public static implicit operator Vector3D(Vector3 v) => new Vector3D(v.x, v.y, v.z);

        public bool Equals(Vector3D other) => this == other;
        public override bool Equals(object obj) => obj is Vector3D other && Equals(other);
        public override int GetHashCode() => (x, y, z).GetHashCode();
        public override string ToString() => $"Vector3D({x:F2}, {y:F2}, {z:F2})";
    }

    // =========================================================================
    // 2. CLASSE BASE ABSTRATA ONTOLOGYELEMENT
    // =========================================================================

    /// <summary>
    /// Classe base abstrata para todos os elementos ontológicos do VirtOnto (C++).
    /// Define a identidade única (Id / URI), rótulo semântico (Label) e propriedades.
    /// </summary>
    [Serializable]
    public abstract class OntologyElement
    {
        [SerializeField] protected string id_;
        [SerializeField] protected string label_;
        [SerializeField] protected Dictionary<string, string> properties_ = new Dictionary<string, string>();

        public string Id
        {
            get => id_;
            set => id_ = value;
        }

        public string Label
        {
            get => label_;
            set => label_ = value;
        }

        public Dictionary<string, string> Properties
        {
            get => properties_;
            set => properties_ = value ?? new Dictionary<string, string>();
        }

        protected OntologyElement(string id, string label)
        {
            id_ = id ?? string.Empty;
            label_ = label ?? string.Empty;
        }

        /// <summary>
        /// Método polimórfico obrigatório: Retorna o código hexadecimal da cor de exibição.
        /// </summary>
        public abstract string GetDisplayColor();

        public virtual Color GetColorRGB()
        {
            string hex = GetDisplayColor();
            if (ColorUtility.TryParseHtmlString(hex, out Color cor))
            {
                return cor;
            }
            return Color.white;
        }

        public override string ToString() => $"[{GetType().Name}] Id: '{id_}', Label: '{label_}'";
    }

    // =========================================================================
    // 3. ENUM NODETYPE E CLASSE NODE
    // =========================================================================

    public enum NodeType
    {
        CLASS = 0,         // Conceito TBox (ex: Pacman, Ghost, Item, StandardGem, Obstacle)
        INDIVIDUAL = 1,    // Indivíduo ABox instanciado no labirinto
        PROPERTY = 2,      // Object/Data Property (ex: consumes, isNearTo)
        RULE = 3,          // Regra SWRL
        AXIOM = 4          // Axioma de Description Logic
    }

    /// <summary>
    /// Representa um Nó no grafo ontológico VirtOnto em C#.
    /// Possui suporte a tipos NodeType, posições 3D (Vector3D), dinâmica física
    /// (velocidade, massa) e cor de exibição visual em RV/RA.
    /// </summary>
    [Serializable]
    public class Node : OntologyElement
    {
        [SerializeField] private NodeType type_;
        [SerializeField] private Vector3D position_;
        [SerializeField] private Vector3D velocity_;
        [SerializeField] private float mass_ = 1.0f;

        // Propriedades semânticas Description Logic
        public string ConceptClass { get; set; } = string.Empty;
        public string DLExpression { get; set; } = string.Empty;
        public bool IsValid { get; set; } = true;
        public int ScoreValue { get; set; } = 10;
        public string SemanticDescription { get; set; } = string.Empty;
        public Color HighlightColor { get; set; } = Color.green;

        // Vínculo opcional de tempo de execução com o GameObject físico no Unity
        [NonSerialized] public GameObject GameObjectInstance;

        public NodeType Type
        {
            get => type_;
            set => type_ = value;
        }

        public Vector3D Position
        {
            get => position_;
            set => position_ = value;
        }

        public Vector3D Velocity
        {
            get => velocity_;
            set => velocity_ = value;
        }

        public float Mass
        {
            get => mass_;
            set => mass_ = value;
        }

        public Node(string id, string label, NodeType type) : base(id, label)
        {
            type_ = type;
            position_ = Vector3D.Zero;
            velocity_ = Vector3D.Zero;
            mass_ = 1.0f;
        }

        public Node(string id, NodeType type, string label) : this(id, label, type)
        {
        }

        public override string GetDisplayColor()
        {
            switch (type_)
            {
                case NodeType.CLASS:
                    return "#FFA500"; // Laranja (TBox)
                case NodeType.INDIVIDUAL:
                    return IsValid ? "#00FF66" : "#FF3344"; // Verde (Válida) / Vermelho (Violação)
                case NodeType.PROPERTY:
                    return "#0088FF"; // Azul (Relações)
                case NodeType.RULE:
                    return "#9B59B6"; // Roxo (SWRL)
                case NodeType.AXIOM:
                    return "#F39C12"; // Âmbar (Axiomas)
                default:
                    return "#FFFFFF";
            }
        }
    }

    // =========================================================================
    // 4. CLASSE EDGE (ARESTAS E AXIOMAS LÓGICOS)
    // =========================================================================

    /// <summary>
    /// Representa uma Aresta direcionada ou simétrica entre nós ontológicos.
    /// Modela axiomas ontológicos OWL e relações SWRL (consumes, isNearTo, rdfs:subClassOf, etc.).
    /// </summary>
    [Serializable]
    public class Edge : OntologyElement
    {
        [SerializeField] private string sourceId_;
        [SerializeField] private string targetId_;
        [SerializeField] private string relationType_;
        [SerializeField] private float weight_ = 1.0f;
        [SerializeField] private bool isSymmetric_ = false;

        public string SourceId
        {
            get => sourceId_;
            set => sourceId_ = value;
        }

        public string TargetId
        {
            get => targetId_;
            set => targetId_ = value;
        }

        public string RelationType
        {
            get => relationType_;
            set => relationType_ = value;
        }

        public float Weight
        {
            get => weight_;
            set => weight_ = value;
        }

        public bool IsSymmetric
        {
            get => isSymmetric_;
            set => isSymmetric_ = value;
        }

        public Edge(string id, string source, string target, string relation, float weight = 1.0f, bool isSymmetric = false)
            : base(id, relation)
        {
            sourceId_ = source ?? string.Empty;
            targetId_ = target ?? string.Empty;
            relationType_ = relation ?? string.Empty;
            weight_ = weight;
            isSymmetric_ = isSymmetric;
        }

        public override string GetDisplayColor()
        {
            if (string.Equals(relationType_, "rdfs:subClassOf", StringComparison.OrdinalIgnoreCase))
                return "#7B68EE";
            if (string.Equals(relationType_, "consumes", StringComparison.OrdinalIgnoreCase))
                return "#FFD700";
            if (string.Equals(relationType_, "isNearTo", StringComparison.OrdinalIgnoreCase))
                return "#00E5FF";
            if (string.Equals(relationType_, "violates", StringComparison.OrdinalIgnoreCase))
                return "#FF3344";
            if (string.Equals(relationType_, "satisfies", StringComparison.OrdinalIgnoreCase))
                return "#00FF66";

            return "#888888";
        }

        public override string ToString() => $"Edge({sourceId_} --[{relationType_}]--> {targetId_})";
    }

    // =========================================================================
    // 5. CLASSE GRAPH (GESTOR CENTRAL DO GRAFO ONTO-ESPACIAL)
    // =========================================================================

    /// <summary>
    /// Gestor central do grafo ontológico VirtOnto em C#.
    /// Mantém um dicionário indexado de nós por ID e uma lista de arestas relacionais,
    /// suportando consultas em tempo constante O(1), inferência de regras SWRL
    /// e conversão de dados para o GerenciadorCenarioSemantico.
    /// </summary>
    [Serializable]
    public class Graph
    {
        private readonly Dictionary<string, Node> nodes_ = new Dictionary<string, Node>(StringComparer.OrdinalIgnoreCase);
        private readonly List<Edge> edges_ = new List<Edge>();

        // Índices otimizados para consultas ontológicas
        private readonly Dictionary<string, List<Edge>> outgoingEdges_ = new Dictionary<string, List<Edge>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<Edge>> incomingEdges_ = new Dictionary<string, List<Edge>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<NodeType, List<Node>> nodesByType_ = new Dictionary<NodeType, List<Node>>();
        private readonly Dictionary<string, List<Node>> individualsByClass_ = new Dictionary<string, List<Node>>(StringComparer.OrdinalIgnoreCase);

        public string NomeOntologia { get; set; } = "OntologiaPacManSemantico_VirtOnto";
        public string VersaoAxiomas { get; set; } = "ALCQ(D)-VirtOnto-v2.0";

        public Graph()
        {
            foreach (NodeType type in Enum.GetValues(typeof(NodeType)))
            {
                nodesByType_[type] = new List<Node>();
            }
        }

        public void AddNode(Node node)
        {
            if (node == null || string.IsNullOrEmpty(node.Id)) return;

            if (nodes_.ContainsKey(node.Id))
            {
                nodes_[node.Id] = node;
                return;
            }

            nodes_[node.Id] = node;

            if (!nodesByType_.TryGetValue(node.Type, out var list))
            {
                list = new List<Node>();
                nodesByType_[node.Type] = list;
            }
            list.Add(node);

            if (node.Type == NodeType.INDIVIDUAL && !string.IsNullOrEmpty(node.ConceptClass))
            {
                if (!individualsByClass_.TryGetValue(node.ConceptClass, out var indList))
                {
                    indList = new List<Node>();
                    individualsByClass_[node.ConceptClass] = indList;
                }
                indList.Add(node);
            }
        }

        public void AddEdge(Edge edge)
        {
            if (edge == null || string.IsNullOrEmpty(edge.SourceId) || string.IsNullOrEmpty(edge.TargetId)) return;

            edges_.Add(edge);

            if (!outgoingEdges_.TryGetValue(edge.SourceId, out var outList))
            {
                outList = new List<Edge>();
                outgoingEdges_[edge.SourceId] = outList;
            }
            outList.Add(edge);

            if (!incomingEdges_.TryGetValue(edge.TargetId, out var inList))
            {
                inList = new List<Edge>();
                incomingEdges_[edge.TargetId] = inList;
            }
            inList.Add(edge);

            if (edge.IsSymmetric)
            {
                if (!outgoingEdges_.TryGetValue(edge.TargetId, out var symOutList))
                {
                    symOutList = new List<Edge>();
                    outgoingEdges_[edge.TargetId] = symOutList;
                }
                symOutList.Add(edge);

                if (!incomingEdges_.TryGetValue(edge.SourceId, out var symInList))
                {
                    symInList = new List<Edge>();
                    incomingEdges_[edge.SourceId] = symInList;
                }
                symInList.Add(edge);
            }
        }

        public Node GetNode(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            nodes_.TryGetValue(id, out Node node);
            return node;
        }

        public bool TryGetNode(string id, out Node node)
        {
            return nodes_.TryGetValue(id, out node);
        }

        public IReadOnlyDictionary<string, Node> GetNodes() => nodes_;
        public IReadOnlyList<Edge> GetEdges() => edges_;

        public int GetNodeCount() => nodes_.Count;
        public int GetEdgeCount() => edges_.Count;

        public void Clear()
        {
            nodes_.Clear();
            edges_.Clear();
            outgoingEdges_.Clear();
            incomingEdges_.Clear();
            individualsByClass_.Clear();

            foreach (var key in nodesByType_.Keys)
            {
                nodesByType_[key].Clear();
            }
        }

        public IReadOnlyList<Node> GetNodesByType(NodeType type)
        {
            return nodesByType_.TryGetValue(type, out var list) ? list : (IReadOnlyList<Node>)new List<Node>();
        }

        public IReadOnlyList<Node> GetIndividualsOfClass(string className)
        {
            return individualsByClass_.TryGetValue(className, out var list) ? list : (IReadOnlyList<Node>)new List<Node>();
        }

        public IReadOnlyList<Edge> GetOutgoingEdges(string nodeId)
        {
            return outgoingEdges_.TryGetValue(nodeId, out var list) ? list : (IReadOnlyList<Edge>)new List<Edge>();
        }

        public IReadOnlyList<Edge> GetIncomingEdges(string nodeId)
        {
            return incomingEdges_.TryGetValue(nodeId, out var list) ? list : (IReadOnlyList<Edge>)new List<Edge>();
        }

        public List<Node> GetNeighbors(string nodeId)
        {
            List<Node> vizinhos = new List<Node>();
            if (outgoingEdges_.TryGetValue(nodeId, out var outList))
            {
                foreach (var e in outList)
                {
                    string target = e.SourceId == nodeId ? e.TargetId : e.SourceId;
                    if (TryGetNode(target, out var n) && !vizinhos.Contains(n))
                    {
                        vizinhos.Add(n);
                    }
                }
            }
            return vizinhos;
        }

        /// <summary>
        /// Popula e indexa o grafo a partir de um CenarioSemanticoDTO.
        /// </summary>
        public void BuildFromCenarioDTO(CenarioSemanticoDTO cenario)
        {
            Clear();
            if (cenario == null) return;

            NomeOntologia = string.IsNullOrEmpty(cenario.nomeOntologia) ? "OntologiaPacMan" : cenario.nomeOntologia;
            VersaoAxiomas = string.IsNullOrEmpty(cenario.versaoAxiomas) ? "v2.0-VirtOnto" : cenario.versaoAxiomas;

            IndexarTBoxPadrao();

            if (cenario.instancias != null)
            {
                int edgeCounter = 1;
                foreach (var inst in cenario.instancias)
                {
                    if (inst == null) continue;

                    Node nodeInd = new Node(inst.id, inst.classeOntologica, NodeType.INDIVIDUAL)
                    {
                        ConceptClass = inst.classeOntologica,
                        DLExpression = inst.expressaoDL,
                        IsValid = inst.ehValida,
                        Position = inst.posicao,
                        ScoreValue = inst.valorPontuacao,
                        SemanticDescription = inst.descricaoSemantica,
                        HighlightColor = inst.corDestaque
                    };

                    AddNode(nodeInd);

                    if (!nodes_.ContainsKey(inst.classeOntologica))
                    {
                        Node classNode = new Node(inst.classeOntologica, inst.classeOntologica, NodeType.CLASS);
                        AddNode(classNode);
                    }

                    AddEdge(new Edge($"e_type_{edgeCounter++}", inst.id, inst.classeOntologica, "rdf:type", 1.0f));

                    string relRegra = inst.ehValida ? "satisfies" : "violates";
                    AddEdge(new Edge($"e_rule_{edgeCounter++}", inst.id, "ALCQ_Axioma", relRegra, 1.0f));
                }
            }

            AtualizarRelacoesProximidade(raioProximidade: 3.5f);
        }

        /// <summary>
        /// Converte o grafo ontológico para o formato de transferência DTO.
        /// </summary>
        public CenarioSemanticoDTO ToCenarioDTO()
        {
            CenarioSemanticoDTO cenario = new CenarioSemanticoDTO
            {
                nomeOntologia = NomeOntologia,
                versaoAxiomas = VersaoAxiomas,
                ontologiaConsistente = true,
                instancias = new List<DadoInstanciaSemantica>()
            };

            foreach (var node in GetNodesByType(NodeType.INDIVIDUAL))
            {
                cenario.instancias.Add(new DadoInstanciaSemantica
                {
                    id = node.Id,
                    classeOntologica = string.IsNullOrEmpty(node.ConceptClass) ? node.Label : node.ConceptClass,
                    expressaoDL = node.DLExpression,
                    ehValida = node.IsValid,
                    posicao = node.Position,
                    valorPontuacao = node.ScoreValue,
                    descricaoSemantica = node.SemanticDescription,
                    corDestaque = node.HighlightColor
                });
            }

            return cenario;
        }

        /// <summary>
        /// Indexa a hierarquia TBox e regras SWRL padrão do Pac-Man Semântico.
        /// </summary>
        public void IndexarTBoxPadrao()
        {
            Node thing = new Node("owl:Thing", "Thing", NodeType.CLASS);
            Node agent = new Node("Agent", "Agent", NodeType.CLASS);
            Node pacman = new Node("Pacman", "Pacman", NodeType.CLASS);
            Node ghost = new Node("Ghost", "Ghost", NodeType.CLASS);
            Node aggressiveGhost = new Node("AggressiveGhost", "AggressiveGhost (Blinky)", NodeType.CLASS);
            Node patrolGhost = new Node("PatrolGhost", "PatrolGhost (Pinky)", NodeType.CLASS);
            Node vulnerableGhost = new Node("VulnerableGhost", "VulnerableGhost", NodeType.CLASS);

            Node item = new Node("Item", "Item", NodeType.CLASS);
            Node standardGem = new Node("StandardGem", "StandardGem (PacDot)", NodeType.CLASS);
            Node specialGem = new Node("SpecialGem", "SpecialGem (PowerPellet)", NodeType.CLASS);
            Node obstacle = new Node("Obstacle", "Obstacle", NodeType.CLASS);

            Node swrlR1 = new Node("SWRL_R1", "Regra 1: Vulnerabilidade do Fantasma", NodeType.RULE)
            {
                SemanticDescription = "Pacman(?p) ^ consumes(?p, ?s) ^ SpecialGem(?s) ^ Ghost(?g) ^ isNearTo(?g, ?p) -> VulnerableGhost(?g)"
            };
            Node swrlR2 = new Node("SWRL_R2", "Regra 2: Validação de Gema Positiva", NodeType.RULE)
            {
                SemanticDescription = "StandardGem(?g) ^ satisfiesAxiom(?g) -> ValidScore(?g, 10)"
            };
            Node swrlR3 = new Node("SWRL_R3", "Regra 3: Violação Ontológica / Obstáculo", NodeType.RULE)
            {
                SemanticDescription = "Obstacle(?o) ^ violatesAxiom(?o) -> Inconsistent(?o)"
            };
            Node alcqAxiom = new Node("ALCQ_Axioma", "ALCQ(D) Consistency Checker", NodeType.AXIOM);

            AddNode(thing);
            AddNode(agent);
            AddNode(pacman);
            AddNode(ghost);
            AddNode(aggressiveGhost);
            AddNode(patrolGhost);
            AddNode(vulnerableGhost);
            AddNode(item);
            AddNode(standardGem);
            AddNode(specialGem);
            AddNode(obstacle);
            AddNode(swrlR1);
            AddNode(swrlR2);
            AddNode(swrlR3);
            AddNode(alcqAxiom);

            AddEdge(new Edge("h_ag", "Agent", "owl:Thing", "rdfs:subClassOf"));
            AddEdge(new Edge("h_it", "Item", "owl:Thing", "rdfs:subClassOf"));
            AddEdge(new Edge("h_pac", "Pacman", "Agent", "rdfs:subClassOf"));
            AddEdge(new Edge("h_gh", "Ghost", "Agent", "rdfs:subClassOf"));
            AddEdge(new Edge("h_agh", "AggressiveGhost", "Ghost", "rdfs:subClassOf"));
            AddEdge(new Edge("h_pgh", "PatrolGhost", "Ghost", "rdfs:subClassOf"));
            AddEdge(new Edge("h_vgh", "VulnerableGhost", "Ghost", "rdfs:subClassOf"));
            AddEdge(new Edge("h_gem", "StandardGem", "Item", "rdfs:subClassOf"));
            AddEdge(new Edge("h_spg", "SpecialGem", "Item", "rdfs:subClassOf"));
            AddEdge(new Edge("h_obs", "Obstacle", "owl:Thing", "rdfs:subClassOf"));
        }

        /// <summary>
        /// Calcula arestas de proximidade simétricas (isNearTo) com base na distância geométrica dos nós 3D.
        /// </summary>
        public void AtualizarRelacoesProximidade(float raioProximidade = 3.5f)
        {
            var individuos = GetNodesByType(NodeType.INDIVIDUAL);
            int count = individuos.Count;

            for (int i = 0; i < count; i++)
            {
                for (int j = i + 1; j < count; j++)
                {
                    Node a = individuos[i];
                    Node b = individuos[j];

                    float dist = a.Position.DistanceTo(b.Position);
                    if (dist <= raioProximidade)
                    {
                        string edgeId = $"near_{a.Id}_{b.Id}";
                        AddEdge(new Edge(edgeId, a.Id, b.Id, "isNearTo", dist, isSymmetric: true));
                    }
                }
            }
        }

        public void AvaliarRegrasSWRL(Action<string> onRegraDisparada = null)
        {
            AtualizarRelacoesProximidade();

            var individuos = GetNodesByType(NodeType.INDIVIDUAL);
            foreach (var ind in individuos)
            {
                var arestasSaida = GetOutgoingEdges(ind.Id);
                foreach (var e in arestasSaida)
                {
                    if (string.Equals(e.RelationType, "isNearTo", StringComparison.OrdinalIgnoreCase))
                    {
                        onRegraDisparada?.Invoke($"[SWRL - Regra 4] isNearTo detectada entre '{ind.Id}' e '{e.TargetId}' (Dist: {e.Weight:F2}m).");
                    }
                }
            }
        }
    }
}
