# Enshrouded 1.0 — investigación de comunidad y plan de construcción para Vhaldren
**Fecha del estudio:** 12 de noviembre de 2026.
**Alcance:** anuncios de Keen Games, wiki oficial, reseñas y discusiones públicas de Reddit entre 2024 y noviembre de 2026. No pretende contar cada mensaje ni medir porcentajes representativos. Las conclusiones distinguen información verificable de preferencias subjetivas.

## Estado del juego estudiado
- Enshrouded terminó acceso anticipado el **15 de octubre de 2026**. El último parche confirmado al 12/11 es hotfix 1.0.1.2 de 04/11/2026.
- 1.0 añadió Cinder's Cradle, amplió mapa, reequilibró combate, nuevos enemigos/hechizos, renderizador de distancia, función Sparkworks (interruptores, trampas y reglas de lógica), mejoras de construcción y vuelo libre para construir.
- Su principio comercial/creativo es exploración de mundo artesanal + combate RPG + bases transformables.

Referencias:
- https://enshrouded.com/en-US/news
- https://enshrouded.wiki.gg/wiki/1.0_Update
- https://enshrouded.wiki.gg/wiki/Building
- https://enshrouded.wiki.gg/wiki/Flame_Altar

## 1. El sistema de construcción: su ventaja real
**Observación verificable:** combina bloques pequeños, piezas de 2 y 4 metros, techos, columnas, marcos de ventana/puerta, rampas, modificación del terreno, vegetación y mobiliario. La zona del Flame Altar delimita dónde persisten construcciones y cambios del terreno. El sistema acepta proyectos verticales, excavados y reutilización de ruinas existentes.

**Opiniones favorables de la comunidad:** personas diseñan torres, ciudades, fortalezas, viviendas subterráneas y asentamientos mixtos con madera/piedra. La experimentación tiene poco coste percibido cuando desmontar recupera materiales. También valoran la iluminación, los materiales combinados y la profundidad arquitectónica.

**Quejas recurrentes:** repetir secciones iguales de muros/ventanas/torres requiere demasiado esfuerzo; piden blueprints/copy-paste, mover piezas ligeramente sin desmantelar, controles de altura, colocación visible y posibilidad de reorganizar grupos. Otros destacan que el sistema es tan libre que abruma a principiantes.

Fuentes comunitarias:
- https://www.reddit.com/r/Enshrouded/comments/1sm8c5r/blueprints_would_go_so_hard_in_this_game/
- https://www.reddit.com/r/Enshrouded/comments/1x966jq/why_is_there_no_copypasteblueprint_feature/
- https://www.reddit.com/r/Enshrouded/comments/1aoqsdh/what_are_your_best_building_tips/

### Para Vhaldren
No tratar de replicar el motor de vóxeles y deformación de todo el terreno en la primera vertical slice. Es un sistema costoso en persistencia, render, física, networking y rendimiento. Usar **piezas modulares con sockets y snap**, máscara de colisión y validación compartida, con opción de ajuste fino y colocación libre. Más adelante, evaluar una capa limitada de excavación/decoración del terreno en zonas controladas.

**Catálogo evolutivo propuesto** (contenido para hacer de forma progresiva, no afirmación de implementación):
- Estructura: cimiento, suelo, pared completa/media, marco de puerta, marco de ventana, esquina, arco, techo plano/inclinado, cumbrera, escaleras, rampa, columna, barandilla, soporte, puerta y portón.
- Servicios: fogata, chimenea, cama, mesa de trabajo, fragua, hornos, banco de alquimia, secadero, cofre, estantería, armario, pozo, sembradío, faroles, antorchas.
- Defensa: empalizada, barricada, foso decorativo, torre, plataforma, trampa, barrera de runas, detector/alarma.
- Decoración: alfombras, tapices, pancartas, estatuas, macetas, musgo, enredaderas, mosaicos, rejas, ventanas, pilares, muebles.
- Calidad de vida: ghost preview siempre visible; verde válido/rojo inválido con motivo; snap on/off; rotación y nudge en tres ejes; espejo; demolición selectiva con confirmación; historial deshacer/rehacer; devolución parcial o total configurable; grupos y blueprints de segmentos repetitivos.

## 2. ¿Para qué construir? La división comunitaria
Unos jugadores disfrutan el sandbox creativo puro. Otros sienten que bastaría una caja funcional y piden motivos de supervivencia/defensa para construir algo elaborado. Hoy los beneficios incluyen confort, duración del buff de descanso, estaciones de crafteo, alojamiento de NPC y viaje rápido; no debe afirmarse que las bases existen solo por estética.

Fuentes:
- https://www.reddit.com/r/Enshrouded/comments/1x3fpp3/does_building_in_this_game_have_a_purpose/
- https://www.reddit.com/r/Enshrouded/comments/1wc85pv/is_base_building_necessary/
- https://enshrouded.wiki.gg/wiki/Comfort
- https://enshrouded.wiki.gg/wiki/Survivors

**Diseño Vhaldren:** la construcción produce ventajas medibles sin obligar a invertir horas en arquitectura:
- Refugio + fogata: recuperación y regeneración temporal de resistencia; hogar como respawn.
- Herrería: mejorar armamento/armadura, reparar equipo; alquimia: consumibles y runas; puesto de cocina: bonificaciones.
- Cama, muebles y luz: bonificación de confort con rendimiento decreciente; mejoras de viaje.
- Atalayas y barricadas: defensa situacional durante eventos/oleadas opcionales; no raids destructivos inevitables para jugadores creativos.
- NPC artesanos reclutables y únicos, con catálogo de recetas dependiente de exploración.
- Progresión narrativa: hallazgo de una reliquia → construir estación → desbloquear habilidad/equipo → explorar nueva región.

## 3. Inventario y almacenamiento: quejas de fricción
Jugadores valoran los cofres mágicos que alimentan directamente fabricación, pero critican que deban investigar qué cofre contiene algo, repetir mejoras, gestionar decenas de cofres y depositar materiales manualmente. En 1.0 se mejoraron varios flujos, incluyendo modernización de cofres; aun así existe feedback sobre claridad de UI.

Fuentes:
- https://www.reddit.com/r/Enshrouded/comments/1tonixl/i_think_storage_chests_should_be_magic_by_default/
- https://www.reddit.com/r/Enshrouded/comments/1v7dbnf/crafting_inventory/
- https://www.reddit.com/r/Enshrouded/comments/1rnvcsu/im_a_librarian_heres_how_i_approached_storage/
- https://www.reddit.com/r/Enshrouded/comments/1x9x6nb/magic_chests_no_longer_working/

**Vhaldren:** fabricar desde cofres autorizados de la base desde el día 1; búsqueda, etiquetas, filtros, ordenar, apilar automáticamente, enviar iguales a cofres, reservar materiales para plano activo, mostrar motivos de receta bloqueada y costes faltantes. No usar microtareas de organización como barreras de progreso.

**Integridad obligatoria:** el servidor o transacción única debe verificar coste, posición, permisos y límites ANTES de gastar recursos; el cliente solo previsualiza. Si falla construcción, inventario queda intacto. El desmantelamiento nunca devuelve más materiales de los invertidos.

## 4. Combate, armas y enemigos: evitar su error más discutido
Feedback histórico: varias discusiones protestan que los enemigos tienen patrones repetidos, poco hit reaction, exceso de grupos o stunlock, y que armas de gran peso no comunican potencia. También hay jugadores que disfrutan la dificultad actual y piden ajustes más exigentes.

Fuentes:
- https://www.reddit.com/r/Enshrouded/comments/1uhv13j/the_combat_is_holding_the_game_back/
- https://www.reddit.com/r/Enshrouded/comments/1l58vmq/combat_has_been_neglected/
- https://www.reddit.com/r/Enshrouded/comments/1iih92g/what_would_it_take_to_make_combat_fun_for_you/
- https://www.reddit.com/r/Enshrouded/comments/1xbwa8f/now_that_10_has_been_out_for_a_little_bit_whats/

**Vhaldren:** señales legibles antes de atacar, daño real y números asociados, parry físico distinto de defensa mágica, arma pesada con poise y recuperación, enemigos de perfiles diferentes, combos sin stun infinito, jefe de fases, IA que reacciona al ruido/hechizo y navegación. Ya existe base 012/013 de flecha, espada, parry, escudos, runa y escarcha: consolidar y probar, no recrear.

## 5. Exploración, riesgos y progresión
Enshrouded premia explorar un mapa diseñado a mano, rescatar especialistas y abrir nuevos biomas. La Shroud introduce tiempo y peligro ambiental. Los jugadores aprecian esa tensión, pero algunos se frustran por tareas repetidas o ítems mejores de cofres que vuelven prescindible la fabricación.

Fuentes:
- https://enshrouded.wiki.gg/wiki/Shroud
- https://enshrouded.wiki.gg/wiki/NPCs
- https://www.reddit.com/r/Enshrouded/comments/1xfw8xg/looting_vs_crafting_early_to_mid_game/
- https://www.reddit.com/r/Enshrouded/comments/1x9bphc/question_about_itemizationloot_in_version_10/

**Vhaldren:** zona de corrupción opcional con temporizador/antídotos y recompensas; crear un par de rutas con atajos/secretos en vez de un mapa enorme; hacer que lo fabricado sea competente, lo encontrado tenga propiedades especiales y ambos se combinen al mejorar equipo. No invalidar el crafting con botín automático superior.

## 6. Multijugador y continuidad
Jugadores elogian la construcción compartida, pero discuten cómo no dejar atrás a quienes no participan en una misión. Las experiencias son mixtas: existen mecanismos para repetir misiones, aunque no todo progreso se sincroniza como cada grupo desea.

Fuentes:
- https://www.reddit.com/r/Enshrouded/comments/1x5fg6m/world_progression_for_coop/
- https://www.reddit.com/r/Enshrouded/comments/1x4yxfo/multiplayer_story_progression/
- https://www.reddit.com/r/Enshrouded/comments/1svtdv8/the_so_promoted_shared_progression_is_extremely/

**Vhaldren:** separar Estado del Mundo, Progreso del Jugador y Contribución de Grupo; permitir repetir jefes/mazmorras, recompensas individuales, permisos de construcción por área, protección frente a desmantelamiento ajeno y estado de base autoritativo.

## 7. 1.0 Sparkworks: gran potencial y riesgo de complejidad
Enshrouded añadió un editor de nodos para reglas entre interruptores, trampas, puertas y eventos. A algunos usuarios les entusiasma, otros señalan que es abstracto y piden sistemas fáciles de compartir.

Fuentes:
- https://enshrouded.wiki.gg/wiki/1.0_Update
- https://www.reddit.com/r/Enshrouded/comments/1x0o2nb/introducing_sparkworks/

**Vhaldren:** empezar con objetos configurables por Inspector o menú simple (presión→abrir puerta, proximidad→encender antorcha, palanca→activar trampa). Los nodos, redes lógicas y plantillas compartidas deben esperar hasta tener bases funcionales, guardado y permisos estables.

## 8. Rendimiento y escala de bases
Hay experiencias muy distintas tras la actualización 1.0. Algunos jugadores reportan mejoras y otros tirones fuertes, especialmente con VSync y ciertos equipos. Evitar presentar casos individuales como diagnóstico técnico universal.

Fuentes:
- https://www.reddit.com/r/Enshrouded/comments/1x6vyog/performance_10/
- https://www.reddit.com/r/Enshrouded/comments/1x6p76e/how_is_performance_for_you_folks/
- https://enshrouded.wiki.gg/wiki/1.0_Update

**Vhaldren:** dividir la base por regiones/chunks lógicos; batching por material, pooling de previsualizaciones, colliders simplificados, límites de piezas por volumen, culling interior/exterior, persistencia incremental, pruebas de 100/500/2000 piezas, 30/60 FPS, telemetría de frame time y GC. Evitar Instantiate/Destroy cada frame de apuntado/preview.

## 9. Prioridades e implementación

### P0: Sistema jugable coherente (antes de mapa ambicioso)
1. Un inventario con costes atómicos y fuentes de almacenamiento definidas.
2. 10–15 piezas útiles: suelo, cimiento, pared, puerta, ventana, techo, escalera, columna, fogata, cofre, banco de trabajo, barricada.
3. Ghost preview y validaciones: distancia, colisión, suelo, soportes, permisos, recursos, límite y señal exacta del motivo de fallo.
4. Confirmación de construcción, desmontaje con reembolso y persistencia simple.
5. Recolección de árboles/rocas/plantas con herramienta correcta, feedback visible, rendimiento limitado.
6. Pequeña experiencia: conseguir recursos, fabricar arma y una fogata, cerrar refugio, defenderse de un enemigo y derrotar a un guardián.

### P1: Construcción verdaderamente agradable
- Snapping opcional, rotación fina, alineación precisa, nudge, undo, copiar segmentos, guardado de planos, reutilizar edificios/ruinas, objetos decorativos.
- Cofres compartidos y mejoras de estaciones.
- Físicas/soportes estructurales configurables; NO colapsos arbitrarios sobre bases creativas por defecto.

### P2: Supervivencia y juego avanzado
- NPC artesanos, cocina, cultivos, iluminación dinámica, confort, eventos de defensa opcionales, granjas, automatización simple y comercio.
- Biomas y narrativa propios de Vhaldren.
- Cooperativo autoritativo, persistencia y permisos por parcela.
- Sistema de piezas avanzadas y eventualmente terreno editable limitado.

## Estado de GitHub y precauciones
La rama `feature/013-dano-unificado-auditoria` no tenía un sistema completo de recolección/inventario/construcción en GitHub al comenzar este análisis. La rama `feature/014-supervivencia-construccion-base` se creó desde 013 y contiene un primer módulo de inventario; **no está probado ni es un sistema de construcción terminado**. No sobrescribir ni asumir integrado el trabajo local de los otros agentes: estudiar los commits y reconciliar interfaces primero.

## Criterios de aceptación obligatorios
- Sin materiales consumidos por colocación fallida.
- Ningún objeto aparece dentro del jugador ni sobre otro objeto sin permiso.
- Preview coincide con geometría/rotación final en terrenos irregulares.
- Desmantelar no duplica recursos, ni destruye soporte sin aviso.
- Guardar/reabrir restaura posición, rotación, tipo, estado, dueño y recursos.
- Animaciones e input de movimiento siguen activos durante selección de piezas.
- Dos jugadores no pueden gastar la misma pila simultáneamente (al activar multijugador).
- La UI comunica receta, coste, material faltante, límite, soporte y permiso sin ambigüedad.
- La prueba de mapa con enemigos sigue funcionando sin regresiones.
