# Auditoría profunda de Vhaldren: jugabilidad, IA, combate y UX

Fecha: 11 de noviembre de 2026  
Rama inspeccionada: `audit/claude-008`  
Alcance: escena de mundo 008 y scripts locales de jugador, enemigos, combate y sesión. Es una lectura estática del checkout actual, más la compilación que hizo el Editor de Unity. No es un playtest ni un perfil de rendimiento.

## Dictamen

Vhaldren ya tiene una base técnica útil para una **prueba vertical local**: entrada separada del motor, movimiento inmediato configurable, esquiva con buffer, disparo hitscan, salud, una sesión de seis enemigos y un ciclo sencillo de muerte/reaparición. La rama también tiene ahora percepción por visión y ruido, memoria de posición y un aviso de ataque.

Eso todavía no equivale a un juego de acción completo. El bucle termina al limpiar un encuentro; no hay objetivo con recompensa, progresión, habilidades funcionales, inventario, guardado, opciones accesibles ni cooperativo. La IA puede perseguir e investigar, pero no navegar un nivel, flanquear ni coordinar roles. El combate es un disparo fijo de prueba, no un conjunto de armas con identidad. El mapa de 92 × 92 m con seis enemigos no demuestra que el diseño soporte un mundo gigante.

La compilación del proyecto aparece como satisfactoria en `Editor.log` después de modificar los scripts, sin errores C# registrados. No se ejecutó el Test Runner ni un recorrido jugable en esta revisión; las pruebas nuevas y existentes quedan pendientes de ejecución real.

## Cableado actual entre sistemas

```text
LocalPlayerInput
  ├─ WASD + cursor + esquiva → PlayerMotionCommand → PlayerMotor → CharacterController
  ├─ clic → LocalPlayerShooter → raycast → CombatHealth
  └─ zoom → SimpleFollowCamera

LocalPlayerShooter ── daño ──→ PrototypeEnemy.HandleDamaged
                └── ruido ──→ enemigos cercanos → investigar origen
PrototypeEnemy ── ataque ──→ CombatHealth del jugador
PrototypeGameSession ── HUD, cura Q, derrota y reaparición
```

La sinergia que sí existe es concreta: disparar alerta a enemigos cercanos; herir a un enemigo le hace recordar el punto de impacto y buscar al jugador. `PlayerActionModifiers` deja una extensión para bloquear acciones o variar velocidad, pero no hay todavía habilidades que la usen en una secuencia jugable.

## 1. Jugabilidad: core loop, respuesta y flujo

### Qué ya funciona en el código

- `LocalPlayerInput` traduce WASD relativo a cámara en una intención `PlayerMotionCommand`; `PlayerMotor` no consulta dispositivos ni cámara. Los eventos del Input System pasan por `KeyboardMovementBuffer`, que retiene pulsaciones rápidas hasta que el motor las consume. La entrada de movimiento está escrita para que un tap corto no desaparezca entre lecturas.
- `PlayerMotor` usa `CharacterController.Move`, permite apuntar mientras se camina en otra dirección y ofrece dos perfiles: movimiento horizontal inmediato, activado por defecto, o aceleración/frenado/inversión configurables. Velocidad base: 6 m/s. La simulación actual ocurre desde `Update` con `Time.deltaTime` variable.
- `PlayerDash` guarda brevemente la solicitud, calcula el tiempo efectivo de esquiva dentro de cada paso y no depende de una cantidad fija de frames. La configuración base es 4,5 m en 0,18 s, con 1,2 s de espera y 0,15 s de buffer. El motor separa movimiento de orientación visual.
- El bucle mínimo actual es: explorar el mapa → ver o atraer guardianes → esquivar/disparar → matar → curarse con Q o morir → reaparecer en el punto inicial. El generador crea un mundo de 92 × 92 m, cinco guardianes menores y un jefe.

### Lo que limita la sensación y el flujo

- Hay controles reactivos en el código, pero “milimétrico” exige validación humana: latencia percibida, lectura de animaciones, colisiones y comodidad no se deducen del hecho de compilar. Aún no hay una pasada de playtest de esta rama.
- La tecla, el mouse y la esquiva están conectados directamente a teclas/clics concretos. No existe reasignación, perfil de mando ni una capa de acciones configurable por jugador.
- No hay salto ni habilidades reales. La cura Q y el disparo son los únicos recursos activos; la cura añade una decisión básica con espera de 12 s, pero no intercambia recursos ni se conecta a estados elementales, terreno o builds.
- El disparo crea una relación de riesgo/recompensa: el ruido de prueba tiene radio de 30 m. No hay sigilo completo, ruido de pasos, ocultación por material, ni una regla para amortiguar el sonido detrás de paredes.
- Los seis encuentros son fijos. No hay escalado por habilidad, fases que se desbloqueen tras dominar una mecánica, presión temporal, objetivo con recompensa ni ajuste dinámico. El jefe comparte el mismo patrón de cerebro y solo cambia números/aviso.

### Evaluación

**Cimiento sólido para el control; flujo y progresión aún ausentes.** No conviene empezar con un sistema de dificultad adaptativa antes de tener un encuentro de 10–15 minutos, una recompensa clara y datos de una prueba real. Primero deben definirse tres momentos de combate con una sola nueva regla por momento, y validar que cada uno enseña una decisión diferente.

## 2. IA de enemigos: percepción, legibilidad y táctica

### Percepción y memoria presentes

`PrototypeEnemy` tiene una máquina de estados explícita: `Idle`, `Pursuing`, `Investigating`, `AttackWindup`, `AttackRecovery`, `ReturningHome`, `LeashReturn` y `Defeated`.

- Detección visual: cono configurable (140° por defecto), radio de 15 m en guardianes y 19 m para el jefe, más una prueba de línea de visión con raycast. La posición del jugador no se lee para perseguirlo si no es visible.
- Memoria: al verlo, recibir daño o oír un ruido, guarda una posición y una caducidad de 6 s. Si pierde visión, va a ese punto; al agotarse la memoria, vuelve a casa. El ruido de disparo se anuncia con `PrototypeEnemy.EmitNoise(origen, radio)` y el listener guarda el origen.
- Territorio: hay un límite de leash de 28 m. Al cruzarlo, el enemigo prioriza volver a su origen y limpia la memoria cuando llega.
- Aviso y oportunidad de esquiva: los ataques menores usan 0,5 s de windup y 0,45 s de recuperación; el jefe usa 0,9 s y 0,7 s. El modelo cambia de color durante el aviso. La dirección se fija al comenzar el windup y el golpe solo alcanza un arco configurable (100° por defecto); al resolver se vuelve a comprobar visión, vida, alcance y arco. Una esquiva lateral que salga del arco puede evitar el golpe aunque siga dentro del alcance radial.

Ejemplo del flujo de aviso en la implementación:

```csharp
attackTimer = windup;
state = BrainState.AttackWindup;
SetAttackTelegraphVisual(true);
```

El daño se aplica después del windup, no al empezar la preparación.

### Qué falta para que sea justa y legible

- El color es el único telegraph del enemigo generado hoy: no hay animación, pose, audio, área de impacto dibujada ni feedback de golpe. Con cápsulas estáticas la señal puede ser difícil de leer en movimiento.
- El telegraph sigue siendo solo un cambio de color: no tiene animación, pose, audio ni sector dibujado para enseñar los 100° del arco. El cálculo permite esquivar lateralmente, pero hay que comprobar visualmente que el arco se lea y que la distancia de esquiva sea suficiente.
- El alcance de escucha es radio geométrico: no hay oclusión, absorción por material, pérdida gradual ni umbral de intensidad. Un ruido propagado alerta a todos los enemigos activos dentro del radio.
- La navegación es directa: el enemigo se mueve hacia el objetivo o hacia el último punto conocido usando su `CharacterController`. No hay NavMesh/A*, waypoints ni rodeo de obstáculos; árboles, ruinas y otros enemigos pueden bloquearlo. No hay separación de grupo.
- No se implementan flanqueo, cobertura, supresión, roles, turnos de ataque ni coordinación. Los enemigos pueden converger en la misma posición y atacar con el mismo patrón.
- `Investigating` va al punto y espera que caduque la memoria; no busca alrededor con una ruta o abanico. Cuando varios puntos de ruido se emiten, la memoria puede cambiar de forma inmediata; todavía no hay selección de prioridad o confianza.

### Evaluación

**Percepción básica más justa que perseguir a través de paredes; tácticas y navegación todavía de prototipo.** La mejora de visión/memoria/aviso está instalada en el código original de Vhaldren, inspirada como patrón conceptual en máquinas de estado y memoria de objetivo. No se trasladó código de Project Zomboid. Para producción, conviene separar `Perception`, `Navigation`, `Decision` y `Attack` en componentes/módulos probables y testeables; no añadir coordinación antes de poder atravesar el escenario sin atascarse.

## 3. Armas y combate: impacto e identidad

### Implementación actual

- `LocalPlayerShooter` proyecta el cursor al mundo, calcula el punto visible más cercano, excluye los colliders del propio jugador y vuelve a lanzar el rayo desde el origen del arma. La geometría intermedia bloquea el disparo. Alcance configurado: 100 m.
- Es un hitscan determinista: cuando el rayo alcanza un objeto con `CombatHealth`, se aplican 25 puntos de daño de prueba. No existe dispersión ni una probabilidad de acierto: acertar depende del rayo/collider y del jugador.
- `CombatHealth` limita daño/cura a la vida restante y emite eventos `Damaged` y `Defeated`. La vida inicial del jugador del generador es 150 PV; los guardianes tienen 55, 75 o 90 PV, y el jefe 320 PV con 18 puntos de daño por golpe y velocidad de 2,5 m/s.
- El proyectil visual es un `LineRenderer` breve (0,2 s). El disparo también alerta enemigos en un radio configurable. El ataque enemigo aplica daño después de su preparación.

Fragmento real de la integración actual:

```csharp
PrototypeEnemy.EmitNoise(origin, shotNoiseRadius);
// ...se resuelve el primer impacto...
health.ApplyDamage(safeDamage);
```

### Lo que aún no transmite peso

- No hay armas diferenciadas: un solo disparo de prueba, sin cadencia, munición, recarga, retroceso, caída, alcance efectivo, daño por distancia, penetración, tipo de daño, estados o animación de recarga.
- No hay efecto de impacto en la superficie, sangre/partículas, sonido, vibración o shake de cámara. El trazador confirma visualmente la trayectoria, pero no confirma contundencia ni material alcanzado.
- No hay knockback. Los props son decoración/colisión; los impactos no rompen coberturas ni dejan marcas. El sistema de daño solo afecta a objetos que tengan `CombatHealth`.
- El enemigo no tiene hit reaction animada; recibe puntos de vida y, si no muere, recuerda dónde está el jugador. No hay stagger o interrupción que cambie el intercambio de golpes.
- El disparo y el daño son locales y no autoritativos. Se deben tratar como código de prototipo mientras no haya servidor; un cliente podría declarar impactos y daños si se usara tal cual en multijugador.

### Evaluación

**Confirmación básica del impacto; sin “game feel” de arma final ni identidad.** Para el siguiente vertical slice, escoger dos herramientas con decisiones distintas (por ejemplo, golpe de área lento que interrumpe y proyectil rápido que atrae más enemigos). Cada una necesita telegraph/animación, tiempo de recuperación, regla de impacto y feedback diferenciado; añadir más armas de daño numérico igual solo aumenta contenido sin cambiar el juego.

## 4. UX y calidad de vida

### Lo que el prototipo ofrece

- HUD `OnGUI` con región aproximada, vida, guardianes derrotados, estado del jefe, controles y temporizador de la cura. Hay mensajes para derrota/reaparición y victoria del encuentro.
- Cura con Q: +35 PV si falta vida, con espera de 12 s. Al morir, el jugador queda desactivado y reaparece 3 s después en la posición que `PrototypeGameSession` guardó al activarse.
- Los guardianes reutilizan su mismo objeto al reaparecer; el modelo se apaga y se vuelve a activar, sin `Instantiate/Destroy` por ciclo de muerte.

### Faltantes

- No se encuentra un sistema de guardado/carga, versión de datos, guardado atómico ni recuperación de partida. Reiniciar la aplicación no preserva progreso.
- La reaparición es fija; no hay checkpoints antes del jefe ni protección del progreso. El HUD informa muertes, pero no existe flujo de pausa, ajustes, inventario, crafting o asignación de puntos.
- `OnGUI` es una interfaz de prueba, no una HUD final adaptable a distintas resoluciones, mando, safe areas, escalado de texto o accesibilidad.
- No hay reasignación de teclas, mando configurado, sensibilidad ajustable, opciones de contraste/daltonismo, tamaño de texto, señales visuales redundantes para audio ni configuración de cámara/campo visual.
- La información de combate no está integrada en el mundo o las animaciones. El estado del dash sí tiene un indicador visual; los estados de vida del enemigo y sus golpes no tienen presentación final.

### Evaluación

**Suficiente para probar el loop de desarrollo; no listo para jugadores finales.** El próximo mínimo útil es una UI de juego estable y pausa/opciones básicas; después, guardado de personaje/progreso con datos versionados y escritura temporal + reemplazo atómico. Los checkpoints deben probarse junto con muerte y recuperación antes de introducir contenido que tarde mucho en completarse.

## 5. Rendimiento, escala y riesgo técnico

### Evidencia presente

- El escenario generado tiene 92 × 92 m y solo seis controladores enemigos. Sus árboles y límites crean objetos/colliders de Unity convencionales; no hay chunks ni streaming del mundo.
- Cada enemigo activo tiene un `Update` propio y hace percepción cada frame. Si un objetivo está dentro de rango y cono, consulta línea de visión. El evento de ruido recorre el conjunto global de enemigos activos para descartar los que queden fuera del radio: no usa una cuadrícula espacial.
- La detección reutiliza un array estático con `Physics.RaycastNonAlloc` de 16 impactos. Si se llena, recurre a `RaycastAll`, que asigna memoria. El disparo reutiliza un buffer que crece si se satura hasta 256 y después usa `RaycastAll` como salvaguarda.
- El aviso usa `MaterialPropertyBlock`, evitando crear una copia de material por enemigo. La reaparición reactiva el mismo actor.
- No se encontró Object Pool genérico, gestión por celdas, culling de IA, nivel de detalle, streaming, Addressables, sistema de navegación ni controlador de GC explícito. No hay pruebas de que miles de enemigos sean viables; tampoco hay perfil del CPU, GC o memoria.

### Qué significa al escalar

El coste actual crece al menos con el número de enemigos actualizados por frame: `N` cerebros hacen percepción y una cantidad variable de raycasts. Cada ruido añade un barrido `O(N)`. Con seis se puede aceptar; con cientos o miles la frecuencia, el coste de física y las llamadas de Unity necesitan medición y diseño de escala. El tamaño del mapa actual tampoco es “gigante”: todo se mantiene cargado en una escena.

La ruta de escala recomendada, antes de añadir miles de actores:

1. Medir la escena con 6, 50, 150 y 300 enemigos en hardware objetivo: CPU por IA/física, GC Alloc/frame, memoria residente, draw calls y tiempo de carga. No prometer 1000 hasta obtener esos datos.
2. Añadir una cuadrícula espacial por celdas para ruido y proximidad; consultar solo vecinos pertinentes en lugar de iterar toda la población.
3. Escalonar pensamiento: enemigos cercanos en ticks frecuentes, enemigos perceptibles fuera de combate a menor frecuencia y agentes lejanos como simulación agregada/virtual. Mantener un único dueño de cada actualización.
4. Usar pathfinding navegable y separación local; el pooling solo es necesario para actores/FX que realmente se creen y destruyan en masa. La reaparición actual ya reutiliza el mismo actor, pero eso no reemplaza un pool de proyectiles o efectos.
5. Dividir el mundo en chunks/celdas de carga, controlar colliders y contenido por distancia, e introducir LOD/culling con perfiles de memoria. Evitar resolver con llamadas forzadas a GC: prevenir asignaciones y medir pausas.
6. Repetir profiling después de cada sistema. Los raycasts no asignan en el camino normal mientras no saturen los buffers, pero las rutas de fallback sí pueden asignar; esto requiere validar en el peor escenario.

## Cambios realizados en esta pasada

- `PrototypeEnemy` ahora modela estados explícitos, visión por cono + línea de visión, memoria temporal, investigación por daño/ruido, regreso al territorio y ataque con aviso/revalidación. La dirección del golpe queda bloqueada al empezar el windup y se valida un arco de ataque.
- `LocalPlayerShooter` emite ruido al disparar para conectar combate y percepción.
- El generador orienta a los enemigos hacia el jugador al crear el encuentro y asigna duraciones distintas al aviso/recuperación del jefe.
- Se agregaron pruebas PlayMode para memoria/ruido y una esquiva lateral dentro del alcance radial pero fuera del arco; también se adaptó la prueba anterior de persecución y línea de visión a la ventana de windup.
- Verificación disponible: `git diff --check` sin errores y compilación de Unity registrada como satisfactoria, cero errores C# en el `Editor.log` leído. **Pendiente:** correr EditMode/PlayMode y jugar el encuentro; el Test Runner no pudo abrirse desde la automatización de escritorio y no se usó una segunda instancia batch porque Unity ya tenía este proyecto abierto.

## Orden de trabajo recomendado para la slice de tres días

1. Ejecutar EditMode/PlayMode y probar una partida de principio a fin; corregir lo que bloquee movimiento, muerte, daño, respawn o IA. No contar la prueba como lista hasta ese pase.
2. Probar el arco de ataque en Unity y añadir ruta de navegación simple; comprobar esquivas y enemigos tras obstáculos.
3. Cerrar un loop pequeño con dos ataques/armas distintos, efectos y sonido básicos, un objetivo y una recompensa persistente.
4. Reemplazar el HUD de desarrollo por una UI legible con pausa, opciones y controles; crear guardado versionado y checkpoint antes del jefe.
5. Hacer build PC y repetir profiling con el tamaño real de la escena. Dejar el trabajo de mundo enorme y población masiva para cuando la slice mida bien.

En tres días el objetivo realista es una **slice completa, acotada y comprobada**, no un mundo masivo con IA/UX de producción. La prioridad es que el jugador termine una sesión divertida y recuperable; después se aumenta el contenido y se optimiza según mediciones.

## Fuentes de código revisadas

- `Assets/Scripts/Player/LocalPlayerInput.cs`, `PlayerMotor.cs`, `PlayerDash.cs`, `PlayerHorizontalMotion.cs` y `PlayerActionModifiers.cs`.
- `Assets/Scripts/Player/LocalPlayerShooter.cs` y `Assets/Scripts/Combat/CombatHealth.cs`.
- `Assets/Scripts/World/PrototypeEnemy.cs` y `PrototypeGameSession.cs`.
- `Assets/Editor/WorldPrototypeSceneTool.cs` y `Assets/Tests/PlayMode/PrototypeWorldPlayModeTests.cs`.
- `README.md`, `docs/ESTADO.md` y `docs/008-MUNDO-JUGABLE-3-DIAS.md` para el alcance esperado de la rama.
