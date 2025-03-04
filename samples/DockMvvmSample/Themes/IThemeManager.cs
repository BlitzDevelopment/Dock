using Avalonia;

namespace Blitz.Themes;

public interface IThemeManager
{
    void Initialize(Application application);

    void Switch(int index);
}
