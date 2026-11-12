# VHALDREN — Primera biblia jugable 016: tres reinos, razas y progresión
Fecha: 12 de noviembre de 2026 · Estado: diseño implementable + primera iteración de recolección y fiabilidad de disparo.

**Regla central:** perder por decisiones, no por inputs ignorados, controles torpes o información insuficiente. Construir un juego antes que un catálogo de sistemas desconectados.

## 0. Qué está realmente implementado y qué NO

**Ya disponible en GitHub 015:** personaje animado KayKit, movilidad/esquiva, flecha física, espada y habilidades, daño visible, enemigos básicos con avisos, bosque y ruinas, construcción de diez piezas, inventario, recursos y siete estaciones/objetos funcionales/prototipo, reaparición y pruebas. Claude informó 67/67 EditMode y 71/71 PlayMode en 015; estos resultados **no validan la rama 016**.

**Nuevo en 016, pendiente de compilar/probar en Unity:** herramientas mineras fabricables, restricción real para extraer piedra/mineral, disparos consecutivos en cola, recolección ligada a animaciones KayKit, corrección de ballesta visual duplicada y catálogo de tres reinos/aldeas/cuevas/linajes.

**No existe aún:** tres mundos 3D terminados, cuevas explorables, NPC con comportamiento/casas, comercio, armaduras equipables, transformaciones con modelos 3D, fauna real, hechizos de escuela o guardado permanente. El catálogo de código no son escenas creadas.

## 1. Tres reinos coherentes

### Tierra — la zona principal y primera prioridad
- Regiones: bosque liminar, aldeas y cultivos, ruinas antiguas, laderas mineras, montañas nevadas, pantanos, costa y un bosque nocturno de clanes.
- Puntos de interés: cuevas físicas con entradas/salidas señalizadas, minas con varias capas, guaridas, criptas, castillos, santuarios, pozos y mazmorras.
- Cuevas: combinar 2–4 cuevas **artesanales** en la primera región con entradas modulares para crecer. Cada cueva tendrá motivo de entrada, sorpresa, recompensa, atajo y lugar seguro. A escala completa, un director por regiones usará arquetipos + semillas con registro de recompensas para evitar duplicación y pasillos sin sentido.
- Habitantes: humanos, magos, vampiros, licántropos, no muertos, razas mixtas y comerciantes. Un pueblo no necesita ser exclusivamente de una raza.

### Alto Reino (Cielo)
- Archipiélagos flotantes, ruinas de mármol y cristal, observatorios, estaciones de magia astral y rutas aéreas.
- Reglas: altura, vientos, puentes y suelo peligroso; criaturas y recursos exclusivos; aliados que no dependan solo de «bueno/malo».
- Acceso posterior mediante avance narrativo y preparación; no portal activo hasta tener escena, navegación, checkpoints y retorno seguro.

### Reino Abisal (Infierno)
- Valle volcánico, ciudades de obsidiana, mercados infernales, cavernas profundas y santuarios de pacto.
- Reglas: calor ambiental, visibilidad, amenazas que aprovechan terreno, minerales exclusivos y magia de riesgo/recompensa.
- No bloquear al jugador de su inventario, base o progreso al cruzar mundo.

**Arquitectura Unity:** escenas/celdas aditivas por región y cuevas, no tres escenas monolíticas. Estado del mundo separado de progreso de personaje y estado temporal de encuentro. Portales con validación de escena de destino, doble confirmación y retorno garantizado. Colisiones y cámaras distintas según cueva y forma del personaje.

## 2. Razas, linajes, formas y progresión

- Humano: versátil; puede estudiar cualquier escuela mediante esfuerzo/alianzas.
- Nacido de magia: afinidad ritual; más herramientas arcanas y menor resistencia física inicial.
- Vampiro: forma de murciélago y niebla nocturna; intercambio entre movilidad, sigilo y combate.
- Licántropo: forma de lobo y lobo terrible; desplazamiento/sentidos/combate de garras con animaciones e hitboxes propias.
- No muerto: resistencia a ciertos estados y forma espectral/ósea; vulnerabilidades diferentes.
- Semidemonio: aspecto infernal progresivo y habilidades de sangre/fuego/pacto.
- Semicelestial: aspecto celestial y habilidades de protección/luz.
- Híbrido: formas se desbloquean por historia y habilidades elegidas, **no recibe automáticamente todas las ventajas de todas las razas**.

**Separar:** (a) linaje natal, (b) afiliación cultural o facción, (c) escuela de magia, (d) forma desbloqueada, (e) reputación, (f) progreso individual. Esto permite un vampiro que viva con humanos, un licántropo que estudie magia y asentamientos mixtos.

**Transformación real futura:** coste, duración/cooldown, modelo 3D y Animator de la forma, habilidades específicas, cámara y colisiones adaptadas, vista previa visible y restauración de control tras cancelar. No cambiar la raíz de movimiento sin un contrato probado. Para evitar exploits: no atravesar paredes, no transformarse dentro de geometría, no perder objetos ni duplicar ataques durante transición. Nunca mostrar forma nueva sin el prefab/animación.

## 3. Pueblos, NPC, academias de magia y comerciantes

- Aldeas mixtas, pueblo de licántropos, enclave vampírico, academia arcana, necrópolis, mercado infernal y ciudad celestial.
- Cada NPC: identificador persistente, horarios/servicios, facción, inventario, animaciones de caminar, hablar, comerciar, defenderse y trabajar. Las tiendas pueden ofrecer armadura/equipo distinto según reputación y nivel de materiales.
- Mercader ambulante: ruta entre nodos seguros con lógica de descanso y supervivencia; un cliente debe poder encontrarlo con señales razonables, sin perder recursos por desconexión.
- Aprender magia en escuelas: prácticas y misiones con feedback, no solo comprar hechizo. Libros, mentores, investigaciones y pruebas. Colegios separados por escuela, no necesariamente por raza.

## 4. Materiales, equipo y progresión equilibrada

**Bucle temprano real en 016:**
1. Recolectar a mano ramas/madera básica y fibra vegetal.
2. Fabricar pico improvisado (3 madera + 2 fibra).
3. Extraer piedra de rocas visibles manteniendo E con animación.
4. Fabricar pico de piedra (3 madera + 5 piedra + 2 fibra).
5. Extraer mineral; empezar a plantear forja, herramientas de metal y primer armamento.

**Capas futuras:** equipo artesanal → piedra/cuero → cobre/hierro → acero/aleación → runas antiguas → materiales de biomas → reliquias celestiales/abisales. Cada capa debe tener motivo y desafío; un mercader puede vender equipo adecuado al progreso pero no suplantar todo el crafteo. Armaduras: defensa física/mágica, resistencia elemental, peso/movilidad y apariencia visible (modelo/skins con piezas reales), sin números ocultos.

**Objetivos de transacciones:** descontar materiales solo cuando fabricación/colocación se confirma; no comprar dos veces por doble clic; evitar items fantasma, reembolso duplicado y crafting sin recursos. Las reglas migrarán a servidor en multijugador.

## 5. Contrato de calidad de cada acción (no negociación)

**Disparo con ballesta:**
input → solicitud con ID → mirar objetivo en el instante del clic → estado/cooldown → aceptación o cola corta → proyectil único → colisión única → defensa del objetivo → PV reales → número de daño/sonido/impacto → recuperación. Dos clics rápidos deben crear dos flechas consecutivas y no una sola. Si se pulsa por encima del buffer, comunicar rechazo, no fingir el disparo. Cola se cancela al morir/desactivar arma; retener el punto de mira en el momento de pulsar. Los impactos registrados solo reflejan daño efectivamente aplicado, nunca click.

**Recolección:**
eliges recurso → revisar herramienta → comenzar PickUp/Chop → progreso → entregar un lote por ciclo → sumar inventario → audio/partículas/cantidad → agotamiento → regeneración. Si falta pico, mostrar causa y acceso a receta. Ningún recurso puede evadir el requisito llamando directamente a HarvestNode.Harvest().

**Construcción:**
selección → ghost preview → validación de distancia/suelo/ocupación/soporte/materiales → confirmación → gasto atómico → modelo final y collider → audio/animación → guardado. Desmontaje autorizado y devolución sin duplicación; vistas previas y pieza terminada coinciden.

**Transformación:**
entrada → permiso de linaje/forma desbloqueada → comprobar espacio/cooldown → animación → swap de modelo/habilidades/cámara/colisiones → movimiento en forma nueva → reversión limpia. No se puede abrir inventario y conservar una forma prohibida para explotar colisiones.

**Defensa y curación:** input correcto del mismo frame, bloqueo frontal/mágico distinguible, parry con ventana legible, esquiva respetada, buff no aplicado si se falló, números verde/rojo basados solo en PV efectivos.

## 6. Artistas y contenido 3D, no solo primitivas

- Rig normalizado para humanoides y cuadrúpedos; clips de idle, caminar, correr, ataque, recibir daño, muerte, hablar, trabajar, recolectar, equipar, transformar.
- Prefabs con sockets de armas/manos, salida de proyectil, puntos para VFX, colliders separados del mesh, materiales/texturas auditados, LOD y perfiles de animación.
- Conservar licencias CC0/compatibles y referencias de autoría. No extraer assets de juegos comerciales o copiar criaturas exclusivas; Vhaldren tendrá identidades originales.
- La ballesta actual debe preferir el mesh animado que trae el personaje KayKit, en vez de superponer una primitiva de gran tamaño.
- El personaje actual tiene silueta low-poly con cabeza/capucha grande: para resolver calidad final hará falta **otro mesh o un rediseño artístico**, no basta con tocar el script de animación.

## 7. Orden de producción para tener un juego que SE PUEDA JUGAR

**Sprint 0: calidad de acciones / sesión actual**
- Compilar 016 en Unity, correr pruebas anteriores + nuevas de cola de disparos, recetas y bloqueo de minería.
- Confirmar modelo de ballesta, activar clips PickUp/Chop con menú `Vhaldren/Personaje/Preparar personaje KayKit`, testear E en piedra, reproducir dos flechas rápidas, matar/morir/reaparecer.
- Arreglar cualquier regresión P0 antes de añadir otro sistema.

**Sprint 1: primera región terrestre bonita y funcional**
- Terreno pequeño artesanal con iluminación/caminos/senderos, vegetación, aldea con 3 NPC, taller, mercado, entrada de una cueva y mazmorra corta.
- Un conjunto sólido de modelos humanoides y enemigo animal con 5–8 animaciones cada uno; no más cápsulas sin reacción.
- Un arco de progresión: material→herramienta→equipamiento→cueva→magia→guardián→recompensa→base.

**Sprint 2: expansión terrestre**
- Dos biomas, 4–6 arquetipos de cuevas, ciudad arcana y asentamiento mixto, comerciantes ambulantes, línea de vampiro/licántropo, primera transformación con mesh propio.
- Guardado persistente de mundo/personaje/base, NPC y comercio.

**Sprint 3+: Alto Reino e Infierno**
- Desbloquear solo cuando la Tierra ya sea divertida, guardable y estable. Empezar con una región contenida por reino, no un continente vacío.

## 8. Pruebas y criterios de aceptación

- 30/60/120 FPS; clics separados por 0,05/0,1/0,37/0,4/0,7 s; proyectiles simultáneos, clic al cambiar de arma, clic al morir.
- Recolección E sin herramienta en piedra/mineral = 0 unidades y causa visible; fabricación sin materiales = inventario inmutable; tras fabricar herramienta, cosecha y animación.
- 100/500 piezas construidas: no builds fantasma, colisiones correctas, no perdida de movimiento, sin spam de instanciaciones de preview ni fugas de materiales.
- Entornos interiores, cuevas estrechas, puertas, paredes, árbol delante de cámara y superficies inclinadas.
- No FPS/latencia privilegiados; tolerancia a inputs sin generar ataques extra por el buffer.
- Pruebas PlayMode/EditMode + Unity Profiler y revisión visual. **El estado de 016 no se declara validado hasta ejecutarlo.**

## Fuentes internas
- `docs/014-ESTUDIO-ENSHROUDED-COMUNIDAD.md`
- `docs/015-CONSTRUCCION-AUDITADA.md`
- `docs/013-AUDITORIA-COMBAT-VIDEOS.md`

## Límite de esta rama
El catálogo de 3 reinos, aldeas, arquetipos de cuevas y linajes es **datos y contrato de arquitectura**, no contenido 3D ya renderizado. La entrega de escenas, mallas, animaciones de transformación, NPC y comercio necesita otro trabajo material que debe ejecutarse y probarse en Unity.
