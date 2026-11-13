# Vhaldren

Juego de acción/supervivencia de fantasía oscura en Unity 6 (`6000.2.12f1`), con cámara elevada,
combate direccional, magia, construcción, razas, progresión y mundo abierto en desarrollo.

## Rama activa

**Continuar únicamente en:** `feature/018-consolidacion-pre-modelado`.

La rama 018 parte de la vertical slice 017 y consolida sus sistemas antes de comenzar la producción de
arte final. `feature/017-integracion-jugable-pueblos-combate` queda como respaldo.

Escena de juego: `Assets/Scenes/WorldPrototype001.unity`.
Menú principal: `Assets/Scenes/MainMenu.unity`.

## Abrir y probar

Abre el proyecto con Unity `6000.2.12f1`, carga `MainMenu.unity` o
`WorldPrototype001.unity` y pulsa Play. F1 muestra los controles actuales.

El proyecto usa Input System 1.18.0. Movimiento, esquiva, combate, defensas, magia, recolección,
construcción, pueblos y varias acciones de UI pasan por el mapa reasignable de `GameInput`.
Clic izquierdo, rueda, Mayús de colocación libre y Ctrl+Z siguen siendo atajos fijos.

## Estado de validación

La última validación registrada de la **017** por Claude fue 81/81 EditMode y 113/113 PlayMode en Unity,
más una build de Windows. La **018 añade correcciones y nuevas regresiones que todavía deben ejecutarse
en Unity**. No darla por certificada hasta correr Test Runner completo.

## Regla de arte y Blender

`WorldPrototype001.unity` es ahora la escena artística persistente. No regenerarla.

El generador técnico está aislado en:
`Vhaldren > Blockout > Regenerar mundo técnico (NO toca escena jugable)`
y escribe `WorldBlockoutGenerated.unity` más recursos en `Assets/WorldBlockout`.

Los modelos finales deben quedar como hijos visuales y usar `AuthoredVisual`. IA, vida, hitboxes,
CharacterController y reglas de combate permanecen en la raíz lógica de Unity. Esto permite reemplazar
placeholders por modelos Blender sin reprogramar el juego.

## Documentación vigente

- `docs/ESTADO.md`: estado real y qué falta validar.
- `docs/TRASPASO.md`: instrucciones para cualquier agente que continúe.
- `docs/018-CONSOLIDACION-PRE-MODELADO.md`: bugs corregidos y contrato Blender → Unity.
- `docs/019-MAGIA-ESENCIA-BESTIARIO-Y-EJECUTABLE.md`: detalle de la vertical slice 017 y su última validación reportada.
- `docs/021-EFECTOS-HECHIZOS-Y-MUERTE-ENEMIGOS.md`: efectos de hechizos por capas, avisos que se llenan y muerte animada de enemigos.

Los documentos numerados anteriores se conservan como historial; no son la fuente actual de rama/estado.
