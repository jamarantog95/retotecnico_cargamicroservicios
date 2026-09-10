# Ejecucion con Docker Compose

## Requisitos

Instala Docker Desktop y verifica que este en ejecucion con `docker version`.

Crea un archivo `.env` en la raiz del repositorio. No lo agregues al control de versiones:

```env
SQL_SA_PASSWORD=UnaClaveSegura!123
SMTP_PASSWORD=tu-contrasena-de-aplicacion-smtp
```

`SMTP_PASSWORD` es opcional para probar el resto del flujo. Sin ella, el microservicio de notificaciones no podra autenticar el envio de correos.

## Inicio

Desde la raiz del repositorio ejecuta:

```powershell
docker compose up --build
```

Todos los servicios se inician con ese comando. Antes de las APIs, `database-init` crea o actualiza las tablas, los indices y los procedimientos almacenados desde los scripts de `database/`. Sus puntos de entrada son:

| Servicio            | URL                                            |
| ------------------- | ---------------------------------------------- |
| Cliente web         | http://127.0.0.1:5500/src/ClientWeb/index.html |
| Auth                | http://localhost:5027                          |
| Control             | http://localhost:5299                          |
| Procesamiento       | http://localhost:5149                          |
| Notificaciones      | http://localhost:5000                          |
| RabbitMQ Management | http://localhost:15672                         |
| SeaweedFS Filer     | http://localhost:8888                          |

Los proyectos se ejecutan con `ASPNETCORE_ENVIRONMENT=Development`, por lo que Swagger queda disponible en la raiz de cada API.

Para detener los contenedores, usa `docker compose down`. Para eliminar tambien los datos persistidos de SQL Server, RabbitMQ y SeaweedFS, usa `docker compose down --volumes`.