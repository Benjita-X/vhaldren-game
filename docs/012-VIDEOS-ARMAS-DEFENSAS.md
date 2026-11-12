# 012 — Referencias visuales, armas, defensas y enemigos

Fecha: 11 de noviembre de 2026 (Chile). Base: `feature/011-ataques-telegrafiados`.
Rama: `feature/012-armas-defensas-enemigos`. Este trabajo es independiente de los cambios locales no subidos de los agentes del usuario para hechizos 010, recoleccion, inventario y construccion.

## Analisis visual directo de los dos MP4 proporcionados

**Download(1).mp4:** ~59,0 s, 576×1024, 30 FPS. Fragmentos vistos:
- 00–06: recoleccion de minerales/sangre, particulas y animaciones de absorcion, siluetas de enemigos sobre el fondo oscuro.
- 09–15: mapa de Dunley Farmlands e inventario de equipo con ranuras y objetos distintos.
- 18–27: refugio, entradas al mundo y desplazamiento por caminos y bosque; contrastes y puntos de interes.
- 30–39: menu de construccion, cofre, estructuras/crafteo y fabricacion de equipamiento.
- 45–54: jefes V Blood, interfaz de investigacion, ballesta Merciless Copper Crossbow, mesa de investigacion.
- 54–59: zonas de bosque y exploracion con equipo visible.
**Download(2).mp4:** ~144,6 s, 576×1024, 30 FPS. Combate en arena de Solarus the Immaculate:
- 00–20: combate inicial, ataques fisicos, desplazamientos y efectos luminosos.
- 20–55: oleadas de columnas de luz con zonas de suelo, necesidad de moverse sin detenerse, ataques simultaneos.
- 65–85: aparicion de Divine Angel y amenazas combinadas; priorizacion de amenazas.
- 90–120: objetos recuperables, efectos circulares grandes, giros y fases; posicionamiento decisivo.
- 120–145: nuevos ataques luminosos, recuperaciones, golpes y proteccion mediante esquivas.

**Lo aprovechable:** telégrafo → impacto → recuperación; identidad por silueta y color; defensas con propiedades distintas; feedback de daños y curaciones; equipamiento visible; progresion legible.
**Lo que el video NO verifica:** hitboxes exactos, daño de habilidades, invulnerabilidad real, logica de red, controles o arquitectura interna.

## Auditoria de la rama 011 antes de editar

1. `LocalPlayerShooter.FireAtScreenPosition` era hitscan: los PV bajaban inmediatamente en la llamada, sin flecha viajera. Seguimos conservando ese modo legacy para pruebas antiguas, pero la escena mundo activa proyectiles.
2. `CombatHealth.ApplyDamage(float)` no distingue si el daño viene de una flecha, espada o magia: no permitia resistencias y guardias correctas sin nueva capa.
3. Enemigos de 008 son capsulas con colores y ojos; hay un modelo CC0 RogueHooded para el jugador, pero no una biblioteca de esqueletos/guerreros texturizados en el repo.
4. La rama 011 ya tiene anillos de aviso, i-frames de dash, reaccion basica y números; esta implementacion los reutiliza y no reescribe `PlayerMotor`.

## Cambios implementados

| Sistema | Implementacion | Tecla |
|---|---|---|
| Ballesta | Modelo 3D procedimental acoplado a ShotOrigin; flecha con vástago, punta metalica y plumas, viaje visible, gravedad reducida, barrido de colision, impacto con flash. El daño solo se registra al colisionar; ignora al tirador, no atraviesa pared ni enemigo. | Clic izquierdo |
| Espada | Tajo frontal con arco visible, 0,12 s de preparacion, daño de clase Melee, cono de 120°, alcance 2,8m y 2,4s de espera. | V |
| Magia runica | Pulso circular centrado en el lugar donde se activo, aviso 0,55s, radio 3,5m, daño Magic y 7s de espera; no atraviesa paredes. | T |
| Escudo fisico | Mantener guardia frontal: reduce espadas ~78% y flechas ~52%, NO detiene magia. Consume resistencia. | Z mantener |
| Barrera magica | Mantener guardia frontal: reduce magia ~80% y explosiones ~62%, NO detiene espadas. Consume más resistencia. | C mantener |
| Parada precisa | Ventana ~0,20s, cooldown ~1,5s; niega un solo golpe frontal de espada o flecha, no detiene magia. Consume resistencia. | X pulsar |
| Esquiva | El dash existente concede proteccion a los ataques nuevos mientras está activo; no modifica motor ni entradas existentes. | Espacio |
| Enemigos | Siluetas 3D procedimentales con cabeza, armadura, capucha, manos, piernas y arma; materiales con textura procedural, estética bosque/ruinas/boss. Sin colisiones extra ni movimientos del root. | — |
| Jefe | Armadura/colores propios; resistencia de flecha/magia; alterna un golpe runico (1 de 3 ataques) identificado con anillo celeste. | — |
| Daño general | `CombatDamageReceiver.Receive(daño, tipo, origen)`: aplica mitigacion, parry, dash, daño real a `CombatHealth`, y feedback PARADA/ESQUIVA/RESISTE. No inventa daño. | — |

La barra de resistencia se rompe al agotarse y no puede reactivarse hasta recuperar el 25%; habilidades y defensas usan el mismo recurso.
**Nota:** la resistencia por tipo solo funciona para ataques que pasan por `CombatDamageReceiver`; los hechizos 010 aun sin commit deben adaptarse a este contrato. Los viejos `CombatHealth.ApplyDamage` siguen funcionando por compatibilidad.

## Enemigos y arte

`EnemyVisualPrototype` reemplaza **visualmente** la capsula basica en Play y monta submallas 3D simples con texturas de ruido creadas al inicio. Son placeholders 3D texturizados editables por codigo. **No son modelos profesionales riggeados, no tienen animaciones de huesos, atlas completo ni asset de esqueletos importado.** Para produccion conviene usar modelos 3D con licencia compatible (e.g. CC0), rig/Animator, hitbox vinculada a root y eventos de animacion, manteniendo la logica de daño desacoplada.

## Plan de prueba manual obligatorio

1. Abre `feature/012-armas-defensas-enemigos` en Unity `6000.2.12f1`.
2. Abre **Assets/Scenes/WorldPrototype001.unity**, NO regenerar ni sobrescribir la escena.
3. En Play, clic izquierdo: comprobar que se ve ballesta/flecha, viaja y baja PV **solo cuando impacta**. Probar cerca, lejos, disparos que fallan y una pared entre jugador/enemigo.
4. V: tajo frontal debe dañar a objetivo delante, no a objetivo detras; no atravesar paredes.
5. T: pulso magico detona tras anillo y hiere varios objetivos visibles en su zona; ninguno tras muro.
6. Z: frente al guerrero reduce espada; frente a magia no. C: frente a anillo celeste del jefe reduce magia, no espada. X: parada corta bloquea un golpe de frente, no uno por la espalda. Espacio: permite evadir.
7. Bloquea hasta agotar stamina; comprobar pausa hasta recuperacion del 25%, sin bloqueos involuntarios de WASD.
8. Verificar PV reales, numeros flotantes, texto PARADA/ESQUIVA, boss resistente, muerte y reaparicion.
9. Ejecutar **toda la bateria EditMode y PlayMode**; mirar Console y Profiler. Las pruebas nuevas cubren tipos de daño, parry, esquiva, arrow travelling, pared, tajo y pulso.
10. Probar a 30/60/120 FPS y medir ritmo del proyectil y colisiones. Registrar bugs antes de fusionar.

## Riesgos y limites abiertos

- **No ejecutado aquí:** los scripts y tests fueron editados por GitHub; esta sesion no tiene Unity para compilar, correr PlayMode o probar visualmente en el engine. No declarar tests aprobados.
- **Concurrencia:** otros agentes tienen trabajos sin commit en hechizos 010, inventario/construccion y pueden tocar `PrototypeEnemy`, `PrototypeGameSession` y PlayerShooter. Integrar con merges puntuales, NO sobreescribir locales, NO fusionar a main sin validación.
- **Sin multijugador autoritativo:** validar servidor antes de online; proyectiles visuales locales, estados en MonoBehaviour.
- **Sin animaciones profesionales:** los cuerpos son mallas procedimentales, no esqueletos de calidad de produccion.
- **Sin locomocion/navmesh de enemigos:** la IA puede atascarse en arboles/pilares.
- **Sin VFX final/audio:** fisicas, siluetas y mecanicas primero; refinar particulas, sonido, direccion de ataques y camara despues.
- **Sin crafting nuevo aqui:** los videos son referencia, el sistema de construccion/crafteo pertenece al trabajo paralelo aun no integrado.

**Criterio de Go:** cero errores de compilacion o excepciones repetidas, flecha no instantanea, guardias efectivamente distintos, tajo y magia sin atravesar muros, vida y respawn correctos. Si falla, mantener PR como borrador.
