module Program

open Expecto


[<EntryPoint>]
let main args =
    let tests =
        testList "All Domain Tests" [ VesselAggregateTests.tests; PortAggregateTests.tests ]

    runTestsWithCLIArgs [] args tests
