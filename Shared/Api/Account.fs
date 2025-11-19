module Shared.Api.BankAccount

open System

type CommandErrors =
    | AccountNotFound
    | InsufficientFunds
    | InternalError

type QueryErrors =
    | AccountNotFound
    | InternalError
    | InsufficientFunds
    
type BankAccountState =
    | Open
    | Closed
    
[<CLIMutable>]
type Account ={
    Id: Guid
    AccountNumber: Guid
    ClientId: string
    Inserted: DateTimeOffset
    Amount: decimal
    State: string //BankAccountState
    Version: int
}

type IAccountApi = {
    GetBalance: Guid -> Async<Result<decimal, QueryErrors>>
    GetAccounts: unit -> Async<Result<Account array, QueryErrors>>
    CreateAccount: unit -> Async<Result<Guid, CommandErrors>>
    Deposit: Guid * decimal -> Async<Result<Guid, CommandErrors>>
    Withdraw: Guid * decimal -> Async<Result<Guid, CommandErrors>>
}
