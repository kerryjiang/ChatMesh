using ChatMesh.Client;
using ChatMesh.MauiClient.Pages;
using ChatMesh.MauiClient.ViewModels;
using Microsoft.Extensions.Logging;

namespace ChatMesh.MauiClient;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		builder.Services.AddSingleton<ChatClient>();
		builder.Services.AddTransient<ChatPageViewModel>();
		builder.Services.AddTransient<ChatPage>();
		builder.Services.AddTransient<SettingsPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
