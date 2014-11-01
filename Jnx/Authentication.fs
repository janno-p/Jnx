module Jnx.Authentication

open Jnx.NancyExtensions
open Jnx.Repositories
open Nancy
open Nancy.Authentication.Forms
open Nancy.Security
open Nancy.SimpleAuthentication
open Nancy.Validation
open System.Collections.Generic

type UserIdentity (user : User) =
    let userName = match user.Name with
                   | Some name -> name
                   | _ -> user.Email.Substring(0, user.Email.IndexOf('@'))
    interface IUserIdentity with
        member this.UserName with get () = userName
        member this.Claims
            with get() =
                let claims = [
                    if not ((user.Roles &&& 1) = 0) then
                        yield "admin"
                ]
                claims :> IEnumerable<string>

type DatabaseUser () =
    interface IUserMapper with
        member this.GetUserFromIdentifier (identifier, context) =
            match Users.GetByIdentifier identifier with
            | Some user -> UserIdentity (user) :> IUserIdentity
            | _ -> null

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
                           | Some user -> populateUser user userInfo
                           | _ -> { (populateUser User.NewUser userInfo) with ProviderName = providerName
                                                                              ProviderIdentity = userInfo.Id }
                           |> Users.Save
                match user.IsApproved with
                | true ->
                    authModule.Context.CurrentUser <- new UserIdentity(user)
                    authModule.Flash "success" "Sisselogimine õnnestus."
                    authModule.LoginAndRedirect (user.Identifier, fallbackRedirectUrl = model.ReturnUrl) :> obj
                | false ->
                    authModule.Flash "error" "Antud kasutajal ei ole veel õigusi rakenduse täielikuks kasutamiseks."
                    authModule.Response.AsRedirect(model.ReturnUrl) :> obj
            | e ->
                authModule.Context.CurrentUser <- null
                authModule.Flash "error" e.Message
                authModule.Response.AsRedirect "/" :> obj
