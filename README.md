# QuickBite API

Backend REST API for a food delivery platform — ZTP group project.


## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

## Run

```bash
cd QuickBite.API
dotnet run
```

Swagger UI: http://localhost:5000/swagger

## Auth endpoints

| Method | URL | Description |
|--------|-----|-------------|
| POST | `/api/auth/register` | Register (Customer / Restaurant / Courier) |
| POST | `/api/auth/login` | Login — returns JWT token |

**Register body example:**
```json
{
  "fullName": "Anna Kowalska",
  "email": "anna@example.com",
  "password": "Secret1",
  "role": 0
}
```
Roles: `0` = Customer, `1` = Restaurant, `2` = Courier

**Using JWT in Swagger:** click *Authorize* and paste `Bearer <token>`.

## Order state machine

```
Pending ──[Restaurant]──► Accepted ──[Restaurant]──► InPreparation ──[Restaurant]──► ReadyForPickup
   │                          │                                                             │
[Customer]               [Customer]                                                     [Courier]
   ▼                          ▼                                                             ▼
Cancelled               Cancelled                                                    OutForDelivery
                                                                                          │
                                                                                       [Courier]
                                                                                          ▼
Pending ──[Restaurant]──► Rejected                                                    Delivered
```

## Background service

Runs every minute and automatically:
- **Rejects** orders stuck in `Pending` for more than 15 minutes (restaurant did not respond)
- **Delivers** orders stuck in `OutForDelivery` for more than 45 minutes
