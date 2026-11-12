# 022 — HUD que no tapa la acción y armas visibles en las manos

Fecha: 12 de noviembre de 2026 · Rama: `feature/018-consolidacion-pre-modelado` · Autor: Claude.

## Por qué

Unos seis recuadros opacos de OnGUI tapaban casi el 40 % de la pantalla (estado y daño de depuración,
defensa, habilidades V/T/F, ayuda F1 en un tercio de la derecha, línea de hechizos, recursos, cartel del
pueblo y avisos). El usuario quiere ver a los enemigos y sus habilidades sin que nada estorbe, con un
acabado bonito, y el equipo visible en los personajes.

Se usaron solo **técnicas generales del género** de acción cenital (barra única abajo, orbe de vida,
viñeta, avisos breves). No se copió ningún diseño, marco, icono ni arte de V Rising ni de otro juego:
todas las texturas (viñeta, degradados, marcos, esquinas, orbe, arco y glifos) se generan por código en
`HudSkin`.

## Qué se ve ahora

### Barra de acción única (abajo en el centro)
- **Orbe de vida** en el centro (líquido carmesí que baja, cristal y aro dorado) con la **resistencia en
  arco** alrededor (amarillo; rojo si la guardia se rompe; blanco durante la ventana de parada). Con
  menos del 30 % de vida el orbe late.
- **Izquierda:** ballesta (clic), espada (V), pulso (T), escarcha (F) y la **poción** (Q) pequeña junto al
  orbe.
- **Derecha:** la **escuela activa** pequeña junto al orbe (Tab), los **hechizos 1, 2 y 3** de esa escuela y
  el **poder de raza** (R).
- Cada icono: cuadro con el color de la escuela o del arma, glifo propio, bisel y borde fino; **tecla
  real** con `GameInput.Label` (cambia al reasignar); **recarga como sombreado que baja** con los segundos
  restantes y el número encima; **puntos de cargas** en los hechizos con varias; **brillo en el borde**
  cuando está listo (destello al quedar listo). Se apaga en gris si falta resistencia o la acción está
  bloqueada (construcción, tablón).
- **Línea mínima encima:** escuela activa y esencia (y «Égida» o «en combate» cuando toca).
- **Aviso de contexto** encima de esa línea: absorber esencia (con progreso), recolectar (con progreso),
  «N: tablón» cerca del puesto.
- Ancho máximo: **45 %** de la pantalla. En pantallas estrechas (4:3) la barra se reduce sola.

### Acabado
- **Viñeta** suave en los bordes con un **velo carmesí** tenue arriba; con menos del 30 % de vida se tiñe de
  **rojo y late**. El centro queda limpio. El destello rojo al recibir daño usa el mismo degradado.
- **Marco ornamentado fino** (doble línea oro envejecido/carmesí y esquinas con rombo) solo alrededor de la
  barra y los paneles. **El menú principal, la pausa, opciones, información, la forja, el tablón y la
  construcción usan el mismo marco** (`MenuStyle.FramedPanel`) y botones con borde y bisel.
- **Textos sin cajas:** contorno oscuro suave para leerse sobre el mundo.

### Avisos breves (arriba en el centro)
`HudNotices`: habilidades (recarga, resistencia), raza, forja, esencia, botín y poción pasan por aquí.
**Máximo 2 a la vez, unos 2 s, con fundido.** Un aviso repetido renueva el que ya está (no se apila).
Lanzar un hechizo o una habilidad no avisa: la barra ya lo enseña.

### Misión actual (arriba a la derecha)
Hora del día (en rojo con Luna carmesí), nombre de la misión y una línea de estado (aceptar en el tablón,
progreso X/Y o entregar). El tablón completo solo aparece al abrirlo con N.

### Ayuda F1
Oculta por defecto (`GameSettings.ShowHelpOnStart` ahora es «no»). Al empezar solo sale el aviso
**«F1: controles»**. Al abrirla, un panel compacto en dos columnas con las teclas asignadas.

### Recursos (abajo a la izquierda)
Fila mínima: cuadradito de color y cantidad de madera, piedra, fibra y mineral, más la capacidad. En
**modo construcción** se amplía: nombres, fabricación de picos y el catálogo con marco a la derecha.

### Sobre los enemigos
Los estados son **puntitos de color** (el de su escuela) con el número de cargas de escarcha; un punto
grande celeste si está congelado; parpadean en el último segundo. El nivel queda en pequeño a la
izquierda, y la esencia («Esencia Feroz 62 % · H») debajo cuando se puede absorber.

### Flechas de golpe fuera de pantalla
Si un enemigo que no se ve prepara un golpe contra el jugador, aparece una **flecha pequeña en el borde**
apuntando hacia él (naranja que pasa a rojo según se llena su aviso).

### Modo de depuración
**F3** (reasignable en Opciones → Controles, «Recuadros de depuración») vuelve a mostrar los recuadros
antiguos con el daño total, la defensa, etc. No se borraron: quedan solo para depurar.

## Armas visibles en las manos

Los modelos KayKit de aventureros (CC0) traen **todas** sus armas colgadas de los huesos `handslot.r` y
`handslot.l` a la vez: el caballero llevaba tres espadas y cuatro escudos superpuestos, el bárbaro dos
hachas, un escudo y una jarra, la maga varita, bastón y dos libros. Los esqueletos no traían ninguna.

`CharacterLoadout` (añadido en tiempo de juego por `PrototypeGameSession`, sin tocar prefabs ni la escena,
mismos GUID) deja solo la de cada tipo:

| Modelo | Arma |
|---|---|
| Jugador (pícaro encapuchado) | ballesta de una mano; **color y brillo según la calidad forjada** (`PlayerEquipment.TierOf(GearSlot.Weapon)`): básica sin brillo, bronce, acero azulado, carmesí |
| Bárbaro | hacha y escudo redondo; el **bruto**, hacha a dos manos |
| Caballero | espada y escudo redondo; el centinela de élite, escudo rectangular; el **jefe**, espadón a dos manos |
| Maga | bastón; la **sanadora**, varita y libro abierto |
| Pícaro | **ballestero**: ballesta; el resto, dos dagas |
| Esqueleto (esbirro) | hoja del pack de esqueletos |
| Esqueleto guerrero | hacha y escudo pequeño del pack de esqueletos |

Las armas de esqueleto se copiaron del pack **KayKit Character Pack: Skeletons 1.0 (CC0)** a
`Assets/Resources/ArmasKayKit/` (`Skeleton_Blade`, `Skeleton_Axe`, `Skeleton_Shield_Small_A` y
`LICENSE.txt`). Usan el mismo atlas que el cuerpo (`skeleton_texture.png`, idéntico al del proyecto), así que
no hizo falta copiar texturas. Las armas son solo visuales (sin colisión) y caen con el cuerpo al morir
porque cuelgan del hueso. `CrossbowVisual` ya usaba la ballesta nativa del modelo cuando existe, y solo
dibuja la ballesta de primitivas en modelos sin ella.

## Archivos

Nuevos: `Assets/Scripts/UI/HudSkin.cs`, `HudNotices.cs`, `ScreenVignette.cs` (con `HudMode`),
`ActionBarHud.cs`, `WorldHud.cs`, `Assets/Scripts/World/CharacterLoadout.cs`,
`Assets/Resources/ArmasKayKit/*`, `Assets/Tests/EditMode/HudTests.cs`,
`Assets/Tests/PlayMode/HudPlayModeTests.cs`.

Cambiados: `PrototypeGameSession` (añade el HUD y las armas, datos de la poción, caída y victoria sin
cajas), `PlayerCombatAbilities` (duraciones de recarga y coste de resistencia públicos), `LocalPlayerShooter`
(recarga de la flecha), `PlayerSpellbook` (`RechargeSeconds`), `PlayerRace` (`HudColor`),
`CombatDamageFeedback` (`LabelOffset`), `PrototypeEnemy` (`Target`), `GameInput` (`DebugHud` = F3, al final
del enum), `GameSettings` (ayuda oculta por defecto), `MenuStyle` (paneles y botones con marco), menús,
`PlayerControlsGuide`, `SurvivalHud`, `VillageNoticeboard` (`SetMenuOpen`), `PlayerEquipment` (forja con
marco), `PlayerFeeding`, `PlayerDefensiveInput`, `GameFeel`, `GameAutoCapture` (capturas 20-27).

## Verificación (12-11-2026, Unity 6000.2.12f1)

- **EditMode 122/122** (antes 107; 15 nuevas en `HudTests`: fracción de recarga, avisos máximo 2 y
  caducidad, aviso repetido que se renueva, fundido, viñeta con poca vida, barra ≤ 45 % en 1280×720,
  1920×1080, 1024×768, 2560×1080 y 800×600, dirección de la flecha fuera de pantalla, la flecha no se pone
  sobre la barra, tecla F3 reasignable, ayuda oculta por defecto y brillo del arma por calidad).
- **PlayMode 144/144** (antes 137; 7 nuevas en `HudPlayModeTests`: la barra lee las recargas reales de la
  espada y las cargas de los hechizos, los avisos caducan en tiempo real, la viñeta sigue la vida, el
  caballero lleva una espada y un escudo en `handslot.r`/`handslot.l` y nada más, los esqueletos reciben el
  arma del pack con su material y tamaño correcto, bárbaro/maga/pícaro conservan una sola arma, y el
  jugador mantiene la ballesta en la mano derecha).
- **Ejecutable**: `VhaldrenBuild.BuildWindows` sin errores. `Vhaldren.exe -vhaldrenCapture` a **1280×720 y
  1920×1080**: 44 archivos cada una, **0 excepciones**. Informe: barra al **44 %** del ancho; recargas leídas
  (espada 1,0 s, pulso 5,6 s, poción 10,6 s, 2 cargas en el hechizo 1); 19 enemigos, cada uno con su arma
  en el hueso correcto (p. ej. `1H_Sword@handslot.r` y `Round_Shield@handslot.l` en los centinelas,
  `2H_Sword` en el jefe, `Arma: Skeleton_Axe@handslot.r` en el esqueleto guerrero); flecha fuera de pantalla
  mostrada ante el ballestero.
- Capturas nuevas: `20-hud-barra-recargas`, `21-hud-avisos`, `22-hud-poca-vida`, `23-hud-ayuda-f1`,
  `24-hud-construccion`, `25a/25-hud-tablon`, `26-armas-en-las-manos`, `27-hud-flecha-fuera-de-pantalla` y
  `28-armas-retratos` (retratos de cerca de los ocho tipos con su arma).
- Problemas encontrados y corregidos durante la verificación: las armas del pack de esqueletos llegaban
  diminutas (su raíz trae 270° y escala ×100; ahora se conserva); la barra de vida de un enemigo cercano se
  pintaba encima de la barra de acción (la profundidad de IMGUI se fija ahora en todos los eventos); la
  flecha marcaba a un enemigo que aún se veía en el borde y podía caer sobre la barra.

Pendiente de probar a mano (no se puede automatizar aquí): sensación general jugando con ratón y teclado,
y la lectura de los textos pequeños en un monitor real.
