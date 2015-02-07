#I "tools/FAKE/tools"
#r "FakeLib.dll"

open System
open System.IO
open Fake
open Fake.FileUtils

type OS =
	Mac | Windows

type Arch =
	X86 | X64

let removeDir subdir project =
	let path = Path.Combine(subdir, project)
	Directory.Delete path, true

let removeBuild project = removeDir "build" project

// Targets
// --------------------------------------------------------
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
"zlib"
	==> "freetype"
	==> "win-iconv"
	==> "libffi"
	==> "openssl"
	==> "gettext-runtime"
	==> "libxml2"
	==> "fontconfig"
	==> "libpng"
	==> "pixman"
	==> "glib"
	==> "harfbuzz"
	==> "atk"
	==> "gdk-pixbuf"
	==> "pango"
	==> "gtk"
	==> "BuildAll"