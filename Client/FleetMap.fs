module Client.FleetMap

open Client.Context
open FS.FluentUI
open Feliz
open Feliz.PigeonMaps

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
      PigeonMaps.map [
        map.center (14.699243741693328, 114.26806853317845)
        map.zoom 5
        map.markers [
          yield!
            ctx.AllVessels
            |> Array.map (fun vessel ->
              PigeonMaps.marker [
                marker.onClick (fun _ -> setCtx (UpdateSelectedVessel (Some vessel)))
                marker.anchor (vessel.Position.Latitude, vessel.Position.Longitude)
                marker.render (fun marker -> [
                  Fui.tooltip [
                    tooltip.content vessel.Name
                    tooltip.children [
                      Fui.button [
                        button.size.large
                        button.appearance.transparent
                        button.children [
                          Fui.icon.vehicleShipFilled [
                            icon.style [style.fontSize 34]
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
                marker.offsetLeft 15
                marker.offsetTop 30
                marker.render (fun marker -> [
                  Fui.tooltip [
                    tooltip.content port.Name
                    tooltip.children [
                      Fui.button [
                        button.size.large
                        button.appearance.transparent
                        button.children [
                          Fui.icon.locationFilled [
                            icon.style [style.fontSize 34]
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
