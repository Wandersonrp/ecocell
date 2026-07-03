using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using AndroidX.Core.View;

namespace Ecocell.Mobile;

// AdjustResize: com a janela em LAYOUT_FULLSCREEN (imposto pelo sistema em navegação
// por gestos), o Android não redimensiona a janela — entrega a altura do teclado via
// WindowInsets. O listener abaixo aplica esse inset como padding no content view,
// encolhendo o BlazorWebView para o campo focado subir acima do teclado (WND-272).
[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, WindowSoftInputMode = SoftInput.AdjustResize, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        var content = FindViewById<ViewGroup>(Android.Resource.Id.Content);
        if (content is null)
            return;

        ViewCompat.SetOnApplyWindowInsetsListener(content, new ImeInsetsListener());
        ViewCompat.RequestApplyInsets(content);
    }

    private sealed class ImeInsetsListener : Java.Lang.Object, IOnApplyWindowInsetsListener
    {
        public WindowInsetsCompat OnApplyWindowInsets(Android.Views.View? view, WindowInsetsCompat? insets)
        {
            if (view is null || insets is null)
                return insets!;

            var imeBottom = insets.GetInsets(WindowInsetsCompat.Type.Ime())?.Bottom ?? 0;
            view.SetPadding(view.PaddingLeft, view.PaddingTop, view.PaddingRight, imeBottom);
            return insets;
        }
    }
}
