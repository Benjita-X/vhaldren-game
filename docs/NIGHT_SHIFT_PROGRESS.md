# Vhaldren — avance de auditoría nocturna

## Estado inicial — 2026-11-10

- Rama inspeccionada: `feature/001-movimiento-base`.
- HEAD remoto/local: `c4c0c1ced5528550d121d366df286b2e7dfbd024` (`Paso 002: aceleracion, frenado e inversion configurables sin alterar apuntado`).
- PR #1: abierto, en borrador, base `main`; no se fusionará.
- El checkout de trabajo es independiente de la copia Unity del usuario y de UMBRAL.
- El repositorio remoto versiona scripts y documentación; no contiene `Packages/`, `ProjectSettings/`, escenas ni pruebas automatizadas.
- La copia local abierta en Unity tiene un `Movement001.unity` y materiales de prueba creados por el usuario. No es un checkout Git y no se ha modificado durante esta misión.

## Hallazgos iniciales

- `PlayerHorizontalMotion.Step` es una función pura y apta para pruebas, pero no hay pruebas automatizadas.
- `PlayerMotor` sanea velocidad de movimiento pero no gravedad ni velocidad de giro no finitas; la gravedad serializada inválida puede contaminar la velocidad vertical.
- `SimpleFollowCamera` no sanea `offset`, `lookOffset` ni `followSharpness` no finitos.
- `LocalPlayerInput.Update` omite completamente `Simulate` si no hay cámara, así que también omite gravedad y contacto.
- La prueba de colisión sólo está descrita en documentación; no hay prueba automatizada de respuesta y deslizamiento.
- El validador de escena confirma conexiones estructurales, pero no valida dimensiones físicas, configuración de respuesta horizontal ni ajustes inválidos del Inspector.

## Registro por fase

| Fase | Estado | Resultado |
|---|---|---|
| 1. Reconocimiento | Completa | Rama `feature/001-movimiento-base` y PR #1 abiertos confirmados; trabajo en clon aislado. |
| 2. Perfeccionamiento del movimiento | Completa | Entradas extremas, magnitudes no representables, gravedad y giro no finitos saneados; velocidad real medida con el delta del paso; colisión bloqueada no acumula inercia. |
| 3. Pruebas automatizadas | Completa | 16/16 EditMode y 6/6 PlayMode pasan con Unity 6000.2.12f1. La prueba de pared fija delta de captura para evitar depender del frame real. |
| 4. Cámara, entrada y orientación | Completa | Cámara recupera valores inválidos, entrada sigue simulando sin cámara y el aim puede rotar sólo el hijo visual. |
| 5. Integración y reproducibilidad | Completa | Se incorporaron manifiesto, versión de Unity, asmdefs, escena guardada, materiales y documentación. Unity generó la escena y el validador informó estructura correcta. |
| 6. Auditoría integral | Completa | Se revisaron cambios con `git diff --check`, se ejecutaron ambas suites y se verificó la rama antes de publicar. |

## Commits

- Se crearán commits pequeños por código/pruebas, proyecto/escena y documentación antes del push final.

## Límite de verificación actual

- La captura del usuario confirma que una escena generada localmente carga sin errores visibles y permite observar el suelo rojo y el muñeco negro.
- No equivale a probar la rama remota actual: la copia Unity abierta no es el checkout Git y no contiene de forma confirmada el solver del Paso 002.
- Verificado en Unity `6000.2.12f1`: EditMode 16/16, PlayMode 6/6; generación de `Movement001.unity` y validación estructural correcta.
- La prueba manual interactiva aún queda por hacer en el Editor del usuario. Los XML/logs generados durante esta auditoría son resultados locales ignorados por Git.
- No se ha fusionado el PR ni se ha tocado `main` o UMBRAL.

## Commits publicados

- `e2a7e31` — endurecimiento del movimiento y pruebas automatizadas.
- `72afa87` — proyecto Unity 6 reproducible, escena y materiales.
- `e3f9860` — documentación de instalación y verificación.
- `7bb0eba` — bitácora de resultados y límites de verificación.

Push final realizado a `origin/feature/001-movimiento-base`; HEAD publicado: `7bb0eba7d0dbd940eb48d95657c894829c895b0a`.

## Preparación visual para prueba de movimiento — 2026-11-10

- La escena se amplió con un personaje 3D provisional de fantasía oscura, creado con primitivas de Unity y materiales separados para tela, capa, armadura, piel, cuero y metal.
- La capa tiene una malla trapezoidal con borde y emblema metálicos; el visual no contiene colliders. El `CharacterController` sigue siendo la única colisión del jugador y el marcador de apuntado queda visible desde la cámara elevada.
- La cámara quedó a 10 unidades de altura y distancia para que el personaje ocupe más pantalla conservando el ángulo elevado.
- El generador creó y validó `Movement001.unity` con el modelo visual; las últimas corridas en Unity dieron EditMode 16/16 y PlayMode 6/6.
- El modelo es un bloque visual temporal, no un asset texturizado final.
- Unity quedó abierto en `Movement001`, en modo edición, listo para pulsar Play.
- La carpeta ZIP del usuario no se reemplazó; el proyecto actualizado está en el checkout Git.

## Respuesta de movimiento y puntería — 2026-11-10

- Se activó movimiento horizontal inmediato para que toques muy cortos de WASD sean visibles; la cápsula física sigue separada del visual y el apuntado no cambia la dirección de marcha.
- El punto de puntería ahora consulta el mundo bajo el pixel del cursor. El clic izquierdo dispara un hitscan de prueba desde el personaje y muestra un trazador; los obstáculos interpuestos lo detienen. No hay daño ni habilidades en esta fase.
- Se añadió un marcador de suelo para el punto de puntería y se amplió el validador para revisar sus referencias.
- Se añadieron dos pruebas PlayMode: respuesta a un toque corto con inversión de dirección y coincidencia entre el cursor, el punto de puntería y el impacto.
- Verificado en Unity 6.2, en una copia aislada del proyecto: EditMode 16/16 y PlayMode 8/8 aprobadas. El PlayMode cubre las nuevas pruebas de toque corto y puntería/disparo.
- Se comprobó manualmente en Unity que un toque de W mueve al personaje y que el visual gira hacia la posición del cursor; el disparo se valida también en PlayMode.
- La animación de piernas se deja para después, como parte separada.

