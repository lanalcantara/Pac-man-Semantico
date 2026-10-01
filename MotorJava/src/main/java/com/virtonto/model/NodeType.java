package com.virtonto.model;

/**
 * Categorias ontológicas fundamentais da arquitetura VirtOnto e OWL Description Logic.
 */
public enum NodeType {
    CLASS,          // Conceito na TBox (ex: Pacman, Ghost, Item, Obstacle)
    INDIVIDUAL,     // Instância na ABox (ex: pacman_01, ghost_blinky)
    PROPERTY,       // Object ou Data Property (ex: consumes, isNearTo)
    RULE,           // Regra lógica SWRL
    AXIOM           // Axioma formal de Description Logic (ALCQ(D))
}
