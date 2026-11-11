# Furniture E-Commerce

[![CI](https://github.com/nartcandan/furniture-ecommerce/actions/workflows/ci.yml/badge.svg)](https://github.com/nartcandan/furniture-ecommerce/actions/workflows/ci.yml)

A single-seller online store for home furniture and decoration. Customers can browse products, manage a cart, and pay by card. Admins manage the catalog, stock, and orders.

> **Status:** In development. The catalog API (categories, products, stock, and product images) and JWT authentication work, and catalog changes require the admin role. The cart, checkout, and the frontend are not built yet.

## Tech stack

- **Backend:** .NET 10, ASP.NET Core Web API, EF Core, ASP.NET Core Identity with JWT
- **Frontend:** React, TypeScript, Vite, Tailwind CSS
- **Database:** PostgreSQL
- **Payments:** iyzico (sandbox)
- **Infrastructure:** Docker Compose, GitHub Actions

## Running the API locally

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download) and Docker.

1. Create your local settings and set your own `POSTGRES_PASSWORD` in `.env`:

   ```shell
   cp .env.example .env
   ```

2. Start PostgreSQL. It listens on port `5433`, so it does not clash with a local PostgreSQL on `5432`:

   ```shell
   docker compose up -d --wait
   ```

3. From the `backend` folder, store the connection string, the JWT signing key and the first admin account with [User Secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets). Use the password from `.env` and a random signing key of at least 32 characters. The admin password needs at least 8 characters with an uppercase letter, a lowercase letter and a digit. The API creates this admin account when it starts:

   ```shell
   cd backend
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=127.0.0.1;Port=5433;Database=furniture_ecommerce;Username=furniture_app;Password=YOUR_PASSWORD" --project src/FurnitureEcommerce.Api
   dotnet user-secrets set "Jwt:SigningKey" "YOUR_RANDOM_SIGNING_KEY" --project src/FurnitureEcommerce.Api
   dotnet user-secrets set "AdminUser:Email" "admin@example.com" --project src/FurnitureEcommerce.Api
   dotnet user-secrets set "AdminUser:Password" "YOUR_ADMIN_PASSWORD" --project src/FurnitureEcommerce.Api
   ```

4. Create the database tables:

   ```shell
   dotnet tool restore
   dotnet build FurnitureEcommerce.slnx
   dotnet tool run dotnet-ef database update --project src/FurnitureEcommerce.Infrastructure --startup-project src/FurnitureEcommerce.Api
   ```

5. Start the API:

   ```shell
   dotnet run --project src/FurnitureEcommerce.Api --launch-profile http
   ```

The API runs at `http://localhost:5068`, and the interactive API reference is at `http://localhost:5068/scalar`.

To stop PostgreSQL, run `docker compose down` from the project root. Your data is kept.

## Using the API

Anyone can read the active catalog, register and log in. Catalog changes need the admin role.

| Endpoints | Access |
|---|---|
| `POST /api/auth/register`, `POST /api/auth/login` | Public |
| `GET /api/categories`, `GET /api/products` and their detail routes | Public, active records only |
| `POST`, `PUT`, `PATCH`, `DELETE` on categories, products, stock and images | Admin |
| `GET /api/admin/categories`, `GET /api/admin/products`, `GET /api/admin/products/{id}` | Admin, including inactive records |

New accounts get the customer role. To make admin requests, log in with the admin account from step 3 and send the `accessToken` from the response in the `Authorization` header:

```http
Authorization: Bearer YOUR_ACCESS_TOKEN
```

Tokens are valid for one hour. After that, log in again. An account is locked for five minutes after five failed login attempts.

## Running the tests

From the `backend` folder:

```shell
dotnet test FurnitureEcommerce.slnx
```

Docker must be running. The integration tests start their own temporary PostgreSQL container, so they do not touch your local database.

GitHub Actions builds the backend and runs the same tests on every push to `main` and on every pull request.
