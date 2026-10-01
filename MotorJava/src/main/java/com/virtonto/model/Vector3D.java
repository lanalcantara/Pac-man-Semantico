package com.virtonto.model;

import java.util.Objects;

/**
 * Representação vetorial 3D alinhada à biblioteca VirtOnto em C++ e C#.
 * Fornece operações de distância euclidiana e álgebra vetorial.
 */
public class Vector3D {
    private float x;
    private float y;
    private float z;

    public Vector3D() {
        this(0.0f, 0.0f, 0.0f);
    }

    public Vector3D(float x, float y, float z) {
        this.x = x;
        this.y = y;
        this.z = z;
    }

    /**
     * Calcula a distância euclidiana tridimensional até outro ponto no espaço.
     */
    public float distanceTo(Vector3D other) {
        if (other == null) return 0.0f;
        float dx = this.x - other.x;
        float dy = this.y - other.y;
        float dz = this.z - other.z;
        return (float) Math.sqrt(dx * dx + dy * dy + dz * dz);
    }

    public Vector3D add(Vector3D other) {
        if (other == null) return new Vector3D(this.x, this.y, this.z);
        return new Vector3D(this.x + other.x, this.y + other.y, this.z + other.z);
    }

    public Vector3D subtract(Vector3D other) {
        if (other == null) return new Vector3D(this.x, this.y, this.z);
        return new Vector3D(this.x - other.x, this.y - other.y, this.z - other.z);
    }

    // Getters e Setters
    public float getX() { return x; }
    public void setX(float x) { this.x = x; }

    public float getY() { return y; }
    public void setY(float y) { this.y = y; }

    public float getZ() { return z; }
    public void setZ(float z) { this.z = z; }

    @Override
    public boolean equals(Object o) {
        if (this == o) return true;
        if (o == null || getClass() != o.getClass()) return false;
        Vector3D vector3D = (Vector3D) o;
        return Float.compare(vector3D.x, x) == 0 &&
               Float.compare(vector3D.y, y) == 0 &&
               Float.compare(vector3D.z, z) == 0;
    }

    @Override
    public int hashCode() {
        return Objects.hash(x, y, z);
    }

    @Override
    public String toString() {
        return String.format("Vector3D(%.2f, %.2f, %.2f)", x, y, z);
    }
}
