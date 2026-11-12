# Vhaldren — estado del proyecto

Actualizado: 10 de noviembre de 2026. Este archivo es la referencia única de dónde está el proyecto; el detalle de cada cambio está en el historial de Git.

## Qué queremos construir

Un juego de acción de fantasía oscura con cámara elevada, movimiento con WASD y apuntado con el mouse, del género de V Rising pero con identidad, mundo y mecánicas propias. La referencia es de género y de sensación de control: no se copian código, arte, nombres ni personajes.

Prioridades del dueño del juego:

- Primero una base de control que se sienta bien; después habilidades, animación y el resto.
- WASD responde siempre, también con pulsaciones muy rápidas.
- El personaje puede mirar a cualquier lado mientras camina hacia otro.
- Los disparos van exactamente al punto bajo el cursor.
- El dueño no programa: el agente decide lo técnico, lo comprueba y lo publica.
- **Es para jugar con hermanos y primos: el juego necesita multijugador cooperativo.**
- Avanzar rápido reutilizando lo que ya existe (arte y librerías con licencia que lo permita) en vez de crear todo desde cero.

## Qué hay hoy

- Movimiento con WASD relativo a la cámara, inmediato por defecto (aceleración opcional).
- Esquiva con Espacio: 4,5 m en 0,18 s, con 1,2 s de espera, estela y un indicador a los pies que se apaga durante la espera.
- Zoom de cámara con la rueda del mouse.
- El modelo gira hacia el cursor sin girar la cápsula física.
- Disparo de prueba (hitscan) al punto bajo el cursor, con trazo. En el mundo 008 aplica daño de prueba.
- Mundo 008: orbe arcano (clic derecho), estallido de área (E), cura (Q) y botín de esencia (ver sección de habilidades; sin probar).
- Cámara elevada que sigue al personaje con ángulo fijo.
- Escena `Movement001`: circuito con pared recta, pared diagonal, pilares, hueco estrecho, escalón y rampa, sobre un suelo cuadriculado de casillas de 2 m.
- Personaje provisional hecho con formas básicas de Unity.
- WASD usa acciones del Input System que conservan el orden de pulsar y soltar dentro de un mismo frame; `Active Input Handling` queda en Both para mantener la entrada heredada mientras se migra.

## Auditoría incremental: correcciones aisladas (rama `audit/correcciones-control-camara-2026-11-10`)

Esta rama parte de `feature/002-esquiva-zoom`. No reemplaza la arquitectura, no modifica `main` y no cambia velocidad, duracion o cooldown del movimiento.

- **Disparos y apuntado:** el buffer de 32 impactos de `RaycastNonAlloc` podia truncar una consulta no ordenada y descartar el objeto mas cercano. Ahora crece de 32 a un maximo de 256 resultados, y usa `RaycastAll` solo si tambien se satura ese limite. Se conserva la exclusion del propio jugador.
- **Camara:** proteccion configurable por `SphereCast` para acercarla cuando una pared bloquea la vista; restaura la distancia al despejarse, sin variar el angulo nominal. Campos de Inspector: `avoidObstacles`, `obstructionRadius`, `obstructionPadding` y `obstructionLayers`.
- **Esquiva:** `PlayerMotor.DashMovedThisStep` permite mantener la estela durante el paso final, incluso cuando `IsDashing` ya es falso al acabar la simulacion.
- **Regresiones agregadas:** PlayMode valida saturacion de raycasts, distancia y angulo de camara frente a paredes, exclusion de la propia capsula como obstaculo, y ultima actualizacion de la estela de esquiva.
- **Entrada de esquiva:** `LocalPlayerInput` ya no depende solamente de `spaceKey.wasPressedThisFrame`; `InputAction` guarda la pulsacion de Espacio (aunque se suelte antes de Update) y el motor la consume una sola vez. Se agregaron pruebas para taps breves y perdida de foco. Si Espacio no funciona en Unity, verificar foco en Game, rama activa y consola antes de dar por resuelto el reporte.
- **Verificacion:** los cambios de esta rama se revisaron estaticamente, pero **NO se han ejecutado ni compilado en Unity desde esta auditoria**. Las cifras de pruebas aprobadas mas abajo corresponden al estado previo, no a esta rama.
- **Pendiente:** ejecutar EditMode/PlayMode y la lista de sensacion manual en Unity `6000.2.12f1`, y revisar posibles ajustes visuales de `obstructionLayers` en escenarios reales.
- **Fuera de alcance deliberadamente:** introducir multiplayer, autoridad de servidor, habilidades o fusionar ramas. Esas tareas necesitan validaciones separadas y podrian alterar el prototipo de manera sustancial.

## Auditoría del 10 de noviembre de 2026

Defectos encontrados y corregidos. Cada uno tiene una prueba que fallaba antes del arreglo.

| # | Defecto | Efecto para quien juega | Arreglo |
|---|---|---|---|
| 1 | Las teclas opuestas se anulaban (A + D = 0) | Al alternar rápido, los dedos se solapan unos milisegundos y el personaje se quedaba quieto con teclas pulsadas | El control principal prioriza el último evento de tecla; la ruta heredada conserva `OpposingKeysAxis` |
| 2 | El `CharacterController` descartaba pasos menores de 1 mm y el motor copiaba esa velocidad nula | Con aceleración y más de ~200 FPS el personaje no arrancaba nunca: 0 m en 300 pasos | `minMoveDistance = 0` y la velocidad medida solo se usa al chocar de lado |
| 3 | El disparo ignoraba el propio cuerpo dejando de buscar | Al apuntar hacia atrás mientras el modelo giraba, el tiro atravesaba la pared de detrás | Se busca el primer impacto que no sea el jugador |
| 4 | El cursor encima del personaje invertía la orientación con un pixel de movimiento | El modelo giraba de golpe al pasar el mouse sobre él | Zona muerta de 0,35 m alrededor del personaje |
| 5 | El proyecto solo tenía activo Input Manager, aunque el código incluía una ruta para Input System | La ruta probada no conservaba el orden de varios taps A/D que ocurren entre lecturas; el personaje podía quedarse quieto | Se añadió `com.unity.inputsystem` 1.18.0, se activó Both y se conectaron eventos `performed`/`canceled` a un buffer que conserva un tap hasta una lectura |
| 6 | Las pruebas no sincronizaban la física antes de simular | Las pruebas de toque corto y de puntería fallaban o pasaban según la duración del frame (el "8/8" anterior no era estable: aquí dio 7/8 y 6/8) | Las pruebas sincronizan los colliders al crear la escena |
| 7 | Suelo brillante y liso, pared sin material, sin sombras, capa roja sobre suelo rojo | El suelo se veía rosado casi blanco, el movimiento no se percibía y el personaje se perdía | Materiales mate, cuadrícula, sombras, capa oscura, marcador de puntería luminoso |
| 8 | La escena no estaba en Build Settings | Una build habría salido vacía | El generador la registra |
| 9 | El validador exigía movimiento inmediato | Probar la aceleración rompía la validación | El validador ya no impone una decisión de sensación |
| 10 | Las pruebas alternaban teclas sostenidas, pero no pulsar y soltar A/D varias veces dentro del mismo frame | La corrección anterior no demostraba que el último tap llegara al motor | Se agregó una prueba PlayMode con A down/up y D down/up antes de una sola lectura; verifica que D gana y el tap se consume una vez |
| 11 | El zoom dividía por 120 el delta del Input System | Con la normalización multiplataforma predeterminada de Unity, la rueda apenas acercaba la cámara | Se usa el delta normalizado; si Windows se configura para conservar unidades nativas, se convierte 120 a un paso. Dos pruebas cubren ambos modos |

Nota sobre el defecto 2: el movimiento inmediato que se activó antes lo tapaba, no lo arreglaba. Ahora los dos modos funcionan a cualquier número de FPS.

## Auditoría profunda del 11 de noviembre de 2026

En `audit/claude-008`, el enemigo de prototipo se amplió con cono de visión, comprobación de obstáculos, memoria temporal de la última posición vista/escuchada, investigación, leash, y windup con dirección/arco fijos y revalidación de alcance y línea de visión. El disparo de prueba emite ruido; el generador orienta a los enemigos hacia el punto inicial del jugador y al jefe le da una ventana de aviso más larga.

La revisión de core loop, percepción/tácticas, armas, UX, guardado y escala está en [009-AUDITORIA-CORE-AI-COMBATE-UX.md](009-AUDITORIA-CORE-AI-COMBATE-UX.md). La escena sigue siendo una prueba local de 92 × 92 m con seis enemigos; no se ha demostrado el comportamiento a gran escala ni existe guardado, progresión, navegación táctica o red.

**Verificación de esta pasada:** Unity registró compilación satisfactoria después de los cambios y no aparecen errores C# en el log inspeccionado. EditMode, PlayMode, playtest y profiling quedan pendientes; no marcar la IA o el combate como validados hasta ejecutar esos pasos.

## Habilidades, botín y HUD de la slice 008 (11 de noviembre de 2026, sin probar)

Añadido en `audit/claude-008` **sin compilar, sin ejecutar pruebas y sin Play Mode** (el dueño lo probará). Todo es local y no autoritativo.

- **Orbe arcano — clic derecho** (`PlayerSpellCaster` + `ArcaneProjectile`): proyectil real que viaja a 21 m/s hasta 32 m, con esfera brillante, halo pulsante, luz y estela (`TrailRenderer`). La colisión es un `SphereCast` por frame con el **mismo radio que la esfera visible** (0,22 m) y el cuerpo se coloca en el punto de contacto antes de aplicar el daño (34). Al impactar: destello que se expande, esquirlas y luz; más grande y con onda si hiere. Espera 0,6 s. Sale del `ShotOrigin` y, si el cursor está en el suelo, vuela horizontal hacia ese punto. Ruido bajo al lanzar (8 m) y mayor en el impacto (14 m): sirve de señuelo.
- **Estallido de sangre — E**: aviso en el suelo (anillo que parpadea cada vez más rápido + disco que se llena) durante 0,6 s, a un máximo de 10 m del jugador y sin atravesar paredes. Detona con radio 4 m: 30 de daño a cada enemigo cuyo collider quede dentro del anillo y con línea despejada desde el centro, y los aturde 0,9 s (cancela un ataque en preparación). Espera 8 s, ruido 22 m. El jugador puede moverse y esquivar mientras carga; si muere, no detona.
- **IA**: `PrototypeEnemy` gana `ReceiveAttackOrigin` (el enemigo herido investiga el punto de lanzamiento del orbe, o el centro del estallido si no ve al jugador; no lee su posición actual a través de paredes), `Stagger` (usa su propia máquina de estados, no lo mueve) y `EmitNoise(origen, radio, escalaOcluida)`: tras una pared el ruido de los hechizos solo llega al 45 % del radio. El disparo hitscan conserva el comportamiento anterior.
- **Botín** (`PrototypeLootOrb`): cada enemigo derrotado suelta una esencia flotante (el jefe, una mayor de 5). Se recoge al pasar cerca: +8 PV por esencia y −1,5 s de espera del estallido por esencia. Caduca a los 45 s, parpadeando los últimos 6.
- **HUD**: nueva línea con el estado de ambas habilidades, barras de espera y contador de esencia; aviso al recoger botín.
- El generador añade `PlayerSpellCaster` al jugador y el validador lo exige; si una escena ya generada no lo tiene, `PrototypeGameSession` lo añade al entrar en Play.
- Efectos con formas nativas de Unity y `Sprites/Default` compartido (`PrototypeVfx`), sin paquetes nuevos.

Pendiente de probar en Unity: compilación, EditMode/PlayMode existentes, y la lista manual: el orbe se ve viajar y explota justo donde toca; no atraviesa paredes ni se dispara contra el propio jugador; el estallido golpea a varios enemigos dentro del anillo y a ninguno tras un muro; el aturdimiento corta el ataque naranja; enemigos tras una pared investigan el impacto/centro y no aparecen directamente sobre el jugador; el botín se recoge y reduce la espera; los efectos no salen rosas (shader). No hay pruebas automáticas nuevas para estas mecánicas.

## Qué está comprobado

**Automático** (Unity 6000.2.12f1, 10 de noviembre de 2026):

- EditMode: 45 de 45 pruebas.
- PlayMode: 26 de 26 pruebas. Las pruebas nuevas procesan cuatro eventos A/D en una actualización y verifican los dos modos de delta de rueda; la prueba de taps confirma que la última dirección llega al motor, mueve al jugador y se consume una vez. Otras siete recorren la escena real `Movement001`: reposo en el suelo, choque con la pared, paso por el hueco, deslizamiento en la diagonal, subida del escalón, subida y caída de la rampa, y esquiva contra la pared.
- La esquiva recorre 4,5 m tanto a 30 como a 240 FPS.
- El generador crea la escena desde cero y el validador la aprueba.
- Foto de la cámara revisada: suelo rojo cuadriculado, obstáculos grises con sombra, personaje oscuro legible.

**Falta comprobar jugando** (nadie lo ha jugado todavía después de estos cambios):

- [ ] En la vista Game, alternar A/D y W/S muy rápido, incluyendo taps breves: el personaje no debe quedarse quieto.
- [ ] Diagonales y cambios de dirección: que se sientan inmediatos.
- [ ] Caminar hacia atrás y de lado mirando al cursor.
- [ ] Pasar el cursor por encima del personaje: no debe girar de golpe.
- [ ] Disparar con una pared en medio: el trazo debe parar en la pared.
- [ ] Recorrer el circuito: diagonal, pilares, hueco, escalón y rampa.
- [ ] Tamaño del personaje en pantalla y ángulo de la cámara: ¿cómodos?
- [ ] Esquiva con Espacio: ¿la distancia, la rapidez y la espera de 1,2 s se sienten bien?
- [ ] Zoom con la rueda: ¿los límites cerca/lejos son cómodos?
- [ ] Opinión: ¿movimiento inmediato o con aceleración? (casilla **Instant Horizontal Movement** de `PlayerMotor`).

## Límites conocidos

- Con el movimiento inmediato activado, los valores de aceleración, frenado e inversión no tienen efecto.
- El punto de puntería es el punto del mundo bajo el cursor. Sobre un obstáculo alto, el personaje apunta a la superficie del obstáculo, no al suelo de detrás.
- El motor se simula en `Update`, con paso variable. Es correcto para un jugador local; el juego en red necesitará un paso fijo.
- El origen del personaje está en el centro de la cápsula. Los modelos animados lo traen en los pies: habrá que ajustar el hijo visual al importarlos.
- WASD y mouse están conectados; el mando todavía no tiene acciones asignadas.
- El personaje, los materiales y la escena son provisionales.

## Qué sigue, en orden recomendado

1. **Prueba jugando** desde `Movement001` y ajuste de sensación según la lista manual.
2. **Validación de AnyRPG 6.3** en su copia separada: abrir la demo, revisar compilación y probarla sin cambiar el proyecto de Vhaldren.
3. **Validación de AnyMMO/FishNet**: cargar la demo, iniciar una partida Lobby y conectar un segundo cliente en la misma red.
4. **Integración vertical**: comprobar en la base real movimiento WASD, orientación libre hacia mouse, disparo al punto exacto, esquiva, habilidad y réplica de red antes de trasladar más contenido.
5. **Integrar el arte KayKit CC0 ya descargado** cuando la escena de prueba y el límite entre el motor y la apariencia estén fijados.
6. **Añadir vida, daño, habilidades y un enemigo** en la arquitectura que haya superado la prueba de dos jugadores.

La auditoría inicial de AnyRPG está en [docs/ANYRPG-EVALUACION.md](ANYRPG-EVALUACION.md). El control de Vhaldren sigue siendo la referencia de movimiento; AnyRPG está en una copia separada y todavía necesita una prueba de ejecución con Unity 6000.3.10f1.

## Reglas de trabajo

- `feature/001-movimiento-base` (PR #1, borrador): base de control. `feature/002-esquiva-zoom` (PR #2, borrador, apilada sobre la anterior): esquiva y zoom. No fusionar a `main` hasta que el dueño apruebe la prueba jugando.
- Solo se importa código o arte de terceros cuya licencia permita reutilizarlo, y se anota de dónde viene.
- Todo arreglo de un defecto lleva una prueba que fallaba antes.
- Las pruebas se ejecutan antes de publicar, y aquí se anota el resultado real, con fecha.
- No se da por hecho lo que solo está planeado.


## Inventario, supervivencia y construcción (11 noviembre 2026, compilado, sin jugar)

Partiendo de la primera versión (SurvivalInventory/Builder/Resource), se completó el ciclo **recoger → fabricar → construir → refugio**. Unity compiló el proyecto **sin errores ni avisos** el 11/11/2026; **no se ejecutaron tests ni Play Mode**.

- **Inventario** (`SurvivalInventory`): 36 casillas, las 9 primeras SON la barra rápida (sin duplicar cantidades). Herramientas no apilables; madera, piedra y bayas hasta 64. Nombres en español, franja de color por objeto, número de tecla, descripción del objeto equipado y mensajes (inventario lleno, herramienta incorrecta, fabricado…). **I** abre/cierra (también **Esc**); clic en origen y luego en destino mueve, combina o intercambia. **1–9** equipa.
- **Kit inicial**: Hacha, Varita, 3 Bayas, 10 madera y 4 piedra (casilla `generousStartKit` para quitar los materiales). El pico y la espada se fabrican.
- **Fabricar** (panel junto a la mochila): Pico (4 madera), Hacha (3 madera), Espada (2 madera + 4 piedra). Comprueba materiales Y espacio antes de gastar nada.
- **Usar con clic izquierdo**: Hacha tala árboles, Pico pica rocas, manos/cualquiera recogen bayas, Espada golpea (28, avisa a la IA con el origen del golpe y hace poco ruido), Varita lanza el orbe arcano, Bayas se comen (+30 hambre, +8 vida). El golpe busca en un arco delante, hacia el cursor, así que alcanza también rocas y arbustos bajos. La herramienta en la mano gira al golpear.
- **Recursos** (`SurvivalResource` + `SurvivalWorldSeeder`): al entrar en Play, los árboles del bosque ya generados pasan a ser talables (12 madera, desaparece el árbol entero al agotarse) y aparecen 10 rocas (10 piedra) y 8 arbustos con bayas rojas alrededor del cruce. No hace falta regenerar la escena. Si el inventario está lleno, el recurso no se pierde.
- **Hambre** (`SurvivalNeeds`): barra sobre la barra rápida; se vacía en ~7 min. A 0 resta vida poco a poco. Tras reaparecer vuelve al 60 % como mínimo.
- **Construcción** (`SurvivalBuilder` + `BuiltPiece`): **B** entra/sale (también Esc). **1–5** eligen pieza: Suelo (4 madera), Pared (6 m + 2 p), Puerta (5 m + 1 p), Rampa (6 m, sube la altura de una pared), Hoguera (3 m + 3 p). Rejilla de 3 m: suelos y rampas en celdas, paredes y puertas en los bordes de las celdas para encajar con los suelos; la hoguera es libre. **R** gira 90°. Vista previa verde/roja sin colisión, con el **motivo** cuando no se puede (lejos, pendiente, ocupado, estás dentro, sin línea directa, faltan materiales). **Clic izquierdo** coloca y descuenta en la misma transacción; **clic derecho** desmonta una pieza propia y devuelve todo. Se puede construir encima de lo construido (p. ej. un suelo sobre una pared). Construir hace ruido (10 m) y atrae enemigos cercanos.
- **Valor del refugio**: las paredes bloquean el movimiento y la visión de los enemigos (usan física real). La **hoguera** cura 1,5 PV/s y reduce a la mitad el hambre dentro de 4,5 m.

Pendiente de probar jugando: tala/picado/recogida y mensajes; fabricar con inventario casi lleno; mover/combinar/intercambiar; comer sin hambre; que el clic sobre la barra o la mochila no golpee el mundo; colocar suelos y paredes en rejilla, puertas transitables, subir la rampa, construir pegado a otra pieza y dentro del jugador; desmontar; hoguera curando; enemigos bloqueados por paredes; legibilidad de la UI en resoluciones pequeñas.

Límites: sin guardado (todo se pierde al salir), sin durabilidad, sin planos con aportes parciales, sin grafo de soporte estructural (una pieza no se cae si quitas lo de debajo), los enemigos no atacan estructuras, sin arrastrar y soltar, iconos provisionales, sin red. No se descompiló ni se copió nada de Minecraft, TLauncher ni The Forest: es un diseño propio a partir de cómo se juega. Análisis: [010-SUPERVIVENCIA-INVENTARIO-CONSTRUCCION.md](010-SUPERVIVENCIA-INVENTARIO-CONSTRUCCION.md).
