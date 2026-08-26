# Fundação técnica

## Topologia inicial

```text
Cloudflare
  -> Nginx
       -> React
       -> ASP.NET Core API
            -> PostgreSQL/PostGIS
            -> Cloudflare R2
            -> Google Maps
            -> Mercado Pago
```

O banco fica em rede privada. A API é a autoridade de autenticação, autorização e regras de negócio.
