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
        this("ParasitologyOntology_VirtOnto", "v2.0-Parasites-ALCQ");
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
     * Inicializa a ontologia canônica real de parasitologia (Protégé OWL / ALCQ(D)).
     */
    public void initializeDefaultPacManOntology() {
        this.nodes.clear();
        this.edges.clear();

        // 1. Classes TBox (Parasitologia Real / Protégé OWL)
        Node classParasite = new Node("Parasite", "Parasite", "Class");
        Node classNematoda = new Node("Nematoda", "Nematoda", "Class");
        Node classPlaty = new Node("Platyhelminthes", "Platyhelminthes", "Class");
        Node classAscaris = new Node("AscarisLumbricoides", "AscarisLumbricoides", "Class");
        Node classEnterobius = new Node("EnterobiusVermicularis", "EnterobiusVermicularis", "Class");
        Node classTaenia = new Node("TaeniaSolium", "TaeniaSolium", "Class");

        Node classHost = new Node("Host", "Host", "Class");
        Node classHuman = new Node("Human", "Human", "Class");
        Node classPig = new Node("Pig", "Pig", "Class");

        Node classHabitat = new Node("Habitat", "Habitat", "Class");
        Node classIntestine = new Node("Intestine", "Intestine", "Class");
        Node classSmallIntestine = new Node("SmallIntestine", "SmallIntestine", "Class");
        Node classPerianal = new Node("PerianalRegion", "PerianalRegion", "Class");

        Node classMorphology = new Node("Morphology", "Morphology", "Class");
        Node classCylindrical = new Node("Cylindrical", "Cylindrical", "Class");
        Node classFlatSegments = new Node("FlatSegments", "FlatSegments", "Class");

        Node classTransmission = new Node("Transmission", "Transmission", "Class");
        Node classFecalOral = new Node("FecalOral", "FecalOral", "Class");
        Node classUndercookedPork = new Node("UndercookedPork", "UndercookedPork", "Class");
        Node classAutoinfection = new Node("Autoinfection", "Autoinfection", "Class");

        Node classResearcher = new Node("BiomedicalResearcher", "BiomedicalResearcher", "Class");
        Node classVector = new Node("InfectionVector", "InfectionVector", "Class");
        Node classTreatment = new Node("AntiparasiticTreatment", "AntiparasiticTreatment", "Class");
        Node classInconsistent = new Node("InconsistentParasiteSpecimen", "InconsistentParasiteSpecimen", "Class");

        addNode(classParasite);
        addNode(classNematoda);
        addNode(classPlaty);
        addNode(classAscaris);
        addNode(classEnterobius);
        addNode(classTaenia);
        addNode(classHost);
        addNode(classHuman);
        addNode(classPig);
        addNode(classHabitat);
        addNode(classIntestine);
        addNode(classSmallIntestine);
        addNode(classPerianal);
        addNode(classMorphology);
        addNode(classCylindrical);
        addNode(classFlatSegments);
        addNode(classTransmission);
        addNode(classFecalOral);
        addNode(classUndercookedPork);
        addNode(classAutoinfection);
        addNode(classResearcher);
        addNode(classVector);
        addNode(classTreatment);
        addNode(classInconsistent);

        // Arestas de taxonomia TBox (rdfs:subClassOf)
        addEdge(new Edge("tax_01", "Nematoda", "Parasite", "rdfs:subClassOf"));
        addEdge(new Edge("tax_02", "Platyhelminthes", "Parasite", "rdfs:subClassOf"));
        addEdge(new Edge("tax_03", "AscarisLumbricoides", "Nematoda", "rdfs:subClassOf"));
        addEdge(new Edge("tax_04", "EnterobiusVermicularis", "Nematoda", "rdfs:subClassOf"));
        addEdge(new Edge("tax_05", "TaeniaSolium", "Platyhelminthes", "rdfs:subClassOf"));
        addEdge(new Edge("tax_06", "Human", "Host", "rdfs:subClassOf"));
        addEdge(new Edge("tax_07", "Pig", "Host", "rdfs:subClassOf"));
        addEdge(new Edge("tax_08", "Intestine", "Habitat", "rdfs:subClassOf"));
        addEdge(new Edge("tax_09", "SmallIntestine", "Intestine", "rdfs:subClassOf"));
        addEdge(new Edge("tax_10", "PerianalRegion", "Habitat", "rdfs:subClassOf"));
        addEdge(new Edge("tax_11", "Cylindrical", "Morphology", "rdfs:subClassOf"));
        addEdge(new Edge("tax_12", "FlatSegments", "Morphology", "rdfs:subClassOf"));
        addEdge(new Edge("tax_13", "FecalOral", "Transmission", "rdfs:subClassOf"));
        addEdge(new Edge("tax_14", "UndercookedPork", "Transmission", "rdfs:subClassOf"));
        addEdge(new Edge("tax_15", "Autoinfection", "Transmission", "rdfs:subClassOf"));

        // 2. Indivíduos ABox (Instâncias de espécimes reais do Protégé)
        Node researcher = new Node("pacman_01", "BiomedicalResearcher", "Individual", new Vector3D(0.0f, 0.0f, -6.0f), "#FFFF00");
        researcher.setConceptClass("BiomedicalResearcher");
        researcher.setProperty("hasState", "Normal");

        Node vectorBlinky = new Node("ghost_blinky", "TriatomineVector_01", "Individual", new Vector3D(2.5f, 0.0f, 2.0f), "#FF0000");
        vectorBlinky.setConceptClass("InfectionVector");
        vectorBlinky.setProperty("hasState", "Patrol");
        vectorBlinky.setValid(false);

        Node sampleAscaris = new Node("sample_ascaris_01", "AscarisLumbricoides_Specimen", "Individual", new Vector3D(0.0f, 0.0f, -3.0f), "#00FFCC");
        sampleAscaris.setConceptClass("AscarisLumbricoides");
        sampleAscaris.setProperty("has_host", "Human");
        sampleAscaris.setProperty("has_habitat", "SmallIntestine");
        sampleAscaris.setProperty("has_transmission", "FecalOral");
        sampleAscaris.setValid(true);

        Node sampleEnterobius = new Node("sample_enterobius_01", "EnterobiusVermicularis_Specimen", "Individual", new Vector3D(-4.0f, 0.0f, 2.0f), "#38BDF8");
        sampleEnterobius.setConceptClass("EnterobiusVermicularis");
        sampleEnterobius.setProperty("has_host", "Human");
        sampleEnterobius.setProperty("has_habitat", "PerianalRegion");
        sampleEnterobius.setProperty("has_transmission", "Autoinfection");
        sampleEnterobius.setValid(true);

        Node sampleTaenia = new Node("sample_taenia_01", "TaeniaSolium_Specimen", "Individual", new Vector3D(2.0f, 0.0f, -5.0f), "#FFD700");
        sampleTaenia.setConceptClass("TaeniaSolium");
        sampleTaenia.setProperty("has_host", "Human");
        sampleTaenia.setProperty("has_habitat", "SmallIntestine");
        sampleTaenia.setProperty("has_transmission", "UndercookedPork");
        sampleTaenia.setValid(true);

        Node treatment = new Node("power_pellet_01", "AntiparasiticTreatment_01", "Individual", new Vector3D(5.0f, 0.0f, -2.0f), "#FF9900");
        treatment.setConceptClass("AntiparasiticTreatment");
        treatment.setProperty("isAntiparasiticTreatment", "true");
        treatment.setValid(true);

        Node inconsistentSpecimen = new Node("sample_inconsistent_01", "InconsistentSpecimen_01", "Individual", new Vector3D(-2.5f, 0.0f, -3.0f), "#FF3344");
        inconsistentSpecimen.setConceptClass("InconsistentParasiteSpecimen");
        inconsistentSpecimen.setProperty("dl_violation", "Cylindrical ⊓ FlatSegments ≡ ⊥");
        inconsistentSpecimen.setValid(false);

        addNode(researcher);
        addNode(vectorBlinky);
        addNode(sampleAscaris);
        addNode(sampleEnterobius);
        addNode(sampleTaenia);
        addNode(treatment);
        addNode(inconsistentSpecimen);

        // Arestas de relacionamento e proximidade
        addEdge(new Edge("edge_01", "pacman_01", "ghost_blinky", "isNearTo"));
        addEdge(new Edge("edge_02", "pacman_01", "sample_ascaris_01", "isNearTo"));
    }

    /**
     * Emula o avaliador de regras SWRL sobre o grafo parasitológico.
     * Regra 1: isNearTo(r, v) ^ hasState(r, Empowered) -> hasState(v, Vulnerable) [Antiparasitic Treatment Effect]
     * Regra 2: isNearTo(r, v) ^ not(hasState(r, Empowered)) -> hasState(v, Aggressive) [Infection Vector Threat]
     */
    public List<String> evaluateSWRLRules() {
        List<String> logs = new ArrayList<>();
        Node pacman = getNode("pacman_01");
        if (pacman == null) return logs;

        boolean isEmpowered = "Empowered".equalsIgnoreCase(pacman.getProperty("hasState"));

        for (Node ghost : getNodesByType("Individual")) {
            if (!"InfectionVector".equalsIgnoreCase(ghost.getConceptClass()) && !"Ghost".equalsIgnoreCase(ghost.getConceptClass()) && !ghost.getId().contains("ghost") && !ghost.getId().contains("vector")) {
                continue;
            }

            float dist = pacman.getPosition().distanceTo(ghost.getPosition());

            // Avalia regra SWRL de proximidade e vulnerabilidade terapêutica
            if (dist < 3.5f && isEmpowered) {
                ghost.setProperty("hasState", "Vulnerable");
                ghost.setDisplayColor("#2980B9");
                logs.add(String.format("SWRL Disparada: Vetor infeccioso '%s' tornou-se VULNERÁVEL sob tratamento antiparasitário (dist=%.2fm)", ghost.getLabel(), dist));
            } else if (dist < 2.5f && !isEmpowered) {
                ghost.setProperty("hasState", "Aggressive");
                ghost.setDisplayColor("#C0392B");
                logs.add(String.format("SWRL Disparada: Vetor infeccioso '%s' assumiu estado AGRESSIVO (dist=%.2fm)", ghost.getLabel(), dist));
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
