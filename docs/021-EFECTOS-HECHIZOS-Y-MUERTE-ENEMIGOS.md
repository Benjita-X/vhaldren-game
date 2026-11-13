# 021 — Efectos de hechizos por capas, avisos que se llenan y muerte de enemigos

Fecha: 12 de noviembre de 2026 · Rama: `feature/018-consolidacion-pre-modelado` · Autor: Claude.

## Por qué

El usuario dijo que las habilidades «no tienen definición» y que los personajes se ven toscos al
interactuar. Los efectos eran esferas, cubos y líneas planas que se creaban y destruían en cada hechizo,
y los enemigos desaparecían de golpe al morir.

Se usaron **técnicas generales del género** (efecto por capas, aviso de área que se llena, cuerpo que cae
y se hunde). No se copió ningún archivo, código ni arte de V Rising ni de otro juego: todas las formas
(brillo, humo, esquirla, franja, gota) se generan por código.

## Qué se ve ahora

### Al conjurar
- Mientras dura la preparación, **partículas de la escuela convergen en la mano** (la que más adelantada
  está según la animación) y un brillo crece en ella. Se dibujan por encima del personaje para que se
  vean aunque la mano quede detrás del cuerpo desde la cámara.
- Al salir el hechizo, destello en la mano en la dirección del lanzamiento.

### Proyectiles (las seis escuelas)
Cada proyectil tiene **núcleo, halo que late, cuerpo estirado en la dirección del vuelo, estela, luz y
partículas propias** que se quedan atrás:

| Escuela | Partículas | Rasgo |
|---|---|---|
| Piromancia | brasas que suben | llama naranja |
| Criomancia | esquirlas de hielo y destellos | lanza de cristal |
| Fulmancia | chispas rápidas y arcos en zigzag alrededor | trazo violeta |
| Umbramancia | humo oscuro | núcleo negro con halo morado |
| Hemomancia | gotas que caen | núcleo granate |
| Lumancia | rayos y destellos dorados | trazo largo de luz |

### Impactos
Destello blanco que vira al color de la escuela, halo, onda en el aire, **onda expansiva en el suelo
hasta el radio real**, partículas del elemento, luz que decae y, en áreas, **marca en el suelo que se
desvanece** (quemado, escarcha, mancha de sombra o de sangre, sello de luz).

### Avisos de área (Zona y Nova)
Un **disco en el suelo**: el área se tiñe, el borde marca el límite exacto, un anillo de runas gira y un
relleno crece desde el centro hasta tocar el borde **justo cuando estalla**. Al terminar destella y se apaga.

### Remates de los definitivos
- Meteoro: una bola de fuego cae en diagonal y llega en el instante del estallido; deja columna de humo.
- Tempestad: rayos sueltos caen dentro del círculo y al final una descarga doble en el centro.
- Juicio celestial: motas de luz bajan del cielo y estalla un pilar de luz.
- Eclipse: anillo de humo negro que se expande.
- Prisión glacial: barrotes de hielo que brotan en el borde. Ventisca: esquirlas y niebla helada.
- Festín carmesí y Torrente sangriento: salpicadura de sangre y motas que vuelven al mago (robo de vida visible).
- Cadena de rayos: dos rayos entrelazados en zigzag que chisporrotean, con chispas en cada objetivo.
- Paso sombrío: bocanadas de humo al desaparecer y al aparecer. Égida: círculo rúnico a los pies con
  motas de luz que suben; destella al absorber un golpe.

### Enemigos
- **Aviso de ataque**: el anillo se convierte en un disco que se llena desde los pies del enemigo hasta el
  borde exacto del golpe; el borde late cada vez más rápido y al golpear destella en rojo.
- **Muerte**: las colisiones se apagan en el mismo instante; el modelo KayKit reproduce `Death_A`, levanta
  polvo al tocar el suelo, se hunde un poco y se oculta a los **1,8 s**. Al reaparecer vuelve al reposo
  (sin pose de muerto). Si reaparece antes de 1,8 s, el ocultado pendiente se cancela.
- Los lobos (sin animación) caen de lado por código con el mismo calendario.
- Los brillos sueltos de magos y sanadoras (orbe) se apagan al morir para no quedar flotando.
- Las chispas de golpe tienen forma de luz (antes eran rectángulos planos) y el polvo ya no brilla.
- Los virotes y brasas de tiradores y acólitos son haces reutilizables.

## Cómo está hecho

| Pieza | Archivo |
|---|---|
| Shaders propios (en `Resources` para que el ejecutable los incluya) | `Assets/Resources/VfxVhaldren/VfxParticula.shader`, `VfxSuelo.shader` |
| Formas, materiales compartidos, paleta por elemento, colocación en el suelo | `Assets/Scripts/Combat/VfxLibrary.cs`, `VfxDisc.cs` |
| Reservas de objetos y un sistema de partículas por elemento | `Assets/Scripts/Combat/VfxHost.cs` |
| Impacto, marca, aviso de área, rayo, cuerpo de proyectil | `SpellImpactFlash.cs`, `GroundMark.cs`, `GroundTelegraph.cs`, `LightningArc.cs`, `VfxOrb.cs` |
| Energía en la mano | `Assets/Scripts/Magic/SpellCastCharge.cs` |
| Firma de cada escuela y remates | `Assets/Scripts/Magic/SpellVfx.cs` |
| Proyectil reutilizable | `Assets/Scripts/Magic/SpellProjectile.cs` |
| Aviso enemigo | `Assets/Scripts/World/EnemyAttackTelegraph.cs` |
| Muerte | `PrototypeEnemy.cs` (cuerpo, ocultado, reaparición), `EnemyAnimationDriver.cs` (disparador `Death`) |

- **Sin basura por hechizo**: proyectiles, impactos, marcas, avisos y rayos salen de reservas
  (`VfxPool`). Las partículas se emiten con `EmitParams` en un sistema por elemento, así que no se crean
  objetos nuevos. La prueba `ProjectilesAndImpactsAreReusedInsteadOfRecreated` lo comprueba.
- `SpellImpactFlash.Spawn` conserva su firma: todo el código que ya lo usaba (razas, armas, jefe,
  sanadora) ganó el impacto nuevo sin cambios.
- Los tiempos y el daño no cambian: los efectos son solo visuales.

### Estado de muerte en los controladores KayKit
`Vhaldren > Personaje > Añadir muerte a enemigos KayKit` (o por consola
`-executeMethod Vhaldren.EditorTools.KayKitEnemySetup.AddDeathAnimation`) añade al controlador base el
disparador `Death`, el estado `Muerte` con `Death_A` (sin salida) y una transición desde cualquier estado
que va **primera** (gana a golpe y ataque en el mismo fotograma), y pone en las 24 variantes el clip de su
propio modelo. Modifica los assets en el sitio: **los 25 GUID no cambiaron** (comprobado antes y después).
Se puede ejecutar varias veces sin duplicar nada. **No ejecutar** `Preparar enemigos KayKit` (`Setup`):
recrea los assets y rompe la escena.

## Verificación (Unity 6000.2.12f1)

- EditMode **98/98** tras unirse con las herramientas Mixamo de la 018 (3 nuevas mías: estado de muerte con prioridad, clip propio en las 24 variantes, shaders
  en `Resources`).
- PlayMode **126/126** (7 nuevas: muerte animada + ocultado + reaparición limpia, reaparición antes de
  ocultarse, caída procedural, aviso enemigo que se llena, preparación en la mano + disco de zona,
  reservas sin crecer, partículas por elemento).
- Compilación de Windows sin errores ni avisos (115,6 MB).
- Captura automática del ejecutable (`-vhaldrenCapture`): 28 capturas nuevas o antiguas, informe con
  `shaders_propios=True`, `proyectiles_en_vuelo=6/6`, `muerte_animada=True procedural=False
  colision_apagada=True`, `cuerpo_oculto_tras_1_8s=True`, 19 enemigos, 120 FPS a 1280×720 y ningún
  error en el registro.

Capturas nuevas de la verificación: `2a-anillo-preparacion`, `3b-meteoro-cayendo`,
`10-proyectiles-seis-escuelas`, `11-tempestad-rayos`, `11b-tempestad-descarga`, `12a-juicio-avisado`,
`12-juicio-celestial`, `13a-eclipse-preparacion`, `13-eclipse`, `14-prision-glacial`, `15-festin-carmesi`,
`16-aviso-enemigo-llenandose`, `17-cadena-de-rayos`, `18-muerte-cayendo`, `19-muerte-en-el-suelo`.

## Pendiente o posible siguiente paso

- El árbol de movimiento del controlador base guarda umbrales 0 / 0,5 / 1 (el código de `AddRunAnimation`
  pretendía 0 / 2 / 4): a más de 1 m/s todos los enemigos usan ya la animación de correr. No se tocó aquí.
- Sonidos propios para la preparación y los remates (hoy reutilizan los sintetizados existentes).
- Notas de estilo recibidas y no hechas aquí (tocan cámara u otros sistemas): resplandor (bloom) con
  Post Processing Stack v2 en la cámara del mundo, estela de arco en los golpes de espada, cúpula verde
  de curación y números de daño más grandes con salto (`CombatDamageFeedback.cs`).
