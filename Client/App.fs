module App

open Browser.Dom
open Client
open Client.Context
open Feliz
open Feliz.Router
open Fable.Core
open FS.FluentUI
open Shared.Api.Port
open Shared.Api.Simulation
open Shared.Api.Vessel

let getVessels (callback: VesselDTO array -> unit) setCtx =
  promise {
    ApiClient.Vessel.GetAllVessels ()
    |> Async.StartAsPromise
    |> Promise.map (fun res ->
      match res with
      | Error e -> Toasts.errorToast setCtx "GetVesselsError" "Could not fetch vessels" $"{e}" None
      | Ok vessels -> callback vessels
    )
    |> Promise.catchEnd (fun _ -> ())
  }
  |> Promise.start

let private getPorts (callback: PortDTO array -> unit) setCtx =
  ApiClient.Port.GetAllPorts ()
  |> Async.StartAsPromise
  |> Promise.map (fun res ->
    match res with
    | Error e -> Toasts.errorToast setCtx "GetPortsError" "Could not fetch ports" $"{e}" None
    | Ok vessels -> callback vessels
  )
  |> Promise.catchEnd (fun _ -> ())

let private getPortStatistics (callback: PortStatistics -> unit) setCtx =
  ApiClient.Simulation.GetPortStatistics ()
  |> Async.StartAsPromise
  |> Promise.map (fun res ->
    match res with
    | Error e -> Toasts.errorToast setCtx "getPortStatisticsError" "Could not fetch port stats :(" $"{e}" None
    | Ok vessels -> callback vessels
  )
  |> Promise.catchEnd (fun _ -> ())

let private getVesselStatistics (callback: VesselStatistics -> unit) setCtx =
  ApiClient.Simulation.GetVesselStatistics ()
  |> Async.StartAsPromise
  |> Promise.map (fun res ->
    match res with
    | Error e -> Toasts.errorToast setCtx "getVesselStatisticsError" "Could not fetch vessel stats :(" $"{e}" None
    | Ok vessels -> callback vessels
  )
  |> Promise.catchEnd (fun _ -> ())

let private getCargoStatistics (callback: CargoStatistics -> unit) setCtx =
  ApiClient.Simulation.GetCargoStatistics ()
  |> Async.StartAsPromise
  |> Promise.map (fun res ->
    match res with
    | Error e -> Toasts.errorToast setCtx "getCargoStatisticsError" "Could not fetch vessel stats :(" $"{e}" None
    | Ok vessels -> callback vessels
  )
  |> Promise.catchEnd (fun _ -> ())

[<ReactComponent>]
let private Application () =
  let ctx, setCtx = Context.useCtx ()

  // Fetch all ports and vessels every 5s
  // Add WS?
  React.useEffectOnce (fun _ ->
    getVessels (UpdateAllVessels >> setCtx) setCtx
    getPorts (UpdateAllPorts >> setCtx) setCtx
    getPortStatistics (fun stats -> UpdatePortStatistics (Some stats) |> setCtx) setCtx
    getVesselStatistics (fun stats -> UpdateVesselStatistics (Some stats) |> setCtx) setCtx
    getCargoStatistics (fun stats -> UpdateCargoStatistics (Some stats) |> setCtx) setCtx

  )
  React.useEffect (
    (fun _ ->
      let pollTimer =
        Browser.Dom.window.setInterval (
          (fun _ ->
            getVessels (UpdateAllVessels >> setCtx) setCtx
            getPorts (UpdateAllPorts >> setCtx) setCtx
            getPortStatistics (fun stats -> UpdatePortStatistics (Some stats) |> setCtx) setCtx
            getVesselStatistics (fun stats -> UpdateVesselStatistics (Some stats) |> setCtx) setCtx
            getCargoStatistics (fun stats -> UpdateCargoStatistics (Some stats) |> setCtx) setCtx
          ),
          1000,
          []
        )
      {new System.IDisposable with
        member __.Dispose () =
          Browser.Dom.window.clearInterval pollTimer
      }
    ),
    [||]
  )
  React.router [
    router.hashMode
    router.children [
      Html.div [
        prop.style [
          style.width (length.vw 100)
          style.height (length.vh 100)
          style.display.flex
          style.flexDirection.row
        ]
        prop.children [
          Sidebar.SideBar ()
          Html.main [
            prop.style [
              style.flexGrow 1
              style.maxHeight (length.vh 100)

              style.backgroundColor Theme.tokens.colorNeutralBackground2
            ]
            prop.children [
              match ctx.SelectedView with
              | FleetMap -> FleetMap.FleetMap ()
              | VesselStatus -> VesselStatus.VesselStatus ()
              | VesselEventHistory -> EventHistory.VesselEvents ()
              | _ -> Fui.text "Other"
            ]
          ]
        ]
      ]
    ]
  ]

[<ReactComponent>]
let private Root () =
  Context.ContextProvider [Application ()]

let private root = ReactDOM.createRoot (document.getElementById "root")
root.render (React.strictMode [Root ()])
