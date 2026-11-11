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
- Disparo de prueba (hitscan) al punto bajo el cursor, con trazo. Sin daño.
- Cámara elevada que sigue al personaje con ángulo fijo.
- Escena `Movement001`: circuito con pared recta, pared diagonal, pilares, hueco estrecho, escalón y rampa, sobre un suelo cuadriculado de casillas de 2 m.
- Personaje provisional hecho con formas básicas de Unity.
- WASD usa acciones del Input System que conservan el orden de pulsar y soltar dentro de un mismo frame; `Active Input Handling` queda en Both para mantener la entrada heredada mientras se migra.

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

Nota sobre el defecto 2: el movimiento inmediato que se activó antes lo tapaba, no lo arreglaba. Ahora los dos modos funcionan a cualquier número de FPS.

## Qué está comprobado

**Automático** (Unity 6000.2.12f1, 10 de noviembre de 2026):

- EditMode: 45 de 45 pruebas.
- PlayMode: 24 de 24 pruebas. La prueba nueva procesa cuatro eventos A/D en una actualización y confirma el orden real de entrada; otras siete recorren la escena real `Movement001`: reposo en el suelo, choque con la pared, paso por el hueco, deslizamiento en la diagonal, subida del escalón, subida y caída de la rampa, y esquiva contra la pared.
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
