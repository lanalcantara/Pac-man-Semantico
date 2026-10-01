package com.virtonto.model;

import java.util.HashMap;
import java.util.Map;
import java.util.Objects;

/**
 * Classe base abstrata para todos os elementos ontológicos (Nós e Arestas).
 * Espelha a hierarquia VirtOnto em C++ e C#.
 */
public abstract class OntologyElement {
    protected String id;
    protected String label;
    protected Map<String, String> properties;

    public OntologyElement() {
        this.id = "";
        this.label = "";
        this.properties = new HashMap<>();
    }

    public OntologyElement(String id, String label) {
        this.id = id != null ? id : "";
        this.label = label != null ? label : "";
        this.properties = new HashMap<>();
    }

    public String getId() {
        return id;
    }

    public void setId(String id) {
        this.id = id;
    }

    public String getLabel() {
        return label;
    }

    public void setLabel(String label) {
        this.label = label;
    }

    public Map<String, String> getProperties() {
        return properties;
    }

    public void setProperties(Map<String, String> properties) {
        this.properties = properties != null ? properties : new HashMap<>();
    }

    public void setProperty(String key, String value) {
        if (this.properties == null) {
            this.properties = new HashMap<>();
        }
        this.properties.put(key, value);
    }

    public String getProperty(String key) {
        return this.properties != null ? this.properties.get(key) : null;
    }

    @Override
    public boolean equals(Object o) {
        if (this == o) return true;
        if (o == null || getClass() != o.getClass()) return false;
        OntologyElement that = (OntologyElement) o;
        return Objects.equals(id, that.id);
    }

    @Override
    public int hashCode() {
        return Objects.hash(id);
    }
}
