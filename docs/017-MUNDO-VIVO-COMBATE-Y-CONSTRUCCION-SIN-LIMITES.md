# VHALDREN — Mundo vivo, combate profundo y construcción libre
**Diseño maestro 017 — 12 noviembre 2026**

## Estado verificado frente a visión
El juego es **Unity 6000.2.12f1**, individual por ahora. La rama 015 declara pruebas Unity anteriores (67/67 EditMode y 71/71 PlayMode). La 016 y la 017 **no tienen aún ejecución validada en Unity en esta sesión**. La 017 añade ajustes al texto de curación, señales y estados de animación de ataque/defensa y un sandbox creativo F9 sobre las diez piezas actuales. **No existe todavía** un mapa infinito, packs completos de modelos de casas, poblados vivos, cuevas navegables, questlines completas ni salvado persistente. Esas funciones deben implementarse y certificarse antes de anunciarse.

## 1. Una fantasía jugable, no un menú de mecánicas
Vhaldren es un RPG de fantasía/supervivencia donde la Tierra conecta Cielo e Infierno. La libertad de construcción rivaliza en importancia con la exploración, la magia y el combate. El mundo responde a tus decisiones. Cuando dejas de combatir, ves viajeros, humo de hogares, oficios, rutas comerciales, disputas, amenazas y ruinas con historia. Cuando dejas de construir, encuentras una razón para volver a tu casa.

**Tres promesas al jugador:**
1. «Perdí porque tomé una mala decisión, no porque el juego ignoró una tecla».
2. «Siempre puedo elegir entre explorar, combatir, aprender, comerciar y crear».
3. «La casa que construyo y las decisiones que tomo afectan a otros habitantes».

### Antídoto contra el vacío: capas en cada zona
- **Forma:** el lugar tiene silueta memorable, caminos, altura, límites visuales, música y puntos de interés.
- **Vida:** fauna, patrullas, trabajo diario, caravanas, conversación, pequeñas sorpresas y objetos interactivos.
- **Conflicto:** una necesidad local, facciones, rumores, misterio, un peligro creciente, secretos.
- **Cambio:** derrotar un enemigo, ayudar a un NPC o construir un puente cambia algo visible.
- **Regreso:** recursos, personajes, rutas y misiones evolucionan cuando vuelves; sin castigar por desconectarte.

No usar densidad aleatoria de enemigos como sustituto de diseño.

## 2. Combate profundo: una gramática, muchas expresiones
### Ciclo de 4 tiempos
1. **Leer:** silueta, sonido, postura y área de amenaza; las animaciones anticipan un golpe.
2. **Elegir:** dash direccional, apartarse, cubrirse, romper postura, contrarrestar o explotar terreno.
3. **Ejecutar:** ataque o habilidad consume recursos *solo si fue aceptado*. Se conserva el input breve con ventana limitada; no hay golpes invisibles ni dobles por error.
4. **Responder:** reacción, número de daño real, sonido, impacto, ventana de recuperación, posible contraataque.

### Armas que realmente cambian la táctica
| Equipo | Fortaleza | Coste y contra |
|---|---|---|
| Espada corta | Combos rápidos, ataques laterales y cancelación selectiva a esquiva | Corto alcance; si fallas el remate quedas expuesto |
| Espada pesada | Rompe guardias, eleva poise y abre armaduras | Golpes comprometidos, no infinite-stunlock |
| Ballesta | Flechas físicas legibles y puntería precisa | Recarga, visibilidad de trayectoria, cambios de terreno |
| Bastón arcano | Control de zona, proyectiles, elementos y rituales | Preparación, ventanas de interrupción |
| Escudo físico | Protege de tajo y proyectil frontal | Drena resistencia, deja flancos vulnerables |
| Barrera mágica | Mitiga hechizo y explosión | Drena más resistencia y no protege igual contra espada |
| Parada precisa | Defiende con tiempo y abre ventana de contraataque | Ventana estrecha, pierde ante agarres/hechizos especiales |
| Esquiva | Reposiciona, puede evitar el impacto | Cooldown, distancia real, no atraviesa geometría |

**Combinaciones originales por aprender:** hielo reduce movilidad, fuego altera zonas heladas, sangre exige administrar coste y recuperación, aire cambia la trayectoria de proyectiles, magia ritual prepara terreno. No basta con añadir diez botones: primero tres habilidades distintas con VFX, animación, hitbox y regla táctil de coste/recarga.

### IA por objetivos, no por persecución ciega
Cada enemigo elige entre **observar → acercarse → flanquear → telegrafiar → ejecutar → recuperarse → retroceder/buscar cobertura**. La decisión depende de distancia, línea visual, resistencia, aliados, terreno y estilo. El cazador persigue flancos; el guardián protege un punto; el hechicero obliga a desplazarte; el bestial busca atajos. Un grupo mezcla roles sin atacar todos a la vez. El jefe combina patrones que primero enseña y luego varía, con pausas castigables.

**Justicia imprescindible:** hitbox alineada a VFX, aviso distinguible de daño, ninguna colisión a través de pared, pausa al morir, bloqueo y dodge con resultado consistente a 30/60/120 FPS, colas de inputs limitadas, salud de curación verdadera y explicación visible de recarga o falta de stamina.

## 3. Recolectar debe ser una aventura, no un trabajo
La progresión inicial 016 es **ramas y fibra → pico improvisado → piedra → pico de piedra → mineral**. Es una puerta de entrada, NO el contenido completo.

**Cuatro caminos para el mismo material:**
- **Trabajo manual breve:** una animación visible, un nodo rico, retroalimentación inmediata; mejores herramientas recogen más por ciclo.
- **Exploración:** vetas raras en cuevas con puzzles, derrumbes, fauna, cofres y rutas de regreso; más material por mayor riesgo.
- **Economía:** vender equipo o cumplir encargos para comprar madera/piedra sin picar durante una hora.
- **Hogar:** contratar artesanos o instalar estaciones simples para procesar materiales ya obtenidos; progreso offline solo si se diseña con reglas transparentes.

Los recursos corrientes deben perder peso en la mitad del juego. Lo especial se obtiene al **descubrir** (bioma, jefe, ruina, contrato, pacto), no al repetir cientos de veces la tecla E. No hacer que toda la progresión dependa del grinding; ofrecer un objetivo tangible cada pocos minutos.

**Sugerencia para primeras 30 min:** recolectar 3 ramas y fibra, fabricar pico, encontrar mineral en una mina, hacer antorcha o espada, resolver problema del pueblo, construir pequeña casa, entrar a cueva y vencer guardián. Las misiones no se reducen a «mata 10 lobos»: deben revelar pista, cambiar la ruta o dar acceso a un maestro de magia.

## 4. Construcción prácticamente ilimitada con casas de cualquier estilo
No confundir «puedo colocar muchas piezas sin materiales» con «el mundo almacena infinitas estructuras»: una construcción realmente masiva necesita streaming, presupuestos de memoria y guardado por región.

### Dos modos sin duplicar el sistema
- **Supervivencia:** recursos, talleres, reparación, decoración, comercio y progresión; piezas cuestan y desmontarlas devuelve su coste cuando corresponde.
- **Creativo (solo local):** F9 alterna modo sin coste ni requisitos de banco, conservando las reglas de distancia/colisiones. Piezas creadas gratis **jamás reembolsan material**. Ideal para levantar castillos sin perder horas extrayendo piedra.

Actualmente hay 10 piezas de formas básicas. El modo creativo 017 **no incorpora nuevos modelos**. Los packs artísticos siguientes son especificación de producción.

### Biblioteca artística modular (objetivo: 8 familias intercambiables)
Cada familia comparte puntos de encaje, pero tiene geometría, texturas y adornos propios.
| Familia | Materiales, formas y fantasía |
|---|---|
| Aldea antigua | madera torcida, adobe, techos de paja, vigas, chimenea y piedra irregular |
| Medieval noble | muros de sillar, arco, ventana ojival, torre, techos de teja, almenas |
| Moderna | hormigón liso, vidrio, acero, terrazas, paneles y grandes ventanales |
| Hechizada | madera luminosa, piedras levitantes, runas, cristales, escaleras imposibles solo visualmente |
| Vampírica | gótico, vitral carmesí, columnas, bóvedas, ataúdes y balcones |
| Licántropa | troncos enormes, pieles, techumbres orgánicas, fogones y tótems |
| Celestial | mármol claro, filigranas, alabastro, arcos flotantes y luz refractada |
| Infernal | obsidiana, hierro negro, bronce, cadenas, hornos y vidrieras de magma |

**Kit base por familia:** cimiento, suelo, muro normal, muro media altura, pared ventana, pared puerta, viga, columna, arco, techo recto y esquinas, escalera, baranda, cornisa, puerta funcional, balcón, fogón, muebles y 8–12 decoraciones. Con piezas combinables, ventanas, tintes, desgaste y ornamentos se logran miles de combinaciones visuales sin crear miles de mallas totalmente independientes. Es obligatorio conseguir/importar modelos con licencia adecuada o producirlos; cambiar el tinte de un cubo NO equivale a crear casas acabadas.

**Herramientas de construcción por prioridad:** snap contextual, colocación libre fina, rotación, duplicar segmento, selección múltiple, deshacer/rehacer, simetría, rellenar paredes y suelos, cambiar estilo sin destruir estructura, planos compartibles, puertas que abren, editor de interiores. Si falla una colocación no gastar; si desmonta no duplicar. Modo creativo debe preservar geometría válida y evitar que se incrusten piezas que impidan caminar.

### Arquitectura
Separar datos de pieza (ID estable, dimensiones, puntos de anclaje, prefab por estilo, coste, función), reglas de soporte/colisión, sistema visual y guardado. Usar prefabs instanciados para piezas complejas; agrupar geometría estática por chunk, instancing/LOD, culling de interiores y colliders de baja complejidad. Para construcción de alta densidad, no mantener un GameObject y un OnGUI para cada mini-bloque.

## 5. Mundo grande tipo Minecraft, pero con identidad Vhaldren
**Modelo híbrido:** geografía procedural por semilla + regiones curadas a mano + construcción modular/voxel detallada. La aleatoriedad crea espacio; la dirección artística crea lugares memorables.

**Director de regiones:**
1. Generador determinista por `worldSeed + chunkCoordinates + biomeVersion`, con biome por clima/altura/humedad, sin saltos absurdos entre biomas.
2. Chunks de superficie (propuesta inicial 64×64 m), cargados en anillos alrededor del jugador, con LOD y descarte fuera de distancia.
3. Cuevas y mazmorras conectadas por entradas físicas, con escenas/aditivos o volúmenes interiores; al principio 1 cueva artesanal, luego familias procedurales validadas.
4. Aldeas, ruinas, ríos y caminos colocados con reglas, no dentro de montañas ni encima de estructuras del jugador. Puntos especiales escritos a mano con historia.
5. Persistencia por chunk de **diferencias** con respecto al terreno semilla (piezas colocadas, recursos agotados, puertas, eventos, inventarios, NPC). Guardado transaccional, versiones y recuperación ante cierre brusco.
6. Coordenadas de largo alcance y desplazamiento del origen cuando haga falta. Seguridad ante 100/1.000/10.000 piezas, sin cargar todo el mundo a la vez.

«Infinito» significa expansión procedural **prácticamente sin límite prefijado**, sujeto a disco, memoria y rendimiento; no afirmarlo implementado mientras el motor de chunks, guardado y pruebas no existan. No intentar crear toda la Tierra, el Cielo y el Infierno en una única escena Unity.

## 6. Pueblos como sociedades, no vendedores inmóviles
Cada asentamiento debe tener una **identidad y memoria**.
```text
TownState:
  seguridad, provisiones, riqueza, lealtades, prestigio,
  miedo, influenciaArcana, reputacionJugador, amenazas
  + NPC persistentes + rutas + eventos completados + obras construidas
```
La simulación diaria procesa trabajo, compra/venta, viaje, descanso y ocio. Una caravana puede llegar con nuevos materiales; un ataque baja seguridad; construir una defensa o reparar un puente cambia las rutas; proteger al herrero desbloquea armas; robar afecta precios y reputación. La IA no necesita usar un LLM por cada personaje: una FSM/utility AI con datos, horarios, eventos y diálogos contextuales es más controlable y eficiente.

### Pueblos que se sienten distintos
- **Cruce de Bruma (mixto):** madera, comida, rumores, mercader ambulante; dilema entre cerrar rutas o proteger refugiados.
- **Aldea de la Luna Rota (licántropos):** leyes de manada, cacerías rituales, medicina, transformaciones y tensiones con aldeas humanas.
- **Noctis (vampiros):** comercio nocturno, casas aristocráticas, deudas, espionaje, acceso a sangre ritual y refugio solar.
- **Liceo de las Siete Runas (magos):** clases, pruebas prácticas, bibliotecas y duelos; aprender magia mediante acciones, no solo pagar.
- **Necrópolis de las Voces (no muertos):** recuerdos, entierros, oficios de conservación y preguntas sobre identidad.
- **Corte de las Alturas (celestial):** guardianes, querubines, serafines de alas y fuego simbólicos, ofanim de ruedas/ojos y órdenes de ángeles humanoides. Inspiraciones en diversas iconografías/tradiciones, reinterpretadas artísticamente; NO presentar un único diseño como hecho religioso definitivo.
- **Casa de Obsidiana (infernal):** nobleza demoníaca, linajes, tratados, mercaderes de pactos, rebeldes y guardianes de hornos. La política importa tanto como la fuerza.

Tierra, Cielo e Infierno no equivalen automáticamente a bueno, neutral y malo. En el Alto Reino puede haber justicia cruel; en la nobleza infernal, quienes respetan sus pactos; en la Tierra, personas que aprovechan las guerras.

### Motor de eventos: ejemplo concreto
**«La caravana de las campanas»**: falta una caravana y el pueblo se queda sin hierro. El jugador investiga marcas en el sendero; descubre que la retiene una patrulla celestial, o puede negociar con una banda licántropa.
- Liberarla por fuerza: comercio restaurado, pero sube tensión con la orden celestial.
- Negociar el paso: pagar favores y abrir ruta, menor violencia.
- Robar el cargamento: materiales inmediatos, reputación empeora y guardias patrullan tu casa.
- Ignorarla: escasez, armas más caras y un herrero que puede abandonar el pueblo.
La solución modifica indicadores persistentes, diálogos, comerciantes, rutas y próxima misión. Ninguna rama se mide únicamente por oro recibido.

## 7. Trama central: «El Juramento Partido»
El firmamento selló hace siglos un pacto para mantener separadas tres fuerzas. Al romperse una de las siete cerraduras, los caminos de Tierra empiezan a mostrar minerales celestiales, grietas infernales y transformaciones inestables. El personaje descubre que las cerraduras también encerraron civilizaciones y memorias; reparar el orden no es necesariamente lo mismo que salvar a sus habitantes.

**Acto I — Tierra:** reparar el pueblo, conocer el coste de la magia, seguir señales de una mina y derrotar al guardián que alimenta la primera grieta.
**Acto II — Alto Reino:** peregrinación entre órdenes angélicas, tribuno de juramentos, visiones de querubines, corredores de ofanim y pregunta moral: ¿quién decide qué merece protección?
**Acto III — Reino Abisal:** casas nobiliarias, pactos, criaturas ancestrales y conflicto entre libertad, deuda y caos.
**Finales de campaña:** restaurar frontera, abrir intercambio o crear un pacto nuevo; el mundo y sus asentamientos deben mostrar cambios medibles. Las razas/linajes condicionan oportunidades de diálogo, no impiden completar la historia.

## 8. Primera experiencia compacta que sí se puede construir y verificar
La escena 016 actual es un bosque/ruinas/altar de 92×92 metros; no es un mundo infinito. Primera aventura individual objetivo 20–40 minutos:
1. Llegas a un cruce con refugio iluminado y tres NPC funcionales: herrero, curandera, viajero.
2. Resuelves un problema de abastecimiento usando una de dos rutas; aprendes recolección y creación de pico sin grind largo.
3. Construyes un refugio con cama, fogata y cofre; o pruebas F9 creativo sin costes.
4. Entras en cueva artesana con bifurcación: combate, recurso raro y secreto.
5. Derrotas un jefe con 3 patrones entendibles (acercamiento, área, ataque interrumpible), obtienes runa y la usas ante un NPC.
6. El pueblo reconoce tu decisión; abres una receta y cambian uno o dos diálogos.

Para evitar humo: hoy **solo** hay una escena base de bosque, enemigos prototipo, 10 piezas, combate básico y recursos. Poblado, tres NPC, cueva, consecuencias, diálogos y jefe avanzado son requisitos pendientes.

## 9. Orden de ejecución y criterios de aprobación
### Bloque A — Reparar antes de expandir
- Integrar en **una rama de trabajo** (sin borrar originales), compilar en Unity, restaurar clips al controller con menú `Vhaldren/Personaje/Preparar personaje KayKit`.
- Comprobar Q con 75/100 PV: recupera **25** y se muestra **+25**, no +35; Q en vida llena no consume cooldown.
- V/T/F se ejecutan en teclas rápidas, informan cooldown/stamina y muestran clip. Z/C bloquean con pose, X parry con pose; no golpes fantasma ni inputs perdidos.
- F9 creativo y B construcción; comprobar muchas piezas, desmontaje sin generar recursos, cofre creativo sin borrar inventario por exceso de capacidad.

### Bloque B — Primer juego, no otro gimnasio
- Un poblado artesanal con 3 NPC y una misión con dos opciones que cambie diálogo/precio.
- Una cueva explorada de principio a fin y un jefe/tesoro con combate justo.
- Paquete visual consistente de tres familias de edificios; puertas, muebles y ventanas funcionales antes de añadir cien estilos.
- Guardado/carga de casa, inventario y estado de misión; menú inicio/continuar, opción de reconstruir guardado corrupto.

### Bloque C — Mundo masivo
- Prototipo aislado de chunks y streaming sin interferir con el escenario funcional.
- Sustituir backend de mundo solo tras pruebas con semillas, bordes, retorno a regiones y persistencia.
- Expandir por biomas con curaduría de ciudades, cuevas, castillos, culturas, historias y un portal verdadero por reino.

**Pruebas automáticas + manuales:** compilar, suites EditMode/PlayMode, 30/60/120 FPS, entradas rápidas, oclusión, guardado/reinicio, 100/500/1000 piezas, cambio de modo creativo sin reembolso explotable, diez ciclos entrar/salir de cueva, cinco muertes, jefe sin daño durante aviso, cero `NullReferenceException` en Console. Cada error se convierte en prueba de regresión; ningún contador de «mil auditorías» sustituye ejecución y pruebas reales.

## 10. Decisiones no negociables
- No borrar `main`, `wip/codex-009-010` ni la 015/016 hasta comparar y verificar. La 017 deriva de 016, no certifica la fusión de las carpetas locales del computador.
- No usar assets extraídos de Enshrouded, V Rising, Minecraft o sagas comerciales. Importar arte propio o con licencia legal y conservar créditos.
- No anunciar como funcional algo que solo está en documentos, listas de prefabs o enums.
- Prioridad a juego individual estable. El modo cooperativo se añade solo después; la persistencia local actual debe diseñarse para no hacer imposible migrar.
