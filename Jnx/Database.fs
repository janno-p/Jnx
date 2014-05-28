module Jnx.Database

open FSharp.Data.Sql
open Jnx.Repositories
open Npgsql
open System
open System.Configuration
open System.Data

(*
module Types =
    type CoinType =
        | CommonCoin of decimal
        | CommemorativeCoin of int * bool

    let addFileSuffix (fileName : string) suffix = fileName.Insert(fileName.Length - 4, suffix)

    type Coin =
        { Id : int
          Type : CoinType
          Country : Country
          Image : string
          ForTrade : int
          CollectedAt : DateTime option
          CollectedBy : string option }
        member this.ImageUri with get() = match this.CollectedAt with
                                          | Some _ -> addFileSuffix this.Image "_collected"
                                          | None -> this.ImageThumbUri
        member this.ImageThumbUri with get() = addFileSuffix this.Image "_thumb"
        member this.CollectedByValue with get() = match this.CollectedBy with
                                                  | Some str -> str
                                                  | _ -> ""
        member this.CollectedAtValue with get() = match this.CollectedAt with
                                                  | Some d -> d.ToString("dd.MM.yyyy HH:mm:ss")
                                                  | _ -> ""

    

open Types

module Conversions =
    let toCountry (reader : DynamicDataReader) =
        { Id = reader?CountryId
          Code = reader?CountryCode
          Name = reader?CountryName
          Genitive = reader?CountryGenitive }

    let toCountryStats (reader : DynamicDataReader) =
        { Country = toCountry reader
          CollectedCommon = reader?CollectedCommon
          TotalCommon = reader?TotalCommon
          CollectedCommemorative = reader?CollectedCommemorative
          TotalCommemorative = reader?TotalCommemorative }

    let toCoin (reader : DynamicDataReader) =
        let coinType =
            match reader.Optional?CoinCommemorativeYear with
            | Some year -> CommemorativeCoin (year, false)
            | _ -> CommonCoin reader?CoinNominalValue
        { Id = reader?CoinId
          Type = coinType
          Image = reader?CoinImage
          ForTrade = 0
          CollectedAt = reader.Optional?CoinCollectedAt
          CollectedBy = reader.Optional?CoinCollectedBy
          Country = toCountry reader }

module Queries =
    let UpdateCoin (coin : Coin) =
        let qry = @"update  coins_coin
                       set  collected_by = :collected_by,
                            collected_at = :collected_at
                     where  id = :id"
        Execute qry [("collected_by", box coin.CollectedBy)
                     ("collected_at", box coin.CollectedAt)
                     ("id", box coin.Id)]
*)
