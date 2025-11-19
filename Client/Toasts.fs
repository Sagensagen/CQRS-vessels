module Client.Toasts

open FS.FluentUI

/// <summary>
/// Success toast that works globally
/// </summary>
/// <param name="setCtx">context needs to be propagated from the parent caller.</param>
/// <param name="toastId">Id needs to be unique. If new toast with same id as an already rendered id, it will not show/overwrite it. </param>
/// <param name="title">Title of the message</param>
/// <param name="description">Small description of the event</param>
let successToast setCtx (toastId: string) (title: string) (description: string) =
  setCtx (
    Context.UpdateToast (
      (Fui.toast [
        toast.children [
          Fui.toastTitle [
            toastTitle.action (
              Fui.button [
                button.text "Ok"
                button.onClick (fun _ -> setCtx (Context.DismissToast toastId))
              ]
            )
            toastTitle.text title
          ]
          Fui.toastBody [toastBody.text description]
        ]
      ]),
      [
        dispatchToastOptions.timeout 5000
        dispatchToastOptions.politeness.polite
        dispatchToastOptions.intent.success
        dispatchToastOptions.toastId toastId
      ]
    )
  )

/// <summary>
/// Info toast that works globally.
/// </summary>
/// <param name="setCtx"></param>
/// <param name="toastId">Id needs to be unique. If new toast with same id as an already rendered id, it will not show/overwrite it. </param>
/// <param name="title">Title of the message</param>
/// <param name="description">Small description of the event</param>
/// <param name="subDescription">Optional extra information</param>
let infoToast setCtx (toastId: string) (title: string) (description: string) (subDescription: string option) =
  setCtx (
    Context.UpdateToast (
      (Fui.toast [
        toast.children [
          Fui.toastTitle [
            toastTitle.action (
              Fui.button [
                button.text "Ok"
                button.onClick (fun _ -> setCtx (Context.DismissToast toastId))
              ]
            )
            toastTitle.text title
          ]
          Fui.toastBody [
            toastBody.text description
            match subDescription with
            | None -> ()
            | Some sb -> toastBody.subtitle sb
          ]
        ]
      ]),
      [
        dispatchToastOptions.timeout 5000
        dispatchToastOptions.politeness.polite
        dispatchToastOptions.intent.info
        dispatchToastOptions.toastId toastId
      ]
    )
  )

/// <summary>
/// Warning toast that works globally.
/// </summary>
/// <param name="setCtx"></param>
/// <param name="toastId">Id needs to be unique. If new toast with same id as an already rendered id, it will not show/overwrite it. </param>
/// <param name="title">Title of the message</param>
/// <param name="description">Small description of the event</param>
/// <param name="subDescription">Optional extra information</param>
let warningToast setCtx (toastId: string) (title: string) (description: string) (subDescription: string option) =
  setCtx (
    Context.UpdateToast (
      (Fui.toast [
        toast.children [
          Fui.toastTitle [toastTitle.text title]
          Fui.toastBody [
            toastBody.text description
            match subDescription with
            | None -> ()
            | Some sb -> toastBody.subtitle sb
          ]
        ]
      ]),
      [
        dispatchToastOptions.timeout 10000
        dispatchToastOptions.politeness.polite
        dispatchToastOptions.intent.warning
        dispatchToastOptions.toastId toastId
      ]
    )
  )

/// <summary>
/// Error toast that works globally.
/// </summary>
/// <param name="setCtx"></param>
/// <param name="toastId">Id needs to be unique. If new toast with same id as an already rendered id, it will not show/overwrite it. </param>
/// <param name="title">Title of the message</param>
/// <param name="description">Small description of the event</param>
/// <param name="subDescription">Optional extra information</param>
let errorToast setCtx (toastId: string) (title: string) (description: string) (subDescription: string option) =
  setCtx (
    Context.UpdateToast (
      (Fui.toast [
        toast.children [
          Fui.toastTitle [
            toastTitle.action (
              Fui.button [
                button.text "Ok"
                button.onClick (fun _ -> setCtx (Context.DismissToast toastId))
              ]
            )
            toastTitle.text title
          ]
          Fui.toastBody [
            toastBody.text description
            match subDescription with
            | None -> ()
            | Some sb -> toastBody.subtitle sb
          ]
        ]
      ]),
      [
        dispatchToastOptions.timeout 5000
        dispatchToastOptions.politeness.assertive
        dispatchToastOptions.intent.error
        dispatchToastOptions.toastId toastId
      ]
    )
  )
