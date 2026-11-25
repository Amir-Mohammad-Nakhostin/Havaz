# HAVAZ

**Environmental intelligence for outdoor activity planning.**

HAVAZ is a Persian-first software prototype that combines a web application, a .NET backend and a separate Python machine-learning service to support environmental risk assessment.

## Project at a glance

| Layer | Technology |
| --- | --- |
| Web interface | React / TypeScript |
| Application backend | ASP.NET Core / .NET 8 |
| ML service | Python / FastAPI |
| Database | PostgreSQL |
| Local deployment | Docker Compose |
| Weather data attribution | Open-Meteo |

## Engineering focus

- Separate application and ML services.
- Reproducible local setup and database migrations.
- Environmental data integration and risk estimation.

**Status:** Development prototype. See `docs/` for implementation details and limitations. The risk estimates are not a guarantee of outdoor safety. Model accuracy and production readiness are not asserted here.

## Run locally

### ML
```bash
cd ml
python3 -m venv .venv && . .venv/bin/activate
pip install -r requirements.txt
python training/train.py
uvicorn service.main:app --port 8001
```

### Backend
Install .NET 8 SDK and PostgreSQL, set `ConnectionStrings__Default`, then:
```bash
dotnet restore backend/Havaz.sln
dotnet ef database update --project backend/Havaz.Infrastructure --startup-project backend/Havaz.Api
dotnet run --project backend/Havaz.Api
```

### Frontend
```bash
cd frontend
npm install
npm run dev
```

The full implementation and limitations are documented in `docs/`. Open-Meteo attribution: https://open-meteo.com/

## Docker (full local stack)
```
POSTGRES_PASSWORD=choose-a-password docker compose up --build
# frontend http://localhost:3000  backend http://localhost:8080  ml http://localhost:8001
dotnet ef database update --project backend/Havaz.Infrastructure --startup-project backend/Havaz.Api \
  --connection "Host=localhost;Port=5432;Database=havaz;Username=havaz;Password=choose-a-password"
```
