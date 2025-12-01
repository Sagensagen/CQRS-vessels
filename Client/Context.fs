module Client.Context

open System
open FS.FluentUI
open Feliz
open Shared.Api.Port
open Shared.Api.Simulation
open Shared.Api.Vessel

let maritimeBlueBrands = {
  ``10`` = "#020304"
  ``20`` = "#11181F"
  ``30`` = "#172736"
  ``40`` = "#1B3449"
  ``50`` = "#20405B"
  ``60`` = "#314D66"
  ``70`` = "#415972"
  ``80`` = "#50667D"
  ``90`` = "#607489"
  ``100`` = "#708195"
  ``110`` = "#808FA1"
  ``120`` = "#909DAD"
  ``130`` = "#A0ACB9"
  ``140`` = "#B1BAC5"
  ``150`` = "#C2C9D2"
  ``160`` = "#D3D8DE"
}

let themeBrands = {
  ``10`` = "#020404"
  ``20`` = "#101C1A"
  ``30`` = "#142E2B"
  ``40`` = "#173C37"
  ``50`` = "#184A44"
  ``60`` = "#195851"
  ``70`` = "#19675F"
  ``80`` = "#17766D"
  ``90`` = "#13867B"
  ``100`` = "#149589"
  ``110`` = "#43A297"
  ``120`` = "#62AFA5"
  ``130`` = "#7DBCB3"
  ``140`` = "#97C9C1"
  ``150`` = "#B0D6D0"
  ``160`` = "#C9E3DF"
}

type ThemeType =
  | Light
  | Dark

type SelectedView =
  | FleetMap
  | VesselStatus
  | VesselEventHistory
  | VesselPlans

type ContextModel = {
  AllVessels: VesselDTO array
  SelectedVessel: VesselDTO option
  AllPorts: PortDTO array
  PortStatistics: PortStatistics option
  VesselStatistics: VesselStatistics option
  SelectedView: SelectedView
  ThemeType: ThemeType
  ToastToUpdate: (ReactElement * IDispatchToastOptionsProp list) option
  ToastToDismiss: string
  IsSimulating: bool
}

type ContextMsg =
  | UpdateAllVessels of VesselDTO array
  | UpdateSelectedVessel of VesselDTO option
  | UpdateAllPorts of PortDTO array
  | UpdateSelectedView of SelectedView
  | UpdatePortStatistics of PortStatistics option
  | UpdateVesselStatistics of VesselStatistics option
  | UpdateToast of (ReactElement * IDispatchToastOptionsProp list)
  | DismissToast of string
  | UpdateThemeType of ThemeType
  | UpdateIsSimulating of bool

let UpdateContext (model: ContextModel) (msg: ContextMsg) =
  match msg with
  | UpdateAllVessels vessels -> {model with AllVessels = vessels}
  | UpdateSelectedVessel vessel -> {model with SelectedVessel = vessel}
  | UpdateAllPorts ports -> {model with AllPorts = ports}
  | UpdatePortStatistics stats -> {model with PortStatistics = stats}
  | UpdateVesselStatistics stats -> {model with VesselStatistics = stats}
  | UpdateSelectedView vessel -> {model with SelectedView = vessel}
  | UpdateToast toast -> {model with ToastToUpdate = Some toast}
  | DismissToast toastName -> {model with ToastToDismiss = toastName}
  | UpdateThemeType theme -> {model with ThemeType = theme}
  | UpdateIsSimulating b -> {model with IsSimulating = b}

let context = React.createContext ()

[<ReactComponent>]
let ContextProvider (children: ReactElement list) =
  let toasterId = Fui.useId (Some "toaster", None)
  let toastController = Fui.useToastController (Some toasterId)

  let initContext () : ContextModel = {
    AllVessels = [||]
    SelectedVessel = None
    AllPorts = [||]
    PortStatistics = None
    VesselStatistics = None
    SelectedView = FleetMap
    ThemeType = Light
    ToastToUpdate = None
    ToastToDismiss = ""
    IsSimulating = false
  }
  let ctx, ctxDispatch = React.useReducer (UpdateContext, initContext ())

  React.useEffect (
    (fun _ ->
      match ctx.ToastToUpdate with
      | None -> ()
      | Some t -> toastController.dispatchToast t
    ),
    [|box ctx.ToastToUpdate|]
  )

  React.useEffect ((fun _ -> toastController.dismissToast ctx.ToastToDismiss), [|box ctx.ToastToDismiss|])

  React.contextProvider (
    context,
    (ctx, ctxDispatch),
    React.fragment [
      Fui.fluentProvider [
        match ctx.ThemeType with
        | Light -> fluentProvider.theme.createLightTheme maritimeBlueBrands
        | Dark -> fluentProvider.theme.createDarkTheme maritimeBlueBrands
        fluentProvider.children [
          yield! children
          Fui.toaster [toaster.toasterId toasterId]
        ]
      ]
    ]
  )

[<Hook>]
let useCtx () = React.useContext context
