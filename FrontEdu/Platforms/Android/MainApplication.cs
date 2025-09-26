using Android.App;
using Android.OS;
using Android.Runtime;

namespace FrontEdu;

[Application]
public class MainApplication : MauiApplication
{
    public MainApplication(IntPtr handle, JniHandleOwnership ownership)
        : base(handle, ownership)
    {
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    public override void OnCreate()
    {
        base.OnCreate();

        // Разрешаем cleartext traffic для отладки
#if DEBUG
        StrictMode.VmPolicy.Builder builder = new StrictMode.VmPolicy.Builder();
        StrictMode.SetVmPolicy(builder.Build());
#endif
    }
}
