# Auditoría crítica del mundo 008 — 11 de noviembre de 2026

**Objeto:** `feature/008-mundo-jugable` sobre `feature/007-integracion`.
**Tipo:** inspección estática de código y estructura Git. **No se ejecutó Unity ni un build en esta auditoría.**
**Objetivo:** priorizar errores que puedan impedir jugar, corromper una escena o permitir daño y estados imposibles.

## Problemas encontrados y corregidos

| Prioridad | Fallo real o riesgo claro de código | Cambio en la rama |
|---|---|---|
| P1 | `CombatHealth` se añadía y el máximo se ajustaba después, pero su vida actual podía quedarse en 100 en vez de 150 (jugador) o los PV indicados para enemigos | Llamar `ResetToFull()` tras fijar el máximo en el generador |
| P1 | Al morir el jugador se desactivaba `PlayerMotor` pero seguía activo `LocalPlayerShooter`; podía disparar estando derrotado | Desactivar disparos y restaurar el estado previo al reaparecer |
| P1 | Enemigos fuera del radio de aggro no siempre regresaban al punto de aparición | Regreso al origen en ausencia de objetivo o de agresión |
| P1 | Al cruzar el radio máximo de persecución, el enemigo podía cambiar entre volver y perseguir cada frame | Estado `returningHome` que permanece hasta estar realmente cerca de su hogar |
| P1 | Si fallaba la generación del mapa después de abrir `Movement001`, había riesgo de alterar en memoria la escena original | Guardar la escena como `WorldPrototype001` **antes** de destruir el circuito de pruebas de la copia |
| P1 | Con escenas aditivas, una sesión podía contar enemigos ajenos o depender de `GameObject.Find` para encontrar a su jugador | Referencia serializada al jugador y filtrado de enemigos por escena |
| P2 | Suscripciones de eventos retenidas si un enemigo cambiaba de escena | Desuscripción siempre al desactivar `PrototypeGameSession`, limpieza de `HashSet` y referencias |
| P2 | Árboles se podían generar en la posición de enemigos del bosque | Exclusiones alrededor de los puntos de aparición |
| P2 | El mundo se generaba sin una comprobación estructural propia | Menú `Vhaldren/Validar mundo jugable 008` y validación automática de 6 enemigos, 1 jefe, suelo, referencias y vida |
| P2 | La prueba de IA dependía de un `GameObject.Find` con nombres potencialmente duplicados | Inyección del objetivo antes de activar el enemigo en tests |

Los cambios **no modifican** `Movement001.unity`, `PlayerMotor`, `LocalPlayerInput`, `PlayerDash`, `PlayerAnimationDriver` ni `Packages/manifest.json`.

## Pruebas PlayMode incluidas (5)

1. `EnemyDefeatDisablesCollisionAndNotifiesExactlyOnce`: muerte, colisión y evento único.
2. `EnemyApproachesPlayerInsideAggroRange`: persecución en suelo real.
3. `WallBlocksMeleeDamageUntilLineOfSightOpens`: no atravesar paredes y atacar cuando vuelve la línea de visión.
4. `EnemyReturnsHomeAfterCrossingItsLeashBoundary`: retorno persistente aunque vuelva a cruzar el límite.
5. `PlayerCannotShootWhenDeadAndCanShootAgainAfterRespawning`: desactivar disparo tras muerte, recuperar movilidad y ataque tras reaparecer.

**Estos cinco casos aún no fueron ejecutados ni compilados en esta auditoría.** Los 60/60 EditMode y 39/39 PlayMode reportados por Claude pertenecen a la rama base 007, no prueban el mundo 008.

## Qué requiere prueba REAL en Unity

- **P0 de validación:** `WorldPrototype001.unity` todavía **no existe versionada** en Git: se genera desde el menú de Unity. Sin generarla y ejecutar Play, no se puede certificar que el mapa funciona.
- Verificar cero errores de compilación en Unity 6000.2.12f1, ejecutar el menú `Vhaldren > Crear mundo jugable 008`, validar la escena desde el menú `Vhaldren > Validar mundo jugable 008` y correr las pruebas EditMode y PlayMode.
- En Play: caminar todo el perímetro, recorrer bosque y ruinas, probar cada enemigo, registrar golpes con y sin paredes, probar matar al jugador, reaparecer, curarse con Q, matar al jefe y completar la misión.
- Repetir a 30, 60, 144 FPS; usar Unity Profiler para medir memoria y CPU en persecución/colisiones.
- Guardar la nueva escena, su `.meta` y los materiales generados tras comprobar su aspecto en Unity.

## Riesgos abiertos (no prometer lo que falta)

- **IA sin navegación:** usa `CharacterController` y direcciones directas, así que los guardianes pueden atascarse con pilares o árboles. Para mapas grandes necesitarán navegación/pathfinding.
- **Ataque poco legible:** la IA golpea sin animación de aviso; hace falta telegrafiar y sincronizar daño antes de considerarlo combate satisfactorio.
- **Sin autoridad de servidor:** tanto disparos como vida son locales. No habilitar el cooperativo sin resolver impactos, estado y movimiento autoritativos.
- **Progreso temporal:** los guardianes reaparecen y la sesión contabiliza su primera muerte; esto es un prototipo de objetivos, no guardado persistente.
- **Arte provisional:** los enemigos son cápsulas, el mapa usa formas básicas; aún no es visualmente comparable a una producción completa.
- **Scripts y scene generada:** la comprobación de delimitadores y .meta en Git no equivale a compilación ni garantiza ausencia de errores de runtime.

## Criterio para comenzar la jornada siguiente

**Go:** escena generada y validada, Test Runner sin errores críticos, jugador capaz de matar enemigos/morir/reaparecer y completar el jefe, sin excepciones recurrentes ni bloqueos de movimiento.

**No-Go:** cualquier excepción, personaje sin vida/ataque tras reaparecer, enemigo inmortal, escena original alterada, mapa sin collider, ataque atravesando paredes o referencias rotas.

Una vez superado el Go, avanzar a dos habilidades y botín, sin volver a reescribir el movimiento.
