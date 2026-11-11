# Auditoría de conexiones, fugas y extensibilidad — Vhaldren

Fecha: 11 de noviembre de 2026. Rama auditada: `feature/004-kaykit-combat-slice`. Esta revisión es **estática**, leyendo código y referencias Git, no un pase de Unity Test Runner ni una prueba de red.

## Contratos actuales

```text
LocalPlayerInput --(PlayerMotionCommand: WASD + facing + dash)--> PlayerMotor
    |                                                     |
    +-- cursor + zoom --> SimpleFollowCamera                +-- CharacterController.Move
    +-- datos de puntería -> LocalPlayerShooter             +-- PlayerDash
                                  |                         +-- PlayerActionModifiers (opcional)
                                  v
                         Raycast: primer obstáculo
                                  |
                         CombatHealth.ApplyDamage
                                  |
                         CombatPracticeDummy (OnGUI + respawn)

Futuras habilidades/estados -> PlayerActionModifiers.AddModifier() -> token
fin, limpieza, dispel     -> PlayerActionModifiers.RemoveModifier(token)
```

El código de locomoción no depende de la cámara, dispositivo, red ni daño; el adaptador local convierte input en `PlayerMotionCommand`. La simulación sigue por `Update` con delta variable, **no** es todavía una simulación segura/predictiva de red.

## Verificaciones comprobables realizadas

1. **Referencias de escena:** se revisaron las cinco referencias `m_Script` de `Movement001.unity` contra los GUIDs de sus `.meta`: `DashFeedback`, `LocalPlayerShooter`, `LocalPlayerInput`, `PlayerMotor`, `SimpleFollowCamera`. Coinciden.
2. **Entrada:** `LocalPlayerInput` crea `InputAction` y dispone de ellas en `OnDestroy`; `OnDisable` borra pulsaciones y deshabilita acciones. No se observó una suscripción estática de por vida.
3. **Efectos:** el material creado por `LocalPlayerShooter` se destruye en `OnDestroy`; ahora además se apaga el trazador en `OnDisable` para evitar efecto residual.
4. **Vida y UI:** `CombatPracticeDummy` suscribe y desuscribe eventos de derrota; el texto de UI se recalcula solo cuando cambia la vida, reduciendo asignaciones en `OnGUI`.
5. **Ciclo del motor:** `PlayerMotor.OnDisable` cancela esquiva y ahora limpia velocidad horizontal/vertical, velocidad medida y estado de suelo, evitando reaparecer con inercia anterior.
6. **Metadatos Unity:** se añadieron `.meta` estables a los nuevos scripts; los archivos KayKit incluidos conservan los bytes originales y su licencia CC0.
7. **Higiene de archivos:** la inspección de nombres del árbol Git no reveló rutas obvias de `.env`, `.pem`, credenciales ni bases de datos. **No es un escaneo de contenido por patrones de secretos ni prueba de ausencia de filtraciones.**
8. **Estructura:** no se cambió `Packages/manifest.json`, no se añadió motor alternativo ni se modificó la escena original. La sala `KayKitCombat001` sigue generándose mediante menú y debe probarse antes de versionarla.

## Extensión para habilidades sin reescribir la base

Se creó `PlayerActionModifiers`, componente **opcional** y neutral por defecto:

- **Movement** bloquea desplazamiento inmediatamente, incluso con aceleración activa; no desactiva gravedad.
- **Dash** impide nuevas esquivas y cancela una esquiva activa si se bloquea.
- **Attack** bloquea el disparo de prueba, pero no la puntería ni el ratón.
- **Facing** puede bloquear la rotación visual, sin rotar el controlador físico.
- **SpeedMultiplier** combina sprint y ralentización sin depender del orden de suscripción; se limita el valor resultante de forma defensiva.
- Cada habilidad/estado recibe un **token único** que debe retirar al terminar. El sistema conserva otros efectos simultáneos: terminar el stun no borra el silencio o slow de otra habilidad.
- `OnDisable` elimina los tokens para no reusar efectos de otro ciclo de vida. Los identificadores no se reinician al desactivar el componente.

Ejemplo de conexión futura de una habilidad (pseudocódigo):

```csharp
// Al iniciar una habilidad que permite apuntar pero no caminar:
int token = modifiers.AddModifier(
    PlayerActionModifiers.LockedAction.Movement,
    speedMultiplier: 1f);

// En FIN de habilidad, cancelación o OnDisable:
modifiers.RemoveModifier(token);
```

**Aún no implementado:** salto, doble salto, planeo, sprint con stamina, dash aéreo, knockback, canales de animación, ataque cargado, combos, cooldowns de habilidades, red ni árbol de habilidades. Este sistema solo ofrece un punto común para conectar esos módulos; no implica que ya funcionen.

### Cómo extender opciones de movimiento

1. **Sprint y ralentización:** usar `SpeedMultiplier` para la velocidad de caminar, sin modificar el estado base; stamina debe vivir en componente independiente.
2. **Dash con distintas variantes:** mantener un único dueño de la esquiva; la habilidad decide permiso y parámetros futuros, sin llamar dos veces a `CharacterController.Move` en el mismo paso.
3. **Salto, impulso, retroceso y empujones:** añadir comandos intencionales y una canalización de movimiento vertical/impulsos dentro del motor. Nunca usar `transform.position` desde la habilidad mientras se simula el controlador físico.
4. **Combos / cast:** el animador y sistema de habilidades adquieren tokens para impedir solo las acciones necesarias. No bloquear apuntado o movimiento de forma indiscriminada.
5. **Cooperativo:** sustituir el adaptador local por uno validado por servidor para avatares remotos, conservando el formato de comando. Se requiere decisión explícita de simulación en ticks, predicción, reconciliación y propiedad de avatar; la arquitectura de `CharacterController` no es equivalente a `PredictionRigidbody` de AnyMMO.

## Riesgos abiertos y prioridades

| Gravedad | Riesgo | Consecuencia | Próxima acción |
|---|---|---|---|
| Alta antes de multijugador | `LocalPlayerShooter` llama a `CombatHealth.ApplyDamage` localmente | Cliente no confiable puede decidir daños si se lleva tal cual a red | Introducir `DamageRequest`/resolución de impactos autoritativa antes de habilitar clientes |
| Alta antes de multijugador | `PlayerMotor.Simulate` se ejecuta en `Update` | Doble simulación o desincronización si además se agrega FishNet sin cambiar dueño del tick | Simulador único y puente de input/red; pruebas con dos clientes |
| Media | `CombatPracticeDummy.OnGUI` por objetivo | CPU y dibujo de UI crecen con centenares de objetivos | Migrar UI a etiquetas agrupadas/pool, no instanciar una ventana por cada enemigo |
| Media | Cámara `SphereCast` con máscara amplia | Puede acercarse por decoraciones o colliders de enemigos | Definir capas apropiadas antes de construir niveles grandes |
| Media | `PlayerActionModifiers` mantiene tokens hasta que los quiten | Un estado olvidado puede dejar al jugador ralentizado o bloqueado durante la sesión | Toda habilidad con token debe retirarlo en final/cancelación/desactivación; añadir pruebas de esos caminos |
| Media | Arte KayKit OBJ/MTL/atlas | Escala, pivotes, materiales y colisiones requieren inspección visual | Importar en Unity y revisar escena generada |
| Media | Estado de pruebas | Las nuevas pruebas no se han ejecutado en Unity | Ejecutar EditMode/PlayMode y registrar fallos reales |
| Baja | No hay sistema de habilidades ni IA completo | El alcance actual es un prototipo de práctica | Diseñar módulos incrementales tras validar el juego base |

## Matriz de pruebas añadidas por esta auditoría

- `PlayerActionModifiersTests`: aislamiento de restricciones, sprint+slow con orden inverso, limpieza al desactivar sin reusar tokens, entradas de velocidad inválidas.
- `PlayerMotorPlayModeTests`: stun cancela movimiento inercial y prohíbe dash, control desbloqueado recupera desplazamiento, silencio impide disparar pero conserva puntería.
- `CombatHealthTests` previas: límites de daño y cura, salud no finita, derrota, max reducido.
- `KayKitImportTests` previas: lectura de cinco modelos OBJ y textura.

**Estado de ejecución:** verificación de referencias/estructura de código **realizada**; compilación, importación y pruebas Unity **pendientes**. No informar “0 fugas”, “tests pasados” ni “multijugador operativo” mientras no exista medición real.

## Orden de validación manual

1. Abrir la rama y ejecutar recompilación en Unity `6000.2.12f1`; revisar Console.
2. Ejecutar todos los tests EditMode/PlayMode (no solo los nuevos).
3. Generar `KayKitCombat001`; ver dianas, vida, disparos, paredes, gravedad y dash.
4. Desactivar/reactivar el jugador; verificar que no conserva inercia ni línea visible.
5. Probar sprint, slow, stun y silencio desde tests antes de construir VFX/UI.
6. Perfilar en Unity Profiler CPU/Memory/GC, especialmente cámara, dianas y raycasts; no se pueden afirmar fugas reales sin ese perfilado.
7. En PR futuro, introducir validación de daños y autoridad de servidor antes de permitir clientes.
