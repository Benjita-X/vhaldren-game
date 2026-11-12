# Supervivencia, inventario y construcción para Vhaldren

Fecha: 11 de noviembre de 2026. Diseño original y análisis de mecánicas públicas; no es un análisis del código interno de Minecraft ni de The Forest. Se localizaron las carpetas locales `.minecraft` y `.tlauncher`, pero no se descompilaron ni se copiaron sus recursos. La implementación concreta y sus límites se registran en ESTADO.md.

## Referencias y alcance de la evidencia

- Minecraft, controles oficiales: https://www.minecraft.net/en-us/article/minecraft-controls
- Minecraft, fabricación oficial: https://www.minecraft.net/en-us/article/how-craft
- Endnight, ficha oficial de The Forest en Steam: https://store.steampowered.com/app/242760/The_Forest/

Minecraft relaciona recoger recursos, seleccionar objetos, usarlos en el mundo y fabricar herramientas. La barra permite acceder a una parte del inventario sin abrir la mochila; la fabricación convierte materiales en nuevas capacidades. Las fuentes describen comportamiento visible, no estructuras de datos internas. La página de Endnight caracteriza The Forest como supervivencia en mundo abierto: las decisiones técnicas siguientes son propuestas para Vhaldren, no afirmaciones sobre su código.

La ficha de The Forest enlaza tala, campamento, fuego, comida, refugios y defensas. También establece una alternancia entre explorar/construir durante el día y defender la base de noche. Para Vhaldren, la implicación de diseño es que el refugio debe ofrecer una ventaja concreta y requerir mantenimiento o decisiones defensivas; una pared decorativa no cierra el ciclo de supervivencia. La vista previa, transacciones y conectores descritos abajo son nuestra propuesta de implementación.

## El ciclo de supervivencia

Explorar → localizar un recurso → elegir herramienta → obtener materiales → decidir qué transportar → fabricar/equipar → construir refugio → sobrevivir a una amenaza → explorar más lejos. Cada paso necesita consecuencias: una herramienta útil abre recursos; una mochila limitada obliga a elegir; un refugio reduce exposición; enemigos y distancia dan valor al regreso. Agregar hambre sin alimentos, obtención y recuperación solo añade un temporizador de castigo.

Primera versión recomendada: madera y piedra recolectables; espada, pico y hacha con funciones distintas; comida consumible; piezas de suelo y pared; recursos necesarios visibles antes de fabricar o colocar. Después: estaciones, durabilidad, almacenamiento en cofres y guardado. Temperatura, sed y deterioro deben esperar a que sus soluciones sean jugables.

## Inventario y barra

La barra de nueve casillas debe usar los mismos objetos que la mochila, sin duplicar cantidades. Una representación sencilla son 36 posiciones: nueve de acceso rápido y 27 de mochila. Cada posición guarda un identificador estable y una cantidad; las herramientas con estado individual necesitan además durabilidad e identidad de instancia. El nombre traducido y el icono pertenecen a la definición del objeto, no sirven como identificadores.

Reglas: ninguna cantidad negativa; herramientas no apilables; materiales con límite configurable; al recoger se completan pilas compatibles y luego espacios vacíos. Si no cabe todo, el resto permanece en el mundo. Mover a una casilla vacía transfiere; mover sobre la misma clase combina hasta el límite; mover sobre otra intercambia. La división debe conservar exactamente el total. Seleccionar una casilla vacía equivale a manos vacías.

Propuesta de controles: I abre mochila, 1–9 selecciona. Mantener E para la magia ya existente y la rueda para el zoom evita romper controles adquiridos. La UI debe mostrar nombre, cantidad, selección y uso del objeto. Abrir la mochila bloquea ataques y construcción; debe consumir el clic de cierre para que no golpee también el mundo. Perder foco o morir libera cualquier objeto arrastrado de forma determinista.

## Herramientas y combate

La espada aplica un ataque corto con alcance, arco y comprobación de obstáculos. El pico trabaja piedra; el hacha trabaja madera. Un clic pasa por un único enrutador de acciones: UI, construcción o herramienta, con prioridad explícita. No deben dispararse a la vez el hitscan antiguo y la espada. La magia conserva su proyectil visible y sus enfriamientos; seleccionar una herramienta no debe convertirla en un disparo invisible.

La recolección entrega recursos solo si el inventario los admite. Un nodo agotado no vuelve a conceder materiales. Los avisos deben explicar herramienta incorrecta, distancia, inventario lleno y espera; fallar no consume durabilidad ni materiales.

## Construcción de estructuras

Para Vhaldren conviene construir módulos de refugio en el mundo existente. Un terreno completamente voxelizado implicaría otra representación del mapa, generación de mallas por regiones, colisiones y persistencia; no es necesario para obtener un refugio construible.

Flujo propuesto: escoger receta → proyectar una vista previa → ajustar posición y rotación → mostrar coste y validez → confirmar → descontar recursos y crear la pieza. La vista previa no tiene colisión de juego y nunca participa en su propia prueba de obstrucción. Comprobar alcance, suelo, pendiente, volumen ocupado y paso libre desde el jugador. El color debe acompañarse de un motivo textual para accesibilidad.

Una construcción progresiva mediante planos puede añadirse después: primero se coloca un proyecto sin colisión sólida, luego se aportan materiales y finalmente aparece la estructura. Los recursos aportados pertenecen al proyecto; cancelar debe especificar devolución o pérdida. Esto permite cooperar sin transportar todo de golpe, pero exige persistir proyectos incompletos y controlar aportaciones simultáneas.

El encaje entre piezas debe usar conectores locales con posición, orientación y tipo compatible. Así una pared puede encajar en un suelo sin que toda la escena dependa de una cuadrícula global. El soporte estructural exige un grafo: retirar un soporte debe evaluar piezas dependientes antes de aplicar consecuencias. No afirmar estabilidad estructural si solo se comprueba que hay suelo bajo una pieza.

## Consistencia, guardado y escala

Fabricar y construir son transacciones: comprobar receta completa, espacio para resultado y validez del destino; preparar resultado; confirmar consumo y creación juntos, con recuperación si falla. Comprobar cada ingrediente y descontarlo inmediatamente produciría pérdidas parciales.

Guardar identificadores, cantidades, estado de herramientas y transformaciones de estructuras con versión de esquema. Escribir a temporal, conservar copia anterior y reemplazar al completar; cargar solo tras validar. El inventario en memoria no equivale a un sistema de guardado. En multijugador el servidor decide recursos, daños y colocación: la vista previa del cliente no concede permisos.

Escala: actualizar UI mediante cambios de estado; limitar búsquedas físicas al alcance de interacción; separar datos de objetos visuales; activar nodos cercanos por regiones; reutilizar efectos frecuentes. La agrupación de estructuras y las mallas combinadas necesitan mantener una identidad individual para daño, desmontaje y guardado. Medir antes de prometer miles de estructuras o enemigos.

## Comprobaciones pendientes del dueño

Sin ejecutar pruebas en esta pasada: probar inventario lleno, combinar al límite, intercambiar objetos, selección vacía, ataques al cerrar UI, muerte durante interacción, herramienta incorrecta, recursos insuficientes y colocación contra paredes o dentro del jugador. Comprobar que recoger, fabricar y construir conservan cantidades y que un clic no ejecuta dos acciones. Verificar visuales y controles a distintas resoluciones. Guardado, carga y multijugador requieren sus propios recorridos cuando se implementen.

## Estado de la implementación (11 de noviembre de 2026)

Implementado y compilado (sin jugar): mochila de 36 casillas con barra 1–9 compartida, fabricación de pico/hacha/espada, recursos de madera/piedra/bayas con la herramienta correcta, hambre con hoguera que cura, y construcción por piezas (suelo, pared, puerta, rampa, hoguera) en rejilla de 3 m con vista previa, motivo de rechazo, coste transaccional y desmontaje con devolución. Detalle y lista de pruebas en ESTADO.md.

Siguiente paso recomendado, en orden: guardado versionado de inventario y estructuras; enemigos que dañen estructuras de noche (la presión que da sentido al refugio); planos con aportes parciales para cooperativo; grafo de soporte; durabilidad de herramientas. Cada uno con su prueba jugando antes del siguiente.
