package com.virtonto.controller;

import com.virtonto.model.Edge;
import com.virtonto.model.Graph;
import com.virtonto.model.Node;
import com.virtonto.model.Vector3D;
import org.springframework.http.HttpStatus;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.*;

import java.util.*;

/**
 * ============================================================================
 * PAC-MAN SEMÂNTICO (CIn-UFPE) - Controlador REST da Ontologia VirtOnto
 * ============================================================================
 * Disponibiliza endpoints para consulta, inserção e gestão de nós (TBox/ABox)
 * e arestas (Object Properties / SWRL) para integração com o Unity 6 (C#).
 * ============================================================================
 */
@RestController
@RequestMapping("/api/ontology")
@CrossOrigin(origins = "*")
public class OntologyController {

    private final Graph graph;

    public OntologyController() {
        this.graph = new Graph("PacManSemanticOntology", "v2.0-VirtOnto");

        // Inicialização com dados de exemplo iniciais para o Pac-Man Semântico
        Node pacman = new Node("pacman_01", "PacMan", "Individual");
        pacman.setDisplayColor("#FFFF00");
        pacman.setPosition(new Vector3D(0f, 0f, 0f));
        pacman.setConceptClass("Pacman");
        pacman.setProperty("hasState", "Normal");

        Node ghost = new Node("ghost_blinky", "Blinky", "Individual");
        ghost.setDisplayColor("#FF0000");
        ghost.setPosition(new Vector3D(2.5f, 0f, 2.0f));
        ghost.setConceptClass("Ghost");
        ghost.setProperty("hasState", "Patrol");
        ghost.setValid(false);

        Node gem = new Node("gem_01", "PacDot_01", "Individual");
        gem.setDisplayColor("#00FF88");
        gem.setPosition(new Vector3D(0f, 0f, -3.0f));
        gem.setConceptClass("StandardGem");
        gem.setValid(true);

        graph.addNode(pacman);
        graph.addNode(ghost);
        graph.addNode(gem);

        // Arestas de relacionamento ontológico inicial
        Edge relacao = new Edge("edge_01", "pacman_01", "ghost_blinky", "isNearTo");
        Edge relacaoGema = new Edge("edge_02", "pacman_01", "gem_01", "isNearTo");
        graph.addEdge(relacao);
        graph.addEdge(relacaoGema);
    }

    // =========================================================================
    // ENDPOINTS DE NÓS (NODES)
    // =========================================================================

    /**
     * Retorna o mapa de todos os nós indexados no grafo.
     */
    @GetMapping("/nodes")
    public Map<String, Node> getAllNodes() {
        return graph.getNodes();
    }

    /**
     * Retorna a lista plana de nós ou filtrada por tipo (ex: Individual, Class).
     */
    @GetMapping("/nodes/list")
    public Collection<Node> getNodesList(@RequestParam(required = false) String type) {
        if (type != null && !type.isBlank()) {
            return graph.getNodesByType(type);
        }
        return graph.getNodes().values();
    }

    /**
     * Retorna um nó específico pelo seu identificador único.
     */
    @GetMapping("/nodes/{id}")
    public ResponseEntity<Node> getNodeById(@PathVariable String id) {
        Node node = graph.getNode(id);
        if (node != null) {
            return ResponseEntity.ok(node);
        }
        return ResponseEntity.status(HttpStatus.NOT_FOUND).build();
    }

    /**
     * Adiciona ou atualiza um nó no grafo ontológico.
     */
    @PostMapping("/nodes")
    public ResponseEntity<Node> addNode(@RequestBody Node newNode) {
        if (newNode == null || newNode.getId() == null || newNode.getId().isBlank()) {
            return ResponseEntity.badRequest().build();
        }
        graph.addNode(newNode);
        return ResponseEntity.status(HttpStatus.CREATED).body(newNode);
    }

    /**
     * Remove um nó e suas arestas associadas pelo ID.
     */
    @DeleteMapping("/nodes/{id}")
    public ResponseEntity<Void> deleteNode(@PathVariable String id) {
        Node removed = graph.removeNode(id);
        if (removed != null) {
            return ResponseEntity.noContent().build();
        }
        return ResponseEntity.notFound().build();
    }

    // =========================================================================
    // ENDPOINTS DE ARESTAS (EDGES)
    // =========================================================================

    /**
     * Retorna a lista de todas as arestas/relações do grafo.
     */
    @GetMapping("/edges")
    public List<Edge> getAllEdges() {
        return graph.getEdges();
    }

    /**
     * Retorna uma aresta pelo seu identificador.
     */
    @GetMapping("/edges/{id}")
    public ResponseEntity<Edge> getEdgeById(@PathVariable String id) {
        Edge edge = graph.getEdge(id);
        if (edge != null) {
            return ResponseEntity.ok(edge);
        }
        return ResponseEntity.notFound().build();
    }

    /**
     * Adiciona ou atualiza uma relação ontológica (aresta) entre dois nós.
     */
    @PostMapping("/edges")
    public ResponseEntity<Edge> addEdge(@RequestBody Edge newEdge) {
        if (newEdge == null || newEdge.getId() == null || newEdge.getId().isBlank()) {
            return ResponseEntity.badRequest().build();
        }
        graph.addEdge(newEdge);
        return ResponseEntity.status(HttpStatus.CREATED).body(newEdge);
    }

    /**
     * Remove uma aresta pelo seu identificador.
     */
    @DeleteMapping("/edges/{id}")
    public ResponseEntity<Void> deleteEdge(@PathVariable String id) {
        boolean removed = graph.removeEdge(id);
        if (removed) {
            return ResponseEntity.noContent().build();
        }
        return ResponseEntity.notFound().build();
    }

    // =========================================================================
    // ENDPOINTS DE ESTADO GERAL E REGRAS SWRL
    // =========================================================================

    /**
     * Retorna a estrutura completa do grafo ontológico (Nós e Arestas).
     */
    @GetMapping("/graph")
    public ResponseEntity<Graph> getFullGraph() {
        return ResponseEntity.ok(this.graph);
    }

    /**
     * Dispara o motor de inferência SWRL sobre as instâncias do grafo.
     */
    @PostMapping("/evaluate-rules")
    public ResponseEntity<Map<String, Object>> evaluateSWRL() {
        List<String> logs = graph.evaluateSWRLRules();
        Map<String, Object> response = new HashMap<>();
        response.put("success", true);
        response.put("logs", logs);
        response.put("nodesCount", graph.getNodeCount());
        response.put("edgesCount", graph.getEdgeCount());
        return ResponseEntity.ok(response);
    }

    /**
     * Reinicializa o grafo para o cenário ontológico padrão do Pac-Man Semântico.
     */
    @PostMapping("/reset")
    public ResponseEntity<Map<String, String>> resetToDefault() {
        graph.initializeDefaultPacManOntology();
        Map<String, String> res = new HashMap<>();
        res.put("status", "Ontologia resetada para padrão com sucesso");
        res.put("nodesCount", String.valueOf(graph.getNodeCount()));
        res.put("edgesCount", String.valueOf(graph.getEdgeCount()));
        return ResponseEntity.ok(res);
    }

    /**
     * Health check e metadados do serviço para o cliente Unity.
     */
    @GetMapping("/status")
    public Map<String, Object> getStatus() {
        Map<String, Object> status = new LinkedHashMap<>();
        status.put("service", "Pac-Man Semantic Ontology Service (VirtOnto)");
        status.put("status", "UP");
        status.put("institution", "CIn-UFPE");
        status.put("version", graph.getVersion());
        status.put("totalNodes", graph.getNodeCount());
        status.put("totalEdges", graph.getEdgeCount());
        return status;
    }
}
