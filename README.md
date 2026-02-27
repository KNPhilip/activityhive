# ActivityHive

![Continuous Integration](https://github.com/KNPhilip/activityhive/workflows/Continuous%20Integration/badge.svg)
![Continuous Deployment](https://github.com/KNPhilip/activityhive/workflows/Continuous%20Deployment/badge.svg)
![Coverage](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/KNPhilip/activityhive/main/.github/badges/coverage.json)

ActivityHive is a case study made for the purpose of exploring functional & AI-Driven web development using F# and TypeScript.

The project itself is a hub for social events. It contains all the basic functionality that you would expect a website to have.
Examples include activity management, user profiles, photo uploads, authentication, real-time comments, following system, and more simple stuff like that.

## AI

Below is a list of the AI Agents that has been used to develop this project. Most of the code in the project is AI-generated.
All of them have different trade-offs, so it is difficult to directly rank them, but I will say that Claude Opus 4.6 (expensive) 
and Claude Sonnet 4.6 are the best on the market right now for software development IMO.

| Agents |
|---|
| Claude Opus 4.6 |
| Claude Sonnet 4.6 |
| GPT-5 |
| GPT-4o |
| GPT-4o mini |
| o3 mini |
| Grok CLI |
| Gemini CLI |

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 18+](https://nodejs.org/)
- [PostgreSQL 14+](https://www.postgresql.org/)
- (Optional) Cloudinary account for photo hosting
- (Optional) SendGrid API key for email
- (Optional) Facebook App credentials for OAuth

### Backend

1. **Clone the repository**

   ```bash
   git clone https://github.com/KNPhilip/activityhive.git
   cd activityhive
   ```

2. **Configure the database**

   Update the connection string in `src/API/appsettings.Development.json`:

   ```json
   "ConnectionStrings": {
     "DefaultConnection": "Server=localhost; Port=5432; User=admin; Password=secret; Database=activityhive"
   }
   ```

3. **Configure optional services** (Cloudinary, SendGrid, Facebook) in `src/API/appsettings.Development.json`.

4. **Restore and run**

   ```bash
   dotnet restore ActivityHive.sln
   dotnet run --project src/API/API.fsproj
   ```

   The API listens on `https://localhost:5000`. Swagger UI is available at `https://localhost:5000/swagger`.

### Frontend

```bash
cd client-app
npm install
npm run dev
```

The React app starts at `http://localhost:5173` and proxies API requests to `https://localhost:5000`.

## Running Tests

```bash
# Unit tests
dotnet test tests/UnitTests/UnitTests.fsproj

# Integration tests (requires Docker)
dotnet test tests/IntegrationTests/IntegrationTests.fsproj

# All tests
dotnet test ActivityHive.sln
```

## API Overview

| Endpoint | Description |
|---|---|
| `GET /api/activities` | Paginated activity list (supports `going`/`hosting` filter) |
| `GET /api/activities/{id}` | Activity detail |
| `POST /api/activities` | Create activity |
| `PUT /api/activities/{id}` | Edit activity (host only) |
| `DELETE /api/activities/{id}` | Delete activity (host only) |
| `POST /api/activities/{id}/attend` | Toggle attendance |
| `GET /api/profile/{username}` | User profile |
| `PUT /api/profile` | Edit own profile |
| `GET /api/profile/{username}/activities` | User's activities |
| `POST /api/photo` | Upload photo |
| `DELETE /api/photo/{id}` | Delete photo |
| `POST /api/photo/{id}/setMain` | Set main profile photo |
| `POST /api/follow/{username}` | Follow / unfollow user |
| `GET /api/follow/{username}` | Followers / following list |
| `POST /api/auth/register` | Register |
| `POST /api/auth/login` | Login |
| `POST /api/auth/facebook-login` | Facebook OAuth |
| `POST /api/auth/refresh-token` | Refresh JWT |
| `WS /chat?activityId={id}` | SignalR real-time comments |

## Docker

```bash
# Build
docker build -t activityhive .

# Run
docker run -p 8080:8080 \
  -e ConnectionStrings__DefaultConnection="<postgres_connection_string>" \
  -e TokenKey="<jwt_secret>" \
  -e Cloudinary__CloudName="<cloud_name>" \
  -e Cloudinary__ApiKey="<api_key>" \
  -e Cloudinary__ApiSecret="<api_secret>" \
  -e Sendgrid__Key="<sendgrid_key>" \
  -e Facebook__ApiSecret="<facebook_secret>" \
  activityhive
```

## Deployment

The project is configured to deploy to [Fly.io](https://fly.io) and can be reached here: https://activityhive.fly.dev
