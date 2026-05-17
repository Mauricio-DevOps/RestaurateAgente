# Restaurantes

Aplicacao operacional de restaurantes em ASP.NET Core MVC/Razor usando Supabase/PostgreSQL.

## Stack

- ASP.NET Core MVC/Razor (.NET 8 LTS)
- ASP.NET Core Identity com roles internas
- Entity Framework Core
- Supabase/PostgreSQL via `SUPABASE_DB_URL`
- xUnit para testes

## Perfis

- `MASTER`
- `ADMIN_RESTAURANTE`
- `GARCOM`

## Rotas

- `/login`
- `/master`
- `/restaurante`
- `/restaurante/cardapio`
- `/restaurante/operacao`
- `/garcom`
- `/cardapio/{restaurantId}`

## APIs

- `GET /api/public/restaurants/{restaurantId}/table-session?tableId=...`
- `POST /api/public/restaurants/{restaurantId}/order`
- `POST /api/public/restaurants/{restaurantId}/service-request`
- `GET /api/garcom/queue`
- `POST /api/garcom/events/status`

## Desenvolvimento local

Defina `SUPABASE_DB_URL` em `.env.supabase.local` para desenvolvimento local.
Em producao/Azure, configure a mesma chave nas configuracoes do Web App.

Tambem configure fora do source control:

- `ExternalLinks__WhatsAppAdminBaseUrl`
- `Sso__SigningKey`
- `InternalApi__BaseUrl`
- `InternalApi__ServiceKey`

`Sso__SigningKey` deve ser igual ao valor usado no MVC principal.
`InternalApi__ServiceKey` deve bater com a API e o MVC principal.

```bash
dotnet restore
dotnet build
dotnet test
dotnet run --project src/Restaurantes.Web
```

A aplicacao sobe em:

```text
http://localhost:5000
```

O `app.db` local fica apenas como backup/fonte historica. O app nao executa migrations EF contra o Supabase; o schema remoto e gerenciado pela importacao SQL.

## Seed

Restaurante demo:

- `Bistro da Praca`

Usuarios:

- `master@restaurantes.local`
- `admin@restaurantes.local`
- `garcom@restaurantes.local`

Senha:

- `DevPass@123`
