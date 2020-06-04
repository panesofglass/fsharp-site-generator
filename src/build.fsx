#r "nuget:AngleSharp"
#r "nuget:Deedle"

open System
open System.IO
open AngleSharp
open AngleSharp.Html.Parser
open Deedle

let src = DirectoryInfo(__SOURCE_DIRECTORY__)
let root = src.Parent.FullName
let dataDir = Path.Combine(root, "data")
let srcDir = src.FullName
let outDir = Path.Combine(root, "out")

// Load and parse CSV
let csvPath = Path.Combine(dataDir, "services.csv")
let df = Frame.ReadCsv(csvPath)
//df.Print()

let now = DateTime.Today
let lastSunday = now.AddDays(-(float now.DayOfWeek))
let nextSunday = lastSunday.AddDays(7.)
let services =
    df.Rows |> Series.filterValues (fun row ->
        let date = row.GetAs<DateTime>("Date")
        lastSunday < date && date <= nextSunday)
//services.Print()

let svcs =
    services |> Series.mapValues (fun row ->
        let date = row.GetAs<DateTime>("Date")
        let time = row.GetAs<string>("Time").Split(':')
        let dt = date.AddHours(float time.[0]).AddMinutes(float time.[1])
        let title = row.GetAs<string>("Title")
        let lang = row.GetAs<string>("Language")
        sprintf "%s %s (%s)" (dt.ToString("d")) title lang)
//svcs.Print()

// Load and parse HTML
let htmlPath = Path.Combine(srcDir, "index.html")
let context = BrowsingContext.New(Configuration.Default)
let parser = context.GetService<IHtmlParser>()
let doc = using (File.OpenRead htmlPath) parser.ParseDocument

// Set the correct form action
let form : Html.Dom.IHtmlFormElement = downcast doc.GetElementsByTagName("form").[0]
form.Action <- "https://some-function-api.azurewebsites.net/"

// Add the options for the upcoming week.
let frag = doc.CreateDocumentFragment()
for key in svcs.Keys do
    let opt : Html.Dom.IHtmlOptionElement = downcast doc.CreateElement("option")
    opt.Value <- svcs.[key]
    opt.TextContent <- svcs.[key]
    frag.AppendChild(opt) |> ignore
let select = doc.GetElementsByTagName("select").[0]
select.AppendChild(frag) |> ignore

doc.Prettify()

// Clean the out directory
if (Directory.Exists outDir) then Directory.Delete(outDir, recursive=true)
Directory.CreateDirectory outDir
// Write the result to the outDir
let outPath = Path.Combine(outDir, "index.html")
// Prettified
using (File.CreateText outPath) (fun writer -> writer.Write(doc.Prettify()))
// Minified
using (File.CreateText outPath) (fun writer -> writer.Write(doc.Minify()))
// Copy stylesheet
File.Copy(Path.Combine(srcDir, "style.css"), Path.Combine(outDir, "style.css"))
// Write the timestamp
let lastWriteTime = DateTimeOffset(File.GetLastWriteTime(outPath))
using (File.CreateText(Path.Combine(outDir, "timestamp"))) (fun writer -> writer.Write(lastWriteTime.ToUnixTimeMilliseconds()))
