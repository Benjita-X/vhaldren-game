# Vhaldren — aceleracion de prototipo jugable (3 dias)

Inicio: 11 de noviembre de 2026. Base: `feature/007-integracion`.
Rama nueva: `feature/008-mundo-jugable`.
Objetivo: **slice jugable inspirada en el genero de V Rising**, NO recrear su contenido ni prometer un RPG completo en 72 horas.

## Plan de las tres jornadas

| Jornada | Lo que se puede probar | Criterio minimo |
|---|---|---|
| 1: Mundo | Mapa continuo 92 x 92 m, bosque, ruinas, altar, 5 guardianes y 1 jefe; Q cura; muerte y respawn | Recorrer, combatir y derrotar al jefe sin tocar Movement001 |
| 2: Combate | 2 habilidades propias (ataque de area y proyectil), indicadores de ataque enemigo, botin sencillo | Peleas interesantes sin colisiones ni habilidades que bloqueen el movimiento accidentalmente |
| 3: Bucle | Objetivo corto, recompensa, balance minimo, audio/FX simples, build de PC | Una sesion de 10-15 minutos jugable de principio a fin; cero errores bloqueantes |

No planificar estas fases como entregas automaticas. Cada fase se valida con Unity antes de desarrollar la siguiente. El dia 1 contiene **codigo y generador subidos a GitHub**, pero su compilacion y PlayMode siguen **sin ejecutar desde esta rama**.

## Ya incorporado a la rama 008

- `Assets/Editor/WorldPrototypeSceneTool.cs`: menu para generar una **nueva escena** `Assets/Scenes/WorldPrototype001.unity` desde `Movement001`. No sobrescribe la escena de movimiento. Suelo fisico unico, zonas visuales, arboles, ruinas KayKit, limites del mapa y un altar.
- `PrototypeEnemy`: 5 enemigos menores y 1 jefe con persecucion basica, ataque cuerpo a cuerpo con comprobacion de linea de vision, vida, muerte y reaparicion. **Sin NavMesh:** la IA puede atascarse tras obstaculos; es provisional.
- `PrototypeGameSession`: HUD simple, progreso de guardianes y jefe, vida del jugador, curacion Q con espera, reaparicion y resultado de la sesion.
- PlayMode: tests para seguimiento y señal de derrota (sin resultados ejecutados todavia).

## Probar el mapa de inmediato (Unity 6000.2.12f1)

1. Descarga/selecciona `feature/008-mundo-jugable` en GitHub Desktop.
2. Abre ese proyecto en Unity y revisa Console.
3. **Sin estar en Play**, usa **Vhaldren > Crear mundo jugable 008 (bosque + ruinas + jefe)**.
4. Se guarda `Assets/Scenes/WorldPrototype001.unity` y se agrega a Build Settings. Abre esa escena y pulsa Play.
5. Control: **WASD** desplazamiento, **mouse** orientacion, **clic izquierdo** disparar, **Espacio** esquiva, **rueda** zoom, **Q** cura (+35 PV cada 12 s si falta salud).
6. Bosque al oeste (X negativo), ruinas al este (X positivo), altar al norte (Z positivo). Enemigos dejan de perseguir al alejarse demasiado del punto de origen.
7. Ejecuta EditMode + PlayMode; registra errores reales. Valida si el jugador puede completar 6 encuentros.
8. Si el mapa se ve y funciona bien, agrega **`WorldPrototype001.unity`**, su `.meta`, los materiales generados en `Assets/WorldPrototype/Materials`, y sus `.meta` en un commit. La escena es generada en Unity y no se almacena en Git hasta ese paso.

## Qué NO es todavía

- Un mundo abierto completo, sistema de biomas con recursos, ciclo dia-noche, crafting, inventario, niveles, progresion RPG, multijugador, jefes complejos ni arte final.
- Los enemigos utilizan capsulas de prueba, no esqueletos animados.
- El disparo aplica daño **en cliente local**; no es seguro para cooperativo sin validacion del servidor.
- El mapa se genera por herramienta Editor, no existe un `WorldPrototype001.unity` versionado antes de generarlo una vez en Unity.

## Regresiones a proteger

- No editar `Movement001.unity` ni alterar `PlayerMotor`, `LocalPlayerInput`, `PlayerDash`, `PlayerAnimationDriver` para incorporar el mapa.
- Sin instalar paquetes ni importar frameworks durante el primer recorrido.
- Revisar colision de arboles y ruinas, paso del jugador, esquiva, resistencia a ataques a traves de paredes y reaparicion en el cruce.
- Si falla la importacion, recopilar Console y pruebas antes de mezclar con otra rama. Mantener PR como borrador.
