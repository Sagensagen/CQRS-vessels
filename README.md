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
#### Populate ocean shapefiles into postgis
Download shapeFile gemoetries https://osmdata.openstreetmap.de/data/water-polygons.html

```bash
shp2pgsql -I -D -s 4326 water_polygons.shp water | psql -h localhost -U postgres -p 5433 -d postgres
```

### Getting started with PostGIS
Install the postgis/enable it at least
```sql
CREATE EXTENSION postgis;
```
Populate the polygon dataset of global waters
```bash
shp2pgsql -I -D -s 4326 water_polygons.shp water | psql -h localhost -U postgres -p 5433 -d postgres
```
Create a grid from the polygons. Shortes paths need a grid network of edges, and polygons does not really make sense. Create single points from these
```sql
CREATE TABLE grid AS
SELECT (ST_HexagonGrid(0.1, ST_Extent(geom))).*
FROM water;
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
