module RemotingHelpers

open System
open Fable.Remoting.Server
open Microsoft.AspNetCore.Http

let routeBuilder (typeName: string) (methodName: string) = $"/api/%s{typeName}/%s{methodName}"

type CustomError = { errorMsg: string }

let errorHandler (ex: Exception) (routeInfo: RouteInfo<HttpContext>) =
    printfn "Error at %s on method %s" routeInfo.path routeInfo.methodName

    match ex with
    | :? System.IO.IOException as x ->
        let customError = { errorMsg = "Generic error happened" }
        Propagate customError
    | _ -> Ignore
