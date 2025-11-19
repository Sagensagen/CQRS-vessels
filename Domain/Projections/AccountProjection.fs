module Domain.Projections.AccountView

open System
open Domain.Account.Event
open Shared.Api.Account

type AccountViewProjection() =
    inherit Marten.Events.Aggregation.SingleStreamProjection<AccountView, Guid>()
    member this.Create(event: BankAccountOpened) : AccountView =
        { Id = event.Id; AccountNumber = event.AccountNumber; ClientId = event.ClientId;Inserted = event.Inserted; Amount = 0m; State = Open; Version = 1}

    member this.Apply(event: DepositEvent, current: AccountView) :AccountView =
        { current with Amount = current.Amount + event.Amount }              
        
    member this.Apply(event: WithdrawEvent, current: AccountView) :AccountView =
        { current with Amount = current.Amount - event.Amount }
        
    member this.Apply(event: BankAccountClosed, current: AccountView) :AccountView =
        { current with State = Closed}
        
        