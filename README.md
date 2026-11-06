# Furniture E-Commerce

A single-seller online store for home furniture and decoration. Customers can browse products, manage a cart, and pay by card. Admins manage the catalog, stock, and orders.

> **Status:** In development. The catalog API (categories, products, stock, and product images) works. Authentication, cart, checkout, and the frontend are not built yet, so the catalog write endpoints are currently unprotected.

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

3. From the `backend` folder, store the connection string with [User Secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets), using the password from `.env`:

   ```shell
   cd backend
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=127.0.0.1;Port=5433;Database=furniture_ecommerce;Username=furniture_app;Password=YOUR_PASSWORD" --project src/FurnitureEcommerce.Api
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

## Running the tests

From the `backend` folder:

```shell
dotnet test FurnitureEcommerce.slnx
```

Docker must be running. The integration tests start their own temporary PostgreSQL container, so they do not touch your local database.
