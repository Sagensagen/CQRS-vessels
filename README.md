# CQRS-vessels
# CQRS-vessels

A CQRS (Command Query Responsibility Segregation) application for managing vessels, built with F# and ASP.NET Core.

## Project Structure

- **Server** - ASP.NET Core backend server
- **Client** - Frontend application
- **Command** - Command handlers for write operations
- **Query** - Query handlers for read operations
- **Domain** - Domain models and business logic
- **Shared** - Shared types and contracts

## Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download)
- [Node.js](https://nodejs.org/) (for frontend dependencies)
- [Docker](https://www.docker.com/) and Docker Compose (for infrastructure)

## Getting Started

### 1. Start Infrastructure

Start required services (databases, message queues, etc.):

```bash
docker-compose up -d
```

### 2. Install Dependencies

```bash
# Install npm/bun dependencies
bun install
```

### 3. Run the Application

#### Development Mode

```bash
# Run both client and server in watch mode from `/Client`
dotnet fable watch -s -o .build -e .jsx --verbose --run bunx --bun vite -c ../vite.config.js
```

#### Run Server Only

```bash
dotnet run --project Server
```

#### Build for Production

```bash
# Build the solution
dotnet build

# Or build specific projects
dotnet build Server/Server.fsproj
```

