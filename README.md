# QuoteStorageGUI
Basic Gui application to view/edit quote storage history.

The application supports console mode.

##Commands

Import and export files from one storage to another or to folder 
```
-i[mport] <Destination> <Source> [-templates <templates> default:\"*\"] [-type <type> default:\"all\"] [-format <format> default:\"LevelDB\"]
-e[xport] <Source> <Destination> [-templates <templates> default:\"*\"] [-type <type> default:\"all\"] [-format <format> default:\"LevelDB\"]
```   

`<Source>` and `<Destination>` are required parameters. Other optional.

`-templates <templates>`  Allow you to select a template for the operation. More details below.

`-type <type>`  Allow you to select a type of files for operation. Options: `All, H1, M1, ticks, ticks level2`


