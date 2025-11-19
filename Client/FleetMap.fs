module Client.FleetMap

open FS.FluentUI
open Fable.DateFunctions.ExternalDateFns
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
