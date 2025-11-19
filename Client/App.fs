module App

open Browser.Dom
open Client
open Client.Context
open Feliz
open Feliz.Router
open Fable.Core
open FS.FluentUI
open Shared.Api.Port
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

let getPorts (callback: PortDTO array -> unit) setCtx =
  ApiClient.Port.GetAllPorts ()
  |> Async.StartAsPromise
  |> Promise.map (fun res ->
    match res with
    | Error e -> Toasts.errorToast setCtx "GetPortsError" "Could not fetch ports" $"{e}" None
    | Ok vessels -> callback vessels
  )
  |> Promise.catchEnd (fun _ -> ())

[<ReactComponent>]
let private Application () =
  let ctx, setCtx = Context.useCtx ()

  React.useEffectOnce (fun _ ->
    getVessels (UpdateAllVessels >> setCtx) setCtx
    getPorts (UpdateAllPorts >> setCtx) setCtx
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
              style.padding (length.rem 1)
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
