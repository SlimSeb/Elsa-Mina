using Autofac;
using ElsaMina.Core.Services.Config;
using ElsaMina.Sheets;
using ElsaMina.Sheets.GoogleDrive;
using ElsaMina.Sheets.GoogleSheets;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Sheets.v4;
using Newtonsoft.Json;

namespace ElsaMina.Core.Modules;

public class SheetsModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        base.Load(builder);

        builder.Register(ctx => CreateCredential(ctx.Resolve<IConfiguration>()))
            .As<GoogleCredential>()
            .SingleInstance();

        builder.Register(ctx => new GoogleSheetProvider(ctx.Resolve<GoogleCredential>()))
            .As<ISheetProvider>()
            .SingleInstance();

        builder.Register(ctx => new GoogleDriveProvider(ctx.Resolve<GoogleCredential>()))
            .As<IDriveProvider>()
            .SingleInstance();
    }

    private static GoogleCredential CreateCredential(IConfiguration configuration)
    {
        // This is stupid but I couldn't find a better way
        var json = JsonConvert.SerializeObject(configuration.GoogleServiceAccountData);
        var serviceAccountCredential = CredentialFactory.FromJson<ServiceAccountCredential>(json);
        return GoogleCredential
            .FromServiceAccountCredential(serviceAccountCredential)
            .CreateScoped(SheetsService.ScopeConstants.Spreadsheets, DriveService.ScopeConstants.DriveReadonly);
    }
}