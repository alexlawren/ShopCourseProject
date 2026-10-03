# Catalog gRPC Stock Reservation Specification

## Overview

The `StockReservationService` is an internal, service-to-service gRPC contract implemented by `Shop.CatalogService` and defined in `contracts/grpc/catalog_stock.proto`.

### Architectural Context
- **Ownership of Stock**: `CatalogService` is the sole owner and authority for product stock quantities and inventory persistence. `OrderService` (and other services) must never query or modify `shop_catalog` directly.
- **Internal Service-to-Service Protocol**: The gRPC service runs on a dedicated internal HTTP/2 cleartext port (`http://localhost:5059` in Development, configurable in Docker via Kestrel settings).
- **Security & Network Boundary**: This endpoint is private and internal. It is never exposed by the API Gateway (`Shop.Gateway`) or accessible from public web clients.
- **REST Catalog Coexistence**: The existing public HTTP/1.1 REST API of `CatalogService` (`http://localhost:5058`) and its `/health` probe remain completely unaffected and accessible.

---

## Service Definition

```protobuf
service StockReservationService {
  rpc ReserveStock (ReserveStockRequest) returns (ReserveStockResponse);
  rpc ReleaseReservation (ReleaseReservationRequest) returns (ReleaseReservationResponse);
  rpc CommitReservation (CommitReservationRequest) returns (CommitReservationResponse);
  rpc CancelCommittedReservation (CancelCommittedReservationRequest) returns (CancelCommittedReservationResponse);
}
```

---

## Operations & Semantics

### 1. `ReserveStock`
Atomically allocates and reserves stock for a list of products.

#### Request (`ReserveStockRequest`)
- `request_id` (string, required): A client-generated non-empty GUID string acting as an **idempotency key**.
- `items` (repeated `ReserveStockItem`, 1 to 100 items):
  - `product_id` (string, required): Non-empty GUID string of the product.
  - `quantity` (int32, required): Quantity to reserve (range 1 to 1000). Duplicate `product_id`s in a single request are rejected.

#### Behavior & Idempotency
1. **Idempotent Retry**: If a reservation with the given `request_id` already exists in `shop_catalog`, `CatalogService` returns the existing reservation details immediately without decrementing stock again.
2. **Pre-Conditions Check**: All requested products must exist, must be active (`IsActive == true`), their categories must be active (`Category.IsActive == true`), and current stock must be greater than or equal to requested quantities.
3. **Atomic Multi-Item Execution**: Stock is decremented inside a database transaction using concurrent-safe conditional updates:
   ```sql
   UPDATE "Products"
   SET "StockQuantity" = "StockQuantity" - @qty, "UpdatedAtUtc" = @now
   WHERE "Id" = @id AND "StockQuantity" >= @qty AND "IsActive" = TRUE
   ```
   If any item cannot be reserved (e.g. concurrent race condition reduced stock, or out of stock), the transaction is immediately rolled back and **no stock is modified for any product in the request**.
4. **Historical Snapshot**: On success, `StockReservation` and `StockReservationItem` records are created. The snapshot stores `ProductName` and `UnitPrice` (represented as minor units `unit_price_minor` = price * 100 to avoid floating point precision loss).

#### Response (`ReserveStockResponse`)
- `success` (bool): `true` if reserved; `false` on business validation failure.
- `reservation_id` (string): GUID of the created/existing reservation (empty on failure).
- `items` (repeated `ReservedStockItem`): List of items with `product_id`, `product_name`, `unit_price_minor`, and `quantity`.
- `error_code` (string): Business error code on failure (`PRODUCT_NOT_FOUND`, `PRODUCT_INACTIVE`, `CATEGORY_INACTIVE`, `OUT_OF_STOCK`).
- `error_message` (string): Human-readable error description.

---

### 2. `ReleaseReservation`
Releases an active uncommitted reservation back to inventory (compensating action on checkout cancellation before durable order persistence).

#### Request (`ReleaseReservationRequest`)
- `reservation_id` (string, required): GUID of the reservation to release.

#### Behavior & Transitions
- If reservation does not exist: throws `RpcException` with `StatusCode.NotFound`.
- If reservation status is `Released`: returns `success = true` (idempotent; no duplicate inventory replenishment).
- If reservation status is `Committed` or `Cancelled`: throws `RpcException` with `StatusCode.FailedPrecondition` (`ReleaseReservation` must never be used for committed or cancelled orders).
- If reservation status is `Reserved`:
  - In a database transaction, restores `StockQuantity += item.Quantity` for each item.
  - Updates reservation status to `Released`.

---

### 3. `CommitReservation`
Permanently confirms and commits the reserved stock (called upon successful durable order placement).

#### Request (`CommitReservationRequest`)
- `reservation_id` (string, required): GUID of the reservation to commit.

#### Behavior & Transitions
- If reservation does not exist: throws `RpcException` with `StatusCode.NotFound`.
- If reservation status is `Committed`: returns `success = true` (idempotent).
- If reservation status is `Released` or `Cancelled`: throws `RpcException` with `StatusCode.FailedPrecondition`.
- If reservation status is `Reserved`:
  - Updates reservation status to `Committed`.

---

### 4. `CancelCommittedReservation`
Safely cancels a previously committed reservation and restores inventory to catalog products. Used during order cancellation.

#### Request (`CancelCommittedReservationRequest`)
- `reservation_id` (string, required): GUID of the committed reservation to cancel.

#### Response (`CancelCommittedReservationResponse`)
- `success` (bool): `true` when cancelled or already cancelled.
- `message` (string): Status message.

#### Behavior & Concurrency Protection
1. If reservation does not exist: throws `RpcException` with `StatusCode.NotFound`.
2. Atomic conditional status claim:
   ```sql
   UPDATE "StockReservations"
   SET "Status" = 'Cancelled', "UpdatedAtUtc" = @now
   WHERE "Id" = @id AND "Status" = 'Committed'
   ```
3. Winner semantics:
   - If `affectedRows == 1`: The winning caller restores `StockQuantity += item.Quantity` for each reservation item and commits the transaction.
   - If `affectedRows == 0`: Reloads final status:
     - If `Cancelled`: Returns idempotent `success = true` without modifying stock.
     - If `Reserved` or `Released`: Throws `RpcException` with `StatusCode.FailedPrecondition` (reservation is not in `Committed` state).

---

## Reservation State Machine

```
        ┌──────────────────┐
        │     Reserved     │
        └────────┬─────────┘
                 │
        ┌────────┴─────────┐
        ▼                  ▼
┌───────────────┐  ┌───────────────┐
│   Committed   │  │   Released    │
└───────┬───────┘  │  (Terminal)   │
        │          └───────────────┘
        ▼
┌───────────────┐
│   Cancelled   │
│  (Terminal)   │
└───────────────┘
```

- **Allowed Transitions**:
  - `Reserved → Committed` (on order checkout completion)
  - `Reserved → Released` (on pre-order failure compensation)
  - `Committed → Cancelled` (on order cancellation)
- **Terminal States**:
  - `Released`: terminal.
  - `Cancelled`: terminal.
- **Idempotent Transitions**:
  - `Committed → Committed` (Success)
  - `Released → Released` (Success)
  - `Cancelled → Cancelled` (Success)
- **Forbidden Transitions**:
  - `Committed → Released` (`FailedPrecondition`)
  - `Released → Committed` (`FailedPrecondition`)
  - `Reserved → Cancelled` (`FailedPrecondition`)
  - `Cancelled → Committed` (`FailedPrecondition`)
  - `Cancelled → Released` (`FailedPrecondition`)
