# 009 — Feedback de combate: vida, daño y confirmacion de impactos

Fecha: 11 de noviembre de 2026. Rama base: `audit/claude-008` (PR #9), que ya incluye el mundo `WorldPrototype001.unity` versionado y el arreglo de linea de vision de enemigos.

## Problema observado jugando

El personaje y los enemigos ya perdian vida y podian morir, pero no habia **barras de vida sobre los enemigos, numeros flotantes del dano ni contador visible de golpes y daño total**. Eso hacia imposible distinguir un fallo de punteria, un golpe real o un enemigo casi derrotado.

## Integracion realizada

- `CombatDamageFeedback`: se subscribe a `CombatHealth.Damaged` y `.Defeated` para mostrar vida actual/maxima, barra de color, `-25` flotando, flash de la barra y `DERROTADO`. El jefe tiene barra mayor y violeta.
- Los numeros reflejan **el daño aplicado** (`ApplyDamage` descuenta hasta los PV restantes). Golpes fallidos o contra objetivos ya muertos no muestran daño. En golpe letal con 10 PV, aparece `-10`, no un daño inventado de `-25`.
- `PrototypeEnemy.Awake` instala el componente si falta. Eso asegura compatibilidad con `WorldPrototype001.unity` **ya guardada** por Claude, sin regenerarla ni editar el YAML a mano.
- `PrototypeGameSession` agrega feedback al jugador: numeros rojos al recibir daño. Su HUD muestra impactos acertados, daño acumulado y valor del ultimo impacto.
- `LocalPlayerShooter` contabiliza `TotalDamageDealt` y `LastDamageDealt` a partir del retorno de `CombatHealth.ApplyDamage`; no suma disparos fallidos.
- El generador de mapas 008 tambien agrega el componente al jugador y cada enemigo de nuevos mundos.
- El componente usa un buffer fijo de **6 avisos** por objetivo y `Texture2D.whiteTexture`; no crea materiales o texturas por impacto. Textos se formatean por golpe, no por frame.

## Cómo probarlo

1. Cambiar a `feature/009-combate-feedback-vida-dano` en GitHub y abrir en Unity `6000.2.12f1`.
2. Abrir directamente la escena **existente** `Assets/Scenes/WorldPrototype001.unity` (no hace falta ejecutar el generador).
3. Pulsar Play y disparar a un guardián: debe aparecer una barra `100/100` o la vida específica del enemigo; al acertar verás `-25`, la barra bajará y el HUD aumentará `IMPACTOS` y `DANO TOTAL`.
4. Al morir el guardián, se verá `DERROTADO` brevemente y los números anteriores desaparecerán; al reaparecer, su barra estará llena.
5. Acercarse al jefe y comprobar su barra más ancha y morada. Dejar que un enemigo ataque: el jugador verá números rojos con el daño recibido.
6. Ejecutar el Test Runner **EditMode + PlayMode**, incluidos los **3 casos nuevos** de `CombatDamageFeedbackPlayModeTests`.

## Auditoría y seguridad del cambio

- El feedback es exclusivamente visual: no inventa ni aplica daño, no modifica `CombatHealth` ni el combate del enemigo.
- `OnEnable` subscribe a `Damaged` / `Defeated`; `OnDisable` desuscribe. Tests verifican no duplicar eventos.
- El daño local no es seguro para multijugador. Antes de FishNet/Netcode se requiere autoridad del servidor.
- Los nombres, textos y números usan IMGUI; es suficiente para 6 encuentros, **no** es la interfaz final de un MMO con cientos de enemigos.
- La barra solo se dibuja delante de la cámara y a distancia limitada; todavía no comprueba oclusión detrás de paredes.
- **NO he ejecutado Unity ni las pruebas nuevas desde esta rama.** La estabilidad deberá verificarse con Unity antes de fusionar.

**No fusionar a main antes de validar.**
