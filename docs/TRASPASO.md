# Traspaso — línea activa

Actualizado: 12 de noviembre de 2026.

## Continuar aquí

Rama: `feature/018-consolidacion-pre-modelado`.
Base de respaldo: `feature/017-integracion-jugable-pueblos-combate` (PR #16).
Escena de juego: `Assets/Scenes/WorldPrototype001.unity`.
No desarrollar nuevas features sobre 007/012/015: son historia del proyecto.

## Regla de escena

`WorldPrototype001.unity` es persistente y se editará para arte final.
`WorldPrototypeSceneTool` fue aislado en 018: su menú `Vhaldren/Blockout/...` genera
`WorldBlockoutGenerated.unity`, no la escena jugable. Nunca volver a apuntar ese generador al mundo final.

## Regla de modelos Blender

Gameplay en raíz; arte en hijo. Añadir `AuthoredVisual` a la raíz visual del modelo/prefab.
`EnemyVisualPrototype` y `EnemyArchetypeVisual` respetan esa marca y no crean primitivas encima.
Los prefabs KayKit generados también reciben la marca.

No trasladar colliders/vida/IA al FBX. El FBX es apariencia y animación; el prefab de Unity hace la unión.

## Correcciones 018 ya publicadas

Raycasts/SphereCasts/Overlap saturables críticos tienen fallback completo; DOT y Estática usan el receptor
central; Estallido infernal respeta paredes; misiones aceptan derrotas posteriores de enemigos reaparecidos;
ranuras 1–0 de construcción son reasignables; puntero de construcción funciona en escenas densas; build
settings deja solo menú y mundo final habilitados.

Hay nuevas pruebas para saturación de física, DOT/resistencias, tracking de daño, misiones reaparecidas,
controles contextuales y protección de modelos autorales.

## Validación

La 017 fue reportada por Claude con 81/81 EditMode y 113/113 PlayMode en Unity 6000.2.12f1.
La 018 NO se ha ejecutado todavía en Unity desde esta auditoría. No afirmar que compila/pasa tests hasta
hacer esa ejecución.

## Próximo paso después de validar

Empezar modelado por un vertical slice artístico, no por cientos de assets: kit modular de una villa,
entrada/interior corto de cueva, un enemigo original completo y una pieza de construcción. Integrarlos
mediante `AuthoredVisual`, comprobar escala/rig/colisiones/LOD/materiales y recién después producir variantes.
