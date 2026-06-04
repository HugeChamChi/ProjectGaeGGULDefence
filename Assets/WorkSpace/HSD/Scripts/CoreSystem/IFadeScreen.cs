using Cysharp.Threading.Tasks;

public interface IFadeScreen
{
    UniTask FadeInAsync(float duration = 0.5f);
    UniTask FadeOutAsync(float duration = 0.5f);
}
