# Evaluación inicial de AnyRPG para Vhaldren

Actualizado: 10 de noviembre de 2026. Esta evaluación compara el código y la configuración de los repositorios con el control que necesita Vhaldren. No afirma que AnyRPG ya esté integrado ni probado en ejecución.

## Fuentes y estado

- [AnyRPGCore](https://github.com/AnyRPG/AnyRPGCore), `master`, commit `d8c22c0507b67d965a0751c16fda62fd58071503`.
- [AnyMMO FishNet addon](https://github.com/AnyRPG/anymmo-fishnet), `main`, commit `5dc8daa811acef2ec23bfa474f13cd4e4bbcbf1a`.
- AnyRPGCore declara Unity `6000.3.10f1`; su manifiesto incluye URP, Input System, navegación, Addressables, localización y pruebas.
- El código fuente y sus demos están clonados en `E:\Vhaldren\AnyRPGCore` y `E:\Vhaldren\anymmo-fishnet`. Son copias de referencia separadas: los archivos no se han copiado al repositorio de Vhaldren.
- El addon indica que se instala después de AnyRPG, desde la ventana Welcome de Unity.

## Lo que ofrece

AnyRPGCore es un framework RPG extensible con personajes, equipo, habilidades, estados, interacción, mundos, escenas y herramientas de editor. Incluye una demo de características; no es un juego de supervivencia completo con el mundo y el bucle de Valheim ya terminados.

AnyMMO añade FishNet, prefabs y contenido preparados para red. La documentación describe un modo Lobby donde una persona aloja una partida y otras se conectan, además de un modo MMO persistente. Para ejecutar el servidor en un build normal, indica **Play Online → Host Server**; Lobby es el modo para partidas alojadas y `7770` es el puerto predeterminado documentado. El prefab `FishNetNetworkManager` va en la escena de carga y `FishNetPhysicsSceneSync` en cada escena jugable.

## Compatibilidad con el control de Vhaldren

La ruta del jugador de AnyRPG reúne entrada, cámara y desplazamiento relativo a cámara. `PlayerController.CollectMoveInput()` recoge `FORWARD`, `BACK`, `STRAFELEFT` y `STRAFERIGHT`. El personaje gira según `CameraWantedDirection` cuando la cámara se usa para mirar o cuando cambia el modo de orientación. La entrada de movimiento no recibe un vector separado que represente el punto del cursor sobre el suelo.

La selección de objetivos y la orientación del jugador son conceptos distintos en esta base. La cámara controla la dirección de mirada, los clics del mouse interactúan con objetivos y el movimiento también admite click-to-move. Para cumplir el contrato de Vhaldren, habría que adaptar la entrada y la orientación para que el mouse determine el apuntado sin cambiar el vector WASD.

La clase `MovementRollState.Update()` está vacía. El estado enum existe, pero no equivale a una esquiva funcional lista para reutilizar. En red, `FishNetUnitController` recopila `MovementData`, la simula en ticks de FishNet y reconcilia un `PredictionRigidbody`; el `CharacterController` local de Vhaldren necesitaría integrarse con ese flujo para obtener predicción y reconciliación.

## Licencias y tamaño

El motor y el addon declaran MIT. El repositorio también contiene material de terceros con licencias propias: `AnyRPGThirdPartyLicenses.txt` remite a atribuciones por carpeta y distingue contenido de demo, engine y contenido grande. Hay que revisar cada recurso visual antes de incorporarlo al juego o republicarlo.

El checkout del motor contiene unas 22.900 rutas Git y ocupa aproximadamente 369 MiB en objetos comprimidos. Conviene probarlo localmente y fijar qué partes se necesitan antes de incorporarlo al historial del repositorio de Vhaldren.

## Siguiente comprobación

1. Abrir la demo de AnyRPG con Unity `6000.3.10f1` y revisar compilación, escena, personajes y habilidades.
2. Instalar AnyMMO desde Welcome Window, ejecutar la demo Lobby y verificar anfitrión más cliente en LAN.
3. Hacer una prueba pequeña de Vhaldren: WASD desacoplado del apuntado con mouse y una acción de combate procesada por FishNet.
4. Revisar licencias del contenido que se quiera usar y evaluar el tamaño antes de integrarlo en Git.

La prueba de movimiento de Vhaldren sigue en `feature/002-esquiva-zoom`; sus resultados EditMode/PlayMode no son una prueba de AnyRPG. La instalación de Unity `6000.3.10f1` todavía está en curso en `E:\Unity\Hub\Editor`.
