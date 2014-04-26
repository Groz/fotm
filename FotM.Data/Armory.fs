﻿namespace FotM.Data

(* DOMAIN TYPES *)
type Realm = {
    realmId: int;
    realmName: string;
    realmSlug: string;
}

type Faction =
| Horde = 0
| Alliance = 1

type Gender =
| Male = 0
| Female = 1

type Race =
| Human = 1
| Orc = 2
| Dwarf = 3
| ``Night Elf`` = 4
| Undead = 5
| Tauren = 6
| Gnome = 7
| Troll = 8
| Goblin = 9
| ``Blood Elf`` = 10
| Draenei = 11
| Worgen = 22
| ``Pandaren Neutral`` = 24
| ``Pandaren Alliance`` = 25
| ``Pandaren Horde`` = 26

type Player = {
    name: string;
    realm: Realm;
    faction: Faction
    race: Race;
    gender: Gender;
    classSpec: Class;
}

type PlayerUpdateFeatures = {
    player: Player;

    // features
    ranking: int;
    rating: int;
    weeklyWins: int;
    weeklyLosses: int;
    seasonWins: int;
    seasonLosses: int;

    // diff features
    ratingDiff: int;
}

type PlayerEntry = {
    player: Player;

    ranking: int;
    rating: int;
    seasonWins: int;
    seasonLosses: int;
    weeklyWins: int;
    weeklyLosses: int;
} 
with
    static member (-) (current: PlayerEntry, previous: PlayerEntry): PlayerUpdateFeatures = {
        player = current.player;
        ranking = current.ranking;
        rating = current.rating;
        weeklyWins = current.weeklyWins;
        weeklyLosses = current.weeklyLosses;
        seasonWins = current.seasonWins;
        seasonLosses = current.seasonLosses;
        ratingDiff = current.rating - previous.rating;
    }

type Bracket = {
    url: string;
    teamSize: int;
}

[<StructuralEquality;NoComparison>]
type LadderSnapshot = {
    bracket: Bracket
    ladder: PlayerEntry[]
    timeTaken: NodaTime.Instant
}

module Brackets =
    let twos = { url = "2v2"; teamSize = 2 }
    let threes = { url = "3v3"; teamSize = 3 }
    let fives = { url = "5v5"; teamSize = 5 }
    let rbg = { url = "rbg"; teamSize = 10 }
    let all = [twos; threes; fives; rbg]

(* /DOMAIN TYPES *)
