module Client.Sidebar

open System
open Browser.Types
open Client.Context
open FS.FluentUI.V8toV9
open Fable.React
open Feliz
open FS.FluentUI
open Shared.Api.Port
open Shared.Api.Simulation
open Shared.Api.Vessel
open Fable.Core

[<ReactComponent>]
let private AddPortDialog () =
  let _ctx, setCtx = Context.useCtx ()
  let isOpen, setIsOpen = React.useState false
  let isSending, setIsSending = React.useState false
  let form, setForm = React.useState RegisterPortRequest.DefaultEmpty

  let createPort (form: RegisterPortRequest) =
    ApiClient.Port.CreatePort form
    |> Async.StartAsPromise
    |> Promise.map (fun res ->
      match res with
      | Error e ->
        Toasts.errorToast setCtx "CreatePortFailed" "Could not create port" $"{e}" None
        setIsSending false
      | Ok accs ->
        setIsSending false
        setIsOpen false
        setForm RegisterPortRequest.DefaultEmpty
        Toasts.successToast setCtx $"CreatePort{accs}Success" "Port created" $"Port {accs} created"
    )
    |> Promise.catchEnd (fun _ -> setIsSending false)

  Fui.dialog [
    dialog.open' isOpen
    dialog.onOpenChange (fun (d: DialogOpenChangeData<MouseEvent>) ->
      if not d.``open`` then
        setForm RegisterPortRequest.DefaultEmpty
      setIsOpen d.``open``
    )
    dialog.children [
      Fui.dialogTrigger [
        dialogTrigger.disableButtonEnhancement true
        dialogTrigger.children (
          Fui.button [
            button.text "Add harbor"
            button.appearance.primary
            button.icon (Fui.icon.branchRegular [])
          ]
        )
      ]
      Fui.dialogSurface [
        dialogSurface.children [
          Fui.dialogBody [
            Fui.dialogTitle [
              dialogTitle.as'.h1
              dialogTitle.text "Add port"
            ]
            Fui.dialogContent [
              dialogContent.style [
                style.display.flex
                style.flexDirection.column
                style.gap (length.rem 1)
              ]
              dialogContent.children [
                Fui.text.caption1 "Add new port to the system and let vesseles start docking"
                Fui.field [
                  field.label "Name"
                  field.children [
                    Fui.input [
                      input.value form.Name
                      input.onChange (fun v -> setForm {form with Name = v})
                      input.placeholder "Hanoi port"
                    ]
                  ]
                ]
                Fui.field [
                  field.label "Country"
                  field.children [
                    Fui.input [
                      input.value form.Country
                      input.onChange (fun v -> setForm {form with Country = v})
                      input.placeholder "Vietnam"
                    ]
                  ]
                ]
                Html.div [
                  prop.style [style.display.flex]
                  prop.children [
                    Fui.field [
                      field.style [style.flexGrow 1]
                      field.label "Latitude"
                      field.children [
                        Fui.input [
                          input.type'.number
                          input.value form.Latitude
                          input.onChange (fun (v: string) ->
                            Double.TryParse v
                            |> function
                              | true, f -> setForm {form with Latitude = f}
                              | _ -> ()
                          )
                          input.placeholder "0011.0"
                        ]
                      ]
                    ]
                    Fui.field [
                      field.style [style.flexGrow 1]
                      field.label "Longitude"
                      field.children [
                        Fui.input [
                          input.type'.number
                          input.value form.Longitude
                          input.onChange (fun (v: string) ->
                            Double.TryParse v
                            |> function
                              | true, f -> setForm {form with Longitude = f}
                              | _ -> ()
                          )
                          input.placeholder "0011.0"
                        ]
                      ]
                    ]

                  ]
                ]
                Fui.field [
                  field.label "Max docks"
                  field.children [
                    Fui.input [
                      input.type'.number
                      input.value form.MaxDocks
                      input.onChange (fun v -> setForm {form with MaxDocks = v})
                      input.placeholder "10"
                    ]
                  ]
                ]
              ]
            ]
            Fui.dialogActions [
              dialogActions.position.end'
              dialogActions.children [
                Fui.dialogTrigger [
                  dialogTrigger.disableButtonEnhancement true
                  dialogTrigger.children (
                    Fui.button [
                      button.appearance.secondary
                      button.text "Close"
                    ]
                  )
                ]
                Fui.button [
                  button.appearance.primary
                  button.disabled isSending
                  button.children [
                    if isSending then
                      Fui.spinner [spinner.size.extraSmall]
                    else
                      Fui.text "Create port"
                  ]
                  button.onClick (fun _ ->
                    setIsSending true
                    createPort form
                  )
                ]
              ]
            ]
          ]
        ]
      ]
    ]
  ]

[<ReactComponent>]
let private AddVesselDialog () =
  let _ctx, setCtx = Context.useCtx ()
  let isOpen, setIsOpen = React.useState false
  let isSending, setIsSending = React.useState false
  let form, setForm = React.useState RegisterVesselRequest.DefaultEmpty

  let createVessel (form: RegisterVesselRequest) =
    ApiClient.Vessel.CreateVessel form
    |> Async.StartAsPromise
    |> Promise.map (fun res ->
      match res with
      | Error e ->
        Toasts.errorToast setCtx "CreateVesselFailed" "Could not create vessel" $"{e}" None
        setIsSending false
      | Ok accs ->
        setIsSending false
        setIsOpen false
        setForm RegisterVesselRequest.DefaultEmpty
        Toasts.successToast setCtx $"CreateVessel{accs}Success" "Vessel created" $"Vessel {accs} created"
    )
    |> Promise.catchEnd (fun _ -> ())

  Fui.dialog [
    dialog.open' isOpen
    dialog.onOpenChange (fun (d: DialogOpenChangeData<MouseEvent>) ->
      if not d.``open`` then
        setForm RegisterVesselRequest.DefaultEmpty
      setIsOpen d.``open``
    )
    dialog.children [
      Fui.dialogTrigger [
        dialogTrigger.disableButtonEnhancement true
        dialogTrigger.children (
          Fui.button [
            button.appearance.primary
            button.icon (Fui.icon.addRegular [icon.size.``24``])
            button.text "Add vessel"
          ]
        )
      ]
      Fui.dialogSurface [
        dialogSurface.children [
          Fui.dialogBody [
            Fui.dialogTitle [
              dialogTitle.as'.h1
              dialogTitle.text "Add vessel"
            ]
            Fui.dialogContent [
              dialogContent.style [
                style.display.flex
                style.flexDirection.column
                style.gap (length.rem 1)
              ]
              dialogContent.children [
                Fui.text.caption1 "Add new vessel to the system and start tracking status and events"
                Fui.field [
                  field.label "Name"
                  field.children [
                    Fui.input [
                      input.value form.Name
                      input.onChange (fun v -> setForm {form with Name = v})
                      input.placeholder "Vessel 123"
                    ]
                  ]
                ]
                Fui.field [
                  field.label "Flag"
                  field.children [
                    Fui.input [
                      input.value form.Flag
                      input.onChange (fun v -> setForm {form with Flag = v})
                      input.placeholder "Vietnam"
                    ]
                  ]
                ]
                Fui.field [
                  field.label "Mmsi"
                  field.children [
                    Fui.input [
                      input.type'.number
                      input.value form.Mmsi
                      input.onChange (fun v -> setForm {form with Mmsi = v})
                      input.placeholder "1234"
                    ]
                  ]
                ]
                Fui.field [
                  field.label "Imo"
                  field.children [
                    Fui.input [
                      input.type'.number
                      input.value (form.Imo |> Option.defaultValue 0)
                      input.onChange (fun v -> setForm {form with Imo = Some v})
                      input.placeholder "1234"
                    ]
                  ]
                ]
                Fui.field [
                  field.label "Crew size"
                  field.children [
                    Fui.input [
                      input.type'.number
                      input.value form.CrewSize
                      input.onChange (fun v -> setForm {form with CrewSize = v})
                      input.placeholder "1"
                    ]
                  ]
                ]
                Fui.field [
                  field.label "Vesseltype"
                  field.children [
                    Fui.dropdown [
                      dropdown.children [
                        Fui.option [
                          option.value (ContainerShip.ToString ())
                          option.text (ContainerShip.ToString ())
                          option.children [Fui.text (ContainerShip.ToString ())]
                        ]
                        Fui.option [
                          option.value (BulkCarrier.ToString ())
                          option.text (BulkCarrier.ToString ())
                          option.children [Fui.text (BulkCarrier.ToString ())]
                        ]
                        Fui.option [
                          option.value (Passenger.ToString ())
                          option.text (Passenger.ToString ())
                          option.children [Fui.text (Passenger.ToString ())]
                        ]
                        Fui.option [
                          option.value (Fishing.ToString ())
                          option.text (Fishing.ToString ())
                          option.children [Fui.text (Fishing.ToString ())]
                        ]
                        Fui.option [
                          option.value "Other"
                          option.text "Other"
                          option.children [Fui.text "Other"]
                        ]
                      ]
                    ]
                  ]
                ]
                Fui.text "Length"
                Fui.text "Beam"
                Fui.text "Draught"
              ]
            ]
            Fui.dialogActions [
              dialogActions.position.end'
              dialogActions.children [
                Fui.dialogTrigger [
                  dialogTrigger.disableButtonEnhancement true
                  dialogTrigger.children (
                    Fui.button [
                      button.appearance.secondary
                      button.text "Close"
                    ]
                  )
                ]
                Fui.button [
                  button.appearance.primary
                  button.disabled isSending
                  button.children [
                    if isSending then
                      Fui.spinner [spinner.size.extraSmall]
                    else
                      Fui.text "Create vessel"
                  ]
                  button.onClick (fun _ ->
                    setIsSending true
                    createVessel form
                  )
                ]
              ]
            ]
          ]
        ]
      ]
    ]
  ]

[<ReactComponent>]
let private VesselDropdown (onSelect: VesselDTO option -> unit) =
  let ctx, _setCtx = Context.useCtx ()
  Fui.field [
    field.label "Select vessel"
    field.children [
      Fui.dropdown [
        dropdown.appearance.underline
        dropdown.value (
          ctx.SelectedVessel
          |> Option.map _.Name
          |> Option.defaultValue ""
        )
        dropdown.onOptionSelect (fun (select: OptionOnSelectData) ->
          select.optionText
          |> Option.bind (fun v ->
            ctx.AllVessels
            |> Array.tryFind (fun vessel -> vessel.Name = v)
          )
          |> onSelect

        )
        dropdown.children [
          yield!
            ctx.AllVessels
            |> Array.map (fun vessel ->
              Fui.option [
                option.disabled (
                  ctx.SelectedVessel
                  |> Option.map (fun v -> v.Id = vessel.Id)
                  |> Option.defaultValue false
                )
                option.text vessel.Name
                option.value vessel.Name
                option.children [Fui.text vessel.Name]
              ]
            )
        ]
      ]
    ]
  ]

[<ReactComponent>]
let SimulationDialog () =
  let simulationForm, setSimulationForm = React.useState 0
  let ctx, setCtx = Context.useCtx ()
  let isOpen, setIsOpen = React.useState false
  Fui.dialog [
    dialog.open' isOpen
    dialog.onOpenChange (fun (d: DialogOpenChangeData<MouseEvent>) -> setIsOpen d.``open``)
    dialog.children [
      Fui.dialogTrigger [
        dialogTrigger.disableButtonEnhancement true
        dialogTrigger.children (
          Fui.card [
            card.appearance.subtle
            card.children [
              Html.div [
                prop.style [
                  style.display.flex
                  style.alignItems.center
                  style.gap 10
                ]
                prop.children [
                  if ctx.IsSimulating then
                    Fui.icon.connectedFilled [
                      icon.size.``24``
                      icon.primaryFill Theme.tokens.colorStatusDangerBackground2
                    ]
                    Fui.text.body1Strong "Stop simulation"
                    Fui.spinner [spinner.size.tiny]
                  else
                    Fui.icon.connectedRegular [icon.size.``24``]
                    Fui.text.body1Strong "Simulate"
                ]
              ]
            ]
          ]
        )
      ]
      Fui.dialogSurface [
        dialogSurface.children [
          Fui.dialogBody [
            Fui.dialogTitle [
              dialogTitle.as'.h1
              dialogTitle.text "Simulation config"
            ]
            Fui.dialogContent [
              dialogContent.style [
                style.display.flex
                style.flexDirection.column
                style.gap 25
              ]
              dialogContent.children [
                if ctx.IsSimulating then
                  Fui.button [
                    button.text "Stop simulation"
                    button.appearance.primary
                    button.icon (Fui.icon.dismissRegular [])
                    button.onClick (fun _ ->
                      ApiClient.Simulation.StopSimulation ()
                      |> Async.StartAsPromise
                      |> Promise.tap (fun res ->
                        match res with
                        | Ok () ->
                          setIsOpen false
                          UpdateIsSimulating (not ctx.IsSimulating) |> setCtx
                        | Error e -> ()
                      )
                      |> Promise.catchEnd (fun _ -> ())
                    )
                  ]
                else
                  Fui.text
                    "This request will dispatch the given number of vessels into the system, and will randomly try to move, dock, undock. There are 50 ports created as well and can be full/available and will reject vessels trying to dock if the latter. Cap is 1000"
                  Fui.field [
                    field.validationMessage
                      "Do not deploy too many vessels. The browser will not be happy rendering 10k vessels per 5sec."
                    field.validationState.none
                    field.label "Number of vessels"
                    field.children [
                      Fui.input [
                        input.value simulationForm
                        input.type'.number
                        input.onChange (fun v ->
                          if v >= 0 && v <= 1000 then
                            setSimulationForm v
                        )
                      ]
                    ]
                  ]
              ]
            ]
            if not ctx.IsSimulating then
              Fui.dialogActions [
                dialogActions.position.end'
                dialogActions.children [
                  Fui.dialogTrigger [
                    dialogTrigger.disableButtonEnhancement true
                    dialogTrigger.children (
                      Fui.button [
                        button.appearance.secondary
                        button.text "Close"
                      ]
                    )
                  ]
                  Fui.button [
                    button.icon (Fui.icon.rocketRegular [])
                    button.disabled (0 = simulationForm)
                    button.appearance.primary
                    button.text "Start"
                    button.onClick (fun _ ->
                      ApiClient.Simulation.ExecuteSimulation simulationForm
                      |> Async.StartAsPromise
                      |> Promise.tap (fun res ->
                        match res with
                        | Ok () ->
                          UpdateIsSimulating (not ctx.IsSimulating) |> setCtx
                          setIsOpen false
                        | Error e -> ()
                      )
                      |> Promise.catchEnd (fun _ -> ())
                    )
                  ]
                ]
              ]
          ]
        ]
      ]
    ]
  ]

[<ReactComponent>]
let SideBar () =
  let ctx, setCtx = Context.useCtx ()
  Fui.fluentProvider [
    fluentProvider.theme.createDarkTheme maritimeBlueBrands
    fluentProvider.children [
      Html.div [
        prop.style [
          style.backgroundColor Theme.tokens.colorBrandStroke2Pressed
          style.height (length.vh 100)
        ]
        prop.children [
          Html.div [
            prop.style [
              style.width 280
              style.display.flex
              style.flexDirection.column
              style.gap 5
              style.padding (length.rem 1)
            ]
            prop.children [
              Fui.stack [
                stack.horizontal true
                stack.verticalAlign.center
                stack.children [
                  Fui.stackItem [
                    Fui.icon.layerRegular [icon.size.``48``]
                  ]
                  Fui.stackItem [Fui.text.title3 "Fleet manager"]
                ]
              ]
              VesselDropdown (UpdateSelectedVessel >> setCtx)
              Html.div [prop.style []; prop.children []]
              Fui.card [
                card.appearance.subtle
                card.selected (SelectedView.FleetMap = ctx.SelectedView)
                card.onClick (fun _ -> (UpdateSelectedView SelectedView.FleetMap) |> setCtx)
                card.children [
                  Html.div [
                    prop.style [
                      style.display.flex
                      style.alignItems.center
                      style.gap 10
                    ]
                    prop.children [
                      Fui.icon.mapRegular [icon.size.``24``]
                      Fui.text.body1Strong "Fleet map"
                    ]
                  ]
                ]
              ]
              Fui.card [
                card.appearance.subtle
                card.selected (SelectedView.VesselStatus = ctx.SelectedView)
                card.onClick (fun _ -> (UpdateSelectedView SelectedView.VesselStatus) |> setCtx)
                card.children [
                  Html.div [
                    prop.style [
                      style.display.flex
                      style.alignItems.center
                      style.gap 10
                    ]
                    prop.children [
                      Fui.icon.vehicleShipRegular [icon.size.``24``]
                      Fui.text.body1Strong "Vessel status"
                    ]
                  ]
                ]
              ]
              Fui.card [
                card.appearance.subtle
                card.selected (SelectedView.VesselEventHistory = ctx.SelectedView)
                card.onClick (fun _ ->
                  (UpdateSelectedView SelectedView.VesselEventHistory)
                  |> setCtx
                )
                card.children [
                  Html.div [
                    prop.style [
                      style.display.flex
                      style.alignItems.center
                      style.gap 10
                    ]
                    prop.children [
                      Fui.icon.timerRegular [icon.size.``24``]
                      Fui.text.body1Strong "Event history"
                    ]
                  ]
                ]
              ]
              Fui.card [
                card.selected (SelectedView.VesselPlans = ctx.SelectedView)
                card.onClick (fun _ -> (UpdateSelectedView SelectedView.VesselPlans) |> setCtx)
                card.appearance.subtle
                card.children [
                  Html.div [
                    prop.style [
                      style.display.flex
                      style.alignItems.center
                      style.gap 10
                    ]
                    prop.children [
                      Fui.icon.calendarRegular [icon.size.``24``]
                      Fui.text.body1Strong "Plans"
                    ]
                  ]
                ]
              ]
              SimulationDialog ()

              AddVesselDialog ()
              AddPortDialog ()
            ]
          ]

        ]
      ]
    ]
  ]
