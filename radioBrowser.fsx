#if INTERACTIVE

#r "nuget: Avalonia"
#r "nuget: Avalonia.Desktop"
#r "nuget: Avalonia.Themes.Fluent"
#r "nuget: Avalonia.FuncUI"
#r "nuget: FluentIcons.Avalonia"
#r "nuget: LibVLCSharp"
#r "nuget: RadioBrowser"
#r "nuget: AsyncImageLoader.Avalonia"

#endif

open System
open System.IO
open Avalonia
open Avalonia.Controls
open Avalonia.FuncUI
open Avalonia.FuncUI.DSL
open Avalonia.FuncUI.Hosts
open Avalonia.Layout
open FluentIcons.Avalonia
open System.Collections.ObjectModel
open Avalonia.Input
open FSharp.Control
open AsyncImageLoader
open LibVLCSharp.Shared
open RadioBrowser
open RadioBrowser.Models
open Avalonia.Media.Imaging
open Avalonia.Media
open Avalonia.Controls.Templates
open Avalonia.Styling

[<AutoOpen>]
module SymbolIcon =
    open Avalonia.FuncUI.Types
    open Avalonia.FuncUI.Builder
    open FluentIcons.Common

    let create (attrs: IAttr<SymbolIcon> list) : IView<SymbolIcon> = ViewBuilder.Create<SymbolIcon>(attrs)

    type SymbolIcon with
        static member symbol<'t when 't :> SymbolIcon>(value: Symbol) : IAttr<'t> =
            AttrBuilder<'t>
                .CreateProperty<Symbol>(SymbolIcon.SymbolProperty, value, ValueNone)

[<AutoOpen>]
module HyperlinkButton =
    open Avalonia.FuncUI.Types
    open Avalonia.FuncUI.Builder

    let create (attrs: IAttr<HyperlinkButton> list) : IView<HyperlinkButton> =
        ViewBuilder.Create<HyperlinkButton>(attrs)

[<AbstractClass; Sealed>]
type Views =

    static member main() =
        Component(fun ctx ->
            let items = ctx.useState<ObservableCollection<StationInfo>> (ObservableCollection())

            let countries =
                ctx.useState<ObservableCollection<NameAndCount>> (ObservableCollection())

            let selectedItem = ctx.useState<Option<StationInfo>> None
            let selectedCountry = ctx.useState<Option<NameAndCount>> None
            let searchButtonEnabled = ctx.useState false
            let playEnabled = ctx.useState false
            let isPlaying = ctx.useState false
            let searchText = ctx.useState ""
            let libVlc = ctx.useState (new LibVLC())

            let limit = 100u

            let getDefaultStations =
                async {
                    printfn "getDefaultStations"
                    let client = RadioBrowserClient()
                    return! client.Stations.GetByVotesAsync(limit) |> Async.AwaitTask
                }

            let getCountries =
                async {
                    printfn "getCountries"
                    let client = RadioBrowserClient()
                    let! result = client.Lists.GetCountriesAsync() |> Async.AwaitTask
                    let empty = new NameAndCount()
                    empty.Name <- ""
                    empty.Stationcount <- 0u
                    result.Insert(0, empty)
                    return result
                }

            ctx.useEffect (
                handler =
                    (fun _ ->
                        printfn "init"

                        Async.StartWithContinuations(
                            getDefaultStations,
                            (fun (stations) -> (stations |> Seq.iter (fun x -> items.Current.Add(x)))),
                            (fun ex -> (printfn "%A" ex)),
                            (fun _ -> ())
                        )

                        Async.StartWithContinuations(
                            getCountries,
                            (fun (_countries) -> (_countries |> Seq.iter (fun x -> countries.Current.Add(x)))),
                            (fun ex -> (printfn "%A" ex)),
                            (fun _ -> ())
                        )),
                triggers = [ EffectTrigger.AfterInit ]
            )

            let getPlayer =
                let _player = new MediaPlayer(libVlc.Current)

                _player.EndReached.Add(fun _ ->
                    isPlaying.Set(false)
                    _player.Media.Dispose()
                    _player.Media <- null)

                _player

            let player = ctx.useState (getPlayer)

            let doSearch =
                async {
                    searchButtonEnabled.Set(false)
                    printfn "search"

                    try
                        items.Current.Clear()
                        let client = RadioBrowserClient()

                        let options =
                            let opt = new AdvancedSearchOptions()
                            opt.Limit <- limit
                            opt.Offset <- 0u

                            if not (String.IsNullOrEmpty(searchText.Current)) then
                                opt.Name <- searchText.Current

                            if
                                selectedCountry.Current.IsSome
                                && selectedCountry.Current.Value.Stationcount > 0u
                            then
                                opt.Country <- selectedCountry.Current.Value.Name

                            opt

                        let! results = client.Search.AdvancedAsync(options) |> Async.AwaitTask

                        results |> Seq.iter (fun x -> items.Current.Add(x))
                    with ex ->
                        printfn "%A" ex

                    searchButtonEnabled.Set(true)

                    if not isPlaying.Current then
                        playEnabled.Set(false)
                }

            let play =
                async {
                    match selectedItem.Current with
                    | None -> ()
                    | Some track ->
                        playEnabled.Set(false)

                        try
                            isPlaying.Set(
                                player.Current.Play(new Media(libVlc.Current, selectedItem.Current.Value.UrlResolved))
                            )
                        with ex ->
                            printfn "%A" ex
                            isPlaying.Set(false)

                        playEnabled.Set(true)
                }

            let playStop =
                async {
                    if isPlaying.Current then
                        player.Current.Stop()
                        isPlaying.Set(false)
                    else
                        play |> Async.Start
                }

            let getItem (item: StationInfo) =
                let defaultImg = new Bitmap(Path.Combine(__SOURCE_DIRECTORY__, "img/radio.png"))

                let img =
                    async {
                        if item.Favicon = null || String.IsNullOrEmpty item.Favicon.AbsoluteUri then
                            return defaultImg
                        else
                            return!
                                ImageLoader.AsyncImageLoader.ProvideImageAsync(item.Favicon.AbsoluteUri)
                                |> Async.AwaitTask
                    }

                let languages = item.Language |> String.concat ", "

                Border.create
                    [ Border.padding 5.
                      Border.margin 0.
                      Border.child (
                          StackPanel.create
                              [ StackPanel.orientation Orientation.Horizontal
                                StackPanel.width 360
                                StackPanel.useLayoutRounding true
                                StackPanel.children
                                    [ Image.create
                                          [ Image.width 90
                                            Image.height 90
                                            Image.init (fun x ->
                                                Async.StartWithContinuations(
                                                    img,
                                                    (fun b -> x.Source <- b),
                                                    (fun _ -> x.Source <- defaultImg),
                                                    (fun _ -> x.Source <- defaultImg)
                                                )) ]
                                      StackPanel.create
                                          [ StackPanel.orientation Orientation.Vertical
                                            StackPanel.margin 5
                                            StackPanel.children
                                                [ TextBlock.create
                                                      [ TextBlock.text item.Name
                                                        TextBlock.fontSize 16.0
                                                        TextBlock.fontWeight FontWeight.Bold ]
                                                  TextBlock.create
                                                      [ TextBlock.text
                                                            $"{item.Codec} : {item.Bitrate} kbps {languages}"
                                                        TextBlock.fontSize 14.0 ]
                                                  TextBlock.create
                                                      [ TextBlock.text (
                                                            item.Tags
                                                            |> Seq.map (fun s -> s.Trim())
                                                            |> String.concat ", "
                                                        )
                                                        TextBlock.textWrapping TextWrapping.WrapWithOverflow
                                                        TextBlock.width 160.0
                                                        TextBlock.height 40.0
                                                        TextBlock.fontSize 12.0 ] ] ] ] ]
                      ) ]

            let getCountryItem (item: NameAndCount) =
                let count =
                    if item.Stationcount = 0u then
                        ""
                    else
                        item.Stationcount.ToString()

                StackPanel.create
                    [ StackPanel.orientation Orientation.Horizontal
                      StackPanel.children
                          [ TextBlock.create [ TextBlock.text item.Name; TextBlock.width 230 ]
                            TextBlock.create [ TextBlock.text (count); TextBlock.width 50 ] ] ]

            let getStyle =
                let style = new Style(fun x -> x.OfType(typeof<ListBoxItem>))
                style.Setters.Add(Setter(ListBoxItem.PaddingProperty, Thickness(2.0)))
                style.Setters.Add(Setter(ListBoxItem.CornerRadiusProperty, CornerRadius(5.0)))
                style.Setters.Add(Setter(ListBoxItem.BorderBrushProperty, Brushes.Gray))
                style.Setters.Add(Setter(ListBoxItem.BorderThicknessProperty, Thickness(1.0)))
                style.Setters.Add(Setter(ListBoxItem.MarginProperty, Thickness(2.0)))
                style :> IStyle

            DockPanel.create
                [ DockPanel.children
                      [ StackPanel.create
                            [ StackPanel.orientation Orientation.Horizontal
                              StackPanel.dock Dock.Top
                              StackPanel.margin 4
                              StackPanel.children
                                  [ ComboBox.create
                                        [ ComboBox.width 300
                                          ComboBox.margin 4
                                          ComboBox.dataItems countries.Current
                                          ComboBox.itemTemplate (
                                              DataTemplateView<_>.create (fun (data: NameAndCount) ->
                                                  getCountryItem data)
                                          )
                                          ComboBox.onSelectedItemChanged (fun item ->
                                              (match box item with
                                               | null -> None
                                               | :? NameAndCount as i -> Some i
                                               | _ -> failwith "Something went horribly wrong!")
                                              |> selectedCountry.Set

                                              let isCountrySelected =
                                                  selectedCountry.Current.IsSome
                                                  && selectedCountry.Current.Value.Stationcount > 0u

                                              if isCountrySelected && not searchButtonEnabled.Current then
                                                  searchButtonEnabled.Set(true)
                                              elif
                                                  not isCountrySelected
                                                  && searchButtonEnabled.Current
                                                  && String.IsNullOrWhiteSpace(searchText.Current)
                                              then
                                                  searchButtonEnabled.Set(false)) ]
                                    TextBox.create
                                        [ TextBox.margin 4
                                          TextBox.watermark "Search radio stations"
                                          TextBox.width 380
                                          TextBox.onKeyDown (fun e ->
                                              if e.Key = Key.Enter then
                                                  let textBox = e.Source :?> TextBox
                                                  searchText.Set(textBox.Text)
                                                  searchButtonEnabled.Set(false)
                                                  Async.StartImmediate doSearch)
                                          TextBox.onTextChanged (fun e ->
                                              (if
                                                   not (String.IsNullOrWhiteSpace(e))
                                                   && not searchButtonEnabled.Current
                                               then
                                                   searchButtonEnabled.Set(true)
                                               elif
                                                   String.IsNullOrWhiteSpace(e)
                                                   && searchButtonEnabled.Current
                                                   && selectedCountry.Current.Value.Stationcount = 0u
                                               then
                                                   searchButtonEnabled.Set(false))) ]
                                    Button.create
                                        [ Button.content (
                                              SymbolIcon.create
                                                  [ SymbolIcon.width 24
                                                    SymbolIcon.height 24
                                                    SymbolIcon.symbol FluentIcons.Common.Symbol.Search ]
                                          )
                                          ToolTip.tip "Search"
                                          Button.isEnabled searchButtonEnabled.Current
                                          Button.onClick (fun e ->
                                              let button = e.Source :?> Button
                                              let stackPanel = button.Parent :?> StackPanel

                                              let textBox =
                                                  stackPanel.Children
                                                  |> Seq.filter (fun c -> c :? TextBox)
                                                  |> Seq.head
                                                  :?> TextBox

                                              printfn "search text is %s" textBox.Text
                                              searchText.Set(textBox.Text)
                                              searchButtonEnabled.Set(false)
                                              Async.StartImmediate doSearch) ]
                                    Button.create
                                        [ Button.content (
                                              SymbolIcon.create
                                                  [ SymbolIcon.width 24
                                                    SymbolIcon.height 24
                                                    SymbolIcon.symbol (
                                                        if isPlaying.Current then
                                                            FluentIcons.Common.Symbol.Stop
                                                        else
                                                            FluentIcons.Common.Symbol.Play
                                                    ) ]
                                          )
                                          ToolTip.tip (if isPlaying.Current then "Stop" else "Play")
                                          Button.isEnabled playEnabled.Current
                                          Button.onClick (fun _ -> Async.StartImmediate playStop) ] ] ]
                        ListBox.create
                            [ ListBox.dock Dock.Top
                              ListBox.dataItems items.Current
                              ListBox.itemsPanel (FuncTemplate<Panel>(fun () -> WrapPanel()))
                              ListBox.styles ([ getStyle ])
                              ListBox.onSelectedItemChanged (fun item ->
                                  (match box item with
                                   | null -> None
                                   | :? StationInfo as i -> Some i
                                   | _ -> failwith "Something went horribly wrong!")
                                  |> selectedItem.Set

                                  playEnabled.Set true)
                              ListBox.itemTemplate (
                                  DataTemplateView<_>.create (fun (data: StationInfo) -> getItem data)
                              )
                              ListBox.margin 4 ] ] ])

type MainWindow() as this =
    inherit HostWindow()

    do
        base.Title <- "Radio Browser"
        base.Width <- 800.0
        base.Height <- 500.0
        base.Icon <- new WindowIcon(new Bitmap(Path.Combine(__SOURCE_DIRECTORY__, "img/Fsharp_logo.png")))
        this.Content <- Views.main ()

type App() =
    inherit Application()

    override this.Initialize() =
        this.Styles.Add(Themes.Fluent.FluentTheme())
        this.RequestedThemeVariant <- Styling.ThemeVariant.Dark

    override this.OnFrameworkInitializationCompleted() =

        match this.ApplicationLifetime with
        | :? ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime as desktop ->
            desktop.MainWindow <- MainWindow()
            printfn "App running..."
        | _ -> ()

let app =
    AppBuilder
        .Configure<App>()
        .UsePlatformDetect()
        .StartWithClassicDesktopLifetime([||])