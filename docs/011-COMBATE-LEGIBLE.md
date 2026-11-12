# 011 — Combate legible inspirado en el video de referencia

**Fecha:** 11 de noviembre de 2026. **Rama base:** `feature/009-combate-feedback-vida-dano` (PR #10). **Escena:** `Assets/Scenes/WorldPrototype001.unity`.

## Auditoría inicial

En la rama 009, `PrototypeEnemy` ejecutaba `targetHealth.ApplyDamage` el mismo frame en el que detectaba un objetivo a distancia y terminaba su cooldown. No había aviso que permitiera esquivar por habilidad. Las cifras de daño estaban implementadas en 009, pero `CombatHealth.Heal` no exponía recuperación real para números verdes. El modelo enemigo podía recibir daño sin ninguna reacción propia. Además, `HasLineOfSight` usaba un buffer fijo de 16 hits que podía omitir obstáculos cuando se saturaba.

## Cambios implementados

1. **Aviso antes del ataque.** `EnemyAttackTelegraph` crea un anillo visible con `LineRenderer`, sin collider. Los guardianes preparan el golpe ~0,48 s, y el jefe >=0,75 s. El radio visual usa `attackRange`; el enemigo no se mueve durante la preparación. El color se intensifica y al ejecutar el golpe destella brevemente. El componente se crea al cargar la escena existente: no hay que regenerar el mapa.
2. **Daño al finalizar el aviso, nunca al iniciarlo.** El enemigo comprueba nuevamente distancia, vida del objetivo, limites de su territorio y obstáculos justo antes de golpear. Si te alejas o te tapas detrás de una pared, cancela sin dañar. El cooldown se consume desde que empieza la preparación.
3. **Esquiva defensiva real.** Si `PlayerMotor.IsDashing` o `DashMovedThisStep` es verdadero justo al resolver el golpe, no se aplica daño. No se modificó el código de movimiento ni se bloquean entradas. La invulnerabilidad se limita a la esquiva que ya existe.
4. **Interrupción sin teletransporte.** Recibir daño cancela el ataque de un guardián corriente y deja un breve recovery. Un jefe no queda permanentemente inmovilizado por disparos normales. `PrototypeEnemy.InterruptAttack(float)` permite a futuras habilidades cancelar la preparación sin desplazar al enemigo.
5. **Reacción visual al golpe.** `EnemyHitReaction` comprime y restaura brevemente la escala **solo de `modelRoot`**, sin alterar colisión, posición o rotación del objeto raíz.
6. **Curación visible.** `CombatHealth.Healed` se emite únicamente con la cantidad realmente recuperada; `CombatDamageFeedback` dibuja números verdes `+35`, sin inventar overheal cuando ya estás al máximo.
7. **Seguridad de visión.** Si el buffer fijo de raycasts se llena, se reintenta de forma exhaustiva con `Physics.RaycastAll`; un obstáculo no se debe ignorar por capacidad.
8. **Pruebas:** se ampliaron las pruebas PlayMode de `PrototypeWorldPlayModeTests` para aviso previo, salida del radio y cancelación al golpe; se adaptó la prueba anterior de paredes al nuevo windup. `CombatDamageFeedbackPlayModeTests` cubre curación real y reacción del modelo sin mover la raíz.

## Prueba manual rápida (Unity 6000.2.12f1)

1. Abrir la rama 011 en Unity y cargar directamente `Assets/Scenes/WorldPrototype001.unity` (NO ejecutar el generador: el mundo de Claude ya está versionado).
2. Pulsar Play y acercarte a un guardián: primero **anillo anaranjado**, luego daño si todavía estás cerca.
3. Salir de su radio o cruzar detrás de una pared durante el aviso: debe fallar el ataque.
4. Esquivar con Espacio justo durante el impacto: no debe bajar la vida; no debe haber cambios extraños de movimiento.
5. Dispararle durante la carga: debe cancelar el golpe del guardián y el modelo debe reaccionar brevemente. El jefe debe resistir interrupciones ligeras.
6. Curarte con Q después de perder vida: aparece **+35 verde** (o los PV efectivamente curados si faltan menos).
7. Probar un combate con 3 enemigos, morir y reaparecer, y repetir con el jefe. Comprobar que los anillos no persisten tras la muerte.
8. Ejecutar **EditMode y PlayMode completos**; controlar Console y Profiler (GC, LineRenderers, draw calls, hasta seis enemigos).

## Estado de validación

- Se escribieron cambios y tests y se revisó su estructura por lectura de código y Git.
- **NO se ha ejecutado Unity ni compilado la rama 011 desde esta sesión.** No afirmar resultados de tests nuevos sin correrlos.
- La rama es independiente para no sobrescribir el trabajo sin commit de otros agentes. Si otro agente ya editó `PrototypeEnemy.cs` en local con hechizos/IA, fusionar por comportamiento y resolver conflictos antes de usar ambas novedades.
- Sistema local. El cooperativo todavía requiere autoridad del servidor, sincronización de estados de ataque y validación de impactos.

## Pendientes posteriores, sin bloquear esta entrega

- Animaciones de anticipación y golpe sobre esqueletos/boss reales (el anillo y el tiempo ya estarán disponibles).
- Navegación alrededor de árboles y pilares.
- Transparencia de vegetación que oculte al jugador, cámara adaptativa y efectos de sonido.
- Sistema de postura y resistencias por tipo de enemigo, no stun ilimitado.
- Integración y pruebas con los hechizos 010 y los recursos/construcción aún locales.
