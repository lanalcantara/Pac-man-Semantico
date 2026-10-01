package com.virtonto.model;

/**
 * Representação de Nó Ontológico na arquitetura VirtOnto.
 * Pode representar classes da TBox (ex: Pacman, Ghost), indivíduos da ABox, ou regras SWRL.
 */
public class Node extends OntologyElement {
    private String type; // Class, Individual, Property, Rule, Axiom
    private Vector3D position;
    private Vector3D velocity;
    private float mass;
    private String displayColor;
    private String conceptClass;
    private boolean valid;

    public Node() {
        super();
        this.type = "Individual";
        this.position = new Vector3D(0.0f, 0.0f, 0.0f);
        this.velocity = new Vector3D(0.0f, 0.0f, 0.0f);
        this.mass = 1.0f;
        this.displayColor = "#FFFFFF";
        this.conceptClass = "";
        this.valid = true;
    }

    public Node(String id, String label, String type) {
        super(id, label);
        this.type = type != null ? type : "Individual";
        this.position = new Vector3D(0.0f, 0.0f, 0.0f);
        this.velocity = new Vector3D(0.0f, 0.0f, 0.0f);
        this.mass = 1.0f;
        this.displayColor = "#FFFFFF";
        this.conceptClass = label;
        this.valid = true;
    }

    public Node(String id, String label, String type, Vector3D position, String displayColor) {
        super(id, label);
        this.type = type != null ? type : "Individual";
        this.position = position != null ? position : new Vector3D(0.0f, 0.0f, 0.0f);
        this.velocity = new Vector3D(0.0f, 0.0f, 0.0f);
        this.mass = 1.0f;
        this.displayColor = displayColor != null ? displayColor : "#FFFFFF";
        this.conceptClass = label;
        this.valid = true;
    }

    // Getters e Setters solicitados
    public String getType() { return type; }
    public void setType(String type) { this.type = type; }

    public Vector3D getPosition() { return position; }
    public void setPosition(Vector3D position) { this.position = position; }

    public Vector3D getVelocity() { return velocity; }
    public void setVelocity(Vector3D velocity) { this.velocity = velocity; }

    public float getMass() { return mass; }
    public void setMass(float mass) { this.mass = mass; }

    public String getDisplayColor() { return displayColor; }
    public void setDisplayColor(String displayColor) { this.displayColor = displayColor; }

    public String getConceptClass() { return conceptClass; }
    public void setConceptClass(String conceptClass) { this.conceptClass = conceptClass; }

    public boolean isValid() { return valid; }
    public void setValid(boolean valid) { this.valid = valid; }

    @Override
    public String toString() {
        return String.format("Node{id='%s', label='%s', type='%s', color='%s', pos=%s}",
                id, label, type, displayColor, position);
    }
}
