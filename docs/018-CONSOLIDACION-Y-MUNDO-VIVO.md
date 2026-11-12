# 018 — Una sola linea jugable, pueblos con consecuencias y tres reinos
Fecha 12 de noviembre de 2026. Documento de diseño y auditoría de consolidación.

## 0. Situación real: NO fusionar carpetas locales a ciegas

Se identificaron ramas paralelas:
- `feature/017-demo-solo-combate-visual`: modo creativo F9, UI de construcción, visuales y notas de calidad.
- `feature/017-integracion-jugable-pueblos-combate`: curación exacta, defensa/animaciones, misiones del Cruce, tablón, pruebas.
- `wip/codex-009-010`: trabajo alternativo anterior de hechizos/inventario; requiere conciliación y no se debe copiar encima sin comparar.
- `feature/018-consolidacion-pueblos-y-combate`: **candidata única para pruebas nuevas**, basada en 017 integración. Conserva la lógica de misiones y reincorpora creativa F9 sin reembolsar piezas gratuitas; además mejora mensajes de habilidades y estado del Cruce.

No borrar ramas remotas ni carpetas del computador hasta respaldar el trabajo local y comprobar que 018 abre, compila y se juega. Esta tarea no ha accedido al disco del usuario. La existencia de 018 en GitHub **no demuestra que las dos carpetas locales sean idénticas**.

## 1. Reglas de calidad y bugs reportados

### Curación: 35 versus 25
`healAmount = 35` es la capacidad máxima de la poción. Si el jugador está a 75/100, solo faltan 25 puntos; por lo tanto el valor real de `CombatHealth.Heal(35)` es **25**, no 35. Correcto: mostrar «Q: poción (recupera +25 PV)» y «Poción: +25 PV reales». Se conserva la lógica de la 017 integración, sin alterar salud para inventar curación. En vida completa no consumir ni aplicar cooldown.

### Magia que no sale
Ruta de fallo: tecla V/T/F → `PlayerCombatAbilities.Update` → `CanStartAbility` (vivo, no construyendo, recarga y stamina) → Animator opcional → ataque o coroutine → colisión/resistencia → feedback. La 017 integrada muestra causas específicas cuando no se acepta. Si no aparece animación, comprobar que el AnimatorController importado incluye `SwordAction`, `SpellAction`, `FrostAction`, `ShieldAction`, `WardAction`, `ParryAction`. En Unity ejecutar `Vhaldren > Personaje > Preparar personaje KayKit` para regenerarlo (antes de Play). Los trigger y las animaciones no se pueden verificar realmente sin Unity.

### Defensa invisible
Z: escudo físico, C: barrera arcana, X: parry, Espacio: esquiva. La rama 017 integración añade `PlayerGuardVisual` y usa los clips `Block` / `Blocking` y otros de KayKit. Verificar que las animaciones existen en el controlador real y no se solapan con esquiva y movimiento.

### HUD legible
La 018 mueve el estado de las habilidades al centro inferior y amplía fuente y mensajes de fallo, separado de las barras de vida y del modo construcción. Los números flotantes deben reflejar PV reales y persistir lo justo para leerse sin apilarse indefinidamente.

### Input confiable
Verificar clics a 30/60/120 fps: la rama 016 incorpora buffer de hasta dos disparos físicos con objetivo congelado y cancelación al desactivar el arma. No se garantiza sincronización multijugador. Validar cancelación por muerte y cambio de modo construcción.

### Construcción y su economía
La 018 agrega F9 creativo de la otra 017: no gasta recursos, y piezas gratis no reembolsan recursos cuando se desmantelan. Supervivencia mantiene coste atómico, banco de trabajo y refund protegido. Hacer prueba adicional de límite de inventario al desmontar cofre y de entrada/salida de B cuando los componentes de combate se agregan dinámicamente.

## 2. Filosofía: cada asentamiento es un pequeño sistema social

Identidad de pueblo = geografía + economía + historia + cultura + seguridad + escasez + magia + facciones + relaciones. Un pueblo no debe ser solo NPC quietos con indicadores de misión. Diseñar NPC con **roles** (artesano, guarda, maestro, familia, forastero, comerciante) y rutinas horarias; conservar consecuencias por jugador y por mundo. No usar un LLM por cada NPC para simular inteligencia básica: usar necesidades, utility scoring, horarios y estados reproducibles.

Se han implementado en 018 las primeras variables de `SettlementState` para el Cruce: seguridad, provisiones, ánimo y tensión arcana. Recibe cambios al completar misiones y por eventos del ciclo; el tablón los muestra. Esto es **solo un primer modelo de estado local**, no NPC caminando ni misiones generativas.

## 3. Primeros asentamientos terrestres (guiones adaptables)

### Cruce de Bruma — comunidad mestiza (primera zona jugable)
**Necesidad:** salvar las caravanas atrapadas por el bosque. **Facciones:** consejo mixto, guardias cansados, refugiados. **Secuencia:** 6 madera para refugio → pico/piedra para puente → despejar guardianes → investigar señales mágicas → fogata para asentamiento → jefe del altar. **Decisiones futuras:** escoltar refugiados baja provisiones y aumenta ánimo; fortificar el paso aumenta seguridad pero perjudica comercio temporalmente. **Cambio visible deseado:** reconstruir pasarela, encender antorchas y aumentar las personas/caminos accesibles. **Fallo permitido:** perder la caravana, no bloquear toda la campaña.

### Villaluna — licántropos
**Necesidad:** contener hambre y maldición durante luna llena. Guardabosques y ancianos conocen rutas de cuevas. **Evento:** niños desaparecidos tras una cacería; tres respuestas (seguir huellas, negociar con cazadores, proteger poblado) revelan distintas versiones del conflicto. **Misión:** cueva de lobos con rutas laterales y transformación útil para oler rastros; recompensa de linaje, no poder gratis. **Peligro:** combates de manada con aviso coordinado, no stun infinito.

### Nocturna — enclave vampírico
**Necesidad:** conservar tratados con humanos y suministros de sangre segura. **Evento:** mercader nocturno trae contrabando y una carta con nombres de desaparecidos; el jugador puede entregarla, investigar o encubrir. **Misión:** cripta conectada a un antiguo hospital; descubrir un pacto fallido sin obligar a matar a todos los vampiros. **Tensión:** la forma de murciélago abre rutas, pero limita armas pesadas y cuesta energía.

### Academia de la Espiral — magos
**Necesidad:** cerrar una falla de magia que contamina el agua. **Enseñanza jugable:** un instructor muestra telegraph, práctica contra objetivos móviles, control de área y contrahechizo. **Evento:** experimento interrumpido provoca daños en el mercado; acudir primero a ayudar o perseguir al culpable cambia reputación. **Beneficio:** aprendizajes por pruebas, no solo comprar un icono de hechizo.

### Osario — ciudad de no muertos
**Necesidad:** recuperar memoria e identidad; la ciudad conserva archivos perdidos. **Misión:** restaurar tres archivos con pistas contradictorias y encuentros de guardianes. **Evento:** peregrinos humanos quieren quemar un santuario; resolución por diálogo, investigación o defensa. **Diseño:** civiles no muertos, artistas y magistrados, no exclusivamente enemigos.

### Puerto Humano / Mercado Errante
**Necesidad:** mantener rutas, forja y pesca. **Evento:** un mercader ambulante cambia ruta por seguridad real del Cruce; misiones de protección comercial y economía dinámica. **Diseño:** poder comprar herramientas y armadura adecuada al nivel, pero los mejores materiales de exploración mantienen su valor.

## 4. Alto Reino — Cielo, diversidad celestial

**Arte referencial interpretado, no una afirmación universal de cómo «son realmente» los ángeles.** Varias fuentes religiosas/tradiciones los representan de forma distinta:
- Serafines: imaginería de fuego y seis alas (Isaías 6). Tronos y liturgia en altas naves de luz; no todos son humanoides.
- Querubines: guardianes con múltiples rostros y alas en visiones como Ezequiel 1 y 10; más cerca de guardianes simbólicos que de bebés alados.
- Ofanim: ruedas superpuestas con ojos, presentes en imaginería visionaria e interpretaciones posteriores. Su movimiento y geometría se expresan con rotación y anillos, pero colisiones/hitbox simples.
- Ángeles mensajeros: pueden aparecer con apariencia humana o luminosa; ofrecer variedad y no una única plantilla.
- Coros/órdenes, principados y arcángeles: jerarquías según distintas tradiciones, no datos científicos. Para Vhaldren crear **casas y autoridades celestiales originales** en lugar de copiar dogmas literalmente.

**Juego:** puentes celestiales, rutas aéreas, tormentas, santuarios, pactos y ciudades en niveles. Las facciones no son automáticamente buenas: el Consejo de la Aurora quiere cerrar portales, los Custodios de las Ruedas protegen viajeros y las Bibliotecas del Umbral guardan secretos. **Misiones:** reparar un puente de luz, defender el paso de una criatura caída, decidir quién accede a la biblioteca y explorar un santuario bajo nubes.

**Diseño técnico:** vuelo/planeo tendrá reglas de cámara, caída, colisión y retorno; no activar portal hasta tener región visitable y punto de regreso seguro.

## 5. Reino Abisal — Infierno y nobleza demoníaca

No convertir todo en una prisión de lava: mercados, puertos de ceniza, ciudades verticales, palacios, cavernas, barrios de obreros y contrabandistas. **Jerarquía original Vhaldren:** Gran Soberano Abisal → Casas de Ceniza / Obsidiana / Sangre / Ecos → magistrados de pacto → caballeros de forja → custodios del umbral → ciudadanos, comerciantes y viajeros. Incluir facciones rivales y diplomacia, sin afirmar que es una jerarquía religiosa universal.

**Misiones:** rescatar artesanos que sufren un pacto abusivo; negociar acceso al mercado; asaltar una mina que explota esclavos; frenar una tormenta de brasas; descubrir un pacto entre una casa infernal y un guardián celestial. **Consecuencias:** reputación, rutas comerciales, mejora de equipamiento, acceso a magia abisal y riesgos permanentes que pueden purificarse mediante acciones.

## 6. Eventos inteligentes y medibles

Disparadores: tiempo local, reputación, seguridad, provisiones, amenaza, etapa narrativa, presencia del jugador y acciones previas. Un evento tiene duración, consecuencias y cooldown, y no debe dispararse repetidamente solo por recargar escena.

**Ejemplos:**
- Caravana de refugiados: seguridad baja → ruta peligrosa; ayudar consume provisiones, sube moral.
- Falla arcana: tensión alta → tareas de investigación y alteraciones visuales puntuales.
- Cacería lunar: amenazas nocturnas y manadas; las guardias cambian la ruta.
- Embajadores celestiales: pueden reparar la brecha si el consejo confía en ti, no son automáticamente aliados.
- Delegación infernal: trae comercio y pactos que suben provisiones pero también tensión.
- Guardias recuperan camino: al superar un umbral de seguridad y comida, ruta comercial desbloqueable.

**Escalado de dificultad:** amenazas adicionales enseñan una respuesta antes de combinarse; progreso no equivale a multiplicar el daño por diez. La dificultad depende de coordinar esquiva, bloqueo físico, barrera, resistencias y navegación. Telegráficos claros y ventanas de castigo verificables.

## 7. Modelo de NPC futuro

Cada NPC requiere ID único, identidad/cultura, arquetipo, necesidades, trabajo, cama/casa, horario, facción, relaciones, equipo, animador, navegación, inventario, diálogo y estado de misión. «Trabajar» debe producir un cambio verificable (comprar/vender, reparar puente, traer objeto, encender lámpara), no solo mover una animación en bucle. Evitar miles de Update y NavMeshAgents fuera de cámara: simulación económica lenta y locomoción detallada solo en zonas activas.

## 8. Próxima sesión jugable que se debe certificar

1. Abrir **una sola copia respaldada** del proyecto y branch `feature/018-consolidacion-pueblos-y-combate` en Unity 6000.2.12f1.
2. Reparar referencias de animación con menú `Vhaldren/Personaje/Preparar personaje KayKit`; revisar Controller y materiales antes de Play.
3. Probar Q a 75/100 → +25; a 50/100 → +35; a 100/100 → +0 sin cooldown.
4. V/T/F deben ejecutar ataque con efecto, daño y números reales; al bloquearse deben mostrar motivo, no silencio.
5. Z/C/X y Espacio: comprobar animación, resistencia, interacciones con muerte/reaparición y contraataque.
6. B y F9: probar supervivencia y creativo, con 30 piezas, desmontaje, rollback y recursos. No reembolsar piezas creativas.
7. N junto al tablero: aceptar misión, entregar recursos reales con Y, matar guardianes y hacer subir seguridad y ánimo. Eventos alteran valores sin duplicar resultados.
8. FPS 30/60/120, doble clic de ballesta, cobertura frente a raycasts, prueba de escena/edición de BuildSettings y TestRunner.
9. Guardado, NPC animados, cuevas, edificios y tres reinos **siguen pendientes**: nunca declarar esta fase completa RPG hasta que estén creados realmente.

## No promesas falsas
Se hizo una fusión **selectiva de lógica en GitHub** y revisión estática. No se ejecutó Unity, no se editaron las carpetas locales y no se construyeron NPC/ciudades 3D completos. El siguiente paso para una experiencia realmente jugable es importar arte bajo licencia, cerrar regresiones y crear una región terrestre con aldea + cueva + jefe + recompensas + guardado.


## 9. Auditoría adicional: AnimatorController obsoleto CONFIRMADO

Se comparó el archivo versionado `Assets/ThirdParty/KayKitAdventurers/RogueHooded.controller` contra `KayKitCharacterSetup.cs`. El script declaraba **Harvest, HarvestStyle, SwordAction, SpellAction, FrostAction, ShieldAction, WardAction y ParryAction**, pero los ocho parámetros **faltaban en el controlador guardado**. `PlayerAnimationDriver` comprueba su presencia antes de disparar el trigger: por eso podían funcionar daño/cooldown y no verse animaciones.

**Corrección en 018:** `Assets/Editor/KayKitAnimationIntegrity.cs` compara parámetros al abrir el Editor y, si faltan, ejecuta `KayKitCharacterSetup.Prepare()` de forma controlada, solo fuera de Play/compilación/batchmode. Informa éxito o error en Console. También ofrece el menú manual `Vhaldren > Personaje > Validar y reparar animaciones del juego`. No modifica el movimiento, hitboxes ni prefabs al ejecutar el juego.

**Limitación:** el `.controller` viejo seguirá versionado hasta que se abra Unity, se regenere y se haga commit del asset reparado; esta rama aún no ha ejecutado Unity.

## 10. Integración con el trabajo paralelo del agente

Mientras se desarrollaba 018, la rama `feature/017-integracion-jugable-pueblos-combate` recibió actualizaciones concurrentes: `PrototypeVfx`, `SpellImpactFlash`, mejoras de curación, defensas, inventario, `VillageTradeCatalog`, stock/precios del mercado y nuevas pruebas. Se copiaron los archivos nuevos y las actualizaciones no conflictivas; en `PlayerCombatAbilities` se preservaron esos VFX y el HUD legible de 018; en `VillageNoticeboard` se conservaron el mercado nuevo y el estado social del Cruce.

El PR 018 se basa en **016**, no en 017, para presentar una propuesta consolidada sin conflictos textuales de ramas paralelas. La rama de desarrollo que debe abrirse para probar es exclusivamente `feature/018-consolidacion-pueblos-y-combate`. Las anteriores quedan como respaldo hasta validar.

## 11. Qué significa «pueblo vivo» en el estado actual

Implementado: objetivos encadenados, reputación, eventos narrativos, comercio limitado, consecuencias en seguridad/provisiones/ánimo/tensión arcana y tablón interactivo. **No implementado todavía:** NPC desplazándose por el pueblo, casas terminadas, caravanas físicas, animaciones de interacción con cada NPC, caminos que cambian físicamente, guardado de decisiones o regiones completas de Cielo/Infierno. Estos sistemas necesitan prefabs, escenas, comportamiento, navegación y persistencia antes de considerarse terminados.
