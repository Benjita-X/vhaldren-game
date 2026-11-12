# 019 — Magia de escuelas, esencia, bestiario y versión de escritorio

Fecha: 12 de noviembre de 2026 · Rama: `feature/017-integracion-jugable-pueblos-combate` (línea única) · Autor: Claude.

## Qué se tomó del estudio de V Rising y qué no

La auditoría de los archivos de V Rising (`auditoria-v-rising/AUDITORIA.md`) se usó **como referencia de mecánicas**: alimentación de sangre con tipo y calidad, ejecutar sin obtener sangre, vulnerabilidad al canalizar, seis escuelas con efectos propios (quemadura, frío que congela, estática, condena, debilidad, robo de vida), cargas y relanzar, estado de combate sin regeneración, prohibición de cambiar habilidades en combate, ciclo día/noche y luna especial.

**No se integró ningún archivo extraído** (mallas OBJ, mapa `ZoneMap`, texturas, animaciones, textos). Son propiedad de Stunlock Studios. Usarlos impediría publicar o compartir Vhaldren y expone el repositorio a una retirada por copyright. Además, la propia auditoría dice que esas mallas no traen esqueletos, materiales ni colocación. Los personajes y jefes concretos de ese juego tampoco se copian. Es la misma regla del documento 016: identidades originales.

## Sistemas nuevos

### Grimorio: 6 escuelas × 3 hechizos (teclas 1, 2, 3 · Tab cambia de escuela)

| Escuela | 1 (básico, 2 cargas) | 2 (control) | 3 (definitivo) | Estado |
|---|---|---|---|---|
| Piromancia | Saeta ígnea | Anillo de llamas | Meteoro | Ignición: 5 PV/s durante 4 s |
| Criomancia | Lanza de hielo | Prisión glacial | Ventisca | Escarcha: ralentiza; con 3 cargas, congela 1,4 s |
| Fulmancia | Chispa | Cadena de rayos (4 saltos) | Tempestad | Estática: el siguiente hechizo hace +18 de daño |
| Umbramancia | Dardo umbrío | Paso sombrío (relanzar en 3 s = volver) | Eclipse | Maldición: recibe +25 % de daño |
| Hemomancia | Saeta carmesí | Torrente sangriento | Festín carmesí | Sangría: tus golpes al marcado te curan un 35 % |
| Lumancia | Rayo solar | Égida (escudo de 40) | Juicio celestial (+20 PV) | Deslumbrado: hace un 35 % menos de daño |

- Se empieza con Piromancia, Criomancia y Fulmancia. **Lumancia, Umbramancia y Hemomancia se aprenden absorbiendo** esencia Sabia, Feroz y Sanguínea. La esencia Arcana, que solo tiene el jefe, enseña las seis escuelas.
- **No se puede cambiar de escuela en combate**: durante 6 s después de causar o recibir daño.
- Los proyectiles usan SphereCast en cada frame, así que no atraviesan objetivos aunque el juego vaya lento, y se detienen en paredes y árboles. Las áreas comprueban la línea de visión. El Paso sombrío usa `CharacterController.Move`, así que no atraviesa muros.
- Todo el daño pasa por `CombatDamageReceiver`: se aplican escudo, parada, resistencias y los modificadores nuevos (interfaces `IDamageTakenModifier` e `IDamageAbsorber`).

### Esencia vital (adaptación de la alimentación)
- Debilita a un enemigo por debajo del **30 %** de vida, acércate a menos de 2,6 m y **mantén H** durante 2,4 s. Encima del enemigo aparecen su tipo y su calidad de esencia.
- Mientras absorbes estás **Expuesto** (recibes un 30 % más de daño). Si te quitan de golpe un 12 % de la vida máxima, la absorción se interrumpe. La víctima queda inmóvil.
- Al terminar: adoptas su **tipo y calidad** (de 1 a 100 %), recuperas un 15 % de vida y el enemigo cae.
- **Clic izquierdo durante la absorción = ejecutar**: muere al instante, pero no recibes esencia.
- Tipos: Sabia (poder de hechizo), Feroz (críticos), Titánica (menos daño recibido), Sanguínea (robo de vida), Salvaje (recargas más cortas) y Arcana (un poco de todo; solo el jefe).
- La reserva se gasta en unos 10 minutos. Por debajo del 25 %, la bonificación baja a la mitad; al vaciarse, pierdes la esencia.

### Bestiario: 8 tipos con roles y 5 campamentos (19 enemigos en total)

| Tipo | Rol | Táctica | Esencia habitual |
|---|---|---|---|
| Saqueador del camino | Cuerpo a cuerpo | Rápido y directo | Feroz |
| Ballestero renegado | Tirador | Retrocede y dispara; avisa con una línea roja. El escudo Z lo bloquea | Feroz |
| Acólito ceniciento | Mago | Retrocede; sus brasas te queman. La barrera C lo bloquea | Sabia |
| Lobo de bruma | Bestia | Manada rápida, cuerpo de cuadrúpedo | Salvaje |
| Bruto de las canteras | Bruto | 140 PV, golpe lento de 22: esquiva o para con X | Titánica |
| Esqueleto de la cripta | Cuerpo a cuerpo | Resiste un 50 % las flechas | Titánica |
| Sanadora del culto | Apoyo | Cura a sus aliados cada 5 s y maldice. Mátala primero | Sanguínea |
| Centinela rúnico | Élite | Alterna espada y runas; resiste todo un poco | Sabia |

- Campamentos: bandidos (sur del camino), lobos (bosque suroeste), cripta (bosque noroeste), culto (ruinas sureste) y cantera (ruinas noreste).
- **Jefe**: al bajar del 50 % se enfurece. Se mueve un 25 % más rápido, recarga un 30 % antes y usa magia en uno de cada dos golpes.

### Día, noche y Luna carmesí
- Un día dura 20 minutos reales y se empieza a las 08:00. De noche (21:00–05:30) los enemigos hacen un **15 %** más de daño y la esencia absorbida sale con **+15** de calidad.
- La noche de cada cuarto día sale la **Luna carmesí**: un **30 %** más de daño enemigo, **+30** de calidad de esencia y luz roja. La hora se ve en la barra de hechizos.

### Versión de escritorio
- `Vhaldren > Compilar juego para Windows` genera `Builds/Windows/Vhaldren.exe` (86 MB, Windows de 64 bits, ventana de 1600×900 redimensionable).
- **Esc** abre la pausa (continuar, pantalla completa, salir).
- `Assets/Resources/VariantesShader` guarda dos materiales de muestra para que el ejecutable no pierda la transparencia de la vista previa de construcción ni el brillo de los enemigos.
- Verificación automática: `Vhaldren.exe -vhaldrenCapture <carpeta>` arranca el mundo, lanza hechizos, visita un campamento, pone la Luna carmesí, guarda capturas y un informe, y se cierra. Resultado: 19 enemigos, 120 FPS a 1280×720 y ninguna excepción en el registro.

### Progresión obligatoria: equipo, niveles y botín (respuesta a «que no sea fácil»)
- ¿Por qué no usar los enemigos de V Rising «ya hechos»? Se revisaron los archivos extraídos. `modelos.json` solo guarda el nombre y el número de vértices de cada malla, y los OBJ son geometría suelta, sin esqueleto, animación ni comportamiento. La IA está compilada en `GameAssembly.dll` (247 MB de código nativo IL2CPP) y el mundo usa entidades DOTS de Unity 2022. No hay nada que se pueda conectar: igualmente habría que reprogramar el comportamiento, y además infringiría el copyright.
- **Nivel de enemigo**: saqueador 5, ballestero 6, acechador 6, lobo 7, centinela de ruinas 9, esqueleto 10, acólito y sanadora 12, guardián 13, bruto 16, centinela rúnico 20 y jefe 26.
- **Nivel de equipo** = media de arma, armadura y talismán: viajero 1, cazador 8, hierro 16, rúnico 24.
- Por cada nivel de diferencia, un 4 %: con menos nivel haces menos daño (mínimo 35 %) y recibes más (máximo ×2). Con equipo inicial, el jefe hace el doble de daño y recibe un tercio.
- **Forja (U)**: cazador = 4 piel, 6 madera, 4 fibra. Hierro = 6 mineral, 4 madera, 3 piel, con banco de trabajo. Rúnico = 3 cristal, 6 mineral, 4 esencia, con banco. La armadura da +20/+45/+80 de vida y el talismán +6/12/20 % de poder de hechizo.
- **Botín por tipo**: la piel sale de lobos y tiradores, el mineral del bruto y las ruinas, y el cristal y la esencia de acólitos, centinela y jefe. Para mejorar hay que cazar en orden.
- **Campamentos en grupo**: al atacar a un enemigo, sus compañeros a menos de 10 m acuden. Un asentamiento no se limpia de uno en uno.
- Encima de cada enemigo se ve su nivel: blanco (a tu altura), amarillo (reto) y rojo (6 o más por encima).

### Arte: texturas reales, enemigos animados y modelos propios con Blender
- **13 texturas de Poly Haven** (CC0, 1K, 26 MB; autores Rob Tuytel y Amal Kumar), en `Assets/ThirdParty/PolyHaven`. Se usan en el suelo, el bosque, las ruinas, el camino de adoquín, el patio del altar, los muros de castillo, los bloques, los troncos, las copas, las rocas y el mineral.
- **Shader `Vhaldren/Triplanar`**: proyecta la textura según la posición en el mundo (metros por repetición) y añade el relieve del mapa normal. Así el suelo de 92 m y un bloque de 2 m comparten la misma escala sin estirarse.
- **Enemigos KayKit animados** (CC0): bárbaro (saqueadores y bruto), pícaro (ballestero y acechadores), maga (acólito y sanadora), caballero (centinelas, guardián y jefe) y dos esqueletos. `Vhaldren > Personaje > Preparar enemigos KayKit` crea un controlador base y 24 variantes por estilo de ataque (cuerpo a cuerpo, pesado, distancia, hechizo). `EnemyAnimationDriver` anima el caminar, el ataque (sincronizado con el aviso) y la reacción al golpe. Hay 16 de 19 enemigos con modelo; los lobos conservan su cuerpo de cuadrúpedo.
- **Flujo Blender → Unity**: `Tools/Blender/*.py` construye modelos originales por script (`blender --background --python ... -- salida.fbx`). El primero es el **obelisco rúnico** del altar, con anillos de runas emisivos. El generador del mundo los coloca si el FBX existe y, si no, vuelve a la primitiva.
- No se usa ningún archivo de V Rising: sus mallas no tienen esqueleto y su lógica está compilada.

### Game feel: cada golpe y cada paso responden
- Cada golpe que quita vida pasa por `CombatDamageReceiver`, que llama a `GameFeel.OnHit`. Eso produce:
  - **Parón** de 30 a 85 ms según el daño (120 ms al rematar).
  - **Temblor de cámara**.
  - **Chispas** en dirección contraria al atacante, de color según el tipo de daño.
  - **Empujón** del enemigo (respeta paredes; el jefe solo un 30 %).
  - **Sonido** de impacto.
- Al recibir daño: parón, temblor más fuerte, **bordes rojos** en la pantalla y quejido.
- Al bloquear o parar: choque metálico con chispas doradas (la parada perfecta, además, con parón).
- Los críticos tienen un impacto extra.
- Las áreas que golpean a muchos enemigos producen **un solo parón** (mínimo 0,1 s entre parones). El parón nunca deshace la pausa (Esc) y, si se interrumpe, el tiempo vuelve a 1.
- **Sonidos sintetizados por código** (`GameAudio`, sin archivos, originales): golpe, corte, metal, conjuro, explosión mágica, hielo, ballesta, pasos, soplido, herida y derrota. Los enemigos hacen un sonido al preparar el golpe, así se oye venir.
- **Movimiento**: un paso cada 1,7 m con polvo y sonido, y una nube de polvo con soplido al esquivar.
- Solo está activo en el mundo jugable: las salas de prueba no cambian.
- Se activó el módulo integrado de partículas de Unity en `Packages/manifest.json`.

## Pruebas
- **EditMode 77/77** y **PlayMode 110/110** en Unity 6000.2.12f1 (incluye 6 pruebas de progresión: forja atómica, banco, armadura sin curación gratis, diferencia de nivel, botín y aviso al grupo).
- Nuevas: catálogo de hechizos y esencias, ciclo día/noche, 11 de magia en PlayMode (proyectil, cargas, congelación, maldición, estática, Égida, escuelas en combate, absorción, ejecución, interrupción, Paso sombrío contra muro y regeneración), 7 del bestiario y una prueba de humo que carga `WorldPrototype001` completo y exige consola limpia.
- El actualizador del Animator 017 se ejecutó al abrir Unity: el controlador pasó de 6 a 14 estados sin perder ninguno.

## Pendiente (no implementado en esta tanda)
- Dominación, prisioneros, sirvientes, corazón del castillo y asedios.
- Transformaciones (lobo, murciélago): por la regla 016 no se activan sin modelo y animaciones propias.
- Monturas.
- PvP y duelos: requieren elegir la red (NGO o FishNet) y que el servidor valide.
- Guardado de partida.
- Modelos 3D y efectos de sonido reales para hechizos y enemigos (ahora son primitivas).
- Ajuste de números tras jugar: daño, recargas y la duración de la absorción.
