module Quipu.ParameterizationExtractor.DSL.ParserResult

    type ParseResult() = class end

    open AST
    open Mapper
    open Quipu.ParameterizationExtractor.Logic.Configs

    [<AbstractClass>]
    type OK<'Y,'X>(result: 'X) =
        inherit ParseResult()
        member this.Result = result    
        abstract member GetResult: 'Y
        // = MapResult result
            //match box result with
            //    | :? list<Script> -> MapResult result
            //    | _ -> failwith "Parser can not find mapper function"
 
    type DslOK (result) =
        inherit OK<Package,list<Script>>(result)
        override this.GetResult = result |> MapResult

    type CommandOK (result: Command) =
        inherit OK<Command,Command>(result)
        override this.GetResult = result

    type Fail(errorMessage: string) =
        inherit ParseResult()
        member this.ErrorMessage = errorMessage

