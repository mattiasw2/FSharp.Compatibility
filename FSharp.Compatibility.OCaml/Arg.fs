﻿(*

Copyright 2005-2009 Microsoft Corporation
Copyright 2012 Jack Pappas

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.

*)

/// Parsing of command line arguments.
[<CompilerMessage(
    "This module is for ML compatibility. \
    This message can be disabled using '--nowarn:62' or '#nowarn \"62\"'.",
    62, IsHidden = true)>]
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module FSharp.Compatibility.OCaml.Arg

type ArgType = 
  | ClearArg of bool ref
  | FloatArg of (float -> unit)
  | IntArg of (int -> unit)
  | RestArg of (string -> unit)
  | SetArg of bool ref
  | StringArg of (string -> unit)
  | UnitArg of (unit -> unit)
  static member Clear  r = ClearArg r
  static member Float  r = FloatArg r
  static member Int    r = IntArg r
  static member Rest   r = RestArg r
  static member Set    r = SetArg r
  static member String r = StringArg r
  static member Unit   r = UnitArg r


type ArgInfo (name,action,help) = 
  member x.Name = name
  member x.ArgType = action
  member x.HelpText = help
  
exception Bad of string
exception Help of string

[<Sealed>]
type ArgParser() = 
    static let getUsage specs u =  
      let sbuf = new System.Text.StringBuilder 100  
      let pstring (s:string) = sbuf.Append s |> ignore 
      let pendline s = pstring s; pstring "\n" 
      pendline u;
      List.iter (fun (arg:ArgInfo) -> 
        match arg.Name, arg.ArgType, arg.HelpText with
        | (s, (UnitArg _ | SetArg _ | ClearArg _), helpText) -> pstring "\t"; pstring s; pstring ": "; pendline helpText
        | (s, StringArg _, helpText) -> pstring "\t"; pstring s; pstring " <string>: "; pendline helpText
        | (s, IntArg _, helpText) -> pstring "\t"; pstring s; pstring " <int>: "; pendline helpText
        | (s, FloatArg _, helpText) ->  pstring "\t"; pstring s; pstring " <float>: "; pendline helpText
        | (s, RestArg _, helpText) -> pstring "\t"; pstring s; pstring " ...: "; pendline helpText)
        specs;
      pstring "\t"; pstring "--help"; pstring ": "; pendline "display this list of options";
      pstring "\t"; pstring "-help"; pstring ": "; pendline "display this list of options";
      sbuf.ToString()


    static member ParsePartial(cursor,argv,argSpecs:seq<ArgInfo>,?other,?usageText) =
        let other = defaultArg other (fun _ -> ())
        let usageText = defaultArg usageText ""
        let nargs = Array.length argv 
        incr cursor;
        let argSpecs = argSpecs |> Seq.toList
        let specs = argSpecs |> List.map (fun (arg:ArgInfo) -> arg.Name, arg.ArgType)
        while !cursor < nargs do
          let arg = argv.[!cursor] 
          let rec findMatchingArg args = 
            match args with
            | ((s, action) :: _) when s = arg -> 
               let getSecondArg () = 
                   if !cursor + 1 >= nargs then 
                     raise(Bad("option "+s+" needs an argument.\n"+getUsage argSpecs usageText));
                   argv.[!cursor+1] 
                 
               match action with 
               | UnitArg f -> 
                 f (); 
                 incr cursor
               | SetArg f ->
                 f := true; 
                 incr cursor
               | ClearArg f -> 
                 f := false; 
                 incr cursor
               | StringArg f-> 
                 let arg2 = getSecondArg() 
                 f arg2; 
                 cursor := !cursor + 2
               | IntArg f -> 
                 let arg2 = getSecondArg () 
                 let arg2 = try int32 arg2 with _ -> raise(Bad(getUsage argSpecs usageText)) in  
                 f arg2;
                 cursor := !cursor + 2;
               | FloatArg f -> 
                 let arg2 = getSecondArg() 
                 let arg2 = try float arg2 with _ -> raise(Bad(getUsage argSpecs usageText)) in 
                 f arg2; 
                 cursor := !cursor + 2;
               | RestArg f -> 
                 incr cursor;
                 while !cursor < nargs do
                     f (argv.[!cursor]);
                     incr cursor;

            | (_ :: more)  -> findMatchingArg more 
            | [] -> 
                if arg = "-help" || arg = "--help" || arg = "/help" || arg = "/help" || arg = "/?" then
                    raise (Help (getUsage argSpecs usageText))
                // Note: for '/abc/def' does not count as an argument
                // Note: '/abc' does
                elif arg.Length>0 && (arg.[0] = '-' || (arg.[0] = '/' && not (arg.Length > 1 && arg.[1..].Contains ("/")))) then
                    raise (Bad ("unrecognized argument: "+ arg + "\n" + getUsage argSpecs usageText))
                else 
                   other arg;
                   incr cursor
          findMatchingArg specs 

    static member Usage (specs,?usage) = 
        let usage = defaultArg usage ""
        System.Console.Error.WriteLine (getUsage (Seq.toList specs) usage)

    #if FX_NO_COMMAND_LINE_ARGS
    #else
    static member Parse (specs,?other,?usageText) = 
        let current = ref 0
        let argv = System.Environment.GetCommandLineArgs() 
        try ArgParser.ParsePartial (current, argv, specs, ?other=other, ?usageText=usageText)
        with 
          | Bad h 
          | Help h -> 
              System.Console.Error.WriteLine h; 
              System.Console.Error.Flush();  
              System.Environment.Exit(1); 
          | e -> 
              reraise()
    #endif



let Clear  x = ArgType.Clear x
let Float  x = ArgType.Float x
let Int    x = ArgType.Int x
let Rest   x = ArgType.Rest x
let Set    x = ArgType.Set x
let String x = ArgType.String x
let Unit   x = ArgType.Unit x

type spec = ArgType
type argspec = (string * spec * string) 
#if FX_NO_COMMAND_LINE_ARGS
#else

let parse_argv cursor argv specs other usageText =
    ArgParser.ParsePartial(cursor, argv, List.map (fun (a,b,c) -> ArgInfo(a,b,c)) specs, other, usageText)

let parse specs other usageText = 
    ArgParser.Parse(List.map (fun (a,b,c) -> ArgInfo(a,b,c)) specs, other, usageText)

let usage specs usageText = 
    ArgParser.Usage(List.map (fun (a,b,c) -> ArgInfo(a,b,c)) specs, usageText)
#endif
