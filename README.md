# ActivityHive

ActivityHive is a full-stack social activity planning platform that lets users create, discover, and join activities while connecting with other participants in real time.

## Features

- **Activity Management** – Create, edit, delete, and browse activities with pagination and filtering
- **Attendance** – Join or leave activities, view attendees and the host
- **User Profiles** – Customisable profiles with bio, display name, and a photo gallery
- **Photo Uploads** – Upload and manage profile photos stored on Cloudinary
- **Social Following** – Follow and unfollow other users
- **Real-time Comments** – Live activity comments powered by SignalR WebSockets
- **Authentication** – Email/password registration and login, Facebook OAuth, JWT access tokens with HttpOnly refresh tokens

## Tech Stack

### Backend

| Layer | Technology |
|---|---|
| Language | F# on .NET 10 |
| Framework | ASP.NET Core 10 |
| Architecture | Clean Architecture + CQRS via MediatR |
| Database | PostgreSQL (production), SQLite (tests) |
| ORM | Entity Framework Core 10 |
| Real-time | SignalR |
| Auth | JWT + Refresh tokens, Facebook OAuth |
| Image hosting | Cloudinary |
| Email | SendGrid |
| Validation | FluentValidation |

### Frontend

| Layer | Technology |
|---|---|
| Language | TypeScript |
| Framework | React 18 |
| Build tool | Vite |
| State management | MobX |
| HTTP client | Axios |
| Real-time | @microsoft/signalr |
| UI | Semantic UI React |
| Forms | Formik + Yup |
| Routing | React Router v6 |

## Project Structure

```
activityhive/
├── src/
│   ├── API/            # Controllers, SignalR hub, auth service, middleware
│   ├── Application/    # CQRS command/query handlers, validators, DTOs
│   ├── Domain/         # Domain models (Activity, User, Photo, Comment, …)
│   ├── Persistence/    # EF Core DbContext and database seeding
│   └── Infrastructure/ # Cross-cutting services
├── client-app/         # React/TypeScript frontend (Vite)
├── tests/
│   ├── UnitTests/      # Domain and application unit tests
│   └── IntegrationTests/ # Handler integration tests (Docker/SQLite)
├── Dockerfile
└── fly.toml
```

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

The project is configured to deploy to [Fly.io](https://fly.io) using `fly.toml`. After installing the Fly CLI and authenticating, run:

```bash
fly deploy
```

## License

This project is open source. See [LICENSE](LICENSE) for details.
