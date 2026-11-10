# Vhaldren

Base de locomoción para Unity 6.2. El código separa la entrada local, la intención de movimiento, el motor del jugador y la cámara. La escena trae un personaje 3D provisional de fantasía oscura para evaluar silueta y apuntado mientras se mueve. Aún no incluye combate, dash ni red.

## Abrir en Unity

1. En Unity Hub, selecciona **Add → Add project from disk**.
2. Elige la carpeta raíz del repositorio, donde están `Assets`, `Packages` y `ProjectSettings`.
3. Abre con Unity `6000.2.12f1` o una versión Unity 6 compatible.
4. Espera a que Package Manager instale Unity Test Framework 1.6.0 y a que termine la compilación. El proyecto aún usa el pipeline integrado de Unity; URP no está instalado/configurado en esta fase.
5. Abre `Assets/Scenes/Movement001.unity` si está presente; si no, usa **Vhaldren → Crear escena de prueba → Movimiento 001**.
6. Ejecuta **Vhaldren → Validar escena 001** antes de entrar a Play.
7. Pulsa **Play**, haz clic dentro de **Game** y prueba WASD, movimiento diagonal, apuntado con el mouse y choque/deslizamiento contra la pared.

El personaje provisional se construye con geometría y materiales de Unity; todavía no usa un modelo externo ni una textura UV final. Su raíz visual gira independientemente de la cápsula de colisión. El marcador metálico al frente indica el apuntado.

## Pruebas

La carpeta `Assets/Tests` contiene pruebas EditMode del solver y pruebas PlayMode del `CharacterController`, movimiento diagonal, apuntado visual, valores inválidos y gravedad sin cámara.

Desde Unity: **Window → General → Test Runner**, ejecuta primero **EditMode** y luego **PlayMode**.

Desde línea de comandos, crea antes la carpeta de salida `TestResults` y ejecuta desde la raíz del proyecto:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.2.12f1\Editor\Unity.exe" -batchmode -projectPath (Get-Location).Path -runTests -testPlatform EditMode -testResults TestResults\editmode.xml -logFile TestResults\editmode.log
& "C:\Program Files\Unity\Hub\Editor\6000.2.12f1\Editor\Unity.exe" -batchmode -projectPath (Get-Location).Path -runTests -testPlatform PlayMode -testResults TestResults\playmode.xml -logFile TestResults\playmode.log
```

Los resultados XML y logs permiten revisar fallos sin inferir que una prueba pasó por el mero hecho de compilar.

## Límites conocidos

- La prueba de control manual aún requiere jugar la escena desde el Editor para revisar sensación de aceleración, orientación con el cursor, pérdida de foco, frenado y recuperación de colisiones.
- El solver es local y determinista por paso; autoridad, tick de red, predicción y reconciliación quedan fuera de esta fase.
- No fusionar el PR a `main` ni iniciar combate hasta completar las pruebas automatizadas y la prueba manual.
