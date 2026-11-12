# 018 — Consolidación antes del modelado y render

Fecha: 12 de noviembre de 2026.
Base: `feature/017-integracion-jugable-pueblos-combate`.
Rama: `feature/018-consolidacion-pre-modelado`.

## Objetivo

Detener temporalmente la expansión de contenido y cerrar incoherencias de física, daño, input, misiones y
pipeline de escena antes de invertir horas en Blender. La 017 ya es una vertical slice amplia; la 018 la
convierte en una base más segura para sustituir placeholders por arte final.

## Correcciones de física

Los `NonAlloc` de Unity no garantizan conservar el impacto más cercano cuando llenan su buffer. En un
mundo vacío esto casi no se nota; con árboles, casas, cuevas, props y jugadores sí.

Se añadieron fallbacks exhaustivos al saturarse para:

- proyectiles mágicos;
- apuntado y línea de visión de hechizos;
- cadena de rayos y áreas;
- poder racial infernal;
- candidato de absorción de esencia;
- alertas y curación de campamentos;
- puntero de construcción.

La ballesta física y la línea de visión de enemigos ya tenían una protección equivalente.

## Regla única de daño

Impactos normales deben entrar por:
`fuente → CombatDamageReceiver → defensa/resistencias/modificadores/absorción → CombatHealth → feedback`.

Ignición y descarga de Estática ya no llaman directamente a `CombatHealth`.
Se añadió `CombatDamageKind.DamageOverTime`: usa resistencia/multiplicadores mágicos pero no genera
hit-stop ni knockback repetitivo. El daño de Ignición conserva al atacante y se acredita a su
`PlayerDamageTracker`.

Las ejecuciones/absorciones que rematan deliberadamente a una víctima conservan su operación directa de
vida porque representan una transición de estado, no un golpe defendible.

## Input y construcción

Las diez ranuras de construcción entraron en `GameInput` y son reasignables. Pueden compartir 1–3 con
magia porque ambos contextos son excluyentes. La interfaz aclara qué atajos permanecen fijos.

El puntero de construcción ya no depende de un raycast truncado en escenas densas.

## Misiones

El HashSet usado para el contador global de enemigos no bloquea el progreso de misiones. Una criatura
derrotada antes de aceptar un encargo puede reaparecer y contar después de aceptar el encargo.

## Protección del mundo artístico

Antes de 018, `WorldPrototypeSceneTool` podía sobrescribir `WorldPrototype001.unity` y borrar/recrear
`TerrenoMundo.asset`. Eso era incompatible con producción artística.

Desde 018:
- `WorldPrototype001.unity` es persistente y no se genera.
- El menú de blockout escribe `Assets/Scenes/WorldBlockoutGenerated.unity`.
- Sus recursos viven bajo `Assets/WorldBlockout`.
- El blockout queda deshabilitado en Build Settings.
- Solo MainMenu y WorldPrototype001 quedan habilitados para la build normal.

## Contrato Blender → Unity

Se añadió `AuthoredVisual`. Un FBX/prefab final se coloca como hijo visual del objeto lógico y se marca
con ese componente. `EnemyVisualPrototype` y `EnemyArchetypeVisual` dejan de crear cuerpos/armas
provisionales encima. Los prefabs KayKit también reciben la marca al generarse.

El modelo visual puede cambiar malla, materiales, rig, animaciones, ropa, armas y LOD. No debe contener la
autoridad del gameplay. Mantener en la raíz Unity: vida, receptor de daño, IA, CharacterController,
colliders jugables, nivel, botín y lógica de red futura.

Escala recomendada: 1 unidad Unity = 1 metro. Antes de exportar, aplicar transformaciones y fijar pivote de
personajes en los pies; para piezas modulares, pivote en el punto de encaje definido por el kit.

## Regresiones nuevas

Se añadieron pruebas para: buffers saturados de proyectil/LoS, resistencia de Ignición y Estática,
atribución de DOT, explosión racial detrás de pared, ranuras de construcción reasignables, enemigo
reaparecido en misión y protección de visuales autorales.

## Validación pendiente

La 017 fue reportada por Claude con 81/81 EditMode y 113/113 PlayMode en Unity 6000.2.12f1. Esos números
no cubren 018.

Antes de fusionar 018: compilar, ejecutar EditMode + PlayMode completos, abrir MainMenu y
WorldPrototype001, comprobar Console sin excepciones, recorrer combate/magia/construcción/misión/jefe y
hacer una build de Windows. Después de eso puede comenzar el reemplazo sistemático de placeholders.

## Primer paquete artístico recomendado

No producir cientos de assets todavía. Crear primero un kit pequeño que pruebe el pipeline completo:
una fachada/casa modular de la primera villa, una entrada de cueva con un módulo interior, un enemigo
original riggeado y una pieza de construcción final. Si escala, pivotes, materiales, animaciones,
colliders y rendimiento pasan, usar ese estándar para producir variantes.
