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

## Qué hay hoy

- Movimiento con WASD relativo a la cámara, inmediato por defecto (aceleración opcional).
- El modelo gira hacia el cursor sin girar la cápsula física.
- Disparo de prueba (hitscan) al punto bajo el cursor, con trazo. Sin daño.
- Cámara elevada que sigue al personaje con ángulo fijo.
- Escena `Movement001`: circuito con pared recta, pared diagonal, pilares, hueco estrecho, escalón y rampa, sobre un suelo cuadriculado de casillas de 2 m.
- Personaje provisional hecho con formas básicas de Unity.

## Auditoría del 10 de noviembre de 2026

Defectos encontrados y corregidos. Cada uno tiene una prueba que fallaba antes del arreglo.

| # | Defecto | Efecto para quien juega | Arreglo |
|---|---|---|---|
| 1 | Las teclas opuestas se anulaban (A + D = 0) | Al alternar rápido, los dedos se solapan unos milisegundos y el personaje se quedaba quieto con teclas pulsadas | Manda la última tecla pulsada (`OpposingKeysAxis`) |
| 2 | El `CharacterController` descartaba pasos menores de 1 mm y el motor copiaba esa velocidad nula | Con aceleración y más de ~200 FPS el personaje no arrancaba nunca: 0 m en 300 pasos | `minMoveDistance = 0` y la velocidad medida solo se usa al chocar de lado |
| 3 | El disparo ignoraba el propio cuerpo dejando de buscar | Al apuntar hacia atrás mientras el modelo giraba, el tiro atravesaba la pared de detrás | Se busca el primer impacto que no sea el jugador |
| 4 | El cursor encima del personaje invertía la orientación con un pixel de movimiento | El modelo giraba de golpe al pasar el mouse sobre él | Zona muerta de 0,35 m alrededor del personaje |
| 5 | Un toque más corto que un frame no se leía | Toques muy breves podían perderse con pocos FPS | También cuenta la tecla que bajó en ese frame |
| 6 | Las pruebas no sincronizaban la física antes de simular | Las pruebas de toque corto y de puntería fallaban o pasaban según la duración del frame (el "8/8" anterior no era estable: aquí dio 7/8 y 6/8) | Las pruebas sincronizan los colliders al crear la escena |
| 7 | Suelo brillante y liso, pared sin material, sin sombras, capa roja sobre suelo rojo | El suelo se veía rosado casi blanco, el movimiento no se percibía y el personaje se perdía | Materiales mate, cuadrícula, sombras, capa oscura, marcador de puntería luminoso |
| 8 | La escena no estaba en Build Settings | Una build habría salido vacía | El generador la registra |
| 9 | El validador exigía movimiento inmediato | Probar la aceleración rompía la validación | El validador ya no impone una decisión de sensación |

Nota sobre el defecto 2: el movimiento inmediato que se activó antes lo tapaba, no lo arreglaba. Ahora los dos modos funcionan a cualquier número de FPS.

## Qué está comprobado

**Automático** (Unity 6000.2.12f1, en una copia del proyecto, 10 de noviembre de 2026):

- EditMode: 27 de 27 pruebas.
- PlayMode: 17 de 17 pruebas. Seis de ellas recorren la escena real `Movement001`: reposo en el suelo, choque con la pared, paso por el hueco, deslizamiento en la diagonal, subida del escalón, y subida y caída de la rampa.
- El generador crea la escena desde cero y el validador la aprueba.
- Foto de la cámara revisada: suelo rojo cuadriculado, obstáculos grises con sombra, personaje oscuro legible.

**Falta comprobar jugando** (nadie lo ha jugado todavía después de estos cambios):

- [ ] Alternar A y D, y W y S, muy rápido: el personaje no debe quedarse quieto.
- [ ] Diagonales y cambios de dirección: que se sientan inmediatos.
- [ ] Caminar hacia atrás y de lado mirando al cursor.
- [ ] Pasar el cursor por encima del personaje: no debe girar de golpe.
- [ ] Disparar con una pared en medio: el trazo debe parar en la pared.
- [ ] Recorrer el circuito: diagonal, pilares, hueco, escalón y rampa.
- [ ] Tamaño del personaje en pantalla y ángulo de la cámara: ¿cómodos?
- [ ] Opinión: ¿movimiento inmediato o con aceleración? (casilla **Instant Horizontal Movement** de `PlayerMotor`).

## Límites conocidos

- Con el movimiento inmediato activado, los valores de aceleración, frenado e inversión no tienen efecto.
- El punto de puntería es el punto del mundo bajo el cursor. Sobre un obstáculo alto, el personaje apunta a la superficie del obstáculo, no al suelo de detrás.
- El motor se simula en `Update`, con paso variable. Es correcto para un jugador local; el juego en red necesitará un paso fijo.
- El origen del personaje está en el centro de la cápsula. Los modelos animados lo traen en los pies: habrá que ajustar el hijo visual al importarlos.
- Solo teclado y mouse (sistema de entrada antiguo de Unity). Sin mando.
- El personaje, los materiales y la escena son provisionales.

## Qué sigue, en orden recomendado

1. **Prueba jugando** de la lista de arriba y ajuste de sensación con lo que salga.
2. **Cámara**: zoom con la rueda del mouse.
3. **Esquiva** (dash con tiempo de espera): es el movimiento que define al género y la primera habilidad real. Obliga a crear la capa de intenciones (mover, apuntar, disparar, esquivar) por la que pasarán todas las habilidades.
4. **Muñeco de práctica con vida y daño**, y un ataque básico de verdad con respuesta al impacto.
5. **Sistema de habilidades** mínimo: teclas, tiempos de espera y tiempos de lanzamiento definidos como datos.
6. **Modelo y animaciones** del personaje.
7. **Primer enemigo** con comportamiento básico.

Decisiones que conviene tomar pronto porque después cuesta cambiarlas:

- **¿Habrá multijugador?** Si la respuesta es sí, hay que pasar la simulación a paso fijo antes de construir habilidades encima.
- **Pipeline de render**: integrado (actual) o URP. Hay que decidirlo antes de producir arte y efectos.

## Reglas de trabajo

- Rama de trabajo: `feature/001-movimiento-base`. PR #1 en borrador; no fusionar a `main` hasta que el dueño apruebe la prueba jugando.
- Todo arreglo de un defecto lleva una prueba que fallaba antes.
- Las pruebas se ejecutan antes de publicar, y aquí se anota el resultado real, con fecha.
- No se da por hecho lo que solo está planeado.
