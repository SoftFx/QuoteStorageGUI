# QuoteStorageGUI
Basic Gui application to view/edit quote storage history.

The application supports console mode.

## Commands
#### Import and export files from one storage to another or to folder 
```
-i[mport] <Destination> <Source> [-templates <templates> default: "*"] [-type <type> default: "all"] [-format <format> default: "LevelDB"]
-e[xport] <Source> <Destination> [-templates <templates> default: "*"] [-type <type> default: "all"] [-format <format> default: "LevelDB"]
```   

`<Source>` and `<Destination>` are required parameters. Other optional.

`-templates <templates>`  Allow you to select a template for the operation. More details below.

`-type <type>`  Allow you to select a type of files for operation. Options: `All, H1, M1, ticks, ticks level2`

`-format <format>`  Allow you to select a format of destination storage. Options: `LevelDB` - standart way, copying to server-like levelDB database; `Ntfs` - export text files in readable format to `<Destination>` folder for further manipulations.

##### Examples

`C:\QuoteStorageGUI.exe -import "C:\ToStorage" "C:\FromStorage"` - import all files from one storage to another

`C:\QuoteStorageGUI.exe -import "C:\ToStorage" "C:\FromStorage" -type M1` - import M1 files from one storage to another

`C:\QuoteStorageGUI.exe -import "C:\ToFolder" "C:\FromStorage" -type M1 -format Ntfs` - import M1 files from storage to folder in text format

`C:\QuoteStorageGUI.exe -export "C:\FromStorage" "C:\ToStorage" -template (EUR*|AUD*)/2016` - export all files for EUR\* and AUD\* symbols for 2016 year from one storage to another.

`C:\QuoteStorageGUI.exe -export "C:\FromStorage" "C:\ToStorage" -template (EUR*|AUD*)/2016 -type "ticks level2"` - export only level2 ticks files for EUR\* and AUD\* symbols for 2016 year from one storage to another.


#### Upstream update 
```
-u[pstream] <Source> [-templates <templates> default: "*"] [-type <type> default: "all"] [-degree <format> default: "8"] 
```   
Allow you to build higher periodicity file from lower.
`<Source>` is required parameters. Other optional.

`-templates <templates>`  Allow you to select a template for the operation. More details below.

`-type <type>`  Allow you to select a type of update. Options: `Full, level2->ticks, ticks->m1, m1->h1`

`-degree <degree>`  Degree of parallelism for optimize ticks level2 to ticks update.

##### Examples

`C:\QuoteStorageGUI.exe -upstream "C:\Storage"` - upstream all files

`C:\QuoteStorageGUI.exe -upstream "C:\Storage -type ticks"` - make only ticks->M1 update

