# Furniture E-Commerce

[![CI](https://github.com/nartcandan/furniture-ecommerce/actions/workflows/ci.yml/badge.svg)](https://github.com/nartcandan/furniture-ecommerce/actions/workflows/ci.yml)

A single-seller online store for home furniture and decoration. Customers can browse products, manage a cart, and pay by card. Admins manage the catalog, stock, and orders.

> **Status:** In development. The catalog API (categories, products, stock, and product images) and JWT authentication work, and catalog changes require the admin role. Signed-in users can manage their addresses and their cart, turn the cart into an order and pay for it on iyzico's sandbox payment page. Order cancellation, admin order management, the clean-up of unpaid orders and the frontend are not built yet.

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

3. From the `backend` folder, store the connection string, the JWT signing key, the first admin account and your iyzico sandbox keys with [User Secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets). Use the password from `.env` and a random signing key of at least 32 characters. The admin password needs at least 8 characters with an uppercase letter, a lowercase letter and a digit. The API creates this admin account when it starts. The iyzico keys come from a free [sandbox account](https://sandbox-merchant.iyzipay.com), and the API does not start without them:

   ```shell
   cd backend
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=127.0.0.1;Port=5433;Database=furniture_ecommerce;Username=furniture_app;Password=YOUR_PASSWORD" --project src/FurnitureEcommerce.Api
   dotnet user-secrets set "Jwt:SigningKey" "YOUR_RANDOM_SIGNING_KEY" --project src/FurnitureEcommerce.Api
   dotnet user-secrets set "AdminUser:Email" "admin@example.com" --project src/FurnitureEcommerce.Api
   dotnet user-secrets set "AdminUser:Password" "YOUR_ADMIN_PASSWORD" --project src/FurnitureEcommerce.Api
   dotnet user-secrets set "Iyzico:ApiKey" "YOUR_SANDBOX_API_KEY" --project src/FurnitureEcommerce.Api
   dotnet user-secrets set "Iyzico:SecretKey" "YOUR_SANDBOX_SECRET_KEY" --project src/FurnitureEcommerce.Api
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

Anyone can read the active catalog, register and log in. Addresses, the cart and orders need a signed-in user. Catalog changes need the admin role.

| Endpoints | Access |
|---|---|
| `POST /api/auth/register`, `POST /api/auth/login` | Public |
| `GET /api/categories`, `GET /api/products` and their detail routes | Public, active records only |
| `POST`, `PUT`, `PATCH`, `DELETE` on categories, products, stock and images | Admin |
| `GET /api/admin/categories`, `GET /api/admin/products`, `GET /api/admin/products/{id}` | Admin, including inactive records |
| `GET`, `POST`, `PUT`, `DELETE` on `/api/addresses` | Signed-in user, own addresses only |
| `GET /api/cart`, `POST /api/cart/items`, `PUT` and `DELETE /api/cart/items/{productId}`, `DELETE /api/cart` | Signed-in user, own cart only |
| `POST /api/orders`, `GET /api/orders`, `GET /api/orders/{id}` | Signed-in user, own orders only |
| `POST /api/payments/callback` | Called by the customer's browser when it returns from iyzico |

New accounts get the customer role. To make signed-in requests, log in and send the `accessToken` from the response in the `Authorization` header. For admin requests, log in with the admin account from step 3:

```http
Authorization: Bearer YOUR_ACCESS_TOKEN
```

Tokens are valid for one hour. After that, log in again. An account is locked for five minutes after five failed login attempts.

### Cart

Add a product with `POST /api/cart/items` and a body such as `{ "productId": "...", "quantity": 2 }`. Adding the same product again increases its quantity. `PUT /api/cart/items/{productId}` with `{ "quantity": 3 }` sets the quantity.

- The cart does not store prices. Every response shows the current price, the line totals and the cart total.
- A quantity above the current stock is rejected with `409 Conflict`. The cart does not reserve stock.
- If a product is deactivated or its stock falls below the quantity in the cart, the line is flagged with `isAvailable: false` or `hasInsufficientStock: true`, and `canCheckout` is `false` until the customer fixes the cart.

### Orders

`POST /api/orders` with `{ "addressId": "..." }` creates an order from the cart.

- The order copies the address, the product names and the current prices, so later changes do not affect it.
- The items are taken out of stock in the same transaction. If any item is unavailable or short of stock, the request is rejected with `409 Conflict` and no stock changes.
- A customer can have only one order waiting for payment. A second request, such as a double click, gets `409 Conflict`.
- The cart is kept until the order is paid.

### Payment

The response of `POST /api/orders` contains `paymentPageUrl`, an iyzico payment page for that order. Open it in a browser and pay with one of iyzico's sandbox test cards, for example `5528790000000008` with any future expiry date and any CVC.

- After the payment, iyzico sends the browser to `POST /api/payments/callback`. The API does not trust that request. It asks iyzico for the result and compares the paid amount with the order.
- A successful payment sets the order to `Paid` and takes the ordered quantities out of the cart. A failed one sets it to `PaymentFailed` and puts the items back in stock.
- The callback can arrive any number of times. Only the first one changes anything.
- If money arrives for an order that is no longer waiting for payment, the payment is cancelled at iyzico right away.
- At the end the browser is sent to `Frontend:BaseUrl` (`http://localhost:5173/orders/{id}?payment=success`). The frontend is not built yet, so the browser shows a connection error there. Check the result with `GET /api/orders/{id}`.

The tests replace iyzico with a fake, so they need no keys and make no calls to iyzico.

## Running the tests

From the `backend` folder:

```shell
dotnet test FurnitureEcommerce.slnx
```

Docker must be running. The integration tests start their own temporary PostgreSQL container, so they do not touch your local database.

GitHub Actions builds the backend and runs the same tests on every push to `main` and on every pull request.
