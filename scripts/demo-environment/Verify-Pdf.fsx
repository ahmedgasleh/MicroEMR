#I "../../.tmp/demo-readiness/document-guid-fix/bin/Debug/net10.0"
#r "UglyToad.PdfPig.Core.dll"
#r "UglyToad.PdfPig.Tokens.dll"
#r "UglyToad.PdfPig.Tokenization.dll"
#r "UglyToad.PdfPig.Fonts.dll"
#r "UglyToad.PdfPig.dll"
open System
open System.Text.RegularExpressions
open UglyToad.PdfPig
let args = fsi.CommandLineArgs |> Array.skip 1
if args.Length <> 3 then failwith "Expected PDF path, patient surname, clinic name."
use document = PdfDocument.Open(args[0])
if document.NumberOfPages < 1 then failwith "PDF has no pages."
let compact (value:string) = Regex.Replace(value,@"\s+","").ToLowerInvariant()
let content = document.GetPages() |> Seq.map(fun p -> p.Text) |> String.concat " " |> compact
for expected in args[1..2] do
    if String.IsNullOrWhiteSpace(expected) || not(content.Contains(compact expected)) then
        failwith "PDF lacks expected patient or clinic branding text."
printfn "PASS: generated PDF contains patient/clinic text; pages=%d" document.NumberOfPages
