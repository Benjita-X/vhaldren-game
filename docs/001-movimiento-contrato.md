# Paso 001 — Contrato de locomocion y prueba en Unity

**Estado:** codigo revisado estaticamente, NO compilado ni jugado en Unity.

## Responsabilidades

- `LocalPlayerInput`: solo captura teclado, mouse y orientacion de la camara del **jugador local**. No se habilita en replicas remotas.
- `PlayerMotionCommand`: solo transporta direcciones en mundo; no lee dispositivos ni sabe de redes.
- `PlayerMotor`: limita y ejecuta movimiento, gravedad y orientacion; publica `ActualVelocity`, `IsGrounded` y `CurrentFacing`.
- `visualRoot` es opcional: cuando se asigna un hijo visual, la raiz con `CharacterController` no gira con el cursor. Nunca asignar el mismo objeto raiz como `visualRoot`.

La simulacion del prototipo se llama desde `Update`. Antes de añadir LAN se debe definir un paso fijo, ownership/autoridad, serializacion de comandos y estrategia de reconciliacion. **Todavia no es un controlador de red ni posee prediccion.**

## Configuracion minima para probar mañana

1. Crear proyecto Unity 6 con plantilla URP y guardar `Assets/` y `ProjectSettings/` en Git.
2. Instalar o habilitar el Input System moderno (o habilitar el antiguo). Ambos estan contemplados mediante directivas de compilacion.
3. Crear un suelo con collider y un jugador raiz con `CharacterController`, `PlayerMotor` y `LocalPlayerInput`.
4. Asignar un hijo de la raiz como `visualRoot` (modelo provisional); apuntar con una camara elevada mirando hacia el escenario.
5. Ejecutar en el editor y observar errores de consola.

## Criterios de aceptacion del primer prototipo

- W/A/S/D corresponden a la orientacion de la camara y la diagonal no corre mas rapido.
- El modelo puede mirar al cursor mientras se desplaza en cualquier direccion.
- Con `visualRoot` asignado, la raiz de colision no rota al apuntar.
- Sin puntero valido se conserva la ultima direccion de apuntado, no hay salto brusco hacia una direccion arbitraria.
- El controlador no atraviesa el suelo ni produce errores al detenerse.
- Ausencia de excepciones en consola con el esquema de entrada instalado.
- No hay scripts de red, ataques, magia ni dash en esta entrega.

## Limites y riesgos actuales

- No hay prefab o escena real de Unity en el repositorio.
- No hay compilacion ejecutada, pruebas de juego ni FPS medidos.
- Movimiento simple sin aceleracion, salto, pendientes especiales ni esquiva.
- El objetivo de orientacion y la rotacion visual pueden diferir durante el giro interpolado: esto es intencional; hitboxes futuras deben definir cual utilizan.
- `PlayerMotor` no impone autorizacion de red: esa comprobacion pertenece a la futura capa de sesion.
- Si el plano del cursor no intersecta el rayo de la camara, permanece la direccion previa.

**Regla:** no fusionar el PR ni anadir funcionalidades hasta realizar la prueba en Unity y registrar el resultado.
