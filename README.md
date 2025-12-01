# CQRS-vessels

A CQRS app with event sourcing

## Project Structure


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
or from root
```bash
bun fable
```


#### Run Server

```bash
dotnet run --project Server
```


