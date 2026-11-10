# Vhaldren

Juego de acción de fantasía oscura con cámara elevada: te mueves con WASD y apuntas con el mouse. Proyecto de Unity 6 (`6000.2.12f1`, pipeline de render integrado).

Esta rama contiene la **base de control**: movimiento, apuntado, un disparo de prueba y una escena-gimnasio para probarlos. Todavía no hay daño, habilidades, animación ni red.

- Estado actual, qué está comprobado y qué sigue: [docs/ESTADO.md](docs/ESTADO.md)
- Qué debe cumplir el control y cómo se verifica: [docs/001-movimiento-contrato.md](docs/001-movimiento-contrato.md)

## Probar el juego

1. En Unity Hub: **Add → Add project from disk** y elige la carpeta raíz del repositorio (donde están `Assets`, `Packages` y `ProjectSettings`).
2. Ábrelo con Unity `6000.2.12f1`.
3. Abre `Assets/Scenes/Movement001.unity`.
4. Pulsa **Play** y haz clic dentro de la ventana **Game**.

| Control | Qué hace |
|---|---|
| W A S D | Mover al personaje (relativo a la cámara) |
| Mouse | El personaje mira hacia el cursor, aunque camine hacia otro lado |
| Clic izquierdo | Disparo de prueba hacia el punto bajo el cursor (solo deja un trazo) |

La escena es un circuito de prueba. Cada pieza sirve para comprobar algo:

| Pieza | Dónde | Qué probar |
|---|---|---|
| Pared recta | Delante | Chocar y deslizarse de lado |
| Pared diagonal | Delante a la izquierda | Deslizarse a lo largo de una superficie inclinada |
| Tres pilares | Delante a la derecha | Rodear obstáculos redondos sin engancharse |
| Hueco estrecho | Detrás | Pasar por un hueco de 1,4 m |
| Escalón bajo | Detrás a la izquierda | Subir 25 cm caminando |
| Rampa | Detrás a la derecha | Subir una pendiente y caer del extremo |

Si cambias el generador, la escena se vuelve a crear con **Vhaldren → Crear escena de prueba → Movimiento 001** y se comprueba con **Vhaldren → Validar escena 001**.

## Cómo está organizado el código

| Archivo | Responsabilidad |
|---|---|
| `Assets/Scripts/Player/LocalPlayerInput.cs` | Lee teclado y mouse del jugador local y los convierte en una intención |
| `Assets/Scripts/Player/OpposingKeysAxis.cs` | Resuelve teclas opuestas pulsadas a la vez (manda la última) |
| `Assets/Scripts/Player/PlayerMotionCommand.cs` | La intención: hacia dónde moverse y hacia dónde mirar |
| `Assets/Scripts/Player/PlayerMotor.cs` | Mueve la cápsula física, aplica gravedad y gira el modelo. No lee teclado ni mouse |
| `Assets/Scripts/Player/PlayerHorizontalMotion.cs` | Cálculo puro de aceleración, frenado e inversión |
| `Assets/Scripts/Player/LocalPlayerShooter.cs` | Punto del mundo bajo el cursor y disparo de prueba |
| `Assets/Scripts/Camera/SimpleFollowCamera.cs` | Cámara que sigue al personaje con ángulo fijo |
| `Assets/Editor/Movement001SceneTool.cs` | Genera y valida la escena de prueba |
| `Assets/Editor/GameViewCapture.cs` | Guarda en PNG lo que ve la cámara |

El movimiento es inmediato por defecto. Para probarlo con aceleración y frenado, desmarca **Instant Horizontal Movement** en el componente `PlayerMotor` de `PlayerRoot`.

## Pruebas automáticas

Desde Unity: **Window → General → Test Runner**, ejecuta **EditMode** y luego **PlayMode**.

Desde PowerShell, con el proyecto **cerrado** en Unity (o sobre una copia de la carpeta):

```powershell
$unity = "C:\Program Files\Unity\Hub\Editor\6000.2.12f1\Editor\Unity.exe"
New-Item -ItemType Directory -Force TestResults | Out-Null
& $unity -batchmode -projectPath (Get-Location).Path -runTests -testPlatform EditMode -testResults TestResults\editmode.xml -logFile TestResults\editmode.log
& $unity -batchmode -projectPath (Get-Location).Path -runTests -testPlatform PlayMode -testResults TestResults\playmode.xml -logFile TestResults\playmode.log
```

Foto de la escena sin abrir el Editor (queda en `Captures/Movement001.png`; no usar `-nographics`):

```powershell
& $unity -batchmode -projectPath (Get-Location).Path -executeMethod Vhaldren.EditorTools.GameViewCapture.CaptureMovement001 -quit
```

Las pruebas automáticas comprueban que el control funciona; no dicen si se siente bien. Eso se decide jugando.
