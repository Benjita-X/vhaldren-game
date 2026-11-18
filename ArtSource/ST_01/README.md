# ST_01 — Cimiento erosionado

Paquete de trabajo de la pieza modular de piedra para Vhaldren. Se conservan las fuentes de Blender, auditorías y renders de las iteraciones, junto con la única exportación FBX disponible. Esta carpeta está fuera de `Assets/`: los `.blend` son fuentes de arte, no recursos importados por Unity.

## Estado de integración

- **Última fuente:** `ST_01_v0008.blend`. Es una iteración de exploración, no una pieza aprobada para Unity. Su auditoría registra topología cerrada y limpia, pero la malla es bastante más densa que v005, sus dimensiones Y son 1.009 en vez del contrato 1.0 y todavía no tiene mapas horneados ni FBX.
- **Último paquete exportado:** `Export_Unity/ST_01_Cimiento.fbx`, derivado de v005. El verificador adjunto registra LOD0 de 618 triángulos, LOD1 de 308, colisión UCX de 12, UV y los mapas Albedo, AO, Normal y Roughness. La importación, escala, colisión y materiales **aún no se han validado dentro de Unity**.
- Hasta completar esa prueba, v005 es la referencia exportable y v0008 sigue siendo WIP. No marcar v0008 como reemplazo de v005 ni integrarlo a una escena de juego todavía.

## Historial auditado

| Versión | Evidencia incluida | Resultado / estado |
|---|---|---|
| v001–v003 | Archivos `.blend`, auditorías e imágenes | Iteraciones de forma; el informe describe deformación con aspecto de tela/almohada. |
| v004 | `.blend`, auditoría, renders y mapas | Transición a cortes booleanos y detalle horneado. |
| v005 | `.blend`, informe, auditoría y renders | Referencia exportada: 2 × 1 × 0.5 m, 618 / 308 tris, 0 aristas abiertas según el informe, un material y UV. Incluye FBX y texturas en `Export_Unity/`. |
| v0006 | `.blend`, auditoría, texturas y renders | 2,782 / 1,250 tris; 5 aristas non-manifold; no usar como candidata final. |
| v0007 | `.blend`, auditoría, texturas y renders | 2,806 / 1,262 tris; 6 aristas non-manifold; fue la fuente de referencia de v0008. |
| v0008 | `.blend`, auditoría y render | 3,300 / 1,400 tris; 0 non-manifold, 0 bordes abiertos, 0 caras degeneradas, 1 UV y 1 material. Dimensiones de la auditoría Blender: 2.0001 × 1.009 × 0.5002 (X/Y/Z). Aún no exportada. |

Los números de v005 provienen del informe y del inventario del FBX; los de v0006–v0008 proceden de sus JSON incluidos. No se presentan como una prueba de importación o de ejecución en Unity.

## Estructura

- `ST_01_Cimiento_v001.blend` … `ST_01_Cimiento_v005.blend`: historial de la fuente.
- `ST_01_Cimiento_v005_export.blend`: escena de exportación usada para preparar v005.
- `ST_01_v0006.blend` … `ST_01_v0008.blend`: iteraciones posteriores.
- `v00*_auditoria.json` y `Reports/`: métricas de malla.
- `v00*_*.png`, `Renders/` y `v0006_texturas/` / `v0007_texturas/`: referencias visuales y mapas entregados por cada etapa.
- `Export_Unity/`: FBX v005, texturas y verificación estructural.
- `Scripts/_build_ST_01_v0008.py`: script de la iteración v0008 con rutas relativas para poder mover el paquete entre máquinas.

Se excluyeron los archivos `.blend1` porque son respaldos automáticos de Blender, no versiones nombradas del historial.

## Reproducibilidad y pendientes

El script v0008 conserva el procedimiento de modelado original; solo se hicieron portables las rutas de entrada y salida. El script espera que la sesión de Blender tenga abierta la escena v0007 con los objetos `ST_01_Cimiento` y `Cam`; no abre por sí mismo el archivo fuente. No se ejecutó de nuevo al empaquetar este material.

Siguiente validación antes de promover una versión: decidir si se conserva la dimensión Y de v0008 o se restablece el contrato 2 × 1 × 0.5 m; revisar la apariencia final; hornear los mapas para la topología elegida; exportar FBX/colisión/LODs; importar el paquete y comprobar escala, pivote, materiales y colisión en Unity 6.

### Nota de consistencia del informe v005

`ST_01_informe.md` todavía lista como pendiente convertir el albedo a imagen. Sin embargo, `Export_Unity/verificacion_fbx.json` enumera `T_ST_01_Albedo.png` junto con AO, Normal y Roughness. Este paquete registra ambos datos para que se confirme el estado en Unity; el inventario del FBX confirma la presencia del archivo, no su visualización correcta dentro del motor.