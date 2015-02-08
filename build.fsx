#I "tools/FAKE/tools"
#I "tools/FSharp.Data/lib/net40"
#r "FakeLib.dll"
#r "FSharp.Data"

open System
open System.IO
open Fake
open Fake.FileUtils
open FSharp.Data

type OS =
  Mac | Windows

type Arch =
  X86 | X64

let removeDir subdir project =
  let path = Path.Combine(subdir, project)
  Directory.Delete path, true

let removeBuild project = removeDir "build" project

let sh command args =
  ProcessHelper.ExecProcessAndReturnMessages (fun info ->
    info.FileName <- command
    info.Arguments <- args
  ) TimeSpan.MaxValue

let extract file =
  ensureDirectory "builddir"

  sprintf "x %s -obuilddir" file
  |> sh "C:\Program Files\7-Zip\7z.exe"
  |> ignore

let urls = Map [("atk", "http://dl.hexchat.net/gtk-win32/src/atk-2.14.0.7z");
                ("cairo", "http://dl.hexchat.net/gtk-win32/src/cairo-1.14.0.7z");
                ("fontconfig", "http://dl.hexchat.net/gtk-win32/src/fontconfig-2.8.0.7z");
                ("freetype", "http://dl.hexchat.net/gtk-win32/src/freetype-2.5.5.7z");
                ("gdk-pixbuf", "http://dl.hexchat.net/gtk-win32/src/gdk-pixbuf-2.30.8.7z");
                ("gettext-runtime", "http://dl.hexchat.net/gtk-win32/src/gettext-runtime-0.18.7z");
                ("glib", "http://dl.hexchat.net/gtk-win32/src/glib-2.42.1.7z");
                ("gtk", "http://dl.hexchat.net/gtk-win32/src/gtk-2.24.25.7z");
                ("harfbuzz", "http://dl.hexchat.net/gtk-win32/src/harfbuzz-0.9.37.7z");
                ("libffi", "http://dl.hexchat.net/gtk-win32/src/libffi-3.0.13.7z");
                ("libpng", "http://dl.hexchat.net/gtk-win32/src/libpng-1.6.16.7z");
                ("libxml2", "http://dl.hexchat.net/gtk-win32/src/libxml2-2.9.1.7z");
                ("openssl", "http://dl.hexchat.net/gtk-win32/src/openssl-1.0.1l.7z");
                ("pango", "http://dl.hexchat.net/gtk-win32/src/pango-1.36.8.7z");
                ("pixman", "http://dl.hexchat.net/gtk-win32/src/pixman-0.32.6.7z");
                ("win-iconv", "http://dl.hexchat.net/gtk-win32/src/win-iconv-0.0.6.7z");
                ("zlib", "http://dl.hexchat.net/gtk-win32/src/zlib-1.2.8.7z")]

// Targets
// --------------------------------------------------------
Target "FetchAll" <| fun _ ->
  let downloadFile (url:string) =
    let filename = url.Split('/') |> Array.toList
                                  |> List.rev
                                  |> List.head
    let path = Path.Combine("cache", filename)

    match fileExists(path) with
      | true -> printfn "%s already downloaded, skipping." filename
      | false -> match Http.Request(url).Body with
                 | Text text ->
                   printfn "Received text instead of binary from %s" url
                 | Binary bytes ->
                   File.WriteAllBytes(path, bytes)
                   printfn "Downloaded: %s" filename

    path

  ensureDirectory "cache"
  urls |> Map.iter (fun k v -> downloadFile(v) |> extract)

Target "freetype" <| fun _ ->
  trace "freetype"

Target "libffi" <| fun _ ->
  trace "libffi"

Target "openssl" <| fun _ ->
  trace "openssl"

Target "gettext-runtime" <| fun _ ->
  trace "gettext-runtime"

Target "libxml2" <| fun _ ->
  trace "libxml2"

Target "fontconfig" <| fun _ ->
  trace "fontconfig"

Target "pixman" <| fun _ ->
  trace "pixman"

Target "glib" <| fun _ ->
  trace "glib"

Target "cairo" <| fun _ ->
  trace "cairo"

Target "harfbuzz" <| fun _ ->
  trace "harfbuzz"

Target "atk" <| fun _ ->
  trace "atk"

Target "gdk-pixbuf" <| fun _ ->
  trace "gdk-pixbuf"

Target "pango" <| fun _ ->
  trace "pango"

Target "gtk" <| fun _ ->
  trace "gtk"

Target "zlib" <| fun _ ->
  trace "zlib"

Target "win-iconv" <| fun _ ->
  trace "win-iconv"

Target "libpng" <| fun _ ->
  trace "libpng"

Target "BuildAll" <| fun _ ->
  let config = getBuildParamOrDefault "config" "debug"
  trace("BuildAll " + config)

// Dependencies
// --------------------------------------------------------
"atk" <== ["glib"]
"cairo" <== ["fontconfig"; "glib"; "pixman"]
"fontconfig" <== ["freetype"; "libxml2"]
"gdk-pixbuf" <== ["glib"; "libpng"]
"gettext-runtime" <== ["win-iconv"]
"glib" <== ["gettext-runtime"; "libffi"; "zlib"]
"gtk" <== ["atk"; "gdk-pixbuf"; "pango"]
"harfbuzz" <== ["freetype"; "glib"]
"libpng" <== ["zlib"]
"libxml2" <== ["win-iconv"]
"openssl" <== ["zlib"]
"pango" <== ["cairo"; "harfbuzz"]
"pixman" <== ["libpng"]

"BuildAll" <== ["FetchAll"; "gtk"]

RunTargetOrDefault "BuildAll"