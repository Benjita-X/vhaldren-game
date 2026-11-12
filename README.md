# Vhaldren — rama unificada 017

**Rama recomendada para continuar:** `feature/017-integracion-jugable-pueblos-combate`, basada en 016→015→014→013→012. Incluye mapa, combate, supervivencia, construccion, progresion inicial, curacion precisa, defensa y primer tablon de misiones/comercio.

**Inicia aqui:** en Unity 6000.2.12f1 abre `Assets/Scenes/WorldPrototype001.unity`, pulsa Play y comprueba los sistemas. Las nuevas pruebas 017 siguen pendientes de ejecutar. El controlador KayKit se revisa y amplía sin destruir estados al cargar Unity; tambien puedes ejecutar `Vhaldren > Personaje > Revisar y completar animaciones`.

**F1:** ayuda con todos los controles. **Q:** muestra curacion real (hasta 35 PV, nunca mas que la vida faltante). **V/T/F:** habilidades con motivo si fallan por recarga/resistencia. **Z/C/X:** escudo, barrera y parry con VFX y clips KayKit. **N:** tablón del Cruce; **Y:** mision; **O/P:** comercio. **B:** construccion y **E:** recoleccion; **J/K:** fabricar picos.

La rama experimental `wip/codex-009-010` guarda otros sistemas de inventario y construccion **no integrados**. Solo se recuperaron sus VFX no duplicados. No borres esa carpeta local hasta verificar que no contiene trabajo pendiente. Ver [integracion, pruebas y limites 017](docs/017-INTEGRACION-UNIFICADA-COMBATE-PUEBLOS.md).

---

## Combate 012 — ballesta con flecha, defensas diferenciadas, habilidades y enemigos 3D

La rama `feature/012-armas-defensas-enemigos` incorpora un prototipo jugable sin regenerar la escena guardada. **Clic izquierdo:** ballesta y flecha viajera con colision real. **Espacio:** esquiva. **Z mantener:** escudo contra espadas/flechas. **C mantener:** barrera contra magia. **X:** parada de precision. **V:** tajo frontal de espada. **T:** pulso runico de area. Q sigue curando. El HUD muestra stamina; cada enemigo tiene modelo 3D procedural texturizado y el jefe alterna golpe fisico/runico.

Abre `Assets/Scenes/WorldPrototype001.unity` y prueba todo. La nueva version **no ha sido compilada ni probada en Unity desde este chat**. La [auditoria visual de los dos videos y plan de pruebas](docs/012-VIDEOS-ARMAS-DEFENSAS.md) distingue lo implementado de lo que falta.

# Vhaldren

Juego de acción de fantasía oscura con cámara elevada: te mueves con WASD y apuntas con el mouse. Proyecto de Unity 6 (`6000.2.12f1`, pipeline de render integrado).

El primer prototipo era solo una base de movimiento; la linea 017 ya integra combate, animaciones KayKit, supervivencia y construccion locales. **No incluye aun cooperativo/red, guardado persistente ni tres reinos 3D terminados.**

El proyecto usa el paquete Unity Input System 1.18.0 con `Active Input Handling` en Both. Al abrirlo, deja que Unity resuelva el paquete y acepta reiniciar el Editor si lo solicita. Las pulsaciones de WASD se guardan en orden hasta la siguiente lectura para que los taps rápidos no desaparezcan.

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
| Espacio | Esquiva: un impulso corto hacia donde caminas (quieto, hacia donde apuntas). Tiene 1,2 s de espera |
| Mouse | El personaje mira hacia el cursor, aunque camine hacia otro lado |
| Rueda del mouse | Acercar o alejar la cámara |
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

## Combate 011 — ataques avisados, esquiva justa y reaccion al impacto

Rama `feature/011-ataques-telegrafiados`: enemigos anuncian sus golpes con un anillo de peligro (el jefe tiene una preparacion mas larga), el daño se calcula al terminar la preparacion y puede evitarse esquivando, alejandose o usando una pared. Los guardianes normales pueden ver interrumpido su ataque al recibir daño; el modelo visual reacciona sin mover colisiones. Al curarse salen numeros verdes `+35` basados en la salud realmente recuperada.

**Prueba:** abrir directamente `Assets/Scenes/WorldPrototype001.unity` en Unity. No hace falta regenerar mapa ni modificar PlayerMotor. Ver [auditoria y pruebas 011](docs/011-COMBATE-LEGIBLE.md). Los nuevos tests no se han ejecutado en esta sesion.

## Combate 009 — vida enemiga y numeros de dano

Esta rama añade **barras de vida sobre cada enemigo**, barra violeta mas grande para el jefe, numeros de dano por golpe real, aviso de derrota y numeros rojos al recibir dano el jugador. El HUD muestra **impactos acertados, dano total y ultimo golpe**. No hay que regenerar el mapa que ya preparo Claude.

**Prueba:** abre `Assets/Scenes/WorldPrototype001.unity` en Unity y pulsa Play. Mira a un enemigo, haz clic y comprueba que sale `-25` al impactar y baja la barra. Consulta [la guia 009](docs/009-FEEDBACK-COMBATE.md) para pruebas y limites. No he ejecutado Unity en esta rama: los tests nuevos siguen pendientes.

## Mundo jugable 008 — primera prueba con enemigos

La rama `feature/008-mundo-jugable` incorpora una **escena nueva generable**: mapa de 92 x 92 m con bosque, ruinas, altar, cinco guardianes y un jefe que persiguen y atacan. El jugador tiene vida, curacion con **Q**, derrota y reaparicion.

**En Unity:** abre la rama, espera a que compile y ejecuta **Vhaldren > Crear mundo jugable 008 (bosque + ruinas + jefe)** (fuera de Play). Abre `Assets/Scenes/WorldPrototype001.unity` y pulsa Play. Controles: WASD, mouse, Espacio, clic izquierdo y Q. El mundo se genera en el editor y debe guardarse en Git tras verificarlo; `Movement001` permanece intacto.

Ver [guia y plan de 3 dias](docs/008-MUNDO-JUGABLE-3-DIAS.md). No se ha ejecutado Unity desde esta rama; las pruebas y el mapa generado estan pendientes de validacion real.

## Nueva sala opcional: combate con KayKit CC0 (PR de integración)

Esta rama incluye modelos originales KayKit Dungeon Remastered (licencia CC0, ver `Assets/ThirdParty/KayKitDungeon/LICENSE.txt`) y una primera diana de entrenamiento con vida, daño y reaparición.

**Para crearla sin tocar la escena de movimiento:** en Unity, con el proyecto abierto y fuera de Play, usa **Vhaldren → Crear escena de prueba → Combate KayKit 001**. Se crea `Assets/Scenes/KayKitCombat001.unity`. Ábrela y pulsa Play: WASD para moverte, Espacio para esquivar y clic izquierdo para disparar a los dos barriles-diana; cada disparo válido causa 25 PV de daño de prueba y la diana reaparece después de unos segundos. La escena `Movement001` permanece intacta.

La primera generación también crea `Assets/ThirdParty/KayKitDungeon/KayKitAtlas.mat`; si decides guardar la escena en Git, añade ambos archivos generados y sus `.meta` después de verificarla.

**Advertencia:** este combate funciona solo en local y no incorpora AnyRPG ni FishNet. Faltan compilación y ejecución de las pruebas en Unity. Consulta [auditoría de sistemas externos e integración](docs/004-AUDITORIA-EXTERNOS-E-INTEGRACION.md) y [auditoría de conexiones, posibles fugas y futuras habilidades](docs/005-CONEXIONES-FUGAS-Y-HABILIDADES.md) antes de fusionar o añadir multiplayer.

## Cómo está organizado el código

| Archivo | Responsabilidad |
|---|---|
| `Assets/Scripts/Player/LocalPlayerInput.cs` | Lee teclado y mouse del jugador local y los convierte en una intención |
| `Assets/Scripts/Player/KeyboardMovementBuffer.cs` | Conserva el orden de pulsar/soltar WASD cuando varios eventos llegan entre dos lecturas |
| `Assets/Scripts/Player/OpposingKeysAxis.cs` | Resuelve teclas opuestas pulsadas a la vez (manda la última) |
| `Assets/Scripts/Player/PlayerMotionCommand.cs` | La intención: hacia dónde moverse y hacia dónde mirar |
| `Assets/Scripts/Player/PlayerMotor.cs` | Mueve la cápsula física, aplica gravedad y gira el modelo. No lee teclado ni mouse |
| `Assets/Scripts/Player/PlayerHorizontalMotion.cs` | Cálculo puro de aceleración, frenado e inversión |
| `Assets/Scripts/Player/PlayerDash.cs` | Cálculo puro de la esquiva: duración, espera y pulsación guardada |
| `Assets/Scripts/Player/DashFeedback.cs` | Estela de la esquiva e indicador de "lista / en espera" |
| `Assets/Scripts/Player/LocalPlayerShooter.cs` | Punto del mundo bajo el cursor y disparo de prueba |
| `Assets/Scripts/Camera/SimpleFollowCamera.cs` | Cámara que sigue al personaje con ángulo fijo y zoom |
| `Assets/Editor/Movement001SceneTool.cs` | Genera y valida la escena de prueba |
| `Assets/Editor/GameViewCapture.cs` | Guarda en PNG lo que ve la cámara |

El movimiento es inmediato por defecto. Para probarlo con aceleración y frenado, desmarca **Instant Horizontal Movement** en el componente `PlayerMotor` de `PlayerRoot`. En ese mismo componente se ajustan la distancia, la duración y la espera de la esquiva.

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
