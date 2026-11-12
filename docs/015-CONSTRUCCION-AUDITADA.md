# 015 — Auditoría de 012-014 y construcción base

Por Claude, 12 nov 2026. Ejecutado en Unity 6000.2.12f1 (modo batch): **EditMode 67/67, PlayMode 71/71.**

## Auditoría de lo subido (012, 013, 014)

- Compila. Antes de mis cambios: EditMode 62/62, PlayMode 62/63.
- **Error real corregido:** `LocalPlayerShooter` limitaba el rayo exactamente a la distancia del punto apuntado, que está en la superficie del objetivo; por precisión el rayo se quedaba a milímetros y el disparo no hacía daño. Ahora tiene 35 cm de margen.
- Hay dos líneas paralelas de supervivencia: la 014 (inventario en `Assets/Scripts/Survival`) y la copia local de Codex `wip/codex-009-010` (otro inventario y constructor en `Assets/Scripts/Player`). **No se mezclaron**: chocarían. Esta rama sigue la 014; de la WIP se pueden rescatar ideas (necesidades, hechizos) más adelante.

## Construcción y recolección (P0 del estudio de Enshrouded)

| Tecla | Acción |
|---|---|
| B | Entrar/salir del modo construcción (bloquea ataques, no el movimiento) |
| 1-0 | Elegir pieza |
| R | Girar 90° |
| Shift | Colocación libre en pasos de 25 cm |
| Clic izq. | Construir (solo cobra si se coloca) |
| G | Desmontar lo apuntado (devuelve todo) |
| Ctrl+Z | Deshacer (hasta 30 piezas, devuelve todo) |
| E (mantener) | Recolectar árbol, roca, fibra o mineral |

Piezas: cimiento, suelo/techo, pared, marco de puerta*, escalera*, fogata, banco de trabajo, cofre, cama*, estacas. (*necesitan banco de trabajo a 12 m)

- **Fogata:** cura 4 PV/s a menos de 4 m y da luz.
- **Cama:** cambia el punto de reaparición; al desmontarla vuelve al original.
- **Cofre:** +120 de capacidad al almacén compartido.
- La vista previa sale verde o roja y la interfaz siempre dice **por qué** no se puede construir.

Arquitectura separada: datos (`BuildCatalog`), encaje (`BuildGrid`), reglas (`BuildPlacement`), creación (`BuildPieceFactory`), estado (`BuiltPiece`), entrada (`PlayerBuilder`), recolección (`HarvestNode`, `PlayerGatherer`), interfaz (`SurvivalHud`).

## Pendiente (P1)

Guardado persistente, planos reutilizables, puertas que abren, artesanos, recetas de equipo en el banco, modelos reales en vez de formas básicas.
