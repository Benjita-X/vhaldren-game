# Auditoría visual y técnica 013 — daños coherentes entre armas

**Referencia:** dos videos MP4 entregados en el chat, examinados visualmente mediante planchas de fotogramas cada 3 segundos. Se procesaron sus metadatos: `Download(1).mp4` de 59 s y `Download(2).mp4` de 144,6 s, ambos 576×1024/30 FPS.

## Lo observado directamente

- **Video 1:** recolección de minerales y sangre (0–6 s), equipamiento e inventario (12 s), campos y bosque con hitos (18–27 s), cofre y mobiliario de refugio (30–39 s), elaboración y combate con jefes V Blood (39–50 s). Interesa que obtener materiales y progresar se ve y se entiende sin romper la exploración.
- **Video 2:** combate contra Solarus. La arena circular marca el espacio seguro; el jefe usa ráfagas de luz, ataques de área, zonas de amenaza y oleadas de proyectiles. Entre 60 y 90 s aparece otro enemigo de gran silueta. Hay ataques superpuestos, pausas de recuperación, números de daño y cambios de posición.
- **No visible/verificable en las grabaciones:** daño real interno, arquitectura, autoridad de red, duración exacta de invulnerabilidad, hitboxes y eventuales colisiones de proyectiles.

## Auditoría de GitHub

Se encontró que `feature/012-armas-defensas-enemigos` ya implementa ballesta con `PhysicalArrowProjectile` (flecha que viaja), tajo V, pulso T, guardia física Z, barrera mágica C, parry X, resistencias por `CombatDamageKind`, modelos procedimentales texturizados y barra de PV/daño emergente. No se debe recrear todo esto desde cero.

### Defecto funcional encontrado y corregido

El HUD de `PrototypeGameSession` todavía mostraba `LocalPlayerShooter.HitsLanded` y `TotalDamageDealt`, que principalmente registraban flechas. Los golpes de espada y pulso sí bajaban vida y mostraban números sobre la víctima, pero no actualizaban el contador principal del jugador.

**Corrección 013:** `PlayerDamageTracker` desacoplado de armas y habilidades. `LocalPlayerShooter` registra sus impactos reales, `PlayerCombatAbilities` registra tajos y magia a partir del resultado aplicado de `CombatDamageReceiver`, y el HUD muestra GOLPES, DAÑO TOTAL y subtotales FLECHA / ESPADA / MAGIA. El nuevo tracker **no calcula ni aplica daño**; solo escucha los resultados ya decididos y evita registrar valores 0, NaN o Infinity.

Se agregaron dos pruebas EditMode para suma por tipo y rechazo de valores inválidos.


## Habilidad adicional implementada en 013: F — Onda de escarcha

- Activación con **F** (Input System y fallback legacy), resistencia compartida (22), cooldown 6 s.
- Ataque frontal en cono de 75°, 6 m de alcance, aviso visual y espera de 0,25 s. No inmoviliza al jugador ni modifica `PlayerMotor`.
- Inflige 14 PV de daño mágico antes de resistencias y defensa; nunca atraviesa obstáculos mediante la comprobación existente `Unobstructed`.
- A los enemigos efectivamente dañados les aplica una ralentización del 45 % (velocidad ×0,55) por 1,75 segundos. No cambia permanentemente `moveSpeed`, no mueve colisiones y no acumula la reducción multiplicativamente.
- La ralentización desaparece al reaparecer y puede renovarse sin llevar la velocidad a cero.
- Los impactos registran daño efectivo en `PlayerDamageTracker` igual que espada, flecha y pulso.
- Prueba PlayMode nueva: impacto delante, no impacto detrás, cooldown, ralentización y contador mágico.
- **No se ejecutó Unity ni se verificó visualmente el efecto en Play Mode** en esta sesión.

## Limitaciones que persisten

- Los enemigos `EnemyVisualPrototype` se componen de primitivas 3D y texturas procedurales de ruido. No son todavía modelos profesionales riggeados con animaciones de combate. El proyecto tiene un RogueHooded CC0 para el jugador; conviene importar enemigos con licencia, Animator y retargeting antes de añadir una biblioteca de jefes.
- La IA usa movimiento directo (sin NavMesh), lo que puede atascar al enemigo con árboles y ruinas.
- No hay verificación de construcción/supervivencia aún: el trabajo local de otros agentes todavía no está en GitHub.
- `WorldPrototype001.unity` sigue siendo el primer mundo de prueba, no un mapa de producción.
- Para multijugador falta un servidor que autorice daño, movimiento, estados y colisiones.
- La validación estática **no equivale a una compilación Unity**. El Test Runner completo y el Play Mode manual están pendientes para 012 y 013.

## Pruebas que habilitan pasar al primer mapa bien hecho

1. Abrir rama 013 y `WorldPrototype001.unity`, comprobar Console sin errores.
2. Ballesta: clic izquierdo dispara una flecha que viaja visiblemente; al chocar aparece daño real, nunca al pulsar.
3. Tajo V: descuenta PV solo a enemigos delante y aumenta el subtotal ESPADA.
4. Pulso T: hace daño a varios objetivos visibles y aumenta subtotal MAGIA; no atraviesa paredes.
5. Z escudo: reduce golpes físicos, pero no magia. C barrera: reduce magia, pero no espada.
6. X parry: detiene un golpe frontal, habilita contraataque; por la espalda no bloquea.
7. Espacio dash: evita golpes correctamente sin quitar control del movimiento.
8. Matar, morir, curarse, reaparecer y matar al jefe; ver barras de PV y números correctos.
9. Repetir a 30 y 120 FPS; medir proyectiles, raycasts y renderizado.
10. Ejecutar tests EditMode y PlayMode, revisar la pila de PRs sin fusionar a `main` antes de validar.

## Siguientes mejoras recomendadas

1. Modelos enemigos CC0 con tres siluetas/sets animados, respetando las hitboxes originales.
2. Nuevo sistema de estados de combate para telegraphs de arcos, proyectiles, lluvia mágica y ataques de jefe en fases.
3. Hit stop muy breve y partículas de impacto con límites de rendimiento.
4. Vegetación que se desvanece en la línea cámara–jugador sin reducir legibilidad de amenazas.
5. Primera mazmorra corta y recorrido con botín, aliado y jefe.
