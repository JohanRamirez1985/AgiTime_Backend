# Despliegue en producción

Copia de los archivos que corren en el servidor (no se usan en local).

| Archivo en el repo | Ruta en el servidor |
|---|---|
| `deploy/docker-compose.yml` | `/root/docker-compose.yml` |
| `deploy/nginx/conf.d/default.conf` | `/root/nginx/conf.d/default.conf` (montado en `agitime-nginx`) |

## Cómo se despliega

1. En el PC, desde la rama de despliegue (`produccion` o `prd-cloud`):
   - Back (este repo): `docker compose build <api>` y `docker push agitime/agitime-<api>:latest`
   - Front (repo `AgiTime_Frontend`): `docker compose build frontend` y `docker push agitime/agitime-frontend:latest`
2. En Portainer: recrear el contenedor con *re-pull image*.

El nginx de producción (`agitime-nginx`, SSL con Let's Encrypt) enruta por nombre de servicio
(`frontend`, `parametrosapi`, ...) dentro de la red externa `agitime-net`.
El `gateway/` de este repo solo se usa en local.

## Volver a la versión anterior

Las imágenes anteriores quedan en el servidor como `<none>` (`docker image ls | grep agitime`):

```bash
docker tag <IMAGE_ID_anterior> agitime/agitime-<servicio>:latest
```

y recrear el contenedor en Portainer **sin** re-pull.

## Si se cambian estos archivos

Actualizarlos aquí y copiarlos al servidor; para nginx: `docker exec agitime-nginx nginx -t && docker exec agitime-nginx nginx -s reload`.
