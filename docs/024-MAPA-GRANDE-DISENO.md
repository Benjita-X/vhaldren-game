# 024 — Mapa grande: análisis de referencia y diseño original

Fecha: 18 de noviembre de 2026 · Carpeta: `vhaldren-017` · Autor: Claude (subagente de diseño del mapa).

Entregables:
- `Assets/Art/Mundo/diseno_mapa.json`: datos que lee el generador del mapa con `JsonUtility`. Validado: esquema exacto, sin nulos ni diccionarios, todos los ids referenciados existen.
- Este documento: lo que se aprendió de la extracción, cómo interpretar el JSON y el diseño completo.

Regla que se mantiene (docs 016, 019 y 023): la extracción de V Rising solo se usó para sacar **hechos y números**. No se copió ningún archivo, textura, malla, nombre de lugar, personaje ni texto. Todos los nombres, lugares, jefes y frases de este diseño son propios de Vhaldren.

---

## 1. Análisis de la referencia

Carpeta analizada: `Documents\Codex\2026-11-12\ing\outputs\auditoria-v-rising`, extracción estática de la versión 1.1.13.0 (Unity 2022.3, DOTS). Se leyeron `AUDITORIA.md`, `LEEME.md`, `resumen.json`, `configuraciones.json`, `texturas.json` (11.194 texturas), `modelos.json` (13.133 mallas), `textos.json` (8.379 textos), `simbolos-il2cpp.txt` (218.283 cadenas) y el mapa de `mapas/`. Los OBJ no se abrieron: bastó con el índice de `modelos.json`.

### 1.1 Datos encontrados (con su fuente)

| Tema | Dato | Fuente en la extracción |
|---|---|---|
| Motor gráfico | HDRP: aparecen `HDRenderPipeline`, `HDCamera` y `HDAdditionalCameraData` | `simbolos-il2cpp.txt` |
| Mapa del mundo | Una sola textura de mapa de **6080 × 6080 px**. Las dos zonas de inicio tienen mapas aparte (512×1024 y 512×512) | `mapas/`, `texturas.json`, AUDITORIA §11 |
| Escala en metros | **No aparece.** La propia auditoría lo advierte (§11). Estimación propia: los caminos miden unos 8 px en el mapa; si un camino real mide 4-5 m, salen **≈ 0,55-0,65 m/px**. Eso da un mundo de 3,3-4 km de lado, con la tierra jugable en unos 2,9-3,4 km | Medido sobre `mapas/ZoneMap_Wilderness…png`, reducida en el scratchpad, sin copiarla |
| Regiones | **9 regiones con nombre** (una de ellas dividida en 3 subzonas y otra en 2), más 2 zonas de inicio. Hay **17 textos de portales de viaje** por región, de 1 a 3 por región | `textos.json`, índices 8138-8366 |
| Forma del mundo | Mancha irregular: la región de inicio, al sur, ocupa ≈ 25 % de la tierra y la central ≈ 20 %. Las otras 7 rodean el centro, con 6-9 % cada una. Las más duras quedan en el borde norte, el oeste y el este. Entre regiones hay bandas de montaña infranqueable con 1-3 pasos por donde va el camino | Observación del mapa |
| Relieve | Dentro de cada región: mesetas y terrazas de 60-250 m cerradas por escalones de acantilado; pasillos bajos con los caminos; charcas pequeñas; grupos de rocas que bloquean el paso. Hay una colina-fortaleza central de anillos concéntricos | Observación del mapa |
| Acantilados | Son **mallas modulares**, no terreno: hay piezas de esquina, esquina interior, tramo medio e inserto por bioma (257 mallas de acantilado) | `texturas.json` y `modelos.json` (nombres por categoría) |
| Texturas de suelo | Casi todas de **1024²**. Solo hay un juego de 2048² en todo el juego (una pieza única). Esquema PBR de 3 mapas: color, normal y máscara (2206 / 1952 / 1862 texturas con esos sufijos). La misma textura base se **recolorea por bioma** (variantes seca, otoñal y maldita de la misma hierba) | `texturas.json` |
| Resoluciones más frecuentes | 512² (3117), 256² (2136), 128² (1977) y 1024² (1340). Detalle del suelo: 256². Mechones de hierba: tiras de 512×128 | `texturas.json` |
| Estilo | Color pintado a mano sobre PBR (HDRP): formas simples con mucho color y poco detalle geométrico | Deducción a partir de las texturas y las mallas |
| Polígonos | Árboles: mediana de 540 vértices (1073 mallas). Rocas: 493 (1054 mallas). LOD0/1/2: 854 / 207 / 17. Poquísimos LOD: la cámara cenital no los necesita | `modelos.json` |
| Vegetación y detalle | Sistema propio por trozos con **densidad**, **distancia de dibujo** y **distancia de fundido** (nombres `FluffDensity`, `FluffDrawDistance`, `FluffFadeDistance`). Los valores no están exportados | `simbolos-il2cpp.txt` |
| Biomas | Polígonos de bioma con **distancia de mezcla** en el borde (`BiomePolygon`, `BlendDistBiomePolygon`). Al cambiar de región sale un aviso con su nombre (`CurrentWorldRegionChangedEvent`, `CalcMapRegionNameAlpha`) | `simbolos-il2cpp.txt` |
| Niebla | Niebla volumétrica y **niebla de altura** de HDRP. Además, la forma de murciélago tiene una niebla propia (`BatFormFog`) | `simbolos-il2cpp.txt` |
| Cámara | Existen inclinación por defecto, distancia de zoom por defecto, FOV, inclinación y zoom mínimos y máximos en modo construcción, **pasos de inclinación**, giro rápido a izquierda y derecha, bloqueo de giro, **zonas que cambian el zoom**, **zoom distinto al montar** (`MountZoomModifierSystem`) y fundido de los objetos que tapan al personaje (`CameraFadingRadiusNear/Far`). **No hay valores numéricos**: viven en datos DOTS sin decodificar. El minimapa no gira (`LockMinimapRotation=true`) | `simbolos-il2cpp.txt`; `configuraciones.json` › ClientSettings |
| Monturas | Solo **caballos**, normales y vampíricos. Stats por montura: velocidad máxima, aceleración, giro y velocidad de galope con su multiplicador. Comida, salud y resurrección. Los mejoran la silla (velocidad y aceleración), la comida y el establo. Se doman canalizando un hechizo y solo puede haber un compañero activo. Al recibir un golpe hay probabilidad de caer. Con ciertos objetos no se puede montar. **No hay velocidades numéricas** | `simbolos-il2cpp.txt` (componentes de montura); `textos.json` 624-686, 2338, 3676, 4018, 4048, 6254-6281 |
| Vuelo | No hay monturas voladoras. Se vuela con la forma de murciélago, sobre terreno infranqueable; **no se puede aterrizar en territorio hostil**, ni dentro de edificios ni fuera del mapa | AUDITORIA §5; `textos.json` 656 |
| Progresión | Nivel de equipo de **30 a 90 en 7 tramos** de 10. La diferencia entre el nivel de tu equipo y el del enemigo cambia el daño en PvE | `configuraciones.json` (presets Level30-90); `textos.json` 3994, 6439-6452, 6783-6788 |
| Dificultad | En «Brutal», los enemigos hacen ×1,4 de daño; los jefes tienen ×1,25 de vida, ×1,7 de daño y +3 niveles | `configuraciones.json` › Difficulty_Brutal |
| Tiempo | El día dura 1080 s (luz de 09:00 a 17:00). Luna de sangre cada 10-18 días, con +20 % | `configuraciones.json` › ServerGameSettings.GameTimeModifiers |

### 1.2 Lo que no está y se propone (valores del género)

- **Cámara**: Vhaldren ya usa inclinación de 54°, FOV vertical de 40° y distancia de 22 m (entre 14 y 30). Se mantienen. La distancia máxima sube a **36 m** para el zoom al montar y al volar, siguiendo la lección de `MountZoomModifierSystem`. Con 54° / 40° / 22 m y pantalla 16:9 se ven unos **21 m de fondo por 24-41 m de ancho** (≈ 700 m²). A 36 m, unos 1900 m². Es decir, en cada momento se ve entre un 0,02 y un 0,05 % del mapa, y eso ya da sensación de inmensidad.
- **Velocidades**: las fija el líder: 6 m/s a pie, 14 en montura terrestre y 24 volando. Aquí varían un poco por montura (12 a 16 en tierra, 24 a 26 en el aire).
- **Densidad de texels del suelo**: a 22 m, 1920 px cubren unos 28 m de ancho, es decir **≈ 67 px/m** en pantalla, y ≈ 110 px/m con el zoom mínimo. Una textura de 1024 que se repite cada **6 m** da 170 px/m: siempre más de un texel por píxel. Se recomienda repetición de **6 m** en el suelo, **4 m** en caminos y adoquín, y **10-12 m** en roca y acantilados (con el `Vhaldren/Triplanar` que ya existe). Para que no se note la repetición, añadir variación de color a gran escala con ruido.
- **Capas de terreno**: V Rising no usa el Terrain de Unity. El generador de Vhaldren (`Assets/Editor/Mundo/MundoBuilder.Terreno.cs`) trae **12 capas**, y el JSON usa solo esas o sus sinónimos. Cada región usa 3-4; las pendientes llevan roca y los caminos tierra. En el pipeline integrado, cada 4 capas cuestan una pasada más. Con un único Terrain son 3 pasadas: aceptable en la GTX 1650 con la cámara cenital, pero hay que medirlo. Si va justo, es mejor dividir en 4×4 teselas de 512 m con 8 capas o menos cada una.

### 1.3 Lecciones aplicadas al diseño

1. **Regiones separadas por cordilleras con pocos pasos.** Las regiones del principio son abiertas (pradera, bosque, pantano y costa). A partir del nivel 18, cada región está detrás de una cresta con 1-2 pasos. Así la progresión se lee en el mapa.
2. **La dificultad crece con la distancia**, sobre todo la distancia por camino. Las regiones más duras ocupan las esquinas del norte.
3. **El relieve no se siente plano**: terrazas (rugosidad alta = escalones de 6-8 m), una meseta a 110 m con un cañón hasta el nivel del agua, cumbres de 230-255 m y un volcán.
4. **Biomas con mezcla en el borde y aviso del nombre al entrar** en cada región.
5. **Pocas texturas y muy reutilizadas**: 1024² recoloreadas por región, y mallas sencillas. Es justo lo que pide una GTX 1650.
6. **Monturas con stats** (velocidad, giro, aceleración) que se mejoran. Hay **reglas de vuelo**: no se aterriza en campamentos ni en recintos de jefe, y no se entra volando en cuevas.
7. **Una piedra de viaje por región**, 11 en total.

---

## 2. Cómo leer el JSON (contrato para el generador)

Coordenadas: X va al este y Z al norte, de -1024 a 1024. Y es la altura en metros (de 0 a 260). El agua es un plano único a 18 m.

Ya se comprobó contra el generador (`Assets/Editor/Mundo/MundoDiseno.cs` y `MundoTerreno.cs`): las clases coinciden con el JSON (`nivel` y `cantidad` son enteros; el resto, decimales) y el generador ya sigue estas reglas.

| Campo | Significado propuesto |
|---|---|
| `regiones[].centro/radio` | La región de un punto es la que tiene menor `distancia(punto, centro) / radio`. Las alturas, texturas y niebla se mezclan en una franja de 40-60 m en el borde |
| `alturaBase` | Altura media del suelo de la región, en metros |
| `relieve` | Amplitud del ruido en metros (±relieve/2 alrededor de la base) |
| `rugosidad` | De 0 a 1. Por debajo de 0,4: colinas suaves. Por encima de 0,5: terrazas con escalones de 6-8 m y bordes de acantilado. A 0,7 o más: terreno muy roto |
| `nivel` | Nivel recomendado al entrar. Los campamentos llevan su propio nivel |
| `colorNiebla`, `densidadNiebla` | Pensado para `FogMode.ExponentialSquared`. Con 0,016 se tapa ≈ 20 % a 30 m. Al volar conviene usar la mitad de la densidad |
| `texturas` | Ids lógicos de capa. El primero es el dominante |
| `densidadArboles` | **Árboles por hectárea**. Se calcula un total de ≈ 8.200 árboles en el mapa, ya restando agua, caminos y pendientes |
| `arboles` | Ids lógicos de árbol |
| `cordilleras` | `altura`: altura **absoluta** de la cresta. `anchura`: anchura total de la base. Se aplica `max(terreno, perfil)` con caída suave hasta anchura/2. Las cordilleras de dos puntos y base ancha son picos: Aguja del Alba (255 m), volcán Brasanegra (245 m), Pico Albor (230 m) y el promontorio del Peñón del Grifo (58 m, con base ancha para que se pueda subir a pie) |
| `pasos` | Están exactamente sobre una cresta. Ahí se baja la cordillera hasta el terreno vecino más 6-10 m, en un corredor de `anchura` metros con rampa. El Paso del Yunque sube del llano (32 m) a la meseta (110 m): mejor hacerlo en zigzag |
| `rios` | Lecho a `nivelAgua - profundidad`. El generador baja el terreno hasta unos 19,5 m en una franja de 2-4 veces la `anchura` para formar un valle. En la meseta (base de 110 m) el valle es estrecho y con paredes: **ese es el Cañón de la Herida**. Cada río nace en una ladera (ahí va una cascada decorativa) o sale de un lago, y termina en un lago o en el mar |
| `lagos` | Círculo con el lecho a `nivelAgua - profundidad` y orillas suaves. **Los círculos que pasan del borde sur forman el mar** (el generador, además, hunde por su cuenta el borde costero). Solo hay lagos en regiones bajas (base de 45 m o menos), porque el plano de agua es único |
| `caminos` | Polilíneas con anchura en metros. Donde un camino cruza un río va un puente o un vado (hay 7 cruces). Se recomienda curvar ±10-15 m con ruido para que no sean rectos |
| `lugares` | Aplanar un círculo de 12-30 m (más grande en los poblados) y subirlo como mínimo a nivelAgua + 2. Los secretos llevan `secreto: true` y no se marcan en el mapa hasta descubrirse. Los lugares de cima (trono, caldera, plumas) y de cornisa están a propósito en laderas de picos o crestas |
| `campamentos` | `tipos` usa los arquetipos de `EnemyArchetypes`. `cantidad` reparte esos tipos. `nivel` sustituye al nivel base del arquetipo |
| `jefes` | `vida` y `dano` son la base **antes** del multiplicador por nivel (4 % por nivel, doc 019). La escala sube de 600 a 3200 para acercarse a los 90-150 s de combate del doc 023. El Caballero de sangre pasa de 320 a 1800: hay que ajustarlo con partidas |
| `monturas` | `velocidad` en m/s. Las que vuelan no pueden aterrizar dentro de campamentos ni de recintos de jefe, ni entrar en cuevas, y se bajan si cargan ciertos objetos de misión |
| `npcs.poblado` | Id de un lugar de tipo `poblado` o `puerto` |

**Texturas**: solo se usan capas del catálogo del generador o sus sinónimos: `pasto`, `pasto_seco`, `musgo_bosque`, `hojarasca`, `tierra` (→ `tierra_camino`), `roca` (→ `roca_acantilado`), `barro` (→ `barro_pantano`), `arena`, `tierra_roja`, `grava`, `nieve` y `ceniza` (→ `ceniza_volcanica`). Las identidades que aún no tienen capa propia se resuelven con tinte: la carmesí usa `tierra_roja`, las ruinas usan `grava` (adoquín) y las cumbres usan `nieve` y `roca`. Si más adelante se añaden `hierba_carmesi`, `hielo` y `basalto` al generador, basta con ponerlas en la lista de la región.

**Árboles (11 ids)**: `pino_a`, `pino_b`, `pino_nevado`, `roble_a`, `roble_b`, `abedul`, `sauce`, `cipres`, `arbol_muerto`, `arbol_carmesi`, `arbol_calcinado`. Si falta el modelo de alguno, el generador usa el más parecido.

**Ajustes de terreno sugeridos para la GTX 1650**: mapa de alturas de 2049 (1 m por muestra; hace falta para los acantilados y el cañón); mapa de capas de 1024; `basemapDistance` de unos 150 m; `heightmapPixelError` 5; árboles con instancias y billboard a partir de 80 m; hierba con resolución de detalle 1024, 32 por parche, `detailObjectDistance` 60 m (con la cámara cenital no se ve más allá de unos 45 m) y densidad 0,5.

---

## 3. El diseño: Vhaldren, el mundo del Lucero caído

### 3.1 Historia que da forma al mapa

Lucifer, el **Lucero**, cayó del cielo hace mil años en la **Corona del Lucero**, las cumbres del noroeste. Ahora despierta otra vez, con las alas rotas, en el **Cráter del Despertar**, junto al **Cruce**. El rayo carmesí del **Altar del Norte**, donde el **Caballero de sangre** tomó el poder la noche de la luna roja, se ve desde todo el mapa: es la primera meta grande. Al vencerlo, el Lucero recupera **la primera ala**, y con ella puede montar bestias voladoras: primero el **grifo** del Peñón y después el **fénix** del volcán. El final está en el **Trono del Lucero**, en la cima más alta. Allí espera **Vaelthar, el Heraldo del Alba**, quien lo arrojó del cielo. Al vencerlo, el Lucero recupera sus **alas completas**.

Las **plumas** que hay en los secretos (negras, blanca y de alba) sirven para enganchar las habilidades nuevas del Lucero: planeo, impulso alado, alas completas. El diseño de esas habilidades queda para otro documento.

### 3.2 Esquema (norte arriba; cada carácter son unos 50 m)

```
z=+1024 ┌──────────────────────────────────────────────────────────┐
        │CORONA ▲Aguja  ╲      │           │            ▲Volcán   │
        │DEL LUCERO(46)  ╲Esc. │  TIERRA   │  TIERRAS DE          │
        │   ╲  PICOS DE   ╲    │  CARMESÍ  │  BRASANEGRA (38)     │
        │    ╲ ALBOR (32) ▲Pico│  (26) ✝Altar│                    │
        │~~Sierra Albor~~~~╳~~~~~Muralla del Juramento~~╳~~Muro del Humo~~│
        │ RUINAS DE  ⌂Campana │        ‖Paso         │ MESETA DEL    │
        │ VELMORA (22)  Sierra│  LLANOS DEL CRUCE (1)│ YUNQUE (18)   │
        │  ✝Catedral  de Velmora   ★ EL CRUCE     Cresta  ≈cañón≈  │
        │~~~~~~~~~~~~~~~~~╲    │      ○Lago Espejo  │ Escarpe del Sur │
        │  BOSQUE DE       ╲   │                    │  MARJAL DE LOS  │
        │  HONDANIEBLA (5) ○   │                    │  AHOGADOS (10) ○│
        │        COSTA DE LAS VELAS ROTAS (14)  ⌂Puerto  Faro ◆Peñón  │
z=-1024 │≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈ MAR DEL SUR ≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈│
        └──────────────────────────────────────────────────────────┘
        x=-1024                                              x=+1024
```

### 3.3 Las diez regiones

| Región (id) | Bioma | Centro (x, z) · radio | Nivel | Distancia al Cruce | Relieve | Niebla | Enemigos | Jefe | Montura |
|---|---|---|---|---|---|---|---|---|---|
| Llanos del Cruce (`llanos_cruce`) | pradera | (0, 0) · 320 | 1 | 0 m | Base 32 m, terrazas suaves | Dorada, muy leve | Saqueador, ballestero, lobo | Gorran el Desollador | — |
| Bosque de Hondaniebla (`bosque_hondaniebla`) | bosque | (-450, -350) · 300 | 5 | 560 m | Base 40, colinas escalonadas | Gris azulada, densa | Lobo, saqueador, ballestero | Ysolde, la Cazadora de Bruma | Lobo de bruma |
| Marjal de los Ahogados (`marjal_ahogados`) | pantano | (470, -390) · 300 | 10 | 600 m | Base 22, a ras del agua | Verde enfermiza, la más densa | Esqueleto, sanadora, acólito | Madre Ciénaga | — |
| Costa de las Velas Rotas (`costa_velas_rotas`) | costa | (0, -760) · 330 | 14 | 740 m | Base 26, acantilados bajos al mar | Bruma marina | Saqueador, ballestero, bruto, esqueleto | Capitán Varek Sal-Negra | Grifo (Peñón) |
| Meseta del Yunque (`meseta_yunque`) | meseta | (740, 130) · 290 | 18 | 755 m | **Altiplano a 110 m** con el Cañón de la Herida | Polvo ocre | Bruto, saqueador, ballestero | Torvald Rompeyunques | León de las cumbres |
| Ruinas de Velmora (`ruinas_velmora`) | ruinas | (-760, 190) · 280 | 22 | 790 m | Base 55, colinas góticas | Gris violácea | Esqueleto, centinela rúnico, acólito, sanadora | El Abad Hueco | — |
| Tierra Carmesí (`tierra_carmesi`) | carmesí | (0, 780) · 250 | 26 | 800 m (890 por el camino) | Base 70, valle cerrado | Roja | Acólito, sanadora, esqueleto, centinela | **Caballero de sangre** | Corcel infernal |
| Picos de Albor (`picos_albor`) | nieve | (-560, 650) · 280 | 32 | 875 m (1110 por el camino) | Base 120, cumbres de 230 m | Blanca azulada | Lobo, bruto, centinela | Hrimvald, Jarl de la Escarcha | Oso acorazado |
| Tierras de Brasanegra (`brasanegra`) | ceniza | (700, 760) · 290 | 38 | 1050 m (1250 por el camino) | Base 90, volcán de 245 m | Humo pardo | Acólito, bruto, centinela, saqueador | Ignaro, Sumo Sacerdote de Ceniza | Fénix de ceniza |
| Corona del Lucero (`corona_lucero`) | cumbres | (-850, 890) · 180 | 46 | 1245 m (1680 por el camino) | Base 170, **cima de 255 m** | Azul noche, clara | Centinela, sanadora, esqueleto | Vaelthar, Heraldo del Alba | — |

Superficie (8 m por celda, sin agua): llanos 49 ha, bosque 57, marjal 51, costa 30, meseta 40, ruinas 36, carmesí 33, picos 35, Brasanegra 41 y Corona 11. Agua dentro del mapa: 36 ha.

**Progresión.** Nivel 1-5 en los llanos → bosque (5) y marjal (10), ambos abiertos → costa (14) → meseta (18) por el Paso del Yunque y ruinas (22) por el Paso de los Cuervos → **Caballero de sangre** (26) tras la Muralla, por el único Paso del Juramento → picos (32) por el Paso de la Escarcha o el del Lobo Blanco, y **grifo** (nivel 30, de vuelta en la costa) → Brasanegra (38) por el Paso de las Brasas o el del Humo, y **fénix** (42) → Corona (46) por la estrecha Escalera del Lucero (10 m de ancho) o volando. Esta escala es la continuación de la actual: los enemigos van del nivel 5 al 20 y el jefe es 26, como el Caballero hoy.

### 3.4 Cordilleras y pasos

| Cordillera | Separa | Cresta | Pasos |
|---|---|---|---|
| Muralla del Juramento | Llanos ↔ Carmesí | 165 m | Paso del Juramento (40, 432), el único camino al altar |
| Sierra de Velmora | Llanos y bosque ↔ ruinas | 140 m | Paso de los Cuervos (-450, 110) y Desfiladero del Ahorcado (-640, -120) |
| Cresta del Yunque | Llanos ↔ meseta | 150 m | Paso del Yunque (420, 120), en rampa |
| Escarpe del Sur | Marjal ↔ meseta | 135 m | Cuesta de los Carros (880, -280). El hueco entre esta cresta y la del Yunque es la **boca del cañón** |
| Dientes de Hierro | Carmesí ↔ picos | 175 m | Paso de la Escarcha (-300, 620) |
| Sierra Albor | Ruinas ↔ picos | 160 m | Paso del Lobo Blanco (-620, 380) |
| Cresta de las Brasas | Carmesí ↔ Brasanegra | 175 m | Paso de las Brasas (330, 640) |
| Muro del Humo | Meseta ↔ Brasanegra | 150 m | Paso del Humo (720, 450) |
| Escalera del Lucero | Picos ↔ Corona | 230 m | Escalera del Lucero (-737, 796), de 10 m |
| Aguja del Alba / Volcán Brasanegra / Pico Albor | Picos (no separan regiones) | 255 / 245 / 230 m | — |

Al noroeste del Cruce, cuatro crestas se juntan en un nudo de montañas: ese macizo es lo que se ve como «zona realmente alejada» desde el pueblo.

### 3.5 Agua

- **Río Albo**: nace en la ladera sur de la Muralla, baja hacia el sur, pasa **junto al Cruce** (el vado que da nombre al pueblo, con puente) y termina en el **Lago Espejo**.
- **Río Espejo**: sale del Lago Espejo hacia la **Laguna Negra** del marjal.
- **Río de los Diezmos**: sale de la Laguna Negra y llega al **mar del sur**.
- **Río Hondo**: nace en una cascada de la Sierra de Velmora (detrás está la Gruta tras la Cascada), atraviesa el **Lago de Hondaniebla** y llega al mar.
- **Río de la Herida**: nace bajo el Muro del Humo, cruza la meseta excavando el **cañón** (unos 90 m de pared), sale por la boca entre las dos crestas y desemboca en la Laguna Negra.
- **Lagos**: Lago Espejo, Lago de Hondaniebla, Laguna Negra, dos charcas del marjal, la Albufera de la Sal y el mar (3 círculos grandes más la Bahía del Grifo).

### 3.6 Caminos y piedras de viaje

16 caminos conectados, 10,8 km en total. Del Cruce salen cuatro caminos reales: norte (al altar), oeste (a Velmora), este (al Yunque y la mina) y sur (al puerto). Los ramales y sendas cierran bucles: Hondaniebla, el Desfiladero, el Marjal, la Cuesta de los Carros, la Costa, la Escarcha, las Brasas, el Humo, las Salinas, Velmora, el Peñón y la Corona. Hay 7 cruces de camino y río; dos de ellos son el mismo puente del cañón, en (775, 207), donde se juntan tres caminos. Cada pueblo y cada piedra queda a menos de 25 m de un camino. Cada paso tiene su camino.

Piedras de viaje (11): Cruce (5, -60) · Hondaniebla (-300, -298) · Brumafría (345, -410) · Ancla (-80, -780) · Faro (230, -720) · Yunque (545, 160) · Campana (-520, 120) · Juramento (60, 555) · Albor (-567, 485) · Ascua (615, 535) · Alba (-760, 858).

### 3.7 Pueblos y NPC (22)

| Pueblo | NPC (oficio) |
|---|---|
| El Cruce | Maela (carpintera), Toren (herrero), Iria (maga), Hermana Odila (sanadora), Bram (mercader) |
| Aserradero de Hondaniebla | Nessa (cazadora), Viejo Corvo (leñador) |
| Pilotes de Brumafría | Merren (herbolaria), Tilo (barquero) |
| Puerto Ancla Rota | Capitana Rhosyn (armadora), Dorn (pescador; ve volver a la grifa al Peñón), Lys (cartógrafa) |
| Campamento del Yunque | Grenda (capataz minera), Hask (domador; da la pista del león) |
| Refugio de la Campana | Hermano Ansel (archivero), Sor Imelda (campanera) |
| Ermita de los Penitentes | Fray Calder (penitente: le abrió la puerta al Caballero) |
| Fuerte Albor | Ulrik (guía; da la pista de la Escalera), Maren (peletera) |
| Avanzada de Ascua | Kaelen (forjador de obsidiana), Brasa (alquimista) |
| Observatorio del Alba | Ismera, la última vigía: vio caer al Lucero |

Maela, Toren e Iria son los personajes del arranque del doc 023. La frase de cada NPC está en el JSON.

### 3.8 Secretos (20)

| Región | Secreto | Tipo |
|---|---|---|
| Llanos | Sótano de la granja (aparece al quemar la paja); Claro de las Luciérnagas (hueco entre rocas) | Trampilla y claro escondido |
| Bosque | Gruta tras la Cascada; Claro de la Dama Blanca (solo en Luna carmesí) | Cueva tras cascada y claro |
| Marjal | Barca de los Diezmos (con la niebla de la mañana); Muro ilusorio de la cripta | Pecio y muro ilusorio |
| Costa | **Peñón del Grifo** (promontorio sobre el mar con una senda de cornisas oculta); Cueva del Contrabandista (se baja por cuerda) | Cornisa y cueva en el acantilado |
| Meseta | Cornisa del Eco (en la pared del cañón); Tesoro del Pastor (roca que solo rompe el oso o la Fulmancia) | Cornisa y regreso con una habilidad nueva |
| Ruinas | Biblioteca tras el muro; Jardín de las Rosas Negras | Muro ilusorio y jardín oculto |
| Carmesí | Capilla Oculta del Lucero (fresco de la caída); Grieta del Corazón | Capilla tras cascada seca y grieta |
| Picos | Cornisas del Cuervo Blanco; Cueva tras la Cascada Helada (romper el hielo con fuego) | Cornisas y cueva |
| Brasanegra | Tubo de Lava Oculto; Cornisa de la Brasa Dormida (único acceso a pie a la caldera) | Túnel y cornisa |
| Corona | Plumas del Primer Vuelo; Escalera Olvidada (segunda ruta a la Corona) | Nido y atajo |

### 3.9 Monturas

| Montura | Dónde | Velocidad | Cómo se consigue |
|---|---|---|---|
| Lobo de bruma | Guarida de la Loba Gris, bosque (-615, -462) | 14 m/s | Nivel 6. Vencer a Ysolde y domar al alfa con H por debajo del 30 % |
| León de las cumbres | Riscos del León, meseta (965, 305) | 15 m/s; salta repisas y sube pendientes que el lobo no puede | Nivel 20. Seguir huellas y domar al alfa |
| Corcel infernal | Altar del Norte (35, 895) | 16 m/s; inmune al fuego | Vencer al Caballero de sangre |
| Oso acorazado | Cubil del Oso, picos (-385, 705) | 12 m/s; aguanta golpes, rompe rocas grandes y cruza la ventisca | Nivel 32. Romperle las placas de hielo con fuego y domarlo |
| Grifo | Cima del Peñón del Grifo, promontorio de la costa (436, -796) | 24 m/s, vuela | Nivel 30 y haber vencido al Caballero (primera ala). Subir de noche por la senda de cornisas y domar a la grifa madre |
| Fénix de ceniza | Caldera de Brasanegra, en la cima del volcán (792, 806) | 26 m/s, vuela; renace a los 60 s | Nivel 42 y haber vencido a Ignaro. Subir por la cornisa oculta en Luna carmesí |

### 3.10 Números del mapa y tiempos de viaje

Contenido: 84 lugares (64 visibles y 20 secretos), 38 campamentos con 160 enemigos, 10 jefes, 6 monturas, 11 piedras, 22 NPC. En la tierra firme (383 ha) hay **un punto de interés cada 3,1 ha**, a unos 175 m de media: **unos 30 s a pie** entre uno y otro, el ritmo de «explorar siempre paga» del doc 023.

| Desde el Cruce hasta | En línea recta | Por camino | A pie (6 m/s) | Montura (14) | Volando (24) |
|---|---|---|---|---|---|
| Aserradero de Hondaniebla | 433 m | 593 m | 1,6 min | 0,7 min | 0,3 min |
| Puerto Ancla Rota | 788 m | 807 m | 2,2 min | 1,0 min | 0,5 min |
| Altar del Norte | 900 m | 892 m | 2,5 min | 1,1 min | 0,6 min |
| Fuerte Albor | 778 m | 1114 m | 3,1 min | 1,3 min | 0,5 min |
| Templo de la Llama Negra | 1035 m | 1553 m | 4,3 min | 1,8 min | 0,7 min |
| Trono del Lucero | 1331 m | 1682 m | 4,7 min | 2,0 min | 0,9 min |

Estos tiempos son solo de viaje, sin combate. **Advertencia sobre la inmensidad**: con 2 km de lado (unos dos tercios del lado estimado de la referencia y menos de la mitad de su superficie) y monturas de 14-24 m/s, el mapa se cruza rápido. Para que se sienta grande:
1. que fuera del camino y en las pendientes se vaya más lento (×0,8 y ×0,6);
2. curvar los caminos;
3. que el galope gaste resistencia;
4. limitar el vuelo con aguante o con zonas sin aterrizaje;
5. usar niebla y la cámara corta, que ya ayudan;
6. si hace falta, bajar la montura terrestre a 10-11 m/s.

---

## 4. Dudas y pendientes

1. **Escala de la referencia**: no figura en los datos. La estimación de 3-4 km sale de medir la anchura de los caminos y puede tener un error del 25 %.
2. **Cámara y velocidades de la referencia**: los parámetros existen en el código, pero sus valores no están exportados. Los de este documento son de Vhaldren o del género.
3. **Banda de montañas del borde (generador)**: `MundoTerreno.AlturaBase` levanta montañas, o hunde el fondo marino en la costa, en los **170 m junto al borde**, con ±85 m de ruido. Eso ocupa casi un tercio del mapa. Consecuencias:
   - Toda la Corona del Lucero queda dentro de esa banda: su altura se recorta a 259,5 m, una meseta plana en lo alto.
   - Los lugares de la Corona que siguen en la banda: Cráter de la Caída, Puente de Hielo, Mirador de las Mil Leguas y Vaelthar.
   - En este diseño ya se movieron hacia dentro el grifo (antes un islote que el borde costero habría hundido; ahora es un promontorio en tierra firme), los Riscos del León, el campamento del culto, el altar, el templo y dos secretos.
   - Recomendación para el generador: una banda de 80-100 m con ±40 m de ruido, y no sumar montañas de borde en la región de cumbres.
4. **Cimas inaccesibles a pie**: con el perfil de cordillera del generador, las laderas del volcán, la Aguja del Alba y el Pico Albor superan los 45°. La Caldera (fénix) y el Trono (jefe final) solo se alcanzan volando: primero con el grifo (nivel 30) y después con el fénix. Esa cadena es intencionada, pero para subir a pie haría falta una rampa o escalera en el generador. El Peñón del Grifo sí tiene la base ancha (≈ 35°) para que se pueda subir a pie.
5. **Pantano a ras del agua** (base de 22 m y relieve de 5): se buscan charcos por todas partes. Hace falta que los lugares se aplanen a 20 m o más.
6. **Vida y daño de los jefes**: es una escala nueva (de 600 a 3200 de vida base). Hay que ajustarla jugando, junto con el multiplicador por nivel.
7. **Rendimiento**: con 12 capas de terreno y unos 8.200 árboles hay que medir los FPS en la GTX 1650. Si van justos, dividir el terreno en teselas o fusionar capas.
8. Quedan por diseñar las habilidades del Lucero (plumas → planeo, impulso y alas) y la regla exacta de los objetos que impiden montar o volar.
