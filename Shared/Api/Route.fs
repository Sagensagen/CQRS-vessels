module Shared.Api.Route

type LatLong = { Latitude: float; Longitude: float }

type RouteError = | CoordinateInvalid

// API interface
type IRouteApi = { CalculateRoute: LatLong -> LatLong -> Async<Result<LatLong array, RouteError>> }
