# Auditoría externa y primera integración modular — Vhaldren

Fecha: 11 de noviembre de 2026.
Rama de trabajo: `feature/004-kaykit-combat-slice`; base: `audit/correcciones-control-camara-2026-11-10`.

## 1. Fuentes inspeccionadas (código, manifiestos, licencias)

| Recurso | Referencia verificable | Licencia | Estado |
|---|---|---|---|
| [AnyRPGCore](https://github.com/AnyRPG/AnyRPGCore) | `master`, referencia auditada `d8c22c0507b67d965a0751c16fda62fd58071503` | MIT para el código; revisar cada asset tercero | **Estudiado, NO importado** |
| [AnyMMO FishNet](https://github.com/AnyRPG/anymmo-fishnet) | `main`, referencia auditada `5dc8daa811acef2ec23bfa474f13cd4e4bbcbf1a` | MIT para el código | **Estudiado, NO importado** |
| [KayKit Dungeon Remastered](https://github.com/KayKit-Game-Assets/KayKit-Dungeon-Remastered-1.0) | Archivos originales OBJ/MTL/PNG, SHAs comprobados al copiarlos | CC0 1.0; licencia incluida | **5 modelos, sus MTL y atlas importados** |
| [KayKit Character Pack Adventures](https://github.com/KayKit-Game-Assets/KayKit-Character-Pack-Adventures-1.0) | README, árbol de assets y licencia | CC0 en la distribución gratuita | **Estudiado, NO importado** |

No confundir «descargado anteriormente en otro directorio» con «integrado y compilado dentro de Vhaldren». No se ha ejecutado el Editor de Unity en este proceso.

## 2. Arquitectura externa: AnyRPGCore

**Sistemas revisados**:

- **Atributos, recursos y vida:** `CharacterStats` tiene diccionarios de estadísticas, recursos, regeneración y estados; `PowerResource` describe máximos, regeneración y tipos de salud. Dependen de gestores y modelos de AnyRPG.
- **Habilidades:** `CharacterAbilityManager` depende de `AbilityManager`, `UnitController`, `AbilityEffectContext`, animaciones y gestores globales; no es un componente aislado que se pueda copiar solo.
- **Combate:** `CharacterCombat` administra ataques, efectos, equipo, objetivos, cooldown y aggro. Integrarlo directamente crearía un segundo orquestador de combate.
- **Inventario / equipo:** `CharacterInventoryManager` depende de `BagNode`, `InventorySlot`, `LootManager`, gestores de servidor y UI; copiar la clase no crea un inventario funcional.
- **IA / enemigos:** `AggroTable`, estados y estrategias de combate existen, pero necesitan los controladores y sistemas de objetivo de AnyRPG.
- **Escenas, guardado y UI:** existen sistemas de datos, escenas, recursos y perfiles; no son independientes del framework.
- **Movimiento:** `UnitMovementController` y sus estados compiten con `LocalPlayerInput -> PlayerMotionCommand -> PlayerMotor`. `MovementRollState.Update` actualmente no implementa una esquiva completa; se preserva la de Vhaldren.

**Compatibilidad exacta pendiente:** Vhaldren está en Unity `6000.2.12f1` con render pipeline integrado. AnyRPG declara `6000.3.10f1` y URP `17.3.0`, Addressables `2.11.1`, Navigation `2.0.12`, Localization `1.5.12`, UI y otros paquetes. Ambos utilizan Input System `1.18.0`. La coincidencia del Input System ayuda, pero no resuelve los conflictos de escenas, render, prefabs ni gestión de jugadores.

**Decisión:** NO importar 370+ MB de infraestructura de golpe. Mantener Vhaldren como proyecto anfitrión y validar AnyRPG por subsistemas y contratos antes de decidir una migración o integración con puente.

## 3. Arquitectura externa: AnyMMO / FishNet

- **Dependencia principal:** el addon requiere AnyRPG; no es una librería de red autónoma lista para añadir al manifiesto de Vhaldren.
- **Gestión:** incluye `FishNetNetworkManager`, `FishNetClientConnector`, bootstrap de servidor, escenas de física y ejemplos.
- **Movimiento en red:** `FishNetUnitController` recopila `MovementData` y lo replica en ticks; usa `ReplicateData`, `ReconcileData` y `PredictionRigidbody` para predicción y reconciliación.
- **Diferencia crucial:** el locomotor actual de Vhaldren usa `CharacterController.Move` desde `Update`; el addon usa un cuerpo de movimiento basado en `Rigidbody`/predicción de FishNet. No es seguro agregar ambos al mismo avatar sin definir una única autoridad física.
- **Otros sistemas:** interacciones, aparición de personajes, autenticación, modelos y eventos de servidor. Deben adaptarse en la ruta de propiedad de cada jugador y verificarse con **host + cliente real**.
- **Seguridad:** daño, ataques y recompensas deben decidirse/verificarse en servidor. `LocalPlayerShooter` es solo una herramienta local de práctica.

**Plan de prueba para el siguiente PR:** (a) arranque de AnyRPG y del addon en Unity `6000.3.10f1`, (b) host + dos clientes en demo original, (c) réplica de `PlayerMotionCommand` de Vhaldren en ticks, (d) validación de impactos desde servidor, (e) reconciliación y pruebas bajo latencia y pérdida de paquetes. No declarar multijugador operativo antes de ejecutarlas.

## 4. KayKit: recursos artísticos

Copiados del paquete **Dungeon Remastered CC0** sin redibujar geometría:

- `barrel_small.obj` — barril y diana.
- `chest.obj` + `chest_lid.obj` — cofre compuesto.
- `pillar.obj` — pilar.
- `torch_lit.obj` — antorcha decorativa.
- `dungeon_texture.png` — atlas de materiales compartido.
- Sus archivos MTL y licencia `Assets/ThirdParty/KayKitDungeon/LICENSE.txt`.

En cada copia se comparó el SHA del blob original con el SHA importado en GitHub: **idénticos**. Hay además archivos `.meta` con GUID fijos para conservar las referencias al mover el proyecto entre equipos.

El Character Pack Adventures declara personajes riggeados, accesorios y animaciones, pero **no** se incorporó: requiere validar huesos, Avatar, escala, materiales y retargeting para evitar romper el pivote del `VisualRoot`.

## 5. Primera integración jugable de Vhaldren

No se han copiado clases de AnyRPG/FishNet para disfrazarlas de compatibilidad. Se añadió una vertical slice propia, sin dependencia nueva:

- `CombatHealth`: vida, daño, curación, derrota y restauración, independiente del controlador físico y del input.
- `LocalPlayerShooter`: al impactar una diana con `CombatHealth` aplica daño local de prueba. **No** se presenta como combate seguro en red.
- `CombatPracticeDummy`: barra/etiqueta de vida sobre el objetivo y reaparición.
- `KayKitCombatSceneTool`: menú **Vhaldren > Crear escena de prueba > Combate KayKit 001**. Copia `Movement001` a `KayKitCombat001` y agrega modelos CC0 y dos dianas, sin sobreescribir la escena base.
- EditMode: pruebas para vida e importación de modelos/atlas. PlayMode: prueba de impacto sobre un objetivo sin dañar un segundo.

**Cómo probar en Unity:**

1. Seleccionar esta rama en GitHub y abrirla con Unity `6000.2.12f1`.
2. Esperar importación completa y verificar que no haya errores en Console.
3. En el menú **Vhaldren > Crear escena de prueba > Combate KayKit 001**, crear la escena.
4. Abrir `Assets/Scenes/KayKitCombat001.unity` y pulsar Play; WASD y Espacio siguen controlando el movimiento, clic izquierdo daña las dianas.
5. Ejecutar Test Runner EditMode y PlayMode, registrar resultados y comprobar que `Movement001` original siga funcionando.
6. Para versionar la escena generada y el material `KayKitAtlas.mat`, agregarlos en un commit separado después de verificar el resultado en Unity.

## 6. Auditoría de riesgo y regresiones

| Riesgo | Medida |
|---|---|
| Duplicar controlador de movimiento | No importar otro motor; conservar `PlayerMotor` |
| Romper escenas existentes | Crear escena nueva por menú; no editar el YAML original |
| Meter paquetes incompatibles | No alterar `Packages/manifest.json` ni versión de Unity |
| Copiar assets con licencia dudosa | Solo KayKit Dungeon CC0, licencia incluida y SHAs verificados |
| Daño autoritativo confundido con disparos locales | Marcar claramente local/de prueba, reservar el servidor para multiplayer |
| Materiales de OBJ importados mal | Material atlas generado por Editor; verificar visualmente |
| Pruebas automáticas no ejecutadas | Nuevas pruebas incluidas, **pendientes de ejecutar en Unity** |

## 7. Segunda auditoría (control de calidad)

**Revisión A — integridad:** hashes originales KayKit y rutas dentro de la rama; revisar que se creen GUIDs y no haya importaciones ajenas.

**Revisión B — arquitectura y regresión:** ninguna dependencia nueva en manifest, motor de movimiento intacto, escena original intacta, daño desacoplado.

**Revisión C — ejecución y jugabilidad:** pendiente de Unity real; verificar importación OBJ/MTL/PNG, materiales, ubicación, hitbox, puntería, dianas, reaparición, pruebas existentes y 30/60/144/240 FPS. No afirmar esta tercera revisión como aprobada hasta contar con resultados.

**Criterio de fusion:** no fusionar esta rama ni las anteriores hasta que compilación, pruebas y prueba de juego hayan pasado.
