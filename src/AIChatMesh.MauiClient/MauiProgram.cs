using AIChatMesh.Client.Services;
using AIChatMesh.MauiClient.Pages;
using AIChatMesh.MauiClient.ViewModels;
using Microsoft.Extensions.Logging;

namespace AIChatMesh.MauiClient;

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

		builder.Services.AddSingleton<ChatService>();
		builder.Services.AddTransient<ChatPageViewModel>();
		builder.Services.AddTransient<ChatPage>();
		builder.Services.AddTransient<SettingsPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
