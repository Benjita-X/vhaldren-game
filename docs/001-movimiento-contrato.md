# Contrato de control — movimiento, esquiva, apuntado, cámara y disparo de prueba

Qué debe cumplir el control del personaje y con qué se comprueba. Si el código y este documento no coinciden, uno de los dos está mal y hay que corregirlo.

## Reglas

| # | Regla | Cómo se comprueba |
|---|---|---|
| 1 | WASD mueve en direcciones relativas a la cámara | Jugando |
| 2 | La diagonal no es más rápida que una dirección recta | `DiagonalInputDoesNotExceedConfiguredSpeed` |
| 3 | Un toque, incluso si se pulsa y suelta entre lecturas, mueve al personaje una vez y acepta la dirección contraria de inmediato | `OneFrameMovementTapsRespondImmediatelyAndReverseDirection`, `TapPressedAndReleasedBeforeReadStillMovesForOneStep` |
| 4 | Con dos teclas opuestas pulsadas manda el último evento; los taps A/D dentro de una misma actualización conservan el orden | `OpposingKeysAxisTests`, `KeyboardMovementInputPlayModeTests` |
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
| 16 | La esquiva recorre la misma distancia a 30 que a 240 FPS | `DashCoversItsDistanceAtAnyFrameRate`, `LastsExactlyItsDurationAtAnyFrameRate` |
| 17 | La esquiva va hacia donde se camina; quieto, hacia donde se apunta | `DashFollowsMovementDirectionInsteadOfAim` |
| 18 | La esquiva no atraviesa paredes | `DashStopsAtWalls`, `DashingIntoTheWallStopsAtTheWall` |
| 19 | La esquiva respeta su espera; una pulsación hecha hasta 0,15 s antes de tiempo no se pierde | `DashRespectsItsCooldown`, `RequestMadeJustBeforeTheCooldownEndsIsNotLost` |
| 20 | El zoom cambia la distancia de la cámara dentro de sus límites, nunca el ángulo; la rueda se normaliza en los modos uniforme y nativo de Windows | `CameraZoomIsClampedAndKeepsTheViewAngle`, `NormalizedMouseWheelDeltaIsNotDividedByNativeWindowsUnits`, `NativePlatformWheelDeltaNormalizesToOneStep` |

## Estructura

`LocalPlayerInput` → `KeyboardMovementBuffer` / `PlayerMotionCommand` → `PlayerMotor`. La cámara es independiente.

- El motor no lee teclado, mouse ni cámara: solo recibe intenciones en espacio del mundo. Así, más adelante, la misma intención podrá venir de la red o de una IA.
- El adaptador local usa Input System `InputAction` para recibir cada pulsar/soltar, y conserva un tap hasta que el motor lo lea; los callbacks se prueban en PlayMode con cuatro eventos dentro de una sola actualización.
- `VisualRoot` es hijo de `PlayerRoot`. El modelo gira; la cápsula de colisión no.
- La única colisión del jugador es el `CharacterController`. Las piezas del modelo no llevan colliders.
- `CurrentFacing` es la dirección **objetivo**; el giro visible del modelo la alcanza a 1080°/s.
- La esquiva es una intención más del comando (`Dash`). El cálculo de su estado es puro (`PlayerDash`) y el motor lo convierte en desplazamiento: las futuras habilidades seguirán el mismo camino.

## Parámetros actuales

| Parámetro | Valor |
|---|---|
| Velocidad | 6 m/s |
| Movimiento inmediato | Activado |
| Aceleración / frenado / inversión (solo sin movimiento inmediato) | 45 / 75 / 100 m/s² |
| Giro del modelo | 1080°/s |
| Gravedad | −25 m/s² |
| Esquiva | 4,5 m en 0,18 s; espera de 1,2 s; pulsación guardada 0,15 s |
| Zoom | Entre 0,6 y 1,5 veces la distancia normal; 0,1 por paso de rueda |
| Cápsula | 2 m de alto, 0,4 m de radio, `stepOffset` 0,3, `skinWidth` 0,08, `minMoveDistance` 0 |
| Cámara | Desplazamiento (0, 10, −8), campo de visión 50° |
| Alcance del disparo | 100 m |

## Fuera de alcance por ahora

Daño, habilidades, animación, enemigos, guardado y red. El orden propuesto está en [ESTADO.md](ESTADO.md).

## Puerta de calidad

No fusionar el PR #1 ni empezar el combate hasta que las pruebas automáticas pasen y el dueño haya jugado la escena y dado el visto bueno al control.
