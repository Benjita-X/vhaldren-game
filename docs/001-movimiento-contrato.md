# Contrato de control — movimiento, apuntado y disparo de prueba

Qué debe cumplir el control del personaje y con qué se comprueba. Si el código y este documento no coinciden, uno de los dos está mal y hay que corregirlo.

## Reglas

| # | Regla | Cómo se comprueba |
|---|---|---|
| 1 | WASD mueve en direcciones relativas a la cámara | Jugando |
| 2 | La diagonal no es más rápida que una dirección recta | `DiagonalInputDoesNotExceedConfiguredSpeed` |
| 3 | Un toque de un solo paso mueve al personaje y acepta la dirección contraria de inmediato | `OneFrameMovementTapsRespondImmediatelyAndReverseDirection` |
| 4 | Con dos teclas opuestas pulsadas manda la última; mientras haya una tecla pulsada, el personaje no se detiene | `OpposingKeysAxisTests` |
| 5 | El movimiento no depende de los FPS: arranca y alcanza su velocidad también a 1000 FPS | `AcceleratedMovementStartsAtHighFrameRates`, `AcceleratedMovementKeepsSpeedAndStopsCleanly` |
| 6 | El personaje no atraviesa paredes y se desliza a lo largo de ellas | `CharacterControllerStopsAtWallAndSlidesAlongIt`, `SlidesAlongTheDiagonalWallWithoutCrossingIt` |
| 7 | Pasa por un hueco de 1,4 m, sube un escalón de 25 cm, sube una rampa de 12° y cae al suelo | `Movement001ScenePlayModeTests` |
| 8 | El modelo mira al punto bajo el cursor sin girar la cápsula física | `AimRotatesVisualWithoutRotatingPhysicsRoot` |
| 9 | Mirar es independiente de moverse: se puede retroceder mirando al cursor | Jugando |
| 10 | Con el cursor a menos de 0,35 m del personaje, o fuera de la ventana, se conserva la orientación anterior | `AimFacingTests` |
| 11 | El disparo va al punto del mundo bajo el cursor | `MouseRaySetsAimPointAndShotImpactAtCursorTarget` |
| 12 | Un obstáculo entre el personaje y el cursor detiene el disparo, aunque el rayo cruce el propio cuerpo | `ShotStopsAtWallEvenWhenRayCrossesOwnBody` |
| 13 | La cámara mantiene su ángulo y nunca cambia los ejes de WASD | Jugando |
| 14 | Valores inválidos en el Inspector (NaN, infinito) no corrompen posición ni rotación | `InvalidGravityAndTurnSpeedDoNotCorruptTransform`, `CameraRecoversFromNonFiniteOffsetsAndSharpness` |
| 15 | Sin cámara, la gravedad y el frenado siguen funcionando | `MissingCameraDoesNotDisableGravitySimulation` |

## Estructura

`LocalPlayerInput` → `PlayerMotionCommand` → `PlayerMotor`. La cámara es independiente.

- El motor no lee teclado, mouse ni cámara: solo recibe intenciones en espacio del mundo. Así, más adelante, la misma intención podrá venir de la red o de una IA.
- `VisualRoot` es hijo de `PlayerRoot`. El modelo gira; la cápsula de colisión no.
- La única colisión del jugador es el `CharacterController`. Las piezas del modelo no llevan colliders.
- `CurrentFacing` es la dirección **objetivo**; el giro visible del modelo la alcanza a 1080°/s.

## Parámetros actuales

| Parámetro | Valor |
|---|---|
| Velocidad | 6 m/s |
| Movimiento inmediato | Activado |
| Aceleración / frenado / inversión (solo sin movimiento inmediato) | 45 / 75 / 100 m/s² |
| Giro del modelo | 1080°/s |
| Gravedad | −25 m/s² |
| Cápsula | 2 m de alto, 0,4 m de radio, `stepOffset` 0,3, `skinWidth` 0,08, `minMoveDistance` 0 |
| Cámara | Desplazamiento (0, 10, −8), campo de visión 50° |
| Alcance del disparo | 100 m |

## Fuera de alcance por ahora

Daño, habilidades, esquiva, animación, enemigos, guardado y red. El orden propuesto está en [ESTADO.md](ESTADO.md).

## Puerta de calidad

No fusionar el PR #1 ni empezar el combate hasta que las pruebas automáticas pasen y el dueño haya jugado la escena y dado el visto bueno al control.
