module Client.FleetMap

open Client.Context
open FS.FluentUI
open Feliz
open Feliz.PigeonMaps

// Shows statistics of all the ports and vessels in the mixer
[<ReactComponent>]
let private StatisticsPanel () =
  let ctx, _setCtx = Context.useCtx ()
  Html.div [
    prop.style [
      style.position.absolute
      style.top 15
      style.right 15
      style.zIndex 9999
      style.backgroundColor "rgba(0,0,0,0.6)"
      style.color.white
      style.padding 10
      style.display.flex
      style.flexDirection.column
    ]
    prop.children [
      match ctx.PortStatistics, ctx.VesselStatistics with
      | Some portStats, Some vesselStats ->
        Fui.text.subtitle2 "Port stats"
        Fui.text $"Total ports: {portStats.Total}"
        Fui.text $"Available docks: {portStats.AvailableDocks}"
        Fui.text $"Closed ports: {portStats.Closed}"
        Fui.text $"Open ports: {portStats.Open}"
        Fui.text $"Occupancy rate: %.2f{portStats.OccupancyRate}%%"
        Fui.text $"Occupied docks: {portStats.OccupiedDocks}"
        Fui.text $"Total docks: {portStats.TotalDocks}"
        Html.div [Fui.divider []]
        Fui.text.subtitle2 "Vessel stats"
        Fui.text $"Total vessels: {vesselStats.Total}"
        Fui.text $"Active vessels: {vesselStats.Active}"
        Fui.text $"Docked vessels: {vesselStats.Docked}"
        Fui.text $"Vessels at sea: {vesselStats.AtSea}"
        Fui.text $"Decommissioned vessels: {vesselStats.Decommissioned}"
      | _ -> Fui.spinner []

    ]
  ]

[<ReactComponent>]
let FleetMap () =
  let ctx, setCtx = Context.useCtx ()
  Fui.card [
    card.style [
      style.display.flex
      style.padding 0
      style.height (length.perc 100)
    ]
    card.children [
      StatisticsPanel ()
      PigeonMaps.map [
        map.center (14.699243741693328, 114.26806853317845)
        map.zoom 5
        map.markers [
          yield!
            ctx.CurrentRoute
            |> Array.map (fun latLong ->
              PigeonMaps.marker [
                marker.anchor (latLong.Latitude, latLong.Longitude)
                marker.offsetLeft 8
                marker.offsetTop 8
                marker.render (fun marker -> [
                  Fui.icon.circleFilled [
                    icon.style [style.fontSize 8]
                    icon.primaryFill Theme.tokens.colorStatusSuccessBackground3
                  ]
                ])
              ]
            )
          yield!
            ctx.AllVessels
            |> Array.map (fun vessel ->
              PigeonMaps.marker [
                marker.onClick (fun _ -> setCtx (UpdateSelectedVessel (Some vessel)))
                marker.anchor (vessel.Position.Latitude, vessel.Position.Longitude)
                marker.offsetLeft 25
                marker.offsetTop 25
                marker.render (fun _marker -> [
                  Fui.tooltip [
                    tooltip.content vessel.Name
                    tooltip.children [
                      Fui.button [
                        button.size.large
                        button.appearance.transparent
                        button.children [
                          Fui.icon.vehicleShipFilled [
                            icon.style [style.fontSize 25]
                            match ctx.SelectedVessel with
                            | Some v when v.Id = vessel.Id ->
                              icon.primaryFill Theme.tokens.colorPaletteBerryBackground3
                            | _ -> icon.primaryFill Theme.tokens.colorStatusDangerBorderActive
                          ]
                        ]
                      ]
                    ]
                  ]
                ])
              ]
            )
          yield!
            ctx.AllPorts
            |> Array.map (fun port ->
              PigeonMaps.marker [
                marker.anchor (port.Latitude, port.Longitude)
                marker.offsetLeft 102
                marker.offsetTop 35
                marker.render (fun marker -> [
                  Fui.tooltip [
                    tooltip.content (
                      Html.div [
                        prop.style [
                          style.display.flex
                          style.backgroundColor.white
                          style.flexDirection.column
                        ]
                        prop.children [
                          Fui.text port.Name
                          Fui.text.caption1 $"{port.CurrentDocked}/{port.MaxDocks}"
                        ]
                      ]
                    )
                    tooltip.children [
                      Fui.button [
                        button.size.large
                        button.appearance.transparent
                        button.style [
                          style.display.flex
                          style.flexDirection.column
                          style.alignItems.center
                          style.maxWidth 205
                          style.maxHeight 100
                          style.padding 0
                        ]
                        button.children [
                          Fui.text [
                            text.align.center
                            text.text $"{port.Name} {port.CurrentDocked}/{port.MaxDocks}"
                            text.style [style.backgroundColor.white]
                          ]
                          Fui.icon.locationFilled [
                            icon.style [style.fontSize 34]
                            if port.CurrentDocked >= port.MaxDocks then
                              icon.primaryFill Theme.tokens.colorStatusWarningBackground3
                            else
                              icon.primaryFill Theme.tokens.colorBrandBackground
                          ]
                        ]
                      ]
                    ]
                  ]
                ])
              ]
            )
        ]
      ]
    ]
  ]
