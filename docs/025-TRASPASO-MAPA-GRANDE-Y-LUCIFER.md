# 025 — Traspaso: mapa grande, Lucifer, monturas y exploración (rama 020)

Fecha: 18 de noviembre de 2026 · Autor: Claude · Rama: `feature/020-mapa-grande-lucifer`
(sale de `feature/018-consolidacion-pre-modelado`, commit `81fb0aa`). Unity 6000.2.12f1, pipeline integrado.

## Qué está hecho y verificado

- **Mapa grande** (`Assets/Scenes/MundoVhaldren.unity`, 2 × 2 km). El menú lo carga en *Nueva partida*.
  - Se **genera por código**: menú `Vhaldren > Mundo > Generar mapa grande`, o por consola
    `-executeMethod Vhaldren.EditorTools.Mundo.MundoBuilder.GenerarDesdeConsola` (≈45 s).
    No editar la escena a mano: se pisa al regenerar.
  - **Diseño**: `Assets/Art/Mundo/diseno_mapa.json`, explicado en `docs/024-MAPA-GRANDE-DISENO.md`.
    Tiene 10 regiones, cordilleras con pasos, 6 ríos, lagos y mar al sur, 84 lugares (20 secretos), 38 campamentos,
    10 jefes, 6 monturas, 11 piedras de viaje y 22 aldeanos.
  - **Código del generador**: `Assets/Editor/Mundo/`.
    - `MundoTerreno`: relieve, ríos, lagos y explanadas.
    - `MundoBuilder.*`: texturas del suelo, árboles, pasto, agua, puentes, lugares, enemigos, mapa ilustrado y atlas.
    - `MundoKit`: prefabs y materiales del kit.
    - `MundoGancho`: conecta componentes por reflexión.
  - **Arte propio** (Blender por código) en `Assets/Art/Mundo/`: 114 FBX, `manifest.json` y 11 texturas de terreno.
    Los scripts de Blender no están en el repo; quedaron en la carpeta temporal del PC del dueño.
- **Exploración** (`Assets/Scripts/Exploration/`), instalada desde `PrototypeGameSession` con `ExplorationBootstrap.Install`:
  - regiones con título, niebla y luz propias;
  - mapa (M) con niebla de guerra, minimapa y viaje rápido en las piedras de viaje (E para activarlas);
  - cofres, piedras de lectura, lugares descubiertos con recompensa y agua profunda que te devuelve.
- **Monturas** (`Assets/Scripts/Mounts/`): domar con E mantenida, G montar/desmontar, mantener G abre la rueda,
  Mayús galopar o correr a pie, Ctrl despegar y aterrizar (grifo y fénix).
  - `QuadrupedGait` anima por código las partes con nombre, y también a los lobos enemigos.
- **Lucifer**: linaje por defecto, escuela exclusiva *Luz caída* (`Assets/Scripts/Magic/FallenLightKit.cs`,
  `FallenLance.cs`, `LuciferFlight.cs`; avatar y alas en `Races/LuciferAvatar.cs`).
  - Teclas: 1 Lanza, 2 Salto de alas, 3 Caída del Lucero, R Estallido.
- **Aldeanos** (`Assets/Scripts/NPC/`): pasean, hablan con E y su oficio tiene función (forja, comercio, curar, guía).
- **Combate**: empuje según el daño, parones, críticos y enemigos que duermen a más de 70 m.
- **Pruebas**: EditMode 170/170. PlayMode completo en verde tras ajustar 3 pruebas, más la nueva `MundoGrandePlayModeTests`.
- **Ejecutable**: probado en la GTX 1650 del dueño con `Vhaldren.exe -vhaldrenCapture <carpeta>`, que ejecuta
  `MapaAutoCapture`: vista aérea, recorrido por 18 lugares, el kit de Lucifer y el informe de FPS. Da 40-90 fps.
- **Último cambio de este traspaso**: el disco de suelo de los hechizos (`Resources/VfxVhaldren/VfxSuelo.shader`)
  ya no queda cortado por las lomas. Se adelanta hacia la cámara 2,2 m a lo largo del rayo de visión.
  Está compilado pero no se ha vuelto a verificar en pantalla.

## Lo que el dueño jugó y NO le gustó (pendiente, en orden de prioridad)

1. **«No sé adónde ir; aquí no me gusta explorar» (como sí le gusta en V Rising).** El mapa tiene contenido, pero no
   lo **muestra**. Falta:
   - Faros visibles desde lejos: columna de luz en el objetivo de la misión y en los lugares; humo en poblados y
     campamentos.
   - Caminos enmarcados como en las referencias que mandó: pilares, braseros y estandartes a los lados, con vegetación
     densa en los bordes.
   - Postes con flechas en los cruces de caminos.
   - Un sendero inicial con historia desde el Cráter del Despertar (-132,-142) hasta el Cruce (0,-20): carreta rota,
     provisiones, barricada y luces cálidas al fondo.
   - Puntos de interés más densos y agrupados, en lugar de dispersos.
2. **Lucifer no parece Lucifer.**
   - Sigue con el modelo del pícaro verde encapuchado (`RogueHooded`). Necesita otro modelo, o al menos el Knight/Mage
     oscuro, con capa y halo.
   - **Las alas salen mal orientadas**: se ve una placa oscura con dientes rojos de lado. Revisar en `LuciferAvatar.cs`
     el enganche al hueso `chest` y la rotación. El prefab `Resources/Lucifer/alas_lucifer` tiene una raíz limpia y el
     hijo `modelo` (FBX con *Bake Axis Conversion*); las alas están modeladas **hacia atrás (−Z)**, no en cruz como
     supuso el código.
3. **«Las habilidades son todas iguales y se pegan».** Las capturas muestran que el kit de Lucifer se lanza y hace daño,
   pero se ve como los demás:
   - todo son discos dorados de suelo;
   - los textos de los enemigos se encabalgan;
   - el efecto se queda pegado al personaje.
   Cada habilidad necesita forma, movimiento y sonido propios:
   - lanza: proyectil largo con estela;
   - salto: ver al personaje en el aire con las alas abiertas y un impacto con cadenas;
   - caída del Lucero: estrella visible cayendo del cielo;
   - estallido: plumas y onda.
   Revisar también las escuelas base.
4. **La noche se ve demasiado oscura.** Quiere un azul profundo legible con luces cálidas (fogatas, braseros), como en
   la referencia nocturna. Revisar `World/DayNightCycle.cs` y la luz ambiente que pone `MundoBuilder.Atlas.Ambiente`.
5. **Acabado del mapa.** Los acantilados de ladera parecen bloques apilados (`MundoBuilder.Terreno.Acantilados`). Las
   plazas de los pueblos quedan vacías. Falta vida ambiental: luciérnagas, hojas y animales pequeños.

## Cómo trabajar

- **Compilación rápida sin abrir Unity**: el compilador Roslyn incluido en Unity, en
  `Editor/Data/NetCoreRuntime/dotnet.exe` + `Editor/Data/DotNetSdkRoslyn/csc.dll`. Se le pasan como referencias
  `Editor/Data/Managed/UnityEngine/*.dll`, `NetStandard/ref/2.1.0/netstandard.dll` y
  `Library/ScriptAssemblies/Unity.InputSystem.dll`, con las definiciones `UNITY_EDITOR;ENABLE_INPUT_SYSTEM`.
- **Pruebas**: `Unity -batchmode -projectPath . -runTests -testPlatform EditMode|PlayMode -testResults r.xml`.
- **Ejecutable**: `-executeMethod Vhaldren.EditorTools.VhaldrenBuild.BuildWindows`. Genera `Builds/Windows/Vhaldren.exe`
  con las escenas MainMenu, MundoVhaldren y WorldPrototype001.
- Los `.cs` usan finales de línea CRLF.
- **PC del dueño**: 12 GB de RAM y GTX 1650. Hay que mantener los FPS y no abrir dos Unity a la vez.
- **Arte**: todo original. La extracción de V Rising del compañero del dueño es solo referencia de números y
  mecánicas: nunca copiar sus archivos, nombres ni arte.
