# Traspaso — dónde quedó el trabajo

Actualizado: 11 de noviembre de 2026, por Claude. Si retomas (Claude o Codex), lee esto y `docs/ESTADO.md` primero.

## Ramas (de la más antigua a la más nueva, cada una contiene a la anterior)

| Rama | PR | Qué trae |
|---|---|---|
| `feature/001-movimiento-base` | #1 | Control base, circuito de prueba |
| `feature/002-esquiva-zoom` | #2 | Esquiva y zoom |
| `audit/correcciones-control-camara-2026-11-10` | #3 | Cámara evita paredes (Codex) |
| `feature/004-kaykit-combat-slice` | — | Sala KayKit, vida y daño, modificadores (Codex) |
| `audit/claude-004` | #4 | Arreglo de importación KayKit y modificadores conectados al motor |
| `feature/006-personaje-kaykit` | #5 | **La más completa.** Personaje Rogue encapuchado animado |

Para probar todo: abrir en Unity una copia de `feature/006-personaje-kaykit` y pulsar Play en `Movement001` o `KayKitCombat001`.

## Última verificación

Unity 6000.2.12f1, modo batch, rama 006: EditMode 55 de 55, PlayMode 37 de 37. Nadie ha jugado todavía nada posterior a la rama 001.

## Personaje animado (rama 006)

- Modelo CC0 `Assets/ThirdParty/KayKitAdventurers/RogueHooded.fbx` (70 animaciones). Licencia en la misma carpeta.
- `KayKitCharacterSetup` (editor) configura la importación y crea el Animator: mezcla direccional de piernas (correr, atrás, lados) según hacia dónde mira, esquivas en 4 direcciones y disparo.
- `PlayerAnimationDriver` (juego) solo lee el motor y alimenta el Animator. Nunca mueve al personaje (`applyRootMotion = false`).
- Si el modelo no está, el generador vuelve al muñeco de formas básicas.

## Siguiente paso recomendado

1. Que el dueño juegue la rama 006 y diga qué se siente mal.
2. Enemigo esqueleto CC0 (ya descargado en `work/terceros/KayKit-Character-Pack-Skeletons-1.0`) que persiga y ataque, usando `CombatHealth`.
3. Multijugador cooperativo (Netcode for GameObjects) antes de sumar muchas habilidades.

## Reglas

- No fusionar a `main` sin que el dueño lo apruebe jugando.
- Cada arreglo con una prueba que fallaba antes. Anotar resultados reales.
- Dos agentes no deben trabajar en la misma carpeta a la vez.
