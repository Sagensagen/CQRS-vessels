module Client.EventHistory

open System
open Fable.DateFunctions
open Feliz
open FS.FluentUI
open Shared.Api.Vessel
open Fable.Core

let private getVesselEvents (vesselId: Guid) (callback: Shared.Api.Shared.EventWrapper array -> unit) setCtx =
  ApiClient.Vessel.GetEvents vesselId
  |> Async.StartAsPromise
  |> Promise.tap (fun res ->
    match res with
    | Ok events -> callback events
    | Error e -> Toasts.errorToast setCtx "getEventsError" "Could not fetch vessel events" "" None
  )
  |> Promise.catchEnd (fun e -> Toasts.errorToast setCtx "updateVesselStatusError" "Could not update" $"{e}" None)

let private eventLayout (event: Shared.Api.Shared.EventWrapper) =
  Html.div [
    prop.style [
      style.display.flex
      style.justifyContent.spaceBetween
      style.flexGrow 1
      style.width (length.perc 100)
    ]
    prop.children [
      Html.div [
        prop.style [
          style.display.flex
          style.gap (length.rem 1)
        ]
        prop.children [
          Html.div [
            prop.style [
              style.display.flex
              style.flexDirection.column
              style.gap 5
            ]
            prop.children [
              Fui.badge [
                badge.style [style.width 40; style.height 40]
                if event.EventType = Shared.Api.Shared.Success then
                  badge.color.success
                  badge.icon (Fui.icon.checkmarkCircleRegular [icon.size.``24``])
                elif event.EventType = Shared.Api.Shared.Info then
                  badge.color.brand
                  badge.icon (Fui.icon.infoRegular [icon.size.``24``])
                else
                  badge.color.danger
                  badge.icon (Fui.icon.warningRegular [icon.size.``24``])
                badge.appearance.tint
                badge.shape.circular
              ]
              Fui.divider [divider.vertical true]
            ]
          ]
          Html.div [
            prop.style [
              style.display.flex
              style.flexDirection.column
              style.gap 5
            ]
            prop.children [
              Fui.text.body1Strong event.Title
              Fui.text event.Description
            ]
          ]
        ]
      ]
      let formatDiff (ts: TimeSpan) =
        if ts.TotalMinutes < 1.0 then
          "just now"
        elif ts.TotalHours < 1.0 then
          let m = int ts.TotalMinutes
          if m = 1 then "1 minute ago" else $"{m} minutes ago"
        elif ts.TotalDays < 1.0 then
          let h = int ts.TotalHours
          if h = 1 then "1 hour ago" else $"{h} hours ago"
        else
          let d = int ts.TotalDays
          if d = 1 then "1 day ago" else $"{d} days ago"
      Fui.text.caption1 $"{formatDiff (DateTimeOffset.UtcNow - event.Inserted)}"
    ]
  ]

[<ReactComponent>]
let VesselEvents id =
  let ctx, setCtx = Context.useCtx ()
  let events, setEvents = React.useState<Shared.Api.Shared.EventWrapper array> [||]
  React.useEffect (
    (fun _ ->
      match ctx.SelectedVessel with
      | None -> ()
      | Some vessel -> getVesselEvents vessel.Id (fun events -> setEvents events) setCtx
    ),
    [|box id|]
  )
  Html.div [
    prop.style [
      style.display.flex
      style.flexDirection.column
      style.gap (length.rem 1)
    ]
    prop.children [
      match ctx.SelectedVessel with
      | None ->
        Html.div [
          prop.children [
            Fui.text.subtitle2 "No vessel selected"
          ]
        ]
      | Some vessel ->
        Fui.card [
          card.style [
            style.display.flex
            style.gap 10
            style.flexDirection.column
          ]
          card.children [
            Fui.text.subtitle1 "Event History"
            yield!
              events
              |> Array.sortByDescending _.Inserted
              |> Array.map (fun s -> eventLayout s)
          ]
        ]
    ]
  ]
