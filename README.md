# MoviePulse

MoviePulse recommends movies based on a manually selected mood or a webcam expression, with optional genre filters. It uses C#, ASP.NET Core MVC, Razor, SQLite, and ML.NET.

## Run locally

Install the .NET 9 SDK, then run these commands from the repository root:

```sh
dotnet restore
dotnet run --launch-profile https
```

Open the local HTTPS address printed by the app. If the browser does not trust the development certificate, run `dotnet dev-certs https --trust` where supported.

Register an account to access the dashboard and recommendation history. The first registered user on a fresh database becomes an administrator. Admins can manage homepage updates and grant admin access to registered accounts.

## Camera and training

Camera mode requires browser camera permission, email verification, and access to the browser model assets served through jsDelivr. For local testing, the verification service writes codes to the local `Data/outbox/` directory.

The app initializes a local SQLite database and trains models from the bundled sample datasets on startup as needed. To run the training workflow separately:

```sh
dotnet run -- --train-models
```

See the [dataset guide](Data/datasets/README.md) for input formats. The facial samples and their augmented variants are synthetic, so their evaluation scores do not measure real-world webcam accuracy.

Local databases, verification messages, training reports, model files, and build outputs are excluded from Git.
