# Reto Técnico JCAG

Aplicación compuesta por múltiples microservicios para autenticación, control de cargas de archivos Excel, procesamiento masivo, notificaciones y una interfaz web sencilla.

## Descripción general

La solución está organizada en estos componentes:

- AuthMicroservice: autenticación con JWT y acceso a usuarios.
- ControlMicroservice: gestión de cargas, historial y validación de archivos.
- BulkProcessMicroservice: procesamiento por lotes usando RabbitMQ y almacenamiento de archivos.
- NotificationMicroservice: envío de notificaciones.
- ClientWeb: frontend ligero para login y carga de archivos Excel.
- SQL Server: base de datos relacional.
- RabbitMQ: mensajería para procesamiento de eventos.
- SeaweedFS: almacenamiento de archivos.

## Arquitectura

```text
ClientWeb --> AuthMicroservice
   |             |
   |             +--> JWT / usuarios
   |
   +--> ControlMicroservice --> RabbitMQ --> BulkProcessMicroservice
                                |
                                +--> SeaweedFS
                                +--> SQL Server

NotificationMicroservice <-- RabbitMQ
```

## Requisitos

- Docker Desktop o Docker Engine
- Docker Compose
- Git
- Un archivo `.env` en la raíz del proyecto

### Variables de entorno

Crea un archivo `.env` en la raíz del repositorio:

```env
SQL_SA_PASSWORD=UnaClaveSegura!123
SMTP_PASSWORD=tu-contrasena-de-aplicacion-smtp
```

> `SMTP_PASSWORD` es opcional para pruebas básicas. Si no está configurado, el microservicio de notificaciones puede fallar al intentar enviar correos.

## Inicio rápido

Desde la raíz del proyecto ejecuta:

```bash
docker compose up --build
```

Esto levanta todos los servicios definidos en `compose.yaml`.

Para detenerlos:

```bash
docker compose down
```

Para eliminarlos junto con volúmenes persistidos:

```bash
docker compose down --volumes
```

## Servicios y puertos (Testear con Swagger)

| Servicio            | URL                                            |
| ------------------- | ---------------------------------------------- |
| Cliente web         | http://127.0.0.1:5500/src/ClientWeb/index.html |
| Auth                | http://localhost:5027                          |
| Control             | http://localhost:5299                          |
| Bulk Process        | http://localhost:5149                          |
| Notification        | http://localhost:5000                          |
| RabbitMQ Management | http://localhost:15672                         |
| SeaweedFS           | http://localhost:8888                          |
| SQL Server          | localhost:1433                                 |

## Credenciales de acceso

El usuario administrador se crea automáticamente con el script `database/03_seed_admin_user.sql`.

- Usuario: `jamarantog95u@gmail.com`
- Contraseña: `Admin123*` (según la configuración del hash cargado en la base de datos)

> Verifica siempre la contraseña exacta en el script si se requiere un acceso real en entorno local.

## Estructura del proyecto

```text
.
├── compose.yaml
├── Dockerfile
├── Dockerfile.client
├── DOCKER.md
├── database/
│   ├── 00_init_retotecnico.sql
│   ├── 01_create_stored_procedures.sql
│   ├── 02_create_trigger_carga_archivo.sql
│   └── 03_seed_admin_user.sql
├── postman/
├── src/
│   ├── ApiGateway/
│   ├── AuthMicroservice/
│   ├── BulkProcessMicroservice/
│   ├── ClientWeb/
│   ├── ControlMicroservice/
│   └── NotificationMicroservice/
└── README.md
```

## Funcionalidades principales

- Login seguro con JWT
- Subida de archivos Excel desde la web
- Validación automática del período a partir de columnas como `Periodo` o `Fecha`
- Registro de historial de cargas
- Procesamiento en segundo plano con RabbitMQ
- Persistencia en SQL Server
- Almacenamiento de archivos en SeaweedFS
- Notificaciones por correo y eventos

## Flujo de trabajo típico

1. El usuario accede a la interfaz web.
2. Se autentica contra `AuthMicroservice`.
3. Sube un archivo Excel desde `ClientWeb`.
4. `ControlMicroservice` valida y registra la carga.
5. Se publica un evento en RabbitMQ.
6. `BulkProcessMicroservice` procesa el archivo y genera resultados.
7. `NotificationMicroservice` puede enviar avisos según el flujo.

## Documentación adicional

- `DOCKER.md`: guía de ejecución con Docker Compose.
- `postman/`: colección para pruebas de endpoints.

## Notas de desarrollo

- Los microservicios se ejecutan con `ASPNETCORE_ENVIRONMENT=Development`.
- En desarrollo, Swagger queda disponible en la raíz de cada API.
- El almacenamiento de datos persistente se mantiene en Docker volumes para SQL Server, RabbitMQ y SeaweedFS.

## Comandos útiles

```bash
# levantar todo
docker compose up --build

# ver logs
docker compose logs -f

# reiniciar un servicio
docker compose restart auth

# detener todo
docker compose down
```

## Licencia

Este repositorio se entrega como proyecto técnico de demostración y evaluación interna.
