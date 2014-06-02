module Jnx.Authentication

open Jnx.NancyExtensions
open Jnx.Repositories
open Nancy
open Nancy.Security
open Nancy.SimpleAuthentication
open Nancy.Validation
open System.Collections.Generic

type UserIdentity (user : User, claims : string list) =
    let userName = match user.Name with
                   | Some name -> name
                   | _ -> user.Email.Substring(0, user.Email.IndexOf('@'))
    interface IUserIdentity with
        member this.UserName with get () = userName
        member this.Claims with get() = claims :> IEnumerable<string>

type AuthenticationCallbackProvider () =
    interface IAuthenticationCallbackProvider with
        member this.OnRedirectToAuthenticationProviderError (context, message) =
            "viga" :> obj
        member this.Process (authModule, model) =
            let populateUser user (userInfo : SimpleAuthentication.Core.UserInformation) =
                { user with Name = match userInfo.Name with | null | "" -> None | x -> Some x
                            Email = userInfo.Email
                            Picture = userInfo.Picture }
            match model.Exception with
            | null ->
                let userInfo = model.AuthenticatedClient.UserInformation
                let providerName = model.AuthenticatedClient.ProviderName
                let user = match Users.GetByIdentity providerName userInfo.Id with
                           | Some user ->
                                Users.Update (populateUser user userInfo)
                           | _ ->
                                let newUser = populateUser User.NewUser userInfo
                                Users.Create { newUser with ProviderName = providerName
                                                            ProviderIdentity = userInfo.Id }
                authModule.Context.CurrentUser <- new UserIdentity(user, [])
                authModule.Flash "success" "Sisselogimine õnnestus"
            | e ->
                authModule.Context.CurrentUser <- null
                authModule.Flash "error" e.Message

            authModule.Response.AsRedirect("~/") :> obj
