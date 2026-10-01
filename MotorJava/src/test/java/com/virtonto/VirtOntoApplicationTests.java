package com.virtonto;

import com.virtonto.controller.OntologyController;
import com.virtonto.model.Edge;
import com.virtonto.model.Node;
import com.virtonto.model.Vector3D;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.http.ResponseEntity;

import java.util.Map;

import static org.junit.jupiter.api.Assertions.*;

@SpringBootTest
class VirtOntoApplicationTests {

    @Autowired
    private OntologyController controller;

    @Test
    @DisplayName("Contexto do Spring Boot deve carregar com sucesso")
    void contextLoads() {
        assertNotNull(controller, "O OntologyController deve ser injetado pelo Spring");
    }

    @Test
    @DisplayName("Vector3D deve calcular distâncias e álgebra euclidiana corretamente")
    void testVector3D() {
        Vector3D v1 = new Vector3D(0f, 0f, 0f);
        Vector3D v2 = new Vector3D(3f, 4f, 0f);
        assertEquals(5.0f, v1.distanceTo(v2), 0.001f);

        Vector3D soma = v1.add(v2);
        assertEquals(3.0f, soma.getX());
        assertEquals(4.0f, soma.getY());
    }

    @Test
    @DisplayName("OntologyController deve listar nós e permitir adição de novo nó")
    void testNodesEndpoints() {
        Map<String, Node> nodes = controller.getAllNodes();
        assertTrue(nodes.containsKey("pacman_01"), "Deve conter o nó pacman_01 inicial");

        Node newNode = new Node("ghost_inky", "Inky", "Individual", new Vector3D(1f, 0f, 1f), "#00FFFF");
        ResponseEntity<Node> response = controller.addNode(newNode);
        assertEquals(201, response.getStatusCode().value());
        assertNotNull(response.getBody());
        assertEquals("ghost_inky", response.getBody().getId());

        Node retrieved = controller.getNodeById("ghost_inky").getBody();
        assertNotNull(retrieved);
        assertEquals("Inky", retrieved.getLabel());
    }

    @Test
    @DisplayName("OntologyController deve gerenciar arestas ontológicas")
    void testEdgesEndpoints() {
        Edge edge = new Edge("edge_test", "pacman_01", "ghost_blinky", "evades");
        ResponseEntity<Edge> response = controller.addEdge(edge);
        assertEquals(201, response.getStatusCode().value());

        Edge retrieved = controller.getEdgeById("edge_test").getBody();
        assertNotNull(retrieved);
        assertEquals("evades", retrieved.getRelationType());
    }
}
