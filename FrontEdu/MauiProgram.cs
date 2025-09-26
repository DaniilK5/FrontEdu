using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Storage;
using FrontEdu.Services;
using FrontEdu.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;

namespace FrontEdu
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            // Настраиваем HttpClient
            builder.Services.AddHttpClient("API", client =>
            {
                client.BaseAddress = new Uri(AppConfig.ApiBaseUrl);
                // Добавляем заголовок Accept как в curl запросе
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
            }).ConfigurePrimaryHttpMessageHandler(() =>
            {
                var handler = new HttpClientHandler();
#if DEBUG
                handler.ServerCertificateCustomValidationCallback = 
                    (message, cert, chain, sslPolicyErrors) => true;
#endif
                return handler;
            });
            builder.Services.AddSingleton<IFileSaver>(FileSaver.Default);
#if WINDOWS
            // Явно указываем поддержку TLS для Windows
            System.Net.ServicePointManager.SecurityProtocol = 
                System.Net.SecurityProtocolType.Tls12 | 
                System.Net.SecurityProtocolType.Tls11 | 
                System.Net.SecurityProtocolType.Tls;
#endif

#if DEBUG
            builder.Logging.AddDebug();
#endif
            // Регистрируем IFileSaver как синглтон
            builder.Services.AddSingleton(FileSaver.Default);

            // Регистрируем SchedulePage как transient
            builder.Services.AddTransient<SchedulePage>();


            return builder.Build();
        }
    }
}
