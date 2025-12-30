module Client.CargoPicker

open Feliz
open Shared.Api.Cargo
open Shared.Api.Vessel
open FS.FluentUI
open Browser.Types
open Fable.Core

let private fetchCargo portId callback setCtx setIsFetching =
  ApiClient.Cargo.GetCargoByPort portId
  |> Async.StartAsPromise
  |> Promise.tap (fun res ->
    try
      match res with
      | Ok cargo -> callback cargo
      | Error e -> Toasts.errorToast setCtx "GetCargoByPortError" "Could not fetch cargo by port" $"{e}" None
    finally
      setIsFetching false
  )
  |> Promise.catchEnd (fun e ->
    setIsFetching false
    Toasts.errorToast setCtx "GetCargoByPortError" "Could not fetch cargo by port" $"{e}" None
  )

let private loadCargo loadCargoReq setCtx setIsLoading setIsOpen =
  ApiClient.Cargo.LoadCargoOntoVessel loadCargoReq
  |> Async.StartAsPromise
  |> Promise.tap (fun res ->
    try
      match res with
      | Ok _cargo -> setIsOpen false
      | Error e -> Toasts.errorToast setCtx "loadCargoError" "Could not fetch cargo by port" $"{e}" None
    finally
      setIsLoading false
  )
  |> Promise.catchEnd (fun e ->
    setIsLoading false
    Toasts.errorToast setCtx "loadCargoError" "Could not fetch cargo by port" $"{e}" None
  )

[<ReactComponent>]
let CargoDialog (vessel: VesselDTO) =
  let _ctx, setCtx = Context.useCtx ()
  let isOpen, setIsOpen = React.useState false
  let isFetching, setIsFetching = React.useState true
  let isSending, setIsSending = React.useState false
  let cargo, setCargo = React.useState [||]
  let selectedCargo, setSelectedCargo = React.useState<CargoDTO option> None
  React.useEffectOnce (fun _ ->
    (match vessel.State with
     | Docked portId -> fetchCargo portId setCargo setCtx setIsFetching
     | _ -> Toasts.warningToast setCtx "VesselNotDockedFOr cargopickup" "Vessel is not docked at a port" "" None)
  )
  Fui.dialog [
    dialog.open' isOpen
    dialog.onOpenChange (fun (d: DialogOpenChangeData<MouseEvent>) -> setIsOpen d.``open``)
    dialog.children [
      Fui.dialogTrigger [
        dialogTrigger.disableButtonEnhancement true
        dialogTrigger.children (
          Html.div [
            Fui.button [
              button.icon (Fui.icon.boxRegular [])
              button.text "Load cargo"
              button.disabled vessel.CurrentCargo.IsSome
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
                dialogTitle.text "Pick up cargo"
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
                  style.flexDirection.row
                  style.justifyContent.spaceBetween
                  style.height (length.perc 100)
                  style.padding 0
                  style.gap (length.rem 1)
                ]
                dialogContent.children [
                  if isFetching then
                    Fui.spinner []
                  else
                    Html.div [
                      prop.style [style.width (length.perc 40)]
                      prop.children [
                        yield!
                          cargo
                          |> Array.map (fun c ->
                            Fui.card [
                              card.selected (selectedCargo |> Option.exists (fun s -> s.Id = c.Id))
                              card.onClick (fun _ -> setSelectedCargo (Some c))
                              card.orientation.vertical
                              card.children [
                                Fui.cardHeader [
                                  cardHeader.image (
                                    Fui.icon.boxRegular [
                                      icon.size.``28``
                                      icon.primaryFill Theme.tokens.colorBrandBackground
                                    ]
                                  )
                                  cardHeader.header $"{c.Id}"
                                  cardHeader.description (Fui.text.caption1 [text.text $"{c.Status}"])
                                ]
                                Fui.text $"Destination port: {c.DestinationPortName}"
                              ]
                            ]
                          )
                      ]
                    ]
                    Html.div [
                      prop.style [style.width (length.perc 50)]
                      prop.children [
                        match selectedCargo with
                        | None -> Fui.text "Select cargo"
                        | Some c -> Fui.text (c.Id.ToString ())
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
                    button.disabled (isSending || selectedCargo.IsNone)
                    button.children [
                      if isSending then
                        Fui.spinner [spinner.size.extraSmall]
                      else
                        Fui.text "Load cargo"
                    ]
                    button.onClick (fun _ ->
                      match selectedCargo with
                      | None -> ()
                      | Some c ->
                        setIsSending true
                        let loadCargoReq = {CargoId = c.Id; VesselId = vessel.Id; PortId = c.OriginPortId}
                        loadCargo loadCargoReq setCtx setIsSending setIsOpen
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
