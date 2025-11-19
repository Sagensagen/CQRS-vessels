module Command.BankAccountHandler

open FsToolkit.ErrorHandling
open Marten
open System
open Domain.Account
open Shared.Api

type OpenBankAccount = {
    BankAccountId: Guid
    AccountNumber: Guid
    ClientId: string
    Inserted: DateTimeOffset
}


type Deposit = {
      BankAccountId: Guid
      Amount: decimal
      Inserted: DateTimeOffset
      Version: int
}
type Withdraw = {
      BankAccountId: Guid
      Amount: decimal
      Inserted: DateTimeOffset
      Version: int
}
type CloseBankAccount = {
      BankAccountId: Guid
      Inserted: DateTimeOffset
      Version: int
}
    
type CommandType =
    | OpenBankAccount of OpenBankAccount
    // | CloseBankAccount of BankAccountClosed
    | CloseBankAccount of CloseBankAccount
    | Deposit of Deposit
    | Withdraw of Withdraw
    
let createAccount (command:OpenBankAccount) (session:IDocumentSession) =
    asyncResult {
        let e = BankAccountOpened {
            Id = command.BankAccountId
            AccountNumber = command.AccountNumber
            Amount = 0m
            State = Open
            ClientId = command.ClientId
            Inserted = command.Inserted
            Version = 1
        }
        let stream = session.Events.StartStream<BankAccountOpened>(command.BankAccountId, [| e :> obj |])
        let event : Domain.Deposit = {
                 BankAccountId = command.BankAccountId
                 Amount = 0m
                 Inserted = command.Inserted
                 Version = 1}
        let stream = session.Events.Append(command.BankAccountId, event)        
        do! session.SaveChangesAsync() |> Async.AwaitTask
        printfn $"created account: {stream.Id}"
        return stream.Id 
    }
    
let deposit (command:Deposit) (session:IDocumentSession)=
    asyncResult {
        do! command.Amount > 0m |> Result.requireTrue Shared.Api.CommandErrors.InsufficientFunds
        
        let event : Domain.Deposit = {
                 BankAccountId = command.BankAccountId
                 Amount = command.Amount
                 Inserted = command.Inserted
                 Version = 1}
        let stream = session.Events.Append(command.BankAccountId, event)
        do! session.SaveChangesAsync() |> Async.AwaitTask
        return stream.Id
    }
    
let withdraw (command:Withdraw) (session:IDocumentSession)=
    asyncResult {
        let event : Domain.Withdraw = {
                BankAccountId = command.BankAccountId
                Amount = command.Amount
                Inserted = command.Inserted
                Version = 1}
        let stream = session.Events.Append(command.BankAccountId, event)
        do! session.SaveChangesAsync() |> Async.AwaitTask
        return stream.Id
    }
    
let closeAccount (command:CloseBankAccount) (session:IDocumentSession)=
    asyncResult {
        let event = BankAccountClosed {
          BankAccountId = command.BankAccountId
          Inserted = command.Inserted
          Version = 1 }
        
        let stream = session.Events.Append(command.BankAccountId, event)
        do! session.SaveChangesAsync() |> Async.AwaitTask
        return stream.Id
    }    
    
let decide command session =
    match command with
    | OpenBankAccount c -> createAccount c session
    | Deposit c -> deposit c session
    | Withdraw c -> withdraw c session
    | CloseBankAccount c -> closeAccount c session