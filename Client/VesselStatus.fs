module Client.VesselStatus

open System
open Browser.Types
open Fable.Core.DynamicExtensions
open Feliz
open FS.FluentUI
open Shared.Api.Vessel
open Fable.Core

// let private updateActivity (activity: VesselActivity) callback setCtx =
//   ApiClient.Vessel.UpdateActivity activity
//   |> Async.StartAsPromise
//   |> Promise.tap (fun res ->
//     match res with
//     | Ok _ ->  ()
//     | Error e -> Toasts.errorToast setCtx "updateVesselActivityError" "Could not update" "" None
//   )

let private updateOperationalStatus (vesselId: Guid) (status: OperationalStatus) callback setCtx =
  ApiClient.Vessel.UpdateOperationalStatus vesselId status
  |> Async.StartAsPromise
  |> Promise.tap (fun res ->
    match res with
    | Ok _ -> callback ()
    | Error e -> Toasts.errorToast setCtx "updateVesselStatusError" "Could not update" $"{e}" None
  )
  |> Promise.catchEnd (fun e -> Toasts.errorToast setCtx "updateVesselStatusError" "Could not update" $"{e}" None)

[<ReactComponent>]
let private VesselActionDialog (vessel: VesselDTO) =
  let _ctx, setCtx = Context.useCtx ()
  let isOpen, setIsOpen = React.useState false
  let isSending, setIsSending = React.useState false
  let status, setStatus = React.useState<OperationalStatus option> None
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
                         | (Some OperationalStatus.AtSea) -> true
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
                      card.onClick (fun _ -> setStatus (Some AtSea))
                    ]
                    Fui.card [
                      let isSelected =
                        (match status with
                         | Some (OperationalStatus.Docked _) -> true
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
                      card.onClick (fun _ -> setStatus (Some (Docked "")))
                    ]
                    Fui.card [
                      let isSelected =
                        (match status with
                         | Some (OperationalStatus.Anchored _) -> true
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

                      card.onClick (fun _ -> setStatus (Some (Anchored "")))
                    ]
                    Fui.card [
                      let isSelected =
                        (match status with
                         | Some OperationalStatus.UnderMaintenance -> true
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
                      card.onClick (fun _ -> setStatus (Some UnderMaintenance))
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
                | Some (Docked port) ->
                  Fui.field [
                    field.label "Port"
                    field.children [
                      Fui.input [
                        input.value port
                        input.placeholder "What port are you docking at?"
                        input.onChange (fun (v: string) -> setStatus (Some (Docked v)))
                      ]
                    ]
                  ]
                | Some (Anchored _) ->
                  Fui.field [
                    field.label "Position"
                    field.children [
                      Fui.input [
                        input.placeholder "Where anchored?"
                        input.onChange (fun (v: string) -> setStatus (Some (Anchored v)))
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
                       | Some (Docked "") -> true
                       | Some (Anchored "") -> true
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
        VesselActionDialog vessel
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
