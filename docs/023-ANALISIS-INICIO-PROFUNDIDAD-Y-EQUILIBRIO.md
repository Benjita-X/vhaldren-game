# 023 — Análisis: el inicio del juego, la profundidad y el equilibrio

Fecha: 13 de noviembre de 2026 · Rama: `feature/018-consolidacion-pre-modelado` · Autor: Claude.

## Qué pidió el usuario

Que el juego no se sienta «básico, básico». Analizar la secuencia completa de una partida —sobre todo
**el inicio**— comparando con juegos que enganchan (LEGO Fortnite, y el resto del género de
supervivencia con construcción), para que al empezar uno: elija quién es, sepa qué hacer, tenga un
objetivo, quiera construir, y que explorar sea «qué bacán» y no «qué fome». También: equilibrio, una
recolección que se vea bien, más variedad y animaciones pulidas.

Regla que se mantiene: se toman **técnicas generales de diseño**, nunca historias, personajes, mapas ni
arte de otros juegos.

---

## 1. Cómo empieza Vhaldren hoy (auditoría honesta)

Recorrido real del ejecutable, minuto a minuto:

| Momento | Qué pasa | Problema |
|---|---|---|
| Menú | «Nueva partida» → lista de 8 razas con texto | Solo se elige **raza**: todas juegan igual y se ven igual (el pícaro encapuchado verde) |
| Segundo 0 en el mundo | Apareces en la plaza del Cruce, de día, sin contexto | **No hay historia ni motivo.** Nadie te habla. No sabes quién eres ni por qué estás ahí |
| Primeros 10 s | Aviso «F1: controles», barra con 9 habilidades y 3 hechizos | **Todo desbloqueado de golpe**: 18 hechizos, 6 escuelas, 8 razas, construcción, forja… abruma y quita la ilusión de conseguir cosas |
| Objetivo | Arriba a la derecha: «Refugio para los desplazados — acéptala en el tablón» | El primer objetivo es **entregar 6 de madera**: un recado, no una aventura |
| Recolectar | Esferas y cubos de colores; mantener E | Los recursos son **figuras primitivas**, sin golpe visible, sin trozos que salten, sin sonido de impacto propio |
| Construir | B abre el catálogo | No hay **razón** para construir: nada amenaza la base, el pueblo no crece, no desbloquea nada |
| Explorar | Campamentos de enemigos alrededor del cruce | **Nada que descubrir**: sin cofres, sin lugares secretos, sin vistas, sin personajes que rescatar |
| Progreso | Nivel de equipo (3 calidades), botín de materiales | Subir de nivel **no cambia cómo juegas** (no hay habilidades nuevas que ganar) |
| Jefe | Caballero de sangre en el altar | No se **ve** desde lejos ni se anuncia: no es una meta que motive desde el minuto 1 |

**Diagnóstico en una frase:** Vhaldren tiene muchos *sistemas* (magia, razas, construcción, misiones,
día/noche), pero le faltan las tres cosas que dan ganas de jugar: **identidad** (quién soy),
**dirección** (qué quiero conseguir) y **descubrimiento** (qué me espera allá).

---

## 2. Qué hacen bien los juegos que enganchan (técnicas generales)

Patrones repetidos en LEGO Fortnite, Valheim, V Rising, Enshrouded, Minecraft y similares. No se copia
ninguno: se extrae el *porqué* funciona.

1. **Eliges quién eres antes de empezar.** Aspecto y estilo de juego. Te identificas con tu personaje
   desde el primer segundo.
2. **Un arranque con algo en juego, en menos de un minuto.** Despiertas débil, en un lugar en ruinas, o
   llegas a un mundo desconocido. Hay una razón emocional para moverte (sobrevivir, vengarte, salvar a
   alguien).
3. **Primeros pasos guiados con metas diminutas y premios inmediatos.** Un diario con pasos cortos:
   «recoge ramas» → «haz una herramienta» → «enciende un fuego». Cada paso tarda 30-90 s y da algo nuevo.
4. **Una casa que crece y que se nota.** El fuego, el banco de trabajo o el corazón del castillo son el
   centro. Construir desbloquea más cosas y **atrae personajes** (aldeanos, artesanos) que dan misiones y
   oficios. El pueblo sube de nivel y se ve más grande.
5. **Una meta grande visible desde lejos.** Un jefe en el mapa, un altar, una torre, un rayo en el cielo.
   Derrotarlo abre la siguiente región o tecnología. Siempre sabes «lo próximo grande».
6. **Explorar siempre paga.** Cada 30-60 s de caminata aparece algo: un cofre, una ruina, un personaje
   atrapado, una cueva, una vista que revela el mapa, una piedra con historia. Las cosas interesantes se
   ven de lejos (humo, luces, columnas) y te atraen.
7. **Recolectar se siente bien.** Golpe con animación de herramienta, el árbol tiembla, saltan astillas,
   sonido seco, número «+3 madera» que vuela hacia ti, el último golpe derriba el árbol. Mejores
   herramientas = más rápido y nuevos materiales.
8. **Progreso por regiones.** Cada zona tiene materiales, enemigos y peligros propios; necesitas
   herramientas o equipo de la zona anterior para aprovecharla.
9. **Se desbloquea poco a poco.** Al principio pocas teclas; cada hora una herramienta, hechizo o pieza
   nueva. La sensación de crecer es el motor del juego.
10. **Jugar juntos.** Repartirse tareas (uno construye, otro caza), revivir al compañero, base común.
    (Para Vhaldren es clave: es para jugar en familia.)

---

## 3. Propuesta: los primeros 20 minutos de Vhaldren

Historia propia (borrador para que el usuario la cambie a su gusto):

> **El Velo carmesí.** El Cruce era el lugar donde se encontraban humanos, nacidos de magia y las
> estirpes de la noche. Una noche de luna roja, el **Caballero de sangre** tomó el altar del norte y
> su esencia corrompió la tierra. Tú eras parte de la caravana que huía. Despiertas entre los restos,
> marcado por la esencia: puedes absorber el poder de los caídos. Los supervivientes están dispersos.
> Si reúnes al pueblo y te haces fuerte, podrás romper el altar.

### Minuto a minuto

| Tiempo | Qué vive el jugador | Qué enseña / qué siente |
|---|---|---|
| 0:00 | **Elige su linaje** (las 8 razas actuales) **y su vocación**: *Tirador* (ballesta), *Guerrero* (espada y escudo) o *Arcanista* (bastón y una escuela de magia). Cada vocación usa un **modelo KayKit distinto** (pícaro, caballero/bárbaro, maga), con su color de linaje | Identidad. «Este soy yo» |
| 0:30 | **Introducción de 30 s**: 3-4 viñetas con texto y cámara sobrevolando el Cruce en llamas, la luna roja y el altar con un rayo carmesí | Motivo y meta grande |
| 1:00 | Despiertas junto a un carro volcado, de noche cayendo al alba. Paso 1 del diario: **«Recupera tu arma»** (cofre a 6 m) | Moverse. Primer premio |
| 2:00 | Un bandido ataca a **Maela, la carpintera**. Paso 2: **«Defiéndela»** (enemigo débil, 2-3 golpes) | Combatir sin presión. Héroe desde el minuto 2 |
| 3:00 | Maela: «Necesitamos un fuego antes de la noche». Paso 3: **recoger ramas y piedras** con recolección vistosa | Recolectar se siente bien |
| 5:00 | Paso 4: **encender el Fuego del refugio** (construcción). Pasa a ser punto de reaparición y zona segura | Primera construcción con sentido. «Esta es mi base» |
| 7:00 | Paso 5: **banco de trabajo** → fabricar el **pico**. Maela se instala: el pueblo pasa a **nivel 1** y se desbloquean muros y techos | El pueblo crece y se nota |
| 9:00 | Desde la base se ve **humo** al este: el campamento de bandidos. Paso 6: **rescatar a Toren, el herrero** (enjaulado) | Explorar con un porqué |
| 12:00 | Toren monta la **forja**. Primera mejora de arma: la ballesta cambia de color y brilla (ya existe) | Progreso visible |
| 14:00 | Paso 7: **debilita a un enemigo y absorbe su esencia** (H). Aprendes tu primera escuela extra | Mecánica única de Vhaldren |
| 16:00 | Se marca en el mapa la **cueva del bosque** y la **torre de la academia** (rescatar a la maga Iria, que enseña magia) | Varios caminos posibles |
| 20:00 | El diario muestra la meta grande: **«Prepárate para el altar»** (nivel de equipo recomendado). El rayo carmesí siempre visible al norte | Rumbo a largo plazo |

Después del minuto 20, el bucle: **explorar → recolectar → construir y mejorar → rescatar aldeanos →
desbloquear → jefe de región → nueva región**.

### Qué cambia respecto a hoy
- **Desbloqueo progresivo**: se empieza con el arma de la vocación, esquiva y una habilidad. La espada,
  el pulso, la escarcha y cada escuela se **ganan** (aldeanos, esencias, jefes). La barra nueva ya muestra
  huecos vacíos o bloqueados.
- **El tablón pasa a ser el diario**: los pasos se siguen solos (sin tener que ir a aceptarlos) y la línea
  de misión arriba a la derecha dice siempre el siguiente paso concreto.
- **Aldeanos que se rescatan** (carpintera, herrero, maga, cazadora, alquimista): cada uno abre un oficio y
  misiones propias. El pueblo sube de nivel al construir y al traer gente.

---

## 4. Explorar «qué bacán»: el mundo

Hoy el mapa es un cruce con campamentos. Para que explorar pague:

- **Un punto de interés cada 30-60 s de caminata**: cofres (comunes, raros, épicos), ruinas con un
  pequeño acertijo, jaulas con aldeanos, cuevas cortas, piedras de historia, santuarios de esencia,
  miradores que revelan el mapa.
- **Faros visuales**: humo de campamentos, luces de la academia, el rayo del altar, árboles gigantes.
  Si se ve, se puede ir.
- **Tres zonas con identidad** alrededor del Cruce (ya existen en bruto): *Bosque oscuro* (lobos, cueva,
  madera noble), *Ruinas* (centinelas, cristal, la academia), *Altar de sangre* (culto, jefe). Cada una
  con un material propio que hace falta para la siguiente mejora.
- **Mapa con niebla** que se descubre caminando, con iconos de lo visto.
- **Eventos**: caravana atacada, Luna carmesí (ya existe: más enemigos y mejores esencias), mercader
  ambulante.

## 5. Recolección que se vea bien

1. Sustituir esferas y cubos por **árboles, rocas y vetas** de verdad (KayKit tiene paquetes CC0 de
   naturaleza y recursos; o modelos propios de Blender, según el contrato de arte de la 018).
2. **Animación por herramienta**: hacha para árboles, pico para rocas, recoger con la mano para plantas
   (hoy el controlador ya tiene el parámetro `Harvest`; los clips de KayKit «Chop» y «Interact» sirven).
3. **Respuesta en cada golpe**: el recurso tiembla, saltan astillas o chispas, sonido propio, el número
   «+3 Madera» vuela al jugador, el último golpe derriba el árbol con un pequeño temblor de cámara.
4. **Niveles de herramienta**: mano → pico improvisado → pico de piedra → herramientas de hierro (más
   rápidas y abren minerales nuevos).

## 6. Profundidad y variedad

Ordenado por impacto para el esfuerzo:

1. **Vocación al inicio** (Tirador, Guerrero, Arcanista) con su modelo, arma y una escuela.
2. **Desbloqueos que cambian el juego**: árbol pequeño por escuela (3-4 mejoras por hechizo) y por arma.
3. **Aldeanos y nivel del pueblo** (oficios, misiones, defensa de la base en Luna carmesí).
4. **Botín con rarezas** (común, raro, épico) con un efecto especial en los épicos.
5. **Comida** que da ventajas temporales (vida, resistencia, regeneración).
6. **Jefes con fases** y recompensa única (cada jefe enseña algo: un hechizo, un poder, una montura).
7. **Más enemigos por zona** reutilizando modelos KayKit con armas y colores distintos (ya hay base).

## 7. Animación pulida

- Ya hay 180 animaciones Mixamo auditadas (docs/020) para un personaje Humanoid futuro; mientras tanto
  KayKit trae clips de ataque, bloqueo, golpe recibido, recoger, interactuar, sentarse y animar.
- Prioridad: **recolectar**, **interactuar** (abrir cofre, hablar), **golpe recibido** del jugador,
  **transiciones** al cambiar de arma y una **pose de victoria** al vencer a un jefe.
- Animación de los aldeanos (trabajar en su puesto) para que el pueblo se vea vivo.

## 8. Equilibrio: cifras objetivo

Medidas de hoy (código): espada 35 de daño; saqueador 60 de vida (**2 golpes**); jugador 150 de vida;
nodo de madera: 3 por recogida en 0,8 s, 12 en total; primera misión: 6 de madera (≈ 2 recogidas).

| Qué | Hoy | Objetivo para que «se sienta bien» |
|---|---|---|
| Enemigo común | 2 golpes de espada | 3-4 golpes (4-6 s): da tiempo a esquivar y leer su ataque |
| Élite | — | 12-18 s de combate, un ataque especial claro |
| Jefe | — | 90-150 s, 2-3 fases, avisos visibles |
| Golpes de saqueador que aguanta el jugador | ≈ 17 (9 de daño contra 150 de vida) | 6-9: que importe esquivar y defender |
| Recoger un nodo | 4 recogidas de 0,8 s | 3-5 golpes con animación (≈ 3-4 s) |
| Primera construcción | Cuando el jugador quiera | Antes del minuto 5, guiada |
| Primera herramienta | Libre | Antes del minuto 8 |
| Primer jefe | Accesible desde el inicio | Hacia el minuto 45-60, con nivel recomendado |
| Desbloqueo nuevo | Todo al inicio | Algo nuevo cada 5-10 min la primera hora |

Método: añadir un registro de partida (tiempo hasta cada paso, muertes, daño recibido) y ajustar con
partidas reales de la familia.

---

## 9. Plan por fases (propuesto)

| Fase | Contenido | Resultado visible |
|---|---|---|
| **A. Primeros 20 minutos** | Vocación + modelo, introducción, diario guiado, rescate de la carpintera y el herrero, Fuego del refugio, desbloqueo progresivo, rayo del altar visible | Empezar a jugar con historia, objetivo y ganas |
| **B. Recolección y mundo** | Árboles y rocas reales, animación y respuesta de golpe, cofres, 10-15 puntos de interés, mapa con niebla | Explorar y recolectar «bacán» |
| **C. Pueblo vivo** | Nivel del pueblo, aldeanos con oficio, defensa en Luna carmesí, comida | Razón para construir |
| **D. Profundidad** | Mejoras por escuela y arma, botín con rareza, jefes con fases, más enemigos | Muchas horas de progreso |
| **E. Equilibrio** | Registro de partida, cifras objetivo, pruebas con la familia | Que se sienta justo y emocionante |
| (paralelo) | Multijugador cooperativo | Jugar con hermanos y primos |

**Recomendación:** empezar por la **fase A**, porque es lo que el usuario más pidió (el inicio) y
reutiliza casi todo lo que ya existe (razas, modelos KayKit, misiones, construcción, forja, esencia,
barra nueva).
