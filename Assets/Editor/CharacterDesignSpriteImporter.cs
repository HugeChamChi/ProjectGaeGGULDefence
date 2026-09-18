using UnityEditor;
using UnityEngine;

/// <summary>
/// Drone_Deck/Character_Designs 폴더에 새로 들어오는 캐릭터 원화를 항상 같은 스프라이트
/// 규격으로 임포트한다. 캔버스를 거의 꽉 채워 그리는 아트 스타일 기준(Betang_1 역산)으로
/// Pixels Per Unit 687, 바닥 중앙 피벗을 적용해 기존 로스터와 시각적 크기를 맞춘다.
/// </summary>
public class CharacterDesignSpriteImporter : AssetPostprocessor
{
    private const string TargetFolder = "Assets/Imports/GGD_ArtWork/LHH_Artwork/Drone_Deck/Character_Designs/";
    private const float PixelsPerUnit = 687f;

    private void OnPreprocessTexture()
    {
        if (!assetPath.Replace('\\', '/').StartsWith(TargetFolder)) return;

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = PixelsPerUnit;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
        settings.spritePivot = new Vector2(0.5f, 0f);
        importer.SetTextureSettings(settings);
    }
}
