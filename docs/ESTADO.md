# Vhaldren — estado actual

Actualizado: 12 de noviembre de 2026.

## Fuente de verdad

La línea activa para continuar es `feature/018-consolidacion-pre-modelado`, basada directamente en
`feature/017-integracion-jugable-pueblos-combate`. La 017 queda como respaldo de la vertical slice
antes de esta consolidación. No volver a 007/012/015 para desarrollar sistemas nuevos.

Escena jugable persistente: `Assets/Scenes/WorldPrototype001.unity`.
Menú: `Assets/Scenes/MainMenu.unity`.
Las escenas `Movement001` y `KayKitCombat001` son laboratorios y están deshabilitadas en Build Settings.

## Qué existe hoy

Vhaldren ya integra movimiento y esquiva, ballesta con flecha física, espada y defensas, seis escuelas
de magia, estados alterados y esencia, ocho razas con poder propio, niveles/equipo/botín, recolección,
herramientas, construcción, misiones/comercio, bestiario con roles, jefe, día/noche, menú, opciones,
códice, terreno, texturas CC0, enemigos KayKit animados, game feel y build de Windows.

No están terminados todavía: guardado persistente, multijugador, ciudades completas, cuevas finales,
transformaciones con modelos propios, monturas, los tres reinos completos, NPC sociales avanzados y
arte final de la mayoría de sistemas.

## Consolidación 018 antes del modelado

La auditoría detectó y corrigió problemas que se vuelven críticos cuando el mundo gana muchos colliders:

- Proyectiles mágicos, apuntado, línea de visión, cadena de rayos, poderes raciales, absorción de esencia,
  alertas de campamento y puntero de construcción tienen fallback exhaustivo cuando un NonAlloc se satura.
- Ignición y Estática pasan por `CombatDamageReceiver`; respetan resistencias y modificadores.
- `DamageOverTime` distingue daño periódico: no genera hit-stop/knockback como un impacto directo.
- El daño periódico se atribuye al atacante y aparece en las estadísticas.
- Estallido infernal no atraviesa paredes.
- Enemigos reaparecidos vuelven a contar para una misión aceptada después de una derrota anterior.
- Las diez ranuras de construcción entraron en `GameInput` y pueden reasignarse.
- El generador antiguo ya NO escribe `WorldPrototype001`: genera únicamente
  `WorldBlockoutGenerated.unity` y recursos bajo `Assets/WorldBlockout`.
- `AuthoredVisual` marca modelos finales de Blender/KayKit para que el runtime no genere cuerpos
  provisionales encima. Lógica, hitboxes y arte quedan separados.

## Pruebas

Último resultado de Unity heredado de la 017 y registrado por Claude en
`docs/019-MAGIA-ESENCIA-BESTIARIO-Y-EJECUTABLE.md`: EditMode 81/81 y PlayMode 113/113 en Unity
6000.2.12f1, además de una build/captura automática.

Esos números NO validan los cambios 018. La 018 añade nuevas regresiones y todavía debe compilarse y
ejecutarse completa en Unity antes de fusionar o dar la base por certificada.

## Contrato para arte final

Los componentes de gameplay viven en la raíz lógica. Los modelos importados desde Blender deben ir como
hijos visuales y marcarse con `AuthoredVisual`. Un cambio de malla, material, rig o animación no debe
mover `CombatHealth`, `CombatDamageReceiver`, `PrototypeEnemy`, `CharacterController` ni las
reglas de colocación.

No regenerar `WorldPrototype001`. El menú bajo `Vhaldren/Blockout` es exclusivamente para blockout
desechable.

## Antes de modelar masivamente

Ejecutar EditMode + PlayMode en Unity 6000.2.12f1 y una sesión manual completa:
menú → raza → recolección → herramienta → construcción → ballesta/espada/magia → defensa → esencia →
equipo → misión/mercado → campamento → jefe → muerte/reaparición.

Si esa cadena pasa, la siguiente fase es arte: primero un kit modular de la primera villa, una entrada de
cueva y un enemigo completo en Blender; después se reemplazan los placeholders por lotes pequeños.
