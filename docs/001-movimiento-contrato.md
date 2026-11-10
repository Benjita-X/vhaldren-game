# Paso 001 — Prueba minima de locomocion (Unity 6 URP)

**Estado:** los archivos de codigo estan en GitHub. Auditado estaticamente; no compilado, no ejecutado en Unity.

## Alcance exacto

- `Assets/Scripts/Player/LocalPlayerInput.cs`: WASD y raton del jugador local. Ignora el apuntado cuando el puntero esta fuera del area de juego y avisa si no hay sistema de entrada habilitado.
- `Assets/Scripts/Player/PlayerMotionCommand.cs`: intencion de desplazamiento y orientacion en espacio del mundo.
- `Assets/Scripts/Player/PlayerMotor.cs`: aplica el comando al CharacterController, controla gravedad, contacto, velocidad y orientacion visual.
- `Assets/Scripts/Camera/SimpleFollowCamera.cs`: sigue al personaje con angulo fijo, sin leer entradas ni fisica; su orientacion es estable para WASD.
- `Assets/Editor/Movement001SceneTool.cs`: genera y valida una escena de prueba. Es un script **solo del editor**.

## Configuracion inicial cuando instalemos Unity

1. Crear en Unity Hub un proyecto nuevo **Unity 6 / Universal 3D (URP)**.
2. Integrar en ese proyecto la carpeta `Assets/` de esta rama. Los archivos de Unity generados para `Packages/`, `ProjectSettings/` y recursos URP deben incorporarse despues a Git. No hay proyecto Unity completo versionado por ahora.
3. Comprobar que `Input System` moderno esta disponible, o configurar el antiguo. Este prototipo soporta ambos por directivas condicionales. Evitar dejar ambos deshabilitados.
4. Esperar a que Unity compile los scripts antes de utilizar el menu del editor.
5. Seleccionar **Vhaldren > Crear escena de prueba > Movimiento 001**. Se guardara `Assets/Scenes/Movement001.unity`; incluye un suelo, el jugador, la camara y la pared `WallObstacle` para probar colisiones.
6. Ejecutar **Vhaldren > Validar escena 001**. El verificador revisa conexiones y objetos principales; NO simula movimiento.
7. Pulsar **Play**, mover con WASD y apuntar con el mouse. La barra delantera de la capsula muestra hacia donde mira el modelo.
8. Guardar en el repositorio la escena generada y sus archivos `.meta` cuando la prueba sea satisfactoria.

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
- Validacion de comandos: vectores no finitos se rechazan y magnitud se limita.
- La escena es generada por script del editor; **el repositorio no contiene todavia un archivo .unity generado**, ya que no se ha abierto en Unity.
- El validador del editor confirma objetos y referencias, pero **no es un test automatizado de fisica ni una prueba de compilacion**.
- La auditoria de esta pasada corrigio busquedas globales en el editor, puntero fuera de ventana, falta de diagnostico de entrada, configuracion visual invalida y ausencia de obstaculo para prueba. Todos los casos siguen pendientes de prueba en Unity.
- `CurrentFacing` expresa la **direccion objetivo**, distinta del giro visual interpolado. Las futuras hitboxes deben escoger su referencia deliberadamente.
- `Simulate` se llama desde `Update` para la prueba local. La futura capa de red requerira tick, ownership, autoridad y una politica explicita de sincronizacion/prediccion.
- No se han implementado combate, esquiva, animacion, salvado ni LAN.

## Puerta de calidad

**No fusionar PR #1 ni iniciar el combate hasta generar, compilar y probar la escena en Unity, registrando el resultado.** Si se encuentra un fallo, corregir solo ese defecto y repetir la prueba.
