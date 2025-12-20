# CQRS-vessels

A vessel and port management system demonstrating CQRS with Event Sourcing for learning purposes. The application tracks vessel positions, manages port operations, and coordinates docking procedures using the Saga pattern for distributed transactions.

## Project Structure
- Client -  Frontend stuff
- Server - Api layer, infrastructure, simulation etc.
- Domain - Aggregate handling
- Command - Actors and Commands
- Query - For reading aggregates or other projections
- Shared - Api data types

## Tech
- MartenDB for ES wrapper with PostgreSQL
- PostGIS + PGRouting for shortest path between two coordinates
- AKKA.net actors
- Giraffe
- Fable+ReactJS

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
Init nix shell with the necessary packages. Check them out in `default.nix`
```bash
direnv allow
direnv reload
```

### 3. Run the Application

#### Database
```bash
docker-compose up -d
```
#### Run client
```bash
bun fable
```
Or
```bash
# Run `/Client`
dotnet fable watch -s -o .build -e .jsx --verbose --run bunx --bun vite -c ../vite.config.js
```
#### Run Server

```bash
dotnet run --project Server
```
#### Populate ocean GeoJSON into postgis
Download GeoJSON gemoetries http://geocommons.com/datasets?id=25

### Getting started with PostGIS
Install the postgis/enable it
```sql
CREATE EXTENSION postgis;
CREATE EXTENSION pgrouting;
```

Populate the dataset. Need Gdal for this
```bash
ogr2ogr -f "PostgreSQL" \
        PG:"host=localhost user=postgres dbname=postgres password=postgres port=5433" \
        ./Command/Route/25.geojson \
        -nln ship_routes \
        -nlt LINESTRING \
        -lco GEOMETRY_NAME=geom \
        -lco FID=gid

```

Alter the columns for readability / QX(query experience)
```sql
ALTER TABLE ship_routes RENAME COLUMN "length0" TO "cost";
ALTER TABLE ship_routes RENAME COLUMN "from node0" TO source;
ALTER TABLE ship_routes RENAME COLUMN "to node0" TO target;
```

```sql
  ALTER TABLE ship_routes
  ALTER COLUMN source TYPE integer USING source::integer,
  ALTER COLUMN target TYPE integer USING target::integer;
```

#### Other
For plotting route: latLong(actually LonLat) https://tbensky.github.io/Maps/points.html

## Domain rules
1. Vessels can not dock in a port if they are not already docked. This should be a handshake/transaction through a saga in order for the dock to be reserved correctly.
2. Vessels can move positions freely, but in order to dock a vessel has to get a route to a port, and can only dock when they have arrived(no more steps to advance in the route)
3. Vessels can only undock when they are docked in a port duh

