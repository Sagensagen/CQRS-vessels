module Client.VesselStatus

open System
open Browser.Types
open Feliz.PigeonMaps
open Feliz
open FS.FluentUI
open Shared.Api.Shared
open Shared.Api.Vessel
open Fable.Core

let private updateOperationalStatus
  (vesselId: Guid)
  (status: VesselStatusCommand)
  callback
  (setIsFetching: bool -> unit)
  setCtx
  =
  ApiClient.Vessel.UpdateOperationalStatus vesselId status
  |> Async.StartAsPromise
  |> Promise.tap (fun res ->
    try
      match res with
      | Ok _ ->
        Toasts.successToast setCtx "updateVesselStatusSuccess" "Vessel updated" $"Vessel updated successfully"
        callback ()
      | Error e -> Toasts.errorToast setCtx "updateVesselStatusError" "Could not update" $"{e}" None
    finally
      setIsFetching false
  )
  |> Promise.catchEnd (fun e ->
    setIsFetching false
    Toasts.errorToast setCtx "updateVesselStatusError" "Could not update" $"{e}" None
  )

let private updatePosition (vesselId: Guid) (position: LatLong) callback setCtx =
  ApiClient.Vessel.UpdatePosition vesselId position
  |> Async.StartAsPromise
  |> Promise.tap (fun res ->
    match res with
    | Ok _ ->
      Toasts.successToast setCtx $"updatePositionsuccess{vesselId}" "Position updated" ""
      callback ()
    | Error e -> Toasts.errorToast setCtx "updateVesselPositionError" "Could not update position" $"{e}" None
  )
  |> Promise.catchEnd (fun e ->
    Toasts.errorToast setCtx "updateVesselPositionError" "Could not update position" $"{e}" None
  )

[<ReactComponent>]
let private VesselPositionDialog (vessel: VesselDTO) =
  let position, setPosition = React.useState<LatLong option> (Some vessel.Position)
  let ctx, setCtx = Context.useCtx ()
  let isOpen, setIsOpen = React.useState false
  let isSending, setIsSending = React.useState false
  Fui.dialog [
    dialog.open' isOpen
    dialog.onOpenChange (fun (d: DialogOpenChangeData<MouseEvent>) -> setIsOpen d.``open``)
    dialog.children [
      Fui.dialogTrigger [
        dialogTrigger.disableButtonEnhancement true
        dialogTrigger.children (
          Html.div [
            Fui.button [
              button.icon (Fui.icon.locationRippleRegular [])
              button.text "Update position"
              button.appearance.primary
              button.iconPosition.after
            ]
          ]
        )
      ]
      Fui.dialogSurface [
        dialogSurface.style [
          style.height (length.vh 95)
          style.minWidth (length.vw 95)
        ]
        dialogSurface.children [
          Fui.dialogBody [
            dialogBody.style [
              style.height (length.perc 100)
              style.width (length.perc 100)
            ]
            dialogBody.children [
              Fui.dialogTitle [
                dialogTitle.as'.h1
                dialogTitle.text "Vessel position"
                dialogTitle.action (
                  Fui.button [
                    button.appearance.transparent
                    button.icon (Fui.icon.dismissRegular [])
                    button.onClick (fun _ -> setIsOpen false)
                  ]
                )
              ]
              Fui.dialogContent [
                dialogContent.style [
                  style.display.flex
                  style.flexDirection.column
                  style.height (length.perc 100)
                  style.padding 0
                  style.gap (length.rem 1)
                ]
                dialogContent.children [
                  PigeonMaps.map [
                    match position with
                    | None -> ()
                    | Some pos -> map.center (pos.Latitude, pos.Longitude)
                    map.onClick (fun pos ->
                      {Latitude = fst pos.latLng; Longitude = snd pos.latLng}
                      |> Some
                      |> setPosition
                    )
                    map.zoom 5
                    map.markers [
                      match position with
                      | None -> ()
                      | Some latLong ->
                        PigeonMaps.marker [
                          marker.anchor (latLong.Latitude, latLong.Longitude)
                          marker.render (fun marker ->
                            Fui.icon.vehicleShipFilled [
                              icon.size.``28``
                              icon.primaryFill Theme.tokens.colorBrandBackground
                            ]
                          )
                        ]
                    ]
                  ]
                  Html.div [
                    prop.style [
                      style.position.absolute
                      style.bottom (length.rem 4)
                      style.right (length.rem 4)
                      style.zIndex 1000
                    ]
                    prop.children [
                      match position with
                      | None -> Html.none
                      | Some pos ->
                        Fui.button [
                          button.children [
                            if isSending then
                              Fui.spinner [spinner.size.extraSmall]
                            else
                              Fui.text "Send"
                          ]
                          button.icon (Fui.icon.sendRegular [])
                          button.appearance.primary
                          button.onClick (fun _ ->
                            setIsSending true
                            updatePosition
                              vessel.Id
                              pos
                              (fun id ->
                                setIsSending false
                                setIsOpen false
                              )
                              setCtx
                          )
                        ]
                    ]
                  ]
                ]
              ]
            ]
          ]
        ]
      ]
    ]
  ]

[<ReactComponent>]
let private VesselStatusDialog (vessel: VesselDTO) =
  let _ctx, setCtx = Context.useCtx ()
  let isOpen, setIsOpen = React.useState false
  let isSending, setIsSending = React.useState false
  let status, setStatus = React.useState<VesselStatusCommand option> None
  Fui.dialog [
    dialog.open' isOpen
    dialog.onOpenChange (fun (d: DialogOpenChangeData<MouseEvent>) -> setIsOpen d.``open``)
    dialog.children [
      Fui.dialogTrigger [
        dialogTrigger.disableButtonEnhancement true
        dialogTrigger.children (
          Html.div [
            Fui.button [
              button.icon (Fui.icon.boxEditRegular [])
              button.text "Update status"
              button.appearance.primary
              button.iconPosition.after
            ]
          ]
        )
      ]
      Fui.dialogSurface [
        dialogSurface.children [
          Fui.dialogBody [
            Fui.dialogTitle [
              dialogTitle.as'.h1
              dialogTitle.text "Vessel Action"
            ]
            Fui.dialogContent [
              dialogContent.style [
                style.display.flex
                style.flexDirection.column
                style.gap (length.rem 1)
              ]
              dialogContent.children [
                Fui.text.caption1 "Execute a command for this vessel"
                Fui.card [
                  card.orientation.horizontal
                  card.children [
                    Fui.cardPreview [
                      cardPreview.children [
                        Fui.icon.backpackRegular [
                          icon.primaryFill Theme.tokens.colorBrandBackground
                          icon.size.``48``
                        ]
                      ]
                    ]
                    Html.div [
                      prop.style [
                        style.display.flex
                        style.flexDirection.column
                        style.gap 2
                      ]
                      prop.children [
                        Fui.text.subtitle1 "Operational status"
                        match vessel.State with
                        | AtSea -> Fui.text "At Sea"
                        | Anchored _ -> Fui.text "Anchored"
                        | Decommissioned -> Fui.text "Decomissioned"
                        | Docked port -> Fui.text $"Docket at {port}"
                        | UnderMaintenance -> Fui.text "Under maintenance"
                        | InRoute _route -> Fui.text "In route"
                      ]
                    ]
                  ]
                ]
                Html.div [
                  prop.style [
                    style.gap 5
                    style.display.flex
                    style.flexWrap.wrap
                  ]
                  prop.children [
                    Fui.card [
                      let isSelected =
                        (match status with
                         | Some (Depart _) -> true
                         | _ -> false)
                      card.orientation.horizontal
                      card.style [
                        if isSelected then
                          style.backgroundColor Theme.tokens.colorBrandBackgroundInvertedSelected
                        style.minWidth 250
                        style.maxWidth (length.perc 100)
                        style.display.flex
                      ]
                      card.disabled (
                        match vessel.State with
                        | AtSea -> true
                        | _ -> false
                      )
                      card.selected isSelected
                      card.children [
                        Fui.text "Depart from port"
                        Fui.icon.sendRegular [icon.size.``24``]
                      ]
                      card.onClick (fun _ ->
                        match vessel.State with
                        | Docked port -> setStatus (Some (Depart port))
                        | _ -> ()
                      )
                    ]
                    Fui.card [
                      let isSelected =
                        (match status with
                         | Some (Arrive _) -> true
                         | _ -> false)
                      card.orientation.horizontal
                      card.disabled (
                        match vessel.State with
                        | Docked _ -> true
                        | _ -> false
                      )
                      card.style [
                        if isSelected then
                          style.backgroundColor Theme.tokens.colorBrandBackgroundInvertedSelected
                        style.minWidth 250
                        style.maxWidth (length.perc 100)
                        style.display.flex
                      ]
                      card.selected isSelected
                      card.children [
                        Fui.text "Arrive at port"
                        Fui.icon.locationArrowLeftRegular [icon.size.``24``]
                      ]
                      card.onClick (fun _ -> setStatus (Some (Arrive (Guid.Empty))))
                    ]
                    Fui.card [
                      let isSelected =
                        (match status with
                         | Some (StartRoute _) -> true
                         | _ -> false)
                      card.orientation.horizontal
                      card.disabled (
                        match vessel.State with
                        | InRoute _ -> true
                        | _ -> false
                      )
                      card.style [
                        if isSelected then
                          style.backgroundColor Theme.tokens.colorBrandBackgroundInvertedSelected
                        style.minWidth 250
                        style.maxWidth (length.perc 100)
                        style.display.flex
                      ]
                      card.selected isSelected
                      card.children [
                        Fui.text "Start route to port"
                        Fui.icon.locationArrowLeftRegular [icon.size.``24``]
                      ]
                      card.onClick (fun _ ->
                        let inRoute = {
                          RouteId = Guid.NewGuid ()
                          DestinationPortId = Guid.Empty
                          DestinationCoordinates = {Latitude = 0.; Longitude = 0.}
                          StartCoordinates = {
                            Latitude = vessel.Position.Latitude
                            Longitude = vessel.Position.Longitude
                          }
                          Waypoints = [||]
                          CurrentWaypointIndex = 0
                          StartedAt = DateTimeOffset.UtcNow
                        }
                        setStatus (Some (StartRoute inRoute))
                      )
                    ]
                    Fui.card [
                      let isSelected =
                        (match status with
                         | Some Advance -> true
                         | _ -> false)
                      card.orientation.horizontal
                      card.disabled (
                        match vessel.State with
                        | InRoute _ -> false // Only allow advancing when InRoute
                        | _ -> true
                      )
                      card.style [
                        if isSelected then
                          style.backgroundColor Theme.tokens.colorBrandBackgroundInvertedSelected
                        style.minWidth 250
                        style.maxWidth (length.perc 100)
                        style.display.flex
                      ]
                      card.selected isSelected
                      card.children [
                        Fui.text "Advance to next waypoint"
                        Fui.icon.arrowMoveRegular [icon.size.``24``]
                      ]
                      card.onClick (fun _ -> setStatus (Some Advance))
                    ]
                    Fui.card [
                      let isSelected =
                        (match status with
                         | Some (Anchor _) -> true
                         | _ -> false)
                      card.orientation.horizontal
                      card.disabled (
                        true
                      // match vessel.State with
                      // | Anchored _ -> true
                      // | _ -> false
                      )
                      card.style [
                        if isSelected then
                          style.backgroundColor Theme.tokens.colorBrandBackgroundInvertedSelected
                        style.minWidth 250
                        style.maxWidth (length.perc 100)
                        style.display.flex
                      ]
                      card.selected isSelected
                      card.children [
                        Fui.text "Anchored"
                        Fui.icon.waterRegular [icon.size.``24``]
                      ]

                      card.onClick (fun _ -> setStatus (Some (Anchor "Somewhere")))
                    ]
                    Fui.card [
                      let isSelected =
                        (match status with
                         | Some StartMaintenance -> true
                         | _ -> false)
                      card.orientation.horizontal
                      card.disabled (
                        true
                      // match vessel.State with

                      // | UnderMaintenance -> true
                      // | _ -> false
                      )
                      card.style [
                        if isSelected then
                          style.backgroundColor Theme.tokens.colorBrandBackgroundInvertedSelected
                        style.minWidth 250
                        style.maxWidth (length.perc 100)
                        style.display.flex
                      ]
                      card.selected isSelected
                      card.children [
                        Fui.text "Under maintenance"
                        Fui.icon.wrenchSettingsRegular [icon.size.``24``]
                      ]
                      card.onClick (fun _ -> setStatus (Some StartMaintenance))
                    ]
                  // Fui.card [
                  //   card.orientation.horizontal
                  //   card.style [style.minWidth 250; style.maxWidth (length.perc 100); style.display.flex]
                  //   card.selected false
                  //   card.children [
                  //     Fui.text "Start loading"
                  //     Fui.icon.boxArrowUpRegular [icon.size.``24``]
                  //   ]
                  // ]
                  // Fui.card [
                  //   card.orientation.horizontal
                  //   card.style [style.minWidth 250; style.maxWidth (length.perc 100); style.display.flex]
                  //   card.selected false
                  //   card.children [
                  //     Fui.text "Start unloading"
                  //     Fui.icon.cubeArrowCurveDownRegular [icon.size.``24``]
                  //   ]
                  // ]
                  ]
                ]
                match status with
                | Some (Arrive port) ->
                  Fui.field [
                    field.label "Port"
                    field.children [
                      Fui.dropdown [
                        dropdown.onOptionSelect (fun vv ->
                          vv.optionValue
                          |> Option.iter (fun v ->
                            let parsed = Guid.Parse v
                            setStatus (Some (Arrive parsed))
                          )
                        )
                        dropdown.children [
                          yield!
                            _ctx.AllPorts
                            |> Array.map (fun port ->
                              Fui.option [
                                option.text port.Name
                                option.value (port.Id.ToString ())
                                option.children [Fui.text port.Name]
                              ]
                            )
                        ]
                      ]
                      Fui.input [
                        input.value port
                        input.placeholder "What port are you docking at?"
                        input.onChange (fun (v: string) -> setStatus (Some (Arrive (Guid.NewGuid ()))))
                      ]
                    ]
                  ]
                | Some (StartRoute route) ->
                  Fui.field [
                    field.label "Port"
                    field.children [
                      Fui.dropdown [
                        dropdown.onOptionSelect (fun vv ->
                          vv.optionValue
                          |> Option.iter (fun v ->
                            let parsed = Guid.Parse v
                            let port = _ctx.AllPorts |> Array.find (fun s -> s.Id = parsed)
                            setStatus (
                              Some (
                                StartRoute {
                                  route with
                                      DestinationPortId = port.Id
                                      DestinationCoordinates = port.Position
                                }
                              )
                            )
                          )
                        )
                        dropdown.children [
                          yield!
                            _ctx.AllPorts
                            |> Array.map (fun port ->
                              Fui.option [
                                option.text port.Name
                                option.value (port.Id.ToString ())
                                option.children [Fui.text port.Name]
                              ]
                            )
                        ]
                      ]
                    ]
                  ]
                | Some (Anchor _) ->
                  Fui.field [
                    field.label "Position"
                    field.children [
                      Fui.input [
                        input.placeholder "Where anchored?"
                        input.onChange (fun (v: string) -> setStatus (Some (Anchor v)))
                      ]
                    ]
                  ]
                | _ -> Html.none
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
                  button.disabled (
                    isSending
                    || match status with
                       | None -> true
                       | Some (Anchor "") -> true
                       | Some (Arrive s) when s = Guid.Empty -> true
                       | _ -> false

                  )
                  button.children [
                    if isSending then
                      Fui.spinner [spinner.size.extraSmall]
                    else
                      Fui.text "Update"
                  ]
                  button.onClick (fun _ ->
                    match status with
                    | Some someStatus ->
                      setIsSending true
                      updateOperationalStatus
                        vessel.Id
                        someStatus
                        (fun s ->
                          setIsSending false
                          setIsOpen false
                        )
                        setIsSending
                        setCtx
                    | None -> ()
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
let VesselStatus () =
  let ctx, setCtx = Context.useCtx ()

  Html.div [
    prop.style [
      style.display.flex
      style.flexDirection.column
      style.gap (length.rem 1)
      style.padding (length.rem 1)
      style.height (length.vh 100)
      style.overflowY.scroll
    ]
    prop.children [
      Fui.text.title2 "Vessel status"
      match ctx.SelectedVessel with
      | None ->
        Html.div [
          prop.children [
            Fui.text.subtitle2 "No vessel selected"
          ]
        ]
      | Some vessel ->
        Fui.text.body1Strong vessel.Name
        Html.div [
          prop.style [
            style.display.flex
            style.gap (length.rem 1)
          ]
          prop.children [
            VesselStatusDialog vessel
            VesselPositionDialog vessel
          ]
        ]
        Html.div [
          prop.style [
            style.display.flex
            style.flexGrow 1
            style.flexWrap.wrap
            style.gap (length.rem 2)
          ]
          prop.children [
            Fui.card [
              card.appearance.filled
              card.style [
                style.justifyContent.spaceBetween
                style.display.flex
                style.flexDirection.row
                style.height 150
                style.width 250
                style.padding (length.rem 1)
              ]
              card.children [
                Html.div [
                  prop.style [
                    style.display.flex
                    style.flexDirection.column
                    style.gap 5
                  ]
                  prop.children [
                    Fui.text "Status"
                    Fui.text.subtitle1 (
                      match vessel.State with
                      | AtSea -> "At Sea"
                      | Docked port -> $"Docket at {port}"
                      | Anchored pos -> $"Anchored at {pos}"
                      | UnderMaintenance -> "UnderMaintenance"
                      | Decommissioned -> "Decommissioned"
                      | InRoute route -> "In route"
                    )
                  ]
                ]
                Html.div [
                  prop.children [
                    Fui.icon.tableCheckerRegular [
                      icon.size.``32``
                      icon.primaryFill Theme.tokens.colorBrandForeground2
                    ]
                  ]
                ]
              ]
            ]
            Fui.card [
              card.appearance.filled
              card.style [
                style.justifyContent.spaceBetween
                style.display.flex
                style.flexDirection.row
                style.height 150
                style.width 250
                style.padding (length.rem 1)
              ]
              card.children [
                Html.div [
                  prop.style [
                    style.display.flex
                    style.flexDirection.column
                    style.gap 5
                  ]
                  prop.children [
                    Fui.text "Current LatLong"
                    Fui.text.subtitle1 $"{vessel.Position.Latitude}, {vessel.Position.Longitude}"
                  ]
                ]
                Html.div [
                  prop.children [
                    Fui.icon.locationRegular [
                      icon.size.``32``
                      icon.primaryFill Theme.tokens.colorBrandForeground2
                    ]
                  ]
                ]
              ]
            ]
            Fui.card [
              card.appearance.filled
              card.style [
                style.justifyContent.spaceBetween
                style.display.flex
                style.flexDirection.row
                style.height 150
                style.width 250
                style.padding (length.rem 1)
              ]
              card.children [
                Html.div [
                  prop.style [
                    style.display.flex
                    style.flexDirection.column
                    style.gap 5
                  ]
                  prop.children [
                    Fui.text "Type"
                    Fui.text.subtitle1 (vessel.VesselType.ToString ())
                  ]
                ]
                Html.div [
                  prop.children [
                    Fui.icon.locationRegular [
                      icon.size.``32``
                      icon.primaryFill Theme.tokens.colorBrandForeground2
                    ]
                  ]
                ]
              ]
            ]
            Fui.card [
              card.appearance.filled
              card.style [
                style.justifyContent.spaceBetween
                style.display.flex
                style.flexDirection.row
                style.height 150
                style.width 250
                style.padding (length.rem 1)
              ]
              card.children [
                Html.div [
                  prop.style [
                    style.display.flex
                    style.flexDirection.column
                    style.gap 5
                  ]
                  prop.children [
                    Fui.text "Crew size"
                    Fui.text.subtitle1 $"{vessel.CrewSize}"
                  ]
                ]
                Html.div [
                  prop.children [
                    Fui.icon.peopleAudienceRegular [
                      icon.size.``32``
                      icon.primaryFill Theme.tokens.colorBrandForeground2
                    ]
                  ]
                ]
              ]
            ]
          ]
        ]
        Fui.card [
          card.style [
            style.height 300
            style.minHeight 300
            style.width (length.perc 100)
          ]
          card.children [
            Fui.cardHeader [
              cardHeader.header (Fui.text.subtitle1 "Vessel information")
            ]
            Html.div [
              prop.style [
                style.display.flex
                style.flexWrap.wrap
                style.gap (length.rem 1)
              ]
              prop.children [
                Fui.field [
                  field.label "Name"
                  field.children [
                    Fui.text.body1Strong [
                      text.style [style.minWidth 400]
                      text.text vessel.Name
                    ]
                  ]
                ]
                Fui.field [
                  field.label "Vessel id"
                  field.children [
                    Fui.text.body1Strong [
                      text.style [style.minWidth 400]
                      text.text $"{vessel.Id}"
                    ]
                  ]
                ]
                Fui.field [
                  field.label "Registered"
                  field.children [
                    Fui.text.body1Strong [
                      text.style [style.minWidth 400]
                      text.text (vessel.Inserted.ToString ("dd MMM yyyy"))
                    ]
                  ]
                ]
              ]
            ]
          ]
        ]
        EventHistory.VesselEvents (id)
    ]
  ]
