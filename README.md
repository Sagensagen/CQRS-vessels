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
#### Populate ocean GeoJSON into postgis
Download GeoJSON gemoetries http://geocommons.com/datasets?id=25


### Getting started with PostGIS
Install the postgis/enable it at least
```sql
CREATE EXTENSION postgis;
CREATE EXTENSION pgrouting;
```


Populate the dataset. Need Gdal for this
```bash
ogr2ogr -f "PostgreSQL" \                                                           on 
        PG:"host=localhost user=postgres dbname=postgres password=postgres port=5433" \
        ./25.geojson \
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

#### Other
For plotting route: latLong [] https://tbensky.github.io/Maps/points.html
Fast link for Google maps https://www.google.com/maps

## Domain rules
1. Vessels can not dock in a port if they are not already docked. This should be a handshake/transaction through a saga in order for the dock to be reserved correctly.
2. Vessels can move positions freely, but in order to dock a vessel has to get a route to a port, and can only dock when they have arrived(no more steps to advance in the route)
3. Vessels can only undock when they are docked in a port duh
4. Some cargo stuff later
5.
