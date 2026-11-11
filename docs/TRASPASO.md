# Traspaso — dónde quedó el trabajo

Actualizado: 11 de noviembre de 2026, por Claude. Si retomas (Claude o Codex), lee esto y `docs/ESTADO.md` primero.

## Ramas (de la más antigua a la más nueva, cada una contiene a la anterior)

| Rama | PR | Qué trae |
|---|---|---|
| `feature/001-movimiento-base` | #1 | Control base, circuito de prueba |
| `feature/002-esquiva-zoom` | #2 | Esquiva y zoom |
| `audit/correcciones-control-camara-2026-11-10` | #3 | Cámara evita paredes (Codex) |
| `feature/004-kaykit-combat-slice` | #5 | Sala KayKit, vida y daño, modificadores (Codex) |
| `audit/claude-004` | #4 | Arreglo de importación KayKit y modificadores conectados al motor |
| `feature/006-personaje-kaykit` | #6 | Personaje Rogue encapuchado animado |
| `feature/007-integracion` | #7 | **La más completa.** 006 + las 14 correcciones finales de Codex en 004 |

Para probar todo: abrir en Unity una copia de `feature/007-integracion` y pulsar Play en `Movement001` o `KayKitCombat001`.

## Última verificación

Unity 6000.2.12f1, modo batch, rama 007 con Library limpia: EditMode 60 de 60, PlayMode 39 de 39. Nadie ha jugado todavía nada posterior a la rama 001.

## Personaje animado (rama 006)

- Modelo CC0 `Assets/ThirdParty/KayKitAdventurers/RogueHooded.fbx` (70 animaciones). Licencia en la misma carpeta.
- `KayKitCharacterSetup` (editor) configura la importación y crea el Animator: mezcla direccional de piernas (correr, atrás, lados) según hacia dónde mira, esquivas en 4 direcciones y disparo.
- `PlayerAnimationDriver` (juego) solo lee el motor y alimenta el Animator. Nunca mueve al personaje (`applyRootMotion = false`).
- Si el modelo no está, el generador vuelve al muñeco de formas básicas.

## Siguiente paso recomendado

1. Que el dueño juegue la rama 006 y diga qué se siente mal.
2. Enemigo esqueleto CC0 (ya descargado en `work/terceros/KayKit-Character-Pack-Skeletons-1.0`) que persiga y ataque, usando `CombatHealth`.
3. Multijugador cooperativo: elegir UNA red (Netcode for GameObjects o FishNet) con un prototipo de 2 jugadores; el disparo debe validarse en el servidor.

## Reglas

- No fusionar a `main` sin que el dueño lo apruebe jugando.
- Cada arreglo con una prueba que fallaba antes. Anotar resultados reales.
- Dos agentes no deben trabajar en la misma carpeta a la vez.

## Lecciones

- No copiar `Library` de otra carpeta: deja scripts "missing" en las escenas. Borrarla y dejar que Unity reimporte.
- En EditMode Unity no llama `OnDisable` al cambiar `enabled`.
