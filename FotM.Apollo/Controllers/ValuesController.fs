namespace FotM.Apollo.Controllers

open System
open System.Collections.Generic
open System.Linq
open System.Net.Http
open System.Web.Http
open System.Net
open System.Text
open Newtonsoft.Json
open FotM.Hephaestus.TraceLogging
open FotM.Hephaestus.Math
open FotM.Data
open FotM.Apollo
open FotM.Aether

type TeamViewModel (rank: int, teamInfo: TeamInfo)=
    member this.rank = rank
    member this.players = teamInfo.lastEntry.players
    member this.factionId = int (teamInfo.lastEntry.players |> Seq.head).faction
    member this.rating = teamInfo.lastEntry.rating
    member this.ratingChange = teamInfo.lastEntry.ratingChange
    member this.seen = teamInfo.lastEntry.snapshotTime.ToDateTimeUtc().ToString()

type SetupViewModel (rank: int, specs: Class list, ratio: float) =
    member this.rank = rank
    member this.specs = specs
    member this.percent = sprintf "%.1f%%" (ratio * 100.0)

/// Retrieves values.
[<RoutePrefix("api")>]
type ValuesController() =
    inherit ApiController()

    let playingNowPeriod = NodaTime.Duration.FromStandardDays(10L)

    let parseFilters (filters: string seq) =
        filters
        |> Seq.map (fun str -> 
            let dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(str)
            Specs.fromString (dict.["className"]) (dict.["specId"]) )
        |> Seq.choose id
        |> Seq.toArray

    [<Route("{region}/{bracket}")>]
    member this.Get(region: string, bracket: string, [<FromUri>]filters: string seq) =
        let fotmFilters = parseFilters filters

        let armoryInfo = Main.repository.getArmory(region, bracket)

        let filteredTeams =
            armoryInfo.teams
            |> Seq.filter (fun (i, t) -> t |> Teams.teamMatchesFilter fotmFilters)

        let filteredSetups = 
            armoryInfo.setups
            |> Seq.mapi(fun i setup -> i+1, setup)
            |> Seq.filter(fun (rank, setup) -> fst setup |> Teams.matchesFilter fotmFilters)
            
        filteredTeams |> Seq.map(fun t -> TeamViewModel t), 
            filteredSetups |> Seq.map(fun (rank, s) -> 
                SetupViewModel(rank, fst s, snd s ./. armoryInfo.totalGames)
            )

    [<Route("{region}/{bracket}/now")>]
    member this.Get(region: string, bracket: string) =
        let now = NodaTime.SystemClock.Instance.Now

        let seen teamInfo = teamInfo.lastEntry.snapshotTime

        let armoryInfo = Main.repository.getArmory(region, bracket)

        let filteredTeams =
            armoryInfo.teams
            |> Seq.filter(fun (rank, team) -> now - seen team < playingNowPeriod)
        
        filteredTeams |> Seq.map(fun t -> TeamViewModel t)