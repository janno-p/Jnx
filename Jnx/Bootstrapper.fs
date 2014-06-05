namespace Jnx

open Jnx.Authentication
open Jnx.NancyExtensions
open Nancy
open Nancy.Authentication.Forms
open Nancy.Conventions
open Nancy.Diagnostics
open Nancy.Session

type Bootstrapper() =
    inherit DefaultNancyBootstrapper()

    override this.ApplicationStartup (_, pipelines) =
        CookieBasedSessions.Enable(pipelines) |> ignore
        SessionFlashStore.Enable(pipelines)
        DiagnosticsHook.Disable(pipelines)

    override this.ConfigureConventions conventions =
        base.ConfigureConventions conventions
        conventions.StaticContentsConventions.Add(StaticContentConventionBuilder.AddDirectory("Scripts", "Scripts", "js"))
        conventions.StaticContentsConventions.Add(StaticContentConventionBuilder.AddDirectory("fonts", "fonts", "eot", "svg", "ttf", "woff"))

    override this.ConfigureRequestContainer (container, context) =
        base.ConfigureRequestContainer (container, context)
        container.Register<IUserMapper, DatabaseUser>() |> ignore

    override this.RequestStartup (container, pipelines, context) =
        base.RequestStartup (container, pipelines, context)
        let configuration = FormsAuthenticationConfiguration(RedirectUrl = "~/login", UserMapper = container.Resolve<IUserMapper>())
        FormsAuthentication.Enable(pipelines, configuration)
