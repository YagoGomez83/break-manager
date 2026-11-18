# ADR-0006 — Control informado y minimización de datos

- **Estado:** Aceptado (sujeto a validación de RRHH o el área legal)
- **Fecha:** 2026-11-18

## Contexto

El art. 71 de la Ley de Contrato de Trabajo (texto según la Ley 27.322) establece que los controles relativos a la actividad
del trabajador deben ser conocidos por este. Un control oculto expone a la organización y puede invalidar los registros
como respaldo de sanciones. Además rige la Ley 25.326 de Protección de Datos Personales.
El negocio quiere evitar que los operadores "simulen" actividad si conocen el mecanismo.

## Decisión

1. **Se informa la existencia** del registro de actividad y presencia, su finalidad, que no se capturan teclas, pantallas ni contenido,
   quién accede a los datos y por cuánto tiempo se conservan.
2. **No se informan los parámetros internos:** umbrales, que se mide el mouse y no el teclado, reglas de escalamiento.
3. **Aviso versionado:** el agente muestra el aviso en el primer login y ante cada versión nueva. La aceptación se registra
   (operador, versión, fecha y hora del servidor, puesto) y es requisito para operar.
4. **Minimización:** solo eventos de estado (inicio y fin de inactividad, descansos, sesiones). Nada de coordenadas, teclas, capturas ni procesos.
5. **Retención:** período definido por la organización (`DataRetention`), con purga automática (HU-52).
6. **Acceso:** solo roles autorizados. Los operadores no acceden a datos de otros.

## Consecuencias

- Los registros tienen valor como respaldo, porque el control fue notificado y aceptado.
- El texto del aviso lo redacta y aprueba la organización (pregunta Q10). El sistema solo lo presenta y registra la aceptación.
