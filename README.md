# MkartLogistics Platform

A scalable logistics management API built with ASP.NET Core 6 for end-to-end order tracking, fleet management, and warehouse operations.

## Features

- Order lifecycle management (create, track, update, close)
- Real-time shipment tracking with carrier integrations
- Warehouse inventory management with bulk import
- Fleet and vehicle management with live GPS coordinates
- Customer management with tier-based segmentation
- Automated notifications via SMS, email, and webhooks
- Reporting and analytics with scheduled exports
- Document management with secure storage
- JWT-based authentication

## Getting Started

### Prerequisites

- .NET 6 SDK
- SQL Server 2019+
- Docker (optional)

### Run locally

```bash
git clone https://github.com/strobes-security/MkartLogistics.git
cd MkartLogistics
dotnet restore
dotnet run --project src/MkartLogistics.API
```

API will be available at `http://localhost:5000`. Swagger UI at `/swagger`.

## Architecture

```
MkartLogistics/
├── src/
│   ├── MkartLogistics.API        # Web API layer (Controllers, Middleware)
│   ├── MkartLogistics.Core       # Business logic, Services, Utilities
│   └── MkartLogistics.Data       # Data access, Repositories
```

## Configuration

Copy `appsettings.json` and update connection strings, JWT config, and AWS settings for your environment.

## License

Internal use only — Mkart Logistics Pvt. Ltd.
