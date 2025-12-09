module Command.Route.AStar

open FsToolkit.ErrorHandling
open Npgsql
open Serilog

let private srid = 4326
let connectionString =
    "Host=localhost;Port=5433;Database=postgres;Username=postgres;Password=postgres"
let private tableName = "ship_routes"

let private dataSource =
    let builder = NpgsqlDataSourceBuilder(connectionString)
    builder.UseNetTopologySuite() |> ignore
    builder.Build()

let aStar
    (startLatitude: float)
    (startLongitude: float)
    (endLatitude: float)
    (endLongitude: float)
    =
    asyncResult {
        let sqlCommand =
            $"""
            WITH nodes AS (
                SELECT source AS id, ST_StartPoint(geom) AS geom
                FROM ship_routes
                UNION
                SELECT target AS id, ST_EndPoint(geom) AS geom
                FROM ship_routes
            ),

                 start_node AS (
                     SELECT id
                     FROM nodes
                     ORDER BY geom <-> ST_SetSRID(ST_Point(@startLon, @startLat), {srid})
                     LIMIT 1
                 ),

                 end_node AS (
                     SELECT id
                     FROM nodes
                     ORDER BY geom <-> ST_SetSRID(ST_Point(@endLon, @endLat), {srid})
                     LIMIT 1
                 ),

                 astar AS (
                     SELECT *
                     FROM pgr_astar(
                             '
                             SELECT
                                 gid AS id,
                                 source,
                                 target,
                                 cost,
                                 ST_X(ST_StartPoint(geom)) AS x1,
                                 ST_Y(ST_StartPoint(geom)) AS y1,
                                 ST_X(ST_EndPoint(geom)) AS x2,
                                 ST_Y(ST_EndPoint(geom)) AS y2
                             FROM ship_routes
                             ',
                             (SELECT id FROM start_node),
                             (SELECT id FROM end_node)
                          )
                 )

            SELECT
                ST_Y(dp.geom) AS lat,
                ST_X(dp.geom) AS lon
            FROM astar a
                     JOIN ship_routes sr ON sr.gid = a.edge
                     CROSS JOIN LATERAL ST_DumpPoints(sr.geom) AS dp
            ORDER BY a.seq, dp.path;
            """

        use conn = dataSource.OpenConnection()
        use cmd = new NpgsqlCommand(sqlCommand, conn)

        cmd.Parameters.AddWithValue("@startLat", startLatitude) |> ignore
        cmd.Parameters.AddWithValue("@startLon", startLongitude) |> ignore
        cmd.Parameters.AddWithValue("@endLat", endLatitude) |> ignore
        cmd.Parameters.AddWithValue("@endLon", endLongitude) |> ignore

        use reader = cmd.ExecuteReader()

        let results = [|
            while reader.Read() do
                let lat = reader.GetDouble(0)
                let lon = reader.GetDouble(1)
                yield (lat, lon)
        |]
        return results
    }
