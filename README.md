# CQRS-vessels

A vessel and port management system demonstrating CQRS with Event Sourcing for learning purposes. The application tracks vessel positions, manages port operations, and coordinates docking procedures and cargo loading/unloading using the Saga pattern for distributed transactions.

## Project Structure
- Client -  Frontend stuff
- Server - Api layer, infrastructure, simulation etc.
- Domain - Aggregate handling
- Command - Actors and Commands
- Query - For reading aggregates or other projections
- Shared - Api data types

## Tech
- MartenDB for ES wrapper with PostgreSQL
- PostGIS + PGRouting for shortest path between two coordinates in ocean graph
- AKKA.net actors
- Giraffe
- Fable+ReactJS

## Getting Started
Nix+direnv is handy and will give you the necessary environment shell.


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

Alter the columns for readability / QX(query experience). This can be skipped, but routinug query needs to be updated with the original column names.
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

## Domain Rules

### Vessels
- Can carry max one cargo at a time
- When loaded with cargo, can ONLY route to that cargo's destination port
- Must be at cargo's origin port to load, destination port to unload
- Capacity limits enforced by volume and weight
- Cannot dock without reservation (via DockingSaga) and must complete route first
- Can only undock when docked

### Cargo
- Lifecycle: `AwaitingPickup` → `ReservedForVessel` → `LoadedOnVessel` → `InTransit` → `Delivered`
- Must be reserved before loading (via CargoLoadingSaga)

### Ports
- Have fixed docking capacity (`MaxDocks`)
- Docking requires reservation; reservations can expire
- Can be `Open` or `Closed` (closed ports reject new reservations)

### Sagas
- **DockingSaga**: Coordinates vessel-port docking with two-phase commit
- **CargoLoadingSaga**: Manages cargo reservation and loading onto vessels
- **CargoUnloadingSaga**: Handles cargo delivery and vessel cargo clearing

