package com.virtonto.model;

/**
 * Representação de Aresta Ontológica (Relação ou Axioma direcionado).
 * Exemplos: isNearTo, consumes, rdfs:subClassOf, hasState.
 */
public class Edge extends OntologyElement {
    private String sourceId;
    private String targetId;
    private String relationType;
    private float weight;

    public Edge() {
        super();
        this.sourceId = "";
        this.targetId = "";
        this.relationType = "relatesTo";
        this.weight = 1.0f;
    }

    public Edge(String id, String sourceId, String targetId, String relationType) {
        super(id, relationType);
        this.sourceId = sourceId != null ? sourceId : "";
        this.targetId = targetId != null ? targetId : "";
        this.relationType = relationType != null ? relationType : "relatesTo";
        this.weight = 1.0f;
    }

    public Edge(String id, String sourceId, String targetId, String relationType, float weight) {
        super(id, relationType);
        this.sourceId = sourceId != null ? sourceId : "";
        this.targetId = targetId != null ? targetId : "";
        this.relationType = relationType != null ? relationType : "relatesTo";
        this.weight = weight;
    }

    public String getSourceId() {
        return sourceId;
    }

    public void setSourceId(String sourceId) {
        this.sourceId = sourceId;
    }

    public String getTargetId() {
        return targetId;
    }

    public void setTargetId(String targetId) {
        this.targetId = targetId;
    }

    public String getRelationType() {
        return relationType;
    }

    public void setRelationType(String relationType) {
        this.relationType = relationType;
        this.label = relationType;
    }

    public float getWeight() {
        return weight;
    }

    public void setWeight(float weight) {
        this.weight = weight;
    }

    @Override
    public String toString() {
        return String.format("Edge{id='%s', '%s' --[%s]--> '%s', weight=%.2f}",
                id, sourceId, relationType, targetId, weight);
    }
}
