# Paso 001 — Prueba minima de locomocion (Unity 6 URP)

**Estado:** el checkout incluye proyecto Unity minimo, Unity Test Framework y pruebas EditMode/PlayMode. La compilacion y los resultados se registran aparte; no asumir que pasaron hasta ejecutar Unity.

## Alcance exacto

- `Assets/Scripts/Player/LocalPlayerInput.cs`: WASD y raton del jugador local. Ignora el apuntado cuando el puntero esta fuera del area de juego y avisa si no hay sistema de entrada habilitado.
- `Assets/Scripts/Player/PlayerMotionCommand.cs`: intencion de desplazamiento y orientacion en espacio del mundo.
- `Assets/Scripts/Player/PlayerHorizontalMotion.cs`: solver determinista de aceleracion, frenado e inversion en plano.
- `Assets/Scripts/Player/PlayerMotor.cs`: aplica el comando al CharacterController, controla gravedad, contacto, velocidad y orientacion visual.
- `Assets/Scripts/Camera/SimpleFollowCamera.cs`: sigue al personaje con angulo fijo, sin leer entradas ni fisica; su orientacion es estable para WASD.
- `Assets/Editor/Movement001SceneTool.cs`: genera y valida una escena de prueba. Es un script **solo del editor**.
- `Assets/Tests/`: pruebas EditMode del solver y pruebas PlayMode del controlador y colisiones.

## Configuracion inicial cuando instalemos Unity

1. Abrir la raiz del repositorio desde Unity Hub con Unity `6000.2.12f1`.
2. Esperar a que Package Manager instale `com.unity.test-framework` 1.6.0 y compile los ensamblados.
3. Comprobar que haya un sistema de entrada habilitado. El prototipo soporta Input System moderno y entrada antigua mediante directivas condicionales.
4. Abrir `Assets/Scenes/Movement001.unity` si existe; de lo contrario, seleccionar **Vhaldren > Crear escena de prueba > Movimiento 001**.
5. Ejecutar **Vhaldren > Validar escena 001**. El verificador revisa conexiones y objetos principales; NO simula movimiento.
6. En Test Runner, correr **EditMode** y despues **PlayMode**. Ambos deben terminar sin errores.
7. Pulsar **Play**, mover con WASD y apuntar con el mouse. La barra delantera de la capsula muestra hacia donde mira el modelo.

**Precauciones del generador:** confirma antes de sobrescribir una escena existente y pregunta si deseas guardar escenas abiertas con cambios. No usarlo durante Play.

## Criterios de aceptacion manuales

- El personaje se mueve en las direcciones relativas a la camara; diagonales sin ventaja de velocidad.
- La camara mantiene el mismo angulo al seguir al personaje y no cambia los ejes de movimiento por su retraso.
- Puedes desplazarte hacia atras o hacia los lados y seguir mirando al cursor.
- Con `VisualRoot` asignado, girar el modelo no hace rotar la raiz que aloja el CharacterController.
- El indicador `FacingIndicator` gira con el modelo.
- Si el cursor sale del area de juego o no hay posicion valida, se conserva la direccion previa.
- Al caminar hacia la pared `WallObstacle`, el personaje debe detenerse ante ella y poder deslizarse lateralmente sin atravesarla.
- El validador busca objetos unicamente dentro de la escena activa, incluso con escenas adicionales abiertas.
- Los objetos visuales no tienen colliders duplicados; `visualRoot` debe ser un hijo del jugador (no la raiz ni un objeto externo).
- El personaje permanece sobre el suelo, la camara le sigue y la consola no muestra errores.
- Regenerar una escena existente exige confirmacion.

## Auditoria tecnica de esta entrega

- Capas separadas: `Input -> PlayerMotionCommand -> PlayerMotor`. Camara independiente.
- Validacion de comandos: vectores no finitos y magnitudes fuera del rango representable se recuperan de forma segura; el desplazamiento diagonal se limita.
- `Assets/Tests/EditMode` cubre aceleracion, frenado, inversion, modos de entrada inmediata, vectores y deltaTime invalidos, y variacion de FPS.
- `Assets/Tests/PlayMode` cubre colision y deslizamiento con CharacterController, recuperacion de velocidad bloqueada, diagonal, giro visual desacoplado, gravedad sin camara y configuraciones no finitas.
- `Packages/manifest.json` fija Unity Test Framework 1.6.0. La escena se genera con el editor de Unity y no se debe editar a mano como YAML.
- La camara y el motor sustituyen configuraciones no finitas por comportamiento seguro; sin camara, el adaptador sigue enviando pasos para que gravedad y frenado no se congelen.
- `CurrentFacing` expresa la **direccion objetivo**, distinta del giro visual interpolado. Las futuras hitboxes deben escoger su referencia deliberadamente.
- `Simulate` se llama desde `Update` para la prueba local. La futura capa de red requerira tick, ownership, autoridad y una politica explicita de sincronizacion/prediccion.
- No se han implementado combate, esquiva, animacion, salvado ni LAN.

## Puerta de calidad

**No fusionar PR #1 ni iniciar el combate hasta que las pruebas EditMode/PlayMode y la prueba manual de control terminen satisfactoriamente, con resultados registrados.** Si se encuentra un fallo, corregirlo y volver a ejecutar las pruebas afectadas.
