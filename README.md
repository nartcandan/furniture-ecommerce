# Furniture E-Commerce

[![CI](https://github.com/nartcandan/furniture-ecommerce/actions/workflows/ci.yml/badge.svg)](https://github.com/nartcandan/furniture-ecommerce/actions/workflows/ci.yml)

A single-seller online store for home furniture and decoration. Customers can browse products, manage a cart, and pay by card. Admins manage the catalog, stock, and orders.

> **Status:** In development. The catalog API (categories, products, stock, and product images) and JWT authentication work, and catalog changes require the admin role. Signed-in users can manage their addresses and their cart, turn the cart into an order, pay for it on iyzico's sandbox payment page and cancel it on the day of payment. Admins can list orders and mark them as shipped and delivered. A background job closes orders that were never paid. The frontend is not built yet.

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
| `GET /api/admin/orders`, `GET /api/admin/orders/{id}`, `POST /api/admin/orders/{id}/ship`, `POST /api/admin/orders/{id}/deliver` | Admin, orders of all customers |
| `GET`, `POST`, `PUT`, `DELETE` on `/api/addresses` | Signed-in user, own addresses only |
| `GET /api/cart`, `POST /api/cart/items`, `PUT` and `DELETE /api/cart/items/{productId}`, `DELETE /api/cart` | Signed-in user, own cart only |
| `POST /api/orders`, `GET /api/orders`, `GET /api/orders/{id}`, `POST /api/orders/{id}/cancel` | Signed-in user, own orders only |
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

### Cancelling an order

`POST /api/orders/{id}/cancel` cancels the whole order, refunds the payment at iyzico and puts the items back in stock.

- Only a `Paid` order can be cancelled, and only on the calendar day (Turkey time) it was paid, because iyzico cancels a payment only on that day. The `canCancel` field of an order shows whether this is still possible.
- The order is first set to `Cancelling`, which stops it from being shipped while the refund is in progress.
- If iyzico refuses the refund, the order goes back to `Paid` and the request gets `502 Bad Gateway`.
- If iyzico cannot be reached, the result is unknown, so the order stays in `Cancelling` and the request gets `202 Accepted`. The background job finishes it.

### Order statuses

| Status | Meaning | Next |
|---|---|---|
| `PendingPayment` | Created, stock is held, waiting for the payment | `Paid`, `PaymentFailed`, `Cancelled` (not paid in time) |
| `PaymentFailed` | The payment failed, stock was given back | |
| `Paid` | The payment arrived | `Shipped` (admin), `Cancelling` (customer, same day) |
| `Cancelling` | The refund is in progress, the order cannot be shipped | `Cancelled`, or back to `Paid` if the refund is refused |
| `Cancelled` | Refunded or never paid, stock was given back | |
| `Shipped` | Sent to the customer, can no longer be cancelled | `Delivered` (admin) |
| `Delivered` | Received by the customer | |

Admins list orders with `GET /api/admin/orders`, which accepts `status`, `page` and `pageSize`, for example `?status=Paid&page=1&pageSize=20`. Every status change checks the current status inside the database update itself, so a cancellation and a shipment that arrive at the same moment cannot both succeed.

### Background job

A payment can end without the API hearing about it: the customer closes the payment page, the callback never arrives, or a refund request gets no answer. A background job (`OrderReconciliationWorker`) runs every minute and finishes these orders.

- An order that has waited for payment for more than 35 minutes is checked at iyzico. If it was paid after all, it becomes `Paid`. Otherwise it is closed and its items go back in stock. The limit is longer than the 30 minutes an iyzico payment page stays valid, so an order is never closed while it can still be paid.
- An order that has been in `Cancelling` for more than 2 minutes gets its refund requested again. iyzico refuses to cancel the same payment twice, so this cannot refund twice. The order becomes `Cancelled`, or `Paid` again if iyzico refuses the refund.
- If iyzico cannot be reached, the order is left as it is and tried again on the next run.

The intervals are in the `Reconciliation` section of `appsettings.json`.

## Running the tests

From the `backend` folder:

```shell
dotnet test FurnitureEcommerce.slnx
```

Docker must be running. The integration tests start their own temporary PostgreSQL container, so they do not touch your local database.

GitHub Actions builds the backend and runs the same tests on every push to `main` and on every pull request.
