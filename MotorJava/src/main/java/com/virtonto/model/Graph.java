package com.virtonto.model;

import java.util.*;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.CopyOnWriteArrayList;
import java.util.stream.Collectors;

/**
 * Gestor central do Grafo Ontológico VirtOnto.
 * Indexa nós (TBox e ABox) e arestas de relacionamento/axiomas lógicos.
 */
public class Graph {
    private String name;
    private String version;
    private final Map<String, Node> nodes;
    private final List<Edge> edges;

    public Graph() {
        this("PacManSemanticOntology", "v2.0-VirtOnto");
    }

    public Graph(String name, String version) {
        this.name = name;
        this.version = version;
        this.nodes = new ConcurrentHashMap<>();
        this.edges = new CopyOnWriteArrayList<>();
    }

    public void addNode(Node node) {
        if (node != null && node.getId() != null && !node.getId().isBlank()) {
            this.nodes.put(node.getId(), node);
        }
    }

    public Node getNode(String id) {
        if (id == null) return null;
        return this.nodes.get(id);
    }

    public Node removeNode(String id) {
        if (id == null) return null;
        this.edges.removeIf(edge -> id.equals(edge.getSourceId()) || id.equals(edge.getTargetId()));
        return this.nodes.remove(id);
    }

    public void addEdge(Edge edge) {
        if (edge != null && edge.getId() != null && !edge.getId().isBlank()) {
            // Evita duplicatas com o mesmo ID
            this.edges.removeIf(e -> e.getId().equals(edge.getId()));
            this.edges.add(edge);
        }
    }

    public Edge getEdge(String id) {
        if (id == null) return null;
        return this.edges.stream()
                .filter(e -> id.equals(e.getId()))
                .findFirst()
                .orElse(null);
    }

    public boolean removeEdge(String id) {
        if (id == null) return false;
        return this.edges.removeIf(e -> id.equals(e.getId()));
    }

    public Map<String, Node> getNodes() {
        return Collections.unmodifiableMap(this.nodes);
    }

    public List<Edge> getEdges() {
        return Collections.unmodifiableList(this.edges);
    }

    public List<Node> getNodesByType(String type) {
        if (type == null) return Collections.emptyList();
        return this.nodes.values().stream()
                .filter(n -> type.equalsIgnoreCase(n.getType()))
                .collect(Collectors.toList());
    }

    public List<Edge> getOutgoingEdges(String nodeId) {
        if (nodeId == null) return Collections.emptyList();
        return this.edges.stream()
                .filter(e -> nodeId.equals(e.getSourceId()))
                .collect(Collectors.toList());
    }

    public List<Edge> getIncomingEdges(String nodeId) {
        if (nodeId == null) return Collections.emptyList();
        return this.edges.stream()
                .filter(e -> nodeId.equals(e.getTargetId()))
                .collect(Collectors.toList());
    }

    public int getNodeCount() {
        return this.nodes.size();
    }

    public int getEdgeCount() {
        return this.edges.size();
    }

    /**
     * Inicializa o cenário ontológico canônico do Pac-Man Semântico.
     */
    public void initializeDefaultPacManOntology() {
        this.nodes.clear();
        this.edges.clear();

        // 1. Classes TBox
        Node classAgent = new Node("Agent", "Agent", "Class");
        Node classPacman = new Node("Pacman", "Pacman", "Class");
        Node classGhost = new Node("Ghost", "Ghost", "Class");
        Node classItem = new Node("Item", "Item", "Class");
        Node classGem = new Node("StandardGem", "StandardGem", "Class");
        Node classPowerPellet = new Node("SpecialGem", "SpecialGem", "Class");

        addNode(classAgent);
        addNode(classPacman);
        addNode(classGhost);
        addNode(classItem);
        addNode(classGem);
        addNode(classPowerPellet);

        addEdge(new Edge("sub_01", "Pacman", "Agent", "rdfs:subClassOf"));
        addEdge(new Edge("sub_02", "Ghost", "Agent", "rdfs:subClassOf"));
        addEdge(new Edge("sub_03", "StandardGem", "Item", "rdfs:subClassOf"));
        addEdge(new Edge("sub_04", "SpecialGem", "Item", "rdfs:subClassOf"));

        // 2. Indivíduos ABox
        Node pacman = new Node("pacman_01", "PacMan", "Individual", new Vector3D(0.0f, 0.0f, -6.0f), "#FFFF00");
        pacman.setConceptClass("Pacman");
        pacman.setProperty("hasState", "Normal");

        Node ghostBlinky = new Node("ghost_blinky", "Blinky", "Individual", new Vector3D(2.5f, 0.0f, 2.0f), "#FF0000");
        ghostBlinky.setConceptClass("Ghost");
        ghostBlinky.setProperty("hasState", "Patrol");
        ghostBlinky.setValid(false);

        Node gem1 = new Node("gem_01", "PacDot_01", "Individual", new Vector3D(0.0f, 0.0f, -3.0f), "#00FF88");
        gem1.setConceptClass("StandardGem");
        gem1.setValid(true);

        Node powerPellet = new Node("power_pellet_01", "PowerPellet_01", "Individual", new Vector3D(5.0f, 0.0f, -2.0f), "#FF9900");
        powerPellet.setConceptClass("SpecialGem");
        powerPellet.setProperty("isPowerPellet", "true");
        powerPellet.setValid(true);

        addNode(pacman);
        addNode(ghostBlinky);
        addNode(gem1);
        addNode(powerPellet);

        // Arestas de proximidade e relações iniciais
        addEdge(new Edge("edge_01", "pacman_01", "ghost_blinky", "isNearTo"));
        addEdge(new Edge("edge_02", "pacman_01", "gem_01", "isNearTo"));
    }

    /**
     * Emula o avaliador de regras SWRL sobre o grafo.
     * Regra 1: IsNearTo(p, g) ^ HasState(p, Empowered) -> HasState(g, Vulnerable)
     */
    public List<String> evaluateSWRLRules() {
        List<String> logs = new ArrayList<>();
        Node pacman = getNode("pacman_01");
        if (pacman == null) return logs;

        boolean isEmpowered = "Empowered".equalsIgnoreCase(pacman.getProperty("hasState"));

        for (Node ghost : getNodesByType("Individual")) {
            if (!"Ghost".equalsIgnoreCase(ghost.getConceptClass()) && !ghost.getId().contains("ghost")) {
                continue;
            }

            float dist = pacman.getPosition().distanceTo(ghost.getPosition());

            // Avalia regra SWRL de proximidade e vulnerabilidade
            if (dist < 3.5f && isEmpowered) {
                ghost.setProperty("hasState", "Vulnerable");
                ghost.setDisplayColor("#2980B9");
                logs.add(String.format("SWRL Disparada: Fantasma '%s' assumiu estado VULNERÁVEL (dist=%.2fm)", ghost.getLabel(), dist));
            } else if (dist < 2.5f && !isEmpowered) {
                ghost.setProperty("hasState", "Aggressive");
                ghost.setDisplayColor("#C0392B");
                logs.add(String.format("SWRL Disparada: Fantasma '%s' assumiu estado AGRESSIVO (dist=%.2fm)", ghost.getLabel(), dist));
            } else {
                ghost.setProperty("hasState", "Patrol");
                ghost.setDisplayColor("#E74C3C");
            }
        }

        return logs;
    }

    public String getName() {
        return name;
    }

    public void setName(String name) {
        this.name = name;
    }

    public String getVersion() {
        return version;
    }

    public void setVersion(String version) {
        this.version = version;
    }
}
