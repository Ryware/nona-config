# Nona Backend

Production container image: `rywaredev/nona:latest`

The standalone container publishes:

- `8080` for the HTTP API

Persistent data is stored in `/var/lib/nona`. Mount this path as a Docker volume so the database survive container restarts.

## Start With Docker Run

Single-line startup:

```bash
docker run -d --name nona --restart unless-stopped -p 18080:8080 -v nona-data:/var/lib/nona rywaredev/nona:latest
```

Nona creates persistent JWT settings automatically when `Jwt__Key`, `Jwt__Issuer`, and `Jwt__Audience` are not provided. To override them, pass all three environment variables when starting the container.


## Start With Production Compose

Copy a Docker Compose file from [deploy/compose](deploy/compose) to server and rename it to docker-compose.yml

Run it:

```bash
docker compose up -d
```

Default host ports:

- API: `http://localhost:18080`


### There is Master-Replica setup available, look at compose files.
